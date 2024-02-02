using BridgeOpsClient.DialogWindows;
using SendReceiveClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace BridgeOpsClient
{
    public partial class NewOrganisation : Window
    {
        bool edit = false;
        string id = "";
        string? originalParent = "";
        string? originalDialNo = "";
        string? originalNotes = "";
        public NewOrganisation() // New organisation.
        {
            InitializeComponent();
            InitialiseFields();

            tabAssetsContacts.IsEnabled = false;
            tabChangeLog.IsEnabled = false;
        }
        public NewOrganisation(string id)
        {
            InitializeComponent();
            InitialiseFields();

            edit = true;
            btnAdd.Visibility = Visibility.Hidden;
            btnEdit.Visibility = Visibility.Visible;
            btnDelete.Visibility = Visibility.Visible;
            this.id = id;
            Title = "Organisation " + id;

            txtOrgID.Text = id;
            txtOrgID.IsReadOnly = true;

            // Sort out contact and asset tables.
            PopulateAssets();
            PopulateContacts();
        } // Edit existing record.
        public NewOrganisation(string id, string record) // History lookup.
        {
            InitializeComponent();
            InitialiseFields();

            btnAdd.Visibility = Visibility.Hidden;
            btnEdit.Visibility = Visibility.Hidden;
            btnDelete.Visibility = Visibility.Hidden;
            this.id = id;
            Title = "Organisation " + id + " Change";

            txtOrgID.Text = id;
            txtOrgID.IsReadOnly = true;
            cmbOrgParentID.IsEditable = true; // This makes it so we can set the value without loading the list of IDs.
            ToggleFieldsEnabled(false);

            tabAssetsContacts.IsEnabled = false;
            tabChangeLog.IsEnabled = false;
        }

        private void InitialiseFields()
        {
            ditOrganisation.Initialise(ColumnRecord.organisation, "Organisation");

            // Implement max lengths. Max lengths in the DataInputTable are set automatically.
            txtOrgID.MaxLength = ColumnRecord.organisation["Organisation_ID"].restriction;
            txtDialNo.MaxLength = ColumnRecord.organisation["Dial_No"].restriction;
            txtNotes.MaxLength = ColumnRecord.organisation["Notes"].restriction;

            // Implement friendly names.
            if (ColumnRecord.organisation["Organisation_ID"].friendlyName != "")
                lblOrgID.Content = ColumnRecord.organisation["Organisation_ID"].friendlyName;
            if (ColumnRecord.organisation["Parent_ID"].friendlyName != "")
                lblOrgParentID.Content = ColumnRecord.organisation["Parent_ID"].friendlyName;
            if (ColumnRecord.organisation["Dial_No"].friendlyName != "")
                lblDialNo.Content = ColumnRecord.organisation["Dial_No"].friendlyName;
            if (ColumnRecord.organisation["Notes"].friendlyName != "")
                lblNotes.Content = ColumnRecord.organisation["Notes"].friendlyName;
        }

        public void PopulateAssets()
        {
            // Error message is displayed by App.SelectAll() if something goes wrong.
            List<string?> columnNames;
            List<List<object?>> rows;

            if (App.SelectAll("Asset", Glo.Tab.ORGANISATION_ID, id, out columnNames, out rows))
                dtgAssets.Update(ColumnRecord.asset, columnNames, rows, Glo.Tab.ORGANISATION_ID);
        }
        public void PopulateContacts()
        {
            // Error message is displayed by App.SelectAll() if something goes wrong.
            List<string?> columnNames;
            List<List<object?>> rows;

            if (App.LinkedContactSelect(id, out columnNames, out rows))
                dtgContacts.Update(ColumnRecord.contact, columnNames, rows, Glo.Tab.CONTACT_ID);
        }

        public void PopulateExistingData(List<object?> data)
        {
#pragma warning disable CS8602
            // This method will not be called if the data has a different Count than expected.
            if (data[1] != null)
                cmbOrgParentID.Text = data[1].ToString();
            else
                cmbOrgParentID.Text = null;
            if (data[2] != null)
                txtDialNo.Text = data[2].ToString();
            else
                txtDialNo.Text = null;
            if (data[3] != null)
                txtNotes.Text = data[3].ToString();
            else
                txtNotes.Text = null;

            // Store the original values to check if any changes have been made to the data. The same takes place
            // in the data input table.
            originalParent = cmbOrgParentID.Text;
            originalDialNo = txtDialNo.Text;
            originalNotes = txtNotes.Text;

            ditOrganisation.ValueChangedHandler = AnyInteraction; // Must be set before Populate().
            ditOrganisation.Populate(data.GetRange(4, data.Count - 4));
            if (edit)
                ditOrganisation.RememberStartingValues();

            btnEdit.IsEnabled = false;
#pragma warning restore CS8602
        }

        public string[]? organisationList;
        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (ditOrganisation.ScoopValues())
            {
                if (txtOrgID.Text == "")
                {
                    MessageBox.Show("You must input a value for Organisation ID");
                    return;
                }

                Organisation newOrg = new Organisation();

                newOrg.sessionID = App.sd.sessionID;

                newOrg.organisationID = txtOrgID.Text;
                newOrg.parentOrgID = cmbOrgParentID.Text.Length == 0 ? null : cmbOrgParentID.Text;
                newOrg.dialNo = txtDialNo.Text.Length == 0 ? null : txtDialNo.Text;
                newOrg.notes = txtNotes.Text.Length == 0 ? null : txtNotes.Text;

                ditOrganisation.ExtractValues(out newOrg.additionalCols, out newOrg.additionalVals);

                // Obtain types and determine whether or not quotes will be needed.
                newOrg.additionalNeedsQuotes = new();
                foreach (string c in newOrg.additionalCols)
                    newOrg.additionalNeedsQuotes.Add(SqlAssist.NeedsQuotes(ColumnRecord.organisation[c].type));

                if (App.SendInsert(Glo.CLIENT_NEW_ORGANISATION, newOrg))
                    Close();
                else
                    MessageBox.Show("Could not create organisation.");
            }
            else
            {
                string message = "One or more values caused an unknown error to occur.";
                if (ditOrganisation.disallowed.Count > 0)
                    message = ditOrganisation.disallowed[0];
                MessageBox.Show(message);
            }
        }

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (ditOrganisation.ScoopValues())
            {
                Organisation org = new Organisation();
                org.sessionID = App.sd.sessionID;
                org.organisationID = id;
                org.changeTracked = true;
                List<string> cols;
                List<string?> vals;
                ditOrganisation.ExtractValues(out cols, out vals);

                // Remove any values equal to their starting value.
                List<int> toRemove = new();
                for (int i = 0; i < vals.Count; ++i)
                    if (ditOrganisation.startingValues[i] == vals[i])
                        toRemove.Add(i);
                int mod = 0; // Each one we remove, we need to take into account that the list is now 1 less.
                foreach (int i in toRemove)
                {
                    cols.RemoveAt(i - mod);
                    vals.RemoveAt(i - mod);
                    ++mod;
                }

                // Obtain types and determine whether or not quotes will be needed.
                org.additionalNeedsQuotes = new();
                foreach (string c in cols)
                    org.additionalNeedsQuotes.Add(SqlAssist.NeedsQuotes(ColumnRecord.organisation[c].type));

                org.additionalCols = cols;
                org.additionalVals = vals;

                // Add the known fields if changed.
                if ((string?)cmbOrgParentID.SelectedItem != originalParent)
                {
                    org.parentOrgID = cmbOrgParentID.Text;
                    org.parentOrgIdChanged = true;
                }
                if (txtDialNo.Text != originalDialNo)
                {
                    org.dialNo = txtDialNo.Text;
                    org.dialNoChanged = true;
                }
                if (txtNotes.Text != originalNotes)
                {
                    org.notes = txtNotes.Text;
                    org.notesChanged = true;
                }

                DialogChangeReason reasonDialog = new("Organisation");
                bool? result = reasonDialog.ShowDialog();
                if (result != null && result == true)
                {
                    org.changeReason = reasonDialog.txtReason.Text;
                    if (App.SendUpdate(Glo.CLIENT_UPDATE_ORGANISATION, org))
                        Close();
                    else
                        MessageBox.Show("Could not edit organisation.");
                }
            }
            else
            {
                string message = "One or more values caused an unknown error to occur.";
                if (ditOrganisation.disallowed.Count > 0)
                    message = ditOrganisation.disallowed[0];
                MessageBox.Show(message);
            }
        }

        private void GetHistory()
        {
            // Error message is displayed by App.SelectAll() if something goes wrong.
            List<string?> columnNames;
            List<List<object?>> rows;

            if (App.SelectHistory("OrganisationChange", id, out columnNames, out rows))
                dtgChangeLog.Update(new List<Dictionary<string, ColumnRecord.Column>>()
                                   { ColumnRecord.organisationChange, ColumnRecord.login }, columnNames, rows,
                                   Glo.Tab.CHANGE_ID);
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (App.SendDelete("Organisation", Glo.Tab.ORGANISATION_ID, id, true))
                Close();
            else
                MessageBox.Show("Could not delete organisation.");
        }

        // Bring up selected asset on double-click.
        private void dtgAssets_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            string currentID = dtgAssets.GetCurrentlySelectedID();
            if (currentID != "")
                App.EditAsset(dtgAssets.GetCurrentlySelectedID());
        }

        // Bring up selected contact on double-click.
        private void dtgContacts_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            string currentID = dtgContacts.GetCurrentlySelectedID();
            if (currentID != "")
                App.EditContact(dtgContacts.GetCurrentlySelectedID());
        }

        private void btnAssetNew_Click(object sender, RoutedEventArgs e)
        {
            NewAsset newAsset = new();

            // Set cmbOrgID to editable but disabled in order to hold the ID without fetching the list from the agent.
            newAsset.cmbOrgID.IsEditable = true;
            newAsset.cmbOrgID.Text = id;
            newAsset.cmbOrgID.IsEnabled = false;

            newAsset.ShowDialog();
            PopulateAssets();
        }

        private void btnAssetAdd_Click(object sender, RoutedEventArgs e)
        {
            LinkRecord lr = new("Asset", ColumnRecord.asset);
            lr.ShowDialog();
            string? assetID = lr.id;
            if (assetID == null)
                return;

            Asset asset = new Asset(App.sd.sessionID, assetID, id, null, new(), new(), new());
            asset.organisationIdChanged = true;
            if (App.SendUpdate(Glo.CLIENT_UPDATE_ASSET, asset))
                PopulateAssets();
            else
                MessageBox.Show("Could not update specified asset.");
        }

        private void btnAssetRemove_Click(object sender, RoutedEventArgs e)
        {
            string? assetID = dtgAssets.GetCurrentlySelectedID();
            if (assetID == null)
            {
                MessageBox.Show("Could not discern asset ID from record.");
                return;
            }
            Asset asset = new Asset(App.sd.sessionID, assetID, null, null, new(), new(), new());
            asset.organisationIdChanged = true;
            if (App.SendUpdate(Glo.CLIENT_UPDATE_ASSET, asset))
                PopulateAssets();
            else
                MessageBox.Show("Could not update specified asset.");
        }

        private void btnAssetsRefresh_Click(object sender, RoutedEventArgs e)
        {
            PopulateAssets();
        }

        private void btnContactsNew_Click(object sender, RoutedEventArgs e)
        {
            NewContact newContact = new();
            newContact.requireIdBack = true;
            newContact.isDialog = true;

            bool? completed = newContact.ShowDialog();

            int contactID;
            if (completed == true)
            {
                if (int.TryParse(newContact.id, out contactID))
                {
                    // Error message presented by LinkContact() if needed.
                    App.LinkContact(id, contactID, false);
                    PopulateContacts();
                }
                else
                {
                    MessageBox.Show("Contact could not be linked to organisation.");
                }
            }
            // else the NewContact form was closed without being completed.
        }

        private void btnContactsAdd_Click(object sender, RoutedEventArgs e)
        {
            LinkRecord lr = new("Contact", ColumnRecord.contact);
            lr.ShowDialog();
            int contactIdInt;
            if (lr.id == null || !int.TryParse(lr.id, out contactIdInt))
                return;

            // Error message handled in LinkContact().
            if (App.LinkContact(id, contactIdInt, false))
                PopulateContacts();
        }

        private void btnContactsRemove_Click(object sender, RoutedEventArgs e)
        {
            string? contactID = dtgContacts.GetCurrentlySelectedID();
            int contactIdInt;
            if (contactID == null || !int.TryParse(contactID, out contactIdInt))
            {
                MessageBox.Show("Could not discern contact ID from record.");
                return;
            }
            if (App.LinkContact(id, contactIdInt, true))
                PopulateContacts();
            else
                MessageBox.Show("Could not update specified asset.");
        }

        private void btnContactsRefresh_Click(object sender, RoutedEventArgs e)
        {
            PopulateContacts();
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
            cmbOrgParentID.IsEnabled = enabled;
            txtDialNo.IsReadOnly = !enabled;
            txtNotes.IsReadOnly = !enabled;
            ditOrganisation.ToggleFieldsEnabled(enabled);
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
            if (App.BuildHistorical("Organisation", selectedID, id, out data))
            {
                NewOrganisation viewOrg = new(id, dtgChangeLog.GetCurrentlySelectedCell(1));
                viewOrg.PopulateExistingData(data);
                viewOrg.lblViewingChange.Height = 20;
                string date = dtgChangeLog.GetCurrentlySelectedCell(1);
                viewOrg.lblViewingChange.Content = "Viewing change at " + date;
                viewOrg.Show();
            }
        }

        // Check for changes whenever the screen something is with.
        private void ValueChanged(object sender, EventArgs e) { AnyInteraction(); }
        public bool AnyInteraction()
        {
            if (!IsLoaded)
                return false;

            string currentParent = cmbOrgParentID.SelectedItem == null ? "" : (string)cmbOrgParentID.SelectedItem;
            btnEdit.IsEnabled = originalParent != currentParent ||
                                originalDialNo != txtDialNo.Text ||
                                originalNotes != txtNotes.Text ||
                                ditOrganisation.CheckForValueChanges();
            return true; // Only because Func<void> isn't legal, and this needs feeding to ditOrganisation.
        }
    }
}
