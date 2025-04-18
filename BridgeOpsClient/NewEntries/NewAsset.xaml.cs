﻿using BridgeOpsClient.DialogWindows;
using SendReceiveClasses;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Sockets;
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
using System.Windows.Threading;

namespace BridgeOpsClient
{
    public partial class NewAsset : CustomWindow
    {
        bool edit = false;
        int id;
        string? originalRef = "";
        string? originalOrg = "";
        string? originalNotes = "";

        private void ApplyPermissions()
        {
            if (!App.sd.createPermissions[Glo.PERMISSION_RECORDS])
                btnAdd.IsEnabled = false;
            if (!App.sd.editPermissions[Glo.PERMISSION_RECORDS])
            {
                btnEdit.IsEnabled = false;
                txtAssetRef.IsReadOnly = true; ;
                ToggleFieldsEnabled(false);
            }
            if (!App.sd.deletePermissions[Glo.PERMISSION_RECORDS])
                btnDelete.IsEnabled = false;

            btnCorrectReason.IsEnabled = App.sd.admin;
        }

        public NewAsset()
        {
            InitializeComponent();
            InitialiseFields();

            tabChangeLog.IsEnabled = false;

            ApplyPermissions();

            txtAssetRef.Focus();
        }
        public NewAsset(int id)
        {
            InitializeComponent();
            InitialiseFields();

            edit = true;
            btnAdd.Visibility = Visibility.Hidden;
            btnEdit.Visibility = Visibility.Visible;
            btnDelete.Visibility = Visibility.Visible;
            this.id = id;
            Title = "Asset";

            ApplyPermissions();
        }
        public NewAsset(int id, string record)
        {
            InitializeComponent();
            InitialiseFields();

            btnAdd.Visibility = Visibility.Hidden;
            btnEdit.Visibility = Visibility.Hidden;
            btnDelete.Visibility = Visibility.Hidden;
            this.id = id;

            cmbOrgRef.IsEditable = true; // This makes it so we can set the value without loading the list of IDs.
            ToggleFieldsEnabled(false);

            tabChangeLog.IsEnabled = false;

            ApplyPermissions();
        }

        private void InitialiseFields()
        {
            ditAsset.headers = ColumnRecord.assetHeaders;
            ditAsset.Initialise(ColumnRecord.orderedAsset, "Asset");

            // Implement max lengths. Max lengths in the DataInputTable are set automatically.
            txtAssetRef.MaxLength = Glo.Fun.LongToInt(ColumnRecord.GetColumn(ColumnRecord.asset,
                                                                             Glo.Tab.ASSET_REF).restriction);
            txtNotes.MaxLength = Glo.Fun.LongToInt(ColumnRecord.GetColumn(ColumnRecord.asset,
                                                                          Glo.Tab.NOTES).restriction);

            // Implement friendly names.
            if (ColumnRecord.GetColumn(ColumnRecord.asset, Glo.Tab.ASSET_REF).friendlyName != "")
                lblAssetID.Content = ColumnRecord.GetColumn(ColumnRecord.asset, Glo.Tab.ASSET_REF).friendlyName;
            if (ColumnRecord.GetColumn(ColumnRecord.asset, Glo.Tab.ORGANISATION_REF).friendlyName != "")
                lblOrgID.Content = ColumnRecord.GetColumn(ColumnRecord.asset, Glo.Tab.ORGANISATION_REF).friendlyName;
            if (ColumnRecord.GetColumn(ColumnRecord.asset, Glo.Tab.NOTES).friendlyName != "")
                lblNotes.Content = ColumnRecord.GetColumn(ColumnRecord.asset, Glo.Tab.NOTES).friendlyName;
        }

        public void PopulateExistingData(List<object?> data)
        {
#pragma warning disable CS8602
            // This method will not be called if the data has a different Count than expected.
            if (data[1] != null)
                txtAssetRef.Text = data[1].ToString();
            else
                cmbOrgRef.Text = null;
            if (data[2] != null)
                cmbOrgRef.Text = data[2].ToString();
            else
                cmbOrgRef.Text = null;
            if (data[3] != null)
                txtNotes.Text = data[3].ToString();
            else
                txtNotes.Text = null;

            // Store the original values to check if any changes have been made to the data. The same takes place
            // in the data input table.
            originalRef = txtAssetRef.Text;
            originalOrg = cmbOrgRef.Text == null ? "" : cmbOrgRef.Text;
            originalNotes = txtNotes.Text;

            ditAsset.Populate(data.GetRange(4, data.Count - 4));
            if (edit)
                ditAsset.RememberStartingValues();
            ditAsset.ValueChangedHandler = AnyInteraction;
#pragma warning restore CS8602

            Title = "Asset - " + originalRef;
            if (!edit)
                Title += " (Change)";

            AnyInteraction(); // Call this again, as the order of events above can cause some issues.
        }

        private void GetHistory()
        {
            // Error message is displayed by App.SelectAll() if something goes wrong.
            List<string?> columnNames;
            List<List<object?>> rows;

            if (App.SelectHistory("AssetChange", id.ToString(), out columnNames, out rows, this))
            {
                foreach (List<object?> row in rows)
                    if (row[2] == null)
                        row[2] = "[deleted]";
                dtgChangeLog.maxLengthOverrides = new Dictionary<string, int> { { "Reason", -1 } };
                dtgChangeLog.Update(new List<OrderedDictionary>()
                                   { ColumnRecord.assetChange, ColumnRecord.login }, columnNames, rows, new(),
                                   Glo.Tab.CHANGE_ID);
            }
        }

        public string[]? organisationList;
        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (ditAsset.ScoopValues())
            {
                //if (txtAssetRef.Text == "")
                //{
                //    App.DisplayError($"You must input a value for {lblAssetID.Content}.");
                //    return;
                //}

                Asset newAsset = new();
                newAsset.changeReason = ""; // Set automatically.

                newAsset.sessionID = App.sd.sessionID;
                newAsset.columnRecordID = ColumnRecord.columnRecordID;

                newAsset.assetRef = txtAssetRef.Text;
                if (newAsset.assetRef == "")
                    newAsset.assetRef = null;
                newAsset.organisationRef = cmbOrgRef.Text.Length == 0 ? null : cmbOrgRef.Text;
                newAsset.notes = txtNotes.Text.Length == 0 ? null : txtNotes.Text;

                ditAsset.ExtractValues(out newAsset.additionalCols, out newAsset.additionalVals);

                // Obtain types and determine whether or not quotes will be needed.
                newAsset.additionalNeedsQuotes = new();
                foreach (string c in newAsset.additionalCols)
                    newAsset.additionalNeedsQuotes.Add(
                        SqlAssist.NeedsQuotes(ColumnRecord.GetColumn(ColumnRecord.asset, c).type));

                if (App.SendInsert(Glo.CLIENT_NEW_ASSET, newAsset, this))
                {
                    changesMade = true;
                    if (MainWindow.pageDatabase != null)
                        MainWindow.pageDatabase.RepeatSearches(1);
                    Close();
                }
            }
            else
            {
                string message = "One or more values caused an unknown error to occur.";
                if (ditAsset.disallowed.Count > 0)
                    message = ditAsset.disallowed[0];
                App.DisplayError(message, this);
            }
        }

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (ditAsset.ScoopValues())
            {
                //if (txtAssetRef.Text == "")
                //{
                //    App.DisplayError("You must input a value for Asset ID");
                //    return;
                //}

                Asset asset = new Asset();
                asset.sessionID = App.sd.sessionID;
                asset.columnRecordID = ColumnRecord.columnRecordID;
                asset.assetID = id;
                List<string> cols;
                List<string?> vals;
                ditAsset.ExtractValues(out cols, out vals);

                // Remove any values equal to their starting value.
                List<int> toRemove = new();
                for (int i = 0; i < vals.Count; ++i)
                    if (ditAsset.startingValues[i] == vals[i])
                        toRemove.Add(i);
                int mod = 0; // Each one we remove, we need to take into account that the list is now 1 less.
                foreach (int i in toRemove)
                {
                    cols.RemoveAt(i - mod);
                    vals.RemoveAt(i - mod);
                    ++mod;
                }

                // Obtain types and determine whether or not quotes will be needed.
                asset.additionalNeedsQuotes = new();
                foreach (string c in cols)
                    asset.additionalNeedsQuotes.Add(SqlAssist.NeedsQuotes(ColumnRecord.GetColumn(ColumnRecord.asset, c).type));

                asset.additionalCols = cols;
                asset.additionalVals = vals;

                // Add the known fields if changed.
                if (txtAssetRef.Text != originalRef)
                {
                    asset.assetRef = txtAssetRef.Text;
                    asset.assetRefChanged = true;
                }
                if ((string?)cmbOrgRef.SelectedItem != originalOrg)
                {
                    asset.organisationRef = cmbOrgRef.Text;
                    asset.organisationRefChanged = true;
                }
                if (txtNotes.Text != originalNotes)
                {
                    asset.notes = txtNotes.Text;
                    asset.notesChanged = true;
                }

                DialogChangeReason reasonDialog = new("Asset");
                reasonDialog.Owner = this;
                bool? result = reasonDialog.ShowDialog();
                if (result != null && result == true)
                {
                    asset.changeReason = reasonDialog.txtReason.Text;
                    if (App.SendUpdate(Glo.CLIENT_UPDATE_ASSET, asset, this))
                    {
                        if (MainWindow.pageDatabase != null)
                            MainWindow.pageDatabase.RepeatSearches(1);
                        Close();
                    }
                }
            }
            else
            {
                string message = "One or more values caused an unknown error to occur.";
                if (ditAsset.disallowed.Count > 0)
                    message = ditAsset.disallowed[0];
                App.DisplayError(message, this);
            }
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!App.DeleteConfirm(false, this))
                return;

            if (App.SendDelete("Asset", Glo.Tab.ASSET_ID, id.ToString(), true, this))
            {
                if (MainWindow.pageDatabase != null)
                    MainWindow.pageDatabase.RepeatSearches(1);
                changesMade = true;
                Close();
            }
        }

        private void SqlDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

        }

        // Pick up history only on the first time the tab is clicked. After that, the user will need to click Refresh.
        bool firstHistoryFocus = false;
        private void tabHistory_GotFocus(object sender, RoutedEventArgs e)
        {
            if (!firstHistoryFocus)
            {
                GetHistory();
                firstHistoryFocus = true;
            }
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            GetHistory();
        }

        private void ToggleFieldsEnabled(bool enabled)
        {
            txtAssetRef.IsReadOnly = !enabled;
            cmbOrgRef.IsEnabled = enabled;
            txtNotes.IsReadOnly = !enabled;
            ditAsset.ToggleFieldsEnabled(enabled);
        }

        private void dtgChangeLog_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            List<object?> data;
            string selectedID = dtgChangeLog.GetCurrentlySelectedID();
            if (selectedID == "")
                return;

            // Error message will present itself in BuildHistorical() if needed.
            if (App.BuildHistorical("Asset", selectedID, id, out data, this))
            {
                NewAsset viewAsset = new(id, dtgChangeLog.GetCurrentlySelectedCell(1));
                viewAsset.PopulateExistingData(data);
                viewAsset.ToggleFieldsEnabled(false);
                viewAsset.lblViewingChange.Height = 20;
                viewAsset.lblViewingChange.Content = "Viewing change at " + dtgChangeLog.GetCurrentlySelectedCell(1);
                viewAsset.Show();
            }
        }

        // Check for changes whenever the screen something is with.
        private void ValueChanged(object sender, EventArgs e) { AnyInteraction(); }
        public bool AnyInteraction()
        {
            if (!IsLoaded)
                return false;

            string currentOrgID = cmbOrgRef.SelectedItem == null ? "" : (string)cmbOrgRef.SelectedItem;
            changesMade = originalRef != txtAssetRef.Text ||
                          originalOrg != currentOrgID ||
                          originalNotes != txtNotes.Text ||
                          ditAsset.CheckForValueChanges();
            btnEdit.IsEnabled = changesMade;
            return true; // Only because Func<void> isn't legal, and this needs feeding to ditOrganisation.
        }

        

        private void btnCorrectReason_Click(object sender, RoutedEventArgs e)
        {
            string changeIdString = dtgChangeLog.GetCurrentlySelectedID();
            string changeReason = dtgChangeLog.GetCurrentlySelectedCell(3);
            int changeID;
            if (int.TryParse(changeIdString, out changeID))
            {
                DialogChangeReason changeDialog = new DialogChangeReason("Asset", changeID, changeReason);
                changeDialog.ShowDialog();
                if (changeDialog.DialogResult == true)
                    GetHistory();
            }
            else
                App.DisplayError("Please select a change record.", this);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            App.WindowClosed();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // CustomWindow.Window_Loaded() already handles this, but this window needs a little more attention as the
            // screen width is variable between tabs, being far wider on the change tab.
            // As this window is not resizable currently, we also want to live a little bit of wiggle room so that
            // dragging the window around while keeping the buttons visible isn't too annoying.
            double screenHeight = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Height;
            double screenWidth = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Width;

            if (MaxHeight > screenHeight - 120)
                MaxHeight = screenHeight - 120;
            if (MaxWidth > screenWidth - 120)
                MaxWidth = screenWidth - 120;

            if (Top + ActualHeight > screenHeight)
                // No idea why + 6, maybe it's the difference between the standard WPF title bar and mine.
                Top = screenHeight - (ActualHeight + 6);
            if (Top < 0)
                Top = 0;
            if (Left + 1000 > screenWidth)
                Left = screenWidth - 1000;
            if (Left < 0)
                Left = 0;
        }

        private void btnOpenOrganisation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cmbOrgRef.Text == "")
                    throw new("No organisation has been selected.");

                List<List<object?>> rows = new();
                if (App.Select("Organisation", new() { Glo.Tab.ORGANISATION_ID },
                                               new() { Glo.Tab.ORGANISATION_REF },
                                               new() { cmbOrgRef.Text },
                                               new() { Conditional.Equals },
                                               out _, out rows, false, false, this) &&
                    rows.Count > 0 && rows[0].Count > 0)
                {
                    if (rows[0][0] is int i)
                    {
                        App.EditOrganisation(i.ToString(), this);
                        return;
                    }
                }
                throw new("Could not discern organisation from reference.");
            }
            catch (Exception ex)
            {
                App.DisplayError(ex.Message, this);
                return;
            }
        }

        private void cmbOrgRef_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnOpenOrganisation.IsEnabled = cmbOrgRef.SelectedIndex >= 0 &&
                                            cmbOrgRef.Items[cmbOrgRef.SelectedIndex] is string s &&
                                            s != "";

            AnyInteraction();
        }
    }
}