﻿using System;
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

        public TimeSpan? GetTime()
        {
            int hour, minute;
            if (!int.TryParse(txtHour.Text, out hour))
                return null;
            if (!int.TryParse(txtMinute.Text, out minute))
                return null;
            return new TimeSpan(hour, minute, 0);
        }

        public void ToggleEnabled(bool enabled)
        {
            txtHour.IsReadOnly = !enabled;
            txtMinute.IsReadOnly = !enabled;
        }

        public void SetTime(long ticks) { SetTime(new TimeSpan(ticks)); }
        public void SetTime(TimeSpan timeSpan)
        {
            txtHour.Text = timeSpan.Hours.ToString();
            txtMinute.Text = timeSpan.Minutes.ToString();
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
            // This used to forcefully set a value if the TextBox was empty, but we want to be able to set null values.
            while (((TextBox)sender).Text.Length < 2 && ((TextBox)sender).Text.Length != 0)
                ((TextBox)sender).Text = '0' + ((TextBox)sender).Text;
        }
    }
}
