using DocumentFormat.OpenXml.InkML;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public static class Log
{
    private static object errorLogLock = new();
    public static void LogError(string message) { LogError("", message); }
    public static void LogError(string context, string message)
    {
        lock (errorLogLock)
        {
            string error = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
            if (context.Length > 0)
                error += "  " + context;
            error += "  " + message;
            Glo.Fun.ExistsOrCreateFolder(Glo.Fun.ApplicationFolder());
            File.AppendAllText(Glo.PathClientErrorLog, error + Glo.NL);
        }
    }
}
