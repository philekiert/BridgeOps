﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
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

            bool editPermission = App.sd.editPermissions[Glo.PERMISSION_USER_ACC_MGMT];
            if (!editPermission)
                btnUserLogOut.IsEnabled = false;
            if (!App.sd.createPermissions[Glo.PERMISSION_USER_ACC_MGMT])
                btnUserAdd.IsEnabled = false;

            if (!App.sd.admin)
                tabDatabaseLayout.IsEnabled = false;
            else
                PopulateColumnList();
        }

        List<string?> columnNames = new();
        List<List<object?>> rows = new();
        Dictionary<int, int> dctID = new(); // Used to store indices of user rows for "logged in" display.


        //   U S E R   L I S T

        private void PopulateUserList()
        {
            dctID.Clear();

            if (App.Select("Login", new List<string>() { Glo.Tab.LOGIN_ID,
                                                         Glo.Tab.LOGIN_USERNAME,
                                                         Glo.Tab.LOGIN_ADMIN,
                                                         Glo.Tab.LOGIN_ENABLED},
                out columnNames, out rows))
            {
                columnNames.Add("Status");
                columnNames[2] = "Type";

                for (int n = 0; n < rows.Count; ++n)
                {
                    // The row needs extending by 1 in order to add a column to the SqlDataGrid.
                    rows[n].Add("Inactive");

                    object? userType = rows[n][2];
                    if (userType != null && userType.GetType() == typeof(bool))
                        rows[n][2] = (bool)userType ? "Administrator" : "User";
                    object? userEnabled = rows[n][3];
                    if (userEnabled != null && userEnabled.GetType() == typeof(bool))
                        rows[n][3] = (bool)userEnabled ? "Yes" : "No";

                    object? userID = rows[n][0];
                    if (userID != null)
                        dctID.Add(((int)userID), n);
                }

                DisplayLoggedInUsers();

                dtgUsers.Update(ColumnRecord.login, columnNames, rows, Glo.Tab.CHANGE_ID, "Login_ID");
            }
        }

        private void DisplayLoggedInUsers()
        {
            NetworkStream? stream = App.sr.NewClientNetworkStream(App.sd.ServerEP);
            try
            {
                if (stream == null)
                    throw new Exception(App.NO_NETWORK_STREAM);
                stream.WriteByte(Glo.CLIENT_LOGGEDIN_LIST);
                App.sr.WriteAndFlush(stream, App.sd.sessionID);
                List<object[]>? loggedIn = App.sr.Deserialise<List<object[]>>(App.sr.ReadString(stream));

                if (loggedIn != null)
                    foreach (object[]? o in loggedIn)
                        if (o != null)
                        {
                            int index;
                            if (int.TryParse(o[0].ToString(), out index))

                                rows[dctID[index]][4] = "Logged In";
                        }
            }
            catch { }
        }

        private void dtgUsers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!App.sd.editPermissions[Glo.PERMISSION_USER_ACC_MGMT])
                return;

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

        private void btnUsersRefresh_Click(object sender, RoutedEventArgs e)
        {
            PopulateUserList();
        }

        private void btnUserAdd_Click(object sender, RoutedEventArgs e)
        {
            NewUser newUser = new();
            newUser.ShowDialog();
            if (newUser.didSomething)
                PopulateUserList();
        }

        private void btnUserLogOut_Click(object sender, RoutedEventArgs e)
        {
            int loginID;
            if (int.TryParse(dtgUsers.GetCurrentlySelectedID(), out loginID))
            {
                if (loginID == App.sd.loginID)
                {
                    MessageBox.Show("You cannot log yourself out from here.");
                    return;
                }
                App.LogOut(loginID);
                PopulateUserList();
            }
            else
                MessageBox.Show("You must first select a user to log out.");
        }


        //   C O L U M N   L I S T

        private void PopulateColumnList()
        {
            lock (ColumnRecord.lockColumnRecord)
            {
                List<List<object?>> rows = new();

                foreach (KeyValuePair<string, ColumnRecord.Column> col in ColumnRecord.organisation)
                {
                    List<object?> row = new() { "Organisation",
                                                col.Key,
                                                col.Value.friendlyName,
                                                col.Value.type,
                                                col.Value.restriction == 0 ? "" : col.Value.restriction.ToString(),
                                                string.Join("; ", col.Value.allowed) };
                    rows.Add(row);
                }

                foreach (KeyValuePair<string, ColumnRecord.Column> col in ColumnRecord.contact)
                {
                    List<object?> row = new() { "Contact",
                                                col.Key,
                                                col.Value.friendlyName,
                                                col.Value.type,
                                                col.Value.restriction == 0 ? "" : col.Value.restriction.ToString(),
                                                string.Join("; ", col.Value.allowed) };
                    rows.Add(row);
                }

                foreach (KeyValuePair<string, ColumnRecord.Column> col in ColumnRecord.asset)
                {
                    List<object?> row = new() { "Asset",
                                                col.Key,
                                                col.Value.friendlyName,
                                                col.Value.type,
                                                col.Value.restriction == 0 ? "" : col.Value.restriction.ToString(),
                                                string.Join("; ", col.Value.allowed) };
                    rows.Add(row);
                }

                foreach (KeyValuePair<string, ColumnRecord.Column> col in ColumnRecord.conference)
                {
                    List<object?> row = new() { "Conference",
                                                col.Key,
                                                col.Value.friendlyName,
                                                col.Value.type,
                                                col.Value.restriction == 0 ? "" : col.Value.restriction.ToString(),
                                                string.Join("; ", col.Value.allowed) };
                    rows.Add(row);
                }

                dtgColumns.maxLengthOverrides = new Dictionary<string, int> { { "Allowed", -1 } };

                dtgColumns.Update(new List<string?>() { "Table", "Column", "Friendly Name",
                                                        "Type", "Max", "Allowed" }, rows);
            }
        }

        private void btnColumnsRefresh_Click(object sender, RoutedEventArgs e)
        {
            App.PullColumnRecord();
            PopulateColumnList();
        }

        private void dtgColumns_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

        }

        private void btnColumnAdd_Click(object sender, RoutedEventArgs e)
        {
            NewColumn newColumn = new NewColumn();
            newColumn.ShowDialog();
            if (newColumn.changeMade)
                InitiateTableChange();
        }

        private void btnColumnRemove_Click(object sender, RoutedEventArgs e)
        {
            string table = dtgColumns.GetCurrentlySelectedCell(0);
            string column = dtgColumns.GetCurrentlySelectedCell(1);
            if (table == "" || column == "")
            {
                MessageBox.Show("You must first select a column to remove.");
                return;
            }

            bool allowed = column != "Notes";
            if (allowed)
                allowed = !((table == "Organisation" &&
                              (column == Glo.Tab.ORGANISATION_ID ||
                               column == Glo.Tab.PARENT_ID ||
                               column == Glo.Tab.DIAL_NO)) ||
                            (table == "Asset" &&
                              (column == Glo.Tab.ASSET_ID ||
                               column == Glo.Tab.ORGANISATION_ID)) ||
                            (table == "Contact" &&
                              (column == Glo.Tab.CONTACT_ID)) ||
                            (table == "Conference" &&
                              (column == Glo.Tab.CONFERENCE_ID ||
                               column == Glo.Tab.RESOURCE_ID ||
                               column == Glo.Tab.ORGANISATION_RESOURCE_ROW ||
                               column == Glo.Tab.CONFERENCE_TYPE ||
                               column == Glo.Tab.CONFERENCE_TITLE ||
                               column == Glo.Tab.CONFERENCE_START ||
                               column == Glo.Tab.CONFERENCE_END ||
                               column == Glo.Tab.CONFERENCE_BUFFER ||
                               column == Glo.Tab.ORGANISATION_ID ||
                               column == Glo.Tab.RECURRENCE_ID)));
            if (!allowed)
            {
                MessageBox.Show("This column is integral to the running of the application, and cannot be removed.");
                return;
            }

            SendReceiveClasses.TableModification mod = new(App.sd.sessionID, table, column);

            NetworkStream? stream = App.sr.NewClientNetworkStream(App.sd.ServerEP);
            try
            {
                if (stream != null)
                {
                    stream.WriteByte(Glo.CLIENT_TABLE_MODIFICATION);
                    App.sr.WriteAndFlush(stream, App.sr.Serialise(mod));
                    int response = stream.ReadByte();
                    if (response == Glo.CLIENT_REQUEST_SUCCESS)
                    {
                        InitiateTableChange();
                        return;
                    }
                    else if (response == Glo.CLIENT_SESSION_INVALID)
                    {
                        App.SessionInvalidated();
                        return;
                    }
                    else if (response == Glo.CLIENT_INSUFFICIENT_PERMISSIONS)
                    {
                        // Shouldn't ever arrive here.
                        MessageBox.Show("Only admins can make table modifications.");
                        return;
                    }
                    else
                        MessageBox.Show("Something went wrong.");
                }
                else
                    MessageBox.Show("Could not create network stream.");
            }
            catch
            {
                MessageBox.Show("Could not run table update.");
                return;
            }
            finally
            {
                if (stream != null) stream.Close();
            }

        }

        private void InitiateTableChange()
        {
            App.PullColumnRecord();
            PopulateColumnList();
        }
    }
}
