using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
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
using DocumentFormat.OpenXml.Office2010.Excel;
using SendReceiveClasses;

namespace BridgeOpsClient
{
    public partial class EditRecurrence : CustomWindow
    {
        int id;

        List<Conference> conferences = new();

        public EditRecurrence(int id)
        {
            this.id = id;

            InitializeComponent();

            dtg.EnableMultiSelect();

            // Set permissions.
            btnAdd.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_CONFERENCES];
            btnRemove.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_CONFERENCES];
            btnSave.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_CONFERENCES];
            btnDelete.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_CONFERENCES];

            Title = "Resource - " + id.ToString();

            Refresh();
        }

        public bool Refresh()
        {
            bool Abort()
            {
                App.DisplayError("Something went wrong when attempting to retrieve the information for this " +
                                 "conference group. Closing window.");
                Close();
                return false;
            }

            List<List<object?>> recurrenceInfoRows;
            if (!App.Select("Recurrence", new() { Glo.Tab.RECURRENCE_NAME, Glo.Tab.NOTES },
                            new() { Glo.Tab.RECURRENCE_ID }, new() { id.ToString() }, new() { Conditional.Equals },
                            out _, out recurrenceInfoRows, false, false))
                return Abort();

            if (recurrenceInfoRows[0][0] is string name)
                txtName.Text = name;
            if (recurrenceInfoRows[0][1] is string notes)
                txtNotes.Text = notes;

            List<List<object?>> confIdSelectRows;
            if (!App.Select("Conference", new() { Glo.Tab.CONFERENCE_ID },
                            new() { Glo.Tab.RECURRENCE_ID }, new() { id.ToString() }, new() { Conditional.Equals },
                            out _, out confIdSelectRows, false, false))
                return Abort();

            List<string> confIDs = new();
            try
            {
                foreach (List<object?> row in confIdSelectRows)
                    confIDs.Add(((int)row[0]!).ToString());
            }
            catch
            {
                // Shouldn't ever get here - it's the primary key, so should never be null or anything but an INT.
                return Abort();
            }

            if (confIDs.Count > 0)
            {
                if (!App.SendConferenceSelectRequest(confIDs, out conferences))
                    return Abort();

                List<string?> columnNames = new()
                {
                    "ID",
                    ColumnRecord.GetPrintName(Glo.Tab.CONFERENCE_TITLE, ColumnRecord.conference),
                    "Start",
                    "End"
                };
                List<List<object?>> data = new();
                foreach (Conference c in conferences)
                    data.Add(new() { c.conferenceID, c.title, c.start, c.end });

                dtg.Update(columnNames, data);
            }
            else
                dtg.Wipe();

            return true;
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (App.DeleteConfirm(false) && App.SendDelete("Recurrence", Glo.Tab.RECURRENCE_ID, id.ToString(), false))
            {
                Close();
                return;
            }

            // App.SendDelete will present any errors necessary.
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            Recurrence r = new()
            {
                sessionID = App.sd.sessionID,
                columnRecordID = ColumnRecord.columnRecordID,
                id = id,
                name = txtName.Text == "" ? null : txtName.Text,
                notes = txtNotes.Text == "" ? null : txtNotes.Text,
                requireIdBack = false
            };

            if (App.SendUpdate(Glo.CLIENT_UPDATE_RECURRENCE, r))
                App.DisplayError("Save successful.");
            else
                App.DisplayError("Save unsuccessful.");
        }

        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            List<string> confIDs = dtg.GetCurrentlySelectedIDs();

            UpdateRequest req = new(App.sd.sessionID, ColumnRecord.columnRecordID, App.sd.loginID, "Conference",
                                    new() { Glo.Tab.RECURRENCE_ID }, new() { null }, new() { false },
                                    Glo.Tab.CONFERENCE_ID, confIDs, false);
            App.SendUpdate(req, true, true, true); // Override all warnings as we're not moving anything.
        }

        private void dtg_CustomDoubleClick(object sender, MouseButtonEventArgs e)
        {
            string editID = dtg.GetCurrentlySelectedID();
            int editIdInt;
            if (int.TryParse(editID, out editIdInt))
                App.EditConference(editIdInt);
            else
                App.DisplayError("Unable to discern the conference ID from the selected row.");
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            LinkRecord lr = new("Conference", ColumnRecord.orderedConference);
            lr.ShowDialog();
            if (lr.id == "") // Error will display in LinkRecord if it couldn't get the ID.
            {
                App.DisplayError("ID could not be ascertained from the record.");
                return;
            }

            UpdateRequest req = new(App.sd.sessionID, ColumnRecord.columnRecordID, App.sd.loginID, "Conference",
                                    new() { Glo.Tab.RECURRENCE_ID }, new() { id.ToString() }, new() { false },
                                    Glo.Tab.CONFERENCE_ID, new() { lr.id! }, false);
            App.SendUpdate(req, true, true, true); // Override all warnings as we're not moving anything.
        }
    }
}
