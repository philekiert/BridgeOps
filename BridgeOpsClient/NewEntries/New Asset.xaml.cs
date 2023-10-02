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
        public NewAsset()
        {
            InitializeComponent();
        }

        public string[]? organisationList;
        private void Button_Click(object sender, RoutedEventArgs e)
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
