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

        public PageSelectBuilderJoin Join(int index) { return (PageSelectBuilderJoin)joins[index].Content; }
        public PageSelectBuilderColumn Column(int index) { return (PageSelectBuilderColumn)columns[index].Content; }
        public PageSelectBuilderWhere Where(int index) { return (PageSelectBuilderWhere)wheres[index].Content; }

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
        }

        List<string> columnNameList = new();

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
                Join(i).cmbColumn2.ItemsSource = columnList;
            for (int i = 0; i < columns.Count; ++i)
                Column(i).cmbColumn.ItemsSource = columnList;
            for (int i = 0; i < wheres.Count; ++i)
                Where(i).cmbColumn.ItemsSource = columnList;
        }

        private void cmbTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateColumns();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Width += 1;
            UpdateLayout();
        }
    }
}
