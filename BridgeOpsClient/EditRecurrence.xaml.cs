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

        MenuItem btnUpdate;
        MenuItem btnAdjustTime;
        MenuItem btnAdjustConnections;
        MenuItem btnSetHost;
        MenuItem btnCancel;
        MenuItem btnDeleteConference;

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

            dtg.AddSeparator(false);
            btnUpdate = dtg.AddContextMenuItem("Update Selected", false, btnUpdate_Click);
            btnAdjustTime = dtg.AddContextMenuItem("Adjust Time", false, btnAdjustTime_Click);
            btnAdjustConnections = dtg.AddContextMenuItem("Adjust Connections", false, btnAdjustConnections_Click);
            btnSetHost = dtg.AddContextMenuItem("Set Host", false, btnSetHost_Click);
            btnCancel = dtg.AddContextMenuItem("Cancel", false, btnCancel_Click);
            btnDeleteConference = dtg.AddContextMenuItem("Delete Conference", false, btnDeleteConference_Click);

            Title = "Recurrence R-" + id.ToString();

            dtg.ContextMenuOpening += Dtg_ContextMenuOpening;

            Refresh();
        }

        private void Dtg_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            bool selectedSomething = dtg.dtg.SelectedItems.Count > 0;
            btnUpdate.IsEnabled = selectedSomething && App.sd.editPermissions[Glo.PERMISSION_CONFERENCES];
            btnAdjustTime.IsEnabled = selectedSomething && App.sd.editPermissions[Glo.PERMISSION_CONFERENCES];
            btnAdjustConnections.IsEnabled = selectedSomething && App.sd.editPermissions[Glo.PERMISSION_CONFERENCES];
            btnSetHost.IsEnabled = selectedSomething && App.sd.editPermissions[Glo.PERMISSION_CONFERENCES];
            btnCancel.IsEnabled = selectedSomething && App.sd.editPermissions[Glo.PERMISSION_CONFERENCES];
            btnDeleteConference.IsEnabled = selectedSomething && App.sd.deletePermissions[Glo.PERMISSION_CONFERENCES];

            btnCancel.Header = "Cancel";
            if (selectedSomething)
            {
                btnCancel.Header = "Uncancel";
                foreach (CustomControls.SqlDataGrid.Row row in dtg.dtg.SelectedItems)
                    if ((bool?)row.items[6] != true)
                    {
                        btnCancel.Header = "Cancel";
                        break;
                    }
            }
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

                // If you change the position of cancelled, make sure to udpate it in the ContextMenuOpening function.
                List<string?> columnNames = new()
                {
                    Glo.Tab.CONFERENCE_ID,
                    ColumnRecord.GetPrintName(Glo.Tab.CONFERENCE_TITLE, ColumnRecord.conference),
                    "Day",
                    "Start",
                    "End",
                    "Duration",
                    "Host",
                    "Cancelled",
                    "Test",
                    ColumnRecord.GetPrintName(Glo.Tab.NOTES, ColumnRecord.conference),
                };
                List<List<object?>> data = new();
                DateTime start;
                DateTime end;
                foreach (Conference c in conferences)
                {
                    bool test = false;
                    foreach (Conference.Connection n in c.connections)
                        if (n.isTest)
                        {
                            test = true;
                            break;
                        }

                    start = (DateTime)c.start!;
                    end = (DateTime)c.end!;
                    data.Add(new()
                    {
                        c.conferenceID,
                        c.title,
                        start.DayOfWeek.ToString(),
                        c.start,
                        c.end,
                        c.end - c.start,
                        c.connections.Count == 0 ? null : c.connections[0].dialNo,
                        c.cancelled,
                        test,
                        c.notes
                    });
                }

                dtg.Update(columnNames, data);
            }
            else
                dtg.Wipe();

            return true;
        }

        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (dtg.dtg.SelectedItems.Count < 1)
            {
                App.DisplayError("You must select at least one item to update.");
                return;
            }

            UpdateMultiple updateMultiple = new(7, "Conference", ColumnRecord.orderedConference,
                                                Glo.Tab.CONFERENCE_ID, dtg.GetCurrentlySelectedIDs(), false);
            updateMultiple.ShowDialog();
        }

        private void btnAdjustTime_Click(object sender, RoutedEventArgs e)
        {
            List<string> ids = dtg.GetCurrentlySelectedIDs();
            DialogWindows.AdjustConferenceTimes adjust = new(ids);
            adjust.ShowDialog();
        }

        private void btnAdjustConnections_Click(object sender, RoutedEventArgs e)
        {
            List<string> ids = dtg.GetCurrentlySelectedIDs();
            DialogWindows.AdjustConferenceConnections adjust = new(ids);
            adjust.ShowDialog();
        }

        private void btnSetHost_Click(object sender, RoutedEventArgs e)
        {
            List<string> ids = dtg.GetCurrentlySelectedIDs();

            SelectResult res;
            if (App.SendConnectionSelectRequest(ids, out res))
            {
                string dialNoFriendly = ColumnRecord.GetPrintName(Glo.Tab.DIAL_NO,
                                            (ColumnRecord.Column)ColumnRecord.organisation[Glo.Tab.DIAL_NO]!);
                string orgRefFriendly = ColumnRecord.GetPrintName(Glo.Tab.ORGANISATION_REF,
                                            (ColumnRecord.Column)ColumnRecord.organisation[Glo.Tab.ORGANISATION_REF]!);
                string orgNameFriendly = ColumnRecord.GetPrintName(Glo.Tab.ORGANISATION_NAME,
                                            (ColumnRecord.Column)ColumnRecord.organisation[Glo.Tab.ORGANISATION_NAME]!);
                res.columnNames = new() { dialNoFriendly, orgRefFriendly, orgNameFriendly, "Test", "Host", "Presence" };

                LinkRecord lr = new(res.columnNames, res.rows, 0);
                lr.ShowDialog();

                if (lr.id == null)
                    return;

                ConferenceAdjustment ca = new();
                ca.intent = ConferenceAdjustment.Intent.Host;
                ca.dialHost = lr.id;
                ca.ids = ids.Select(int.Parse).ToList();

                // Error will display in the below function if it fails.
                if (App.SendConferenceAdjustment(ca))
                    MainWindow.RepeatSearches(7);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool uncancel = btnCancel.Header.ToString() == "Uncancel";
                List<string> ids = dtg.GetCurrentlySelectedIDs();
                UpdateRequest req = new(App.sd.sessionID, ColumnRecord.columnRecordID, App.sd.loginID,
                                        "Conference", new() { Glo.Tab.CONFERENCE_CANCELLED },
                                        new() { uncancel ? "0" : "1" },
                                        new() { false }, Glo.Tab.CONFERENCE_ID, ids, false);
                if (App.SendUpdate(req))
                    MainWindow.RepeatSearches(7);
            }
            catch { } // No catch required due to intended inactivity on a conference disappearing and error
                      // messages in App.Update().
        }

        private void btnDeleteConference_Click(object? sender, RoutedEventArgs? e)
        {
            try
            {
                List<string> ids = dtg.GetCurrentlySelectedIDs();
                if (ids.Count == 0)
                    return;
                if (App.DeleteConfirm(ids.Count > 1))
                {
                    if (App.SendDelete("Conference", Glo.Tab.CONFERENCE_ID, ids, false))
                        MainWindow.RepeatSearches(7);
                }
            }
            catch { } // No catch required due to intended inactivity on a conference disappearing and error
                      // messages in App.Update().
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
            {
                MainWindow.RepeatSearches(9);
                App.DisplayError("Save successful.");
            }
            else
                App.DisplayError("Save unsuccessful.");
        }

        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            List<string> confIDs = dtg.GetCurrentlySelectedIDs();
            if (confIDs.Count == 0)
                return;

            UpdateRequest req = new(App.sd.sessionID, ColumnRecord.columnRecordID, App.sd.loginID, "Conference",
                                    new() { Glo.Tab.RECURRENCE_ID }, new() { null }, new() { false },
                                    Glo.Tab.CONFERENCE_ID, confIDs, false);
            if (App.SendUpdate(req, true, true, true)) // Override all warnings as we're not moving anything.
                MainWindow.RepeatSearches(7);
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
            LinkRecord lr = new("Conference", ColumnRecord.orderedConference, typeof(int), new() { id }, 3);
            lr.EnableMultiLink();
            lr.HideColumns(1, 2, 3);
            lr.ShowDialog();

            if (lr.DialogResult == false)
                return;

            if (lr.ids == null || lr.ids.Count == 0) // Error will display in LinkRecord if it couldn't get the ID.
            {
                App.DisplayError("IDs could not be ascertained from the selected records.");
                return;
            }

            UpdateRequest req = new(App.sd.sessionID, ColumnRecord.columnRecordID, App.sd.loginID, "Conference",
                                    new() { Glo.Tab.RECURRENCE_ID }, new() { id.ToString() }, new() { false },
                                    Glo.Tab.CONFERENCE_ID, lr.ids, false);
            if (App.SendUpdate(req, true, true, true)) // Override all warnings as we're not moving anything.)
                MainWindow.RepeatSearches(7);
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (App.DeleteConfirm(false) && App.SendDelete("Recurrence", Glo.Tab.RECURRENCE_ID, id.ToString(), false))
            {
                MainWindow.RepeatSearches(9);
                Close();
                return;
            }

            // App.SendDelete will present any errors necessary.
        }

        private void dtg_SelectionChanged(object sender, RoutedEventArgs e)
        {
            btnRemove.IsEnabled = dtg.dtg.SelectedItems.Count > 0;
            btnDuplicate.IsEnabled = dtg.dtg.SelectedItems.Count == 1;
        }

        private void btnDuplicate_Click(object sender, RoutedEventArgs e)
        {
            string id = dtg.GetCurrentlySelectedID();
            RecurrenceSelect rs = new(id);
            rs.ShowDialog();
        }
    }
}
