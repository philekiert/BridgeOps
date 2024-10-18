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

    public class ScheduleResources : Canvas
    {
        public PageConferenceView? conferenceView;

        public ScheduleView? view;

        Typeface segoeUI = new("Segoe UI");

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
                if (view.cursor != null && view.cursor.Value.X != -1 && view.cursor.Value.Y != -1)
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
}
