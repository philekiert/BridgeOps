using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
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
            public DateTime start;
            public DateTime end;
            public int capacity;
            // Unused in the list of resources, but use to pass the selected row to the New Conference.
            private int selectedRow = 0;
            private int selectedRowTotal = 0;
            public int SelectedRow { get { return selectedRow; } }  
            public int SelectedRowTotal { get { return selectedRowTotal; } }

            public ResourceInfo(int id, string name, DateTime start, DateTime end, int capacity)
            {
                this.id = id;
                this.name = name;
                this.start = start;
                this.end = end;
                this.capacity = capacity;
                selectedRow = 0;
            }
            public void SetSelectedRow(int selected, int selectedTotal)
            {
                selectedRow = Math.Clamp(selected, 0, capacity);
                selectedRowTotal = Math.Clamp(selectedTotal, 0, totalCapacity);
            }
        }
        // All resources are stored here, regardless of resources visibility (not yet implemented as of 19/12/2023).
        public static List<ResourceInfo> resources = new();
        public static List<string> resourceRowNames = new();
        // This is a list of indices, linking displayed rows to resources to allow the user customise the display.
        public static List<int> resourcesOrder = new();
        public bool updateScrollBar = false;
        public void SetResources()
        {
            lock (resourceRowNames)
            {
                lock (resourcesOrder)
                {
                    resourceRowNames.Clear();
                    resourcesOrder.Clear();
                    totalCapacity = 0;
                    foreach (ResourceInfo ri in resources)
                    {
                        resourcesOrder.Add(resourcesOrder.Count);
                        for (int i = 1; i <= ri.capacity; ++i)
                            resourceRowNames.Add(ri.name + " " + i);
                        totalCapacity += ri.capacity;
                    }
                }
            }

            // This causes UpdateScrollBar() to trigger in TimerUpdate(). Can't figure out right now why just calling
            // it from here doesn't work.
            updateScrollBar = true;
        }
        public static ResourceInfo? GetResourceFromSelectedRow(int row)
        {
            int resourceStart = 0;
            if (row >= 0)
            {
                for (int i = 0; i < resourcesOrder.Count; ++i)
                {
                    if (row < resourceStart + resources[resourcesOrder[i]].capacity)
                    {
                        resources[resourcesOrder[i]].SetSelectedRow(row - resourceStart, row);
                        return resources[resourcesOrder[i]];
                    }
                    else
                        resourceStart += resources[resourcesOrder[i]].capacity;
                }
            }
            return null;
        }

        public static int totalCapacity = 0;
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

            MainWindow.pageConferenceViews.Add(this);
        }

        void RedrawRuler()
        {
            schRuler.InvalidateVisual();
        }
        void RedrawResources()
        {
            schResources.InvalidateVisual();
        }
        void RedrawGrid()
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
                    MathHelper.Lerp(ref schView.zoomTimeCurrent, schView.zoomTime,
                                                             smoothZoomSpeed * deltaTime, .2d);
                else
                    schView.zoomTimeCurrent = schView.zoomTime;

                horizontalChange = true;
            }
            if (!verticalChange && schView.zoomResourceCurrent != schView.zoomResource)
            {
                double old = schView.zoomResourceCurrent;
                if (schView.smoothZoom)
                    MathHelper.Lerp(ref schView.zoomResourceCurrent, schView.zoomResource,
                                                                     smoothZoomSpeed * deltaTime, .2d);
                else
                    schView.zoomResourceCurrent = schView.zoomResource;

                // Nudge the scroll down .5 of the screen so that the zoom tracks with the middle of the scroll,
                // not the start.
                schView.scrollResource += schView.ActualHeight * .5d;
                schView.scrollResource *= schView.zoomResourceCurrent / old;
                schView.scrollResource -= schView.ActualHeight * .5d;
                schView.EnforceResourceScrollLimits();

                UpdateScrollBar();

                verticalChange = true;
            }

            if (!(verticalChange && horizontalChange) && schView.scheduleTime != schView.lastScheduleTime)
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

            if (!horizontalChange && cursorMoved)
            {
                cursorMoved = false;
                horizontalChange = true;
            }

            if (verticalChange) RedrawResources();
            if (horizontalChange) RedrawRuler();
            if (verticalChange || horizontalChange) RedrawGrid();

            lastFrame = Environment.TickCount64;
        }

        bool dragging = false; // Switched on in MouseDown() inside the grid, switched off in MouseUp() anywhere.
        private void schView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Double Click
            if (e.ClickCount == 2)
            {
                DateTime time = schView.SnapDateTime(schView.GetDateTimeFromX(e.GetPosition(schView).X));
                int resource = schView.GetResourceFromY(e.GetPosition(schView).Y);
                if (resource != -1)
                {
                    NewConference newConf = new(GetResourceFromSelectedRow(resource), time);
                    try { newConf.Show(); } catch { }
                }
            }

            // Drag
            else if (e.ChangedButton == MouseButton.Middle ||
                e.ChangedButton == MouseButton.Left)
            {
                dragging = true;
                ((IInputElement)sender).CaptureMouse();
            }
        }

        private void schView_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (dragging &&
                (e.ChangedButton == MouseButton.Middle ||
                 e.ChangedButton == MouseButton.Left))
            {
                dragging = false;
                ((IInputElement)sender).ReleaseMouseCapture();
                schView.EnforceResourceScrollLimits();
                if (e.GetPosition(schView).X >= 0 && e.GetPosition(schView).Y >= 0)
                {
                    schView.SetCursor((int)e.GetPosition(schView).X, (int)e.GetPosition(schView).Y);
                    cursorMoved = true;
                }
            }
        }

        double lastX = 0;
        double lastY = 0;
        bool cursorMoved = false;
        private void schView_MouseMove(object sender, MouseEventArgs e)
        {
            double newX = (int)e.GetPosition(this).X;
            double newY = (int)e.GetPosition(this).Y;
            if (dragging)
            {
                schView.Drag(lastX - newX, lastY - newY);
                UpdateScrollBar();
                schView.SetCursor(-1d, -1d);
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
            if (CtrlDown())
            {
                if (ShiftDown())
                    schView.ZoomResource(e.Delta > 0 ? 1 : -1);
                else
                    schView.ZoomTime(e.Delta > 0 ? 1 : -1);
                RedrawGrid();
                RedrawRuler();
            }
            else
            {
                if (ShiftDown())
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
                                                schView.scheduleTime.Hour,
                                                schView.scheduleTime.Minute,
                                                schView.scheduleTime.Second,
                                                schView.scheduleTime.Millisecond);
        }

        private void scrollBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateScrollBar();
        }
    }

    public class ScheduleRuler : Canvas
    {
        public ScheduleView? view;
        public ScheduleResources? res;

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
                int viewHalfSeconds = (int)((viewHalfMinutes - (int)viewHalfMinutes) * 1000);
                int viewHalfDays = (int)(viewHalfHours / 24);

                TimeSpan half = new TimeSpan(viewHalfDays, (int)viewHalfHours, (int)viewHalfMinutes, viewHalfSeconds);

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
                Typeface segoeUI = new Typeface("Segoe UI");
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
                                FormattedText date = new($"{t.DayOfWeek} {t.Day}/{t.Month}/{t.Year}",
                                           CultureInfo.CurrentCulture,
                                           FlowDirection.LeftToRight,
                                           segoeUI,
                                           12,
                                           Brushes.Black,
                                           VisualTreeHelper.GetDpi(this).PixelsPerDip);
                                dc.DrawText(date, new Point(xInt, 0));
                                if (xInt < firstDateX)
                                    firstDateX = xInt;
                            }
                            else
                                firstDateStringOverride = $"{t.DayOfWeek} {t.Day}/{t.Month}/{t.Year}";
                        }
                    }

                    t = t.AddTicks(incrementTicks);
                    x += incrementX;
                }

                FormattedText dateEdge = new(firstDateStringOverride == "" ?
                                                $"{start.DayOfWeek} {start.Day}/{start.Month}/{start.Year}" :
                                                firstDateStringOverride,
                                             CultureInfo.CurrentCulture,
                                             FlowDirection.LeftToRight,
                                             segoeUI,
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
        public ScheduleView? view;

        Typeface segoeUI = new Typeface("Segoe UI");

        Brush brsHighlight;
        Brush brsDivider;
        Pen penDivider;
        public ScheduleResources()
        {
            brsHighlight = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0));
            brsDivider = new SolidColorBrush(Color.FromArgb(255, 120, 120, 120));
            penDivider = new Pen(brsDivider, 1);

            penDivider.Freeze();
        }

        // OnRender() has some repeated code from ScheduleView, but I have kept the draw code for the resources
        // separate due to not wanting to redraw the ruler unless we have to. The function still makes use of some
        // public members from ScheduleView.
        protected override void OnRender(DrawingContext dc)
        {
            if (view != null)
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

                    int row = view.GetResourceFromY(yPix);
                    PageConferenceView.ResourceInfo? resource = PageConferenceView.GetResourceFromSelectedRow(row);
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
                    double y = view.GetResourceFromY(view.cursor.Value.Y);

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
        public double gridHeight = 0;

        public bool smoothZoom = true;
        public int zoomTime = 70; // How many pixels an hour can be reduced to.
        public double zoomTimeCurrent = 70d; // Used for smooth Lerp()ing.
        public int zoomTimeMinimum = 10;
        public int zoomTimeMaximum = 210;
        public int zoomTimeSensitivity = 10;
        public int zoomResource = 40;
        public double zoomResourceCurrent = 40d; // Used for smooth Lerp()ing.
        public int zoomResourceMinimum = 20;
        public int zoomResourceMaximum = 200;
        public int zoomResourceSensitivity = 20;

        float minShade = .2f;

        public const long ticks5Min = 3_000_000_000;
        public const long ticks15Min = 9_000_000_000;
        public const long ticks30Min = 18_000_000_000;
        public const long ticks1Hour = 36_000_000_000;
        public const long ticks1Day = 864_000_000_000;

        public DateTime lastScheduleTime = DateTime.Now;
        public DateTime scheduleTime = DateTime.Now;
        public double lastScrollResource = 0f;
        public double scrollResource = 0f;

        public Point? cursor = null;

        // These brushes remain the same forever, so declare, initialise and freeze.
        Brush brsCursor;
        Pen penCursor;
        Brush brsStylus;
        Pen penStylus;
        public ScheduleView()
        {
            brsCursor = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            penCursor = new Pen(brsCursor, 1);
            brsStylus = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            penStylus = new Pen(brsStylus, 1);
            penCursor.Freeze();
            penStylus.Freeze();
        }

        protected override void OnRender(DrawingContext dc)
        {
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

                double shadeFive = (zoomTimeDisplay - 65) / 30f;
                MathHelper.Clamp(ref shadeFive, 0f, 1f);
                shadeFive *= 255f;
                double shadeQuarter = (zoomTimeDisplay - 20f) / 30f;
                MathHelper.Clamp(ref shadeQuarter, 0f, 1f);
                shadeQuarter *= 255f;
                double shadeHour = zoomTimeDisplay / 40f;
                MathHelper.Clamp(ref shadeHour, .45f, 1f);
                shadeHour *= 255f;

                Brush brsScheduleLineFive = new SolidColorBrush(Color.FromArgb((byte)shadeFive,
                                                                               240, 240, 240));
                Brush brsScheduleLineQuarter = new SolidColorBrush(Color.FromArgb((byte)shadeQuarter,
                                                                                  225, 225, 225));
                Brush brsScheduleLineHour = new SolidColorBrush(Color.FromArgb((byte)shadeHour,
                                                                               180, 180, 180));
                Brush brsScheduleLineDay = new SolidColorBrush(Color.FromRgb(120, 120, 120));

                Pen penScheduleLineFive = new Pen(brsScheduleLineFive, 1);
                Pen penScheduleLineQuarter = new Pen(brsScheduleLineQuarter, 1);
                Pen penScheduleLineHour = new Pen(brsScheduleLineHour, 1);
                Pen penScheduleLineDay = new Pen(brsScheduleLineDay, 1);


                // Freez()ing Pens increases draw speed dramatically. Freez()ing the Brushes helps a bit, but Freez()ing
                // the Pens seems to implicitly catch the Brushes as well to draw outrageously fast.
                penScheduleLineFive.Freeze();
                penScheduleLineQuarter.Freeze();
                penScheduleLineHour.Freeze();
                penScheduleLineDay.Freeze();

                // Time

                // Get number of intervals for half the width of the view.
                double viewHalfHours = (ActualWidth * .5d) / zoomTimeDisplay;
                double viewHalfMinutes = (viewHalfHours - (int)viewHalfHours) * 60;
                int viewHalfSeconds = (int)((viewHalfMinutes - (int)viewHalfMinutes) * 1000);
                int viewHalfDays = (int)(viewHalfHours / 24);

                TimeSpan half = new TimeSpan(viewHalfDays, (int)viewHalfHours, (int)viewHalfMinutes, viewHalfSeconds);

                DateTime start = scheduleTime - half;
                DateTime end = scheduleTime + half;

                double incrementX;
                long incrementTicks;
                if (shadeFive < minShade)
                    if (shadeQuarter < minShade)
                        if (shadeHour < minShade)
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
                    dc.DrawLine(penScheduleLineDay, new Point(.5f, yPix),
                                                    new Point(ActualWidth, yPix));
                }

                // Draw the cursor.
                if (cursor != null)
                {
                    double y = GetResourceFromY(cursor.Value.Y);
                    if (y >= 0)
                    {
                        y *= zoomResourceCurrent;
                        y -= scroll;
                        DateTime xDT = GetDateTimeFromX(cursor.Value.X, zoomTimeDisplay);
                        xDT = SnapDateTime(xDT, zoomTimeDisplay);
                        x = (int)GetXfromDateTime(xDT, zoomTimeDisplay) + .5d;

                        dc.DrawLine(penCursor, new Point(x, y < 0 ? 0 : y),
                                               new Point(x, y + zoomResourceDisplay));
                        dc.DrawLine(penCursor, new Point(x, -5d),
                                               new Point(x, .5d));
                    }
                }

                // Draw the stylus.
                dc.DrawLine(penStylus, new Point((int)(ActualWidth * .5d) + .5d, -3d),
                                       new Point((int)(ActualWidth * .5d) + .5d, 4d));
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
        }
        public void ZoomResource(int strength)
        {
            zoomResource += zoomResourceSensitivity * strength;
            zoomResource = Math.Clamp(zoomResource, zoomResourceMinimum, zoomResourceMaximum);
        }
        public void ScrollResource(double strength)
        {
            scrollResource += zoomResourceCurrent * strength;
            EnforceResourceScrollLimits();
        }

        public void EnforceResourceScrollLimits()
        {
            if (scrollResource > 1 + (zoomResourceCurrent * PageConferenceView.totalCapacity) - ActualHeight)
                scrollResource = 1 + (zoomResourceCurrent * PageConferenceView.totalCapacity) - ActualHeight;
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
            if (scroll > 1 + (zoomResourceCurrent * PageConferenceView.totalCapacity) - ActualHeight)
                scroll = 1 + (zoomResourceCurrent * PageConferenceView.totalCapacity) - ActualHeight;
            if (scroll < 0) scroll = 0;
            return (int)scroll;
        }

        public double MaxLineDepth(double zoomResourceDisplay)
        {
            // This method calculates how far down lines should be drawn in case of the screen not being filled with
            // resource rows.
            double maxLineDepth = PageConferenceView.totalCapacity * zoomResourceDisplay + .5f;
            if (maxLineDepth > ActualHeight)
                maxLineDepth = ActualHeight;
            return maxLineDepth;
        }

        public double ScrollMax()
        {
            return ((zoomResourceCurrent * PageConferenceView.totalCapacity) - ActualHeight) + 1;
        }
        public double ScrollPercent()
        {
            return scrollResource / (((zoomResourceCurrent * PageConferenceView.totalCapacity) - ActualHeight) + 1);
        }
        public double ViewPercent()
        {
            double ret = ActualHeight / ((zoomResourceCurrent * PageConferenceView.totalCapacity) + 1);
            return ret > 1 ? 1 : ret;
        }

        public int GetResourceFromY(double y)
        {
            int resource = (int)((y + DisplayResourceScroll()) / zoomResourceCurrent);
            return resource < PageConferenceView.totalCapacity ? resource : -1;
        }
        public double GetYfromResource(int resource)
        {
            double y = resource * zoomResourceCurrent;
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
