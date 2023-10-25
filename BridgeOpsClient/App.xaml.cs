using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
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
using static BridgeOpsClient.ColumnRecord;
using static BridgeOpsClient.CustomControls.SqlDataGrid;

namespace BridgeOpsClient
{
    public partial class App : Application
    {
        public static bool IsLoggedIn { get { return sd.sessionID != ""; } }

        public App()
        {
            // Set current working directory.
            string? currentDir = System.Reflection.Assembly.GetExecutingAssembly().Location;
            currentDir = Path.GetDirectoryName(currentDir);
            if (currentDir == null)
                MessageBox.Show("Could not get working directory for application.");
            else
                Environment.CurrentDirectory = currentDir;

            // Get the port numbers, if present.
            try
            {
                string[] networkConfig = File.ReadAllLines(Glo.PATH_CONFIG_FILES + Glo.CONFIG_NETWORK);
                foreach (string s in networkConfig)
                {
                    if (s.Length > Glo.NETWORK_SETTINGS_LENGTH && !s.StartsWith("# "))
                    {
                        int iVal;
                        if (s.StartsWith(Glo.NETWORK_SETTINGS_PORT_INBOUND))
                        {
                            if (int.TryParse(s.Substring(Glo.NETWORK_SETTINGS_LENGTH,
                                                         s.Length - Glo.NETWORK_SETTINGS_LENGTH), out iVal) &&
                                iVal >= 1025 && iVal <= 65535)
                                sd.portInbound = iVal;
                        }
                        else if (s.StartsWith(Glo.NETWORK_SETTINGS_PORT_OUTBOUND))
                        {
                            if (int.TryParse(s.Substring(Glo.NETWORK_SETTINGS_LENGTH,
                                                         s.Length - Glo.NETWORK_SETTINGS_LENGTH), out iVal) &&
                                iVal >= 1025 && iVal <= 65535)
                                sd.portOutbound = iVal;
                        }
                    }
                }
            }
            catch
            {
                MessageBox.Show("network-config.txt not found. Using default settings.");
                return;
            }
        }

        public static SendReceive sr = new SendReceive();
        public static SessionDetails sd = new SessionDetails();

        private void ApplicationExit(object sender, EventArgs e)
        {
            LogOut();
        }

        public static string LogIn(string username, string password)
        {
            LoginRequest loginReq = new LoginRequest(username, password);
            string send = sr.Serialise(loginReq);

            NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
            try
            {
                if (stream == null)
                    throw new Exception("NetworkStream could not be connected");
                stream.WriteByte(Glo.CLIENT_LOGIN);
                sr.WriteAndFlush(stream, send);

                string result = sr.ReadString(stream);

                if (result.StartsWith(Glo.CLIENT_LOGIN_ACCEPT))
                {
                    sd.username = loginReq.username;
                    sd.sessionID = result.Replace(Glo.CLIENT_LOGIN_ACCEPT, "");

                    return Glo.CLIENT_LOGIN_ACCEPT;
                }
                else
                {
                    return result;
                }
            }
            catch
            {
                return "";
            }
        }

        public static bool LogOut()
        {
            if (IsLoggedIn)
            {
                NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                if (stream != null)
                {
                    stream.WriteByte(Glo.CLIENT_LOGOUT);
                    sr.WriteAndFlush(stream, sr.Serialise(new LogoutRequest(sd.sessionID,
                                                          sd.username)));
                    sr.ReadString(stream); // Empty the pipe.

                    sd = new SessionDetails();

                    return true;
                }
                else
                    return false;
            }
            else
                return false;
        }

        public static bool EditOrganisation(string id)
        {
            string[]? organisationList = SelectColumnPrimary("Organisation", Glo.Tab.ORGANISATION_ID);
            if (organisationList == null)
            {
                MessageBox.Show("Could not pull organisation list from server.");
                return false;
            }

            List<string?> columnNames;
            List<List<object?>> rows;
            if (App.Select("Organisation",
                           new List<string> { "*" },
                           new List<string> { Glo.Tab.ORGANISATION_ID },
                           new List<string> { id },
                           out columnNames, out rows))
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
            string[]? organisationList = SelectColumnPrimary("Organisation", Glo.Tab.ORGANISATION_ID);
            if (organisationList == null)
            {
                MessageBox.Show("Could not pull organisation list from server.");
                return false;
            }

            List<string?> columnNames;
            List<List<object?>> rows;
            if (App.Select("Asset",
                           new List<string> { "*" },
                           new List<string> { Glo.Tab.ASSET_ID },
                           new List<string> { id },
                           out columnNames, out rows))
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
                           out columnNames, out rows))
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

        public static void SessionInvalidated()
        {
            sd = new SessionDetails(); // Tells the app that it's no longer logged in.
            MessageBox.Show("Session is no longer valid, please log back in.");
        }

        public static bool PullColumnRecord()
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
            }
        }

        public static bool SendInsert(byte fncByte, object toSerialise)
        {
            return SendInsert(fncByte, toSerialise, out _);
        }
        public static bool SendInsert(byte fncByte, object toSerialise, out string returnID)
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
                        returnID = "";
                        return false;
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

        public static bool SendUpdate(byte fncByte, object toSerialise)
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
                        return false;
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

        public static bool SendDelete(string table, string column, string id, bool isString)
        {
            NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
            try
            {
                if (stream != null)
                {
                    DeleteRequest req = new DeleteRequest(sd.sessionID, table, column, id, isString);

                    stream.WriteByte(Glo.CLIENT_DELETE);
                    sr.WriteAndFlush(stream, sr.Serialise(req));
                    int response = stream.ReadByte();
                    if (response == Glo.CLIENT_REQUEST_SUCCESS)
                        return true;
                    else if (response == Glo.CLIENT_SESSION_INVALID)
                    {
                        SessionInvalidated();
                        return false;
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

        // Returns null if the operation failed, returns an array if successful, empty or otherwise.
        public static string[]? SelectColumnPrimary(string table, string column)
        {
            NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
            try
            {
                if (stream != null)
                {
                    PrimaryColumnSelect pcs = new PrimaryColumnSelect(sd.sessionID, table, column);
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

        public static bool SelectAll(string table, out List<string?> columnNames, out List<List<object?>> rows)
        {
            return Select(table, new List<string> { "*" }, new(), new(), out columnNames, out rows);
        }
        public static bool SelectAll(string table, string likeColumn, string likeValue,
                                     out List<string?> columnNames, out List<List<object?>> rows)
        {
            return Select(table, new List<string> { "*" },
                          new List<string> { likeColumn }, new List<string> { likeValue },
                          out columnNames, out rows);
        }

        public static bool Select(string table, List<string> select,
                                  List<string> likeColumns, List<string> likeValues,
                                  out List<string?> columnNames, out List<List<object?>> rows)
        {
            NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
            try
            {
                if (stream != null)
                {
                    SelectRequest req = new SelectRequest(sd.sessionID, table, select, likeColumns, likeValues);
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

        public static bool LinkContact(string organisationID, int contactID, bool unlink)
        {
            NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
            try
            {
                if (stream != null)
                {
                    LinkContactRequest req = new LinkContactRequest(sd.sessionID, organisationID, contactID, unlink);

                    stream.WriteByte(Glo.CLIENT_LINK_CONTACT);
                    sr.WriteAndFlush(stream, sr.Serialise(req));
                    int response = stream.ReadByte();
                    if (response == Glo.CLIENT_REQUEST_SUCCESS)
                        return true;
                    else if (response == Glo.CLIENT_SESSION_INVALID)
                    {
                        SessionInvalidated();
                        return false;
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

        public static bool LinkedContactSelect(string organisationID,
                                               out List<string?> columnNames, out List<List<object?>> rows)
        {
            NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
            try
            {
                if (stream != null)
                {
                    LinkedContactSelectRequest req = new(sd.sessionID, organisationID);
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

        public static bool SelectHistory(string table, string id,
                                         out List<string?> columnNames, out List<List<object?>> rows)
        {
            NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
            try
            {
                if (stream != null)
                {
                    SelectHistoryRequest req = new(sd.sessionID, table, id);
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

        public static bool BuildHistorical(string table, string changeID, string recordID, out List<object?> data)
        {
            NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
            try
            {
                if (stream != null)
                {
                    SelectHistoricalRecordRequest req = new(sd.sessionID, table, changeID, recordID);
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

        static private void ConvertUnknownJsonObjectsToRespectiveTypes(List<string?> columnTypes, List<List<object?>> rows)
        {
            for (int n = 0; n < rows.Count; ++n)
            {
#pragma warning disable CS8602
                for (int i = 0; i < columnTypes.Count; ++i)
                {
                    if (rows[n][i] != null)
                    {
                        if (columnTypes[i] == "DateTime")
                        {
                            DateTime dt;
                            DateTime.TryParse(rows[n][i].ToString(), out dt);
                            rows[n][i] = dt;
                        }
                    }
                }
#pragma warning restore CS8602
            }
        }
    }

    public class SessionDetails
    {
        public string sessionID = "";
        public string username = "";

        public int portInbound = 0; // Inbound to server.
        public int portOutbound = 0; // Outbound from server.

        public IPAddress serverIP = new IPAddress(new byte[] { 127, 0, 0, 1 });
        public IPEndPoint ServerEP { get { return new IPEndPoint(serverIP, portInbound); } }
    }

    public static class ColumnRecord
    {
        // Add to this as development continues.
        public struct Column
        {
            // column name is stored as the key in Dictionary
            public string type;
            public int restriction; // Only for character strings
            public string[] allowed; // Allowed values, only ever present in user-added columns
            public string friendlyName;

            public Column(string type, int restriction, string[] allowed, string friendlyName)
            {
                this.type = type;
                this.restriction = restriction;
                this.allowed = allowed;
                this.friendlyName = friendlyName;
            }
        }

        public static string GetPrintName(KeyValuePair<string, Column> col)
        {
            if (col.Value.friendlyName != "")
                return col.Value.friendlyName.Replace('_', ' ');
            else
                return col.Key.Replace('_', ' ');
        }
        public static string GetPrintName(string key, Column col)
        {
            if (col.friendlyName != "")
                return col.friendlyName.Replace('_', ' ');
            else
                return key.Replace('_', ' ');
        }

        public static Dictionary<string, Column> organisation = new();
        public static Dictionary<string, string> organisationFriendlyNameReversal = new();
        public static Dictionary<string, Column> organisationChange = new();
        public static Dictionary<string, string> organisationChangeFriendlyNameReversal = new();
        public static Dictionary<string, Column> asset = new();
        public static Dictionary<string, string> assetFriendlyNameReversal = new();
        public static Dictionary<string, Column> assetChange = new();
        public static Dictionary<string, string> assetChangeFriendlyNameReversal = new();
        public static Dictionary<string, Column> contact = new();
        public static Dictionary<string, string> contactFriendlyNameReversal = new();
        public static Dictionary<string, Column> conferenceType = new();
        public static Dictionary<string, string> conferenceTypeFriendlyNameReversal = new();
        public static Dictionary<string, Column> conference = new();
        public static Dictionary<string, string> conferenceFriendlyNameReversal = new();
        public static Dictionary<string, Column> conferenceRecurrence = new();
        public static Dictionary<string, string> conferenceRecurrenceFriendlyNameReversal = new();
        public static Dictionary<string, Column> resource = new();
        public static Dictionary<string, string> resourceFriendlyNameReversal = new();
        public static Dictionary<string, Column> login = new();
        public static Dictionary<string, string> loginFriendlyNameReversal = new();

        public static bool IsTypeString(Column col)
        { return col.type == "TEXT"; }
        public static bool IsTypeInt(Column col)
        { return col.type.Contains("INT"); }

        public static void Initialise(string columns)
        {
            try
            {
                string[] lines = columns.Split('\n');
                // Start at 1, since the first line is an edit warning.
                int n = 1;
                for (; n < lines.Length; ++n)
                {
                    if (lines[n] == "-")
                    {
                        ++n;
                        break; // Proceed to reading friendly names.
                    }

                    int cInd = lines[n].IndexOf("[C]");
                    int rInd = lines[n].IndexOf("[R]");
                    int aInd = lines[n].IndexOf("[A]"); // Likely the first of many

                    string table = lines[n].Remove(cInd);
                    string column = lines[n].Substring(cInd + 3, rInd - (cInd + 3));
                    string restriction = lines[n].Substring(rInd + 3);
                    string[] allowedArr = new string[0];
                    if (aInd != -1)
                    {
                        restriction = restriction.Remove(restriction.IndexOf("[A]"));
                        string allowed = lines[n].Substring(aInd + 3);
                        allowedArr = allowed.Split("[A]");
                    }

                    // Type and restriction will be corrected in a sec.
                    string type = restriction;
                    int max = 0;

                    // If type is text, change type name and set max length.
                    int r;
                    if (int.TryParse(restriction, out r))
                    {
                        type = "TEXT";
                        max = r;
                    }
                    else if (type == "TINYINT")
                        max = 255;
                    else if (type == "SMALLINT")
                        max = 32_767;
                    else if (type == "INT")
                        max = 2_147_483_647;
                    // else max remains 0 for dates.

                    Column col = new Column(type, max, allowedArr, "");


                    // Add column to the relevant Dictionary, using the column name as the key.
                    if (table == "Organisation")
                        organisation.Add(column, col);
                    if (table == "OrganisationChange")
                        organisationChange.Add(column, col);
                    else if (table == "Contact")
                        contact.Add(column, col);
                    else if (table == "Asset")
                        asset.Add(column, col);
                    else if (table == "AssetChange")
                        assetChange.Add(column, col);
                    else if (table == "ConferenceType")
                        conferenceType.Add(column, col);
                    else if (table == "Conference")
                        conference.Add(column, col);
                    else if (table == "Recurrence")
                        conferenceRecurrence.Add(column, col);
                    else if (table == "Resource")
                        resource.Add(column, col);
                    else if (table == "Login")
                        login.Add(column, col);
                }

                for (; n < lines.Length; ++n) // Won't run if there are no friendly names.
                {
                    string[] friendlySplit = lines[n].Split(";;");
                    // The option is open for the user to specify friendly names using spaces instead of
                    // underscores, so make that uniform here.
                    friendlySplit[1] = friendlySplit[1].Replace(' ', '_');

                    void AddFriendlyName(Dictionary<string, Column> dict)
                    {
                        if (dict.ContainsKey(friendlySplit[1]))
                        {
                            Column col = dict[friendlySplit[1]];
                            col.friendlyName = friendlySplit[2];
                            dict[friendlySplit[1]] = col;
                        }
                    }

                    if (friendlySplit[0] == "Organisation")
                        AddFriendlyName(organisation);
                    if (friendlySplit[0] == "OrganisationChange")
                        AddFriendlyName(organisationChange);
                    if (friendlySplit[0] == "Asset")
                        AddFriendlyName(asset);
                    if (friendlySplit[0] == "AssetChange")
                        AddFriendlyName(assetChange);
                    if (friendlySplit[0] == "Contact")
                        AddFriendlyName(contact);
                    if (friendlySplit[0] == "ConferenceType")
                        AddFriendlyName(conferenceType);
                    if (friendlySplit[0] == "Conference")
                        AddFriendlyName(conference);
                    if (friendlySplit[0] == "Recurrence")
                        AddFriendlyName(conferenceRecurrence);
                    if (friendlySplit[0] == "Resource")
                        AddFriendlyName(resource);
                    if (friendlySplit[0] == "Login")
                        AddFriendlyName(login);
                }

                // Populate the friendly name reversal dictionaries.
                foreach (KeyValuePair<string, Column> kvp in organisation)
                    organisationFriendlyNameReversal.Add(GetPrintName(kvp), kvp.Key);
                foreach (KeyValuePair<string, Column> kvp in organisationChange)
                    organisationChangeFriendlyNameReversal.Add(GetPrintName(kvp), kvp.Key);
                foreach (KeyValuePair<string, Column> kvp in asset)
                    assetFriendlyNameReversal.Add(GetPrintName(kvp), kvp.Key);
                foreach (KeyValuePair<string, Column> kvp in assetChange)
                    assetChangeFriendlyNameReversal.Add(GetPrintName(kvp), kvp.Key);
                foreach (KeyValuePair<string, Column> kvp in contact)
                    contactFriendlyNameReversal.Add(GetPrintName(kvp), kvp.Key);
                foreach (KeyValuePair<string, Column> kvp in conferenceType)
                    conferenceTypeFriendlyNameReversal.Add(GetPrintName(kvp), kvp.Key);
                foreach (KeyValuePair<string, Column> kvp in conference)
                    conferenceFriendlyNameReversal.Add(GetPrintName(kvp), kvp.Key);
                foreach (KeyValuePair<string, Column> kvp in conferenceRecurrence)
                    conferenceRecurrenceFriendlyNameReversal.Add(GetPrintName(kvp), kvp.Key);
                foreach (KeyValuePair<string, Column> kvp in resource)
                    resourceFriendlyNameReversal.Add(GetPrintName(kvp), kvp.Key);
                foreach (KeyValuePair<string, Column> kvp in login)
                    loginFriendlyNameReversal.Add(GetPrintName(kvp), kvp.Key);
            }
            catch
            {
                // File corrupted
            }
        }
    }

    static class MathC
    {
        public static void Clamp(ref float value, float minimum, float maximum)
        {
            if (value < minimum) value = minimum;
            else if (value > maximum) value = maximum;
        }

        public static void Lerp(ref float value, float target, float amount)
        {
            if (value < target)
            {
                value += ((target - value) * amount);
                if (value > target) value = target;
            }
            else if (value > target)
            {
                value += ((target - value) * amount);
                if (value < target) value = target;
            }
        }
        public static void Lerp(ref float value, float target, float amount, float minimum)
        {
            if (value < target)
            {
                float movement = ((target - value) * amount);
                value += movement > minimum ? movement : minimum;
                if (value > target) value = target;
            }
            else if (value > target)
            {
                float movement = ((target - value) * amount);
                value += movement < -minimum ? movement : -minimum;
                if (value < target) value = target;
            }
        }
    }
}
