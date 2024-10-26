using DocumentFormat.OpenXml.Office2010.Excel;
using SendReceiveClasses;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using System.Windows;
using System.Windows.Automation.Text;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xaml;
using static BridgeOpsClient.PageConferenceView;
using static System.Windows.Forms.AxHost;

namespace BridgeOpsClient
{
    public partial class PageConferenceView : Page
    {
        DispatcherTimer tmrRender = new(DispatcherPriority.Render);

        public class ResourceInfo
        {
            public int id;
            public string name;
            public int connectionCapacity;
            public int conferenceCapacity;
            public int rowsAdditional;
            public int rowsTotal;

            // Unused in the list of resources, but use to pass the selected row to the New Conference.
            private int selectedRow = 0;
            private int selectedRowTotal = 0;
            public int SelectedRow { get { return selectedRow; } }
            public int SelectedRowTotal { get { return selectedRowTotal; } }

            public List<OverflowPoint> capacityChangePoints = new();
            public List<DateTime> overflowWarningAlternations = new();

            public ResourceInfo(int id, string name,
                                int connectionCapacity, int conferenceCapacity, int rowsAdditional)
            {
                this.id = id;
                this.name = name;
                this.connectionCapacity = connectionCapacity;
                this.conferenceCapacity = conferenceCapacity;
                this.rowsAdditional = rowsAdditional;
                rowsTotal = conferenceCapacity + rowsAdditional;
                selectedRow = 0;
            }
            public void SetSelectedRow(int selected, int selectedTotal)
            {
                selectedRow = Math.Clamp(selected, 0, conferenceCapacity + rowsAdditional);
                selectedRowTotal = selectedTotal;
            }
        }
        // All resources are stored here, regardless of resources visibility (not yet implemented as of 19/12/2023).
        public static Dictionary<int, ResourceInfo> resources = new();
        public static List<string> resourceRowNames = new();
        // This is a list of resource IDs, allowing the user to customise the display.
        public List<int> resourcesOrder = new(); // Current set in App.PullResourceInformation(), should be custom.
        public bool updateScrollBar = false;
        public int totalRows = 0;
        public void SetResources()
        {
            lock (resourceRowNames)
            {
                lock (resourcesOrder)
                {
                    // Temp code until custom resource orders are configurable by the user.
                    resourcesOrder = resources.Select(r => r.Value.id).ToList();

                    resourceRowNames.Clear();
                    totalRows = 0;
                    foreach (int i in resourcesOrder)
                    {
                        if (!resources.ContainsKey(i))
                            continue;

                        ResourceInfo ri = resources[i];

                        for (int n = 1; n <= ri.rowsTotal; ++n)
                            resourceRowNames.Add(ri.name + " " + n);
                        totalRows += ri.rowsTotal;
                    }
                }
            }

            UpdateOverflowPoints();
            UpdateScrollBar();
            RedrawGrid();
            RedrawResources();
        }
        public ResourceInfo? GetResourceFromSelectedRow(int row)
        {
            int resourceStart = 0;
            if (row >= 0)
            {
                foreach (int i in resourcesOrder)
                {
                    if (!resources.ContainsKey(i))
                        continue;
                    if (row < resourceStart + resources[i].rowsTotal)
                    {
                        resources[i].SetSelectedRow(row - resourceStart, row);
                        return resources[i];
                    }
                    else
                        resourceStart += resources[i].rowsTotal;
                }
            }
            return null;
        }
        public int GetResourceRowInView(int resourceID, int resourceRow)
        {
            int rowsSoFar = 0;
            foreach (int i in resourcesOrder)
            {
                if (!resources.ContainsKey(i))
                    continue;
                if (resources[i].id == resourceID)
                    return rowsSoFar + resourceRow;
                else
                    rowsSoFar += resources[i].rowsTotal;
            }
            return 0;
        }

        public enum StatusContext { None, Overflow, Clash, Selection, Info }
        public StatusContext statusContext = StatusContext.None;
        public void SetStatus() { stkStatus.Children.Clear(); statusContext = StatusContext.None; }
        public void SetStatus(StatusContext context, string message, bool bold, bool alignLeft)
        { SetStatus(context, new List<string>() { message }, new() { bold }); }
        public void SetStatus(StatusContext context, List<string> messages, List<bool> bold)
        {
            stkStatus.Children.Clear();
            if (messages.Count != bold.Count)
                return;
            for (int i = 0; i < messages.Count; ++i)
            {
                Label label = new()
                {
                    Margin = new(10, 0, 0, 0),
                    Padding = new(0),
                    FontWeight = bold[i] ? FontWeights.Bold : FontWeights.Normal,
                };
                label.Content = messages[i];
                stkStatus.Children.Add(label);
            }
            statusContext = context;
        }

        // Used for creating the overflow warning graphics in Render().
        public static object overflowCalculationLock = new();
        public void UpdateOverflowPoints()
        {
            if (conferenceView == null)
                return;

            lock (overflowCalculationLock)
                lock (resources)
                {
                    foreach (ResourceInfo ri in resources.Values)
                    {
                        ri.capacityChangePoints.Clear();
                        ri.overflowWarningAlternations.Clear();
                    }

                    // First, calculate resource change points.

                    lock (conferenceListLock)
                    {
                        List<Conference> confsToCheck = conferences.Values.ToList();
                        confsToCheck.AddRange(dragConferenceGhosts);
                        foreach (Conference c in confsToCheck)
                        {
                            if (c.cancelled || !resources.ContainsKey(c.resourceID))
                                continue;

                            ResourceInfo ri = resources[c.resourceID];
                            ri.capacityChangePoints.Add(new(c.start, c.dialNos.Count, 1, c.resourceID));
                            ri.capacityChangePoints.Add(new(c.end, -c.dialNos.Count, -1, c.resourceID));
                        }
                    }

                    foreach (ResourceInfo ri in resources.Values)
                    {
                        ri.capacityChangePoints = ri.capacityChangePoints.OrderBy(e => e.point).ToList();

                        // Build up a running list of points at which the warning switches on and off.
                        int currentConnections = 0;
                        int currentConferences = 0;
                        bool on = false;
                        foreach (OverflowPoint point in ri.capacityChangePoints)
                        {
                            currentConnections += point.change;
                            currentConferences += point.confAdd;

                            if (!on && (currentConnections > ri.connectionCapacity ||
                                        currentConferences > ri.conferenceCapacity))
                            {
                                ri.overflowWarningAlternations.Add(point.point);
                                on = true;
                            }
                            else if (on && (currentConnections <= ri.connectionCapacity &&
                                            currentConferences <= ri.conferenceCapacity))
                            {
                                ri.overflowWarningAlternations.Add(point.point);
                                on = false;
                            }
                        }
                    }
                }
        }
        public struct OverflowPoint
        {
            public DateTime point;
            public int change;
            public int confAdd; // +1 if starting, -1 if ending.
            public int resourceID;
            public OverflowPoint(DateTime point, int change, int confAdd, int resourceID)
            {
                this.point = point;
                this.change = change;
                this.confAdd = confAdd;
                this.resourceID = resourceID;
            }
        }
        public static HashSet<int> clashIDs = new();
        public static List<DateTime> clashRegionAlternations = new();
        struct ClashPoint
        {
            public DateTime point;
            public int dif;
            public ClashPoint(DateTime point, int dif)
            { this.point = point; this.dif = dif; }
        }
        public void UpdateClashIDs()
        {
            lock (clashIDs)
                lock (conferenceListLock)
                {
                    clashIDs.Clear();
                    clashRegionAlternations.Clear();

                    List<ClashPoint> points = new();

                    HashSet<int> checkedIDs = new();

                    List<Conference> confsToCheck = conferences.Values.ToList();
                    confsToCheck.AddRange(dragConferenceGhosts);

                    foreach (Conference c in confsToCheck)
                    {
                        if (c.cancelled)
                            continue;
                        bool addedConf = clashIDs.Contains(c.id);
                        foreach (Conference compare in conferences.Values)
                        {
                            if (compare.cancelled)
                                continue;

                            bool addedCompare = clashIDs.Contains(compare.id);
                            // Skip if both have already been added or if they're both the same conference
                            if ((addedConf && addedCompare || c == compare) ||
                                // or if they don't overlap
                                (c.end <= compare.start || c.start >= compare.end))
                                continue;

                            foreach (string s in c.dialNos)
                                if (compare.dialNos.Contains(s))
                                {
                                    if (!addedConf)
                                    {
                                        points.Add(new(c.start, 1));
                                        points.Add(new(c.end, -1));
                                        addedConf = true;
                                        clashIDs.Add(c.id);
                                    }
                                    if (!addedCompare)
                                    {
                                        points.Add(new(compare.start, 1));
                                        points.Add(new(compare.end, -1));
                                        addedCompare = true;
                                        clashIDs.Add(compare.id);
                                    }
                                    break;
                                }

                            checkedIDs.Add(c.id);
                        }
                    }

                    int ongoing = 0;
                    points = points.OrderBy(p => p.point).ToList();
                    foreach (ClashPoint p in points)
                    {
                        ongoing += p.dif;
                        if (ongoing == 1 && p.dif == 1)
                            clashRegionAlternations.Add(p.point);
                        else if (ongoing == 0)
                            clashRegionAlternations.Add(p.point);
                    }
                }
        }

        float smoothZoomSpeed = .25f;

        public PageConferenceView()
        {
            InitializeComponent();

            mnuScheduleAdjustTime.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_CONFERENCES];
            mnuScheduleAdjustConnections.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_CONFERENCES];
            mnuScheduleSetHost.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_CONFERENCES];
            mnuScheduleUpdate.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_CONFERENCES];
            mnuScheduleCancel.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_CONFERENCES];
            mnuScheduleDelete.IsEnabled = App.sd.deletePermissions[Glo.PERMISSION_CONFERENCES];
            mnuScheduleAddToRecurrence.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_CONFERENCES];
            mnuScheduleRemoveFromRecurrence.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_CONFERENCES];
            mnuScheduleCreateRecurrence.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_CONFERENCES] &&
                                                    App.sd.createPermissions[Glo.PERMISSION_CONFERENCES];

            SetResources();

            tmrRender.Tick += TimerUpdate;
            tmrRender.Interval = new TimeSpan(10000);
            tmrRender.Start();

            schResources.view = schView;
            schRuler.view = schView;
            schRuler.res = schResources;

            schResources.conferenceView = this;
            schView.conferenceView = this;

            MainWindow.pageConferenceViews.Add(this);
        }

        public class Conference
        {
            public int id;
            public string title = "";
            public DateTime start;
            public DateTime end;
            public int resourceID;
            public int resourceRow;
            public int? recurrenceID;
            public string? recurrenceName;
            public bool cancelled;
            public string closure = "";
            public bool test = false;
            public HashSet<string> dialNos = new();
            public bool hasUnclosedConnection = false;

            public bool isGhost = false; // Used for deciding whether or not a conference is a copy representation.

            // Used only for dragging.
            public DateTime resizeOriginStart = new(); // The time the conference started at when dragging to resize.
            public DateTime resizeOriginEnd = new(); // The time the conference ended at when dragging to resize.
            public DateTime moveOriginStart = new(); // The time the conference started at when dragging to move.
            public int moveOriginResourceID = new(); // The resource the conference started when dragging to move.
            public int moveOriginResourceRow = new(); // ^^
            public int moveOriginTotalRow = 0;

            public Conference Ghost()
            {
                Conference c = Clone();
                c.isGhost = true;
                return c;
            }
            public Conference Clone()
            {
                Conference c = new();
                c.id = id;
                c.title = title;
                c.start = start;
                c.end = end;
                c.resourceID = resourceID;
                c.resourceRow = resourceRow;
                c.recurrenceID = recurrenceID;
                c.recurrenceName = recurrenceName;
                c.cancelled = cancelled;
                c.closure = closure;
                c.test = test;
                c.dialNos = dialNos.ToHashSet();
                c.hasUnclosedConnection = false;
                return c;
            }
        }

        public static object conferenceListLock = new();
        public static Dictionary<int, Conference> conferences = new();
        public static Dictionary<int, Conference> conferencesUpdate = new();

        object conferenceSearchLock = new();
        object conferenceSearchThreadLock = new();
        bool searchTimeframeThreadQueued = false;
        DateTime searchStart;
        DateTime searchEnd;
        public void SearchTimeframe() { SearchTimeframe(searchStart, searchEnd); }
        public void SearchTimeframe(DateTime start, DateTime end)
        {
            // We never need more than one search queued, so cancel if one is already pending after the current thread.
            // Occasionally one might sneak through, but it's not the end of the world.
            // Also, if the user is currently dragging a conference around, don't pull the rug out from under them
            // whenever they drag past the search threshold.
            if (searchTimeframeThreadQueued || drag == Drag.Resize || drag == Drag.Move)
                return;
            searchTimeframeThreadQueued = true;

            lock (conferenceSearchLock)
            {
                searchStart = start;
                searchEnd = end;
                Thread searchThread = new(SearchTimeFrameThread);
                searchThread.Start();
            }
        }
        private void SearchTimeFrameThread()
        {
            lock (conferenceSearchThreadLock)
                lock (App.streamLock)
                    App.SendConferenceViewSearchRequest(searchStart, searchEnd, conferencesUpdate);

            // Clone the updated dictionary into the dictionary list.
            lock (conferenceListLock)
            {
                conferences = conferencesUpdate.ToDictionary(e => e.Key, e => e.Value);

                List<Conference> selectedConferences = new();
                selectedConferences.AddRange(schView.selectedConferences);
                lock (conferenceListLock)
                {
                    foreach (Conference c in boxSelectConferences)
                    {
                        if (schView.selectedConferences.Contains(c))
                            selectedConferences.Add(c);
                    }
                }

                bool dragging = drag == Drag.Move || drag == Drag.Resize;

                void CarryOverSelection(List<Conference> carryOver)
                {
                    // Don't update selection as the user may be actively doing something with them. If anything
                    // doesn't work in the end, an error will display and the change will be cancelled anyway.
                    List<Conference> conferencesToRemoveFromSelection = new();
                    List<Conference> conferencesToAddToSelection = new();

                    foreach (Conference c in carryOver)
                    {
                        if (conferences.ContainsKey(c.id))
                        {
                            // Copy any meaningful data over, then update the reference in the dictionary.
                            Conference dc = conferences[c.id];
                            if (dragging)
                            {
                                // Only retain this information if the user is current dragging to prevent
                                // conferences jumping around.
                                dc.start = c.start;
                                dc.end = c.end;
                                dc.resourceID = c.resourceID;
                                dc.resourceRow = c.resourceRow;
                                dc.moveOriginResourceID = c.moveOriginResourceID;
                                dc.moveOriginResourceRow = c.moveOriginResourceRow;
                                dc.moveOriginStart = c.moveOriginStart;
                                dc.resizeOriginStart = c.resizeOriginStart;
                                dc.resizeOriginEnd = c.resizeOriginEnd;
                            }
                            conferencesToAddToSelection.Add(dc);
                            conferencesToRemoveFromSelection.Add(c);
                        }
                        else
                        {
                            // It may be that the conference exists in more than one selection list, so check first.
                            if (!conferences.ContainsKey(c.id))
                                conferences.Add(c.id, c);
                        }
                    }
                    foreach (Conference c in conferencesToRemoveFromSelection)
                        carryOver.Remove(c);
                    foreach (Conference c in conferencesToAddToSelection)
                        carryOver.Add(c);
                }

                CarryOverSelection(boxSelectConferences);
                CarryOverSelection(schView.selectedConferences);
            }

            UpdateOverflowPoints();
            UpdateClashIDs();
            queueGridRedrawFromOtherThread = true;
            searchTimeframeThreadQueued = false;
        }

        private bool invalidatedRulerThisFrame = false;
        private bool invalidatedResourcesThisFrame = false;
        private bool invalidatedGridThisFrame = false;
        void RedrawRuler()
        {
            if (!invalidatedRulerThisFrame)
            {
                schRuler.InvalidateVisual();
                invalidatedRulerThisFrame = true;
            }
        }
        void RedrawResources()
        {
            if (!invalidatedResourcesThisFrame)
            {
                schResources.InvalidateVisual();
                invalidatedRulerThisFrame = true;
            }
        }
        public void RedrawGrid()
        {
            if (!invalidatedGridThisFrame)
            {
                schView.InvalidateVisual();
                invalidatedRulerThisFrame = true;
            }
        }

        //   E V E N T   H A N D L E R S

        private void btnTimeZoomOut_Click(object sender, RoutedEventArgs e)
        {
            schView.ZoomTime(-2);
        }

        private void btnTimeZoomIn_Click(object sender, RoutedEventArgs e)
        {
            schView.ZoomTime(2);
        }

        private void btnResourceZoomOut_Click(object sender, RoutedEventArgs e)
        {
            schView.ZoomResource(-1);
        }

        private void btnResourceZoomIn_Click(object sender, RoutedEventArgs e)
        {
            schView.ZoomResource(1);
        }

        void WindowResized(object sender, SizeChangedEventArgs e)
        {
            RedrawGrid();
        }

        static bool queueGridRedrawFromOtherThread = false;

        long lastFrame = 0;
        DateTime lastUpdate = DateTime.Now;
        long totalFrames = 0;
        void TimerUpdate(object? sender, EventArgs e)
        {
            ++totalFrames;

            if (updateScrollBar)
            {
                UpdateScrollBar();
                updateScrollBar = false;
            }

            // Smooth zoom (prefer 60Hz).
            float deltaTime = (float)((Environment.TickCount64 - lastFrame) / 16.6666f);

            // Horizotal Change will affect only the ruler, vertical change will affect only the resource pane, and
            // the schedule view will be affected by both.
            bool horizontalChange = false;
            bool verticalChange = false;

            // Update horizontally every second or two to account for the stylus moving.
            if ((DateTime.Now - lastUpdate).Seconds > 1)
            {
                horizontalChange = true;
                lastUpdate = DateTime.Now;
            }

            if (schView.zoomTimeCurrent != schView.zoomTime)
            {
                if (schView.smoothZoom)
                {
                    MathHelper.Lerp(ref schView.zoomTimeCurrent, schView.zoomTime,
                                                             smoothZoomSpeed * deltaTime, .2d);

                    schView.scheduleTime = new DateTime((long)(MathHelper.Lerp((double)schView.scheduleTime.Ticks,
                                                                               schView.zoomTimeTarget.Ticks,
                                                                               smoothZoomSpeed * deltaTime)));
                }
                else
                {
                    schView.zoomTimeCurrent = schView.zoomTime;
                    schView.scheduleTime = schView.zoomTimeTarget;
                }

                horizontalChange = true;
            }
            if (!verticalChange && schView.zoomResourceCurrent != schView.zoomResource)
            {
                double old = schView.zoomResourceCurrent;
                if (schView.smoothZoom)
                {
                    MathHelper.Lerp(ref schView.zoomResourceCurrent, schView.zoomResource,
                                                                     smoothZoomSpeed * deltaTime, .2d);
                    //MathHelper.Lerp(ref schView.scrollResource, schView.zoomResourceScrollTarget,
                    //                                            smoothZoomSpeed * deltaTime);
                }
                else
                {
                    schView.zoomResourceCurrent = schView.zoomResource;
                }

                // Nudge the scroll down a certain percentage of the screen so that the zoom tracks with the relative
                // position of the mouse cursor, not the start. The * .2d and * 1.4 are to widen the window in which
                // this takes place in order to feel slightly more natural. i.e. if you want to zoom in on the top row,
                // you want it to move slightly closer to the centre.
                schView.scrollResource += -(schView.ActualHeight * .2d) +
                                           (schView.ActualHeight * (schView.zoomResourceScrollCentre
                                                                 / schView.ActualHeight) * 1.4d);
                schView.scrollResource += (schView.scrollResource * (schView.zoomResourceCurrent / old))
                                          - schView.scrollResource;
                schView.scrollResource -= -(schView.ActualHeight * .2d) +
                                           (schView.ActualHeight * (schView.zoomResourceScrollCentre
                                                                 / schView.ActualHeight) * 1.4d);
                schView.EnforceResourceScrollLimits();

                UpdateScrollBar();

                verticalChange = true;
            }

            if (!(verticalChange && horizontalChange) && schView.scheduleTime.Ticks != schView.lastScheduleTime.Ticks)
            {
                verticalChange = true;
                horizontalChange = true;
            }

            if (!verticalChange && schView.scrollResource != schView.lastScrollResource)
            {
                schView.lastScrollResource = schView.scrollResource;
                verticalChange = true;
                // Without a horizontal update here, it sometimes doesn't update when scrolling vertically and
                // horizontally at the same time. I can't figure out why, and to be honest it kind of makes having the
                // ruler separate from the schedule view often a bit pointless, but it's too much to change now on time
                // constraints.
                horizontalChange = true;
            }

            // Smoothly scroll around horizontally at the edges if the user is dragging a conference to move, resize
            // or box select.
            if (dragMouseHasMoved && (drag == Drag.Resize || drag == Drag.Move || drag == Drag.BoxSelect))
            {
                double pos = Mouse.GetPosition(schView).X;
                double difference;
                if (pos < 100)
                    difference = (pos - 100);
                else if (pos > schView.ActualWidth - 100)
                    difference = (pos - schView.ActualWidth) + 100;
                else
                    difference = 0;

                difference = Math.Clamp(difference, -100, 100);

                // Scroll an amount that's relavent to the current zoom.
                difference *= 1d / schView.DisplayTimeZoom();

                // 4_000_000_000 feels comfortable.
                schView.scheduleTime = new DateTime(schView.scheduleTime.Ticks + (long)(difference * 4_000_000_000));
                horizontalChange = true;

                dragPositionChanged = true;
            }
            // Same vertically if dragging to move or box select.
            if (dragMouseHasMoved && (drag == Drag.Move || drag == Drag.BoxSelect))
            {
                double pos = Mouse.GetPosition(schView).Y;
                double difference;
                if (pos < 100)
                    difference = (pos - 100);
                else if (pos > schView.ActualHeight - 100)
                    difference = (pos - schView.ActualHeight) + 100;
                else
                    difference = 0;

                difference = Math.Clamp(difference, -100, 100);

                // Scroll an amount that's relavent to the current zoom.
                difference *= 1d / schView.zoomResourceCurrent;

                // .14d feels comfortable.
                schView.ScrollResource(difference * .14d);
                UpdateScrollBar();
                verticalChange = true;

                dragPositionChanged = true;
            }

            if (dragPositionChanged)
            {
                UpdateDragResizeOrMove();
                dragPositionChanged = false;
            }

            if (!horizontalChange && cursorMoved)
            {
                cursorMoved = false;
                horizontalChange = true;
            }

            if (verticalChange) RedrawResources();
            if (horizontalChange) RedrawRuler();
            if (verticalChange || horizontalChange || queueGridRedrawFromOtherThread)
            {
                queueGridRedrawFromOtherThread = false;
                RedrawGrid();
            }
            invalidatedRulerThisFrame = false;
            invalidatedResourcesThisFrame = false;
            invalidatedGridThisFrame = false;

            RedrawRuler(); // This should not need to be here, but I can't figure out just now why horiztonalChange
                           // isn't being set to true when dragging to scroll.

            lastFrame = Environment.TickCount64;
            schView.lastScheduleTime = schView.scheduleTime;
        }

        public bool resizeStart = false; // False if extending the end of the conference.
        public DateTime moveOriginCursor = new(); // The time the cursor started at when dragging to move.
        public static DateTime boxSelectFromX = new();
        public static DateTime boxSelectToX = new();
        public static int boxSelectFromY = new();
        public static int boxSelectToY = new();
        // Stored separately as the user may alternated between holding shift and not.
        public static List<Conference> boxSelectConferences = new();
        public enum Drag { None, Scroll, Resize, Move, BoxSelect }
        public Drag drag = Drag.None; // Switched on in MouseDown() inside the grid, and off in MouseUp() anywhere.
        bool conferenceSelectionAffected = false; // Used by MouseUp to determine whether or not to clear selection.
        private void schView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            schView.Focus();

            double mouseX = e.GetPosition(schView).X;
            double mouseY = e.GetPosition(schView).Y;

            // Double Click
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2 &&
                App.sd.createPermissions[Glo.PERMISSION_CONFERENCES])
            {
                schView.selectedConferences.Clear();
                DateTime time = schView.SnapDateTime(schView.GetDateTimeFromX(mouseX));
                int resource = schView.GetRowFromY(mouseY, true);
                if (schView.currentConference != null)
                    App.EditConference(schView.currentConference.id);
                else if (resource != -1)
                {
                    NewConference newConf = new(GetResourceFromSelectedRow(resource), time);
                    try { newConf.Show(); } catch { }
                }
            }

            // Drag
            else
            {
                if (e.ChangedButton == MouseButton.Middle)
                {
                    drag = Drag.Scroll;
                    ((IInputElement)sender).CaptureMouse();
                }
                else if (e.ChangedButton == MouseButton.Right)
                {
                    if (schView.currentConference != null &&
                        !schView.selectedConferences.Contains(schView.currentConference))
                        schView.selectedConferences = new() { schView.currentConference };
                }
                else if (e.ChangedButton == MouseButton.Left)
                {
                    // Start with Drag.Scroll and adjust if needed.
                    drag = Drag.Scroll;

                    // Box select.
                    if (schView.currentConference == null)
                    {
                        drag = Drag.BoxSelect;
                        boxSelectFromX = schView.GetDateTimeFromX(mouseX);
                        boxSelectToX = boxSelectFromX;
                        boxSelectFromY = schView.GetRowFromY(mouseY, true);
                        boxSelectToY = boxSelectFromY;
                    }

                    // Select conferences
                    else if (schView.currentConference != null)
                    {
                        if (!ShiftDown() && !SelectedConferences.Contains(schView.currentConference))
                        {
                            schView.selectedConferences = new() { schView.currentConference };
                            conferenceSelectionAffected = true;
                        }
                        else if (!schView.selectedConferences.Contains(schView.currentConference))
                        {
                            schView.selectedConferences.Add(schView.currentConference);
                            conferenceSelectionAffected = true;
                        }

                        var dragConfs = SelectedConferences;
                        if (schView.Cursor == Cursors.SizeWE && App.sd.editPermissions[Glo.PERMISSION_CONFERENCES])
                        {
                            drag = Drag.Resize;
                            currentAtStartOfDrag = schView.currentConference;
                            foreach (Conference c in dragConfs)
                            {
                                c.resizeOriginStart = c.start;
                                c.resizeOriginEnd = c.end;
                            }
                        }
                        else if (schView.currentConference != null)
                        {
                            drag = Drag.Move;

                            bool hasPermissions = false;
                            if (AltDown() && App.sd.createPermissions[Glo.PERMISSION_CONFERENCES])
                            {
                                CreateGhosts();
                                dragConfs = dragConferenceGhosts;
                                hasPermissions = true;
                            }
                            else if (App.sd.editPermissions[Glo.PERMISSION_CONFERENCES])
                            {
                                currentAtStartOfDrag = schView.currentConference;
                                hasPermissions = true;
                            }

                            if (hasPermissions) // Same for either duplicate of move.
                            {
                                moveOriginCursor = schView.GetDateTimeFromX(mouseX);
                                foreach (Conference c in dragConfs)
                                {
                                    c.moveOriginStart = c.start;
                                    c.moveOriginResourceID = c.resourceID;
                                    c.moveOriginResourceRow = c.resourceRow;
                                    c.moveOriginTotalRow = GetResourceRowInView(c.resourceID, c.resourceRow);
                                }
                            }
                        }
                    }

                    ((IInputElement)sender).CaptureMouse();
                }
            }
        }

        private void schView_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                CancelAllDrags(schView);
                // This should run on mouse down, but if right clicking out of a context menu into another, that
                // doesn't trigger for some reason.
                if (schView.currentConference != null &&
                    !schView.selectedConferences.Contains(schView.currentConference))
                    schView.selectedConferences = new() { schView.currentConference };
            }
            else if (drag != Drag.None &&
                (e.ChangedButton == MouseButton.Middle ||
                 e.ChangedButton == MouseButton.Left))
            {
                // Carry this section out first, storing drag in this bool, otherwise an error message could display
                // and wreak havoc with autoscroll while the user has no control over it.
                bool wasDraggingConference = drag == Drag.Move || drag == Drag.Resize;
                bool wasDraggingResize = drag == Drag.Resize;
                bool wasDraggingMove = drag == Drag.Move;
                bool wasDraggingBoxSelect = drag == Drag.BoxSelect;
                bool dragMouseHADmoved = dragMouseHasMoved;

                drag = Drag.None;
                dragMouseHasMoved = false;
                ((IInputElement)sender).ReleaseMouseCapture();
                schView.EnforceResourceScrollLimits();
                if (e.GetPosition(schView).X >= 0 && e.GetPosition(schView).Y >= 0)
                {
                    schView.SetCursor((int)e.GetPosition(schView).X, (int)e.GetPosition(schView).Y);
                    cursorMoved = true;
                }

                // Send off any conference updates needed if the user was making edits.
                if (wasDraggingConference && dragMouseHADmoved && schView.currentConference != null)
                {
                    List<int> conferenceIDs = new();
                    List<string> conferenceNames = new();
                    List<DateTime> starts = new();
                    List<DateTime> ends = new();
                    List<int> resourceIDs = new();
                    List<int> resourceRows = new();
                    List<Conference> confs;
                    if (draggingGhosts)
                        confs = dragConferenceGhosts;
                    else
                        confs = SelectedConferences;

                    foreach (Conference c in confs)
                    {
                        if ((wasDraggingResize && (c.start != c.resizeOriginStart ||
                                                   c.end != c.resizeOriginEnd)) ||
                            (wasDraggingMove && (c.start != c.moveOriginStart ||
                                                 c.resourceID != c.moveOriginResourceID ||
                                                 c.resourceRow != c.moveOriginResourceRow)))
                        {
                            conferenceIDs.Add(c.id);
                            conferenceNames.Add(c.title);
                            starts.Add(c.start);
                            ends.Add(c.end);
                            resourceIDs.Add(c.resourceID);
                            resourceRows.Add(c.resourceRow);
                        }
                    }

                    if (conferenceIDs.Count > 0)
                    {
                        if (!App.SendConferenceQuickMoveRequest(draggingGhosts, conferenceIDs, starts, ends,
                                                                resourceIDs, resourceRows, true, false, false))
                        {
                            if (wasDraggingResize)
                                foreach (Conference c in schView.selectedConferences)
                                {
                                    c.start = c.resizeOriginStart;
                                    c.end = c.resizeOriginEnd;
                                }
                            else if (wasDraggingMove && !draggingGhosts)
                            {
                                foreach (Conference c in schView.selectedConferences)
                                {
                                    TimeSpan length = new(c.end.Ticks - c.start.Ticks);
                                    c.start = c.moveOriginStart;
                                    c.end = c.moveOriginStart + length;
                                    c.resourceID = c.moveOriginResourceID;
                                    c.resourceRow = c.moveOriginResourceRow;
                                }
                            }
                        }
                        DropGhosts();
                        SearchTimeframe();
                        // schView.UpdateOverflowPoints(); will be called by SearchtimeFrame().
                    }
                }
                else if (schView.currentConference != null && !conferenceSelectionAffected && !dragMouseHADmoved)
                {
                    if (ShiftDown())
                        schView.selectedConferences.Remove(schView.currentConference);
                    else
                        schView.selectedConferences = new() { schView.currentConference };
                }
                else if (!dragMouseHADmoved && schView.currentConference == null)
                {
                    schView.selectedConferences.Clear();
                    boxSelectConferences.Clear();
                }
                else if (wasDraggingBoxSelect)
                {
                    lock (conferenceListLock)
                    {
                        if (boxSelectConferences.Count > 0)
                        {
                            if (ShiftDown())
                            {
                                foreach (Conference c in boxSelectConferences)
                                    if (!schView.selectedConferences.Contains(c))
                                        schView.selectedConferences.Add(c);
                            }
                            else
                                schView.selectedConferences = boxSelectConferences.ToList();
                        }
                    }
                    boxSelectConferences.Clear();
                }

                if (e.ChangedButton == MouseButton.Middle)
                    RedrawResources(); // Bring back the highlight.
            }

            conferenceSelectionAffected = false;
            currentAtStartOfDrag = null;
        }

        private List<Conference> SelectedConferences
        {
            get
            {
                lock (conferenceListLock)
                {
                    if (schView.selectedConferences.Count == 0)
                        return schView.currentConference == null ? new() : new() { schView.currentConference };
                    else
                        return schView.selectedConferences;
                }
            }
        }

        // These two methods can't be in MouseMove as they also need to be called when automatically drag-scrolling.
        // This means that MouseMove switches on dragMoved, and it's updated in the timer. dragMoved is also switched
        // on if the TimerUpdate caused the scroll to change while dragging.
        bool dragPositionChanged = false; // Switched on by MouseMove or when smooth scrolling.
        public bool dragMouseHasMoved = false; // Off until MouseMove, this is to prevent auto scrolling while double-clicking.
        Conference? currentAtStartOfDrag = null; // Make sure we retain the same primary selection while dragging.
        private void UpdateDragResizeOrMove()
        {
            double mouseX = Mouse.GetPosition(schView).X;
            double mouseY = Mouse.GetPosition(schView).Y;

            if (drag == Drag.Resize && dragMouseHasMoved)
            {
                if (currentAtStartOfDrag != null)
                {
                    DateTime time = schView.GetDateTimeFromX(mouseX);

                    // Snap to grid unless ctrl is pressed, then snap to 5 mins.
                    if (CtrlDown())
                        time = schView.SnapDateTime(time, schView.zoomTimeMaximum);
                    else
                        time = schView.SnapDateTime(time);

                    lock (conferenceListLock)
                    {
                        var dragConfs = SelectedConferences;

                        // First, decide whether to proceed with the resize in case of overlapping start and end time.
                        bool resizeLegal = true;

                        // Get the potential resize difference.
                        TimeSpan dif = new((resizeStart ? currentAtStartOfDrag.resizeOriginStart.Ticks :
                                                          currentAtStartOfDrag.resizeOriginEnd.Ticks) - time.Ticks);
                        dif = -dif;

                        foreach (Conference c in dragConfs)
                            if ((resizeStart && c.resizeOriginStart + dif >= c.end) ||
                                (!resizeStart && c.resizeOriginEnd + dif <= c.start))
                            {
                                resizeLegal = false;
                                break;
                            }

                        if (resizeLegal)
                            foreach (Conference c in dragConfs)
                                if (resizeStart)
                                    c.start = c.resizeOriginStart + dif;
                                else
                                    c.end = c.resizeOriginEnd + dif;
                    }
                }
            }
            else if ((drag == Drag.Move) && dragMouseHasMoved)
            {
                if (currentAtStartOfDrag != null)
                {
                    lock (conferenceListLock)
                    {
                        List<Conference> confs;
                        if (draggingGhosts)
                            confs = dragConferenceGhosts;
                        else
                            confs = SelectedConferences;

                        int resourceRow = schView.GetRowFromY(mouseY, true);
                        int resourceRowDif = resourceRow - currentAtStartOfDrag.moveOriginTotalRow;

                        // Make sure the row difference is legal and restrict if it isn't.
                        int correction = 0;
                        int worstBreach = 0;
                        foreach (Conference c in confs)
                        {
                            int rowInView = GetResourceRowInView(c.moveOriginResourceID, c.moveOriginResourceRow);
                            int potentialRow = rowInView + resourceRowDif;
                            if (potentialRow < 0 && potentialRow < worstBreach)
                            {
                                correction = -potentialRow;
                                worstBreach = potentialRow;
                            }
                            else if (potentialRow >= resourceRowNames.Count && potentialRow > worstBreach)
                            {
                                correction = (resourceRowNames.Count - 1) - potentialRow;
                                worstBreach = potentialRow;
                            }
                        }
                        resourceRowDif += correction;

                        // Record the desired time for the primarily selected conference, and the difference to move
                        // all the others by.
                        DateTime time = schView.GetDateTimeFromX(mouseX);
                        if (CtrlDown())
                            time = schView.SnapDateTime(time, schView.zoomTimeMaximum);
                        else
                            time = schView.SnapDateTime(time);

                        // This code is a bit confusing, but it makes it so only the primarily selected conference
                        // snaps and all other conferences follow it. Otherwise, each conference individually snaps,
                        // providing in much less control and utility.
                        TimeSpan cursorDifference = new(moveOriginCursor.Ticks - time.Ticks);
                        DateTime snappedDestination = currentAtStartOfDrag.moveOriginStart - cursorDifference;
                        // Snap to grid unless ctrl is pressed, then snap to 5 mins.
                        if (CtrlDown())
                            snappedDestination = schView.SnapDateTime(snappedDestination, schView.zoomTimeMaximum);
                        else
                            snappedDestination = schView.SnapDateTime(snappedDestination);
                        cursorDifference = new(currentAtStartOfDrag.moveOriginStart.Ticks - snappedDestination.Ticks);

                        foreach (Conference c in confs)
                        {
                            DateTime newConfStart = c.moveOriginStart - cursorDifference;

                            TimeSpan confLength = new(c.end.Ticks - c.start.Ticks);
                            c.start = newConfStart;
                            c.end = newConfStart + confLength;

                            // Apply the new resource.
                            int rowInView = GetResourceRowInView(c.moveOriginResourceID, c.moveOriginResourceRow);
                            ResourceInfo? ri = GetResourceFromSelectedRow(rowInView + resourceRowDif);
                            if (ri != null)
                            {
                                c.resourceID = ri.id;
                                c.resourceRow = ri.SelectedRow;
                            }
                        }
                    }
                }
            }
            else if (drag == Drag.BoxSelect && dragMouseHasMoved)
            {
                boxSelectToX = schView.GetDateTimeFromX(mouseX);
                boxSelectToY = schView.GetRowFromY(mouseY, true);

                lock (conferenceListLock)
                {
                    boxSelectConferences.Clear();
                    DateTime startX;
                    DateTime endX;
                    int startY;
                    int endY;
                    // Switch the starts and ends the right way round if needed.
                    if (boxSelectFromX > boxSelectToX)
                    {
                        startX = boxSelectToX;
                        endX = boxSelectFromX;
                    }
                    else
                    {
                        startX = boxSelectFromX;
                        endX = boxSelectToX;
                    }
                    if (boxSelectFromY > boxSelectToY)
                    {
                        startY = boxSelectToY;
                        endY = boxSelectFromY;
                    }
                    else
                    {
                        startY = boxSelectFromY;
                        endY = boxSelectToY;
                    }

                    foreach (Conference c in conferences.Values)
                    {
                        // Only proceed if the conference is actually in a visible resource.
                        if (!resourcesOrder.Contains(c.resourceID))
                            continue;

                        int row = schView.GetRowFromY(schView.GetYfromResource(c.resourceID, c.resourceRow), true);
                        if (row >= startY && row <= endY && endX > c.start && startX < c.end)
                            boxSelectConferences.Add(c);
                    }
                }
            }

            UpdateOverflowPoints();
            UpdateClashIDs();
        }

        double lastX = 0;
        double lastY = 0;
        bool cursorMoved = false;
        private void schView_MouseMove(object sender, MouseEventArgs e)
        {

            double newX = (int)e.GetPosition(this).X;
            double newY = (int)e.GetPosition(this).Y;
            if (drag == Drag.Scroll)
            {
                schView.Drag(lastX - newX, lastY - newY);
                UpdateScrollBar();
                schView.SetCursor(-1d, -1d);
                dragMouseHasMoved = true;
            }
            else if (drag == Drag.Resize)
            {
                dragPositionChanged = true;
                dragMouseHasMoved = true;
            }
            else if (drag == Drag.Move)
            {
                dragPositionChanged = true;
                dragMouseHasMoved = true;
            }
            else if (drag == Drag.BoxSelect)
            {
                dragPositionChanged = true;
                dragMouseHasMoved = true;
                if (!ShiftDown())
                    schView.selectedConferences.Clear();
            }
            else
            {
                schView.SetCursor((int)e.GetPosition(schView).X, (int)e.GetPosition(schView).Y);
                RedrawResources(); // Highlight needs updating.
            }
            cursorMoved = true;
            lastX = newX;
            lastY = newY;
        }

        private void UpdateScrollBar()
        {
            scrollBar.ViewportSize = schView.ViewPercent();
            scrollBar.Maximum = 1 - scrollBar.ViewportSize;
            scrollBar.Value = schView.ScrollPercent() * scrollBar.Maximum;
        }

        private void scrollBar_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateScrollBar();
        }

        private void scrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            double scrollPercent = scrollBar.Value / scrollBar.Maximum;
            schView.scrollResource = schView.ScrollMax() * scrollPercent;
        }

        private void datePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (datePicker.SelectedDate != null)
            {
                schView.scheduleTime = new(datePicker.SelectedDate.Value.Year,
                                           datePicker.SelectedDate.Value.Month,
                                           datePicker.SelectedDate.Value.Day,
                                           schView.scheduleTime.Hour,
                                           schView.scheduleTime.Minute,
                                           schView.scheduleTime.Second,
                                           schView.scheduleTime.Millisecond);
                RedrawRuler();
                RedrawGrid();
            }
        }

        private void schView_MouseLeave(object sender, MouseEventArgs e)
        {
            schView.SetCursor(-1, -1);
            RedrawGrid();
            RedrawResources();
        }

        private void btnDayPrevious_Click(object sender, RoutedEventArgs e)
        {
            schView.scheduleTime = schView.scheduleTime.AddDays(-1);
        }

        private void btnDayNext_Click(object sender, RoutedEventArgs e)
        {
            schView.scheduleTime = schView.scheduleTime.AddDays(1);
        }

        private void schView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            bool redrawGrid = false;
            bool redrawRuler = false;

            if (CtrlDown() || ShiftDown())
            {
                if (CtrlDown())
                {
                    schView.ZoomTime(e.Delta > 0 ? 1 : -1, e.GetPosition(schView).X);
                    redrawGrid = true;
                    redrawRuler = true;
                }
                if (ShiftDown())
                {
                    schView.ZoomResource(e.Delta > 0 ? 1 : -1, e.GetPosition(schView).Y);
                    redrawGrid = true;
                    redrawRuler = true;
                }

                if (redrawGrid) RedrawGrid();
                if (redrawRuler) RedrawRuler();
            }
            else
            {
                if (AltDown())
                    schView.scheduleTime = schView.scheduleTime.AddTicks(
                        (long)((66.66d * (e.Delta > 0 ? -ScheduleView.ticks1Hour : ScheduleView.ticks1Hour)) /
                        schView.zoomTimeCurrent));
                else
                {
                    schView.ScrollResource(e.Delta > 0 ? -.333d : .333d);
                    UpdateScrollBar();
                }
            }
        }

        private bool CtrlDown() { return Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl); }
        private bool AltDown() { return Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt); }
        private bool ShiftDown() { return Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift); }

        private void btnDayToday_Click(object sender, RoutedEventArgs e)
        {
            schView.scheduleTime = new DateTime(DateTime.Now.Year,
                                                DateTime.Now.Month,
                                                DateTime.Now.Day,
                                                12,
                                                30,
                                                0);
        }

        private void scrollBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateScrollBar();
        }

        public void CancelAllDrags(object? sender) // Also called by MainWindow when the window loses focus.
        {
            if (drag == Drag.Move)
            {
                if (schView.currentConference != null && !draggingGhosts)
                {
                    foreach (Conference c in SelectedConferences)
                    {
                        TimeSpan confLength = new(c.end.Ticks - c.start.Ticks);
                        c.start = c.moveOriginStart;
                        c.end = c.start + confLength;
                        c.resourceID = c.moveOriginResourceID;
                        c.resourceRow = c.moveOriginResourceRow;
                    }
                }
                dragMouseHasMoved = false;
                drag = Drag.None;
                // If I add separate panes, this will need updating to accommodate whatever form that takes.
                if (sender != null)
                    ((IInputElement)sender).ReleaseMouseCapture();

                DropGhosts();
                UpdateOverflowPoints();
                UpdateClashIDs();
            }
            else if (drag == Drag.Resize)
            {
                Conference? conf = schView.currentConference;
                if (conf != null)
                {
                    if (schView.selectedConferences.Count == 0)
                    {
                        conf.start = conf.resizeOriginStart;
                        conf.end = conf.resizeOriginEnd;
                    }
                    else
                    {
                        foreach (Conference c in schView.selectedConferences)
                        {
                            c.start = c.resizeOriginStart;
                            c.end = c.resizeOriginEnd;
                        }
                    }
                }
                dragMouseHasMoved = false;
                drag = Drag.None;

                UpdateOverflowPoints();
                UpdateClashIDs();

                if (sender != null)
                    // If I add separate panes, this will need updating to accommodate whatever form that takes.
                    ((IInputElement)sender).ReleaseMouseCapture();
            }
            else if (drag == Drag.BoxSelect)
            {
                boxSelectConferences.Clear();
                dragMouseHasMoved = false;
                drag = Drag.None;
            }
        }
        private void schView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CancelAllDrags(sender);
            }
            if (e.Key == Key.Delete)
            {
                mnuScheduleDelete_Click(null, null);
            }
            if (CtrlDown())
            {
                if (e.Key == Key.C)
                {
                    mnuScheduleCopy_Click(null, null);
                }
                if (e.Key == Key.V)
                {
                    mnuSchedulePaste_Click(null, null);
                }
            }
        }

        private void conferenceView_Loaded(object sender, RoutedEventArgs e)
        {
            RedrawResources();
        }

        private void mnuSchedule_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (schView.currentConference == null)
            {
                mnuScheduleRefresh.Visibility = Visibility.Visible;
                mnuScheduleSepOne.Visibility = Visibility.Visible;
                mnuScheduleCopy.Visibility = Visibility.Collapsed;
                mnuSchedulePaste.Visibility = Visibility.Visible;
                mnuScheduleSepTwo.Visibility = Visibility.Collapsed;
                mnuScheduleAdjustTime.Visibility = Visibility.Collapsed;
                mnuScheduleAdjustConnections.Visibility = Visibility.Collapsed;
                mnuScheduleSetHost.Visibility = Visibility.Collapsed;
                mnuScheduleUpdate.Visibility = Visibility.Collapsed;
                mnuScheduleCancel.Visibility = Visibility.Collapsed;
                mnuScheduleDelete.Visibility = Visibility.Collapsed;
                mnuScheduleSepThree.Visibility = Visibility.Collapsed;
                mnuScheduleAddToRecurrence.Visibility = Visibility.Collapsed;
                mnuScheduleRemoveFromRecurrence.Visibility = Visibility.Collapsed;
                mnuScheduleCreateRecurrence.Visibility = Visibility.Collapsed;
                mnuScheduleEditRecurrence.Visibility = Visibility.Collapsed;

                mnuSchedulePaste.IsEnabled = copiedConferences.Count > 0;

                return;
            }

            mnuScheduleRefresh.Visibility = Visibility.Visible;
            mnuScheduleSepOne.Visibility = Visibility.Visible;
            mnuScheduleCopy.Visibility = Visibility.Visible;
            mnuSchedulePaste.Visibility = Visibility.Collapsed;
            mnuScheduleSepTwo.Visibility = Visibility.Visible;
            mnuScheduleSetHost.Visibility = Visibility.Visible;
            mnuScheduleAdjustTime.Visibility = Visibility.Visible;
            mnuScheduleAdjustConnections.Visibility = Visibility.Visible;
            mnuScheduleUpdate.Visibility = Visibility.Visible;
            mnuScheduleCancel.Visibility = Visibility.Visible;
            mnuScheduleDelete.Visibility = Visibility.Visible;
            mnuScheduleSepThree.Visibility = Visibility.Visible;

            // Set button to "Uncancel" only if all selected conferences are cancelled.
            lock (conferenceListLock)
            {
                mnuScheduleCancel.Header = "Uncancel";
                List<Conference> toCheck = SelectedConferences;

                // Figure out cancel button text.
                foreach (Conference c in toCheck)
                {
                    if (!c.cancelled)
                    {
                        mnuScheduleCancel.Header = "Cancel";
                        break;
                    }
                }

                // Enable or disable the the recurring buttons.
                bool addToRec = true;
                bool removeFromRec = false;
                bool createRec = true;
                bool editRec = true;

                foreach (Conference c in toCheck)
                {
                    if (c.recurrenceID != null)
                    {
                        addToRec = false;
                        createRec = false;
                        removeFromRec = true;
                        break;
                    }
                }

                if (toCheck[0].recurrenceID == null)
                    editRec = false;
                else
                {
                    int? recurrenceID = toCheck[0].recurrenceID;
                    for (int i = 1; i < toCheck.Count; ++i)
                    {
                        if (toCheck[i].recurrenceID != recurrenceID)
                        {
                            editRec = false;
                            break;
                        }
                    }
                }

                mnuScheduleAddToRecurrence.Visibility = addToRec ? Visibility.Visible : Visibility.Collapsed;
                mnuScheduleRemoveFromRecurrence.Visibility = removeFromRec ? Visibility.Visible : Visibility.Collapsed;
                mnuScheduleCreateRecurrence.Visibility = createRec ? Visibility.Visible : Visibility.Collapsed;
                mnuScheduleEditRecurrence.Visibility = editRec ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void mnuScheduleUpdate_Click(object sender, RoutedEventArgs e)
        {
            List<string> ids = new();
            lock (conferenceListLock)
                foreach (Conference c in SelectedConferences)
                    ids.Add(c.id.ToString());
            UpdateMultiple um = new(7, "Conference", ColumnRecord.orderedConference, Glo.Tab.CONFERENCE_ID, ids, false);
            um.ShowDialog();
        }

        private void mnuScheduleAdjustTime_Click(object sender, RoutedEventArgs e)
        {
            List<string> ids = new();
            lock (conferenceListLock)
                foreach (Conference c in SelectedConferences)
                    ids.Add(c.id.ToString());
            DialogWindows.AdjustConferenceTimes adjust = new(ids);
            adjust.ShowDialog();
        }

        private void mnuScheduleAdjustConnections_Click(object sender, RoutedEventArgs e)
        {
            List<string> ids = new();
            lock (conferenceListLock)
                foreach (Conference c in SelectedConferences)
                    ids.Add(c.id.ToString());
            DialogWindows.AdjustConferenceConnections adjust = new(ids);
            adjust.ShowDialog();
        }

        private void mnuScheduleSetHost_Click(object sender, RoutedEventArgs e)
        {
            List<int> ids = new();
            lock (conferenceListLock)
                foreach (Conference c in SelectedConferences)
                    ids.Add(c.id);

            SelectResult res;
            if (App.SendConnectionSelectRequest(ids.Select(i => i.ToString()).ToList(), out res))
            {
                string dialNoFriendly = ColumnRecord.GetPrintName(Glo.Tab.DIAL_NO,
                                            (ColumnRecord.Column)ColumnRecord.organisation[Glo.Tab.DIAL_NO]!);
                string orgRefFriendly = ColumnRecord.GetPrintName(Glo.Tab.ORGANISATION_REF,
                                            (ColumnRecord.Column)ColumnRecord.organisation[Glo.Tab.ORGANISATION_REF]!);
                string orgNameFriendly = ColumnRecord.GetPrintName(Glo.Tab.ORGANISATION_NAME,
                                            (ColumnRecord.Column)ColumnRecord.organisation[Glo.Tab.ORGANISATION_NAME]!);
                res.columnNames = new() { dialNoFriendly, orgRefFriendly, orgNameFriendly, "Test", "Host", "Presence" };

                LinkRecord lr = new(res.columnNames, res.rows, 0);
                lr.ShowDialog();

                if (lr.id == null)
                    return;

                ConferenceAdjustment ca = new();
                ca.intent = ConferenceAdjustment.Intent.Host;
                ca.dialHost = lr.id;
                ca.ids = ids;

                // Error will display in the below function if it fails.
                App.SendConferenceAdjustment(ca);
            }
        }

        private void mnuScheduleDelete_Click(object? sender, RoutedEventArgs? e)
        {
            try
            {
                List<string> ids = new();
                lock (conferenceListLock)
                {
                    List<Conference> confs = SelectedConferences;
                    foreach (Conference c in SelectedConferences)
                        ids.Add(c.id.ToString());
                    if (ids.Count == 0 && e != null) // e will be null if called by KeyDown.
                        ids.Add(schView.lastCurrentConferenceID.ToString());
                }
                // Clear the selection, as this will cause the program to hold onto these references even though they
                // won't be returned next time frame search.
                if (ids.Count == 0)
                    return;
                schView.selectedConferences.Clear();
                if (App.DeleteConfirm(ids.Count > 1))
                    App.SendDelete("Conference", Glo.Tab.CONFERENCE_ID, ids, false);
            }
            catch { } // No catch required due to intended inactivity on a conference disappearing and error
                      // messages in App.Update().
        }

        private void mnuScheduleCancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool uncancel = mnuScheduleCancel.Header.ToString() == "Uncancel";

                List<string> ids = new();
                lock (conferenceListLock)
                {
                    List<Conference> confs = SelectedConferences;
                    foreach (Conference c in SelectedConferences)
                        ids.Add(c.id.ToString());
                    if (ids.Count == 0)
                        ids.Add(schView.lastCurrentConferenceID.ToString());
                }
                UpdateRequest req = new(App.sd.sessionID, ColumnRecord.columnRecordID, App.sd.loginID,
                                        "Conference", new() { Glo.Tab.CONFERENCE_CANCELLED },
                                        new() { uncancel ? "0" : "1" },
                                        new() { false }, Glo.Tab.CONFERENCE_ID, ids, false);
                App.SendUpdate(req);
            }
            catch { } // No catch required due to intended inactivity on a conference disappearing and error
                      // messages in App.Update().
        }

        private void mnuScheduleAddToRecurrence_Click(object sender, RoutedEventArgs e)
        {
            List<string> ids = new();
            lock (conferenceListLock)
            {
                List<Conference> confs = SelectedConferences;
                foreach (Conference c in SelectedConferences)
                    ids.Add(c.id.ToString());
                if (ids.Count == 0)
                    ids.Add(schView.lastCurrentConferenceID.ToString());
            }

            if (ids.Count == 0)
                return;

            LinkRecord lr = new("Recurrence", ColumnRecord.conferenceRecurrence);
            schView.selectedConferences.Clear();
            lr.ShowDialog();
            if (lr.id == "") // Error will display in LinkRecord if it couldn't get the ID.
            {
                App.DisplayError("ID could not be ascertained from the record.");
                return;
            }

            UpdateRequest req = new(App.sd.sessionID, ColumnRecord.columnRecordID, App.sd.loginID, "Conference",
                                    new() { Glo.Tab.RECURRENCE_ID }, new() { lr.id }, new() { false },
                                    Glo.Tab.CONFERENCE_ID, ids, false);
            App.SendUpdate(req, true, true, true); // Override all warnings as we're not moving anything.
        }

        private void mnuScheduleRemoveFromRecurrence_Click(object sender, RoutedEventArgs e)
        {
            List<string> ids = new();
            lock (conferenceListLock)
            {
                List<Conference> confs = SelectedConferences;
                foreach (Conference c in SelectedConferences)
                    ids.Add(c.id.ToString());
                if (ids.Count == 0)
                    ids.Add(schView.lastCurrentConferenceID.ToString());
            }

            if (ids.Count == 0)
                return;

            UpdateRequest req = new(App.sd.sessionID, ColumnRecord.columnRecordID, App.sd.loginID, "Conference",
                                    new() { Glo.Tab.RECURRENCE_ID }, new() { null }, new() { false },
                                    Glo.Tab.CONFERENCE_ID, ids, false);
            App.SendUpdate(req, true, true, true); // Override all warnings as we're not moving anything.
        }

        private void mnuScheduleEditRecurrence_Click(object sender, RoutedEventArgs e)
        {
            int? id;

            lock (conferenceListLock)
            {
                // All selected conferences should have the same ID, or the button would have been disabled.
                List<Conference> confs = SelectedConferences;
                if (confs.Count == 0)
                    confs.Add(conferences[schView.lastCurrentConferenceID]);
                id = confs[0].recurrenceID;
            }

            if (id == null) // This really should never be the case.
                return;

            EditRecurrence editRec = new((int)id);
            schView.selectedConferences.Clear();
            editRec.Show();
        }

        private void mnuScheduleCreateRecurrence_Click(object sender, RoutedEventArgs e)
        {
            List<string> ids = new();
            lock (conferenceListLock)
            {
                List<Conference> confs = SelectedConferences;
                foreach (Conference c in SelectedConferences)
                    ids.Add(c.id.ToString());
                if (ids.Count == 0)
                    ids.Add(schView.lastCurrentConferenceID.ToString());
            }

            if (ids.Count == 0)
                return;

            NewRecurrence newRec = new();
            newRec.ShowDialog();
            if (newRec.DialogResult == true && newRec.returnID != "")
            {
                UpdateRequest req = new(App.sd.sessionID, ColumnRecord.columnRecordID, App.sd.loginID, "Conference",
                                        new() { Glo.Tab.RECURRENCE_ID }, new() { newRec.returnID }, new() { false },
                                        Glo.Tab.CONFERENCE_ID, ids, false);
                App.SendUpdate(req, true, true, true); // Override all warnings as we're not moving anything.
            }
        }

        public static List<SendReceiveClasses.Conference> copiedConferences = new();
        // For storing the position of each conference relative to the top-leftmost on the grid.
        struct RelativePos
        {
            public int row; public TimeSpan time;
            public RelativePos(int row, TimeSpan time) { this.row = row; this.time = time; }
        }
        Dictionary<int, RelativePos> relativePositions = new();
        private void mnuScheduleCopy_Click(object? sender, RoutedEventArgs? e)
        {
            copiedConferences.Clear();
            relativePositions.Clear();
            try
            {
                List<string> confIDs = new();
                DateTime earliest = DateTime.MaxValue;
                int topmost = int.MaxValue;

                // Store these so we don't have to calculate them twice.
                Dictionary<int, int> rowsInView = new();

                lock (conferenceListLock)
                {

                    List<Conference> selected = SelectedConferences;
                    if (selected.Count == 0)
                        selected.Add(conferences[schView.lastCurrentConferenceID]);
                    foreach (Conference c in selected)
                    {
                        confIDs.Add(c.id.ToString());
                        int row = schView.GetRowFromResource(c.resourceID, c.resourceRow);
                        rowsInView.Add(c.id, row);
                        if (row < topmost)
                            topmost = row;
                        if (c.start < earliest)
                            earliest = c.start;
                    }
                    foreach (Conference c in selected)
                    {
                        // Store the relative positions to the top-leftmost start time, used when pasting.
                        relativePositions.Add(c.id, new(rowsInView[c.id] - topmost, c.start - earliest));
                    }
                }
                if (App.SendConferenceSelectRequest(confIDs, out copiedConferences) && copiedConferences.Count == 0)
                {
                    relativePositions.Clear();
                    throw new();
                }
            }
            catch { App.DisplayError("Due to an unknown error, conference could not be copied."); }
        }

        private void mnuSchedulePaste_Click(object? sender, RoutedEventArgs? e)
        {
            DateTime startX = schView.GetDateTimeFromX(schView.lastCursor.X, schView.DisplayTimeZoom());
            startX = schView.SnapDateTime(startX, schView.DisplayTimeZoom());
            int startY = schView.GetRowFromY(schView.lastCursor.Y, true);

            List<SendReceiveClasses.Conference> conferencesToPaste = new();
            foreach (var c in copiedConferences)
            {
                SendReceiveClasses.Conference conf = c;
                TimeSpan length = conf.end!.Value - conf.start!.Value;
                conf.start = startX + relativePositions[conf.conferenceID!.Value].time;
                conf.end = conf.start + length;

                ResourceInfo? r = GetResourceFromSelectedRow(startY + relativePositions[conf.conferenceID!.Value].row);
                if (r == null)
                {
                    App.DisplayError("Copied conferences will not fit here as they would extend past the bottom row.");
                    return;
                }

                conf.resourceID = r.id;
                conf.resourceRow = r.SelectedRow;

                // Wipe the connection and disconnection times as it's unlikely that the user would want these copied.
                List<SendReceiveClasses.Conference.Connection> newConnections = new();
                foreach (var conn in c.connections)
                {
                    SendReceiveClasses.Conference.Connection altered = conn;
                    altered.connected = null;
                    altered.disconnected = null;
                    altered.conferenceID = null;
                    newConnections.Add(altered);
                }

                conf.connections = newConnections;

                conferencesToPaste.Add(conf);
            }

            App.SendInsert(Glo.CLIENT_NEW_CONFERENCE, conferencesToPaste);
        }

        private bool draggingGhosts = false;
        public static List<Conference> dragConferenceGhosts = new();
        private void CreateGhosts()
        {
            Conference? clickedConf = null;

            if (schView.currentConference != null)
                lock (conferenceListLock)
                {
                    List<Conference> confs = SelectedConferences;
                    dragConferenceGhosts.Clear();

                    foreach (Conference c in confs)
                    {
                        Conference copied = c.Ghost();
                        copied.moveOriginStart = copied.start;
                        copied.moveOriginResourceID = copied.resourceID;
                        copied.moveOriginResourceRow = copied.resourceRow;
                        copied.moveOriginTotalRow = GetResourceRowInView(copied.resourceID, copied.resourceRow);
                        dragConferenceGhosts.Add(copied);

                        if (c.id == schView.currentConference.id)
                            clickedConf = copied;
                    }
                }

            currentAtStartOfDrag = clickedConf;
            draggingGhosts = dragConferenceGhosts.Count > 0 && currentAtStartOfDrag != null;
        }
        private void DropGhosts()
        {
            lock (conferenceListLock)
            {
                dragConferenceGhosts.Clear();
                draggingGhosts = false;
            }
        }

        private void mnuRefresh_Click(object sender, RoutedEventArgs e)
        {
            SearchTimeframe();
        }

        private void schView_LostFocus(object sender, RoutedEventArgs e)
        {
            CancelAllDrags(this);
        }
    }
}
