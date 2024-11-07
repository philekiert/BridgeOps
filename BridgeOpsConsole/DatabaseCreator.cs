using Microsoft.Data.SqlClient;
using SendReceiveClasses;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static FieldDefs;
using static System.Net.Mime.MediaTypeNames;

public class DatabaseCreator
{
    public const string DATABASE_NAME = "BridgeManager";
    public const string DATABASE_FILENAME = "BridgeManager_Data.mdf";
    public const string DATABASE_LOGNAME = "BridgeManager_Log.ldf";
    public static string DatabaseFilePath
    { get { return Path.Combine(Glo.Fun.ApplicationFolder(), DATABASE_FILENAME); } }
    public static string DatabaseLogPath
    { get { return Path.Combine(Glo.Fun.ApplicationFolder(), DATABASE_LOGNAME); } }
    FieldDefs fieldDefs;

    SqlConnection sqlConnect = new SqlConnection();
    SqlCommand sqlCommand = new SqlCommand();

    // Options
    public bool showFileReadSuccesses = false;

    public DatabaseCreator(FieldDefs fieldDefs)
    {
        this.fieldDefs = fieldDefs;

        /* The master database refers to the top-level database that maintains
         * records of all other databases running on the SQL Server instance. */
        try
        {
            sqlConnect = new SqlConnection($"server=localhost\\{sqlServerName};" +
                                            "integrated security=SSPI;" +
                                            "encrypt=false;" +
                                            "database=master;" +
                                            "Application Name=BridgeManageConsole;");
            sqlCommand = new SqlCommand("", sqlConnect);

            // Test opening a connection and operating on sqlConnect.
            try
            {
                OpenSQL();
                string version = sqlConnect.ServerVersion;
                Writer.Message("Connection to SQL Server successfully established.");
                Writer.Message("SQL Server Version: " + version);
                CloseSQL();

                // Try switching to bridge Manager database.
                if (!CheckDatabaseFileExistence())
                    Writer.Negative("Database not yet created.");
                else
                    SwitchToDatabase(DATABASE_NAME);
            }
            catch (Exception e)
            {
                Writer.Message("\nConnection to database could not be established. See error:", ConsoleColor.Red);
                Writer.Message(e.Message, ConsoleColor.Red);
                CloseSQL();
            }
        }
        catch (Exception e)
        {
            Writer.Message("Couldn't set up connection with SQL Server. See error:", ConsoleColor.Red);
            Writer.Message(e.Message);
        }
    }

    public void LoadOverridesFromFile()
    {
        try
        {
            Writer.Message("\nReading " + Glo.CONFIG_TYPE_OVERRIDES + "...");
            string[] lines = File.ReadAllLines(Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_TYPE_OVERRIDES));

            int valuesRead = 0;
            int valuesChanged = 0;

            bool stillReadingPrimaryKeys = true;

            void UpdateForeignKeys(string currentKey, string newType)
            {
                foreach (KeyValuePair<string, Definition> kvp in fieldDefs.defs)
                {
                    if (kvp.Key.EndsWith(currentKey))
                        kvp.Value.sqlType = newType;
                }
            }

            foreach (string line in lines)
            {
                if (!line.StartsWith("# ") && line.Length >= 6)
                {
                    string key = line.Substring(0, line.IndexOf(':'));

                    // Only read if def is overridable, or is a primary key and we haven't read our first non-primary.
                    if (fieldDefs.defs.ContainsKey(key) && (fieldDefs.defs[key].canOverride ||
                        (fieldDefs.defs[key].primaryKey && stillReadingPrimaryKeys)))
                    {
                        FieldDefs.Definition def = fieldDefs.defs[key];

                        // As soon as we read our first non-primary, don't allow any more primary keys.
                        if (!def.primaryKey)
                            stillReadingPrimaryKeys = false;

                        int valIndex = line.LastIndexOf(" = ") + 3;
                        if (valIndex > -1)
                        {
                            string val = line.Remove(0, valIndex);
                            string type = def.sqlType;
                            bool isList = false;
                            bool readSuccess = false;
                            // First, check to see if the type is a list, extract and remember for later.
                            if (type.StartsWith("LIST("))
                            {
                                type = fieldDefs.ExtractLISTType(type);
                                isList = true;
                            }
                            if (type.StartsWith("CHAR("))
                            {
                                int iVal;
                                if (int.TryParse(val, out iVal) && iVal > 0 && iVal <= 65535)
                                {
                                    string newVal = "CHAR(" + val + ")";
                                    if (newVal != def.sqlType && !(isList && "LIST(" + newVal + ")" == def.sqlType))
                                    {
                                        ++valuesChanged;
                                        def.sqlType = "CHAR(" + val + ")";
                                        if (stillReadingPrimaryKeys)
                                            UpdateForeignKeys(key, def.sqlType);
                                    }
                                    readSuccess = true;
                                }
                            }
                            else if (type.StartsWith("VARCHAR("))
                            {
                                int iVal;
                                if (int.TryParse(val, out iVal) && iVal > 0 && iVal <= 65535)
                                {
                                    string newVal = "VARCHAR(" + val + ")";
                                    if (newVal != def.sqlType && !(isList && "LIST(" + newVal + ")" == def.sqlType))
                                    {
                                        ++valuesChanged;
                                        def.sqlType = "VARCHAR(" + val + ")";
                                        if (stillReadingPrimaryKeys)
                                            UpdateForeignKeys(key, def.sqlType);
                                    }
                                    readSuccess = true;
                                }
                            }
                            else if (fieldDefs.TestIntegralType(val))
                            {
                                if (val != def.sqlType && !(isList && "LIST(" + val + ")" == def.sqlType))
                                {
                                    ++valuesChanged;
                                    def.sqlType = val;
                                    if (stillReadingPrimaryKeys)
                                        UpdateForeignKeys(key, def.sqlType);
                                }
                                readSuccess = true;
                            }

                            if (isList)
                                def.sqlType = "LIST(" + def.sqlType + ")";

                            if (readSuccess)
                            {
                                ++valuesRead;
                                //if (showFileReadSuccesses)
                                    Writer.Affirmative(key + " read as " + def.sqlType);
                            }
                            else
                                Writer.Negative(key + " couldn't be read.");
                        }
                    }
                    else if (fieldDefs.defs.ContainsKey(key) && fieldDefs.defs[key].primaryKey &&
                             !stillReadingPrimaryKeys)
                        Writer.Negative(key + " couldn't be read as it was placed below non-primary keys.");
                }
            }
            //if (!showFileReadSuccesses)
            //{
                if (valuesRead > 0)
                    Writer.Affirmative(valuesRead.ToString() + " value overrides read successfully, " +
                                                                valuesChanged + " differed from current values.");
                else
                    Writer.Neutral("0 value overrides read successfully.");
            //}
            //else
            //    Writer.Neutral(valuesChanged.ToString() + " settings differed from current values.");
        }
        catch
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Reading " + Glo.CONFIG_TYPE_OVERRIDES +
                              " failed. Try agian after checking filename, values and text formatting. " +
                              "Using default values.");
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
    public void ResetTypeOverrides()
    {
        foreach (KeyValuePair<string, Definition> def in fieldDefs.defs)
            def.Value.sqlType = def.Value.sqlTypeDefault;
    }

    public void DeleteDatabase()
    {
        if (!CheckDatabaseFileExistence())
        {
            Writer.Message("Database file '" + DatabaseFilePath + "' not found.", ConsoleColor.Red);
            Writer.Message("Proceeding with DROP command anyway, just in case SQL Server still thinks it's there.");
        }

        Writer.Message("Are you sure you want to delete the database? " +
                       "This will permanently delete all data - there is no way to undo this.");
        if (Writer.YesNo())
        {
            int n = 0; // Set to 4 to skip destruct sequence for debugging.
#if DEBUG
            // Bypass the destruct sequence if debugging.
            n = 4;
#endif
            while (n < 5)
            {
                ++n;
                if (n != 5)
                    Console.WriteLine("\n          DESTRUCT SEQUENCE");

                if (n == 1)
                {
                    Console.Write("                 ONE\n");
                    Console.Write("              CODE: ");
                    string? read = Console.ReadLine();
                    if (read == "11A")
                        continue;
                    else
                        break;
                }
                else if (n == 2)
                {
                    Console.Write("                 TWO\n");
                    Console.Write("             CODE: ");
                    string? read = Console.ReadLine();
                    if (read == "11A2B")
                        continue;
                    else
                        break;
                }
                else if (n == 3)
                {
                    Console.Write("                THREE\n");
                    Console.Write("             CODE: ");
                    string? read = Console.ReadLine();
                    if (read == "1B2B3")
                        continue;
                    else
                        break;
                }
                else if (n == 4)
                {
                    Console.Write("              COMPLETED\n");
                    Console.Write("             AND ENGAGED\n");
                    Console.Write("\n\n");
                    Console.Write("              AWAITING\n");
                    Console.Write("             FINAL CODE\n");
                    Console.Write("         CODE: ");
                    if (Console.ReadLine() == "000DESTRUCT0")
                        continue;
                    else
                        break;
                }
                else // if n == 5
                {
                    try
                    {
                        Console.WriteLine("");
                        SwitchToDatabase("master");
                        // This first command should make it so the database can still be deleted even if it's
                        // currently in use. I can't figure out why it sometimes gets held up as I can see no sessions
                        // are currently using it, 
                        if (SendCommandSQL($"ALTER DATABASE[{DATABASE_NAME}] " +
                                            "SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                                            "DROP DATABASE " + DATABASE_NAME))
                        {
                            Console.WriteLine("");
                            Writer.Affirmative("Database deleted.");
                        }
                        else
                            Writer.Message("\nIs the database in use by another application, possibly the agent?");
                    }
                    catch (Exception e)
                    {
                        Writer.Message("\nSomething went wrong when attempting to delete the database. See error:",
                                       ConsoleColor.Red);
                        Writer.Message(e.Message, ConsoleColor.Red);
                    }
                }
            }

            if (n != 5)
                Writer.Message("\nCode incorrect. Destruct sequence aborted.", ConsoleColor.Red);
        }
        else
            Writer.Message("Destruct sequence aborted.");
    }
    public void CreateDatabase()
    {
        Writer.Message("Creating database and log file...");
        string creation = "CREATE DATABASE " + DATABASE_NAME + " ON PRIMARY (" +
                            "NAME = " + DATABASE_NAME + "_Data, " +
                            "FILENAME = '" + DatabaseFilePath + "', " +
                            "SIZE = 2GB, MAXSIZE = 10GB, FILEGROWTH = 10%) " +
                          "LOG ON (" +
                            "NAME = BridgeManager_Log, " +
                            "FILENAME = '" + DatabaseLogPath + "', " +
                            "SIZE = 1GB, MAXSIZE = 5GB, FILEGROWTH = 10%)";

        if (SendCommandSQL(creation))
        {
            Writer.Affirmative(string.Format("Database created successfully as '{0}'.", DatabaseFilePath));
            try
            {
                OpenSQL();
                SwitchToDatabase(DATABASE_NAME);
                sqlCommand.Connection = sqlConnect;
            }
            catch (Exception e)
            {
                Writer.Message("Could not connect to " + DATABASE_NAME + " database. See error:", ConsoleColor.Red);
                Writer.Message(e.Message, ConsoleColor.Red);
            }
            finally
            {
                CloseSQL();
            }
        }
        // Any error messages are printed in SendCommandSQL().

        //---- CREATE TABLES ----//

        if (sqlConnect.Database == DATABASE_NAME)
        {
            string organisation = "";
            string contact = "";
            string login = "";
            string asset = "";
            string resource = "";
            //string conferenceType = "";
            string conference = "";
            string recurrence = "";
            string connections = "";
            //string dialNo = "";
            //string conferencesByDay = "";
            string organisationChange = "";
            string assetChange = "";
            string junctionOrgContacts = "";
            //string junctionOrgEngineers = "";
            //string junctionOrgChangeContacts = "";
            //string junctionOrgChangeEngineers = "";
            string junctionConfResource = "";

            foreach (var def in fieldDefs.defs)
            {
                // CREATE TABLE clause is added automatically in AddColumn().
                if (fieldDefs.Category(def) == "Organisation")
                    AddColumn(ref organisation, def);
                if (fieldDefs.Category(def) == "Contact")
                    AddColumn(ref contact, def);
                if (fieldDefs.Category(def) == "Login")
                    AddColumn(ref login, def);
                if (fieldDefs.Category(def) == "Asset")
                    AddColumn(ref asset, def);
                if (fieldDefs.Category(def) == "Resource")
                    AddColumn(ref resource, def);
                //if (fieldDefs.Category(def) == "Conference Type")
                //    AddColumn(ref conferenceType, def);
                if (fieldDefs.Category(def) == "Conference")
                    AddColumn(ref conference, def);
                if (fieldDefs.Category(def) == "Recurrence")
                    AddColumn(ref recurrence, def);
                if (fieldDefs.Category(def) == "Connection")
                    AddColumn(ref connections, def);
                //if (fieldDefs.Category(def) == "Dial No")
                //    AddColumn(ref dialNo, def);
                //if (fieldDefs.Category(def) == "Conferences by Day")
                //    AddColumn(ref conferencesByDay, def);
                if (fieldDefs.Category(def) == "Organisation Change")
                    AddColumn(ref organisationChange, def);
                if (fieldDefs.Category(def) == "Asset Change")
                    AddColumn(ref assetChange, def);
                if (fieldDefs.Category(def) == "Organisation Contacts")
                    AddColumn(ref junctionOrgContacts, def);
                //if (fieldDefs.Category(def) == "Organisation Engineers")
                //    AddColumn(ref junctionOrgEngineers, def);
                //if (fieldDefs.Category(def) == "Organisation Change Contacts")
                //    AddColumn(ref junctionOrgChangeContacts, def);
                //if (fieldDefs.Category(def) == "Organisation Change Engineers")
                //    AddColumn(ref junctionOrgChangeEngineers, def);
                //if (fieldDefs.Category(def) == "Conference Resource")
                //    AddColumn(ref junctionConfResource, def);
            }

            void AddColumn(ref string column, KeyValuePair<string, FieldDefs.Definition> def)
            {
                if (column.Length > 0)
                    column += ", ";
                else
                    column += "CREATE TABLE " + fieldDefs.TableName(def.Key) + " (";
                // UNSIGNED is not supported in SQL Server.
                column += def.Value.columnName + " " +
                          (def.Value.sqlType == "BOOLEAN" ? "BIT" : def.Value.sqlType).Replace(" UNSIGNED", "");

                // Auto Increment keys.
                if (def.Value.autoIncrement)
                    column += " IDENTITY";
            }

            // Entity Table Strings
            organisation += ", CONSTRAINT pk_OrgID PRIMARY KEY (Organisation_ID)" +
                            ", CONSTRAINT fk_ParentOrgRef FOREIGN KEY (Parent_Reference) REFERENCES Organisation (Organisation_Reference)" +
                            ", CONSTRAINT u_OrgRef UNIQUE (Organisation_Reference) );" +
                             " CREATE UNIQUE INDEX u_OrgDialNo ON Organisation (Dial_No) WHERE Dial_No IS NOT NULL;"; // Needs adding separately due to the WHERE clause.
            contact += ", CONSTRAINT pk_ContactID PRIMARY KEY (Contact_ID) );";
            login += ", CONSTRAINT pk_LoginID PRIMARY KEY (Login_ID)" +
                     ", CONSTRAINT u_Username UNIQUE (Username) );";
            asset += ", CONSTRAINT pk_AssetID PRIMARY KEY (Asset_ID)" +
                     ", CONSTRAINT fk_AssetOrganisation FOREIGN KEY (Organisation_Reference) REFERENCES Organisation (Organisation_Reference) ON DELETE SET NULL ON UPDATE CASCADE );" +
                     " CREATE UNIQUE INDEX u_assetRef ON Asset (Asset_Reference) WHERE Asset_Reference IS NOT NULL;"; // Needs adding separately due to the WHERE clause.
            resource += ", CONSTRAINT pk_ResourceID PRIMARY KEY (Resource_ID)" +
                        ", CONSTRAINT u_ResourceName UNIQUE (Resource_Name) );";
            //conferenceType += ", CONSTRAINT pk_ConfTypeID PRIMARY KEY (Type_ID) );";
            conference += ", CONSTRAINT pk_ConfID PRIMARY KEY (Conference_ID)" +
                          ", CONSTRAINT fk_ConfResource FOREIGN KEY (Resource_ID) REFERENCES Resource (Resource_ID)" +
                          ", CONSTRAINT fk_ConfRecurrence FOREIGN KEY (Recurrence_ID) REFERENCES Recurrence (Recurrence_ID)" +
                          ", CONSTRAINT fk_ConfCreationLogin FOREIGN KEY (Creation_Login_ID) REFERENCES Login (Login_ID) ON DELETE SET NULL" +
                          ", CONSTRAINT dt_ConfStartEnd CHECK (Start_Time < End_Time) );";
            //               We have to manually implement the Edit_Login_ID cascades below due to SQL Server's cautious nature regarding cascade cycles.
            //            Reccurrence ID would be a foreign key but for the cascade loop it would cause with the Recurrence table.
            recurrence += ", CONSTRAINT pk_ConfRecID PRIMARY KEY (Recurrence_ID) );";

            // Supplemental Tables Strings
            //dialNo += ", CONSTRAINT pk_DialNo PRIMARY KEY (Dial_No)" +
            //          ", CONSTRAINT fk_DialNoOrganisation FOREIGN KEY (Organisation_Reference) REFERENCES Organisation (Organisation_Reference) ON DELETE CASCADE );";
            connections += ", CONSTRAINT pk_ConnID PRIMARY KEY (Connection_ID)" +
                           ", CONSTRAINT fk_ConnectionConfID FOREIGN KEY (Conference_ID) REFERENCES Conference (Conference_ID) ON DELETE CASCADE" +
                           ", CONSTRAINT u_ConnectionConfIDDialNo UNIQUE (Conference_ID, Dial_No)" +
                           ", CONSTRAINT dt_ConnConnectDisconnect CHECK (Connection_Time < Disconnection_Time) );";
            //conferencesByDay += ", CONSTRAINT pk_Date_ConfID PRIMARY KEY (Date, Conference_ID)" +
            //                    ", CONSTRAINT fk_ConfbyDay_ConfID FOREIGN KEY (Conference_ID) REFERENCES Conference (Conference_ID) ON DELETE CASCADE );";
            organisationChange += ", CONSTRAINT pk_OrgID_ChangeID PRIMARY KEY (Organisation_ID, Change_ID)" +
                                  ", CONSTRAINT fk_OrgChange_LoginID FOREIGN KEY (Login_ID) REFERENCES Login (Login_ID) ON DELETE SET NULL ON UPDATE CASCADE " +
                                  ", CONSTRAINT fk_OrgChange_OrgID FOREIGN KEY (Organisation_ID) REFERENCES Organisation (Organisation_ID) ON DELETE CASCADE );";
            //                    No real point making a foreign key for Login_ID - we don't want to cascade delete or set to null if the login is deleted.
            assetChange += ", CONSTRAINT pk_AssetID_ChangeID PRIMARY KEY (Asset_ID, Change_ID)" +
                           ", CONSTRAINT fk_AssetChange_LoginID FOREIGN KEY (Login_ID) REFERENCES Login (Login_ID) ON DELETE SET NULL ON UPDATE CASCADE " +
                           ", CONSTRAINT fk_AssetChange_AssetID FOREIGN KEY (Asset_ID) REFERENCES Asset (Asset_ID) ON DELETE CASCADE );";

            // Junction Tables Strings
            junctionOrgContacts += ", CONSTRAINT pk_jncContacts_OrgRef_ContactID PRIMARY KEY (Organisation_Reference, Contact_ID)" +
                                   ", CONSTRAINT fk_jncContacts_OrgRef FOREIGN KEY (Organisation_Reference) REFERENCES Organisation (Organisation_Reference) ON DELETE CASCADE ON UPDATE CASCADE" +
                                   ", CONSTRAINT fk_jncContacts_ContactID FOREIGN KEY (Contact_ID) REFERENCES Contact (Contact_ID) ON DELETE CASCADE );";
            //junctionOrgEngineers += ", CONSTRAINT pk_jncEngs_OrgID_ContactID PRIMARY KEY (Organisation_ID, Contact_ID)" +
            //                        ", CONSTRAINT fk_jncEngs_OrgID FOREIGN KEY (Organisation_ID) REFERENCES Organisation (Organisation_ID) ON DELETE CASCADE" +
            //                        ", CONSTRAINT fk_jncEngs_ContactID FOREIGN KEY (Contact_ID) REFERENCES Contact (Contact_ID) ON DELETE CASCADE );";
            //junctionOrgChangeContacts += ", CONSTRAINT pk_jncChangeContacts_OrgID_ChangeID_ContactID PRIMARY KEY (Organisation_ID, Change_ID, Contact_ID)" +
            //                             ", CONSTRAINT fk_jncChangeContacts_OrgID FOREIGN KEY (Organisation_ID, Change_ID) REFERENCES OrganisationChange (Organisation_ID, Change_ID) ON DELETE CASCADE" +
            //                             ", CONSTRAINT fk_jncChangeContacts_ContactID FOREIGN KEY (Contact_ID) REFERENCES Contact (Contact_ID) ON DELETE CASCADE );";
            //junctionOrgChangeEngineers += ", CONSTRAINT pk_jncChangeEngs_OrgID_ChangeID_ContactID PRIMARY KEY (Organisation_ID, Change_ID, Contact_ID)" +
            //                              ", CONSTRAINT fk_jncChangeEngs_OrgID FOREIGN KEY (Organisation_ID, Change_ID) REFERENCES OrganisationChange (Organisation_ID, Change_ID) ON DELETE CASCADE" +
            //                              ", CONSTRAINT fk_jncChangeEngs_ContactID FOREIGN KEY (Contact_ID) REFERENCES Contact (Contact_ID) ON DELETE CASCADE );";
            junctionConfResource += ", CONSTRAINT pk_jncConfRes_ConfID_ResID PRIMARY KEY (Conference_ID, Resource_ID)" +
                                    ", CONSTRAINT fk_jncConfRes_ConfID FOREIGN KEY (Conference_ID) REFERENCES Conference (Conference_ID) ON DELETE CASCADE" +
                                    ", CONSTRAINT fk_jncConfRes_ResID FOREIGN KEY (Resource_ID) REFERENCES Resource (Resource_ID) ON DELETE CASCADE );";

            // Create tables
            Writer.Message("\nCreating Organisation table...");
            SendCommandSQL(organisation);
            Writer.Message("Creating Contact table...");
            SendCommandSQL(contact);
            Writer.Message("Creating Login table...");
            SendCommandSQL(login);
            Writer.Message("Creating Asset table...");
            SendCommandSQL(asset);
            Writer.Message("Creating Resource table...");
            SendCommandSQL(resource);
            //Writer.Message("Creating Conference Type table...");
            //SendCommandSQL(conferenceType);
            Writer.Message("Creating Recurrence table...");
            SendCommandSQL(recurrence);
            Writer.Message("Creating Conference table...");
            SendCommandSQL(conference);
            //Writer.Message("Creating Dial No table...");
            //SendCommandSQL(dialNo);
            Writer.Message("Creating Connection table...");
            SendCommandSQL(connections);
            Writer.Message("Creating Organisation Change table...");
            SendCommandSQL(organisationChange);
            //Writer.Message("Creating Conferences by Day table...");
            //SendCommandSQL(conferencesByDay);
            Writer.Message("Creating Asset Change table...");
            SendCommandSQL(assetChange);
            Writer.Message("Creating Organisation Contacts junction table...");
            SendCommandSQL(junctionOrgContacts);
            //Writer.Message("Creating Organisation Engineers junction table...");
            //SendCommandSQL(junctionOrgEngineers);
            //Writer.Message("Creating Organisation Change Contacts junction table...");
            //SendCommandSQL(junctionOrgChangeContacts);
            //Writer.Message("Creating Organisation Change Engineers junction table...");
            //SendCommandSQL(junctionOrgChangeEngineers);
            //Writer.Message("Creating Conference Resource junction table...");
            //SendCommandSQL(junctionConfResource);

            // We can't have two foreign keys that cascade on the same table relating to the same column. Create a trigger to implement the second one manually.
            Writer.Message("\nApplying triggers to Conference table for editor updates and deletions...");
            //SendCommandSQL("CREATE TRIGGER trg_updateConfEditLogin ON Login " +
            //               "AFTER UPDATE AS UPDATE Conference SET Edit_Login_ID = i.Login_ID FROM Conference c JOIN UPDATED i ON c.Edit_Login_ID = i.Login_ID;");
            SendCommandSQL("CREATE TRIGGER trg_deleteConfEditLogin ON Login " +
                           "AFTER DELETE AS UPDATE Conference SET Edit_Login_ID = NULL WHERE Edit_Login_ID IN (SELECT Login_ID FROM DELETED);");
            Writer.Message("Applying triggers to Connection table for Dial No updates and deletions...");
            SendCommandSQL("CREATE TRIGGER trg_updateConnDialNo ON Organisation " +
                           "AFTER UPDATE AS UPDATE Connection SET Dial_No = (SELECT i.Dial_No FROM INSERTED i) FROM Connection c JOIN DELETED d ON c.Dial_No = d.Dial_No WHERE c.Is_Managed = 1;");
            SendCommandSQL("CREATE TRIGGER trg_deleteConnDialNo ON Organisation " +
                           "AFTER DELETE AS UPDATE Connection SET Is_Managed = 0 WHERE Dial_No IN (SELECT Dial_No FROM DELETED);"); // Doesnt matter if we catch unmanaged connections in this.

            Writer.Message("\nApplying conference closure options...");
            SendCommandSQL("ALTER TABLE Conference ADD CONSTRAINT chk_ConferenceClosure CHECK (Closure IN ('Successful', 'Degraded', 'No Show', 'Failed'));");

            Writer.Message("\nApplying column additions...");
            foreach (ColumnAddition addition in columnAdditions)
            {
                string command = "ALTER TABLE " + addition.table +
                                 " ADD " + addition.column + " " + (addition.type == "BOOLEAN" ? "BIT" : addition.type);
                // allow list should have already been checked for validity in LoadAdditionalColumns().
                if (addition.allowed.Length > 0)
                {
                    command += " CONSTRAINT chk_" + addition.table + addition.column + " CHECK (" +
                        addition.column + " in ('" + string.Join("\',\'", addition.allowed);
                    command += "'))";
                }
                command += ";";

                if (SendCommandSQL(command))
                    Writer.Affirmative("Adding " + addition.column + " to " + addition.table);
            }

            Writer.Message("\nReplicating column additions for change tables...");
            foreach (ColumnAddition addition in columnAdditions)
            {
                if (addition.table == "Organisation" || addition.table == "Asset")
                {
                    string table = addition.table + "Change";

                    string command = "ALTER TABLE " + table + " ADD ";
                    command += addition.column + Glo.Tab.CHANGE_SUFFIX + " BIT, ";
                    command += addition.column + " " + (addition.type == "BOOLEAN" ? "BIT" : addition.type) + ";";

                    if (SendCommandSQL(command))
                        Writer.Affirmative("Adding " + addition.column + " to " + table);
                }
            }

            Writer.Message("\nCreating Friendly Name table...");
            SendCommandSQL($"CREATE TABLE FriendlyNames ({Glo.Tab.FRIENDLY_TABLE} VARCHAR(128) NOT NULL, " +
                                                       $"{Glo.Tab.FRIENDLY_COLUMN} VARCHAR(128) NOT NULL, " +
                                                       $"{Glo.Tab.FRIENDLY_NAME} VARCHAR(128) NOT NULL);");
            if (friendlyNames.Count > 0)
            {
                Writer.Message("Populating friendly names...");
                foreach (string[] names in friendlyNames)
                    if (SendCommandSQL($"INSERT INTO FriendlyNames VALUES ('{names[0]}', '{names[1]}', '{names[2]}');"))
                        Writer.Affirmative($"{names[1]} -> {names[2]}");
            }

            Writer.Message("\nCreating Column Order tables...");
            string OrderString(string table)
            {
                string commandCreate = $"CREATE TABLE {table}Order (";
                string commandInsert = $"INSERT INTO {table}Order VALUES (";
                // 1024 is the max number of columns. This is only for simplifying development, the user will never get
                // that far. It does, however, mean we need four separate tables as we can't use the first column to
                // store the relevant table name, but the storage considerations here are negligible.
                for (int i = 0; i < 1024; ++i)
                {
                    commandCreate += $"_{i} SMALLINT NOT NULL, ";
                    commandInsert += $"{i}, ";
                }
                commandCreate = commandCreate.Remove(commandCreate.Length - 2); // Remove the trailing ", ".
                commandInsert = commandInsert.Remove(commandInsert.Length - 2); // Remove the trailing ", ".
                commandCreate += "); ";
                commandInsert += ");";

                return commandCreate + commandInsert;
            }
            SendCommandSQL(OrderString("Organisation"));
            SendCommandSQL(OrderString("Asset"));
            SendCommandSQL(OrderString("Contact"));
            SendCommandSQL(OrderString("Conference"));

            Writer.Message("\nCreating view for additional conference information...");
            SendCommandSQL($@"
CREATE VIEW ConferenceAdditional AS
WITH HostInfo AS (
SELECT Conference.{Glo.Tab.CONFERENCE_ID}, Connection.{Glo.Tab.DIAL_NO}
FROM
	Conference  
LEFT JOIN
	Connection ON Conference.{Glo.Tab.CONFERENCE_ID} = Connection.{Glo.Tab.CONFERENCE_ID}
	WHERE Connection.Row = 1
),
AbsoluteConnectionTimes AS (
SELECT Connection.{Glo.Tab.CONFERENCE_ID},
    MIN(Connection.{Glo.Tab.CONNECTION_TIME_FROM}) AS {Glo.Tab.CONFERENCE_ADD_CONNECTED_START},
    MAX(Connection.{Glo.Tab.CONNECTION_TIME_TO}) AS {Glo.Tab.CONFERENCE_ADD_CONNECTED_END}
FROM Connection
    WHERE {Glo.Tab.CONFERENCE_ID} in (SELECT {Glo.Tab.CONFERENCE_ID} FROM HostInfo) AND
          {Glo.Tab.CONNECTION_TIME_FROM} IS NOT NULL AND {Glo.Tab.CONNECTION_TIME_TO} IS NOT NULL
GROUP BY {Glo.Tab.CONFERENCE_ID}
)
SELECT
	Conference.{Glo.Tab.CONFERENCE_ID},
	HostInfo.{Glo.Tab.DIAL_NO} AS {Glo.Tab.CONFERENCE_ADD_HOST_DIAL_NO},
	COUNT(Connection.{Glo.Tab.DIAL_NO}) AS {Glo.Tab.CONFERENCE_ADD_CONNECTIONS},
    COUNT(CASE WHEN Connection.{Glo.Tab.CONNECTION_TIME_FROM} IS NOT NULL AND 
                    Connection.{Glo.Tab.CONNECTION_TIME_TO} IS NOT NULL
               THEN 1 END) AS {Glo.Tab.CONFERENCE_ADD_CONNECTED},
    AbsoluteConnectionTimes.{Glo.Tab.CONFERENCE_ADD_CONNECTED_START} AS {Glo.Tab.CONFERENCE_ADD_CONNECTED_START},
    AbsoluteConnectionTimes.{Glo.Tab.CONFERENCE_ADD_CONNECTED_END} AS {Glo.Tab.CONFERENCE_ADD_CONNECTED_END},
	CONVERT(DATE, AbsoluteConnectionTimes.{Glo.Tab.CONFERENCE_ADD_CONNECTED_START}) AS {Glo.Tab.CONFERENCE_ADD_CONNECTED_START_DATE},
	CONVERT(DATE, AbsoluteConnectionTimes.{Glo.Tab.CONFERENCE_ADD_CONNECTED_END}) AS {Glo.Tab.CONFERENCE_ADD_CONNECTED_END_DATE},
	CONVERT(TIME, AbsoluteConnectionTimes.{Glo.Tab.CONFERENCE_ADD_CONNECTED_START}) AS {Glo.Tab.CONFERENCE_ADD_CONNECTED_START_TIME},
	CONVERT(TIME, AbsoluteConnectionTimes.{Glo.Tab.CONFERENCE_ADD_CONNECTED_END}) AS {Glo.Tab.CONFERENCE_ADD_CONNECTED_END_TIME},
	AbsoluteConnectionTimes.{Glo.Tab.CONFERENCE_ADD_CONNECTED_END} - AbsoluteConnectionTimes.{Glo.Tab.CONFERENCE_ADD_CONNECTED_START} AS {Glo.Tab.CONFERENCE_ADD_CONNECTED_DURATION},
    CAST((CASE WHEN MAX(CAST(Connection.{Glo.Tab.CONNECTION_IS_TEST} AS INT)) = 1 THEN 1 ELSE 0 END) AS BIT) AS Is_Test,
	CONVERT(DATE, Conference.{Glo.Tab.CONFERENCE_START}) AS {Glo.Tab.CONFERENCE_ADD_START_DATE},
	CONVERT(DATE, Conference.{Glo.Tab.CONFERENCE_END}) AS {Glo.Tab.CONFERENCE_ADD_END_DATE},
	CONVERT(TIME, Conference.{Glo.Tab.CONFERENCE_START}) AS {Glo.Tab.CONFERENCE_ADD_START_TIME},
	CONVERT(TIME, Conference.{Glo.Tab.CONFERENCE_END}) AS {Glo.Tab.CONFERENCE_ADD_END_TIME},
	Conference.{Glo.Tab.CONFERENCE_END} - Conference.{Glo.Tab.CONFERENCE_START} AS {Glo.Tab.CONFERENCE_ADD_DURATION}
FROM
	Conference
LEFT JOIN AbsoluteConnectionTimes ON Conference.{Glo.Tab.CONFERENCE_ID} = AbsoluteConnectionTimes.{Glo.Tab.CONFERENCE_ID}
LEFT JOIN HostInfo ON Conference.{Glo.Tab.CONFERENCE_ID} = HostInfo.{Glo.Tab.CONFERENCE_ID}
LEFT JOIN Connection ON Conference.{Glo.Tab.CONFERENCE_ID} = Connection.{Glo.Tab.CONFERENCE_ID}
LEFT JOIN Organisation ON Organisation.{Glo.Tab.DIAL_NO} = Connection.{Glo.Tab.DIAL_NO}
GROUP BY
	Conference.{Glo.Tab.CONFERENCE_ID},
	HostInfo.{Glo.Tab.DIAL_NO},
    AbsoluteConnectionTimes.{Glo.Tab.CONFERENCE_ADD_CONNECTED_START},
    AbsoluteConnectionTimes.{Glo.Tab.CONFERENCE_ADD_CONNECTED_END},
	Conference.{Glo.Tab.CONFERENCE_START},
	Conference.{Glo.Tab.CONFERENCE_END}
");

            Writer.Message("\nCreating Bridge Manager admin login...");
            SendCommandSQL(string.Format("INSERT INTO Login (Username, Password, Admin, {0}, {1}, {2}, {3}) " +
                                         "VALUES ('admin', HASHBYTES('SHA2_512', 'admin'), 1, " +
                                                  "{4}, {4}, {4}, 1);",
                                         Glo.Tab.LOGIN_CREATE_PERMISSIONS,
                                         Glo.Tab.LOGIN_EDIT_PERMISSIONS,
                                         Glo.Tab.LOGIN_DELETE_PERMISSIONS,
                                         Glo.Tab.LOGIN_ENABLED,
                                         Glo.PERMISSIONS_MAX_VALUE));
        }
    }

    public List<string[]> friendlyNames = new();

    List<ColumnAddition> columnAdditions = new List<ColumnAddition>();
    struct ColumnAddition
    {
        public string table;
        public string column;
        public string type;
        public string[] allowed;

        public ColumnAddition(string table, string column, string type, string[] allowed)
        {
            this.table = table;
            this.column = column;
            this.type = type;
            this.allowed = allowed;
        }
    }
    public void WipeColumnAdditions()
    {
        columnAdditions.Clear();
        Writer.Affirmative("Column additions wiped.");
    }
    public void LoadAdditionalColumns(bool detailed)
    {
        columnAdditions.Clear();

        if (File.Exists(Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_COLUMN_ADDITIONS)))
        {
            string[] lines = File.ReadAllLines(Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_COLUMN_ADDITIONS));
            int readsFailed = 0;
            int readSuccesses = 0;

            int n = 0;
            foreach (string line in lines)
            {
                int colIndex = 0;
                int typeIndex = 0;
                int allowIndex = 0;

                bool validFormat = false;
                bool foundAllowList = false;
                if (line.Length > 8 && line.StartsWith("[table] "))
                {
                    colIndex = line.IndexOf(" [column] ");
                    if (colIndex > 9)
                    {
                        typeIndex = line.IndexOf(" [type] ");
                        if (typeIndex > colIndex + 10)
                        {
                            validFormat = true;
                            allowIndex = line.IndexOf(" [allowed] ");
                            if (allowIndex > typeIndex + 8)
                                foundAllowList = true;
                        }
                    }
                }

                if (validFormat)
                {
                    string table = line.Substring(8, colIndex - 8);
                    string column = line.Substring(colIndex + 10, typeIndex - (colIndex + 10)).Replace(' ', '_');
                    string type = line.Substring(typeIndex + 8);
                    string[] allowArr = new string[0];
                    int varcharLength = 0;
                    if (foundAllowList)
                    {
                        string allowList = line.Substring(allowIndex + 11);
                        type = type.Remove(type.IndexOf(" [allowed] "));
                        allowArr = allowList.Split(";");
                    }

                    // Exit this iteration if the any value is invalid.
                    bool invalidated = false;
                    if (table != "Organisation" && table != "Contact" && table != "Asset" && table != "Conference")
                        invalidated = true;
                    if (!invalidated)
                    {
                        // Correct column name by removing any illegal characters.
                        for (int c = 0; c < column.Length; ++c)
                            if (!char.IsLetterOrDigit(column[c]) && column[c] != '_')
                            {
                                column = column.Remove(c, 1);
                                --c;
                            }
                        // Make sure the column name is still valid after the alterations, and invalidate if duplicate.
                        bool stillValid = column.Length > 0;
                        if (stillValid)
                            foreach (ColumnAddition colAdd in columnAdditions)
                                if (colAdd.table == table && colAdd.column == column)
                                {
                                    stillValid = false;
                                    break;
                                }
                        if (!stillValid)
                            invalidated = true;
                    }
                    if (!invalidated)
                    {
                        if (type.StartsWith("VARCHAR") && type != "VARCHAR(MAX)")
                        {
                            if (int.TryParse(fieldDefs.ExtractVARCHARLength(type), out varcharLength))
                            {
                                if (varcharLength < 1 || varcharLength > 65535)
                                    invalidated = true;
                            }
                            else
                                invalidated = true;
                        }
                        if (fieldDefs.TestIntegralType(type))
                        {
                            foreach (string i in allowArr)
                            {
                                int val;
                                if (!int.TryParse(i, out val) ||
                                      ((type == "TINYINT" && (val < -128 || val > 127)) ||
                                      (type == "SMALLINT" && (val < -32768 || val > 32767)) ||
                                      (type == "INT" && (val < -2147483648 || val > 2147483647))))
                                {
                                    invalidated = true;
                                    break;
                                }
                            }
                        }
                        else if (type.StartsWith("VARCHAR")) // length already validated above :)
                        {
                            if (type != "VARCHAR(MAX)")
                                foreach (string a in allowArr)
                                {
                                    if (a.Length > varcharLength || a.Contains("' OR ["))
                                    {
                                        invalidated = true;
                                        break;
                                    }
                                }
                        }
                        else
                        {
                            // SQL Server doesn't like BOOLEAN, but this is corrected to BIT in CreateDatabase().
                            bool isOtherType = type == "FLOAT" ||
                                               type == "DATE" ||
                                               type == "DATETIME" ||
                                               type == "TIME" ||
                                               type == "BOOLEAN";
                            if (isOtherType && foundAllowList)
                                invalidated = true;
                            else if (!isOtherType)
                                invalidated = true;
                        }
                    }

                    if (invalidated)
                    {
                        ++readsFailed;
                        if (detailed)
                            Writer.Negative("Line " + n + " couldn't be interpreted.");
                    }
                    else
                    {
                        columnAdditions.Add(new ColumnAddition(table, column, type, allowArr));

                        ++readSuccesses;
                        if (detailed)
                        {
                            Writer.Affirmative("Line " + n + " read as:\n  " +
                                table + " | " + column + " | " + type);
                            if (allowArr.Length > 0)
                            {
                                Writer.Message("  Allowed values: ");
                                foreach (string a in allowArr)
                                    Writer.Message("    " + a, ConsoleColor.DarkGray);
                            }
                        }
                    }
                }

                ++n;
            }

            if (!detailed)
                Writer.Affirmative(readSuccesses + " columns read successfully.");
            else
                Writer.Neutral(readSuccesses + " columns read successfully.");
            if (readsFailed > 0)
            {
                if (!detailed)
                    Writer.Negative(readsFailed + " columns couldn't be interpreted.");
                else
                {
                    Console.WriteLine();
                    Writer.Negative(readsFailed + " lines couldn't be interpreted, but looked like they were" +
                                                  " supposed to be column definitions. If this is the case, please" +
                                                  " check formatting and that all values given are correct.");
                }
            }
        }
        else
        {
            Writer.Negative(Glo.CONFIG_COLUMN_ADDITIONS + " not found.");
        }
    }

    private void OpenSQL()
    {
        try
        {
            if (sqlConnect.State == System.Data.ConnectionState.Closed)
                sqlConnect.Open();
        }
        catch (Exception e)
        {
            Writer.Message("\nSomething went wrong when attempting to open the SQL connection. See error:",
                           ConsoleColor.Red);
            Writer.Message(e.Message, ConsoleColor.Red);
        }
    }

    private void CloseSQL()
    {
        if (sqlConnect.State == System.Data.ConnectionState.Open)
            sqlConnect.Close();
    }

    public static string sqlServerName = Glo.SQL_SERVER_NAME_DEFAULT;
    private bool CheckDatabaseFileExistence()
    {
        return File.Exists(DatabaseFilePath);
    }
    private bool SwitchToDatabase(string dbName)
    {
        try
        {
            OpenSQL();

            if (CheckDatabaseFileExistence())
            {
                string oldDb = sqlConnect.Database;
                sqlConnect.ChangeDatabase(dbName);
                SendCommandSQL("USE " + dbName);

                sqlConnect.Dispose();
                sqlConnect.ConnectionString = $"server=localhost\\{sqlServerName};" +
                                               "integrated security=SSPI;" +
                                               "encrypt=false;" +
                                               "database=" + dbName + ";" +
                                               "Application Name=BridgeManagerConsole;";

                Writer.Affirmative(string.Format("Switched from {0} to {1}.", oldDb, dbName));

                return true;
            }
            else
            {
                Writer.Message("'" + dbName + "' database does not currently exist.", ConsoleColor.Red);
                return false;
            }
        }
        catch (Exception e)
        {
            Writer.Message("Could not switch to '" + dbName + "' database. See error:", ConsoleColor.Red);
            Writer.Message(e.Message, ConsoleColor.Red);
            return false;
        }
        finally
        {
            CloseSQL();
        }
    }

    public List<string> GetAllColumnNames(string table)
    {
        try
        {
            OpenSQL();
            List<string> columnNames = new();

            if (sqlCommand == null || sqlCommand.Connection.Database != sqlConnect.Database)
                sqlCommand = new SqlCommand("", sqlConnect);
            sqlCommand.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS" +
                                   $" WHERE TABLE_NAME = '{table}' AND" +
                                   $" COLUMN_NAME LIKE '%{Glo.Tab.CHANGE_SUFFIX}';";

            using (SqlDataReader reader = sqlCommand.ExecuteReader())
                while (reader.Read())
                    columnNames.Add(reader.GetString(0));

            return columnNames;
        }
        catch
        {
            Writer.Message("Could not read column names.", ConsoleColor.Red);
        }
        finally
        {
            CloseSQL();
        }
        return new List<string>();
    }

    // Automatically opens and closes the connection.
    // If query is true, sqlReader is updated with the results.
    public string lastSqlError = "";
    public int lastSqlRowsAffected = 0;
    public bool SendCommandSQL(string s) { return SendCommandSQL(s, false); }
    public bool SendCommandSQL(string s, bool silent)
    {
        if (sqlCommand == null || sqlCommand.Connection.Database != sqlConnect.Database)
            sqlCommand = new SqlCommand("", sqlConnect);

        try
        {
            OpenSQL();
            sqlCommand.CommandText = s;
            lastSqlRowsAffected = sqlCommand.ExecuteNonQuery();

            return true;
        }
        catch (Exception e)
        {
            if (!silent)
            {
                Writer.Message("\nSomething went wrong when attempting to send the SQL command. See error:",
                               ConsoleColor.Red);
                Writer.Message(e.Message, ConsoleColor.Red);
            }
            lastSqlError = e.Message;

            return false;
        }
        finally
        {
            CloseSQL();
        }
    }
}
