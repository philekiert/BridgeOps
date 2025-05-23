﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
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
using SendReceiveClasses;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace BridgeOpsClient
{
    public partial class NewTask : CustomWindow
    {
        bool edit = false;
        public string id = "";
        public bool changeMade = false;
        string? orgID = null;

        MenuItem btnUpdateVisit;
        MenuItem btnDeleteVisit;
        MenuItem btnUpdateDocument;
        MenuItem btnDeleteDocument;

        public NewTask()
        {
            InitializeComponent();

            dit.headers = ColumnRecord.taskHeaders;
            dit.Initialise(ColumnRecord.orderedTask, "Task");

            txtTaskRef.MaxLength = Glo.Fun.LongToInt(ColumnRecord.GetColumn(ColumnRecord.task,
                                                                            Glo.Tab.TASK_REFERENCE).restriction);
            txtNotes.MaxLength = Glo.Fun.LongToInt(ColumnRecord.GetColumn(ColumnRecord.contact,
                                                                          Glo.Tab.NOTES).restriction);
            // SqlDataGrids and their context menu items.
            dtgDocs.identity = (int)UserSettings.TableIndex.TaskDocument;
            dtgDocs.canHideColumns = true;
            dtgDocs.EnableMultiSelect();
            dtgDocs.AddSeparator(false);
            btnUpdateDocument = dtgDocs.AddContextMenuItem("Update", false, btnUpdateDocument_Click);
            btnDeleteDocument = dtgDocs.AddContextMenuItem("Delete", false, btnDeleteDocument_Click);
            dtgVisits.identity = (int)UserSettings.TableIndex.TaskVisit;
            dtgVisits.canHideColumns = true;
            dtgVisits.EnableMultiSelect();
            dtgVisits.AddSeparator(false);
            btnUpdateVisit = dtgVisits.AddContextMenuItem("Update", false, btnUpdateVisit_Click);
            btnDeleteVisit = dtgVisits.AddContextMenuItem("Delete", false, btnDeleteVisit_Click);

            StoreOriginalValues();
            AnyInteraction(null, null);
        }

        public NewTask(string id) : this()
        {
            InitializeComponent();

            edit = true;
            this.id = id;

            btnOrganisation.IsEnabled = true;
            btnAddDoc.IsEnabled = true;
            btnAddVisit.IsEnabled = true;
            btnDelete.Visibility = Visibility.Visible;
            btnBreakOut.IsEnabled = true;

            EnforcePermissions();

            dit.ValueChangedHandler = () => { AnyInteraction(null, null); return true; };
        }

        public void Populate(List<object?> data)
        {
            try
            {
                txtTaskRef.Text = (string)data[1]!; // NOT NULL in database, can't be null.
                originalTaskRef = (string)data[1]!;
                datOpened.SelectedDate = (DateTime?)data[2];
                datClosed.SelectedDate = (DateTime?)data[3];
                txtNotes.Text = (string?)data[4];

                Title = "Task - " + txtTaskRef.Text;

                dit.Populate(data.GetRange(5, data.Count - 5));

                PopulateDocuments();
                PopulateVisits();

                SetOrganisationButton();

                StoreOriginalValues();
                AnyInteraction(null, null);
            }
            catch
            {
                App.DisplayError("Something went wrong when pulling the data for this task.", this);
            }
        }
        public void SetOrganisationButton()
        {
            List<List<object?>> rows;
            if (App.Select("Organisation", new() { Glo.Tab.ORGANISATION_REF },
                           new() { Glo.Tab.TASK_REFERENCE }, new() { txtTaskRef.Text },
                           new() { Conditional.Equals }, out _, out rows, false, false, this))
            {
                if (rows.Count >= 1) // Task_Reference is unique in the database, so there can only be one result if any.
                {
                    btnOrganisation.Content = (string)rows[0][0]!;
                    orgID = (string)btnOrganisation.Content;
                }
                else
                {
                    btnOrganisation.Content = "Create New";
                    orgID = null;
                }
            }
        }

        // Edit detection.
        string originalTaskRef = ""; // Also to check whether to ask if refs should be updated on all attached records.
        DateTime? originalOpened;
        DateTime? originalClosed;
        string originalNotes = "";
        
        private void StoreOriginalValues()
        {
            originalTaskRef = txtTaskRef.Text ?? "";
            originalOpened = datOpened.SelectedDate;
            originalClosed = datClosed.SelectedDate;
            originalNotes = txtNotes.Text ?? "";

            dit.RememberStartingValues();
        }
        private void AnyInteraction(object? o, EventArgs? e)
        {
            changesMade = originalTaskRef != (txtTaskRef.Text ?? "") ||
                          originalOpened != datOpened.SelectedDate ||
                          originalClosed != datClosed.SelectedDate ||
                          originalNotes != (txtNotes.Text ?? "") ||
                          dit.CheckForValueChanges();

            btnSave.IsEnabled = changesMade &&
                                ((edit && App.sd.editPermissions[Glo.PERMISSION_TASKS]) ||
                                (!edit && App.sd.createPermissions[Glo.PERMISSION_TASKS]));
        }

        public void PopulateVisits() { PopulateTable("Visit"); }
        public void PopulateDocuments() { PopulateTable("Document"); }
        public void PopulateTable(string table)
        {
            if (txtTaskRef.Text == null)
                return;

            bool doc = table == "Document";

            var dict = doc ? ColumnRecord.orderedDocument : ColumnRecord.orderedVisit;

            List<string?> colNames;
            List<List<object?>> rows;
            SelectRequest req = new(App.sd.sessionID, ColumnRecord.columnRecordID, table, false,
                                    new(), new(), new(), new(),
                                    dict.Keys.Cast<string>().ToList(), Enumerable.Repeat(string.Empty, dict.Count).ToList(),
                                    new() { Glo.Tab.TASK_REFERENCE }, new() { "=" },
                                    new() { txtTaskRef.Text }, new() { true }, new(), new(), new(),
                                    new() { doc ? Glo.Tab.DOCUMENT_DATE : Glo.Tab.VISIT_DATE },
                                    new() { true });
            if (App.SendSelectRequest(req, out colNames, out rows, this))
                (doc ? dtgDocs : dtgVisits).Update(dict, colNames, rows);
        }

        private void EnforcePermissions()
        {
            // Permissions not relevant for new entry, so no need to call this from the primary constructor.
            bool taskCreate = App.sd.createPermissions[Glo.PERMISSION_TASKS];
            bool taskEdit = App.sd.editPermissions[Glo.PERMISSION_TASKS];
            bool taskDelete = App.sd.deletePermissions[Glo.PERMISSION_TASKS];
            btnSave.IsEnabled = taskEdit;
            btnDelete.IsEnabled = taskDelete;
            btnAddVisit.IsEnabled = taskCreate;
            btnAddDoc.IsEnabled = taskCreate;
            btnBreakOut.IsEnabled = taskCreate && taskEdit;
            btnUpdateDocument.IsEnabled = taskEdit;
            btnUpdateVisit.IsEnabled = taskEdit;
            btnDeleteDocument.IsEnabled = taskDelete;
            btnDeleteVisit.IsEnabled = taskDelete;
            btnOrganisation.IsEnabled = taskEdit;

            if (!taskEdit)
            {
                txtTaskRef.IsReadOnly = true;
                datOpened.IsEnabled = false;
                datClosed.IsEnabled = false;
                dit.ToggleFieldsEnabled(false);
                txtNotes.IsReadOnly = true;
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!dit.ScoopValues())
            {
                App.Abort("Something went wrong when extracting the data from the input fields.", this);
                return;
            }

            if (txtTaskRef.Text == "")
            {
                App.Abort("You must select a task reference.", this);
                return;
            }

            string? notes = txtNotes.Text == "" ? null : txtNotes.Text;

            SendReceiveClasses.Task task = new(App.sd.sessionID, ColumnRecord.columnRecordID, txtTaskRef.Text,
                                               datOpened.SelectedDate, datClosed.SelectedDate, notes);
            dit.ExtractValues(out task.additionalCols, out task.additionalVals);
            task.additionalNeedsQuotes = dit.GetNeedsQuotes();

            if (edit)
            {
                if (task.taskRef != originalTaskRef)
                {
                    task.updateAllTaskRefs = App.DisplayQuestion("Would you like to update the task reference for " +
                                                                 "any attached organisation, as well as any visits " +
                                                                 "and documents?",
                                                                 "Update Task Reference",
                                                                 DialogWindows.DialogBox.Buttons.YesNo, this);
                    task.oldTaskRef = originalTaskRef;
                }

                if (!int.TryParse(id, out task.taskID))
                {
                    App.Abort("Could not ascertain the task's ID.", this);
                    return;
                }

                if (App.SendUpdate(Glo.CLIENT_UPDATE_TASK, task, this))
                {
                    if (MainWindow.pageDatabase != null)
                        MainWindow.pageDatabase.RepeatSearches((int)UserSettings.TableIndex.Task);
                    changeMade = true;
                    Close();
                }
            }
            else
            {
                if (App.SendInsert(Glo.CLIENT_NEW_TASK, task, out id, this))
                {
                    changeMade = true;
                    // Not need to call pageDatabase.RepeatSearches() here, as it can't possibly affected any other
                    // table in the application but the Organisation that added it, if there was one.
                    if (MainWindow.pageDatabase != null)
                        MainWindow.pageDatabase.RepeatSearches(11);
                    Close();
                }
            }
        }

        private void btnAddVisit_Click(object sender, RoutedEventArgs e)
        {
            NewVisit newVisit = new();
            newVisit.cmbTaskRef.Text = txtTaskRef.Text;
            newVisit.Populate(new()); // This enables or disables the View task button based on the above if empty.
            newVisit.Show();
        }

        private void btnAddDoc_Click(object sender, RoutedEventArgs e)
        {
            NewDocument newDoc = new();
            newDoc.cmbTaskRef.Text = txtTaskRef.Text;
            newDoc.Populate(new()); // This enables or disables the View task button based on the above if empty.
            newDoc.Show();
        }

        private void dtg_CustomDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender == dtgDocs)
                App.EditDocument(dtgDocs.GetCurrentlySelectedID(), App.mainWindow);
            else
                App.EditVisit(dtgVisits.GetCurrentlySelectedID(), App.mainWindow);
        }

        private void btnOrganisation_Click(object sender, RoutedEventArgs e)
        {
            if (orgID != null)
            {
                // Get the ID for the reference, then edit.
                List<List<object?>> rows;
                if (App.Select("Organisation", new() { Glo.Tab.ORGANISATION_ID },
                               new() { Glo.Tab.ORGANISATION_REF }, new() { orgID }, new() { Conditional.Equals },
                               out _, out rows, false, false, this))
                {
                    if (rows.Count != 1)
                        App.DisplayError("Could not find the requested task.", this);
                    App.EditOrganisation(rows[0][0]!.ToString()!, this); // Primary key, won't be null.
                }

                return;
            }

            // Create new.
            NewOrganisation org = new();
            // Populate parent organisation list.
            bool successful;
            string[]? organisationList = App.GetOrganisationList(out successful, this);
            if (!successful || organisationList == null)
                App.DisplayError("Could not pull organisation list from server.", this);
            else
                org.cmbOrgParentID.ItemsSource = organisationList;
            org.PopulateOnlyTaskRef(txtTaskRef.Text);
            org.Show();
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!App.DeleteConfirm(false, this))
                return;

            if (App.SendDelete("Task", Glo.Tab.TASK_ID, id, false, this))
            {
                changeMade = true;
                if (MainWindow.pageDatabase != null)
                    MainWindow.pageDatabase.RepeatSearches((int)UserSettings.TableIndex.Task);
                Close();
            }
        }

        private void btnBreakOut_Click(object sender, RoutedEventArgs e)
        {
            if (id != "")
            {
                TaskBreakOut breakOut = new(txtTaskRef.Text, orgID == null ? null : (string)btnOrganisation.Content, this);
                breakOut.ShowDialog();
            }
        }

        private void btnDeleteVisit_Click(object sender, RoutedEventArgs e)
        {
            if (dtgVisits.dtg.SelectedItems.Count < 1)
            {
                App.DisplayError("You must select at least one item to delete.", this);
                return;
            }

            if (!App.DeleteConfirm(dtgVisits.dtg.SelectedItems.Count > 1, this))
                return;

            if (App.SendDelete("Visit", Glo.Tab.VISIT_ID, dtgVisits.GetCurrentlySelectedIDs(), false, this) &&
                MainWindow.pageDatabase != null)
                MainWindow.pageDatabase.RepeatSearches(dtgVisits.identity);
        }
        private void btnDeleteDocument_Click(object sender, RoutedEventArgs e)
        {
            if (dtgDocs.dtg.SelectedItems.Count < 1)
            {
                App.DisplayError("You must select at least one item to delete.", this);
                return;
            }

            if (!App.DeleteConfirm(dtgDocs.dtg.SelectedItems.Count > 1, this))
                return;

            if (App.SendDelete("Document", Glo.Tab.DOCUMENT_ID, dtgDocs.GetCurrentlySelectedIDs(), false, this) &&
                MainWindow.pageDatabase != null)
                MainWindow.pageDatabase.RepeatSearches(dtgDocs.identity);
        }

        private void btnUpdateVisit_Click(object sender, RoutedEventArgs e)
        {
            if (dtgVisits.dtg.SelectedItems.Count < 1)
            {
                App.DisplayError("You must select at least one item to update.", this);
                return;
            }

            new UpdateMultiple(dtgVisits.identity, "Visit", ColumnRecord.orderedVisit, Glo.Tab.VISIT_ID,
                               dtgVisits.GetCurrentlySelectedIDs(), false).ShowDialog();
        }
        private void btnUpdateDocument_Click(object sender, RoutedEventArgs e)
        {
            if (dtgDocs.dtg.SelectedItems.Count < 1)
            {
                App.DisplayError("You must select at least one item to update.", this);
                return;
            }

            new UpdateMultiple(dtgDocs.identity, "Document", ColumnRecord.orderedDocument, Glo.Tab.DOCUMENT_ID,
                               dtgDocs.GetCurrentlySelectedIDs(), false).ShowDialog();
        }

        private void CustomWindow_Closed(object sender, EventArgs e)
        {
            if (WindowState != WindowState.Maximized)
            {
                Settings.Default.TaskWinSizeX = Width;
                Settings.Default.TaskWinSizeY = Height;
                Settings.Default.Save();
                App.WindowClosed();
            }
        }
    }
}
