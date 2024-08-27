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
            TimeSpan? time = timePicker.GetTime();
            if (time == null)
                return null;
            if (datePicker.SelectedDate == null)
                return null;

            return ((DateTime)datePicker.SelectedDate).Add((TimeSpan)time);
        }

        public void SetDateTime(DateTime dt)
        {
            datePicker.SelectedDate = dt.Date;
            timePicker.txtHour.Text = dt.Hour < 10 ? "0" + dt.Hour.ToString() : dt.Hour.ToString();
            timePicker.txtMinute.Text = dt.Minute < 10 ? "0" + dt.Minute.ToString() : dt.Minute.ToString();
        }
    }
}
