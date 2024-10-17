using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BridgeOpsClient.PageConferenceView;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BridgeOpsClient
{

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
        Brush brsConferenceHover;
        Brush brsConferenceSolid;
        Brush brsConferenceTest;
        Brush brsConferenceCancelled;
        Brush brsConferenceCancelledHover;
        Brush brsConferenceTestBorder;
        Brush brsConferenceTestHover;
        Brush brsConferenceTestSolid;
        Brush brsConferenceEnded;
        Brush brsConferenceEndedBorder;
        Brush brsConferenceEndedHover;
        Brush brsConferenceEndedSolid;
        Brush brsConferenceFailed;
        Brush brsConferenceFailedBorder;
        Brush brsConferenceFailedHover;
        Brush brsConferenceFailedSolid;
        Brush brsConferenceDegraded;
        Brush brsConferenceDegradedBorder;
        Brush brsConferenceDegradedHover;
        Brush brsConferenceDegradedSolid;
        Brush brsOverflow;
        Brush brsOverflowCheck;
        Pen penConferenceBorder;
        Pen penConferenceDegradedBorder;
        Pen penConferenceFailedBorder;
        Pen penConferenceTestBorder;
        Pen penConferenceEndedBorder;
        Pen penStylus;
        Pen penStylusFade;

        PathGeometry symbolPlay = new();

        public ScheduleView()
        {
            ClipToBounds = true;

            // Create a play symbol for active conferences.
            Point p1 = new Point(0, 0);
            Point p2 = new Point(0, 10d);
            Point p3 = new Point(7.33d, 5d);
            PathFigure pathPlay = new PathFigure { StartPoint = p1 };
            pathPlay.Segments.Add(new LineSegment(p2, true));
            pathPlay.Segments.Add(new LineSegment(p3, true));
            pathPlay.IsClosed = true;
            symbolPlay.Figures.Add(pathPlay);

            // Prepare colors, brushes and pens.
            brsCursor = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));
            penCursor = new Pen(brsCursor, 1.4);
            brsStylus = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0));
            brsStylusFade = new LinearGradientBrush(Color.FromArgb(100, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 90);
            Color clrConference = (Color)Application.Current.Resources.MergedDictionaries[0]["colorConference"];
            Color clrFailed = (Color)Application.Current.Resources.MergedDictionaries[0]["colorConferenceFailed"];
            Color clrTest = (Color)Application.Current.Resources.MergedDictionaries[0]["colorConferenceTest"];
            Color clrEnded = (Color)Application.Current.Resources.MergedDictionaries[0]["colorConferenceEnded"];
            Color clrDegraded = (Color)Application.Current.Resources.MergedDictionaries[0]["colorConferenceDegraded"];
            Color clrOverflow = (Color)Application.Current.Resources.MergedDictionaries[0]["colorConferenceWarning"];
            Color clrOverflowCheck = (Color)Application.Current.Resources.MergedDictionaries[0]["colorConferenceCheck"];

            // Conference
            brsConferenceSolid = new SolidColorBrush(clrConference);
            brsConferenceBorder = new SolidColorBrush(Color.FromRgb((byte)(clrConference.R * .5f),
                                                                    (byte)(clrConference.G * .5f),
                                                                    (byte)(clrConference.B * .5f)));
            clrConference.A = 200;
            brsConferenceHover = new SolidColorBrush(clrConference);
            clrConference.A = 150;
            brsConference = new SolidColorBrush(clrConference);
            clrConference.A = 100;
            brsConferenceCancelledHover = new SolidColorBrush(clrConference);
            clrConference.A = 50;
            brsConferenceCancelled = new SolidColorBrush(clrConference);

            // Failed
            brsConferenceFailedSolid = new SolidColorBrush(clrFailed);
            brsConferenceFailedBorder = new SolidColorBrush(Color.FromRgb((byte)(clrFailed.R * .5f),
                                                                             (byte)(clrFailed.G * .5f),
                                                                             (byte)(clrFailed.B * .5f)));
            clrFailed.A = 200;
            brsConferenceFailedHover = new SolidColorBrush(clrFailed);
            clrFailed.A = 150;
            brsConferenceFailed = new SolidColorBrush(clrFailed);

            // Degraded
            brsConferenceDegradedSolid = new SolidColorBrush(clrDegraded);
            brsConferenceDegradedBorder = new SolidColorBrush(Color.FromRgb((byte)(clrDegraded.R * .5f),
                                                                             (byte)(clrDegraded.G * .5f),
                                                                             (byte)(clrDegraded.B * .5f)));
            clrDegraded.A = 200;
            brsConferenceDegradedHover = new SolidColorBrush(clrDegraded);
            clrDegraded.A = 150;
            brsConferenceDegraded = new SolidColorBrush(clrDegraded);

            // Test
            brsConferenceTestSolid = new SolidColorBrush(clrTest);
            brsConferenceTestBorder = new SolidColorBrush(Color.FromRgb((byte)(clrTest.R * .5f),
                                                                        (byte)(clrTest.G * .5f),
                                                                        (byte)(clrTest.B * .5f)));
            clrTest.A = 200;
            brsConferenceTestHover = new SolidColorBrush(clrTest);
            clrTest.A = 210;
            brsConferenceTest = new SolidColorBrush(clrTest);

            // Ended
            brsConferenceEndedSolid = new SolidColorBrush(clrEnded);
            brsConferenceEndedBorder = new SolidColorBrush(Color.FromRgb((byte)(clrEnded.R * .5f),
                                                                         (byte)(clrEnded.G * .5f),
                                                                         (byte)(clrEnded.B * .5f)));
            clrEnded.A = 200;
            brsConferenceEndedHover = new SolidColorBrush(clrEnded);
            clrEnded.A = 150;
            brsConferenceEnded = new SolidColorBrush(clrEnded);

            penConferenceBorder = new Pen(brsConferenceBorder, 1);
            penConferenceDegradedBorder = new Pen(brsConferenceDegradedBorder, 1);
            penConferenceFailedBorder = new Pen(brsConferenceFailedBorder, 1);
            penConferenceTestBorder = new Pen(brsConferenceTestBorder, 1);
            penConferenceEndedBorder = new Pen(brsConferenceEndedBorder, 1);
            brsOverflow = new SolidColorBrush(clrOverflow);
            brsOverflowCheck = new SolidColorBrush(clrOverflowCheck);
            penStylus = new Pen(brsStylus, 1);
            penStylusFade = new Pen(brsStylusFade, 1.5);
            penCursor.Freeze();
            penStylus.Freeze();
            brsStylusFade.Freeze();

            brsConference.Freeze();
            brsConferenceHover.Freeze();
            brsConferenceSolid.Freeze();
            brsConferenceBorder.Freeze();
            penConferenceBorder.Freeze();

            brsConferenceFailed.Freeze();
            brsConferenceFailedHover.Freeze();
            brsConferenceFailedSolid.Freeze();
            brsConferenceFailedBorder.Freeze();
            penConferenceFailedBorder.Freeze();

            brsConferenceDegraded.Freeze();
            brsConferenceDegradedHover.Freeze();
            brsConferenceDegradedSolid.Freeze();
            brsConferenceDegradedBorder.Freeze();
            penConferenceDegradedBorder.Freeze();

            brsConferenceTest.Freeze();
            brsConferenceTestHover.Freeze();
            brsConferenceTestSolid.Freeze();
            brsConferenceTestBorder.Freeze();
            penConferenceTestBorder.Freeze();

            brsConferenceEnded.Freeze();
            brsConferenceEndedHover.Freeze();
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
        // Sometimes, right clicking on a conference without first making a selection will cause  currentConference to
        // be set to null, leading some button functions to fail. Always select the conference on a right click, if not
        // already selected.
        public int lastCurrentConferenceID = -1;
        public List<PageConferenceView.Conference> selectedConferences = new();

        protected override void OnRender(DrawingContext dc)
        {
            if (conferenceView == null)
                return;

            if (ActualWidth > 0)
            {
                DateTime now = DateTime.Now;

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
                            if (drawCursor && (Keyboard.IsKeyDown(Key.R)))
                            {
                                int hoveredRow = GetRowFromY(cursor!.Value.Y, true);
                                int resourceTopRow = GetRowFromY(GetYfromResource(ri.id, 0), true);
                                int resourceBottomRow = GetRowFromY(GetYfromResource(ri.id, ri.rowsTotal), true);
                                if (hoveredRow >= resourceTopRow && hoveredRow <= resourceBottomRow)
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
                                                                 new() { "Connections:  0", "Conferences:  0" },
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

                                                // Write messages to status bar.
                                                List<string> statusMessages = new()
                                                    {
                                                        $"Connections:  {connections}/{ri.connectionCapacity}",
                                                        $"Conferences:  {confs}/{ri.conferenceCapacity}"
                                                    };
                                                List<bool> bold = new()
                                                    {
                                                        connections > ri.connectionCapacity,
                                                        confs > ri.conferenceCapacity
                                                    };
                                                conferenceView.SetStatus(StatusContext.Overflow,
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
                                    conferenceView.SetStatus();
                            }
                        }
                    }

                    // Draw any dial number clash regions while we're here.
                    for (int i = 0; i < clashRegionAlternations.Count; i += 2)
                    {
                        DateTime s = clashRegionAlternations[i];
                        DateTime e = clashRegionAlternations[i + 1];

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
                            Rect r = new(startPoint, 0, endPoint - startPoint, ActualHeight);
                            dc.DrawRectangle(brsOverflow, null, r);
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

                // Draw the time.
                double timeX = (int)GetXfromDateTime(now, zoomTimeDisplay) + .5d;
                {
                    Pen penTime = penScheduleLineDay.Clone();
                    penTime.DashStyle = new DashStyle(new double[] { 3, 6 }, 0);
                    dc.DrawLine(penTime, new(timeX, 0d), new(timeX, ActualHeight));
                }

                // Draw conferences.
                bool resizeCursorSet = false;
                bool isMouseOverConference = false;

                bool dDown = Keyboard.IsKeyDown(Key.D);

                lock (conferences)
                    lock (clashIDs)
                    {
                        bool cursorOverConference = false;
                        Point cursorPoint = new();
                        if (cursor != null)
                            cursorPoint = cursor.Value;

                        foreach (var kvp in conferences)
                        {
                            PageConferenceView.Conference conference = kvp.Value;
                            if (conference.end > start && conference.start < end)
                            {
                                // Assemble the correct paint set :)
                                Brush textBrush = Brushes.Black;
                                Brush brush = brsConference;
                                Brush brushHover = brsConferenceHover;
                                Brush brushSolid = brsConferenceSolid;
                                Pen border = penConferenceBorder;
                                Pen borderSelected = new Pen(brsConferenceBorder, 2);
                                if (conference.test)
                                {
                                    brush = brsConferenceTest;
                                    brushHover = brsConferenceTestHover;
                                    brushSolid = brsConferenceTestSolid;
                                    border = penConferenceTestBorder;
                                    borderSelected.Brush = brsConferenceTestBorder;
                                }
                                if (conference.cancelled)
                                {
                                    brush = brsConferenceCancelled;
                                    brushHover = brsConferenceCancelledHover;
                                    brushSolid = brsConferenceSolid;
                                    border = penConferenceBorder;
                                    borderSelected.Brush = brsConferenceBorder;
                                }
                                if (conference.end < now && !conference.test && !conference.cancelled)
                                {
                                    if (conference.closure == "Succeeded" && !conference.hasUnclosedConnection)
                                    {
                                        brush = brsConferenceEnded;
                                        brushHover = brsConferenceEndedHover;
                                        brushSolid = brsConferenceEndedSolid;
                                        border = penConferenceEndedBorder;
                                        borderSelected.Brush = brsConferenceEndedBorder;
                                    }
                                    else if (conference.closure == "Degraded" && !conference.hasUnclosedConnection)
                                    {
                                        brush = brsConferenceDegraded;
                                        brushHover = brsConferenceDegradedHover;
                                        brushSolid = brsConferenceDegradedSolid;
                                        border = penConferenceDegradedBorder;
                                        borderSelected.Brush = brsConferenceDegradedBorder;
                                    }
                                    else if (conference.closure == "Failed" ||
                                             // Temporary, really one of these need their own colour.
                                             (dDown && clashIDs.Contains(conference.id)))
                                    {
                                        brush = brsConferenceFailed;
                                        brushHover = brsConferenceFailedHover;
                                        brushSolid = brsConferenceFailedSolid;
                                        border = penConferenceFailedBorder;
                                        borderSelected.Brush = brsConferenceFailedBorder;
                                    }
                                }

                                double startX = GetXfromDateTime(conference.start > start ? conference.start : start,
                                                                   zoomTimeDisplay);
                                double endX = GetXfromDateTime(conference.end < end ? conference.end : end,
                                                                 zoomTimeDisplay);
                                startX = startX < 0 ? (int)(startX - 1) : (int)startX;
                                endX = endX < 0 ? (int)(endX - 1) : (int)endX;

                                // If the conferences are off-screen, indicate this and skip the rest.
                                int startY = (int)GetYfromResource(conference.resourceID, conference.resourceRow);
                                if (startY >= ActualHeight - 4)
                                {
                                    dc.DrawRectangle(brushHover, null,
                                                     new Rect(startX, ActualHeight - 5, endX - startX, 5));
                                    continue;
                                }
                                else if (startY + zoomResourceDisplay < 4)
                                {
                                    dc.DrawRectangle(brushHover, null, new Rect(startX, 0, endX - startX, 5));
                                    continue;
                                }

                                // Never end need to start before -1, and never any need to proceed if end < 0.
                                if (startX < -1)
                                    startX = -1;
                                if (endX < 0)
                                    continue;

                                Rect area = new Rect(startX + .5, startY + .5,
                                                     ((int)endX - startX), (int)zoomResourceDisplay);

                                bool dragMove = conferenceView.drag == PageConferenceView.Drag.Move;
                                bool dragResize = conferenceView.drag == PageConferenceView.Drag.Resize;
                                bool thickBorder = false; // Needed to figure out some spacing below.
                                if (area.Contains(cursorPoint) ||
                                    ((dragMove || dragResize) && conference == currentConference))
                                {
                                    // Record this for switching off the status further down  if needed.
                                    cursorOverConference = true;
                                    lastCurrentConferenceID = conference.id;
                                    currentConference = conference;

                                    // Add
                                    string times;
                                    if (conference.start.Date == conference.end.Date)
                                        times = conference.start.ToString("hh:mm") + " - " +
                                                conference.end.ToString("hh:mm");
                                    else
                                        times = conference.start.ToString("dd/MM/yyyy hh:mm") + " - " +
                                                conference.end.ToString("dd/MM/yyyy hh:mm");
                                    if (conferenceView.statusBarContext == StatusContext.None)
                                        conferenceView.SetStatus(StatusContext.None,
                                            new() { conference.title,
                                                    times,
                                                    "Dial Count: " + conference.dialNos.Count.ToString()},
                                            new() { false, false, false });

                                    isMouseOverConference = true;
                                    if (selectedConferences.Contains(conference) ||
                                        boxSelectConferences.Contains(conference))
                                    {
                                        // Reduce the size slightly for drawing so the pen is on the inside.
                                        dc.DrawRectangle(brushSolid, borderSelected,
                                                         new Rect(area.X + .5d, area.Y + .5d,
                                                         area.Width - 1, area.Height - 1));
                                        thickBorder = true;
                                        textBrush = PrintBrushFromLuminance(brushSolid);
                                    }
                                    else
                                    {
                                        dc.DrawRectangle(brushHover, border, area);
                                        textBrush = PrintBrushFromLuminance(brushHover);
                                    }

                                    if ((cursorPoint.X < area.Left + 5 || cursorPoint.X > area.Right - 5) && !dragMove)
                                    {
                                        if (App.sd.editPermissions[Glo.PERMISSION_CONFERENCES])
                                            Cursor = Cursors.SizeWE;
                                        resizeCursorSet = true;
                                        if (!conferenceView.dragMouseHasMoved)
                                            conferenceView.resizeStart = cursorPoint.X < area.Left + 5;
                                    }
                                }
                                else if (selectedConferences.Contains(conference) ||
                                         boxSelectConferences.Contains(conference))
                                {
                                    // Reduce the size of the rect slightly for drawing so the pen is on the inside.
                                    dc.DrawRectangle(brushSolid, borderSelected,
                                        new Rect(area.X + .5d, area.Y + .5d,
                                                 area.Width - 1, area.Height - 1));
                                    thickBorder = true;
                                }
                                else
                                {
                                    dc.DrawRectangle(brush, border, area);
                                    textBrush = PrintBrushFromLuminance(brush);
                                }

                                Geometry clipGeometry = new RectangleGeometry(new(area.Left, area.Top,
                                    area.Width >= .5d ? area.Width - .5d : area.Width, area.Height));
                                dc.PushClip(clipGeometry);

                                // If the conference is currently running and is not cancelled, indicate this.
                                if (conference.start < now && conference.end > now && !conference.cancelled)
                                {
                                    Rect iconTray;
                                    if (thickBorder)
                                        iconTray = new Rect(area.Left + 1.5d, area.Top + 1.5d, 14d, area.Height - 3d);
                                    else
                                        iconTray = new Rect(area.Left + .5d, area.Top + .5d, 14d, area.Height - 1d);

                                    dc.DrawRectangle(brushSolid, null, iconTray);

                                    // Draw the play symbol.
                                    PathGeometry play = symbolPlay.Clone();
                                    TranslateTransform translateTransform = new(startX + 4.5d, startY + 5d);
                                    play.Transform = translateTransform;
                                    dc.DrawGeometry(PrintBrushFromLuminance(brushSolid), null, play);

                                    startX += 14d; // Adjust start for drawing text.
                                }

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

                                    dc.DrawText(title, new(startX + 5, startY + 2));

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
                                        dc.DrawText(time, new(startX + 5, startY + zoomResourceSensitivity));
                                    }
                                }

                                dc.Pop();
                            }
                        }

                        if (!cursorOverConference && conferenceView.statusBarContext == StatusContext.None)
                            conferenceView.SetStatus();
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

                // Draw the box select.
                if (conferenceView.drag == PageConferenceView.Drag.BoxSelect)
                {
                    double startX;
                    double endX;
                    double startY;
                    double endY;
                    // Switch the starts and ends the right way round if needed.
                    if (boxSelectFromX > boxSelectToX)
                    {
                        startX = GetXfromDateTime(boxSelectToX, zoomTimeDisplay);
                        endX = GetXfromDateTime(boxSelectFromX, zoomTimeDisplay);
                    }
                    else
                    {
                        startX = GetXfromDateTime(boxSelectFromX, zoomTimeDisplay);
                        endX = GetXfromDateTime(boxSelectToX, zoomTimeDisplay);
                    }
                    if (boxSelectFromY > boxSelectToY)
                    {
                        startY = (boxSelectToY * zoomResourceDisplay) - scroll;
                        endY = (boxSelectFromY * zoomResourceDisplay) - scroll;
                    }
                    else
                    {
                        startY = (boxSelectFromY * zoomResourceDisplay) - scroll; ;
                        endY = (boxSelectToY * zoomResourceDisplay) - scroll; ;
                    }

                    endY += zoomResourceDisplay;

                    dc.DrawRectangle(brsOverflowCheck, null, new Rect(startX, startY, endX - startX, endY - startY));
                }

                // Draw the dial clash tooltip.
                if (cursor != null && Keyboard.IsKeyDown(Key.D) && currentConference != null)
                {
                    PageConferenceView.Conference cc = currentConference;
                    HashSet<string> clashingDialNos = new();
                    foreach (PageConferenceView.Conference c in conferences.Values)
                        if (cc.end > c.start && cc.start < c.end && cc.id != c.id)
                            foreach (string s in c.dialNos)
                                if (cc.dialNos.Contains(s) && !clashingDialNos.Contains(s))
                                    clashingDialNos.Add(s);

                    List<string> status = new();
                    List<bool> bold = new();

                    // First add the clashes in bold.
                    if (clashingDialNos.Count > 0)
                    {
                        status.Add("Clashes:  " + string.Join(", ", clashingDialNos));
                        bold.Add(true);
                    }

                    // Then add the rest as normal.
                    List<string> notClashing = new();
                    foreach (string s in cc.dialNos)
                        if (!clashingDialNos.Contains(s))
                            notClashing.Add(s);
                    if (notClashing.Count > 0)
                    {
                        status.Add((clashingDialNos.Count > 0 ? "Others:  " : "Dial Nos:  ") +
                                   string.Join(", ", notClashing));
                        bold.Add(false);
                    }

                    conferenceView.SetStatus(StatusContext.Clash, status, bold);
                }
                else if (conferenceView.statusBarContext == StatusContext.Clash)
                    conferenceView.SetStatus();
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
            if (lastScrollResource > 1 + (zoomResourceCurrent * conferenceView!.totalRows) - ActualHeight)
                lastScrollResource = 1 + (zoomResourceCurrent * conferenceView!.totalRows) - ActualHeight;
            if (lastScrollResource < 0) lastScrollResource = 0;
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

        public static Color PrintColorFromLuminance(Color c)
        {
            // The following assumes a background colour of white.
            Color colorBetween = Color.FromRgb((byte)(c.R + (255 - c.R) * (1d - (c.A / 255d))),
                                               (byte)(c.G + (255 - c.G) * (1d - (c.A / 255d))),
                                               (byte)(c.B + (255 - c.B) * (1d - (c.A / 255d))));
            return 0.2126d * (colorBetween.R / 255d) +
                   0.7152d * (colorBetween.G / 255d) +
                   0.0722d * (colorBetween.B / 255d) > .5d ? Colors.Black : Colors.White;
        }
        public static SolidColorBrush PrintBrushFromLuminance(Brush b)
        { return new SolidColorBrush(PrintColorFromLuminance(((SolidColorBrush)b).Color)); }
    }
}
