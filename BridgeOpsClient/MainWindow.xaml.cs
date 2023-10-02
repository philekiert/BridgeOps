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
            LogIn logIn = new LogIn();
            logIn.ShowDialog();
            if (!App.IsLoggedIn)
                Close();

            InitializeComponent();

            frameMain.Content = new PageConferenceView();
        }

        private void menuDatabaseNewContact_Click(object sender, RoutedEventArgs e)
        {
            NewContact newContact = new();
            newContact.ditContact.Initialise(ColumnRecord.contact, "Contact");

            // Implemement friendly name.
            if (ColumnRecord.contact["Notes"].friendlyName != "")
                newContact.lblNotes.Content = ColumnRecord.contact["Notes"].friendlyName;
            newContact.txtNotes.MaxLength = ColumnRecord.contact["Notes"].restriction;

            newContact.Show();
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
            newOrganisation.ditOrganisation.Initialise(ColumnRecord.organisation, "Organisation");

            // Implement friendly names.
            if (ColumnRecord.organisation["Organisation_ID"].friendlyName != "")
                newOrganisation.lblOrgID.Content = ColumnRecord.organisation["Organisation_ID"].friendlyName;
            if (ColumnRecord.organisation["Parent_ID"].friendlyName != "")
                newOrganisation.lblOrgParentID.Content = ColumnRecord.organisation["Parent_ID"].friendlyName;
            if (ColumnRecord.organisation["Dial_No"].friendlyName != "")
                newOrganisation.lblDialNo.Content = ColumnRecord.organisation["Dial_No"].friendlyName;
            if (ColumnRecord.organisation["Notes"].friendlyName != "")
                newOrganisation.lblNotes.Content = ColumnRecord.organisation["Notes"].friendlyName;

            // Implement max lengths.
            newOrganisation.txtOrgID.MaxLength = ColumnRecord.organisation["Organisation_ID"].restriction;
            newOrganisation.txtDialNo.MaxLength = ColumnRecord.organisation["Dial_No"].restriction;
            newOrganisation.txtNotes.MaxLength = ColumnRecord.organisation["Notes"].restriction;

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
            newAsset.ditAsset.Initialise(ColumnRecord.asset, "Asset");

            // Implement friendly names.
            if (ColumnRecord.asset["Asset_ID"].friendlyName != "")
                newAsset.lblAssetID.Content = ColumnRecord.asset["Asset_ID"].friendlyName;
            if (ColumnRecord.asset["Organisation_ID"].friendlyName != "")
                newAsset.lblOrgID.Content = ColumnRecord.asset["Organisation_ID"].friendlyName;
            if (ColumnRecord.asset["Notes"].friendlyName != "")
                newAsset.lblNotes.Content = ColumnRecord.asset["Notes"].friendlyName;

            // Implement max lengths.
            newAsset.txtAssetID.MaxLength = ColumnRecord.asset["Asset_ID"].restriction;
            newAsset.txtNotes.MaxLength = ColumnRecord.asset["Notes"].restriction;

            newAsset.cmbOrgID.ItemsSource = organisationList;

            newAsset.Show();
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
                LogIn logIn = new LogIn();
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
    }
}
