using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace SendReceiveClasses
{
    public class SendReceive
    {
        // Inbound/outbound from perspective of server.
        public static int portInbound;  // Should be the same for all instances.
        public static int portOutbound; // Should be the same for all isntances.

        static JsonSerializerOptions jsonOpts = new JsonSerializerOptions();
        static UnicodeEncoding unicodeEncoding = new UnicodeEncoding();

        public SendReceive()
        {
            jsonOpts.IncludeFields = true;
        }

        public string Serialise<T>(T classObj)
        {
            return JsonSerializer.Serialize(classObj, jsonOpts);
        }
        public T? Deserialise<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, jsonOpts);
        }

        public byte[] PrepareBytes(string send)
        {
            byte[] sendBytes = unicodeEncoding.GetBytes(send);
            List<byte> lB = new List<byte>();
            lB.AddRange(BitConverter.GetBytes(sendBytes.Length));
            lB.AddRange(sendBytes);
            return lB.ToArray();
        }
        public void WriteAndFlush(NamedPipeClientStream server, string send)
        {
            server.Write(PrepareBytes(send));
            server.Flush();
        }
        public void WriteAndFlush(NamedPipeServerStream server, string send)
        {
            server.Write(PrepareBytes(send));
            server.Flush();
        }
        public void WriteAndFlush(NetworkStream stream, string send)
        {
            stream.Write(PrepareBytes(send));
            stream.Flush();
        }

        public string ReadString(NamedPipeClientStream server)
        {
            byte[] lengthBytes = new byte[4];
            server.Read(lengthBytes);
            int length = BitConverter.ToInt32(lengthBytes);
            byte[] bString = new byte[length];
            server.Read(bString);
            return unicodeEncoding.GetString(bString);
        }
        public string ReadString(NamedPipeServerStream server)
        {
            byte[] lengthBytes = new byte[4];
            server.Read(lengthBytes);
            int length = BitConverter.ToInt32(lengthBytes);
            byte[] bString = new byte[length];
            server.Read(bString);
            return unicodeEncoding.GetString(bString);
        }
        public string ReadString(NetworkStream stream)
        {
            byte[] lengthBytes = new byte[4];
            int bytesRead = 0;
            // Allow at least 2 seconds to receive stream.
            for (int attempts = 0; attempts < 200 && bytesRead < 4; ++attempts)
            {
                bytesRead += stream.Read(lengthBytes, bytesRead, 4 - bytesRead);
                if (bytesRead < 4)
                    Thread.Sleep(10);
            }
            int length = BitConverter.ToInt32(lengthBytes);
            byte[] bString = new byte[length];
            bytesRead = 0;
            for (int attempts = 0; attempts < 200 && bytesRead < length; ++attempts)
            {
                bytesRead += stream.Read(bString, bytesRead, length - bytesRead);
                if (bytesRead < length)
                    Thread.Sleep(10);
            }
            return unicodeEncoding.GetString(bString);
        }
            
        public NamedPipeServerStream NewServerNamedPipe(string pipeName) // Inbound to server.
        {
            // timeout not supported on this type of stream.
            return new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1);
        }
        public NamedPipeClientStream NewClientNamedPipe(string pipeName) // Inbound to server.
        {
            // timeout not supported on this type of stream.
            return new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.None,
                                                                          TokenImpersonationLevel.Impersonation);
        }

        // Only use this if you don't need to do stuff with the client afterwards.
        public NetworkStream? NewClientNetworkStream(IPEndPoint ep)
        {
            try
            {
                TcpClient client = new TcpClient();
                client.Connect(ep);
                NetworkStream stream = client.GetStream();
                stream.ReadTimeout = 5000;
                return stream;
            }
            catch
            {
                return null;
            }
        }
    }


    //   C O N N E C T E D   C L I E N T S

    struct ConnectedClient
    {
        public string ip;
        public string username;

        public ConnectedClient(string ip, string username)
        {
            this.ip = ip;
            this.username = username;
        }
    }
    class ConnectedClients
    {
        // Bits of information like IP, login name, etc.
        public List<ConnectedClient> connectedClients = new List<ConnectedClient>();

        public void Add(string ip, string username)
        {
            connectedClients.Add(new ConnectedClient(ip, username));
        }
    }


    //   S E S S I O N   M A N A G E M E N T

    struct LoginRequest
    {
        public string username;
        public string password;

        public LoginRequest(string username, string password)
        {
            this.username = username;
            this.password = password;
        }
    }

    struct LogoutRequest
    {
        public string sessionID;
        public string username;

        public LogoutRequest(string sessionID, string username)
        {
            this.sessionID = sessionID;
            this.username = username;
        }
    }


    //   D A T A B A S E   I N T E R A C T I O N

    struct ColumnAddition
    {
        public string tableName;
        public string columnName;
        public string columnType;
        public string[] columnAllowed;

        public ColumnAddition(string tableName, string columnName, string columnType, string[] columnAllowed)
        {
            this.tableName = tableName;
            this.columnName = columnName;
            this.columnType = columnType;
            this.columnAllowed = columnAllowed;
        }
    }

    struct Organisation
    {
        public string sessionID;
        public string organisationID;
        public string? parentOrgID;
        public string? dialNo;
        public string? notes;
        public List<string> additionalCols;
        public List<string?> additionalVals;

        public Organisation(string sessionID, string organisationID, string? parentOrgID,
                            string? dialNo, string? notes, List<string> additionalCols,
                                                           List<string?> additionalVals)
        {
            this.sessionID = sessionID;
            this.organisationID = organisationID;
            this.parentOrgID = parentOrgID;
            this.dialNo = dialNo;
            this.notes = notes;
            this.additionalCols = additionalCols;
            this.additionalVals = additionalVals;

            SqlAssist.LevelAdditionals(ref additionalCols, ref additionalVals);
        }

        public string SqlInsert()
        {
            return SqlAssist.InsertInto("Organisation",
                                        SqlAssist.ColConcat(additionalCols, Glo.Tab.ORGANISATION_ID,
                                                                            Glo.Tab.PARENT_ID,
                                                                            Glo.Tab.DIAL_NO,
                                                                            "Notes"),
                                        SqlAssist.ValConcat(additionalVals, organisationID,
                                                                            parentOrgID,
                                                                            dialNo,
                                                                            notes));
        }
    }

    struct Contact
    {
        public string sessionID;
        public int contactID = -1;
        public string? notes;
        public List<string> additionalCols;
        public List<string?> additionalVals;

        public Contact(string sessionID, int contactID, string? notes, List<string> additionalCols,
                                                                       List<string?> additionalVals)
        {
            this.sessionID = sessionID;
            this.contactID = contactID;
            this.notes = notes;
            this.additionalCols = additionalCols;
            this.additionalVals = additionalVals;
            SqlAssist.LevelAdditionals(ref additionalCols, ref additionalVals);
        }

        public string SqlInsert()
        {
            return SqlAssist.InsertInto("Contact",
                                        SqlAssist.ColConcat(additionalCols,
                                                            "Notes"),
                                        SqlAssist.ValConcat(additionalVals, notes));
        }
    }

    struct Asset
    {
        public string sessionID;
        public string assetID;
        public string? organisationID;
        public string? notes;
        public List<string> additionalCols;
        public List<string?> additionalVals;

        public Asset(string sessionID, string assetID, string? organisationID, string? notes,
                     List<string> additionalCols,
                     List<string?> additionalVals)
        {
            this.sessionID = sessionID;
            this.assetID = assetID;
            this.organisationID = organisationID;
            this.notes = notes;
            this.additionalCols = additionalCols;
            this.additionalVals = additionalVals;

            SqlAssist.LevelAdditionals(ref additionalCols, ref additionalVals);
        }

        public string SqlInsert()
        {
            return SqlAssist.InsertInto("Asset",
                                        SqlAssist.ColConcat(additionalCols, Glo.Tab.ASSET_ID,
                                                                            Glo.Tab.ORGANISATION_ID,
                                                                            "Notes"),
                                        SqlAssist.ValConcat(additionalVals, assetID,
                                                                            organisationID,
                                                                            notes));
        }
    }

    struct ConferenceType
    {
        public string sessionID;
        public int typeID;
        public string name;

        public ConferenceType(string sessionID, int typeID, string name)
        {
            this.sessionID = sessionID;
            this.typeID = typeID;
            this.name = name;
        }

        public string SqlInsert()
        {
            return "INSERT INTO ConferenceType (" + Glo.Tab.CONFERENCE_TYPE_NAME + ") VALUES (" + name + ");";
        }
    }

    struct Conference
    {
        public string sessionID;
        public int conferenceID;
        public int typeID;
        public string? title;
        public DateTime start;
        public DateTime end;
        public TimeSpan buffer;
        public string? organisationID;
        public int? recurrenceID;
        public string? notes;
        public List<string> additionalCols;
        public List<string?> additionalVals;

        public Conference(string sessionID, int conferenceID, int typeID, string? title,
                          DateTime start, DateTime end, TimeSpan buffer,
                          string? organisationID, int? recurrenceID, string? notes, List<string> additionalCols,
                                                                                    List<string?> additionalVals)
        {
            this.sessionID = sessionID;
            this.conferenceID = conferenceID;
            this.typeID = typeID;
            this.title = title;
            this.start = start;
            this.end = end;
            this.buffer = buffer;
            this.organisationID = organisationID;
            this.recurrenceID = recurrenceID;
            this.notes = notes;
            this.additionalCols = additionalCols;
            this.additionalVals = additionalVals;

            SqlAssist.LevelAdditionals(ref additionalCols, ref additionalVals);
        }

        public string SqlInsert()
        {
            return SqlAssist.InsertInto("Conference",
                                        SqlAssist.ColConcat(additionalCols, Glo.Tab.CONFERENCE_TYPE,
                                                                            Glo.Tab.CONFERENCE_TITLE,
                                                                            Glo.Tab.CONFERENCE_START,
                                                                            Glo.Tab.CONFERENCE_END,
                                                                            Glo.Tab.CONFERENCE_BUFFER,
                                                                            Glo.Tab.ORGANISATION_ID,
                                                                            Glo.Tab.RECURRENCE_ID,
                                                                            "Notes"),
                                        SqlAssist.ValConcat(additionalVals, typeID.ToString(),
                                                                            title,
                                                                            SqlAssist.DateTimeToSQLType(start),
                                                                            SqlAssist.DateTimeToSQLType(end),
                                                                            SqlAssist.TimeSpanToSQLType(buffer),
                                                                            organisationID,
                                                                            recurrenceID.ToString(),
                                                                            notes));
        }
    }

    struct Resource
    {
        public string sessionID;
        public int resourceID;
        public string name;
        public DateTime availableFrom;
        public DateTime availableTo;
        public int capacity;

        public Resource(string sessionID, int resourceID, string name,
                        DateTime availableFrom, DateTime availableTo, int capacity)
        {
            this.sessionID = sessionID;
            this.resourceID = resourceID;
            this.name = name;
            this.availableFrom = availableFrom;
            this.availableTo = availableTo;
            this.capacity = capacity;
        }

        public string SqlInsert()
        {
            return SqlAssist.InsertInto("Contact",
                                        SqlAssist.ColConcat(Glo.Tab.RESOURCE_NAME,
                                                            Glo.Tab.RESOURCE_FROM,
                                                            Glo.Tab.RESOURCE_TO,
                                                            Glo.Tab.RESOURCE_CAPACITY),
                                        SqlAssist.ValConcat(name,
                                                            SqlAssist.DateTimeToSQLType(availableFrom),
                                                            SqlAssist.DateTimeToSQLType(availableTo),
                                                            capacity.ToString()));
        }
    }

    struct Login
    {
        public string sessionID;
        public int loginID;
        public string username;
        public string password;
        public int type;

        public Login(string sessionID, int loginID, string username, string password, int type)
        {
            this.sessionID = sessionID;
            this.loginID = loginID;
            this.username = username;
            this.password = password;
            this.type = type;
        }

        public string SqlInsert()
        {
            return "INSERT INTO Login (" + Glo.Tab.LOGIN_USERNAME + ", " + Glo.Tab.LOGIN_PASSWORD + ", " +
                                           Glo.Tab.LOGIN_TYPE + ") VALUES ('" +
                                           username + "', HASHBYTES('SHA2_512', '" + password + "'), " +
                                           type.ToString() + "');";
        }
    }

    struct PrimaryColumnSelect
    {
        public string sessionID;
        public string table;
        public string column;

        public PrimaryColumnSelect(string sessionID, string table, string column)
        {
            this.sessionID = sessionID;
            this.table = table;
            this.column = column;
        }

        public string SqlSelect { get { return "SELECT " + column + " FROM " + table + ";"; } }
    }

    //   H E L P E R   F U N C T I O N S

    public class SqlAssist
    {
        public static void LevelAdditionals(ref List<string> columns, ref List<string?> values)
        {
            if (columns.Count > values.Count)
                columns.RemoveRange(values.Count, columns.Count - values.Count);
            else if (values.Count > columns.Count)
                values.RemoveRange(columns.Count, values.Count - columns.Count);
        }

        public static string InsertInto(string tableName, string columns, string values)
        {
            return "INSERT INTO " + tableName + " (" + columns + ") VALUES (" + values + ");";
        }

        public static string ValConcat(params string[] values)
        {
            return ValConcat(new List<string?>(), values);
        }
        public static string ValConcat(List<string?> additionalVals, params string?[] setValues)
        {
            additionalVals.AddRange(setValues);
            if (additionalVals.Count > 0)
            {
                for (int i = 0; i < additionalVals.Count; ++i)
                    if (additionalVals[i] == null)
                        additionalVals[i] = "NULL";
                    else
#pragma warning disable CS8602
                        additionalVals[i] = "'" + additionalVals[i].Replace("'", "''") + "'";
#pragma warning restore CS8602
                return string.Join(", ", additionalVals);
            }
            else return "";
        }
        public static string ColConcat(params string[] columns)
        {
            return ColConcat(new List<string>(), columns);
        }
        public static string ColConcat(List<string> additionalCols, params string[] setColumns)
        {
            additionalCols.AddRange(setColumns);
            if (additionalCols.Count > 0)
            {
                string concat = additionalCols[0];
                for (int n = 1; n < additionalCols.Count; ++n)
                    concat += ", " + additionalCols[n];
                return concat;
            }
            else return "";
        }

        public static string DateTimeToSQLType(DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
        public static string TimeSpanToSQLType(TimeSpan timeSpan)
        {
            return timeSpan.ToString(@"dd\.hh\:mm");
        }
    }
}