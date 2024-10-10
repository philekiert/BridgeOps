using DocumentFormat.OpenXml.Drawing.Wordprocessing;
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

namespace BridgeOpsClient
{
    public partial class PageConferenceView : Page
    {
        DispatcherTimer tmrRender = new DispatcherTimer(DispatcherPriority.Render);

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
                selectedRowTotal = Math.Clamp(selectedTotal, 0, rowsTotal);
            }
        }
        // All resources are stored here, regardless of resources visibility (not yet implemented as of 19/12/2023).
        public static Dictionary<int, ResourceInfo> resources = new();
        public static List<string> resourceRowNames = new();
        // This is a list of resource IDs, allowing the user to customise the display.
        public List<int> resourcesOrder = new() { 1, 2 };
        public bool updateScrollBar = false;
        public int totalRows = 0;
        public void SetResources()
        {
            lock (resourceRowNames)
            {
                lock (resourcesOrder)
                {
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

        public enum StatusContext { None, Overflow }
        public StatusContext statusBarContext = StatusContext.None;
        public void SetStatusBar() { stkStatus.Children.Clear(); statusBarContext = StatusContext.None; }
        public void SetStatusBar(StatusContext context, string message, bool bold, bool alignLeft)
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
            statusBarContext = context;
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
                    lock (conferences)
                    {
                        foreach (var kvp in conferences)
                        {

                            Conference c = kvp.Value;
                            if (c.cancelled || !resources.ContainsKey(c.resourceID))
                                continue;

                            ResourceInfo ri = resources[c.resourceID];
                            ri.capacityChangePoints.Add(new(c.start, c.connectionCount, 1, c.resourceID));
                            ri.capacityChangePoints.Add(new(c.end, -c.connectionCount, -1, c.resourceID));
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

        float smoothZoomSpeed = .25f;

        public PageConferenceView()
        {
            InitializeComponent();

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
            public bool cancelled;
            public bool test;
            public int connectionCount;

            // Used only for dragging.
            public DateTime resizeOriginStart = new(); // The time the conference started at when dragging to resize.
            public DateTime resizeOriginEnd = new(); // The time the conference ended at when dragging to resize.
            public DateTime moveOriginStart = new(); // The time the conference started at when dragging to move.
            public int moveOriginResourceID = new(); // The resource the conference started when dragging to move.
            public int moveOriginResourceRow = new(); // ^^
            public int moveOriginTotalRow = 0;
        }
        public Dictionary<int, Conference> conferences = new();
        public Dictionary<int, Conference> conferencesUpdate = new();

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
                Thread searchThread = new Thread(SearchTimeFrameThread);
                searchThread.Start();
            }
        }
        private void SearchTimeFrameThread()
        {
            lock (conferenceSearchThreadLock)
                lock (App.streamLock)
                    App.SendConferenceViewSearchRequest(searchStart, searchEnd, conferencesUpdate);

            // Clone the updated dictionary into the dictionary list.
            lock (conferences)
            {
                conferences = conferencesUpdate.ToDictionary(e => e.Key, e => e.Value);
                // Don't update selection as the user may be actively doing something with them. If anything doesn't
                // work in the end, an error will display and the change will be cancelled anyway.
                List<Conference> conferencesToRemoveFromSelection = new();
                List<Conference> conferencesToAddToSelection = new();
                foreach (Conference c in schView.selectedConferences)
                {
                    if (conferences.ContainsKey(c.id))
                    {
                        // Copy any meaningful data over, then update the reference in the dictionary.
                        Conference dc = conferences[c.id];
                        if (drag == Drag.Move || drag == Drag.Resize)
                        {
                            // Only retain this information if the user is current dragging to prevent conferences
                            // jumping around.
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
                        conferences.Add(c.id, c);
                }
                foreach (Conference c in conferencesToRemoveFromSelection)
                    schView.selectedConferences.Remove(c);
                foreach (Conference c in conferencesToAddToSelection)
                    schView.selectedConferences.Add(c);
            }

            UpdateOverflowPoints();
            queueGridRedrawFromOtherThread = true;
            searchTimeframeThreadQueued = false;
        }

        void RedrawRuler()
        {
            schRuler.InvalidateVisual();
        }
        void RedrawResources()
        {
            schResources.InvalidateVisual();
        }
        public void RedrawGrid()
        {
            schView.InvalidateVisual();
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
        void TimerUpdate(object? sender, EventArgs e)
        {
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
                verticalChange = true;
                // Without a horizontal update here, it sometimes doesn't update when scrolling vertically and
                // horizontally at the same time. I can't figure out why, and to be honest it kind of makes having the
                // ruler separate from the schedule view often a bit pointless, but it's too much to change now on time
                // constraints.
                horizontalChange = true;
            }

            // Smoothly scroll around horizontally at the edges if the user is dragging a conference to move or resize.
            if (dragMouseHasMoved && (drag == Drag.Resize || drag == Drag.Move))
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
            // Same vertically if dragging to move.
            if (dragMouseHasMoved && drag == Drag.Move)
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

            lastFrame = Environment.TickCount64;
            schView.lastScheduleTime = schView.scheduleTime;
        }

        public bool resizeStart = false; // False if extending the end of the conference.
        public DateTime moveOriginCursor = new(); // The time the cursor started at when dragging to move.
        public enum Drag { None, Scroll, Resize, Move }
        public Drag drag = Drag.None; // Switched on in MouseDown() inside the grid, and off in MouseUp() anywhere.
        bool conferenceSelectionAffected = false; // Used by MouseUp to determine whether or not to clear selection.
        private void schView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            schView.Focus();

            // Double Click
            if (e.ClickCount == 2 && App.sd.createPermissions[Glo.PERMISSION_CONFERENCES])
            {
                schView.selectedConferences.Clear();
                DateTime time = schView.SnapDateTime(schView.GetDateTimeFromX(e.GetPosition(schView).X));
                int resource = schView.GetRowFromY(e.GetPosition(schView).Y, true);
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
                else if (e.ChangedButton == MouseButton.Left)
                {
                    // Start with Drag.Scroll and adjust if needed.
                    drag = Drag.Scroll;

                    // Select conferences
                    if (schView.currentConference != null)
                    {
                        if (!CtrlDown() && !DragConferences.Contains(schView.currentConference))
                        {
                            schView.selectedConferences = new() { schView.currentConference };
                            conferenceSelectionAffected = true;
                        }
                        else if (!schView.selectedConferences.Contains(schView.currentConference))
                        {
                            schView.selectedConferences.Add(schView.currentConference);
                            conferenceSelectionAffected = true;
                        }

                        if (App.sd.editPermissions[Glo.PERMISSION_CONFERENCES])
                        {
                            var dragConfs = DragConferences;
                            if (schView.Cursor == Cursors.SizeWE)
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
                                currentAtStartOfDrag = schView.currentConference;
                                moveOriginCursor = schView.GetDateTimeFromX(e.GetPosition(schView).X);
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
            if (drag != Drag.None &&
                (e.ChangedButton == MouseButton.Middle ||
                 e.ChangedButton == MouseButton.Left))
            {
                // Carry this section out first, storing drag in this bool, otherwise an error message could display
                // and wreak havoc with autoscroll while the user has no control over it.
                bool wasDraggingConference = drag == Drag.Move || drag == Drag.Resize;
                bool wasDraggingResize = drag == Drag.Resize;
                bool wasDraggingMove = drag == Drag.Move;
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
                    foreach (Conference c in DragConferences)
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
                        if (!App.SendConferenceQuickMoveRequest(conferenceIDs, conferenceNames, starts, ends,
                                                                resourceIDs, resourceRows, false, false))
                        {
                            if (wasDraggingResize)
                                foreach (Conference c in schView.selectedConferences)
                                {
                                    c.start = c.resizeOriginStart;
                                    c.end = c.resizeOriginEnd;
                                }
                            else if (wasDraggingMove)
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
                            //schView.selectedConferences.Clear();
                            SearchTimeframe();
                            // schView.UpdateOverflowPoints(); will be called by SearchtimeFrame().
                        }
                    }
                }
                else if (schView.currentConference != null && !conferenceSelectionAffected && !dragMouseHADmoved)
                {
                    if (CtrlDown())
                        schView.selectedConferences.Remove(schView.currentConference);
                    else
                        schView.selectedConferences = new() { schView.currentConference };
                }
                else if (!dragMouseHADmoved && schView.currentConference == null)
                    schView.selectedConferences.Clear();
            }

            conferenceSelectionAffected = false;
            currentAtStartOfDrag = null;
        }

        private List<Conference> DragConferences
        {
            get
            {
                if (schView.selectedConferences.Count == 0)
                    return schView.currentConference == null ? new() : new() { schView.currentConference };
                else
                    return schView.selectedConferences;
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
            if (drag == Drag.Resize && dragMouseHasMoved)
            {
                if (currentAtStartOfDrag != null)
                {
                    DateTime time = schView.GetDateTimeFromX(Mouse.GetPosition(schView).X);

                    // Snap to grid unless ctrl is pressed, then snap to 5 mins.
                    if (CtrlDown())
                        time = schView.SnapDateTime(time, schView.zoomTimeMaximum);
                    else
                        time = schView.SnapDateTime(time);

                    var dragConfs = DragConferences;

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
            else if (drag == Drag.Move && dragMouseHasMoved)
            {
                if (currentAtStartOfDrag != null)
                {
                    var dragConfs = DragConferences;

                    int resourceRow = schView.GetRowFromY(Mouse.GetPosition(schView).Y, true);
                    int resourceRowDif = resourceRow - currentAtStartOfDrag.moveOriginTotalRow;

                    // Make sure the row difference is legal and restrict if it isn't.
                    int correction = 0;
                    int worstBreach = 0;
                    foreach (Conference c in dragConfs)
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

                    // Record the desired time for the primarily selected conference, and the difference to move all
                    // the others by.
                    DateTime time = schView.GetDateTimeFromX(Mouse.GetPosition(schView).X);
                    TimeSpan cursorDifference = new(moveOriginCursor.Ticks - time.Ticks);

                    foreach (Conference c in dragConfs)
                    {
                        DateTime newConfStart = c.moveOriginStart - cursorDifference;

                        // Snap to grid unless ctrl is pressed, then snap to 5 mins.
                        if (CtrlDown())
                            newConfStart = schView.SnapDateTime(newConfStart, schView.zoomTimeMaximum);
                        else
                            newConfStart = schView.SnapDateTime(newConfStart);

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

            UpdateOverflowPoints();
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
            else
            {
                schView.SetCursor((int)e.GetPosition(schView).X, (int)e.GetPosition(schView).Y);
                cursorMoved = true;
            }
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

        private void schView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (drag == Drag.Move)
                {
                    if (schView.currentConference != null)
                    {
                        foreach (Conference c in DragConferences)
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
                    ((IInputElement)sender).ReleaseMouseCapture();
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
                    ((IInputElement)sender).ReleaseMouseCapture();
                }
            }
        }

        private void conferenceView_Loaded(object sender, RoutedEventArgs e)
        {
            RedrawResources();
        }
    }

    public class ScheduleRuler : Canvas
    {
        public ScheduleRuler() { ClipToBounds = true; }

        public ScheduleView? view;
        public ScheduleResources? res;

        Typeface segoeUI = new Typeface("Segoe UI");
        Typeface segoeUISemiBold = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold,
                                                FontStretches.Normal);

        // OnRender() has some repeated code from ScheduleView, but I have kept the draw code for the ruler separate
        // due to not wanting to redraw the ruler unless we have to. The function still makes use of some public
        // members from ScheduleView.
        protected override void OnRender(DrawingContext dc)
        {
            if (view != null && res != null)
            {
                double zoomTimeDisplay = view.DisplayTimeZoom();

                // Get number of intervals for half the width of the view.
                double viewHalfHours = (ActualWidth * .5d) / zoomTimeDisplay;
                double viewHalfMinutes = (viewHalfHours - (int)viewHalfHours) * 60;
                int viewHalfSeconds = (int)((viewHalfMinutes - (int)viewHalfMinutes) * 60);
                int viewHalfDays = (int)(viewHalfHours / 24);

                TimeSpan half = new TimeSpan(0, (int)viewHalfHours, (int)viewHalfMinutes, viewHalfSeconds);

                DateTime start = view.scheduleTime - half;
                // Overshoot so text doesn't cut disappear early when scrolling.
                DateTime end = view.scheduleTime + half.Add(new TimeSpan(1, 0, 0));

                double incrementX = zoomTimeDisplay;
                long incrementTicks = ScheduleView.ticks1Hour;
                if (zoomTimeDisplay > 120)
                {
                    incrementTicks = ScheduleView.ticks15Min;
                    incrementX /= 4d;
                }
                else if (zoomTimeDisplay > 60)
                {
                    incrementTicks = ScheduleView.ticks30Min;
                    incrementX /= 2d;
                }

                // Get the start time, rounded down to the nearest increment.
                DateTime t = start.AddTicks(-(start.Ticks % incrementTicks));

                // Cast this to (int) if you want it dead on the pixel.
                double x = view.TimeToX(t, zoomTimeDisplay) + .5f;

                // Irritating to have to do this, but I don't want to create a new formatted text every draw, and the
                // text property doesn't have a setter that I can see.
                FormattedText[] formattedText = new FormattedText[24];
                for (int i = 0; i < 24; ++i)
                    formattedText[i] = new(i < 10 ? "0" + i.ToString() : i.ToString(),
                                           CultureInfo.CurrentCulture,
                                           FlowDirection.LeftToRight,
                                           segoeUI,
                                           12,
                                           Brushes.Black,
                                           VisualTreeHelper.GetDpi(this).PixelsPerDip);
                FormattedText[] formattedTextLight = new FormattedText[24];
                for (int i = 0; i < 3; ++i)
                    formattedTextLight[i] = new(((i * 15) + 15).ToString(),
                                           CultureInfo.CurrentCulture,
                                           FlowDirection.LeftToRight,
                                           segoeUI,
                                           9,
                                           Brushes.LightSlateGray,
                                           VisualTreeHelper.GetDpi(this).PixelsPerDip);

                double hourWidth = formattedText[0].Width;
                double hourHeight = formattedTextLight[0].Height;
                double minuteWidth = formattedTextLight[0].Width;
                double minuteYmod = (formattedText[0].Height - hourHeight) - 1;

                double firstDateX = double.MaxValue;
                string firstDateStringOverride = "";

                // partialFirstDay is used for printing the partial day at the start if needed.
                DateTime partialFirstDay = (t - new TimeSpan(TimeSpan.TicksPerDay)).AddDays(1);
                int hourDisplay = 1;
                if (zoomTimeDisplay < 12)
                    hourDisplay = 4;
                else if (zoomTimeDisplay < 24)
                    hourDisplay = 2;
                while (t < end)
                {
                    double xInt = (int)x + .5d; // Snap to nearest pixel.
                    if (xInt > -(hourWidth * .5d))
                    {
                        // Hour markers
                        if (xInt < ActualWidth + hourWidth * .5d)
                        {
                            if (t.Hour % hourDisplay == 0 && t.Minute == 0)
                            {
                                dc.DrawText(formattedText[t.Hour],
                                            new Point(xInt - (hourWidth * .5d), 22d));
                            }
                            else if (t.Minute % 15 == 0 && hourDisplay == 1)
                            {
                                dc.DrawText(formattedTextLight[(t.Minute / 15 - 1)],
                                            new Point(xInt - (minuteWidth * .5d), 22d + minuteYmod));
                            }
                        }

                        // Day markers
                        if (t.Hour == 0 && t.Minute == 0 && xInt < ActualWidth - 1d)
                        {
                            if (xInt >= 2)
                            {
                                FormattedText date = new($"{t.Day}/{t.Month}/{t.Year} {t.DayOfWeek}",
                                           CultureInfo.CurrentCulture,
                                           FlowDirection.LeftToRight,
                                           segoeUISemiBold,
                                           12,
                                           Brushes.Black,
                                           VisualTreeHelper.GetDpi(this).PixelsPerDip);
                                dc.DrawText(date, new Point(xInt, 0));
                                if (xInt < firstDateX)
                                    firstDateX = xInt;
                            }
                            else
                                firstDateStringOverride = $"{t.Day}/{t.Month}/{t.Year} {t.DayOfWeek}";
                        }
                    }

                    t = t.AddTicks(incrementTicks);
                    x += incrementX;
                }

                FormattedText dateEdge = new(firstDateStringOverride == "" ?
                                                $"{partialFirstDay.Day}/" +
                                                $"{partialFirstDay.Month}/" +
                                                $"{partialFirstDay.Year} " +
                                                $"{partialFirstDay.DayOfWeek}" :
                                                firstDateStringOverride,
                                             CultureInfo.CurrentCulture,
                                             FlowDirection.LeftToRight,
                                             segoeUISemiBold,
                                             12,
                                             Brushes.Black,
                                             VisualTreeHelper.GetDpi(this).PixelsPerDip);
                if (firstDateX > dateEdge.Width + 20)
                    dc.DrawText(dateEdge, new Point(2, 0));

                // Mask overflowing hour markers on the left-hand side. I tried this with the Z index, but that caused
                // the resource names to overflow onto the schedule view. This way round it's slightly more manageable.
                double xMask = res.ActualWidth;
                dc.DrawRectangle(Brushes.White, null, new Rect(-xMask, 0, xMask, ActualHeight));

            }
        }
    }

    public class ScheduleResources : Canvas
    {
        public PageConferenceView? conferenceView;

        public ScheduleView? view;

        Typeface segoeUI = new Typeface("Segoe UI");

        Brush brsHighlight;
        Brush brsDivider;
        Pen penDivider;
        public ScheduleResources()
        {
            ClipToBounds = true;

            brsHighlight = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0));
            brsDivider = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0
                ));
            penDivider = new Pen(brsDivider, 1);

            penDivider.Freeze();
        }

        // OnRender() has some repeated code from ScheduleView, but I have kept the draw code for the resources
        // separate due to not wanting to redraw the ruler unless we have to. The function still makes use of some
        // public members from ScheduleView.
        protected override void OnRender(DrawingContext dc)
        {
            if (view != null && conferenceView != null)
            {
                // Background
                dc.DrawRectangle(Brushes.White,
                                 new Pen(Brushes.LightGray, 1),
                                 new Rect(-.5d, .5d, ActualWidth + 1, ActualHeight - 1d));

                // maxLineDepth is worked out slightly differently to ScheduleView. We add zoomResourceCurrent because
                // we may want to draw one additional row if the top and bottom rows are only partially visible, but
                // we don't wrap around as that would sometimes cause the incorrect text to be renderred at the top.
                double maxLineDepth = (view.MaxLineDepth(view.zoomResourceCurrent) + view.zoomResourceCurrent) - 1d;
                // Resources (drawn last as these lines want to overlay the time lines).
                double scroll = view.DisplayResourceScroll();
                for (double y = 0; y < maxLineDepth; y += view.zoomResourceCurrent)
                {
                    double yPix = (y + .5f) - (scroll % view.zoomResourceCurrent);
                    // Wrap around if the line falls off the top.
                    if (yPix < -(view.zoomResourceCurrent - 1))
                        yPix += ActualHeight + (view.zoomResourceCurrent - (ActualHeight % view.zoomResourceCurrent));
                    if (yPix >= 0)
                        dc.DrawLine(penDivider, new Point(.5f, yPix), new Point(ActualWidth, yPix));

                    int row = view.GetRowFromY(yPix, false);
                    PageConferenceView.ResourceInfo? resource = conferenceView.GetResourceFromSelectedRow(row);
                    if (resource != null)
                    {
                        FormattedText formattedText = new(resource.name + " " + (resource.SelectedRow + 1).ToString(),
                                                          CultureInfo.CurrentCulture,
                                                          FlowDirection.LeftToRight,
                                                          segoeUI,
                                                          12,
                                                          Brushes.Black,
                                                          VisualTreeHelper.GetDpi(this).PixelsPerDip);
                        if (yPix > -formattedText.Height)
                            dc.DrawText(formattedText, new Point(5, yPix + 2));

                        // If text went over the line at the top of the screen, redraw the grey border on top.
                        if (yPix + 2 < 0)
                            dc.DrawLine(new Pen(Brushes.LightGray, 1), new Point(.5d, .5d), new Point(ActualWidth, .5d));
                    }
                }

                // Highlight cursor resource.
                if (view.cursor != null)
                {
                    double y = view.GetRowFromY(view.cursor.Value.Y, true);

                    if (y >= 0)
                    {
                        y *= view.zoomResourceCurrent;
                        y -= scroll;

                        double height = view.zoomResourceCurrent;

                        if (y < 0)
                        {
                            height += y;
                            y = 0;
                        }

                        dc.DrawRectangle(brsHighlight, null, new Rect(0, y, ActualWidth, height));
                    }
                }
            }
        }
    }

    public class ScheduleView : Canvas
    {
        public PageConferenceView? conferenceView;

        Typeface segoeUI = new Typeface("Segoe UI");
        Typeface segoeUISemiBold = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold,
                                                FontStretches.Normal);

        public double gridHeight = 0;

        public bool smoothZoom = true;
        public int zoomTime = 70;
        public double zoomTimeCurrent = 70d; // Used for smooth Lerp()ing.
        public DateTime zoomTimeTarget = DateTime.Now;
        public int zoomTimeMinimum = 10; // How many pixels an hour can be reduced to.
        public int zoomTimeMaximum = 210;
        public int zoomTimeSensitivity = 10;
        public int zoomResource = 40;
        public double zoomResourceCurrent = 40d; // Used for smooth Lerp()ing.
        public double zoomResourceScrollCentre = 0;
        public int zoomResourceMinimum = 20; // How many pixels a resource can be reduced to.
        public int zoomResourceMaximum = 200;
        public int zoomResourceSensitivity = 20;

        float minShade = .03f;

        public const long ticks5Min = 3_000_000_000;
        public const long ticks15Min = 9_000_000_000;
        public const long ticks30Min = 18_000_000_000;
        public const long ticks1Hour = 36_000_000_000;
        public const long ticks1Day = 864_000_000_000;

        public DateTime lastScheduleTime = new();
        public DateTime scheduleTime = new();
        public double lastScrollResource = 0f;
        public double scrollResource = 0f;

        public Point? cursor = null;

        // These brushes remain the same forever, so declare, initialise and freeze.
        Brush brsCursor;
        Pen penCursor;
        Brush brsStylus;
        LinearGradientBrush brsStylusFade;
        Brush brsConference;
        Brush brsConferenceBorder;
        Brush brsConferenceSolid;
        Brush brsConferenceCancelled;
        Brush brsConferenceCancelledBorder;
        Brush brsConferenceCancelledSolid;
        Brush brsConferenceTest;
        Brush brsConferenceTestBorder;
        Brush brsConferenceTestSolid;
        Brush brsConferenceEnded;
        Brush brsConferenceEndedBorder;
        Brush brsConferenceEndedSolid;
        Brush brsOverflow;
        Brush brsOverflowCheck;
        Pen penConferenceBorder;
        Pen penConferenceCancelledBorder;
        Pen penConferenceTestBorder;
        Pen penConferenceEndedBorder;
        Pen penStylus;
        Pen penStylusFade;
        public ScheduleView()
        {
            ClipToBounds = true;

            brsCursor = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));
            penCursor = new Pen(brsCursor, 1.4);
            brsStylus = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0));
            brsStylusFade = new LinearGradientBrush(Color.FromArgb(100, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 90);
            Color clrConference = (Color)Application.Current.Resources.MergedDictionaries[0]["colorConference"];
            Color clrCancelled = (Color)Application.Current.Resources.MergedDictionaries[0]["colorConferenceCancelled"];
            Color clrTest = (Color)Application.Current.Resources.MergedDictionaries[0]["colorConferenceTest"];
            Color clrEnded = (Color)Application.Current.Resources.MergedDictionaries[0]["colorConferenceEnded"];
            brsConferenceSolid = new SolidColorBrush(clrConference);
            brsConferenceBorder = new SolidColorBrush(Color.FromRgb((byte)(clrConference.R * .5f),
                                                                    (byte)(clrConference.G * .5f),
                                                                    (byte)(clrConference.B * .5f)));
            clrConference.A = 150;
            brsConference = new SolidColorBrush(clrConference);

            brsConferenceCancelledSolid = new SolidColorBrush(clrCancelled);
            brsConferenceCancelledBorder = new SolidColorBrush(Color.FromRgb((byte)(clrCancelled.R * .5f),
                                                                             (byte)(clrCancelled.G * .5f),
                                                                             (byte)(clrCancelled.B * .5f)));
            clrCancelled.A = 150;
            brsConferenceCancelled = new SolidColorBrush(clrCancelled);

            brsConferenceTestSolid = new SolidColorBrush(clrTest);
            brsConferenceTestBorder = new SolidColorBrush(Color.FromRgb((byte)(clrTest.R * .5f),
                                                                        (byte)(clrTest.G * .5f),
                                                                        (byte)(clrTest.B * .5f)));
            clrTest.A = 210;
            brsConferenceTest = new SolidColorBrush(clrTest);

            brsConferenceEndedSolid = new SolidColorBrush(clrEnded);
            brsConferenceEndedBorder = new SolidColorBrush(Color.FromRgb((byte)(clrEnded.R * .5f),
                                                                         (byte)(clrEnded.G * .5f),
                                                                         (byte)(clrEnded.B * .5f)));
            clrEnded.A = 150;
            brsConferenceEnded = new SolidColorBrush(clrEnded);

            penConferenceBorder = new Pen(brsConferenceBorder, 1);
            penConferenceCancelledBorder = new Pen(brsConferenceCancelledBorder, 1);
            penConferenceTestBorder = new Pen(brsConferenceTestBorder, 1);
            penConferenceEndedBorder = new Pen(brsConferenceEndedBorder, 1);
            brsOverflow = new SolidColorBrush(Color.FromArgb(50, 166, 65, 85));
            brsOverflowCheck = new SolidColorBrush(Color.FromArgb(50, 100, 104, 110));
            penStylus = new Pen(brsStylus, 1);
            penStylusFade = new Pen(brsStylusFade, 1.5);
            penCursor.Freeze();
            penStylus.Freeze();
            brsStylusFade.Freeze();

            brsConference.Freeze();
            brsConferenceSolid.Freeze();
            brsConferenceBorder.Freeze();
            penConferenceBorder.Freeze();

            brsConferenceCancelled.Freeze();
            brsConferenceCancelledSolid.Freeze();
            brsConferenceCancelledBorder.Freeze();
            penConferenceCancelledBorder.Freeze();

            brsConferenceTest.Freeze();
            brsConferenceTestSolid.Freeze();
            brsConferenceTestBorder.Freeze();
            penConferenceTestBorder.Freeze();

            brsConferenceEnded.Freeze();
            brsConferenceEndedSolid.Freeze();
            brsConferenceEndedBorder.Freeze();
            penConferenceEndedBorder.Freeze();

            brsOverflow.Freeze();

            scheduleTime = DateTime.Now;
            lastScheduleTime = scheduleTime;
        }

        public DateTime start = new();
        public DateTime end = new();
        public DateTime lastSearchStart = new();
        public DateTime lastSearchEnd = new();
        public void UpdateStartAndEnd()
        {
            // Get number of intervals for half the width of the view.
            double viewHalfHours = (ActualWidth * .5d) / DisplayTimeZoom();
            double viewHalfMinutes = (viewHalfHours - (int)viewHalfHours) * 60;
            int viewHalfSeconds = (int)((viewHalfMinutes - (int)viewHalfMinutes) * 1000);
            int viewHalfDays = (int)(viewHalfHours / 24);

            TimeSpan half = new TimeSpan(viewHalfDays, (int)viewHalfHours, (int)viewHalfMinutes, viewHalfSeconds);

            start = scheduleTime - half;
            end = scheduleTime + half;

            DateTime startSearchThresh = start - (half);
            DateTime endSearchThresh = end + (half);

            if ((startSearchThresh < lastSearchStart || endSearchThresh > lastSearchEnd)
                && conferenceView != null)
            {
                lastSearchStart = start - (half * 2);
                lastSearchEnd = end + (half * 2);
                conferenceView.SearchTimeframe(lastSearchStart, lastSearchEnd);
            }
        }

        // currentConference represents the currently hovered over conference, selectedConferences is for multi-select.
        public PageConferenceView.Conference? currentConference = null;
        public List<PageConferenceView.Conference> selectedConferences = new();

        protected override void OnRender(DrawingContext dc)
        {
            if (conferenceView == null)
                return;

            if (ActualWidth > 0)
            {
                // Background
                dc.DrawRectangle(Brushes.White, new Pen(Brushes.LightGray, 1),
                                 new Rect(0d, .5d, ActualWidth - .5d, ActualHeight - .5d));

                // Reduce zoom sensitivity the further out you get.
                double zoomTimeDisplay = DisplayTimeZoom();
                double zoomResourceDisplay = zoomResourceCurrent;

                // Calculate how far down lines should be drawn in case of only a few resource rows.
                double maxLineDepth = MaxLineDepth(zoomResourceDisplay);

                // Prepare shades and brushes.

                double shadeFive = (zoomTimeDisplay - 65) / 700f;
                MathHelper.Clamp(ref shadeFive, 0f, .075f);
                shadeFive *= 255f;
                double shadeQuarter = (zoomTimeDisplay - 20f) / 250f;
                MathHelper.Clamp(ref shadeQuarter, 0f, .14f);
                shadeQuarter *= 255f;
                double shadeHour = zoomTimeDisplay / 150f;
                MathHelper.Clamp(ref shadeHour, 0f, .26f);
                shadeHour *= 255f;

                Brush brsScheduleLineFive = new SolidColorBrush(Color.FromArgb((byte)shadeFive,
                                                                               0, 0, 0));
                Brush brsScheduleLineQuarter = new SolidColorBrush(Color.FromArgb((byte)shadeQuarter,
                                                                                  0, 0, 0));
                Brush brsScheduleLineHour = new SolidColorBrush(Color.FromArgb((byte)shadeHour,
                                                                               0, 0, 0));
                Brush brsScheduleLineDay = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0));

                Pen penScheduleLineFive = new Pen(brsScheduleLineFive, 1d);
                Pen penScheduleLineQuarter = new Pen(brsScheduleLineQuarter, 1d);
                Pen penScheduleLineHour = new Pen(brsScheduleLineHour, 1d);
                Pen penScheduleLineDay = new Pen(brsScheduleLineDay, 1.4d);
                Pen penScheduleResource = new Pen(brsScheduleLineDay, 1d);


                // Freez()ing Pens increases draw speed dramatically. Freez()ing the Brushes helps a bit, but Freez()ing
                // the Pens seems to implicitly catch the Brushes as well to draw outrageously fast.
                penScheduleLineFive.Freeze();
                penScheduleLineQuarter.Freeze();
                penScheduleLineHour.Freeze();
                penScheduleLineDay.Freeze();
                penScheduleResource.Freeze();

                // Time
                UpdateStartAndEnd();

                double incrementX;
                long incrementTicks;
                if (shadeFive / 255d < minShade)
                    if (shadeQuarter / 255d < minShade)
                        if (shadeHour / 255d < minShade)
                        {
                            incrementX = zoomTimeDisplay / 24d;
                            incrementTicks = ticks1Day;
                        }
                        else
                        {
                            incrementX = zoomTimeDisplay;
                            incrementTicks = ticks1Hour;
                        }
                    else
                    {
                        incrementX = zoomTimeDisplay / 4d;
                        incrementTicks = ticks15Min;
                    }
                else
                {
                    incrementX = zoomTimeDisplay / 12d;
                    incrementTicks = ticks5Min;
                }

                bool drawCursor = cursor != null &&
                                  (conferenceView.drag == PageConferenceView.Drag.None ||
                                   conferenceView.drag == PageConferenceView.Drag.Scroll);

                // Draw any conference overflow warnings beneath the grid lines.
                lock (PageConferenceView.resources)
                {
                    foreach (PageConferenceView.ResourceInfo ri in PageConferenceView.resources.Values)
                    {
                        double startY = GetYfromResource(ri.id, 0);
                        double endY = GetYfromResource(ri.id, ri.rowsTotal);

                        // Skip if out of frame.
                        if (endY > 0 && startY < ActualHeight)
                        {
                            // Only draw as far as necessary.
                            if (startY < 0)
                                startY = 0;
                            if (endY > ActualHeight)
                                endY = ActualHeight;

                            for (int i = 0; i < ri.overflowWarningAlternations.Count; i += 2)
                            {
                                DateTime s = ri.overflowWarningAlternations[i];
                                DateTime e = ri.overflowWarningAlternations[i + 1];

                                // Skip if out of frame.
                                if (e > start && s < end)
                                {
                                    // Only draw as far as necessary.
                                    if (s < start)
                                        s = start;
                                    if (e > end)
                                        e = end;

                                    double startPoint = GetXfromDateTime(s, zoomTimeDisplay);
                                    double endPoint = GetXfromDateTime(e, zoomTimeDisplay);
                                    Rect r = new(startPoint, startY, endPoint - startPoint, endY - startY);
                                    dc.DrawRectangle(brsOverflow, null, r);
                                }
                            }

                            // Draw the capacity highlight overlay, regardless of overflow.
                            if (drawCursor && Keyboard.IsKeyDown(Key.O))
                            {
                                int hoveredRow = GetRowFromY(cursor!.Value.Y, true);
                                int resourceTopRow = GetRowFromY(GetYfromResource(ri.id, 0), true);
                                int resourceBottomRow = GetRowFromY(GetYfromResource(ri.id, ri.rowsTotal), true);
                                if (hoveredRow >= resourceTopRow && hoveredRow < resourceBottomRow)
                                {
                                    DateTime xDT = GetDateTimeFromX(cursor!.Value.X, zoomTimeDisplay);

                                    int connections = 0;
                                    int confs = 0;

                                    // If completely empty, still draw a big rectangle and some 0s.
                                    if (ri.capacityChangePoints.Count == 0)
                                    {
                                        Rect r = new(0, startY, ActualWidth, endY - startY);
                                        dc.DrawRectangle(brsOverflowCheck, null, r);
                                        conferenceView.SetStatus(PageConferenceView.StatusContext.Overflow,
                                                                 new() { "Connections: 0", "Conferences: 0" }, 
                                                                 new() { false, false });
                                    }
                                    else
                                    {
                                        for (int i = 0; i < ri.capacityChangePoints.Count - 1; ++i)
                                        {
                                            PageConferenceView.OverflowPoint op = ri.capacityChangePoints[i];
                                            DateTime s = ri.capacityChangePoints[i].point;
                                            DateTime e = ri.capacityChangePoints[i + 1].point;

                                            // If the first change point is after the start of the screen or if the last
                                            // is before the end of the screen, there will be regions at the beginning and
                                            // end of the schedule that don't show a capacity of 0. Force this.
                                            if (i == 0 && xDT < s)
                                            {
                                                e = s;
                                                s = start;
                                            }
                                            else if (i == ri.capacityChangePoints.Count - 2 && xDT > e)
                                            {
                                                s = e;
                                                e = end;
                                                connections = 0;
                                                confs = 0;
                                            }
                                            else
                                            {
                                                connections += op.change;
                                                confs += op.confAdd;
                                            }

                                            if (xDT >= s && xDT < e)
                                            {
                                                // Only draw as far as necessary.
                                                if (s < start)
                                                    s = start;
                                                if (e > end)
                                                    e = end;

                                                double startPoint = GetXfromDateTime(s, zoomTimeDisplay);
                                                double endPoint = GetXfromDateTime(e, zoomTimeDisplay);
                                                Rect r = new(startPoint, startY, endPoint - startPoint, endY - startY);
                                                dc.DrawRectangle(brsOverflowCheck, null, r);

                                                List<string> statusMessages = new()
                                                {
                                                    $"Connections: {connections}/{ri.connectionCapacity}",
                                                    $"Conferences: {confs}/{ri.conferenceCapacity}"
                                                };
                                                List<bool> bold = new()
                                                {
                                                    connections > ri.connectionCapacity,
                                                    confs > ri.conferenceCapacity
                                                };
                                                conferenceView.SetStatus(PageConferenceView.StatusContext.Overflow,
                                                                         statusMessages, bold);

                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (conferenceView.statusBarContext == PageConferenceView.StatusContext.Overflow)
                                    conferenceView.SetStatusBar();
                            }
                        }
                    }
                }

                // Get the start time, rounded down to the nearest increment.
                DateTime t = start.AddTicks(-(start.Ticks % incrementTicks));

                double x = TimeToX(t, zoomTimeDisplay) + .5f;

                while (t < end)
                {
                    double xInt = (int)x + .5d; // Snap to nearest pixel.
                    if (x > 0d && x < ActualWidth)
                    {
                        if (t.Ticks % ticks1Day == 0)
                            dc.DrawLine(penScheduleLineDay, new Point(xInt, .5f),
                                                             new Point(xInt, maxLineDepth));
                        else if (t.Ticks % ticks1Hour == 0)
                            dc.DrawLine(penScheduleLineHour, new Point(xInt, .5f),
                                                                new Point(xInt, maxLineDepth));
                        else if (t.Ticks % ticks15Min == 0)
                            dc.DrawLine(penScheduleLineQuarter, new Point(xInt, .5f),
                                                             new Point(xInt, maxLineDepth));
                        else // has to be 5 minutes
                            dc.DrawLine(penScheduleLineFive, new Point(xInt, .5f),
                                                            new Point(xInt, maxLineDepth));
                    }

                    t = t.AddTicks(incrementTicks);
                    x += incrementX;
                }

                // Resources (drawn last as these lines want to overlay the time lines).
                double scroll = DisplayResourceScroll();
                for (double y = 0; y < maxLineDepth; y += zoomResourceDisplay)
                {
                    double yPix = (y + .5f) - (scroll % zoomResourceDisplay);
                    // Wrap around if the line falls off the top.
                    if (yPix < 0)
                        yPix += ActualHeight + (zoomResourceDisplay - (ActualHeight % zoomResourceDisplay));
                    dc.DrawLine(penScheduleResource, new Point(.5f, yPix),
                                                    new Point(ActualWidth, yPix));
                }

                // This is a central vertical line in the centre of the screen to indicate the current time, but it
                // needs to be prettier to use it.
                // Draw the stylus.
                //dc.DrawLine(penStylus, new Point((int)(ActualWidth * .5d) + .5d, -3d),
                //                       new Point((int)(ActualWidth * .5d) + .5d, 4d));

                // Draw conferences.
                bool resizeCursorSet = false;
                bool isMouseOverConference = false;

                lock (conferenceView.conferences)
                {
                    Point cursorPoint = new();
                    if (cursor != null)
                        cursorPoint = cursor.Value;

                    foreach (var kvp in conferenceView.conferences)
                    {
                        PageConferenceView.Conference conference = kvp.Value;
                        if (conference.end > start && conference.start < end)
                        {
                            // Assemble the correct paint set :)
                            Brush textBrush = Brushes.Black;
                            Brush brush = brsConference;
                            Brush brushSolid = brsConferenceSolid;
                            Pen border = penConferenceBorder;
                            Pen borderSelected = new Pen(brsConferenceBorder, 2);
                            if (conference.test)
                            {
                                textBrush = Brushes.White;
                                brush = brsConferenceTest;
                                brushSolid = brsConferenceTestSolid;
                                border = penConferenceTestBorder;
                                borderSelected.Brush = brsConferenceTestBorder;
                            }
                            if (conference.cancelled)
                            {
                                brush = brsConferenceCancelled;
                                brushSolid = brsConferenceCancelledSolid;
                                border = penConferenceCancelledBorder;
                                borderSelected.Brush = brsConferenceCancelledBorder;
                            }

                            double startX = GetXfromDateTime(conference.start > start ? conference.start : start,
                                                               zoomTimeDisplay);
                            double endX = GetXfromDateTime(conference.end < end ? conference.end : end,
                                                             zoomTimeDisplay);
                            startX = startX < 0 ? (int)(startX - 1) : (int)startX;
                            endX = endX < 0 ? (int)(endX - 1) : (int)endX;

                            // Never end need to start before -1, and never any need to proceed if end < 0.
                            if (startX < -1)
                                startX = -1;
                            if (endX < 0)
                                continue;

                            int startY = (int)GetYfromResource(conference.resourceID, conference.resourceRow);
                            Rect area = new Rect(startX + .5, startY + .5,
                                                 ((int)endX - startX), (int)zoomResourceDisplay);

                            bool dragMove = conferenceView.drag == PageConferenceView.Drag.Move;
                            bool dragResize = conferenceView.drag == PageConferenceView.Drag.Resize;
                            if (area.Contains(cursorPoint) ||
                                ((dragMove || dragResize) && conference == currentConference))
                            {
                                currentConference = conference;
                                isMouseOverConference = true;
                                if (selectedConferences.Contains(conference))
                                    // Reduce the size of the rect slightly for drawing so the pen is on the inside.
                                    dc.DrawRectangle(brushSolid, borderSelected,
                                                     new Rect(area.X + .5d, area.Y + .5d,
                                                     area.Width - 1, area.Height - 1));
                                else
                                    dc.DrawRectangle(brushSolid, border, area);

                                if ((cursorPoint.X < area.Left + 5 || cursorPoint.X > area.Right - 5) && !dragMove)
                                {
                                    if (App.sd.editPermissions[Glo.PERMISSION_CONFERENCES])
                                        Cursor = Cursors.SizeWE;
                                    resizeCursorSet = true;
                                    if (!conferenceView.dragMouseHasMoved)
                                        conferenceView.resizeStart = cursorPoint.X < area.Left + 5;
                                }
                            }
                            else if (selectedConferences.Contains(conference))
                                // Reduce the size of the rect slightly for drawing so the pen is on the inside.
                                dc.DrawRectangle(brushSolid, borderSelected,
                                    new Rect(area.X + .5d, area.Y + .5d,
                                             area.Width - 1, area.Height - 1));
                            else
                                dc.DrawRectangle(brush, border, area);

                            if (area.Width > 8)
                            {
                                // Draw title.
                                FormattedText title = new(conference.title,
                                                          CultureInfo.CurrentCulture,
                                                          FlowDirection.LeftToRight,
                                                          segoeUISemiBold,
                                                          12,
                                                          textBrush,
                                                          VisualTreeHelper.GetDpi(this).PixelsPerDip);

                                Geometry clipGeometry = new RectangleGeometry(area);
                                dc.PushClip(clipGeometry);

                                dc.DrawText(title, new(startX + 4, startY + 3));

                                // Draw start and end time.
                                if (area.Height > 25)
                                {
                                    FormattedText time = new(conference.start.ToString("HH:mm") + " - " +
                                                             conference.end.ToString("HH:mm"),
                                                             CultureInfo.CurrentCulture,
                                                             FlowDirection.LeftToRight,
                                                             segoeUI,
                                                             12,
                                                             textBrush,
                                                             VisualTreeHelper.GetDpi(this).PixelsPerDip);
                                    dc.DrawText(time, new(startX + 4, startY + 21));
                                }

                                dc.Pop();
                            }
                        }
                    }
                }

                // Don't forget the current conference or switch off the resize cursor while we're dragging it.
                if (!resizeCursorSet && conferenceView.drag != PageConferenceView.Drag.Resize)
                    Cursor = Cursors.Arrow;
                if (!isMouseOverConference && conferenceView.drag == PageConferenceView.Drag.None)
                    currentConference = null;

                // Draw the cursor.
                if (drawCursor)
                {
                    double y = GetRowFromY(cursor!.Value.Y, true);
                    if (y >= 0)
                    {
                        y *= zoomResourceCurrent;
                        y -= scroll;
                        DateTime xDT = GetDateTimeFromX(cursor.Value.X, zoomTimeDisplay);
                        xDT = SnapDateTime(xDT, zoomTimeDisplay);
                        x = (int)GetXfromDateTime(xDT, zoomTimeDisplay) + .5d;

                        if (!isMouseOverConference)
                            dc.DrawLine(penCursor, new Point(x, y < 0 ? 0 : y),
                                                   new Point(x, y + zoomResourceDisplay));

                        dc.DrawGeometry(null, penStylusFade,
                                        new LineGeometry(new Point(x, y * .6d), new Point(x, 0d)));
                    }
                }
            }
        }

        public void Drag(double xDif, double yDif)
        {
            lastScheduleTime = scheduleTime;
            scheduleTime = scheduleTime.AddSeconds(xDif * (3600 * (1 / DisplayTimeZoom())));
            lastScrollResource = scrollResource;
            scrollResource += yDif;
        }
        public void SetCursor(double x, double y)
        {
            if (x < 0 || y < 0)
                cursor = null;
            else
                cursor = new Point(x, y);
        }
        public void ZoomTime(int strength)
        {
            ZoomTime(strength, ActualWidth * .5d);
        }
        public void ZoomTime(int strength, double xCentre)
        {
            zoomTime += zoomTimeSensitivity * strength;
            zoomTime = Math.Clamp(zoomTime, zoomTimeMinimum, zoomTimeMaximum);

            double xDif = xCentre - (ActualWidth * .5d);

            // This sets the target time for zooming relative to the mouse cursor. I can't really explain my reasoning
            // as it all seems very arbitrary, but the .4d and .1d as opposed to 1 feel better to me that scrolling the
            // whole way towards the target. Likewise, reversing the zoom out direction just feels more natural.
            if (strength > 0)
                zoomTimeTarget = scheduleTime.AddSeconds(xDif * .4d * (3600 * (1 / DisplayTimeZoom())));
            else
                zoomTimeTarget = scheduleTime.AddSeconds(-xDif * .1d * (3600 * (1 / DisplayTimeZoom())));
        }
        public void ZoomResource(int strength)
        {
            ZoomResource(strength, ActualHeight * .5f);
        }
        public void ZoomResource(int strength, double yCentre)
        {
            zoomResource += zoomResourceSensitivity * strength;
            zoomResource = Math.Clamp(zoomResource, zoomResourceMinimum, zoomResourceMaximum);
            EnforceResourceScrollLimits();

            // See reason for weird values above in ZoomTime(int, double).
            if (strength > 0)
                zoomResourceScrollCentre = yCentre;
            else
                zoomResourceScrollCentre = ActualHeight * .5f;
        }
        public void ScrollResource(double strength)
        {
            scrollResource += zoomResourceCurrent * strength;
            EnforceResourceScrollLimits();
        }

        public void EnforceResourceScrollLimits()
        {
            if (scrollResource > 1 + (zoomResourceCurrent * conferenceView!.totalRows) - ActualHeight)
                scrollResource = 1 + (zoomResourceCurrent * conferenceView!.totalRows) - ActualHeight;
            if (scrollResource < 0) scrollResource = 0;
        }

        //   H E L P E R   F U N C T I O N S

        // These Display functions are because the values used by Render() want to be slightly different from the
        // actual values.
        public double DisplayTimeZoom()
        {
            // Here reside the mathematical flailings of a mathematically inept programmer. My have a curve of
            // sensitivity while zooming in and out, while maintaining some anchor points.

            // Convert X to a value between 0 and 1, where 0 is the minimum and 1 is the maximum.
            double x = (zoomTimeCurrent - zoomTimeMinimum) / (zoomTimeMaximum - zoomTimeMinimum);
            double xForCurve = x * .4d + .55d;
            // Make it into a curve to simulate higher sensitivity when zoomed in.
            double curve = Math.Sqrt(1d - (xForCurve * xForCurve));
            // Soften the curve.
            curve -= (curve - (1 - xForCurve)) * .7f;
            // Rotate the curve.
            curve = -curve + 1;

            // Bring it all together.
            double zoomTimeDisplay = x * (x * .6d + .4d) * curve * (zoomTimeMaximum - zoomTimeMinimum);
            zoomTimeDisplay += zoomTimeMinimum;

            return zoomTimeDisplay;
        }
        public double DisplayResourceScroll()
        {
            double scroll = scrollResource;
            if (scroll > 1 + (zoomResourceCurrent * conferenceView!.totalRows) - ActualHeight)
                scroll = 1 + (zoomResourceCurrent * conferenceView!.totalRows) - ActualHeight;
            if (scroll < 0) scroll = 0;
            return (int)scroll;
        }

        public double MaxLineDepth(double zoomResourceDisplay)
        {
            // This method calculates how far down lines should be drawn in case of the screen not being filled with
            // resource rows.
            double maxLineDepth = conferenceView!.totalRows * zoomResourceDisplay + .5f;
            if (maxLineDepth > ActualHeight)
                maxLineDepth = ActualHeight;
            return maxLineDepth;
        }

        public double ScrollMax()
        {
            if (conferenceView == null) return 1;
            return ((zoomResourceCurrent * conferenceView.totalRows) - ActualHeight) + 1;
        }
        public double ScrollPercent()
        {
            if (conferenceView == null) return 0;
            return scrollResource / (((zoomResourceCurrent * conferenceView.totalRows) - ActualHeight) + 1);
        }
        public double ViewPercent()
        {
            if (conferenceView == null) return 0;
            double ret = ActualHeight / ((zoomResourceCurrent * conferenceView.totalRows) + 1);
            return ret > 1 ? 1 : ret;
        }

        public int GetRowFromY(double y, bool capped)
        {
            int resource = (int)((y + DisplayResourceScroll()) / zoomResourceCurrent);
            if (capped)
                return resource < conferenceView!.totalRows ? resource : conferenceView!.totalRows - 1;
            else
                return resource;
        }
        public double GetYfromResource(int resourceID, int resourceRow)
        {
            double row = conferenceView!.GetResourceRowInView(resourceID, resourceRow);
            double y = row * zoomResourceCurrent;
            return y - DisplayResourceScroll();
        }
        public DateTime GetDateTimeFromX(double x)
        {
            return GetDateTimeFromX(x, DisplayTimeZoom());
        }
        public DateTime GetDateTimeFromX(double x, double zoom)
        {
            x -= ActualWidth * .5d;
            TimeSpan dif = new TimeSpan((long)(36_000_000_000d * (x / zoom)));
            return scheduleTime + dif;
        }
        public double GetXfromDateTime(DateTime dt, double zoom)
        {
            TimeSpan dif = dt - scheduleTime;
            return ActualWidth * .5d + .49d + zoom * ((dif.Days * 24d) +
                                                     dif.Hours +
                                                     (dif.Minutes / 60d) +
                                                     (dif.Seconds / 3600d));
        }
        public DateTime SnapDateTime(DateTime dt)
        {
            return SnapDateTime(dt, DisplayTimeZoom());
        }
        public DateTime SnapDateTime(DateTime dt, double zoomTimeDisplay)
        {
            int minsSnap = 1;
            if (zoomTimeDisplay < 30)
                minsSnap = 60;
            else if (zoomTimeDisplay < 90)
                minsSnap = 15;
            else if (zoomTimeDisplay < 220)
                minsSnap = 5;

            // Nudge dt forwards half of minutes in order to snap to nearest rather than floor.
            dt = dt.AddMinutes(minsSnap / 2);
            if (minsSnap % 1f != 0)
                dt = dt.AddSeconds((minsSnap % 1f) * 60);

            long minutesInTicks = minsSnap * 600_000_000L;
            return new DateTime(dt.Ticks - (dt.Ticks % minutesInTicks));
        }

        // Get the X coordinate on the cnvConferenceView from DateTime.
        public double TimeToX(DateTime dt, double zoomTimeDisplay)
        {
            double canvasMid = ActualWidth * .5d;
            long relativeTicks = dt.Ticks - scheduleTime.Ticks;
            double relativePixels = ((double)relativeTicks / (double)ticks1Hour) * zoomTimeDisplay;

            return canvasMid + relativePixels;
        }
    }
}
