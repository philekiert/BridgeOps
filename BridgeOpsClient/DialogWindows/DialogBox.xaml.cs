using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace BridgeOpsClient.DialogWindows
{
    public partial class DialogBox : CustomWindow
    {
        public enum Buttons { YesNo, OKCancel }

        public DialogBox(string message)
        {
            InitializeComponent();

            txtMessage.Text = message;

            if (message.Length > 100)
            {
                // Expand dynamically if it's a long message.
                int messageDif = message.Length - 100;
                MinWidth = MinWidth + ((MaxWidth - MinWidth) * (messageDif / 200f));
            }
        }
        public DialogBox(string message, string title) : this(message)
        {
            Title = title;
            Grid.SetColumnSpan(btnOkay, 2);
            btnCancel.Visibility = Visibility.Hidden;
            btnOkay.HorizontalAlignment = HorizontalAlignment.Center;
        }
        public DialogBox(string message, string title, Buttons buttons) : this(message, title)
        {
            if (buttons == Buttons.YesNo)
            {
                btnOkay.Content = "Yes";
                btnCancel.Content = "No";
            }
            Grid.SetColumnSpan(btnOkay, 1);
            btnCancel.Visibility = Visibility.Visible;
            btnOkay.HorizontalAlignment = HorizontalAlignment.Right;
        }

        private void Set(bool isTrue)
        {
            try
            { DialogResult = true; }
            catch { } // No need to handle this.
        }

        private void btnOkay_Click(object sender, RoutedEventArgs e)
        {
            Set(true);
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        { Close(); } // DialogResult is false by default.

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && btnCancel.Visibility != Visibility.Visible)
            {
                Set(true);
                Close();
            }
        }
    }
}
