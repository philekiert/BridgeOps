using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SendReceiveClasses;
using ClosedXML.Excel;

using System.Text.Json.Nodes;
using System.Net.Sockets;
using DocumentFormat.OpenXml.Office2010.ExcelAc;

namespace BridgeOpsClient
{
    public partial class SelectBuilder : Window
    {
        public SelectBuilder()
        {
            InitializeComponent();

            AddTab();

            PresetLoad(true);
            cmbPresets.SelectedIndex = 0;

            btnAddPreset.IsEnabled = App.sd.createPermissions[Glo.PERMISSION_REPORTS];
        }

        private PageSelectBuilder AddTab()
        {
            PageSelectBuilder pageSelectBuilder = new();
            Frame frame = new() { Content = pageSelectBuilder };
            TabItem tabItem = new()
            {
                Content = frame,
                Header = "SQL Query"
            };
            tabControl.Items.Add(tabItem);
            tabControl.SelectedItem = tabItem;

            return pageSelectBuilder;
        }

        PageSelectBuilder GetBuilder(TabItem tabItem)
        {
            return (PageSelectBuilder)((Frame)(tabItem.Content)).Content;
        }

        private void btnAddTab_Click(object sender, RoutedEventArgs e)
        {
            AddTab();
            if (tabControl.Items.Count > 1)
                btnRemoveTab.IsEnabled = true;

            ToggleMoveButtons();
        }

        private void btnRemoveTab_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = tabControl.SelectedIndex;

            blockTabInfoUpdate = true;
            tabControl.Items.Remove(tabControl.SelectedItem);
            if (tabControl.Items.Count == 1)
                btnRemoveTab.IsEnabled = false;
            blockTabInfoUpdate = false;

            tabControl.SelectedIndex = selectedIndex < tabControl.Items.Count ? selectedIndex : selectedIndex - 0;

            ToggleMoveButtons();
        }

        private void btnMoveLeft_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = tabControl.SelectedIndex;

            if (selectedIndex <= 0)
                return;

            blockTabInfoUpdate = true;
            TabItem tabItem = (TabItem)tabControl.SelectedItem;
            tabControl.Items.Remove(tabItem);
            tabControl.Items.Insert(selectedIndex - 1, tabItem);
            blockTabInfoUpdate = false;
            tabControl.SelectedItem = tabItem;

            ToggleMoveButtons();
        }

        private void btnMoveRight_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = tabControl.SelectedIndex;

            if (selectedIndex == -1 || selectedIndex >= tabControl.Items.Count - 1)
                return;

            blockTabInfoUpdate = true;
            TabItem tabItem = (TabItem)tabControl.SelectedItem;
            tabControl.Items.Remove(tabItem);
            tabControl.Items.Insert(selectedIndex + 1, tabItem);
            blockTabInfoUpdate = false;
            tabControl.SelectedItem = tabItem;

            ToggleMoveButtons();
        }

        private void ToggleMoveButtons()
        {
            btnMoveLeft.IsEnabled = tabControl.SelectedIndex > 0;
            btnMoveRight.IsEnabled = tabControl.SelectedIndex < tabControl.Items.Count - 1;
        }

        bool blockTabInfoUpdate = false;
        private void tabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (blockTabInfoUpdate)
                return;

            ToggleMoveButtons();
            if (tabControl.SelectedIndex != -1)
                txtTabName.Text = (string)((TabItem)tabControl.Items[tabControl.SelectedIndex]).Header;
        }

        private void txtTabName_TextChanged(object sender, TextChangedEventArgs e)
        {
            ((TabItem)tabControl.SelectedItem).Header = txtTabName.Text;
        }

        private void btnExportAllPages_Click(object sender, RoutedEventArgs e)
        {
            XLWorkbook xl = new();

            foreach (TabItem tab in tabControl.Items)
            {
                List<string?> columnNames;
                List<List<object?>> rows;
                if (!GetBuilder(tab).Run(out columnNames, out rows, false))
                    return;

                PageSelectBuilder builder = GetBuilder(tab);
                IXLWorksheet sheet = xl.AddWorksheet((string)tab.Header);

                // Add headers.
                IXLCell cell = sheet.Cell(1, 1);
                int columnCount = 0;
                foreach (string? s in columnNames)
                {
                    cell.Value = s;
                    cell = cell.CellRight();
                    ++columnCount;
                }

                // Add rows.
                cell = sheet.Cell(2, 1);
                foreach (var row in rows)
                {
                    cell.InsertData(row, true);
                    cell = cell.CellBelow();
                }

                // Apply suitable column widths.
                FileExport.AutoWidthColumns(columnCount, sheet);
            }

            // Save as...
            string fileName;
            if (!FileExport.GetSaveFileName(out fileName))
                return;
            FileExport.SaveFile(xl, fileName);
        }

        private string GetNewPresetName()
        {
            DialogWindows.NameObject nameObject;
            while (true)
            {
                nameObject = new("Preset Name");
                nameObject.ShowDialog();
                if (nameObject.DialogResult == false || nameObject.txtName.Text == "")
                    return "";
                if (!cmbPresets.Items.Contains(nameObject.txtName.Text))
                    break;
                App.DisplayError("This name is already in use.");
            }

            return nameObject.txtName.Text;
        }

        private void ApplyLoadedPreset(string jsonString)
        {
            blockTabInfoUpdate = true;
            try
            {
                // A lot could go wrong in terms of trying to read potentially null objects, but if that happens, the file
                // has been corrupted and an exception should be thrown anyway.
#pragma warning disable CS8602

                tabControl.Items.Clear();

                JsonNode? node = JsonNode.Parse(jsonString);

                JsonObject json = JsonNode.Parse(jsonString).AsObject();

                int totalTabs = json["TabCount"].GetValue<int>();
                for (int i = 0; i < totalTabs; ++i)
                {
                    JsonObject tab = json[i.ToString()].AsObject();
                    PageSelectBuilder pageSelectBuilder = AddTab();
                    ((TabItem)tabControl.Items[i]).Header = tab["Name"].GetValue<string>();
                    pageSelectBuilder.cmbTable.Text = tab["Table"].GetValue<string>();
                    pageSelectBuilder.chkDistinct.IsChecked = tab["Distinct"].GetValue<bool>();
                    int joinCount = tab["JoinCount"].GetValue<int>();
                    for (int j = 0; j < joinCount; ++j)
                    {
                        JsonObject joinObj = tab["Join" + j.ToString()].AsObject();
                        var join = pageSelectBuilder.AddJoin();
                        join.cmbTable.Text = joinObj["Table"].GetValue<string>();
                        join.cmbColumn1.Text = GetFriendlyName(joinObj["Column1"].GetValue<string>(), false);
                        join.cmbColumn2.Text = GetFriendlyName(joinObj["Column2"].GetValue<string>(), true);
                        join.cmbType.Text = joinObj["Type"].GetValue<string>();
                    }
                    int columnCount = tab["ColumnCount"].GetValue<int>();
                    for (int c = 0; c < columnCount; ++c)
                    {
                        JsonObject columnObj = tab["Column" + c.ToString()].AsObject();
                        var column = pageSelectBuilder.AddColumn();
                        column.cmbColumn.Text = GetFriendlyName(columnObj["Column"].GetValue<string>(), true);
                        column.txtAlias.Text = columnObj["Alias"].GetValue<string>();
                    }
                    int whereCount = tab["WhereCount"].GetValue<int>();
                    for (int w = 0; w < whereCount; ++w)
                    {
                        JsonObject whereObj = tab["Where" + w.ToString()].AsObject();
                        var where = pageSelectBuilder.AddWhere();
                        where.cmbColumn.Text = GetFriendlyName(whereObj["Column"].GetValue<string>(), true);
                        where.cmbOperator.Text = whereObj["Operator"].GetValue<string>();
                        where.txtValue.Text = whereObj["ValueText"].GetValue<string>();
                        where.numValue.Text = whereObj["ValueNumber"].GetValue<string>();
                        if (whereObj["ValueDateTime"] != null)
                            where.dtmValue.SetDateTime(whereObj["ValueDateTime"].GetValue<DateTime>());
                        if (whereObj["ValueDate"] != null)
                            where.datValue.SelectedDate = whereObj["ValueDate"].GetValue<DateTime>();
                        if (whereObj["ValueTime"] != null)
                            where.timValue.SetTime(whereObj["ValueTime"].GetValue<long>());
                        where.chkValue.IsChecked = whereObj["ValueBool"].GetValue<bool>();
                    }
                    int orderByCount = tab["OrderByCount"].GetValue<int>();
                    for (int o = 0; o < orderByCount; ++o)
                    {
                        JsonObject orderByObj = tab["OrderBy" + o.ToString()].AsObject();
                        var orderBy = pageSelectBuilder.AddOrderBy();
                        orderBy.cmbOrderBy.Text = GetFriendlyName(orderByObj["Column"].GetValue<string>(), true);
                        orderBy.cmbAscDesc.Text = orderByObj["AscDesc"].GetValue<string>();
                    }
                }

#pragma warning restore CS8602
            }
            catch
            {
                App.DisplayError("JSON file could not be read. It could be corrupted.");
            }

            tabControl.SelectedIndex = 0;
            blockTabInfoUpdate = false;
        }

        private void SavePreset(string name)
        {
            if (cmbPresets.Text != "<New>" &&
                !App.DisplayQuestion("Overwrite current preset?", "Save Changes",
                                    DialogWindows.DialogBox.Buttons.YesNo))
                return;

            JsonObject json = new();
            json["Name"] = name;
            json["TabCount"] = tabControl.Items.Count;

            int tabIndex = 0;
            foreach (TabItem tabItem in tabControl.Items)
            {
                JsonObject jsonTab = new();
                jsonTab["Name"] = (string)tabItem.Header;
                PageSelectBuilder pageSelectBuilder = (PageSelectBuilder)((Frame)(tabItem.Content)).Content;
                jsonTab["Table"] = pageSelectBuilder.cmbTable.Text;
                jsonTab["Distinct"] = pageSelectBuilder.chkDistinct.IsChecked == true;
                jsonTab["JoinCount"] = pageSelectBuilder.joins.Count;
                jsonTab["ColumnCount"] = pageSelectBuilder.columns.Count;
                jsonTab["WhereCount"] = pageSelectBuilder.wheres.Count;
                jsonTab["OrderByCount"] = pageSelectBuilder.orderBys.Count;
                for (int i = 0; i < pageSelectBuilder.joins.Count; ++i)
                {
                    PageSelectBuilderJoin join = pageSelectBuilder.Join(i);
                    JsonObject jsonJoin = new();
                    jsonJoin["Table"] = join.cmbTable.Text;
                    if (join.cmbColumn1.Text == "")
                        jsonJoin["Column1"] = "";
                    else
                        jsonJoin["Column1"] = pageSelectBuilder.GetProperColumnName(join.cmbTable.Text + "." +
                                                                                    join.cmbColumn1.Text);
                    jsonJoin["Column2"] = pageSelectBuilder.GetProperColumnName(join.cmbColumn2.Text);
                    jsonJoin["Type"] = join.cmbType.Text;
                    jsonTab["Join" + i.ToString()] = jsonJoin;
                }
                for (int i = 0; i < pageSelectBuilder.columns.Count; ++i)
                {
                    PageSelectBuilderColumn column = pageSelectBuilder.Column(i);
                    JsonObject jsonColumn = new();
                    jsonColumn["Column"] = pageSelectBuilder.GetProperColumnName(column.cmbColumn.Text);
                    jsonColumn["Alias"] = column.txtAlias.Text;
                    jsonTab["Column" + i.ToString()] = jsonColumn;
                }
                for (int i = 0; i < pageSelectBuilder.wheres.Count; ++i)
                {
                    PageSelectBuilderWhere where = pageSelectBuilder.Where(i);
                    JsonObject jsonWhere = new();
                    jsonWhere["Column"] = pageSelectBuilder.GetProperColumnName(where.cmbColumn.Text);
                    jsonWhere["Operator"] = where.cmbOperator.Text;
                    jsonWhere["ValueText"] = where.txtValue.Text;
                    jsonWhere["ValueNumber"] = where.numValue.Text;
                    jsonWhere["ValueDateTime"] = where.dtmValue.GetDateTime();
                    jsonWhere["ValueDate"] = where.datValue.SelectedDate;
                    TimeSpan? ts = where.timValue.GetTime();
                    jsonWhere["ValueTime"] = ts == null ? null : ((TimeSpan)ts).Ticks;
                    jsonWhere["ValueBool"] = where.chkValue.IsChecked == true;
                    jsonTab["Where" + i.ToString()] = jsonWhere;
                }
                for (int i = 0; i < pageSelectBuilder.orderBys.Count; ++i)
                {
                    PageSelectBuilderOrderBy orderBy = pageSelectBuilder.OrderBy(i);
                    JsonObject jsonOrderBy = new();
                    jsonOrderBy["Column"] = pageSelectBuilder.GetProperColumnName(orderBy.cmbOrderBy.Text);
                    jsonOrderBy["AscDesc"] = orderBy.cmbAscDesc.Text;
                    jsonTab["OrderBy" + i.ToString()] = jsonOrderBy;
                }

                json[tabIndex.ToString()] = jsonTab;
                ++tabIndex;
            }

            if (App.SendJsonObject(Glo.CLIENT_SELECT_BUILDER_PRESET_SAVE, json))
            {
                skipPresetLoad = true;
                PresetLoad(true);
                cmbPresets.SelectedItem = name;
                skipPresetLoad = false;
                if (savingNew)
                    App.DisplayError("Saved successfully");
                else
                    App.DisplayError("Changes saved successfully.");
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
                            App.DisplayError(App.NO_NETWORK_STREAM);
                            return;
                        }
                        stream.WriteByte(Glo.CLIENT_SELECT_BUILDER_PRESET_LOAD);
                        App.sr.WriteAndFlush(stream, App.sd.sessionID);
                        if (list) // if loading the preset list
                            App.sr.WriteAndFlush(stream, "/");
                        else // if loading a specific preset
                            App.sr.WriteAndFlush(stream, (string)cmbPresets.Items[cmbPresets.SelectedIndex]);

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
                            throw new Exception(App.sr.ReadString(stream));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                App.DisplayError(App.ErrorConcat("Could not retrieve preset list.", e.Message));
            }
        }

        private void ClearDown()
        {
            blockTabInfoUpdate = true;
            tabControl.Items.Clear();
            AddTab();
            txtTabName.Text = "SQL Query";
            blockTabInfoUpdate = false;
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
                {
                    ClearDown();
                    txtTabName.Text = (string)((TabItem)tabControl.Items[0]).Header; // Botch, but it's late.
                }
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
            if (!App.DeleteConfirm(false))
                return;

            try
            {
                lock (App.streamLock)
                {
                    using NetworkStream? stream = App.sr.NewClientNetworkStream(App.sd.ServerEP);
                    {
                        if (stream == null)
                        {
                            App.DisplayError(App.NO_NETWORK_STREAM);
                            return;
                        }
                        stream.WriteByte(Glo.CLIENT_SELECT_BUILDER_PRESET_DELETE);
                        App.sr.WriteAndFlush(stream, App.sd.sessionID);
                        string preset = (string)cmbPresets.Items[cmbPresets.SelectedIndex];
                        App.sr.WriteAndFlush(stream, preset);

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
                App.DisplayError(App.ErrorConcat("Could not remove the selected preset.", ex.Message));
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
            string newName = GetNewPresetName();
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
                            App.DisplayError(App.NO_NETWORK_STREAM);
                            return;
                        }
                        stream.WriteByte(Glo.CLIENT_SELECT_BUILDER_PRESET_RENAME);
                        App.sr.WriteAndFlush(stream, App.sd.sessionID);
                        string preset = (string)cmbPresets.Items[cmbPresets.SelectedIndex];
                        preset += "/" + newName;
                        App.sr.WriteAndFlush(stream, preset);

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
                App.DisplayError(App.ErrorConcat("Could not rename the selected preset.", ex.Message));
            }
        }

        private string GetFriendlyName(string name, bool retainTableName)
        {
            if (name.EndsWith('*'))
                return name;
            try
            {
                string table = name.Remove(name.IndexOf('.'));
                string column = name.Substring(name.IndexOf('.') + 1);
                var dictionary = ColumnRecord.GetDictionary(table, false);
                if (dictionary == null)
                    return "";
                name = ColumnRecord.GetPrintName(column, (ColumnRecord.Column)dictionary[column]!);

                return retainTableName ? table + "." + name : name;
            }
            catch { return ""; }
        }

        private void btnSaveChanges_Click(object sender, RoutedEventArgs e)
        {
            SavePreset(cmbPresets.Text);
        }

        private void btnClearDown_Click(object sender, RoutedEventArgs e)
        {
            ClearDown();
        }
    }
}
