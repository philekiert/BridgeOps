using ClosedXML.Excel;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Office2016.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using SendReceiveClasses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace BridgeOpsClient
{
    public partial class ReportToTemplates : CustomWindow
    {
        public ReportToTemplates(bool startWithBuilder)
        {
            InitializeComponent();

            // Configure preset bar.
            PresetLoad(true);
            cmbPresets.SelectedIndex = 0;
            btnAddPreset.IsEnabled = App.sd.createPermissions[Glo.PERMISSION_REPORTS];
        }

        public static Dictionary<string, object?> storedVariables = new();

        private string GetNewPresetName() { return GetNewPresetName(""); }
        private string GetNewPresetName(string rename)
        {
            DialogWindows.NameObject nameObject;
            while (true)
            {
                if (rename == "")
                    nameObject = new("Preset Name");
                else
                    nameObject = new("Preset Name", cmbPresets.Text);
                nameObject.Owner = this;

                nameObject.ShowDialog();
                if (nameObject.DialogResult == false || nameObject.txtName.Text == "")
                    return "";
                if (!cmbPresets.Items.Contains(nameObject.txtName.Text))
                    break;
                App.DisplayError("This name is already in use.", this);
            }

            return nameObject.txtName.Text;
        }

        private void SavePreset(string name)
        {
            if (cmbPresets.Text != "<New>" && !savingNew &&
                !App.DisplayQuestion("Overwrite current preset?", "Save Changes",
                                     DialogWindows.DialogBox.Buttons.YesNo, this))
                return;

            // This section should never run in the current design due to name parsing in NameObject.xaml.cs
            HashSet<char> illegalCharacters = name.Where(c => System.IO.Path.GetInvalidFileNameChars().Contains(c)).ToHashSet();
            if (illegalCharacters.Any())
            {
                App.DisplayError("The chosen preset name contains the following illegal characters:\n\n" +
                                 string.Join(", ", illegalCharacters), this);
                return;
            }

            JsonObject json = new();
            json["Name"] = name;
            json["Context"] = Glo.CLIENT_PRESET_CONTEXT_REPORT_TO_TEMPLATES;
            json["AutoOverwrite"] = chkOverwrite.IsChecked == true;
            JsonArray templates = new();
            foreach (string s in lstFiles.Items)
                templates.Add(s);
            json["Templates"] = templates;
            json["ParamsFile"] = txtParams.Text;
            json["OutputFolder"] = txtFolder.Text;

            if (App.SendJsonObject(Glo.CLIENT_PRESET_SAVE, json, this))
            {
                skipPresetLoad = true;
                PresetLoad(true);
                cmbPresets.SelectedItem = name;
                skipPresetLoad = false;
                if (savingNew)
                    App.DisplayError("Saved successfully.", this);
                else
                    App.DisplayError("Changes saved successfully.", this);
            }
        }

        private void PresetLoad(bool list)
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
                            return;
                        }
                        stream.WriteByte(Glo.CLIENT_PRESET_LOAD);
                        App.sr.WriteAndFlush(stream, App.sd.sessionID);
                        if (list) // if loading the preset list
                            App.sr.WriteAndFlush(stream, "/");
                        else // if loading a specific preset
                            App.sr.WriteAndFlush(stream, (string)cmbPresets.Items[cmbPresets.SelectedIndex]);
                        App.sr.WriteAndFlush(stream, Glo.FOLDER_REPORT_TO_TEMPLATES_PRESETS);

                        int response = App.sr.ReadByte(stream);
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                        {
                            if (list)
                            {
                                List<string> presets = new() { "<New>" };
                                foreach (string s in App.sr.ReadString(stream).Split('/'))
                                    if (s.EndsWith(".pre"))
                                        presets.Add(s.Remove(s.Length - 4));
                                cmbPresets.ItemsSource = presets;
                            }
                            else
                            {
                                string jsonString = App.sr.ReadString(stream);
                                ApplyLoadedPreset(jsonString);
                            }
                            return;
                        }
                        if (response == Glo.CLIENT_SESSION_INVALID)
                        {
                            App.SessionInvalidated();
                            return;
                        }
                        if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                        {
                            // We can skip the preset load when reverting back to the last selection,
                            // since it was already loaded.
                            skipPresetLoad = true;
                            cmbPresets.SelectedIndex = lastSelectedIndex;
                            skipPresetLoad = false;
                            throw new Exception(App.sr.ReadString(stream));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                App.DisplayError(App.ErrorConcat("Could not retrieve preset" + (list ? " list." : "."), e.Message),
                                 this);
            }
        }

        private void ApplyLoadedPreset(string jsonString)
        {
            try
            {
                // A lot could go wrong in terms of trying to read potentially null objects, but if that happens, the file
                // has been corrupted and an exception should be thrown anyway.
#pragma warning disable CS8602

                JsonObject json = JsonNode.Parse(jsonString).AsObject();

                // Not using ItemsSource since we want to work directly with the collection when the user modifies the list.
                lstFiles.Items.Clear();
                foreach (string s in json["Templates"].Deserialize<List<string>>()!)
                    lstFiles.Items.Add(s);

                txtParams.Text = json["ParamsFile"].GetValue<string>().ToString();
                txtFolder.Text = json["OutputFolder"].GetValue<string>().ToString();
                chkOverwrite.IsChecked = json["AutoOverwrite"].GetValue<bool>();

#pragma warning restore CS8602
            }
            catch
            {
                App.DisplayError("JSON file could not be read. It could be corrupted.", this);
            }

            ToggleFileButtons();
        }


        /// <summary> Reset the window to a blank configuration. </summary>
        private void ClearDown()
        {
            lstFiles.Items.Clear();
            lstSummary.Items.Clear();
            btnCheck.IsEnabled = false;
            btnRun.IsEnabled = false;
            validTagCount = 0;
        }

        bool skipPresetLoad = false;
        int lastSelectedIndex = 0;
        private void cmbPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Clear down.
            if (cmbPresets.SelectedIndex <= 0 && !skipPresetLoad)
            {
                btnRemovePreset.IsEnabled = false;
                btnRenamePreset.IsEnabled = false;
                btnSaveChanges.IsEnabled = false;

                if (lastSelectedIndex > 0)
                    ClearDown();
            }

            // Load preset.
            else if (cmbPresets.SelectedIndex > 0)
            {
                btnRemovePreset.IsEnabled = App.sd.deletePermissions[Glo.PERMISSION_REPORTS];
                btnRenamePreset.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_REPORTS];
                btnSaveChanges.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_REPORTS];

                if (!skipPresetLoad)
                    PresetLoad(false);
            }
            lastSelectedIndex = cmbPresets.SelectedIndex;

            btnRun.IsEnabled = false;
        }

        bool savingNew = false;
        private void btnAddPreset_Click(object sender, RoutedEventArgs e)
        {
            string name = GetNewPresetName();
            if (name == "")
                return;

            savingNew = true;
            SavePreset(name);
            savingNew = false;
        }

        private void btnRemovePreset_Click(object sender, RoutedEventArgs e)
        {
            if (!App.DeleteConfirm(false, this))
                return;

            try
            {
                lock (App.streamLock)
                {
                    using NetworkStream? stream = App.sr.NewClientNetworkStream(App.sd.ServerEP);
                    {
                        if (stream == null)
                        {
                            App.DisplayError(App.NO_NETWORK_STREAM, this);
                            return;
                        }
                        stream.WriteByte(Glo.CLIENT_PRESET_DELETE);
                        App.sr.WriteAndFlush(stream, App.sd.sessionID);
                        string preset = (string)cmbPresets.Items[cmbPresets.SelectedIndex];
                        App.sr.WriteAndFlush(stream, preset);
                        App.sr.WriteAndFlush(stream, Glo.FOLDER_REPORT_TO_TEMPLATES_PRESETS);

                        int response = App.sr.ReadByte(stream);
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                        {
                            skipPresetLoad = true;
                            PresetLoad(true);
                            cmbPresets.SelectedIndex = 0;
                            skipPresetLoad = false;
                            return;
                        }
                        if (response == Glo.CLIENT_SESSION_INVALID)
                        {
                            App.SessionInvalidated();
                            return;
                        }
                        if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                        {
                            throw new Exception(App.sr.ReadString(stream));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.DisplayError(App.ErrorConcat("Could not remove the selected preset.", ex.Message), this);
            }
            finally
            {
                btnRemovePreset.IsEnabled = cmbPresets.Items.Count > 1 && cmbPresets.Text != "<New>" &&
                                            App.sd.deletePermissions[Glo.PERMISSION_REPORTS];
                btnSaveChanges.IsEnabled = false;
            }
        }

        private void btnRenamePreset_Click(object sender, RoutedEventArgs e)
        {
            string newName = GetNewPresetName(cmbPresets.Text);
            if (newName == "")
                return;

            try
            {
                lock (App.streamLock)
                {
                    using NetworkStream? stream = App.sr.NewClientNetworkStream(App.sd.ServerEP);
                    {
                        if (stream == null)
                        {
                            App.DisplayError(App.NO_NETWORK_STREAM, this);
                            return;
                        }
                        stream.WriteByte(Glo.CLIENT_PRESET_RENAME);
                        App.sr.WriteAndFlush(stream, App.sd.sessionID);
                        string preset = (string)cmbPresets.Items[cmbPresets.SelectedIndex];
                        preset += "/" + newName;
                        App.sr.WriteAndFlush(stream, preset);
                        App.sr.WriteAndFlush(stream, Glo.FOLDER_REPORT_TO_TEMPLATES_PRESETS);

                        int response = App.sr.ReadByte(stream);
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                        {
                            skipPresetLoad = true;
                            PresetLoad(true);
                            cmbPresets.SelectedItem = newName;
                            skipPresetLoad = false;
                            return;
                        }
                        if (response == Glo.CLIENT_SESSION_INVALID)
                        {
                            App.SessionInvalidated();
                            return;
                        }
                        if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                        {
                            throw new Exception(App.sr.ReadString(stream));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.DisplayError(App.ErrorConcat("Could not rename the selected preset.", ex.Message), this);
            }
        }

        private void btnSaveChanges_Click(object sender, RoutedEventArgs e)
        {
            SavePreset(cmbPresets.Text);
        }

        private void CustomWindow_Closed(object sender, EventArgs e)
        {
            if (WindowState != WindowState.Maximized)
            {
                Settings.Default.ReportToTempWinSizeX = Width;
                Settings.Default.ReportToTempWinSizeY = Height;
                Settings.Default.Save();
                App.WindowClosed();
            }
        }

        private void btnAddFile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new();
            dialog.Multiselect = true;
            dialog.Filter = "Excel Workbooks & Word Documents|*.xlsx;*.xlsm;*.docx";
            string[] files;
            if (dialog.ShowDialog() == false)
                return;

            files = dialog.FileNames;

            bool foundMacroFile = false; // ClosedXML doesn't like macros, so warn the user if .xlsm files are found.
            foreach (string f in files)
            {
                if (!lstFiles.Items.Contains(f) && ((f.EndsWith(".docx") || f.EndsWith(".xlsx") || f.EndsWith(".xlsm"))))
                    lstFiles.Items.Add(f);
                if (!foundMacroFile && f.EndsWith("xlsm"))
                    foundMacroFile = true;
            }
            if (foundMacroFile)
                App.DisplayError("Note that macros in .xlsm files will not be carried over to the output.", this);

            ToggleFileButtons();
        }

        private void btnRemoveFile_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < lstFiles.Items.Count; ++i)
                if (lstFiles.SelectedItems.Contains(lstFiles.Items[i]))
                {
                    lstFiles.Items.RemoveAt(i);
                    --i;
                }

            ToggleFileButtons();
        }

        private void btnFileUp_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 1; i < lstFiles.Items.Count; ++i)
                if (lstFiles.SelectedItems.Contains(lstFiles.Items[i]))
                {
                    var temp = lstFiles.Items[i];
                    skipFileSelectionChanged = true;
                    lstFiles.Items.RemoveAt(i);
                    lstFiles.Items.Insert(i - 1, temp);
                    lstFiles.SelectedItems.Add(temp);
                    skipFileSelectionChanged = false;
                }

            ToggleFileButtons();
        }

        private void btnFileDown_Click(object sender, RoutedEventArgs e)
        {
            if (lstFiles.Items.Count < 2)
                return;
            for (int i = lstFiles.Items.Count - 2; i >= 0; --i)
                if (lstFiles.SelectedItems.Contains(lstFiles.Items[i]))
                {
                    var temp = lstFiles.Items[i];
                    skipFileSelectionChanged = true;
                    lstFiles.Items.RemoveAt(i);
                    if (i == lstFiles.Items.Count)
                        lstFiles.Items.Add(temp);
                    else
                        lstFiles.Items.Insert(i + 1, temp);
                    lstFiles.SelectedItems.Add(temp);
                    skipFileSelectionChanged = false;
                }

            ToggleFileButtons();
        }

        private void ToggleFileButtons()
        {
            btnAddFile.IsEnabled = true;
            btnRemoveFile.IsEnabled = lstFiles.SelectedItems.Count > 0;
            btnCheck.IsEnabled = lstFiles.Items.Count > 0;
            if (lstFiles.Items.Count > 1)
            {
                btnFileUp.IsEnabled = lstFiles.SelectedItems.Count > 0 &&
                                      !lstFiles.SelectedItems.Contains(lstFiles.Items[0]);
                btnFileDown.IsEnabled = lstFiles.SelectedItems.Count > 0 &&
                                        !lstFiles.SelectedItems.Contains(lstFiles.Items[lstFiles.Items.Count - 1]);
            }
            else
            {
                btnFileUp.IsEnabled = false;
                btnFileDown.IsEnabled = false;
            }
        }

        private void ToggleRunButton()
        {
            btnRun.IsEnabled = lstFiles.Items.Count > 0 && validTagCount > 0 && txtFolder.Text.Length > 0;
        }

        bool skipFileSelectionChanged = false;
        private void lstFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!skipFileSelectionChanged)
                ToggleFileButtons();
        }

        private void btnParams_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openDialog = new();
            openDialog.DefaultExt = ".txt";
            openDialog.Filter = "Text File|*.txt";
            bool? result = openDialog.ShowDialog();

            if (result != true)
                return;
            if (!openDialog.FileName.EndsWith(".txt"))
                App.DisplayError("Invalid file extension.", this);
            else
                txtParams.Text = openDialog.FileName;
        }

        private void btnFolder_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new();
            if (Directory.Exists(txtFolder.Text))
                dialog.InitialDirectory = txtFolder.Text;

            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK)
                return;
            txtFolder.Text = dialog.SelectedPath;
        }

        public class ReportTag
        {
            public string filepath = "";    // Path including filename.
            public string filename = "";    // Filename only.
            public string tag = "";         // The tag's original text.
            public string presetName = "";  // The tag's preset name.
            public string tabName = "";     // The tag's preset tab name.
            public string descriptor = "";
            public bool valid;
            public int x;                  // Column number, 1-indexed.
            public int y;                  // Row number, 1-indexed.
            public Dictionary<string, string> paramSetters = new();
            public List<List<object?>> data; // Used by ReportToTemplatesRun.
            public List<string?> types;      // Used by ReportToTemplatesRun.
        }
        public class ReportTagExcel : ReportTag
        {
            public string sheetName = "";  // Name of the tab in the Excel file.
            public string xLetter = "";    // Column letter.
        }
        public class ReportTagWord : ReportTag
        {
            public int tableNo = 0;
        }

        List<ReportTag> reportTags = new();

        Dictionary<(string filename, string preset, string tab), Dictionary<string, string>> paramSetterValues = new();
        public void LoadDateSetters()
        {
            paramSetterValues = new();
            if (txtParams.Text.Length == 0)
                return;
            if (!File.Exists(txtParams.Text))
                App.DisplayError("The selected Date Setters file could not be found.", this);

            // Cycle through the param setters file and build a dictionary of params
            string[] lines = File.ReadAllLines(txtParams.Text);
            Dictionary<string, HashSet<(string filename, string preset, string tab, string param)>> paramSetters = new();
            List<string> paramSettersOrder = new(); // Guaranteed unique, used to track paramSetters key order.
            string currentParamName = "";
            foreach (string line in lines)
            {
                if (line.Length <= 2)
                    continue;
                string s = line.Substring(2);
                // Headers (param names)
                if (line.StartsWith("$ "))
                {
                    if (paramSetters.TryAdd(s, new()))
                        paramSettersOrder.Add(s);
                    currentParamName = s;
                }
                // Param Identities (which preset tab parameters are going to be filled with the specified value)
                else if (currentParamName != "" && line.StartsWith("+ "))
                {
                    string[] split = s.Split(" > ");
                    if (split.Length != 4)
                        continue;
                    paramSetters[currentParamName].Add((split[0], split[1], split[2], split[3]));
                }
            }

            // Get the desired values for all param setters.
            List<PageSelectStatement.Param> paramList = new();
            int i = 0;
            foreach (string name in paramSettersOrder)
            {
                paramList.Add(new()
                {
                    type = PageSelectStatement.Param.Type.Date,
                    position = i,
                    name = name,
                    start = 0,             // Unused
                    length = 0,            // Unused
                    unorderedPosition = i  // Unused
                });
                ++i;
            }
            SetParameters setParameters = new("Date Setters", paramList);
            setParameters.ShowDialog();
            // Allow the user to bypass setting these if needed, the process will continue with them unset as normal.
            if (setParameters.DialogResult == false)
                return;

            // Build a dictionary of file/preset/tab/param identifiers that stores the values to insert.
            foreach (PageSelectStatement.Param param in paramList)
                foreach (var fptp in paramSetters[param.name])
                {
                    // SetParameters guarantees non-null values if we didn't return just above.
                    (string filename, string preset, string tab) key = (fptp.filename, fptp.preset, fptp.tab);
                    paramSetterValues.TryAdd(key, new());
                    paramSetterValues[key].TryAdd(fptp.param, ((DateTime)param.value!).ToString("yyyy-MM-dd"));
                }
        }

        public List<string[]> ParseTagText(string text)
        {
            List<string[]> pairs = new();
            if (text.Length >= 8 && text.StartsWith("{{") && text.EndsWith("}}"))
            {
                string content = text.Substring(2, text.Length - 4);
                // The first pair should be a preset name followed by a tab name, the rest will be param setters.
                string[] parts = content.Split(";;");
                foreach (string s in parts)
                {
                    string[] pair = s.Split("//");
                    if (pair.Length == 2 && pair[0].Length > 0 && pair[1].Length > 0)
                        pairs.Add(pair);
                }
            }
            return pairs;
        }

        public Dictionary<string, string> BuildParamSetterDict(string filepath, List<string[]> setters)
        {
            Dictionary<string, string> setterDict = new();
            for (int i = 1; i < setters.Count; ++i)
                setterDict.TryAdd(setters[i][0], setters[i][1]);
            // Pull in param setters if any were set for this file, preset and tab.
            (string filename, string preset, string tab) key = (filepath, setters[0][0], setters[0][1]);
            if (paramSetterValues.ContainsKey(key))
            {
                foreach (KeyValuePair<string, string> kvp in paramSetterValues[key])
                    setterDict.TryAdd(kvp.Key, kvp.Value);
            }
            return setterDict;
        }

        private int validTagCount = 0;
        private void btnCheck_Click(object sender, RoutedEventArgs e)
        {
            reportTags.Clear();
            lstSummary.Items.Clear();

            LoadDateSetters();

            TextBlock CreateTextBlock(string text, Brush brush)
            {
                return new TextBlock()
                {
                    Text = text,
                    Foreground = brush
                };
            }

            validTagCount = 0;

            List<List<object>> report = new();
            foreach (string filepath in lstFiles.Items)
            {
                string filename = Path.GetFileName(filepath);
                if (!File.Exists(filepath))
                {
                    lstSummary.Items.Add(CreateTextBlock($"{filename} could not be located.", Brushes.Red));
                    continue;
                }

                try
                {
                    // Excel files.
                    if (filename.EndsWith(".xlsx") || filename.EndsWith(".xlsm"))
                        using (XLWorkbook file = new(filepath))
                        {
                            foreach (var sheet in file.Worksheets)
                                foreach (var cell in sheet.CellsUsed())
                                    try
                                    {
                                        string celltext = cell.GetText();
                                        List<string[]> pairs = ParseTagText(celltext);
                                        if (pairs.Count == 0)
                                            continue;
                                        reportTags.Add(new ReportTagExcel()
                                        {
                                            filepath = filepath,
                                            filename = filename,
                                            sheetName = sheet.Name,
                                            x = cell.Address.ColumnNumber,
                                            xLetter = cell.Address.ColumnLetter,
                                            y = cell.Address.RowNumber,
                                            tag = celltext,
                                            presetName = pairs[0][0],
                                            tabName = pairs[0][1],
                                            paramSetters = BuildParamSetterDict(filepath, pairs),
                                            valid = false
                                        });
                                    }
                                    catch
                                    {
                                        // Do nothing. Sometimes cell.GetText() hits a type cast error, so this is
                                        // just to mitigate that.
                                    }
                        }
                    // Word files.
                    else if (filename.EndsWith(".docx"))
                        using (WordprocessingDocument doc = WordprocessingDocument.Open(filepath, false))
                        {
                            int tableNo = 0;
                            foreach (var table in doc.MainDocumentPart!.Document.Body!
                                                     .Descendants<DocumentFormat.OpenXml.Wordprocessing.Table>())
                            {
                                int y = 0;
                                foreach (var row in table.Elements<TableRow>())
                                {
                                    int x = 0;
                                    foreach (var cell in row.Elements<TableCell>())
                                    {
                                        // Get all text in the cell.
                                        string text = "";
                                        foreach (var t in cell.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>())
                                            text += t.Text;

                                        List<string[]> pairs = ParseTagText(text);
                                        List<string[]> paramSetters = new();
                                        if (pairs.Count > 1)
                                            paramSetters = pairs.GetRange(1, pairs.Count - 1);
                                        if (pairs.Count > 0)
                                            reportTags.Add(new ReportTagWord()
                                            {
                                                filepath = filepath,
                                                filename = filename,
                                                tag = text,
                                                presetName = pairs[0][0],
                                                tabName = pairs[0][1],
                                                x = x,
                                                y = y,
                                                tableNo = tableNo,
                                                paramSetters = BuildParamSetterDict(filepath, pairs),
                                                valid = false
                                            });
                                        ++x;
                                    }
                                    ++y;
                                }
                                ++tableNo;
                            }
                        }
                }
                catch (Exception except)
                {
                    App.DisplayError($"Unable to process {filepath}, see error: {except.Message}", this);
                }
            }

            PresetCheckRequest pcr;
            if (App.SendPresetCheckList(reportTags.Select(t => t.presetName).ToList(),
                                        reportTags.Select(t => t.tabName).ToList(), out pcr, this) &&
                pcr.presets.Count == pcr.tabs.Count &&
                pcr.presets.Count == pcr.present.Count)
            {
                HashSet<string> existingPresetTabs = new();
                for (int i = 0; i < pcr.presets.Count; ++i)
                    if (pcr.present[i])
                        existingPresetTabs.Add(pcr.presets[i] + "/" + pcr.tabs[i]);

                foreach (ReportTag tag in reportTags)
                {
                    if (tag is ReportTagExcel rte)
                        rte.descriptor = $"{rte.filename}/{rte.sheetName} [{rte.xLetter}{rte.y}]: {rte.presetName} > {rte.tabName}";
                    else if (tag is ReportTagWord rtw)
                        rtw.descriptor = $"{rtw.filename}/Table {rtw.tableNo + 1} [x{rtw.x + 1},y{rtw.y + 1}]: " +
                            $"{rtw.presetName} > {rtw.tabName}";
                    else
                        tag.descriptor = "Error";
                    // List param setters, if present.
                    string setters = "";
                    foreach (KeyValuePair<string, string> kvp in tag.paramSetters)
                        setters += "\n  •  " + kvp.Key + " = " + kvp.Value;
                    if (existingPresetTabs.Contains(tag.presetName + "/" + tag.tabName))
                    {
                        lstSummary.Items.Add(CreateTextBlock(tag.descriptor + setters, Brushes.Black));
                        tag.valid = true;
                        ++validTagCount;
                    }
                    else
                        lstSummary.Items.Add(CreateTextBlock("Stated preset and/or preset tab not found:\n" +
                                             tag.descriptor, Brushes.Red));
                }
            }

            btnRun.IsEnabled = validTagCount > 0;
        }

        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(txtFolder.Text))
            {
                App.DisplayError("The output directory does not exist.", this);
                return;
            }

            if (validTagCount == 0)
            {
                App.DisplayError("No valid tags could be found.", this);
            }

            ReportToTemplatesRun runner = new(reportTags, txtFolder.Text, chkOverwrite.IsChecked == true);
            runner.ShowDialog();
        }

        private void txtFolder_TextChanged(object sender, TextChangedEventArgs e)
        {
            ToggleRunButton();
        }
    }
}
