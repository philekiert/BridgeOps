﻿using SendReceiveClasses;
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

namespace BridgeOpsClient
{
    public partial class NewContact : Window
    {
        bool edit = false;
        string id = "";
        string? originalNotes = "";
        public NewContact()
        {
            InitializeComponent();
        }
        public NewContact(string id)
        {
            this.id = id;

            InitializeComponent();

            edit = true;
            btnAdd.Visibility = Visibility.Hidden;
            btnEdit.Visibility = Visibility.Visible;
            btnDelete.Visibility = Visibility.Visible;
        }

#pragma warning disable CS8602
        public void Populate(List<object?> data)
        {
            // This method will not be called if the data has a different Count than expected.
            if (data[1] != null)
                txtNotes.Text = data[1].ToString();

            // Store the original values to check if any changes have been made for the data. The same takes place
            // in the data input table.
            originalNotes = txtNotes.Text;

            ditContact.Populate(data.GetRange(2, data.Count - 2));
            if (edit)
                ditContact.RememberStartingValues();
        }
#pragma warning restore CS8602

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (ditContact.ScoopValues())
            {
                Contact nc = new Contact();

                nc.sessionID = App.sd.sessionID;
                if (txtNotes.Text.Length == 0)
                    nc.notes = null;
                else
                    nc.notes = txtNotes.Text;

                ditContact.ExtractValues(out nc.additionalCols, out nc.additionalVals);

                if (App.SendInsert(Glo.CLIENT_NEW_CONTACT, nc))
                    Close();
                else
                {
                    // There shouldn't be any errors with insert on this one, as everything is either text or null.
                    MessageBox.Show("Could not create contact.");
                }
            }
            else
            {
                string message = "One or more values caused an unknown error to occur.";
                if (ditContact.disallowed.Count > 0)
                    message = ditContact.disallowed[0];
                MessageBox.Show(message);
            }
        }

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            int idInt;
            if (!int.TryParse(id, out idInt))
            {
                // This should never trigger as the ID cannot be adjusted, but just to be diligent...
                MessageBox.Show("Customer ID is invalid, cannot edit record.");
                return;
            }
            if (ditContact.ScoopValues())
            {
                Contact contact = new Contact();
                contact.sessionID = App.sd.sessionID;
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
                contact.additionalNeedsQuotes = new();
                foreach (string c in cols)
                    contact.additionalNeedsQuotes.Add(SqlAssist.NeedsQuotes(ColumnRecord.contact[c].type));

                contact.additionalCols = cols;
                contact.additionalVals = vals;

                // Add the known fields if changed.
                if (txtNotes.Text != originalNotes)
                {
                    contact.notes = txtNotes.Text;
                    contact.notesChanged = true;
                }

                if (App.SendUpdate(Glo.CLIENT_UPDATE_CONTACT, contact))
                    Close();
                else
                    MessageBox.Show("Could not edit contact.");
            }
            else
            {
                string message = "One or more values caused an unknown error to occur.";
                if (ditContact.disallowed.Count > 0)
                    message = ditContact.disallowed[0];
                MessageBox.Show(message);
            }
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (App.SendDelete("Contact", Glo.Tab.CONTACT_ID, id, false))
                Close();
            else
                MessageBox.Show("Could not delete contact.");
        }
    }
}
