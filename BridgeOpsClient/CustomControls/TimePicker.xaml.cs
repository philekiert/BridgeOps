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
        private int maxHours = 23;
        private int maxMinutes = 59;
        public void SetMaxValues(int hours, int minutes)
        {
            maxHours = Math.Clamp(hours, 0, 99);
            maxMinutes = Math.Clamp(minutes, 0, 59);
        }

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

        private void txtMinutes_TextChanged(object sender, TextChangedEventArgs e)
        { EnforceValueRestriction((TextBox)sender, maxHours); }

        private void txtHours_TextChanged(object sender, TextChangedEventArgs e)
        { EnforceValueRestriction((TextBox)sender, maxHours); }

        private void EnforceValueRestriction(TextBox textBox, int max)
        {
            int value;
            if (int.TryParse(textBox.Text, out value))
            {
                // Shouldn't trigger if the user has erased both characters.
                if (value > max)
                    textBox.Text = max.ToString();
                else if (value < 0)
                    textBox.Text = "00";
            }
        }

        private void txt_LostFocus(object sender, RoutedEventArgs e)
        {
            while (((TextBox)sender).Text.Length < 2)
                ((TextBox)sender).Text = '0' + ((TextBox)sender).Text;
        }
    }
}
