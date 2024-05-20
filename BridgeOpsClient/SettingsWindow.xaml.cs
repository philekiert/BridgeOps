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
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            PopulateUserList();
        }

        private void PopulateUserList()
        {
            List<string?> columnNames;
            List<List<object?>> rows;

            //App.Select("Login", new List<string>() { Glo.Tab.LOGIN_USERNAME, Glo.Tab.}
            if (App.SelectAll("Login", out columnNames, out rows))
                dtgUsers.Update(ColumnRecord.login, columnNames, rows, Glo.Tab.CHANGE_ID);
        }

        private void btnUserAdd_Click(object sender, RoutedEventArgs e)
        {
            NewUser newUser = new();
            newUser.ShowDialog();
        }
    }
}
