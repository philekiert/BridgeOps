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
    public partial class DateTimePicker : UserControl
    {
        public DateTimePicker()
        {
            InitializeComponent();
        }

        public void ToggleEnabled(bool enabled)
        {
            datePicker.IsEnabled = enabled;
            timePicker.ToggleEnabled(enabled);
        }

        public DateTime? GetDateTime()
        {
            return GetDateTime(0);
        }
        public DateTime? GetDate()
        {
            return GetDateTime(1);
        }
        public DateTime? GetTime()
        {
            return GetDateTime(2);
        }
        private DateTime? GetDateTime(int which)
        {
            // which: 0 DateTime
            //        1 Date
            //        2 Time

            TimeSpan? time = timePicker.GetTime();
            if (time == null)
                return null;
            if (datePicker.SelectedDate == null)
                return null;

            if (which == 0)
                return (dateVisible ? (DateTime)datePicker.SelectedDate : new DateTime()).Add((TimeSpan)time);
            else if (which == 1 && dateVisible)
                return (DateTime)datePicker.SelectedDate;
            else if (which == 2)
                return new DateTime().Add((TimeSpan)time);

            return null;
        }

        public void SetDateTime(DateTime dt)
        {
            datePicker.SelectedDate = dt.Date;
            timePicker.txtHour.Text = dt.Hour < 10 ? "0" + dt.Hour.ToString() : dt.Hour.ToString();
            timePicker.txtMinute.Text = dt.Minute < 10 ? "0" + dt.Minute.ToString() : dt.Minute.ToString();
        }

        bool dateVisible = true;
        public void ToggleDatePicker(bool show)
        {
            grd.ColumnDefinitions[0].Width = show ? new GridLength(110) : new GridLength(0);
            dateVisible = show;
        }
    }
}
