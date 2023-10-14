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

            // Implement max lengths. Max lengths in the DataInputTable are set automatically.
            txtAssetID.MaxLength = ColumnRecord.asset["Asset_ID"].restriction;
            txtNotes.MaxLength = ColumnRecord.asset["Notes"].restriction;
        }
        public NewAsset(string id)
        {
            this.id = id;

            InitializeComponent();

            // Implement max length. Max lengths in the DataInputTable are set automatically.
            txtNotes.MaxLength = ColumnRecord.asset["Notes"].restriction;

            edit = true;
            btnAdd.Visibility = Visibility.Hidden;
            btnEdit.Visibility = Visibility.Visible;
            btnDelete.Visibility = Visibility.Visible;

            txtAssetID.Text = id;
            txtAssetID.IsReadOnly = true;
        }

#pragma warning disable CS8602
        public void Populate(List<object?> data)
        {
            // This method will not be called if the data has a different Count than expected.
            if (data[1] != null)
                cmbOrgID.Text = data[1].ToString();
            if (data[2] != null)
                txtNotes.Text = data[2].ToString();

            // Store the original values to check if any changes have been made for the data. The same takes place
            // in the data input table.
            originalOrgID = cmbOrgID.Text;
            originalNotes = txtNotes.Text;

            ditAsset.Populate(data.GetRange(3, data.Count - 3));
            if (edit)
                ditAsset.RememberStartingValues();
        }
#pragma warning restore CS8602

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

                Asset ao = new();

                ao.sessionID = App.sd.sessionID;

                ao.assetID = txtAssetID.Text;
                ao.organisationID = cmbOrgID.Text.Length == 0 ? null : cmbOrgID.Text;
                ao.notes = txtNotes.Text.Length == 0 ? null : txtNotes.Text;

                ditAsset.ExtractValues(out ao.additionalCols, out ao.additionalVals);

                // Obtain types and determine whether or not quotes will be needed.
                ao.additionalNeedsQuotes = new();
                foreach (string c in ao.additionalCols)
                    ao.additionalNeedsQuotes.Add(SqlAssist.NeedsQuotes(ColumnRecord.asset[c].type));

                if (App.SendInsert(Glo.CLIENT_NEW_ASSET, ao))
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

                if (App.SendUpdate(Glo.CLIENT_UPDATE_ASSET, asset))
                    Close();
                else
                    MessageBox.Show("Could not edit asset.");
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
            if (App.SendDelete("Asset", Glo.Tab.ASSET_ID, id, false))
                Close();
            else
                MessageBox.Show("Could not delete contact.");
        }
    }
}
