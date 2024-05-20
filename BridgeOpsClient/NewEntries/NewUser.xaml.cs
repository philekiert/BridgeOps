using SendReceiveClasses;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace BridgeOpsClient
{
    public partial class NewUser : Window
    {
        bool edit = false;
        public bool didSomething = false;
        public int id = 0;
        string originalUsername = "";
        string originalPassword = "";
        bool originalAdmin = false;
        int originalCreate = 0;
        int originalEdit = 0;
        int originalDelete = 0;
        int currentCreate = 0;
        int currentEdit = 0;
        int currentDelete = 0;

        string? originalNotes = "";
        public NewUser()
        {
            InitializeComponent();
            InitialiseFields();
            GetCheckBoxArray();
        }
        public NewUser(int id)
        {
            this.id = id;

            InitializeComponent();
            InitialiseFields();
            GetCheckBoxArray();

            edit = true;
            btnAdd.Visibility = Visibility.Hidden;
            btnEdit.Visibility = Visibility.Visible;
            btnDelete.Visibility = Visibility.Visible;
        }

        private void InitialiseFields()
        {
            // Implement max length. Max lengths in the DataInputTable are set automatically.
            txtUsername.MaxLength = ColumnRecord.login["Username"].restriction;
            txtPassword.MaxLength = ColumnRecord.login["Password"].restriction;
            txtPasswordConfirm.MaxLength = ColumnRecord.login["Password"].restriction;

            // I don't think there's any reason to implement friendly names for user accounts. If they're added, they
            // just won't do anything, and I think that's fine.
        }

        CheckBox[,] permissionsGrid = new CheckBox[4, 6];
        bool[] createPermissions = new bool[6];
        bool[] editPermissions = new bool[6];
        bool[] deletePermissions = new bool[6];
        private void GetCheckBoxArray()
        {
            foreach (UIElement checkBox in grdPermissions.Children)
                if (checkBox is CheckBox)
                    permissionsGrid[Grid.GetColumn(checkBox) - 1, Grid.GetRow(checkBox) - 1] = (CheckBox)checkBox;
        }

#pragma warning disable CS8602
        public void Populate(List<object?> data)
        {
            // EDIT
            // This method will not be called if the data has a different Count than expected.
            //if (data[1] != null)
            //    txtUsername.Text = data[0].ToString();
            //if (data[2] != null)
            //    txtUsername.Text = data[0].ToString();
            //if (data[1] != null)
            //    txtUsername.Text = data[0].ToString();

            // Store the original values to check if any changes have been made for the data. The same takes place
            // in the data input table.
            originalUsername = txtUsername.Text == null ? "" : txtUsername.Text;
            originalPassword = txtPassword.Text == null ? "" : txtPassword.Text;

            // Need a function that converts the int to individual bools.
            //originalAdmin = chkAdmin.IsChecked == null ? false : (bool)chkAdmin.IsChecked;
            //originalRecordAddEdit = chkRecordsAddEdit.IsChecked == null ? false : (bool)chkRecordsAddEdit.IsChecked;
            //originalRecordDelete = chkRecordsDelete.IsChecked == null ? false : (bool)chkRecordsDelete.IsChecked;
            //originalResourceAddEdit = chkResourcesAddEdit.IsChecked == null ? false : (bool)chkResourcesAddEdit.IsChecked;
            //originalResourceDelete = chkResourcesDelete.IsChecked == null ? false : (bool)chkResourcesDelete.IsChecked;
            //originalConferenceTypeAddEdit = chkConfTypesAddEdit.IsChecked == null ? false : (bool)chkConfTypesAddEdit.IsChecked;
            //originalConferenceTypeDelete = chkConferenceTypesDelete.IsChecked == null ? false : (bool)chkConferenceTypesDelete.IsChecked;
            //originalReportAddEditDelete = chkReportsAddEditDelete.IsChecked == null ? false : (bool)chkReportsAddEditDelete.IsChecked;
            //originalAccountManagement = chkUserAccountManagement.IsChecked == null ? false : (bool)chkUserAccountManagement.IsChecked;
        }
#pragma warning restore CS8602

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            Login nl = new Login();

            nl.sessionID = App.sd.sessionID;

            if (txtUsername.Text == "")
            {
                MessageBox.Show("You must input a value for Username ID");
                return;
            }
            else if (txtPassword.Text != txtPasswordConfirm.Text)
            {
                MessageBox.Show("Passwords do not match.");
                return;
            }

            nl.loginID = id;
            nl.username = txtUsername.Text;
            nl.password = txtPassword.Text;
            if (chkAdmin.IsChecked != null && chkAdmin.IsChecked == true)
            {
                nl.admin = true;
                nl.createPermissions = 255;
                nl.editPermissions = 255;
                nl.deletePermissions = 255;
            }
            else
            {
                nl.admin = false;
                UpdateWriteEditDelete();
                nl.createPermissions = currentCreate;
                nl.editPermissions = currentEdit;
                nl.deletePermissions = currentDelete;
            }

            if (App.SendInsert(Glo.CLIENT_NEW_LOGIN, nl))
            {
                didSomething = true;
                Close();
            }
            else
            {
                // There shouldn't be any errors with insert on this one, as everything is either text or null.
                MessageBox.Show("Could not create new user.");
            }
        }

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            //int idInt;
            //if (!int.TryParse(id, out idInt))
            //{
            //    // This should never trigger as the ID cannot be adjusted, but just to be diligent...
            //    MessageBox.Show("Customer ID is invalid, cannot edit record.");
            //    return;
            //}
            //if (ditContact.ScoopValues())
            //{
            //    Contact contact = new Contact();
            //    contact.sessionID = App.sd.sessionID;
            //    contact.contactID = idInt;
            //    List<string> cols;
            //    List<string?> vals;
            //    ditContact.ExtractValues(out cols, out vals);

            //    // Remove any values equal to their starting value.
            //    List<int> toRemove = new();
            //    for (int i = 0; i < vals.Count; ++i)
            //        if (ditContact.startingValues[i] == vals[i])
            //            toRemove.Add(i);
            //    int mod = 0; // Each one we remove, we need to take into account that the list is now 1 less.
            //    foreach (int i in toRemove)
            //    {
            //        cols.RemoveAt(i - mod);
            //        vals.RemoveAt(i - mod);
            //        ++mod;
            //    }

            //    // Obtain types and determine whether or not quotes will be needed.
            //    contact.additionalNeedsQuotes = new();
            //    foreach (string c in cols)
            //        contact.additionalNeedsQuotes.Add(SqlAssist.NeedsQuotes(ColumnRecord.contact[c].type));

            //    contact.additionalCols = cols;
            //    contact.additionalVals = vals;

            //    // Add the known fields if changed.
            //    if (txtNotes.Text != originalNotes)
            //    {
            //        contact.notes = txtNotes.Text;
            //        contact.notesChanged = true;
            //    }

            //    if (App.SendUpdate(Glo.CLIENT_UPDATE_CONTACT, contact))
            //        Close();
            //    else
            //        MessageBox.Show("Could not edit contact.");
            //}
            //else
            //{
            //    string message = "One or more values caused an unknown error to occur.";
            //    if (ditContact.disallowed.Count > 0)
            //        message = ditContact.disallowed[0];
            //    MessageBox.Show(message);
            //}
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            //if (App.SendDelete("Contact", Glo.Tab.CONTACT_ID, id, false))
            //    Close();
            //else
            //    MessageBox.Show("Could not delete contact.");
        }

        // Check for changes whenever the screen something is with.
        private void ValueChanged(object sender, EventArgs e) { AnyInteraction(); }
        public bool AnyInteraction()
        {
            // EDIT !!!
            //btnEdit.IsEnabled = originalNotes != txtNotes.Text ||
            //                    ditContact.CheckForValueChanges();
            return true; // Only because Func<void> isn't legal, and this needs feeding to ditOrganisation.
        }

        private void chkAdmin_Clicked(object sender, RoutedEventArgs e)
        {
            if (chkAdmin.IsChecked == true)
                grdPermissions.Visibility = Visibility.Collapsed;
            else
                grdPermissions.Visibility = Visibility.Visible;
        }

        // Handle the All column when switching boxes on and off.
        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            int gridX = Grid.GetColumn((CheckBox)sender) - 1;
            int gridY = Grid.GetRow((CheckBox)sender) - 1;

            if (gridX == 3)
            {
                bool toSet = true;
                if (permissionsGrid[0, gridY].IsChecked == true &&
                    permissionsGrid[1, gridY].IsChecked == true &&
                    permissionsGrid[2, gridY].IsChecked == true)
                    toSet = false;
                permissionsGrid[0, gridY].IsChecked = toSet;
                permissionsGrid[1, gridY].IsChecked = toSet;
                permissionsGrid[2, gridY].IsChecked = toSet;
            }
            else
            {
                if (permissionsGrid[0, gridY].IsChecked == true &&
                    permissionsGrid[1, gridY].IsChecked == true &&
                    permissionsGrid[2, gridY].IsChecked == true)
                    permissionsGrid[3, gridY].IsChecked = true;
                else
                    permissionsGrid[3, gridY].IsChecked = false;

            }
        }
        private void UpdateWriteEditDelete()
        {
            for (int x = 0; x < 3; ++x) // No need to go over the "All" box.
            {
                int permissions = 0;
                for (int y = 0; y < 6; ++y)
                {
                    if (permissionsGrid[x, y].IsChecked == true)
                        permissions += 1 << y;
                }
                if (x == 0)
                    currentCreate = permissions;
                else if (x == 1)
                    currentEdit = permissions;
                else
                    currentDelete = permissions;
            }
        }
    }
}
