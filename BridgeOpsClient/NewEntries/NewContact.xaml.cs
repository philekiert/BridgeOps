using SendReceiveClasses;
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
    public partial class NewContact : Window
    {
        bool edit = false;
        public string id = "";
        public bool requireIdBack = false; // Set by caller on load.
        public bool isDialog = false;
        string? originalNotes = "";

        public bool changeMade = false;

        private void ApplyPermissions()
        {
            if (!App.sd.createPermissions[Glo.PERMISSION_RECORDS])
                btnAdd.IsEnabled = false;
            if (!App.sd.editPermissions[Glo.PERMISSION_RECORDS])
                btnEdit.IsEnabled = false;
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
            ditContact.Initialise(ColumnRecord.orderedContact, "Contact");

            // Implement max length. Max lengths in the DataInputTable are set automatically.
            txtNotes.MaxLength = ColumnRecord.asset["Notes"].restriction;

            // Implemement friendly name.
            if (ColumnRecord.contact["Notes"].friendlyName != "")
                lblNotes.Content = ColumnRecord.contact["Notes"].friendlyName;
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
                    nc.additionalNeedsQuotes.Add(SqlAssist.NeedsQuotes(ColumnRecord.contact[c].type));

                if (App.SendInsert(Glo.CLIENT_NEW_CONTACT, nc, out id))
                {
                    changeMade = true;
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
                MessageBox.Show(message);
            }
        }

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            int idInt;
            if (!int.TryParse(id, out idInt))
            {
                // This should never trigger as the ID cannot be adjusted, but just to be diligent...
                MessageBox.Show("Contact ID is invalid, cannot edit record.");
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
                {
                    if (MainWindow.pageDatabase != null)
                        MainWindow.pageDatabase.RepeatSearches(2);
                    changeMade = true;
                    Close();
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

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (App.SendDelete("Contact", Glo.Tab.CONTACT_ID, id, false))
            {
                changeMade = true;
                if (MainWindow.pageDatabase != null)
                    MainWindow.pageDatabase.RepeatSearches(2);
                Close();
            }
        }

        // Check for changes whenever the user interacts with any control.
        private void ValueChanged(object sender, EventArgs e) { AnyInteraction(); }
        public bool AnyInteraction()
        {
            btnEdit.IsEnabled = originalNotes != txtNotes.Text ||
                                ditContact.CheckForValueChanges();
            return true; // Only because Func<void> isn't legal, and this needs feeding to ditOrganisation.
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            App.WindowClosed();
        }
    }
}
