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
using System.Threading.Channels;
using System.Reflection.PortableExecutable;
using System.Xml.Schema;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;

internal class BridgeOpsAgent
{
    static UnicodeEncoding unicodeEncoding = new UnicodeEncoding();
    static SendReceive sr = new SendReceive();

    struct ClientSession
    {
        public string username;
        public string ip;
        public int loginID;
        public List<bool[]> permissions;
        public bool[] createPermissions;
        public bool[] editPermissions;
        public bool[] deletePermissions;
        public ClientSession(string username, string ip, int loginID,
                             int createPermissions, int editPermissions, int deletePermissions)
        {
            this.username = username;
            this.ip = ip;
            this.loginID = loginID;
            this.createPermissions = Glo.Fun.GetPermissionsArray(createPermissions);
            this.editPermissions = Glo.Fun.GetPermissionsArray(editPermissions);
            this.deletePermissions = Glo.Fun.GetPermissionsArray(deletePermissions);

            permissions = new() { this.createPermissions, this.editPermissions, this.deletePermissions };
        }
    }
    static Dictionary<string, ClientSession> clientSessions = new Dictionary<string, ClientSession>();
    static bool CheckSessionValidity(string id)
    {
        return clientSessions.ContainsKey(id);
    }
    static bool CheckSessionPermission(ClientSession session, int category, int intent)
    {
        return session.permissions[intent][category];
    }
    static SqlCommand PullUpUserFromPassword(string username, string password, SqlConnection sqlConnect)
    {
        return new SqlCommand(string.Format("SELECT * FROM Login WHERE (Username = '{0}' AND " +
                                            "Password = HASHBYTES('SHA2_512', '{1}'));",
                                            username, password), sqlConnect);
    }
    // If you need to check the quickly condition, but need the bool outside of scope.
    static bool CheckSessionValidity(string id, out bool result)
    {
        result = clientSessions.ContainsKey(id);
        return result;
    }

    // Multiple threads may try to access this at once, so hold them up if stopnecessary.
    private static bool currentlyWriting = false;
    private static void LogError(string context, Exception e)
    {
        while (currentlyWriting) Thread.Sleep(10);
        currentlyWriting = true;
        string error = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss") + "   ";
        if (context.Length > 0) error += context + "   ";
        error += e.Message + "\n";
        File.AppendAllText(Glo.LOG_ERROR_AGENT, error);
        currentlyWriting = false;
    }
    private static void LogError(Exception e)
    {
        LogError("", e);
    }

    // Network Configuraiton
    private static int portInbound = Glo.PORT_INBOUND_DEFAULT;
    private static int portOutbound = Glo.PORT_OUTBOUND_DEFAULT;
    private static int LoadNetworkConfig()
    {
        int iVal;
        int valuesSet = 0;
        try
        {
            string[] networkConfig = File.ReadAllLines(Glo.PATH_CONFIG_FILES + Glo.CONFIG_NETWORK);
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
    private static IPAddress thisIP = new IPAddress(new byte[] { 127, 0, 0, 1 });
    private static IPEndPoint thisEP = new IPEndPoint(thisIP, portInbound);
    private static TcpListener listener = new TcpListener(thisEP);

    private static void GetColumnRecordFromFile()
    {
        try
        {
            if (!ColumnRecord.Initialise(File.ReadAllText(Glo.PATH_AGENT + Glo.CONFIG_COLUMN_RECORD)))
                throw new Exception("Column record file may be corrupted. Restore from console.");
        }
        catch (Exception e)
        {
            LogError("Column record could not be initialised, see error:", e);
        }
    }


    // Apart from the console generating the the database, Agent is the only one that needs access to SQL Server.
    private static string connectionString = "server=localhost\\SQLEXPRESS;" +
                                             "integrated security=SSPI;" +
                                             //"user id=sa; password=^2*Re98E;" +
                                             "encrypt=false;" +
                                             "database=BridgeOps;" +
                                             "Application Name=BridgeOpsAgent;";

    private static void Main(string[] args)
    {
        // Get the column record.
        GetColumnRecordFromFile();

        // Test sending a command to the database to see if it works. If it doesn't, keep trying every 5 seconds.
        bool successfulSqlConnection = false;
        while (!successfulSqlConnection)
        {
            SqlConnection sqlConnect = new SqlConnection(connectionString);

            try
            {
                sqlConnect.Open();
                // Send a pointless but minimal query just to make sure we have a working connection.
                SqlCommand sqlCommand = new SqlCommand("SELECT TOP 1 Username FROM Login;", sqlConnect);
                sqlCommand.ExecuteNonQuery();
                // If we got this far in the try/catch, we're in business.
                successfulSqlConnection = true;
            }
            catch (Exception e)
            {
                LogError("Couldn't interact with database, retry in 5 seconds. See error:", e);
                Thread.Sleep(5_000); // Wait 5 seconds for the next retry.
            }
            finally
            {
                if (sqlConnect.State == System.Data.ConnectionState.Open)
                    sqlConnect.Close();
            }
        }

        // Read network configuraiton.
        LoadNetworkConfig();

        // Start the thread responsible for nudging SQL Server. Remove this at some point.
        Thread sqlNudgeThr = new Thread(SqlServerNudge);
        sqlNudgeThr.Start();

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

        SqlConnection sqlConnect = new SqlConnection(connectionString);
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
            // Do not accept any 
            autoResetEvent.WaitOne();
            autoResetEvent.Reset();
        }
    }
    private static AutoResetEvent autoResetEvent = new AutoResetEvent(false);
    private static void HandleClientListenAccept(IAsyncResult result)
    {
        // I believe each thread will need its own dedicated SqlConnection object.
        SqlConnection sqlConnect = new SqlConnection(connectionString);

        if (result.AsyncState != null)
        {
            TcpListener listener = (TcpListener)result.AsyncState;
            TcpClient client = listener.EndAcceptTcpClient(result);

            NetworkStream stream = client.GetStream();
            autoResetEvent.Set();

            try
            {
                int fncByte = stream.ReadByte();
                if (fncByte == Glo.CLIENT_PULL_COLUMN_RECORD)
                    ClientPullColumnRecord(stream);
                else if (fncByte == Glo.CLIENT_LOGIN)
                    ClientLogin(stream, sqlConnect, (IPEndPoint?)client.Client.RemoteEndPoint);
                else if (fncByte == Glo.CLIENT_LOGOUT)
                    ClientSessionLogout(stream);
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
                else if (fncByte == Glo.CLIENT_UPDATE_ORGANISATION ||
                         fncByte == Glo.CLIENT_UPDATE_CONTACT ||
                         fncByte == Glo.CLIENT_UPDATE_ASSET ||
                         fncByte == Glo.CLIENT_UPDATE_CONFERENCE_TYPE ||
                         fncByte == Glo.CLIENT_UPDATE_CONFERENCE ||
                         fncByte == Glo.CLIENT_UPDATE_RESOURCE ||
                         fncByte == Glo.CLIENT_UPDATE_LOGIN)
                    ClientUpdate(stream, sqlConnect, fncByte);
                else if (fncByte == Glo.CLIENT_SELECT_COLUMN_PRIMARY)
                    ClientSelectColumnPrimary(stream, sqlConnect);
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
        ConnectedClients connectedClients = new ConnectedClients();
        foreach (KeyValuePair<string, ClientSession> client in clientSessions)
            connectedClients.Add(client.Value.ip, client.Value.username);

        sr.WriteAndFlush(server, sr.Serialise(connectedClients));
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
            if (sessionId.Length > 0)
            {
                clientSessions.Remove(sessionId);
                server.WriteByte(0);
                server.Flush();
            }
            else
            {
                server.WriteByte(1);
                server.Flush();
            }
        }
    }


    //   C L I E N T   R E Q U E S T   F U N C T I O N S

    private static void ClientPullColumnRecord(NetworkStream stream)
    {
        try
        {
            if (!CheckSessionValidity(sr.ReadString(stream)))
                stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
            else
            {
                stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
                sr.WriteAndFlush(stream, File.ReadAllText(Glo.PATH_AGENT + Glo.CONFIG_COLUMN_RECORD));
            }
        }
        catch (Exception e)
        {
            LogError("Could not read or send type restrictions file. See error: ", e);
        }
    }

    private static async void ClientLogin(NetworkStream stream, SqlConnection sqlConnect, IPEndPoint? ep)
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
            Task taskSqlConnect = sqlConnect.OpenAsync();

            // Prepare variables for if/else block below.
            string userAlreadyLoggedIn = "";
            string ipAlreadyConnected = "";

            // Identify any duplicate IPs and users for removal from open sessions if login is successful.
            lock (clientSessions)
            {
                foreach (KeyValuePair<string, ClientSession> cs in clientSessions)
                {
                    if (userAlreadyLoggedIn == "" && cs.Value.username == loginReq.username)
                        userAlreadyLoggedIn = cs.Key;
                    if (ipAlreadyConnected == "" && cs.Value.ip == ipStr)
                        ipAlreadyConnected = cs.Key;

                    if (userAlreadyLoggedIn != "" && ipAlreadyConnected != "")
                        break;
                }
            }

            await taskSqlConnect;
            SqlCommand sqlCommand = PullUpUserFromPassword(loginReq.username, loginReq.password, sqlConnect);

            SqlDataReader reader = sqlCommand.ExecuteReader();
            if (reader.Read())
            {
                int loginID = Convert.ToInt32(reader.GetValue(0)); // Has to be an int if anything was returned at all.
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

                clientSessions.Add(key, new ClientSession(loginReq.username, ipStr, loginID,
                                                          (byte)reader.GetValue(4),
                                                          (byte)reader.GetValue(5),
                                                          (byte)reader.GetValue(6)));

                sr.WriteAndFlush(stream, Glo.CLIENT_LOGIN_ACCEPT + key);
                sr.WriteAndFlush(stream, loginID.ToString());
            }
            else // Username or password incorrect.
                sr.WriteAndFlush(stream, Glo.CLIENT_LOGIN_REJECT_USER_INVALID);
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

    private static async void ClientPasswordReset(NetworkStream stream, SqlConnection sqlConnect)
    {
        SqlCommand com;


        try
        {
            Task taskSqlConnect = sqlConnect.OpenAsync();

            PasswordResetRequest req = sr.Deserialise<PasswordResetRequest>(sr.ReadString(stream));
            if (!CheckSessionValidity(req.sessionID))
            {
                stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                return;
            }

            // Must be here if the above code works.
            ClientSession session = clientSessions[req.sessionID];

            // Confirm that the request is from a session with the correct permissions, or is owned by the account
            // whos password is being changed.
            if (req.loginID != session.loginID &&
                !CheckSessionPermission(session, Glo.PERMISSION_USER_ACC_MGMT, Glo.PERMISSION_EDIT))
            {
                stream.WriteByte(Glo.CLIENT_INSUFFICIENT_PERMISSIONS);
                return;
            }

            await taskSqlConnect;

            // If the session isn't admin, check to see if the original password was correct.
            if (!CheckSessionPermission(session, Glo.PERMISSION_USER_ACC_MGMT, Glo.PERMISSION_EDIT))
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
            if (CheckSessionValidity(sr.ReadString(stream)))
            {
                List<object[]> users = new();
                foreach (KeyValuePair<string, ClientSession> session in clientSessions)
                    users.Add(new object[] { session.Value.loginID, session.Value.username });
                sr.WriteAndFlush(stream, sr.Serialise(users));
            }
        }
        catch (Exception e)
        {
            LogError(e);
        }
    }

    private static void ClientSessionLogout(NetworkStream stream)
    {
        try
        {
            string sessionDetails = sr.ReadString(stream);
            LogoutRequest logoutReq = sr.Deserialise<LogoutRequest>(sessionDetails);

            if (clientSessions.ContainsKey(logoutReq.sessionID))
            {
                ClientSession thisSession = clientSessions[logoutReq.sessionID];
                if (clientSessions[logoutReq.sessionID].username == logoutReq.username)
                {
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
                        if (session.Value.username == logoutReq.username)
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
        catch (Exception e)
        {
            LogError(e);
        }

    }

    private static void ClientNewInsert(NetworkStream stream, SqlConnection sqlConnect, int target)
    {
        try
        {
            sqlConnect.Open();
            SqlCommand com = new SqlCommand("", sqlConnect);

            bool sessionValid = false;

            if (target == Glo.CLIENT_NEW_ORGANISATION)
            {
                Organisation newRow = sr.Deserialise<Organisation>(sr.ReadString(stream));
                if (CheckSessionValidity(newRow.sessionID, out sessionValid))
                    com.CommandText = newRow.SqlInsert(clientSessions[newRow.sessionID].loginID);
            }
            else if (target == Glo.CLIENT_NEW_ASSET)
            {
                Asset newRow = sr.Deserialise<Asset>(sr.ReadString(stream));
                if (CheckSessionValidity(newRow.sessionID, out sessionValid))
                    com.CommandText = newRow.SqlInsert(clientSessions[newRow.sessionID].loginID);
            }
            else if (target == Glo.CLIENT_NEW_CONTACT)
            {
                Contact newRow = sr.Deserialise<Contact>(sr.ReadString(stream));
                if (CheckSessionValidity(newRow.sessionID, out sessionValid))
                    com.CommandText = newRow.SqlInsert();
            }
            else if (target == Glo.CLIENT_NEW_CONFERENCE_TYPE)
            {
                ConferenceType newRow = sr.Deserialise<ConferenceType>(sr.ReadString(stream));
                if (CheckSessionValidity(newRow.sessionID, out sessionValid))
                    com.CommandText = newRow.SqlInsert();
            }
            else if (target == Glo.CLIENT_NEW_CONFERENCE)
            {
                Conference newRow = sr.Deserialise<Conference>(sr.ReadString(stream));
                if (CheckSessionValidity(newRow.sessionID, out sessionValid))
                    com.CommandText = newRow.SqlInsert();
            }
            else if (target == Glo.CLIENT_NEW_RESOURCE)
            {
                Resource newRow = sr.Deserialise<Resource>(sr.ReadString(stream));
                if (CheckSessionValidity(newRow.sessionID, out sessionValid))
                    com.CommandText = newRow.SqlInsert();
            }
            else if (target == Glo.CLIENT_NEW_LOGIN)
            {
                Login newRow = sr.Deserialise<Login>(sr.ReadString(stream));
                if (CheckSessionValidity(newRow.sessionID, out sessionValid))
                    com.CommandText = newRow.SqlInsert();
            }

            if (!sessionValid)
            {
                stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
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
                    stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
            }
        }
        catch (Exception e)
        {
            LogError("Couldn't create new contact. See error:", e);
            stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);
        }
        finally
        {
            if (sqlConnect.State == System.Data.ConnectionState.Open)
                sqlConnect.Close();
        }
    }

    private static void ClientSelectColumnPrimary(NetworkStream stream, SqlConnection sqlConnect)
    {
        try
        {
            sqlConnect.Open();
            PrimaryColumnSelect pcs = sr.Deserialise<PrimaryColumnSelect>(sr.ReadString(stream));
            if (!clientSessions.ContainsKey(pcs.sessionID))
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
            stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);
        }
        finally
        {
            if (sqlConnect.State == System.Data.ConnectionState.Open)
                sqlConnect.Close();
        }
    }

    private static void ClientSelect(NetworkStream stream, SqlConnection sqlConnect)
    {
        try
        {
            sqlConnect.Open();
            SelectRequest req = sr.Deserialise<SelectRequest>(sr.ReadString(stream));
            if (!CheckSessionValidity(req.sessionID))
            {
                stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                return;
            }

            string command = "SELECT " + string.Join(", ", req.select) + " FROM " + req.table;
            List<string> conditions = new();
            if (req.likeColumns.Count > 0)
            {
                for (int i = 0; i < req.likeColumns.Count; ++i)
                {
                    // If the search is blank, we want to return everything, including null values.
                    if (req.likeValues[i] != "")
                        conditions.Add(req.likeColumns[i] + " LIKE \'%" + req.likeValues[i] + "%'");
                }

                if (conditions.Count > 0)
                    command += " WHERE " + string.Join(" AND ", conditions);
            }

            SqlCommand com = new SqlCommand(command, sqlConnect);

            SelectResult result = new(com.ExecuteReader());
            stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
            sr.WriteAndFlush(stream, sr.Serialise(result));
        }
        catch (Exception e)
        {
            LogError("Couldn't run or return query, see error:", e);
            stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);
        }
        finally
        {
            if (sqlConnect.State == System.Data.ConnectionState.Open)
                sqlConnect.Close();
        }
    }

    private static void ClientSelectWide(NetworkStream stream, SqlConnection sqlConnect)
    {
        try
        {
            sqlConnect.Open();
            SelectWideRequest req = sr.Deserialise<SelectWideRequest>(sr.ReadString(stream));
            if (!CheckSessionValidity(req.sessionID))
            {
                stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                return;
            }

            // Get the list of columns to search, reject if the table was invalid.
            Dictionary<string, ColumnRecord.Column> columns;
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

            string command = "SELECT " + string.Join(", ", req.select) + " FROM " + req.table;
            List<string> conditions = new();
            foreach (KeyValuePair<string, ColumnRecord.Column> kvp in columns)
            {
                if (ColumnRecord.IsTypeString(kvp.Value))
                    conditions.Add(kvp.Key + " LIKE \'%" + req.value + "%'");
            }

            command += " WHERE " + string.Join(" OR ", conditions);

            SqlCommand com = new SqlCommand(command, sqlConnect);

            SelectResult result = new(com.ExecuteReader());
            stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
            sr.WriteAndFlush(stream, sr.Serialise(result));
        }
        catch (Exception e)
        {
            LogError("Couldn't run or return query, see error:", e);
            stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);
        }
        finally
        {
            if (sqlConnect.State == System.Data.ConnectionState.Open)
                sqlConnect.Close();
        }
    }

    private static void ClientUpdate(NetworkStream stream, SqlConnection sqlConnect, int target)
    {
        try
        {
            sqlConnect.Open();
            SqlCommand com = new SqlCommand("", sqlConnect);

            bool sessionValid = false;

            if (target == Glo.CLIENT_UPDATE_ORGANISATION)
            {
                Organisation newRow = sr.Deserialise<Organisation>(sr.ReadString(stream));
                if (CheckSessionValidity(newRow.sessionID, out sessionValid))
                    com.CommandText = newRow.SqlUpdate(clientSessions[newRow.sessionID].loginID);
            }
            else if (target == Glo.CLIENT_UPDATE_ASSET)
            {
                Asset newRow = sr.Deserialise<Asset>(sr.ReadString(stream));
                if (CheckSessionValidity(newRow.sessionID, out sessionValid))
                    com.CommandText = newRow.SqlUpdate(clientSessions[newRow.sessionID].loginID);
            }
            else if (target == Glo.CLIENT_UPDATE_CONTACT)
            {
                Contact newRow = sr.Deserialise<Contact>(sr.ReadString(stream));
                if (CheckSessionValidity(newRow.sessionID, out sessionValid))
                    com.CommandText = newRow.SqlUpdate();
            }
            else if (target == Glo.CLIENT_UPDATE_CONFERENCE_TYPE)
            {
                ConferenceType newRow = sr.Deserialise<ConferenceType>(sr.ReadString(stream));
                if (CheckSessionValidity(newRow.sessionID, out sessionValid))
                    com.CommandText = newRow.SqlUpdate();
            }
            else if (target == Glo.CLIENT_UPDATE_CONFERENCE)
            {
                Conference newRow = sr.Deserialise<Conference>(sr.ReadString(stream));
                //if (CheckSessionValidity(newRow.sessionID, out sessionValid))
                //    com.CommandText = newRow.SqlUpdate();
            }
            else if (target == Glo.CLIENT_UPDATE_RESOURCE)
            {
                Resource newRow = sr.Deserialise<Resource>(sr.ReadString(stream));
                //if (CheckSessionValidity(newRow.sessionID, out sessionValid))
                //    com.CommandText = newRow.SqlUpdate();
            }
            else if (target == Glo.CLIENT_UPDATE_LOGIN)
            {
                Login newRow = sr.Deserialise<Login>(sr.ReadString(stream));
                if (CheckSessionValidity(newRow.sessionID, out sessionValid))
                    com.CommandText = newRow.SqlUpdate();
            }

            if (!sessionValid)
            {
                stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                return;
            }

            try
            {
                com.ExecuteNonQuery();
            }
            finally
            {
                stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
            }
        }
        catch (Exception e)
        {
            LogError("Couldn't run update, see error:", e);
            stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);
        }
        finally
        {
            if (sqlConnect.State == System.Data.ConnectionState.Open)
                sqlConnect.Close();
        }
    }

    private static void ClientDelete(NetworkStream stream, SqlConnection sqlConnect)
    {
        try
        {
            sqlConnect.Open();
            DeleteRequest req = sr.Deserialise<DeleteRequest>(sr.ReadString(stream));
            if (!CheckSessionValidity(req.sessionID))
            {
                stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                return;
            }

            SqlCommand com = new SqlCommand(req.SqlDelete(), sqlConnect);

            if (com.ExecuteNonQuery() == 0)
                stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);
            else
                stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
        }
        catch (Exception e)
        {
            LogError("Couldn't delete record, see error:", e);
            stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);
        }
        finally
        {
            if (sqlConnect.State == System.Data.ConnectionState.Open)
                sqlConnect.Close();
        }
    }

    private static void ClientLinkContact(NetworkStream stream, SqlConnection sqlConnect)
    {
        try
        {
            sqlConnect.Open();
            LinkContactRequest req = sr.Deserialise<LinkContactRequest>(sr.ReadString(stream));
            if (!CheckSessionValidity(req.sessionID))
            {
                stream.WriteByte(Glo.CLIENT_SESSION_INVALID);
                return;
            }

            SqlCommand com;
            if (req.unlink)
                com = new SqlCommand(req.SqlDelete(), sqlConnect);
            else
                com = new SqlCommand(req.SqlInsert(), sqlConnect);

            if (com.ExecuteNonQuery() == 0)
                stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);
            else
                stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
        }
        catch (Exception e)
        {
            LogError("Couldn't create or modify organisation/contact link, see error:", e);
            stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);
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
            if (!CheckSessionValidity(req.sessionID))
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
            stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);
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
            if (!CheckSessionValidity(req.sessionID))
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
            stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);
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
            if (!CheckSessionValidity(req.sessionID))
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
            stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);
        }
        finally
        {
            if (sqlConnect.State == ConnectionState.Open)
                sqlConnect.Close();
        }
    }
}
