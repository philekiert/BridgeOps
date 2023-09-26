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
            NewContact newContact = new NewContact();
            newContact.Show();
        }

        private void menuUserLogIn_Click(object sender, RoutedEventArgs e)
        {
            LogIn logIn = new LogIn();
            logIn.Show();
        }

        private void menuUserLogOut_Click(object sender, RoutedEventArgs e)
        {
            App.LogOut();
        }
    }
}
