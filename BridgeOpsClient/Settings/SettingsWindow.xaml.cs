using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
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

            dtgUsers.identity = 5;
            dtgColumns.identity = 6;

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

            dtgColumns.AddSeparator(true);
            MenuItem item = new MenuItem()
            {
                Header = "Remove",
            };
            item.Click += btnColumnRemove_Click;
            dtgColumns.AddContextMenuItem(item, true);

            dtgUsers.AddSeparator(true);
            item = new MenuItem()
            {
                Header = "Logout"
            };
            item.Click += btnUserLogOut_Click;
            dtgUsers.AddContextMenuItem(item, true);
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
                                                         Glo.Tab.LOGIN_ENABLED },
                out columnNames, out rows, false))
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
            else
                Close();
        }

        private void DisplayLoggedInUsers()
        {
            lock (App.streamLock)
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
                                   new() { SendReceiveClasses.Conditional.Equals },
                                   out columnNames, out rows, true, false))
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
                                App.DisplayError("Incorrect number of fields received.");
                        }
                        else
                            App.DisplayError("Could no longer retrieve record.");
                    }
                }
                else
                    App.DisplayError("User ID invalid.");
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
                    App.DisplayError("You cannot log yourself out from here.");
                    return;
                }
                App.LogOut(loginID);
                PopulateUserList();
            }
            else
                App.DisplayError("You must first select a user to log out.");
        }


        //   C O L U M N   L I S T

        private void PopulateColumnList()
        {
            lock (ColumnRecord.lockColumnRecord)
            {
                List<List<object?>> rows = new();

                void AddTable(string name, OrderedDictionary tableRecord, string[] omit)
                {
                    foreach (DictionaryEntry de in tableRecord)
                    {
                        string colName = (string)de.Key;
                        if (omit.Contains(colName))
                            continue;
                        ColumnRecord.Column col = (ColumnRecord.Column)de.Value!;
                        List<object?> row = new() { name,
                                                    colName,
                                                    col.friendlyName,
                                                    // Boolean is more widely understood by the user than BIT.
                                                    col.type == "BIT" ? "BOOLEAN" : col.type,
                                                    col.restriction == 0 ? "" : col.restriction.ToString(),
                                                    string.Join("; ", col.allowed) };
                        if (col.type == "VARCHAR" && col.restriction == Int32.MaxValue)
                            row[3] = "VARCHAR(MAX)";
                        rows.Add(row);
                    }
                }

                AddTable("Organisation", ColumnRecord.orderedOrganisation, new string[0]);
                AddTable("Asset", ColumnRecord.orderedAsset, new string[0]);
                AddTable("Contact", ColumnRecord.orderedContact, new string[0]);
                AddTable("Conference", ColumnRecord.orderedConference, new[] { Glo.Tab.CONFERENCE_CANCELLED,
                                                                               Glo.Tab.CONFERENCE_END,
                                                                               Glo.Tab.CONFERENCE_START,
                                                                               Glo.Tab.CONFERENCE_TYPE_ID,
                                                                               Glo.Tab.CONFERENCE_RESOURCE_ROW,
                                                                               Glo.Tab.CONFERENCE_TYPE_ID,
                                                                               Glo.Tab.RESOURCE_ID,
                                                                               Glo.Tab.CONFERENCE_CREATION_LOGIN,
                                                                               Glo.Tab.CONFERENCE_CREATION_TIME,
                                                                               Glo.Tab.CONFERENCE_EDIT_LOGIN,
                                                                               Glo.Tab.CONFERENCE_EDIT_TIME,
                                                                               Glo.Tab.ORGANISATION_REF,
                                                                               Glo.Tab.RECURRENCE_ID  });
                AddTable("Login", ColumnRecord.login, new[] { Glo.Tab.LOGIN_ADMIN,
                                                              Glo.Tab.LOGIN_CREATE_PERMISSIONS,
                                                              Glo.Tab.LOGIN_DELETE_PERMISSIONS,
                                                              Glo.Tab.LOGIN_EDIT_PERMISSIONS,
                                                              Glo.Tab.LOGIN_ENABLED,
                                                              Glo.Tab.LOGIN_PASSWORD,
                                                              Glo.Tab.LOGIN_VIEW_SETTINGS });

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
            string table = dtgColumns.GetCurrentlySelectedCell(0);
            string column = dtgColumns.GetCurrentlySelectedCell(1);
            ColumnRecord.Column? col = ColumnRecord.GetColumnNullable(table, column);
            if (col == null)
            {
                App.DisplayError("Something went wrong.");
                return;
            }

            NewColumn newColumn = new(table, column,
                (ColumnRecord.Column)col);
            newColumn.ShowDialog();
            if (newColumn.changeMade)
                InitiateTableChange();
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
            if (!App.DeleteConfirm(false))
                return;

            string table = dtgColumns.GetCurrentlySelectedCell(0);
            string column = dtgColumns.GetCurrentlySelectedCell(1);
            if (table == "" || column == "")
            {
                App.DisplayError("You must first select a column to remove.");
                return;
            }

            if (!Glo.Fun.ColumnRemovalAllowed(table, column))
            {
                App.DisplayError("This column is integral to the running of the application, and cannot be removed.");
                return;
            }

            SendReceiveClasses.TableModification mod = new(App.sd.sessionID, ColumnRecord.columnRecordID,
                                                           table, column);

            lock (App.streamLock)
            {
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
                        else if (response == Glo.CLIENT_CONFIRM)
                        {
                            if (App.DisplayQuestion("This column contains data, either current or historical. " +
                                                    "There will be no way to retrieve this data, so it is advisable to back " +
                                                    "up the database before making changes that could result in data loss." +
                                                    "\n\nAre you sure you wish to proceed?",
                                                    "Remove Column", App.QuestionOptions.OKCancel))
                            {
                                stream.WriteByte(Glo.CLIENT_CONFIRM);
                                if (stream.ReadByte() != Glo.CLIENT_REQUEST_SUCCESS)
                                    throw new Exception();
                                else
                                    InitiateTableChange();
                            }
                            else
                                stream.WriteByte(Glo.CLIENT_CANCEL);
                        }
                        else if (response == Glo.CLIENT_SESSION_INVALID)
                        {
                            App.SessionInvalidated();
                            return;
                        }
                        else if (response == Glo.CLIENT_INSUFFICIENT_PERMISSIONS)
                        {
                            // Shouldn't ever arrive here.
                            App.DisplayError("Only admins can make table modifications.");
                            return;
                        }
                        else if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                        {
                            App.DisplayError("The column could not be removed. See SQL error:\n\n" +
                                            App.sr.ReadString(stream));
                            return;
                        }
                        else
                            throw new Exception();
                    }
                    else
                        App.DisplayError("Could not create network stream.");
                }
                catch
                {
                    App.DisplayError("Could not run table update.");
                    return;
                }
                finally
                {
                    if (stream != null) stream.Close();
                }
            }
        }

        private void InitiateTableChange()
        {
            App.PullColumnRecord();
            PopulateColumnList();
        }

        private void dtgColumns_SelectionChanged(object sender, RoutedEventArgs e)
        {
            btnColumnRemove.IsEnabled = Glo.Fun.ColumnRemovalAllowed(dtgColumns.GetCurrentlySelectedCell(0),
                                                                     dtgColumns.GetCurrentlySelectedCell(1));
            ((MenuItem)dtgColumns.mnuData.Items[0]).IsEnabled = btnColumnRemove.IsEnabled;
        }

        private void dtgUsers_SelectionChanged(object sender, RoutedEventArgs e)
        {
            btnUserLogOut.IsEnabled = !(dtgUsers.GetCurrentlySelectedID() == App.sd.loginID.ToString()) &&
                                      !(dtgUsers.GetCurrentlySelectedCell(4) == "Inactive");
            ((MenuItem)dtgUsers.mnuData.Items[0]).IsEnabled = btnUserLogOut.IsEnabled;
        }

        private void btnReorder_Click(object sender, RoutedEventArgs e)
        {
            ReorderColumns reorderColumns = new();
            reorderColumns.ShowDialog();
            if (reorderColumns.changeMade)
                InitiateTableChange();
        }
    }
}
