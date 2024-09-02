using System;
using System.Collections.Generic;
using System.Linq;
using SendReceiveClasses;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace BridgeOpsClient.DialogWindows
{
    public partial class DialogChangeReason : Window
    {
        string table = "";

        // Reason for new change
        public DialogChangeReason(string table)
        {
            this.table = table;

            InitializeComponent();

            if (table == "Organisation")
            {
                txtReason.MaxLength = Glo.Fun.LongToInt(((ColumnRecord.Column)ColumnRecord
                    .organisationChange[Glo.Tab.CHANGE_REASON]!).restriction);
            }
            else if (table == "Asset")
            {
                txtReason.MaxLength = Glo.Fun.LongToInt(((ColumnRecord.Column)ColumnRecord
                    .assetChange[Glo.Tab.CHANGE_REASON]!).restriction);
            }
            else
            {
                App.DisplayError("Relevant table not known.");
                DialogResult = false;
                Close();
            }

            txtReason.Focus();
        }

        // Reason correction.
        bool correctExistingRecord = false;
        int changeID = -1;
        public DialogChangeReason(string table, int changeID, string reason) : this(table)
        {
            txtReason.Text = reason;
            correctExistingRecord = true;

            Title = "Change Reason Correction";

            this.changeID = changeID;
        }

        private void btnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (!correctExistingRecord)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                ChangeReasonUpdate update = new ChangeReasonUpdate(App.sd.sessionID, ColumnRecord.columnRecordID,
                                                                   table, changeID, txtReason.Text);
                if (App.SendUpdate(Glo.CLIENT_UPDATE_CHANGE_REASON, update))
                {
                    DialogResult = true;
                    Close();
                }
                else
                {
                    DialogResult = false;
                    App.DisplayError("The correction could not be made. It could be that the connection to the agent " +
                                    "has been lost. If this error persists, please contact your software " +
                                    "administrator.");
                }
            }
        }
    }
}
