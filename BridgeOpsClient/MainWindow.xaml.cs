using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using SendReceiveClasses;

namespace BridgeOpsClient
{
    public partial class MainWindow : Window
    {
        public const int CONF_PANE_MIN_WIDTH = 500;
        public const int DATA_PANE_MIN_WIDTH = 818;
        private void SetMinWidth(int option)
        {
            if (option == 0)
                MinWidth = CONF_PANE_MIN_WIDTH;
            else if (option == 1)
                MinWidth = CONF_PANE_MIN_WIDTH + 16 + DATA_PANE_MIN_WIDTH;
            else if (option == 2)
                MinWidth = DATA_PANE_MIN_WIDTH + 16;
        }

        public static List<PageConferenceView> pageConferenceViews = new();
        public static PageDatabase? pageDatabase;

        public MainWindow()
        {
            // Request login credentials on startup.
            LogIn logIn = new LogIn(this);
            logIn.ShowDialog();
            if (!App.IsLoggedIn)
            {
                Close();
                App.WindowClosed(); // InitializeComponent() hasn't been called yet, so Window_Closed() won't fire.
            }

            InitializeComponent();

            frameConf.Content = new PageConferenceView();
            pageDatabase = new PageDatabase(this);
            frameData.Content = pageDatabase;

            MinWidth = CONF_PANE_MIN_WIDTH + 16 + DATA_PANE_MIN_WIDTH;

            mixedPaneRedrawOverride = true;
            ApplyViewState();
            GreyOutPermissions();

            App.mainWindow = this;
        }

        public void ApplyViewState()
        {
            // Set default view here:
            if (viewState == 0)
                btnConfPane_Click(new bool(), new RoutedEventArgs()); // Conference View
            else if (viewState == 1)
                btnMixedPane_Click(new bool(), new RoutedEventArgs()); // Mixed View
            else
                btnDataPane_Click(new bool(), new RoutedEventArgs()); // Data View
        }

        public void ClearSqlDataGrids()
        {
            if (pageDatabase != null)
                pageDatabase.ClearSqlDataGrids();
        }

        public void GreyOutPermissions()
        {
            menuDatabaseNewOrganisation.IsEnabled = App.sd.createPermissions[Glo.PERMISSION_RECORDS];
            menuDatabaseNewAsset.IsEnabled = App.sd.createPermissions[Glo.PERMISSION_RECORDS];
            menuDatabaseNewContact.IsEnabled = App.sd.createPermissions[Glo.PERMISSION_RECORDS];
            menuDatabaseNewConferenceType.IsEnabled = App.sd.createPermissions[Glo.PERMISSION_CONFERENCE_TYPES];
            menuDatabaseNewResource.IsEnabled = App.sd.createPermissions[Glo.PERMISSION_RESOURCES];
        }

        private void menuDatabaseNewOrganisation_Click(object sender, RoutedEventArgs e)
        {
            string[]? organisationList = App.SelectColumnPrimary("Organisation", Glo.Tab.ORGANISATION_REF);
            if (organisationList == null)
            {
                MessageBox.Show("Could not pull organisation list from server.");
                return;
            }

            NewOrganisation newOrganisation = new();
            newOrganisation.cmbOrgParentID.ItemsSource = organisationList;
            newOrganisation.Show();
        }

        private void menuDatabaseNewAsset_Click(object sender, RoutedEventArgs e)
        {
            string[]? organisationList = App.SelectColumnPrimary("Organisation", Glo.Tab.ORGANISATION_REF);
            if (organisationList == null)
            {
                MessageBox.Show("Could not pull organisation list from server.");
                return;
            }

            NewAsset newAsset = new();
            newAsset.cmbOrgRef.ItemsSource = organisationList;
            newAsset.Show();
        }

        private void menuDatabaseNewContact_Click(object sender, RoutedEventArgs e)
        {
            NewContact newContact = new();

            newContact.Show();
        }

        private void menuDatabaseNewResource_Click(object sender, RoutedEventArgs e)
        {
            NewResource newResource = new();

            if (ColumnRecord.GetColumn(ColumnRecord.resource, Glo.Tab.RESOURCE_ID).friendlyName != "")
                newResource.lblResourceName.Content = ColumnRecord.GetColumn(ColumnRecord.resource,
                                                                             Glo.Tab.RESOURCE_ID).friendlyName;
            if (ColumnRecord.GetColumn(ColumnRecord.resource, Glo.Tab.RESOURCE_FROM).friendlyName != "")
                newResource.lblAvailableFrom.Content = ColumnRecord.GetColumn(ColumnRecord.resource,
                                                                              Glo.Tab.RESOURCE_FROM).friendlyName;
            if (ColumnRecord.GetColumn(ColumnRecord.resource, Glo.Tab.RESOURCE_TO).friendlyName != "")
                newResource.lblAvailableTo.Content = ColumnRecord.GetColumn(ColumnRecord.resource,
                                                                            Glo.Tab.RESOURCE_TO).friendlyName;

            newResource.txtResourceName.MaxLength = Glo.Fun.LongToInt(ColumnRecord.GetColumn(ColumnRecord.resource,
                                                                      Glo.Tab.RESOURCE_ID).restriction);

            newResource.Show();
        }

        private void menuDatabaseNewConferenceType_Click(object sender, RoutedEventArgs e)
        {
            NewConferenceType newResource = new();

            if (ColumnRecord.GetColumn(ColumnRecord.conferenceType, Glo.Tab.CONFERENCE_TYPE_NAME).friendlyName != "")
                newResource.lblTypeName.Content = ColumnRecord.GetColumn(ColumnRecord.conferenceType,
                                                                         Glo.Tab.CONFERENCE_TYPE_NAME).friendlyName;

            newResource.txtTypeName.MaxLength = Glo.Fun.LongToInt(ColumnRecord.GetColumn(ColumnRecord.conferenceType,
                                                                  Glo.Tab.CONFERENCE_TYPE_NAME).restriction);

            newResource.Show();
        }

        private void menuUserLogIn_Click(object sender, RoutedEventArgs e)
        {
            if (App.IsLoggedIn)
                MessageBox.Show("Already logged in as " + App.sd.username + ".");
            else
            {
                LogIn logIn = new LogIn(this);
                logIn.Show();
            }
        }

        private void menuUserLogOut_Click(object sender, RoutedEventArgs e)
        {
            if (App.IsLoggedIn)
                App.LogOut();
            else
                MessageBox.Show("Not logged in.");
        }

        private void menuChangePassword_Click(object sender, RoutedEventArgs e)
        {
            PasswordChange pc = new(App.sd.loginID, false);
            pc.ShowDialog();
        }

        private void menuResetViewSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxButton messageBoxButton = MessageBoxButton.YesNo;
            MessageBoxResult result = MessageBox.Show("Are you sure? This will:\n\n• Reset all data table view " +
                                                      "settings, such as hidden columns, column orders and columns " +
                                                      "widths.\n• Reset pane layout.\n\nYou will need to log back in.",
                                                      "Reset View Settings",
                                                      messageBoxButton);
            if (result == MessageBoxResult.Yes)
            {
                // Reset settings before LogOut(), and they will be stored on the database as part of that method.
                App.us = new UserSettings();
                App.LogOut();
            }
        }

        private void menuSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settingsWindow = new();
            settingsWindow.ShowDialog();
        }

        private void menuExit_Click(object sender, RoutedEventArgs e)
        {
            // No need for cleanup as this is handled in App.ApplicationExit().
            Close();
        }

        public void ToggleLogInOut(bool loggedIn)
        {
            // This can fail if only the login window is visible, no big deal and no need to report.
            if (menuUserLogIn != null)
                menuUserLogIn.IsEnabled = !loggedIn;
            if (menuUserLogOut != null)
                menuUserLogOut.IsEnabled = loggedIn;
        }

        /* This is surely needlessly verbose, will optimise later if time allows. Removing and re-adding the columns
         * was the only way I could get it all to work. */
        public static double oldConfWidth = 1;
        public static double oldDataWidth = 1;
        public static int viewState = 2; // 0: Conference, 1: Mixed, 2: Data

        private void btnConfPane_Click(object sender, RoutedEventArgs e)
        {
            viewState = 0;

            if (frameData.Visibility == Visibility.Visible)
            {
                SetMinWidth(0);

                frameConf.Visibility = Visibility.Visible;
                spltConfData.Visibility = Visibility.Collapsed;
                frameData.Visibility = Visibility.Collapsed;

                grdConfData.ColumnDefinitions.Clear();
                ColumnDefinition col = new ColumnDefinition();
                col.MinWidth = CONF_PANE_MIN_WIDTH;
                grdConfData.ColumnDefinitions.Add(col);

                if (frameConf.Content is PageConferenceView pcv)
                {
                    pcv.scrollBar.Margin = new Thickness(0);
                }
            }
        }

        public bool mixedPaneRedrawOverride = false; // Needed for first run.
        private void btnMixedPane_Click(object sender, RoutedEventArgs e)
        {
            viewState = 1;

            if (frameConf.Visibility == Visibility.Collapsed || frameData.Visibility == Visibility.Collapsed ||
                mixedPaneRedrawOverride)
            {
                mixedPaneRedrawOverride = false;

                SetMinWidth(1);
                frameConf.Visibility = Visibility.Visible;
                spltConfData.Visibility = Visibility.Visible;
                frameData.Visibility = Visibility.Visible;

                grdConfData.ColumnDefinitions.Clear();
                ColumnDefinition col = new ColumnDefinition();
                col.MinWidth = CONF_PANE_MIN_WIDTH;
                col.Width = new GridLength(oldConfWidth, GridUnitType.Star);
                grdConfData.ColumnDefinitions.Add(col);
                col = new ColumnDefinition();
                col.MinWidth = DATA_PANE_MIN_WIDTH;
                col.Width = new GridLength(oldDataWidth, GridUnitType.Star);
                grdConfData.ColumnDefinitions.Add(col);

                frameData.SetValue(Grid.RowProperty, 1);

                foreach (PageConferenceView confView in pageConferenceViews)
                    confView.scrollBar.Margin = new Thickness(0, 0, 7, 0);
            }
        }

        private void btnDataPane_Click(object sender, RoutedEventArgs e)
        {
            viewState = 2;

            if (frameConf.Visibility == Visibility.Visible)
            {
                SetMinWidth(2);

                frameConf.Visibility = Visibility.Collapsed;
                spltConfData.Visibility = Visibility.Collapsed;
                frameData.Visibility = Visibility.Visible;

                grdConfData.ColumnDefinitions.Clear();
                ColumnDefinition col = new ColumnDefinition();
                col.MinWidth = DATA_PANE_MIN_WIDTH;
                grdConfData.ColumnDefinitions.Add(col);

                frameConf.SetValue(Grid.RowProperty, 0);

                if (frameConf.Content is PageConferenceView pcv)
                    pcv.scrollBar.Margin = new Thickness(0);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            App.WindowClosed();
        }

        // Remember widths. This is stored in the user view settings.
        private void frameConf_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (frameConf.Visibility == Visibility.Visible && frameData.Visibility == Visibility.Visible)
            {
                oldConfWidth = grdConfData.ColumnDefinitions[0].Width.Value;
                oldDataWidth = grdConfData.ColumnDefinitions[1].Width.Value;
            }
        }

        private void menuSelect_Click(object sender, RoutedEventArgs e)
        {
            SelectBuilder selectBuilder = new();
            selectBuilder.Show();
        }
    }
}
