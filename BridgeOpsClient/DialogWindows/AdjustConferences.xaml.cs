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
    public partial class AdjustConferences : CustomWindow
    {
        public AdjustConferences(List<string> ids)
        {
            InitializeComponent();

            timStartTime.SetMaxValues(23, 59);
            numWeeks.SetMinMax(0, 999);
            numDays.SetMinMax(0, 6);
            numHours.SetMinMax(0, 23);
            numMinutes.SetMinMax(0, 59);
            timLength.SetMaxValues(99, 59);

            ToggleStartTime(false);
            ToggleMove(false);
            ToggleLength(false);
        }

        private void chkTime_Click(object sender, RoutedEventArgs e)
        {
            if (sender == chkStartTime)
            {
                bool isChecked = chkStartTime.IsChecked == true;
                ToggleStartTime(isChecked);
                if (isChecked)
                {
                    ToggleMove(false);
                    chkMove.IsChecked = false;
                }
            }
            if (sender == chkMove )
            {
                bool isChecked = chkMove.IsChecked == true;
                ToggleMove(isChecked);
                if (isChecked)
                {
                    ToggleStartTime(false);
                    chkStartTime.IsChecked = false;
                }
            }
            if (sender == chkLength)
                ToggleLength(chkLength.IsChecked == true);
        }

        void ToggleStartTime(bool enabled)
        {
            timStartTime.ToggleEnabled(enabled);
        }

        void ToggleMove(bool enabled)
        {
            numWeeks.ToggleEnabled(enabled);
            numDays.ToggleEnabled(enabled);
            numHours.ToggleEnabled(enabled);
            numMinutes.ToggleEnabled(enabled);
        }

        void ToggleLength(bool enabled)
        {
            timLength.ToggleEnabled(enabled);
        }

        private bool Adjust()
        {
            TimeSpan? startTime = timStartTime.GetTime();
            TimeSpan? move = null;
            TimeSpan? length = timLength.GetTime();

            if (chkStartTime.IsChecked == true && startTime == null)
                return App.Abort("Start time is checked, but no time has been entered.");
            if (chkMove.IsChecked == true)
            {
                int? weeks = numDays.GetNumber();
                int? days = numDays.GetNumber();
                if (days == null) days = 0;
                if (weeks != null) days += weeks * 7;
                int? hours = numDays.GetNumber();
                int? minutes = numDays.GetNumber();
                move = new((int)days, hours == null ? 0 : (int)hours, minutes == null ? 0 : (int)minutes, 0);
                if (move == TimeSpan.Zero)
                    return App.Abort("chkMove is checked, but the move amount is 0 or has not been entered.");
                if (cmbMoveDirection.SelectedIndex == 1)
                    move = -move;
            }
            if (chkLength.IsChecked == true && length == null)
                return App.Abort("Length is checked, but no time has been entered.");

            SendReceiveClasses.ConferenceAdjustment req = new();
            req.startTime = startTime;
            req.move = move;
            req.length = length;

            if (App.SendConferenceAdjustment(req))
            {
                Close();
                return true;
            }
            else
                return false;
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Adjust();
        }
    }
}
