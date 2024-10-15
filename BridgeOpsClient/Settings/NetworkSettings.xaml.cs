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
    public partial class NetworkSettings : CustomWindow
    {
        public NetworkSettings()
        {
            InitializeComponent();

            txtIPAddress.Text = App.sd.ServerIP.ToString();
            txtPortInbound.Text = App.sd.portInbound.ToString();
            txtPortOutbound.Text = App.sd.portOutbound.ToString();

            txtIPAddress.Focus();
        }

        private void btnConfirm_Click(object sender, RoutedEventArgs e)
        {
            int outbound;
            int inbound;
            if (!System.Net.IPAddress.TryParse(txtIPAddress.Text, out _))
            {
                App.DisplayError("IP address invalid.");
                return;
            }
            if (!int.TryParse(txtPortOutbound.Text, out outbound) || outbound < 1025 || outbound > 65535)
            {
                App.DisplayError("Outbound port invalid.");
                return;
            }
            if (!int.TryParse(txtPortInbound.Text, out inbound) || inbound < 1025 || inbound > 65535)
            {
                App.DisplayError("Inbound port invalid.");
                return;
            }
            if (inbound == outbound)
            {
                App.DisplayError("Inbound and outbound ports cannot be the same.");
                return;
            }

            System.IO.File.WriteAllText(App.networkConfigFile, txtIPAddress.Text + ";" +
                                                               inbound.ToString() + ";" +
                                                               outbound.ToString());

            App.sd.SetServerIP(txtIPAddress.Text);
            App.sd.portOutbound = outbound;
            App.sd.portInbound = inbound;

            Close();
        }

        // Field default resets. The way of getting the default values doesn't seem ideal, but it works.
        private void btnIPDefault_Click(object sender, RoutedEventArgs e)
        { txtIPAddress.Text = "127.0.0.1"; }
        private void btnOutboundDefault_Click(object sender, RoutedEventArgs e)
        { txtPortOutbound.Text = Glo.PORT_INBOUND_DEFAULT.ToString(); }
        private void btnInboundDefault_Click(object sender, RoutedEventArgs e)
        { txtPortInbound.Text = Glo.PORT_OUTBOUND_DEFAULT.ToString(); }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                btnConfirm_Click(sender, e);
        }
    }
}
