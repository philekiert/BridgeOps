﻿using System.Collections.Generic;
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
        Writer.Message("   B R I D G E   O P S\n", ConsoleColor.White);

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

        if (File.Exists(Glo.PATH_CONFIG_FILES + Glo.CONFIG_NETWORK))
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