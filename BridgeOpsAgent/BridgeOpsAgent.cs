using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using System.Text.Json;
using System.Net;

using SendReceiveClasses;
using System.Windows.Markup;
using System.Linq.Expressions;
using System.Net.Sockets;
using System.IO;
using System.Security.Cryptography;
using System.Data;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using System.Reflection.PortableExecutable;
using System.Xml.Schema;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;
using System.Collections.Specialized;
using System.Collections;

internal class BridgeOpsAgent
{
    static UnicodeEncoding unicodeEncoding = new UnicodeEncoding();
    static SendReceive sr = new SendReceive();

    // int is fine for column version - you'd have to make one update every second for over 130 years with no power
    // outages or other agent restarts to exhaust all possible values.
    static int columnRecordID = 0;
    static string columnRecord = "";
    static bool columnRecordIntact = false;

    const int clientNudgeInterval = /*100_000*/ 10_000_000; // Increased for testing only. Revert for deployment.
    const int notificationSendRetries = 3;
    const int maxMissedNudges = 2;

    class ClientSession
    {
        public string username;
        public string ipString;
        public int loginID;
        public string sessionID; // This is stored in its key, but it's good to have it here as well.
        public bool admin;
        public List<bool[]> permissions;
        public bool[] createPermissions;
        public bool[] editPermissions;
        public bool[] deletePermissions;

        // This will count up each time SqlServerNudge() gets no response. If it exceeds maxMissedNudges, the session
        // is terminated.
        private int missedNudges = 0;
        public int MissedNudges { get { return missedNudges; } }

        public NetworkStream? stream;
        public IPAddress? ipAddess;

        public ClientSession(string username, string ip, int loginID, string sessionID, bool admin,
                             int createPermissions, int editPermissions, int deletePermissions)
        {
            this.username = username;
            ipString = ip;
            this.loginID = loginID;
            this.sessionID = sessionID;
            this.admin = admin;
            this.createPermissions = Glo.Fun.GetPermissionsArray(createPermissions);
            this.editPermissions = Glo.Fun.GetPermissionsArray(editPermissions);
            this.deletePermissions = Glo.Fun.GetPermissionsArray(deletePermissions);

            permissions = new() { this.createPermissions, this.editPermissions, this.deletePermissions };

            ipAddess = Glo.Fun.GetIPAddressFromString(ip);

            stream = null;
        }

        // Clients should only ever be contacted using the network stream in this struct. This is to prevent multiple
        // threads attempting to open a network stream on the same port simultaneously, and the NetworkStream needs to
        // be locked in every instance.
        public bool SetStream(NetworkStream? stream)
        {
            if (this.stream != null)
                return false;
            else
            {
                this.stream = stream;
                return true;
            }
        }
        private object streamLock = new();

        public void CloseStream()
        {
            if (stream != null)
            {
                stream.Close();
                stream = null;
            }
        }

        // This bool is only switched off after the client requests the column record. It's purpose is to prevent the
        // agent from demanding the client make numerous column record pulls where only one would suffice.
        public bool columnRecordChangeNotificationUnsent = false;
        public void SendColumnRecordChangeNotification()
        {
            if (!columnRecordChangeNotificationUnsent)
            {
                columnRecordChangeNotificationUnsent = true;
                if (!SendNotification(Glo.SERVER_COLUMN_RECORD_UPDATED))
                    LogError($"Column record change notification could not be sent to {ipString} " +
                             $"after {notificationSendRetries} retries.");
            }
        }
        public void SendResourceChangeNotification()
        {
            if (!SendNotification(Glo.SERVER_RESOURCES_UPDATED))
                LogError($"Column record change notification could not be sent to {ipString} " +
                         $"after {notificationSendRetries} retries.");
        }
        public void SendLoggedOutNotification()
        {
            if (SendNotification(Glo.SERVER_FORCE_LOGOUT))
                LogError($"Logged out notification could not be sent to {ipString} " +
                         $"after {notificationSendRetries} retries.");
        }

        public void SendNudge()
        {
            if (ipAddess == null)
                return; // Will never be the case.

            lock (streamLock)
            {
                try
                {
                    stream = sr.NewClientNetworkStream(new IPEndPoint(ipAddess, portOutbound));

                    if (stream != null)
                    {
                        stream.WriteByte(Glo.SERVER_CLIENT_NUDGE);
                        stream.ReadTimeout = 1000; // Set to 5000ms originally by NewClientNetworkStream(), but that's
                                                   // overkill for this.
                        stream.ReadByte();
                        CloseStream();
                        missedNudges = 0;
                    }
                    else
                        ++missedNudges;
                }
                catch
                {
                    CloseStream();
                    ++missedNudges;
                }
            }

            if (missedNudges == maxMissedNudges)
                lock (clientSessions)
                {
                    if (clientSessions.ContainsKey(sessionID))
                        clientSessions.Remove(sessionID);
                }
        }

        private bool SendNotification(byte notificationByte)
        {
            bool sent = false;

            if (ipAddess == null)
                return sent;

            lock (streamLock)
            {
                for (int i = 0; i < notificationSendRetries; ++i)
                {
                    try
                    {
                        stream = sr.NewClientNetworkStream(new IPEndPoint(ipAddess, portOutbound));
                        if (stream != null)
                        {
                            stream.WriteByte(notificationByte);
                            CloseStream();
                            sent = true;
                            break;
                        }
                    }
                    catch
                    { CloseStream(); }

                    if (i != notificationSendRetries - 1)
                        Thread.Sleep(1_000); // Sleep for a second if we didn't get anywhere.
                }
            }

            return sent;
        }
    }
    static Dictionary<string, ClientSession> clientSessions = new Dictionary<string, ClientSession>();
    static bool CheckSessionValidity(string id, int crID)
    {
        lock (clientSessions)
        {
            return clientSessions.ContainsKey(id) && crID == columnRecordID;
        }
    }
    static bool CheckSessionPermission(ClientSession session, int category, int intent)
    {
        return session.admin || session.permissions[intent][category];
    }
    static SqlCommand PullUpUserFromPassword(string username, string password, SqlConnection sqlConnect)
    {
        return new SqlCommand(string.Format("SELECT * FROM Login WHERE (Username = '{0}' AND " +
                                            "Password = HASHBYTES('SHA2_512', '{1}'));",
                                            username, password), sqlConnect);
    }
    // If you need to check the quickly condition, but need the bool outside of scope.
    static bool CheckSessionValidity(string id, int crID, out bool result)
    {
        result = CheckSessionValidity(id, crID);
        return result;
    }
    static bool CheckSessionPermission(ClientSession session, int category, int intent, out bool result)
    {
        result = CheckSessionPermission(session, category, intent);
        return result;
    }

    // Multiple threads may try to access this at once, so hold them up if necessary.
    static object logErrorLock = new();
    private static void LogError(string context, Exception? e)
    {
        lock (logErrorLock)
        {
            try
            {
                string error = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                if (context.Length > 0)
                    error += "   " + context;
                if (e != null)
                    error += "   " + e.Message;
                File.AppendAllText(Glo.PathAgentErrorLog, error + Glo.NL);
            }
            catch { }
        }
    }
    private static void LogError(Exception e) { LogError("", e); }
    private static void LogError(string s) { LogError(s, null); }

    // For if we want to let a client know that a request failed in a catch block.
    private static void SafeFail(NetworkStream stream)
    {
        try { stream.WriteByte(Glo.CLIENT_REQUEST_FAILED); } catch { }
    }
    private static void SafeFail(NetworkStream stream, byte signal)
    {
        try { stream.WriteByte(signal); } catch { }
    }
    private static void SafeFail(NetworkStream stream, string message)
    {
        try
        {
            stream.WriteByte(Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW);
            sr.WriteAndFlush(stream, message);
        }
        catch { }
    }

    // Network Configuration
    private static int portInbound = Glo.PORT_INBOUND_DEFAULT;
    private static int portOutbound = Glo.PORT_OUTBOUND_DEFAULT;
    private static int LoadNetworkConfig()
    {
        int iVal;
        int valuesSet = 0;
        try
        {
            string[] networkConfig = File.ReadAllLines(Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_NETWORK));
            foreach (string s in networkConfig)
            {
                if (s.Length > Glo.NETWORK_SETTINGS_LENGTH && !s.StartsWith("# "))
                    if (s.StartsWith(Glo.NETWORK_SETTINGS_PORT_INBOUND))
                    {
                        if (int.TryParse(s.Substring(Glo.NETWORK_SETTINGS_LENGTH,
                                                     s.Length - Glo.NETWORK_SETTINGS_LENGTH), out iVal) &&
                            iVal >= 1025 && iVal <= 65535)
                        {
                            portInbound = iVal;
                            thisEP.Port = portInbound; // listener only references thisEP, so no need to update.
                            ++valuesSet;
                        }
                    }
                    else if (s.StartsWith(Glo.NETWORK_SETTINGS_PORT_OUTBOUND))
                    {
                        if (int.TryParse(s.Substring(Glo.NETWORK_SETTINGS_LENGTH,
                                                     s.Length - Glo.NETWORK_SETTINGS_LENGTH), out iVal) &&
                            iVal >= 1025 && iVal <= 65535)
                        {
                            if (iVal != portInbound)
                            {
                                portOutbound = iVal;
                                ++valuesSet;
                            }
                        }
                    }
            }
        }
        catch (Exception e)
        {
            LogError("Could not read network config from \"" + Glo.CONFIG_NETWORK + "\". See error:", e);
        }

        return valuesSet;
    }
    private static IPAddress thisIP = new IPAddress(new byte[] { 0, 0, 0, 0 });
    private static IPEndPoint thisEP = new IPEndPoint(thisIP, portInbound);
    private static TcpListener listener = new TcpListener(thisEP);

    static bool updatingColumnRecord = false;
    // This function has a prior implementation in DatabaseCreator, need to reduce that to just this one at some point.
    private static bool RebuildColumnRecord(SqlConnection sqlConnect)
    {
        try
        {
            if (sqlConnect.State != ConnectionState.Open)
                sqlConnect.Open();

            // Get a list of columns and their allowed values.
            SqlCommand sqlCommand = new SqlCommand("SELECT t.[name], con.[definition] " +
                                                   "FROM sys.check_constraints con " +
                                                       "LEFT OUTER JOIN sys.objects t " +
                                                           "ON con.parent_object_id = t.object_id " +
                                                       "LEFT OUTER JOIN sys.all_columns col " +
                                                           "ON con.parent_column_id = col.column_id " +
                                                           "AND con.parent_object_id = col.object_id;", sqlConnect);

            SqlDataReader reader = sqlCommand.ExecuteReader(CommandBehavior.Default);
            Dictionary<string, string[]> checkConstraints = new Dictionary<string, string[]>();

            while (reader.Read())
            {
                try
                {
                    // Line will read like something along the lines of "([Fruit]='Banana' OR [Fruit]='Apple')"
                    string table = reader.GetString(0);
                    string constraint = reader.GetString(1);
                    string column = constraint.Substring(2, constraint.IndexOf(']') - 2);
                    // SQL Server either lists or holds the constraints in the wrong order, so reverse here.
                    string[] possVals = constraint.Split(" OR [");
                    Array.Reverse(possVals);
                    for (int n = 0; n < possVals.Length; ++n)
                    {
                        possVals[n] = possVals[n].Substring(possVals[n].IndexOf('\'') + 1);
                        possVals[n] = possVals[n].Remove(possVals[n].LastIndexOf('\''));
                    }

                    // The key makes it easy to match the constraints up to their columns in the following section.
                    checkConstraints.Add(table + column, possVals);
                }
                catch { /* Just ignore the exception and press on, but there shouldn't ever be any */ }
            }
            reader.Close();

            // Get the max lengths of varchars.
            sqlCommand = new SqlCommand("SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH " +
                                        "FROM BridgeOps.INFORMATION_SCHEMA.COLUMNS " +
                                        "WHERE TABLE_NAME != 'OrganisationOrder' AND " +
                                              "TABLE_NAME != 'AssetOrder' AND " +
                                              "TABLE_NAME != 'ContactOrder' AND " +
                                              "TABLE_NAME != 'ConferenceOrder';", sqlConnect);
            reader = sqlCommand.ExecuteReader();

            List<string[]> columns = new List<string[]>();
            while (reader.Read())
            {
                string length = "";
                if (!reader.IsDBNull(3))
                {
                    int lengthInt = reader.GetInt32(3);
                    length = lengthInt.ToString();
                }
                columns.Add(new string[] { reader.GetString(0), reader.GetString(1), reader.GetString(2), length });
            }
            reader.Close();

            string fileText = (++columnRecordID).ToString() + $"{Glo.NL}^{Glo.NL}";

            int[] orderCounts = new int[4]; // Record the column counts for the four tables that require ordering.

            foreach (string[] column in columns)
            {
                if (column[0] == "Organisation")
                    ++orderCounts[0];
                else if (column[0] == "Asset")
                    ++orderCounts[1];
                else if (column[0] == "Contact")
                    ++orderCounts[2];
                else if (column[0] == "Conference")
                    ++orderCounts[3];

                fileText += column[0] + "[C]" + column[1] + "[R]";
                if (column[3] == "") // int or text
                    fileText += column[2].ToUpper();
                else if (column[2].ToUpper() == "TEXT")
                {
                    fileText += "TEXT";
                    column[3] = ""; // No need to track limit for TEXT as it's always the same.
                }
                else // varchar (char is not used by the application)
                    fileText += column[3]; // VARCHAR(MAX) will show up as -1 here.
                if (checkConstraints.ContainsKey(column[0] + column[1]))
                {
                    foreach (string s in checkConstraints[column[0] + column[1]])
                    {
                        fileText += "[A]";
                        fileText += s;
                    }
                }
                fileText += Glo.NL;
            }

            fileText += "-" + Glo.NL;

            // Get any friendly names.
            sqlCommand = new SqlCommand("SELECT * FROM FriendlyNames;", sqlConnect);
            reader = sqlCommand.ExecuteReader();
            while (reader.Read())
                if (reader.GetString(2) != "")
                    fileText += reader.GetString(0) + ";;" + reader.GetString(1) + ";;" + reader.GetString(2) + Glo.NL;
            reader.Close();

            fileText += ">" + Glo.NL;

            // Get the column order;
            void AddColumnOrder(string table, int columnCount)
            {
                --columnCount; // Just to save some calculations in the for loop. Reverted below;
                string command = "SELECT ";
                for (int i = 0; i < columnCount; ++i)
                    command += $"_{i}, ";
                command += $"_{columnCount} FROM {table}Order;";
                ++columnCount;

                sqlCommand = new SqlCommand(command, sqlConnect);
                reader = sqlCommand.ExecuteReader();

                reader.Read();
                for (int i = 0; i < columnCount; ++i)
                    fileText += $"{reader.GetInt16(i)},";
                fileText = fileText.Remove(fileText.Length - 1) + Glo.NL;
                reader.Close();
            }

            AddColumnOrder("Organisation", orderCounts[0]);
            AddColumnOrder("Asset", orderCounts[1]);
            AddColumnOrder("Contact", orderCounts[2]);
            AddColumnOrder("Conference", orderCounts[3]);

            fileText += "<" + Glo.NL; ;

            // Add a line for headers from each table.
            Dictionary<string, List<string>> headerTables = new()
                {
                    { "Organisation", new() },
                    { "Asset",  new() },
                    { "Contact",  new() },
                    { "Conference", new() }
                };
            string headerFilePath = Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_HEADERS);
            if (File.Exists(headerFilePath))
            {
                foreach (string s in File.ReadAllLines(headerFilePath))
                {
                    string[] ss = s.Split(';');
                    if (!headerTables.ContainsKey(ss[0]))
                        continue;
                    if (ss[1].Length < 0 || ss[1].Length > 128)
                        continue;
                    if (!Glo.Fun.IsValidInt(ss[2], 0, int.MaxValue))
                        continue;

                    headerTables[ss[0]].Add(ss[1] + ";" + ss[2]);
                }
            }

            fileText += string.Join(';', headerTables["Organisation"]) + Glo.NL;
            fileText += string.Join(';', headerTables["Asset"]) + Glo.NL;
            fileText += string.Join(';', headerTables["Contact"]) + Glo.NL;
            fileText += string.Join(';', headerTables["Conference"]);

            columnRecord = fileText;

            // Automatically generates a file for debugging if one isn't present.
            Glo.Fun.ExistsOrCreateFolder(Glo.Fun.ApplicationFolder());
            File.WriteAllText(Glo.Fun.ApplicationFolder(Glo.CONFIG_COLUMN_RECORD), columnRecord);

            return true;
        }
        catch (Exception e)
        {
            LogError("Couldn't create type restrictions file. See error:", e);
            return false;
        }
        finally
        {
            if (sqlConnect.State == System.Data.ConnectionState.Open)
                sqlConnect.Close();
        }
    }
    private static bool ParseColumnRecord(bool fromFile)
    {
        try
        {
            string init = fromFile ? File.ReadAllText(Glo.Fun.ApplicationFolder(Glo.CONFIG_COLUMN_RECORD)) :
                                     columnRecord;
            if (!ColumnRecord.Initialise(init))
                throw new Exception();
            else
                return true;
        }
        catch
        {
            LogError("Column record could not be initialised due to some form of corruption.");
            return false;
        }
    }

    private static void SendSingleNotification(string clientID, byte fncByte)
    {
        Thread? notificationThread = null;
        lock (clientSessions)
        {
            if (clientSessions.ContainsKey(clientID))
            {
                if (fncByte == Glo.SERVER_FORCE_LOGOUT)
                    notificationThread = new Thread(clientSessions[clientID].SendLoggedOutNotification);
            }
        }
        if (notificationThread != null)
            notificationThread.Start();
    }
    private static void SendChangeNotifications(string? initiatorClientID, byte fncByte)
    {
        lock (clientSessions)
        {
            foreach (var kvp in clientSessions)
            {
                // Skip the requestor if provided, as they'll be making their own request.
                if (initiatorClientID != null && kvp.Key == initiatorClientID)
                    continue;

                Thread notificationThread;
                if (fncByte == Glo.SERVER_COLUMN_RECORD_UPDATED)
                    notificationThread = new Thread(kvp.Value.SendColumnRecordChangeNotification);
                else if (fncByte == Glo.SERVER_RESOURCES_UPDATED)
                    notificationThread = new Thread(kvp.Value.SendResourceChangeNotification);
                else
                    continue;

                notificationThread.Start();
            }
        }
    }

    // Apart from the console generating the the database, Agent is the only one that needs access to SQL Server.
    private static string sqlServerName = Glo.SQL_SERVER_NAME_DEFAULT;
    private static string ConnectionString
    {
        get
        {
            return $"server=localhost\\{sqlServerName};" +
                    "integrated security=SSPI;" +
                    "encrypt=false;" +
                    "database=BridgeOps;" +
                    "Application Name=BridgeOpsAgent;";
        }
    }

    private static void Main(string[] args)
    {
        bool firstRun = true;

        // Test sending a command to the database to see if it works. If it doesn't, keep trying every 5 seconds.
        bool successfulSqlConnection = false;
        while (!successfulSqlConnection || !columnRecordIntact)
        {
            if (!firstRun)
                Thread.Sleep(5000);
            firstRun = false;

            Glo.Fun.ExistsOrCreateFolder(Glo.Fun.ApplicationFolder(Glo.PathConfigFiles));

            string serverNameFile = Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_SQL_SERVER_NAME);
            // Get the name of the SQL server instance.
            if (File.Exists(serverNameFile))
                sqlServerName = File.ReadAllLines(serverNameFile)[0];
            else
                LogError($"Unable to locate \"BridgeOps/{Glo.CONFIG_SQL_SERVER_NAME}\". " +
                         $"Connecting using default SLQ Server instance name: {sqlServerName}.");

            SqlConnection sqlConnect = new SqlConnection(ConnectionString);

            try
            {
                sqlConnect.Open();
                // Send a pointless but minimal query just to make sure we have a working connection.
                SqlCommand sqlCommand = new SqlCommand("SELECT TOP 1 Username FROM Login;", sqlConnect);

                // If we got this far in the try/catch, we're in business.
                successfulSqlConnection = true;

                // Catches its own exception and logs its own error.
                columnRecordIntact = RebuildColumnRecord(sqlConnect);
                if (!columnRecordIntact)
                    LogError("Couldn't rebuild the column record, retry in 5 seconds.");
                columnRecordIntact = ParseColumnRecord(false);
                if (!columnRecordIntact)
                    LogError("Couldn't parse the newly built column record, retry in 5 seconds.");
            }
            catch (Exception e)
            {
                LogError("Couldn't interact with database, retry in 5 seconds. See error:", e);
            }
            finally
            {
                if (sqlConnect.State == ConnectionState.Open)
                    sqlConnect.Close();
            }
        }

        // Read network configuraiton.
        LoadNetworkConfig();

        // Start the thread responsible for nudging SQL Server. Remove this at some point.
        Thread sqlNudgeThr = new Thread(SqlServerNudge);
        sqlNudgeThr.Start();

        // Start the thread responsible for nudging clients to see if they're still there.
        Thread clientNudgeThr = new Thread(ClientNudge);
        clientNudgeThr.Start();

        // Start the thread responsible for handling requests from the server console.
        Thread bridgeOpsConsoleRequestsThr = new Thread(BridgeOpsConsoleRequests);
        bridgeOpsConsoleRequestsThr.Start();

        // Start the thread responsible for handling requests from the clients.
        Thread bridgeOpsLocalClientRequestsThr = new Thread(BridgeOpsClientRequests);
        bridgeOpsLocalClientRequestsThr.Start();
    }


    //   T H R E A D   F U N C T I O N S

    private static void SqlServerNudge()
    {
        // If the database is inactive for more than a few minutes, we see a very slight delay to the next query. This
        // causes logins to fail for some inexplicable reason when developing on one machine.

        SqlConnection sqlConnect = new SqlConnection(ConnectionString);
        while (true)
        {
            Thread.Sleep(120_000); // Sleep for two minutes.
            try
            {
                sqlConnect.Open();
                // Carry out the most lightweight query I can think of.
                SqlCommand com = new SqlCommand("SELECT TOP 1 Login_ID FROM Login;", sqlConnect);
                com.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                LogError("Could not nudge SQL Server. See error:", e);
            }
            finally
            {
                sqlConnect.Close();
            }
        }
    }
    private static void ClientNudge()
    {
        while (true)
        {
            Thread.Sleep(clientNudgeInterval);

            lock (clientSessions)
            {
                foreach (var kvp in clientSessions)
                {
                    Thread clientNudgeThread = new Thread(kvp.Value.SendNudge);
                    clientNudgeThread.Start();
                }
            }
        }
    }

    private static void BridgeOpsConsoleRequests()
    {
        NamedPipeServerStream server = sr.NewServerNamedPipe(Glo.PIPE_CONSOLE);
        while (true)
        {
            server.WaitForConnection();

            try
            {
                int fncByte = server.ReadByte();
                if (fncByte == Glo.CONSOLE_CLIENT_LIST)
                    ConsoleClientList(server);
                else if (fncByte == Glo.CONSOLE_LOGOUT_USER)
                    ConsoleLogoutUser(server);

                else
                    throw new Exception("Received int " + fncByte + " does not correspond to a function.");
            }
            catch (Exception e)
            {
                LogError(e);
            }
            finally
            {
                if (server.IsConnected)
                    server.Disconnect();
            }
        }
    }

    private static void BridgeOpsClientRequests()
    {
        try
        {
            listener.Start();
        }
        catch (Exception e)
        {
            LogError("TcpListener could not start, see error:", e);
        }

        while (true)
        {
            // No idea why this has to be here, but for some reason the client receives a string length of 0 after the
            // agent WriteAndFlush()es when logging in. If there's a small sleep here or in the listener thread, the
            // issue goes away. Only logins are affected, everything else works perfectly. I'm also hoping this only
            // affects the client when running on the same machine as the agent, so this will need testing again when
            // development shifts to using two machines.
            Thread.Sleep(50);

            // Start an ASync Accept.
            IAsyncResult result = listener.BeginAcceptTcpClient(HandleClientListenAccept, listener);
            // [seems I accidentally deleted the end of the comment below, need to remember what this does...]
            // Do not accept any
            autoResetEvent.WaitOne();
            autoResetEvent.Reset();
        }
    }
    private static AutoResetEvent autoResetEvent = new AutoResetEvent(false);
    private static void HandleClientListenAccept(IAsyncResult result)
    {
        // I believe each thread will need its own dedicated SqlConnection object.
        SqlConnection sqlConnect = new SqlConnection(ConnectionString);

        if (result.AsyncState != null)
        {
            TcpListener listener = (TcpListener)result.AsyncState;
            TcpClient client = listener.EndAcceptTcpClient(result);

            NetworkStream stream = client.GetStream();
            autoResetEvent.Set();

            try
            {
                // Move this ridiculousness to a dictionary at some point.

                int fncByte = stream.ReadByte();
                if (fncByte == Glo.CLIENT_PULL_COLUMN_RECORD)
                    ClientPullColumnRecord(stream);
                else if (fncByte == Glo.CLIENT_PULL_USER_SETTINGS)
                    ClientPullUserSettings(stream, sqlConnect);
                else if (fncByte == Glo.CLIENT_LOGIN)
                    ClientLogin(stream, sqlConnect, (IPEndPoint?)client.Client.RemoteEndPoint);
                else if (fncByte == Glo.CLIENT_LOGOUT)
                    ClientLogout(stream, sqlConnect);
                else if (fncByte == Glo.CLIENT_PASSWORD_RESET)
                    ClientPasswordReset(stream, sqlConnect);
                else if (fncByte == Glo.CLIENT_LOGGEDIN_LIST)
                    ClientLoggedInUserList(stream);
                else if (fncByte == Glo.CLIENT_NEW_ORGANISATION ||
                         fncByte == Glo.CLIENT_NEW_CONTACT ||
                         fncByte == Glo.CLIENT_NEW_ASSET ||
                         fncByte == Glo.CLIENT_NEW_CONFERENCE_TYPE ||
                         fncByte == Glo.CLIENT_NEW_CONFERENCE ||
                         fncByte == Glo.CLIENT_NEW_RESOURCE ||
                         fncByte == Glo.CLIENT_NEW_LOGIN)
                    ClientNewInsert(stream, sqlConnect, fncByte);
                else if (fncByte == Glo.CLIENT_UPDATE)
                    ClientUpdate(stream, sqlConnect);
                else if (fncByte == Glo.CLIENT_UPDATE_ORGANISATION ||
                         fncByte == Glo.CLIENT_UPDATE_CONTACT ||
                         fncByte == Glo.CLIENT_UPDATE_ASSET ||
                         fncByte == Glo.CLIENT_UPDATE_CONFERENCE_TYPE ||
                         fncByte == Glo.CLIENT_UPDATE_CONFERENCE ||
                         fncByte == Glo.CLIENT_UPDATE_RESOURCE ||
                         fncByte == Glo.CLIENT_UPDATE_LOGIN ||
                         fncByte == Glo.CLIENT_UPDATE_CHANGE_REASON)
                    ClientUpdate(stream, sqlConnect, fncByte);
                else if (fncByte == Glo.CLIENT_SELECT_COLUMN_PRIMARY)
                    ClientSelectColumnPrimary(stream, sqlConnect);
                else if (fncByte == Glo.CLIENT_SELECT_QUICK)
                    ClientSelectQuick(stream, sqlConnect);
                else if (fncByte == Glo.CLIENT_SELECT)
                    ClientSelect(stream, sqlConnect);
                else if (fncByte == Glo.CLIENT_SELECT_WIDE)
                    ClientSelectWide(stream, sqlConnect);
                else if (fncByte == Glo.CLIENT_DELETE)
                    ClientDelete(stream, sqlConnect);
                else if (fncByte == Glo.CLIENT_LINK_CONTACT)
                    ClientLinkContact(stream, sqlConnect);
                else if (fncByte == Glo.CLIENT_LINKED_CONTACT_SELECT)
                    ClientLinkedContactSelect(stream, sqlConnect);
                else if (fncByte == Glo.CLIENT_SELECT_HISTORY)
                    ClientSelectHistory(stream, sqlConnect);
                else if (fncByte == Glo.CLIENT_SELECT_HISTORICAL_RECORD)
                    ClientBuildHistoricalRecord(stream, sqlConnect);
                else if (fncByte == Glo.CLIENT_TABLE_MODIFICATION)
                    ClientColumnModification(stream, sqlConnect);
                else if (fncByte == Glo.CLIENT_COLUMN_ORDER_UPDATE)
                    ClientColumnOrderUpdate(stream, sqlConnect);
                else if (fncByte == Glo.CLIENT_SELECT_BUILDER_PRESET_SAVE)
                    ClientSaveSelectBuilderPreset(stream);
                else if (fncByte == Glo.CLIENT_SELECT_BUILDER_PRESET_LOAD)
                    ClientLoadSelectBuilder(stream);
                else if (fncByte == Glo.CLIENT_SELECT_BUILDER_PRESET_DELETE)
                    ClientDeleteSelectBuilderPreset(stream);
                else if (fncByte == Glo.CLIENT_SELECT_BUILDER_PRESET_RENAME)
                    ClientRenameSelectBuilderPreset(stream);
                else if (fncByte == Glo.CLIENT_CONFERENCE_VIEW_SEARCH)
                    ClientConferenceViewSearch(stream, sqlConnect);
                else if (fncByte == Glo.CLIENT_CONFERENCE_SELECT)
                    ClientConferenceSelect(stream, sqlConnect);
                else if (fncByte == Glo.CLIENT_CONFERENCE_CANCEL)
                    ClientConferenceCancel(stream, sqlConnect);
                else
                {
                    stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);
                    throw new Exception("Received int " + fncByte + " does not correspond to a function.");
                }
            }
            catch (Exception e)
            {
                LogError(e);
            }
            finally
            {
                if (client.Connected)
                    client.Close();
            }
        }
    }


    //   C O N S O L E   R E Q U E S T   F U N C T I O N S

    private static void ConsoleClientList(NamedPipeServerStream server)
    {
        lock (clientSessions)
        {
            ConnectedClients connectedClients = new ConnectedClients();
            foreach (KeyValuePair<string, ClientSession> client in clientSessions)
                connectedClients.Add(client.Value.ipString, client.Value.username);

            sr.WriteAndFlush(server, sr.Serialise(connectedClients));
        }
    }

    private static void ConsoleLogoutUser(NamedPipeServerStream server)
    {
        string username = sr.ReadString(server);
        string sessionId = "";
        lock (clientSessions)
        {
            foreach (KeyValuePair<string, ClientSession> client in clientSessions)
                if (client.Value.username == username)
                    sessionId = client.Key;
            if (sessionId.Length > 0 && clientSessions.ContainsKey(sessionId))
            {
                SendSingleNotification(sessionId, Glo.SERVER_FORCE_LOGOUT);
                clientSessions.Remove(sessionId);
                server.WriteByte(0);
                server.Flush();
                return;
            }

            server.WriteByte(1);
            server.Flush();
        }
    }

    private static void ConsoleResetAdminPassword(NamedPipeServerStream server, SqlConnection sqlConnect)
    {
        try
        {
            sqlConnect.Open();
            SqlCommand comm = new("UPDATE " +
                                 $"SET {Glo.Tab.LOGIN_PASSWORD} = {SqlAssist.HashBytes("admin")} " +
                                 $"WHERE {Glo.Tab.LOGIN_ID} = 1;",
                                 sqlConnect);
            comm.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            LogError(e);
        }
    }


    //   C L I E N T   R E Q U E S T   F U N C T I O N S

    // Multiple threads may be pulling the column record at once, so this action can't be tracked as a bool. Instead,

    // increment and decrement the count, and the agent will only attempt to update the record if the value is 0.
    static int pullingColumnRecord = 0;
    private static void ClientPullColumnRecord(NetworkStream stream)
    {
        while (updatingColumnRecord)
            Thread.Sleep(10);

        ++pullingColumnRecord;

        try
        {
            lock (clientSessions)
            {
                string id = sr.ReadString(stream);
                if (!CheckSessionValidity(id, columnRecordID))
                {
                    stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                    --pullingColumnRecord;
                    return;
                }
                else
                    clientSessions[id].columnRecordChangeNotificationUnsent = false;
            }

            if (columnRecordIntact)
            {
                stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
                sr.WriteAndFlush(stream, columnRecord);
            }
            else
                stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);
        }
        catch (Exception e)
        {
            LogError("Could not read or send type restrictions file. See error: ", e);
        }

        --pullingColumnRecord;
    }

    private static void ClientPullUserSettings(NetworkStream stream, SqlConnection sqlConnect)
    {
        try
        {
            int loginID;
            lock (clientSessions)
            {
                string sessionID = sr.ReadString(stream);
                if (!CheckSessionValidity(sessionID, columnRecordID))
                {
                    sr.WriteAndFlush(stream, "Failed"); // This will simply fail to be read by the client.
                    return;
                }
                else
                    loginID = clientSessions[sessionID].loginID;
            }

            sqlConnect.Open();

            string settings = "";
            SqlCommand command = new SqlCommand($"SELECT {Glo.Tab.LOGIN_VIEW_SETTINGS} FROM Login " +
                                                $"WHERE {Glo.Tab.LOGIN_ID} = {loginID}", sqlConnect);
            SqlDataReader reader = command.ExecuteReader();
            reader.Read();
            if (!reader.IsDBNull(0))
                settings = reader.GetString(0);
            sr.WriteAndFlush(stream, settings);
        }
        catch (Exception e)
        {
            LogError("Could not pull user settings. See error", e);
        }
        finally
        {
            if (sqlConnect.State == ConnectionState.Open)
                sqlConnect.Close();
        }
    }

    private static void ClientLogin(NetworkStream stream, SqlConnection sqlConnect, IPEndPoint? ep)
    {
        string credentials = sr.ReadString(stream);
        LoginRequest loginReq = sr.Deserialise<LoginRequest>(credentials);
        Random rnd = new Random();

        if (ep == null)
        {
            sr.WriteAndFlush(stream, Glo.CLIENT_LOGIN_REJECT_IP_UNKNOWN);
            return;
        }
        string ipStr = ep.Address.ToString();

        try
        {
            // Identify any duplicate IPs and users for removal from open sessions if login is successful.
            lock (clientSessions)
            {
                // Prepare variables for if/else block below.
                string userAlreadyLoggedIn = "";
                string ipAlreadyConnected = "";
                foreach (KeyValuePair<string, ClientSession> cs in clientSessions)
                {
                    if (userAlreadyLoggedIn == "" && cs.Value.username == loginReq.username)
                        userAlreadyLoggedIn = cs.Key;
                    if (ipAlreadyConnected == "" && cs.Value.ipString == ipStr)
                        ipAlreadyConnected = cs.Key;

                    if (userAlreadyLoggedIn != "" && ipAlreadyConnected != "")
                        break;
                }

                sqlConnect.Open();
                SqlCommand sqlCommand = PullUpUserFromPassword(loginReq.username, loginReq.password, sqlConnect);

                SqlDataReader reader = sqlCommand.ExecuteReader();
                if (reader.Read())
                {
                    int loginID = Convert.ToInt32(reader.GetValue(0)); // Has to be an int if anything was returned.

                    // Assuming login was successful, kick out any sessions with clashing IPs or usernames.
                    if (userAlreadyLoggedIn != "")
                        clientSessions.Remove(userAlreadyLoggedIn);
                    if (ipAlreadyConnected != "" && ipAlreadyConnected != userAlreadyLoggedIn)
                        clientSessions.Remove(ipAlreadyConnected);

                    object enabled = reader.GetValue(7);
                    if (!(bool)(reader.GetValue(7)))
                    {
                        sr.WriteAndFlush(stream, Glo.CLIENT_LOGIN_REJECT_USER_DISABLED);
                        return;
                    }

                    // Generate a random unique session ID.
                    string key;
                    do
                    {
                        key = "";
                        for (int n = 0; n < 16; ++n)
                            key += (char)(rnd.Next() % 256);
                    }
                    while (clientSessions.ContainsKey(key));

                    bool admin = (bool)reader.GetValue(3);
                    byte createPermissions = (byte)reader.GetValue(4);
                    byte editPermissions = (byte)reader.GetValue(5);
                    byte deletePermissions = (byte)reader.GetValue(6);

                    clientSessions.Add(key, new ClientSession(loginReq.username, ipStr, loginID, key, admin,
                                                              createPermissions,
                                                              editPermissions,
                                                              deletePermissions));

                    if (clientSessions[key].ipAddess == null)
                        throw new Exception();

                    sr.WriteAndFlush(stream, Glo.CLIENT_LOGIN_ACCEPT + key);
                    stream.WriteByte((byte)(admin ? 1 : 0));
                    stream.WriteByte(createPermissions);
                    stream.WriteByte(editPermissions);
                    stream.WriteByte(deletePermissions);
                    sr.WriteAndFlush(stream, loginID.ToString());

                }
                else // Username or password incorrect.
                    sr.WriteAndFlush(stream, Glo.CLIENT_LOGIN_REJECT_USER_INVALID);
            }
        }
        catch (Exception e)
        {
            LogError(e);
        }
        finally
        {
            sqlConnect.Close();
        }
    }

    // Permission restricted.
    private static void ClientPasswordReset(NetworkStream stream, SqlConnection sqlConnect)
    {
        SqlCommand com;

        try
        {
            ClientSession session;
            PasswordResetRequest req;

            lock (clientSessions)
            {
                req = sr.Deserialise<PasswordResetRequest>(sr.ReadString(stream));
                if (!CheckSessionValidity(req.sessionID, columnRecordID))
                {
                    stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                    return;
                }

                // Must be here if the above code works.
                session = clientSessions[req.sessionID];

                // Confirm that the request is from a session with the correct permissions, or is owned by the account
                // whos password is being changed.
                if (req.loginID != session.loginID &&
                    !CheckSessionPermission(session, Glo.PERMISSION_USER_ACC_MGMT, Glo.PERMISSION_EDIT))
                {
                    stream.WriteByte(Glo.CLIENT_INSUFFICIENT_PERMISSIONS);
                    return;
                }

                sqlConnect.Open();

                // If the session isn't admin, check to see if the original password was correct.
                if (!CheckSessionPermission(session, Glo.PERMISSION_USER_ACC_MGMT, Glo.PERMISSION_EDIT) ||
                    !req.userManagementMenu)
                {
                    com = PullUpUserFromPassword(session.username, req.password, sqlConnect);
                    SqlDataReader reader = com.ExecuteReader();
                    if (!reader.Read())
                    {
                        stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);
                        return;
                    }
                    reader.Close();
                }
            }
            // If we're still good to go, set the new password.
            com = new SqlCommand(req.SqlUpdate(), sqlConnect);
            com.ExecuteNonQuery();
            stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
        }
        catch (Exception e)
        {
            LogError(e);
        }
        finally
        {
            sqlConnect.Close();
        }
    }

    private static void ClientLoggedInUserList(NetworkStream stream)
    {
        try
        {
            if (CheckSessionValidity(sr.ReadString(stream), columnRecordID))
            {
                List<object[]> users = new();
                lock (clientSessions)
                {
                    foreach (KeyValuePair<string, ClientSession> session in clientSessions)
                        users.Add(new object[] { session.Value.loginID, session.Value.username });
                }
                sr.WriteAndFlush(stream, sr.Serialise(users));
            }
        }
        catch (Exception e)
        {
            LogError(e);
        }
    }

    // Permission restricted.
    private static void ClientLogout(NetworkStream stream, SqlConnection sqlConnect)
    {
        try
        {
            string sessionDetails = sr.ReadString(stream);
            LogoutRequest logoutReq = sr.Deserialise<LogoutRequest>(sessionDetails);

            lock (clientSessions)
            {
                if (clientSessions.ContainsKey(logoutReq.sessionID))
                {
                    ClientSession thisSession = clientSessions[logoutReq.sessionID];
                    if (clientSessions[logoutReq.sessionID].loginID == logoutReq.loginID)
                    {
                        if (logoutReq.settings != null)
                        {
                            logoutReq.settings = SqlAssist.SecureValue(logoutReq.settings);
                            sqlConnect.Open();
                            SqlCommand command = new("UPDATE Login " +
                                                    $"SET {Glo.Tab.LOGIN_VIEW_SETTINGS} = '{logoutReq.settings}' " +
                                                    $"WHERE {Glo.Tab.LOGIN_ID} = {logoutReq.loginID};", sqlConnect);
                            if (command.ExecuteNonQuery() == 0)
                                LogError($"Could not update user settings for login {logoutReq.loginID}.");
                        }

                        // Users may log themselves out without restriction.
                        clientSessions.Remove(logoutReq.sessionID);
                        sr.WriteAndFlush(stream, Glo.CLIENT_LOGOUT_ACCEPT);
                        return;
                    }

                    // If a user is logging out another user, they must have the required permissions.
                    if (CheckSessionPermission(thisSession, Glo.PERMISSION_USER_ACC_MGMT, Glo.PERMISSION_EDIT))
                    {
                        string sessionToLogOut = "";
                        foreach (KeyValuePair<string, ClientSession> session in clientSessions)
                        {
                            if (session.Value.loginID == logoutReq.loginID)
                            {
                                sessionToLogOut = session.Key;
                                break;
                            }
                        }

                        if (sessionToLogOut == "")
                        {
                            sr.WriteAndFlush(stream, Glo.CLIENT_LOGOUT_SESSION_NOT_FOUND);
                            return;
                        }

                        SendSingleNotification(sessionToLogOut, Glo.SERVER_FORCE_LOGOUT);
                        clientSessions.Remove(sessionToLogOut);
                        sr.WriteAndFlush(stream, Glo.CLIENT_LOGOUT_ACCEPT);
                        return;
                    }
                    else
                    {
                        sr.WriteAndFlush(stream, Glo.CLIENT_INSUFFICIENT_PERMISSIONS.ToString());
                        return;
                    }
                }
                else
                    sr.WriteAndFlush(stream, Glo.CLIENT_LOGOUT_SESSION_INVALID);
            }
        }
        catch (Exception e)
        {
            LogError(e);
        }
        finally
        {
            stream.Close();
            if (sqlConnect.State == ConnectionState.Open)
                sqlConnect.Close();
        }
    }

    // Permission restricted.
    private static void ClientNewInsert(NetworkStream stream, SqlConnection sqlConnect, int target)
    {
        try
        {
            sqlConnect.Open();
            SqlCommand com = new SqlCommand("", sqlConnect);

            bool sessionValid = false;
            bool permission = false;
            int create = Glo.PERMISSION_CREATE;

            lock (clientSessions)
            {
                if (target == Glo.CLIENT_NEW_ORGANISATION)
                {
                    Organisation newRow = sr.Deserialise<Organisation>(sr.ReadString(stream));
                    if (CheckSessionValidity(newRow.sessionID, newRow.columnRecordID, out sessionValid) &&
                        CheckSessionPermission(clientSessions[newRow.sessionID], Glo.PERMISSION_RECORDS,
                        create, out permission))
                        com.CommandText = newRow.SqlInsert(clientSessions[newRow.sessionID].loginID);
                }
                else if (target == Glo.CLIENT_NEW_ASSET)
                {
                    Asset newRow = sr.Deserialise<Asset>(sr.ReadString(stream));
                    if (CheckSessionValidity(newRow.sessionID, newRow.columnRecordID, out sessionValid) &&
                        CheckSessionPermission(clientSessions[newRow.sessionID], Glo.PERMISSION_RECORDS,
                        create, out permission))
                        com.CommandText = newRow.SqlInsert(clientSessions[newRow.sessionID].loginID);
                }
                else if (target == Glo.CLIENT_NEW_CONTACT)
                {
                    Contact newRow = sr.Deserialise<Contact>(sr.ReadString(stream));
                    if (CheckSessionValidity(newRow.sessionID, newRow.columnRecordID, out sessionValid) &&
                        CheckSessionPermission(clientSessions[newRow.sessionID], Glo.PERMISSION_RECORDS,
                        create, out permission))
                        com.CommandText = newRow.SqlInsert();
                }
                else if (target == Glo.CLIENT_NEW_CONFERENCE)
                {
                    Conference newRow = sr.Deserialise<Conference>(sr.ReadString(stream));
                    if (CheckSessionValidity(newRow.sessionID, newRow.columnRecordID, out sessionValid) &&
                        CheckSessionPermission(clientSessions[newRow.sessionID], Glo.PERMISSION_CONFERENCES,
                        create, out permission))
                        com.CommandText = newRow.SqlInsert();
                }
                else if (target == Glo.CLIENT_NEW_RESOURCE)
                {
                    Resource newRow = sr.Deserialise<Resource>(sr.ReadString(stream));
                    if (CheckSessionValidity(newRow.sessionID, newRow.columnRecordID, out sessionValid) &&
                        CheckSessionPermission(clientSessions[newRow.sessionID], Glo.PERMISSION_RESOURCES,
                        create, out permission))
                        com.CommandText = newRow.SqlInsert();
                }
                else if (target == Glo.CLIENT_NEW_LOGIN)
                {
                    Login newRow = sr.Deserialise<Login>(sr.ReadString(stream));
                    if (CheckSessionValidity(newRow.sessionID, newRow.columnRecordID, out sessionValid) &&
                        CheckSessionPermission(clientSessions[newRow.sessionID], Glo.PERMISSION_USER_ACC_MGMT,
                        create, out permission))
                        com.CommandText = newRow.SqlInsert();
                }
            }

            if (!sessionValid)
            {
                stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                return;
            }

            if (!permission)
            {
                stream.WriteByte(Glo.CLIENT_INSUFFICIENT_PERMISSIONS);
                return;
            }

            // Contact inserts work slightly differently, as we need to get the ID back out.
            if (target == Glo.CLIENT_NEW_CONTACT)
            {
                SqlParameter id = new SqlParameter("@ID", SqlDbType.Int);
                id.Direction = ParameterDirection.Output;
                com.Parameters.Add(id);

                if (com.ExecuteNonQuery() == 0)
                    stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);
                else if (com.Parameters["@ID"].Value.GetType() == typeof(DBNull))
                    stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
                else
                {
                    stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS_MORE_TO_FOLLOW);
                    string? idStr = Convert.ToInt32(com.Parameters["@ID"].Value).ToString();
                    sr.WriteAndFlush(stream, idStr == null ? "" : idStr);
                }
            }
            else
            {
                if (com.ExecuteNonQuery() == 0)
                    stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);
                else
                {
                    stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
                    if (target == Glo.CLIENT_NEW_RESOURCE)
                        SendChangeNotifications(null, Glo.SERVER_RESOURCES_UPDATED);
                }
            }
        }
        catch (Exception e)
        {
            LogError("Couldn't create new contact. See error:", e);
            if (e.Message.Contains("FOREIGN KEY"))
                SafeFail(stream, Glo.CLIENT_REQUEST_FAILED_FOREIGN_KEY);
            else if (e.Message.Contains("PRIMARY KEY"))
                SafeFail(stream, "Record ID already exists.");
            else
                SafeFail(stream, e.Message);
        }
        finally
        {
            if (sqlConnect.State == System.Data.ConnectionState.Open)
                sqlConnect.Close();
            stream.Close();
        }
    }

    private static void ClientSelectColumnPrimary(NetworkStream stream, SqlConnection sqlConnect)
    {
        try
        {
            sqlConnect.Open();
            PrimaryColumnSelect pcs = sr.Deserialise<PrimaryColumnSelect>(sr.ReadString(stream));

            if (!CheckSessionValidity(pcs.sessionID, pcs.columnRecordID))
            {
                stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                return;
            }

            // Reject cheekiness
            if (pcs.table.Contains('\'') || pcs.table.Contains(';') ||
                pcs.column.Contains('\'') || pcs.column.Contains(';'))
                stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);

            SqlCommand com = new SqlCommand("SELECT " + pcs.column + " FROM " + pcs.table + ";", sqlConnect);
            SqlDataReader reader = com.ExecuteReader();
            StringBuilder result = new();
            while (reader.Read())
                result.Append(reader.GetString(0) + ';');
            // Remove the trailing semicolon.
            if (result.Length > 0)
                result = result.Remove(result.Length - 1, 1);
            stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
            sr.WriteAndFlush(stream, result.ToString());
        }
        catch (Exception e)
        {
            LogError("Couldn't run query, see error:", e);
            SafeFail(stream);
        }
        finally
        {
            if (sqlConnect.State == System.Data.ConnectionState.Open)
                sqlConnect.Close();
        }
    }

    private static void ClientSelectQuick(NetworkStream stream, SqlConnection sqlConnect)
    {
        try
        {
            sqlConnect.Open();
            QuickSelectRequest req = sr.Deserialise<QuickSelectRequest>(sr.ReadString(stream));
            if (!CheckSessionValidity(req.sessionID, req.columnRecordID))
            {
                stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                return;
            }

            if (req.Validate())
                throw new Exception("Value lists not of equal length.");
            req.Prepare();

            // For a historical search, we'll want to build  our commands a little differently. First we need to get a
            // list of unique IDs.
            bool historical = req.includeHistory && (req.table == "Organisation" || req.table == "Asset");
            string command = "";
            if (historical)
                command = "SELECT DISTINCT " + (req.table == "Organisation" ?
                                                Glo.Tab.ORGANISATION_REF :
                                                Glo.Tab.ASSET_REF)
                                                + " FROM " + req.table + "Change";
            else
                command = "SELECT " + string.Join(", ", req.select) + " FROM " + req.table;

            List<string> conditions = new();
            if (req.likeColumns.Count > 0)
            {
                for (int i = 0; i < req.likeColumns.Count; ++i)
                {
                    // If the search is blank, we want to return everything, including null values.
                    if (req.likeValues[i] != "")
                    {
                        if (req.conditionals[i] == Conditional.Like)
                            conditions.Add(req.likeColumns[i] + " LIKE '%" + req.likeValues[i] + "%'");
                        else // if Conditional.Equals
                            conditions.Add(req.likeColumns[i] + " = '" + req.likeValues[i] + "'");
                    }
                }

                if (conditions.Count > 0)
                    command += " WHERE " + string.Join(req.and ? " AND " : " OR ", conditions);
            }

            if (historical)
                command = "SELECT " + string.Join(", ", req.select) + " FROM " + req.table +
                          " WHERE " + (req.table == "Organisation" ? Glo.Tab.ORGANISATION_REF : Glo.Tab.ASSET_REF) +
                          " IN (" + command + ");";

            SqlCommand com = new SqlCommand(command, sqlConnect);

            SelectResult result = new(com.ExecuteReader());
            stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
            sr.WriteAndFlush(stream, sr.Serialise(result));
        }
        catch (Exception e)
        {
            LogError("Couldn't run or return query, see error:", e);
            SafeFail(stream);
        }
        finally
        {
            if (sqlConnect.State == System.Data.ConnectionState.Open)
                sqlConnect.Close();
        }
    }

    private static void ClientSelect(NetworkStream stream, SqlConnection sqlConnect)
    {
        // This function does not support historical searches.

        try
        {
            sqlConnect.Open();
            SelectRequest req = sr.Deserialise<SelectRequest>(sr.ReadString(stream));

            if (!CheckSessionValidity(req.sessionID, req.columnRecordID))
            {
                stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                return;
            }

            SqlCommand com = new SqlCommand(req.SqlSelect(), sqlConnect);

            SelectResult result = new(com.ExecuteReader());
            stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
            sr.WriteAndFlush(stream, sr.Serialise(result));
        }
        catch (Exception e)
        {
            SafeFail(stream, e.Message);
        }
        finally
        {
            if (sqlConnect.State == ConnectionState.Open)
                sqlConnect.Close();
        }
    }

    private static void ClientSelectWide(NetworkStream stream, SqlConnection sqlConnect)
    {
        try
        {
            sqlConnect.Open();
            SelectWideRequest req = sr.Deserialise<SelectWideRequest>(sr.ReadString(stream));
            if (!CheckSessionValidity(req.sessionID, req.columnRecordID))
            {
                stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                return;
            }

            req.Prepare();

            // Get the list of columns to search, reject if the table was invalid.
            OrderedDictionary columns;
            if (req.table == "Organisation")
                columns = ColumnRecord.organisation;
            else if (req.table == "Asset")
                columns = ColumnRecord.asset;
            else if (req.table == "Contact")
                columns = ColumnRecord.contact;
            else
            {
                stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);
                return;
            }

            bool historical = req.includeHistory && (req.table == "Organisation" || req.table == "Asset");
            string command = "";
            if (historical)
                command = "SELECT DISTINCT " + (req.table == "Organisation" ?
                                                Glo.Tab.ORGANISATION_REF :
                                                Glo.Tab.ASSET_REF)
                                                + " FROM " + req.table + "Change";
            else
                command = "SELECT " + string.Join(", ", req.select) + " FROM " + req.table;

            List<string> conditions = new();
            if (req.value != "") // In that case, just select everything.
            {
                foreach (DictionaryEntry de in columns)
                    if (ColumnRecord.IsTypeString((ColumnRecord.Column)de.Value!))
                        conditions.Add(de.Key + " LIKE \'%" + req.value + "%'");

                command += " WHERE " + string.Join(" OR ", conditions);
            }

            if (historical)
                command = "SELECT " + string.Join(", ", req.select) + " FROM " + req.table +
                          " WHERE " + (req.table == "Organisation" ? Glo.Tab.ORGANISATION_REF : Glo.Tab.ASSET_REF) +
                          " IN (" + command + ");";

            SqlCommand com = new SqlCommand(command, sqlConnect);

            SelectResult result = new(com.ExecuteReader());
            stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
            sr.WriteAndFlush(stream, sr.Serialise(result));
        }
        catch (Exception e)
        {
            LogError("Couldn't run or return query, see error:", e);
            SafeFail(stream);
        }
        finally
        {
            if (sqlConnect.State == System.Data.ConnectionState.Open)
                sqlConnect.Close();
        }
    }

    // Permission restricted.
    private static void ClientUpdate(NetworkStream stream, SqlConnection sqlConnect, int target)
    {
        try
        {
            sqlConnect.Open();
            SqlCommand com = new SqlCommand("", sqlConnect);

            bool sessionValid = false;
            bool permission = false;
            int edit = Glo.PERMISSION_EDIT;

            lock (clientSessions)
            {
                if (target == Glo.CLIENT_UPDATE_ORGANISATION)
                {
                    Organisation update = sr.Deserialise<Organisation>(sr.ReadString(stream));
                    // If the organisation reference is being changed, reject if not admin off the bat.
                    if (update.organisationRefChanged)
                        permission = clientSessions[update.sessionID].admin;
                    if (CheckSessionValidity(update.sessionID, update.columnRecordID, out sessionValid) &&
                        CheckSessionPermission(clientSessions[update.sessionID], Glo.PERMISSION_RECORDS, edit,
                        out permission))
                        com.CommandText = update.SqlUpdate(clientSessions[update.sessionID].loginID);
                }
                else if (target == Glo.CLIENT_UPDATE_ASSET)
                {
                    Asset update = sr.Deserialise<Asset>(sr.ReadString(stream));
                    // If the asset reference is being changed, reject if not admin off the bat.
                    if (update.assetRefChanged)
                        permission = clientSessions[update.sessionID].admin;
                    if (CheckSessionValidity(update.sessionID, update.columnRecordID, out sessionValid) &&
                        CheckSessionPermission(clientSessions[update.sessionID], Glo.PERMISSION_RECORDS, edit,
                        out permission))
                        com.CommandText = update.SqlUpdate(clientSessions[update.sessionID].loginID);
                }
                else if (target == Glo.CLIENT_UPDATE_CONTACT)
                {
                    Contact update = sr.Deserialise<Contact>(sr.ReadString(stream));
                    if (CheckSessionValidity(update.sessionID, update.columnRecordID, out sessionValid) &&
                        CheckSessionPermission(clientSessions[update.sessionID], Glo.PERMISSION_RECORDS, edit,
                        out permission))
                        com.CommandText = update.SqlUpdate();
                }
                else if (target == Glo.CLIENT_UPDATE_CONFERENCE)
                {
                    Conference update = sr.Deserialise<Conference>(sr.ReadString(stream));
                    if (CheckSessionValidity(update.sessionID, update.columnRecordID, out sessionValid) &&
                        CheckSessionPermission(clientSessions[update.sessionID], Glo.PERMISSION_CONFERENCES, edit,
                        out permission))
                        com.CommandText = update.SqlUpdate();
                }
                else if (target == Glo.CLIENT_UPDATE_RESOURCE)
                {
                    // Make sure, when you get around to implementing this, that you check for permissions (as above).
                    Resource update = sr.Deserialise<Resource>(sr.ReadString(stream));
                    //if (CheckSessionValidity(update.sessionID, update.columnRecordID, out sessionValid))
                    //    com.CommandText = update.SqlUpdate();
                }
                else if (target == Glo.CLIENT_UPDATE_LOGIN)
                {
                    Login update = sr.Deserialise<Login>(sr.ReadString(stream));
                    if (CheckSessionValidity(update.sessionID, update.columnRecordID, out sessionValid) &&
                        CheckSessionPermission(clientSessions[update.sessionID], Glo.PERMISSION_USER_ACC_MGMT, edit,
                        out permission))
                        com.CommandText = update.SqlUpdate();
                }
                else if (target == Glo.CLIENT_UPDATE_CHANGE_REASON)
                {
                    ChangeReasonUpdate update = sr.Deserialise<ChangeReasonUpdate>(sr.ReadString(stream));
                    if (CheckSessionValidity(update.sessionID, update.columnRecordID, out sessionValid) &&
                        CheckSessionPermission(clientSessions[update.sessionID], Glo.PERMISSION_USER_ACC_MGMT, edit,
                        out permission))
                        com.CommandText = update.SqlUpdate();
                }
            }

            if (!sessionValid)
            {
                stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                return;
            }

            if (!permission)
            {
                stream.WriteByte(Glo.CLIENT_INSUFFICIENT_PERMISSIONS);
                return;
            }

            com.ExecuteNonQuery();
            if (target == Glo.CLIENT_UPDATE_RESOURCE)
                SendChangeNotifications(null, Glo.SERVER_RESOURCES_UPDATED);
            stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
        }
        catch (Exception e)
        {
            LogError("Couldn't run update, see error:", e);
            SafeFail(stream, e.Message);
        }
        finally
        {
            stream.Close();
            if (sqlConnect.State == System.Data.ConnectionState.Open)
                sqlConnect.Close();
        }
    }

    // Permission restricted.
    private static void ClientUpdate(NetworkStream stream, SqlConnection sqlConnect)
    {
        try
        {
            sqlConnect.Open();
            UpdateRequest req = sr.Deserialise<UpdateRequest>(sr.ReadString(stream));
            if (!CheckSessionValidity(req.sessionID, columnRecordID))
            {
                stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                return;
            }

            // Determine whether or not the user has the required permission, given the table name.
            int category = Glo.Fun.GetPermissionRelevancy(req.table);

            if (category == -1 ||
                !CheckSessionPermission(clientSessions[req.sessionID], category, Glo.PERMISSION_EDIT))
            {
                stream.WriteByte(Glo.CLIENT_INSUFFICIENT_PERMISSIONS);
                return;
            }

            SqlCommand com = new SqlCommand(req.SqlUpdate(), sqlConnect);

            if (com.ExecuteNonQuery() == 0)
                stream.WriteByte(Glo.CLIENT_REQUEST_FAILED_RECORD_DELETED);
            else
            {
                stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
                if (req.table == "Resource")
                    SendChangeNotifications(null, Glo.SERVER_RESOURCES_UPDATED);
            }
        }
        catch (Exception e)
        {
            LogError("Couldn't delete record, see error:", e);
            SafeFail(stream, e.Message);
        }
        finally
        {
            if (sqlConnect.State == System.Data.ConnectionState.Open)
                sqlConnect.Close();
        }
    }

    // Permission restricted.
    private static void ClientDelete(NetworkStream stream, SqlConnection sqlConnect)
    {
        try
        {
            sqlConnect.Open();
            DeleteRequest req = sr.Deserialise<DeleteRequest>(sr.ReadString(stream));

            if (!CheckSessionValidity(req.sessionID, columnRecordID))
            {
                stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                return;
            }

            // Determine whether or not the user has the required permission, given the table name.
            int category = Glo.Fun.GetPermissionRelevancy(req.table);

            if (category == -1 ||
                !CheckSessionPermission(clientSessions[req.sessionID], category, Glo.PERMISSION_DELETE))
            {
                stream.WriteByte(Glo.CLIENT_INSUFFICIENT_PERMISSIONS);
                return;
            }

            SqlCommand com = new SqlCommand(req.SqlDelete(), sqlConnect);

            // Organisation deletions can be really heavy if a lot of assets will be affected and the deletion count
            // is upwards of 1000.
            if (req.table == "Organisation" && req.ids.Count > 1000)
                com.CommandTimeout = 180;
            if (com.ExecuteNonQuery() == 0)
                stream.WriteByte(Glo.CLIENT_REQUEST_FAILED_RECORD_DELETED);
            else
            {
                stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
                if (req.table == "Resource")
                    SendChangeNotifications(null, Glo.SERVER_RESOURCES_UPDATED);
            }
        }
        catch (Exception e)
        {
            LogError("Couldn't delete record, see error:", e);
            SafeFail(stream, e.Message);
        }
        finally
        {
            if (sqlConnect.State == System.Data.ConnectionState.Open)
                sqlConnect.Close();
        }
    }

    // Permission restricted.
    private static void ClientLinkContact(NetworkStream stream, SqlConnection sqlConnect)
    {
        try
        {
            sqlConnect.Open();
            LinkContactRequest req = sr.Deserialise<LinkContactRequest>(sr.ReadString(stream));
            if (!CheckSessionValidity(req.sessionID, req.columnRecordID))
            {
                stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                return;
            }

            if (!CheckSessionPermission(clientSessions[req.sessionID], Glo.PERMISSION_RECORDS, Glo.PERMISSION_EDIT))
            {
                stream.WriteByte(Glo.CLIENT_INSUFFICIENT_PERMISSIONS);
                return;
            }

            SqlCommand com;
            if (req.unlink)
                com = new SqlCommand(req.SqlDelete(), sqlConnect);
            else
                com = new SqlCommand(req.SqlInsert(), sqlConnect);

            if (com.ExecuteNonQuery() == 0)
                stream.WriteByte(Glo.CLIENT_REQUEST_FAILED_RECORD_DELETED);
            else
                stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
        }
        catch (Exception e)
        {
            // This will almost certainly because someone deleted the contact in the meantime, so respond with this.
            if (e.Message.Contains("duplicate key"))
                SafeFail(stream, "Contact link already exists.");
            else
                SafeFail(stream, e.Message);
            LogError("Couldn't create or modify organisation/contact link, see error:", e);
        }
        finally
        {
            if (sqlConnect.State == System.Data.ConnectionState.Open)
                sqlConnect.Close();
        }
    }

    private static void ClientLinkedContactSelect(NetworkStream stream, SqlConnection sqlConnect)
    {
        try
        {
            sqlConnect.Open();
            LinkedContactSelectRequest req = sr.Deserialise<LinkedContactSelectRequest>(sr.ReadString(stream));
            if (!CheckSessionValidity(req.sessionID, columnRecordID))
            {
                stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                return;
            }

            SqlCommand com = new SqlCommand(req.SqlSelect(), sqlConnect);

            SelectResult result = new(com.ExecuteReader());
            stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
            sr.WriteAndFlush(stream, sr.Serialise(result));
        }
        catch (Exception e)
        {
            LogError("Couldn't run or return query, see error:", e);
            SafeFail(stream);
        }
        finally
        {
            if (sqlConnect.State == ConnectionState.Open)
                sqlConnect.Close();
        }
    }

    private static void ClientSelectHistory(NetworkStream stream, SqlConnection sqlConnect)
    {
        try
        {
            sqlConnect.Open();
            SelectHistoryRequest req = sr.Deserialise<SelectHistoryRequest>(sr.ReadString(stream));
            if (!CheckSessionValidity(req.sessionID, columnRecordID))
            {
                stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                return;
            }

            SqlCommand com = new SqlCommand(req.SqlSelect(), sqlConnect);

            SelectResult result = new(com.ExecuteReader());
            stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
            sr.WriteAndFlush(stream, sr.Serialise(result));
        }
        catch (Exception e)
        {
            LogError("Couldn't select history, see error:", e);
            SafeFail(stream);
        }
        finally
        {
            if (sqlConnect.State == ConnectionState.Open)
                sqlConnect.Close();
        }
    }

    private static void ClientBuildHistoricalRecord(NetworkStream stream, SqlConnection sqlConnect)
    {
        /* This function builds the state of an organisation or asset from the change log. It selects all rows before
         * the chosen date, then goes down each column to find the first row where the Register value is set to 1
         * (or rather, not NULL). It uses all of these values to build a faux-organisation or asset.
         * */

        try
        {
            sqlConnect.Open();
            SelectHistoricalRecordRequest req = sr.Deserialise<SelectHistoricalRecordRequest>(sr.ReadString(stream));
            if (!CheckSessionValidity(req.sessionID, columnRecordID))
            {
                stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                return;
            }

            SqlCommand com = new SqlCommand(req.SqlSelect(), sqlConnect);

            SqlDataReader reader = com.ExecuteReader();

            List<string?> columnNames = new();
            List<string?> columnTypes = new();
            List<object?> data = new();

            DataTable schema = reader.GetSchemaTable();

            // Cycle through and add every non-Register column name.
            for (int i = 0; i < schema.Rows.Count; i += 2)
            {
                columnNames.Add(schema.Rows[i].Field<string>("ColumnName"));
                Type? t = schema.Rows[i].Field<Type>("DataType");
                columnTypes.Add(t == null ? null : t.Name);

                if (i == 0) // Skip the change-specific columns near the start of the table.
                    i += 4;
            }

            // Create a bool array to track which columns have found their resting value.
            int fieldCount = columnNames.Count;
            bool[] found = new bool[fieldCount];
            int foundSoFar = 0;
            for (int i = 0; i < fieldCount; ++i)
                data.Add(null); // Give every column a starting value.

            List<List<object?>> rows = new();

            while (reader.Read() && foundSoFar < fieldCount)
            {
                // Add the organisation number first.
                if (!found[0] && !reader.IsDBNull(0))
                {
                    found[0] = true;
                    data[0] = reader[0];
                    ++foundSoFar;
                }

                // i is the index of the row, n is the index for data List.
                for (int i = 5, n = 1; i < reader.FieldCount; i += 2, ++n)
                {
                    if (found[n] || reader.IsDBNull(i)) continue;

                    // i represents the Register field, so + 1 to get the relevant value.
                    if (!reader.IsDBNull(i + 1))
                        data[n] = reader[i + 1];
                    found[n] = true;
                    ++foundSoFar;
                }
            }

            SelectResult result = new(columnNames, columnTypes, new List<List<object?>> { data });
            stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
            sr.WriteAndFlush(stream, sr.Serialise(result));
        }
        catch (Exception e)
        {
            LogError("Couldn't build historical record, see error:", e);
            SafeFail(stream);
        }
        finally
        {
            if (sqlConnect.State == ConnectionState.Open)
                sqlConnect.Close();
        }
    }

    // Permission restricted.
    private static void ClientColumnModification(NetworkStream stream, SqlConnection sqlConnect)
    {
        while (pullingColumnRecord != 0 || updatingColumnRecord)
            Thread.Sleep(10);
        updatingColumnRecord = true;

        try
        {
            sqlConnect.Open();
            TableModification req = sr.Deserialise<TableModification>(sr.ReadString(stream));

            lock (clientSessions)
            {
                if (!CheckSessionValidity(req.sessionID, req.columnRecordID))
                {
                    stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                    return;
                }

                if (!clientSessions[req.sessionID].admin)
                {
                    stream.WriteByte(Glo.CLIENT_INSUFFICIENT_PERMISSIONS);
                    return;
                }
            }

            if (req.friendly != null)
            {
                string friendly = req.friendly.Replace(" ", "_");
                OrderedDictionary? dictionary = ColumnRecord.GetDictionary(req.table, false);
                if (dictionary == null)
                    throw new Exception("Table name not recognised.");
                if (dictionary.Contains(friendly))
                    throw new Exception("Cannot select an existing column name as a friendly name.");
            }


            string orderUpdateCommand = "";

            // Protect integral tables and columns (could be structured better, but I've tried to keep it readable).
            if (req.intent == TableModification.Intent.Removal ||
                (req.intent == TableModification.Intent.Modification && req.columnType != null))
            {
                string column = req.column;
                string table = req.table;
                if (req.intent == TableModification.Intent.Removal)
                {
                    if (!Glo.Fun.ColumnRemovalAllowed(table, column))
                    {
                        stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);
                        return;
                    }

                    // Fix the column order as required if removing a column.

                    var order = ColumnRecord.GetOrder(req.table);
                    if (order == null)
                        throw new("Error discerning the correct table dictionary being affected by removal.");

                    int oldColIndex = ColumnRecord.GetColumnIndex(req.table, req.column);

                    // Remove the current column's index, and decrement anything higher.
                    for (int i = 0; i < order.Count; ++i)
                        if (order[i] == oldColIndex)
                        {
                            order.RemoveAt(i);
                            --i;
                        }
                        else if (order[i] > oldColIndex)
                            --order[i];
                    // Paste over the crack left at the end by the removal. Without this, when get bugs later when
                    // making additions.
                    order.Add(order.Count);

                    // We'll need to make a transaction with both the removal and order change, so create the second
                    // command here.
                    ColumnOrdering colOrder = new ColumnOrdering("", 0, ColumnRecord.organisationOrder,
                                                                        ColumnRecord.assetOrder,
                                                                        ColumnRecord.contactOrder,
                                                                        ColumnRecord.conferenceOrder);
                    orderUpdateCommand = colOrder.SqlCommand();
                }

                // If the column contains any data (either present or historic), require confirmation.

                bool dataPresent = false;
                SqlCommand comDataPresent = new SqlCommand($"SELECT TOP 1 {column} FROM {table} " +
                                                           $"WHERE {column} IS NOT NULL",
                                                           sqlConnect);
                SqlDataReader reader = comDataPresent.ExecuteReader();
                dataPresent = reader.Read();
                reader.Close();
                if (!dataPresent && (table == "Organisation" || table == "Asset"))
                {
                    comDataPresent = new SqlCommand($"SELECT TOP 1 {column} FROM {table}Change " +
                                                    $"WHERE {column} IS NOT NULL",
                                                    sqlConnect);
                    reader = comDataPresent.ExecuteReader();
                    if (reader.Read())
                        dataPresent = true;
                    reader.Close();
                }

                if (dataPresent)
                {
                    stream.WriteByte(Glo.CLIENT_CONFIRM);
                    if (stream.ReadByte() != Glo.CLIENT_CONFIRM)
                        return;
                }
            }

            SqlCommand com = new(orderUpdateCommand == "" ?
                                         req.SqlCommand() :
                                         SqlAssist.Transaction(orderUpdateCommand, req.SqlCommand()),
                                 sqlConnect);

            if (com.ExecuteNonQuery() == 0)
                stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);
            else
            {
                // Update the column record. Error message printed in both functions if this fails.
                columnRecordIntact = RebuildColumnRecord(sqlConnect);
                columnRecordIntact = ParseColumnRecord(false);

                // Don't report success until the column record has been updated, otherwise the client
                // will attempt to pull the record first.stopst
                stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);

                // All clients will now request an updated column record.
                SendChangeNotifications(req.sessionID, Glo.SERVER_COLUMN_RECORD_UPDATED);
            }
        }
        catch (Exception e)
        {
            SafeFail(stream, e.Message);
            LogError(e);
        }
        finally
        {
            if (sqlConnect.State == System.Data.ConnectionState.Open)
                sqlConnect.Close();
            updatingColumnRecord = false;
        }
    }

    // Permission restricted.
    private static void ClientColumnOrderUpdate(NetworkStream stream, SqlConnection sqlConnect)
    {
        while (pullingColumnRecord != 0 || updatingColumnRecord)
            Thread.Sleep(10);
        updatingColumnRecord = true;

        try
        {
            sqlConnect.Open();
            ColumnOrdering req = sr.Deserialise<ColumnOrdering>(sr.ReadString(stream));

            lock (clientSessions)
            {
                if (!CheckSessionValidity(req.sessionID, req.columnRecordID))
                {
                    stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                    return;
                }

                if (!clientSessions[req.sessionID].admin)
                {
                    stream.WriteByte(Glo.CLIENT_INSUFFICIENT_PERMISSIONS);
                    return;
                }
            }

            // Reject if the column record isn't intact, or if the order column counts aren't right.
            if (!columnRecordIntact ||
                req.organisationOrder.Count != ColumnRecord.organisation.Count ||
                req.assetOrder.Count != ColumnRecord.asset.Count ||
                req.contactOrder.Count != ColumnRecord.contact.Count ||
                req.conferenceOrder.Count != ColumnRecord.conference.Count)
                throw new Exception("Could not apply new column order. Either the column record " +
                                    "is no longer intact, or the column counts were incorrect.");

            // Get the headers out the way first.
            Glo.Fun.ExistsOrCreateFolder(Glo.PathConfigFiles);
            File.WriteAllText(Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_HEADERS), req.HeaderConfigText());

            // Make sure the column numbers look right.
            void CheckStartingOrderValues(List<int> order, int cutoff)
            {
                for (int n = 0; n < Glo.Tab.ORGANISATION_STATIC_COUNT; ++n)
                    if (req.organisationOrder[n] != n)
                        throw new Exception("Could not apply new column order. It looks like the user is attempting " +
                                            "to reorder un-reorderable columns.");
            }
            CheckStartingOrderValues(req.organisationOrder, Glo.Tab.ORGANISATION_STATIC_COUNT);
            CheckStartingOrderValues(req.assetOrder, Glo.Tab.ASSET_STATIC_COUNT);
            CheckStartingOrderValues(req.contactOrder, Glo.Tab.CONTACT_STATIC_COUNT);
            CheckStartingOrderValues(req.conferenceOrder, Glo.Tab.CONFERENCE_STATIC_COUNT);

            SqlCommand sqlCommand = new SqlCommand(req.SqlCommand(), sqlConnect);
            if (sqlCommand.ExecuteNonQuery() != 4)
                stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);
            else
            {
                // Update the column record. Error message printed in both functions if this fails.
                columnRecordIntact = RebuildColumnRecord(sqlConnect);
                columnRecordIntact = ParseColumnRecord(false);

                // Don't report success until the column record has been updated, otherwise the client
                // will attempt to pull the record first.stopst
                stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);

                // All clients will now request an updated column record.
                SendChangeNotifications(req.sessionID, Glo.SERVER_COLUMN_RECORD_UPDATED);
            }
        }
        catch (Exception e)
        {
            SafeFail(stream);
            LogError(e);
        }
        finally
        {
            if (sqlConnect.State == ConnectionState.Open)
                sqlConnect.Close();
            updatingColumnRecord = false;
        }
    }

    static object selectBuilderPresetFileLock = new();

    // Permission restricted.
    private static void ClientSaveSelectBuilderPreset(NetworkStream stream)
    {
        try
        {
            string sessionID = sr.ReadString(stream);
            // No need to check record ID for this, if it fails, it fails.
            string jsonString = sr.ReadString(stream);

            lock (clientSessions)
            {
                if (!clientSessions.ContainsKey(sessionID))
                {
                    stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                    return;
                }

                if (!CheckSessionPermission(clientSessions[sessionID], Glo.PERMISSION_REPORTS, Glo.PERMISSION_CREATE))
                {
                    stream.WriteByte(Glo.CLIENT_INSUFFICIENT_PERMISSIONS);
                    return;
                }
            }

            // Don't serialise the whole thing because it'll just waste time.
            int index = jsonString.IndexOf("\",");
            string name = jsonString.Substring(9, index - 9);
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string folder = Path.Combine(documents, "BridgeOps", Glo.FOLDER_QUERY_BUILDER_PRESETS);
            lock (selectBuilderPresetFileLock)
            {
                Glo.Fun.ExistsOrCreateFolder(folder);
                string file = Path.Combine(folder, name + ".pre");
                File.WriteAllText(file, jsonString);
            }
            stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
        }
        catch (Exception e)
        {
            SafeFail(stream, e.Message);
            LogError(e);
        }
        finally
        {
            stream.Close();
        }
    }

    private static void ClientLoadSelectBuilder(NetworkStream stream)
    {
        // This method is used both for getting the preset list and loading a specific preset.
        try
        {
            string sessionID = sr.ReadString(stream);
            string presetName = sr.ReadString(stream);
            bool list = presetName == "/"; // Forward slashes are disallowed in preset names.

            lock (clientSessions)
            {
                if (!clientSessions.ContainsKey(sessionID))
                {
                    stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                    return;
                }
            }

            string folder = Glo.Fun.ApplicationFolder(Glo.FOLDER_QUERY_BUILDER_PRESETS);
            if (!Directory.Exists(folder)) // In this case, we succeed with no files to report.
            {
                stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
                sr.WriteAndFlush(stream, "");
            }

            if (list)
            {
                List<string> files = new();
                lock (selectBuilderPresetFileLock)
                {
                    foreach (string s in Directory.GetFiles(folder))
                        files.Add(Path.GetFileName(s));
                }
                stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
                sr.WriteAndFlush(stream, string.Join('/', files));
            }
            else
            {
                string jsonString;
                lock (selectBuilderPresetFileLock)
                {
                    jsonString = File.ReadAllText(Path.Combine(folder, presetName + ".pre"));
                }
                stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
                sr.WriteAndFlush(stream, jsonString);
            }
        }
        catch (Exception e)
        {
            SafeFail(stream, e.Message);
            LogError(e);
        }
        finally
        {
            stream.Close();
        }
    }

    // Permission restricted.
    private static void ClientDeleteSelectBuilderPreset(NetworkStream stream)
    {
        try
        {
            string sessionID = sr.ReadString(stream);
            // No need to check record ID for this, if it fails, it fails.
            string file = sr.ReadString(stream) + ".pre";

            lock (clientSessions)
            {
                if (!clientSessions.ContainsKey(sessionID))
                {
                    stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                    return;
                }

                if (!CheckSessionPermission(clientSessions[sessionID], Glo.PERMISSION_REPORTS, Glo.PERMISSION_DELETE))
                {
                    stream.WriteByte(Glo.CLIENT_INSUFFICIENT_PERMISSIONS);
                    return;
                }
            }

            string folder = Glo.Fun.ApplicationFolder(Glo.FOLDER_QUERY_BUILDER_PRESETS);
            lock (selectBuilderPresetFileLock)
            {
                File.Delete(Path.Combine(folder, file));
            }
            stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
        }
        catch (Exception e)
        {
            SafeFail(stream, e.Message);
            LogError(e);
        }
        finally
        {
            stream.Close();
        }
    }

    // Permission restricted.
    private static void ClientRenameSelectBuilderPreset(NetworkStream stream)
    {
        try
        {
            string sessionID = sr.ReadString(stream);
            // No need to check record ID for this, if it fails, it fails.
            string[] files = sr.ReadString(stream).Split('/');
            string oldName = files[0] + ".pre";
            string newName = files[1] + ".pre";

            lock (clientSessions)
            {
                if (!clientSessions.ContainsKey(sessionID))
                {
                    stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                    return;
                }

                if (!CheckSessionPermission(clientSessions[sessionID], Glo.PERMISSION_REPORTS, Glo.PERMISSION_EDIT))
                {
                    stream.WriteByte(Glo.CLIENT_INSUFFICIENT_PERMISSIONS);
                    return;
                }
            }

            string folder = Glo.Fun.ApplicationFolder(Glo.FOLDER_QUERY_BUILDER_PRESETS);
            lock (selectBuilderPresetFileLock)
            {
                File.Move(Path.Combine(folder, oldName), Path.Combine(folder, newName));
            }
            stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
        }
        catch (Exception e)
        {
            SafeFail(stream, e.Message);
            LogError(e);
        }
        finally
        {
            stream.Close();
        }
    }

    private static void ClientConferenceViewSearch(NetworkStream stream, SqlConnection sqlConnect)
    {
        try
        {
            sqlConnect.Open();
            string sessionID = sr.ReadString(stream);
            string start = SqlAssist.DateTimeToSQL(sr.Deserialise<DateTime>(sr.ReadString(stream)), false);
            string end = SqlAssist.DateTimeToSQL(sr.Deserialise<DateTime>(sr.ReadString(stream)), false);

            lock (clientSessions)
            {
                if (!clientSessions.ContainsKey(sessionID))
                {
                    stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                    return;
                }
            }

            SqlCommand com = new($"SELECT Conference.{Glo.Tab.CONFERENCE_ID}, " +
                                        $"Conference.{Glo.Tab.CONFERENCE_TITLE}, " +
                                        $"Conference.{Glo.Tab.CONFERENCE_START}, " +
                                        $"Conference.{Glo.Tab.CONFERENCE_END}, " +
                                        $"Conference.{Glo.Tab.RESOURCE_ID}, " +
                                        $"Conference.{Glo.Tab.CONFERENCE_RESOURCE_ROW}, " +
                                        $"Conference.{Glo.Tab.CONFERENCE_CANCELLED}, " +
                                        $"MAX(CONVERT(INT, Connection.{Glo.Tab.CONNECTION_IS_TEST})) AS IsTest, " +
                                         "COUNT(*) AS ConnectionCount " +
                                  "FROM Conference " +
                                 $"LEFT JOIN Connection ON Conference.{Glo.Tab.CONFERENCE_ID} = " +
                                                         $"Connection.{Glo.Tab.CONFERENCE_ID} " +
                                 $"WHERE Conference.{Glo.Tab.CONFERENCE_END} >= '{start}' " +
                                   $"AND Conference.{Glo.Tab.CONFERENCE_START} <= '{end}' " +
                                 $"GROUP BY Conference.{Glo.Tab.CONFERENCE_ID}, " +
                                          $"Conference.{Glo.Tab.CONFERENCE_TITLE}, " +
                                          $"Conference.{Glo.Tab.CONFERENCE_START}, " +
                                          $"Conference.{Glo.Tab.CONFERENCE_END}, " +
                                          $"Conference.{Glo.Tab.RESOURCE_ID}, " +
                                          $"Conference.{Glo.Tab.CONFERENCE_CANCELLED}, " +
                                          $"Conference.{Glo.Tab.CONFERENCE_RESOURCE_ROW};",
                                 sqlConnect);

            SelectResult result = new(com.ExecuteReader());
            stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
            sr.WriteAndFlush(stream, sr.Serialise(result));
        }
        catch (Exception e)
        {
            LogError("Couldn't get list of conferences for client schedule view, see error:", e);
            SafeFail(stream, e.Message);
        }
        finally
        {
            if (sqlConnect.State == ConnectionState.Open)
                sqlConnect.Close();
        }
    }

    private static void ClientConferenceSelect(NetworkStream stream, SqlConnection sqlConnect)
    {
        try
        {
            string sessionID = sr.ReadString(stream);
            int conferenceID;
            string conferenceIdStr = sr.ReadString(stream);
            string crID = sr.ReadString(stream);
            if (!int.TryParse(conferenceIdStr, out conferenceID))
            {
                stream.WriteByte(Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW);
                SafeFail(stream, "Conference ID was not an integer.");
                return;
            }

            lock (clientSessions)
            {
                if (!clientSessions.ContainsKey(sessionID) || crID != columnRecordID.ToString())
                {
                    stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                    return;
                }
            }

            // Get the conference details.

            List<string> conferenceColNames = new();
            foreach (DictionaryEntry de in ColumnRecord.orderedConference)
                conferenceColNames.Add((string)de.Key);

            sqlConnect.Open();
            SqlCommand com = new($"SELECT {string.Join(", ", conferenceColNames)} " +
                                 $"FROM Conference WHERE {Glo.Tab.CONFERENCE_ID} = {conferenceID};",
                                 sqlConnect);
            SelectResult result = new(com.ExecuteReader());
            List<object?> confRow = result.rows[0];
            Conference conf = new()
            {
                sessionID = sessionID,
                columnRecordID = columnRecordID,
                conferenceID = conferenceID,
                resourceID = (int)Glo.Fun.GetInt32FromNullableObject(confRow[1])!,
                resourceRow = Convert.ToInt32(confRow[2]!),
                title = (string?)confRow[3]!,
                start = (DateTime)confRow[4]!,
                end = (DateTime)confRow[5]!,
                cancelled = (bool?)confRow[6]!,
                createLoginID = (int)Glo.Fun.GetInt32FromNullableObject(confRow[7])!,
                createTime = (DateTime?)confRow[8]!,
                editLoginID = confRow[9] == null ? null : Convert.ToInt32(confRow[9]!),
                editDateTime = (DateTime?)confRow[10]!,
                notes = (string?)confRow[11]!,
                additionalValTypes = new(),
                additionalValObjects = new()
            };

            // Add the additional columns.
            for (int i = 12; i < confRow.Count; ++i)
            {
                conf.additionalValTypes.Add(result.columnTypes[i]);
                conf.additionalValObjects.Add(confRow[i]);
            }

            // Get a list of connections.

            com.CommandText = $"SELECT Connection.*, " +
                                     $"Organisation.{Glo.Tab.ORGANISATION_ID}, " +
                                     $"Organisation.{Glo.Tab.ORGANISATION_REF}, " +
                                     $"Organisation.{Glo.Tab.ORGANISATION_NAME} " +
                              $"FROM Connection " +
                              $"LEFT JOIN Organisation ON Organisation.{Glo.Tab.DIAL_NO} = " +
                                                         $"Connection.{Glo.Tab.DIAL_NO} " +
                              $"WHERE Connection.{Glo.Tab.CONFERENCE_ID} = {conferenceID} " +
                              $"ORDER BY {Glo.Tab.CONNECTION_ROW};";
            result = new(com.ExecuteReader());

            conf.connections = new();
            foreach (var row in result.rows)
            {
                Conference.Connection connection = new()
                {
                    connectionID = (int)Glo.Fun.GetInt32FromNullableObject(row[0])!,
                    conferenceID = conferenceID,
                    dialNo = (string)row[2]!,
                    isManaged = row[3] != null && (bool)row[3]!,
                    connected = (DateTime?)row[4],
                    disconnected = (DateTime?)row[5],
                    row = (int)Glo.Fun.GetInt32FromNullableObject(row[6])!,
                    isTest = row[7] != null && (bool)row[7]!,
                    orgId = Glo.Fun.GetInt32FromNullableObject(row[8]),
                    orgReference = (string?)row[9],
                    orgName = (string?)row[10]
                };
                conf.connections.Add(connection);
            }

            stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
            sr.WriteAndFlush(stream, sr.Serialise(conf));
        }
        catch (Exception e)
        {
            LogError("Couldn't load conference, see error:", e);
            SafeFail(stream, e.Message);
        }
        finally
        {
            if (sqlConnect.State == ConnectionState.Open)
                sqlConnect.Close();
        }
    }

    // Permission restricted.
    private static void ClientConferenceCancel(NetworkStream stream, SqlConnection sqlConnect)
    {
        try
        {
            string sessionID = sr.ReadString(stream);
            string conferenceID = sr.ReadString(stream);
            bool uncancel = stream.ReadByte() == 1;
            if (!int.TryParse(conferenceID, out _))
            {
                stream.WriteByte(Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW);
                SafeFail(stream, "Conference ID was not an integer.");
                return;
            }

            lock (clientSessions)
            {
                if (!clientSessions.ContainsKey(sessionID))
                {
                    stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                    return;
                }
                if (!CheckSessionPermission(clientSessions[sessionID], Glo.PERMISSION_CONFERENCES,
                                                                       Glo.PERMISSION_EDIT))
                {
                    stream.WriteByte(Glo.CLIENT_INSUFFICIENT_PERMISSIONS);
                    return;
                }
            }

            // Get the conference details.

            sqlConnect.Open();
            SqlCommand com = new($"UPDATE Conference SET {Glo.Tab.CONFERENCE_CANCELLED} = {(uncancel ? "0" : "1")} " +
                                 $"WHERE {Glo.Tab.CONFERENCE_ID} = {conferenceID};", sqlConnect);
            if (com.ExecuteNonQuery() == 0)
            {
                stream.WriteByte(Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW);
                sr.WriteAndFlush(stream, "Conference could not be found in the database.");
            }
            else
                stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
        }
        catch (Exception e)
        {
            LogError("Couldn't cancel conference, see error:", e);
            SafeFail(stream, e.Message);
        }
        finally
        {
            if (sqlConnect.State == ConnectionState.Open)
                sqlConnect.Close();
        }
    }
}