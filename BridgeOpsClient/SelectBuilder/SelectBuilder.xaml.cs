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

namespace BridgeOpsClient
{
    public partial class SelectBuilder : Window
    {
        public SelectBuilder()
        {
            InitializeComponent();
        }

        public List<Frame> joins = new();
        public List<Frame> columns = new();
        public List<Frame> wheres = new();
        public List<Frame> orderBys = new();

        public PageSelectBuilderJoin Join(int index) { return (PageSelectBuilderJoin)joins[index].Content; }
        public PageSelectBuilderColumn Column(int index) { return (PageSelectBuilderColumn)columns[index].Content; }
        public PageSelectBuilderWhere Where(int index) { return (PageSelectBuilderWhere)wheres[index].Content; }
        public PageSelectBuilderOrderBy OrderBy(int index) { return (PageSelectBuilderOrderBy)orderBys[index].Content; }

        private void btnAddJoin_Click(object sender, RoutedEventArgs e)
        {
            grdJoins.RowDefinitions.Add(new RowDefinition());
            Frame frame = new();
            PageSelectBuilderJoin join = new(this, frame);
            frame.Content = join;
            joins.Add(frame);
            grdJoins.Children.Add(frame);
            Grid.SetRow(frame, joins.Count - 1);
            Grid.SetColumnSpan(frame, 4);
            for (int i = 0; i < joins.Count - 1; ++i)
                Join(i).ToggleUpDownButtons();
            join.ToggleUpDownButtons();
        }

        private void btnAddColumn_Click(object sender, RoutedEventArgs e)
        {
            grdColumns.RowDefinitions.Add(new RowDefinition());
            Frame frame = new();
            PageSelectBuilderColumn column = new(this, frame);
            frame.Content = column;
            columns.Add(frame);
            grdColumns.Children.Add(frame);
            Grid.SetRow(frame, columns.Count - 1);
            Grid.SetColumnSpan(frame, 4);
            for (int i = 0; i < columns.Count - 1; ++i)
                Column(i).ToggleUpDownButtons();
            column.ToggleUpDownButtons();
        }

        private void btnAddWhere_Click(object sender, RoutedEventArgs e)
        {
            grdWheres.RowDefinitions.Add(new RowDefinition());
            Frame frame = new();
            PageSelectBuilderWhere where = new(this, frame);
            frame.Content = where;
            wheres.Add(frame);
            grdWheres.Children.Add(frame);
            Grid.SetRow(frame, wheres.Count - 1);
            Grid.SetColumnSpan(frame, 4);
            for (int i = 0; i < wheres.Count - 1; ++i)
                Where(i).ToggleUpDownButtons();
            where.ToggleUpDownButtons();
        }

        private void btnAddOrderBy_Click(object sender, RoutedEventArgs e)
        {
            grdOrderBys.RowDefinitions.Add(new RowDefinition());
            Frame frame = new();
            PageSelectBuilderOrderBy orderBy = new(this, frame);
            frame.Content = orderBy;
            orderBys.Add(frame);
            grdOrderBys.Children.Add(frame);
            Grid.SetRow(frame, orderBys.Count - 1);
            Grid.SetColumnSpan(frame, 4);
            for (int i = 0; i < orderBys.Count - 1; ++i)
                OrderBy(i).ToggleUpDownButtons();
            orderBy.ToggleUpDownButtons();
        }

        public void MoveRow(object row, Frame frame, bool up)
        {
            if (row is PageSelectBuilderJoin)
            {
                int index = joins.IndexOf(frame);

                if (up && index > 0)
                {
                    joins.RemoveAt(index);
                    joins.Insert(index - 1, frame);
                }
                else if (!up && index < joins.Count - 1)
                {
                    joins.RemoveAt(index);
                    joins.Insert(index + 1, frame);
                }
                else return;

                for (int i = 0; i < joins.Count; ++i)
                    Grid.SetRow(joins[i], i);
                for (int i = 0; i < joins.Count; ++i)
                    ((PageSelectBuilderJoin)joins[i].Content).ToggleUpDownButtons();
            }
            else if (row is PageSelectBuilderColumn)
            {
                int index = columns.IndexOf(frame);

                if (up && index > 0)
                {
                    columns.RemoveAt(index);
                    columns.Insert(index - 1, frame);
                }
                else if (!up && index < columns.Count - 1)
                {
                    columns.RemoveAt(index);
                    columns.Insert(index + 1, frame);
                }
                else return;

                for (int i = 0; i < columns.Count; ++i)
                    Grid.SetRow(columns[i], i);
                for (int i = 0; i < columns.Count; ++i)
                    ((PageSelectBuilderColumn)columns[i].Content).ToggleUpDownButtons();
            }
            else if (row is PageSelectBuilderWhere)
            {
                int index = wheres.IndexOf(frame);

                if (up && index > 0)
                {
                    wheres.RemoveAt(index);
                    wheres.Insert(index - 1, frame);
                }
                else if (!up && index < wheres.Count - 1)
                {
                    wheres.RemoveAt(index);
                    wheres.Insert(index + 1, frame);
                }
                else return;

                for (int i = 0; i < wheres.Count; ++i)
                    Grid.SetRow(wheres[i], i);
                for (int i = 0; i < wheres.Count; ++i)
                    ((PageSelectBuilderWhere)wheres[i].Content).ToggleUpDownButtons();
            }
            else if (row is PageSelectBuilderOrderBy)
            {
                int index = orderBys.IndexOf(frame);

                if (up && index > 0)
                {
                    orderBys.RemoveAt(index);
                    orderBys.Insert(index - 1, frame);
                }
                else if (!up && index < orderBys.Count - 1)
                {
                    orderBys.RemoveAt(index);
                    orderBys.Insert(index + 1, frame);
                }
                else return;

                for (int i = 0; i < orderBys.Count; ++i)
                    Grid.SetRow(orderBys[i], i);
                for (int i = 0; i < orderBys.Count; ++i)
                    ((PageSelectBuilderOrderBy)orderBys[i].Content).ToggleUpDownButtons();
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
                    if (joins[i].Content == row && !removed)
                    {
                        grdJoins.Children.Remove(joins[i]);
                        joins.RemoveAt(i);
                        --i;
                        removed = true;
                        grdJoins.RowDefinitions.RemoveAt(0);
                    }
                    else
                        Grid.SetRow(joins[i], i);

                for (int i = 0; i < joins.Count; ++i)
                    ((PageSelectBuilderJoin)joins[i].Content).ToggleUpDownButtons();

                UpdateColumns();
            }
            else if (row is PageSelectBuilderColumn)
            {
                for (int i = 0; i < columns.Count; ++i)
                    if (columns[i].Content == row && !removed)
                    {
                        grdColumns.Children.Remove(columns[i]);
                        columns.RemoveAt(i);
                        --i;
                        removed = true;
                        grdColumns.RowDefinitions.RemoveAt(0);
                    }
                    else
                        Grid.SetRow(columns[i], i);

                for (int i = 0; i < columns.Count; ++i)
                    ((PageSelectBuilderColumn)columns[i].Content).ToggleUpDownButtons();
            }
            else if (row is PageSelectBuilderWhere)
            {
                for (int i = 0; i < wheres.Count; ++i)
                    if (wheres[i].Content == row && !removed)
                    {
                        grdWheres.Children.Remove(wheres[i]);
                        wheres.RemoveAt(i);
                        --i;
                        removed = true;
                        grdWheres.RowDefinitions.RemoveAt(0);
                    }
                    else
                        Grid.SetRow(wheres[i], i);

                for (int i = 0; i < wheres.Count; ++i)
                    ((PageSelectBuilderWhere)wheres[i].Content).ToggleUpDownButtons();
            }
            else if (row is PageSelectBuilderOrderBy)
            {
                for (int i = 0; i < orderBys.Count; ++i)
                    if (orderBys[i].Content == row && !removed)
                    {
                        grdOrderBys.Children.Remove(orderBys[i]);
                        orderBys.RemoveAt(i);
                        --i;
                        removed = true;
                        grdOrderBys.RowDefinitions.RemoveAt(0);
                    }
                    else
                        Grid.SetRow(orderBys[i], i);

                for (int i = 0; i < orderBys.Count; ++i)
                    ((PageSelectBuilderOrderBy)orderBys[i].Content).ToggleUpDownButtons();
            }
            else return;

            ResetTabIndices();
        }

        void ResetTabIndices()
        {
            int i = 0;
            int tabStop = 2; // Start on 3 due to controls above.
            for (i = 0; i < joins.Count; ++i)
                joins[i].TabIndex = i + tabStop;
            tabStop += i;
            btnAddColumn.TabIndex = tabStop++;
            for (i = 0; i < columns.Count; ++i)
                columns[i].TabIndex = i + tabStop;
            tabStop += i;
            btnAddWhere.TabIndex = tabStop++;
            for (i = 0; i < wheres.Count; ++i)
                wheres[i].TabIndex = i + tabStop;
            tabStop += i;
            btnAddOrderBy.TabIndex = tabStop++;
            for (i = 0; i < orderBys.Count; ++i)
                orderBys[i].TabIndex = i + tabStop;
        }

        List<string> columnNameList = new();

        private string GetProperColumnName(string column)
        {
            if (column.EndsWith('*'))
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
                    foreach (var kvp in dictionary)
                        columnList.Add(s + "." + ColumnRecord.GetPrintName(kvp));
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

        private SelectRequest selectRequest = new();
        private bool BuildQuery()
        {
            bool Abort(string message)
            {
                MessageBox.Show(message);
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
                selectColumns.Add(GetProperColumnName(col.cmbColumn.Text));
                columnAliases.Add(col.txtAlias.Text);
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
                for (int j = i + 1; j < joinTables.Count; ++i)
                    if (joinTables[i] == joinTables[j])
                        return Abort("Each table cannot be selected more than once.");

            // Where
            for (int i = 0; i < wheres.Count; ++i)
            {
                PageSelectBuilderWhere where = Where(i);
                if (where.cmbColumn.SelectedIndex < 0 || where.cmbColumn.Text == "")
                    return Abort("All WHERE clauses must reference a column.");
                if (!where.cmbOperator.Text.Contains("NULL"))
                    if (ColumnRecord.IsTypeInt(where.type) && !int.TryParse(where.txtValue.Text, out _))
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
                    else if (where.dtmValue.Visibility == Visibility.Visible)
                        whereValues.Add(SqlAssist.DateTimeToSQL(where.dtmValue.GetDateTime(), false));
                    else if (where.datValue.Visibility == Visibility.Visible)
                        whereValues.Add(SqlAssist.DateTimeToSQL(where.dtmValue.GetDateTime(), true));
                    else if (where.timValue.Visibility == Visibility.Visible)
                        whereValues.Add(SqlAssist.TimeSpanToSQL(where.timValue.GetTime()));
                    else if (where.chkValue.Visibility == Visibility.Visible)
                        whereValues.Add(where.chkValue.IsChecked == true ? "1" : "0");
                    else
                        return Abort("All WHERE clauses must either be null or contain a value.");
                }
                whereValueTypesNeedQuotes.Add(!ColumnRecord.IsTypeInt(where.type) &&
                                              where.type != "BIT" &&
                                              !where.type.Contains("BOOLEAN"));
            }

            // Order By
            for (int i = 0; i < orderBys.Count; ++i)
            {
                PageSelectBuilderOrderBy orderBy = OrderBy(i);
                if (orderBy.cmbOrderBy.SelectedIndex < 0 || orderBy.cmbOrderBy.Text == "")
                    return Abort("All ORDER BY fields must have a column selected.");
                selectOrderBys.Add(GetProperColumnName(orderBy.cmbOrderBy.Text));
            }

            selectRequest = new(App.sd.sessionID, ColumnRecord.columnRecordID,
                                table, chkDistinct.IsChecked == true,
                                joinTables, joinColumns1, joinColumns2, joinTypes,
                                selectColumns, columnAliases,
                                whereColumns, whereOperators, whereValues, whereValueTypesNeedQuotes,
                                whereBracketsOpen, whereBracketsClose, whereAndOrs,
                                selectOrderBys);

            return true;
        }

        private void DisplayCode()
        {
            txtCode.Text = selectRequest.SqlSelect();
        }

        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            if (BuildQuery())
            {
                DisplayCode();
                tabOutput.Focus();
                List<string?> columnNames;
                List<List<object?>> rows;
                App.SendSelectRequest(selectRequest, out columnNames, out rows);
                dtgOutput.Update(columnNames, rows);
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
    }
}
