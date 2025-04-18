﻿using System;
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
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Drawing.Diagrams;
using DocumentFormat.OpenXml.Office2010.Excel;
using SendReceiveClasses;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

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
        MenuItem btnGoTo;

        public EditRecurrence(int id)
        {
            this.id = id;

            InitializeComponent();

            txtName.MaxLength = (int)((ColumnRecord.Column)ColumnRecord.recurrence[Glo.Tab.RECURRENCE_NAME]!)
                .restriction;
            txtNotes.MaxLength = (int)((ColumnRecord.Column)ColumnRecord.recurrence[Glo.Tab.NOTES]!)
                .restriction;

            dtg.canHideColumns = true;
            dtg.EnableMultiSelect();
            dtg.identity = 10;

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
            btnGoTo = dtg.AddContextMenuItem("Go To", false, btnGoTo_Click);

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
            btnGoTo.IsEnabled = dtg.dtg.SelectedItems.Count == 1;

            btnCancel.Header = "Cancel";
            if (selectedSomething)
            {
                btnCancel.Header = "Uncancel";
                foreach (CustomControls.SqlDataGrid.Row row in dtg.dtg.SelectedItems)
                    if ((bool?)row.items[10] != true)
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
                                 "conference group. Closing window.", this);
                Close();
                return false;
            }

            List<List<object?>> recurrenceInfoRows;
            if (!App.Select("Recurrence", new() { Glo.Tab.RECURRENCE_NAME, Glo.Tab.NOTES },
                            new() { Glo.Tab.RECURRENCE_ID }, new() { id.ToString() }, new() { Conditional.Equals },
                            out _, out recurrenceInfoRows, false, false, this))
                return Abort();

            if (recurrenceInfoRows[0][0] is string name)
            {
                originalName = name;
                txtName.Text = name;
            }
            if (recurrenceInfoRows[0][1] is string notes)
            {
                originalNotes = notes;
                txtNotes.Text = notes;
            }

            List<List<object?>> confIdSelectRows;
            if (!App.Select("Conference", new() { Glo.Tab.CONFERENCE_ID },
                            new() { Glo.Tab.RECURRENCE_ID }, new() { id.ToString() }, new() { Conditional.Equals },
                            out _, out confIdSelectRows, false, false, this))
                return Abort();

            List<string> confIDs = new();
            try
            {
                foreach (List<object?> row in confIdSelectRows)
                    confIDs.Add(((int)row[0]!).ToString());
            }
            catch
            {
                // Shouldn't get here - it's the primary key, so should never be null or anything but an INT above 0.
                return Abort();
            }

            if (confIDs.Count > 0)
            {
                if (!App.SendConferenceSelectRequest(confIDs, out conferences, this))
                    return Abort();

                // If you change the position of cancelled, make sure to udpate it in the ContextMenuOpening function.
                List<string?> colNames = new()
                {
                    Glo.Tab.CONFERENCE_ID,
                    ColumnRecord.GetPrintName(Glo.Tab.CONFERENCE_TITLE, ColumnRecord.conference),
                    "Day",
                    "Start",
                    "End",
                    "Duration",
                    "Host No",
                    "Host Ref",
                    "Connections",
                    "Closure",
                    "Cancelled",
                    "Test",
                    "Resource",
                    ColumnRecord.GetPrintName(Glo.Tab.NOTES, ColumnRecord.conference),
                };

                List<List<object?>> data = new();
                DateTime start;
                DateTime end;

                // Add user-added columns.
                if (conferences.Count > 0)
                    colNames.AddRange(conferences[0].additionalCols);

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
                    List<object?> newRow = new()
                    {
                        c.conferenceID,
                        c.title,
                        start.DayOfWeek.ToString(),
                        c.start,
                        c.end,
                        c.end - c.start,
                        c.connections.Count == 0 ? null : c.connections[0].dialNo,
                        c.connections.Count == 0 ? null : c.connections[0].orgReference,
                        c.connections.Count,
                        c.closure,
                        c.cancelled,
                        test,
                        c.resourceName == null ? (c.resourceRow + 1).ToString() :
                                                 $"{c.resourceName} {c.resourceRow + 1}",
                        c.notes
                    };
                    // Add user-added columns.
                    newRow.AddRange(c.additionalValObjects);

                    data.Add(newRow);
                }

                dtg.Update(colNames, data);
            }
            else
                dtg.Wipe();

            return true;
        }

        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (dtg.dtg.SelectedItems.Count < 1)
            {
                App.DisplayError("You must select at least one item to update.", this);
                return;
            }

            UpdateMultiple updateMultiple = new(7, "Conference", ColumnRecord.orderedConference,
                                                Glo.Tab.CONFERENCE_ID, dtg.GetCurrentlySelectedIDs(), false);
            updateMultiple.Owner = this;
            updateMultiple.ShowDialog();
        }

        private void btnAdjustTime_Click(object sender, RoutedEventArgs e)
        {
            List<string> ids = dtg.GetCurrentlySelectedIDs();
            DialogWindows.AdjustConferenceTimes adjust = new(ids);
            adjust.Owner = this;
            adjust.ShowDialog();
        }

        private void btnAdjustConnections_Click(object sender, RoutedEventArgs e)
        {
            List<string> ids = dtg.GetCurrentlySelectedIDs();
            DialogWindows.AdjustConferenceConnections adjust = new(ids);
            adjust.Owner = this;
            adjust.ShowDialog();
        }

        private void btnSetHost_Click(object sender, RoutedEventArgs e)
        {
            List<string> ids = dtg.GetCurrentlySelectedIDs();

            SelectResult res;
            if (App.SendConnectionSelectRequest(ids, out res, this))
            {
                string dialNoFriendly = ColumnRecord.GetPrintName(Glo.Tab.DIAL_NO,
                                            (ColumnRecord.Column)ColumnRecord.organisation[Glo.Tab.DIAL_NO]!);
                string orgRefFriendly = ColumnRecord.GetPrintName(Glo.Tab.ORGANISATION_REF,
                                            (ColumnRecord.Column)ColumnRecord.organisation[Glo.Tab.ORGANISATION_REF]!);
                string orgNameFriendly = ColumnRecord.GetPrintName(Glo.Tab.ORGANISATION_NAME,
                                            (ColumnRecord.Column)ColumnRecord.organisation[Glo.Tab.ORGANISATION_NAME]!);
                res.columnNames = new() { dialNoFriendly, orgRefFriendly, orgNameFriendly, "Test", "Host", "Presence" };

                LinkRecord lr = new(res.columnNames, res.rows, 0, "Set Host");
                lr.Owner = this;
                lr.ShowDialog();

                if (lr.id == null)
                    return;

                ConferenceAdjustment ca = new();
                ca.intent = ConferenceAdjustment.Intent.Host;
                ca.dialHost = lr.id;
                ca.ids = ids.Select(int.Parse).ToList();

                // Error will display in the below function if it fails.
                if (App.SendConferenceAdjustment(ca, this))
                    MainWindow.RepeatSearches(7);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (!App.DisplayQuestion("Are you sure sure?", "Cancel", DialogWindows.DialogBox.Buttons.YesNo, this))
                return;

            try
            {
                bool uncancel = btnCancel.Header.ToString() == "Uncancel";
                List<string> ids = dtg.GetCurrentlySelectedIDs();

                UpdateRequest req = new(App.sd.sessionID, ColumnRecord.columnRecordID, App.sd.loginID,
                                        "Conference", new() { Glo.Tab.CONFERENCE_CANCELLED },
                                        new() { uncancel ? "0" : "1" },
                                        new() { false }, Glo.Tab.CONFERENCE_ID, ids, false);
                if (App.SendUpdate(req, this))
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
                if (App.DeleteConfirm(ids.Count > 1, this))
                {
                    if (App.SendDelete("Conference", Glo.Tab.CONFERENCE_ID, ids, false, this))
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

            if (App.SendUpdate(Glo.CLIENT_UPDATE_RECURRENCE, r, this))
            {
                App.DisplayError("Save successful.", this);
                MainWindow.RepeatSearches(8);
                MainWindow.RepeatSearches(7);
            }
            else
                App.DisplayError("Save unsuccessful.", this);
        }

        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            List<string> confIDs = dtg.GetCurrentlySelectedIDs();
            if (confIDs.Count == 0)
                return;

            UpdateRequest req = new(App.sd.sessionID, ColumnRecord.columnRecordID, App.sd.loginID, "Conference",
                                    new() { Glo.Tab.RECURRENCE_ID }, new() { null }, new() { false },
                                    Glo.Tab.CONFERENCE_ID, confIDs, false);
            if (App.SendUpdate(req, true, true, true, this)) // Override all warnings as we're not moving anything.
                MainWindow.RepeatSearches(7);
        }

        private void dtg_CustomDoubleClick(object sender, MouseButtonEventArgs e)
        {
            string editID = dtg.GetCurrentlySelectedID();
            int editIdInt;
            if (int.TryParse(editID, out editIdInt))
                App.EditConference(editIdInt, this);
            else
                App.DisplayError("Unable to discern the conference ID from the selected row.", this);
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            LinkRecord lr = new("Conference", ColumnRecord.orderedConference, "Select Conference", typeof(int), new() { id }, 3);
            lr.EnableMultiLink();
            lr.HideColumns(1, 2, 3);
            lr.Owner = this;
            lr.ShowDialog();

            if (lr.DialogResult == false)
                return;

            if (lr.ids == null || lr.ids.Count == 0) // Error will display in LinkRecord if it couldn't get the ID.
            {
                App.DisplayError("IDs could not be ascertained from the selected records.", this);
                return;
            }

            UpdateRequest req = new(App.sd.sessionID, ColumnRecord.columnRecordID, App.sd.loginID, "Conference",
                                    new() { Glo.Tab.RECURRENCE_ID }, new() { id.ToString() }, new() { false },
                                    Glo.Tab.CONFERENCE_ID, lr.ids, false);
            if (App.SendUpdate(req, true, true, true, this)) // Override all warnings as we're not moving anything.
                MainWindow.RepeatSearches(7);
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (App.DeleteConfirm(false, this) &&
                App.SendDelete("Recurrence", Glo.Tab.RECURRENCE_ID, id.ToString(), false, this))
            {
                MainWindow.RepeatSearches(8);
                Close();
                return;
            }

            // App.SendDelete will present any errors necessary.
        }

        private void btnGoTo_Click(object sender, RoutedEventArgs e)
        {
            string id = dtg.GetCurrentlySelectedID();
            List<Conference> selectResult;
            if (!App.SendConferenceSelectRequest(new() { id }, out selectResult, this))
                return;
            if (selectResult.Count != 1)
                return;

            Conference c = selectResult[0];

            foreach (PageConferenceView view in MainWindow.pageConferenceViews)
            {
                ScheduleView sv = view.schView;
                sv.scheduleTime = (DateTime)c.start!;
                sv.scrollResource = sv.GetScrollYfromResource(c.resourceID, c.resourceRow) -
                                                              (sv.ActualHeight * .5d - sv.zoomResourceCurrent * .5d);
                sv.EnforceResourceScrollLimits();
                view.updateScrollBar = true;
            }
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
            rs.Owner = this;
            rs.ShowDialog();
        }

        private void CustomWindow_Closed(object sender, EventArgs e)
        {
            if (WindowState != WindowState.Maximized)
            {
                Settings.Default.RecWinSizeX = Width;
                Settings.Default.RecWinSizeY = Height;
                Settings.Default.Save();
                App.WindowClosed();
            }
        }

        string originalNotes = "";
        string originalName = "";
        private void AnyInteraction(object? o, EventArgs? e)
        {
            changesMade = txtName.Text != originalName || txtNotes.Text != originalNotes;
            btnSave.IsEnabled = changesMade && App.sd.editPermissions[Glo.PERMISSION_CONFERENCES];
        }
    }
}
