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

namespace BridgeOpsClient.DialogWindows
{
    public partial class AdjustConferenceConnections : CustomWindow
    {
        List<string> ids;

        public AdjustConferenceConnections(List<string> ids)
        {
            this.ids = ids;

            if (ids.Count == 0)
            {
                App.DisplayError("No conferences selected.");
                Close();
            }

            InitializeComponent();
        }

        private bool Adjust()
        {
            SendReceiveClasses.ConferenceAdjustment req = new();

            req.ids = ids.Select(int.Parse).ToList();
            req.intent = SendReceiveClasses.ConferenceAdjustment.Intent.Connections;

            if (App.SendConferenceAdjustment(req))
            {
                Close();
                return true;
            }
            else
                return false;
        }

        private void btnAdjust_Click(object sender, RoutedEventArgs e)
        {
            Adjust();
        }
    }
}
