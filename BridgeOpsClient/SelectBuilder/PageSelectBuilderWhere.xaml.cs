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
    public partial class PageSelectBuilderWhere : Page
    {
        public SelectBuilder selectBuilder;
        public Frame frame;

        public PageSelectBuilderWhere(SelectBuilder selectBuilder, Frame frame)
        {
            InitializeComponent();

            if (selectBuilder.wheres.Count == 0)
            {
                btnUp.IsEnabled = false;
                btnDown.IsEnabled = false;

                cmbAndOr.Visibility = Visibility.Hidden;
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
            btnDown.IsEnabled = Grid.GetRow(frame) < selectBuilder.wheres.Count - 1;

            // Makes sense to update the AND/OR selector here.
            cmbAndOr.Visibility = btnUp.IsEnabled ? Visibility.Visible : Visibility.Hidden;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            selectBuilder.UpdateColumns();
        }

        private void cmbColumn_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            void SwitchValuesOff()
            {
                txtValue.Visibility = Visibility.Collapsed;
                dtmValue.Visibility = Visibility.Collapsed;
                datValue.Visibility = Visibility.Collapsed;
                timValue.Visibility = Visibility.Collapsed;
                chkValue.Visibility = Visibility.Collapsed;
                cmbOperator.Items.Clear();
            }

            string type = "";

            string newColumn = "";
            if (cmbColumn.SelectedIndex >= 0)
                newColumn = (string)cmbColumn.Items[cmbColumn.SelectedIndex];
            else
            {
                SwitchValuesOff();
                return;
            }

            var dictionary = ColumnRecord.GetDictionary(newColumn.Remove(newColumn.IndexOf('.')), false);
            if (dictionary == null)
            {
                SwitchValuesOff();
                return;
            }

            string column = ColumnRecord.ReversePrintName(newColumn.Substring(newColumn.IndexOf('.') + 1), dictionary);

            if (column == "")
            {
                SwitchValuesOff();
                return;
            }

            type = dictionary[column].type;
            txtValue.Visibility = ColumnRecord.IsTypeString(type) || ColumnRecord.IsTypeInt(type) ?
                                                                     Visibility.Visible :
                                                                     Visibility.Collapsed;
            dtmValue.Visibility = type == "DATETIME" ? Visibility.Visible : Visibility.Collapsed;
            datValue.Visibility = type == "DATE" ? Visibility.Visible : Visibility.Collapsed;
            timValue.Visibility = type == "TIME" ? Visibility.Visible : Visibility.Collapsed;
            chkValue.Visibility = type.Contains("BOOL") || type == "BIT" ? Visibility.Visible : Visibility.Collapsed;

            if (txtValue.Visibility == Visibility.Visible)
            {
                if (ColumnRecord.IsTypeString(type))
                    cmbOperator.ItemsSource = new List<string>() { "=", "LIKE" };
                else // if int
                    cmbOperator.ItemsSource = new List<string>() { "=", "<", ">", "<=, >=" };
            }
            else if (dtmValue.Visibility == Visibility.Visible)
                cmbOperator.ItemsSource = new List<string>() { "=", "<", ">", "<=, >=" };
            else if (datValue.Visibility == Visibility.Visible)
                cmbOperator.ItemsSource = new List<string>() { "=", "<", ">", "<=, >=" };
            else if (timValue.Visibility == Visibility.Visible)
                cmbOperator.ItemsSource = new List<string>() { "=", "<", ">", "<=, >=" };
            else if (chkValue.Visibility == Visibility.Visible)
            {
                cmbOperator.ItemsSource = new List<string>() { "=" };
                cmbOperator.IsEnabled = false;
            }
            else
                return;
            cmbOperator.SelectedIndex = 0;
        }
    }
}
