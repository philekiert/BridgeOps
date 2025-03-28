﻿using SendReceiveClasses;
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
    public partial class LogIn : CustomWindow
    {
        MainWindow mainWindow;

        bool networkSettings = false; // Alternates button functionality between login and network settings.

        public LogIn(MainWindow mainWindow)
        {
            InitializeComponent();

            this.mainWindow = mainWindow;

            lblVersion.Content = Glo.VersionNumber;

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
            if (e.Key == Key.Escape)
                Close();

            ToggleNetworkSettings(sender, e);
        }

        private void CustomWindow_KeyUp(object sender, KeyEventArgs e)
        {
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
                settings.Owner = this;
                settings.ShowDialog();
                ToggleNetworkSettings(sender, e);
                return;
            }

            string result = App.LogIn(txtUsername.Text, pwdPassword.Password);
            // Function automatically stores the session ID in App if logged in successfully.
            if (result == Glo.CLIENT_LOGIN_ACCEPT)
            {
                // No need to warn if this fails, it will simply fail and be reset/rebuilt on the user's next logout.
                App.PullUserSettings();

                if (App.PullColumnRecord(this))
                {
                    // If Main window hasn't opened yet, the buttons will be null. If they aren't and MainWindow is
                    // still starting up, it will toggle the buttons correctly itself.
                    if (mainWindow.menuUserLogIn != null)
                        mainWindow.ToggleLogInOut(true);

                    if (!App.PullResourceInformation(this))
                    {
                        App.DisplayError("Log in was successful, but could not pull resource information. Logging out. " +
                                         "Please contact the software administrator.", this);
                        App.LogOut(this);
                    }
                    else
                    {
                        if (MainWindow.pageConferenceViews.Count > 0)
                            foreach (PageConferenceView pcv in MainWindow.pageConferenceViews)
                                pcv.SearchTimeframe();
                        Close();
                    }
                }
                else
                {
                    App.DisplayError("Log in was successful, but could not pull column record. Logging out. Please " +
                                     "contact the software administrator.", this);
                    App.LogOut(this);
                }

                if (mainWindow.btnDocsAdmin != null) // This condition checks that the window has actually been displayed.
                    mainWindow.ApplyPermissions();

                if (MainWindow.pageDatabase != null)
                    MainWindow.pageDatabase.ApplyPermissions();
            }
            else
            {
                if (result == Glo.CLIENT_LOGIN_REJECT_USER_INVALID)
                    App.DisplayError("Username or password invalid.", this);
                else if (result == Glo.CLIENT_LOGIN_REJECT_USER_DUPLICATE)
                    App.DisplayError("User already logged in. If you believe this to be incorrect, please contact " +
                                    "the software administrator immediately.", this);
                else if (result == Glo.CLIENT_LOGIN_REJECT_IP_DUPLICATE)
                    App.DisplayError("IP address already associated with active session. Please try again in a " +
                                    "minute. If the problem does not resolve itself, contact the software " +
                                    "administrator.", this);
                else if (result == Glo.CLIENT_LOGIN_REJECT_USER_DISABLED)
                    App.DisplayError("Account disabled, please speak to your administrator.", this);
                else
                    App.DisplayError("Could not connect to Agent.", this);

                ToggleNetworkSettings(sender, e);
            }

            if (mainWindow.IsLoaded)
            {
                mainWindow.ApplyPermissions();
                mainWindow.mixedPaneRedrawOverride = true;
                mainWindow.ApplyViewState();
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            App.WindowClosed();
        }
    }
}
