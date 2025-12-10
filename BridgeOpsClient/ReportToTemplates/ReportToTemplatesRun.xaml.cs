using DocumentFormat.OpenXml.Office2010.ExcelAc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace BridgeOpsClient
{
    public partial class ReportToTemplatesRun : CustomWindow
    {
        float[] progressSteps = new float[] { .2f, .2f, .6f };
        private void SetProgress(float percentage)
        {
            bdrProgressBar.Width = bdrProgressBarRail.ActualWidth * percentage;
        }

        List<ReportToTemplates.ReportTag> reportTags;
        string outputDirectory;

        public ReportToTemplatesRun(List<ReportToTemplates.ReportTag> reportTags, string outputDirectory)
        {
            InitializeComponent();
            SetProgress(0); // Rail ActualWidth is 0 due to not being rendered, but it doesn't matter here.

            this.reportTags = reportTags;
            this.outputDirectory = outputDirectory;
        }

        private void Log(string message)
        { Log(message, Brushes.Black); }
        private void Log(string message, Brush brush)
        { lstEventLog.Items.Add(new TextBlock() { Text = message, Foreground = brush }); }

        Dictionary<string, JsonObject?> presetJsonObjects = new();
        Dictionary<string, Dictionary<string, string?>> selectStatements = new();
        Dictionary<string, Dictionary<string, object?>> storedVariables = new();

        // Process starts here.
        private void CustomWindow_ContentRendered(object sender, EventArgs e)
        {
            SelectBuilder.storedVariables.Clear();

            // Load all required presets so that we don't make more calls than necessary to the agent to load
            // individual tabs or repeated queries.
            float p = 0;
            foreach (ReportToTemplates.ReportTag reportTag in reportTags)
            {
                if (!presetJsonObjects.ContainsKey(reportTag.presetName) && reportTag.valid)
                {
                    try
                    {
                        string presetJson;
                        if (PresetLoad(reportTag.presetName, out presetJson))
                            presetJsonObjects.Add(reportTag.presetName, JsonNode.Parse(presetJson)!.AsObject());
                    }
                    catch
                    {
                        // Add to dictionary as null so we don't repeat failed loads.
                        presetJsonObjects.Add(reportTag.presetName, null);
                        Log("Unable to load preset: " + reportTag.presetName);
                    }
                    storedVariables.Add(reportTag.presetName, new());
                    selectStatements.Add(reportTag.presetName, new());
                }

                SetProgress(progressSteps[0] * (p++ / reportTags.Count));
            }

            // Build a dictionaries of select statements by tab name inside an enclosing dictionary of preset names.
            p = 0;
            foreach (ReportToTemplates.ReportTag reportTag in reportTags)
            {
                // Do this first, since there are some continues and a break in play.
                SetProgress(progressSteps[0] + (progressSteps[1] * (p++ / reportTags.Count)));

                if (!reportTag.valid ||
                    presetJsonObjects[reportTag.presetName] == null ||
                    selectStatements[reportTag.presetName].ContainsKey(reportTag.tabName))
                    continue;

                JsonObject? jsonObj = presetJsonObjects[reportTag.presetName]!;
                Dictionary<string, string?> presetStatements = selectStatements[reportTag.presetName];

                for (int i = 0; true; ++i)
                    try
                    {
                        JsonNode? tab;
                        if (jsonObj.TryGetPropertyValue(i.ToString(), out tab))
                        {
                            if (tab!["Name"]!.GetValue<string>() != reportTag.tabName)
                                continue;
                            presetStatements.Add(reportTag.tabName, tab!["Statement"]!.GetValue<string>());
                            if (tab!["Type"]!.GetValue<string>() == "Statement")
                            {
                                string finalStatement;
                                if (PageSelectStatement.InsertParameters(presetStatements[reportTag.tabName]!,
                                                                         out finalStatement,
                                                                         $"{reportTag.presetName} > {reportTag.tabName}",
                                                                         this, storedVariables[reportTag.presetName]))
                                    presetStatements[reportTag.presetName] = finalStatement;
                            }
                        }
                        else
                            break;
                    }
                    catch
                    {
                        presetStatements.Add(reportTag.presetName, null);
                    }
            }
        }

        private bool PresetLoad(string presetName, out string presetJSON)
        {
            try
            {
                lock (App.streamLock)
                {
                    using NetworkStream? stream = App.sr.NewClientNetworkStream(App.sd.ServerEP);
                    {
                        if (stream == null)
                        {
                            App.DisplayError(App.NO_NETWORK_STREAM, this);
                            presetJSON = "";
                            return false;
                        }
                        stream.WriteByte(Glo.CLIENT_PRESET_LOAD);
                        App.sr.WriteAndFlush(stream, App.sd.sessionID);
                        App.sr.WriteAndFlush(stream, presetName);
                        App.sr.WriteAndFlush(stream, Glo.FOLDER_QUERY_BUILDER_PRESETS);

                        int response = App.sr.ReadByte(stream);
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                        {
                            presetJSON = App.sr.ReadString(stream);
                            return true;
                        }
                        if (response == Glo.CLIENT_SESSION_INVALID)
                            App.SessionInvalidated();
                        if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                        {
                            // We can skip the preset load when reverting back to the last selection,
                            // since it was already loaded.
                            throw new Exception(App.sr.ReadString(stream));
                        }
                        throw new Exception("Could not retrieve preset: " + presetName);
                    }
                }
            }
            catch (Exception e)
            {
                presetJSON = e.Message;
                return false;
            }
        }
    }
}
