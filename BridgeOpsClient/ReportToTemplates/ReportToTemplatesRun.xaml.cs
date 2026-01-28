using Azure;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Office2016.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Vml.Office;
using DocumentFormat.OpenXml.Wordprocessing;
using ExcelNumberFormat;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static BridgeOpsClient.CustomControls.SqlDataGrid;
using static BridgeOpsClient.ReportToTemplates;

namespace BridgeOpsClient
{
    public partial class ReportToTemplatesRun : CustomWindow
    {
        // progressSteps represent progress chunks between things like running the queries and inserting the data.
        float[] progressSteps = new float[] { .4f, .6f };
        // Step 0: Load/construct select statements for each preset tab.
        // Step 1: Run each query.
        // Step 2: Inject data into each file.
        int progressStep = 0;
        private void SetProgress(float percentageAlongStep)
        {
            float progress = 0f;
            for (int i = 0; i < progressStep; ++i)
                progress += progressSteps[i];
            progress += progressSteps[progressStep] * percentageAlongStep;
            bdrProgressBar.Width = bdrProgressBarRail.ActualWidth * progress;
        }

        List<ReportTag> reportTags;
        string outputDirectory;
        bool autoOverwite;

        public ReportToTemplatesRun(List<ReportTag> reportTags, string outputDirectory, bool autoOverwrite)
        {
            InitializeComponent();
            SetProgress(0); // Rail ActualWidth is 0 due to not being rendered, but it doesn't matter here.

            this.reportTags = reportTags;
            this.outputDirectory = outputDirectory;
            this.autoOverwite = autoOverwrite;
        }

        private void Log(string message)
        { Log(message, Brushes.Black); }
        private void LogError(string message)
        { Log(message, Brushes.Red); }
        private void Log(string message, Brush brush)
        { lstEventLog.Items.Add(new TextBlock() { Text = message, Foreground = brush }); }

        Dictionary<string, JsonObject?> presetJsonObjects = new();
        Dictionary<string, Dictionary<string, string?>> selectStatements = new();

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
            }

            int totalStatementsToGather = 0;
            foreach (var tabs in presetsAndTabs.Values)
                totalStatementsToGather += tabs.Count;
            float p = 0;

            // Build a dictionary of select statements by tab name inside an enclosing dictionary of preset names.
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
                        LogError($"Not found in presets: {presetName} / {tabName}");
                        continue;
                    }
                    // Gather the statement for the preset.
                    try
                    {
                        JsonObject tab = jsonObj[index.ToString()]!.AsObject();
                        presetStatements.Add(tabName, tab!["Statement"]!.GetValue<string>());
                    }
                    catch
                    {
                        presetStatements.Add(tabName, null);
                    }
                }
            }

            p = 0;
            ++progressStep;
            // This LINQ sorts the tags into a list for each filename, with their original filename ordering preserved.
            List<List<ReportTag>> grouped = reportTags.Select((item, index) => (Item: item, Index: index))
                                                      .GroupBy(x => x.Item.filename)
                                                      .OrderBy(g => g.Min(x => x.Index))
                                                      .Select(g => g.Select(x => x.Item).ToList())
                                                      .ToList();
            int stepCount = grouped.Count + reportTags.Count; // To account for query selects and file writes.
            foreach (List<ReportTag> tagList in grouped)
            {
                if (tagList.Count == 0)
                {
                    SetProgress(p++ / stepCount);
                    continue;
                }
                foreach (ReportTag tag in tagList)
                {
                    SetProgress(p++ / stepCount);
                    if (!tag.valid)
                        continue;

                    string failPre = $"Failed to run: {tag.filename} > {tag.presetName} > {tag.tabName}. ";

                    string? sqlStatement = selectStatements[tag.presetName][tag.tabName];
                    if (sqlStatement == null)
                        continue;
                    else if (!PageSelectStatement.InsertParameters(sqlStatement!,
                                                             out sqlStatement,
                                                             $"{tag.filename} > {tag.presetName} > {tag.tabName}",
                                                             this, new(),
                                                             tag.paramSetters))
                        LogError(failPre + "User opted not to set parameters.");
                    else if (!App.SendSelectStatement(sqlStatement,
                            out _, out tag.data, out tag.types, this))
                        LogError(failPre + "Data could not be retrieved.");
                    // We don't want to reference old set params with templates in subsequent iterations.
                    SelectBuilder.storedVariables.Clear();
                }

                SetProgress(p++ / stepCount);
                InsertDataExcel(tagList.OfType<ReportTagExcel>().ToList());
                InsertDataWord(tagList.OfType<ReportTagWord>().ToList());
            }

            SetProgress(1); // Done :)
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

        private bool InsertDataExcel(List<ReportTagExcel> tags)
        {
            if (tags.Count == 0)
                return true;
            string filepath = tags[0].filepath;
            string newFilePath = Path.Combine(outputDirectory, Path.GetFileName(filepath));
            if (File.Exists(newFilePath))
            {
                if (!autoOverwite &&
                    !App.DisplayQuestion("Would you like to replace this file?\n" + newFilePath, "File already exists",
                                         DialogWindows.DialogBox.Buttons.YesNo, this))
                {
                    LogError($" ✘  Skipped by user (file already existed): ${filepath}");
                    return false;
                }
                try
                {
                    File.Delete(newFilePath);
                }
                catch (Exception e)
                {
                    App.DisplayError(e.Message, "Unable to replace file", this);
                    LogError($" ✘  File skipped as it could not be replaced: ${filepath}");
                    return false;
                }
            }

            Dictionary<string, List<ReportTagExcel>> tagsBySheet = new();
            foreach (ReportTagExcel tag in tags)
            {
                tagsBySheet.TryAdd(tag.sheetName, new());
                tagsBySheet[tag.sheetName].Add(tag);
            }

            try
            {
                using (XLWorkbook file = new(filepath))
                {
                    string successMessage = "";

                    foreach (string sheetName in tagsBySheet.Keys)
                    {
                        IXLWorksheet sheet;
                        if (file.TryGetWorksheet(sheetName, out sheet))
                        {
                            // Start at the bottom of the sheet and work up to avoid row insertions creating gaps in
                            // previous data chunks.
                            var sheetTags = tagsBySheet[sheetName].OrderByDescending(t => t.y)
                                                                     .ThenBy(t => t.x).ToList();
                            // Record the greatest number of insertions for a single row to accont for multiple tags on
                            // one row. If a row has already been 'expanded', we may not need to insert any more rows
                            // for subsequent insertions on one row, or we certainly won't need to insert as many.
                            Dictionary<int, int> insertionCounts = new();
                            foreach (ReportTagExcel tag in sheetTags)
                            {
                                List<List<object?>> data = tag.data!;
                                List<string?> colTypes = tag.types;
                                if (!tag.valid || tag.data == null || colTypes == null)
                                    continue;
                                var topRow = sheet.Row(tag.y);
                                topRow.Cell(tag.x).SetValue(""); // Wipe the tag (don't .Clear(), that wipes the formatting).
                                // Store the 

                                // Insert rows as needed.
                                if (data.Count > 1)
                                {
                                    if (!insertionCounts.ContainsKey(tag.y))
                                    {
                                        topRow.InsertRowsBelow(data.Count - 1);
                                        insertionCounts.Add(tag.y, data.Count - 1);
                                    }
                                    else if (data.Count - 1 > insertionCounts[tag.y])
                                    {
                                        int lastIn = insertionCounts[tag.y];
                                        sheet.Row(tag.y + lastIn).InsertRowsBelow((data.Count - 1) - lastIn);
                                        insertionCounts[tag.y] = data.Count - 1;
                                    }
                                }
                                // else do nothing

                                for (int y = 0; y < data.Count; ++y)
                                    for (int x = 0; x < data[y].Count; ++x)
                                    {
                                        var c = sheet.Row(tag.y + y).Cell(tag.x + x);

                                        // The desired behaviour here is to set the appropriate number format if the
                                        // source cell was set to General, but if it was manually set to something else
                                        // like a custom format in the top row, we want to retain the user's choice.
                                        bool wasGeneral = c.Style.NumberFormat.NumberFormatId == 0;
                                        IXLStyle oldStyle = c.Style;

                                        if (colTypes[x]!.Contains("Int") || colTypes[x] == "Byte") // Can be left as General
                                        {
                                            c.Value = (int?)data[y][x];
                                            if (!wasGeneral)
                                                c.Style = oldStyle;
                                        }
                                        else if (colTypes[x] == "Date")
                                        {
                                            c.Value = (DateTime?)data[y][x];
                                            if (wasGeneral)
                                                c.Style.NumberFormat.Format = "dd/mm/yyyy";
                                            else
                                                c.Style = oldStyle;
                                        }
                                        else if (colTypes[x] == "DateTime")
                                        {
                                            c.Value = (DateTime?)data[y][x];
                                            if (wasGeneral)
                                                c.Style.NumberFormat.Format = "dd/mm/yyyy hh:mm";
                                            else
                                                c.Style = oldStyle;
                                        }
                                        else if (colTypes[x] == "TimeSpan")
                                        {
                                            c.Value = (TimeSpan?)data[y][x];
                                            if (wasGeneral)
                                                c.Style.NumberFormat.Format = "hh:mm";
                                            else
                                                c.Style = oldStyle;
                                        }
                                        else if (colTypes[x] == "Boolean")  // Can be left as General
                                        {
                                            c.Value = (bool?)data[y][x] == true ? "Yes" : "No";
                                            if (!wasGeneral)
                                                c.Style = oldStyle;
                                        }
                                        else // String or otherwise, can be left as General
                                        {
                                            c.Value = data[y][x] == null ? "" : data[y][x]!.ToString();
                                            if (!wasGeneral)
                                                c.Style = oldStyle;
                                        }
                                    }
                                successMessage += " └  Tag processed: " + tag.descriptor + "\n";
                            }
                        }
                    }
                    file.SaveAs(newFilePath);
                    if (successMessage.EndsWith("\n"))
                        successMessage = successMessage.Remove(successMessage.Length - 1);
                    Log("✔ " + tags[0].filepath + "\n" + successMessage);
                }
                return true;
            }
            catch (Exception except)
            {
                App.DisplayError($"Unable to process file, see error: {except.Message}", this);
                LogError($" ✘  Could not process file: \"{filepath}\". Error: {except.Message}");
                return false;
            }
        }

        private bool InsertDataWord(List<ReportTagWord> tags)
        {
            if (tags.Count == 0)
                return true;
            string filepath = tags[0].filepath;
            string newFilePath = Path.Combine(outputDirectory, Path.GetFileName(filepath));
            if (File.Exists(newFilePath))
            {
                if (!autoOverwite &&
                    !App.DisplayQuestion("Would you like to replace this file?\n" + newFilePath, "File already exists",
                                         DialogWindows.DialogBox.Buttons.YesNo, this))
                {
                    LogError($" ✘  Skipped by user (file already existed): ${filepath}");
                    return false;
                }
                try
                {
                    File.Delete(newFilePath);
                }
                catch (Exception e)
                {
                    App.DisplayError(e.Message, "Unable to replace file", this);
                    LogError($" ✘  File skipped as it could not be replaced: ${filepath}");
                    return false;
                }
            }

            Dictionary<int, List<ReportTagWord>> tagsByTable = new();
            foreach (ReportTagWord tag in tags)
            {
                tagsByTable.TryAdd(tag.tableNo, new());
                tagsByTable[tag.tableNo].Add(tag);
            }

            bool copied = false;
            try
            {
                File.Copy(filepath, newFilePath);
                copied = true;

                using (WordprocessingDocument doc = WordprocessingDocument.Open(newFilePath, true))
                {
                    var tables = doc.MainDocumentPart!.Document.Body!
                                    .Descendants<DocumentFormat.OpenXml.Wordprocessing.Table>();

                    string successMessage = "";

                    foreach (int tableNo in tagsByTable.Keys)
                    {
                        var table = tables.ElementAt(tableNo);

                        Dictionary<int, int> insertionCounts = new();

                        // Start at the bottom of the table and work up to avoid row insertions creating gaps in
                        // previous data chunks.
                        var tableTags = tagsByTable[tableNo].OrderByDescending(t => t.y).ThenBy(t => t.x).ToList();
                        foreach (ReportTagWord tag in tableTags)
                        {
                            if (!tag.valid || tag.data == null || tag.types == null)
                                continue;
                            List<List<object?>> data = tag.data;
                            List<string?> colTypes = tag.types;
                            var topRow = table.Elements<TableRow>().ElementAt(0);
                            // Remove the tag.
                            SetWordTableCell(topRow.Elements<TableCell>().ElementAt(tag.x));

                            // Insert rows as needed.
                            if (data.Count > 1)
                            {
                                if (!insertionCounts.ContainsKey(tag.y))
                                {
                                    InsertBlankRowsBelow(topRow, data.Count - 1);
                                    insertionCounts.Add(tag.y, data.Count - 1);
                                }
                                else if (data.Count - 1 > insertionCounts[tag.y])
                                {
                                    int lastIn = insertionCounts[tag.y];
                                    InsertBlankRowsBelow(table.Elements<TableRow>()
                                                              .ElementAt(tag.y + lastIn), (data.Count - 1) - lastIn);
                                    insertionCounts[tag.y] = data.Count - 1;
                                }
                            }
                            // else do nothing

                            var rows = table.Elements<TableRow>().ToList();

                            if (data.Count == 0) // If nothing, just clear the tag's cell.
                                SetWordTableCell(rows[tag.y].Elements<TableCell>().ToList()[tag.x]);
                            else
                                for (int y = 0; y < data.Count; ++y)
                                {
                                    var cells = rows[y + tag.y].Elements<TableCell>().ToList();
                                    for (int x = 0; x < data[y].Count && x + tag.x < cells.Count; ++x)
                                    {
                                        var cell = cells[tag.x + x];
                                        SetWordTableCell(cell, Glo.Fun.UnknownObjectToString(data[y][x]));
                                    }
                                }

                            successMessage += " └  Tag processed: " + tag.descriptor + "\n";
                        }
                    }
                    doc.Save();
                    if (successMessage.EndsWith("\n"))
                        successMessage = successMessage.Remove(successMessage.Length - 1);
                    Log("✔ " + tags[0].filepath + "\n" + successMessage);
                }
                return true;
            }
            catch (Exception except)
            {
                App.DisplayError($"Unable to process file, see error: {except.Message}", this);
                LogError($" ✘  Could not process file: \"{filepath}\". Error: {except.Message}");
                if (copied)
                    File.Delete(newFilePath);
                return false;
            }
        }
        private void InsertBlankRowsBelow(TableRow referenceRow, int rowCount)
        {
            TableRow templateRow = (TableRow)referenceRow.CloneNode(true);
            // Wipe all contents.
            foreach (TableCell cell in templateRow.Elements<TableCell>())
                SetWordTableCell(cell);
            TableRow currentRow = referenceRow;
            for (int i = 0; i < rowCount; i++)
            {
                TableRow newRow = (TableRow)templateRow.CloneNode(true);
                currentRow.InsertAfterSelf(newRow);
                currentRow = newRow;
            }
        }
        private void SetWordTableCell(TableCell cell, string text = "")
        {
            Paragraph para = cell.Elements<Paragraph>().First();
            para.RemoveAllChildren<Run>();
            para.AppendChild(new Run(new Text(text))); // Important to keep Word happy.
        }
    }
}
