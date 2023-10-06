using SendReceiveClasses;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
using System.Xml.Linq;
using static BridgeOpsClient.CustomControls.SqlDataGrid;

namespace BridgeOpsClient
{
    public partial class PageDatabaseView : Page
    {
        public PageDatabaseView()
        {
            InitializeComponent();

            // This will trigger cmbTableSelectionchanged(), which will PopulateColumnComboBox().
            cmbTable.SelectedIndex = 0;

            dtgResults.MouseDoubleClick += dtg_DoubleClick;
        }

        private void PopulateColumnComboBox()
        {
            Dictionary<string, ColumnRecord.Column> table;
            if (cmbTable.SelectedIndex == 0)
                table = ColumnRecord.organisation;
            else if (cmbTable.SelectedIndex == 1)
                table = ColumnRecord.asset;
            else
                table = ColumnRecord.contact;

            cmbColumn.Items.Clear();
            dtgResults.Wipe();

            foreach (KeyValuePair<string, ColumnRecord.Column> kvp in table)
            {
                string name = ColumnRecord.GetPrintName(kvp);
                cmbColumn.Items.Add(name);
            }

            if (cmbTable.SelectedIndex == 2 && cmbColumn.Items.Count > 0)
                cmbColumn.Items.RemoveAt(0); // Removed Contact_ID as it's not required.

            cmbColumn.SelectedIndex = 0;
        }

        struct Row
        {
            public List<object?> items { get; set; }

            public Row(List<object?> items)
            {
                this.items = items;
            }
        }

        private void cmbTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PopulateColumnComboBox();
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
            List<List<object?>> rows;
            if (App.SelectAll(cmbTable.Text, out columnNames, out rows))
                dtgResults.Update(tableColDefs, columnNames, rows);
        }

        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<string, ColumnRecord.Column> tableColDefs;
            Dictionary<string, string> nameReversals;
            if (cmbTable.Text == "Organisation")
            {
                tableColDefs = ColumnRecord.organisation;
                nameReversals = ColumnRecord.organisationFriendlyNameReversal;
            }
            else if (cmbTable.Text == "Asset")
            {
                tableColDefs = ColumnRecord.asset;
                nameReversals = ColumnRecord.assetFriendlyNameReversal;
            }
            else // if == "Contact"
            {
                tableColDefs = ColumnRecord.contact;
                nameReversals = ColumnRecord.contactFriendlyNameReversal;
            }

            List<string?> columnNames;
            List<List<object?>> rows;
            if (!nameReversals.ContainsKey(cmbColumn.Text)) // Should only trigger on no selection.
            {
                MessageBox.Show("Please select a column to search.");
                return;
            }
            if (App.Select(cmbTable.Text,
                           new List<string> { "*" },
                           new List<string> { nameReversals[cmbColumn.Text] },
                           new List<string> { txtSearch.Text },
                           out columnNames, out rows))
                dtgResults.Update(tableColDefs, columnNames, rows);
        }

        // Bring up selected organisation on double-click.
        private void dtg_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (cmbTable.Text == "Organisation")
            {
                App.EditOrganisation(dtgResults.GetCurrentlySelectedID());
                string id = dtgResults.GetCurrentlySelectedID();
            }
            if (cmbTable.Text == "Asset")
            {
                App.EditAsset(dtgResults.GetCurrentlySelectedID());
                string id = dtgResults.GetCurrentlySelectedID();
            }
            if (cmbTable.Text == "Contact")
            {
                App.EditContact(dtgResults.GetCurrentlySelectedID());
                string id = dtgResults.GetCurrentlySelectedID();
            }
        }
    }
}
