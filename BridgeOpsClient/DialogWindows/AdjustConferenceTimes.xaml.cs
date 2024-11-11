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
    public partial class AdjustConferenceTimes : CustomWindow
    {
        List<string> ids;

        public AdjustConferenceTimes(List<string> ids)
        {
            if (ids.Count == 0)
            {
                App.DisplayError("No conferences selected.");
                Close();
            }

            InitializeComponent();

            timStartTime.SetMaxValue(new TimeSpan(23, 59, 0));
            numWeeks.SetMinMax(0, 999);
            numDays.SetMinMax(0, 6);
            numHours.SetMinMax(0, 23);
            numMinutes.SetMinMax(0, 59);
            timLength.SetMaxValue(new TimeSpan(99, 59, 0));

            ToggleStartTime(false);
            ToggleEndTime(false);
            ToggleMove(false);
            ToggleLength(false);

            this.ids = ids;
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
            else if (sender == chkMove )
            {
                bool isChecked = chkMove.IsChecked == true;
                ToggleMove(isChecked);
                if (isChecked)
                {
                    ToggleStartTime(false);
                    chkStartTime.IsChecked = false;
                }
            }
            else if (sender == chkEndTime)
            {
                bool isChecked = chkEndTime.IsChecked == true;
                ToggleEndTime(isChecked);
                if (isChecked)
                {
                    ToggleLength(false);
                    chkLength.IsChecked = false;
                }
            }
            else if (sender == chkLength)
            {
                bool isChecked = chkLength.IsChecked == true;
                ToggleLength(isChecked);
                if (isChecked)
                {
                    ToggleEndTime(false);
                    chkEndTime.IsChecked = false;
                }
            }
        }

        void ToggleStartTime(bool enabled)
        {
            timStartTime.ToggleEnabled(enabled);
        }

        void ToggleEndTime(bool enabled)
        {
            timEndTime.ToggleEnabled(enabled);
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
            TimeSpan? endTime = timEndTime.GetTime();
            TimeSpan? length = timLength.GetTime();

            if (chkStartTime.IsChecked == true && startTime == null)
                return App.Abort("Start time is checked, but no time has been entered.");
            if (chkMove.IsChecked == true)
            {
                int? weeks = numWeeks.GetNumber();
                int? days = numDays.GetNumber();
                if (days == null) days = 0;
                if (weeks != null) days += weeks * 7;
                int? hours = numHours.GetNumber();
                int? minutes = numMinutes.GetNumber();
                move = new((int)days, hours == null ? 0 : (int)hours, minutes == null ? 0 : (int)minutes, 0);
                if (move == TimeSpan.Zero)
                    return App.Abort("Move is checked, but the move amount is 0 or has not been entered.");
                if (cmbMoveDirection.SelectedIndex == 1)
                    move = -move;
            }
            if (chkEndTime.IsChecked == true && endTime == null)
                return App.Abort("End time is checked, but no time has been entered.");
            if (chkLength.IsChecked == true && length == null)
                return App.Abort("Length is checked, but no time has been entered.");

            SendReceiveClasses.ConferenceAdjustment req = new();
            req.startTime = startTime;
            req.move = move;
            req.endTime = endTime;
            req.length = length;

            req.ids = ids.Select(int.Parse).ToList();

            req.intent = SendReceiveClasses.ConferenceAdjustment.Intent.Times;

            if (App.SendConferenceAdjustment(req))
            {
                App.RepeatSearches(7);
                return true;
            }
            else
                return false;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (Adjust())
                Close();
        }
    }
}
