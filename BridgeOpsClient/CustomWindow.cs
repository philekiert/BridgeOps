using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;

namespace BridgeOpsClient
{
    // Partially adapted from the wonderful XAML code posted by David Rickard at:
    // https://engy.us/blog/2020/01/01/implementing-a-custom-window-title-bar-in-wpf/

    public class CustomWindow : Window
    {
        private Grid grid;
        private TextBlock title;
        private Border minimiseButton;
        private Border maximiseButton;
        private Border closeButton;

        public CustomWindow()
        {
            WindowStyle = WindowStyle.None;

            // Set WindowChrome properties
            var windowChrome = new WindowChrome
            {
                CaptionHeight = 30,
                ResizeBorderThickness = SystemParameters.WindowResizeBorderThickness
            };
            WindowChrome.SetWindowChrome(this, windowChrome);

            grid = new();
            grid.RowDefinitions.Add(new() { Height = new GridLength(30) });
            grid.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Auto) });

            minimiseButton = new()
            {
                Width = 30,
                Background = Brushes.Gray
            };
            maximiseButton = new()
            {
                Width = 30,
                Background = Brushes.LightGray
            };
            closeButton = new()
            {
                Width = 30,
                Background = Brushes.Gray
            };

            WindowChrome.SetIsHitTestVisibleInChrome(minimiseButton, true);
            WindowChrome.SetIsHitTestVisibleInChrome(maximiseButton, true);
            WindowChrome.SetIsHitTestVisibleInChrome(closeButton, true);

            minimiseButton.MouseUp += MinimiseButton_MouseUp;
            minimiseButton.MouseDown += MinimiseButton_MouseDown;
            maximiseButton.MouseUp += MaximiseButton_MouseUp;
            maximiseButton.MouseDown += MaximiseButton_MouseDown;
            closeButton.MouseUp += CloseButton_MouseUp;
            closeButton.MouseDown += CloseButton_MouseDown;

            Grid.SetColumn(minimiseButton, 1);
            Grid.SetColumn(maximiseButton, 2);
            Grid.SetColumn(closeButton, 3);
            grid.Children.Add(minimiseButton);
            grid.Children.Add(maximiseButton);
            grid.Children.Add(closeButton);

            // Set up the title bar.
            title = new()
            {
                Foreground = Brushes.Black,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(10, 0, 0, 0),
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };
            Content = grid;
            Grid.SetRow(title, 0);
            grid.Children.Add(title);

            Loaded += CustomWindow_Loaded;
        }

        int buttonPressed = -1;

        private void MinimiseButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        { buttonPressed = 0; }
        private void MinimiseButton_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (buttonPressed == 0)
                WindowState = WindowState.Minimized;
            buttonPressed = -1;
        }

        private void MaximiseButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        { buttonPressed = 1; }
        private void MaximiseButton_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (buttonPressed == 1)
            {
                if (WindowState == WindowState.Maximized)
                    WindowState = WindowState.Normal;
                else if (WindowState == WindowState.Normal)
                    WindowState = WindowState.Maximized;
            }
            buttonPressed = -1;
        }

        private void CloseButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        { buttonPressed = 2; }
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
            Grid.SetColumnSpan(contentPresenter, 4);
            grid.Children.Add(contentPresenter);

            // This has to be done after load, or the content defined in XAML just overrides the grid.
            object initialContent = Content;
            contentPresenter.Content = initialContent;
            Content = grid;

            // Carry out some other tasks after load to take advantage of properties set in XAML.
            title.Text = Title;
        }
    }
}
