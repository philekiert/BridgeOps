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

        public MainWindow()
        {
            // Request login credentials on startup.
            LogIn logIn = new LogIn(this);
            logIn.ShowDialog();
            if (!App.IsLoggedIn)
                Close();

            InitializeComponent();

            frameConf.Content = new PageConferenceView();
            frameData.Content = new PageDatabase(this);
            MinWidth = CONF_PANE_MIN_WIDTH + 16 + DATA_PANE_MIN_WIDTH;

            // Set default view here:
            //btnConfPane_Click(new bool(), new RoutedEventArgs()); // Conference View
            btnDataPane_Click(new bool(), new RoutedEventArgs()); // Data View
            //btnMixedPane_Click(new bool(), new RoutedEventArgs()); // Mixed View
        }

        private void menuDatabaseNewOrganisation_Click(object sender, RoutedEventArgs e)
        {
            string[]? organisationList = App.SelectColumnPrimary("Organisation", "Organisation_ID");
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
            string[]? organisationList = App.SelectColumnPrimary("Organisation", "Organisation_ID");
            if (organisationList == null)
            {
                MessageBox.Show("Could not pull organisation list from server.");
                return;
            }

            NewAsset newAsset = new();
            newAsset.cmbOrgID.ItemsSource = organisationList;
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

            if (ColumnRecord.resource["Resource_ID"].friendlyName != "")
                newResource.lblResourceName.Content = ColumnRecord.resource["Resource_ID"].friendlyName;
            if (ColumnRecord.resource["Available_From"].friendlyName != "")
                newResource.lblAvailableFrom.Content = ColumnRecord.resource["Available_From"].friendlyName;
            if (ColumnRecord.resource["Available_To"].friendlyName != "")
                newResource.lblAvailableTo.Content = ColumnRecord.resource["Available_To"].friendlyName;

            newResource.txtResourceName.MaxLength = ColumnRecord.resource["Resource_ID"].restriction;

            newResource.Show();
        }

        private void menuDatabaseNewConferenceType_Click(object sender, RoutedEventArgs e)
        {
            NewConferenceType newResource = new();

            if (ColumnRecord.conferenceType["Type_Name"].friendlyName != "")
                newResource.lblTypeName.Content = ColumnRecord.conferenceType["Type_Name"].friendlyName;

            newResource.txtTypeName.MaxLength = ColumnRecord.conferenceType["Type_Name"].restriction;

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

        public void ToggleLogInOut(bool loggedIn)
        {
            // This can fail if only the login window is visible, no big deal and no need to report.
            try
            {
                menuUserLogIn.IsEnabled = !loggedIn;
                menuUserLogOut.IsEnabled = loggedIn;
            }
            catch { }
        }

        /* This is surely needlessly verbose, will optimise later if time allows. Removing and re-adding the columns
         * was the only way I could get it all to work. */
        double oldConfWidth = 1;
        double oldDataWidth = 1;

        private void btnConfPane_Click(object sender, RoutedEventArgs e)
        {
            if (frameData.Visibility == Visibility.Visible)
            {
                // If we're coming from a pane split, remember their widths.
                if (frameConf.Visibility == Visibility.Visible)
                {
                    oldConfWidth = grdConfData.ColumnDefinitions[0].Width.Value;
                    oldDataWidth = grdConfData.ColumnDefinitions[1].Width.Value;
                }

                SetMinWidth(0);

                frameConf.Visibility = Visibility.Visible;
                spltConfData.Visibility = Visibility.Collapsed;
                frameData.Visibility = Visibility.Collapsed;

                grdConfData.ColumnDefinitions.Clear();
                ColumnDefinition col = new ColumnDefinition();
                col.MinWidth = CONF_PANE_MIN_WIDTH;
                grdConfData.ColumnDefinitions.Add(col);

                if (frameConf.Content is PageConferenceView pcv)
                    pcv.scrollBar.Margin = new Thickness(0);
            }
        }

        private void btnMixedPane_Click(object sender, RoutedEventArgs e)
        {
            if (frameConf.Visibility == Visibility.Collapsed || frameData.Visibility == Visibility.Collapsed)
            {
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

                if (frameConf.Content is PageConferenceView pcv)
                    pcv.scrollBar.Margin = new Thickness(0, 0, 7, 0);
            }
        }

        private void btnDataPane_Click(object sender, RoutedEventArgs e)
        {
            if (frameConf.Visibility == Visibility.Visible)
            {
                // If we're coming from a pane split, remember their widths.
                if (frameData.Visibility == Visibility.Visible)
                {
                    oldConfWidth = grdConfData.ColumnDefinitions[0].Width.Value;
                    oldDataWidth = grdConfData.ColumnDefinitions[1].Width.Value;
                }

                SetMinWidth(2);

                frameConf.Visibility = Visibility.Collapsed;
                spltConfData.Visibility = Visibility.Collapsed;
                frameData.Visibility = Visibility.Visible;

                grdConfData.ColumnDefinitions.Clear();
                ColumnDefinition col = new ColumnDefinition();
                col.MinWidth = CONF_PANE_MIN_WIDTH;
                grdConfData.ColumnDefinitions.Add(col);

                frameConf.SetValue(Grid.RowProperty, 0);

                if (frameConf.Content is PageConferenceView pcv)
                    pcv.scrollBar.Margin = new Thickness(0);
            }
        }
    }
}
