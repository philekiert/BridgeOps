using SendReceiveClasses;
using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Security.Principal;
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
        PageDatabase containingPage;
        Frame containingFrame;

        // Values need to be stored for multi-field searches.
        List<string> fieldValues = new();

        public PageDatabaseView(PageDatabase containingPage, Frame containingFrame)
        {
            InitializeComponent();

            this.containingPage = containingPage;
            this.containingFrame = containingFrame;

            // This will trigger cmbTableSelectionchanged(), which will PopulateColumnComboBox().
            cmbTable.SelectedIndex = 0;

            dtgResults.canHideColumns = true;
            dtgResults.CustomDoubleClick += dtg_DoubleClick;
            dtgResults.EnableMultiSelect();

            txtSearch.Focus();

            dtgResults.AddWipeButton();
            dtgResults.WipeCallback = WipeCallback;

            dtgResults.AddContextMenuItem("Update Selected", false, btnUpdate_Click).IsEnabled =
                App.sd.editPermissions[Glo.PERMISSION_RECORDS];
            dtgResults.AddContextMenuItem("Delete Selected", false, btnDelete_Click).IsEnabled =
                App.sd.deletePermissions[Glo.PERMISSION_RECORDS];
        }

        public RowDefinition GetRowDefinitionForFrame()
        {
            return containingPage.grdPanes.RowDefinitions[Grid.GetRow(containingFrame)];
        }

        private void WipeCallback()
        {
            SetStatusBar();
        }

        public void PopulateColumnComboBox()
        {
            OrderedDictionary table;
            if (cmbTable.SelectedIndex == 0)
                table = ColumnRecord.orderedOrganisation;
            else if (cmbTable.SelectedIndex == 1)
                table = ColumnRecord.orderedAsset;
            else
                table = ColumnRecord.orderedContact;

            cmbColumn.Items.Clear();
            fieldValues.Clear();

            btnClear.IsEnabled = false;

            foreach (DictionaryEntry de in table)
            {
                // Anything that's TEXT type. Could add date and int search in the future, but it's just not urgent
                // as you can just sort by date or int in the results.
                if (ColumnRecord.IsTypeString((ColumnRecord.Column)de.Value!))
                {
                    ComboBoxItem item = new ComboBoxItem();
                    item.Content = ColumnRecord.GetPrintName(de);
                    cmbColumn.Items.Add(item);
                    fieldValues.Add("");
                }
            }

            cmbColumn.SelectedIndex = 0;

            txtSearch.IsEnabled = cmbColumn.Items.Count > 0;
            btnSearch.IsEnabled = cmbColumn.Items.Count > 0;
            btnWideSearch.IsEnabled = cmbColumn.Items.Count > 0;
            txtSearch.Text = "";
        }

        struct Row
        {
            public List<object?> items { get; set; }
            public Row(List<object?> items)
            {
                this.items = items;
            }
        }

        // Last search variables for updating the SqlDataGrid when a change is made by the user.
        bool lastSearchWide = false;
        bool lastSearchHistorical = false;
        List<string> lastSearchColumns = new();
        List<string> lastSearchValues = new();
        List<Conditional> lastSearchConditionals = new();
        string lastWideValue = "";
        OrderedDictionary lastColumnDefinitions = new();
        public void RepeatSearch(int identity)
        {
            if (identity != dtgResults.identity)
                return;

            string table = "Organisation";
            if (dtgResults.identity == 1)
                table = "Asset";
            else if (dtgResults.identity == 2)
                table = "Contact";

            if (dtgResults.identity != -1)
            {
                List<string?> columnNames;
                List<List<object?>> rows;
                if (lastSearchWide && App.SelectWide(table, txtSearch.Text,
                                                     out columnNames, out rows, lastSearchHistorical))
                {
                    dtgResults.Update(lastColumnDefinitions, columnNames, rows);
                    SetStatusBar(rows.Count, columnNames.Count, -1);
                }
                else if (App.Select(cmbTable.Text,
                                    new List<string> { "*" },
                                    lastSearchColumns, lastSearchValues, lastSearchConditionals,
                                    out columnNames, out rows, true, lastSearchHistorical))
                {
                    dtgResults.Update(lastColumnDefinitions, columnNames, rows);
                    SetStatusBar(rows.Count, columnNames.Count, lastSearchColumns.Count);
                }
                else
                    SetStatusBar();

            }
        }

        private void cmbTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PopulateColumnComboBox();
        }

        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            btnSearch_Click(sender, e, cmbTable.SelectedIndex);
        }
        private void btnSearch_Click(object sender, RoutedEventArgs e, int identity)
        {
            bool historical = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            OrderedDictionary tableColDefs;
            Dictionary<string, string> nameReversals;
            if (cmbTable.Text == "Organisation")
            {
                tableColDefs = ColumnRecord.organisation;
                nameReversals = ColumnRecord.organisationFriendlyNameReversal;
            }
            else if (cmbTable.Text == "Asset")
            {
                identity = 1;
                tableColDefs = ColumnRecord.asset;
                nameReversals = ColumnRecord.assetFriendlyNameReversal;
            }
            else // if == "Contact"
            {
                identity = 2;
                tableColDefs = ColumnRecord.contact;
                nameReversals = ColumnRecord.contactFriendlyNameReversal;
            }

            if (!nameReversals.ContainsKey(cmbColumn.Text)) // Should only trigger on no selection.
            {
                App.DisplayError("Please select a column to search.");
                return;
            }

            List<string> selectColumns = new();
            List<string> selectValues = new();
            for (int n = 0; n < fieldValues.Count; ++n)
            {
                if (fieldValues[n] != "")
                {
                    selectColumns.Add(nameReversals[(string)((ComboBoxItem)cmbColumn.Items[n]).Content]);
                    selectValues.Add(fieldValues[n]);
                }
            }

            List<Conditional> conditionals = new();
            for (int i = 0; i < selectColumns.Count; ++i)
                conditionals.Add(Conditional.Like);

            // Error message is displayed by App.SelectAll() if something goes wrong.
            List<string?> columnNames;
            List<List<object?>> rows;
            if (App.Select(cmbTable.Text, // Needs changing in RepeatSearch() as well if adjusted.
                           new List<string> { "*" },
                           selectColumns, selectValues, conditionals,
                           out columnNames, out rows, true, historical))
            {
                lastSearchWide = false;
                lastSearchColumns = selectColumns;
                lastSearchValues = selectValues;
                lastSearchConditionals = conditionals;
                lastColumnDefinitions = tableColDefs;

                dtgResults.identity = identity;
                dtgResults.Update(tableColDefs, columnNames, rows);

                SetStatusBar(rows.Count, columnNames.Count, selectColumns.Count);
            }
            else
                SetStatusBar();
        }

        // Wide search on either enter or click.
        private void WideSearch(int identity)
        {
            bool historical = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            OrderedDictionary tableColDefs;
            Dictionary<string, string> nameReversals;
            if (identity == 0) // Organisation
            {
                tableColDefs = ColumnRecord.organisation;
                nameReversals = ColumnRecord.organisationFriendlyNameReversal;
            }
            else if (identity == 1) // Asset
            {
                tableColDefs = ColumnRecord.asset;
                nameReversals = ColumnRecord.assetFriendlyNameReversal;
            }
            else // if == 2 / Contact
            {
                tableColDefs = ColumnRecord.contact;
                nameReversals = ColumnRecord.contactFriendlyNameReversal;
            }

            // Error message is displayed by App.SelectAll() if something goes wrong.
            List<string?> columnNames;
            List<List<object?>> rows;
            if (App.SelectWide(cmbTable.Text, txtSearch.Text, // Needs changing in RepeatSearch() as well if adjusted.
                               out columnNames, out rows, historical))
            {
                lastSearchWide = true;
                lastWideValue = txtSearch.Text;
                lastColumnDefinitions = tableColDefs;

                dtgResults.identity = identity;
                dtgResults.Update(tableColDefs, columnNames, rows);

                SetStatusBar(rows.Count, columnNames.Count, -1);
            }
            else
                SetStatusBar();
        }
        private void btnWideSearch_Click(object sender, RoutedEventArgs e)
        {
            WideSearch(cmbTable.SelectedIndex);
        }
        private void txtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                WideSearch(cmbTable.SelectedIndex);
        }

        private void SetStatusBar(params int[] vals)
        {
            if (vals.Length != 3)
            {
                lblRows.Content = "";
                lblColumns.Content = "";
                lblColumnsSearched.Content = "";
                lblSelected.Content = "";
                lblTable.Content = "";
                return;
            }

            // Set all labels to updated values.
            lblRows.Content = "Rows: " + vals[0].ToString();
            lblColumns.Content = "Columns: " + vals[1].ToString();
            lblColumnsSearched.Content = vals[2] == -1 ? "Wide search" : ("Fields searched: " + vals[2].ToString());
            dtgResults_SelectionChanged(dtgResults, null);

            string tableSearched = "Organisations";
            if (dtgResults.identity == 1)
                tableSearched = "Assets";
            else if (dtgResults.identity == 2)
                tableSearched = "Contacts";
            lblTable.Content = tableSearched;

            // Highlight searches where more than one field was searched in case the user was not aware.
            lblColumnsSearched.FontWeight = vals[2] > 1 ? FontWeights.SemiBold : FontWeights.Normal;
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

        // Highlight fields with values, and reload those values when selecting fields.
        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            fieldValues[cmbColumn.SelectedIndex] = txtSearch.Text;
            ((ComboBoxItem)cmbColumn.Items[cmbColumn.SelectedIndex]).FontWeight = txtSearch.Text == "" ?
                                                                                  FontWeights.Normal :
                                                                                  FontWeights.Bold;

            // Only enabled btnClear when there's actually text to clear.
            for (int n = 0; n < fieldValues.Count; ++n)
                if (fieldValues[n] != "")
                {
                    btnClear.IsEnabled = true;
                    return;
                }
            btnClear.IsEnabled = false;
        }
        private void cmbColumn_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbColumn.SelectedIndex != -1 && fieldValues.Count > 0)
                txtSearch.Text = fieldValues[cmbColumn.SelectedIndex];
        }

        // Wipe all stored field search strings.
        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            for (int n = 0; n < fieldValues.Count; ++n)
            {
                if (fieldValues[n] != "")
                {
                    fieldValues[n] = "";
                    ((ComboBoxItem)cmbColumn.Items[n]).FontWeight = FontWeights.Normal;
                }
            }

            txtSearch.Text = "";

            btnClear.IsEnabled = false;
        }

        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (dtgResults.dtg.SelectedItems.Count < 1)
            {
                App.DisplayError("You must select at least one item to update.");
                return;
            }

            string table = "Organisation";
            string idColumn = Glo.Tab.ORGANISATION_ID;
            bool needsQuotes = true; ;
            if (dtgResults.identity == 1)
            {
                table = "Asset";
                idColumn = Glo.Tab.ASSET_ID;
            }
            else if (dtgResults.identity == 2)
            {
                needsQuotes = true;
                table = "Contact";
                idColumn = Glo.Tab.CONTACT_ID;
            }

            var columns = ColumnRecord.GetDictionary(table, true);
            if (columns == null)
                return;

            UpdateMultiple updateMultiple = new(dtgResults.identity, table, columns,
                                                idColumn, dtgResults.GetCurrentlySelectedIDs(), needsQuotes);
            updateMultiple.ShowDialog();
        }
        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!App.DeleteConfirm(dtgResults.dtg.SelectedItems.Count > 1))
                return;

            string table = "Organisation";
            string column = Glo.Tab.ORGANISATION_ID;
            bool needsQuotes = true; ;
            if (dtgResults.identity == 1)
            {
                table = "Asset";
                column = Glo.Tab.ASSET_ID;
            }
            else if (dtgResults.identity == 2)
            {
                needsQuotes = true;
                table = "Contact";
                column = Glo.Tab.CONTACT_ID;
            }

            if (App.SendDelete(table, column, dtgResults.GetCurrentlySelectedIDs(), needsQuotes) &&
                MainWindow.pageDatabase != null)
                MainWindow.pageDatabase.RepeatSearches(dtgResults.identity);
        }

        // Bring up selected organisation on double-click.
        private void dtg_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            string currentID = dtgResults.GetCurrentlySelectedID();
            if (currentID != "")
            {
                if (dtgResults.identity == 0)
                    App.EditOrganisation(currentID);
                if (dtgResults.identity == 1)
                    App.EditAsset(currentID);
                if (dtgResults.identity == 2)
                    App.EditContact(currentID);
            }
        }

        private void btnAddPane_Click(object sender, RoutedEventArgs e)
        {
            containingPage.AddPane(containingFrame);
        }

        private void btnRemovePane_Click(object sender, RoutedEventArgs e)
        {
            containingPage.RemovePane(containingFrame);
        }
    }
}
