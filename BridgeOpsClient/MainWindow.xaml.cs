using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public partial class MainWindow : CustomWindow
    {
        public const int CONF_PANE_MIN_WIDTH = 500;
        public const int DATA_PANE_MIN_WIDTH = 818;
        private void SetMinWidth(int option)
        {
            if (option == 0)
                MinWidth = CONF_PANE_MIN_WIDTH;
            else if (option == 1)
                MinWidth = CONF_PANE_MIN_WIDTH + DATA_PANE_MIN_WIDTH;
            else if (option == 2)
                MinWidth = DATA_PANE_MIN_WIDTH;
        }

        public static List<PageConferenceView> pageConferenceViews = new();
        public static PageDatabase? pageDatabase;
        public static void RepeatSearches(int identity)
        {
            if (pageDatabase != null)
                pageDatabase.RepeatSearches(identity);
        }

        string? rptExporterLocation = null;

        public MainWindow()
        {
            App.mainWindow = this;

            // Request login credentials on startup.
            LogIn logIn = new LogIn(this);
            logIn.ShowDialog();
            if (!App.IsLoggedIn)
            {
                Close();
                App.WindowClosed(); // InitializeComponent() hasn't been called yet, so Window_Closed() won't fire.
            }

            InitializeComponent();

            // Look for RPT Exporter and grey out if it isn't found.
            if (App.currentDir != null)
                rptExporterLocation = App.currentDir + "/RPT Exporter/RPT Exporter.exe";
            if (!System.IO.File.Exists(rptExporterLocation))
            {
                menuRPTExporter.Visibility = Visibility.Collapsed;
                menuRPTExporterSeparator.Visibility = Visibility.Collapsed; 
            }

            frameConf.Content = new PageConferenceView();

            pageDatabase = new PageDatabase(this);
            frameData.Content = pageDatabase;

            grdConfData.Focus();

            grdMain.Children.Remove(menuBar);
            grdMain.Children.Remove(stkPaneButtons);
            grdMain.RowDefinitions[0].Height = new(0);
            grdMain.RowDefinitions[1].Height = new(0);
            AssignMenuBar(menuBar, stkPaneButtons, 0);
            if (ScheduleView.PrintColorFromLuminance((Color)Application.Current.Resources.MergedDictionaries[0]["colorTitleBar"]) == Colors.White)
                SetIcon("/Resources/Icons/x20TitleBarIconDark.png");
            else
                SetIcon("/Resources/Icons/x20TitleBarIcon.png");

            MinWidth = CONF_PANE_MIN_WIDTH + 16 + DATA_PANE_MIN_WIDTH;

            mixedPaneRedrawOverride = true;

            ApplyViewState();
            GreyOutPermissions();

            // DELETE !!!
            //stkPaneButtons.Visibility = Visibility.Collapsed;
        }

        public void ApplyViewState()
        {
            // DELETE !!!
            //viewState = 2;

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
            menuDatabaseNewConference.IsEnabled = App.sd.createPermissions[Glo.PERMISSION_CONFERENCES];
            menuDatabaseNewRecurrence.IsEnabled = App.sd.createPermissions[Glo.PERMISSION_CONFERENCES];
            menuDatabaseNewResource.IsEnabled = App.sd.createPermissions[Glo.PERMISSION_RESOURCES];

            // DELETE !!!
            //menuDatabaseNewConference.IsEnabled = false;
            //menuDatabaseNewRecurrence.IsEnabled = false;
            //menuDatabaseNewResource.IsEnabled = false;
        }

        private void menuDatabaseNewOrganisation_Click(object sender, RoutedEventArgs e)
        {
            bool successful;
            string[]? organisationList = App.GetOrganisationList(out successful, this);
            if (!successful)
                return;
            if (organisationList == null)
            {
                App.DisplayError("Could not pull organisation list from server.", this);
                return;
            }

            NewOrganisation newOrganisation = new();
            newOrganisation.cmbOrgParentID.ItemsSource = organisationList;
            newOrganisation.Show();
        }

        private void menuDatabaseNewAsset_Click(object sender, RoutedEventArgs e)
        {
            bool successful;
            string[]? organisationList = App.GetOrganisationList(out successful, this);
            if (!successful)
                return;
            if (organisationList == null)
            {
                App.DisplayError("Could not pull organisation list from server.", this);
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

        private void menuDatabaseNewConference_Click(object sender, RoutedEventArgs e)
        {
            DateTime start = DateTime.Now;
            start = start.Date + new TimeSpan(start.Hour + 1, 0, 0);
            NewConference newConference = new(null, start);
            newConference.Show();
        }

        private void menuDatabaseNewRecurrence_Click(object sender, RoutedEventArgs e)
        {
            NewRecurrence recurrence = new();
            recurrence.Owner = this;
            recurrence.ShowDialog();
        }

        private void menuDatabaseNewResource_Click(object sender, RoutedEventArgs e)
        {
            NewResource newResource = new();

            if (ColumnRecord.GetColumn(ColumnRecord.resource, Glo.Tab.RESOURCE_ID).friendlyName != "")
                newResource.lblResourceName.Content = ColumnRecord.GetColumn(ColumnRecord.resource,
                                                                             Glo.Tab.RESOURCE_ID).friendlyName;

            newResource.txtResourceName.MaxLength = Glo.Fun.LongToInt(ColumnRecord.GetColumn(ColumnRecord.resource,
                                                                      Glo.Tab.RESOURCE_ID).restriction);

            newResource.Show();
        }

        private void menuUserLogIn_Click(object sender, RoutedEventArgs e)
        {
            if (App.IsLoggedIn)
                App.DisplayError("Already logged in as " + App.sd.username + ".", this);
            else
            {
                LogIn logIn = new LogIn(this);
                logIn.Show();
            }
        }

        private void menuUserLogOut_Click(object sender, RoutedEventArgs e)
        {
            if (App.IsLoggedIn)
                App.LogOut(this);
            else
            {
                App.DisplayError("Not logged in.", this);
                // Just to be safe:
                ToggleLogInOut(false);
            }
        }

        private void menuChangePassword_Click(object sender, RoutedEventArgs e)
        {
            PasswordChange pc = new(App.sd.loginID, false);
            pc.Owner = this;
            pc.ShowDialog();
        }

        private void menuResetViewSettings_Click(object sender, RoutedEventArgs e)
        {
            if (App.DisplayQuestion("Are you sure? This will:\n" +
                                    "\n• Reset all data table view settings, such as hidden columns, column orders " +
                                        "and columns widths." +
                                    "\n• Reset pane selection and widths." +
                                    "\n• Reset data pane layout and heights." +
                                    "\n• Reset resource selection in the schedule pane.\n" +
                                    "\nYou will need to log back in.",
                                    "Reset View Settings",
                                    DialogWindows.DialogBox.Buttons.YesNo, this))
            {
                // Reset settings before LogOut(), and they will be stored on the database as part of that method.
                App.us = new UserSettings();
                foreach (PageConferenceView pcv in pageConferenceViews)
                    pcv.resourcesOrder = new();
                
                App.LogOut(this);
            }
        }

        private void menuSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settingsWindow = new();
            settingsWindow.Owner = this;
            if (App.IsLoggedIn) // Might not load if the session was invalidated.
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
            SelectBuilder selectBuilder = new(true);
            selectBuilder.Show();
        }

        private void menuSelectStatement_Click(object sender, RoutedEventArgs e)
        {
            SelectBuilder selectBuilder = new(false);
            selectBuilder.Show();
        }

        private void CustomWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (!(sender is TextBox) && !(sender is ComboBox) && !(sender is DataGrid))
                foreach (var view in pageConferenceViews)
                    view.RedrawGrid();
        }

        private void CustomWindow_KeyUp(object sender, KeyEventArgs e)
        {
            if (!(sender is TextBox) && !(sender is ComboBox) && !(sender is DataGrid))
                foreach (var view in pageConferenceViews)
                    view.RedrawGrid();
        }

        private void CustomWindow_Deactivated(object sender, EventArgs e)
        {
            foreach (PageConferenceView view in pageConferenceViews)
                view.CancelAllDrags(view.schView);
        }

        private void menuRPTExporter_Click(object sender, RoutedEventArgs e)
        {
            if (!System.IO.File.Exists(rptExporterLocation))
            {
                App.DisplayError("Unable to locate ./RPT Exporter/RPT Exporter.exe", this);
                return;
            }

            try
            {
                Process.Start(rptExporterLocation);
            }
            catch
            {
                App.DisplayError("Could not load RPT Exporter.exe", this);
            }
        }

        private void CustomWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (App.Current.Windows.Count > 1 &&
                !App.DisplayQuestion("Are you sure you wish to log out?",
                                     "Closing Application", DialogWindows.DialogBox.Buttons.YesNo, this))
                e.Cancel = true;
        }

        private void btnAbout_Click(object sender, RoutedEventArgs e)
        {
            About about = new();
            about.Owner = this;
            about.ShowDialog();
        }
    }
}
