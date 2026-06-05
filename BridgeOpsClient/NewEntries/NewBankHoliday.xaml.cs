using SendReceiveClasses;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Windows;

namespace BridgeOpsClient
{
    public partial class NewBankHoliday : CustomWindow
    {
        int id = -1;

        int connMax = Int32.MaxValue;
        int confMax = Int16.MaxValue;
        int rowsMax = Int16.MaxValue;

        public NewBankHoliday()
        {
            InitializeComponent();

            txtNotes.MaxLength = Glo.Fun.LongToInt(ColumnRecord.GetColumn(ColumnRecord.bankHoliday,
                                                                          Glo.Tab.NOTES).restriction);
            txtNotes.Focus();

            btnAdd.IsEnabled = App.sd.createPermissions[Glo.PERMISSION_RECORDS];
        }

        public NewBankHoliday(string id)
        {
            Title = "Edit Bank Holiday";

            if (!int.TryParse(id, out this.id))
                App.DisplayError("Could not discern resource ID.", this);

            InitializeComponent();

            txtNotes.MaxLength = Glo.Fun.LongToInt(ColumnRecord.GetColumn(ColumnRecord.bankHoliday,
                                                                          Glo.Tab.NOTES).restriction);
            txtNotes.Focus();

            btnAdd.Content = "Save";
            btnDelete.Visibility = Visibility.Visible;

            dtp.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_RECORDS];
            txtNotes.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_RECORDS];

            btnAdd.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_RECORDS];
            btnDelete.IsEnabled = App.sd.deletePermissions[Glo.PERMISSION_RECORDS];
        }

        public void Populate(List<object?> data)
        {
            if (data[1] != null)
                dtp.SelectedDate = (DateTime)data[1]!;
            if (data[2] != null)
                txtNotes.Text = (string)data[2]!;
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            BankHoliday bh = new BankHoliday();

            bh.sessionID = App.sd.sessionID;
            bh.columnRecordID = ColumnRecord.columnRecordID;

            DateTime? date = dtp.SelectedDate;
            string notes = txtNotes.Text;
            if (date == null)
            {
                App.DisplayError("Must select a date.", this);
                return;
            }

            bh.date = date.Value;
            bh.notes = txtNotes.Text.Length > 0 ? txtNotes.Text : null;

            bool success = false;
            if (id < 0)
                success = App.SendInsert(Glo.CLIENT_NEW_BANK_HOLIDAY, bh, this);
            else
            {
                bh.id = id;
                success = App.SendUpdate(Glo.CLIENT_UPDATE_BANK_HOLIDAY, bh, this);
            }

            if (success)
            {
                Close();
                if (MainWindow.pageDatabase != null)
                    MainWindow.pageDatabase.RepeatSearches((int)UserSettings.TableIndex.BankHoliday);
                if (MainWindow.pageConferenceViews != null)
                    foreach (PageConferenceView view in MainWindow.pageConferenceViews)
                        view.SearchTimeframe();
            }
            // Errors presented in SendInsert() or SendUpdate().
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            App.WindowClosed();
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!App.DeleteConfirm(false, this))
                return;

            if (id < 0)
            {
                App.DisplayError("Could not discern ID from bank holiday.", this);
                return;
            }

            if (App.SendDelete("BankHoliday", Glo.Tab.BANK_HOL_ID, id.ToString(), false, this))
            {
                Close();
                if (MainWindow.pageDatabase != null)
                    MainWindow.pageDatabase.RepeatSearches((int)UserSettings.TableIndex.BankHoliday);
                if (MainWindow.pageConferenceViews != null)
                    foreach (PageConferenceView view in MainWindow.pageConferenceViews)
                        view.SearchTimeframe();
            }
        }
    }
}
