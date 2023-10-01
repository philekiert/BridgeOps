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

            newContact.Show();
        }

        private void menuDatabaseNewOrganisation_Click(object sender, RoutedEventArgs e)
        {
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

            string[]? organisationList = App.SelectColumnPrimary("Organisation", "Organisation_ID");
            newOrganisation.cmbOrgParentID.ItemsSource = organisationList;

            newOrganisation.Show();
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
