using System;
using System.Collections;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using BridgeOpsClient.CustomControls;
using SendReceiveClasses;
using static System.Runtime.CompilerServices.RuntimeHelpers;
using static BridgeOpsClient.SelectBuilder;

namespace BridgeOpsClient
{
    public partial class PageSelectStatement : Page
    {
        public TabItem? tabItem;
        public Window? builderWindow;

        public PageSelectStatement()
        {
            InitializeComponent();

            dtgOutput.EnableMultiSelect();
            dtgOutput.AddWipeButton();
            dtgOutput.WipeCallback = WipeCallback;

            btnUpdateSelected = dtgOutput.AddContextMenuItem("Update Selected", false, btnUpdate_Click);
            btnDeleteSelected = dtgOutput.AddContextMenuItem("Delete Selected", false, btnDelete_Click);
            btnUpdateSelected.IsEnabled = false;
            btnDeleteSelected.IsEnabled = false;
        }

        MenuItem btnUpdateSelected;
        MenuItem btnDeleteSelected;

        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (dtgOutput.dtg.SelectedItems.Count < 1)
            {
                App.DisplayError("You must select at least one item to update.", builderWindow);
                return;
            }

            string table;
            string idColumn;
            int identity;
            if (!SelectBuilder.GetRelevancy(relevantTable, out table, out idColumn, out identity))
                return;

            var columns = ColumnRecord.GetDictionary(table, true);
            if (columns == null)
                return;

            UpdateMultiple updateMultiple = new(identity, table, columns,
                                                idColumn, dtgOutput.GetCurrentlySelectedIDs(), true);
            updateMultiple.Owner = App.GetParentWindow(this);
            if (updateMultiple.ShowDialog() == true)
                Run(out _, out _, out _);
            // Error message for failed updates are displayed by the UpdateMultiple window.
        }
        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!App.DeleteConfirm(dtgOutput.dtg.SelectedItems.Count > 1, builderWindow))
                return;

            string table;
            string idColumn;
            int identity;
            if (!SelectBuilder.GetRelevancy(relevantTable, out table, out idColumn, out identity))
                return;

            if (App.SendDelete(table, idColumn, dtgOutput.GetCurrentlySelectedIDs(), true, builderWindow) &&
                MainWindow.pageDatabase != null)
            {
                MainWindow.pageDatabase.RepeatSearches(identity);
                Run(out _, out _, out _);
            }
        }

        public struct FrameContent
        {
            // Frame.Content isn't accessible immediately after assignment, so can't be called right away. Use this
            // struct to store the associated content so it can be called right away.
            public Frame frame;
            public object page;
            public FrameContent(Frame frame, object page)
            {
                this.frame = frame;
                this.page = page;
                frame.Content = page;
            }
        }
        public List<FrameContent> joins = new();
        public List<FrameContent> columns = new();
        public List<FrameContent> wheres = new();
        public List<FrameContent> orderBys = new();

        // This is used to allow the opening of relevant windows by double clicking on the SqlDataGrid when a
        // compatible query has been run, i.e. ID as the first column.
        SelectBuilder.RelevantTable relevantTable = SelectBuilder.RelevantTable.None;

        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            SelectBuilder.storedVariables.Clear();
            Run(out _, out _, out _);
        }

        public class Param
        {
            public enum Type { Text, Number, DateTime, Date, Time, Bool, Dropdown, Checklist }

            public Type type;
            public int position;
            public string name = "";
            public object? value = null;
            public string? variableName = null;
            public string[]? allowed = null;

            // Used to track where to replace the parameter with the value.
            public int start;
            public int length;

            // Used to revert to the original order ahead of replacements.
            public int unorderedPosition;

            public Param Clone(int start, int length, int unorderedPosition)
            {
                return new()
                {
                    type = type,
                    position = position,
                    name = name,
                    value = value,
                    variableName = variableName,
                    allowed = allowed,
                    start = start,
                    length = length,
                    unorderedPosition = unorderedPosition
                };
            }
        }
        List<Param> paramList = new();

        Dictionary<string, Param> paramVariables = new();

        private bool InsertParameters(string input, out string result, string? pageName)
        {
            paramList.Clear();
            paramVariables.Clear();

            result = input;

            List<string[]> stringsToCheck = new();
            List<int[]> startsAndLengths = new();

            for (int i = 0; i < input.Length; ++i)
            {
                if (input[i] == '{')
                {
                    string theRest = input.Substring(i);
                    if (input.Length > i + 5 && theRest.StartsWith("{,,") && theRest.Contains("}"))
                    {
                        int endIndex = theRest.IndexOf("}");
                        startsAndLengths.Add(new int[] { i, endIndex + 1 });
                        stringsToCheck.Add(new string[1] { input.Substring(i, endIndex + 1) });
                    }
                    else if (input.Length > i + 9)
                    {
                        string sub = input.Substring(i + 1, 9);
                        if (sub.StartsWith("checklist") || sub.StartsWith("dropdown") ||
                            sub.StartsWith("date") || sub.StartsWith("datetime") || sub.StartsWith("time") ||
                            sub.StartsWith("text") || sub.StartsWith("number") || sub.StartsWith("bool"))
                        {
                            int endIndex = theRest.IndexOf('}');

                            if (endIndex > 0)
                            {
                                startsAndLengths.Add(new int[] { i, endIndex + 1 });
                                stringsToCheck.Add(input.Substring(i + 1, endIndex - 1).Split(",,"));
                                i = i + endIndex + 1;
                            }
                            else
                                break;
                        }
                    }
                }
            }

            int n = 0;
            foreach (string[] strs in stringsToCheck)
            {
                if (strs.Length == 1 && strs[0].StartsWith("{,,") && strs[0].EndsWith("}"))
                {
                    // The string in between has to be at least one character in length here, or it wouldn't have been
                    // picked up in the for loop above.
                    string varName = strs[0].Substring(3, strs[0].Length - 4);
                    if (paramVariables.ContainsKey(varName))
                        paramList.Add(paramVariables[varName].Clone(startsAndLengths[n][0], startsAndLengths[n][1],
                                                                    n));
                    ++n;
                    continue;
                }

                bool valueList = strs[0] == "dropdown" || strs[0] == "checklist";
                if (!(((strs.Length == 3 && !valueList) || strs.Length == 4) ||
                      (strs.Length == 5 && valueList) ||
                      (strs.Length == 1)))
                {
                    ++n;
                    continue;
                }

                Param param = new();
                if (strs[0] == "text")
                    param.type = Param.Type.Text;
                else if (strs[0] == "number")
                    param.type = Param.Type.Number;
                else if (strs[0] == "datetime")
                    param.type = Param.Type.DateTime;
                else if (strs[0] == "date")
                    param.type = Param.Type.Date;
                else if (strs[0] == "time")
                    param.type = Param.Type.Time;
                else if (strs[0] == "bool")
                    param.type = Param.Type.Bool;
                else if (strs[0] == "dropdown")
                    param.type = Param.Type.Dropdown;
                else if (strs[0] == "checklist")
                    param.type = Param.Type.Checklist;
                else
                    continue;

                if (!int.TryParse(strs[1], out param.position))
                    continue;

                param.name = strs[2];
                // Allowed list comes after variable name, but the variable name may not be present so we need to
                // record this.
                bool variableNamePresent = (strs.Length == 4 && !valueList) || (strs.Length == 5 && valueList);

                if (variableNamePresent)
                {
                    param.variableName = strs[3];
                    if (SelectBuilder.storedVariables.ContainsKey(param.variableName))
                        param.value = SelectBuilder.storedVariables[param.variableName];
                    // If it's the first time we're seeing this variable name, we'll add it to the dictionary once we
                    // have the value from the SetParameters dialog.
                }
                if (valueList)
                {
                    List<string> values = new();
                    string str = strs.Last();
                    foreach (string s in str.Split(";;"))
                    {
                        bool allowed = s.StartsWith("$$");
                        bool queryValues = s.StartsWith("??");
                        if (allowed || queryValues)
                        {
                            string[] tc = s.Split('.');
                            if (tc.Length != 2)
                                param.allowed = null;
                            else
                            {
                                if (allowed)
                                {
                                    var column = ColumnRecord.GetColumnNullable(tc[0].Substring(2), tc[1]);
                                    if (column != null)
                                        values.AddRange(column.Value.allowed);
                                }
                                else
                                {
                                    List<List<object?>> rows;
                                    // This seems like a weird way to run this, but it's so SQL Server doesn't kick up
                                    // two separate errors for the same column name if the user got it wrong, one for
                                    // the select statement and one for the NOT NULL condition.
                                    App.SendSelectStatement($"WITH Rows AS (SELECT DISTINCT {tc[1]} AS Col " +
                                                                            $"FROM {tc[0].Substring(2)})\n" +
                                                            $"SELECT Col FROM Rows WHERE Col IS NOT NULL;",
                                                            out _, out rows, App.mainWindow);
                                    if (rows.Count > 0)
                                        values.AddRange(rows.Select(s => s[0]!.ToString()!));
                                }
                            }
                        }
                        else
                            values.Add(s);
                    }

                    param.allowed = values.ToArray();
                }

                param.start = startsAndLengths[n][0];
                param.length = startsAndLengths[n][1];
                param.unorderedPosition = n;
                paramList.Add(param);
                if (param.variableName != null && !paramVariables.ContainsKey(param.variableName))
                    paramVariables.Add(param.variableName, param);
                ++n;
            }

            paramList = paramList.OrderBy(i => i.position).ToList();

            if (paramList.Count == 0)
            {
                result = input;
                return true;
            }

            bool paramsToSet = false;
            foreach (Param p in paramList)
                if (p.value == null)
                {
                    paramsToSet = true;
                    break;
                }
            if (paramsToSet)
            {
                SetParameters setParameters = new(pageName, paramList);
                setParameters.Owner = App.GetParentWindow(this);
                setParameters.ShowDialog();
                if (setParameters.DialogResult == false)
                    return false;
            }

            // Store any variables that might have been assigned values for the first time.
            foreach (Param p in paramList)
                if (p.variableName != null && !SelectBuilder.storedVariables.ContainsKey(p.variableName))
                    SelectBuilder.storedVariables.Add(p.variableName, p.value);

            // Revert to the as-written order to make sure replacements work sequentially.
            paramList = paramList.OrderBy(i => i.unorderedPosition).ToList();

            // Because we're making replacements, we need to modify start indices for replacement starts.
            int lengthMod = 0;

            foreach (Param param in paramList)
            {
                if (param.value == null)
                    return App.Abort("Not all parameter values could be read. Run cancelled.", builderWindow);

                string value;
                if (param.value is string txt)
                    value = txt; //  :)
                else if (param.value is int num)
                    value = num.ToString();
                else if (param.type is Param.Type.DateTime)
                    value = SqlAssist.DateTimeToSQL((DateTime)param.value, false, true);
                else if (param.type is Param.Type.Date)
                    value = SqlAssist.DateTimeToSQL((DateTime)param.value, true, true);
                else if (param.value is TimeSpan tsp)
                    value = $"'{SqlAssist.TimeSpanToSQL(tsp)}'";
                else if (param.value is bool boo)
                    value = boo ? "1" : "0";
                else
                    return App.Abort("Not all parameter values could be read. Run cancelled.", builderWindow);

                int originalLength = input.Length;
                input = input.Remove(param.start - lengthMod, param.length);
                input = input.Insert(param.start - lengthMod, value);

                lengthMod += originalLength - input.Length;
            }

            result = input;
            return true;
        }

        public bool Run(out List<string?> columnNames, out List<string?> columnTypes, out List<List<object?>> rows)
        {
            columnNames = new();
            columnTypes = new();
            rows = new();

            string final;
            if (!InsertParameters(txtStatement.Text, out final, tabItem == null ? null : tabItem.Header.ToString()))
                return false;

            if (!App.SendSelectStatement(final, out columnNames, out rows, out columnTypes, builderWindow))
                return false;

            HashSet<int> dateCols = new();
            for (int i = 0; i < columnTypes.Count; ++i)
                if (columnTypes[i] == "Date")
                    dateCols.Add(i);

            try
            {
                relevantTable = RelevantTable.None;
                dtgOutput.Update(columnNames, rows, dateCols);
                int permissionRelevancy = -1;
                if (cmbRelevancy.Text == "Organisation")
                {
                    relevantTable = RelevantTable.Organisation;
                    permissionRelevancy = Glo.PERMISSION_RECORDS;
                }
                else if (cmbRelevancy.Text == "Asset")
                {
                    relevantTable = RelevantTable.Asset;
                    permissionRelevancy = Glo.PERMISSION_RECORDS;
                }
                else if (cmbRelevancy.Text == "Contact")
                {
                    relevantTable = RelevantTable.Contact;
                    permissionRelevancy = Glo.PERMISSION_RECORDS;
                }
                else if (cmbRelevancy.Text == "Conference")
                {
                    relevantTable = RelevantTable.Conference;
                    permissionRelevancy = Glo.PERMISSION_RECORDS;
                }
                else if (cmbRelevancy.Text == "Recurrence")
                {
                    relevantTable = RelevantTable.Recurrence;
                    permissionRelevancy = Glo.PERMISSION_RECORDS;
                }
                else if (cmbRelevancy.Text == "Resource")
                {
                    relevantTable = RelevantTable.Resource;
                    permissionRelevancy = Glo.PERMISSION_RECORDS;
                }
                else if (cmbRelevancy.Text == "Task")
                {
                    relevantTable = RelevantTable.Task;
                    permissionRelevancy = Glo.PERMISSION_TASKS;
                }
                else if (cmbRelevancy.Text == "Visit")
                {
                    relevantTable = RelevantTable.Visit;
                    permissionRelevancy = Glo.PERMISSION_TASKS;
                }
                else if (cmbRelevancy.Text == "Document")
                {
                    relevantTable = RelevantTable.Document;
                    permissionRelevancy = Glo.PERMISSION_TASKS;
                }
                btnDeleteSelected.IsEnabled = permissionRelevancy > -1 &&
                                              App.sd.deletePermissions[permissionRelevancy];
                btnUpdateSelected.IsEnabled = permissionRelevancy > -1 &&
                                              App.sd.editPermissions[permissionRelevancy];
                SetStatusBar(rows.Count, columnNames.Count);
            }
            catch (Exception e)
            {
                App.DisplayError("Unable to update SqlDataGrid. See error:\n\n" + e.Message, builderWindow);
                SetStatusBar();
                return false;
            }
            return true;
        }

        private void WipeCallback()
        {
            SetStatusBar();
        }

        private void SetStatusBar(params int[] vals)
        {
            if (vals.Length != 2)
            {
                lblRows.Content = "";
                lblColumns.Content = "";
                lblSelected.Content = "";
                return;
            }

            lblRows.Content = "Rows: " + vals[0].ToString();
            lblColumns.Content = "Columns: " + vals[1].ToString();

            lblRelevancy.Content = "Relevancy: " + cmbRelevancy.Text;

            dtgResults_SelectionChanged(dtgOutput, null);
        }
        // Updated the selected row count.
        private void dtgResults_SelectionChanged(object sender, RoutedEventArgs? e)
        {
            if (sender is CustomControls.SqlDataGrid sqlDataGrid && sqlDataGrid.dtg.Items.Count > 0)
            {
                lblSelected.Content = "Selected: " + sqlDataGrid.dtg.SelectedItems.Count;
                lblSelected.FontWeight = sqlDataGrid.dtg.SelectedItems.Count > 0 ? FontWeights.SemiBold :
                                                                                   FontWeights.Normal;
            }
        }

        private void dtgOutput_CustomDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (relevantTable == RelevantTable.None)
                return;

            if (relevantTable == RelevantTable.Organisation)
                App.EditOrganisation(dtgOutput.GetCurrentlySelectedID(), App.mainWindow);
            else if (relevantTable == RelevantTable.Asset)
                App.EditAsset(dtgOutput.GetCurrentlySelectedID(), App.mainWindow);
            else if (relevantTable == RelevantTable.Contact)
                App.EditContact(dtgOutput.GetCurrentlySelectedID(), App.mainWindow);
            else if (relevantTable == RelevantTable.Conference)
            {
                int id;
                if (!int.TryParse(dtgOutput.GetCurrentlySelectedID(), out id))
                    return;
                App.EditConference(id, builderWindow);
            }
            else if (relevantTable == RelevantTable.Recurrence)
            {
                int id;
                if (!int.TryParse(dtgOutput.GetCurrentlySelectedID(), out id))
                    return;
                App.EditRecurrence(id);
            }
            else if (relevantTable == RelevantTable.Resource)
                App.EditResource(dtgOutput.GetCurrentlySelectedID(), App.mainWindow);
            else if (relevantTable == RelevantTable.Task)
                App.EditTask(dtgOutput.GetCurrentlySelectedID(), App.mainWindow);
            else if (relevantTable == RelevantTable.Visit)
                App.EditVisit(dtgOutput.GetCurrentlySelectedID(), App.mainWindow);
            else if (relevantTable == RelevantTable.Document)
                App.EditDocument(dtgOutput.GetCurrentlySelectedID(), App.mainWindow);
        }
    }
}
