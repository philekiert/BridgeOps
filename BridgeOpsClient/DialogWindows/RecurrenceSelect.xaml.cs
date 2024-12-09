using DocumentFormat.OpenXml.Bibliography;
using SendReceiveClasses;
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

namespace BridgeOpsClient
{
    public partial class RecurrenceSelect : CustomWindow
    {
        string id;

        public RecurrenceSelect(string id)
        {
            InitializeComponent();
            this.id = id;

            numWeeks.SetMinMax(1, 99);
            numMonths.SetMinMax(1, 99);
            numMonthlyDate.SetMinMax(1, 31);
            numEnd.SetMinMax(1, 999);
        }

        private void rdbEndChoice_Click(object sender, RoutedEventArgs e)
        {
            bool endBy = sender == rdbEndOn;
            rdbEndOn.IsChecked = endBy;
            datEnd.IsEnabled = endBy;
            rdbEndAfter.IsChecked = !endBy;
            numEnd.IsEnabled = !endBy;
        }

        private void rdbMonthly_Click(object sender, RoutedEventArgs e)
        {
            bool date = sender == rdbMonthlyDate;
            rdbMonthlyDate.IsChecked = date;
            numMonthlyDate.IsEnabled = date;
            rdbMonthlyNth.IsChecked = !date;
            cmbMonthlyNth.IsEnabled = !date;
            cmbMonthlyWeekday.IsEnabled = !date;
            chkSqueezeInFifth.IsEnabled = !date;
        }

        private void cmbChoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool weekly = cmbChoice.SelectedIndex == 0;

            stkWeekly.Visibility = weekly ? Visibility.Visible : Visibility.Collapsed;
            stkMonthly.Visibility = weekly ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!GenerateDuplicateDates())
                return;

            List<Conference> selReturn;
            if (!App.SendConferenceSelectRequest(new() { id }, out selReturn, this))
                return;
            if (selReturn.Count != 1)
                return;

            Conference toClone = selReturn[0];
            List<Conference> duplicates = new();
            foreach (DateTime date in dates)
            {
                Conference c = toClone.CloneForNewInsert(App.sd.sessionID, ColumnRecord.columnRecordID);
                c.closure = null;
                DateTime start = (DateTime)c.start!;
                TimeSpan length = (DateTime)c.end! - start;
                toClone.start = date.Date + start.TimeOfDay;
                toClone.end = toClone.start + length;

                duplicates.Add(toClone.CloneForNewInsert(App.sd.sessionID, ColumnRecord.columnRecordID));
            }

            if (App.SendInsert(Glo.CLIENT_NEW_CONFERENCE, duplicates, this))
                Close();
        }

        List<DateTime> dates = new List<DateTime>();
        public bool GenerateDuplicateDates()
        {
            string abortMessage = "You must input all information to proceed.";

            dates = new List<DateTime>();
            DateTime startDate;
            DateTime endDate = DateTime.MaxValue;
            int maxOccurrences = int.MaxValue;

            HashSet<DayOfWeek> selectedWeekdays = new();
            if (chkMonday.IsChecked == true) selectedWeekdays.Add(DayOfWeek.Monday);
            if (chkTuesday.IsChecked == true) selectedWeekdays.Add(DayOfWeek.Tuesday);
            if (chkWednesday.IsChecked == true) selectedWeekdays.Add(DayOfWeek.Wednesday);
            if (chkThursday.IsChecked == true) selectedWeekdays.Add(DayOfWeek.Thursday);
            if (chkFriday.IsChecked == true) selectedWeekdays.Add(DayOfWeek.Friday);
            if (chkSaturday.IsChecked == true) selectedWeekdays.Add(DayOfWeek.Saturday);
            if (chkSunday.IsChecked == true) selectedWeekdays.Add(DayOfWeek.Sunday);

            // Confirm everything is selected for weekly recurrences, if chosen.
            if (cmbChoice.SelectedIndex == 0)
                if (numWeeks.GetNumber() == null || datWeeklyStart.SelectedDate == null || selectedWeekdays.Count == 0)
                    return App.Abort(abortMessage, this);
                else
                    startDate = (DateTime)datWeeklyStart.SelectedDate;

            // Confirm everything is selected for monthly recurrences, if chosen.
            else // if SelectedIndex == 1
                if ((datMonthlyStart.SelectedDate == null || numMonths.GetNumber() == null) ||
                    (rdbMonthlyDate.IsChecked != true && rdbMonthlyNth.IsChecked != true) ||
                    (rdbMonthlyDate.IsChecked == true && numMonthlyDate.GetNumber() == null))
                return App.Abort(abortMessage, this);
            else
                startDate = (DateTime)datMonthlyStart.SelectedDate;

            // Confirm that the end information is set.
            if ((rdbEndOn.IsChecked == true && datEnd.SelectedDate != null))
                endDate = (DateTime)datEnd.SelectedDate!;
            else if ((rdbEndAfter.IsChecked == true && numEnd.GetNumber() != null))
                maxOccurrences = (int)numEnd.GetNumber()!;
            else
                return App.Abort(abortMessage, this);

            DateTime current = startDate; // Start date has to have been assigned going by the above code.

            //if (startDate <= DateTime.Now.Date)
            //    return App.Abort("Duplication can not begin earlier than tomorrow.", this);
            if (endDate < startDate)
                return App.Abort($"Recurrence can not end before it begins, as this would require time-travel " +
                                 $"technology not available in Bridge Manager {Glo.VersionNumber}." +
                                 $"\n\nCheck for updates before trying again.", this);
            // Weekly
            if (cmbChoice.SelectedIndex == 0)
            {
                int weekInterval = (int)numWeeks.GetNumber()!;
                if (weekInterval == 0)
                    Close();

                while (current.Date <= endDate.Date && dates.Count < maxOccurrences)
                {
                    do
                    {
                        if (selectedWeekdays.Contains(current.DayOfWeek))
                            dates.Add(current);
                        current = current.AddDays(1);
                    }
                    while (current.Date <= endDate.Date &&
                           dates.Count < maxOccurrences &&
                           current.DayOfWeek != DayOfWeek.Monday); // Break when we wrap around to monday

                    // Progress to the next week.
                    current = current.AddDays(7 * (weekInterval - 1));
                }
            }

            // Monthly
            else if (rdbMonthlyDate.IsChecked == true)
            {
                // Date of month

                int monthInterval = (int)numMonths.GetNumber()!;
                int day = (int)numMonthlyDate.GetNumber()!;
                // Record days in month, as if the desired date exceeds this value, we'll use this instead.
                int daysInMonth = DateTime.DaysInMonth(current.Year, current.Month);
                current = new(current.Year, current.Month, day > daysInMonth ? daysInMonth : day);
                if (startDate.Date.Day > day)
                    current = current.AddMonths(1);
                while (current.Date <= endDate.Date && dates.Count < maxOccurrences)
                {
                    daysInMonth = DateTime.DaysInMonth(current.Year, current.Month);
                    dates.Add(new(current.Year, current.Month, day > daysInMonth ? daysInMonth : day));
                    current = current.AddMonths(monthInterval);
                }
            }
            else // if rdbMonthlyDate.IsChecked
            {
                // Nth weekday of month

                int monthInterval = (int)numMonths.GetNumber()!;
                int nth = cmbMonthlyNth.SelectedIndex;
                bool squeezeIn = chkSqueezeInFifth.IsChecked == true;

                current = new(current.Year, current.Month, 1);
                int dayIndex = cmbMonthlyWeekday.SelectedIndex;
                while (dates.Count < maxOccurrences) // Loops also breaks if the date exceeds the end date.
                {
                    // Get first date of a weekday in a month.
                    int first = (dayIndex - (int)new DateTime(current.Year, current.Month, 1).DayOfWeek);
                    first += first < 0 ? 8 : 1;

                    // Get the nth date.
                    first += 7 * nth;

                    DateTime? date = null;
                    if (first <= DateTime.DaysInMonth(current.Year, current.Month))
                        date = new DateTime(current.Year, current.Month, first);
                    else if (squeezeIn) // Go for fourth day instead of fifth if selected.
                        date = new DateTime(current.Year, current.Month, first - 7);

                    if (date != null && date > startDate)
                        if (date <= endDate)
                        {
                            dates.Add((DateTime)date!);
                            current = current.AddMonths(monthInterval);
                        }
                        else
                            break;
                    else if (date <= startDate)
                        current = current.AddMonths(1);
                }
            }

            return true;
        }

        private void cmbMonthlyNth_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (chkSqueezeInFifth != null)
                chkSqueezeInFifth.Visibility = cmbMonthlyNth.SelectedIndex == 4 ? Visibility.Visible :
                                                                                  Visibility.Collapsed;
        }
    }
}
