using System;
using System.CodeDom;
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

namespace BridgeOpsClient
{
    public partial class PageDatabaseView : Page
    {
        // This list holds the original column names of respective ComboBoxItems for easier lookup.
        List<string> friendlyNameReversal = new();

        public PageDatabaseView()
        {
            InitializeComponent();

            cmbTable.SelectedIndex = 0;

            PopulateColumnComboBox();
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
            friendlyNameReversal.Clear();
            foreach (KeyValuePair<string, ColumnRecord.Column> col in table)
            {
                ComboBoxItem cbi = new ComboBoxItem();
                cbi.Content = ColumnRecord.GetPrintName(col);
                friendlyNameReversal.Add(col.Key);
                cmbColumn.Items.Add(cbi);
                if (col.Value.type != "TEXT")
                {
                    DataGridTextColumn header = new();
                    header.Header = cbi.Content;
                    dtgResults.Columns.Add(header);
                }
            }

            if (cmbTable.SelectedIndex == 2)
                cmbColumn.Items.RemoveAt(0); // Removed Contact_ID as it's not required.

            cmbColumn.SelectedIndex = 0;
        }

        private void cmbTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PopulateColumnComboBox();
        }

        private void btnListAll_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
