using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace BridgeOpsClient.DialogWindows
{
    public partial class DialogChangeReason : Window
    {
        public DialogChangeReason(string table)
        {
            InitializeComponent();

            if (table == "Organisation")
                txtReason.MaxLength = ColumnRecord.organisationChange[Glo.Tab.CHANGE_REASON].restriction;
            else if (table == "Asset")
                txtReason.MaxLength = ColumnRecord.assetChange[Glo.Tab.CHANGE_REASON].restriction;
            else
            {
                MessageBox.Show("Relevant table not known.");
                DialogResult = false;
                Close();
            }
        }

        private void btnSubmit_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
