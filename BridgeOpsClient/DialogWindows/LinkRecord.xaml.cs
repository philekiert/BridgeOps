﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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
    public partial class LinkRecord : CustomWindow
    {
        string table = "";
        OrderedDictionary columns = new();
        public string? id = null;

        int idColumn = 0;

        public LinkRecord(string table, OrderedDictionary columns)
        {
            InitializeComponent();

            this.table = table;
            this.columns = columns;

            // Error message is displayed by App.SelectAll() if something goes wrong.
            List<string?> columnNames;
            List<List<object?>> rows;
            if (App.SelectAll(table, out columnNames, out rows, false))
                dtg.Update(columns, columnNames, rows);
            dtg.CustomDoubleClick += dtg_DoubleClick;

            txtSearch.Focus();
        }

        public LinkRecord(List<string?> columns, List<List<object?>> data, int idColumn)
        {
            this.idColumn = idColumn;

            InitializeComponent();

            dtg.Update(columns, data);
            dtg.CustomDoubleClick += dtg_DoubleClick;

            txtSearch.Focus();
        }

        private void dtg_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            id = dtg.GetCurrentlySelectedCell(idColumn);
            Close();
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            MaxHeight = double.PositiveInfinity;
            MaxWidth = double.PositiveInfinity;
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = txtSearch.Text.ToLower();

            // This is Copilot's code. I've modified it and seems to make sense, but I have little knowledge
            // of this approach, so hopefully it's the most efficient way.
            ICollectionView collectionView = CollectionViewSource.GetDefaultView(dtg.dtg.ItemsSource);

            if (collectionView != null)
            {
                collectionView.Filter = item =>
                {
                    var row = (CustomControls.SqlDataGrid.Row)item;
                    if (row.items == null)
                        return false;

                    foreach (object? cell in row.items)
                        if (cell != null && cell.ToString()!.ToLower().Contains(text))
                            return true;
                    return false;
                };

                // Refresh the view to apply the filter
                collectionView.Refresh();
            }
        }
    }
}
