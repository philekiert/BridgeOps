﻿using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using SendReceiveClasses;
using static BridgeOpsClient.CustomControls.SqlDataGrid;

namespace BridgeOpsClient
{
    public partial class App : Application
    {
        public static bool IsLoggedIn { get { return sd.sessionID != ""; } }

        public static string NO_NETWORK_STREAM = "NetworkStream could not be connected.";
        public static string PERMISSION_DENIED = "You do not have sufficient permissions to carry out this action";

        public App()
        {
            // Set current working directory.
            string? currentDir = System.Reflection.Assembly.GetExecutingAssembly().Location;
            currentDir = Path.GetDirectoryName(currentDir);
            if (currentDir == null)
                MessageBox.Show("Could not get working directory for application.");
            else
                Environment.CurrentDirectory = currentDir;

            // Apply network settings.
            try
            {
                sd.SetServerIP(BridgeOpsClient.Properties.Settings.Default.serverAddress);
                sd.portInbound = BridgeOpsClient.Properties.Settings.Default.portInbound;
                sd.portOutbound = BridgeOpsClient.Properties.Settings.Default.portOutbound;
                try
                {
                    sd.InitialiseListener();
                    listenerThread = new Thread(new ThreadStart(ListenForServer));
                    listenerThread.Start();
                }
                catch
                {
                    MessageBox.Show("Could not initiate TCP listener. Please check network adapter is enabled and " +
                                    "reload the application.");
                    Environment.Exit(1);
                }
            }
            catch
            {
                MessageBox.Show("Something went wrong when applying the network settings. " +
                                "Using loopback address, and ports 52343 (outbound) and 52344 (inbound).");
                sd.SetServerIP("127.0.0.1");
                sd.portInbound = 52343;
                sd.portOutbound = 52344;
            }

            foreach (Window win in Application.Current.Windows)
                if (win is BridgeOpsClient.MainWindow mainWin)
                    mainWindow = mainWin;
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
        private static AutoResetEvent autoResetEvent = new AutoResetEvent(false);
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
                            MessageBox.Show("The column record is out of date, but a new one could not be pulled. " +
                                            "Logging out.");
                        else
                        {
                            MessageBox.Show("Column record has been updated. It would be advisable to restart the " +
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

        public static SendReceive sr = new SendReceive();
        public static SessionDetails sd = new SessionDetails();
        public static UserSettings us = new UserSettings();
        public static object streamLock = new();

        public static BridgeOpsClient.MainWindow? mainWindow = null;

        private void ApplicationExit(object sender, EventArgs e)
        {
            LogOut();
        }

        public static string LogIn(string username, string password)
        {
            lock (streamLock)
            {
                LoginRequest loginReq = new LoginRequest(username, password);
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
                        for (int i = 0; i < us.dataOrder.Length; ++i)
                            settings += string.Join(";", us.dataOrder[i]) + "\n";
                        for (int i = 0; i < us.dataHidden.Length; ++i)
                            settings += string.Join(";", us.dataHidden[i]) + "\n";
                        for (int i = 0; i < us.dataWidths.Length; ++i)
                            settings += string.Join(";", us.dataWidths[i]) + "\n";
                        settings += (int)BridgeOpsClient.MainWindow.oldConfWidth + ";" +
                                    (int)BridgeOpsClient.MainWindow.oldDataWidth + ";" +
                                    BridgeOpsClient.MainWindow.viewState;
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
                                    MessageBox.Show("Could not find this user's session.");
                                else if (response == Glo.CLIENT_LOGOUT_ACCEPT)
                                    MessageBox.Show("User logged out successfully");
                                else if (response == Glo.CLIENT_INSUFFICIENT_PERMISSIONS.ToString())
                                    MessageBox.Show("You do not have the required permissions for this action");
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

                    LogIn logIn = new LogIn((MainWindow)Current.MainWindow);
                    logIn.ShowDialog();
                }

                return true;
            }
            catch
            {
                MessageBox.Show("Something went wrong.");
                return false;
            }
        }

        public static void SessionInvalidated()
        {
            sd.sessionID = ""; // Tells the app that it's no longer logged in.
            if (mainWindow != null)
            {
                mainWindow.ToggleLogInOut(false);
                if (mainWindow.IsLoaded)
                    MessageBox.Show("Session is no longer valid. Please copy any unsaved work, then log out and back in.");
            }
        }

        public static bool EditOrganisation(string id)
        {
            string[]? organisationArray = SelectColumnPrimary("Organisation", Glo.Tab.ORGANISATION_ID);
            if (organisationArray == null)
            {
                MessageBox.Show("Could not pull organisation list from server.");
                return false;
            }
            List<string> organisationList = organisationArray.ToList();
            organisationList.Insert(0, "");

            List<string?> columnNames;
            List<List<object?>> rows;
            if (App.Select("Organisation",
                           new List<string> { "*" },
                           new List<string> { Glo.Tab.ORGANISATION_ID },
                           new List<string> { id },
                           new List<Conditional> { Conditional.Equals },
                           out columnNames, out rows, false))
            {
                if (rows.Count > 0)
                {
                    // We would expect data for every field. If the count is different, the operation must have failed.
                    if (rows[0].Count == ColumnRecord.organisation.Count)
                    {
                        NewOrganisation org = new(id);
                        org.cmbOrgParentID.ItemsSource = organisationList;
                        org.PopulateExistingData(rows[0]);
                        org.Show();
                    }
                    else
                    {
                        MessageBox.Show("Incorrect number of fields received.");
                    }
                }
                else
                    MessageBox.Show("Could no longer retrieve record.");
            }
            return true;
        }

        public static bool EditAsset(string id)
        {
            string[]? organisationArray = SelectColumnPrimary("Organisation", Glo.Tab.ORGANISATION_ID);
            if (organisationArray == null)
            {
                MessageBox.Show("Could not pull organisation list from server.");
                return false;
            }
            List<string> organisationList = organisationArray.ToList();
            organisationList.Insert(0, "");

            List<string?> columnNames;
            List<List<object?>> rows;
            if (App.Select("Asset",
                           new List<string> { "*" },
                           new List<string> { Glo.Tab.ASSET_ID },
                           new List<string> { id },
                           new List<Conditional> { Conditional.Equals },
                           out columnNames, out rows, false))
            {
                if (rows.Count > 0)
                {
                    // We expect data for every field. If the count is different, the operation must have failed.
                    if (rows[0].Count == ColumnRecord.asset.Count)
                    {
                        NewAsset asset = new NewAsset(id);
                        asset.cmbOrgID.ItemsSource = organisationList;
                        asset.Populate(rows[0]);
                        asset.Show();
                    }
                    else
                    {
                        MessageBox.Show("Incorrect number of fields received.");
                    }
                }
                else
                    MessageBox.Show("Could no longer retrieve record.");
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
                           out columnNames, out rows, false))
            {
                if (rows.Count > 0)
                {
                    // We expect data for every field. If the count is different, the operation must have failed.
                    if (rows[0].Count == ColumnRecord.contact.Count)
                    {
                        NewContact contact = new NewContact(id);
                        contact.Populate(rows[0]);
                        contact.Show();
                    }
                    else
                    {
                        MessageBox.Show("Incorrect number of fields received.");
                    }
                }
                else
                    MessageBox.Show("Could no longer retrieve record.");
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
                                ColumnRecord.Initialise(sr.ReadString(stream));
                                return true;
                            }
                            else
                            {
                                MessageBox.Show("Could not pull column record.");
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
                    int startTimes = columnNames.IndexOf(Glo.Tab.RESOURCE_FROM);
                    int endTimes = columnNames.IndexOf(Glo.Tab.RESOURCE_TO);
                    int capacities = columnNames.IndexOf(Glo.Tab.RESOURCE_CAPACITY);

                    if (ids == -1 || names == -1 || startTimes == -1 || endTimes == -1 || capacities == -1)
                        return false;

                    foreach (List<object?> row in rows)
                    {
                        if (row[ids] != null &&
                            row[names] != null &&
                            row[startTimes] != null &&
                            row[endTimes] != null &&
                            row[capacities] != null)
                        {
#pragma warning disable CS8602
#pragma warning disable CS8604
#pragma warning disable CS8605
                            PageConferenceView.resources.Add(new PageConferenceView.ResourceInfo((int)row[ids],
                                                                                                 row[names].ToString(),
                                                                                                 (DateTime)row[startTimes],
                                                                                                 (DateTime)row[endTimes],
                                                                                                 (int)row[capacities]));
#pragma warning restore CS8602
#pragma warning restore CS8604
#pragma warning restore CS8605
                        }
                    }
                    foreach (PageConferenceView confView in BridgeOpsClient.MainWindow.pageConferenceViews)
                        confView.SetResources();

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
                        stream.WriteByte(Glo.CLIENT_PULL_USER_SETTINGS);
                        {
                            sr.WriteAndFlush(stream, sd.sessionID);
                            string result = sr.ReadString(stream);
                            stream.Close();

                            string[] settings = result.Split("\n");

                            if (settings.Length != (us.dataOrder.Length * 3) + 1)
                                return false;

                            double dVal;
                            for (int i = 0; i < us.dataOrder.Length; ++i)
                            {
                                // Order strings.
                                us.dataOrder[i] = settings[i].Split(";").ToList();
                                us.dataWidths[i].Clear();
                                us.dataHidden[i].Clear();

                                // Hidden bools.
                                int index = i + us.dataOrder.Length;
                                foreach (string s in settings[index].Split(";"))
                                    us.dataHidden[i].Add(s == "True");

                                // Width ints.
                                index += us.dataOrder.Length;
                                foreach (string s in settings[index].Split(";"))
                                    if (double.TryParse(s, out dVal) && dVal >= 0)
                                        us.dataWidths[i].Add(dVal);
                            }

                            // Conference view width, database view width, then view state.
                            string[] viewSplit = settings[us.dataOrder.Length * 3].Split(";");
                            if (viewSplit.Length != 3)
                                return false;

                            int iVal;
                            if (int.TryParse(viewSplit[0], out iVal))
                                BridgeOpsClient.MainWindow.oldConfWidth = iVal;
                            if (int.TryParse(viewSplit[1], out iVal))
                                BridgeOpsClient.MainWindow.oldDataWidth = iVal;
                            if (int.TryParse(viewSplit[2], out iVal) && iVal >= 0 && iVal <= 2)
                                BridgeOpsClient.MainWindow.viewState = iVal;

                            for (int i = 0; i < us.dataOrder.Length; ++i)
                                if (us.dataOrder[i].Count != us.dataHidden[i].Count ||
                                    us.dataOrder[i].Count != us.dataWidths[i].Count)
                                    return false;
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
            return SendInsert(fncByte, toSerialise, out _);
        }
        public static bool SendInsert(byte fncByte, object toSerialise, out string returnID)
        {
            lock (streamLock)
            {
                NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                try
                {
                    if (stream != null)
                    {
                        stream.WriteByte(fncByte);
                        sr.WriteAndFlush(stream, sr.Serialise(toSerialise));
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
                            SessionInvalidated();
                        }
                        else if (response == Glo.CLIENT_INSUFFICIENT_PERMISSIONS)
                        {
                            MessageBox.Show(PERMISSION_DENIED);
                        }
                        else if (response == Glo.CLIENT_REQUEST_FAILED_FOREIGN_KEY)
                        {
                            MessageBox.Show("The foreign key could no longer be found.");
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
        {
            lock (streamLock)
            {
                NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                try
                {
                    if (stream != null)
                    {
                        stream.WriteByte(fncByte);
                        sr.WriteAndFlush(stream, sr.Serialise(toSerialise));
                        int response = stream.ReadByte();
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                            return true;
                        else if (response == Glo.CLIENT_SESSION_INVALID)
                        {
                            SessionInvalidated();
                        }
                        else if (response == Glo.CLIENT_INSUFFICIENT_PERMISSIONS)
                        {
                            MessageBox.Show(PERMISSION_DENIED);
                        }
                        else if (response == Glo.CLIENT_REQUEST_FAILED_RECORD_DELETED)
                        {
                            MessageBox.Show("The record could no longer be found.");
                            string reason = sr.ReadString(stream);
                        }
                    }
                    return false;
                }
                catch
                {
                    MessageBox.Show("Could not run table update.");
                    return false;
                }
                finally
                {
                    if (stream != null) stream.Close();
                }
            }
        }

        public static bool SendDelete(string table, string column, string id, bool isString)
        {
            lock (streamLock)
            {
                NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                try
                {
                    if (stream != null)
                    {
                        DeleteRequest req = new DeleteRequest(sd.sessionID, ColumnRecord.columnRecordID,
                                                              table, column, id, isString);

                        stream.WriteByte(Glo.CLIENT_DELETE);
                        sr.WriteAndFlush(stream, sr.Serialise(req));
                        int response = stream.ReadByte();
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                            return true;
                        else if (response == Glo.CLIENT_SESSION_INVALID)
                        {
                            SessionInvalidated();
                        }
                        else if (response == Glo.CLIENT_INSUFFICIENT_PERMISSIONS)
                        {
                            MessageBox.Show(PERMISSION_DENIED);
                        }
                        else if (response == Glo.CLIENT_REQUEST_FAILED_RECORD_DELETED)
                        {
                            MessageBox.Show("The record could no longer be found.");
                        }
                    }
                    return false;
                }
                catch
                {
                    MessageBox.Show("Could not delete record.");
                    return false;
                }
                finally
                {
                    if (stream != null) stream.Close();
                }
            }
        }

        // Returns null if the operation failed, returns an array if successful, empty or otherwise.
        public static string[]? SelectColumnPrimary(string table, string column)
        {
            lock (streamLock)
            {
                NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                try
                {
                    if (stream != null)
                    {
                        PrimaryColumnSelect pcs = new PrimaryColumnSelect(sd.sessionID, ColumnRecord.columnRecordID,
                                                                          table, column);
                        stream.WriteByte(Glo.CLIENT_SELECT_COLUMN_PRIMARY);
                        sr.WriteAndFlush(stream, sr.Serialise(pcs));
                        int response = stream.ReadByte();
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                            return sr.ReadString(stream).Split(';');
                        else if (response == Glo.CLIENT_SESSION_INVALID)
                            SessionInvalidated();
                        return null;
                    }
                    return null;
                }
                catch
                {
                    MessageBox.Show("Could not run or return query.");
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
            return Select(table, new List<string> { "*" }, new(), new(), new(), out columnNames, out rows, historical);
        }
        public static bool SelectAll(string table, string likeColumn, string likeValue, Conditional conditional,
                                     out List<string?> columnNames, out List<List<object?>> rows, bool historical)
        {
            return Select(table, new List<string> { "*" },
                          new List<string> { likeColumn }, new List<string> { likeValue },
                          new List<Conditional> { conditional },
                          out columnNames, out rows, historical);
        }
        public static bool Select(string table, List<string> select,
                                  out List<string?> columnNames, out List<List<object?>> rows, bool historical)
        {
            return Select(table, select, new(), new(), new(), out columnNames, out rows, historical);
        }
        public static bool Select(string table, List<string> select,
                                  List<string> likeColumns, List<string> likeValues, List<Conditional> conditionals,
                                  out List<string?> columnNames, out List<List<object?>> rows, bool historical)
        {
            // For the Organisation, Asset, Conference and Contact tables that allow different column orders,
            // the columns must be stated in the correct order when populating the DataInputTable.
            var dictionary = ColumnRecord.GetDictionary(table, true);
            if (dictionary != null)
            {
                select.Clear();
                foreach (var col in dictionary)
                    select.Add(col.Key);
            }

            lock (streamLock)
            {
                using NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                {
                    try
                    {
                        if (stream != null)
                        {
                            SelectRequest req = new SelectRequest(sd.sessionID, ColumnRecord.columnRecordID,
                                                                  table, select, likeColumns, likeValues, conditionals,
                                                                  historical);
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
                            else if (response == Glo.CLIENT_SESSION_INVALID)
                                SessionInvalidated();
                            throw new Exception();
                        }
                        throw new Exception();
                    }
                    catch
                    {
                        MessageBox.Show("Could not run or return query.");
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
                foreach (var col in dictionary)
                    select.Add(col.Key);

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
                                SessionInvalidated();
                            throw new Exception();
                        }
                        throw new Exception();
                    }
                    catch
                    {
                        MessageBox.Show("Could not run or return query.");
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

        public static bool LinkContact(string organisationID, int contactID, bool unlink)
        {
            lock (streamLock)
            {
                NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                try
                {
                    if (stream != null)
                    {
                        LinkContactRequest req = new(sd.sessionID, ColumnRecord.columnRecordID,
                                                     organisationID, contactID, unlink);

                        stream.WriteByte(Glo.CLIENT_LINK_CONTACT);
                        sr.WriteAndFlush(stream, sr.Serialise(req));
                        int response = stream.ReadByte();
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                            return true;
                        else if (response == Glo.CLIENT_SESSION_INVALID)
                        {
                            SessionInvalidated();
                        }
                        else if (response == Glo.CLIENT_INSUFFICIENT_PERMISSIONS)
                        {
                            MessageBox.Show(PERMISSION_DENIED);
                        }
                        else if (response == Glo.CLIENT_REQUEST_FAILED_RECORD_DELETED)
                        {
                            MessageBox.Show("The record could no longer be found.");
                        }
                    }
                    return false;
                }
                catch
                {
                    MessageBox.Show("Could not link or unlink contact to organisation.");
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
                            SessionInvalidated();
                        throw new Exception();
                    }
                    throw new Exception();
                }
                catch
                {
                    MessageBox.Show("Could not run or return query.");
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
            lock (streamLock)
            {
                NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                try
                {
                    if (stream != null)
                    {
                        SelectHistoryRequest req = new(sd.sessionID, ColumnRecord.columnRecordID, table, id);
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
                            SessionInvalidated();
                        throw new Exception();
                    }
                    throw new Exception();
                }
                catch
                {
                    MessageBox.Show("Could not run or return history list.");
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

        public static bool BuildHistorical(string table, string changeID, string recordID, out List<object?> data)
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
                            SessionInvalidated();
                        throw new Exception();
                    }
                    throw new Exception();
                }
                catch
                {
                    MessageBox.Show("Could not run or return historical record.");
                    data = new();
                    return false;
                }
                finally
                {
                    if (stream != null) stream.Close();
                }
            }
        }

        static private void ConvertUnknownJsonObjectsToRespectiveTypes(List<string?> columnTypes, List<List<object?>> rows)
        {
            for (int n = 0; n < rows.Count; ++n)
            {
#pragma warning disable CS8600
#pragma warning disable CS8602
                for (int i = 0; i < columnTypes.Count; ++i)
                {
                    if (rows[n][i] != null)
                    {
                        if (columnTypes[i].StartsWith("Int") || columnTypes[i].StartsWith("Byte"))
                        {
                            int result;
                            int.TryParse(rows[n][i].ToString(), out result);
                            rows[n][i] = result;
                        }
                        else if (columnTypes[i] == "DateTime")
                        {
                            DateTime dt;
                            DateTime.TryParse(rows[n][i].ToString(), out dt);
                            rows[n][i] = dt;
                        }
                        else if (columnTypes[i] == "Boolean")
                        {
                            bool result;
                            bool.TryParse(rows[n][i].ToString(), out result);
                            rows[n][i] = result;
                        }
                        else if (columnTypes[i] == "String")
                        {
                            rows[n][i] = ((JsonValue)rows[n][i]).ToString();
                        }
                    }
                }
#pragma warning restore CS8600
#pragma warning restore CS8602
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
        // These collections store the user's desired order and display settings for the database view.
        // Array indices:  0  Organisation
        //                 1  Asset
        //                 2  Contact
        //                 3  Asset (Organisation links table)
        //                 4  Contact (Organisation links table)
        public List<string>[] dataOrder = new List<string>[7];
        public List<bool>[] dataHidden = new List<bool>[7];
        public List<double>[] dataWidths = new List<double>[7];

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
