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

            if (App.Select("Login", new List<string>() { Glo.Tab.LOGIN_ID,
                                                         Glo.Tab.LOGIN_USERNAME,
                                                         Glo.Tab.LOGIN_ADMIN,
                                                         Glo.Tab.LOGIN_ENABLED},
                out columnNames, out rows))
            {
                columnNames[2] = "Type";

                foreach (List<object?> row in rows)
                {
                    object? userType = row[2];
                    if (userType != null && userType.GetType() == typeof(bool))
                        row[2] = (bool)userType ? "Administrator" : "User";
                    object? userEnabled = row[3];
                    if (userEnabled != null && userEnabled.GetType() == typeof(bool))
                        row[3] = (bool)userEnabled ? "Yes" : "No";
                }

                dtgUsers.Update(ColumnRecord.login, columnNames, rows, Glo.Tab.CHANGE_ID, "Login_ID");
            }
        }

        private void btnUserAdd_Click(object sender, RoutedEventArgs e)
        {
            NewUser newUser = new();
            newUser.ShowDialog();
            if (newUser.didSomething)
                PopulateUserList();
        }

        private void dtgUsers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            string currentID = dtgUsers.GetCurrentlySelectedID();
            if (currentID != "")
            {
                int id;
                if (int.TryParse(currentID, out id))
                {
                    List<string?> columnNames;
                    List<List<object?>> rows;
                    if (App.Select("Login",
                                   new() { "*" },
                                   new() { Glo.Tab.LOGIN_ID },
                                   new() { id.ToString() },
                                   out columnNames, out rows))
                    {
                        if (rows.Count > 0)
                        {
                            // We expect data for every field. If the count is different, the operation must have failed.
                            if (rows[0].Count == ColumnRecord.login.Count)
                            {
                                NewUser user = new NewUser(id);
                                user.Populate(rows[0]);
                                user.ShowDialog();
                                if (user.didSomething)
                                    PopulateUserList();
                            }
                            else
                                MessageBox.Show("Incorrect number of fields received.");
                        }
                        else
                            MessageBox.Show("Could no longer retrieve record.");
                    }
                }
                else
                    MessageBox.Show("User ID invalid.");
            }
        }
    }
}
