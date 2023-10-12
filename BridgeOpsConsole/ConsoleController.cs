
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
using System.Diagnostics;
using System.IO.Pipes;
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
    public const int MENU_AGENT = 2;
    public const int MENU_NETWORK = 3;
    // Used by ConsoleWriter.Help() to determine the list. Should always be the highest menu index + 1.
    public const int MENU_LAST_INDEX = 4;

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
                   "Load type overrides from file \"" + Glo.PATH_CONFIG_FILES + Glo.CONFIG_TYPE_OVERRIDES +
                   "\". Filepath is relevant to the path of this executable.");
        AddCommand("create type overrides", ValType.None, menu, CreateTypeOverrides,
                   "Generate default values and output to \"" + Glo.PATH_CONFIG_FILES +
                   Glo.CONFIG_TYPE_OVERRIDES + "\". Filepath is relevant to the path of this executable.");
        AddCommand("reset type verrides", ValType.None, menu, ResetTypeOverrides,
                   "Reset type overrides to default values.");
        AddCommand("view current values", ValType.None, menu, ViewCurrentValuesWithOverrides,
                   "View the list of columns in their respective tables with the currently loaded overrides. Does " +
                   "not include column additions.");
        AddCommand("load column additions", ValType.None, menu, LoadColumnAdditions,
                   "Load column additions from file \"" + Glo.PATH_CONFIG_FILES + Glo.CONFIG_COLUMN_ADDITIONS +
                   "\". Filepath is relevant to the path of this executable.");
        AddCommand("create column additions", ValType.None, menu, CreateColumnAdditions,
                   "Create a new column additions template file under \"" + Glo.PATH_CONFIG_FILES +
                   Glo.CONFIG_COLUMN_ADDITIONS + "\". Filepath is relevant to the path of this executable.");
        AddCommand("wipe column additions", ValType.None, menu, WipeColumnAdditions,
                   "Wipe all loaded column additions. Does not affect the column additions template file.");
        AddCommand("restore column record", ValType.None, menu, RestoreColumnRecord,
                   "Restore the column record used by the Client application for limiting values in fields and " +
                   "registering column additions. Database must first have been created as this process uses the " +
                   "created database as its reference, and does not consider any config files.");
        AddCommand("create friendly names", ValType.None, menu, CreateFriendlyNames,
                   "Create a new friendly names template file under \"" + Glo.PATH_CONFIG_FILES +
                   Glo.CONFIG_FRIENDLY_NAMES + "\". Filepath is relevant to the path of this executable.");
        AddCommand("parse friendly names", ValType.None, menu, ParseFriendlyNames,
                   "Check to make sure \"" + Glo.PATH_CONFIG_FILES + Glo.CONFIG_FRIENDLY_NAMES +
                   "\" is legible for agent. Does not verify stated column names. Filepath is relevent to the path " +
                   "of this executable.");
        AddCommand("create database", ValType.None, menu, CreateDatabase,
                   "Create a new database on the localhost\\SQLEXPRESS server.");
        AddCommand("delete database", ValType.None, menu, DeleteDatabase,
                   "Delete the BridgeOps database on the localhost\\SQLEXPRESS server.");

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

        // Network
        menu = MENU_NETWORK;
        AddCommand("parse network config", ValType.None, menu, ParseNetworkSettings,
                   "Check to make sure \"" + Glo.PATH_CONFIG_FILES + Glo.CONFIG_NETWORK +
                   "\" is legible for agent. Filepath is relevent to the path of this executable.");
        AddCommand("create network config", ValType.None, menu, CreateNetworkSettings,
                   "Generate default values and output to \"" + Glo.PATH_CONFIG_FILES + Glo.CONFIG_NETWORK +
                    "\". Filepath is relevant to the path of this executable.");
    }

    public int ProcessCommand(string command)
    {
        if (commandDefs.ContainsKey(command) && (commandDefs[command].menu == currentMenu ||
            commandDefs[command].menu == MENU_GLOBAL) && commandDefs[command].valType == ValType.None)
            return commandDefs[command].method();
        else // Split up command to see if it's a command that takes a value.
        {
            int valueStart = command.LastIndexOf(' ') + 1;
            string commandString = "";
            string valueString = "";
            int valueInt = 0;
            if (valueStart >= 2 && valueStart < command.Length)
            {
                commandString = command.Remove(valueStart - 1);
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
                        return 0;
                    }
                }
                return commandDefs[commandString].method();
            }
            else // Check for menu selection.
            {
                for (int n = 0; n < MENU_LAST_INDEX; ++n)
                    if (command == MenuName(n).ToLower())
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
        Writer.Message("View each override success or failure as the file is read?");
        dbCreate.showFileReadSuccesses = Writer.YesNo();
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
        Writer.Message("View each column success or failure as the file is read?");
        dbCreate.LoadAdditionalColumns(Writer.YesNo());

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

    private int ParseFriendlyNames()
    {
        if (File.Exists(Glo.PATH_CONFIG_FILES + Glo.CONFIG_FRIENDLY_NAMES))
        {
            string[] lines = File.ReadAllLines(Glo.PATH_CONFIG_FILES + Glo.CONFIG_FRIENDLY_NAMES);
            int relevantLinesFound = 0;
            for (int l = 0; l < lines.Length; ++l)
            {
                if (lines[l].Length >= 8 && !lines[l].StartsWith('#'))
                {
                    ++relevantLinesFound;
                    string[] names = lines[l].Split(";;");
                    if (names.Length == 3)
                    {
                        if (names[0].Length > 0 || names[1].Length > 0 || names[2].Length > 0)
                        {
                            if (names[0] == "Organisation" || names[0] == "Contact" ||
                                names[0] == "Asset" || names[0] == "Conference")
                                Writer.Affirmative("'" + names[1] + "' will display as '" + names[2] + "'");
                            else
                                Writer.Negative("Line " + l + " stated an invalid table name.");
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
            File.WriteAllText(Glo.PATH_CONFIG_FILES + Glo.CONFIG_FRIENDLY_NAMES,
                            "# To use this file, simply write the table name, followed by the column name, followed" +
                            "# by the name you wish to be used, separated by ';;'. For example (omitting  the '# '):" +
                            "\n" + 
                            "\n# TableName;;ColumnName;;Your Custom Field Name" +
                            "\n# Food;;Bananas;;Yellow Fruit" +
                            "\n# Food;;Raisins;;Dried Grapes" +
                            "\n" +
                            "\n# This file supercedes the column names stated in " +
                            Glo.CONFIG_COLUMN_ADDITIONS + ".\n\n");
        }

        return 0;
    }

    private int RestoreColumnRecord()
    {
        dbCreate.RestoreColumnRecord();

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
                                Glo.LOG_ERROR_AGENT + " for details.");
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
        string settings = Glo.NETWORK_SETTINGS_PORT_INBOUND + Glo.PORT_INBOUND_DEFAULT + "\n" +
                          Glo.NETWORK_SETTINGS_PORT_OUTBOUND + Glo.PORT_OUTBOUND_DEFAULT + "\n";

        // Agent also reads from this file, so 
        try
        {
            File.WriteAllText(Glo.PATH_CONFIG_FILES + Glo.CONFIG_NETWORK, settings);
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

        string[] settings = File.ReadAllLines(Glo.PATH_CONFIG_FILES + Glo.CONFIG_NETWORK);

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