using System;
using System.Collections.Generic;
using System.IO;
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
    public partial class NewColumn : Window
    {
        // Add
        public NewColumn()
        {
            InitializeComponent();
        }
        // Edit
        public NewColumn(string table, string column, string friendly, string type, string limit, string[] allowed)
        {

        }

        private void cmbType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string newSel = (string)((ComboBoxItem)cmbType.Items[cmbType.SelectedIndex]).Content;
            if (newSel != "TEXT")
            {
                txtLimit.IsEnabled = false;
                txtAllowed.IsEnabled = false;
            }
            switch (newSel)
            {
                case "TEXT":
                    txtLimit.IsEnabled = true;
                    txtLimit.Text = "";
                    txtLimit.MaxLength = 5;
                    txtAllowed.IsEnabled = true;
                    break;
                case "TINYINT":
                    txtLimit.Text = "255";
                    break;
                case "SMALLINT":
                    txtLimit.Text = "32767";
                    break;
                case "INT":
                    txtLimit.Text = "2,147,483,647";
                    break;
                case "BIGINT":
                    txtLimit.Text = "9,223,372,036,854,775,807";
                    break;
                default: // DATE or BOOLEAN
                    txtLimit.Text = "";
                    break;
            }
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            List<string> allowed = new();
            if (txtAllowed.Text.Length > 0)
                txtAllowed.Text.Split("\r\n").ToList();

            SendReceiveClasses.TableModification mod = new(App.sd.sessionID,
                                                            cmbTable.Text, txtColumnName.Text, cmbType.Text, allowed);
            SendToServer(mod);

        }

        public bool changeMade = false;
        private void SendToServer(SendReceiveClasses.TableModification mod)
        {
            NetworkStream? stream = App.sr.NewClientNetworkStream(App.sd.ServerEP);
            try
            {
                if (stream != null)
                {
                    stream.WriteByte(Glo.CLIENT_TABLE_MODIFICATION);
                    App.sr.WriteAndFlush(stream, App.sr.Serialise(mod));
                    int response = stream.ReadByte();
                    if (response == Glo.CLIENT_REQUEST_SUCCESS)
                    {
                        changeMade = true;
                        Close();
                        return;
                    }
                    else if (response == Glo.CLIENT_SESSION_INVALID)
                    {
                        App.SessionInvalidated();
                        Close();
                        return;
                    }
                    else if (response == Glo.CLIENT_INSUFFICIENT_PERMISSIONS)
                    {
                        // Shouldn't ever arrive here.
                        MessageBox.Show("Only admins can make table modifications.");
                        return;
                    }
                }
            }
            catch
            {
                MessageBox.Show("Could not run table update.");
                return;
            }
            finally
            {
                if (stream != null) stream.Close();
            }
        }
    }
}
