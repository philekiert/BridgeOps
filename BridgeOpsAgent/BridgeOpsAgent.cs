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

internal class BridgeOpsAgent
{
    static UnicodeEncoding unicodeEncoding = new UnicodeEncoding();
    static SendReceive sr = new SendReceive();

    struct ClientSession
    {
        public string username;
        public string ip;
        public ClientSession(string username, string ip)
        { this.username = username; this.ip = ip; }
    }
    static Dictionary<string, ClientSession> clientSessions = new Dictionary<string, ClientSession>();
    static bool CheckSessionValidity(string id)
    {
        return clientSessions.ContainsKey(id);
    }
    // If you need to check the quickly condition, but need the bool outside of scope.
    static bool CheckSessionValidity(string id, out bool result)
    {
        result = clientSessions.ContainsKey(id);
        return result;
    }

    // Multiple threads may try to access this at once, so hold them up if necessary.
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


    // Apart from the console generating the the database, Agent is the only one that needs access to SQL Server.
    private static string connectionString = "server=localhost\\SQLEXPRESS;" +
                                             "integrated security=SSPI;" +
                                             //"user id=sa; password=^2*Re98E;" +
                                             "encrypt=false;" +
                                             "database=BridgeOps;" +
                                             "Application Name=BridgeOpsAgent;";

    private static void Main(string[] args)
    {
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

        // Start the thread responsible for handling requests from the server console.
        Thread bridgeOpsConsoleRequestsThr = new Thread(BridgeOpsConsoleRequests);
        bridgeOpsConsoleRequestsThr.Start();

        // Start the thread responsible for handling requests from the clients.
        Thread bridgeOpsLocalClientRequestsThr = new Thread(BridgeOpsClientRequests);
        bridgeOpsLocalClientRequestsThr.Start();
    }


    //   T H R E A D   F U N C T I O N S

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
        listener.Start();

        while (true)
        {
            // Start an ASync Accept.
            IAsyncResult result = listener.BeginAcceptTcpClient(HandleClientListenAccept, listener);
            // Do not accept any 
            autoResetEvent.WaitOne();
            autoResetEvent.Reset();

            Thread.Sleep(10);
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
                else if (fncByte == Glo.CLIENT_NEW_ORGANISATION ||
                         fncByte == Glo.CLIENT_NEW_CONTACT ||
                         fncByte == Glo.CLIENT_NEW_ASSET ||
                         fncByte == Glo.CLIENT_NEW_CONFERENCE_TYPE ||
                         fncByte == Glo.CLIENT_NEW_CONFERENCE ||
                         fncByte == Glo.CLIENT_NEW_RESOURCE ||
                         fncByte == Glo.CLIENT_NEW_LOGIN)
                    ClientNewInsert(stream, sqlConnect, fncByte);
                else if (fncByte == Glo.CLIENT_SELECT_COLUMN_PRIMARY)
                    ClientSelectColumnPrimary(stream, sqlConnect);

                else
                    throw new Exception("Received int " + fncByte + " does not correspond to a function.");
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

            // Prepare variables if else block below.
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
            SqlCommand sqlCommand = new SqlCommand(string.Format("SELECT * FROM Login WHERE (Username = '{0}' AND " +
                                                   "Password = HASHBYTES('SHA2_512', '{1}'));",
                                                   loginReq.username, loginReq.password), sqlConnect);

            if (sqlCommand.ExecuteScalar() != null)
            {
                // Assuming login was successful, kick out any sessions with clashing IPs or usernames.
                if (userAlreadyLoggedIn != "")
                    clientSessions.Remove(userAlreadyLoggedIn);
                if (ipAlreadyConnected != "" && ipAlreadyConnected != userAlreadyLoggedIn)
                    clientSessions.Remove(ipAlreadyConnected);

                // Generate a random unique session ID.
                string key;
                do
                {
                    key = "";
                    for (int n = 0; n < 16; ++n)
                        key += (char)(rnd.Next() % 256);
                }
                while (clientSessions.ContainsKey(key));

                clientSessions.Add(key, new ClientSession(loginReq.username, ipStr));

                sr.WriteAndFlush(stream, Glo.CLIENT_LOGIN_ACCEPT + key);
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

    private static void ClientSessionLogout(NetworkStream stream)
    {
        string sessionDetails = sr.ReadString(stream);
        LogoutRequest logoutReq = sr.Deserialise<LogoutRequest>(sessionDetails);

        if (clientSessions.ContainsKey(logoutReq.sessionID))
        {
            clientSessions.Remove(logoutReq.sessionID);
            sr.WriteAndFlush(stream, Glo.CLIENT_LOGOUT_ACCEPT);
        }
        else
            sr.WriteAndFlush(stream, Glo.CLIENT_LOGOUT_SESSION_INVALID);

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
                com.CommandText = newRow.SqlInsert();
            }
            else if (target == Glo.CLIENT_NEW_CONTACT)
            {
                Contact newRow = sr.Deserialise<Contact>(sr.ReadString(stream));
                if (CheckSessionValidity(newRow.sessionID, out sessionValid))
                    com.CommandText = newRow.SqlInsert();
            }
            else if (target == Glo.CLIENT_NEW_ASSET)
            {
                Asset newRow = sr.Deserialise<Asset>(sr.ReadString(stream));
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

            if (com.ExecuteNonQuery() == 0)
                stream.WriteByte(Glo.CLIENT_REQUEST_FAILED);
            else
                stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
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
            string result = "";
            while (reader.Read())
                result += reader.GetString(0) + ';';
            // Remove the trailing semicolon.
            if (result.Length > 0)
                result = result.Remove(result.Length - 1);
            stream.WriteByte(Glo.CLIENT_REQUEST_SUCCESS);
            sr.WriteAndFlush(stream, result);
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
}
