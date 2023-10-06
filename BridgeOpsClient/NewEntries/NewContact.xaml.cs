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

namespace BridgeOpsClient
{
    public partial class NewContact : Window
    {
        bool edit = false;
        string id = "";
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

            ditContact.Populate(data.GetRange(2, data.Count - 2));
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
    }
}
