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
    public partial class ReorderColumns : Window
    {
        public ReorderColumns()
        {
            GetColumnOrders();
            InitializeComponent();

            ChangeTable();
        }

        // Use our own order lists here to allow for recall when switching between tables.
        List<int> organisationOrder = new();
        List<int> assetOrder = new();
        List<int> contactOrder = new();
        List<int> conferenceOrder = new();

        int upperLimit = 0;
        int lowerLimit = 0;

        private void GetColumnOrders()
        {
            organisationOrder.Clear();
            assetOrder.Clear();
            contactOrder.Clear();
            conferenceOrder.Clear();
            organisationOrder.AddRange(ColumnRecord.organisationOrder);
            assetOrder.AddRange(ColumnRecord.assetOrder);
            contactOrder.AddRange(ColumnRecord.contactOrder);
            conferenceOrder.AddRange(ColumnRecord.conferenceOrder);
        }

        private void ChangeTable()
        {
            if (lstColumns == null)
                return;

            lstColumns.Items.Clear();

            string table;
            List<int> order;
            Dictionary<string, ColumnRecord.Column> dictionary;

            if (cmbTable.SelectedIndex == 0)
            {
                table = "Organisation";
                dictionary = ColumnRecord.organisation;
                lowerLimit = 4;
                order = organisationOrder;
            }
            else if (cmbTable.SelectedIndex == 1)
            {
                table = "Asset";
                dictionary = ColumnRecord.asset;
                lowerLimit = 3;
                order = assetOrder;
            }
            else if (cmbTable.SelectedIndex == 2)
            {
                table = "Contact";
                dictionary = ColumnRecord.contact;
                lowerLimit = 2;
                order = contactOrder;
            }
            else
            {
                table = "Conference";
                dictionary = ColumnRecord.conference;
                lowerLimit = 11;
                order = conferenceOrder;
            }

            List<object> columnNames = new();
            foreach (KeyValuePair<string, ColumnRecord.Column> kvp in dictionary)
            {
                ListViewItem item = new();
                item.Content = ColumnRecord.GetPrintName(kvp);
                if (!Glo.Fun.ColumnRemovalAllowed(table, kvp.Key))
                    item.Foreground = Brushes.Gray;
                columnNames.Add(item);
            }

            if (order.Count != columnNames.Count)
            {
                MessageBox.Show("The column record appears to be corrupted, as the column and order lists are of" +
                                " different lengths. Logging out, please log in again.");
                App.SessionInvalidated();
                Close();
                return;
            }

            // Add the column names in the correct order.
            foreach (int i in order)
                lstColumns.Items.Add(columnNames[i]);

            upperLimit = lstColumns.Items.Count - 1;
        }

        private void btnUp_Click(object sender, RoutedEventArgs e) { MoveItem(false); }
        private void btnDown_Click(object sender, RoutedEventArgs e) { MoveItem(true); }

        private void MoveItem(bool down)
        {
            updatingSelection = true;

            List<int> indices = OrderListIndices();

            if (indices.Count == 0)
                return;

            int selectedIndexStart = indices[0];
            int selectedLength = indices.Count;

            if ((!down && selectedIndexStart <= lowerLimit) ||
                (down && selectedIndexStart + (selectedLength - 1) >= upperLimit))
                return;

            List<object> items = new();
            foreach (object s in lstColumns.SelectedItems)
                items.Add(s);

            lstColumns.SelectedItems.Clear();

            int selectionLength = items.Count;

            List<int> orderRange = new();

            for (int i = 0; i < selectionLength; ++i)
            {
                lstColumns.Items.RemoveAt(selectedIndexStart);
                if (cmbTable.Text == "Organisation")
                {
                    orderRange.Add(organisationOrder[selectedIndexStart]);
                    organisationOrder.RemoveAt(selectedIndexStart);
                }
                else if (cmbTable.Text == "Asset")
                {
                    orderRange.Add(assetOrder[selectedIndexStart]);
                    assetOrder.RemoveAt(selectedIndexStart);
                }
                else if (cmbTable.Text == "Contact")
                {
                    orderRange.Add(contactOrder[selectedIndexStart]);
                    contactOrder.RemoveAt(selectedIndexStart);
                }
                else if (cmbTable.Text == "Conference")
                {
                    orderRange.Add(conferenceOrder[selectedIndexStart]);
                    conferenceOrder.RemoveAt(selectedIndexStart);
                }
            }
            selectedIndexStart += down ? 1 : -1;
            for (int i = 0; i < selectedLength; ++i)
            {
                lstColumns.Items.Insert(selectedIndexStart + i, items[i]);
                if (cmbTable.Text == "Organisation")
                    organisationOrder.Insert(selectedIndexStart + i, orderRange[i]);
                else if (cmbTable.Text == "Asset")
                    assetOrder.Insert(selectedIndexStart + i, orderRange[i]);
                else if (cmbTable.Text == "Contact")
                    contactOrder.Insert(selectedIndexStart + i, orderRange[i]);
                else if (cmbTable.Text == "Conference")
                    conferenceOrder.Insert(selectedIndexStart + i, orderRange[i]);
            }

            updatingSelection = false;

            foreach (object s in items)
                lstColumns.SelectedItems.Add(s);

            List<int> order, orderComparison;

            // Mark the table in bold if changes have been made.

            if (cmbTable.Text == "Organisation")
            {
                order = organisationOrder;
                orderComparison = ColumnRecord.organisationOrder;
            }
            else if (cmbTable.Text == "Asset")
            {
                order = assetOrder;
                orderComparison = ColumnRecord.assetOrder;
            }
            else if (cmbTable.Text == "Contact")
            {
                order = contactOrder;
                orderComparison = ColumnRecord.contactOrder;
            }
            else // if Conference
            {
                order = conferenceOrder;
                orderComparison = ColumnRecord.conferenceOrder;
            }

            bool changed = false;
            for (int i = 0; i < order.Count; ++i)
                if (order[i] != orderComparison[i])
                {
                    ((ComboBoxItem)cmbTable.Items[cmbTable.SelectedIndex]).FontWeight = FontWeights.Bold;
                    cmbTable.FontWeight = FontWeights.Bold;
                    changed = true;
                    break;
                }

            if (!changed)
            {
                ((ComboBoxItem)cmbTable.Items[cmbTable.SelectedIndex]).FontWeight = FontWeights.Normal;
                cmbTable.FontWeight = FontWeights.Normal;
            }
        }

        bool updatingSelection = false;
        List<object> lastSelection = new();
        private void lstColumns_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!updatingSelection)
            {
                List<int> indices = OrderListIndices();

                if (lstColumns.SelectedItems.Count > 1)
                {
                    // Cancel the selection if the user tries to select something that isn't adjacent.
                    for (int i = 0; i < indices.Count - 1; ++i)
                    {
                        if (indices[i] != indices[i + 1] - 1)
                        {
                            updatingSelection = true;
                            lstColumns.SelectedItems.Clear();
                            try
                            {
                                foreach (object o in lastSelection)
                                    lstColumns.SelectedItems.Add(o);
                            }
                            catch { } // No need for a catch, just clear the selected items.
                            updatingSelection = false;
                            return;
                        }
                    }
                }

                btnUp.IsEnabled = indices.Count > 0 && indices[0] > lowerLimit;
                btnDown.IsEnabled = indices.Count > 0 && indices[0] > lowerLimit - 1 &&
                                    indices[indices.Count - 1] < upperLimit;

                lastSelection.Clear();
                foreach (object o in lstColumns.SelectedItems)
                    lastSelection.Add(o);
            }
        }

        private List<int> OrderListIndices()
        {
            List<int> indices = new();
            foreach (object o in lstColumns.SelectedItems)
                indices.Add(lstColumns.Items.IndexOf(o));
            indices.Sort();
            return indices;
        }

        private void cmbTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ChangeTable();

            cmbTable.FontWeight = ((ComboBoxItem)cmbTable.Items[cmbTable.SelectedIndex]).FontWeight;
        }
    }
}