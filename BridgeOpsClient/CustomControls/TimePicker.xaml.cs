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

    // This picker only supports times up to 23:59. If you need more than 23 hours, you will need to amend the
    // txt_LostFocus() method to handle days, as TimeSpan.TryParse needs the string broken down into
    // days:hours:minutes.

    public partial class TimePicker : UserControl
    {
        private TimeSpan max = new(23, 59, 0);
        public void SetMaxValue(TimeSpan max)
        {
            if (max.TotalHours > 23)
                max = new(23, max.Minutes, 0);
            else if (max < new TimeSpan(0, 2, 0))
                max = new TimeSpan(0, 1, 0);
            this.max = max;
        }

        public TimePicker()
        {
            InitializeComponent();
        }

        public TimeSpan? GetTime()
        {
            TimeSpan time;
            if (TimeSpan.TryParse(txt.Text, out time))
                return time;
            else
                return null;
        }

        public void ToggleEnabled(bool enabled)
        {
            txt.IsReadOnly = !enabled;
            txt.IsReadOnly = !enabled;
        }

        public void SetTime(long ticks) { SetTime(new TimeSpan(ticks)); }
        public void SetTime(TimeSpan timeSpan)
        {
            txt.Text = timeSpan.ToString("hh\\:mm");
            EnforceValueRestriction();
        }

        private void EnforceValueRestriction()
        {
            TimeSpan time;
            if (!TimeSpan.TryParse(txt.Text, out time))
                return;

            TimeSpan correctedTime = time;
            if (time > max)
                time = max;
            if (time < new TimeSpan(0, 1, 0))
                time = new(0, 1, 0);

            if (time != correctedTime)
            {
                int selection = txt.SelectionStart;
                txt.Text = $"{correctedTime.TotalHours:00}:{correctedTime.Minutes}";
                // Shouldn't trigger if the user has erased both characters.
                txt.SelectionStart = selection;
            }

        }

        private void txt_LostFocus(object sender, RoutedEventArgs e)
        {
            // If this isn't here and we had stuff highlighted when we lost focus, the next mouse down will reselect.
            txt.SelectionLength = 0;

            // Tidy up if possible, otherwise set to blank.

            string check = txt.Text;

            // If someone types "1", for example, change that to an hour selection. This is all way more comprehensive
            // than it needs to be, but I love it.
            int val;
            if (!check.Contains(':') && int.TryParse(check, out val) && val >= 0)
            {
                if (val < 24)
                    check += ":00";
                else if (val < 60)
                    check = "00:" + check;
                else if (val < 100)
                    check = check.Insert(1, ":");
                else if (val < 1000)
                {
                    if (val % 100 < 60)
                        check = check.Insert(1, ":");
                    else
                        check = check.Insert(2, ":");
                }
                else
                    check = check.Insert(2, ":");
            }

            if (check.Contains(":"))
            {
                string[] parts = check.Split(':');
                if (parts.Length != 2)
                {
                    txt.Text = "";
                    return;
                }
                if (int.TryParse(parts[0], out val) && val > max.Hours)
                    parts[0] = max.Hours.ToString();
                if (int.TryParse(parts[1], out val) && val > max.Minutes)
                    parts[1] = max.Minutes.ToString();
                check = parts[0] + ":" + parts[1];
            }

            TimeSpan ts;
            if (!TimeSpan.TryParse(check, out ts))
            {
                txt.Text = "";
                return;
            }
            if (ts < TimeSpan.Zero)
            {
                txt.Text = "";
                return;
            }

            if (ts > max)
                ts = max;

            txt.Text = $"{ts.Hours:00}:{ts.Minutes:00}";
        }

        private void txt_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!int.TryParse(e.Text, out _) && e.Text != ":")
                e.Handled = true;
        }

        private void txt_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (Mouse.LeftButton != MouseButtonState.Pressed)
                txt.SelectAll();
        }

        private void txt_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            txt.SelectAll();
        }
    }
}
