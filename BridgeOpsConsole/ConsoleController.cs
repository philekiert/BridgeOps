
/* NOTES FOR USE
 * -------------
 * 
 * Adding a menu:
 *   - Add another const int near the top of the class.
 *   - Increase MENU_LAST_INDEX by 1.
 *   - Add another case to MenuName().
 *   - Everything else should be automated, included the help display.
 * 
 * Adding a command:
 *   - Add a definition to the class constructor.
 *   - Add a corresponding function towards the bottom of the file.
 *   - Command functions must return an integer due to the CommandDef constructor.
 */


using Azure.Core.GeoJson;
using Microsoft.Data.SqlClient;
using SendReceiveClasses;
using System.ComponentModel.Design;
using System.Data;
using System.Diagnostics;
using System.IO.Pipes;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

public class ConsoleController
{
    public enum ValType
    {
        None,
        Int, // Not yet implemented.
        String
    }
    string commandValString = "";
    int commandValInt = 0;

    SendReceive sr = new SendReceive();

    public const int MENU_GLOBAL = -1;
    public const int MENU_HOME = 0;
    public const int MENU_DATABASE = 1;
    public const int MENU_DATA = 2;
    public const int MENU_AGENT = 3;
    public const int MENU_NETWORK = 4;
    // Used by ConsoleWriter.Help() to determine the list. Should always be the highest menu index + 1.
    public const int MENU_LAST_INDEX = 5;

    static int currentMenu = 0;
    public static string MenuName(int menuState)
    {
        switch (menuState)
        {
            case MENU_GLOBAL:
                return "global";
            case MENU_HOME:
                return "home";
            case MENU_DATABASE:
                return "database";
            case MENU_DATA:
                return "data";
            case MENU_AGENT:
                return "agent";
            case MENU_NETWORK:
                return "network";
            default:
                return "unknown";
        }
    }
    public static string CurrentMenuName()
    {
        return MenuName(currentMenu);
    }
    public static string Capitalise(string str)
    {
        char[] cStr = str.ToCharArray();

        if (cStr.Length > 0)
            cStr[0] = char.ToUpper(cStr[0]);
        for (int n = 0; n < str.Length - 1; ++n)
        {
            if (cStr[n] == ' ')
            {
                cStr[n + 1] = cStr[n + 1].ToString().ToUpper().ToCharArray()[0];
            }
        }

        return new string(cStr);
    }

    /* This Dictionary collection of structs contains details for every possible command.
     * The command definitions are set in the class constructor. */
    struct CommandDef
    {
        public ValType valType;
        public int menu;
        public Func<int> method;
        public string help;
        public CommandDef(ValType valType, int menu, Func<int> method, string help)
        {
            this.valType = valType;
            this.menu = menu;
            this.method = method;
            this.help = help;
        }
    }
    Dictionary<string, CommandDef> commandDefs = new Dictionary<string, CommandDef>();

    DatabaseCreator dbCreate;
    FieldDefs fieldDefs;

    // Commands defined here.
    public ConsoleController(DatabaseCreator dbCreate, FieldDefs fieldDefs)
    {
        this.dbCreate = dbCreate;
        this.fieldDefs = fieldDefs;

        // Define commands.

        // Home
        int menu = MENU_GLOBAL;
        AddCommand("help", ValType.None, menu, Help, "");
        AddCommand("exit", ValType.None, menu, Exit,
                   "Safely exit the program.");
        AddCommand("[menu name]", ValType.None, menu, Exit,
                   "Switch to stated menu name." /* Possible menus listed in ConsoleWriter.HelpItem() */);

        // Database
        menu = MENU_DATABASE;
        AddCommand("load type overrides", ValType.None, menu, LoadTypeOverrides,
                   "Load type overrides from file \"" + Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_TYPE_OVERRIDES) +
                   "\". File path is relevant to the path of this executable.");
        AddCommand("create type overrides", ValType.None, menu, CreateTypeOverrides,
                   "Generate default values and output to \"" + Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_TYPE_OVERRIDES) + "\". File path is relevant to the path of this executable.");
        AddCommand("reset type overrides", ValType.None, menu, ResetTypeOverrides,
                   "Reset type overrides to default types.");
        AddCommand("view current types", ValType.None, menu, ViewCurrentValuesWithOverrides,
                   "View the list of columns in their respective tables with the currently loaded overrides. Does " +
                   "not include column additions.");
        AddCommand("load column additions", ValType.None, menu, LoadColumnAdditions,
                   "Load column additions from file \"" + Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_COLUMN_ADDITIONS) +
                   "\". File path is relevant to the path of this executable.");
        AddCommand("create column additions", ValType.None, menu, CreateColumnAdditions,
                   "Create a new column additions template file under \"" + Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_COLUMN_ADDITIONS) + "\". File path is relevant to the path of this executable.");
        AddCommand("wipe column additions", ValType.None, menu, WipeColumnAdditions,
                   "Wipe all loaded column additions. Does not affect the column additions template file.");
        AddCommand("create friendly names", ValType.None, menu, CreateFriendlyNames,
                   "Create a new friendly names template file under \"" + Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_FRIENDLY_NAMES) + "\". File path is relevant to the path of this executable.");
        AddCommand("load friendly names", ValType.None, menu, LoadFriendlyNames,
                   "Load friendly names from \"" + Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_FRIENDLY_NAMES) +
                   "\". File path is relevent to the path of this executable.");
        AddCommand("wipe friendly names", ValType.None, menu, WipeFriendlyNaemes,
                   "Wipe all loaded friendly names. Does not affect the friendly names file.");
        AddCommand("create database", ValType.None, menu, CreateDatabase,
                   "Create a new database on the localhost\\SQLEXPRESS server.");
        AddCommand("delete database", ValType.None, menu, DeleteDatabase,
                   "Delete the BridgeOps database on the localhost\\SQLEXPRESS server.");

        // Data
        menu = MENU_DATA;
        AddCommand("import", ValType.None, menu, ImportAuto,
                   "Import organisations from a Organisations.csv and assets from Assets.csv, " +
                   "followed by the parents defined in each." +
                   "Full file path is required, make sure to read docs before proceeding.");
        AddCommand("import organisations", ValType.String, menu, ImportOrganisations,
                   "Import organisations from a specified .csv file. " +
                   "Full file path is required, make sure to read docs before proceeding.");
        AddCommand("import organisation parents", ValType.String, menu, ImportOrganisationParents,
                   "Import organisation parents from a specified .csv file. " +
                   "Full file path is required, make sure to read docs before proceeding.");
        AddCommand("import assets", ValType.String, menu, ImportAssets,
                   "Import assets from a specified .csv file. Full file path is required, " +
                   "make sure to read docs before proceeding.");
        AddCommand("import asset parents", ValType.String, menu, ImportAssetParents,
                   "Import asset parents from a specified .csv file. Full file path is required, " +
                   "make sure to read docs before proceeding.");
        AddCommand("generate test data", ValType.Int, menu, GenerateTestData,
                   "Outputs C:/Organisations.csv with the specified number of randomly generated organisations, " +
                   "along with C:/Assets.csv filled by assets with randomly assigned parents.");

        // Agent
        menu = MENU_AGENT;
        AddCommand("start", ValType.None, menu, StartAgent,
                   "Start the BridgeOps agent responsible for carrying out requests from the client application.");
        AddCommand("stop", ValType.None, menu, StopAgent,
                   "Stop the BridgeOps agent. Clients will no longer be able to communicate with the server " +
                   "application, and all users will be logged out.");
        AddCommand("status", ValType.None, menu, CheckAgent,
                   "Check to see if agent is running.");
        AddCommand("logout", ValType.String, menu, LogoutUser,
                   "End session for specified user.");
        AddCommand("reset admin password", ValType.None, menu, ResetAdminPassword,
                   "Reset the admin password to 'admin'.");

        // Network
        menu = MENU_NETWORK;
        AddCommand("parse network config", ValType.None, menu, ParseNetworkSettings,
                   "Check to make sure \"" + Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_NETWORK) +
                   "\" is legible for agent. File path is relevent to the path of this executable.");
        AddCommand("create network config", ValType.None, menu, CreateNetworkSettings,
                   "Generate default values and output to \"" + Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_NETWORK) +
                    "\". File path is relevant to the path of this executable.");
    }

    public int ProcessCommand(string command)
    {
        if (commandDefs.ContainsKey(command.ToLower()))
        {
            if ((commandDefs[command].menu == currentMenu || commandDefs[command].menu == MENU_GLOBAL) &&
                commandDefs[command].valType == ValType.None)
                return commandDefs[command].method();
        }
        else // Split up command to see if it's a command that takes a value.
        {
            int valueStart = command.LastIndexOf(' ') + 1;
            string commandString = "";
            string valueString = "";
            int valueInt = 0;
            if (valueStart >= 2 && valueStart < command.Length)
            {
                commandString = command.Remove(valueStart - 1).ToLower(); // Make only the command case-insensitive.
                valueString = command.Substring(valueStart, command.Length - valueStart);
            }
            if (commandDefs.ContainsKey(commandString))
            {
                if (commandDefs[commandString].valType == ValType.String)
                    commandValString = valueString;
                else if (commandDefs[commandString].valType == ValType.Int)
                {
                    if (int.TryParse(valueString, out valueInt))
                    {
                        commandValInt = valueInt;
                        return commandDefs[commandString].method();
                    }
                    else
                    {
                        Writer.Message("Invalid integer value.");
                        return 1;
                    }
                }
                return commandDefs[commandString].method();
            }
            else // Check for menu selection.
            {
                for (int n = 0; n < MENU_LAST_INDEX; ++n)
                    if (command.ToLower() == MenuName(n).ToLower())
                    {
                        currentMenu = n;
                        return 0;
                    }
            }
        }

        Writer.Message("Command not recognised.");

        return 0;
    }

    // Method for easy addition by potential other users of this class.
    public void AddCommand(string commandName, ValType valType, int menu, Func<int> method, string help)
    {
        commandDefs.Add(commandName, new CommandDef(valType, menu, method, help));
    }


    /* Command Methods,
     * ---------------
     * ● All must return an int.
     */

    private int Help()
    {
        List<string> globalCommands = new List<string>();
        List<string> globalExplanations = new List<string>();
        List<string> currentCommands = new List<string>();
        List<string> currentExplanations = new List<string>();

        foreach (var def in commandDefs)
        {
            if (def.Value.help != "")
            {
                if (def.Value.menu == MENU_GLOBAL)
                {
                    globalCommands.Add(def.Key);
                    if (def.Value.valType == ValType.String)
                        globalCommands[globalCommands.Count - 1] += " [string]";
                    else if (def.Value.valType == ValType.Int)
                        globalCommands[globalCommands.Count - 1] += " [int]";
                    globalExplanations.Add(def.Value.help);
                }
                else if (def.Value.menu == currentMenu)
                {
                    currentCommands.Add(def.Key);
                    if (def.Value.valType == ValType.String)
                        currentCommands[currentCommands.Count - 1] += " [string]";
                    else if (def.Value.valType == ValType.Int)
                        currentCommands[currentCommands.Count - 1] += " [int]";
                    currentExplanations.Add(def.Value.help);
                }
            }
        }

        if (globalCommands.Count > 0)
        {
            Writer.Header("Global Commands");
            for (int n = 0; n < globalCommands.Count; ++n)
                Writer.HelpItem(globalCommands[n], globalExplanations[n]);
        }

        if (currentCommands.Count > 0)
        {
            Writer.Header(Capitalise(CurrentMenuName()) + " Menu Commands");
            for (int n = 0; n < currentCommands.Count; ++n)
                Writer.HelpItem(currentCommands[n], currentExplanations[n]);
        }

        Writer.Message("");

        return 0;
    }

    private int Exit()
    {
        Writer.Message("\nAre you sure?");
        if (Writer.YesNo())
            return -1;
        else
        {
            Writer.Message("Cancelled.");
            return 0;
        }
    }

    //   D A T A B A S E

    private int LoadTypeOverrides()
    {
        //Writer.Message("View each override success or failure as the file is read?");
        //dbCreate.showFileReadSuccesses = Writer.YesNo();
        dbCreate.LoadOverridesFromFile();

        return 0;
    }
    private int CreateTypeOverrides()
    {
        Writer.Message("If " + Glo.CONFIG_TYPE_OVERRIDES +
                       " is already present, this will overwrite it with default values. Continue?");
        if (Writer.YesNo())
            fieldDefs.UnloadTxtStringToOverridesFile(fieldDefs.UnloadToTxtOverrideString());
        else
            Writer.Message("Cancelled.");

        return 0;
    }
    private int ViewCurrentValuesWithOverrides()
    {
        fieldDefs.PrintToConsole();

        return 0;
    }
    private int ResetTypeOverrides()
    {
        dbCreate.ResetTypeOverrides();
        return 0;
    }

    private int LoadColumnAdditions()
    {
        //Writer.Message("View each column success or failure as the file is read?");
        //dbCreate.LoadAdditionalColumns(Writer.YesNo());
        dbCreate.LoadAdditionalColumns(true);

        return 0;
    }
    private int CreateColumnAdditions()
    {
        Writer.Message("If " + Glo.CONFIG_COLUMN_ADDITIONS +
                       " is already present, this will overwrite it with default values. Continue?");
        if (Writer.YesNo())
            fieldDefs.CreateNewColumnAdditionsTemplate();

        return 0;
    }
    private int WipeColumnAdditions()
    {
        dbCreate.WipeColumnAdditions();
        return 0;
    }

    List<string[]> friendlyNames = new();
    private int LoadFriendlyNames()
    {
        dbCreate.friendlyNames.Clear();

        if (File.Exists(Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_FRIENDLY_NAMES)))
        {
            string[] lines = File.ReadAllLines(Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_FRIENDLY_NAMES));
            int relevantLinesFound = 0;
            for (int l = 0; l < lines.Length; ++l)
            {
                if (lines[l].Length >= 8 && !lines[l].StartsWith('#'))
                {
                    ++relevantLinesFound;
                    string[] names = lines[l].Split(";;");
                    if (names.Length == 3)
                    {
                        names[1] = names[1].Replace(" ", "_");
                        if (names[0].Length > 0 || names[1].Length > 0 || names[2].Length > 0)
                        {
                            if (!Glo.Fun.ColumnRemovalAllowed(names[0], names[1].Replace(" ", "_")))
                            {
                                if (Glo.Fun.ColumnRemovalAllowed(names[0], names[2].Replace(" ", "_")))
                                {
                                    bool alreadyUsed = false;
                                    foreach (string[] sa in dbCreate.friendlyNames)
                                        if (sa[0] == names[0] &&
                                            (sa[1].Replace(" ", "_") == names[1].Replace(" ", "_") ||
                                             sa[2].Replace(" ", "_") == names[2].Replace(" ", "_")))
                                        {
                                            alreadyUsed = true;
                                            break;
                                        }
                                    if (!alreadyUsed)
                                    {
                                        dbCreate.friendlyNames.Add(names);
                                        Writer.Affirmative($"'{names[0]}.{names[1]}' will display as '{names[2]}'");
                                    }
                                    else
                                        Writer.Negative($"Line {l} tried to use a previously assigned name.");
                                }
                                else
                                    Writer.Negative($"Line {l} tried to use an existing column name as a friendly " +
                                                     "name.");
                            }
                            else
                                Writer.Negative("Line " + l + " stated an invalid table or column name.");
                        }
                        else
                            Writer.Negative("Line " + l + " stated an empty name.");
                    }
                    else
                        Writer.Negative("Line " + l + " had too " + (names.Length < 3 ? "few" : "many") + " values.");
                }
            }

            if (relevantLinesFound == 0)
                Writer.Negative("No directives found in file.");
        }
        else
        {
            Writer.Negative(Glo.CONFIG_FRIENDLY_NAMES + " not found.");
        }

        return 0;
    }
    private int CreateFriendlyNames()
    {
        Writer.Message("If " + Glo.CONFIG_FRIENDLY_NAMES +
                       " is already present, this will overwrite it. Continue?");
        if (Writer.YesNo())
        {
            File.WriteAllText(Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_FRIENDLY_NAMES),
                            "# To use this file, simply write the table name, followed by the column name, followed" + Glo.NL +
                            "# by the name you wish to be used, separated by ';;'. For example (omitting  the '# '):" +
                            Glo.DNL + "# TableName;;ColumnName;;Your Custom Field Name" +
                            Glo.NL + "# Food;;Bananas;;Yellow Fruit" +
                            Glo.NL + "# Food;;Raisins;;Dried Grapes" +
                            Glo.DNL + "# Only some columns in the Organisation, Asset, Contact and Conference tables are supported." +
                            Glo.DNL + "# This file supercedes the column names stated in " +
                            Glo.CONFIG_COLUMN_ADDITIONS + Glo.DNL);
        }

        return 0;
    }
    private int WipeFriendlyNaemes()
    {
        dbCreate.friendlyNames = new();
        Writer.Affirmative("Friendly names list reset.");
        return 0;
    }

    private int CreateDatabase()
    {
        dbCreate.CreateDatabase();

        return 0;
    }
    private int DeleteDatabase()
    {
        dbCreate.DeleteDatabase();

        return 0;
    }

    private int GenerateTestData()
    {
        Writer.Message("If the Organisations.csv and Assets.csv already exist," +
                       "this will overwrite them. Continue?");
        if (!Writer.YesNo())
            return 0;

        string[] parents = new string[] { "Earth Spacedock",
                                          "Mars Colony",
                                          "Titan Shipyards",
                                          "Europa Deep Exploration Centre" };
        StringBuilder o = new($"TEXT,TEXT,TEXT,TEXT,TEXT{Glo.NL}" +
                              $"Organisation_Reference,Parent_Reference,Organisation_Name,Dial_No,Notes{Glo.NL}");
        StringBuilder a = new($"TEXT,TEXT,TEXT{Glo.NL}Asset_Reference,Organisation_Reference,Notes{Glo.NL}");

        o.AppendLine("Sol Space Agency");
        o.AppendLine($"{parents[0]},Sol Space Agency,{parents[0]}");
        o.AppendLine($"{parents[1]},Sol Space Agency,{parents[1]}");
        o.AppendLine($"{parents[2]},Sol Space Agency,{parents[2]}");
        o.AppendLine($"{parents[3]},Sol Space Agency,{parents[3]}");

        string[] orgNotes = new string[] { "Installed with no issues.",
                                           "Installed on second attempt with no issues.",
                                           "\"Installed, engineer noted minor packet loss on tests, quality unaffected.\"",
                                           "\"Installed with packet loss. Quality effected, user happy enough.\"" };

        int assetIndex = 0;

        Random r = new();
        for (int i = 0; i < commandValInt; ++i)
        {
            string parent = parents[r.Next(0, 4)];
            o.AppendLine($"SSA{i},{parent},{parent} SSA{i},{i},{orgNotes[r.Next(0, 4)]}");

            int assets = r.Next(0, 10);
            for (int j = 0; j < assets; ++j)
                a.AppendLine($"42-{assetIndex++},SSA{i},Tested and functional at installed location.");
        }

        try
        {
            string folder = Glo.Fun.ApplicationFolder("Data Import");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "Organisations.csv"), o.ToString());
            File.WriteAllText(Path.Combine(folder, "Assets.csv"), a.ToString());
        }
        catch
        {
            Writer.Negative("Failed to write files. Run the console as administrator and try again.");
            return 1;
        }

        Console.WriteLine();
        Writer.Affirmative("Files written successfully.");

        return 0;
    }

    // These methods are barely optimised due to time restrictions and how often this feature is likely to be used.
    // Keep "Organisation" and "Asset" capitalised.
    private int ImportAuto()
    {
        commandValString = "Organisations.csv";
        if (ImportOrganisations() == 1)
            return 1;
        if (ImportOrganisationParents() == 1)
            return 1;
        commandValString = "Assets.csv";
        if (ImportAssets() == 1)
            return 1;
        if (ImportAssetParents() == 1)
            return 1;

        return 0;
    }
    private int ImportOrganisations() { return ImportRecords("Organisation", Glo.Tab.ORGANISATION_REF); }
    private int ImportAssets() { return ImportRecords("Asset", Glo.Tab.ASSET_REF); }
    private int ImportRecords(string table, string refKey)
    {
        commandValString = commandValString.Replace("\"", "");
        string fileName = Path.Combine(Glo.PathImportFiles, commandValString);
        if (!File.Exists(fileName))
        {
            Writer.Negative("\nFile not found.");
            return 1;
        }

        // This is the simplest way I can find to parse a CSV file without a third party library.
        Microsoft.VisualBasic.FileIO.TextFieldParser parser = new(fileName);
        parser.TextFieldType = Microsoft.VisualBasic.FileIO.FieldType.Delimited;
        parser.SetDelimiters(",");

        // The first line should be the types, so parse this first.
        string[]? types = parser.ReadFields();
        if (types == null)
        {
            Writer.Negative("\nNo types row found. Please see docs for explanation.");
            parser.Close();
            return 1;
        }
        foreach (string type in types)
        {
            if (type != "TEXT" && type != "NUMBER" && type != "DATETIME")
            {
                Writer.Negative("\nTypes must be TEXT, NUMBER or DATETIME.");
                parser.Close();
                return 1;
            }
        }

        // The second line should be the header names, so parse this second.
        string[]? headers = parser.ReadFields();
        if (headers == null || !headers.Contains(refKey))
        {
            Writer.Negative("\nNo " + refKey + " header found. This is a necessary key for the " +
                            table + " table and must be present.");
            parser.Close();
            return 1;
        }
        // Record the ID index, necessary for some error reporting below.
        int idIndex = -1; // This code cannot end with -1, was we already made sure primaryKey is present above.
        for (int i = 0; i < headers.Length; ++i)
            if (headers[i] == refKey)
            {
                idIndex = i;
                break;
            }

        // Cycle through the next rows one by one, carrying out inserts. There are faster ways to accomplish this,
        // but they could lean on RAM quite heavily.
        int rowNum = 0;
        int successes = 0;
        List<List<string>> failed = new();

        // Set parent organisation ID to null. User can and should load parents separately.
        bool parentPresent = false;
        int parentIndex = -1;
        if (table == "Organisation")
        {
            for (int i = 0; i < headers.Length; ++i)
                if (headers[i] == Glo.Tab.PARENT_REF)
                {
                    parentIndex = i;
                    break;
                }
        }
        else // if Asset
        {
            for (int i = 0; i < headers.Length; ++i)
                if (headers[i] == Glo.Tab.ORGANISATION_REF)
                {
                    parentIndex = i;
                    break;
                }
        }
        if (parentIndex != -1)
            parentPresent = true;

        Writer.Message($"\nAttempting {table.ToLower()} inserts from {commandValString}...");

        // Create a list of register column names, as these will all want setting to 1.
        string registersOn = "";
        List<string> headersRegister = new(headers);
        List<string> allTableColumnNames = dbCreate.GetAllColumnNames(table + "Change");
        foreach (string s in allTableColumnNames)
            if (s.EndsWith(Glo.Tab.CHANGE_SUFFIX))
            {
                // There will always be a value in front of the list of 1's due to an ID needing to be present.
                registersOn += ",1";
                headersRegister.Add(s);
            }
        headersRegister.AddRange(new string[] { Glo.Tab.LOGIN_ID, Glo.Tab.CHANGE_REASON, Glo.Tab.CHANGE_TIME });
        headersRegister.Add(table == "Organisation" ? Glo.Tab.ORGANISATION_ID : Glo.Tab.ASSET_ID);
        string register = "Imported " + (table == "Organisation" ? "organisation." : "asset.");
        registersOn += $",1,'{register}', "; // 1 is always admin due to the database creation process.

        while (!parser.EndOfData)
        {
            ++rowNum;
            try
            {
                string[]? readFields = parser.ReadFields();
                string[] row = new string[headers.Length];
                if (readFields == null)
                {
                    Writer.Negative("Line " + rowNum + " could not be read.");
                    continue;
                }

                Array.Copy(readFields, row, readFields.Length);

                string[] rowUnchanged = (string[])row.Clone();

                string insertInto = "INSERT INTO " + table + " (";
                string insertRegisterInto = "INSERT INTO " + table + "Change (";
                string values = ") VALUES (";

                // Add quotes to text fields.
                for (int i = 0; i < row.Length; ++i)
                {
                    if (row[i] == "" || row[i] == null)   // NULL
                        row[i] = "NULL";
                    else if (types[i] == "TEXT") // TEXT
                        row[i] = "'" + row[i].Replace("'", "''") + "'";
                    else if (types[i] == "NUMBER") // NUMBER
                        row[i] = row[i].Replace("'", "''");
                    else // DATETIME
                    {
                        long unixTimestamp;
                        DateTime dateTime;
                        if (long.TryParse(row[i], out unixTimestamp))
                        {
                            DateTime test = new DateTime(1686830167);
                            if (unixTimestamp == 0)
                                row[i] = "NULL";
                            else
                            {
                                DateTimeOffset dto = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
                                row[i] = $"'{SqlAssist.DateTimeToSQL(dto.DateTime, false)}'";
                            }
                        }
                        else if (DateTime.TryParse(row[i], out dateTime))
                            row[i] = $"'{SqlAssist.DateTimeToSQL(dateTime, false)}'";
                    }
                }

                if (parentPresent)
                    row[parentIndex] = "NULL";

                string rowString = string.Join(',', row);

                // Create a transaction for the inserts into the main and change tables.
                StringBuilder bldr = new StringBuilder("BEGIN TRANSACTION; BEGIN TRY ");
                bldr.Append(insertInto + string.Join(',', headers) + values + rowString + "); ");
                bldr.Append(insertRegisterInto + string.Join(',', headersRegister) +
                            values + rowString + registersOn +
                            "'" + SqlAssist.DateTimeToSQL(DateTime.Now, false) + "', SCOPE_IDENTITY()); ");
                bldr.Append("COMMIT TRANSACTION; END TRY BEGIN CATCH ROLLBACK TRANSACTION; THROW; END CATCH;");

                // Attempt insert, and if it fails, store the row for review by the user.
                if (!dbCreate.SendCommandSQL(bldr.ToString(), true))
                {
                    List<string> failedRow = new(rowUnchanged);
                    failedRow.Add(dbCreate.lastSqlError);
                    failed.Add(failedRow);
                    PrintCSVReadDot(false);
                }
                else
                {
                    ++successes;
                    PrintCSVReadDot(true);
                }
            }
            catch
            {
                Writer.Negative("Line " + rowNum + " could not be read.");
            }
        }
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine();

        // Write any failed inserts to a CSV file for the user to review.
        if (failed.Count > 0)
        {
            string errorFileName = GetErrorCSVPath(fileName);
            string count = failed.Count == 1 ? "There was 1 failure." : "There were " + failed.Count + " failures.";
            Writer.Negative(count + " Generating " + errorFileName + " for review.");


        TryFileWrite:
            try
            {
                File.WriteAllText(errorFileName, string.Join(',', types) + Glo.NL);
            }
            catch
            {
                Writer.Message("Could not write to " + errorFileName + "\nMake sure the file is not currently open " +
                               "and press any key to try again.", ConsoleColor.Red);
                Console.ReadKey(true);
                goto TryFileWrite;
            }
            File.AppendAllText(errorFileName, string.Join(',', headers) + Glo.NL);
            foreach (List<string> line in failed)
            {
                // Make data safe for CSV file again.
                for (int i = 0; i < line.Count; ++i)
                    line[i] = Glo.Fun.MakeSafeForCSV(line[i]);
                File.AppendAllText(errorFileName, string.Join(',', line) + Glo.NL);
            }
        }
        if (failed.Count != rowNum)
            Writer.Affirmative(successes + " inserts carried out successfully.");

        parser.Close();
        return 0;
    }
    private int ImportOrganisationParents() { return ImportRecordParents("Organisation", Glo.Tab.ORGANISATION_REF); }
    private int ImportAssetParents() { return ImportRecordParents("Asset", Glo.Tab.ASSET_REF); }
    private int ImportRecordParents(string table, string refKey)
    {
        commandValString = commandValString.Replace("\"", "");
        string fileName = Path.Combine(Glo.PathImportFiles, commandValString);

        if (!File.Exists(fileName))
        {
            Writer.Negative("\nFile not found.");
            return 1;
        }

        // This is the simplest way I can find to parse a CSV file without a third party library.
        Microsoft.VisualBasic.FileIO.TextFieldParser parser = new(fileName);
        parser.TextFieldType = Microsoft.VisualBasic.FileIO.FieldType.Delimited;
        parser.SetDelimiters(",");

        // The first line should be the types, so parse this first.
        string[]? types = parser.ReadFields();
        if (types == null)
        {
            Writer.Negative("\nNo types row found. Please see docs for explanation.");
            parser.Close();
            return 1;
        }
        foreach (string type in types)
        {
            if (type != "TEXT" && type != "NUMBER" && type != "DATETIME")
            {
                Writer.Negative("\nTypes must be TEXT, NUMBER or DATETIME.");
                parser.Close();
                return 1;
            }
        }

        // The second line should be the header names, so parse this second.
        string[]? headers = parser.ReadFields();
        string parentColName = (table == "Organisation" ? Glo.Tab.PARENT_REF : Glo.Tab.ORGANISATION_REF);
        if (headers == null || !(headers.Contains(refKey) && headers.Contains(parentColName)))
        {
            Writer.Negative("\n" + refKey + " and " + parentColName + " columns must be present.");
            parser.Close();
            return 1;
        }

        // Cycle through the next rows one by one, carrying out updates. There are faster ways to accomplish this,
        // but they could lean on RAM quite heavily.
        int rowNum = 0;
        int successes = 0;
        List<List<string>> failed = new();

        // Set parent organisation ID to null. User can and should load parents separately.
        int parentIndex = -1;
        int idIndex = -1;
        if (table == "Organisation")
        {
            for (int i = 0; i < headers.Length && (parentIndex == -1 || idIndex == -1); ++i)
                if (headers[i] == parentColName)
                    parentIndex = i;
                else if (headers[i] == refKey)
                    idIndex = i;
        }
        else // if Asset
        {
            for (int i = 0; i < headers.Length && (parentIndex == -1 || idIndex == -1); ++i)
                if (headers[i] == parentColName)
                    parentIndex = i;
                else if (headers[i] == refKey)
                    idIndex = i;
        }

        Writer.Message($"\nAttempting {table.ToLower()} parent updates from {commandValString}...");

        while (!parser.EndOfData)
        {
            ++rowNum;

            try
            {
                string[]? readFields = parser.ReadFields();
                string[] row = new string[headers.Length];
                if (readFields == null)
                {
                    Writer.Negative("Line " + rowNum + " could not be read.");
                    continue;
                }

                Array.Copy(readFields, row, readFields.Length);

                string[] rowUnchanged = (string[])row.Clone();

                for (int i = 0; i < row.Length; ++i)
                {
                    if (row[i] == "" || row[i] == null)   // NULL
                        row[i] = "NULL";
                    else if (types[i] == "TEXT") // TEXT
                        row[i] = "'" + row[i].Replace("'", "''") + "'";
                    else // NUMBER
                        row[i] = row[i].Replace("'", "''");
                }

                if (row[parentIndex] == "NULL")
                    continue;

                // Create a transaction for the inserts into the main and change tables.
                StringBuilder bldr = new StringBuilder("BEGIN TRANSACTION; BEGIN TRY ");
                string primaryKey = table == "Organisation" ? Glo.Tab.ORGANISATION_ID : Glo.Tab.ASSET_ID;
                bldr.Append($"DECLARE @id INT; " +
                            $"SELECT @id = {primaryKey} FROM {table} " +
                            $"WHERE {refKey} = {row[idIndex]}; " +
                             "UPDATE " + table +
                             " SET " + parentColName + " = " + row[parentIndex] +
                             " WHERE " + refKey + " = " + row[idIndex] + "; ");
                bldr.Append($"INSERT INTO {table}Change " +
                                   $"({primaryKey}, " +
                                   $"{parentColName}, " +
                                   $"{parentColName + Glo.Tab.CHANGE_SUFFIX}, " +
                                   $"{Glo.Tab.LOGIN_ID}, " +
                                   $"{Glo.Tab.CHANGE_REASON}, " +
                                   $"{Glo.Tab.CHANGE_TIME})" +
                            $"VALUES(@id, " +
                                   $"{row[parentIndex]}, " +
                                   $"1, " + // Switch on the register.
                                   $"1, " + // Admin account should always be 1 after database creation.
                                   $"'Set parent to imported value, '{row[parentIndex]}'.', " +
                                   $"'{SqlAssist.DateTimeToSQL(DateTime.Now, false)}'); ");
                bldr.Append("COMMIT TRANSACTION; END TRY BEGIN CATCH ROLLBACK TRANSACTION; THROW; END CATCH;");

                // Attempt update, and if it fails, store the row for review by the user.
                if (!dbCreate.SendCommandSQL(bldr.ToString(), true))
                {
                    List<string> failedRow = new(rowUnchanged);
                    failedRow.Add(dbCreate.lastSqlError);
                    failed.Add(failedRow);
                    PrintCSVReadDot(false);
                }
                else
                {
                    ++successes;
                    PrintCSVReadDot(true);
                }
            }
            catch
            {
                Writer.Negative("Line " + rowNum + " could not be read.");
                continue;
            }
        }
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine();

        // Write any failed inserts to a CSV file for the user to review.
        if (failed.Count > 0)
        {
            string errorFileName = GetErrorCSVPath(fileName);
            string count = failed.Count == 1 ? "There was 1 recorded failure." : "There were " + failed.Count +
                                                                                 " recorded failures.";
            Writer.Negative(count + " Generating " + errorFileName + " for review.");

        TryFileWrite:
            try
            {
                File.WriteAllText(errorFileName, string.Join(',', types) + Glo.NL);
            }
            catch
            {
                Writer.Message("Could not write to " + errorFileName + "\nMake sure the file is not currently open " +
                               "and press any key to try again.", ConsoleColor.Red);
                Console.ReadKey(true);
                goto TryFileWrite;
            }
            File.AppendAllText(errorFileName, string.Join(',', headers) + Glo.NL);
            foreach (List<string> line in failed)
            {
                // Make data safe for CSV file again.
                for (int i = 0; i < line.Count; ++i)
                    line[i] = Glo.Fun.MakeSafeForCSV(line[i]);
                File.AppendAllText(errorFileName, string.Join(',', line) + Glo.NL);
            }
        }
        if (failed.Count != rowNum)
            Writer.Affirmative(successes + " updates carried out successfully.");

        parser.Close();
        return 0;
    }
    private string GetErrorCSVPath(string path)
    {
        if (path.ToLower().EndsWith(".csv"))
            path = path.Remove(path.Length - 4);
        return path + "-import-errors.csv";
    }
    private void PrintCSVReadDot(bool success)
    {
        // Colour must be reset to white outside of this function.
        if (success) Console.ForegroundColor = ConsoleColor.DarkGreen;
        else Console.ForegroundColor = ConsoleColor.Red;
        Console.Write(":");
        Console.ForegroundColor = ConsoleColor.White;
    }


    //   P R O C E S S

    Process? process;
    string agentPath = Glo.PATH_AGENT;
    string agentExe = Glo.EXE_AGENT;
    string agentRelativePath { get { return agentPath + agentExe; } }
    string agentFullPath { get { return new FileInfo(agentRelativePath).FullName; } }

    private int StartAgent()
    {
        if (!GetProcess(false))
        {
            try
            {
                process = new Process();
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.FileName = agentFullPath;

                process.Start();

                Writer.Affirmative("Process started.");
            }
            catch
            {
                process = null;
                Writer.Negative(agentExe + " could not be found in this application's directory.");
            }
        }
        else
        {
            Writer.Negative("Process is already running.");
        }
        return 0;
    }

    private int CheckAgent()
    {
        if (GetProcess(true))
        {
            Writer.Affirmative("Process is running.\nGetting list of open client sessions...");

            NamedPipeClientStream server = sr.NewClientNamedPipe(Glo.PIPE_CONSOLE);
            try
            {
                server.Connect(2);
            }
            catch (Exception e)
            {
                Writer.Negative("Unable to connect to agent. See error:");
                Writer.Message(e.Message);
            }

            if (server.IsConnected)
            {
                // Ping the server.
                server.WriteByte(Glo.CONSOLE_CLIENT_LIST);

                ConnectedClients? clients = sr.Deserialise<ConnectedClients>(sr.ReadString(server));
                if (clients != null && clients.connectedClients.Count > 0)
                {
                    Writer.Message("\nCurrent sessions:");
                    foreach (var client in clients.connectedClients)
                        Writer.Affirmative(client.ip + WhiteSpace(17 - client.ip.Length) + client.username);
                }
                else
                    Writer.Message("There are no client sessions currently open.");

                server.Close();
            }
        }
        else
            Writer.Negative("Agent process is not currently running. Type \"start\" to begin.");
        return 0;
    }

    private int LogoutUser()
    {
        if (GetProcess(true))
        {
            NamedPipeClientStream server = sr.NewClientNamedPipe(Glo.PIPE_CONSOLE);
            try
            {
                server.Connect(2);
            }
            catch (Exception e)
            {
                Writer.Negative("Unable to connect to agent. See error:");
                Writer.Message(e.Message);
            }

            if (server.IsConnected)
            {
                server.WriteByte(Glo.CONSOLE_LOGOUT_USER);
                sr.WriteAndFlush(server, commandValString);
                try
                {
                    if (server.ReadByte() == 0)
                    {
                        Writer.Affirmative(commandValString + " logged out.");
                    }
                    else
                    {
                        Writer.Negative("User not found.");
                    }
                }
                catch (Exception e)
                {
                    Writer.Message("No response from agent, see error:", ConsoleColor.Red);
                    Writer.Message(e.Message, ConsoleColor.Red);
                }
            }
        }
        else
            Writer.Negative("Agent process is not currently running. Type \"start\" to begin.");

        return 0;
    }

    // SECURITY RISK, REMOVE FOR DEPLOYMENT NOT ON AN AIR-GAPPED NETWORK.
    private int ResetAdminPassword()
    {
        string command = "UPDATE Login " +
                        $"SET {Glo.Tab.LOGIN_PASSWORD} = {SqlAssist.HashBytes("admin")} " +
                        $"WHERE {Glo.Tab.LOGIN_ID} = 1;";
        if (dbCreate.SendCommandSQL(command))
            Writer.Affirmative("Admin password successfully reset.");
        else
            Writer.Affirmative("Something went wrong, check connection to SQL datbase " +
                               "and try restarting the console.");
        return 0;
    }

    private int StopAgent()
    {
        if (process != null)
        {
            if (GetProcess(false))
            {
                try
                {
                    process.Kill();
                    process = null;
                    Writer.Affirmative("Process stopped.");
                }
                catch (Exception e)
                {
                    Writer.Message("Something is preventing the process from terminating. See error:", ConsoleColor.Red);
                    Writer.Message(e.Message);
                }
            }
            else
            {
                Writer.Negative("It looks like the process was started, but has since terminated. See " +
                                Glo.PathAgentErrorLog + " for details.");
            }
        }
        else
        {
            Writer.Negative("Process has not been started.");
        }
        return 0;
    }

    public bool GetProcess(bool acquire)
    {
        Process[] processes = Process.GetProcessesByName(agentExe.Replace(".exe", ""));

        for (int n = 0; n < processes.Length; ++n)
        {
            // Confirm we're definitely looking at the right process.

            ProcessModule? procMod = processes[n].MainModule;
            if (procMod != null && procMod.FileName == agentFullPath)
            {
                if (acquire)
                    process = processes[n];
                return true;
            }
        }

        return false;
    }


    //   N E T W O R K

    private int CreateNetworkSettings()
    {
        Writer.Message("If " + Glo.CONFIG_NETWORK +
                       " is already present, this will overwrite it with default values. Continue?");
        if (Writer.YesNo())
            CreateNetworkSettings(true);
        else
            Writer.Message("Cancelled.");

        return 0;
    }
    public void CreateNetworkSettings(bool defaultSettings)
    {
        string settings = Glo.NETWORK_SETTINGS_PORT_INBOUND + Glo.PORT_INBOUND_DEFAULT + Glo.NL +
                          Glo.NETWORK_SETTINGS_PORT_OUTBOUND + Glo.PORT_OUTBOUND_DEFAULT + Glo.NL;

        // Agent also reads from this file, so 
        try
        {
            File.WriteAllText(Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_NETWORK), settings);
            Writer.Affirmative("Network config file written successfully.");
        }
        catch (Exception e)
        {
            Writer.Message("Could not create file, see error:", ConsoleColor.Red);
            Writer.Message(e.Message, ConsoleColor.Red);
        }
    }


    private int ParseNetworkSettings() // Used by the command, bypassed by BridgeOpsConsole().
    {
        Writer.Message("View each success or failure as the file is read?");
        ParseNetworkSettings(Writer.YesNo());
        Writer.Message("Agent must be restarted for any config changes to take effect.");

        return 0;
    }
    public void ParseNetworkSettings(bool detailed)
    {
        Writer.Message("\nParsing " + Glo.CONFIG_NETWORK + "...");

        string[] settings = File.ReadAllLines(Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_NETWORK));

        int inboundPort = Glo.PORT_INBOUND_DEFAULT; // Store inboundPort to make sure outboundPort isn't the same.
        int iVal;
        int valuesSet = 0;
        int differedFromDefaults = 0;
        foreach (string s in settings)
        {
            if (s.Length > Glo.NETWORK_SETTINGS_LENGTH && !s.StartsWith("# "))
            {
                if (s.StartsWith(Glo.NETWORK_SETTINGS_PORT_INBOUND))
                {
                    if (int.TryParse(s.Substring(Glo.NETWORK_SETTINGS_LENGTH,
                                                 s.Length - Glo.NETWORK_SETTINGS_LENGTH), out iVal) &&
                        iVal >= 1025 && iVal <= 65535)
                    {
                        inboundPort = iVal;
                        ++valuesSet;
                        if (iVal != Glo.PORT_INBOUND_DEFAULT) ++differedFromDefaults;
                        if (detailed) Writer.Affirmative("Inbound port will be read as " + iVal);
                    }
                    else if (detailed)
                        Writer.Negative("Inbound port must be an integral value between 1025 and 65535.");
                }
                else if (s.StartsWith(Glo.NETWORK_SETTINGS_PORT_OUTBOUND))
                {
                    if (int.TryParse(s.Substring(Glo.NETWORK_SETTINGS_LENGTH,
                                                 s.Length - Glo.NETWORK_SETTINGS_LENGTH), out iVal) &&
                        iVal >= 1025 && iVal <= 65535)
                    {
                        if (iVal != inboundPort)
                        {
                            ++valuesSet;
                            if (iVal != Glo.PORT_OUTBOUND_DEFAULT) ++differedFromDefaults;
                            if (detailed) Writer.Affirmative("Outbound port will be read as " + iVal);
                        }
                        else
                            Writer.Negative("Outbound port cannot be the same as inbound port.");
                    }
                    else if (detailed)
                        Writer.Negative("Outbound port must be an integral value between 1025 and 65535.");
                }
            }
        }

        if (!detailed)
        {
            if (valuesSet > 0)
            {

                Writer.Affirmative(valuesSet + " settings read successfully, " +
                                   differedFromDefaults + " differed from default values.");
            }
            else
                Writer.Negative("0 settings read successfully.");
        }
        else
            Writer.Neutral(differedFromDefaults + " settings differed from default values");
    }


    string WhiteSpace(int length)
    {
        string ws = "";
        for (int n = 0; n < length; ++n)
            ws += " ";
        return ws;
    }
}