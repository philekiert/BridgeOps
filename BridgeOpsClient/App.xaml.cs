using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using SendReceiveClasses;

namespace BridgeOpsClient
{
    public partial class App : Application
    {
        public static bool IsLoggedIn { get { return sd.sessionID != ""; } }

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
            if (sd.sessionID != "")
            {
                NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
                if (stream != null)
                {
                    stream.WriteByte(Glo.CLIENT_LOGOUT);
                    sr.WriteAndFlush(stream, sr.Serialise(
                                                            new LogoutRequest(sd.sessionID,
                                                            sd.username)));
                    sr.ReadString(stream); // Empty the pipe.

                    return true;
                }
                else
                    return false;
            }
            else
                return false;
        }

        public static bool PullColumnRecord()
        {
            NetworkStream? stream = sr.NewClientNetworkStream(sd.ServerEP);
            if (stream == null)
                return false;
            try
            {
                stream.WriteByte(Glo.CLIENT_PULL_COLUMN_RECORD);
                ColumnRecord.Initialise(sr.ReadString(stream));
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class SessionDetails
    {
        public string sessionID = "";
        public string username = "";

        public int portInbound = 1337; // Inbound to server.
        public int portOutbound = 1338; // Outbound from server.
        public IPAddress serverIP = new IPAddress(new byte[] { 127, 0, 0, 1 });
        public IPEndPoint ServerEP { get { return new IPEndPoint(serverIP, portInbound); } }
    }

    static class ColumnRecord
    {
        // Add to this as development continues.
        struct Column
        {
            // column name is stored as the key in Dictionary
            public string type;
            public string restriction; // Only for character strings
            public string[] allowed; // Allowed values, only ever present in user-added columns
            public string friendlyName;

            public Column(string type, string restriction, string[] allowed, string friendlyName)
            {
                this.type = type;
                this.restriction = restriction;
                this.allowed = allowed;
                this.friendlyName = friendlyName;
            }
        }

        static Dictionary<string, Column> organisation = new Dictionary<string, Column>();
        static Dictionary<string, Column> contact = new Dictionary<string, Column>();
        static Dictionary<string, Column> asset = new Dictionary<string, Column>();
        static Dictionary<string, Column> conferenceType = new Dictionary<string, Column>();
        static Dictionary<string, Column> conference = new Dictionary<string, Column>();
        static Dictionary<string, Column> conferenceRecurrence = new Dictionary<string, Column>();
        static Dictionary<string, Column> resource = new Dictionary<string, Column>();
        static Dictionary<string, Column> login = new Dictionary<string, Column>();

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

                    int r;
                    Column col = new Column(int.TryParse(restriction, out r) ? "Text" : restriction,
                                            restriction, allowedArr, "");

                    if (table == "Organisation")
                        organisation.Add(column, col);
                    else if (table == "Contact")
                        contact.Add(column, col);
                    else if (table == "Asset")
                        asset.Add(column, col);
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
                    // The option is open for the use to specify friendly names using spaces instead of
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
                    if (friendlySplit[0] == "Contact")
                        AddFriendlyName(contact);
                    if (friendlySplit[0] == "Asset")
                        AddFriendlyName(asset);
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
            }
            catch (Exception e)
            {
                // File corrupted
            }
        }
    }

    class AgentCheckUp
    {


        void ListenAndRespond()
        {


            while (true)
            {

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
