using BridgeOpsClient.DialogWindows;
using SendReceiveClasses;
using System;
using System.Collections.Generic;
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
    public partial class NewAsset : Window
    {
        bool edit = false;
        string id = "";
        string? originalOrgID = "";
        string? originalNotes = "";
        public NewAsset()
        {
            InitializeComponent();
            InitialiseFields();

            tabChangeLog.IsEnabled = false;
        }
        public NewAsset(string id)
        {
            InitializeComponent();
            InitialiseFields();

            edit = true;
            btnAdd.Visibility = Visibility.Hidden;
            btnEdit.Visibility = Visibility.Visible;
            btnDelete.Visibility = Visibility.Visible;
            this.id = id;
            Title = "Asset " + id;

            txtAssetID.Text = id;
            txtAssetID.IsReadOnly = true;
        }
        public NewAsset(string id, string record)
        {
            InitializeComponent();
            InitialiseFields();

            btnAdd.Visibility = Visibility.Hidden;
            btnEdit.Visibility = Visibility.Hidden;
            btnDelete.Visibility = Visibility.Hidden;
            this.id = id;
            Title = "Asset " + id + " Change";

            txtAssetID.Text = id;
            txtAssetID.IsReadOnly = true;
            cmbOrgID.IsEditable = true; // This makes it so we can set the value without loading the list of IDs.
            ToggleFieldsEnabled(false);

            tabChangeLog.IsEnabled = false;
        }

        private void InitialiseFields()
        {
            // Implement max lengths. Max lengths in the DataInputTable are set automatically.
            txtAssetID.MaxLength = ColumnRecord.asset["Asset_ID"].restriction;
            txtNotes.MaxLength = ColumnRecord.asset["Notes"].restriction;

            // Implement friendly names.
            if (ColumnRecord.asset["Asset_ID"].friendlyName != "")
                lblAssetID.Content = ColumnRecord.asset["Asset_ID"].friendlyName;
            if (ColumnRecord.asset["Organisation_ID"].friendlyName != "")
                lblOrgID.Content = ColumnRecord.asset["Organisation_ID"].friendlyName;
            if (ColumnRecord.asset["Notes"].friendlyName != "")
                lblNotes.Content = ColumnRecord.asset["Notes"].friendlyName;

            ditAsset.Initialise(ColumnRecord.asset, "Asset");
        }

        public void Populate(List<object?> data)
        {
#pragma warning disable CS8602
            // This method will not be called if the data has a different Count than expected.
            if (data[1] != null)
                cmbOrgID.Text = data[1].ToString();
            else
                cmbOrgID.Text = null;
            if (data[2] != null)
                txtNotes.Text = data[2].ToString();
            else
                txtNotes.Text = null;

            // Store the original values to check if any changes have been made to the data. The same takes place
            // in the data input table.
            originalOrgID = cmbOrgID.Text;
            originalNotes = txtNotes.Text;

            ditAsset.Populate(data.GetRange(3, data.Count - 3));
            if (edit)
                ditAsset.RememberStartingValues();
#pragma warning restore CS8602
        }

        private void GetHistory()
        {
            // Error message is displayed by App.SelectAll() if something goes wrong.
            List<string?> columnNames;
            List<List<object?>> rows;

            if (App.SelectHistory("AssetChange", id, out columnNames, out rows))
                dtgChangeLog.Update(new List<Dictionary<string, ColumnRecord.Column>>()
                                   { ColumnRecord.assetChange, ColumnRecord.login }, columnNames, rows,
                                   Glo.Tab.CHANGE_ID);
        }

        public string[]? organisationList;
        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (ditAsset.ScoopValues())
            {
                if (txtAssetID.Text == "")
                {
                    MessageBox.Show("You must input a value for Asset ID");
                    return;
                }

                Asset newAsset = new();

                newAsset.sessionID = App.sd.sessionID;

                newAsset.assetID = txtAssetID.Text;
                newAsset.organisationID = cmbOrgID.Text.Length == 0 ? null : cmbOrgID.Text;
                newAsset.notes = txtNotes.Text.Length == 0 ? null : txtNotes.Text;

                ditAsset.ExtractValues(out newAsset.additionalCols, out newAsset.additionalVals);

                // Obtain types and determine whether or not quotes will be needed.
                newAsset.additionalNeedsQuotes = new();
                foreach (string c in newAsset.additionalCols)
                    newAsset.additionalNeedsQuotes.Add(SqlAssist.NeedsQuotes(ColumnRecord.asset[c].type));

                if (App.SendInsert(Glo.CLIENT_NEW_ASSET, newAsset))
                    Close();
                else
                    MessageBox.Show("Could not create asset.");
            }
            else
            {
                string message = "One or more values caused an unknown error to occur.";
                if (ditAsset.disallowed.Count > 0)
                    message = ditAsset.disallowed[0];
                MessageBox.Show(message);
            }
        }

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (ditAsset.ScoopValues())
            {
                Asset asset = new Asset();
                asset.sessionID = App.sd.sessionID;
                asset.assetID = id;
                asset.changeTracked = true;
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
                    asset.additionalNeedsQuotes.Add(SqlAssist.NeedsQuotes(ColumnRecord.asset[c].type));

                asset.additionalCols = cols;
                asset.additionalVals = vals;

                // Add the known fields if changed.
                if (cmbOrgID.Text != originalOrgID)
                {
                    asset.organisationID = cmbOrgID.Text;
                    asset.organisationIdChanged = true;
                }
                if (txtNotes.Text != originalNotes)
                {
                    asset.notes = txtNotes.Text;
                    asset.notesChanged = true;
                }

                DialogChangeReason reasonDialog = new("Asset");
                bool? result = reasonDialog.ShowDialog();
                if (result != null && result == true)
                {
                    asset.changeReason = reasonDialog.txtReason.Text;
                    if (App.SendUpdate(Glo.CLIENT_UPDATE_ASSET, asset))
                        Close();
                    else
                        MessageBox.Show("Could not edit asset.");
                }
            }
            else
            {
                string message = "One or more values caused an unknown error to occur.";
                if (ditAsset.disallowed.Count > 0)
                    message = ditAsset.disallowed[0];
                MessageBox.Show(message);
            }
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (App.SendDelete("Asset", Glo.Tab.ASSET_ID, id, true))
                Close();
            else
                MessageBox.Show("Could not delete asset.");
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
            cmbOrgID.IsEnabled = enabled;
            txtNotes.IsReadOnly = !enabled;
            ditAsset.ToggleFieldsEnabled(enabled);
            btnEdit.IsEnabled = enabled;
            btnDelete.IsEnabled = enabled;
        }

        private void dtgChangeLog_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            List<object?> data;
            string selectedID = dtgChangeLog.GetCurrentlySelectedID();
            if (selectedID == "")
                return;

            // Error message will present itself in BuildHistorical() if needed.
            if (App.BuildHistorical("Asset", selectedID, id, out data))
            {
                NewAsset viewAsset = new(id, dtgChangeLog.GetCurrentlySelectedCell(1));
                viewAsset.Populate(data);
                viewAsset.ToggleFieldsEnabled(false);
                viewAsset.lblViewingChange.Height = 20;
                viewAsset.lblViewingChange.Content = "Viewing change at " + dtgChangeLog.GetCurrentlySelectedCell(1);
                viewAsset.Show();
            }
        }
    }
}