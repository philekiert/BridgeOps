using Microsoft.Data.SqlClient;
using SendReceiveClasses;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static FieldDefs;
using static System.Net.Mime.MediaTypeNames;

public class DatabaseCreator
{
    public const string DATABASE_NAME = "BridgeOps";
    public const string DATABASE_FILEPATH = "C:\\BridgeOps_Data.mdf";
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
            sqlConnect = new SqlConnection("server=localhost\\SQLEXPRESS;" +
                                           "integrated security=SSPI;" +
                                           //"user id=sa; password=^2*Re98E;" +
                                           "encrypt=false;" +
                                           "database=master;" +
                                           "Application Name=BridgeOpsConsole;");
            sqlCommand = new SqlCommand("", sqlConnect);

            // Test opening a connection and operating on sqlConnect.
            try
            {
                OpenSQL();
                string version = sqlConnect.ServerVersion;
                Writer.Message("Connection to SQL Server successfully established.");
                Writer.Message("SQL Server Version: " + version);
                CloseSQL();

                // Try switching to BridgeOps database.
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
            string[] lines = File.ReadAllLines(Glo.PATH_CONFIG_FILES + Glo.CONFIG_TYPE_OVERRIDES);

            int valuesRead = 0;
            int valuesChanged = 0;

            bool stillReadingPrimaryKeys = true;

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
                            string type = def.type;
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
                                    if (newVal != def.type && !(isList && "LIST(" + newVal + ")" == def.type))
                                    {
                                        ++valuesChanged;
                                        def.type = "CHAR(" + val + ")";
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
                                    if (newVal != def.type && !(isList && "LIST(" + newVal + ")" == def.type))
                                    {
                                        ++valuesChanged;
                                        def.type = "VARCHAR(" + val + ")";
                                    }
                                    readSuccess = true;
                                }
                            }
                            else if (fieldDefs.TestIntegralType(val))
                            {
                                if (val != def.type && !(isList && "LIST(" + val + ")" == def.type))
                                {
                                    ++valuesChanged;
                                    def.type = val;
                                }
                                readSuccess = true;
                            }

                            if (isList)
                                def.type = "LIST(" + def.type + ")";

                            if (readSuccess)
                            {
                                ++valuesRead;
                                if (showFileReadSuccesses)
                                    Writer.Affirmative(key + " read as " + def.type);
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
            if (!showFileReadSuccesses)
            {
                if (valuesRead > 0)
                    Writer.Affirmative(valuesRead.ToString() + " value overrides read successfully, " +
                                                                valuesChanged + " differed from current values.");
                else
                    Writer.Neutral("0 value overrides read successfully.");
            }
            else
                Writer.Neutral(valuesChanged.ToString() + " settings differed from current values.");
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
            def.Value.type = def.Value.typeDefault;
    }

    public void DeleteDatabase()
    {
        if (CheckDatabaseFileExistence())
        {
            Writer.Message("Are you sure you want to delete the database? " +
                           "This will permanently delete all data - there is no way to undo this.");
            if (Writer.YesNo())
            {
                int n = 0;
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
                            if (SendCommandSQL("DROP DATABASE " + DATABASE_NAME))
                            {
                                Console.WriteLine("");
                                Writer.Affirmative("Database deleted.");
                            }
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
            {
                Writer.Message("Destruct sequence aborted.");
            }
        }
        else
            Writer.Message("Database file '" + DATABASE_FILEPATH + "' not found.", ConsoleColor.Red);
    }
    public void CreateDatabase()
    {
        string creation = "CREATE DATABASE " + DATABASE_NAME + " ON PRIMARY (" +
                            "NAME = " + DATABASE_NAME + "_Data, " +
                            "FILENAME = '" + DATABASE_FILEPATH + "', " +
                            "SIZE = 2GB, MAXSIZE = 10GB, FILEGROWTH = 10%) " +
                          "LOG ON (" +
                            "NAME = BridgeOps_Log, " +
                            "FILENAME = 'C:\\BridgeOps_Log.ldf', " +
                            "SIZE = 1GB, MAXSIZE = 5GB, FILEGROWTH = 10%)";

        if (SendCommandSQL(creation))
        {
            Writer.Affirmative(string.Format("Database created successfully as '{0}'.", DATABASE_FILEPATH));
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
            string conferenceType = "";
            string conference = "";
            string conferenceRecurrence = "";
            string connections = "";
            string dialNo = "";
            string conferencesByDay = "";
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
                if (fieldDefs.Category(def) == "Conference Type")
                    AddColumn(ref conferenceType, def);
                if (fieldDefs.Category(def) == "Conference")
                    AddColumn(ref conference, def);
                if (fieldDefs.Category(def) == "Conference Recurrence")
                    AddColumn(ref conferenceRecurrence, def);
                if (fieldDefs.Category(def) == "Connection")
                    AddColumn(ref connections, def);
                if (fieldDefs.Category(def) == "Dial No")
                    AddColumn(ref dialNo, def);
                if (fieldDefs.Category(def) == "Conferences by Day")
                    AddColumn(ref conferencesByDay, def);
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
                if (fieldDefs.Category(def) == "Conference Resource")
                    AddColumn(ref junctionConfResource, def);
            }

            void AddColumn(ref string column, KeyValuePair<string, FieldDefs.Definition> def)
            {
                if (column.Length > 0)
                    column += ", ";
                else
                    column += "CREATE TABLE " + fieldDefs.TableName(def.Key) + " (";
                // UNSIGNED is not supported in SQL Server.
                column += def.Value.columnName + " " +
                          (def.Value.type == "BOOLEAN" ? "BIT" : def.Value.type).Replace(" UNSIGNED", "");

                // Auto Increment keys.
                if (def.Value.autoIncrement)
                    column += " IDENTITY";
            }

            // Entity Table Strings
            organisation += ", CONSTRAINT pk_OrgID PRIMARY KEY (Organisation_ID)" +
                            ", CONSTRAINT fk_ParentOrgID FOREIGN KEY (Organisation_ID) REFERENCES Organisation (Organisation_ID)  );";
            contact += ", CONSTRAINT pk_ContactID PRIMARY KEY (Contact_ID) );";
            login += ", CONSTRAINT pk_LoginID PRIMARY KEY (Login_ID)" +
                     ", CONSTRAINT nq_Username UNIQUE (Username) );";
            asset += ", CONSTRAINT pk_AssetID PRIMARY KEY (Asset_ID)" +
                     ", CONSTRAINT fk_AssetOrganisation FOREIGN KEY (Organisation_ID) REFERENCES Organisation (Organisation_ID) ON DELETE SET NULL );";
            resource += ", CONSTRAINT pk_ResourceID PRIMARY KEY (Resource_ID) );";
            conferenceType += ", CONSTRAINT pk_ConfTypeID PRIMARY KEY (Type_ID) );";
            conference += ", CONSTRAINT pk_ConfID PRIMARY KEY (Conference_ID)" +
                          ", CONSTRAINT fk_ConfType FOREIGN KEY (Type) REFERENCES ConferenceType (Type_ID)" +
                          ", CONSTRAINT fk_ConfOrg FOREIGN KEY (Organisation_ID) REFERENCES Organisation (Organisation_ID) ON DELETE SET NULL );";
            //            Reccurrence ID would be a foreign key but for the cascade loop it would cause with the ConferenceRecurrence table.
            conferenceRecurrence += ", CONSTRAINT pk_ConfRecID PRIMARY KEY (Recurrence_ID)" +
                                    ", CONSTRAINT fk_ConfID FOREIGN KEY (Conference_ID) REFERENCES Conference (Conference_ID) ON DELETE CASCADE );";

            // Supplemental Tables Strings
            dialNo += ", CONSTRAINT pk_DialNo PRIMARY KEY (Dial_No)" +
                      ", CONSTRAINT fk_DialNoOrganisation FOREIGN KEY (Organisation_ID) REFERENCES Organisation (Organisation_ID) ON DELETE CASCADE );";
            connections += ", CONSTRAINT pk_ConfID_DialNo PRIMARY KEY (Conference_ID, Dial_No)" +
                           ", CONSTRAINT fk_ConnectionConfID FOREIGN KEY (Conference_ID) REFERENCES Conference (Conference_ID) ON DELETE CASCADE" +
                           ", CONSTRAINT fk_ConnectionDialNo FOREIGN KEY (Dial_No) REFERENCES DialNo (Dial_No) );";
            conferencesByDay += ", CONSTRAINT pk_Date_ConfID PRIMARY KEY (Date, Conference_ID)" +
                                ", CONSTRAINT fk_ConfbyDay_ConfID FOREIGN KEY (Conference_ID) REFERENCES Conference (Conference_ID) ON DELETE CASCADE );";
            organisationChange += ", CONSTRAINT pk_OrgID_ChangeID PRIMARY KEY (Organisation_ID, Change_ID)" +
                                  ", CONSTRAINT fk_OrgChange_OrgID FOREIGN KEY (Organisation_ID) REFERENCES Organisation (Organisation_ID) ON DELETE CASCADE );";
            //                    No real point making a foreign key for Login_ID - we don't want to cascade delete or set to null if the login is deleted.
            assetChange += ", CONSTRAINT pk_AssetID_Change_ID PRIMARY KEY (Asset_ID, Change_ID)" +
                           ", CONSTRAINT fk_AssetChange_AssetID FOREIGN KEY (Asset_ID) REFERENCES Asset (Asset_ID) ON DELETE CASCADE );";

            // Junction Tables Strings
            junctionOrgContacts += ", CONSTRAINT pk_jncContacts_OrgID_ContactID PRIMARY KEY (Organisation_ID, Contact_ID)" +
                                   ", CONSTRAINT fk_jncContacts_OrgID FOREIGN KEY (Organisation_ID) REFERENCES Organisation (Organisation_ID) ON DELETE CASCADE" +
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
            Writer.Message("Creating Conference Type table...");
            SendCommandSQL(conferenceType);
            Writer.Message("Creating Conference table...");
            SendCommandSQL(conference);
            Writer.Message("Creating Recurrence table...");
            SendCommandSQL(conferenceRecurrence);
            Writer.Message("Creating Dial No table...");
            SendCommandSQL(dialNo);
            Writer.Message("Creating Connection table...");
            SendCommandSQL(connections);
            Writer.Message("Creating Organisation Change table...");
            SendCommandSQL(organisationChange);
            Writer.Message("Creating Conferences by Day table...");
            SendCommandSQL(conferencesByDay);
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
            Writer.Message("Creating Conference Resource junction table...");
            SendCommandSQL(junctionConfResource);

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
                    command += addition.column + Glo.Tab.CHANGE_REGISTER_SUFFIX + " BIT, ";
                    command += addition.column + " " + (addition.type == "BOOLEAN" ? "BIT" : addition.type) + ";";

                    if (SendCommandSQL(command))
                        Writer.Affirmative("Adding " + addition.column + " to " + table);
                }
            }

            Writer.Message("\nCreating admin login...");
            SendCommandSQL(string.Format("INSERT INTO Login (Username, Password, Type) " +
                                         "VALUES ('admin', HASHBYTES('SHA2_512', 'admin'), {0});", Glo.USER_ADMIN));


            //---- CREATE TYPE RESTRICTIONS FILE ----// (For use by Client in determining what to display/allow in UI)
            RestoreColumnRecord();
        }
    }
    public void RestoreColumnRecord()
    {
        try
        {
            Writer.Message("\nGathering data from database for column records file...");
            sqlConnect.Open();

            // Get a list of columns and their allowed values.
            sqlCommand = new SqlCommand("SELECT t.[name], con.[definition] " +
                                        "FROM sys.check_constraints con " +
                                            "LEFT OUTER JOIN sys.objects t " +
                                                "ON con.parent_object_id = t.object_id " +
                                            "LEFT OUTER JOIN sys.all_columns col " +
                                                "ON con.parent_column_id = col.column_id " +
                                                "AND con.parent_object_id = col.object_id", sqlConnect);

            SqlDataReader reader = sqlCommand.ExecuteReader(System.Data.CommandBehavior.Default);
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

            // Get the max lengths of varchars. TEXT will later be limited to 65535 for compatibility with MySQL.
            sqlCommand = new SqlCommand("SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH " +
                                        "FROM BridgeOps.INFORMATION_SCHEMA.COLUMNS;", sqlConnect);
            reader = sqlCommand.ExecuteReader(System.Data.CommandBehavior.Default);

            List<string[]> columns = new List<string[]>();
            while (reader.Read())
            {
                string length = "";
                if (!reader.IsDBNull(3))
                {
                    int lengthInt = reader.GetInt32(3);
                    // Limit TEXT length to 65535, as SQL Server returns this value as bytes allowed, not characters.
                    length = (lengthInt > 65535 ? 65535 : lengthInt).ToString();
                }
                columns.Add(new string[] { reader.GetString(0), reader.GetString(1), reader.GetString(2), length });
            }
            reader.Close();

            string fileText = "!!! NEVER, EVER, EVER EDIT THIS FILE !!!\n";

            foreach (string[] column in columns)
            {
                fileText += column[0] + "[C]" + column[1] + "[R]";
                if (column[3] == "") // int
                    fileText += column[2].ToUpper();
                else // char or varchar
                    fileText += column[3];
                if (checkConstraints.ContainsKey(column[0] + column[1]))
                {
                    foreach (string s in checkConstraints[column[0] + column[1]])
                    {
                        fileText += "[A]";
                        fileText += s;
                    }
                }
                fileText += '\n';
            }

            // Add friendly names, if present and valid.
            List<string> friendlyNames = new List<string>();
            if (File.Exists(Glo.PATH_CONFIG_FILES + Glo.CONFIG_FRIENDLY_NAMES))
            {
                fileText += "-\n"; // This signals the start of friendly names when read by Client.
                friendlyNames = File.ReadAllLines(Glo.PATH_CONFIG_FILES + Glo.CONFIG_FRIENDLY_NAMES).ToList();
                for (int n = 0; n < friendlyNames.Count; ++n)
                {
                    if (friendlyNames[n].Length >= 8 && !friendlyNames[n].StartsWith('#'))
                    {
                        string[] split = friendlyNames[n].Split(";;");
                        if (split.Length == 3 && split[0].Length > 0 || split[1].Length > 0 || split[2].Length > 0)
                            if (split[0] == "Organisation" || split[0] == "Contact" ||
                                split[0] == "Asset" || split[0] == "Conference")
                                fileText += friendlyNames[n] + '\n';
                    }
                }
            }

            if (fileText.EndsWith('\n'))
                fileText = fileText.Remove(fileText.Length - 1);

            // Automatically generates the file if one isn't present.
            if (!Directory.Exists(Glo.PATH_CONFIG_FILES))
                Directory.CreateDirectory(Glo.PATH_CONFIG_FILES);
            File.WriteAllText(Glo.PATH_AGENT + Glo.CONFIG_COLUMN_RECORD, fileText);
            Writer.Affirmative("Type restrictions file written successfully.");
        }
        catch (Exception e)
        {
            Writer.Message("Couldn't create type restrictions file. See error:", ConsoleColor.Red);
            Writer.Message(e.Message, ConsoleColor.Red);
        }
        finally
        {
            CloseSQL();
        }
    }

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
    public void LoadAdditionalColumns(bool detailed)
    {
        columnAdditions.Clear();

        if (File.Exists(Glo.PATH_CONFIG_FILES + Glo.CONFIG_COLUMN_ADDITIONS))
        {
            string[] lines = File.ReadAllLines(Glo.PATH_CONFIG_FILES + Glo.CONFIG_COLUMN_ADDITIONS);
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
                        if (type.StartsWith("VARCHAR"))
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
                                               type == "TINYTEXT" || type == "MEDIUMTEXT" || type == "TEXT" ||
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
    public void WipeColumnAdditions()
    {
        columnAdditions.Clear();
        Writer.Affirmative("Column additions wiped.");
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

    private bool CheckDatabaseFileExistence()
    {
        return File.Exists(DATABASE_FILEPATH);
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
                sqlConnect.ConnectionString = "server=localhost\\SQLEXPRESS;" +
                                              "integrated security=SSPI;" +
                                              //"user id=sa; password=^2*Re98E;" +
                                              "encrypt=false;" +
                                              "database=" + dbName + ";" +
                                              "Application Name=BridgeOpsConsole;";

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
                                   $" COLUMN_NAME LIKE '%{Glo.Tab.CHANGE_REGISTER_SUFFIX}';";

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
    public bool SendCommandSQL(string s) { return SendCommandSQL(s, false); }
    public bool SendCommandSQL(string s, bool silent)
    {
        if (sqlCommand == null || sqlCommand.Connection.Database != sqlConnect.Database)
            sqlCommand = new SqlCommand("", sqlConnect);

        try
        {
            OpenSQL();
            sqlCommand.CommandText = s;
            sqlCommand.ExecuteNonQuery();

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
