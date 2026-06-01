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
            txtPort.Text = App.sd.port.ToString();
            chkUseSSL.IsChecked = SendReceiveClasses.SendReceive.useSSL;

            txtIPAddress.Focus();
        }

        private void btnConfirm_Click(object sender, RoutedEventArgs e)
        {
            int initialPort = App.sd.port;

            int port;
            if (!System.Net.IPAddress.TryParse(txtIPAddress.Text, out _))
            {
                App.DisplayError("IP address invalid.", this);
                return;
            }
            if (!int.TryParse(txtPort.Text, out port) || port < 1025 || port > 65535)
            {
                App.DisplayError("Port invalid.", this);
                return;
            }

            bool useSSL = chkUseSSL.IsChecked == true;

            System.IO.File.WriteAllText(App.networkConfigFile, txtIPAddress.Text + ";" +
                                                               port.ToString() + ";" +
                                                               useSSL.ToString());
            App.sd.SetServerIP(txtIPAddress.Text);
            App.sd.port = port;
            SendReceiveClasses.SendReceive.useSSL = chkUseSSL.IsChecked == true;

            if (initialPort != port)
                App.DisplayError("You must restart the application for the change to the port to take effect.",
                                 "Port Changed", this);

            Close();
        }

        // Field default resets. The way of getting the default values doesn't seem ideal, but it works.
        private void btnIPDefault_Click(object sender, RoutedEventArgs e)
        { txtIPAddress.Text = "127.0.0.1"; }
        private void btnPortDefault_Click(object sender, RoutedEventArgs e)
        { txtPort.Text = Glo.PORT_DEFAULT.ToString(); }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                btnConfirm_Click(sender, e);
        }
    }
}
