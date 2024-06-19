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
    public partial class NetworkSettings : Window
    {
        public NetworkSettings()
        {
            InitializeComponent();

            txtIPAddress.Text = Properties.Settings.Default.serverAddress;
            txtPortInbound.Text = Properties.Settings.Default.portInbound.ToString();
            txtPortOutbound.Text = Properties.Settings.Default.portOutbound.ToString();
        }

        private void btnConfirm_Click(object sender, RoutedEventArgs e)
        {
            int inbound;
            int outbound;
            if (!System.Net.IPAddress.TryParse(txtIPAddress.Text, out _))
            {
                MessageBox.Show("IP address invalid.");
                return;
            }
            if (!int.TryParse(txtPortInbound.Text, out inbound) || inbound < 1025 || inbound > 65535)
            {
                MessageBox.Show("Inbound port invalid.");
                return;
            }
            if (!int.TryParse(txtPortOutbound.Text, out outbound) || outbound < 1025 || outbound > 65535)
            {
                MessageBox.Show("Outbound port invalid.");
                return;
            }

            Properties.Settings.Default.serverAddress = txtIPAddress.Text;
            Properties.Settings.Default.portInbound = inbound;
            Properties.Settings.Default.portOutbound = outbound;

            Properties.Settings.Default.Save();

            App.sd.SetIP(txtIPAddress.Text);

            Close();
        }

        // Field default resets. The way of getting the default values doesn't seem ideal, but it works.
        private void btnIPDefault_Click(object sender, RoutedEventArgs e)
        { txtIPAddress.Text = (string)Properties.Settings.Default.Properties["serverAddress"].DefaultValue; }
        private void btnInboundDefault_Click(object sender, RoutedEventArgs e)
        { txtPortInbound.Text = (string)Properties.Settings.Default.Properties["portInbound"].DefaultValue; }
        private void btnOutboundDefault_Click(object sender, RoutedEventArgs e)
        { txtPortOutbound.Text = (string)Properties.Settings.Default.Properties["portOutbound"].DefaultValue; }
    }
}
