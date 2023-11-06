﻿using System;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using SendReceiveClasses;

namespace BridgeOpsClient
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            // Request login credentials on startup.
            LogIn logIn = new LogIn(this);
            logIn.ShowDialog();
            if (!App.IsLoggedIn)
                Close();

            InitializeComponent();

            frameMain.Content = new PageConferenceView();
            frameMain.Content = new PageDatabase(this);
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
            menuUserLogIn.IsEnabled = loggedIn;
            menuUserLogOut.IsEnabled = !loggedIn;
        }
    }
}
