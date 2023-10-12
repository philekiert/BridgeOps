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
        public NewOrganisation()
        {
            InitializeComponent();
        }
        public NewOrganisation(string id)
        {
            InitializeComponent();

            edit = true;
            btnAdd.Visibility = Visibility.Hidden;
            btnEdit.Visibility = Visibility.Visible;
            btnDelete.Visibility = Visibility.Visible;
            this.id = id;

            txtOrgID.Text = id;
            txtOrgID.IsReadOnly = true;
        }

        public void Populate(List<object?> data)
        {
#pragma warning disable CS8602
            // This method will not be called if the data has a different Count than expected.
            if (data[1] != null)
                cmbOrgParentID.Text = data[1].ToString();
            if (data[2] != null)
                txtDialNo.Text = data[2].ToString();
            if (data[3] != null)
                txtNotes.Text = data[3].ToString();

            // Store the original values to check if any changes have been made for the data. The same takes place
            // in the data input table.
            originalParent = cmbOrgParentID.Text;
            originalDialNo = txtDialNo.Text;
            originalNotes = txtNotes.Text;

            ditOrganisation.Populate(data.GetRange(4, data.Count - 4));
            if (edit)
                ditOrganisation.RememberStartingValues();
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

                Organisation no = new Organisation();

                no.sessionID = App.sd.sessionID;

                no.organisationID = txtOrgID.Text;
                no.parentOrgID = cmbOrgParentID.Text.Length == 0 ? null : cmbOrgParentID.Text;
                no.dialNo = txtDialNo.Text.Length == 0 ? null : txtDialNo.Text;
                no.notes = txtNotes.Text.Length == 0 ? null : txtNotes.Text;

                ditOrganisation.ExtractValues(out no.additionalCols, out no.additionalVals);

                // Obtain types and determine whether or not quotes will be needed.
                no.additionalNeedsQuotes = new();
                foreach (string c in no.additionalCols)
                    no.additionalNeedsQuotes.Add(SqlAssist.NeedsQuotes(ColumnRecord.organisation[c].type));

                if (App.SendInsert(Glo.CLIENT_NEW_ORGANISATION, no))
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
                if (cmbOrgParentID.Text != originalParent)
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

                if (App.SendUpdate(Glo.CLIENT_UPDATE_ORGANISATION, org))
                    Close();
                else
                    MessageBox.Show("Could not edit organisation.");
            }
            else
            {
                string message = "One or more values caused an unknown error to occur.";
                if (ditOrganisation.disallowed.Count > 0)
                    message = ditOrganisation.disallowed[0];
                MessageBox.Show(message);
            }
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (App.SendDelete("Organisation", Glo.Tab.ORGANISATION_ID, id, true))
                Close();
            else
                MessageBox.Show("Could not delete organisation.");
        }
    }
}
