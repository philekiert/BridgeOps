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
        private TimeSpan max = new(23, 59, 0);
        public void SetMaxValue(TimeSpan max)
        {
            if (max.TotalHours > 99)
                max = new(99, max.Minutes, 0);
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
            // This used to forcefully set a value if the TextBox was empty, but we want to be able to set null values.
            while (((TextBox)sender).Text.Length < 2 && ((TextBox)sender).Text.Length != 0)
                ((TextBox)sender).Text = '0' + ((TextBox)sender).Text;
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
    }
}
