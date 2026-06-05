using BridgeOpsClient;
using DocumentFormat.OpenXml.Bibliography;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class FileOpener
{
    public static void OpenFile(string path, bool isFile, CustomWindow owner) // isFile indicates file or directory.
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception e)
        {
            App.DisplayError("See error: " + e.Message, "Unable to Open " + (isFile ? "File" : "Directory"), owner);
        }
    }
}