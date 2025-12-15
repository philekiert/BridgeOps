using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Office2016.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static BridgeOpsClient.ReportToTemplates;

namespace BridgeOpsClient
{
    public partial class ReportToTemplatesRun : CustomWindow
    {
        // progressSteps represent progress chunks between things like running the queries and inserting the data.
        float[] progressSteps = new float[] { .2f, .2f, .2f, .4f };
        int progressStep = 0;
        private void SetProgress(float percentageAlongStep)
        {
            float progress = 0f;
            for (int i = 0; i < progressStep; ++i)
                progress += progressSteps[i];
            progress += progressSteps[progressStep] * percentageAlongStep;
            bdrProgressBar.Width = bdrProgressBarRail.ActualWidth * percentageAlongStep;
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
        Dictionary<string, Dictionary<string, List<List<object?>>>> data = new();

        // Process starts here.
        private void CustomWindow_ContentRendered(object sender, EventArgs e)
        {
            // Build a dictionary of presets and tabs to load.
            Dictionary<string, HashSet<string>> presetsAndTabs = new();
            foreach (ReportToTemplates.ReportTag tag in reportTags)
            {
                if (!tag.valid)
                    continue;
                presetsAndTabs.TryAdd(tag.presetName, new());
                presetsAndTabs[tag.presetName].Add(tag.tabName);
            }

            // Build a dictionary of all loaded presets.
            float p = 0;
            foreach (string presetName in presetsAndTabs.Keys)
            {
                if (!presetJsonObjects.ContainsKey(presetName))
                {
                    try
                    {
                        string presetJson;
                        if (PresetLoad(presetName, out presetJson))
                            presetJsonObjects.Add(presetName, JsonNode.Parse(presetJson)!.AsObject());
                    }
                    catch
                    {
                        // Add to dictionary as null so we don't repeat failed loads.
                        presetJsonObjects.Add(presetName, null);
                        Log("Unable to load preset: " + presetName);
                    }
                    selectStatements.Add(presetName, new());
                }

                SetProgress(p++ / reportTags.Count);
            }

            int totalStatementsToGather = 0;
            foreach (var tabs in presetsAndTabs.Values)
                totalStatementsToGather += tabs.Count;

            // Build a dictionary of select statements by tab name inside an enclosing dictionary of preset names.
            p = 0;
            ++progressStep;
            foreach (string presetName in presetsAndTabs.Keys)
            {
                SelectBuilder.storedVariables.Clear();
                Dictionary<string, object?> presetStoredVariables = new();

                if (presetJsonObjects[presetName] == null)
                    continue;

                JsonObject jsonObj = presetJsonObjects[presetName]!;
                Dictionary<string, string?> presetStatements = selectStatements[presetName];

                foreach (string tabName in presetsAndTabs[presetName])
                {
                    // Do this first, since there are some continues and a break in play.
                    SetProgress(p++ / totalStatementsToGather);

                    // Search through the preset for the tab index we're looking for.
                    int index = 0;
                    while (true)
                    {
                        JsonNode? tab;
                        if (jsonObj.TryGetPropertyValue(index.ToString(), out tab))
                        {
                            if (tab!["Name"]!.GetValue<string>() == tabName)
                                break;
                        }
                        else // The tab index didn't exist in the preset, so we've reached the end and found nothing.
                        {
                            index = -1;
                            break;
                        }
                        ++index;
                    }
                    if (index == -1)
                    {
                        presetStatements.Add(tabName, null);
                        continue;
                    }
                    // Gather the statement for the preset.
                    try
                    {
                        JsonObject tab = jsonObj[index.ToString()]!.AsObject();
                        string rawStatement = tab!["Statement"]!.GetValue<string>();
                        if (tab!["Type"]!.GetValue<string>() == "Statement")
                        {
                            string finalStatement;
                            if (PageSelectStatement.InsertParameters(rawStatement!,
                                                                     out finalStatement,
                                                                     $"{presetName} > {tabName}",
                                                                     this,
                                                                     presetStoredVariables))
                                presetStatements.Add(tabName, finalStatement);
                            else
                            {
                                presetStatements.Add(tabName, null);
                                break;
                            }
                        }
                        else
                            presetStatements.Add(tabName, rawStatement);
                    }
                    catch
                    {
                        presetStatements.Add(tabName, null);
                    }
                }
            }

            // Load the results for all queries into memory. This shouldn't pose an issue, as the returned data would
            // have to be gargantuan to exceed memory capacity.
            p = 0;
            ++progressStep;
            foreach (string presetName in presetsAndTabs.Keys)
            {
                data.Add(presetName, new());
                foreach (string tabName in presetsAndTabs[presetName])
                {
                    data[presetName].Add(tabName, new());
                    if (selectStatements[presetName][tabName] == null)
                        continue;
                    List<List<object?>> dataTemp;
                    if (App.SendSelectStatement(selectStatements[presetName][tabName]!, out _, out dataTemp, this))
                        data[presetName][tabName] = dataTemp;
                }

                // Do this first, since there are some continues and a break in play.
                SetProgress(p++ / totalStatementsToGather);
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
