﻿using SendReceiveClasses;
using System;
using System.Collections.Generic;
using System.Data;
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

namespace BridgeOpsClient
{
    public partial class NewContact : CustomWindow
    {
        bool edit = false;
        public string id = "";
        public bool requireIdBack = false; // Set by caller on load.
        public bool isDialog = false;
        string? originalNotes = "";

        private void ApplyPermissions()
        {
            if (!App.sd.createPermissions[Glo.PERMISSION_RECORDS])
                btnAdd.IsEnabled = false;
            if (!App.sd.editPermissions[Glo.PERMISSION_RECORDS])
            {
                btnEdit.IsEnabled = false;
                txtNotes.IsReadOnly = true;
                ditContact.ToggleFieldsEnabled(false);
            }
            if (!App.sd.deletePermissions[Glo.PERMISSION_RECORDS])
                btnDelete.IsEnabled = false;
        }

        public NewContact()
        {
            InitializeComponent();
            InitialiseFields();

            ApplyPermissions();
        }
        public NewContact(string id)
        {
            Title = "Contact";

            this.id = id;

            InitializeComponent();
            InitialiseFields();

            edit = true;
            btnAdd.Visibility = Visibility.Hidden;
            btnEdit.Visibility = Visibility.Visible;
            btnDelete.Visibility = Visibility.Visible;

            ditContact.ValueChangedHandler = AnyInteraction;

            ApplyPermissions();
        }

        private void InitialiseFields()
        {
            ditContact.headers = ColumnRecord.contactHeaders;
            ditContact.Initialise(ColumnRecord.orderedContact, "Contact");

            // Implement max length. Max lengths in the DataInputTable are set automatically.
            txtNotes.MaxLength = Glo.Fun.LongToInt(ColumnRecord.GetColumn(ColumnRecord.contact,
                                                                          Glo.Tab.NOTES).restriction);

            // Implemement friendly name.
            if (ColumnRecord.GetColumn(ColumnRecord.contact, Glo.Tab.NOTES).friendlyName != "")
                lblNotes.Content = ColumnRecord.GetColumn(ColumnRecord.contact, Glo.Tab.NOTES).friendlyName;
        }

#pragma warning disable CS8602
        public void Populate(List<object?> data)
        {
            // This method will not be called if the data has a different Count than expected.
            txtNotes.Text = (string?)data[1];

            // Store the original values to check if any changes have been made for the data. The same takes place
            // in the data input table.
            originalNotes = txtNotes.Text;

            ditContact.Populate(data.GetRange(2, data.Count - 2));
            if (edit)
                ditContact.RememberStartingValues();

            AnyInteraction(); // Call this again, as the order of events above can cause some issues.
        }
#pragma warning restore CS8602

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (ditContact.ScoopValues())
            {
                Contact nc = new Contact();

                // Only switched on when adding from NewOrganisation.
                nc.requireIdBack = requireIdBack;

                nc.sessionID = App.sd.sessionID;
                nc.columnRecordID = ColumnRecord.columnRecordID;
                if (txtNotes.Text.Length == 0)
                    nc.notes = null;
                else
                    nc.notes = txtNotes.Text;

                ditContact.ExtractValues(out nc.additionalCols, out nc.additionalVals);

                // Obtain types and determine whether or not quotes will be needed.
                nc.additionalNeedsQuotes = new List<bool>();
                foreach (string c in nc.additionalCols)
                    nc.additionalNeedsQuotes.Add(
                        SqlAssist.NeedsQuotes(ColumnRecord.GetColumn(ColumnRecord.contact, c).type));

                if (App.SendInsert(Glo.CLIENT_NEW_CONTACT, nc, out id, this))
                {
                    changesMade = true;
                    // Not need to call pageDatabase.RepeatSearches() here, as it can't possibly affected any other
                    // table in the application but the Organisation that added it, if there was one.
                    if (isDialog)
                        DialogResult = true;
                    if (MainWindow.pageDatabase != null)
                        MainWindow.pageDatabase.RepeatSearches(2);
                    Close();
                }
            }
            else
            {
                string message = "One or more values caused an unknown error to occur.";
                if (ditContact.disallowed.Count > 0)
                    message = ditContact.disallowed[0];
                App.DisplayError(message, this);
            }
        }

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            int idInt;
            if (!int.TryParse(id, out idInt))
            {
                // This should never trigger as the ID cannot be adjusted, but just to be diligent...
                App.DisplayError("Contact ID is invalid, cannot edit record.", this);
                return;
            }
            if (ditContact.ScoopValues())
            {
                Contact contact = new Contact();
                contact.sessionID = App.sd.sessionID;
                contact.columnRecordID = ColumnRecord.columnRecordID;
                contact.contactID = idInt;
                List<string> cols;
                List<string?> vals;
                ditContact.ExtractValues(out cols, out vals);

                // Remove any values equal to their starting value.
                List<int> toRemove = new();
                for (int i = 0; i < vals.Count; ++i)
                    if (ditContact.startingValues[i] == vals[i])
                        toRemove.Add(i);
                int mod = 0; // Each one we remove, we need to take into account that the list is now 1 less.
                foreach (int i in toRemove)
                {
                    cols.RemoveAt(i - mod);
                    vals.RemoveAt(i - mod);
                    ++mod;
                }

                // Obtain types and determine whether or not quotes will be needed.
                contact.additionalNeedsQuotes = ditContact.GetNeedsQuotes();
                contact.additionalNeedsQuotes = new();
                foreach (string c in cols)
                    contact.additionalNeedsQuotes.Add(
                        SqlAssist.NeedsQuotes(ColumnRecord.GetColumn(ColumnRecord.contact, c).type));

                contact.additionalCols = cols;
                contact.additionalVals = vals;

                // Add the known fields if changed.
                if (txtNotes.Text != originalNotes)
                {
                    contact.notes = txtNotes.Text;
                    contact.notesChanged = true;
                }

                if (App.SendUpdate(Glo.CLIENT_UPDATE_CONTACT, contact, this))
                {
                    if (MainWindow.pageDatabase != null)
                        MainWindow.pageDatabase.RepeatSearches(2);
                    Close();
                }
            }
            else
            {
                string message = "One or more values caused an unknown error to occur.";
                if (ditContact.disallowed.Count > 0)
                    message = ditContact.disallowed[0];
                App.DisplayError(message, this);
            }
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!App.DeleteConfirm(false, this))
                return;

            if (App.SendDelete("Contact", Glo.Tab.CONTACT_ID, id, false, this))
            {
                changesMade = true;
                if (MainWindow.pageDatabase != null)
                    MainWindow.pageDatabase.RepeatSearches(2);
                Close();
            }
        }

        // Check for changes whenever the user interacts with any control.
        private void ValueChanged(object sender, EventArgs e) { AnyInteraction(); }
        public bool AnyInteraction()
        {
            changesMade = originalNotes != txtNotes.Text ||
                          ditContact.CheckForValueChanges();
            btnEdit.IsEnabled = changesMade;
            return true; // Only because Func<void> isn't legal, and this needs feeding to ditOrganisation.
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            App.WindowClosed();
        }

        private void CustomWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // CustomWindow.Window_Loaded() already handles this, but this window needs a little more attention.
            // As this window is not resizable currently, we also want to live a little bit of wiggle room so that
            // dragging the window around while keeping the buttons visible isn't too annoying.
            double screenHeight = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Height;
            double screenWidth = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Width;

            if (MaxHeight > screenHeight - 120)
                MaxHeight = screenHeight - 120;

            if (Top + ActualHeight > screenHeight)
                // No idea why + 6, maybe it's the difference between the standard WPF title bar and mine.
                Top = screenHeight - (ActualHeight + 6);
            if (Top < 0)
                Top = 0;
        }
    }
}
