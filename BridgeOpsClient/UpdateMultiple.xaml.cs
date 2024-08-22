using BridgeOpsClient.DialogWindows;
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
    public partial class UpdateMultiple : Window
    {
        int identity;
        string table;
        Dictionary<string, ColumnRecord.Column> columns;
        List<string> ids;
        string idColumn;
        bool idsNeedQuotes;
        List<string> columnNames = new();

        DataTemplate btnRemoveTemplate;
        DataTemplate cmbSelectorTemplate;
        DataTemplate txtTemplate;
        DataTemplate cmbTemplate;
        DataTemplate numTemplate;
        DataTemplate dtmTemplate;
        DataTemplate datTemplate;
        DataTemplate timTemplate;
        DataTemplate chkTemplate;

        class FieldRow
        {
            public Button button;
            public ComboBox selector;
            public object? value;

            public FieldRow(Button button, ComboBox selector, object? value)
            {
                this.button = button;
                this.selector = selector;
                this.value = value;
            }
        }
        List<FieldRow> rows = new();

        public UpdateMultiple(int identity, string table, Dictionary<string, ColumnRecord.Column> columns,
                              string idColumn, List<string> ids, bool idsNeedQuotes)
        {
            InitializeComponent();

            this.identity = identity;
            this.table = table;
            this.columns = columns;
            this.ids = ids;
            this.idColumn = idColumn;
            this.idsNeedQuotes = idsNeedQuotes;

            UpdateColumnList();

            btnRemoveTemplate = (DataTemplate)FindResource("fieldRemoveButton");
            cmbSelectorTemplate = (DataTemplate)FindResource("fieldSelector");
            txtTemplate = (DataTemplate)FindResource("fieldTxt");
            cmbTemplate = (DataTemplate)FindResource("fieldCmb");
            numTemplate = (DataTemplate)FindResource("fieldNum");
            dtmTemplate = (DataTemplate)FindResource("fieldDtm");
            datTemplate = (DataTemplate)FindResource("fieldDat");
            timTemplate = (DataTemplate)FindResource("fieldTim");
            chkTemplate = (DataTemplate)FindResource("fieldChk");
        }

        private void UpdateColumnList()
        {
            columnNames = new();
            foreach (var kvp in columns)
                if (kvp.Key != idColumn && !(table == "Organisation" && kvp.Key == Glo.Tab.ORGANISATION_REF) &&
                                           !(table == "Asset" && kvp.Key == Glo.Tab.ASSET_REF))
                    columnNames.Add(ColumnRecord.GetPrintName(kvp));
        }

        private void UpdateGridRows()
        {
            for (int i = 0; i < rows.Count; ++i)
            {
                Grid.SetRow(rows[i].button, i);
                Grid.SetRow(rows[i].selector, i);
                if (rows[i].value != null)
                    Grid.SetRow((UIElement)rows[i].value!, i);
            }
        }


        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            int row = grdFields.RowDefinitions.Count;

            grdFields.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            Button removeButton = (btnRemoveTemplate.LoadContent() as Button)!;
            ComboBox selector = (cmbSelectorTemplate.LoadContent() as ComboBox)!;
            Grid.SetRow(removeButton, row);
            Grid.SetRow(selector, row);
            selector.ItemsSource = columnNames;

            rows.Add(new FieldRow(removeButton, selector, null));

            grdFields.Children.Add(removeButton);
            grdFields.Children.Add(selector);

            btnUpdate.IsEnabled = true;
        }

        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            grdFields.RowDefinitions.RemoveAt(0);
            int index = Grid.GetRow((Button)sender);
            FieldRow row = rows[index];
            grdFields.Children.Remove(row.button);
            grdFields.Children.Remove(row.selector);
            if (row.value != null)
                grdFields.Children.Remove((UIElement)row.value);
            rows.RemoveAt(index);
            UpdateGridRows();

            btnUpdate.IsEnabled = rows.Count > 0;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cmb = (ComboBox)sender;
            if (cmb.SelectedIndex < 0)
                return;

            string key = ColumnRecord.ReversePrintName((string)cmb.Items[cmb.SelectedIndex], columns);
            ColumnRecord.Column column = columns[key];

            int index = Grid.GetRow(cmb);

            object value;

            List<string> allowed = column.allowed.ToList<string>();
            if ((table == "Asset" && key == Glo.Tab.ORGANISATION_REF) ||
                (table == "Organisation" && key == Glo.Tab.PARENT_REF))
            {
                // Allowed will always be empty if we get here as the user cannot add an allowed list to core columns.
                allowed = App.GetOrganisationList().ToList();
            }
            if (allowed.Count > 0)
            {
                value = (cmbTemplate.LoadContent() as ComboBox)!;
                allowed.Insert(0, "");
                ((ComboBox)value).ItemsSource = allowed;
            }
            else if (ColumnRecord.IsTypeString(column))
            {
                value = (txtTemplate.LoadContent() as TextBox)!;
                ((TextBox)value).MaxLength = (int)column.restriction;
            }
            else if (ColumnRecord.IsTypeInt(column))
                value = (numTemplate.LoadContent() as CustomControls.NumberEntry)!;
            else if (column.type == "DATETIME")
                value = (dtmTemplate.LoadContent() as CustomControls.DateTimePicker)!;
            else if (column.type == "DATE")
                value = (datTemplate.LoadContent() as DatePicker)!;
            else if (column.type == "TIME")
                value = (timTemplate.LoadContent() as CustomControls.TimePicker)!;
            else // if bool
                value = (chkTemplate.LoadContent() as CheckBox)!;

            rows[index].value = value;
            Grid.SetRow((UIElement)value, index);
            grdFields.Children.Add((UIElement)value);
        }

        SendReceiveClasses.UpdateRequest request;

        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (AssembleUpdate() && App.SendUpdate(request))
            {
                if (MainWindow.pageDatabase != null)
                    MainWindow.pageDatabase.RepeatSearches(identity);
                Close();
            }
        }

        private bool AssembleUpdate()
        {
            request = new(App.sd.sessionID, ColumnRecord.columnRecordID, App.sd.loginID,
                          table, new(), new(), new(), idColumn, ids, idsNeedQuotes);

            HashSet<string> nameDuplicateCheck = new();

            bool Abort(string message)
            {
                MessageBox.Show(message);
                return false;
            }

            foreach (FieldRow row in rows)
            {
                string column;
                string value;
                bool needsQuotes;

                if (row.selector.SelectedIndex == -1)
                    return Abort("You must select a column for all added fields.");
                if (row.value == null)
                    return Abort($"You must select a value for {row.selector.Text}.");
                if (nameDuplicateCheck.Contains(row.selector.Text))
                    return Abort($"You have tried to select {row.selector.Text} twice.");

                nameDuplicateCheck.Add(row.selector.Text);

                column = ColumnRecord.ReversePrintName(row.selector.Text, columns);

                if (row.value is ComboBox cmb)
                {
                    if (cmb.SelectedIndex < 0)
                        return Abort($"You must select a value for {row.selector.Text}.");
                    value = cmb.Text;
                    needsQuotes = true;
                }
                else if (row.value is TextBox txt)
                {
                    value = txt.Text;
                    needsQuotes = true;
                }
                else if (row.value is CustomControls.NumberEntry num)
                {
                    if (num.Text == "")
                        return Abort($"You must select a value for {row.selector.Text}.");
                    value = num.Text;
                    needsQuotes = false;
                }
                else if (row.value is CustomControls.DateTimePicker dtm)
                {
                    value = SendReceiveClasses.SqlAssist.DateTimeToSQL(dtm.GetDateTime(), false);
                    needsQuotes = true;
                }
                else if (row.value is DatePicker dat)
                {
                    if (dat.SelectedDate == null)
                        return Abort($"You must select a value for {row.selector.Text}.");
                    value = SendReceiveClasses.SqlAssist.DateTimeToSQL((DateTime)dat.SelectedDate, true);
                    needsQuotes = true;
                }
                else if (row.value is CustomControls.TimePicker tim)
                {
                    value = SendReceiveClasses.SqlAssist.TimeSpanToSQL(tim.GetTime());
                    needsQuotes = true;
                }
                else if (row.value is CheckBox chk)
                {
                    if (chk.IsChecked == null)
                        return Abort($"You must select a value for {row.selector.Text}.");
                    value = chk.IsChecked == true ? "1" : "0";
                    needsQuotes = true;
                }
                else
                    return Abort($"You must select a value for {row.selector.Text}.");

                request.columns.Add(column);
                request.values.Add(value);
                request.columnsNeedQuotes.Add(needsQuotes);
            }

            // Record the change reason if needed.
            if (table == "Organisation" || table == "Asset")
            {
                DialogChangeReason dialogChangeReason = new(table);
                bool? result = dialogChangeReason.ShowDialog();
                if (result != null && result == true)
                    request.changeReason = dialogChangeReason.txtReason.Text;
                else
                    return false;
            }


            return true;
        }
    }
}
