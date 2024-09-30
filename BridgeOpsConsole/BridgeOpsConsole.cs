using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.Drawing;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;


internal class BridgeOpsConsole
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("\n///");
        Writer.Message("   B R I D G E   M A N A G E R", ConsoleColor.White);
        Writer.Message($"      {Glo.VersionNumber}\n", ConsoleColor.DarkGray);

        // Set current working directory, as some command need this.
        string? currentDir = System.Reflection.Assembly.GetExecutingAssembly().Location;
        currentDir = Path.GetDirectoryName(currentDir);
        if (currentDir == null)
        {
            Writer.Negative("Could not ascertain current working directory.");
            Writer.Message("Press any key to exit.");
            Console.ReadKey();
            return 1;
        }
        Environment.CurrentDirectory = currentDir;

        // Create application folders if needed.
        Glo.Fun.ExistsOrCreateFolder(Glo.PathConfigFiles);

        // Read or create SQL server name file.
        string serverNameFile = Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_SQL_SERVER_NAME);
        if (File.Exists(serverNameFile))
        {
            DatabaseCreator.sqlServerName = File.ReadAllLines(serverNameFile)[0];
            Writer.Affirmative($"SQL Server name read as {DatabaseCreator.sqlServerName}");
        }
        else
        {
            File.WriteAllText(serverNameFile,
                              Glo.SQL_SERVER_NAME_DEFAULT);
            Writer.Neutral($"{Glo.CONFIG_SQL_SERVER_NAME} not found. Created with default name.");
        }

        FieldDefs fieldDefs = new FieldDefs();
        fieldDefs.DefineFields();
        DatabaseCreator dbCreate = new DatabaseCreator(fieldDefs);

        // Set up the console.
        ConsoleController con = new ConsoleController(dbCreate, fieldDefs);

        // Set up agent process.
        Console.WriteLine("");
        if (con.GetProcess(true))
            Writer.Affirmative("Agent process found.");
        else
            Writer.Negative("Agent process not yet running.");

        if (File.Exists(Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_NETWORK)))
            con.ParseNetworkSettings(false);
        else
            Writer.Negative("Network config file not found.");

        // Run the program.
        while (true)
        {
            Writer.Prompt();
            string? command = Console.ReadLine();
            if (command != null)
            {
                int i = con.ProcessCommand(command);

                if (i == -1) // Exit signal.
                {
                    return 0;
                }
            }
        }
    }
}