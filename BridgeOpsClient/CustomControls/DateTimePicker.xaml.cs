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

            // If the date is null, return null unless only the time was requested, and vice versa.
            TimeSpan? time = timePicker.GetTime();
            if (time == null && which != 1)
                return null;
            if (datePicker.SelectedDate == null && which != 2)
                return null;

            if (which == 0)
                return (dateVisible || datePicker.SelectedDate != null ? (DateTime)datePicker.SelectedDate! :
                                                                         new DateTime()).Add((TimeSpan)time!);
            else if (which == 1 && dateVisible)
                return (DateTime)datePicker.SelectedDate!;
            else if (which == 2)
                return new DateTime().Add((TimeSpan)time!);

            return null;
        }

        public void SetDateTime(DateTime dt)
        {
            datePicker.SelectedDate = dt.Date;
            timePicker.txt.Text = (dt.Hour < 10 ? "0" + dt.Hour.ToString() : dt.Hour.ToString()) + ":" +
                                  (dt.Minute < 10 ? "0" + dt.Minute.ToString() : dt.Minute.ToString());
        }

        bool dateVisible = true;
        public bool DateVisible { get { return dateVisible; } }
        public void ToggleDatePicker(bool show)
        {
            grd.ColumnDefinitions[0].Width = show ? new GridLength(110) : new GridLength(0);
            dateVisible = show;
            datePicker.Focusable = show;
        }
    }
}
