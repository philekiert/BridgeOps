using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics.Metrics;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
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

        static readonly JsonSerializerOptions jsonOpts = new();
        static readonly UnicodeEncoding unicodeEncoding = new();

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
            List<byte> lB = new();
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
                TcpClient client = new();
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
        public List<ConnectedClient> connectedClients = new();

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
        public bool softDuplicateCheck;
        public bool unique;

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
            softDuplicateCheck = false;
            unique = false;
        }

        // Addition
        public TableModification(string sessionID, int columnRecordID, string table, string column,
                                 string columnType, List<string> allowed, bool unique)
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
            softDuplicateCheck = false;
            this.unique = unique;
        }

        // Modification
        public TableModification(string sessionID, int columnRecordID, string table, string column,
                                 string? columnRename, string? friendly,
                                 string? columnType, List<string> allowed, bool unique)
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
            softDuplicateCheck = false;
            this.unique = unique;
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
                allowed[n] = SqlAssist.AddQuotes(SqlAssist.SecureValue(allowed[n]))
                             .Replace("\r\n", "")
                             .Replace("\n", ""); // If any new lines sneak in, they could break the column record.
        }

        private string DropConstraints(string table, string column)
        {
            // First drop the allow list if there was one, then the unique constraint if there was one.
            string command = $"IF EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS " +
                             $"WHERE CONSTRAINT_TYPE = 'CHECK' AND CONSTRAINT_NAME = 'chk_{table}{column}') " +
                              "BEGIN " +
                             $"ALTER TABLE {table} DROP CONSTRAINT chk_{table}{column}; " +
                              "END ";
            if (Glo.Fun.ColumnRemovalAllowed(table, column))
                command += $"IF EXISTS(SELECT 1 FROM sys.indexes " +
                           $"WHERE name = 'u_{table}{column}') " +
                            "BEGIN " +
                           $"DROP INDEX u_{table}{column} ON {table}; " +
                            "END ";
            return command;
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
                    command += $" CONSTRAINT chk_{table}{column}" +
                               $" CHECK ({column} IN ({string.Join(", ", allowed)}))";
                command += ";";
                // Unique cannot be set on new columns, and must be set in an edit after the fact.

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
                command = DropConstraints(table, column);
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
                    commands.Add(DropConstraints(table, column));
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
                    commands.Add(DropConstraints(table, column));
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
                        commands.Add(DropConstraints(table, column));
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
                            commands.Add($"ALTER TABLE OrganisationContacts ALTER COLUMN {column} {columnType} NOT NULL;");
                        }
                        if (column == Glo.Tab.DIAL_NO)
                        {
                            reAddKeys = true;
                            commands.Add("DROP INDEX u_OrgDialNo ON Organisation;");
                            commands.Add("ALTER TABLE Connection DROP CONSTRAINT u_ConnectionConfIDDialNo;");
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
                            commands.Add("DROP INDEX u_AssetRef ON Asset;");
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
                    else if (table == "Task")
                    {
                        if (column == Glo.Tab.TASK_ID)
                        {
                            reAddKeys = true;
                            commands.Add("ALTER TABLE Task DROP CONSTRAINT pk_TaskID;");
                        }
                        else if (column == Glo.Tab.TASK_REFERENCE)
                        {
                            reAddKeys = true;
                            commands.Add("DROP INDEX u_OrgTaskRef ON Organisation;");
                            commands.Add("DROP INDEX U_TaskRef ON Task;");
                            commands.Add($"ALTER TABLE Organisation ALTER COLUMN {column} {columnType};");
                            commands.Add($"ALTER TABLE Visit ALTER COLUMN {column} {columnType};");
                            commands.Add($"ALTER TABLE Document ALTER COLUMN {column} {columnType};");
                        }
                    }
                    else if (table == "Visit")
                    {
                        if (column == Glo.Tab.VISIT_ID)
                        {
                            reAddKeys = true;
                            commands.Add("ALTER TABLE Visit DROP CONSTRAINT pk_VisitID;");
                        }
                    }
                    else if (table == "Document")
                    {
                        if (column == Glo.Tab.DOCUMENT_ID)
                        {
                            reAddKeys = true;
                            commands.Add("ALTER TABLE Document DROP CONSTRAINT pk_DocumentID;");
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
                                commands.Add("ALTER TABLE Connection ADD CONSTRAINT u_ConnectionConfIDDialNo UNIQUE (Conference_ID, Dial_No);");
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
                                commands.Add("CREATE UNIQUE INDEX u_AssetRef ON Asset (Asset_Reference) WHERE Asset_Reference IS NOT NULL;");
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
                                commands.Add("ALTER TABLE Conference ADD CONSTRAINT fk_ConfCreationLogin FOREIGN KEY (Creation_Login_ID) REFERENCES Login (Login_ID) ON DELETE SET NULL;");
                                // fk_ConfEditLogin cascades are handled in triggers trg_deleteConfEditLogin and trg_updateConfEditLogin to avoid cascade cycle warnings.
                                commands.Add("ALTER TABLE OrganisationChange ADD CONSTRAINT fk_OrgChange_LoginID FOREIGN KEY (Login_ID) REFERENCES Login (Login_ID) ON DELETE SET NULL ON UPDATE CASCADE;");
                                commands.Add("ALTER TABLE AssetChange ADD CONSTRAINT fk_AssetChange_LoginID FOREIGN KEY (Login_ID) REFERENCES Login (Login_ID) ON DELETE SET NULL ON UPDATE CASCADE;");
                            }
                            else if (column == Glo.Tab.LOGIN_USERNAME)
                            {
                                commands.Add("ALTER TABLE Login ADD CONSTRAINT u_Username UNIQUE (Username);");
                            }
                        }
                        else if (table == "Task")
                        {
                            if (column == Glo.Tab.TASK_ID)
                                commands.Add("ALTER TABLE Task ADD CONSTRAINT pk_TaskID PRIMARY KEY (Task_ID);");
                            else if (column == Glo.Tab.TASK_REFERENCE)
                            {
                                commands.Add($"ALTER TABLE {table} ALTER COLUMN {column} {columnType} NOT NULL;");
                                commands.Add("CREATE UNIQUE INDEX u_TaskRef ON Task (Task_Reference);");
                                commands.Add("CREATE UNIQUE INDEX u_OrgTaskRef ON Organisation (Task_Reference) WHERE Task_Reference IS NOT NULL;");
                            }
                        }
                        else if (table == "Visit" && column == Glo.Tab.VISIT_ID)
                            commands.Add("ALTER TABLE Visit ADD CONSTRAINT pk_VisitID PRIMARY KEY (Visit_ID);");
                        else if (table == "Document" && column == Glo.Tab.DOCUMENT_ID)
                            commands.Add("ALTER TABLE Document ADD CONSTRAINT pk_DocumentID PRIMARY KEY (Document_ID);");
                    }
                }
                if (allowed.Count > 0)
                {
                    if (!droppedConstraint)
                        commands.Add(DropConstraints(table, column));
                    commands.Add($"EXEC sp_executesql N'ALTER TABLE {table} " +
                                 $"ADD CONSTRAINT chk_{table}{column} " +
                                 $"CHECK ({column} " +
                                 $"IN ('{string.Join("','", allowed)}'));'");
                }
                if ((unique && Glo.Fun.ColumnRemovalAllowed(table, column)))
                {
                    if (!droppedConstraint)
                        commands.Add(DropConstraints(table, column));
                    commands.Add($"EXEC sp_executesql N'" +
                                 $"CREATE UNIQUE INDEX u_{table}{column} ON {table} ({column}) " +
                                 $"WHERE {column} IS NOT NULL;'");
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
        public List<int> taskOrder = new();
        public List<int> visitOrder = new();
        public List<int> documentOrder = new();
        public List<Header> organisationHeaders = new();
        public List<Header> assetHeaders = new();
        public List<Header> contactHeaders = new();
        public List<Header> conferenceHeaders = new();
        public List<Header> taskHeaders = new();
        public List<Header> visitHeaders = new();
        public List<Header> documentHeaders = new();

        public struct Header
        {
            public int position;
            public string name;
            public Header(int position, string name) { this.position = position; this.name = name; }
        }

        public ColumnOrdering(string sessionID, int columnRecordID, List<int> organisationOrder,
                                                                    List<int> assetOrder,
                                                                    List<int> contactOrder,
                                                                    List<int> conferenceOrder,
                                                                    List<int> taskOrder,
                                                                    List<int> visitOrder,
                                                                    List<int> documentOrder)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            this.organisationOrder = organisationOrder;
            this.assetOrder = assetOrder;
            this.contactOrder = contactOrder;
            this.conferenceOrder = conferenceOrder;
            this.taskOrder = taskOrder;
            this.visitOrder = visitOrder;
            this.documentOrder = documentOrder;
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
                                                        Command("Conference", conferenceOrder),
                                                        Command("Task", taskOrder),
                                                        Command("Visit", visitOrder),
                                                        Command("Document", documentOrder)});
        }

        public string HeaderConfigText()
        {
            StringBuilder str = new();
            for (int i = 0; i < 7; ++i)
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
                else if (i == 3)
                {
                    table = "Conference";
                    headers = conferenceHeaders;
                }
                else if (i == 4)
                {
                    table = "Task";
                    headers = taskHeaders;
                }
                else if (i == 5)
                {
                    table = "Visit";
                    headers = visitHeaders;
                }
                else // if 6
                {
                    table = "Document";
                    headers = documentHeaders;
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
        public string? taskRef;
        public string? notes;
        public bool organisationRefChanged;
        public bool parentOrgRefChanged;
        public bool nameChanged;
        public bool dialNoChanged;
        public bool availableChanged;
        public bool taskChanged;
        public bool notesChanged;
        public List<string> additionalCols;
        public List<string?> additionalVals;
        public List<bool> additionalNeedsQuotes;
        public string changeReason;

        // Bool to disable encasing the commands in transactions. The task breakout method in Agent
        // needs this - it should not be used in any other case.
        public bool overrideTransaction = false;

        public Organisation(string sessionID, int columnRecordID,
                            int organisationID, string? organisationRef, string? parentOrgRef, string? name,
                            string? dialNo, bool? available, string? taskRef,
                            string? notes, List<string> additionalCols,
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
            this.taskRef = taskRef;
            this.notes = notes;
            this.additionalCols = additionalCols;
            this.additionalVals = additionalVals;
            this.additionalNeedsQuotes = additionalNeedsQuotes;
            organisationRefChanged = false;
            parentOrgRefChanged = false;
            nameChanged = false;
            dialNoChanged = false;
            availableChanged = false;
            taskChanged = false;
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
            if (taskRef != null)
                taskRef = SqlAssist.AddQuotes(SqlAssist.SecureValue(taskRef));
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
                                                                                  Glo.Tab.TASK_REFERENCE,
                                                                                  Glo.Tab.NOTES),
                                              SqlAssist.ValConcat(additionalVals, organisationRef,
                                                                                  parentOrgRef,
                                                                                  name,
                                                                                  dialNo,
                                                                                  available == true ? "1" : "0",
                                                                                  taskRef,
                                                                                  notes));
            // Create a first change instance.
            additionalCols.RemoveRange(additionalCols.Count - 7, 7); // ColConcat and ValConcat added the main fields
            additionalVals.RemoveRange(additionalVals.Count - 7, 7); // to the Lists, so walk that back here.
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
                                                            Glo.Tab.TASK_REFERENCE,
                                                            Glo.Tab.TASK_REFERENCE + Glo.Tab.CHANGE_SUFFIX,
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
                                                            taskRef, "1",
                                                            notes, "1"));
            if (overrideTransaction)
                return com;
            else
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
                setters.Add(Glo.Tab.ORGANISATION_AVAILABLE + " = " + (available == true ? "1" : "0"));
            if (taskChanged)
                setters.Add(SqlAssist.Setter(Glo.Tab.TASK_REFERENCE, taskRef));
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
            if (taskChanged)
            {
                additionalCols.Add(Glo.Tab.TASK_REFERENCE);
                additionalCols.Add(Glo.Tab.TASK_REFERENCE + Glo.Tab.CHANGE_SUFFIX);
                additionalVals.Add(taskRef);
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

            if (overrideTransaction)
                return string.Join(' ', commands);
            else
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
            //if (assetRef == null || assetRef == "" ||
            //    assetRef == "''" || assetRef == "NULL")
            //    return "";

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
            //if (assetRefChanged)
            //    if (assetRef == null || assetRef == "" ||
            //        assetRef == "''" || assetRef == "NULL")
            //        return "";

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

    public struct Conference
    {
        public struct Connection
        {
            public int? connectionID;
            public int? conferenceID;
            public string dialNo;
            public bool isManaged;
            public DateTime? connected;
            public DateTime? disconnected;
            public int row;
            public bool isTest;

            // Just for select requests to speed things up:
            public int? orgId;
            public string? orgReference;
            public string? orgName;

            public Connection(int? conferenceID, string dialNo, bool isManaged,
                              DateTime? connected, DateTime? disconnected, int row, bool isTest)
            {
                connectionID = null;
                this.conferenceID = conferenceID;
                this.dialNo = dialNo;
                this.isManaged = isManaged;
                this.connected = connected;
                this.disconnected = disconnected;
                this.row = row;
                this.isTest = isTest;
                orgId = null;
                orgReference = null;
                orgName = null;
            }

            public void Prepare()
            {
                if (dialNo != null)
                    dialNo = SqlAssist.AddQuotes(SqlAssist.SecureValue(dialNo));
            }
        }

        public string sessionID;
        public int columnRecordID;
        public int? conferenceID;
        public int resourceID;
        public string? resourceName;
        public int resourceRow;
        public int? recurrenceID;
        public string? recurrenceName;
        public string? title;
        public DateTime? start;
        public DateTime? end;
        public string? closure;
        public bool? cancelled;
        public int? createLoginID;
        public DateTime? createTime;
        public int? editLoginID;
        public DateTime? editTime;
        public string? notes;
        public List<string> additionalCols;
        public List<string?> additionalVals;
        public List<bool> additionalNeedsQuotes;
        public List<Connection> connections;

        // These are for use in select requests only to speed things up:
        public List<string?> additionalValTypes;
        public List<object?> additionalValObjects;
        public string? createdUsername;
        public string? editedUsername;

        public Conference(string sessionID, int columnRecordID,
                          int resourceID, int resourceRow, string? title,
                          DateTime start, DateTime end,
                          int createLoginID,
                          string? notes, List<string> additionalCols,
                                         List<string?> additionalVals,
                                         List<bool> additionalNeedsQuotes,
                          List<Connection> connections)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            conferenceID = null;
            this.resourceID = resourceID;
            resourceName = null;
            this.resourceRow = resourceRow;
            recurrenceID = null;
            recurrenceName = null;
            this.title = title;
            this.start = start;
            this.end = end;
            closure = null;
            cancelled = null;
            this.createLoginID = createLoginID;
            createTime = null;
            editLoginID = null;
            editTime = null;
            this.notes = notes;
            this.additionalCols = additionalCols;
            this.additionalVals = additionalVals;
            additionalValObjects = new();
            this.additionalNeedsQuotes = additionalNeedsQuotes;
            this.connections = connections;

            additionalValTypes = new();
            additionalValObjects = new();
            createdUsername = null;
            editedUsername = null;

            SqlAssist.LevelAdditionals(ref additionalCols, ref additionalVals);
        }

        private void Prepare()
        {
            // Make sure the columns and values are safe, then add quotes where needed.
            if (title != null)
                title = SqlAssist.AddQuotes(SqlAssist.SecureValue(title));
            if (closure != null)
                closure = SqlAssist.AddQuotes(SqlAssist.SecureValue(closure));
            if (notes != null)
                notes = SqlAssist.AddQuotes(SqlAssist.SecureValue(notes));
            SqlAssist.SecureColumn(additionalCols);
            SqlAssist.SecureValue(additionalVals);
            SqlAssist.AddQuotes(additionalVals, additionalNeedsQuotes);
        }

        public string SqlInsert() { return SqlUpdate(true); }
        public string SqlInsert(bool asTransaction)
        {
            Prepare();

            List<string> commands = new()
            {
                "INSERT INTO Conference (" + SqlAssist.ColConcat(additionalCols, Glo.Tab.RESOURCE_ID,
                                                                 Glo.Tab.CONFERENCE_RESOURCE_ROW,
                                                                 Glo.Tab.RECURRENCE_ID,
                                                                 Glo.Tab.CONFERENCE_TITLE,
                                                                 Glo.Tab.CONFERENCE_START,
                                                                 Glo.Tab.CONFERENCE_END,
                                                                 Glo.Tab.CONFERENCE_CREATION_LOGIN,
                                                                 Glo.Tab.CONFERENCE_CREATION_TIME,
                                                                 Glo.Tab.CONFERENCE_CLOSURE,
                                                                 Glo.Tab.CONFERENCE_CANCELLED,
                                                                 "Notes") + ") " +
                $"OUTPUT INSERTED.{Glo.Tab.CONFERENCE_ID} INTO #IDs VALUES (" +
                SqlAssist.ValConcat(additionalVals, resourceID.ToString(),
                                    resourceRow.ToString(),
                                    recurrenceID == null ? "NULL" : recurrenceID.ToString(),
                                    title,
                                    SqlAssist.DateTimeToSQL((DateTime)start!,
                                                            false, true),
                                    SqlAssist.DateTimeToSQL((DateTime)end!,
                                                            false, true),
                                    createLoginID.ToString(),
                                    SqlAssist.DateTimeToSQL(DateTime.Now,
                                                            false, true),
                                    closure,
                                    cancelled == true ? "1" : "0",
                                    notes == null || notes == "" ? "NULL" : notes) + ");"
            };

            if (connections.Count > 0)
            {
                commands.Add("SET @NewID = SCOPE_IDENTITY();");

                foreach (Connection c in connections)
                {
                    c.Prepare();
                    commands.Add(SqlAssist.InsertInto("Connection",
                                          SqlAssist.ColConcat(Glo.Tab.CONFERENCE_ID,
                                                              Glo.Tab.DIAL_NO,
                                                              Glo.Tab.CONNECTION_IS_MANAGED,
                                                              Glo.Tab.CONNECTION_TIME_FROM,
                                                              Glo.Tab.CONNECTION_TIME_TO,
                                                              Glo.Tab.CONNECTION_ROW,
                                                              Glo.Tab.CONNECTION_IS_TEST),
                                          SqlAssist.ValConcat(c.conferenceID == null ? "@NewID" :
                                                                                       c.conferenceID.ToString(),
                                                              c.dialNo,
                                                              c.isManaged ? "1" : "0",
                                                              c.connected == null ? "NULL" :
                                                              SqlAssist.DateTimeToSQL((DateTime)c.connected,
                                                                                      false, true),
                                                              c.disconnected == null ? "NULL" :
                                                              SqlAssist.DateTimeToSQL((DateTime)c.disconnected,
                                                                                      false, true),
                                                              c.row.ToString(),
                                                              c.isTest ? "1" : "0")));
                }
            }

            if (asTransaction)
                return SqlAssist.Transaction(commands.ToArray());
            else
                return string.Join(" ", commands);
        }

        public string SqlUpdate() { return SqlUpdate(true); }
        public string SqlUpdate(bool asTransaction)
        {
            Prepare();

            List<string> setters = new()
            {
                SqlAssist.Setter(Glo.Tab.RESOURCE_ID, resourceID.ToString()),
                SqlAssist.Setter(Glo.Tab.CONFERENCE_RESOURCE_ROW, resourceRow.ToString()),
                SqlAssist.Setter(Glo.Tab.CONFERENCE_TITLE, title),
                SqlAssist.Setter(Glo.Tab.CONFERENCE_START, SqlAssist.DateTimeToSQL((DateTime)start!, false, true)),
                SqlAssist.Setter(Glo.Tab.CONFERENCE_END, SqlAssist.DateTimeToSQL((DateTime)end!, false, true)),
                SqlAssist.Setter(Glo.Tab.CONFERENCE_EDIT_LOGIN, editLoginID.ToString()),
                SqlAssist.Setter(Glo.Tab.CONFERENCE_EDIT_TIME, SqlAssist.DateTimeToSQL(DateTime.Now, false, true)),
                SqlAssist.Setter(Glo.Tab.CONFERENCE_CLOSURE, closure),
                SqlAssist.Setter(Glo.Tab.CONFERENCE_CANCELLED, cancelled == true ? "1" : "0"),
                SqlAssist.Setter(Glo.Tab.NOTES, notes == null ? "NULL" : notes)
            };

            for (int i = 0; i < additionalCols.Count; ++i)
                setters.Add(SqlAssist.Setter(additionalCols[i], additionalVals[i]));

            List<string> commands = new()
            {
                SqlAssist.Update("Conference", string.Join(", ", setters),
                                 Glo.Tab.CONFERENCE_ID, conferenceID.ToString()!)
            };

            // Delete any connections that are no longer included.
            List<string> connectionIDs = new();
            foreach (Connection c in connections)
                if (c.connectionID != null)
                    connectionIDs.Add(c.connectionID.ToString()!);
            if (connectionIDs.Count > 0)
                commands.Add("DELETE FROM Connection " +
                            $"WHERE {Glo.Tab.CONFERENCE_ID} = {conferenceID.ToString()!} " +
                              $"AND {Glo.Tab.CONNECTION_ID} NOT IN ({string.Join(", ", connectionIDs)});");
            else
                commands.Add($"DELETE FROM Connection WHERE {Glo.Tab.CONFERENCE_ID} = {conferenceID.ToString()!};");

            if (connections.Count > 0)
            {
                commands.Add("DECLARE @NewID INT; SET @NewID = SCOPE_IDENTITY();");

                foreach (Connection c in connections)
                {
                    c.Prepare();

                    // Create the update command if we believe the connection ID already exists.
                    if (c.connectionID != null)
                    {
                        setters = new()
                        {
                            // Conference ID not needed as it can never change.
                            SqlAssist.Setter(Glo.Tab.DIAL_NO, c.dialNo),
                            SqlAssist.Setter(Glo.Tab.CONNECTION_IS_MANAGED, c.isManaged == true ? "1" : "0"),
                            SqlAssist.Setter(Glo.Tab.CONNECTION_TIME_FROM, c.connected == null ? "NULL" :
                                                                        SqlAssist.DateTimeToSQL((DateTime)c.connected,
                                                                        false, true)),
                            SqlAssist.Setter(Glo.Tab.CONNECTION_TIME_TO, c.disconnected == null ? "NULL" :
                                                                      SqlAssist.DateTimeToSQL((DateTime)c.disconnected,
                                                                      false, true)),
                            SqlAssist.Setter(Glo.Tab.CONNECTION_ROW, c.row.ToString()),
                            SqlAssist.Setter(Glo.Tab.CONNECTION_IS_TEST, c.isTest ? "1" : "0")
                        };

                        commands.Add(SqlAssist.Update("Connection", string.Join(", ", setters),
                                     Glo.Tab.CONNECTION_ID, c.connectionID.ToString()!));
                    }

                    // Even if the connection ID already exists, we'll add an insert just in case no rows were
                    // affected.
                    commands.Add(SqlAssist.InsertInto("Connection",
                                          SqlAssist.ColConcat(Glo.Tab.CONFERENCE_ID,
                                                              Glo.Tab.DIAL_NO,
                                                              Glo.Tab.CONNECTION_IS_MANAGED,
                                                              Glo.Tab.CONNECTION_TIME_FROM,
                                                              Glo.Tab.CONNECTION_TIME_TO,
                                                              Glo.Tab.CONNECTION_ROW,
                                                              Glo.Tab.CONNECTION_IS_TEST),
                                          SqlAssist.ValConcat(c.conferenceID == null ? "@NewID" :
                                                                                       c.conferenceID.ToString(),
                                                              c.dialNo,
                                                              c.isManaged ? "1" : "0",
                                                              c.connected == null ? "NULL" :
                                                              SqlAssist.DateTimeToSQL((DateTime)c.connected,
                                                                                      false, true),
                                                              c.disconnected == null ? "NULL" :
                                                              SqlAssist.DateTimeToSQL((DateTime)c.disconnected,
                                                                                      false, true),
                                                              c.row.ToString(),
                                                              c.isTest ? "1" : "0")));

                    // If an update was tried before, only attempt the insert if the update failed.
                    if (c.connectionID != null)
                        commands[commands.Count - 1] = "IF @@ROWCOUNT = 0 BEGIN " + commands[commands.Count - 1] + " END";
                }
            }

            if (asTransaction)
                return SqlAssist.Transaction(commands.ToArray());
            else
                return string.Join(" ", commands);
        }

        public static string SqlCheckForRowClashes(List<int>? confIDs, SqlConnection sqlConnect, bool autoResolve)
        {
            StringBuilder idIn = new();
            // Build a list of IDs.
            if (confIDs == null)
                idIn.Append("SELECT ID FROM #IDs");
            else
            {
                for (int i = 0; i < confIDs.Count; ++i)
                    idIn.Append(confIDs[i].ToString() + ", ");
                if (idIn.Length > 2)
                    idIn.Remove(idIn.Length - 2, 2);
            }

            StringBuilder str = new();
            str.Append("WITH NewConfs AS (" +
                      $"SELECT {Glo.Tab.CONFERENCE_ID}, " +
                             $"{Glo.Tab.RESOURCE_ID}, {Glo.Tab.CONFERENCE_RESOURCE_ROW}, " +
                             $"{Glo.Tab.CONFERENCE_START}, {Glo.Tab.CONFERENCE_END} " +
                      $"FROM Conference WHERE {Glo.Tab.CONFERENCE_ID} IN ({idIn.ToString()}) " +
                      $") ");
            str.Append($"SELECT DISTINCT nc.{Glo.Tab.CONFERENCE_ID} FROM NewConfs nc " +
                       $"JOIN Conference c " +
                           $"ON c.{Glo.Tab.RESOURCE_ID} = nc.{Glo.Tab.RESOURCE_ID} " +
                           $"AND c.{Glo.Tab.CONFERENCE_RESOURCE_ROW} = nc.{Glo.Tab.CONFERENCE_RESOURCE_ROW} " +
                           $"AND c.{Glo.Tab.CONFERENCE_END} > nc.{Glo.Tab.CONFERENCE_START} " +
                           $"AND c.{Glo.Tab.CONFERENCE_START} < nc.{Glo.Tab.CONFERENCE_END} " +
                           $"AND c.{Glo.Tab.CONFERENCE_ID} != nc.{Glo.Tab.CONFERENCE_ID}; ");
            str.Append($"IF @@ROWCOUNT > 0\n");

            if (!autoResolve)
                str.Append($"BEGIN THROW 50000, '{Glo.ROW_CLASH_WARNING}', 1;\nEND");
            else
            {
                // Out of time, so modifying Copilot's output for more complex SQL at this point. All is checked
                // and thoroughly tested before committing.
                str.Append($@"
BEGIN
DECLARE @CurrentRowUp INT;
DECLARE @CurrentRowDown INT;
DECLARE @ConflictCount INT;
DECLARE @ConferenceID INT;
DECLARE @ResourceID INT;
DECLARE @StartTime DATETIME;
DECLARE @EndTime DATETIME;
DECLARE @MaxRow INT;
DECLARE @HomeRow INT;

-- Cursor to iterate over selected conferences
DECLARE conference_cursor CURSOR FOR
SELECT {Glo.Tab.CONFERENCE_ID}, {Glo.Tab.RESOURCE_ID}, {Glo.Tab.CONFERENCE_RESOURCE_ROW},
       {Glo.Tab.CONFERENCE_START}, {Glo.Tab.CONFERENCE_END}
FROM Conference
WHERE {Glo.Tab.CONFERENCE_ID} IN ({idIn.ToString()})
ORDER BY {Glo.Tab.CONFERENCE_START};

OPEN conference_cursor;
FETCH NEXT FROM conference_cursor INTO @ConferenceID, @ResourceID, @HomeRow, @StartTime, @EndTime;

WHILE @@FETCH_STATUS = 0
BEGIN
    -- Calculate the maximum row for the current resource
    SELECT @MaxRow = {Glo.Tab.RESOURCE_CAPACITY_CONFERENCE} + {Glo.Tab.RESOURCE_ROWS_ADDITIONAL}
    FROM Resource
    WHERE {Glo.Tab.RESOURCE_ID} = @ResourceID;

    SET @CurrentRowUp = @HomeRow;
    SET @CurrentRowDown = @HomeRow + 1;
    

    WHILE @CurrentRowDown < @MaxRow OR @CurrentRowUp >= 0
    BEGIN
        IF @CurrentRowUp >= 0
        BEGIN
            -- Same again, going up.
            SELECT @ConflictCount = COUNT(*)
            FROM Conference
            WHERE {Glo.Tab.RESOURCE_ID} = @ResourceID
              AND {Glo.Tab.CONFERENCE_RESOURCE_ROW} = @CurrentRowUp
              AND {Glo.Tab.CONFERENCE_END} > @StartTime
              AND {Glo.Tab.CONFERENCE_START} < @EndTime
              AND {Glo.Tab.CONFERENCE_ID} != @ConferenceID;

            IF @ConflictCount = 0
            BEGIN
                -- No conflict, update the conference to the current row
                UPDATE Conference
                SET {Glo.Tab.CONFERENCE_RESOURCE_ROW} = @CurrentRowUp
                WHERE {Glo.Tab.CONFERENCE_ID} = @ConferenceID;
                BREAK;
            END
        END

        IF @CurrentRowDown < @MaxRow
        BEGIN
            -- Check for conflicts on the current row down
            SELECT @ConflictCount = COUNT(*)
            FROM Conference
            WHERE {Glo.Tab.RESOURCE_ID} = @ResourceID
              AND {Glo.Tab.CONFERENCE_RESOURCE_ROW} = @CurrentRowDown
              AND {Glo.Tab.CONFERENCE_END} > @StartTime
              AND {Glo.Tab.CONFERENCE_START} < @EndTime
              AND {Glo.Tab.CONFERENCE_ID} != @ConferenceID;

            IF @ConflictCount = 0
            BEGIN
                -- No conflict, update the conference to the current row
                UPDATE Conference
                SET {Glo.Tab.CONFERENCE_RESOURCE_ROW} = @CurrentRowDown
                WHERE {Glo.Tab.CONFERENCE_ID} = @ConferenceID;
                BREAK;
            END
        END

        SET @CurrentRowUp = @CurrentRowUp - 1;
        SET @CurrentRowDown = @CurrentRowDown + 1;
    END

    IF @CurrentRowDown >= @MaxRow AND @CurrentRowUp < 0
    BEGIN
        -- If we exceed the maximum row, throw an exception
        THROW 50000, '{Glo.ROW_CLASH_FAILED_RESOLVE}', 1;
    END

    FETCH NEXT FROM conference_cursor INTO @ConferenceID, @ResourceID, @HomeRow, @StartTime, @EndTime;
END

CLOSE conference_cursor;
DEALLOCATE conference_cursor;

END");
            }
            return str.ToString();
        }
        public static string SqlCheckForDialNoClashes(List<int>? confIDs, SqlConnection sqlConnect)
        {
            StringBuilder idIn = new();
            // Build a list of IDs.
            if (confIDs == null)
                idIn.Append("SELECT ID FROM #IDs");
            else
            {
                for (int i = 0; i < confIDs.Count; ++i)
                    idIn.Append(confIDs[i].ToString() + ", ");
                if (idIn.Length > 2)
                    idIn.Remove(idIn.Length - 2, 2);
            }

            StringBuilder str = new();
            str.Append("WITH NewConnections AS (" +
                      $"SELECT f.{Glo.Tab.CONFERENCE_ID}, f.{Glo.Tab.CONFERENCE_TITLE}, n.{Glo.Tab.DIAL_NO}, " +
                             $"f.{Glo.Tab.CONFERENCE_START}, f.{Glo.Tab.CONFERENCE_END} " +
                       "FROM Connection n " +
                      $"INNER JOIN Conference f ON f.{Glo.Tab.CONFERENCE_ID} = n.{Glo.Tab.CONFERENCE_ID} " +
                                             $"AND f.{Glo.Tab.CONFERENCE_ID} IN ({idIn.ToString()}) " +
                                             $"AND f.{Glo.Tab.CONFERENCE_CANCELLED} = 0 " +
                      $") ");
            str.Append($"SELECT nc.{Glo.Tab.CONFERENCE_ID}, " +
                              $"nc.{Glo.Tab.CONFERENCE_TITLE}, " +
                              $"nc.{Glo.Tab.CONFERENCE_START}, " +
                              $"nc.{Glo.Tab.CONFERENCE_END}, " +
                              $"nc.{Glo.Tab.DIAL_NO}, " +
                              $"f.{Glo.Tab.CONFERENCE_ID}, " +
                              $"f.{Glo.Tab.CONFERENCE_TITLE}, " +
                              $"f.{Glo.Tab.CONFERENCE_START}, " +
                              $"f.{Glo.Tab.CONFERENCE_END} " +
                       $"FROM NewConnections nc " +
                       $"JOIN Connection n ON n.{Glo.Tab.DIAL_NO} = nc.{Glo.Tab.DIAL_NO} " +
                       $"JOIN Conference f ON f.{Glo.Tab.CONFERENCE_ID} = n.{Glo.Tab.CONFERENCE_ID} " +
                                        $"AND f.{Glo.Tab.CONFERENCE_CANCELLED} = 0 " +
                                        $"AND f.{Glo.Tab.CONFERENCE_END} > nc.{Glo.Tab.CONFERENCE_START} " +
                                        $"AND f.{Glo.Tab.CONFERENCE_START} < nc.{Glo.Tab.CONFERENCE_END} " +
                                        $"AND f.{Glo.Tab.CONFERENCE_ID} != nc.{Glo.Tab.CONFERENCE_ID}; ");

            str.Append("IF @@ROWCOUNT > 0 BEGIN " +
                       $"THROW 50000, '{Glo.DIAL_CLASH_WARNING}', 1; END");

            return str.ToString();
        }
        public static string SqlCheckForResourceOverflows(List<int>? confIDs,
                                                          SqlConnection sqlConnect)
        {
            StringBuilder idIn = new();
            // Build a list of IDs.
            if (confIDs == null)
                idIn.Append("SELECT ID FROM #IDs");
            else
            {
                for (int i = 0; i < confIDs.Count; ++i)
                    idIn.Append(confIDs[i].ToString() + ", ");
                if (idIn.Length > 2)
                    idIn.Remove(idIn.Length - 2, 2);
            }

            // Run a command to:
            // 1) Pull a list of conferences including dial no counts.
            // 2) Create a series of points, each adding the conferences's dial numbers at its start point start, then
            //    subtracting them at the ends.
            // 3) Create a list of rows that each report their current conference and connection load at the given
            //    points, partitioned by resource ID.
            // 4) Narrow down the list to only contain capacity overflows.
            // 5) Report all needed information for SqlDataReader's consumption, then throw if there were errors.
            return $@"
WITH ConferenceTimes AS (
    SELECT DISTINCT f.{Glo.Tab.CONFERENCE_ID}
    FROM Conference c
    JOIN Conference f ON c.{Glo.Tab.CONFERENCE_ID} IN ({idIn})
                     AND (f.{Glo.Tab.CONFERENCE_END} >= c.{Glo.Tab.CONFERENCE_START}
                     AND f.{Glo.Tab.CONFERENCE_START} <= c.{Glo.Tab.CONFERENCE_END})
),
DialCounts AS (
    SELECT f.{Glo.Tab.CONFERENCE_START},
           f.{Glo.Tab.CONFERENCE_END},
           f.{Glo.Tab.RESOURCE_ID},
           COUNT({Glo.Tab.CONNECTION_ID}) AS DialCount
    FROM Conference f
    LEFT JOIN Connection n ON f.{Glo.Tab.CONFERENCE_ID} = n.{Glo.Tab.CONFERENCE_ID}
    JOIN ConferenceTimes ct ON f.{Glo.Tab.CONFERENCE_ID} = ct.{Glo.Tab.CONFERENCE_ID}
    WHERE f.{Glo.Tab.CONFERENCE_CANCELLED} = 0
    GROUP BY f.{Glo.Tab.CONFERENCE_ID}, f.{Glo.Tab.CONFERENCE_START}, f.{Glo.Tab.CONFERENCE_END}, f.{Glo.Tab.RESOURCE_ID}
),
TimeWindows AS (
    SELECT {Glo.Tab.CONFERENCE_START} AS TimePoint,
           {Glo.Tab.RESOURCE_ID},
		   DialCount,
		   1 AS ConferenceCount
    FROM DialCounts
    UNION ALL
    SELECT {Glo.Tab.CONFERENCE_END} AS TimePoint,
           {Glo.Tab.RESOURCE_ID},
		   -DialCount,
		   -1 AS ConferenceCount
    FROM DialCounts
),
CumulativeLoad AS (
SELECT DISTINCT TimePoint,
       {Glo.Tab.RESOURCE_ID},
	   SUM(DialCount) OVER (PARTITION BY {Glo.Tab.RESOURCE_ID} ORDER BY TimePoint) AS CumulativeDialCount,
	   SUM(ConferenceCount) OVER (PARTITION BY {Glo.Tab.RESOURCE_ID} ORDER BY TimePoint) AS CumulativeConferenceCount
FROM TimeWindows
),
CumulativeLoadPoints AS (
SELECT TimePoint, cl.{Glo.Tab.RESOURCE_ID},
                  r.{Glo.Tab.RESOURCE_NAME},
	   CASE WHEN cl.CumulativeDialCount > r.{Glo.Tab.RESOURCE_CAPACITY_CONNECTION}
            THEN cl.CumulativeDialCount ELSE NULL END AS CumulativeDialCount,
	   CASE WHEN cl.CumulativeConferenceCount > r.{Glo.Tab.RESOURCE_CAPACITY_CONFERENCE}
            THEN cl.CumulativeConferenceCount ELSE NULL END AS CumulativeConferenceCount
FROM CumulativeLoad cl
JOIN Resource r ON r.{Glo.Tab.RESOURCE_ID} = cl.{Glo.Tab.RESOURCE_ID}
			   AND (cl.CumulativeDialCount > r.{Glo.Tab.RESOURCE_CAPACITY_CONNECTION}
			   OR cl.CumulativeConferenceCount > r.{Glo.Tab.RESOURCE_CAPACITY_CONFERENCE})

)
SELECT DISTINCT f.{Glo.Tab.CONFERENCE_ID}, f.{Glo.Tab.CONFERENCE_TITLE}, lp.{Glo.Tab.RESOURCE_NAME},
                lp.TimePoint, lp.CumulativeDialCount,lp.CumulativeConferenceCount
FROM Conference f
JOIN CumulativeLoadPoints lp ON lp.TimePoint >= f.{Glo.Tab.CONFERENCE_START} 
						    AND lp.TimePoint <= f.{Glo.Tab.CONFERENCE_END}
							AND lp.{Glo.Tab.RESOURCE_ID} = f.{Glo.Tab.RESOURCE_ID}
WHERE f.{Glo.Tab.CONFERENCE_ID} IN ({idIn.ToString()}) AND f.{Glo.Tab.CONFERENCE_CANCELLED} = 0;

IF @@ROWCOUNT > 0
BEGIN
    THROW 50000, '{Glo.RESOURCE_OVERFLOW_WARNING}', 1;
END
";
        }

        public Conference CloneForNewInsert(string sessionID, int columnRecordID)
        {
            Conference clone = new();
            // Add the missing assignments to the clone object
            clone.sessionID = sessionID;
            clone.columnRecordID = columnRecordID;
            clone.conferenceID = conferenceID;
            clone.resourceID = resourceID;
            clone.resourceRow = resourceRow;
            clone.recurrenceID = recurrenceID;
            clone.recurrenceName = recurrenceName;
            clone.title = title;
            clone.start = start;
            clone.end = end;
            clone.closure = closure;
            clone.cancelled = cancelled;
            clone.createLoginID = createLoginID;
            clone.createTime = createTime;
            clone.editLoginID = editLoginID;
            clone.editTime = editTime;
            clone.notes = notes;

            clone.additionalCols = new();
            foreach (string s in additionalCols)
                clone.additionalCols.Add(s);
            clone.additionalVals = new();
            foreach (string? s in additionalVals)
                clone.additionalVals.Add(s);
            clone.additionalNeedsQuotes = new();
            foreach (bool b in additionalNeedsQuotes)
                clone.additionalNeedsQuotes.Add(b);
            clone.connections = new(); ;
            foreach (Connection c in connections)
                clone.connections.Add(new(null, c.dialNo, c.isManaged, null, null, c.row, c.isTest));

            return clone;
        }
    }

    public struct ConferenceAdjustment
    {
        public string sessionID;
        public int columnRecordID;
        public List<int> ids;

        public int? editLoginID;
        public DateTime? editTime;

        public bool resolveRowClashes;
        public bool overrideDialNoClashes;
        public bool overrideResourceOverflows;

        public TimeSpan? startTime;
        public TimeSpan? move;
        public TimeSpan? endTime;
        public TimeSpan? length;

        public class Connection
        {
            public string dialNo;
            public bool isManaged;
            public bool isTest;
            public Connection(string dialNo, bool isManaged, bool isTest)
            { this.dialNo = dialNo; this.isManaged = isManaged; this.isTest = isTest; }
        }

        public List<Connection>? additions;
        public List<Connection>? removals;
        public string? dialHost;

        public enum Intent { Times, Connections, Host }
        public Intent intent;

        private void Prepare()
        {
            if (additions != null)
                foreach (Connection c in additions)
                    c.dialNo = SqlAssist.SecureValue(c.dialNo);
            if (removals != null)
                foreach (Connection c in removals)
                    c.dialNo = SqlAssist.SecureValue(c.dialNo);

            if (dialHost != null)
                dialHost = SqlAssist.SecureValue(dialHost);
        }

        public string SqlUdpate()
        {
            if (ids.Count == 0)
                return "";

            Prepare();

            List<string> idStr = ids.Select(i => i.ToString()).ToList();
            string idCat = string.Join(", ", idStr);
            List<string> commands = new();

            if (intent == Intent.Times)
            {
                // Conferences
                string com = "";

                List<string> timeSets = new();
                com = "UPDATE Conference SET ";

                if (startTime != null)
                    timeSets.Add($"{Glo.Tab.CONFERENCE_START} = " +
                                 $"CAST(CAST({Glo.Tab.CONFERENCE_START} AS DATE) AS DATETIME) + " +
                                 $"CAST('{SqlAssist.TimeSpanToSQL((TimeSpan)startTime)}' AS DATETIME)");
                else if (move != null)
                    timeSets.Add($"{Glo.Tab.CONFERENCE_START} = " +
                                 $"DATEADD(MINUTE, {((TimeSpan)move).TotalMinutes}, {Glo.Tab.CONFERENCE_START})");

                if (endTime != null)
                    timeSets.Add($"{Glo.Tab.CONFERENCE_END} = " +
                                 $"CAST(CAST({Glo.Tab.CONFERENCE_END} AS DATE) AS DATETIME) + " +
                                 $"CAST('{SqlAssist.TimeSpanToSQL((TimeSpan)endTime)}' AS DATETIME)");
                else if (length != null)
                {
                    if (startTime == null && move == null)
                    {
                        timeSets.Add($"{Glo.Tab.CONFERENCE_END} = " +
                                     $"DATEADD(MINUTE, {((TimeSpan)length).TotalMinutes}, " +
                                     $"{Glo.Tab.CONFERENCE_START})");
                    }
                    if (startTime != null)
                    {
                        timeSets.Add($"{Glo.Tab.CONFERENCE_END} = " +
                                     $"DATEADD(MINUTE, {((TimeSpan)length).TotalMinutes}, " +
                                     $"CAST(CAST({Glo.Tab.CONFERENCE_START} AS DATE) AS DATETIME) + " +
                                     $"CAST('{SqlAssist.TimeSpanToSQL((TimeSpan)startTime)}' AS DATETIME))");
                    }
                    else if (move != null)
                    {
                        timeSets.Add($"{Glo.Tab.CONFERENCE_END} = " +
                                     $"DATEADD(MINUTE, {((TimeSpan)length).TotalMinutes}, " +
                                     $"DATEADD(MINUTE, {((TimeSpan)move).TotalMinutes}, {Glo.Tab.CONFERENCE_START}))");
                    }
                }
                else if (move != null)
                {
                    timeSets.Add($"{Glo.Tab.CONFERENCE_END} = " +
                                 $"DATEADD(MINUTE, {((TimeSpan)move).TotalMinutes}, {Glo.Tab.CONFERENCE_END})");
                }
                timeSets.Add($"{Glo.Tab.CONFERENCE_EDIT_LOGIN} = {editLoginID}");
                timeSets.Add($"{Glo.Tab.CONFERENCE_EDIT_TIME} = '{SqlAssist.DateTimeToSQL((DateTime)editTime!)}'");

                if (timeSets.Count > 0)
                    com += $"{string.Join(", ", timeSets)} " +
                           $"WHERE {Glo.Tab.CONFERENCE_ID} IN ({idCat});";

                commands.Add(com);

                // Connections
                if (move != null && move.Value.Days != 0)
                {
                    timeSets = new();
                    com = "UPDATE Connection SET ";

                    timeSets.Add($"{Glo.Tab.CONNECTION_TIME_FROM} = " +
                                 $"DATEADD(DAY, {((TimeSpan)move).Days}, {Glo.Tab.CONNECTION_TIME_FROM})");
                    timeSets.Add($"{Glo.Tab.CONNECTION_TIME_TO} = " +
                                 $"DATEADD(DAY, {((TimeSpan)move).Days}, {Glo.Tab.CONNECTION_TIME_TO})");

                    if (timeSets.Count > 0)
                        com += $"{string.Join(", ", timeSets)} " +
                               $"WHERE {Glo.Tab.CONFERENCE_ID} IN ({idCat});";

                    commands.Add(com);
                }
            }

            else if (intent == Intent.Connections)
            {
                // Removals need to be carried out before, otherwise additions could potentially violate constraints.
                if (removals != null && removals.Count > 0)
                {
                    // This isn't the most efficient way I expect, but hey, we'll never have more than 255! And
                    // in practice, probably never more than three or four.
                    foreach (Connection c in removals)
                        commands.Add(@$"
DELETE FROM Connection
WHERE {Glo.Tab.CONFERENCE_ID} IN ({idCat})
AND {Glo.Tab.DIAL_NO} = '{c.dialNo}'
AND {Glo.Tab.CONNECTION_IS_MANAGED} = {(c.isManaged ? "1" : "0")}
AND {Glo.Tab.CONNECTION_IS_TEST} = {(c.isTest ? "1" : "0")};
");
                }

                // Carry out additions.
                if (additions != null && additions.Count > 0)
                {
                    List<string> inV = new();
                    foreach (string s in idStr)
                    {
                        int row = 255 - additions.Count; // 255 is the most sites a conference can contain.
                        foreach (Connection c in additions)
                        {
                            inV.Add($"{s}, {c.dialNo}, {(c.isManaged ? "1" : "0")}, {(c.isTest ? "1" : "0")}, {row}");
                            ++row;
                        }
                    }

                    // Insert each new row, skipping any where the conference already has that dial no.
                    commands.Add($@"
MERGE INTO Connection AS target
USING (VALUES ({string.Join("), (", inV)}))
    AS source ({Glo.Tab.CONFERENCE_ID}, {Glo.Tab.DIAL_NO},
               {Glo.Tab.CONNECTION_IS_MANAGED}, {Glo.Tab.CONNECTION_IS_TEST}, {Glo.Tab.CONNECTION_ROW})
ON target.{Glo.Tab.CONFERENCE_ID} = source.{Glo.Tab.CONFERENCE_ID} 
AND target.{Glo.Tab.DIAL_NO} = source.{Glo.Tab.DIAL_NO}
WHEN NOT MATCHED BY TARGET THEN
INSERT ({Glo.Tab.CONFERENCE_ID}, {Glo.Tab.DIAL_NO},
           {Glo.Tab.CONNECTION_IS_MANAGED}, {Glo.Tab.CONNECTION_IS_TEST}, {Glo.Tab.CONNECTION_ROW})
VALUES (source.{Glo.Tab.CONFERENCE_ID}, source.{Glo.Tab.DIAL_NO}, source.{Glo.Tab.CONNECTION_IS_MANAGED},
        source.{Glo.Tab.CONNECTION_IS_TEST}, source.{Glo.Tab.CONNECTION_ROW});
");

                    commands.Add($"UPDATE Conference SET " +
                                 $"{Glo.Tab.CONFERENCE_EDIT_LOGIN} = {editLoginID}, " +
                                 $"{Glo.Tab.CONFERENCE_EDIT_TIME} = '{SqlAssist.DateTimeToSQL((DateTime)editTime!)}' " +
                                 $"WHERE {Glo.Tab.CONFERENCE_ID} IN ({idCat});");
                }

                if ((additions != null && additions.Count > 0) ||
                    (removals != null && removals.Count > 0))
                    commands.Add(ReseatAllConnections());
            }

            else if (intent == Intent.Host && dialHost != null)
            {
                // Set the row of the desired host to 0.
                commands.Add($"UPDATE Connection SET {Glo.Tab.CONNECTION_ROW} = 0 " +
                             $"WHERE {Glo.Tab.CONFERENCE_ID} IN ({string.Join(", ", idStr)}) " +
                               $"AND {Glo.Tab.DIAL_NO} = '{dialHost}';");

                commands.Add($"UPDATE Conference SET " +
                             $"{Glo.Tab.CONFERENCE_EDIT_LOGIN} = {editLoginID}, " +
                             $"{Glo.Tab.CONFERENCE_EDIT_TIME} = '{SqlAssist.DateTimeToSQL((DateTime)editTime!)}' " +
                             $"WHERE {Glo.Tab.CONFERENCE_ID} IN ({idCat});");

                // ReseatAllConnections() will amend this to 1 and nudge all other connections down if needed.
                commands.Add(ReseatAllConnections());
            }

            return string.Join(" ", commands);
        }

        public string ReseatAllConnections()
        {
            List<string> idStr = ids.Select(i => i.ToString()).ToList();

            return $@"
WITH OrderedConnections AS (
    SELECT 
        {Glo.Tab.CONNECTION_ID},
        {Glo.Tab.CONFERENCE_ID},
        {Glo.Tab.CONNECTION_ROW},
        ROW_NUMBER() OVER (PARTITION BY {Glo.Tab.CONFERENCE_ID} ORDER BY {Glo.Tab.CONNECTION_ROW}) AS NewRowNumber
    FROM 
        Connection
    WHERE {Glo.Tab.CONFERENCE_ID} IN ({string.Join(", ", idStr)})
)
UPDATE Connection
SET {Glo.Tab.CONNECTION_ROW} = OrderedConnections.NewRowNumber
FROM Connection
JOIN OrderedConnections
ON Connection.{Glo.Tab.CONNECTION_ID} = OrderedConnections.{Glo.Tab.CONNECTION_ID};";
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
        public Resource(string sessionID, int columnRecordID, int resourceID, string? name,
                        int connectionCapacity, int conferenceCapacity, int rowsAdditional, bool nameChanged,
                        bool connectionCapacityChanged, bool conferenceCapacityChanged, bool rowsAdditionalChanged)
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

        public string SqlUpdate()
        {
            Prepare();

            List<string> setters = new();
            setters.Add(SqlAssist.Setter(Glo.Tab.RESOURCE_NAME, name));
            setters.Add(SqlAssist.Setter(Glo.Tab.RESOURCE_CAPACITY_CONNECTION, connectionCapacity.ToString()));
            setters.Add(SqlAssist.Setter(Glo.Tab.RESOURCE_CAPACITY_CONFERENCE, conferenceCapacity.ToString()));
            setters.Add(SqlAssist.Setter(Glo.Tab.RESOURCE_ROWS_ADDITIONAL, rowsAdditional.ToString()));

            StringBuilder bld = new();
            bld.Append($"IF EXISTS (SELECT c.{Glo.Tab.CONFERENCE_ID} FROM Conference c " +
                                    $"JOIN Resource r ON r.{Glo.Tab.RESOURCE_ID} = c.{Glo.Tab.RESOURCE_ID} " +
                                    $"WHERE c.{Glo.Tab.RESOURCE_ID} = {resourceID} AND " +
                                          $"c.{Glo.Tab.CONFERENCE_RESOURCE_ROW} >= " +
                                          $"{conferenceCapacity} + {rowsAdditional}) " +
                       $"BEGIN THROW 50000, 'That would remove rows that conferences are currently placed on.', 1; " +
                       $"END ELSE BEGIN ");

            bld.Append(SqlAssist.Update("Resource", string.Join(", ", setters), Glo.Tab.RESOURCE_ID, resourceID));
            bld.Append(" END;");

            return SqlAssist.Transaction(bld.ToString());
        }
    }

    struct Recurrence
    {
        public string sessionID;
        public int columnRecordID;
        public int? id;
        public string? name;
        public string? notes;
        public bool? requireIdBack;

        public Recurrence(string sessionID, int columnRecordID, int? id,
                          string? name, string? notes, bool? requireIdBack)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            this.id = id;
            this.name = name;
            this.notes = notes;
            this.requireIdBack = requireIdBack;
        }

        private void Prepare()
        {
            // Make sure the columns and values are safe, then add quotes where needed.
            if (name != null)
                name = SqlAssist.AddQuotes(SqlAssist.SecureValue(name));
            if (notes != null)
                notes = SqlAssist.AddQuotes(SqlAssist.SecureValue(notes));
        }

        public string SqlInsert()
        {
            Prepare();

            string com = SqlAssist.InsertInto("Recurrence", $"{Glo.Tab.RECURRENCE_NAME}, {Glo.Tab.NOTES}",
                                              (name == null || name == "''" ? "NULL" : name) + ", " +
                                              (notes == null || notes == "''" ? "NULL" : notes));
            if (requireIdBack == true)
                com += " SET @ID = SCOPE_IDENTITY();"; // Picked up by ExecuteNonQuery() in agent.

            return com;
        }

        public string SqlUpdate()
        {
            if (id == null)
                return "";

            Prepare();
            string recName = name == null || name == "''" ? "NULL" : name;
            string recNotes = notes == null || notes == "''" ? "NULL" : notes;

            return SqlAssist.Update("Recurrence",
                                    $"{Glo.Tab.RECURRENCE_NAME} = {recName}, " +
                                    $"{Glo.Tab.NOTES} = {recNotes}",
                                    Glo.Tab.RECURRENCE_ID, id.ToString()!);
        }
    }

    struct Task
    {
        public string sessionID;
        public int columnRecordID;
        public int taskID = -1;
        public string taskRef;
        public DateTime? opened;
        public DateTime? closed;
        public string? notes;
        public List<string> additionalCols = new();
        public List<string?> additionalVals = new();
        public List<bool> additionalNeedsQuotes = new();

        // Used for updating any attached organisations, visits and documents.
        public string oldTaskRef = "";
        public bool updateAllTaskRefs = false;

        public Task(string sessionID, int columnRecordID,
                       string taskRef, DateTime? opened, DateTime? closed, string? notes)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            this.taskRef = taskRef;
            this.opened = opened;
            this.closed = closed;
            this.notes = notes;
        }

        private void Prepare()
        {
            // Make sure the columns and values are safe, then add quotes where needed.
            taskRef = SqlAssist.AddQuotes(SqlAssist.SecureValue(taskRef));
            if (notes != null)
                notes = SqlAssist.AddQuotes(SqlAssist.SecureValue(notes));
            SqlAssist.SecureColumn(additionalCols);
            SqlAssist.SecureValue(additionalVals);
            SqlAssist.AddQuotes(additionalVals, additionalNeedsQuotes);

            oldTaskRef = SqlAssist.AddQuotes(SqlAssist.SecureValue(oldTaskRef));
        }

        public string SqlInsert()
        {
            Prepare();

            string ret = SqlAssist.InsertInto("Task",
                                              SqlAssist.ColConcat(additionalCols,
                                                                  Glo.Tab.TASK_REFERENCE,
                                                                  Glo.Tab.TASK_OPENED,
                                                                  Glo.Tab.TASK_CLOSED,
                                                                  Glo.Tab.NOTES),
                                              SqlAssist.ValConcat(additionalVals,
                                                                  taskRef,
                                                                  opened == null ? null :
                                                                  SqlAssist.DateTimeToSQL((DateTime)opened!,
                                                                                          true, true),
                                                                  closed == null ? null :
                                                                  SqlAssist.DateTimeToSQL((DateTime)closed!,
                                                                                          true, true),
                                                                  notes));
            return ret;
        }

        public string SqlUpdate()
        {
            Prepare();

            List<string> setters = new()
            {
                SqlAssist.Setter(Glo.Tab.TASK_REFERENCE, taskRef),
                SqlAssist.Setter(Glo.Tab.TASK_OPENED, opened == null ? null :
                                                      SqlAssist.DateTimeToSQL(opened.Value, true, true)),
                SqlAssist.Setter(Glo.Tab.TASK_CLOSED, closed == null ? null :
                                                      SqlAssist.DateTimeToSQL(closed.Value, true, true)),
                SqlAssist.Setter(Glo.Tab.NOTES, notes)
            };
            for (int i = 0; i < additionalCols.Count; ++i)
                setters.Add(SqlAssist.Setter(additionalCols[i], additionalVals[i]));

            List<string> commands = new() { SqlAssist.Update("Task", string.Join(", ", setters),
                                                             Glo.Tab.TASK_ID, taskID) };
            if (updateAllTaskRefs)
            {
                commands.Add(SqlAssist.Update("Organisation", $"{Glo.Tab.TASK_REFERENCE} = {taskRef}",
                                              Glo.Tab.TASK_REFERENCE, oldTaskRef));
                commands.Add(SqlAssist.Update("Visit", $"{Glo.Tab.TASK_REFERENCE} = {taskRef}",
                                              Glo.Tab.TASK_REFERENCE, oldTaskRef));
                commands.Add(SqlAssist.Update("Document", $"{Glo.Tab.TASK_REFERENCE} = {taskRef}",
                                              Glo.Tab.TASK_REFERENCE, oldTaskRef));
            }

            return SqlAssist.Transaction(commands.ToArray());
        }
    }

    struct Visit
    {
        public string sessionID;
        public int columnRecordID;
        public int visitID = -1;
        public string? taskRef;
        public string? type;
        public DateTime? date;
        public string? notes;
        public List<string> additionalCols = new();
        public List<string?> additionalVals = new();
        public List<bool> additionalNeedsQuotes = new();

        public Visit(string sessionID, int columnRecordID,
                    string? taskRef, string? type, DateTime? date, string? notes)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            this.taskRef = taskRef;
            this.type = type;
            this.date = date;
            this.notes = notes;
        }

        public Visit Clone() { return Clone(null); }
        public Visit Clone(string? taskRef)
        {
            Visit visit = new(sessionID, columnRecordID, taskRef, type, date, notes);
            // Create deep copies of lists and return.
            visit.additionalCols = additionalCols.Select(s => new string(s)).ToList();
            visit.additionalVals = additionalVals.Select(s => s == null ? null : new string(s)).ToList();
            visit.additionalNeedsQuotes = additionalNeedsQuotes.ToList();
            visit.taskRef = taskRef;
            return visit;
        }

        private void Prepare()
        {
            // Make sure the columns and values are safe, then add quotes where needed.
            if (taskRef != null)
                taskRef = SqlAssist.AddQuotes(SqlAssist.SecureValue(taskRef));
            if (type != null)
                type = SqlAssist.AddQuotes(SqlAssist.SecureValue(type));
            if (notes != null)
                notes = SqlAssist.AddQuotes(SqlAssist.SecureValue(notes));
            SqlAssist.SecureColumn(additionalCols);
            SqlAssist.SecureValue(additionalVals);
            SqlAssist.AddQuotes(additionalVals, additionalNeedsQuotes);
        }

        public string SqlInsert()
        {
            Prepare();

            string ret = SqlAssist.InsertInto("Visit",
                                              SqlAssist.ColConcat(additionalCols,
                                                                  Glo.Tab.TASK_REFERENCE,
                                                                  Glo.Tab.VISIT_TYPE,
                                                                  Glo.Tab.VISIT_DATE,
                                                                  Glo.Tab.NOTES),
                                              SqlAssist.ValConcat(additionalVals,
                                                                  taskRef,
                                                                  type,
                                                                  date == null ? null :
                                                                  SqlAssist.DateTimeToSQL((DateTime)date!,
                                                                                          true, true),
                                                                  notes));
            return ret;
        }

        public string SqlUpdate()
        {
            Prepare();

            List<string> setters = new()
            {
                SqlAssist.Setter(Glo.Tab.TASK_REFERENCE, taskRef),
                SqlAssist.Setter(Glo.Tab.VISIT_TYPE, type),
                SqlAssist.Setter(Glo.Tab.VISIT_DATE, date == null ? null :
                                                      SqlAssist.DateTimeToSQL(date.Value, true, true)),
                SqlAssist.Setter(Glo.Tab.NOTES, notes)
            };
            for (int i = 0; i < additionalCols.Count; ++i)
                setters.Add(SqlAssist.Setter(additionalCols[i], additionalVals[i]));

            return SqlAssist.Update("Visit", string.Join(", ", setters),
                                    Glo.Tab.VISIT_ID, visitID);
        }
    }

    struct Document
    {
        public string sessionID;
        public int columnRecordID;
        public int documentID = -1;
        public string? taskRef;
        public string? type;
        public DateTime? date;
        public string? notes;
        public List<string> additionalCols = new();
        public List<string?> additionalVals = new();
        public List<bool> additionalNeedsQuotes = new();

        public Document(string sessionID, int columnRecordID,
                    string? taskRef, string? type, DateTime? date, string? notes)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            this.taskRef = taskRef;
            this.type = type;
            this.date = date;
            this.notes = notes;
        }

        public Document Clone() { return Clone(null); }
        public Document Clone(string? taskRef)
        {
            Document doc = new(sessionID, columnRecordID, taskRef, type, date, notes);
            // Create deep copies of lists and return.
            doc.additionalCols = additionalCols.Select(s => new string(s)).ToList();
            doc.additionalVals = additionalVals.Select(s => s == null ? null : new string(s)).ToList();
            doc.additionalNeedsQuotes = additionalNeedsQuotes.ToList();
            doc.taskRef = taskRef;
            return doc;
        }

        private void Prepare()
        {
            // Make sure the columns and values are safe, then add quotes where needed.
            if (taskRef != null)
                taskRef = SqlAssist.AddQuotes(SqlAssist.SecureValue(taskRef));
            if (type != null)
                type = SqlAssist.AddQuotes(SqlAssist.SecureValue(type));
            if (notes != null)
                notes = SqlAssist.AddQuotes(SqlAssist.SecureValue(notes));
            SqlAssist.SecureColumn(additionalCols);
            SqlAssist.SecureValue(additionalVals);
            SqlAssist.AddQuotes(additionalVals, additionalNeedsQuotes);
        }

        public string SqlInsert()
        {
            Prepare();

            string ret = SqlAssist.InsertInto("Document",
                                              SqlAssist.ColConcat(additionalCols,
                                                                  Glo.Tab.TASK_REFERENCE,
                                                                  Glo.Tab.DOCUMENT_TYPE,
                                                                  Glo.Tab.DOCUMENT_DATE,
                                                                  Glo.Tab.NOTES),
                                              SqlAssist.ValConcat(additionalVals,
                                                                  taskRef,
                                                                  type,
                                                                  date == null ? null :
                                                                  SqlAssist.DateTimeToSQL((DateTime)date!,
                                                                                          true, true),
                                                                  notes));
            return ret;
        }

        public string SqlUpdate()
        {
            Prepare();

            List<string> setters = new()
            {
                SqlAssist.Setter(Glo.Tab.TASK_REFERENCE, taskRef),
                SqlAssist.Setter(Glo.Tab.DOCUMENT_TYPE, type),
                SqlAssist.Setter(Glo.Tab.DOCUMENT_DATE, date == null ? null :
                                                      SqlAssist.DateTimeToSQL(date.Value, true, true)),
                SqlAssist.Setter(Glo.Tab.NOTES, notes)
            };
            for (int i = 0; i < additionalCols.Count; ++i)
                setters.Add(SqlAssist.Setter(additionalCols[i], additionalVals[i]));

            return SqlAssist.Update("Document", string.Join(", ", setters),
                                    Glo.Tab.DOCUMENT_ID, documentID);
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

    public struct ExistenceCheck
    {
        public string sessionID;
        public int columnRecordID;
        public string table;
        public string idColumn;
        public List<string?> IDs;
        public bool idNeedsQuotes;
        public List<string> columns;
        public List<string?> values;
        public List<bool> needsQuotes;

        public ExistenceCheck(string sessionID, int columnRecordID,
                              string table, string idColumn, List<string?> IDs, bool idNeedsQuotes,
                              List<string> columns, List<string?> values, List<bool> needsQuotes)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            this.table = table;
            this.idColumn = idColumn;
            this.IDs = IDs;
            this.idNeedsQuotes = idNeedsQuotes;
            this.columns = columns;
            this.values = values;
            this.needsQuotes = needsQuotes;
        }

        public bool Prepare()
        {
            table = SqlAssist.SecureColumn(table);
            idColumn = SqlAssist.SecureColumn(idColumn);
            SqlAssist.SecureValue(IDs);
            if (idNeedsQuotes)
                SqlAssist.AddQuotes(IDs);
            SqlAssist.SecureColumn(columns);
            SqlAssist.SecureValue(values);
            SqlAssist.AddQuotes(values, needsQuotes);

            return (columns.Count == values.Count &&
                    columns.Count == needsQuotes.Count);
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

        // Some conference or recurrence searches want to automatically account for C- or R-.
        public bool? autoConfIdPrefix = null;
        public bool? autoRecIdPrefix = null;

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

            // Conference and Resource IDs need some special attention.
            if (whereValues.Count > 0)
            {
                if ((table == "Conference" || joinTables.Contains("Conference")) && autoConfIdPrefix == true)
                {
                    int idCol = whereColumns.IndexOf(Glo.Tab.CONFERENCE_ID);
                    whereValues.Add("%" + whereValues[idCol == -1 ? 0 : idCol] + "%");
                    whereColumns.Add($"'C-' + CAST(Conference.{Glo.Tab.CONFERENCE_ID} AS VARCHAR(MAX))");
                    whereAndOrs.Add("OR");
                    whereOperators.Add("LIKE");
                    whereValueTypesNeedQuotes.Add(true);
                }
                if ((table == "Recurrence" || joinTables.Contains("Recurrence")) && autoRecIdPrefix == true)
                {
                    int idCol = whereColumns.IndexOf(Glo.Tab.RECURRENCE_ID);
                    whereValues.Add("%" + whereValues[idCol == -1 ? 0 : idCol] + "%");
                    whereColumns.Add($"'R-' + CAST(Recurrence.{Glo.Tab.RECURRENCE_ID} AS VARCHAR(MAX))");
                    whereAndOrs.Add("OR");
                    whereOperators.Add("LIKE");
                    whereValueTypesNeedQuotes.Add(true);
                }
            }
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
                    for (int i = 0; i < whereColumns.Count - 1; ++i)
                        str.Append('(');
                for (int i = 0; i < whereColumns.Count; ++i)
                {
                    if (i > 0)
                        str.Append($"{(whereAndOrs[i - 1] == "OR" ? "   " : "  ")}{whereAndOrs[i - 1]} ");
                    str.Append($"{whereColumns[i]} {whereOperators[i]}");
                    if (whereValues[i] != null)
                        str.Append(whereValueTypesNeedQuotes[i] ?
                                   " " + SqlAssist.AddQuotes(whereValues[i]!) : " " + whereValues[i]);
                    str.Append(addBrackets && i != 0 ? ")\n" : "\n");
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

    public struct SelectResult
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
        public SelectResult(SqlDataReader reader, bool keepOpen)
        {
            columnNames = new();
            columnTypes = new();
            DataTable schema = reader.GetSchemaTable();
            foreach (DataRow row in schema.Rows)
            {
                columnNames.Add(row.Field<string>("ColumnName"));
                // We need to differentiate dates, or they get lost in translaction back to the client.
                if (row.Field<string>("DataTypeName") == "date")
                    columnTypes.Add("Date");
                else
                {
                    Type? t = row.Field<Type>("DataType");
                    columnTypes.Add(t == null ? null : t.Name);
                }
            }

            rows = new();
            while (reader.Read())
            {
                List<object?> row = new();
                for (int i = 0; i < reader.FieldCount; i++)
                    if (reader.IsDBNull(i))
                        row.Add(null);
                    else
                        row.Add(reader[i]);
                rows.Add(row);
            }

            if (!keepOpen)
                reader.Close();
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
            return SqlUpdate(true);
        }
        public string SqlUpdate(bool inTransaction)
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

            // If the table is Conference, we need to record the edit information.
            if (table == "Conference")
            {
                setters.Add(SqlAssist.Setter(Glo.Tab.CONFERENCE_EDIT_LOGIN, loginID.ToString()));
                setters.Add(SqlAssist.Setter(Glo.Tab.CONFERENCE_EDIT_TIME,
                                             SqlAssist.DateTimeToSQL(DateTime.Now, false, true)));
            }

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

                // Replace any null values with "NULL"
                for (int i = 0; i < values.Count; ++i)
                    if (values[i] == null)
                        values[i] = "NULL";

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

            if (commands.Count > 1 && inTransaction)
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
        public List<int> contactIDs;
        public bool unlink;

        public LinkContactRequest(string sessionID, int columnRecordID,
                                  string organisationRef, List<int> contactIDs, bool unlink)
        {
            this.sessionID = sessionID;
            this.columnRecordID = columnRecordID;
            this.organisationRef = organisationRef;
            this.contactIDs = contactIDs;
            this.unlink = unlink;
        }

        private void Prepare()
        {
            organisationRef = SqlAssist.AddQuotes(SqlAssist.SecureValue(organisationRef));
        }

        public string SqlInsert()
        {
            Prepare();
            List<string> commands = new();
            foreach (int i in contactIDs)
                commands.Add(SqlAssist.InsertInto("OrganisationContacts",
                                                  Glo.Tab.ORGANISATION_REF + ", " + Glo.Tab.CONTACT_ID,
                                                  organisationRef + ", " + i.ToString()));
            return SqlAssist.Transaction(commands.ToArray());
        }

        public string SqlDelete()
        {
            Prepare();
            List<string> commands = new();
            foreach (int i in contactIDs)
                commands.Add("DELETE FROM OrganisationContacts " +
                            $"WHERE {Glo.Tab.ORGANISATION_REF} = {organisationRef} " +
                            $"AND {Glo.Tab.CONTACT_ID} = {i};");
            return SqlAssist.Transaction(commands.ToArray());
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

    public struct PresetCheckRequest
    {
        public string sessionID;
        public List<string> presets;
        public List<string> tabs;
        public List<bool> present;
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

        private static readonly HashSet<string> joinTypes = new() { "INNER", "LEFT", "RIGHT", "LEFT OUTER",
                                                                    "RIGHT OUTER", "FULL OUTER" };
        private static readonly HashSet<string> operators = new() { "=", "!=", "<", ">", "<=", ">=",
                                                                    "LIKE", "NOT LIKE", "IS NULL", "IS NOT NULL" };
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
            if (type.ToUpper().Contains("INT"))
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
            {
                if (statements[i].EndsWith(' '))
                    statements[i] = statements[i].Remove(statements[i].Length - 1);
                if (!statements[i].EndsWith(';'))
                    statements[i] += ";";
            }

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
                    if (additionalVals[i] == null || additionalVals[i] == "")
                        additionalVals[i] = "NULL";
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

        public static string? ConvertObjectToSqlString(object? o)
        {
            if (o == null)
                return null;
            else if (o is DateTime dt)
                return DateTimeToSQL(dt, false, false);
            else if (o is byte b)
                return b.ToString();
            else if (o is Int16 i16)
                return i16.ToString();
            else if (o is Int32 i32)
                return i32.ToString();
            else if (o is TimeSpan ts)
                return TimeSpanToSQL(ts);
            else if (o is string s)
                return s;
            else // Hopefully this catches anything I missed...
            {
                if (o.ToString() == null)
                    return "";
                else
                    return o.ToString()!;
            }
        }
        public static string? ConvertObjectToSqlString(object? o, out bool needsQuotes)
        {
            if (o == null)
            {
                needsQuotes = false;
                return null;
            }
            else if (o is DateTime dt)
            {
                needsQuotes = true;
                return DateTimeToSQL(dt, false, false);
            }
            else if (o is byte b)
            {
                needsQuotes = false;
                return b.ToString();
            }
            else if (o is Int16 i16)
            {
                needsQuotes = false;
                return i16.ToString();
            }
            else if (o is Int32 i32)
            {
                needsQuotes = false;
                return i32.ToString();
            }
            else if (o is TimeSpan ts)
            {
                needsQuotes = true;
                return TimeSpanToSQL(ts);
            }
            else if (o is string s)
            {
                needsQuotes = true;
                return s;
            }
            else // Hopefully this catches anything I missed...
            {
                needsQuotes = false; // Not reliable in this instance.
                if (o.ToString() == null)
                    return "";
                else
                    return o.ToString()!;
            }
        }
        public static string ConvertObjectToSqlStringWithQuotes(object? o)
        {
            if (o == null)
                return "NULL";
            else if (o is DateTime dt)
                return DateTimeToSQL(dt, false, true);
            else if (o is byte b)
                return b.ToString();
            else if (o is Int16 i16)
                return i16.ToString();
            else if (o is Int32 i32)
                return i32.ToString();
            else if (o is TimeSpan ts)
                return "'" + TimeSpanToSQL(ts) + "'";
            else if (o is string s)
                return "'" + s + "'";
            else
                return "NULL";
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

        public static string DateTimeToSQL(DateTime dateTime)
        {
            return DateTimeToSQL(dateTime, false, false);
        }
        public static string DateTimeToSQL(DateTime dateTime, bool dateOnly)
        {
            return DateTimeToSQL(dateTime, dateOnly, false);
        }
        public static string DateTimeToSQL(DateTime dateTime, bool dateOnly, bool addQuotes)
        {
            if (dateOnly)
            {
                return addQuotes ? "'" + dateTime.ToString("yyyy-MM-dd") + "'" :
                                   dateTime.ToString("yyyy-MM-dd");
            }
            else
            {
                return addQuotes ? "'" + dateTime.ToString("yyyy-MM-dd HH:mm:ss.fff") + "'" :
                                   dateTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
            }
        }
        public static string TimeSpanToSQL(TimeSpan timeSpan)
        {
            return timeSpan.ToString(@"hh\:mm");
        }
    }
}