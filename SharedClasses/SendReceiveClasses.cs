using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics.Metrics;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.Xml;
using System.Security.Policy;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            jsonOpts.UnknownTypeHandling = System.Text.Json.Serialization.JsonUnknownTypeHandling.JsonNode;
        }

        public string Serialise<T>(T classObj)
        {
            return JsonSerializer.Serialize(classObj, jsonOpts);
        }
        public T? Deserialise<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, jsonOpts);
        }

        public int ReadByte(NetworkStream stream)
        {
            if (!stream.DataAvailable)
            {
                Thread.Sleep(20);
            }
            return stream.ReadByte();
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
        public int loginID;

        public LogoutRequest(string sessionID, int loginID)
        {
            this.sessionID = sessionID;
            this.loginID = loginID;
        }
    }

    struct PasswordResetRequest
    {
        public string sessionID;
        public int loginID;
        public string password;
        public string newPassword;

        public PasswordResetRequest(string sessionID, int loginID, string password, string newPassword)
        {
            this.sessionID = sessionID;
            this.loginID = loginID;
            this.password = password;
            this.newPassword = newPassword;
        }

        public string SqlUpdate()
        {
            return SqlAssist.Update("Login", SqlAssist.Setter(Glo.Tab.LOGIN_PASSWORD,
                                                              SqlAssist.HashBytes(newPassword)),
                                             Glo.Tab.LOGIN_ID, loginID.ToString());
        }
    }


    //   D A T A B A S E   I N T E R A C T I O N

    struct TableModification
    {
        public enum Intent { Addition, Removal, Modification }

        public string sessionID;
        public Intent intent;
        public string table;
        public string column;
        public string? columnRename;
        public string? columnType;
        public List<string> allowed;

        // Removal
        public TableModification(string sessionID, string table, string column)
        {
            this.sessionID = sessionID;
            intent = Intent.Removal;
            this.table = table;
            this.column = column;
            columnRename = null;
            columnType = null;
            allowed = new();
        }

        // Addition
        public TableModification(string sessionID, string table, string column,
                                 string columnType, List<string> allowed)
        {
            this.sessionID = sessionID;
            intent = Intent.Addition;
            this.table = table;
            this.column = column;
            columnRename = null;
            this.columnType = columnType;
            this.allowed = allowed;
        }

        // Modification
        public TableModification(string sessionID, string table, string column,
                                 string? columnRename, string? columnType, List<string> allowed, bool wipeAllowed)
        {
            this.sessionID = sessionID;
            intent = Intent.Modification;
            this.table = table;
            this.column = column;
            this.columnRename = columnRename;
            this.columnType = columnType;
            this.allowed = allowed;
        }

        private void Prepare()
        {
            // SecureColumn() should suffice here for table names and types.
            table = SqlAssist.SecureColumn(table);
            column = SqlAssist.SecureColumn(column);

            if (columnRename != null)
                columnRename = SqlAssist.SecureColumn(columnRename);

            if (columnType != null)
                columnType = SqlAssist.SecureColumn(columnType);

            for (int n = 0; n < allowed.Count; ++n)
                allowed[n] = SqlAssist.AddQuotes(SqlAssist.SecureColumn(allowed[n]));
        }

        public string SqlCommand()
        {
            Prepare();

            string command;

            if (intent == Intent.Addition)
            {
                command = $"ALTER TABLE {table} ";
                command += $"ADD {column} {(columnType == "BOOLEAN" ? "BIT" : columnType)}";
                if (allowed != null)
                {
                    command += $" CONSTRAINT chk_{table}{column}" +
                               $" CHECK ({column} IN ('{string.Join("\',\'", allowed)}'))";
                }
                command += ";";
            }
            else if (intent == Intent.Removal)
            {
                command = $"ALTER TABLE {table} DROP {column};";
            }
            else // if (intent == Intent.Modification)
            {
                bool droppedConstraint = false;
                List<string> commands = new();
                if (columnRename != null)
                {
                    commands.Add($"ALTER TABLE {table} RENAME COLUMN {column} TO {columnRename};");
                }
                if (allowed.Count == 0)
                {
                    commands.Add($"IF EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS " +
                                 $"WHERE CONSTRAINT_TYPE = 'CHECK' AND CONSTRAINT_NAME = 'chk_{table}{column}') " +
                                  "BEGIN " +
                                 $" ALTER TABLE {table} DROP CONSTRAINT chk_{table}{column} " +
                                  "END;");
                    droppedConstraint = true;
                }
                if (columnType != null)
                {
                    commands.Add($"ALTER TABLE {table} ALTER COLUMN {column} {columnType};");
                    if (!droppedConstraint)
                    {
                        // The constraint needs to be dropped and remade due to the column being renamed.
                        commands.Add($"IF EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS " +
                                     $"WHERE CONSTRAINT_TYPE = 'CHECK' AND CONSTRAINT_NAME = 'chk_{table}{column}') " +
                                      "BEGIN " +
                                     $" ALTER TABLE {table} DROP CONSTRAINT chk_{table}{column} " +
                                      "END;");
                        droppedConstraint = true;
                    }
                }
                if (allowed.Count > 0)
                {
                    if (!droppedConstraint)
                    {
                        commands.Add($"IF EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS " +
                                     $"WHERE CONSTRAINT_TYPE = 'CHECK' AND CONSTRAINT_NAME = 'chk_{table}{column}') " +
                                      "BEGIN " +
                                     $" ALTER TABLE {table} DROP CONSTRAINT chk_{table}{column} " +
                                      "END;");
                    }
                    commands.Add($"ALTER TABLE {table}" +
                                 $"CHECK ({(columnRename == null ? column : columnRename)} " +
                                 $"IN ('{string.Join("\',\'", allowed)}'));");
                }

                if (commands.Count == 1)
                    command = commands[0];
                else
                    command = "BEGIN TRANSACTION; " + string.Join(" ", commands) + " COMMIT TRANSACTION;";
            }

            return command;
        }
    }

    struct Organisation
    {
        public string sessionID;
        public string organisationID;
        public string? parentOrgID;
        public string? dialNo;
        public string? notes;
        public bool parentOrgIdChanged;
        public bool dialNoChanged;
        public bool notesChanged;
        public List<string> additionalCols;
        public List<string?> additionalVals;
        public List<bool> additionalNeedsQuotes;
        public bool changeTracked;
        public string changeReason = "";

        public Organisation(string sessionID, string organisationID, string? parentOrgID,
                            string? dialNo, string? notes, List<string> additionalCols,
                                                           List<string?> additionalVals,
                                                           List<bool> additionalNeedsQuotes)
        {
            this.sessionID = sessionID;
            this.organisationID = organisationID;
            this.parentOrgID = parentOrgID;
            this.dialNo = dialNo;
            this.notes = notes;
            this.additionalCols = additionalCols;
            this.additionalVals = additionalVals;
            this.additionalNeedsQuotes = additionalNeedsQuotes;
            parentOrgIdChanged = false;
            dialNoChanged = false;
            notesChanged = false;
            changeTracked = false;
            changeReason = "";
        }
        private void Prepare()
        {
            // Make sure the columns and values are safe, then add quotes where needed.
            organisationID = SqlAssist.AddQuotes(SqlAssist.SecureValue(organisationID));
            if (parentOrgID != null)
                parentOrgID = SqlAssist.AddQuotes(SqlAssist.SecureValue(parentOrgID));
            if (dialNo != null)
                dialNo = SqlAssist.AddQuotes(SqlAssist.SecureValue(dialNo));
            if (notes != null)
                notes = SqlAssist.AddQuotes(SqlAssist.SecureValue(notes));
            SqlAssist.SecureColumn(additionalCols);
            SqlAssist.SecureValue(additionalVals);
            SqlAssist.AddQuotes(additionalVals, additionalNeedsQuotes);
            if (changeTracked && changeReason != null)
                changeReason = SqlAssist.AddQuotes(SqlAssist.SecureValue(changeReason));
        }

        public string SqlInsert(int loginID)
        {
            Prepare();

            string com = SqlAssist.InsertInto("Organisation",
                                              SqlAssist.ColConcat(additionalCols, Glo.Tab.ORGANISATION_ID,
                                                                                  Glo.Tab.PARENT_ID,
                                                                                  Glo.Tab.DIAL_NO,
                                                                                  Glo.Tab.NOTES),
                                              SqlAssist.ValConcat(additionalVals, organisationID,
                                                                                  parentOrgID,
                                                                                  dialNo,
                                                                                  notes));
            // Create a first change instance.
            additionalCols.RemoveRange(additionalCols.Count - 4, 4); // ColConcat and ValConcat added the main fields
            additionalVals.RemoveRange(additionalVals.Count - 4, 4); // to the Lists, so walk that back here.
            int initialCount = additionalCols.Count;
            for (int i = 0; i < initialCount; ++i)
            {
                additionalCols.Add(additionalCols[i] + Glo.Tab.CHANGE_REGISTER_SUFFIX);
                additionalVals.Add("1");
            }
            com += SqlAssist.InsertInto("OrganisationChange",
                                        SqlAssist.ColConcat(additionalCols,
                                                            Glo.Tab.ORGANISATION_ID,
                                                            Glo.Tab.CHANGE_TIME,
                                                            Glo.Tab.LOGIN_ID,
                                                            Glo.Tab.CHANGE_REASON,
                                                            Glo.Tab.PARENT_ID,
                                                            Glo.Tab.PARENT_ID + Glo.Tab.CHANGE_REGISTER_SUFFIX,
                                                            Glo.Tab.DIAL_NO,
                                                            Glo.Tab.DIAL_NO + Glo.Tab.CHANGE_REGISTER_SUFFIX,
                                                            Glo.Tab.NOTES,
                                                            Glo.Tab.NOTES + Glo.Tab.CHANGE_REGISTER_SUFFIX),
                                        SqlAssist.ValConcat(additionalVals,
                                                            organisationID,
                                                            '\'' + SqlAssist.DateTimeToSQLType(DateTime.Now) + '\'',
                                                            loginID.ToString(),
                                                            "'Created new organisation.'",
                                                            parentOrgID, "1",
                                                            dialNo, "1",
                                                            notes, "1")); ;
            return SqlAssist.Transaction(com);
        }

        public string SqlUpdate(int loginID)
        {
            Prepare();

            List<string> setters = new();
            if (parentOrgIdChanged)
                setters.Add(SqlAssist.Setter(Glo.Tab.PARENT_ID, parentOrgID));
            if (dialNoChanged)
                setters.Add(SqlAssist.Setter(Glo.Tab.DIAL_NO, dialNo));
            if (notesChanged)
                setters.Add(SqlAssist.Setter(Glo.Tab.NOTES, notes));
            for (int i = 0; i < additionalCols.Count; ++i)
                setters.Add(SqlAssist.Setter(additionalCols[i], additionalVals[i]));
            string command = SqlAssist.Update("Organisation", string.Join(", ", setters),
                                              Glo.Tab.ORGANISATION_ID, organisationID);

            if (changeTracked)
            {
                // Add _Register bools for each column affected.
                int initialCount = additionalCols.Count;
                for (int i = 0; i < initialCount; ++i)
                {
                    additionalCols.Add(additionalCols[i] + "_Register");
                    additionalVals.Add("1");
                }
                // Put the other values into additionals for the sake of simplicity this time around.
                if (parentOrgIdChanged)
                {
                    additionalCols.Add(Glo.Tab.PARENT_ID);
                    additionalCols.Add(Glo.Tab.PARENT_ID + Glo.Tab.CHANGE_REGISTER_SUFFIX);
                    additionalVals.Add(parentOrgID);
                    additionalVals.Add("1");
                }
                if (dialNoChanged)
                {
                    additionalCols.Add(Glo.Tab.DIAL_NO);
                    additionalCols.Add(Glo.Tab.DIAL_NO + Glo.Tab.CHANGE_REGISTER_SUFFIX);
                    additionalVals.Add(dialNo);
                    additionalVals.Add("1");
                }
                if (notesChanged)
                {
                    additionalCols.Add(Glo.Tab.NOTES);
                    additionalCols.Add(Glo.Tab.NOTES + Glo.Tab.CHANGE_REGISTER_SUFFIX);
                    additionalVals.Add(notes);
                    additionalVals.Add("1");
                }

                command += SqlAssist.InsertInto("OrganisationChange",
                                           SqlAssist.ColConcat(additionalCols,
                                                               Glo.Tab.ORGANISATION_ID,
                                                               Glo.Tab.CHANGE_TIME,
                                                               Glo.Tab.LOGIN_ID,
                                                               Glo.Tab.CHANGE_REASON),
                                           SqlAssist.ValConcat(additionalVals,
                                                               organisationID,
                                                               '\'' + SqlAssist.DateTimeToSQLType(DateTime.Now) + '\'',
                                                               loginID.ToString(),
                                                               changeReason == null ? "''" : changeReason));
            }

            return SqlAssist.Transaction(command);
        }
    }

    struct Asset
    {
        public string sessionID;
        public string assetID;
        public string? organisationID;
        public string? notes;
        public bool organisationIdChanged = false;
        public bool notesChanged = false;
        public List<string> additionalCols;
        public List<string?> additionalVals;
        public List<bool> additionalNeedsQuotes;
        public bool changeTracked = false;
        public string changeReason = "";

        public Asset(string sessionID, string assetID, string? organisationID, string? notes,
                     List<string> additionalCols,
                     List<string?> additionalVals,
                     List<bool> additionalNeedsQuotes)
        {
            this.sessionID = sessionID;
            this.assetID = assetID;
            this.organisationID = organisationID;
            this.notes = notes;
            this.additionalCols = additionalCols;
            this.additionalVals = additionalVals;
            this.additionalNeedsQuotes = additionalNeedsQuotes;
        }

        private void Prepare()
        {
            // Make sure the columns and values are safe, then add quotes where needed.
            assetID = SqlAssist.AddQuotes(SqlAssist.SecureValue(assetID));
            if (organisationID != null)
                organisationID = SqlAssist.AddQuotes(SqlAssist.SecureValue(organisationID));
            if (notes != null)
                notes = SqlAssist.AddQuotes(SqlAssist.SecureValue(notes));
            SqlAssist.SecureColumn(additionalCols);
            SqlAssist.SecureValue(additionalVals);
            SqlAssist.AddQuotes(additionalVals, additionalNeedsQuotes);
            if (changeTracked && changeReason != null)
                changeReason = SqlAssist.AddQuotes(SqlAssist.SecureValue(changeReason));
        }

        public string SqlInsert(int loginID)
        {
            Prepare();

            string com = SqlAssist.InsertInto("Asset",
                                              SqlAssist.ColConcat(additionalCols, Glo.Tab.ASSET_ID,
                                                                                  Glo.Tab.ORGANISATION_ID,
                                                                                  Glo.Tab.NOTES),
                                              SqlAssist.ValConcat(additionalVals, assetID,
                                                                                  organisationID,
                                                                                  notes));
            // Create a first change instance.
            additionalCols.RemoveRange(additionalCols.Count - 3, 3); // ColConcat and ValConcat added the main fields
            additionalVals.RemoveRange(additionalVals.Count - 3, 3); // to the Lists, so walk that back here.
            int initialCount = additionalCols.Count;
            for (int i = 0; i < initialCount; ++i)
            {
                additionalCols.Add(additionalCols[i] + Glo.Tab.CHANGE_REGISTER_SUFFIX);
                additionalVals.Add("1");
            }
            com += SqlAssist.InsertInto("AssetChange",
                                        SqlAssist.ColConcat(additionalCols,
                                                            Glo.Tab.ASSET_ID,
                                                            Glo.Tab.CHANGE_TIME,
                                                            Glo.Tab.LOGIN_ID,
                                                            Glo.Tab.CHANGE_REASON,
                                                            Glo.Tab.ORGANISATION_ID,
                                                            Glo.Tab.ORGANISATION_ID + Glo.Tab.CHANGE_REGISTER_SUFFIX,
                                                            Glo.Tab.NOTES,
                                                            Glo.Tab.NOTES + Glo.Tab.CHANGE_REGISTER_SUFFIX),
                                        SqlAssist.ValConcat(additionalVals,
                                                            assetID,
                                                            '\'' + SqlAssist.DateTimeToSQLType(DateTime.Now) + '\'',
                                                            loginID.ToString(),
                                                            "'Created new asset.'",
                                                            organisationID, "1",
                                                            notes, "1"));
            return SqlAssist.Transaction(com);
        }

        public string SqlUpdate(int loginID)
        {
            Prepare();

            List<string> setters = new();
            if (organisationIdChanged)
                setters.Add(SqlAssist.Setter(Glo.Tab.ORGANISATION_ID, organisationID));
            if (notesChanged)
                setters.Add(SqlAssist.Setter(Glo.Tab.NOTES, notes));
            for (int i = 0; i < additionalCols.Count; ++i)
                setters.Add(SqlAssist.Setter(additionalCols[i], additionalVals[i]));
            string com = SqlAssist.Update("Asset", string.Join(", ", setters),
                                          Glo.Tab.ASSET_ID, assetID);

            if (changeTracked)
            {
                // Add _Register bools for each column affected.
                int initialCount = additionalCols.Count;
                for (int i = 0; i < initialCount; ++i)
                {
                    additionalCols.Add(additionalCols[i] + "_Register");
                    additionalVals.Add("1");
                }
                // Put the other values into additionals for the sake of simplicity this time around.
                if (organisationIdChanged)
                {
                    additionalCols.Add(Glo.Tab.ORGANISATION_ID);
                    additionalCols.Add(Glo.Tab.ORGANISATION_ID + Glo.Tab.CHANGE_REGISTER_SUFFIX);
                    additionalVals.Add(organisationID);
                    additionalVals.Add("1");
                }
                if (notesChanged)
                {
                    additionalCols.Add(Glo.Tab.NOTES);
                    additionalCols.Add(Glo.Tab.NOTES + Glo.Tab.CHANGE_REGISTER_SUFFIX);
                    additionalVals.Add(notes);
                    additionalVals.Add("1");
                }

                com += SqlAssist.InsertInto("AssetChange",
                                            SqlAssist.ColConcat(additionalCols,
                                                                Glo.Tab.ASSET_ID,
                                                                Glo.Tab.CHANGE_TIME,
                                                                Glo.Tab.LOGIN_ID,
                                                                Glo.Tab.CHANGE_REASON),
                                            SqlAssist.ValConcat(additionalVals,
                                                                assetID,
                                                                '\'' + SqlAssist.DateTimeToSQLType(DateTime.Now) + '\'',
                                                                loginID.ToString(),
                                                                changeReason == null ? "" : changeReason));
            }

            return SqlAssist.Transaction(com);
        }
    }

    struct Contact
    {
        public string sessionID;
        public int contactID = -1;
        public string? notes;
        public bool notesChanged;
        public List<string> additionalCols;
        public List<string?> additionalVals;
        public List<bool> additionalNeedsQuotes;
        public bool requireIdBack;

        public Contact(string sessionID, int contactID, string? notes, List<string> additionalCols,
                                                                       List<string?> additionalVals,
                                                                       List<bool> additionalNeedsQuotes)
        {
            this.sessionID = sessionID;
            this.contactID = contactID;
            this.notes = notes;
            this.additionalCols = additionalCols;
            this.additionalVals = additionalVals;
            this.additionalNeedsQuotes = additionalNeedsQuotes;
            notesChanged = false;
            requireIdBack = false;
        }

        private void Prepare()
        {
            // Make sure the columns and values are safe, then add quotes where needed.
            if (notes != null)
                notes = SqlAssist.AddQuotes(SqlAssist.SecureValue(notes));
            SqlAssist.SecureColumn(additionalCols);
            SqlAssist.SecureValue(additionalVals);
            SqlAssist.AddQuotes(additionalVals, additionalNeedsQuotes);
        }

        public string SqlInsert()
        {
            Prepare();

            string ret = SqlAssist.InsertInto("Contact",
                                              SqlAssist.ColConcat(additionalCols,
                                                                  Glo.Tab.NOTES),
                                              SqlAssist.ValConcat(additionalVals, notes));
            if (requireIdBack)
                ret += " SET @ID = SCOPE_IDENTITY();"; // Picked up by ExecuteNonQuery() in agent.
            return ret;
        }

        public string SqlUpdate()
        {
            Prepare();

            List<string> setters = new();
            if (notesChanged)
                setters.Add(SqlAssist.Setter(Glo.Tab.NOTES, notes));
            for (int i = 0; i < additionalCols.Count; ++i)
                setters.Add(SqlAssist.Setter(additionalCols[i], additionalVals[i]));
            return SqlAssist.Update("Contact", string.Join(", ", setters),
                                    Glo.Tab.CONTACT_ID, contactID);
        }
    }

    struct ConferenceType
    {
        public string sessionID;
        public int typeID;
        public string? name;
        public bool nameChanged;

        public ConferenceType(string sessionID, int typeID, string? name)
        {
            this.sessionID = sessionID;
            this.typeID = typeID;
            this.name = name;
            nameChanged = false;
        }

        private void Prepare()
        {
            if (name != null)
                name = SqlAssist.AddQuotes(SqlAssist.SecureValue(name));
        }

        public string SqlInsert()
        {
            Prepare();
            return "INSERT INTO ConferenceType (" + Glo.Tab.CONFERENCE_TYPE_NAME + ") VALUES (" + name + ");";
        }

        public string SqlUpdate()
        {
            Prepare();
            List<string> setters = new();
            if (nameChanged && name != null)
            {
                return SqlAssist.Update("ConferenceType",
                                        Glo.Tab.CONFERENCE_TYPE_NAME + " = " + name,
                                        Glo.Tab.CONFERENCE_TYPE_ID, typeID);
            }
            else
                return "";
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
        public string? name;
        public DateTime availableFrom;
        public DateTime availableTo;
        public int capacity;

        public Resource(string sessionID, int resourceID, string? name,
                        DateTime availableFrom, DateTime availableTo, int capacity)
        {
            this.sessionID = sessionID;
            this.resourceID = resourceID;
            this.name = name;
            this.availableFrom = availableFrom;
            this.availableTo = availableTo;
            this.capacity = capacity;
        }

        private void Prepare()
        {
            // Make sure the columns and values are safe, then add quotes where needed.
            if (name != null)
                name = SqlAssist.AddQuotes(SqlAssist.SecureValue(name));
        }

        public string SqlInsert()
        {
            Prepare();
            return SqlAssist.InsertInto("Resource",
                                        SqlAssist.ColConcat(Glo.Tab.RESOURCE_NAME,
                                                            Glo.Tab.RESOURCE_FROM,
                                                            Glo.Tab.RESOURCE_TO,
                                                            Glo.Tab.RESOURCE_CAPACITY),
                                        SqlAssist.ValConcat(name,
                                                            SqlAssist.AddQuotes(SqlAssist.DateTimeToSQLType(availableFrom)),
                                                            SqlAssist.AddQuotes(SqlAssist.DateTimeToSQLType(availableTo)),
                                                            capacity.ToString()));
        }
    }

    struct Login
    {
        public string sessionID;
        public int loginID;
        public string username;
        public string password;
        public bool admin;
        public int createPermissions;
        public int editPermissions;
        public int deletePermissions;
        public bool enabled;

        public Login(string sessionID, int loginID, string username, string password, bool admin,
                     int createPermissions, int editPermissions, int deletePermissions, bool enabled)
        {
            this.sessionID = sessionID;
            this.loginID = loginID;
            this.username = username;
            this.password = password;
            this.admin = admin;
            this.createPermissions = createPermissions;
            this.editPermissions = editPermissions;
            this.deletePermissions = deletePermissions;
            this.enabled = enabled;
        }

        private void Prepare()
        {
            // Make sure the columns and values are safe, then add quotes where needed.
            username = SqlAssist.AddQuotes(SqlAssist.SecureValue(username));
            password = SqlAssist.HashBytes(password);
        }

        public string SqlInsert()
        {
            Prepare();


            return SqlAssist.InsertInto("Login",
                                        SqlAssist.ColConcat(Glo.Tab.LOGIN_USERNAME,
                                                            Glo.Tab.LOGIN_PASSWORD,
                                                            Glo.Tab.LOGIN_ADMIN,
                                                            Glo.Tab.LOGIN_CREATE_PERMISSIONS,
                                                            Glo.Tab.LOGIN_EDIT_PERMISSIONS,
                                                            Glo.Tab.LOGIN_DELETE_PERMISSIONS,
                                                            Glo.Tab.LOGIN_ENABLED),
                                        SqlAssist.ValConcat(username,
                                                            password,
                                                            admin ? "1" : "0",
                                                            createPermissions.ToString(),
                                                            editPermissions.ToString(),
                                                            deletePermissions.ToString(),
                                                            enabled ? "1" : "0"));
        }

        public string SqlUpdate()
        {
            Prepare();

            List<string> setters = new() { SqlAssist.Setter(Glo.Tab.LOGIN_USERNAME, username),
                                           SqlAssist.Setter(Glo.Tab.LOGIN_ADMIN, admin ? "1" : "0"),
                                           SqlAssist.Setter(Glo.Tab.LOGIN_CREATE_PERMISSIONS,
                                                            createPermissions.ToString()),
                                           SqlAssist.Setter(Glo.Tab.LOGIN_EDIT_PERMISSIONS,
                                                            editPermissions.ToString()),
                                           SqlAssist.Setter(Glo.Tab.LOGIN_DELETE_PERMISSIONS,
                                                            deletePermissions.ToString()),
                                           SqlAssist.Setter(Glo.Tab.LOGIN_ENABLED, enabled ? "1" : "0") };

            return SqlAssist.Update("Login", string.Join(", ", setters), Glo.Tab.LOGIN_ID, loginID.ToString());
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

    struct SelectRequest
    {
        public string sessionID;
        public string table;
        public List<string> select;
        public List<string> likeColumns;
        public List<string> likeValues;

        public SelectRequest(string sessionID, string table,
                              List<string> select, List<string> likeColumns, List<string> likeValues)
        {
            /* There is no check here to make sure that columns and values are the equal lengths. Be careful
               to respect this restriction. Agent will throw an exception if they are unequal. */
            this.sessionID = sessionID;
            this.table = table;
            this.select = select;
            this.likeColumns = likeColumns;
            this.likeValues = likeValues;
        }
    }

    struct SelectWideRequest
    {
        public string sessionID;
        public List<string> select;
        public string table;
        public string value;

        public SelectWideRequest(string sessionID, List<string> select, string table, string value)
        {
            this.sessionID = sessionID;
            this.table = table;
            this.select = select;
            this.value = value;
        }
    }

    struct SelectResult
    {
        /* The reason we return the type as well as the column name because it's so cumbersome to get some of this
         * information across the JSON serialiser. */
        public List<string?> columnNames;
        public List<string?> columnTypes;
        public List<List<object?>> rows;

        public SelectResult(List<string?> columnNames, List<string?> columnTypes, List<List<object?>> rows)
        {
            this.columnNames = columnNames;
            this.columnTypes = columnTypes;
            this.rows = rows;
        }

        // This constructor will automatically get the required information from the SqlDataReader.
        public SelectResult(SqlDataReader reader)
        {
            columnNames = new();
            columnTypes = new();
            DataTable schema = reader.GetSchemaTable();
            foreach (DataRow row in schema.Rows)
            {
                columnNames.Add(row.Field<string>("ColumnName"));
                Type? t = row.Field<Type>("DataType");
                columnTypes.Add(t == null ? null : t.Name);
            }

            rows = new();
            while (reader.Read())
            {
                List<object?> row = new List<object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                    if (reader.IsDBNull(i))
                        row.Add(null);
                    else
                        row.Add(reader[i]);
                rows.Add(row);
            }
        }
    }

    struct DeleteRequest
    {
        public string sessionID;
        public string table;
        public string column;
        public string id;
        public bool needsQuotes;

        public DeleteRequest(string sessionID, string table, string column, string id, bool isString)
        {
            this.sessionID = sessionID;
            this.table = table;
            this.column = column;
            this.id = id;
            this.needsQuotes = isString;
        }

        public string SqlDelete()
        {
            return "DELETE FROM " + table +
                   " WHERE " + SqlAssist.SecureColumn(column) + " = " +
                               SqlAssist.AddQuotes(SqlAssist.SecureValue(id), needsQuotes) + ';';
        }
    }

    struct LinkContactRequest
    {
        public string sessionID;
        public string organisationID;
        public int contactID;
        public bool unlink;

        public LinkContactRequest(string sessionID, string organisationID, int contactID, bool unlink)
        {
            this.sessionID = sessionID;
            this.organisationID = organisationID;
            this.contactID = contactID;
            this.unlink = unlink;
        }

        private void Prepare()
        {
            organisationID = SqlAssist.AddQuotes(SqlAssist.SecureValue(organisationID));
        }

        public string SqlInsert()
        {
            Prepare();
            return SqlAssist.InsertInto("OrganisationContacts",
                                        Glo.Tab.ORGANISATION_ID + ", " + Glo.Tab.CONTACT_ID,
                                        organisationID + ", " + contactID.ToString());
        }

        public string SqlDelete()
        {
            Prepare();
            return "DELETE FROM OrganisationContacts " +
                   "WHERE " + Glo.Tab.ORGANISATION_ID + " = " + organisationID +
                  " AND " + Glo.Tab.CONTACT_ID + " = " + contactID.ToString() + ";";
        }
    }

    struct LinkedContactSelectRequest
    {
        public string sessionID;
        public string organisationID;

        public LinkedContactSelectRequest(string sessionID, string organisationID)
        {
            this.sessionID = sessionID;
            this.organisationID = organisationID;
        }

        private void Prepare()
        {
            organisationID = SqlAssist.AddQuotes(SqlAssist.SecureValue(organisationID));
        }

        public string SqlSelect()
        {
            Prepare();

            return "SELECT Contact.* FROM Contact" +
                  " JOIN OrganisationContacts ON OrganisationContacts." + Glo.Tab.CONTACT_ID +
                        " = Contact." + Glo.Tab.CONTACT_ID +
                  " JOIN Organisation ON Organisation." + Glo.Tab.ORGANISATION_ID +
                        " = OrganisationContacts." + Glo.Tab.ORGANISATION_ID +
                  " WHERE Organisation." + Glo.Tab.ORGANISATION_ID + " = " + organisationID + ";";
        }
    }

    struct SelectHistoryRequest
    {
        public string sessionID;
        public string tableName;
        public string recordID;

        public SelectHistoryRequest(string sessionID, string tableName, string recordID)
        {
            this.sessionID = sessionID;
            this.tableName = tableName;
            this.recordID = recordID;
        }

        private void Prepare()
        {
            tableName = SqlAssist.SecureColumn(tableName);
            recordID = SqlAssist.AddQuotes(SqlAssist.SecureValue(recordID));
        }

        public string SqlSelect()
        {
            Prepare();

            // This really needs putting into string.Format() at some point...
            return "SELECT " + tableName + "." + Glo.Tab.CHANGE_ID + ", "
                             + tableName + "." + Glo.Tab.CHANGE_TIME + ", "
                             + "Login." + Glo.Tab.LOGIN_USERNAME + ", "
                             + tableName + "." + Glo.Tab.CHANGE_REASON +
                  " FROM " + tableName + // LEFT JOIN here because we want to include records with NULL login IDs.
                  " LEFT JOIN " + "Login ON " + tableName + "." + Glo.Tab.LOGIN_ID + " = Login." + Glo.Tab.LOGIN_ID +
                  " WHERE " + (tableName == "AssetChange" ? Glo.Tab.ASSET_ID : Glo.Tab.ORGANISATION_ID)
                            + " = " + recordID +
                  " ORDER BY " + Glo.Tab.CHANGE_TIME + " DESC;";
        }
    }

    struct SelectHistoricalRecordRequest
    {
        public string sessionID;
        public string tableName;
        public string changeID;
        public string recordID;

        public SelectHistoricalRecordRequest(string sessionID, string tableName, string changeID, string recordID)
        {
            this.sessionID = sessionID;
            this.tableName = tableName;
            this.changeID = changeID;
            this.recordID = recordID;
        }

        private void Prepare()
        {
            tableName = SqlAssist.SecureColumn(tableName);
            changeID = SqlAssist.SecureValue(changeID);
            recordID = SqlAssist.AddQuotes(SqlAssist.SecureValue(recordID));
        }

        public string SqlSelect()
        {
            Prepare();

            string recordColumnName;
            if (tableName == "Organisation")
                recordColumnName = Glo.Tab.ORGANISATION_ID;
            else // if == Asset
                recordColumnName = Glo.Tab.ASSET_ID;

            return string.Format("SELECT * FROM {0}Change " +
                                 "WHERE {2} = {3} " +
                                 "AND {1} <= (SELECT {1} FROM {0}Change " +
                                               "WHERE {2} = {3} AND {4} = {5}) " +
                                 "ORDER BY {1} DESC;",
                                 tableName, Glo.Tab.CHANGE_TIME, recordColumnName, recordID,
                                                                 Glo.Tab.CHANGE_ID, changeID);
        }
    }


    //   H E L P E R   F U N C T I O N S

    public class SqlAssist
    {
        public static string SecureColumn(string val)
        {
            if (val.Contains(';') || val.Contains('\''))
                return "";
            else
                return val;
        }
        public static void SecureColumn(List<string> vals)
        {
            // Blank columns will cause an exception when the command is run, causing the SQL query/non-query to abort.
            for (int i = 0; i < vals.Count; ++i)
                if (vals[i].Contains(';') || vals[i].Contains('\''))
                    vals[i] = "";
        }
        public static string SecureValue(string val)
        {
            return val.Replace("\'", "''");
        }
        public static void SecureValue(List<string?> val)
        {
            for (int i = 0; i < val.Count; ++i)
            {
                string? s = val[i];
                if (s != null)
                    val[i] = SecureValue(s);
            }
        }

        public static void LevelAdditionals(ref List<string> columns, ref List<string?> values)
        {
            if (columns.Count > values.Count)
                columns.RemoveRange(values.Count, columns.Count - values.Count);
            else if (values.Count > columns.Count)
                values.RemoveRange(columns.Count, values.Count - columns.Count);
        }

        public static List<string?> ApplySingleQuotes(List<string?> values, List<bool> isString)
        {
            // This method will replace any null entries with NULL, rather than wrapping in single quotes.
            List<string?> ret = new();
            for (int i = 0; i < values.Count && i < isString.Count; ++i)
                if (isString[i])
                    ret.Add('\'' + values[i] + '\'');
            return ret;
        }

        public static string InsertInto(string tableName, string columns, string values)
        {
            return "INSERT INTO " + tableName + " (" + columns + ") VALUES (" + values + ");";
        }

        public static bool NeedsQuotes(string type)
        {
            if (type.Contains("INT"))
                return false;
            return true;
        }
        public static string Update(string tableName, string columnsAndValues, string whereCol, string whereID)
        {
            return "UPDATE " + tableName + " SET " + columnsAndValues +
                   " WHERE " + whereCol + " = " + whereID + ";";
        }
        public static string Update(string tableName, string columnsAndValues, string whereCol, int whereID)
        {
            return "UPDATE " + tableName + " SET " + columnsAndValues +
                   " WHERE " + SecureColumn(whereCol) + " = " + whereID.ToString() + ";";
        }

        public static string Transaction(params string[] statements)
        {
            for (int i = 0; i < statements.Length; ++i)
                if (!statements[i].EndsWith(';'))
                    statements[i] += ";";

            StringBuilder bldr = new("BEGIN TRANSACTION; BEGIN TRY");
            foreach (string s in statements)
                bldr.Append(" " + s);
            bldr.Append(" COMMIT TRANSACTION; END TRY BEGIN CATCH ROLLBACK TRANSACTION; THROW; END CATCH;");

            return bldr.ToString();
        }

        public static string AddQuotes(string val)
        {
            return '\'' + val + '\'';
        }
        public static string AddQuotes(string val, bool needsQuotes)
        {
            return needsQuotes ? '\'' + val + '\'' : val;
        }
        public static void AddQuotes(List<string?> vals)
        {
            for (int i = 0; i < vals.Count; ++i)
                if (vals[i] != null)
                    vals[i] = '\'' + vals[i] + '\'';
        }
        public static void AddQuotes(List<string?> vals, List<bool> needsQuotes)
        {
            for (int i = 0; i < vals.Count; ++i)
                if (vals[i] != null && needsQuotes[i])
                    vals[i] = '\'' + vals[i] + '\'';
        }

        public static string ValConcat(params string?[] values)
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
                        additionalVals[i] = additionalVals[i];
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

        public static string HashBytes(string str)
        {
            return "HASHBYTES('SHA2_512', '" + str + "')";
        }

        public static string Setter(string column, string? value)
        {
            if (value == null || value == "" || value == "''")
                return SecureColumn(column) + " = NULL";
            else
                return SecureColumn(column) + " = " + value;
        }

        public static string UnknownObjectToString(object val)
        {
            if (val.GetType() == typeof(string))
                return ((string)val).Replace("'", "''");
            else if (val.GetType() == typeof(int))
                return ((int)val).ToString();
            else if (val.GetType() == typeof(TimeSpan))
                return ((TimeSpan)val).ToString("hh\\:mm");
            else if (val.GetType() == typeof(DateTime))
            {
                DateTime valCast = (DateTime)val;
                if (valCast.Ticks % 864_000_000_000 == 0) // On the day
                    return valCast.ToString("yyyy-MM-dd");
                else
                    return valCast.ToString("yyyy-MM-dd HH:mm");
            }
            else
                return "";
        }
        public static string UnknownObjectToString(object val, string type)
        {
            type = type.ToLower();
            if (type == "string")
                return "'" + ((string)val).Replace("'", "''") + "'";
            else if (type == "int")
                return ((int)val).ToString().Replace("'", "''");
            else if (type == "timespan")
                return ((TimeSpan)val).ToString("hh\\:mm");
            else if (type == "datetime")
            {
                DateTime valCast = (DateTime)val;
                if (valCast.Ticks % 864_000_000_000 == 0) // On the day
                    return valCast.ToString("yyyy-MM-dd");
                else
                    return valCast.ToString("yyyy-MM-dd HH:mm");
            }
            else
                return "";
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