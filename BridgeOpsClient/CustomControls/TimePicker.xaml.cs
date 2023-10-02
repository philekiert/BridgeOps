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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BridgeOpsClient.CustomControls
{
    public partial class TimePicker : UserControl
    {
        public TimePicker()
        {
            InitializeComponent();
        }

        public TimeSpan GetTime()
        {
            // The text box itself can only ever hold a number, so no need to handle potential errors.
            int hour, minute;
            int.TryParse(txtHour.Text, out hour);
            int.TryParse(txtMinute.Text, out minute);
            return new TimeSpan(hour, minute, 0);
        }

        private void txt_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            int value;
            if (!int.TryParse(e.Text, out value))
                e.Handled = true; // Disallow and cancel if this would make the text alphanumeric.
            // Restrict to 0-59.
            if (value > 59)
            {
                e.Handled = true;
                ((TextBox)sender).Text = "59";
            }
            else if (value < 0)
            {
                // Restrict to 59.
                e.Handled = true;
                ((TextBox)sender).Text = "0";
            }
        }

        private void txt_TextChanged(object sender, TextChangedEventArgs e)
        {
            int value;
            if (int.TryParse(((TextBox)sender).Text, out value))
            {
                // Shouldn't trigger if the user has erased both characters.
                if (value > 59)
                    ((TextBox)sender).Text = "59";
                else if (value < 0)
                    ((TextBox)sender).Text = "00";
            }
        }

        private void txt_LostFocus(object sender, RoutedEventArgs e)
        {
            while (((TextBox)sender).Text.Length < 2)
                ((TextBox)sender).Text = '0' + ((TextBox)sender).Text;

        }
    }
}
