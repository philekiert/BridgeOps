using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Office2010.Excel;
using Irony;
using SendReceiveClasses;
using static BridgeOpsClient.PageConferenceView;
using CR = ColumnRecord;

namespace BridgeOpsClient
{
    public partial class App : Application
    {
        public static bool IsLoggedIn { get { return sd.sessionID != ""; } }

        public static string ErrorConcat(string error, string additional)
        {
            return additional == "" ? error : $"{error} See error:\n\n{additional}";
        }
        public static void DisplayError(string error) { DisplayError(error, ""); }
        public static void DisplayError(string error, string title)
        {
            try
            {
                DialogWindows.DialogBox dialog = new(error, title);
                dialog.ShowDialog();
            }
            catch { }
        }
        public static void DisplayError(Window owner, string error) { DisplayError(owner, error, ""); }
        public static void DisplayError(Window owner, string error, string title)
        {
            try
            {
                DialogWindows.DialogBox dialog = new(error, title);
                dialog.Owner = owner;
                dialog.ShowDialog();
            }
            catch { }
        }
        public enum QuestionOptions
        { YesNo, OKCancel }
        public static bool DisplayQuestion(string error, string title, DialogWindows.DialogBox.Buttons buttons)
        {
            try
            {
                DialogWindows.DialogBox dialog = new(error, title, buttons);
                return dialog.ShowDialog() == true;
            }
            catch { return false; }
        }
        public static bool DeleteConfirm(bool multiple)
        {
            try
            {
                return DisplayQuestion("Are you sure? It will be impossible to recover " +
                                  $"{(multiple ? "these items." : "this item.")}",
                                   "Confirm Deletion",
                                   DialogWindows.DialogBox.Buttons.YesNo);
            }
            catch { return false; }
        }

        // This is a handy function to have, as a lot of other functions display an error and then return false.
        public static bool Abort(string message)
        {
            DisplayError(message);
            return false;
        }

        public static bool RowClashConfirm()
        {
            try
            {
                DialogWindows.DialogBox dialog = new(Glo.ROW_CLASH_WARNING + "\n\nDo you wish to resolve automatically?",
                                                     "Row Clash",
                                                 DialogWindows.DialogBox.Buttons.YesNo);
                dialog.ShowDialog();
                return dialog.DialogResult == true;
            }
            catch { return false; }
        }
        public static bool DialNoClashConfirm(SelectResult selectRes)
        {
            try
            {
                ConvertUnknownJsonObjectsToRespectiveTypes(selectRes.columnTypes, selectRes.rows);
                DialogWindows.DialogBox dialog = new(Glo.DIAL_CLASH_WARNING + " Do you wish to proceed?",
                                                     "Dial Clash",
                                                 DialogWindows.DialogBox.Buttons.YesNo,
                                                 selectRes.columnNames, selectRes.rows);
                dialog.ShowDialog();
                return dialog.DialogResult == true;
            }
            catch { return false; }


        }
        public static bool ResourceOverflowConfirm(SelectResult selectRes)
        {
            try
            {
                ConvertUnknownJsonObjectsToRespectiveTypes(selectRes.columnTypes, selectRes.rows);
                selectRes.columnNames[3] = "Some Time Around";
                selectRes.columnNames[4] = "Dial No Count";
                selectRes.columnNames[5] = "Conference Count";
                foreach (var row in selectRes.rows)
                {
                    if (row[4] == null)
                        row[4] = "-";
                    else if (row[5] == null)
                        row[5] = "-";
                }
                DialogWindows.DialogBox dialog = new(Glo.RESOURCE_OVERFLOW_WARNING + " Do you wish to proceed?",
                                                     "Resource Overflow",
                                                 DialogWindows.DialogBox.Buttons.YesNo,
                                                 selectRes.columnNames, selectRes.rows);
                dialog.ShowDialog();
                return dialog.DialogResult == true;
            }
            catch { return false; }
        }

        public static string NO_NETWORK_STREAM = "NetworkStream could not be connected.";
        public static string PERMISSION_DENIED = "You do not have sufficient permissions to carry out this action.";

        // The listener thread can't interact with WPF components, so have this timer handle anything that comes
        // in that requires that functionality, such as force logouts.
        static bool forceLogoutQueued = false;
        static bool recurrenceUpdateQueued = false;
        DispatcherTimer tmrStatusCheck;
        private void StatusCheck(object? sender, EventArgs e)
        {
            if (forceLogoutQueued)
            {
                if (IsLoggedIn)
                    SessionInvalidated();
                forceLogoutQueued = false;
            }
            if (recurrenceUpdateQueued)
            {
                foreach (object cw in App.Current.Windows)
                    if (cw is EditRecurrence er)
                        er.Refresh();
                recurrenceUpdateQueued = false;
            }
        }

        public static string documentsFolder = "";
        public static string networkConfigFile = "";

        public App()
        {
            // Set current working directory.
            string? currentDir = System.Reflection.Assembly.GetExecutingAssembly().Location;
            currentDir = Path.GetDirectoryName(currentDir);
            if (currentDir == null)
                DisplayError("Could not get working directory for application.");
            else
                Environment.CurrentDirectory = currentDir;

            documentsFolder = Glo.Fun.ApplicationFolder();
            networkConfigFile = Path.Combine(documentsFolder, Glo.CONFIG_NETWORK_CLIENT);

            // Apply network settings.
            try
            {
                // Check for Bridge Manager folder, and generate if needed.
                Glo.Fun.ExistsOrCreateFolder(Glo.Fun.ApplicationFolder());
                if (!File.Exists(networkConfigFile))
                    File.WriteAllText(networkConfigFile, "127.0.0.1;" +
                                                         Glo.PORT_OUTBOUND_DEFAULT.ToString() + ";" +
                                                         Glo.PORT_INBOUND_DEFAULT.ToString());
                string[] networkSettings = File.ReadAllText(networkConfigFile).Split(';');

                sd.SetServerIP(networkSettings[0]);
                if (!int.TryParse(networkSettings[1], out sd.portInbound) ||
                    !int.TryParse(networkSettings[2], out sd.portOutbound))
                    throw new();
                try
                {
                    sd.InitialiseListener();
                    listenerThread = new Thread(new ThreadStart(ListenForServer));
                    listenerThread.Start();
                }
                catch
                {
                    DisplayError("Could not initiate TCP listener. Please check network adapter is enabled and " +
                                    "reload the application.");
                    Environment.Exit(1);
                }
            }
            catch
            {
                DisplayError("Something went wrong when applying the network settings. " +
                             "Using loopback address, and ports 52343 (outbound) and 52344 (inbound).");
                sd.SetServerIP("127.0.0.1");
                sd.portInbound = Glo.PORT_INBOUND_DEFAULT;
                sd.portOutbound = Glo.PORT_OUTBOUND_DEFAULT;
            }

            foreach (Window win in Application.Current.Windows)
                if (win is BridgeOpsClient.MainWindow mainWin)
                    mainWindow = mainWin;

            tmrStatusCheck = new()
            {
                Interval = new TimeSpan(TimeSpan.TicksPerSecond / 10)
            };
            tmrStatusCheck.Tick += StatusCheck;
            tmrStatusCheck.Start();
        }

        public static Thread? listenerThread;
        private static void ListenForServer()
        {
            sd.InitialiseListener();

            if (sd.listener == null)
            {
                listenerThread = null;
                return;
            }
            else
            {
                try
                {
                    sd.listener.Start();

                    while (true)
                    {
                        // Start an ASync Accept.
                        IAsyncResult result = sd.listener.BeginAcceptTcpClient(HandleServerListenAccept, sd.listener);
                        autoResetEvent.WaitOne();
                        autoResetEvent.Reset();
                    }
                }
                catch
                {
                    listenerThread = null;
                }
            }
        }
        private static AutoResetEvent autoResetEvent = new(false);
        private static void HandleServerListenAccept(IAsyncResult result)
        {
            if (result.AsyncState != null)
            {
                TcpListener listener = (TcpListener)result.AsyncState;
                TcpClient client = listener.EndAcceptTcpClient(result);

                NetworkStream stream = client.GetStream();
                autoResetEvent.Set();

                try
                {
                    int fncByte = stream.ReadByte();
                    if (fncByte == Glo.SERVER_COLUMN_RECORD_UPDATED)
                    {
                        if (!PullColumnRecord())
                            DisplayError("The column record is out of date, but a new one could not be pulled. " +
                                            "Logging out.");
                        else
                        {
                            DisplayError("Column record has been updated. It would be advisable to restart the " +
                                            "application.");
                        }
                    }
                    else if (fncByte == Glo.SERVER_RESOURCES_UPDATED)
                    {
                        PullResourceInformation();
                    }
                    else if (fncByte == Glo.SERVER_CLIENT_NUDGE)
                    {
                        stream.WriteByte(Glo.SERVER_CLIENT_NUDGE);
                    }
                    else if (fncByte == Glo.SERVER_FORCE_LOGOUT)
                    {
                        forceLogoutQueued = true;
                    }
                    else if (fncByte == Glo.SERVER_CONFERENCES_UPDATED)
                    {
                        foreach (PageConferenceView pcv in BridgeOpsClient.MainWindow.pageConferenceViews)
                            pcv.SearchTimeframe();
                        recurrenceUpdateQueued = true;
                    }
                }
                catch
                {
                    // Haven't decided what to do here yet.
                }
                finally
                {
                    if (client.Connected)
                        client.Close();
                }
            }
        }

        public static SendReceive sr = new();
        public static SessionDetails sd = new();
        public static UserSettings us = new();
        public static object streamLock = new();

        public static MainWindow? mainWindow = null;

        private void ApplicationExit(object sender, EventArgs e)
        {
            LogOut();
        }

        public static string LogIn(string username, string password)
        {
            lock (streamLock)
            {
                LoginRequest loginReq = new(username, password);
                string send = sr.Serialise(loginReq);

                us = new UserSettings();

                NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                try
                {
                    if (stream == null)
                        throw new Exception(NO_NETWORK_STREAM);
                    stream.WriteByte(Glo.CLIENT_LOGIN);
                    sr.WriteAndFlush(stream, send);

                    string result = sr.ReadString(stream);

                    if (result.StartsWith(Glo.CLIENT_LOGIN_ACCEPT))
                    {
                        sd.admin = stream.ReadByte() == 0 ? false : true;
                        sd.createPermissions = Glo.Fun.GetPermissionsArray(stream.ReadByte());
                        sd.editPermissions = Glo.Fun.GetPermissionsArray(stream.ReadByte());
                        sd.deletePermissions = Glo.Fun.GetPermissionsArray(stream.ReadByte());
                        sd.username = username;

                        if (!int.TryParse(sr.ReadString(stream), out sd.loginID))
                            return "";

                        sd.sessionID = result.Replace(Glo.CLIENT_LOGIN_ACCEPT, "");

                        return Glo.CLIENT_LOGIN_ACCEPT;
                    }
                    else
                    {
                        return result;
                    }
                }
                catch (Exception e)
                {
                    return e.Message;
                }
            }
        }

        public static bool LogOut()
        {
            return LogOut(sd.loginID);
        }
        public static bool LogOut(int loginID) // Used for logging out either self or others.
        {
            try
            {
                if (IsLoggedIn)
                {
                    // Only store the user settings if it's actually their session logging out.
                    string? settings = null;
                    if (loginID == sd.loginID && us.settingsPulled)
                    {
                        settings = "";
                        // Store the data table column configurations.
                        for (int i = 0; i < us.dataOrder.Length; ++i)
                            settings += string.Join(";", us.dataOrder[i]) + "\n";
                        for (int i = 0; i < us.dataHidden.Length; ++i)
                            settings += string.Join(";", us.dataHidden[i]) + "\n";
                        for (int i = 0; i < us.dataWidths.Length; ++i)
                            settings += string.Join(";", us.dataWidths[i]) + "\n";
                        // Store the conference and database view state and widths.
                        settings += (int)BridgeOpsClient.MainWindow.oldConfWidth + ";" +
                                    (int)BridgeOpsClient.MainWindow.oldDataWidth + ";" +
                                    BridgeOpsClient.MainWindow.viewState + "\n";
                        // Loosely store the database view pane states.
                        foreach (PageDatabaseView pdv in PageDatabase.views)
                        {
                            settings += pdv.cmbTable.Text + ";";
                            settings += pdv.GetRowDefinitionForFrame().Height.Value.ToString() + ";";
                        }
                        us = new();
                    }

                    lock (streamLock)
                    {
                        NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);

                        if (stream != null)
                        {
                            stream.WriteByte(Glo.CLIENT_LOGOUT);
                            sr.WriteAndFlush(stream, sr.Serialise(new LogoutRequest(sd.sessionID, loginID, settings)));
                            if (loginID == sd.loginID)
                            {
                                sr.ReadString(stream); // Empty the pipe.

                                // No real need for an error if connection is lost and the logout 'fails'. An error
                                // will present when the user tries to log in again.
                                sd.sessionID = "";

                                if (mainWindow != null)
                                {
                                    mainWindow.ToggleLogInOut(false);
                                    mainWindow.ClearSqlDataGrids();
                                }
                            }
                            else
                            {
                                string response = sr.ReadString(stream);
                                if (response == Glo.CLIENT_LOGOUT_SESSION_NOT_FOUND)
                                    DisplayError("Could not find this user's session.");
                                else if (response == Glo.CLIENT_LOGOUT_ACCEPT)
                                    DisplayError("User logged out successfully");
                                else if (response == Glo.CLIENT_INSUFFICIENT_PERMISSIONS.ToString())
                                    DisplayError("You do not have the required permissions for this action");
                                else
                                    throw new Exception();
                            }
                        }
                    }
                }
                if (Current.MainWindow != null && loginID == sd.loginID && !Current.Windows.OfType<LogIn>().Any())
                {
                    // Thought about making this while (!IsLoggedIn) in case the user closes the login window, but
                    // decided it might be useful for the user to get back to the app if they accidentally clicked
                    // the button, need to copy some unsaved work, then become disconnected for some reason.

                    // Sometimes logout will fail, make sure to ungrey the button anyway.
                    if (IsLoggedIn && mainWindow != null)
                        mainWindow.ToggleLogInOut(false);

                    LogIn logIn = new((MainWindow)Current.MainWindow);
                    logIn.ShowDialog();
                }

                return true;
            }
            catch
            {
                DisplayError("Something went wrong.");
                return false;
            }
        }

        public static string[] GetOrganisationList(out bool successful)
        {
            string[]? organisationArray = SelectColumnPrimary("Organisation", Glo.Tab.ORGANISATION_REF,
                                                              out successful);
            if (!successful)
                return new string[0];

            if (organisationArray == null)
            {
                DisplayError("Could not pull organisation list from server.");
                successful = false;
                return new string[0];
            }
            successful = true;
            return organisationArray;
        }

        public static bool SessionInvalidated()
        {
            sd.sessionID = ""; // Tells the app that it's no longer logged in.
            if (mainWindow != null)
            {
                mainWindow.ToggleLogInOut(false);
                if (mainWindow.IsLoaded)
                    // Specify the owner as the main window, otherwise the user might miss it if called from a timer.
                    DisplayError((Window)mainWindow,
                                 "Session is no longer valid. Please copy any unsaved work, then log back in.");
            }

            // Some methods want to invalidate and then go no further, returning false.
            return false;
        }

        public static bool EditOrganisation(string id)
        {
            int idInt;
            if (!int.TryParse(id, out idInt))
            {
                DisplayError("Could not discern record ID.");
                return false;
            }

            bool couldGetOrgList = false;
            List<string> organisationList = GetOrganisationList(out couldGetOrgList).ToList();
            organisationList.Insert(0, "");
            if (!couldGetOrgList)
                return false;

            List<string?> columnNames;
            List<List<object?>> rows;
            if (App.Select("Organisation",
                           new List<string> { "*" },
                           new List<string> { Glo.Tab.ORGANISATION_ID },
                           new List<string> { id },
                           new List<Conditional> { Conditional.Equals },
                           out columnNames, out rows, true, false))
            {
                if (rows.Count > 0)
                {
                    // We would expect data for every field. If the count is different, the operation must have failed.
                    if (rows[0].Count == ColumnRecord.organisation.Count)
                    {
                        NewOrganisation org = new(idInt);
                        org.cmbOrgParentID.ItemsSource = organisationList;
                        org.PopulateExistingData(rows[0]);
                        org.Show();
                    }
                    else
                    {
                        DisplayError("Incorrect number of fields received.");
                    }
                }
                else
                    DisplayError("Could no longer retrieve record.");
            }
            return true;
        }

        public static bool EditAsset(string id)
        {
            int idInt;
            if (!int.TryParse(id, out idInt))
            {
                DisplayError("Could not discern record ID.");
                return false;
            }

            bool couldGetOrgList;
            List<string> organisationList = GetOrganisationList(out couldGetOrgList).ToList();
            organisationList.Insert(0, "");
            if (!couldGetOrgList)
                return false;

            List<string?> columnNames;
            List<List<object?>> rows;
            if (App.Select("Asset",
                           new List<string> { "*" },
                           new List<string> { Glo.Tab.ASSET_ID },
                           new List<string> { id },
                           new List<Conditional> { Conditional.Equals },
                           out columnNames, out rows, true, false))
            {
                if (rows.Count > 0)
                {
                    // We expect data for every field. If the count is different, the operation must have failed.
                    if (rows[0].Count == ColumnRecord.asset.Count)
                    {
                        NewAsset asset = new(idInt);
                        asset.cmbOrgRef.ItemsSource = organisationList;
                        asset.PopulateExistingData(rows[0]);
                        asset.Show();
                    }
                    else
                    {
                        DisplayError("Incorrect number of fields received.");
                    }
                }
                else
                    DisplayError("Could no longer retrieve record.");
            }
            return true;
        }

        public static bool EditContact(string id)
        {
            List<string?> columnNames;
            List<List<object?>> rows;
            if (App.Select("Contact",
                           new List<string> { "*" },
                           new List<string> { Glo.Tab.CONTACT_ID },
                           new List<string> { id.ToString() },
                           new List<Conditional> { Conditional.Equals },
                           out columnNames, out rows, true, false))
            {
                if (rows.Count > 0)
                {
                    // We expect data for every field. If the count is different, the operation must have failed.
                    if (rows[0].Count == ColumnRecord.contact.Count)
                    {
                        NewContact contact = new(id);
                        contact.Populate(rows[0]);
                        contact.Show();
                    }
                    else
                    {
                        DisplayError("Incorrect number of fields received.");
                    }
                }
                else
                    DisplayError("Could no longer retrieve record.");
            }
            return true;
        }

        public static bool EditConference(int id)
        {
            List<SendReceiveClasses.Conference> confs;
            if (SendConferenceSelectRequest(new() { id.ToString() }, out confs))
            {
                if (confs.Count == 0)
                {
                    DisplayError("It appears this conference no longer exists. Perhaps the archives are incomplete?");
                    return false;
                }
                else
                {
                    NewConference newConf = new(confs[0]);
                    newConf.ShowDialog();
                    return true;
                }
            }
            else
                // An error will display if SendConferenceSelectRequest() fails.
                return false;
        }

        public static bool EditResource(string id)
        {
            List<List<object?>> rows;
            if (App.Select("Resource",
                           new List<string> { "*" },
                           new List<string> { Glo.Tab.RESOURCE_ID },
                           new List<string> { id.ToString() },
                           new List<Conditional> { Conditional.Equals },
                           out _, out rows, true, false))
            {
                if (rows.Count > 0)
                {
                    // We expect data for every field. If the count is different, the operation must have failed.
                    if (rows[0].Count == ColumnRecord.resource.Count)
                    {
                        NewResource resource = new(id);
                        resource.Populate(rows[0]);
                        resource.ShowDialog();
                    }
                    else
                    {
                        DisplayError("Incorrect number of fields received.");
                    }
                }
                else
                    DisplayError("Could no longer retrieve resource.");
            }
            return true;
        }

        static bool columnRecordPulInProgress = false;
        public static bool PullColumnRecord()
        {
            if (columnRecordPulInProgress)
                Thread.Sleep(20);

            lock (ColumnRecord.lockColumnRecord)
            {
                columnRecordPulInProgress = true;

                lock (streamLock)
                {
                    NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                    try
                    {
                        if (stream != null)
                        {
                            stream.WriteByte(Glo.CLIENT_PULL_COLUMN_RECORD);
                            sr.WriteAndFlush(stream, sd.sessionID);
                            if (stream.ReadByte() == Glo.CLIENT_REQUEST_SUCCESS)
                            {
                                if (ColumnRecord.Initialise(sr.ReadString(stream)))
                                    return true;
                                else
                                    return false;
                            }
                            else
                            {
                                DisplayError("Could not pull column record.");
                                return false;
                            }
                        }
                        else
                            return false;
                    }
                    catch
                    {
                        return false;
                    }
                    finally
                    {
                        if (stream != null) stream.Close();

                        foreach (PageDatabaseView pdv in PageDatabase.views)
                            pdv.PopulateColumnComboBox();

                        columnRecordPulInProgress = false;
                    }
                }
            }
        }

        static bool resourcePullInProgress = false;
        public static bool PullResourceInformation()
        {
            if (resourcePullInProgress)
                Thread.Sleep(20);
            resourcePullInProgress = true;

            List<string?> columnNames;
            List<List<object?>> rows;
            if (SelectAll("Resource", out columnNames, out rows, false))
            {
                try
                {
                    PageConferenceView.resources.Clear();

                    int ids = columnNames.IndexOf(Glo.Tab.RESOURCE_ID);
                    int names = columnNames.IndexOf(Glo.Tab.RESOURCE_NAME);
                    int connectionCapacities = columnNames.IndexOf(Glo.Tab.RESOURCE_CAPACITY_CONNECTION);
                    int conferenceCapacities = columnNames.IndexOf(Glo.Tab.RESOURCE_CAPACITY_CONFERENCE);
                    int rowAdditional = columnNames.IndexOf(Glo.Tab.RESOURCE_ROWS_ADDITIONAL);

                    if (ids == -1 || names == -1 ||
                        conferenceCapacities == -1 || rowAdditional == -1 || connectionCapacities == -1)
                        return false;

                    foreach (List<object?> row in rows)
                    {
                        if (row[ids] != null &&
                            row[names] != null &&
                            row[connectionCapacities] != null &&
                            row[conferenceCapacities] != null &&
                            row[rowAdditional] != null)
                        {
#pragma warning disable CS8602
#pragma warning disable CS8604
#pragma warning disable CS8605
                            resources.Add((int)row[ids], new ResourceInfo((int)row[ids],
                                                         row[names].ToString(),
                                                         (int)row[connectionCapacities],
                                                         (int)row[conferenceCapacities],
                                                         (int)row[rowAdditional]));
#pragma warning restore CS8602
#pragma warning restore CS8604
#pragma warning restore CS8605
                        }
                    }
                    foreach (PageConferenceView confView in BridgeOpsClient.MainWindow.pageConferenceViews)
                    {
                        confView.resourcesOrder = resources.Select(r => r.Value.id).ToList();
                        confView.SetResources();
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
                finally
                {
                    resourcePullInProgress = false;
                }
            }
            else
            {
                resourcePullInProgress = false;
                return false;
            }
        }

        public static bool PullUserSettings()
        {
            lock (streamLock)
            {
                NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                if (stream != null)
                {
                    us.settingsPulled = true;
                    try
                    {
                        // Set defaults.
#if DEBUG
                        BridgeOpsClient.MainWindow.viewState = 1;
#else
                        BridgeOpsClient.MainWindow.viewState = 2;
#endif

                        stream.WriteByte(Glo.CLIENT_PULL_USER_SETTINGS);
                        {
                            sr.WriteAndFlush(stream, sd.sessionID);
                            string result = sr.ReadString(stream);
                            stream.Close();

                            string[] settings = result.Split("\n");

                            double dVal;
                            if (settings[0] != "")
                            {
                                try
                                {
                                    for (int i = 0; i < us.dataOrder.Length; ++i)
                                    {
                                        // Order strings.
                                        us.dataOrder[i] = settings[i].Split(';').ToList();
                                        us.dataWidths[i].Clear();
                                        us.dataHidden[i].Clear();

                                        // Hidden bools.
                                        int index = i + us.dataOrder.Length;
                                        foreach (string s in settings[index].Split(';'))
                                            us.dataHidden[i].Add(s == "True");

                                        // Width ints.
                                        index += us.dataOrder.Length;
                                        foreach (string s in settings[index].Split(';'))
                                            if (double.TryParse(s, out dVal) && dVal >= 0)
                                                us.dataWidths[i].Add(dVal);
                                    }
                                }
                                catch { }
                            }

                            // Conference view width, database view width, then view state.
                            int next = settings.Length - 2;
                            if (next > 0)
                            {
                                string[] viewSplit = settings[next].Split(';');
                                if (viewSplit.Length == 3)
                                {
                                    int iVal;
                                    if (int.TryParse(viewSplit[0], out iVal))
                                        BridgeOpsClient.MainWindow.oldConfWidth = iVal;
                                    if (int.TryParse(viewSplit[1], out iVal))
                                        BridgeOpsClient.MainWindow.oldDataWidth = iVal;
                                    if (int.TryParse(viewSplit[2], out iVal) && iVal >= 0 && iVal <= 2)
                                        BridgeOpsClient.MainWindow.viewState = iVal;
                                }
                            }

                            // Data view pane instances.
                            ++next;
                            try
                            {
                                if (BridgeOpsClient.MainWindow.pageDatabase == null)
                                    PageDatabase.storedViewSettingsToApply = settings[next];
                                else
                                    BridgeOpsClient.MainWindow.pageDatabase.ApplyViewSettings(settings[next]);

                                // Verify table settings integrity.
                                for (int i = 0; i < us.dataOrder.Length; ++i)
                                    if (us.dataOrder[i].Count != us.dataHidden[i].Count ||
                                        us.dataOrder[i].Count != us.dataWidths[i].Count)
                                        return false;
                            }
                            catch { }
                        }
                    }
                    catch
                    {
                        stream.Close();
                        return false;
                    }
                }
            }

            return true;
        }

        public static bool SendInsert(byte fncByte, object toSerialise)
        {
            return SendInsert(fncByte, toSerialise, false, false, false, out _);
        }
        public static bool SendInsert(byte fncByte, object toSerialise,
                                      bool resolveRowClashes,
                                      bool overrideDialNoClashes, bool overrideResourceOverflows)
        {
            return SendInsert(fncByte, toSerialise,
                              resolveRowClashes, overrideDialNoClashes, overrideResourceOverflows, out _);
        }

        public static bool SendInsert(byte fncByte, object toSerialise, out string returnID)
        {
            return SendInsert(fncByte, toSerialise, false, false, false, out returnID);
        }
        public static bool SendInsert(byte fncByte, object toSerialise,
                                      bool resolveRowClashes,
                                      bool overrideDialNoClashes, bool overrideResourceOverflows, out string returnID)
        {
            // Carry out soft duplicate checks if needed.
            try
            {
                string table = "";
                List<string> columns = new();
                List<string?> values = new();
                List<bool> needsQuotes = new();
                if (toSerialise is Organisation org)
                {
                    table = "Organisation";
                    columns = org.additionalCols;
                    values = org.additionalVals;
                    needsQuotes = org.additionalNeedsQuotes;
                }
                else if (toSerialise is Asset asset)
                {
                    table = "Asset";
                    columns = asset.additionalCols;
                    values = asset.additionalVals;
                    needsQuotes = asset.additionalNeedsQuotes;
                }
                else if (toSerialise is Contact contact)
                {
                    table = "Contact";
                    columns = contact.additionalCols;
                    values = contact.additionalVals;
                    needsQuotes = contact.additionalNeedsQuotes;
                }
                else if (toSerialise is SendReceiveClasses.Conference conf)
                {
                    table = "Conference";
                    columns = conf.additionalCols;
                    values = conf.additionalVals;
                    needsQuotes = conf.additionalNeedsQuotes;
                }

                if (table != "")
                {
                    List<string> columnsToSend = new();
                    List<string?> valuesToSend = new();
                    List<bool> needsQuotesToSend = new();

                    var dict = ColumnRecord.GetDictionary(table, false)!;
                    for (int i = 0; i < columns.Count; ++i)
                    {
                        ColumnRecord.Column col = ColumnRecord.GetColumn(dict, columns[i]);
                        if (col.softDuplicateCheck)
                        {
                            columnsToSend.Add(columns[i]);
                            valuesToSend.Add(values[i]);
                            needsQuotesToSend.Add(needsQuotes[i]);
                        }
                    }

                    if (columnsToSend.Count > 0 &&
                        !SoftDuplicateCheck(table, "", new(), false,
                                            columnsToSend, valuesToSend, needsQuotesToSend))
                    {
                        returnID = "";
                        return false;
                    }
                }
            }
            catch
            {
                DisplayError("Soft duplicate check could not be carried out. Cancelling save.");
                returnID = "";
                return false;
            }

            // Carry out the insert.
            lock (streamLock)
            {
                NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                try
                {
                    if (stream != null)
                    {
                        stream.WriteByte(fncByte);
                        sr.WriteAndFlush(stream, sr.Serialise(toSerialise));
                        if (fncByte == Glo.CLIENT_NEW_CONFERENCE)
                        {
                            stream.WriteByte((byte)(resolveRowClashes ? 1 : 0));
                            stream.WriteByte((byte)(overrideDialNoClashes ? 1 : 0));
                            stream.WriteByte((byte)(overrideResourceOverflows ? 1 : 0));
                        }
                        int response = stream.ReadByte();
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                        {
                            returnID = "";
                            return true;
                        }
                        else if (response == Glo.CLIENT_REQUEST_SUCCESS_MORE_TO_FOLLOW)
                        {
                            returnID = sr.ReadString(stream);
                            return true;
                        }
                        else if (response == Glo.CLIENT_SESSION_INVALID)
                        {
                            returnID = "";
                            return SessionInvalidated();
                        }
                        else if (response == Glo.CLIENT_INSUFFICIENT_PERMISSIONS)
                        {
                            DisplayError(PERMISSION_DENIED);
                        }
                        else if (response == Glo.CLIENT_REQUEST_FAILED_FOREIGN_KEY)
                        {
                            DisplayError("The foreign key could no longer be found.");
                        }
                        else if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                        {
                            string reason = sr.ReadString(stream);
                            // If there was an overridable clash, ask first and then re-run the request if the
                            // user desires.if (reason == Glo.DIAL_CLASH_WARNING)
                            if (reason == Glo.ROW_CLASH_WARNING)
                            {
                                returnID = "";
                                if (RowClashConfirm())
                                {
                                    stream.Close();
                                    return SendInsert(fncByte, toSerialise,
                                                      true, overrideDialNoClashes, overrideResourceOverflows);
                                }
                                else return false;
                            }
                            else if (reason == Glo.DIAL_CLASH_WARNING)
                            {
                                returnID = "";
                                SelectResult res = sr.Deserialise<SelectResult>(sr.ReadString(stream));
                                if (DialNoClashConfirm(res))
                                {
                                    stream.Close();
                                    return SendInsert(fncByte, toSerialise,
                                                      resolveRowClashes, true, overrideResourceOverflows);
                                }
                                else return false;
                            }
                            else if (reason == Glo.RESOURCE_OVERFLOW_WARNING)
                            {
                                returnID = "";
                                SelectResult res = sr.Deserialise<SelectResult>(sr.ReadString(stream));
                                if (ResourceOverflowConfirm(res))
                                {
                                    stream.Close();
                                    return SendInsert(fncByte, toSerialise,
                                                      resolveRowClashes, overrideDialNoClashes, true);
                                }
                                else return false;
                            }
                            else
                                DisplayError(ErrorConcat("Could not insert new record.", reason));
                        }
                    }
                    returnID = "";
                    return false;
                }
                catch
                {
                    returnID = "";
                    return false;
                }
                finally
                {
                    if (stream != null) stream.Close();
                }
            }
        }

        public static bool SendUpdate(byte fncByte, object toSerialise)
        { return SendUpdate(fncByte, toSerialise, false, false, false); }
        public static bool SendUpdate(byte fncByte, object toSerialise,
                                      bool resolveRowClashes,
                                      bool overrideDialNoClashes, bool overrideResourceOverflows)
        {
            // Carry out soft duplicate checks if needed.
            try
            {
                string table = "";
                string idColumn = "";
                string id = "";
                List<string> columns = new();
                List<string?> values = new();
                List<bool> needsQuotes = new();
                if (toSerialise is Organisation org)
                {
                    table = "Organisation";
                    idColumn = Glo.Tab.ORGANISATION_ID;
                    id = org.organisationID.ToString();
                    columns = org.additionalCols;
                    values = org.additionalVals;
                    needsQuotes = org.additionalNeedsQuotes;
                }
                else if (toSerialise is Asset asset)
                {
                    table = "Asset";
                    idColumn = Glo.Tab.ASSET_ID;
                    id = asset.assetID.ToString();
                    columns = asset.additionalCols;
                    values = asset.additionalVals;
                    needsQuotes = asset.additionalNeedsQuotes;
                }
                else if (toSerialise is Contact contact)
                {
                    table = "Contact";
                    idColumn = Glo.Tab.CONTACT_ID;
                    id = contact.contactID.ToString();
                    columns = contact.additionalCols;
                    values = contact.additionalVals;
                    needsQuotes = contact.additionalNeedsQuotes;
                }
                else if (toSerialise is SendReceiveClasses.Conference conf)
                {
                    table = "Conference";
                    idColumn = Glo.Tab.CONFERENCE_ID;
                    id = conf.conferenceID.ToString()!;
                    columns = conf.additionalCols;
                    values = conf.additionalVals;
                    needsQuotes = conf.additionalNeedsQuotes;
                }

                if (table != "")
                {
                    List<string> columnsToSend = new();
                    List<string?> valuesToSend = new();
                    List<bool> needsQuotesToSend = new();

                    var dict = ColumnRecord.GetDictionary(table, false)!;
                    for (int i = 0; i < columns.Count; ++i)
                    {
                        ColumnRecord.Column col = ColumnRecord.GetColumn(dict, columns[i]);
                        if (col.softDuplicateCheck)
                        {
                            columnsToSend.Add(columns[i]);
                            valuesToSend.Add(values[i]);
                            needsQuotesToSend.Add(needsQuotes[i]);
                        }
                    }

                    if (columnsToSend.Count > 0 &&
                        !SoftDuplicateCheck(table, idColumn, new() { id }, false,
                                            columnsToSend, valuesToSend, needsQuotesToSend))
                        return false;
                }
            }
            catch
            {
                DisplayError("Soft duplicate check could not be carried out. Cancelling save.");
                return false;
            }

            // Carry out the update.
            lock (streamLock)
            {
                NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                try
                {
                    if (stream != null)
                    {
                        stream.WriteByte(fncByte);
                        sr.WriteAndFlush(stream, sr.Serialise(toSerialise));
                        if (fncByte == Glo.CLIENT_UPDATE_CONFERENCE)
                        {
                            stream.WriteByte((byte)(resolveRowClashes ? 1 : 0));
                            stream.WriteByte((byte)(overrideDialNoClashes ? 1 : 0));
                            stream.WriteByte((byte)(overrideResourceOverflows ? 1 : 0));
                        }
                        int response = stream.ReadByte();
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                            return true;
                        else if (response == Glo.CLIENT_SESSION_INVALID)
                        {
                            return SessionInvalidated();
                        }
                        else if (response == Glo.CLIENT_INSUFFICIENT_PERMISSIONS)
                        {
                            DisplayError(PERMISSION_DENIED);
                        }
                        else if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                        {
                            string reason = sr.ReadString(stream);

                            // If there was an overridable clash, ask first and then re-run the request if the
                            // user desires.
                            if (reason == Glo.ROW_CLASH_WARNING)
                            {
                                if (RowClashConfirm())
                                {
                                    stream.Close();
                                    return SendUpdate(fncByte, toSerialise,
                                                      true, overrideDialNoClashes, overrideResourceOverflows);
                                }
                                else return false;
                            }
                            else if (reason == Glo.DIAL_CLASH_WARNING)
                            {
                                SelectResult res = sr.Deserialise<SelectResult>(sr.ReadString(stream));
                                if (DialNoClashConfirm(res))
                                {
                                    stream.Close();
                                    return SendUpdate(fncByte, toSerialise,
                                                      resolveRowClashes, true, overrideResourceOverflows);
                                }
                                else return false;
                            }
                            else if (reason == Glo.RESOURCE_OVERFLOW_WARNING)
                            {
                                SelectResult res = sr.Deserialise<SelectResult>(sr.ReadString(stream));
                                if (ResourceOverflowConfirm(res))
                                {
                                    stream.Close();
                                    return SendUpdate(fncByte, toSerialise,
                                                      resolveRowClashes, overrideDialNoClashes, true);
                                }
                                else return false;
                            }
                            else
                                DisplayError(ErrorConcat("Could not update record.", reason));
                            return false;
                        }
                    }
                    return false;
                }
                catch
                {
                    DisplayError("Could not run table update.");
                    return false;
                }
                finally
                {
                    if (stream != null) stream.Close();
                }
            }
        }

        public static bool SendUpdate(UpdateRequest req)
        { return SendUpdate(req, false, false, false); }
        public static bool SendUpdate(UpdateRequest req,
                                      // Only in use for Conference updates:
                                      bool resolveRowClashes,
                                      bool overrideDialNoClashes, bool overrideResourceOverflows)
        {
            // Carry out soft duplicate checks if needed.
            try
            {
                List<string> tablesToSend = new();
                List<string> columnsToSend = new();
                List<string?> valuesToSend = new();
                List<bool> needsQuotesToSend = new();

                var dict = ColumnRecord.GetDictionary(req.table, false)!;
                for (int i = 0; i < req.columns.Count; ++i)
                {
                    CR.Column col = ColumnRecord.GetColumn(dict, req.columns[i]);
                    if (col.softDuplicateCheck)
                    {
                        tablesToSend.Add(req.table);
                        columnsToSend.Add(req.columns[i]);
                        valuesToSend.Add(req.values[i]);
                        needsQuotesToSend.Add(req.columnsNeedQuotes[i]);
                    }
                }

                // If the number of IDs is > 1, then obviously it will introduce duplicates, no need to trouble
                // the server.
                if (tablesToSend.Count > 0)
                {
                    if (req.ids.Count > 1)
                    {
                        StringBuilder message = new("The chosen values are already in use in the following " +
                                                    "columns:\n");
                        foreach (string s in columnsToSend)
                        {
                            message.Append($"\n• {CR.GetPrintName(s, (CR.Column)dict![s]!)}");
                        }
                        message.Append("\n\nAre you sure you wish to proceed?");
                        if (!DisplayQuestion(message.ToString(), "Soft Duplicate Check",
                                             DialogWindows.DialogBox.Buttons.YesNo))
                            return false;
                    }
                    else if (!SoftDuplicateCheck(req.table, req.idColumn, req.ids.Cast<string?>().ToList(),
                                                 req.idQuotes, columnsToSend, valuesToSend, needsQuotesToSend))
                        return false;
                }
            }
            catch
            {
                DisplayError("Soft duplicate check could not be carried out. Cancelling update.");
                return false;
            }

            // Carry out the update.
            lock (streamLock)
            {
                NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                try
                {
                    if (stream != null)
                    {
                        stream.WriteByte(Glo.CLIENT_UPDATE);
                        sr.WriteAndFlush(stream, sr.Serialise(req));
                        if (req.table == "Conference")
                        {
                            stream.WriteByte((byte)(resolveRowClashes ? 1 : 0));
                            stream.WriteByte((byte)(overrideDialNoClashes ? 1 : 0));
                            stream.WriteByte((byte)(overrideResourceOverflows ? 1 : 0));
                        }
                        int response = stream.ReadByte();
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                            return true;
                        else if (response == Glo.CLIENT_SESSION_INVALID)
                        {
                            return SessionInvalidated();
                        }
                        else if (response == Glo.CLIENT_INSUFFICIENT_PERMISSIONS)
                        {
                            DisplayError(PERMISSION_DENIED);
                        }
                        else if (response == Glo.CLIENT_REQUEST_FAILED_RECORD_DELETED)
                        {
                            DisplayError("The record could no longer be found.");
                        }
                        else if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                        {
                            string reason = sr.ReadString(stream);

                            // If there was an overridable clash, ask first and then re-run the request if the
                            // user desires.
                            if (reason == Glo.ROW_CLASH_WARNING)
                            {
                                if (RowClashConfirm())
                                {
                                    stream.Close();
                                    return SendUpdate(req, true, overrideDialNoClashes, overrideResourceOverflows);
                                }
                                else return false;
                            }
                            else if (reason == Glo.DIAL_CLASH_WARNING)
                            {
                                SelectResult res = sr.Deserialise<SelectResult>(sr.ReadString(stream));
                                if (DialNoClashConfirm(res))
                                {
                                    stream.Close();
                                    return SendUpdate(req, resolveRowClashes, true, overrideResourceOverflows);
                                }
                                else return false;
                            }
                            else if (reason == Glo.RESOURCE_OVERFLOW_WARNING)
                            {
                                SelectResult res = sr.Deserialise<SelectResult>(sr.ReadString(stream));
                                if (ResourceOverflowConfirm(res))
                                {
                                    stream.Close();
                                    return SendUpdate(req, resolveRowClashes, overrideDialNoClashes, true);
                                }
                                else return false;
                            }
                            else
                                DisplayError(ErrorConcat("Could not update record.", reason));
                            return false;
                        }
                        else throw new Exception();
                    }
                    return false;
                }
                catch
                {
                    DisplayError("Could not update record.");
                    return false;
                }
                finally
                {
                    if (stream != null) stream.Close();
                }
            }
        }

        public static bool SendDelete(string table, string column, string id, bool isString)
        { return SendDelete(table, column, new List<string>() { id }, isString); }
        public static bool SendDelete(string table, string column, List<string> ids, bool isString)
        {
            lock (streamLock)
            {
                NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                try
                {
                    if (stream != null)
                    {
                        DeleteRequest req = new(sd.sessionID, ColumnRecord.columnRecordID, sd.loginID,
                                                table, column, ids, isString);

                        stream.WriteByte(Glo.CLIENT_DELETE);
                        sr.WriteAndFlush(stream, sr.Serialise(req));
                        int response = stream.ReadByte();
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                            return true;
                        else if (response == Glo.CLIENT_SESSION_INVALID)
                        {
                            return SessionInvalidated();
                        }
                        else if (response == Glo.CLIENT_INSUFFICIENT_PERMISSIONS)
                        {
                            DisplayError(PERMISSION_DENIED);
                        }
                        else if (response == Glo.CLIENT_REQUEST_FAILED_RECORD_DELETED)
                        {
                            DisplayError("The record could no longer be found.");
                        }
                        else if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                        {
                            string error = sr.ReadString(stream);
                            if (error.Contains("fk_ConfRecurrence"))
                                DisplayError("Can not delete a recurrence with conferences still attached.");
                            else if (error.Contains("fk_ConfResource"))
                                DisplayError("Can not delete a resource that currently holds conferences.");
                            else
                                DisplayError(ErrorConcat("Could not delete record.", sr.ReadString(stream)));
                        }
                        else throw new Exception();
                    }
                    return false;
                }
                catch
                {
                    DisplayError("Could not delete record.");
                    return false;
                }
                finally
                {
                    if (stream != null) stream.Close();
                }
            }
        }

        public static bool SoftDuplicateCheck(string table, string idColumn, List<string?> ids, bool idNeedsQuotes,
                                              List<string> columns, List<string?> values, List<bool> needsQuotes)
        {
            lock (streamLock)
            {
                NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                try
                {
                    if (stream != null)
                    {
                        ExistenceCheck check = new(sd.sessionID, CR.columnRecordID,
                                                   table, idColumn, ids, idNeedsQuotes, columns, values, needsQuotes);
                        stream.WriteByte(Glo.CLIENT_SELECT_EXISTS);
                        sr.WriteAndFlush(stream, sr.Serialise(check));
                        int response = stream.ReadByte();
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                            return true;
                        else if (response == Glo.CLIENT_REQUEST_SUCCESS_MORE_TO_FOLLOW)
                        {
                            List<string> columnsAffected = sr.Deserialise<List<string>>(sr.ReadString(stream))!;
                            StringBuilder message = new("The chosen values are already in use in the following " +
                                                        "columns:\n");
                            foreach (string s in columnsAffected)
                            {
                                var dict = CR.GetDictionary(table, false);
                                message.Append($"\n• {CR.GetPrintName(s, (CR.Column)dict![s]!)}");
                            }
                            message.Append("\n\nAre you sure you wish to proceed?");
                            return DisplayQuestion(message.ToString(), "Soft Duplicate Check",
                                                   DialogWindows.DialogBox.Buttons.YesNo);
                        }
                        else if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                        {
                            DisplayError("The SQL query could not be run. See error:\n\n" +
                                            sr.ReadString(stream));
                            return false;
                        }
                        else if (response == Glo.CLIENT_SESSION_INVALID)
                            SessionInvalidated();
                        return false;
                    }
                    return false;
                }
                catch
                {
                    DisplayError("Could not run soft duplicate check.");
                    return false;
                }
                finally
                {
                    if (stream != null) stream.Close();
                }
            }
        }

        // Returns null if the operation failed, returns an array if successful, empty or otherwise.
        public static string[]? SelectColumnPrimary(string table, string column, out bool successful)
        {
            lock (streamLock)
            {
                NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                try
                {
                    if (stream != null)
                    {
                        PrimaryColumnSelect pcs = new(sd.sessionID, ColumnRecord.columnRecordID,
                                                      table, column);
                        stream.WriteByte(Glo.CLIENT_SELECT_COLUMN_PRIMARY);
                        sr.WriteAndFlush(stream, sr.Serialise(pcs));
                        int response = stream.ReadByte();
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                        {
                            successful = true;
                            return sr.ReadString(stream).Split(';');
                        }
                        else if (response == Glo.CLIENT_SESSION_INVALID)
                            SessionInvalidated();
                        successful = false;
                        return null;
                    }
                    successful = false;
                    return null;
                }
                catch
                {
                    DisplayError("Could not run or return query.");
                    successful = false;
                    return null;
                }
                finally
                {
                    if (stream != null) stream.Close();
                }
            }
        }

        public static bool SelectAll(string table, out List<string?> columnNames, out List<List<object?>> rows,
                                     bool historical)
        {
            return Select(table, new List<string> { "*" }, new(), new(), new(), out columnNames, out rows,
                          true, historical);
        }
        public static bool SelectAll(string table, string likeColumn, string likeValue, Conditional conditional,
                                     out List<string?> columnNames, out List<List<object?>> rows, bool historical)
        {
            return Select(table, new List<string> { "*" },
                          new List<string> { likeColumn }, new List<string> { likeValue },
                          new List<Conditional> { conditional },
                          out columnNames, out rows, true, historical);
        }
        public static bool Select(string table, List<string> select,
                                  out List<string?> columnNames, out List<List<object?>> rows, bool historical)
        {
            return Select(table, select, new(), new(), new(), out columnNames, out rows, true, historical);
        }
        public static bool Select(string table, List<string> select,
                                  List<string> likeColumns, List<string> likeValues, List<Conditional> conditionals,
                                  out List<string?> columnNames, out List<List<object?>> rows,
                                  bool and, bool historical)
        {
            // For the Organisation, Asset, Conference and Contact tables that allow different column orders,
            // the columns must be stated in the correct order when populating the DataInputTable.
            if (select.Count == 0 || (select.Count == 1 && select[0] == "*"))
            {
                var dictionary = ColumnRecord.GetDictionary(table, true);
                if (dictionary != null)
                {
                    select.Clear();
                    foreach (DictionaryEntry de in dictionary)
                        select.Add((string)de.Key);
                }
            }

            lock (streamLock)
            {
                using NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                {
                    try
                    {
                        if (stream != null)
                        {
                            QuickSelectRequest req = new(sd.sessionID, ColumnRecord.columnRecordID,
                                                        table, select, likeColumns, likeValues, conditionals,
                                                        and, historical);
                            stream.WriteByte(Glo.CLIENT_SELECT_QUICK);
                            sr.WriteAndFlush(stream, sr.Serialise(req));
                            int response = stream.ReadByte();
                            if (response == Glo.CLIENT_REQUEST_SUCCESS)
                            {
                                SelectResult result = sr.Deserialise<SelectResult>(sr.ReadString(stream));
                                columnNames = result.columnNames;
                                rows = result.rows;
                                ConvertUnknownJsonObjectsToRespectiveTypes(result.columnTypes, rows);
                                return true;
                            }
                            else if (response == Glo.CLIENT_SESSION_INVALID)
                            {
                                columnNames = new();
                                rows = new();
                                return SessionInvalidated();
                            }
                            throw new Exception();
                        }
                        throw new Exception();
                    }
                    catch
                    {
                        DisplayError("Could not run or return query.");
                        columnNames = new();
                        rows = new();
                        return false;
                    }
                    finally
                    {
                        if (stream != null) stream.Close();
                    }
                }
            }
        }
        public static bool SendSelectRequest(SelectRequest req,
                                             out List<string?> columnNames, out List<List<object?>> rows)
        {
            lock (streamLock)
            {
                using NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                {
                    try
                    {
                        if (stream != null)
                        {
                            stream.WriteByte(Glo.CLIENT_SELECT);
                            sr.WriteAndFlush(stream, sr.Serialise(req));
                            int response = stream.ReadByte();
                            if (response == Glo.CLIENT_REQUEST_SUCCESS)
                            {
                                SelectResult result = sr.Deserialise<SelectResult>(sr.ReadString(stream));
                                columnNames = result.columnNames;
                                rows = result.rows;
                                ConvertUnknownJsonObjectsToRespectiveTypes(result.columnTypes, rows);
                                return true;
                            }
                            columnNames = new();
                            rows = new();
                            if (response == Glo.CLIENT_SESSION_INVALID)
                            {
                                return SessionInvalidated();
                            }
                            else if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                            {
                                DisplayError("The SQL query could not be run. See error:\n\n" +
                                             sr.ReadString(stream));
                                columnNames = new();
                                rows = new();
                                return false;

                            }
                        }
                        throw new Exception();
                    }
                    catch
                    {
                        DisplayError("Could not run or return query.");
                        columnNames = new();
                        rows = new();
                        return false;
                    }
                    finally
                    {
                        if (stream != null) stream.Close();
                    }
                }
            }
        }
        public static bool SendSelectStatement(string statement,
                                               out List<string?> columnNames, out List<List<object?>> rows)
        {
            lock (streamLock)
            {
                using NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                {
                    try
                    {
                        if (stream != null)
                        {
                            stream.WriteByte(Glo.CLIENT_SELECT_STATEMENT);
                            sr.WriteAndFlush(stream, App.sd.sessionID);
                            sr.WriteAndFlush(stream, ColumnRecord.columnRecordID.ToString());
                            sr.WriteAndFlush(stream, statement);
                            int response = stream.ReadByte();
                            if (response == Glo.CLIENT_REQUEST_SUCCESS)
                            {
                                SelectResult result = sr.Deserialise<SelectResult>(sr.ReadString(stream));
                                columnNames = result.columnNames;
                                rows = result.rows;
                                ConvertUnknownJsonObjectsToRespectiveTypes(result.columnTypes, rows);
                                return true;
                            }
                            columnNames = new();
                            rows = new();
                            if (response == Glo.CLIENT_SESSION_INVALID)
                            {
                                return SessionInvalidated();
                            }
                            else if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                            {
                                DisplayError("The SQL query could not be run. See error:\n\n" +
                                             sr.ReadString(stream));
                                columnNames = new();
                                rows = new();
                                return false;

                            }
                        }
                        throw new Exception();
                    }
                    catch
                    {
                        DisplayError("Could not run or return query.");
                        columnNames = new();
                        rows = new();
                        return false;
                    }
                    finally
                    {
                        if (stream != null) stream.Close();
                    }
                }
            }
        }

        public static bool SelectWide(string table, string value,
                                      out List<string?> columnNames, out List<List<object?>> rows,
                                      bool historical)
        {
            // For the Organisation, Asset, Conference and Contact tables that allow different column orders,
            // the columns must be stated in the correct order when populating the DataInputTable.
            var dictionary = ColumnRecord.GetDictionary(table, true);
            List<string> select = new();
            if (dictionary != null)
                foreach (DictionaryEntry de in dictionary)
                    select.Add((string)de.Key);

            lock (streamLock)
            {
                using NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                {
                    try
                    {
                        if (stream != null)
                        {
                            SelectWideRequest req = new(sd.sessionID, ColumnRecord.columnRecordID,
                                                        dictionary == null ? new() { "*" } : select, table, value,
                                                        historical);
                            stream.WriteByte(Glo.CLIENT_SELECT_WIDE);
                            sr.WriteAndFlush(stream, sr.Serialise(req));
                            int response = stream.ReadByte();
                            if (response == Glo.CLIENT_REQUEST_SUCCESS)
                            {
                                SelectResult result = sr.Deserialise<SelectResult>(sr.ReadString(stream));
                                columnNames = result.columnNames;
                                rows = result.rows;
                                ConvertUnknownJsonObjectsToRespectiveTypes(result.columnTypes, rows);
                                return true;
                            }
                            else if (response == Glo.CLIENT_SESSION_INVALID)
                            {
                                columnNames = new();
                                rows = new();
                                return SessionInvalidated();
                            }
                            throw new Exception();
                        }
                        throw new Exception();
                    }
                    catch
                    {
                        DisplayError("Could not run or return query.");
                        columnNames = new();
                        rows = new();
                        return false;
                    }
                    finally
                    {
                        if (stream != null) stream.Close();
                    }
                }
            }
        }

        public static bool LinkContact(string organisationRef, int contactID, bool unlink)
        {
            lock (streamLock)
            {
                NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                try
                {
                    if (stream != null)
                    {
                        LinkContactRequest req = new(sd.sessionID, ColumnRecord.columnRecordID,
                                                     organisationRef, contactID, unlink);

                        stream.WriteByte(Glo.CLIENT_LINK_CONTACT);
                        sr.WriteAndFlush(stream, sr.Serialise(req));
                        int response = stream.ReadByte();
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                            return true;
                        else if (response == Glo.CLIENT_SESSION_INVALID)
                            return SessionInvalidated();
                        else if (response == Glo.CLIENT_INSUFFICIENT_PERMISSIONS)
                            DisplayError(PERMISSION_DENIED);
                        else if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                            DisplayError(sr.ReadString(stream));
                        else if (response == Glo.CLIENT_REQUEST_FAILED_RECORD_DELETED)
                            DisplayError("The record could no longer be found.");
                    }
                    return false;
                }
                catch
                {
                    DisplayError("Could not link or unlink contact to organisation.");
                    return false;
                }
                finally
                {
                    if (stream != null) stream.Close();
                }
            }
        }

        public static bool LinkedContactSelect(string organisationID,
                                               out List<string?> columnNames, out List<List<object?>> rows)
        {
            lock (streamLock)
            {
                NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                try
                {
                    if (stream != null)
                    {
                        LinkedContactSelectRequest req = new(sd.sessionID, ColumnRecord.columnRecordID,
                                                             organisationID);
                        stream.WriteByte(Glo.CLIENT_LINKED_CONTACT_SELECT);
                        sr.WriteAndFlush(stream, sr.Serialise(req));
                        int response = stream.ReadByte();
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                        {
                            SelectResult result = sr.Deserialise<SelectResult>(sr.ReadString(stream));
                            columnNames = result.columnNames;
                            rows = result.rows;
                            ConvertUnknownJsonObjectsToRespectiveTypes(result.columnTypes, rows);
                            return true;
                        }
                        else if (response == Glo.CLIENT_SESSION_INVALID)
                        {
                            columnNames = new();
                            rows = new();
                            return SessionInvalidated();
                        }
                        throw new Exception();
                    }
                    throw new Exception();
                }
                catch
                {
                    DisplayError("Could not run or return query.");
                    columnNames = new();
                    rows = new();
                    return false;
                }
                finally
                {
                    if (stream != null) stream.Close();
                }
            }
        }

        public static bool SelectHistory(string table, string id,
                                         out List<string?> columnNames, out List<List<object?>> rows)
        {
            int idInt;
            if (!int.TryParse(id, out idInt))
            {
                DisplayError("Could not run or return history list. ID doesn't appear to be an integer.");
                columnNames = new();
                rows = new();
                return false;
            }

            lock (streamLock)
            {
                NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                try
                {
                    if (stream != null)
                    {
                        SelectHistoryRequest req = new(sd.sessionID, ColumnRecord.columnRecordID, table, idInt);
                        stream.WriteByte(Glo.CLIENT_SELECT_HISTORY);
                        sr.WriteAndFlush(stream, sr.Serialise(req));
                        int response = stream.ReadByte();
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                        {
                            SelectResult result = sr.Deserialise<SelectResult>(sr.ReadString(stream));
                            columnNames = result.columnNames;
                            rows = result.rows;
                            ConvertUnknownJsonObjectsToRespectiveTypes(result.columnTypes, rows);
                            return true;
                        }
                        else if (response == Glo.CLIENT_SESSION_INVALID)
                        {
                            columnNames = new();
                            rows = new();
                            return SessionInvalidated();
                        }
                        throw new Exception();
                    }
                    throw new Exception();
                }
                catch
                {
                    DisplayError("Could not run or return history list.");
                    columnNames = new();
                    rows = new();
                    return false;
                }
                finally
                {
                    if (stream != null) stream.Close();
                }
            }
        }

        public static bool BuildHistorical(string table, string changeID, int recordID, out List<object?> data)
        {
            lock (streamLock)
            {
                NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                try
                {
                    if (stream != null)
                    {
                        SelectHistoricalRecordRequest req = new(sd.sessionID, ColumnRecord.columnRecordID,
                                                                table, changeID, recordID);
                        stream.WriteByte(Glo.CLIENT_SELECT_HISTORICAL_RECORD);
                        sr.WriteAndFlush(stream, sr.Serialise(req));
                        int response = stream.ReadByte();
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                        {
                            SelectResult result = sr.Deserialise<SelectResult>(sr.ReadString(stream));
                            if (result.rows.Count > 0 &&
                                (table == "Organisation" && result.rows[0].Count == ColumnRecord.organisation.Count) ||
                                (table == "Asset" && result.rows[0].Count == ColumnRecord.asset.Count))
                            {
                                data = result.rows[0];
                                ConvertUnknownJsonObjectsToRespectiveTypes(result.columnTypes, result.rows);
                                // Historical records are delivered in the database column order, so the data has to be
                                // be arranged in a way the calling Window expects it.

                                List<int> order = table == "Organisation" ? ColumnRecord.organisationOrder :
                                                                            ColumnRecord.assetOrder;

                                object?[] orderedArray = new object?[result.rows[0].Count];

                                int i = 0;
                                foreach (object? o in result.rows[0])
                                {
                                    for (int n = 0; n < orderedArray.Length; ++n)
                                        if (order[n] == i)
                                        {
                                            orderedArray[n] = o;
                                            break;
                                        }
                                    ++i;
                                }

                                data = new();

                                foreach (object? o in orderedArray)
                                    data.Add(o);

                                return true;
                            }
                            else
                            {
                                throw new Exception();
                            }
                        }
                        else if (response == Glo.CLIENT_SESSION_INVALID)
                        {
                            data = new();
                            return SessionInvalidated();
                        }
                        throw new Exception();
                    }
                    throw new Exception();
                }
                catch
                {
                    DisplayError("Could not run or return historical record.");
                    data = new();
                    return false;
                }
                finally
                {
                    if (stream != null) stream.Close();
                }
            }
        }

        public static bool SendConferenceViewSearchRequest(DateTime start, DateTime end,
                                                           Dictionary<int, PageConferenceView.Conference> confs)
        {
            lock (streamLock)
            {
                using NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                {
                    try
                    {
                        if (stream != null)
                        {
                            stream.WriteByte(Glo.CLIENT_CONFERENCE_VIEW_SEARCH);
                            sr.WriteAndFlush(stream, sd.sessionID);
                            sr.WriteAndFlush(stream, sr.Serialise(start));
                            sr.WriteAndFlush(stream, sr.Serialise(end));
                            int response = stream.ReadByte();
                            if (response == Glo.CLIENT_REQUEST_SUCCESS)
                            {
                                SelectResult result = sr.Deserialise<SelectResult>(sr.ReadString(stream));
                                ConvertUnknownJsonObjectsToRespectiveTypes(result.columnTypes, result.rows);
                                confs.Clear();
                                PageConferenceView.Conference? c = null;
                                foreach (List<object?> row in result.rows)
                                {
                                    if (c == null || (int)row[0]! != c.id)
                                    {
                                        c = new();
                                        c.id = (int)row[0]!;
                                        confs.Add(c.id, c);
                                        c.title = (string)row[1]!;
                                        c.start = (DateTime)row[2]!;
                                        c.end = (DateTime)row[3]!;
                                        c.resourceID = (int)row[4]!;
                                        c.resourceRow = (int)row[5]!;
                                        c.recurrenceID = (int?)row[6]!;
                                        c.recurrenceName = (string?)row[7];
                                        c.cancelled = (bool)row[8]!;
                                        c.closure = (string)row[9]!;
                                    }
                                    if (row[10] != null)
                                    {
                                        c.dialNos.Add((string)row[10]!);
                                        if ((bool?)row[11] == true)
                                            c.test = true;
                                        if ((bool?)row[12] == false)
                                            c.hasUnclosedConnection = true;
                                    }
                                }
                                return true;
                            }
                            if (response == Glo.CLIENT_SESSION_INVALID)
                            {
                                return SessionInvalidated();
                            }
                            else if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                            {
                                DisplayError("The SQL query could not be run. See error:\n\n" +
                                                sr.ReadString(stream));
                                return false;
                            }
                        }
                        throw new Exception();
                    }
                    catch
                    {
                        DisplayError("Could not run or return query.");
                        return false;
                    }
                    finally
                    {
                        if (stream != null) stream.Close();
                    }
                }
            }
        }

        public static bool SendConferenceQuickMoveRequest(bool duplicate, List<int> conferenceIDs,
                                                          List<DateTime> starts, List<DateTime> ends,
                                                          List<int> resourceIDs, List<int> resourceRows,
                                                          bool resolveRowClashes,
                                                          bool overrideDialNoClashes,
                                                          bool overrideResourceOverflows)
        {
            lock (streamLock)
            {
                using NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                {
                    try
                    {
                        if (stream != null)
                        {
                            string json = sr.Serialise(conferenceIDs);
                            stream.WriteByte(Glo.CLIENT_CONFERENCE_QUICK_MOVE);
                            stream.WriteByte((byte)(duplicate ? 1 : 0));
                            sr.WriteAndFlush(stream, sd.sessionID);
                            sr.WriteAndFlush(stream, sr.Serialise(conferenceIDs));
                            sr.WriteAndFlush(stream, sr.Serialise(starts));
                            sr.WriteAndFlush(stream, sr.Serialise(ends));
                            sr.WriteAndFlush(stream, sr.Serialise(resourceIDs));
                            sr.WriteAndFlush(stream, sr.Serialise(resourceRows));
                            stream.WriteByte((byte)(resolveRowClashes ? 1 : 0));
                            stream.WriteByte((byte)(overrideDialNoClashes ? 1 : 0));
                            stream.WriteByte((byte)(overrideResourceOverflows ? 1 : 0));
                            int response = stream.ReadByte();
                            if (response == Glo.CLIENT_REQUEST_SUCCESS)
                                return true;
                            if (response == Glo.CLIENT_SESSION_INVALID)
                                return SessionInvalidated();
                            else if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                            {
                                string reason = sr.ReadString(stream);

                                // If there was an overridable clash, ask first and then re-run the request if the
                                // user desires.
                                if (reason == Glo.ROW_CLASH_WARNING)
                                {
                                    if (RowClashConfirm())
                                    {
                                        stream.Close();
                                        return SendConferenceQuickMoveRequest(duplicate, conferenceIDs,
                                                                              starts, ends, resourceIDs, resourceRows,
                                                                              true, overrideDialNoClashes,
                                                                              overrideResourceOverflows);
                                    }
                                    else return false;
                                }
                                else if (reason == Glo.DIAL_CLASH_WARNING)
                                {
                                    SelectResult res = sr.Deserialise<SelectResult>(sr.ReadString(stream));
                                    if (DialNoClashConfirm(res))
                                    {
                                        stream.Close();
                                        return SendConferenceQuickMoveRequest(duplicate, conferenceIDs,
                                                                              starts, ends, resourceIDs, resourceRows,
                                                                              resolveRowClashes, true,
                                                                              overrideResourceOverflows);
                                    }
                                    else return false;
                                }
                                else if (reason == Glo.RESOURCE_OVERFLOW_WARNING)
                                {
                                    SelectResult res = sr.Deserialise<SelectResult>(sr.ReadString(stream));
                                    if (ResourceOverflowConfirm(res))
                                    {
                                        stream.Close();
                                        return SendConferenceQuickMoveRequest(duplicate, conferenceIDs,
                                                                              starts, ends, resourceIDs, resourceRows,
                                                                              resolveRowClashes, overrideDialNoClashes,
                                                                              true);
                                    }
                                    else return false;
                                }
                                else
                                    DisplayError("Could not carry out move. See error:\n\n" + reason);
                                return false;
                            }
                        }
                        throw new Exception();
                    }
                    catch
                    {
                        DisplayError("Could not move conference.");
                        return false;
                    }
                    finally
                    {
                        if (stream != null) stream.Close();
                    }
                }
            }
        }

        public static bool SendConferenceSelectRequest(List<string> conferenceIDs,
                                                       out List<SendReceiveClasses.Conference> confs)
        {
            if (conferenceIDs.Count == 0)
            {
                confs = new();
                return true;
            }

            lock (streamLock)
            {
                using NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                {
                    try
                    {
                        if (stream != null)
                        {
                            stream.WriteByte(Glo.CLIENT_CONFERENCE_SELECT);
                            sr.WriteAndFlush(stream, sd.sessionID);
                            sr.WriteAndFlush(stream, ColumnRecord.columnRecordID.ToString());
                            sr.WriteAndFlush(stream, sr.Serialise(conferenceIDs));
                            int response = stream.ReadByte();
                            if (response == Glo.CLIENT_REQUEST_SUCCESS)
                            {
                                confs = sr.Deserialise<List<SendReceiveClasses.Conference>>(sr.ReadString(stream))!;
                                foreach (SendReceiveClasses.Conference conf in confs)
                                    ConvertUnknownJsonObjectsToRespectiveTypes(conf.additionalValTypes,
                                                                               new() { conf.additionalValObjects });
                                return true;
                            }
                            if (response == Glo.CLIENT_SESSION_INVALID)
                            {
                                confs = new();
                                return SessionInvalidated();
                            }
                            else if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                            {
                                DisplayError("The conferences could not be selected. See error:\n\n" +
                                                sr.ReadString(stream));
                                confs = new();
                                return false;
                            }
                        }
                        throw new Exception();
                    }
                    catch
                    {
                        DisplayError("Could not run or return conference query.");
                        confs = new();
                        return false;
                    }
                    finally
                    {
                        if (stream != null) stream.Close();
                    }
                }
            }
        }

        public static bool SendConferenceAdjustment(ConferenceAdjustment req)
        {
            req.sessionID = sd.sessionID;
            req.columnRecordID = ColumnRecord.columnRecordID;
            lock (streamLock)
            {
                using NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                {
                    try
                    {
                        if (stream != null)
                        {
                            stream.WriteByte(Glo.CLIENT_CONFERENCE_ADJUST);
                            sr.WriteAndFlush(stream, sr.Serialise(req));
                            int response = stream.ReadByte();
                            if (response == Glo.CLIENT_REQUEST_SUCCESS)
                            {
                                return true;
                            }
                            if (response == Glo.CLIENT_SESSION_INVALID)
                            {
                                return SessionInvalidated();
                            }
                            else if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                            {
                                string reason = sr.ReadString(stream);

                                // If there was an overridable clash, ask first and then re-run the request if the
                                // user desires.
                                if (reason == Glo.DIAL_CLASH_WARNING)
                                {
                                    SelectResult res = sr.Deserialise<SelectResult>(sr.ReadString(stream));
                                    if (DialNoClashConfirm(res))
                                    {
                                        stream.Close();
                                        req.overrideDialNoClashes = true;
                                        return SendConferenceAdjustment(req);
                                    }
                                    else return false;
                                }
                                else if (reason == Glo.RESOURCE_OVERFLOW_WARNING)
                                {
                                    SelectResult res = sr.Deserialise<SelectResult>(sr.ReadString(stream));
                                    if (ResourceOverflowConfirm(res))
                                    {
                                        stream.Close();
                                        req.overrideResourceOverflows = true;
                                        return SendConferenceAdjustment(req);
                                    }
                                    else return false;
                                }
                                else
                                    DisplayError(ErrorConcat("Could not update record.", reason));
                            }
                        }
                        throw new Exception();
                    }
                    catch
                    {
                        DisplayError("Could not run conference adjustment.");
                        return false;
                    }
                    finally
                    {
                        if (stream != null) stream.Close();
                    }
                }
            }
        }

        public static bool SendConnectionSelectRequest(List<string> conferenceIDs, out SelectResult res)
        {
            lock (streamLock)
            {
                using NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                {
                    if (stream == null)
                        throw new();
                    try
                    {
                        stream.WriteByte(Glo.CLIENT_CONFERENCE_SELECT_CONNECTIONS);
                        sr.WriteAndFlush(stream, sd.sessionID);
                        sr.WriteAndFlush(stream, sr.Serialise(conferenceIDs));
                        int response = stream.ReadByte();
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                        {
                            res = sr.Deserialise<SelectResult>(sr.ReadString(stream));
                            ConvertUnknownJsonObjectsToRespectiveTypes(res.columnTypes, res.rows);
                            return true;
                        }
                        if (response == Glo.CLIENT_SESSION_INVALID)
                            SessionInvalidated();
                        else if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                        {
                            DisplayError(ErrorConcat("Could not select connection list.", sr.ReadString(stream)));
                            res = new();
                            return false;
                        }
                        throw new Exception();
                    }
                    catch
                    {
                        DisplayError("Could not select connection list.");
                        res = new();
                        return false;
                    }
                    finally
                    {
                        if (stream != null) stream.Close();
                    }
                }
            }
        }

        enum JsonTypes { Number, DateTime, TimeSpan, Boolean, String, Unknown }
        static private void ConvertUnknownJsonObjectsToRespectiveTypes(List<string?> columnTypes,
                                                                       List<List<object?>> rows)
        {
            JsonTypes[] types = new JsonTypes[columnTypes.Count];

            for (int i = 0; i < columnTypes.Count; ++i)
            {
                if (columnTypes[i]!.Contains("Int") || columnTypes[i] == "Byte")
                    types[i] = JsonTypes.Number;
                else if (columnTypes[i] == "DateTime")
                    types[i] = JsonTypes.DateTime;
                else if (columnTypes[i] == "TimeSpan")
                    types[i] = JsonTypes.TimeSpan;
                else if (columnTypes[i] == "Boolean")
                    types[i] = JsonTypes.Boolean;
                else if (columnTypes[i] == "String")
                    types[i] = JsonTypes.String;
                else
                    types[i] = JsonTypes.Unknown;
            }

            for (int n = 0; n < rows.Count; ++n)
            {
#pragma warning disable CS8600
#pragma warning disable CS8602
                for (int i = 0; i < columnTypes.Count; ++i)
                {
                    if (rows[n][i] == null)
                        continue;

                    switch (types[i])
                    {
                        case JsonTypes.Number:
                            int resultI;
                            int.TryParse(rows[n][i].ToString(), out resultI);
                            rows[n][i] = resultI;
                            break;
                        case JsonTypes.DateTime:
                            DateTime dt;
                            DateTime.TryParse(rows[n][i].ToString(), out dt);
                            rows[n][i] = dt;
                            break;
                        case JsonTypes.TimeSpan:
                            TimeSpan ts;
                            TimeSpan.TryParse(rows[n][i].ToString(), out ts);
                            rows[n][i] = ts;
                            break;
                        case JsonTypes.Boolean:
                            bool resultB;
                            bool.TryParse(rows[n][i].ToString(), out resultB);
                            rows[n][i] = resultB;
                            break;
                        case JsonTypes.String:
                            rows[n][i] = ((JsonValue)rows[n][i]).ToString();
                            break;
                    }
                }
#pragma warning restore CS8600
#pragma warning restore CS8602
            }
        }

        public static bool SendJsonObject(byte fncByte, JsonObject jsonObject)
        {
            try
            {
                lock (streamLock)
                {
                    using NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                    {
                        if (stream == null)
                            throw new Exception(NO_NETWORK_STREAM);

                        stream.WriteByte(fncByte);
                        sr.WriteAndFlush(stream, sd.sessionID);
                        sr.WriteAndFlush(stream, jsonObject.ToJsonString());

                        int response = sr.ReadByte(stream);

                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                            return true;
                        if (response == Glo.CLIENT_INSUFFICIENT_PERMISSIONS)
                            throw new Exception(PERMISSION_DENIED);
                        if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                            throw new Exception(sr.ReadString(stream));
                        if (response == Glo.CLIENT_SESSION_INVALID)
                            return SessionInvalidated();
                        throw new Exception();
                    }
                }
            }
            catch (Exception e)
            {
                DisplayError(ErrorConcat("Unable to send Json Object to agent.", e.Message));
                return false;
            }
        }

        // Called whenever a window that could be the remaining one open is closed. This used to end the application on
        // its own, but now the listener thread is running, this is no longer the case.
        public static void WindowClosed()
        {
            if (Application.Current.Windows.Count == 0 ||
                !Current.Windows.OfType<BridgeOpsClient.MainWindow>().Any())
            {
                LogOut();
                Environment.Exit(0);
            }
        }


        //   C O N F E R E N C I N G
        //public static 
    }

    public class SessionDetails
    {
        public string sessionID = "";
        public int loginID = -1;
        public string username = "";

        static public byte[] thisIpAddress = new byte[] { 0, 0, 0, 0 };
        public byte[] serverIpAddress = new byte[] { 127, 0, 0, 1 };
        public int portOutbound = 0; // Outbound to the server.
        public int portInbound = 0; // Inbound from the server.

        public TcpListener? listener;

        public bool admin = false;

        // Permissions are enforced in the application, but crucially also in the agent.
        public bool[] createPermissions = new bool[6];
        public bool[] editPermissions = new bool[6];
        public bool[] deletePermissions = new bool[6];

        public IPAddress ThisIP { get { return new IPAddress(thisIpAddress); } }
        public IPEndPoint ThisEP { get { return new IPEndPoint(ThisIP, portInbound); } }
        public IPAddress ServerIP { get { return new IPAddress(serverIpAddress); } }
        public IPEndPoint ServerEP { get { return new IPEndPoint(ServerIP, portOutbound); } }

        public bool SetServerIP(string strIP)
        {
            IPAddress? adrIp;
            if (IPAddress.TryParse(strIP, out adrIp))
            {
                serverIpAddress = adrIp.GetAddressBytes();
                return true;
            }
            return false;
        }

        public bool InitialiseListener()
        {
            try
            {
                listener = new TcpListener(ThisIP, portInbound);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class UserSettings
    {
        // These collections store the user's desired order and display settings for SqlDataGrids.
        // Array indices:  0  Organisation
        //                 1  Asset
        //                 2  Contact
        //                 3  Asset (Organisation links table)
        //                 4  Contact (Organisation links table)
        //                 5  User (Settings menu)
        //                 6  Column (Settings menu)
        //                 7  Conference
        //                 8  Recurrence
        //                 9  Resource
        public List<string>[] dataOrder = new List<string>[10];
        public List<bool>[] dataHidden = new List<bool>[10];
        public List<double>[] dataWidths = new List<double>[10];

        public bool settingsPulled = false;

        public UserSettings()
        {
            for (int i = 0; i < dataOrder.Length; ++i)
            {
                dataOrder[i] = new();
                dataHidden[i] = new();
                dataWidths[i] = new();
            }
        }
    }
}
