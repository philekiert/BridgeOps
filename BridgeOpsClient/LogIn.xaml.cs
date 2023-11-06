using SendReceiveClasses;
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
    public partial class LogIn : Window
    {
        MainWindow mainWindow;

        public LogIn(MainWindow mainWindow)
        {
            InitializeComponent();

            this.mainWindow = mainWindow;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Auto login for dev purposes.
            txtUsername.Text = "admin";
            txtPassword.Text = "admin";
            btnLogIn_Click(btnLogIn, new());
        }

        private void btnLogIn_Click(object sender, RoutedEventArgs e)
        {
            string result = App.LogIn(txtUsername.Text, txtPassword.Text);
            // Function automatically stores the session ID in App if logged in successfully.
            if (result == Glo.CLIENT_LOGIN_ACCEPT)
            {
                if (App.PullColumnRecord())
                {
                    Close();
                    // If Main window hasn't opened yet, the buttons will be null. If they aren't and MainWindow is
                    // still starting up, it will toggle the buttons correctly itself.
                    if (mainWindow.menuUserLogIn != null)
                        mainWindow.ToggleLogInOut(true);
                }
                else
                {
                    MessageBox.Show("Log in was successful, but could not pull column record. Logging out. Please " +
                                    "contact the software adminitrator.");
                    App.LogOut();
                }
            }
            else
            {
                if (result == Glo.CLIENT_LOGIN_REJECT_USER_INVALID)
                    MessageBox.Show("Username or password invalid.");
                else if (result == Glo.CLIENT_LOGIN_REJECT_USER_DUPLICATE)
                    MessageBox.Show("User already logged in. If you believe this to be incorrect, please contact " +
                                    "the software administrator immediately.");
                else if (result == Glo.CLIENT_LOGIN_REJECT_IP_DUPLICATE)
                    MessageBox.Show("IP address already associated with active session. Please try again in a " +
                                    "minute. If the problem does not resolve itself, contact the software " +
                                    "administrator.");
                else
                    MessageBox.Show("Could not connect to Agent.");
            }
        }
    }
}
