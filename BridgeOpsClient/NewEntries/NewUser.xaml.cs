﻿using SendReceiveClasses;
using System;
using System.Collections.Generic;
using System.Windows;

namespace BridgeOpsClient
{
    public partial class NewUser : Window
    {
        bool edit = false;
        public int id = 0;
        string originalUsername = "";
        string originalPassword = "";
        int originalType = 0;

        string? originalNotes = "";
        public NewUser()
        {
            InitializeComponent();
            InitialiseFields();
        }
        public NewUser(int id)
        {
            this.id = id;

            InitializeComponent();
            InitialiseFields();

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
            else if (txtPassword != txtPasswordConfirm)
            {
                MessageBox.Show("Passwords do not match.");
                return;
            }

            nl.loginID = id;
            nl.username = txtUsername.Text;
            nl.password = txtPassword.Text;
            //nl.type = type

            if (App.SendInsert(Glo.CLIENT_NEW_LOGIN, nl))
                Close();
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
    }
}
