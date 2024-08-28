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

        bool networkSettings = false;

        public LogIn(MainWindow mainWindow)
        {
            InitializeComponent();

            this.mainWindow = mainWindow;

            txtUsername.Focus();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Auto login for dev purposes.
            //txtUsername.Text = "admin";
            //pwdPassword.Password = "admin";
            //btnLogIn_Click(btnLogIn, new());
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                btnLogIn_Click(sender, new RoutedEventArgs());

            ToggleNetworkSettings(sender, e);
        }

        private void ToggleNetworkSettings(object sender, EventArgs e)
        {
            if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) &&
                (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) &&
                (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)))
            {
                networkSettings = true;
                btnLogIn.Content = "Network Settings";
            }
            else
            {
                networkSettings = false;
                btnLogIn.Content = "Log In";
            }
        }

        private void btnLogIn_Click(object sender, RoutedEventArgs e)
        {
            // Server IP Address
            if (networkSettings)
            {
                NetworkSettings settings = new NetworkSettings();
                settings.ShowDialog();
                ToggleNetworkSettings(sender, e);
                return;
            }

            string result = App.LogIn(txtUsername.Text, pwdPassword.Password);
            // Function automatically stores the session ID in App if logged in successfully.
            if (result == Glo.CLIENT_LOGIN_ACCEPT)
            {
                if (App.PullColumnRecord())
                {
                    // If Main window hasn't opened yet, the buttons will be null. If they aren't and MainWindow is
                    // still starting up, it will toggle the buttons correctly itself.
                    if (mainWindow.menuUserLogIn != null)
                        mainWindow.ToggleLogInOut(true);

                    if (!App.PullResourceInformation())
                    {
                        MessageBox.Show("Log in was successful, but could not pull resource information. Logging out. " +
                                        "Please contact the software administrator.");
                        App.LogOut();
                    }
                    else
                        Close();
                }
                else
                {
                    MessageBox.Show("Log in was successful, but could not pull column record. Logging out. Please " +
                                    "contact the software administrator.");
                    App.LogOut();
                }

                // No need to warn if this fails, it will simply fail and be reset/rebuilt on the user's next logout.
                App.PullUserSettings();

                if (MainWindow.pageDatabase != null)
                    MainWindow.pageDatabase.ReflectPermissions();
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
                else if (result == Glo.CLIENT_LOGIN_REJECT_USER_DISABLED)
                    MessageBox.Show("Account disabled, please speak to your administrator.");   
                else
                    MessageBox.Show("Could not connect to Agent.");

                ToggleNetworkSettings(sender, e);
            }

            if (mainWindow.IsLoaded)
            {
                mainWindow.GreyOutPermissions();
                mainWindow.mixedPaneRedrawOverride = true;
                mainWindow.ApplyViewState();
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            App.WindowClosed();
        }

        private void txtUsername_GotFocus(object sender, RoutedEventArgs e)
        {
            txtUsername.SelectAll();
        }

        private void pwdPassword_GotFocus(object sender, RoutedEventArgs e)
        {
            pwdPassword.SelectAll();
        }
    }
}
