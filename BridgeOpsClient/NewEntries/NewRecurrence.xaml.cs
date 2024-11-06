using System;
using System.Collections.Generic;
using System.Linq;
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
    public partial class NewRecurrence : CustomWindow
    {
        public string returnID = "";

        public NewRecurrence()
        {
            InitializeComponent();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            SendReceiveClasses.Recurrence recurrence = new()
            {
                sessionID = App.sd.sessionID,
                columnRecordID = ColumnRecord.columnRecordID,
                name = txtName.Text == "" ? null : txtName.Text,
                notes = txtNotes.Text == "" ? null : txtNotes.Text,
                requireIdBack = true
            };

            if (App.SendInsert(Glo.CLIENT_NEW_RECURRENCE, recurrence, out returnID))
            {
                DialogResult = true;
                Close();
                if (MainWindow.pageDatabase != null)
                    MainWindow.pageDatabase.RepeatSearches(8);
            }
        }
    }
}
