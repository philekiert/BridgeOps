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
    public partial class NewConference : Window
    {
        string[]? typeList = null;

        public NewConference(int resource, DateTime start)
        {
            InitializeComponent();

            cmbResource.IsEditable = true;
            cmbResource.Text = (resource + 1).ToString();
            dtpStart.SetDateTime(start);
            dtpEnd.SetDateTime(start.AddHours(1));

            typeList = App.SelectColumnPrimary("ConferenceType", Glo.Tab.CONFERENCE_TYPE_NAME);
            if (typeList == null)
            {
                MessageBox.Show("Could not pull conference type list from server.");
                Close();
            }
            cmbResource.ItemsSource = typeList;
        }
    }
}
