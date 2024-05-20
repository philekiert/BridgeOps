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

            if (App.Select("Login", new List<string>() { Glo.Tab.LOGIN_USERNAME, Glo.Tab.LOGIN_ADMIN },
                out columnNames, out rows))
            {
                columnNames[1] = "Type";

                foreach (List<object?> row in rows)
                {
                    object? userType = row[1];
                    if (userType != null && userType.GetType() == typeof(bool))
                        row[1] = (bool)userType ? "Administrator" : "User";
                }

                dtgUsers.Update(ColumnRecord.login, columnNames, rows, Glo.Tab.CHANGE_ID);
            }
        }

        private void btnUserAdd_Click(object sender, RoutedEventArgs e)
        {
            NewUser newUser = new();
            newUser.ShowDialog();
        }
    }
}
