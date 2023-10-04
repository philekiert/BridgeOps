using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Data;
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

namespace BridgeOpsClient
{
    public partial class PageDatabaseView : Page
    {
        // This list holds the original column names of respective ComboBoxItems for easier lookup.
        Dictionary<string, string> friendlyNameReversal = new();

        public PageDatabaseView()
        {
            InitializeComponent();

            // This will trigger cmbTableSelectionchanged() and therefore PopulateColumnComboBox().
            cmbTable.SelectedIndex = 0;
        }

        private void PopulateColumns()
        {
            Dictionary<string, ColumnRecord.Column> table;
            if (cmbTable.SelectedIndex == 0)
                table = ColumnRecord.organisation;
            else if (cmbTable.SelectedIndex == 1)
                table = ColumnRecord.asset;
            else
                table = ColumnRecord.contact;

            cmbColumn.Items.Clear();
            dtgResults.Items.Clear();
            friendlyNameReversal.Clear();

            if (cmbTable.SelectedIndex == 2)
                cmbColumn.Items.RemoveAt(0); // Removed Contact_ID as it's not required.

            cmbColumn.SelectedIndex = 0;
        }

        private void cmbTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PopulateColumns();
        }

        private void btnListAll_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<string, ColumnRecord.Column> tableColDefs;
            if (cmbTable.Text == "Organisation")
                tableColDefs = ColumnRecord.organisation;
            else if (cmbTable.Text == "Asset")
                tableColDefs = ColumnRecord.asset;
            else // if == "Contact"
                tableColDefs = ColumnRecord.contact;

            List<string?> columnNames;
            List<List<string?>> rows;
            if (App.SelectAll(cmbTable.Text, out columnNames, out rows))
            {
                dtgResults.Columns.Clear();
                foreach (string? s in columnNames)
                {
                    DataGridTextColumn header = new();
                    if (s == null)
                        header.Header = "";
                    else
                    {
                        header.Header = ColumnRecord.GetPrintName(s, tableColDefs[s]);
                        header.Header = s;
                        header.Binding = new Binding("rows[0][0]");
                    }
                    dtgResults.Columns.Add(header);
                }
                

                // Data
                dtgResults.ItemsSource = rows;

            }
        }
    }
}
