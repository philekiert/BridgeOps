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
using System.Windows.Navigation;
using System.Windows.Shapes;
using static BridgeOpsClient.CustomControls.SqlDataGrid;

namespace BridgeOpsClient.CustomControls
{
    public partial class SqlDataGrid : UserControl
    {
        public SqlDataGrid()
        {
            InitializeComponent();
        }

        public struct Row
        {
            public List<object?> items { get; set; }

            public Row(List<object?> items)
            {
                this.items = items;
            }
        }

        public void Wipe()
        {
            dtg.ItemsSource = null;
            dtg.Columns.Clear();
        }

        List<Row> rowsBinder = new();

        // I have allowed more than one dictionary to be fed to this array, because sometimes you want a select result
        // that joins two tables. The downside is that tables sharing column names need to use the same type and
        // friendly name. I can't see this being an issue, but note it for future reference.
        public void Update(Dictionary<string, ColumnRecord.Column> tableColDefs,
                           List<string?> columnNames, List<List<object?>> rows, params string[] omitColumns)
        {
            Update(new List<Dictionary<string, ColumnRecord.Column>>() { tableColDefs },
                   columnNames, rows, omitColumns);
        }
        public void Update(List<Dictionary<string, ColumnRecord.Column>> tableColDefs,
                           List<string?> columnNames, List<List<object?>> rows, params string[] omitColumns)
        {
            // Add any columns to omit to a dictionary for fast lookup.
            Dictionary<string, bool> columnstoOmit = new();
            if (omitColumns.Length > 0)
            {
                foreach (string s in omitColumns)
                    columnstoOmit.Add(s, true);
            }

            dtg.Columns.Clear();
            dtg.ItemsSource = null;

            int count = 0;

            DataGridTextColumn? notes = null; // We want to add this one last.
            foreach (string? s in columnNames)
            {
                DataGridTextColumn header = new();
                header.MaxWidth = 256;
                if (s == null)
                    header.Header = "";
                else
                {
                    ColumnRecord.Column col = new ColumnRecord.Column();
                    foreach (Dictionary<string, ColumnRecord.Column> defs in tableColDefs)
                    {
                        if (defs.ContainsKey(s))
                        {
                            col = defs[s];
                            break;
                        }
                    }

                    header.Header = ColumnRecord.GetPrintName(s, col);
                    header.IsReadOnly = true;
                    if (col.type == "DATE")
                    {
                        header.Binding = new Binding(string.Format("items[{0}]", count));
                        header.Binding.StringFormat = "{0:dd/MM/yyyy}";
                    }
                    else
                        header.Binding = new Binding(string.Format("items[{0}]", count));
                }
                if (s == Glo.Tab.NOTES) // We want notes at the end at the moment.
                    notes = header;
                else if (s != null && columnstoOmit.ContainsKey(s))
                    header.Visibility = Visibility.Hidden;
                else
                    dtg.Columns.Add(header);
                ++count;
            }

            // Move the notes column to the end.
            if (notes != null)
                dtg.Columns.Add(notes);

            // Data
            rowsBinder = new();
            foreach (List<object?> row in rows)
                rowsBinder.Add(new Row(row));

            dtg.ItemsSource = rowsBinder;
        }

        public string GetCurrentlySelectedID()
        {
            if (dtg.SelectedItem == null)
                return "";
            Row selectedRow = (Row)dtg.SelectedItem;
            object? firstItem = selectedRow.items[0];
            if (firstItem == null)
                return "";
            string? id = firstItem.ToString();
            if (id == null)
                return "";
            return id;
        }
    }
}
