﻿using System;
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
        public void Update(Dictionary<string, ColumnRecord.Column> tableColDefs,
                           List<string?> columnNames, List<List<object?>> rows)
        {
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
                    header.Header = ColumnRecord.GetPrintName(s, tableColDefs[s]);
                    header.IsReadOnly = true;
                    if (tableColDefs[s].type == "DATE")
                    {
                        header.Binding = new Binding(string.Format("items[{0}]", count));
                        header.Binding.StringFormat = "{0:yy/MM/dd}";
                    }
                    else
                        header.Binding = new Binding(string.Format("items[{0}]", count));
                }
                if (s != Glo.Tab.NOTES)
                    dtg.Columns.Add(header);
                else
                    notes = header;
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
