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
using SendReceiveClasses;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace BridgeOpsClient
{
    public partial class PageSelectBuilder : Page
    {
        public Window? builderWindow;

        public PageSelectBuilder()
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

        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (dtgOutput.dtg.SelectedItems.Count < 1)
            {
                App.DisplayError("You must select at least one item to update.");
                return;
            }

            string table;
            string idColumn;
            int identity;
            if (relevantTable == RelevantTable.Organisation)
            {
                table = "Organisation";
                idColumn = Glo.Tab.ORGANISATION_ID;
                identity = 0;
            }
            else if (relevantTable == RelevantTable.Asset)
            {
                table = "Asset";
                idColumn = Glo.Tab.ASSET_ID;
                identity = 1;
            }
            else if (relevantTable == RelevantTable.Contact)
            {
                table = "Contact";
                idColumn = Glo.Tab.CONTACT_ID;
                identity = 2;
            }
            else
                return;

            var columns = ColumnRecord.GetDictionary(table, true);
            if (columns == null)
                return;

            UpdateMultiple updateMultiple = new(identity, table, columns,
                                                idColumn, dtgOutput.GetCurrentlySelectedIDs(), true);
            if (updateMultiple.ShowDialog() == true)
                Run(out _, out _, out _, true, true);
            // Error message for failed updates are displayed by the UpdateMultiple window.
        }
        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!App.DeleteConfirm(dtgOutput.dtg.SelectedItems.Count > 1))
                return;

            string table;
            string idColumn;
            int identity;
            if (relevantTable == RelevantTable.Organisation)
            {
                table = "Organisation";
                idColumn = Glo.Tab.ORGANISATION_ID;
                identity = 0;
            }
            else if (relevantTable == RelevantTable.Asset)
            {
                table = "Asset";
                idColumn = Glo.Tab.ASSET_ID;
                identity = 1;
            }
            else if (relevantTable == RelevantTable.Contact)
            {
                table = "Contact";
                idColumn = Glo.Tab.CONTACT_ID;
                identity = 2;
            }
            else
                return;

            if (App.SendDelete(table, idColumn, dtgOutput.GetCurrentlySelectedIDs(), true) &&
                MainWindow.pageDatabase != null)
            {
                MainWindow.pageDatabase.RepeatSearches(identity);
                Run(out _, out _, out _, true, true);
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

        MenuItem btnUpdateSelected;
        MenuItem btnDeleteSelected;

        public PageSelectBuilderJoin Join(int index) { return (PageSelectBuilderJoin)joins[index].page; }
        public PageSelectBuilderColumn Column(int index) { return (PageSelectBuilderColumn)columns[index].page; }
        public PageSelectBuilderWhere Where(int index) { return (PageSelectBuilderWhere)wheres[index].page; }
        public PageSelectBuilderOrderBy OrderBy(int index) { return (PageSelectBuilderOrderBy)orderBys[index].page; }

        private void btnAddJoin_Click(object sender, RoutedEventArgs e) { AddJoin(); }
        public PageSelectBuilderJoin AddJoin()
        {
            grdJoins.RowDefinitions.Add(new RowDefinition());
            Frame frame = new();
            PageSelectBuilderJoin join = new(this, frame);
            joins.Add(new FrameContent(frame, join));
            grdJoins.Children.Add(frame);
            Grid.SetRow(frame, joins.Count - 1);
            Grid.SetColumnSpan(frame, 4);
            for (int i = 0; i < joins.Count - 1; ++i)
                Join(i).ToggleUpDownButtons();
            join.ToggleUpDownButtons();
            return join;
        }

        private void btnAddColumn_Click(object sender, RoutedEventArgs e) { AddColumn(); }
        public PageSelectBuilderColumn AddColumn()
        {
            grdColumns.RowDefinitions.Add(new RowDefinition());
            Frame frame = new();
            PageSelectBuilderColumn column = new(this, frame);
            columns.Add(new FrameContent(frame, column));
            UpdateColumns();
            grdColumns.Children.Add(frame);
            Grid.SetRow(frame, columns.Count - 1);
            Grid.SetColumnSpan(frame, 4);
            for (int i = 0; i < columns.Count - 1; ++i)
                Column(i).ToggleUpDownButtons();
            column.ToggleUpDownButtons();
            return column;
        }

        private void btnAddWhere_Click(object sender, RoutedEventArgs e) { AddWhere(); }
        public PageSelectBuilderWhere AddWhere()
        {
            grdWheres.RowDefinitions.Add(new RowDefinition());
            Frame frame = new();
            PageSelectBuilderWhere where = new(this, frame);
            wheres.Add(new FrameContent(frame, where));
            UpdateColumns();
            grdWheres.Children.Add(frame);
            Grid.SetRow(frame, wheres.Count - 1);
            Grid.SetColumnSpan(frame, 4);
            for (int i = 0; i < wheres.Count - 1; ++i)
                Where(i).ToggleUpDownButtons();
            where.ToggleUpDownButtons();
            return where;
        }

        private void btnAddOrderBy_Click(object sender, RoutedEventArgs e) { AddOrderBy(); }
        public PageSelectBuilderOrderBy AddOrderBy()
        {
            grdOrderBys.RowDefinitions.Add(new RowDefinition());
            Frame frame = new();
            PageSelectBuilderOrderBy orderBy = new(this, frame);
            orderBys.Add(new FrameContent(frame, orderBy));
            UpdateColumns();
            grdOrderBys.Children.Add(frame);
            Grid.SetRow(frame, orderBys.Count - 1);
            Grid.SetColumnSpan(frame, 4);
            for (int i = 0; i < orderBys.Count - 1; ++i)
                OrderBy(i).ToggleUpDownButtons();
            orderBy.ToggleUpDownButtons();
            return orderBy;
        }

        public void MoveRow(FrameContent frameContent, bool up)
        {
            if (frameContent.page is PageSelectBuilderJoin)
            {
                int index = joins.IndexOf(frameContent);

                if (up && index > 0)
                {
                    joins.RemoveAt(index);
                    joins.Insert(index - 1, frameContent);
                }
                else if (!up && index < joins.Count - 1)
                {
                    joins.RemoveAt(index);
                    joins.Insert(index + 1, frameContent);
                }
                else return;

                for (int i = 0; i < joins.Count; ++i)
                    Grid.SetRow(joins[i].frame, i);
                for (int i = 0; i < joins.Count; ++i)
                    ((PageSelectBuilderJoin)joins[i].page).ToggleUpDownButtons();
            }
            else if (frameContent.page is PageSelectBuilderColumn)
            {
                int index = columns.IndexOf(frameContent);

                if (up && index > 0)
                {
                    columns.RemoveAt(index);
                    columns.Insert(index - 1, frameContent);
                }
                else if (!up && index < columns.Count - 1)
                {
                    columns.RemoveAt(index);
                    columns.Insert(index + 1, frameContent);
                }
                else return;

                for (int i = 0; i < columns.Count; ++i)
                    Grid.SetRow(columns[i].frame, i);
                for (int i = 0; i < columns.Count; ++i)
                    ((PageSelectBuilderColumn)columns[i].page).ToggleUpDownButtons();
            }
            else if (frameContent.page is PageSelectBuilderWhere)
            {
                int index = wheres.IndexOf(frameContent);

                if (up && index > 0)
                {
                    wheres.RemoveAt(index);
                    wheres.Insert(index - 1, frameContent);
                }
                else if (!up && index < wheres.Count - 1)
                {
                    wheres.RemoveAt(index);
                    wheres.Insert(index + 1, frameContent);
                }
                else return;

                for (int i = 0; i < wheres.Count; ++i)
                    Grid.SetRow(wheres[i].frame, i);
                for (int i = 0; i < wheres.Count; ++i)
                    ((PageSelectBuilderWhere)wheres[i].page).ToggleUpDownButtons();
            }
            else if (frameContent.page is PageSelectBuilderOrderBy)
            {
                int index = orderBys.IndexOf(frameContent);

                if (up && index > 0)
                {
                    orderBys.RemoveAt(index);
                    orderBys.Insert(index - 1, frameContent);
                }
                else if (!up && index < orderBys.Count - 1)
                {
                    orderBys.RemoveAt(index);
                    orderBys.Insert(index + 1, frameContent);
                }
                else return;

                for (int i = 0; i < orderBys.Count; ++i)
                    Grid.SetRow(orderBys[i].frame, i);
                for (int i = 0; i < orderBys.Count; ++i)
                    ((PageSelectBuilderOrderBy)orderBys[i].page).ToggleUpDownButtons();
            }
            else return;

            ResetTabIndices();
        }

        public void RemoveRow(object row)
        {
            bool removed = false;

            if (row is PageSelectBuilderJoin)
            {
                for (int i = 0; i < joins.Count; ++i)
                    if (joins[i].page == row && !removed)
                    {
                        grdJoins.Children.Remove(joins[i].frame);
                        joins.RemoveAt(i);
                        --i;
                        removed = true;
                        grdJoins.RowDefinitions.RemoveAt(0);
                    }
                    else
                        Grid.SetRow(joins[i].frame, i);

                for (int i = 0; i < joins.Count; ++i)
                    ((PageSelectBuilderJoin)joins[i].page).ToggleUpDownButtons();

                UpdateColumns();
            }
            else if (row is PageSelectBuilderColumn)
            {
                for (int i = 0; i < columns.Count; ++i)
                    if (columns[i].page == row && !removed)
                    {
                        grdColumns.Children.Remove(columns[i].frame);
                        columns.RemoveAt(i);
                        --i;
                        removed = true;
                        grdColumns.RowDefinitions.RemoveAt(0);
                    }
                    else
                        Grid.SetRow(columns[i].frame, i);

                for (int i = 0; i < columns.Count; ++i)
                    ((PageSelectBuilderColumn)columns[i].page).ToggleUpDownButtons();
            }
            else if (row is PageSelectBuilderWhere)
            {
                for (int i = 0; i < wheres.Count; ++i)
                    if (wheres[i].page == row && !removed)
                    {
                        grdWheres.Children.Remove(wheres[i].frame);
                        wheres.RemoveAt(i);
                        --i;
                        removed = true;
                        grdWheres.RowDefinitions.RemoveAt(0);
                    }
                    else
                        Grid.SetRow(wheres[i].frame, i);

                for (int i = 0; i < wheres.Count; ++i)
                    ((PageSelectBuilderWhere)wheres[i].page).ToggleUpDownButtons();
            }
            else if (row is PageSelectBuilderOrderBy)
            {
                for (int i = 0; i < orderBys.Count; ++i)
                    if (orderBys[i].page == row && !removed)
                    {
                        grdOrderBys.Children.Remove(orderBys[i].frame);
                        orderBys.RemoveAt(i);
                        --i;
                        removed = true;
                        grdOrderBys.RowDefinitions.RemoveAt(0);
                    }
                    else
                        Grid.SetRow(orderBys[i].frame, i);

                for (int i = 0; i < orderBys.Count; ++i)
                    ((PageSelectBuilderOrderBy)orderBys[i].page).ToggleUpDownButtons();
            }
            else return;

            ResetTabIndices();
        }

        void ResetTabIndices()
        {
            int i = 0;
            int tabStop = 2; // Start on 3 due to controls above.
            for (i = 0; i < joins.Count; ++i)
                joins[i].frame.TabIndex = i + tabStop;
            tabStop += i;
            btnAddColumn.TabIndex = tabStop++;
            for (i = 0; i < columns.Count; ++i)
                columns[i].frame.TabIndex = i + tabStop;
            tabStop += i;
            btnAddWhere.TabIndex = tabStop++;
            for (i = 0; i < wheres.Count; ++i)
                wheres[i].frame.TabIndex = i + tabStop;
            tabStop += i;
            btnAddOrderBy.TabIndex = tabStop++;
            for (i = 0; i < orderBys.Count; ++i)
                orderBys[i].frame.TabIndex = i + tabStop;
        }

        List<string> columnNameList = new();

        public string GetProperColumnName(string column)
        {
            if (column == "" || column.EndsWith('*'))
                return column;
            // All column names are displayed as "."
            int split = column.IndexOf('.') + 1;
            string table = column.Remove(split - 1);
            var dictionary = ColumnRecord.GetDictionary(table, false);
            if (dictionary == null)
                return "";
            return table + "." +
                   ColumnRecord.ReversePrintName(column.Substring(split, column.Length - split), dictionary);
        }

        public void UpdateColumns()
        {
            List<string> tableNames = new();

            // The selected index updated before the text, so check that rather than the text.
            if (cmbTable.SelectedIndex >= 0)
            {
                tableNames.Add((string)((ComboBoxItem)cmbTable.Items[cmbTable.SelectedIndex]).Content);
            }

            // Get the table names from each join, and add if not previously used.
            for (int i = 0; i < joins.Count; ++i)
                if (Join(i).cmbTable.SelectedIndex >= 0)
                {
                    string s = (string)((ComboBoxItem)Join(i).cmbTable.Items[Join(i).cmbTable.SelectedIndex]).Content;
                    if (!tableNames.Contains(s))
                        tableNames.Add(s);
                }

            List<string> columnList = new();
            foreach (string s in tableNames)
            {
                var dictionary = ColumnRecord.GetDictionary(s, true);
                if (dictionary != null)
                    foreach (DictionaryEntry de in dictionary)
                        columnList.Add(s + "." + ColumnRecord.GetPrintName(de));
            }

            for (int i = 0; i < joins.Count; ++i)
            {
                string thisJoinTable = "";
                if (Join(i).cmbTable.SelectedIndex >= 0)
                    thisJoinTable = (string)((ComboBoxItem)Join(i).cmbTable.Items[Join(i).cmbTable.SelectedIndex]).Content;
                if (thisJoinTable == "")
                    Join(i).cmbColumn2.ItemsSource = columnList;
                else
                {
                    List<string> ammendedList = new();
                    foreach (string s in columnList)
                        if (!s.StartsWith(thisJoinTable + "."))
                            ammendedList.Add(s);
                    Join(i).cmbColumn2.ItemsSource = ammendedList;
                }
            }
            for (int i = 0; i < columns.Count; ++i)
            {
                List<string> ammendedList = new();
                for (int t = 0; t < tableNames.Count; ++t)
                    ammendedList.Add(tableNames[t] + ".*");
                ammendedList.AddRange(columnList);
                Column(i).cmbColumn.ItemsSource = ammendedList;
            }
            for (int i = 0; i < wheres.Count; ++i)
                Where(i).cmbColumn.ItemsSource = columnList;
            for (int i = 0; i < orderBys.Count; ++i)
                OrderBy(i).cmbOrderBy.ItemsSource = columnList;
        }

        private void cmbTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateColumns();
        }

        // This is used to allow the opening of relevant windows by double clicking on the SqlDataGrid when a
        // compatible query has been run, i.e. ID as the first column.
        enum RelevantTable { None, Organisation, Asset, Contact, Conference, Recurrence, Resource }
        RelevantTable relevantTable = RelevantTable.None;

        // Used for retaining friendly names as we don't have the dictionaries to help us in the output stage.
        private List<string> chosenColumnNames = new();
        private SelectRequest selectRequest = new();
        private bool BuildQuery()
        {
            chosenColumnNames = new();

            bool Abort(string message)
            {
                App.DisplayError(message);
                return false;
            }

            string table;
            List<string> joinTables = new();
            List<string> joinColumns1 = new();
            List<string> joinColumns2 = new();
            List<string> joinTypes = new();
            List<string> selectColumns = new();
            List<string> columnAliases = new();
            List<string> whereColumns = new();
            List<string> whereOperators = new();
            List<string?> whereValues = new();
            List<bool> whereValueTypesNeedQuotes = new();
            List<string> whereAndOrs = new();
            List<int> whereBracketsOpen = new();
            List<int> whereBracketsClose = new();
            List<string> selectOrderBys = new();
            List<bool> orderByAsc = new();

            // Table
            if (cmbTable.SelectedIndex < 0 || cmbTable.Text == "")
                return Abort("Must select a table to query.");
            table = cmbTable.Text;

            // Columns
            if (columns.Count == 0)
                return Abort("Must select at least one column.");
            for (int i = 0; i < columns.Count; ++i)
            {
                PageSelectBuilderColumn col = Column(i);
                if (col.cmbColumn.SelectedIndex < 0 || col.cmbColumn.Text == "")
                    return Abort("All column fields must contain a selection.");
                string colText = col.cmbColumn.Text;
                string colName = colText.Substring(colText.IndexOf('.') + 1);
                if (colText.EndsWith("*"))
                {
                    string colTable = colText.Remove(colText.IndexOf('.'));
                    var dictionary = ColumnRecord.GetDictionary(colTable, true);
                    foreach (DictionaryEntry de in dictionary!)
                    {
                        selectColumns.Add(colTable + "." + (string)de.Key);
                        columnAliases.Add("");
                        chosenColumnNames.Add(ColumnRecord.GetPrintName(de));
                    }
                }
                else
                {
                    selectColumns.Add(GetProperColumnName(col.cmbColumn.Text));
                    columnAliases.Add(col.txtAlias.Visibility == Visibility.Visible ? col.txtAlias.Text : "");
                    chosenColumnNames.Add(col.txtAlias.Text == "" ? colName : col.txtAlias.Text);
                }
            }

            // Joins
            for (int i = 0; i < joins.Count; ++i)
            {
                PageSelectBuilderJoin join = Join(i);
                if (join.cmbTable.SelectedIndex < 0 || join.cmbTable.Text == "")
                    return Abort("All JOINs must reference a table.");
                if (join.cmbColumn1.SelectedIndex < 0 || join.cmbColumn1.Text == "" ||
                    join.cmbColumn2.SelectedIndex < 0 || join.cmbColumn2.Text == "")
                    return Abort("All JOINs must state which columns to join the tables on.");
                joinTables.Add(join.cmbTable.Text);
                joinColumns1.Add(GetProperColumnName(join.cmbTable.Text + "." + join.cmbColumn1.Text));
                joinColumns2.Add(GetProperColumnName(join.cmbColumn2.Text));
                joinTypes.Add(join.cmbType.Text);
            }

            for (int i = 0; i < joinTables.Count - 1; ++i)
                for (int j = i + 1; j < joinTables.Count; ++j)
                    if (joinTables[i] == joinTables[j])
                        return Abort("Each table cannot be selected more than once.");

            // Where
            for (int i = 0; i < wheres.Count; ++i)
            {
                PageSelectBuilderWhere where = Where(i);
                if (where.cmbColumn.SelectedIndex < 0 || where.cmbColumn.Text == "")
                    return Abort("All WHERE clauses must reference a column.");
                if (!where.cmbOperator.Text.Contains("NULL"))
                    if (ColumnRecord.IsTypeInt(where.type) && where.numValue.GetNumber() == null)
                        return Abort("All values for INT fields must be a whole number.");
                whereColumns.Add(GetProperColumnName(where.cmbColumn.Text));
                whereOperators.Add(where.cmbOperator.Text);
                whereAndOrs.Add(where.cmbAndOr.Text); // Ignored for the first condition in SQL statement.
                if (where.cmbOperator.Text.Contains("NULL"))
                    whereValues.Add(null);
                else
                {
                    if (where.txtValue.Visibility == Visibility.Visible)
                        whereValues.Add(where.txtValue.Text);
                    else if (where.cmbValue.Visibility == Visibility.Visible)
                        whereValues.Add(where.cmbValue.Text);
                    else if (where.numValue.Visibility == Visibility.Visible)
                        whereValues.Add(where.numValue.GetNumber().ToString());
                    else if (where.dtmValue.Visibility == Visibility.Visible)
                    {
                        DateTime? dt = where.dtmValue.GetDateTime();
                        if (dt == null)
                            return Abort("Must select a value for all WHERE value fields.");
                        whereValues.Add(SqlAssist.DateTimeToSQL((DateTime)dt, false));
                    }
                    else if (where.datValue.Visibility == Visibility.Visible)
                    {
                        DateTime? dt = where.datValue.SelectedDate;
                        if (dt == null)
                            return Abort("Must select a value for all WHERE value fields.");
                        whereValues.Add(SqlAssist.DateTimeToSQL((DateTime)dt, true));
                    }
                    else if (where.timValue.Visibility == Visibility.Visible)
                    {
                        TimeSpan? ts = where.timValue.GetTime();
                        if (ts == null)
                            return Abort("Must select a value for all WHERE value fields.");
                        whereValues.Add(SqlAssist.TimeSpanToSQL((TimeSpan)ts));
                    }
                    else if (where.chkValue.Visibility == Visibility.Visible)
                        whereValues.Add(where.chkValue.IsChecked == true ? "1" : "0");
                    else
                        return Abort("All WHERE clauses must either be null or contain a value.");
                }
                whereValueTypesNeedQuotes.Add(!ColumnRecord.IsTypeInt(where.type) &&
                                              where.type != "BIT" &&
                                              !where.type.Contains("BOOLEAN"));
            }
            if (whereAndOrs.Count > 0)
                whereAndOrs.RemoveAt(0);

            // Order By
            for (int i = 0; i < orderBys.Count; ++i)
            {
                PageSelectBuilderOrderBy orderBy = OrderBy(i);
                if (orderBy.cmbOrderBy.SelectedIndex < 0 || orderBy.cmbOrderBy.Text == "")
                    return Abort("All ORDER BY fields must have a column selected.");
                selectOrderBys.Add(GetProperColumnName(orderBy.cmbOrderBy.Text));
                orderByAsc.Add(orderBy.cmbAscDesc.Text == "ASC");
            }

            selectRequest = new(App.sd.sessionID, ColumnRecord.columnRecordID,
                                table, chkDistinct.IsChecked == true,
                                joinTables, joinColumns1, joinColumns2, joinTypes,
                                selectColumns, columnAliases,
                                whereColumns, whereOperators, whereValues, whereValueTypesNeedQuotes,
                                whereBracketsOpen, whereBracketsClose, whereAndOrs,
                                selectOrderBys, orderByAsc);

            return true;
        }

        private void DisplayCode()
        {
            txtCode.Text = selectRequest.SqlSelect();
            dtgOutput.Wipe();
            btnDeleteSelected.IsEnabled = false;
            btnUpdateSelected.IsEnabled = false;
        }

        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            Run(out _, out _, out _, true);
        }

        public bool Run(out List<string?> columnNames, out List<string?> columnTypes, out List<List<object?>> rows,
                        bool fillGrid)
        {
            return Run(out columnNames, out columnTypes, out rows, fillGrid, false);
        }
        public bool Run(out List<string?> columnNames, out List<string?> columnTypes, out List<List<object?>> rows,
                        bool fillGrid, bool useLast)
        {
            if (useLast || BuildQuery())
            {
                if (!useLast)
                    DisplayCode();
                tabOutput.Focus();
                App.SendSelectRequest(selectRequest, out columnNames, out rows, out columnTypes);
                if (columnNames.Count == chosenColumnNames.Count)
                    for (int i = 0; i < columnNames.Count; ++i)
                        columnNames[i] = chosenColumnNames[i];

                HashSet<int> dateCols = new();
                for (int i = 0; i < columnTypes.Count; ++i)
                    if (columnTypes[i] == "Date")
                        dateCols.Add(i);

                if (fillGrid)
                    try
                    {
                        relevantTable = RelevantTable.None;
                        dtgOutput.Update(columnNames, rows, dateCols);
                        int permissionRelevancy = -1;
                        if (selectRequest.columns[0] == $"Organisation.{Glo.Tab.ORGANISATION_ID}" ||
                            selectRequest.columns[0] == $"OrganisationChange.{Glo.Tab.ORGANISATION_ID}")
                        {
                            relevantTable = RelevantTable.Organisation;
                            permissionRelevancy = Glo.PERMISSION_RECORDS;
                        }
                        else if (selectRequest.columns[0] == $"Asset.{Glo.Tab.ASSET_ID}" ||
                                 selectRequest.columns[0] == $"AssetChange.{Glo.Tab.ASSET_ID}")
                        {
                            relevantTable = RelevantTable.Asset;
                            permissionRelevancy = Glo.PERMISSION_RECORDS;
                        }
                        else if (selectRequest.columns[0] == $"Contact.{Glo.Tab.CONTACT_ID}")
                        {
                            relevantTable = RelevantTable.Contact;
                            permissionRelevancy = Glo.PERMISSION_RECORDS;
                        }
                        else if (selectRequest.columns[0] == $"Conference.{Glo.Tab.CONFERENCE_ID}")
                        {
                            relevantTable = RelevantTable.Conference;
                            permissionRelevancy = Glo.PERMISSION_CONFERENCES;
                        }
                        else if (selectRequest.columns[0] == $"Recurrence.{Glo.Tab.RECURRENCE_ID}")
                        {
                            relevantTable = RelevantTable.Recurrence;
                            permissionRelevancy = Glo.PERMISSION_CONFERENCES;
                        }
                        else if (selectRequest.columns[0] == $"Resource.{Glo.Tab.RESOURCE_ID}")
                        {
                            relevantTable = RelevantTable.Resource;
                            permissionRelevancy = Glo.PERMISSION_RESOURCES;
                        }
                        btnDeleteSelected.IsEnabled = permissionRelevancy > -1 &&
                                                      App.sd.deletePermissions[permissionRelevancy];
                        btnUpdateSelected.IsEnabled = permissionRelevancy > -1 &&
                                                      App.sd.editPermissions[permissionRelevancy];
                        SetStatusBar(rows.Count, columnNames.Count);
                    }
                    catch (Exception e)
                    {
                        App.DisplayError("Unable to update SqlDataGrid. See error:\n\n" + e.Message);
                        SetStatusBar();
                        return false;
                    }
                return true;
            }
            columnNames = new();
            columnTypes = new();
            rows = new();
            return false;
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

        private void btnDisplayCode_Click(object sender, RoutedEventArgs e)
        {
            if (BuildQuery())
                DisplayCode();
            else
                txtCode.Text = "SQL code could not be generated.";
            tabCode.Focus();
        }

        private void dtgOutput_CustomDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (relevantTable == RelevantTable.None)
                return;

            if (relevantTable == RelevantTable.Organisation)
                App.EditOrganisation(dtgOutput.GetCurrentlySelectedID());
            else if (relevantTable == RelevantTable.Asset)
                App.EditAsset(dtgOutput.GetCurrentlySelectedID());
            else if (relevantTable == RelevantTable.Contact)
                App.EditContact(dtgOutput.GetCurrentlySelectedID());
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
                EditRecurrence editRec = new(id);
                editRec.Show();
            }
            else if (relevantTable == RelevantTable.Resource)
                App.EditResource(dtgOutput.GetCurrentlySelectedID());
        }
    }
}
