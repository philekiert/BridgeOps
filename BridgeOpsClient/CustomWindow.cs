using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;

namespace BridgeOpsClient
{
    // Partially adapted from the wonderful XAML code posted by David Rickard at:
    // https://engy.us/blog/2020/01/01/implementing-a-custom-window-title-bar-in-wpf/

    public class CustomWindow : Window
    {
        // Known bug: MouseLeave doesn't fire, and likewise, IsMouseOver doens't set to false, if moving straight from a
        // title bar button when the window was displayed with ShowDialog(). There are people online with the same issue,
        // maybe come back and fix it another time as it's not crucial. Here is a hacky solution.
        static DispatcherTimer? tmrWindowLeaveDetect;
        private void TmrWindowLeaveDetect_Tick(object? sender, EventArgs e)
        {
            foreach (Window win in Application.Current.Windows)
                if (win is CustomWindow cw)
                {
                    System.Drawing.Point mousePosition = System.Windows.Forms.Cursor.Position;
                    cw.UnhoverTitleBarButtons(cw.WindowState != WindowState.Maximized &&
                    (mousePosition.X < cw.Left || mousePosition.X > cw.Left + cw.ActualWidth ||
                     mousePosition.Y < cw.Top || mousePosition.Y > cw.Left + cw.ActualHeight));
                }
        }
        const double borderRadius = 7d;
        public const double titleBarHeight = 26;

        private Border windowBorder;
        private Grid grid;
        private Border menuBar;
        private Border titleBar;
        private Border minimiseButton;
        private Border maximiseButton;
        private Border closeButton;

        private double hoverOpacity = .75d;
        private double clickOpacity = .5d;

        public CustomWindow()
        {
            Activated += CustomWindow_Activated;

            // Only instantiate once.
            if (tmrWindowLeaveDetect == null)
            {
                tmrWindowLeaveDetect = new(DispatcherPriority.Render)
                { Interval = new TimeSpan(100_000), /* 1/100 second. */ };
                tmrWindowLeaveDetect.Tick += TmrWindowLeaveDetect_Tick;
                tmrWindowLeaveDetect.Start();
            }

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            UseLayoutRounding = true;

            // Set WindowChrome properties
            var windowChrome = new WindowChrome
            {
                // I have no idea why - 5, but it overshoots otherwise. Possible the border thickness size is included.
                CaptionHeight = titleBarHeight - 5,
                ResizeBorderThickness = new Thickness(6)
            };
            WindowChrome.SetWindowChrome(this, windowChrome);

            // This hard coding of the dictionary index is not ideal, but I can't load the resources from FindName here
            // for some reason. Might need updating if more dictionaries are added.
            var resources = Application.Current.Resources.MergedDictionaries[0];

            windowBorder = new()
            {
                CornerRadius = new CornerRadius(borderRadius),
                BorderBrush = (Brush)resources["brushWindowBorder"],
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                ClipToBounds = true
            };

            grid = new();
            grid.RowDefinitions.Add(new() { Height = new GridLength(titleBarHeight) });
            grid.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new() { Width = new GridLength(titleBarHeight) });
            grid.ColumnDefinitions.Add(new() { Width = new GridLength(titleBarHeight) });
            grid.ColumnDefinitions.Add(new() { Width = new GridLength(titleBarHeight) });
            windowBorder.Child = grid;

            FontFamily robotoMono = new FontFamily(new Uri("pack://application:,,,/"),
                                                           "./Resources/Fonts/Roboto Mono/#Roboto Mono");
            // Set up the title bar.
            titleBar = new()
            {
                Height = titleBarHeight,
                CornerRadius = new CornerRadius(borderRadius, 0, 0, 0),
                Background = resources["brushTitleBar"] as Brush,
                Margin = new Thickness(0, 0, 0, 0),
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = resources["brushTitleBarBorder"] as Brush
            };
            grid.Children.Add(titleBar);

            menuBar = new()
            {
                Height = titleBarHeight,
                Background = resources["brushTitleBar"] as Brush,
                Margin = new Thickness(0, 0, 0, 0),
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = resources["brushTitleBarBorder"] as Brush,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            Grid.SetColumn(menuBar, 1);
            grid.Children.Add(menuBar);

            Path minimisePath = new()
            {
                Fill = (Brush)resources["brushPrimaryButton"],
                Data = new RectangleGeometry(new Rect(8, 16, 10, 3))
            };
            CombinedGeometry combinedGeometry = new(GeometryCombineMode.Exclude,
                                                    new RectangleGeometry(new Rect(8, 8, 10, 11)),
                                                    new RectangleGeometry(new Rect(9, 11, 8, 7)));
            Path maximisePath = new()
            {
                Fill = (Brush)resources["brushPrimaryButton"],
                Data = combinedGeometry
            };

            TextBlock closeText = new()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)resources["brushCloseButton"],
                FontFamily = robotoMono,
                FontSize = 21,
                FontWeight = FontWeights.Black,
                Margin = new Thickness(-1, -3, 0, 0),
                Text = "x"
            };
            minimiseButton = new()
            {
                Height = titleBarHeight,
                Width = titleBarHeight,
                Background = (Brush)resources["brushMinimise"],
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = resources["brushTitleBarBorder"] as Brush,
                Child = minimisePath,
                Margin = new Thickness(0, 0, 0, 0)
            };
            maximiseButton = new()
            {
                Height = titleBarHeight,
                Width = titleBarHeight,
                Background = (Brush)resources["brushMaximise"],
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = resources["brushTitleBarBorder"] as Brush,
                Child = maximisePath,
                Margin = new Thickness(0, 0, 0, 0)
            };
            closeButton = new()
            {
                CornerRadius = new CornerRadius(0, borderRadius, 0, 0),
                Height = titleBarHeight,
                Width = titleBarHeight,
                Background = (Brush)resources["brushClose"],
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = resources["brushTitleBarBorder"] as Brush,
                Child = closeText,
                Margin = new Thickness(0, 0, 0, 0),
            };

            WindowChrome.SetIsHitTestVisibleInChrome(minimiseButton, true);
            WindowChrome.SetIsHitTestVisibleInChrome(maximiseButton, true);
            WindowChrome.SetIsHitTestVisibleInChrome(closeButton, true);

            minimiseButton.MouseUp += MinimiseButton_MouseUp;
            minimiseButton.MouseDown += MinimiseButton_MouseDown;
            minimiseButton.MouseEnter += Button_MouseEnter;
            minimiseButton.MouseLeave += Button_MouseLeave;
            maximiseButton.MouseUp += MaximiseButton_MouseUp;
            maximiseButton.MouseDown += MaximiseButton_MouseDown;
            maximiseButton.MouseEnter += Button_MouseEnter;
            maximiseButton.MouseLeave += Button_MouseLeave;
            closeButton.PreviewMouseUp += CloseButton_MouseUp;
            closeButton.MouseDown += CloseButton_MouseDown;
            closeButton.MouseEnter += Button_MouseEnter;
            closeButton.MouseLeave += Button_MouseLeave;
            PreviewMouseUp += CustomWindow_PreviewMouseUp;
            MouseUp += CustomWindow_MouseUp;

            Grid.SetColumn(minimiseButton, 2);
            Grid.SetColumn(maximiseButton, 3);
            Grid.SetColumn(closeButton, 4);
            grid.Children.Add(minimiseButton);
            grid.Children.Add(maximiseButton);
            grid.Children.Add(closeButton);

            TextBlock title = new()
            {
                Foreground = Brushes.Black,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(10, 0, 0, 0),
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };

            titleBar.Child = title;

            Content = windowBorder;

            Loaded += CustomWindow_Loaded;
        }

        private void UnhoverTitleBarButtons(bool unhover)
        {
            if (unhover)
            {
                minimiseButton.Opacity = 1;
                maximiseButton.Opacity = 1;
                closeButton.Opacity = 1;
                return;
            }
            if (minimiseButton.IsMouseOver)
                minimiseButton.Opacity = Mouse.LeftButton == MouseButtonState.Pressed &&
                                         buttonPressed == 0 ? clickOpacity :
                                                              hoverOpacity;
            if (maximiseButton.IsMouseOver)
                maximiseButton.Opacity = Mouse.LeftButton == MouseButtonState.Pressed &&
                                         buttonPressed == 1 ? clickOpacity :
                                                              hoverOpacity;
            if (closeButton.IsMouseOver)
                closeButton.Opacity = Mouse.LeftButton == MouseButtonState.Pressed &&
                                      buttonPressed == 2 ? clickOpacity :
                                                           hoverOpacity;
        }

        public void CentreTitle(bool centre)
        {
            ((TextBlock)grid.Children[0]).HorizontalAlignment = centre ? HorizontalAlignment.Center :
                                                                         HorizontalAlignment.Left;
            Grid.SetColumnSpan(grid.Children[0], centre ? 5 : 1);
        }
        public void AssignMenuBar(Menu menu, double topPadding)
        {
            menuBar.Child = menu;
            menuBar.Padding = new(15, topPadding, 0, 0);
            WindowChrome.SetIsHitTestVisibleInChrome(menuBar, false);
            foreach (IInputElement iie in menu.Items)
                WindowChrome.SetIsHitTestVisibleInChrome(iie, true);
        }

        private void Button_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ((Border)sender).Opacity = hoverOpacity;
        }

        private void Button_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ((Border)sender).Opacity = 1d;
        }

        int buttonPressed = -1;

        private void CustomWindow_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        { buttonPressed = -1; }
        private void CustomWindow_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            grid.Children[3].ClearValue(OpacityProperty);
            grid.Children[4].ClearValue(OpacityProperty);
            grid.Children[5].ClearValue(OpacityProperty);
        }

        private void MinimiseButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        { buttonPressed = 0; ((Border)sender).Opacity = clickOpacity; }
        private void MinimiseButton_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (buttonPressed == 0)
                WindowState = WindowState.Minimized;
            buttonPressed = -1;
        }

        private void MaximiseButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        { buttonPressed = 1; ((Border)sender).Opacity = clickOpacity; }
        private void MaximiseButton_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (buttonPressed == 1)
            {
                if (WindowState == WindowState.Maximized)
                    WindowState = WindowState.Normal;
                else if (WindowState == WindowState.Normal)
                {
                    WindowState = WindowState.Maximized;
                    MaxHeight = SystemParameters.MaximumWindowTrackHeight;
                }
            }
            buttonPressed = -1;
        }

        private void CloseButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        { buttonPressed = 2; ((Border)sender).Opacity = clickOpacity; }
        private void CloseButton_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (buttonPressed == 2)
                Close();
            buttonPressed = -1;
        }

        private void CustomWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Create a content presenter for the second row
            ContentPresenter contentPresenter = new();
            Grid.SetRow(contentPresenter, 1);
            Grid.SetColumnSpan(contentPresenter, 5);
            grid.Children.Add(contentPresenter);

            // This has to be done after load, or the content defined in XAML just overrides the grid.
            object initialContent = Content;
            contentPresenter.Content = initialContent;
            Content = windowBorder;

            // Carry out some other tasks after load to take advantage of properties set in XAML.
            ((TextBlock)titleBar.Child).Text = Title;
            if (ResizeMode == ResizeMode.CanMinimize)
                grid.ColumnDefinitions[3].Width = new GridLength(0);
            if (ResizeMode == ResizeMode.NoResize)
            {
                grid.ColumnDefinitions[2].Width = new GridLength(0);
                grid.ColumnDefinitions[3].Width = new GridLength(0);
                ((Border)grid.Children[4]).Background = ((Border)grid.Children[4]).Background;
            }
        }

        private void CustomWindow_Activated(object? sender, EventArgs e)
        {
            foreach (Window w in Application.Current.Windows)
            {
                if (w.Owner == this)
                    w.Activate();
            }
        }

        // All code below is slightly modified from David Rickard's. It handles maximised window placement.
        // The original code had an issue where the taskbar wouldn't display if set to auto-hide. This is fixed here,
        // but bear in mind that it only works when not debugging for some reason I can't prioritise getting to the
        // bottom of.

        #region Maximise logic

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            ((HwndSource)PresentationSource.FromVisual(this)).AddHook(HookProc);
        }

        public static IntPtr HookProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                // We need to tell the system what our size should be when maximized. Otherwise it will cover the whole screen,
                // including the task bar.
                MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO))!;

                // Adjust the maximized size and position to fit the work area of the correct monitor
                IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

                if (monitor != IntPtr.Zero)
                {
                    MONITORINFO monitorInfo = new MONITORINFO();
                    monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                    GetMonitorInfo(monitor, ref monitorInfo);
                    RECT rcWorkArea = monitorInfo.rcWork;
                    RECT rcMonitorArea = monitorInfo.rcMonitor;

                    // Check if the taskbar is set to auto-hide
                    APPBARDATA appBarData = new APPBARDATA();
                    appBarData.cbSize = Marshal.SizeOf(typeof(APPBARDATA));
                    int autoHide = (int)SHAppBarMessage(ABM_GETSTATE, ref appBarData);

                    // If the taskbar is set to auto-hide, leave a few pixels at the bottom
                    if ((autoHide & ABS_AUTOHIDE) == ABS_AUTOHIDE)
                    {
                        rcWorkArea.Bottom -= 1; // Adjust this value as needed
                    }

                    mmi.ptMaxPosition.X = Math.Abs(rcWorkArea.Left - rcMonitorArea.Left);
                    mmi.ptMaxPosition.Y = Math.Abs(rcWorkArea.Top - rcMonitorArea.Top);
                    mmi.ptMaxSize.X = Math.Abs(rcWorkArea.Right - rcWorkArea.Left);
                    mmi.ptMaxSize.Y = Math.Abs(rcWorkArea.Bottom - rcWorkArea.Top);
                }

                Marshal.StructureToPtr(mmi, lParam, true);
            }

            return IntPtr.Zero;
        }

        private const int WM_GETMINMAXINFO = 0x0024;
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        private const int ABS_AUTOHIDE = 0x1;
        private const int ABM_GETSTATE = 0x4;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("shell32.dll")]
        private static extern IntPtr SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public int lParam;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public RECT(int left, int top, int right, int bottom)
            {
                this.Left = left;
                this.Top = top;
                this.Right = right;
                this.Bottom = bottom;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        #endregion
    }
}
