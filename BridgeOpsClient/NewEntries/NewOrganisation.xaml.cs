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

            txtOrgID.Text = id;
            txtOrgID.IsReadOnly = true;
        }

#pragma warning disable CS8602
        public void Populate(List<object?> data)
        {
            // This method will not be called if the data has a different Count than expected.
            if (data[1] != null)
                cmbOrgParentID.Text = data[1].ToString();
            if (data[2] != null)
                txtDialNo.Text = data[2].ToString();
            if (data[3] != null)
                txtNotes.Text = data[3].ToString();

            ditOrganisation.Populate(data.GetRange(4, data.Count - 4));
        }
#pragma warning restore CS8602

        public string[]? organisationList;
        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (ditOrganisation.ScoopValues())
            {
                if (txtOrgID.Text == "")
                    MessageBox.Show("You must input a value for Organisation ID");

                Organisation no = new Organisation();

                no.sessionID = App.sd.sessionID;

                no.organisationID = txtOrgID.Text;
                no.parentOrgID = cmbOrgParentID.Text.Length == 0 ? null : cmbOrgParentID.Text;
                no.dialNo = txtDialNo.Text.Length == 0 ? null : txtDialNo.Text;
                no.notes = txtNotes.Text.Length == 0 ? null : txtNotes.Text;

                ditOrganisation.ExtractValues(out no.additionalCols, out no.additionalVals);

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

        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
