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

    public class ScheduleRuler : Canvas
    {
        public ScheduleRuler() { ClipToBounds = true; }

        public ScheduleView? view;
        public ScheduleResources? res;

        Typeface segoeUI = new("Segoe UI");
        Typeface segoeUISemiBold = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold,
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

                TimeSpan half = new(0, (int)viewHalfHours, (int)viewHalfMinutes, viewHalfSeconds);

                DateTime start = view.scheduleTime - half;
                // Overshoot so text doesn't cut disappear early when scrolling.
                DateTime end = view.scheduleTime + half.Add(new(1, 0, 0));

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
}
