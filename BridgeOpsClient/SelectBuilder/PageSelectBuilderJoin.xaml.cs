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

namespace BridgeOpsClient
{
    public partial class PageSelectBuilderJoin : Page
    {
        public PageSelectBuilder selectBuilder;
        public Frame frame;

        public PageSelectBuilderJoin(PageSelectBuilder selectBuilder, Frame frame)
        {
            InitializeComponent();

            if (selectBuilder.joins.Count == 0)
            {
                btnUp.IsEnabled = false;
                btnDown.IsEnabled = false;
            }

            this.selectBuilder = selectBuilder;
            this.frame = frame;
        }

        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            selectBuilder.RemoveRow(this);
        }

        private void btnUp_Click(object sender, RoutedEventArgs e)
        {
            selectBuilder.MoveRow(this, frame, true);
        }

        private void btnDown_Click(object sender, RoutedEventArgs e)
        {
            selectBuilder.MoveRow(this, frame, false);
        }

        public void ToggleUpDownButtons()
        {
            btnUp.IsEnabled = Grid.GetRow(frame) > 0;
            btnDown.IsEnabled = Grid.GetRow(frame) < selectBuilder.joins.Count - 1;
        }

        private void cmbTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            List<string> columns = new();
            string? selectedTable = ((ComboBoxItem)cmbTable.Items[cmbTable.SelectedIndex]).Content.ToString();
            if (selectedTable != null)
            {
                var dictionary = ColumnRecord.GetDictionary(selectedTable, true);
                if (dictionary != null)
                    foreach (var kvp in dictionary)
                        columns.Add(ColumnRecord.GetPrintName(kvp));
            }
            cmbColumn1.ItemsSource = columns;

            selectBuilder.UpdateColumns();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            selectBuilder.UpdateColumns();
        }
    }
}
