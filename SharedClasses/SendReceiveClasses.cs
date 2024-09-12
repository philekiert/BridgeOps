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

        public NetworkStream? NewClientNetworkStream(IPEndPoint ep)
        {
            try
            {
                TcpClient client = new TcpClient();
                client.Connect(ep);
                NetworkStream stream = client.GetStream();
                stream.ReadTimeout = 30000;
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
        public string? settings;

        public LogoutRequest(string sessionID, int loginID, string? settings)
        {
            this.sessionID = sessionID;
            this.loginID = loginID;
            this.settings = settings;
        }
    }

    struct UserSettings
    {
        public string sessionID;
        public List<string> organisationDataOrder;
        public List<string> assetDataOrder;
        public List<string> contactDataOrder;
        public List<bool> organisationDataHidden;
        public List<bool> assetDataHidden;
        public List<bool> contactDataHidden;
        public List<int> organisationDataWidths;
        public List<int> assetDataWidths;
        public List<int> contactDataWidths;
        public int conferenceViewWidth;
        public int databaseViewWidth;
        public int viewState;

        public UserSettings(string sessionID,
                            List<string> organisationDataOrder,
                            List<string> assetDataOrder,
                            List<string> contactDataOrder,
                            List<bool> organisationDataHidden,
                            List<bool> assetDataHidden,
                            List<bool> contactDataHidden,
                            List<int> organisationDataWidths,
                            List<int> assetDataWidths,
                            List<int> contactDataWidths,
                            int conferenceViewWidth, int databaseViewWidth, int viewState)
        {
            this.sessionID = sessionID;
            this.organisationDataOrder = organisationDataOrder;
            this.assetDataOrder = assetDataOrder;
            this.contactDataOrder = contactDataOrder;
            this.organisationDataHidden = organisationDataHidden;
            this.assetDataHidden = assetDataHidden;
            this.contactDataHidden = contactDataHidden;
            this.organisationDataWidths = organisationDataWidths;
            this.assetDataWidths = assetDataWidths;
            this.contactDataWidths = contactDataWidths;
            this.conferenceViewWidth = conferenceViewWidth;
            this.databaseViewWidth = databaseViewWidth;
            this.viewState = viewState;
        }
    }

    struct PasswordResetRequest
    {
        public string sessionID;
        public int loginID;
        public string password;
        public string newPassword;
        public bool userManagementMenu;

        public PasswordResetRequest(string sessionID, int loginID,
                                    string password, string newPassword,
                                    bool userManagementMenu)
        {
            this.sessionID = sessionID;
            this.loginID = loginID;
            this.password = password;
            this.newPassword = newPassword;
            this.userManagementMenu = userManagementMenu;
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
        public int columnRecordID;
        public Intent intent;
        public string table;
        public string column;
        public string? columnRename;
        public string? friendly;
        public string? columnType;
        public List<string> allowed;

        // Removal
        public TableModification(string sessionID, int columnRecordID, string table, string column)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            intent = Intent.Removal;
            this.table = table;
            this.column = column;
            columnRename = null;
            friendly = null;
            columnType = null;
            allowed = new();
        }

        // Addition
        public TableModification(string sessionID, int columnRecordID, string table, string column,
                                 string columnType, List<string> allowed)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            intent = Intent.Addition;
            this.table = table;
            this.column = column;
            columnRename = null;
            friendly = null;
            this.columnType = columnType;
            this.allowed = allowed;
        }

        // Modification
        public TableModification(string sessionID, int columnRecordID, string table, string column,
                                 string? columnRename, string? friendly,
                                 string? columnType, List<string> allowed)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            intent = Intent.Modification;
            this.table = table;
            this.column = column;
            this.columnRename = columnRename;
            this.friendly = friendly;
            this.columnType = columnType;
            this.allowed = allowed;
        }

        private void Prepare()
        {
            // SecureColumn() should suffice here for table names and types.
            table = SqlAssist.SecureColumn(table);
            column = SqlAssist.SecureColumn(column).Replace(' ', '_');

            if (columnRename != null)
                columnRename = SqlAssist.SecureColumn(columnRename).Replace(' ', '_');

            if (columnType != null)
            {
                columnType = SqlAssist.SecureColumn(columnType);
                if (columnType == "BOOLEAN")
                    columnType = "BIT";
            }

            for (int n = 0; n < allowed.Count; ++n)
                allowed[n] = SqlAssist.AddQuotes(SqlAssist.SecureColumn(allowed[n]))
                             .Replace("\r\n", "")
                             .Replace("\n", ""); // If any new lines sneak in, they could break the column record.
        }

        private string DropConstraint(string table, string column)
        {
            return $"IF EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS " +
                   $"WHERE CONSTRAINT_TYPE = 'CHECK' AND CONSTRAINT_NAME = 'chk_{table}{column}') " +
                    "BEGIN " +
                   $"ALTER TABLE {table} DROP CONSTRAINT chk_{table}{column}; " +
                    "END ";
        }

        public string SqlCommand()
        {
            Prepare();

            string command;

            // Addition
            if (intent == Intent.Addition)
            {
                command = $"ALTER TABLE {table} ";
                command += $"ADD {column} {columnType}";
                if (allowed.Count > 0)
                {
                    command += $" CONSTRAINT chk_{table}{column}" +
                               $" CHECK ({column} IN ({string.Join(", ", allowed)}))";
                }
                command += ";";

                // Create register columns if needed.
                if (table == "Organisation" || table == "Asset")
                {
                    command += $"ALTER TABLE {table}Change ";
                    command += $"ADD {column}{Glo.Tab.CHANGE_SUFFIX} BIT;";
                    command += $"ALTER TABLE {table}Change ";
                    command += $"ADD {column} {columnType}; ";
                }

                // Column orders are handled in the agent code, and the SQL command is added in a nested transaction.

                command = SqlAssist.Transaction(command);
            }

            // Removal
            else if (intent == Intent.Removal)
            {
                command = DropConstraint(table, column);
                command += $"ALTER TABLE {table} DROP COLUMN {column};";

                // Remove register columns if needed.
                if (table == "Organisation" || table == "Asset")
                {
                    command += $"ALTER TABLE {table}Change DROP COLUMN {column};";
                    command += $"ALTER TABLE {table}Change DROP COLUMN {column}{Glo.Tab.CHANGE_SUFFIX};";
                }

                // Column orders are handled in the agent code, and the SQL command is added in a nested transaction.

                command = SqlAssist.Transaction(command);
            }

            // Modification
            else // if (intent == Intent.Modification)
            {
                // Any constraints must be dropped and remade, but we obviously only want to do this once.
                bool droppedConstraint = false;
                List<string> commands = new();
                bool renamingColumn = columnRename != null;
                if (renamingColumn)
                {
                    commands.Add(DropConstraint(table, column));
                    droppedConstraint = true;

                    commands.Add($"EXEC sp_rename '{table}.{column}', '{columnRename}', 'COLUMN';");

                    // Rename register columns if needed.
                    if (table == "Organisation" || table == "Asset")
                    {
                        commands.Add($"EXEC sp_rename '{table}Change." +
                                     $"{column}{Glo.Tab.CHANGE_SUFFIX}', " +
                                     $"'{columnRename}{Glo.Tab.CHANGE_SUFFIX}', 'COLUMN';");
                        commands.Add($"EXEC sp_rename '{table}Change." +
                                     $"{column}', '{columnRename}', 'COLUMN';");
                    }

                    // SQL Server needs to recognise this rename immediately for some further operations. The type
                    // change seems to work, but the constraint creation fails using the new column name. Best to
                    // play it safe by updating the schema consistently regardless of further operations.

                    commands.Add($"ALTER TABLE {table} WITH CHECK CHECK CONSTRAINT ALL;");
                    if (table == "Organisation" || table == "Asset")
                        commands.Add($"ALTER TABLE {table}Change WITH CHECK CHECK CONSTRAINT ALL;");
                }
                if (allowed.Count == 0 && !droppedConstraint)
                {
                    commands.Add(DropConstraint(table, column));
                    droppedConstraint = true;
                }

                // From here onwards, we only want to use the new column name if there is one.
                if (columnRename != null)
                    column = columnRename;

                if (columnType != null)
                {
                    if (!droppedConstraint)
                    {
                        // The constraint needs to be dropped and remade due to the column being renamed.
                        commands.Add(DropConstraint(table, column));
                        droppedConstraint = true;
                    }

                    // For OrganisationReference, we have to remove all the foreign keys, update those types as well,
                    // then re-add them. Some primary keys too.
                    bool reAddKeys = false;
                    if (table == "Organisation")
                    {
                        if (column == Glo.Tab.ORGANISATION_ID)
                        {
                            reAddKeys = true;
                            commands.Add("ALTER TABLE OrganisationChange DROP CONSTRAINT fk_OrgChange_OrgID;");
                            commands.Add("ALTER TABLE Organisation DROP CONSTRAINT pk_OrgID;");
                            commands.Add("ALTER TABLE OrganisationChange DROP CONSTRAINT pk_OrgID_ChangeID");
                        }
                        if (column == Glo.Tab.ORGANISATION_REF)
                        {
                            reAddKeys = true;
                            commands.Add("ALTER TABLE Organisation DROP CONSTRAINT fk_ParentOrgRef;");
                            commands.Add("ALTER TABLE Asset DROP CONSTRAINT fk_AssetOrganisation;");
                            commands.Add("ALTER TABLE OrganisationContacts DROP CONSTRAINT fk_jncContacts_OrgRef;");
                            commands.Add("ALTER TABLE OrganisationContacts DROP CONSTRAINT pk_jncContacts_OrgRef_ContactID;");
                            commands.Add("ALTER TABLE Organisation DROP CONSTRAINT u_OrgRef;");

                            commands.Add($"ALTER TABLE Organisation ALTER COLUMN {column} {columnType};");
                            commands.Add($"ALTER TABLE Organisation ALTER COLUMN {Glo.Tab.PARENT_REF} {columnType};");
                            commands.Add($"ALTER TABLE Asset ALTER COLUMN {column} {columnType};");
                            commands.Add($"ALTER TABLE Connection ALTER COLUMN {column} {columnType} NOT NULL;");
                            commands.Add($"ALTER TABLE OrganisationContacts ALTER COLUMN {column} {columnType} NOT NULL;");
                        }
                        if (column == Glo.Tab.DIAL_NO)
                        {
                            reAddKeys = true;
                            commands.Add("DROP INDEX u_OrgDialNo ON Organisation;");
                            commands.Add($"ALTER TABLE Connection ALTER COLUMN {column} {columnType};");
                        }
                    }
                    else if (table == "Asset")
                    {
                        if (column == Glo.Tab.ASSET_ID)
                        {
                            reAddKeys = true;
                            commands.Add("ALTER TABLE AssetChange DROP CONSTRAINT fk_AssetChange_AssetID;");
                            commands.Add("ALTER TABLE Asset DROP CONSTRAINT pk_AssetID;");
                            commands.Add("ALTER TABLE AssetChange DROP CONSTRAINT pk_AssetID_ChangeID");
                        }
                        if (column == Glo.Tab.ASSET_REF)
                        {
                            reAddKeys = true;
                            commands.Add("ALTER TABLE Asset DROP CONSTRAINT u_AssetRef;");
                        }
                    }
                    else if (table == "Contact" && column == Glo.Tab.CONTACT_ID)
                    {
                        reAddKeys = true;
                        commands.Add("ALTER TABLE OrganisationContacts DROP CONSTRAINT fk_jncContacts_ContactID;");
                        commands.Add("ALTER TABLE OrganisationContacts DROP CONSTRAINT pk_jncContacts_OrgRef_ContactID;");
                        commands.Add("ALTER TABLE Contact DROP CONSTRAINT pk_ContactID;");
                    }
                    else if (table == "Login")
                    {
                        if (column == Glo.Tab.LOGIN_ID)
                        {
                            reAddKeys = true;
                            commands.Add("ALTER TABLE Conference DROP CONSTRAINT fk_ConfCreationLogin;");
                            commands.Add("ALTER TABLE Conference DROP CONSTRAINT fk_ConfEditLogin;");
                            commands.Add("ALTER TABLE OrganisationChange DROP CONSTRAINT fk_OrgChange_LoginID;");
                            commands.Add("ALTER TABLE AssetChange DROP CONSTRAINT fk_AssetChange_LoginID;");
                            commands.Add("ALTER TABLE Login DROP CONSTRAINT pk_LoginID;");

                            commands.Add($"ALTER TABLE Conference ALTER COLUMN {Glo.Tab.CONFERENCE_CREATION_LOGIN} {columnType};");
                            commands.Add($"ALTER TABLE Conference ALTER COLUMN {Glo.Tab.CONFERENCE_EDIT_LOGIN} {columnType};");
                            commands.Add($"ALTER TABLE OrganisationChange ALTER COLUMN {column} {columnType};");
                            commands.Add($"ALTER TABLE AssetChange ALTER COLUMN {column} {columnType};");
                        }
                        else if (column == Glo.Tab.LOGIN_USERNAME)
                        {
                            reAddKeys = true;
                            commands.Add("ALTER TABLE Login DROP CONSTRAINT u_Username;");
                        }
                    }

                    commands.Add($"ALTER TABLE {table} ALTER COLUMN {column} {columnType};");

                    // Change register column types if needed (overriden below if NOT NULL needs adding).
                    if (table == "Organisation" || table == "Asset")
                        commands.Add($"ALTER TABLE {table}Change ALTER COLUMN {column} {columnType};");

                    if (reAddKeys)
                    {
                        if (table == "Organisation")
                        {
                            if (column == Glo.Tab.ORGANISATION_ID)
                            {
                                // In this very rare case, for simplicity's sake, alter again to make NOT NULL so we can re-add the primary key constraints.
                                commands.Add($"ALTER TABLE Organisation ALTER COLUMN {column} {columnType} NOT NULL;");
                                commands.Add($"ALTER TABLE OrganisationChange ALTER COLUMN {column} {columnType} NOT NULL;");
                                commands.Add("ALTER TABLE Organisation ADD CONSTRAINT pk_OrgID PRIMARY KEY (Organisation_ID);");
                                commands.Add("ALTER TABLE OrganisationChange ADD CONSTRAINT pk_OrgID_ChangeID PRIMARY KEY (Organisation_ID, Change_ID)");
                                commands.Add("ALTER TABLE OrganisationChange ADD CONSTRAINT fk_OrgChange_OrgID FOREIGN KEY (Organisation_ID) REFERENCES Organisation (Organisation_ID) ON DELETE CASCADE;");
                            }
                            else if (column == Glo.Tab.ORGANISATION_REF)
                            {
                                commands.Add("ALTER TABLE Organisation ADD CONSTRAINT u_OrgRef UNIQUE (Organisation_Reference);");
                                commands.Add("ALTER TABLE Organisation ADD CONSTRAINT fk_ParentOrgRef FOREIGN KEY (Parent_Reference) REFERENCES Organisation (Organisation_Reference);");
                                commands.Add("ALTER TABLE Asset ADD CONSTRAINT fk_AssetOrganisation FOREIGN KEY (Organisation_Reference) REFERENCES Organisation (Organisation_Reference) ON DELETE SET NULL ON UPDATE CASCADE;");
                                commands.Add("ALTER TABLE OrganisationContacts ADD CONSTRAINT fk_jncContacts_OrgRef FOREIGN KEY (Organisation_Reference) REFERENCES Organisation (Organisation_Reference) ON DELETE CASCADE ON UPDATE CASCADE;");
                                commands.Add("ALTER TABLE OrganisationContacts ADD CONSTRAINT pk_jncContacts_OrgRef_ContactID PRIMARY KEY (Organisation_Reference, Contact_ID);");
                            }
                            else if (column == Glo.Tab.DIAL_NO)
                            {
                                commands.Add("CREATE UNIQUE INDEX u_OrgDialNo ON Organisation (Dial_No) WHERE Dial_No IS NOT NULL;");
                            }
                        }
                        else if (table == "Asset")
                        {
                            if (column == Glo.Tab.ASSET_ID)
                            {
                                // In this very rare case, for simplicity's sake, just alter again to make NOT NULL so we can re-add the primary key constraints.
                                commands.Add($"ALTER TABLE Asset ALTER COLUMN {column} {columnType} NOT NULL;");
                                commands.Add($"ALTER TABLE AssetChange ALTER COLUMN {column} {columnType} NOT NULL;");
                                commands.Add("ALTER TABLE Asset ADD CONSTRAINT pk_AssetID PRIMARY KEY (Asset_ID);");
                                commands.Add("ALTER TABLE AssetChange ADD CONSTRAINT pk_AssetID_ChangeID PRIMARY KEY (Asset_ID, Change_ID)");
                                commands.Add("ALTER TABLE AssetChange ADD CONSTRAINT fk_AssetChange_AssetID FOREIGN KEY (Asset_ID) REFERENCES Asset (Asset_ID) ON DELETE CASCADE;");
                            }
                            else if (column == Glo.Tab.ASSET_REF)
                            {
                                commands.Add("ALTER TABLE Asset ADD CONSTRAINT u_AssetRef UNIQUE (Asset_Reference);");
                            }
                        }
                        else if (table == "Contact" && column == Glo.Tab.CONTACT_ID)
                        {
                            commands.Add($"ALTER TABLE Contact ALTER COLUMN {column} {columnType} NOT NULL;");
                            commands.Add($"ALTER TABLE OrganisationContacts ALTER COLUMN {column} {columnType} NOT NULL;");
                            commands.Add("ALTER TABLE Contact ADD CONSTRAINT pk_ContactID PRIMARY KEY (Contact_ID);");
                            commands.Add("ALTER TABLE OrganisationContacts ADD CONSTRAINT fk_jncContacts_ContactID FOREIGN KEY (Contact_ID) REFERENCES Contact (Contact_ID) ON DELETE CASCADE;");
                            commands.Add("ALTER TABLE OrganisationContacts ADD CONSTRAINT pk_jncContacts_OrgRef_ContactID PRIMARY KEY (Organisation_Reference, Contact_ID);");
                        }
                        else if (table == "Login")
                        {
                            if (column == Glo.Tab.LOGIN_ID)
                            {
                                commands.Add("ALTER TABLE Login ADD CONSTRAINT pk_LoginID PRIMARY KEY (Login_ID);");
                                commands.Add("ALTER TABLE Conference ADD CONSTRAINT fk_ConfCreationLogin FOREIGN KEY (Creation_Login_ID) REFERENCES Login (Login_ID) ON DELETE SET NULL ON UPDATE CASCADE;");
                                // fk_ConfEditLogin cascades are handled in triggers trg_deleteConfEditLogin and trg_updateConfEditLogin to avoid cascade cycle warnings.
                                commands.Add("ALTER TABLE Conference ADD CONSTRAINT fk_ConfEditLogin FOREIGN KEY (Edit_Login_ID) REFERENCES Login (Login_ID) ON DELETE NO ACTION ON UPDATE NO ACTION;");
                                commands.Add("ALTER TABLE OrganisationChange ADD CONSTRAINT fk_OrgChange_LoginID FOREIGN KEY (Login_ID) REFERENCES Login (Login_ID) ON DELETE SET NULL ON UPDATE CASCADE;");
                                commands.Add("ALTER TABLE AssetChange ADD CONSTRAINT fk_AssetChange_LoginID FOREIGN KEY (Login_ID) REFERENCES Login (Login_ID) ON DELETE SET NULL ON UPDATE CASCADE;");
                            }
                            else if (column == Glo.Tab.LOGIN_USERNAME)
                            {
                                commands.Add("ALTER TABLE Login ADD CONSTRAINT u_Username UNIQUE (Username);");
                            }
                        }
                    }
                }
                if (allowed.Count > 0)
                {
                    if (!droppedConstraint)
                        commands.Add(DropConstraint(table, column));
                    commands.Add($"EXEC sp_executesql N'ALTER TABLE {table} " +
                                 $"ADD CONSTRAINT chk_{table}{column} " +
                                 $"CHECK ({column} " +
                                 $"IN ('{string.Join("','", allowed)}'));'");
                }

                if (friendly != null)
                {
                    commands.Add($"IF EXISTS(SELECT 1 FROM FriendlyNames " +
                             $"WHERE {Glo.Tab.FRIENDLY_TABLE} = '{table}' " +
                             $"AND {Glo.Tab.FRIENDLY_COLUMN} = '{column}') " +
                             "BEGIN " +
                             $"UPDATE FriendlyNames SET {Glo.Tab.FRIENDLY_NAME} = '{friendly}' " +
                             $"WHERE {Glo.Tab.FRIENDLY_TABLE} = '{table}' " +
                             $"AND {Glo.Tab.FRIENDLY_COLUMN} = '{column}';" +
                             "END " +
                             $"ELSE IF EXISTS(SELECT 1 FROM FriendlyNames " +
                             $"WHERE {Glo.Tab.FRIENDLY_TABLE} = '{table}' " +
                             $"AND REPLACE({Glo.Tab.FRIENDLY_NAME}, ' ', '_') = REPLACE('{friendly}', ' ', '_')) " +
                              "BEGIN " +
                             $"THROW 50000, 'That friendly name already exists.', 1; " +
                             $"END " +
                             $"ELSE " +
                             $"BEGIN " +
                             $"INSERT INTO FriendlyNames VALUES ('{table}', '{column}', '{friendly}'); " +
                              "END;");
                }

                if (commands.Count == 1)
                    command = commands[0];
                else
                    command = SqlAssist.Transaction(string.Join(" ", commands));
            }

            return command;
        }
    }

    public struct ColumnOrdering
    {
        public string sessionID;
        public int columnRecordID;
        public List<int> organisationOrder = new();
        public List<int> assetOrder = new();
        public List<int> contactOrder = new();
        public List<int> conferenceOrder = new();
        public List<Header> organisationHeaders = new();
        public List<Header> assetHeaders = new();
        public List<Header> contactHeaders = new();
        public List<Header> conferenceHeaders = new();

        public struct Header
        {
            public int position;
            public string name;
            public Header(int position, string name) { this.position = position; this.name = name; }
        }

        public ColumnOrdering(string sessionID, int columnRecordID, List<int> organisationOrder,
                                                                    List<int> assetOrder,
                                                                    List<int> contactOrder,
                                                                    List<int> conferenceOrder)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            this.organisationOrder = organisationOrder;
            this.assetOrder = assetOrder;
            this.contactOrder = contactOrder;
            this.conferenceOrder = conferenceOrder;
        }

        public string SqlCommand()
        {
            string Command(string table, List<int> order)
            {
                List<string> setters = new();

                string command = $"UPDATE {table}Order SET ";

                for (int n = 0; n < order.Count; ++n)
                    command += $"_{n} = {order[n]}, ";

                // Remove the trailing ", " (the command will fail anyway if there are no setters).
                command = command.Remove(command.Length - 2);

                return command + ";";
            }

            return SqlAssist.Transaction(new string[] { Command("Organisation", organisationOrder),
                                                        Command("Asset", assetOrder),
                                                        Command("Contact", contactOrder),
                                                        Command("Conference", conferenceOrder) });
        }

        public string HeaderConfigText()
        {
            StringBuilder str = new();
            for (int i = 0; i < 4; ++i)
            {
                string table;
                List<Header> headers;
                if (i == 0)
                {
                    table = "Organisation";
                    headers = organisationHeaders;
                }
                else if (i == 1)
                {
                    table = "Asset";
                    headers = assetHeaders;
                }
                else if (i == 2)
                {
                    table = "Contact";
                    headers = contactHeaders;
                }
                else
                {
                    table = "Conference";
                    headers = conferenceHeaders;
                }

                foreach (Header h in headers)
                    str.Append(table + ";" + h.name + ";" + h.position.ToString() + "\n");
            }

            return str.ToString();
        }
    }

    struct Organisation
    {
        public string sessionID;
        public int columnRecordID;
        public int organisationID;
        public string? organisationRef;
        public string? parentOrgRef;
        public string? name;
        public string? dialNo;
        public bool? available;
        public string? notes;
        public bool organisationRefChanged;
        public bool parentOrgRefChanged;
        public bool nameChanged;
        public bool dialNoChanged;
        public bool availableChanged;
        public bool notesChanged;
        public List<string> additionalCols;
        public List<string?> additionalVals;
        public List<bool> additionalNeedsQuotes;
        public string changeReason;

        public Organisation(string sessionID, int columnRecordID,
                            int organisationID, string? organisationRef, string? parentOrgRef, string? name,
                            string? dialNo, bool? available, string? notes, List<string> additionalCols,
                                                                            List<string?> additionalVals,
                                                                            List<bool> additionalNeedsQuotes)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            this.organisationID = organisationID;
            this.organisationRef = organisationRef;
            this.parentOrgRef = parentOrgRef;
            this.name = name;
            this.dialNo = dialNo;
            this.available = available;
            this.notes = notes;
            this.additionalCols = additionalCols;
            this.additionalVals = additionalVals;
            this.additionalNeedsQuotes = additionalNeedsQuotes;
            organisationRefChanged = false;
            parentOrgRefChanged = false;
            nameChanged = false;
            dialNoChanged = false;
            availableChanged = false;
            notesChanged = false;
            changeReason = "";
        }
        private void Prepare()
        {
            // Make sure the columns and values are safe, then add quotes where needed.
            if (organisationRef != null)
                organisationRef = SqlAssist.AddQuotes(SqlAssist.SecureValue(organisationRef));
            if (parentOrgRef != null)
                parentOrgRef = SqlAssist.AddQuotes(SqlAssist.SecureValue(parentOrgRef));
            if (name != null)
                name = SqlAssist.AddQuotes(SqlAssist.SecureValue(name));
            if (dialNo != null)
                dialNo = SqlAssist.AddQuotes(SqlAssist.SecureValue(dialNo));
            if (notes != null)
                notes = SqlAssist.AddQuotes(SqlAssist.SecureValue(notes));
            SqlAssist.SecureColumn(additionalCols);
            SqlAssist.SecureValue(additionalVals);
            SqlAssist.AddQuotes(additionalVals, additionalNeedsQuotes);
            changeReason = SqlAssist.AddQuotes(SqlAssist.SecureValue(changeReason));
        }

        public string SqlInsert(int loginID)
        {
            Prepare();

            // Not allowed under any circumstances.
            if (organisationRef == null || organisationRef == "" ||
                organisationRef == "''" || organisationRef == "NULL")
                return "";

            string com = SqlAssist.InsertInto("Organisation",
                                              SqlAssist.ColConcat(additionalCols, Glo.Tab.ORGANISATION_REF,
                                                                                  Glo.Tab.PARENT_REF,
                                                                                  Glo.Tab.ORGANISATION_NAME,
                                                                                  Glo.Tab.DIAL_NO,
                                                                                  Glo.Tab.ORGANISATION_AVAILABLE,
                                                                                  Glo.Tab.NOTES),
                                              SqlAssist.ValConcat(additionalVals, organisationRef,
                                                                                  parentOrgRef,
                                                                                  name,
                                                                                  dialNo,
                                                                                  available == true ? "1" : "0",
                                                                                  notes));
            // Create a first change instance.
            additionalCols.RemoveRange(additionalCols.Count - 6, 6); // ColConcat and ValConcat added the main fields
            additionalVals.RemoveRange(additionalVals.Count - 6, 6); // to the Lists, so walk that back here.
            int initialCount = additionalCols.Count;
            for (int i = 0; i < initialCount; ++i)
            {
                additionalCols.Add(additionalCols[i] + Glo.Tab.CHANGE_SUFFIX);
                additionalVals.Add("1");
            }
            com += SqlAssist.InsertInto("OrganisationChange",
                                        SqlAssist.ColConcat(additionalCols,
                                                            Glo.Tab.ORGANISATION_ID,
                                                            Glo.Tab.CHANGE_TIME,
                                                            Glo.Tab.LOGIN_ID,
                                                            Glo.Tab.CHANGE_REASON,
                                                            Glo.Tab.ORGANISATION_REF,
                                                            Glo.Tab.ORGANISATION_REF + Glo.Tab.CHANGE_SUFFIX,
                                                            Glo.Tab.PARENT_REF,
                                                            Glo.Tab.PARENT_REF + Glo.Tab.CHANGE_SUFFIX,
                                                            Glo.Tab.ORGANISATION_NAME,
                                                            Glo.Tab.ORGANISATION_NAME + Glo.Tab.CHANGE_SUFFIX,
                                                            Glo.Tab.DIAL_NO,
                                                            Glo.Tab.DIAL_NO + Glo.Tab.CHANGE_SUFFIX,
                                                            Glo.Tab.ORGANISATION_AVAILABLE,
                                                            Glo.Tab.ORGANISATION_AVAILABLE + Glo.Tab.CHANGE_SUFFIX,
                                                            Glo.Tab.NOTES,
                                                            Glo.Tab.NOTES + Glo.Tab.CHANGE_SUFFIX),
                                        SqlAssist.ValConcat(additionalVals,
                                                            "SCOPE_IDENTITY()",
                                                            '\'' + SqlAssist.DateTimeToSQL(DateTime.Now, false) + '\'',
                                                            loginID.ToString(),
                                                            "'Created new organisation.'",
                                                            organisationRef, "1",
                                                            parentOrgRef, "1",
                                                            name, "1",
                                                            dialNo, "1",
                                                            available == true ? "1" : "0", "1",
                                                            notes, "1"));
            return SqlAssist.Transaction(com);
        }

        public string SqlUpdate(int loginID)
        {
            Prepare();

            List<string> commands = new();

            if (organisationRefChanged)
            {
                // Not allowed under any circumstances:
                if (organisationRef == null || organisationRef == "" ||
                    organisationRef == "''" || organisationRef == "NULL")
                    return "";

                // Get the old reference ID here before we got changing it.
                commands.Add($"DECLARE @orgRef VARCHAR(MAX) = (SELECT TOP 1 {Glo.Tab.ORGANISATION_REF} " +
                                                             $"FROM Organisation " +
                                                             $"WHERE {Glo.Tab.ORGANISATION_ID} = {organisationID});");
                commands.Add($"ALTER TABLE Organisation DROP CONSTRAINT fk_ParentOrgRef;");
            }

            List<string> setters = new();
            if (organisationRefChanged)
                setters.Add(SqlAssist.Setter(Glo.Tab.ORGANISATION_REF, organisationRef));
            if (parentOrgRefChanged)
                setters.Add(SqlAssist.Setter(Glo.Tab.PARENT_REF, parentOrgRef));
            if (nameChanged)
                setters.Add(SqlAssist.Setter(Glo.Tab.ORGANISATION_NAME, name));
            if (dialNoChanged)
                setters.Add(SqlAssist.Setter(Glo.Tab.DIAL_NO, dialNo));
            if (availableChanged)
                setters.Add(Glo.Tab.ORGANISATION_AVAILABLE + " = " + (available == true ? "1": "0"));
            if (notesChanged)
                setters.Add(SqlAssist.Setter(Glo.Tab.NOTES, notes));
            for (int i = 0; i < additionalCols.Count; ++i)
                setters.Add(SqlAssist.Setter(additionalCols[i], additionalVals[i]));
            string command = SqlAssist.Update("Organisation", string.Join(", ", setters),
                                              Glo.Tab.ORGANISATION_ID, organisationID);

            // Add _Register bools for each column affected.
            int initialCount = additionalCols.Count;
            for (int i = 0; i < initialCount; ++i)
            {
                additionalCols.Add(additionalCols[i] + "_Register");
                additionalVals.Add("1");
            }
            // Put the other values into additionals for the sake of simplicity this time around.
            if (organisationRefChanged)
            {
                additionalCols.Add(Glo.Tab.ORGANISATION_REF);
                additionalCols.Add(Glo.Tab.ORGANISATION_REF + Glo.Tab.CHANGE_SUFFIX);
                additionalVals.Add(organisationRef);
                additionalVals.Add("1");
            }
            if (parentOrgRefChanged)
            {
                additionalCols.Add(Glo.Tab.PARENT_REF);
                additionalCols.Add(Glo.Tab.PARENT_REF + Glo.Tab.CHANGE_SUFFIX);
                additionalVals.Add(parentOrgRef);
                additionalVals.Add("1");
            }
            if (nameChanged)
            {
                additionalCols.Add(Glo.Tab.ORGANISATION_NAME);
                additionalCols.Add(Glo.Tab.ORGANISATION_NAME + Glo.Tab.CHANGE_SUFFIX);
                additionalVals.Add(name);
                additionalVals.Add("1");
            }
            if (dialNoChanged)
            {
                additionalCols.Add(Glo.Tab.DIAL_NO);
                additionalCols.Add(Glo.Tab.DIAL_NO + Glo.Tab.CHANGE_SUFFIX);
                additionalVals.Add(dialNo);
                additionalVals.Add("1");
            }
            if (availableChanged)
            {
                additionalCols.Add(Glo.Tab.ORGANISATION_AVAILABLE);
                additionalCols.Add(Glo.Tab.ORGANISATION_AVAILABLE + Glo.Tab.CHANGE_SUFFIX);
                additionalVals.Add(available == true ? "1" : "0");
                additionalVals.Add("1");
            }
            if (notesChanged)
            {
                additionalCols.Add(Glo.Tab.NOTES);
                additionalCols.Add(Glo.Tab.NOTES + Glo.Tab.CHANGE_SUFFIX);
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
                                                           organisationID.ToString(),
                                                           '\'' + SqlAssist.DateTimeToSQL(DateTime.Now, false) + '\'',
                                                           loginID.ToString(),
                                                           changeReason == null ? "''" : changeReason));

            commands.Add(command);

            if (organisationRefChanged && organisationRef != null)
            {
                // Update any affected organisations.
                commands.Add("WITH orgs AS ( SELECT " +
                            $"{Glo.Tab.ORGANISATION_ID} FROM Organisation " +
                            $"WHERE {Glo.Tab.PARENT_REF} = @orgRef AND {Glo.Tab.ORGANISATION_REF} != @orgRef) " +
                            $"INSERT INTO OrganisationChange ({Glo.Tab.ORGANISATION_ID}, " +
                                                            $"{Glo.Tab.LOGIN_ID}, " +
                                                            $"{Glo.Tab.CHANGE_TIME}, " +
                                                            $"{Glo.Tab.CHANGE_REASON}, " +
                                                            $"{Glo.Tab.PARENT_REF}, " +
                                                            $"{Glo.Tab.PARENT_REF}{Glo.Tab.CHANGE_SUFFIX}) " +
                            $"SELECT {Glo.Tab.ORGANISATION_ID}, " +
                                   $"{loginID}, " +
                                   $"'{SqlAssist.DateTimeToSQL(DateTime.Now, false)}', " +
                                   $"'Parent organisation ''' + @orgRef + ''' was renamed to '{organisationRef}'.', " +
                                   $"{organisationRef}, 1 " +
                             "FROM orgs;");
                commands.Add($"UPDATE Organisation SET {Glo.Tab.PARENT_REF} = {organisationRef} " +
                             $"WHERE {Glo.Tab.PARENT_REF} = @orgRef;"
                             );

                // Provide affected assets with change records.
                commands.Add("WITH assets AS ( SELECT " +
                            $"{Glo.Tab.ASSET_ID} FROM Asset " +
                            $"WHERE {Glo.Tab.ORGANISATION_REF} = @orgRef) " +
                            $"INSERT INTO AssetChange ({Glo.Tab.ASSET_ID}, " +
                                                     $"{Glo.Tab.LOGIN_ID}, " +
                                                     $"{Glo.Tab.CHANGE_TIME}, " +
                                                     $"{Glo.Tab.CHANGE_REASON}, " +
                                                     $"{Glo.Tab.ORGANISATION_REF}, " +
                                                     $"{Glo.Tab.ORGANISATION_REF}{Glo.Tab.CHANGE_SUFFIX}) " +
                            $"SELECT {Glo.Tab.ASSET_ID}, " +
                                   $"{loginID}, " +
                                   $"'{SqlAssist.DateTimeToSQL(DateTime.Now, false)}', " +
                                   $"'Organisation ''' + @orgRef + ''' was renamed to '{organisationRef}'.', " +
                                   $"{organisationRef}, 1 " +
                            $"FROM assets;");
            }

            if (organisationRefChanged)
            {
                commands.Add($"ALTER TABLE Organisation " +
                             $"ADD CONSTRAINT fk_ParentOrgRef FOREIGN KEY({Glo.Tab.PARENT_REF}) " +
                             $"REFERENCES Organisation({Glo.Tab.ORGANISATION_REF});");
            }

            return SqlAssist.Transaction(commands.ToArray());
        }
    }

    struct Asset
    {
        public string sessionID;
        public int columnRecordID;
        public int assetID;
        public string? assetRef;
        public string? organisationRef;
        public string? notes;
        public bool assetRefChanged = false;
        public bool organisationRefChanged = false;
        public bool notesChanged = false;
        public List<string> additionalCols;
        public List<string?> additionalVals;
        public List<bool> additionalNeedsQuotes;
        public string changeReason;

        public Asset(string sessionID, int columnRecordID,
                     int assetID, string? assetRef, string? organisationRef, string? notes,
                     List<string> additionalCols,
                     List<string?> additionalVals,
                     List<bool> additionalNeedsQuotes)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            this.assetID = assetID;
            this.assetRef = assetRef;
            this.organisationRef = organisationRef;
            this.notes = notes;
            this.additionalCols = additionalCols;
            this.additionalVals = additionalVals;
            this.additionalNeedsQuotes = additionalNeedsQuotes;
            changeReason = "";
        }

        private void Prepare()
        {
            // Make sure the columns and values are safe, then add quotes where needed.
            if (assetRef != null)
                assetRef = SqlAssist.AddQuotes(SqlAssist.SecureValue(assetRef));
            if (organisationRef != null)
                organisationRef = SqlAssist.AddQuotes(SqlAssist.SecureValue(organisationRef));
            if (notes != null)
                notes = SqlAssist.AddQuotes(SqlAssist.SecureValue(notes));
            SqlAssist.SecureColumn(additionalCols);
            SqlAssist.SecureValue(additionalVals);
            SqlAssist.AddQuotes(additionalVals, additionalNeedsQuotes);
            changeReason = SqlAssist.AddQuotes(SqlAssist.SecureValue(changeReason));
        }

        public string SqlInsert(int loginID)
        {
            Prepare();

            // Not allowed under any circumstances.
            if (assetRef == null || assetRef == "" ||
                assetRef == "''" || assetRef == "NULL")
                return "";

            string com = SqlAssist.InsertInto("Asset",
                                              SqlAssist.ColConcat(additionalCols, Glo.Tab.ASSET_REF,
                                                                                  Glo.Tab.ORGANISATION_REF,
                                                                                  Glo.Tab.NOTES),
                                              SqlAssist.ValConcat(additionalVals, assetRef,
                                                                                  organisationRef,
                                                                                  notes));
            // Create a first change instance.
            additionalCols.RemoveRange(additionalCols.Count - 3, 3); // ColConcat and ValConcat added the main fields
            additionalVals.RemoveRange(additionalVals.Count - 3, 3); // to the Lists, so walk that back here.
            int initialCount = additionalCols.Count;
            for (int i = 0; i < initialCount; ++i)
            {
                additionalCols.Add(additionalCols[i] + Glo.Tab.CHANGE_SUFFIX);
                additionalVals.Add("1");
            }
            com += SqlAssist.InsertInto("AssetChange",
                                        SqlAssist.ColConcat(additionalCols,
                                                            Glo.Tab.ASSET_ID,
                                                            Glo.Tab.CHANGE_TIME,
                                                            Glo.Tab.LOGIN_ID,
                                                            Glo.Tab.CHANGE_REASON,
                                                            Glo.Tab.ASSET_REF,
                                                            Glo.Tab.ASSET_REF + Glo.Tab.CHANGE_SUFFIX,
                                                            Glo.Tab.ORGANISATION_REF,
                                                            Glo.Tab.ORGANISATION_REF + Glo.Tab.CHANGE_SUFFIX,
                                                            Glo.Tab.NOTES,
                                                            Glo.Tab.NOTES + Glo.Tab.CHANGE_SUFFIX),
                                        SqlAssist.ValConcat(additionalVals,
                                                            "SCOPE_IDENTITY()",
                                                            '\'' + SqlAssist.DateTimeToSQL(DateTime.Now, false) + '\'',
                                                            loginID.ToString(),
                                                            "'Created new asset.'",
                                                            assetRef, "1",
                                                            organisationRef, "1",
                                                            notes, "1"));
            return SqlAssist.Transaction(com);
        }

        public string SqlUpdate(int loginID)
        {
            Prepare();

            // Not allowed under any circumstances.
            if (assetRefChanged)
                if (assetRef == null || assetRef == "" ||
                    assetRef == "''" || assetRef == "NULL")
                    return "";

            List<string> setters = new();
            if (assetRefChanged)
                setters.Add(SqlAssist.Setter(Glo.Tab.ASSET_REF, assetRef));
            if (organisationRefChanged)
                setters.Add(SqlAssist.Setter(Glo.Tab.ORGANISATION_REF, organisationRef));
            if (notesChanged)
                setters.Add(SqlAssist.Setter(Glo.Tab.NOTES, notes));
            for (int i = 0; i < additionalCols.Count; ++i)
                setters.Add(SqlAssist.Setter(additionalCols[i], additionalVals[i]));
            string com = SqlAssist.Update("Asset", string.Join(", ", setters),
                                          Glo.Tab.ASSET_ID, assetID);

            // Add _Register bools for each column affected.
            int initialCount = additionalCols.Count;
            for (int i = 0; i < initialCount; ++i)
            {
                additionalCols.Add(additionalCols[i] + "_Register");
                additionalVals.Add("1");
            }
            // Put the other values into additionals for the sake of simplicity this time around.
            if (assetRefChanged)
            {
                additionalCols.Add(Glo.Tab.ASSET_REF);
                additionalCols.Add(Glo.Tab.ASSET_REF + Glo.Tab.CHANGE_SUFFIX);
                additionalVals.Add(assetRef);
                additionalVals.Add("1");
            }
            if (organisationRefChanged)
            {
                additionalCols.Add(Glo.Tab.ORGANISATION_REF);
                additionalCols.Add(Glo.Tab.ORGANISATION_REF + Glo.Tab.CHANGE_SUFFIX);
                additionalVals.Add(organisationRef);
                additionalVals.Add("1");
            }
            if (notesChanged)
            {
                additionalCols.Add(Glo.Tab.NOTES);
                additionalCols.Add(Glo.Tab.NOTES + Glo.Tab.CHANGE_SUFFIX);
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
                                                            assetID.ToString(),
                                                            '\'' + SqlAssist.DateTimeToSQL(DateTime.Now, false) + '\'',
                                                            loginID.ToString(),
                                                            changeReason == null ? "" : changeReason));
            return SqlAssist.Transaction(com);
        }
    }

    struct Contact
    {
        public string sessionID;
        public int columnRecordID;
        public int contactID = -1;
        public string? notes;
        public bool notesChanged;
        public List<string> additionalCols;
        public List<string?> additionalVals;
        public List<bool> additionalNeedsQuotes;
        public bool requireIdBack;

        public Contact(string sessionID, int columnRecordID,
                       int contactID, string? notes, List<string> additionalCols,
                                                     List<string?> additionalVals,
                                                     List<bool> additionalNeedsQuotes)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
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

    struct Conference
    {
        public string sessionID;
        public int columnRecordID;
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

        public Conference(string sessionID, int columnRecordID,
                          int conferenceID, int typeID, string? title,
                          DateTime start, DateTime end, TimeSpan buffer,
                          string? organisationID, int? recurrenceID, string? notes, List<string> additionalCols,
                                                                                    List<string?> additionalVals)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
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
                                        SqlAssist.ColConcat(additionalCols, Glo.Tab.CONFERENCE_TYPE_ID,
                                                                            Glo.Tab.CONFERENCE_TITLE,
                                                                            Glo.Tab.CONFERENCE_START,
                                                                            Glo.Tab.CONFERENCE_END,
                                                                            Glo.Tab.CONFERENCE_CANCELLED,
                                                                            Glo.Tab.ORGANISATION_REF,
                                                                            Glo.Tab.RECURRENCE_ID,
                                                                            "Notes"),
                                        SqlAssist.ValConcat(additionalVals, typeID.ToString(),
                                                                            title,
                                                                            SqlAssist.DateTimeToSQL(start, false),
                                                                            SqlAssist.DateTimeToSQL(end, false),
                                                                            SqlAssist.TimeSpanToSQL(buffer),
                                                                            organisationID,
                                                                            recurrenceID.ToString(),
                                                                            notes));
        }
    }

    struct Resource
    {
        public string sessionID;
        public int columnRecordID;
        public int resourceID;
        public string? name;
        public int connectionCapacity;
        public int conferenceCapacity;
        public int rowsAdditional;

        public Resource(string sessionID, int columnRecordID, int resourceID, string? name,
                        int connectionCapacity, int conferenceCapacity, int rowsAdditional)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            this.resourceID = resourceID;
            this.name = name;
            this.connectionCapacity = connectionCapacity;
            this.conferenceCapacity = conferenceCapacity;
            this.rowsAdditional = rowsAdditional;
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
                                                 Glo.Tab.RESOURCE_CAPACITY_CONNECTION,
                                                 Glo.Tab.RESOURCE_CAPACITY_CONFERENCE,
                                                 Glo.Tab.RESOURCE_ROWS_ADDITIONAL),
                             SqlAssist.ValConcat(name,
                                                 connectionCapacity.ToString(),
                                                 conferenceCapacity.ToString(),
                                                 rowsAdditional.ToString()));
        }
    }

    struct Login
    {
        public string sessionID;
        public int columnRecordID;
        public int loginID;
        public string username;
        public string password;
        public bool admin;
        public int createPermissions;
        public int editPermissions;
        public int deletePermissions;
        public bool enabled;

        public Login(string sessionID, int columnRecordID,
                     int loginID, string username, string password, bool admin,
                     int createPermissions, int editPermissions, int deletePermissions, bool enabled)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
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
        public int columnRecordID;
        public string table;
        public string column;

        public PrimaryColumnSelect(string sessionID, int columnRecordID, string table, string column)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            this.table = table;
            this.column = column;
        }

        public string SqlSelect { get { return "SELECT " + column + " FROM " + table + ";"; } }
    }

    public enum Conditional
    {
        Equals,
        Like
    }
    struct QuickSelectRequest
    {
        public string sessionID;
        public int columnRecordID;
        public string table;
        public List<string> select;
        public List<string> likeColumns;
        public List<string> likeValues;
        public List<Conditional> conditionals;
        public bool and; // false: or
        public bool includeHistory;

        public QuickSelectRequest(string sessionID, int columnRecordID, string table,
                             List<string> select,
                             List<string> likeColumns, List<string> likeValues, List<Conditional> conditionals,
                             bool and, bool includeHistory)
        {
            /* There is no check here to make sure that columns and values are the equal lengths. Be careful
               to respect this restriction. Agent will throw an exception if they are unequal. */
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            this.table = table;
            this.select = select;
            this.likeColumns = likeColumns;
            this.likeValues = likeValues;
            this.conditionals = conditionals;
            this.includeHistory = includeHistory;
            this.and = and;
        }

        public void Prepare()
        {
            table = SqlAssist.SecureColumn(table);
            SqlAssist.SecureColumn(select);
            SqlAssist.SecureColumn(likeColumns);
            for (int i = 0; i < likeValues.Count; ++i)
                likeValues[i] = SqlAssist.SecureValue(likeValues[i]);
        }

        public bool Validate()
        {
            return (likeColumns.Count != likeValues.Count || likeColumns.Count != conditionals.Count);
        }
    }

    public struct SelectRequest // Public due to App.SendSelectRequest() being public.
    {
        public string sessionID;
        public int columnRecordID;
        public string table;
        public bool distinct;
        public List<string> joinTables;
        public List<string> joinColumns1;
        public List<string> joinColumns2;
        public List<string> joinTypes;
        public List<string> columns;
        public List<string> columnAliases;
        public List<string> whereColumns;
        public List<string> whereOperators;
        public List<string?> whereValues;
        public List<bool> whereValueTypesNeedQuotes;
        public List<string> whereAndOrs;
        public List<int> whereBracketsOpen;
        public List<int> whereBracketsClose;
        public List<string> orderBy;
        public List<bool> orderByAsc;

        public SelectRequest(string sessionID, int columnRecordID,
                             string table, bool distinct,
                             List<string> joinTables,
                             List<string> joinColumns1, List<string> joinColumns2,
                             List<string> joinTypes,
                             List<string> columns, List<string> columnAliases,
                             List<string> whereColumns, List<string> whereOperators,
                             List<string?> whereValues, List<bool> whereValueTypesNeedQuotes,
                             List<int> whereBracketsOpen, List<int> whereBracketsClose, List<string> whereAndOrs,
                             List<string> orderBy,
                             List<bool> orderByAsc)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            this.table = table;
            this.distinct = distinct;
            this.joinTables = joinTables;
            this.joinColumns1 = joinColumns1;
            this.joinColumns2 = joinColumns2;
            this.joinTypes = joinTypes;
            this.columns = columns;
            this.columnAliases = columnAliases;
            this.whereColumns = whereColumns;
            this.whereOperators = whereOperators;
            this.whereValues = whereValues;
            this.whereValueTypesNeedQuotes = whereValueTypesNeedQuotes;
            this.whereAndOrs = whereAndOrs;
            this.whereBracketsOpen = whereBracketsOpen;
            this.whereBracketsClose = whereBracketsClose;
            this.orderBy = orderBy;
            this.orderByAsc = orderByAsc;
        }

        public void Prepare()
        {
            table = SqlAssist.SecureColumn(table);
            SqlAssist.SecureColumn(joinTables);
            SqlAssist.SecureColumn(joinColumns1);
            SqlAssist.SecureColumn(joinColumns2);
            if (!SqlAssist.CheckJoinTypes(joinTypes))
                joinTypes.Clear();
            SqlAssist.SecureColumn(columns);
            SqlAssist.SecureColumn(columnAliases);
            SqlAssist.SecureColumn(whereColumns);
            if (!SqlAssist.CheckOperators(whereOperators))
                whereOperators.Clear();
            SqlAssist.SecureValue(whereValues);
            SqlAssist.SecureColumn(orderBy);
        }

        public bool Validate()
        {
            return joinTables.Count == joinColumns1.Count &&
                   joinTables.Count == joinColumns2.Count &&
                   joinTables.Count == joinTypes.Count &&
                   columns.Count == columnAliases.Count &&
                   whereColumns.Count == whereOperators.Count &&
                   whereOperators.Count == whereValues.Count &&
                   whereValues.Count == whereValueTypesNeedQuotes.Count &&
                   whereBracketsOpen.Count == whereBracketsClose.Count &&
                   (whereColumns.Count == 0 || whereAndOrs.Count == whereColumns.Count - 1) &&
                   orderBy.Count == orderByAsc.Count;
        }

        public string SqlSelect()
        {
            if (!Validate())
                return "";

            Prepare();

            // SELECT columns
            StringBuilder str = new("SELECT ");
            if (distinct)
                str.Append("DISTINCT ");
            for (int i = 0; i < columns.Count; ++i)
            {
                if (i > 0)
                    str.Append($"{(distinct ? "                " : "       ")}");
                str.Append(columnAliases[i] == "" ? $"{columns[i]},\n" : $"{columns[i]} AS {columnAliases[i]},\n");
            }

            // Get rid of the trailing ",", leaving the new line.
            str.Remove(str.Length - 2, 1);

            str.Append($"FROM {table}\n");

            // JOINs
            for (int i = 0; i < joinTables.Count; ++i)
                str.Append($"{joinTypes[i]} JOIN {joinTables[i]} ON {joinColumns1[i]} = {joinColumns2[i]}\n");

            // WHEREs (sort left-associatively)
            bool foundAnd = false;
            bool foundOr = false;
            foreach (string andOr in whereAndOrs)
            {
                if (!foundAnd && andOr == "AND")
                    foundAnd = true;
                else if (!foundOr && andOr == "OR")
                    foundOr = true;
                if (foundAnd && foundOr)
                    break;
            }
            bool addBrackets = foundAnd && foundOr; // Brackets only needed if true.
            if (whereColumns.Count > 0)
            {
                str.Append("WHERE ");
                if (addBrackets)
                    for (int i = 0; i < whereColumns.Count; ++i)
                        str.Append('(');
                for (int i = 0; i < whereColumns.Count; ++i)
                {
                    if (i > 0)
                        str.Append($"{(whereAndOrs[i - 1] == "OR" ? "   " : "  ")}{whereAndOrs[i - 1]} ");
                    str.Append($"{whereColumns[i]} {whereOperators[i]}");
                    if (whereValues[i] != null)
                        str.Append(whereValueTypesNeedQuotes[i] ?
                                   " " + SqlAssist.AddQuotes(whereValues[i]!) : " " + whereValues[i]);
                    str.Append(addBrackets ? ")\n" : "\n");
                }
            }

            // ORDER BY
            if (orderBy.Count > 0)
            {
                str.Append("ORDER BY ");
                for (int i = 0; i < orderBy.Count; ++i)
                {
                    if (i > 0)
                        str.Append("         ");
                    str.Append(orderBy[i] + (orderByAsc[i] ? " ASC" : " DESC") + ",\n");
                }
                // Get rid of the trailing ",", leaving the new line.
                str = str.Remove(str.Length - 2, 1);
            }

            str = str.Remove(str.Length - 1, 1);
            str.Append(';');

            return str.ToString();
        }
    }

    struct SelectWideRequest
    {
        public string sessionID;
        public int columnRecordID;
        public List<string> select;
        public string table;
        public string value;
        public bool includeHistory;

        public SelectWideRequest(string sessionID, int columnRecordID,
                                 List<string> select, string table, string value,
                                 bool includeHistory)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            this.table = table;
            this.select = select;
            this.value = value;
            this.includeHistory = includeHistory;
        }

        public void Prepare()
        {
            table = SqlAssist.SecureColumn(table);
            SqlAssist.SecureColumn(select);
            value = SqlAssist.SecureValue(value);
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

    public struct UpdateRequest
    {
        public string sessionID;
        public int columnRecordID;
        public int loginID;
        public string table;
        public List<string> columns;
        public List<string?> values;
        public List<bool> columnsNeedQuotes;
        public string idColumn;
        public List<string> ids;
        public bool idQuotes;
        public string changeReason = "";

        public UpdateRequest(string sessionID, int columnRecordID, int loginID,
                             string table, List<string> columns, List<string?> values, List<bool> columnsNeedQuotes,
                             string idColumn, List<string> ids, bool idQuotes)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            this.loginID = loginID;
            this.table = table;
            this.columns = columns;
            this.values = values;
            this.columnsNeedQuotes = columnsNeedQuotes;
            this.idColumn = idColumn;
            this.ids = ids;
            this.idQuotes = idQuotes;
        }

        private void Prepare()
        {
            table = SqlAssist.SecureColumn(table);
            SqlAssist.SecureColumn(columns);
            SqlAssist.SecureValue(values);
            idColumn = SqlAssist.SecureColumn(idColumn);
            SqlAssist.SecureValue(ids!);
            changeReason = SqlAssist.SecureValue(changeReason);
        }

        private bool Validate()
        {
            return columns.Count > 0 &&
                   columns.Count == values.Count &&
                   columns.Count == columnsNeedQuotes.Count &&
                   ids.Count > 0 &&
                   !(table == "Organisation" && columns.Contains(Glo.Tab.ORGANISATION_REF)); // Not allowed.
        }

        public string SqlUpdate()
        {
            Prepare();

            SqlAssist.AddQuotes(values, columnsNeedQuotes);

            if (!Validate())
                return "";

            List<string> commands = new();

            // We'll need the organisation or reference ids 

            StringBuilder str = new("UPDATE " + table + " SET ");
            List<string> setters = new();
            for (int i = 0; i < columns.Count; ++i)
                setters.Add(SqlAssist.Setter(columns[i], values[i]));
            str.Append(string.Join(", ", setters));
            str.Append(" WHERE ");
            List<string> wheres = new();
            foreach (string id in ids)
                wheres.Add(SqlAssist.Setter(idColumn, SqlAssist.AddQuotes(id, idQuotes)));
            str.Append(string.Join(" OR ", wheres) + ";");
            commands.Add(str.ToString());

            // Add records to the change log if this is for the organisation or asset table.
            if (table == "Asset" || table == "Organisation")
            {
                // Note that we don't need to worry about organisation references changing in the organisation table,
                // as this isn't allowed for generic updates. That must be done through the NewOrganisation window in
                // the client.

                // Add register columns and switch them all on.
                int count = columns.Count;
                for (int i = 0; i < count; ++i)
                    columns.Add(columns[i] + Glo.Tab.CHANGE_SUFFIX);
                for (int i = 0; i < count; ++i)
                    values.Add("1");

                // I tried this all in one command together using INSERT INTO / SELECT / FROM, but there was literally
                // no speed increase with 24,000-odd records. I'll leave it like this as I feel like it's slightly
                // more readable.
                foreach (string id in ids)
                    commands.Add($"INSERT INTO {table}Change ({idColumn}, " +
                                                            $"{Glo.Tab.CHANGE_TIME}, " +
                                                            $"{Glo.Tab.LOGIN_ID}, " +
                                                            $"{Glo.Tab.CHANGE_REASON}, " +
                                                            $"{string.Join(", ", columns)}) " +
                                 $"VALUES ({id}, " +
                                         $"'{SqlAssist.DateTimeToSQL(DateTime.Now, false)}', " +
                                         $"{loginID}, " +
                                         $"'{changeReason}', " +
                                         $"{string.Join(", ", values)});" // Quotes were already added previously.
                        );
            }

            if (commands.Count > 1)
                return SqlAssist.Transaction(commands.ToArray());
            else
                return commands[0];
        }
    }

    struct DeleteRequest
    {
        public string sessionID;
        public int columnRecordID;
        public int loginID;
        public string table;
        public string column;
        public List<string?> ids;
        public bool needsQuotes;

        public DeleteRequest(string sessionID, int columnRecordID, int loginID,
                             string table, string column, List<string> ids, bool needsQuotes)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            this.loginID = loginID;
            this.table = table;
            this.column = column;
            this.ids = ids!;
            this.needsQuotes = needsQuotes;
        }

        public void Prepare()
        {
            table = SqlAssist.SecureColumn(table);
            column = SqlAssist.SecureColumn(column);
            SqlAssist.SecureValue(ids);
        }

        public string SqlDelete()
        {
            Prepare();

            List<string> commands = new();

            // Organisation deletions are a tad more complicated, because change log alterations need to be made
            // for any referencing assets or organisations.
            if (table == "Organisation")
            {
                StringBuilder idList = new();
                foreach (string? s in ids)
                    if (s != null)
                        idList.Append(s + ", ");
                // Remove the last ", "
                idList.Remove(idList.Length - 2, 2);

                // Update affected organisations.
                commands.Add($"WITH orgs AS ( SELECT " +
                             $"{Glo.Tab.ORGANISATION_ID}, {Glo.Tab.PARENT_REF} FROM Organisation " +
                             $"WHERE {Glo.Tab.PARENT_REF} IN ( " +
                                 $"SELECT {Glo.Tab.ORGANISATION_REF} " +
                                 $"FROM Organisation " +
                                 $"WHERE {Glo.Tab.ORGANISATION_ID} IN ({idList}))) " +
                             $"INSERT INTO OrganisationChange ({Glo.Tab.ORGANISATION_ID}, " +
                                                             $"{Glo.Tab.LOGIN_ID}, " +
                                                             $"{Glo.Tab.CHANGE_TIME}, " +
                                                             $"{Glo.Tab.CHANGE_REASON}, " +
                                                             $"{Glo.Tab.PARENT_REF}, " +
                                                             $"{Glo.Tab.PARENT_REF}{Glo.Tab.CHANGE_SUFFIX}) " +
                             $"SELECT {Glo.Tab.ORGANISATION_ID}, " +
                                    $"{loginID}, " +
                                    $"'{SqlAssist.DateTimeToSQL(DateTime.Now, false)}', " +
                                    $"'Parent organisation ''' + {Glo.Tab.PARENT_REF} + ''' was deleted.', " +
                                    $"NULL, 1 " +
                              "FROM orgs; ");
                commands.Add($"UPDATE Organisation SET {Glo.Tab.PARENT_REF} = NULL " +
                             $"WHERE {Glo.Tab.PARENT_REF} IN ( " +
                                 $"SELECT {Glo.Tab.ORGANISATION_REF} " +
                                 $"FROM Organisation " +
                                 $"WHERE {Glo.Tab.ORGANISATION_ID} IN ({idList}));"
                             );

                // Affected assets.
                commands.Add("WITH assets AS ( SELECT " +
                            $"{Glo.Tab.ASSET_ID}, {Glo.Tab.ORGANISATION_REF} FROM Asset " +
                            $"WHERE {Glo.Tab.ORGANISATION_REF} IN (" +
                                 $"SELECT {Glo.Tab.ORGANISATION_REF} " +
                                 $"FROM Organisation " +
                                 $"WHERE {Glo.Tab.ORGANISATION_ID} IN ({idList}))) " +
                            $"INSERT INTO AssetChange ({Glo.Tab.ASSET_ID}, " +
                                                     $"{Glo.Tab.LOGIN_ID}, " +
                                                     $"{Glo.Tab.CHANGE_TIME}, " +
                                                     $"{Glo.Tab.CHANGE_REASON}, " +
                                                     $"{Glo.Tab.ORGANISATION_REF}, " +
                                                     $"{Glo.Tab.ORGANISATION_REF}{Glo.Tab.CHANGE_SUFFIX}) " +
                            $"SELECT {Glo.Tab.ASSET_ID}, " +
                                   $"{loginID}, " +
                                   $"'{SqlAssist.DateTimeToSQL(DateTime.Now, false)}', " +
                                   $"'Organisation ''' + {Glo.Tab.ORGANISATION_REF} + ''' was deleted.', " +
                                   $"NULL, 1 " +
                            $"FROM assets;");
            }

            // Actually delete the record.

            string[] conditions = new string[ids.Count];
            for (int i = 0; i < ids.Count; ++i)
                conditions[i] = column + " = " +
                                SqlAssist.AddQuotes(ids[i], needsQuotes);

            commands.Add("DELETE FROM " + table +
                         " WHERE " + string.Join(" OR ", conditions) + ';');

            if (commands.Count > 1)
                return SqlAssist.Transaction(commands.ToArray());
            else
                return commands[0];
        }
    }

    struct LinkContactRequest
    {
        public string sessionID;
        public int columnRecordID;
        public string organisationRef;
        public int contactID;
        public bool unlink;

        public LinkContactRequest(string sessionID, int columnRecordID,
                                  string organisationRef, int contactID, bool unlink)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            this.organisationRef = organisationRef;
            this.contactID = contactID;
            this.unlink = unlink;
        }

        private void Prepare()
        {
            organisationRef = SqlAssist.AddQuotes(SqlAssist.SecureValue(organisationRef));
        }

        public string SqlInsert()
        {
            Prepare();
            return SqlAssist.InsertInto("OrganisationContacts",
                                        Glo.Tab.ORGANISATION_REF + ", " + Glo.Tab.CONTACT_ID,
                                        organisationRef + ", " + contactID.ToString());
        }

        public string SqlDelete()
        {
            Prepare();
            return "DELETE FROM OrganisationContacts " +
                   "WHERE " + Glo.Tab.ORGANISATION_REF + " = " + organisationRef +
                  " AND " + Glo.Tab.CONTACT_ID + " = " + contactID.ToString() + ";";
        }
    }

    struct LinkedContactSelectRequest
    {
        public string sessionID;
        public int columnRecordID;
        public string organisationRef;

        public LinkedContactSelectRequest(string sessionID, int columnRecordID, string organisationRef)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            this.organisationRef = organisationRef;
        }

        private void Prepare()
        {
            organisationRef = SqlAssist.AddQuotes(SqlAssist.SecureValue(organisationRef));
        }

        public string SqlSelect()
        {
            Prepare();

            return "SELECT Contact.* FROM Contact" +
                  " JOIN OrganisationContacts ON OrganisationContacts." + Glo.Tab.CONTACT_ID +
                        " = Contact." + Glo.Tab.CONTACT_ID +
                  " JOIN Organisation ON Organisation." + Glo.Tab.ORGANISATION_REF +
                        " = OrganisationContacts." + Glo.Tab.ORGANISATION_REF +
                  " WHERE Organisation." + Glo.Tab.ORGANISATION_REF + " = " + organisationRef + ";";
        }
    }

    struct SelectHistoryRequest
    {
        public string sessionID;
        public int columnRecordID;
        public string tableName;
        public int recordID;

        public SelectHistoryRequest(string sessionID, int columnRecordID, string tableName, int recordID)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            this.tableName = tableName;
            this.recordID = recordID;
        }

        private void Prepare()
        {
            tableName = SqlAssist.SecureColumn(tableName);
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
                            + " = " + recordID.ToString() +
                  " ORDER BY " + Glo.Tab.CHANGE_TIME + " DESC;";
        }
    }

    struct SelectHistoricalRecordRequest
    {
        public string sessionID;
        public int columnRecordID;
        public string tableName;
        public string changeID;
        public int recordID;

        public SelectHistoricalRecordRequest(string sessionID, int columnRecordID,
                                             string tableName, string changeID, int recordID)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            this.tableName = tableName;
            this.changeID = changeID;
            this.recordID = recordID;
        }

        private void Prepare()
        {
            tableName = SqlAssist.SecureColumn(tableName);
            changeID = SqlAssist.SecureValue(changeID);
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

    struct ChangeReasonUpdate
    {
        public string sessionID;
        public int columnRecordID;
        public string tableName;
        public int changeID;
        public string reason;

        public ChangeReasonUpdate(string sessionID, int columnRecordID,
                                  string tableName, int changeID, string reason)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            this.tableName = tableName;
            this.changeID = changeID;
            this.reason = reason;
        }

        private void Prepare()
        {
            tableName = SqlAssist.SecureColumn(tableName) + "Change";
            reason = SqlAssist.AddQuotes(SqlAssist.SecureValue(reason));
        }

        public string SqlUpdate()
        {
            Prepare();

            return SqlAssist.Update(tableName, $"{Glo.Tab.CHANGE_REASON} = {reason}", Glo.Tab.CHANGE_ID, changeID);
        }
    }


    //   H E L P E R   F U N C T I O N S

    public class SqlAssist
    {
        public static string SecureColumn(string val)
        {
            if (val.Contains(';') || val.Contains('\'') || val.Contains(' '))
                return "";
            else
                return val;
        }
        public static void SecureColumn(List<string> vals)
        {
            // Blank columns will cause an exception when the command is run, causing the SQL query/non-query to abort.
            for (int i = 0; i < vals.Count; ++i)
                if (vals[i].Contains(';') || vals[i].Contains('\'') || vals[i].Contains(' '))
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

        private static HashSet<string> joinTypes = new() { "INNER", "LEFT", "RIGHT", "LEFT OUTER", "RIGHT OUTER", "FULL OUTER" };
        private static HashSet<string> operators = new() { "=", "<", ">", "<=", ">=", "LIKE", "IS NULL", "IS NOT NULL" };
        public static bool CheckJoinTypes(List<string> toCheck)
        {
            foreach (string s in toCheck)
                if (!joinTypes.Contains(s))
                    return false;
            return true;
        }
        public static bool CheckOperators(List<string> toCheck)
        {
            foreach (string s in operators)
                if (!operators.Contains(s))
                    return false;
            return true;
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
        public static string AddQuotes(string? val, bool needsQuotes)
        {
            if (val == null)
                return "";
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

        public static string DateTimeToSQL(DateTime dateTime, bool dateOnly)
        {
            if (dateOnly)
            {
                return dateTime.ToString("yyyy-MM-dd");
            }
            else
            {
                return dateTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
            }
        }
        public static string TimeSpanToSQL(TimeSpan timeSpan)
        {
            return timeSpan.ToString(@"hh\:mm");
        }
    }
}