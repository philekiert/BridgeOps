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
        public NewAsset()
        {
            InitializeComponent();
        }
        public NewAsset(string id)
        {
            InitializeComponent();

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

            ditAsset.Populate(data.GetRange(3, data.Count - 3));
        }
#pragma warning restore CS8602

        public string[]? organisationList;
        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (ditAsset.ScoopValues())
            {
                if (txtAssetID.Text == "")
                    MessageBox.Show("You must input a value for Asset ID");

                Asset ao = new();

                ao.sessionID = App.sd.sessionID;

                ao.assetID = txtAssetID.Text;
                ao.organisationID = cmbOrgID.Text.Length == 0 ? null : cmbOrgID.Text;
                ao.notes = txtNotes.Text.Length == 0 ? null : txtNotes.Text;

                ditAsset.ExtractValues(out ao.additionalCols, out ao.additionalVals);

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
    }
}
