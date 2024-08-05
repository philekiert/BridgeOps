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
    public partial class NewConferenceType : Window
    {
        public NewConferenceType()
        {
            InitializeComponent();

            txtTypeName.Focus();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ConferenceType nct = new();

            nct.sessionID = App.sd.sessionID;
            nct.columnRecordID = ColumnRecord.columnRecordID;

            if (txtTypeName.Text.Length == 0)
                nct.name = null;
            else
                nct.name = txtTypeName.Text;

            if (App.SendInsert(Glo.CLIENT_NEW_CONFERENCE_TYPE, nct))
                Close();
            else
            {
                // There shouldn't be any errors with insert on this one, as everything is either text or null.
                MessageBox.Show("Could not create conference type.");
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            App.WindowClosed();
        }
    }
}
