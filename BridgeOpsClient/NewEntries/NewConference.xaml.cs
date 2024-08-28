using SendReceiveClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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

        public NewConference(PageConferenceView.ResourceInfo? resource, DateTime start)
        {
            InitializeComponent();

            tmpBuffer.SetMaxValues(99, 59);
            dtpStart.SetDateTime(start);
            dtpEnd.SetDateTime(start.AddHours(1));

            // Populate available types
            bool successful;
            typeList = App.SelectColumnPrimary("ConferenceType", Glo.Tab.CONFERENCE_TYPE_NAME, out successful);
            if (!successful)
                Close();
            if (typeList == null || typeList.Length == 0)
            {
                MessageBox.Show("Could not pull conference type list from server.");
                Close();
            }
            cmbType.ItemsSource = typeList;
            cmbType.SelectedIndex = 0;

            // Populate available resources and select whichever one the user clicked on in the schedule view.
            cmbResource.ItemsSource = PageConferenceView.resourceRowNames;
            if (resource == null)
                MessageBox.Show("Could not determine resource from selected row, please set manually.");
            else
                cmbResource.SelectedIndex = resource.SelectedRowTotal;

        }

        private void Window_Closed(object sender, EventArgs e)
        {
            App.WindowClosed();
        }
    }
}
