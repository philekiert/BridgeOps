using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ClosedXML.Excel;
using static BridgeOpsClient.CustomControls.SqlDataGrid;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Collections.Specialized;
using System.Globalization;
using static System.Resources.ResXFileRef;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using DocumentFormat.OpenXml.Spreadsheet;
using System.ComponentModel;


namespace BridgeOpsClient.CustomControls
{
    public partial class SqlDataGrid : UserControl
    {
        ContextMenu menuShowHideColumns;
        ScrollViewer? scrollViewer;

        public bool canHideColumns = false;
        
        DataTemplate tickIcon;

        // Set by the caller when calling Update(). This is used to make sure the correct column settings are updated
        // if the user reorders, resizes or hides columns.
        public int identity = -1;

        public SqlDataGrid()
        {
            InitializeComponent();
            menuShowHideColumns = (ContextMenu)FindResource("contextColumns");
            tickIcon = (DataTemplate)FindResource("tickIcon");

            // ContextMenu is set on Update().
            dtg.ContextMenu = null;
        }

        private void dtg_Loaded(object sender, RoutedEventArgs e)
        {
            // Get the scroll viewer. We need this to prevent horizontal auto-scrolling when the selection changes.

            void FindScrollViewer(DependencyObject obj)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
                {
                    if (VisualTreeHelper.GetChild(obj, i) is ScrollViewer sv)
                    {
                        scrollViewer = sv;
                        scrollViewer.ScrollChanged += scrollViewer_ScrollChanged;
                        break;
                    }
                    else if (scrollViewer == null)
                        FindScrollViewer(VisualTreeHelper.GetChild(obj, i));

                }
            }

            FindScrollViewer(dtg);
        }

        // INotifyPropertyChanged is necessary in case of checkboxes.
        public class Row : INotifyPropertyChanged
        {
            private bool isChecked;
            public bool IsChecked
            {
                get => isChecked;
                set
                {
                    if (isChecked != value)
                    {
                        isChecked = value;
                        OnPropertyChanged(nameof(IsChecked));
                    }
                }
            }
            public List<object?> items { get; set; }

            public Row(List<object?> items)
            {
                this.items = items;
                isChecked = false;
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public Action? WipeCallback = null;
        public void Wipe()
        {
            identity = -1;
            dtg.ItemsSource = null;
            dtg.Columns.Clear();

            if (WipeCallback != null)
                WipeCallback();

            dtg.ContextMenu = null;
        }
        public void btnWipe_Click(object sender, EventArgs e) { Wipe(); }

        List<Row> rowsBinder = new();

        public bool checkBox = false;
        public bool AddCheckBoxes
        {
            get { return checkBox; }
            set { checkBox = value; }
        }

        // I have allowed more than one dictionary to be fed to this array, because sometimes you want a select result
        // that joins two tables. The downside is that tables sharing column names need to use the same type and
        // friendly name. I can't see this being an issue, but note it for future reference.
        public void Update(List<string?> columnNames, List<List<object?>> rows, params string[] omitColumns)
        {
            Update(new List<OrderedDictionary>(), columnNames, rows, omitColumns);
        }
        public void Update(OrderedDictionary tableColDefs,
                           List<string?> columnNames, List<List<object?>> rows, params string[] omitColumns)
        {
            Update(new List<OrderedDictionary>() { tableColDefs },
                   columnNames, rows, omitColumns);
        }
        public void Update(List<OrderedDictionary> tableColDefs,
                           List<string?> columnNames, List<List<object?>> rows, params string[] omitColumns)
        {
            // Add any columns to omit to a dictionary for fast lookup.
            Dictionary<string, bool> columnstoOmit = new();
            if (omitColumns.Length > 0)
                foreach (string s in omitColumns)
                    columnstoOmit.Add(s, true);

            dtg.Columns.Clear();
            dtg.ItemsSource = null;

            int count = 0;

            if (checkBox)
            {
                DataGridCheckBoxColumn col = new()
                {
                    Header = "Remove",
                    Binding = new Binding("IsChecked"),
                    IsReadOnly = false
                };
                dtg.Columns.Add(col);
            }

            foreach (string? s in columnNames)
            {
                DataGridTextColumn header = new();
                if (s != null && maxLengthOverrides.ContainsKey(s))
                {
                    if (maxLengthOverrides[s] == -1)
                        header.MaxWidth = float.PositiveInfinity;
                    else
                        header.MaxWidth = maxLengthOverrides[s];
                }
                else
                    // See dtg_LayoutUpdated() for explanation.
                    header.MaxWidth = 250;

                if (s == null)
                    header.Header = "";
                else
                {
                    ColumnRecord.Column col = new ColumnRecord.Column();
                    foreach (OrderedDictionary defs in tableColDefs)
                    {
                        if (defs.Contains(s))
                        {
                            col = (ColumnRecord.Column)defs[s]!;
                            break;
                        }
                    }

                    Type? dataType = null;
                    for (int i = 0; i < rows.Count; ++i)
                        if (rows[i][count] != null)
                        {
                            dataType = rows[i][count]!.GetType();
                            break;
                        }

                    header.Header = ColumnRecord.GetPrintName(s, col);
                    header.IsReadOnly = true;
                    if (col.type != null && col.type.ToUpper() == "DATE")
                    {
                        header.Binding = new Binding(string.Format("items[{0}]", count));
                        header.Binding.StringFormat = "{0:dd/MM/yyyy}";
                    }
                    else if ((col.type != null && col.type.ToUpper() == "DATETIME") ||
                             dataType == typeof(DateTime))
                    {
                        header.Binding = new Binding(string.Format("items[{0}]", count));
                        header.Binding.StringFormat = "{0:dd/MM/yyyy HH:mm}";
                    }
                    else if ((col.type != null &&
                              (col.type.ToUpper() == "TIMESPAN" || col.type.ToUpper() == "TIME")) ||
                             dataType == typeof(TimeSpan))
                    {
                        header.Binding = new Binding(string.Format("items[{0}]", count));
                        header.Binding.StringFormat = "{0:hh\\:mm}";
                    }
                    else if ((col.type != null &&
                              (col.type.ToUpper().StartsWith("BOOL") || col.type.ToUpper() == "BIT")) ||
                             dataType == typeof(bool))
                    {
                        header.Binding = new Binding(string.Format("items[{0}]", count))
                        { Converter = new BooleanToYesNoConverter() };
                    }
                    else
                        header.Binding = new Binding(string.Format("items[{0}]", count));
                }

                if (s != null && columnstoOmit.ContainsKey(s))
                    header.Visibility = Visibility.Hidden;
                else
                    dtg.Columns.Add(header);
                ++count;
            }

            // Data
            rowsBinder = new();
            foreach (List<object?> row in rows)
                rowsBinder.Add(new Row(row));

            if (!canHideColumns)
                menuShowHideColumns.Visibility = Visibility.Hidden;
            if (identity != -1)
            {
                // Apply user's view configuration.

                List<string> order = App.us.dataOrder[identity];
                List<bool> hidden = App.us.dataHidden[identity];
                List<double> widths = App.us.dataWidths[identity];

                if (order.Count != 0 && (order.Count == widths.Count && widths.Count == hidden.Count))
                {
                    int setIndex = 0;
                    for (int i = 0; i < order.Count; ++i)
                    {
                        foreach (DataGridColumn col in dtg.Columns)
                        {
                            if ((string)col.Header == order[i])
                            {
                                col.MaxWidth = double.PositiveInfinity;
                                col.Width = widths[i];
                                if (canHideColumns)
                                    if (hidden[i]) col.Visibility = Visibility.Hidden;
                                col.DisplayIndex = setIndex;
                                ++setIndex;
                                break;
                            }
                        }
                    }
                }
            }

            dtg.ItemsSource = rowsBinder;

            // This sets up the event for detecting column resizes.
            foreach (object column in dtg.Columns)
                if (column is DataGridTextColumn dgtc)
                    // Courtesy of Copilot.
                    System.ComponentModel.DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty,
                        typeof(DataGridColumn)).AddValueChanged(dgtc, dtg_ColumnResized!);

            dtg.ContextMenu = mnuData;
        }

        // Present bools in line with the rest of the application.
        public class BooleanToYesNoConverter : IValueConverter

        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value is bool boolValue)
                    return boolValue ? "Yes" : "No";
                return "";
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value is string stringValue)
                    return stringValue.Equals("Yes", StringComparison.OrdinalIgnoreCase);
                return false;
            }
        }

        public Dictionary<string, int> maxLengthOverrides = new();

        public string GetCurrentlySelectedID()
        {
            return GetCurrentlySelectedCell(0);
        }
        public string GetCurrentlySelectedCell(int column)
        {
            if (dtg.SelectedItem == null)
                return "";
            Row selectedRow = (Row)dtg.SelectedItem;
            if (column > selectedRow.items.Count)
                return "";
            object? item = selectedRow.items[column];
            if (item == null)
                return "";
            string? id = item.ToString();
            if (id == null)
                return "";
            return id;
        }
        public List<string> GetCurrentlySelectedIDs()
        {
            return GetCurrentlySelectedCells(0);
        }
        public List<string> GetCurrentlySelectedCells(int column)
        {
            List<string> ids = new();

            try
            {
                foreach (Row row in dtg.SelectedItems)
                {
                    object? o = row.items[column];
                    if (o == null)
                        return new();
                    string? id = o.ToString();
                    if (id == null)
                        return new();
                    ids.Add(id);
                }
                return ids;
            }
            catch
            {
                return new();
            }
        }

        private void StoreViewSettings()
        {
            if (identity == -1)
                return;


            List<string> order = App.us.dataOrder[identity];
            List<bool> hidden = App.us.dataHidden[identity];
            List<double> widths = App.us.dataWidths[identity];

            order.Clear(); hidden.Clear(); widths.Clear();

            int colCount = dtg.Columns.Count;
            order.AddRange(new string[colCount]);
            widths.AddRange(new double[colCount]);
            hidden.AddRange(new bool[colCount]);
            foreach (DataGridColumn col in dtg.Columns)
            {
                order[col.DisplayIndex] = (string)col.Header;
                widths[col.DisplayIndex] = col.Width.DisplayValue;
                hidden[col.DisplayIndex] = col.Visibility == Visibility.Hidden;
            }
        }

        public void EnableMultiSelect()
        {
            dtg.SelectionMode = DataGridSelectionMode.Extended;
            mnuSelectAll.IsEnabled = true;
        }
        public void AddWipeButton()
        {
            MenuItem menuItem = new() { Header = "Clear View" };
            menuItem.Click += btnWipe_Click;
            AddSeparator(false);
            AddContextMenuItem(menuItem, false);

        }

        private void contextShowHideColumnsToggle(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
                foreach (DataGridColumn col in dtg.Columns)
                    if (item.Header == col.Header)
                    {
                        if (col.Visibility == Visibility.Visible)
                        {
                            col.Visibility = Visibility.Hidden;
                            item.Icon = null;
                        }
                        else
                        {
                            col.Visibility = Visibility.Visible;
                            item.Icon = tickIcon.LoadContent();
                        }
                        StoreViewSettings();
                        break;
                    }
        }

        // When clicking on an empty space on the DataGrid, select item 0 if present.
        private void dtg_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

            //// Select a row even if the columns don't extend that far.
            //var hit = VisualTreeHelper.HitTest(dtg, e.GetPosition(dtg));
            //if (hit != null)
            //{
            //    DependencyObject obj = hit.VisualHit;
            //    while (obj != null && !(obj is DataGridRow))
            //        obj = VisualTreeHelper.GetParent(obj);

            //    if (obj is DataGridRow row)
            //        dtg.SelectedItem = row.Item;
            //}
        }

        private void dtg_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // A mousedown on the DataGrid won't be triggered if it's on a data row or header.
            if (dtg.Items.Count != 0 && dtg.SelectedIndex == -1)
            {
                dtg.SelectedIndex = dtg.Items.Count - 1;
                dtg.Focus(); // Otherwise the item highlight is greyed out.
            }

            // Select a row even if the columns don't extend that far.
            var hit = VisualTreeHelper.HitTest(dtg, e.GetPosition(dtg));
            if (hit != null)
            {
                DependencyObject obj = hit.VisualHit;
                while (obj != null && !(obj is DataGridRow))
                    obj = VisualTreeHelper.GetParent(obj);

                if (obj is DataGridRow dgr)
                {
                    Row row = (Row)dgr.Item;
                    dtg.SelectedItem = row;

                    // While we're in here, switch checkboxes on and off if present.
                    if (checkBox)
                    {
                        row.IsChecked = row.IsChecked != true;
                    }
                }
            }
        }

        // When clicking on cell that overflows past the view, don't automatically scroll to bring it into view.
        private void dtg_CancelAutoScroll(object sender, RequestBringIntoViewEventArgs e)
        {
            //e.Handled = true;
        }

        #region Custom Selection Changed Event

        // Define a custom routed event
        public static RoutedEvent SelectionChangedEvent = EventManager.RegisterRoutedEvent(
            "SelectionChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SqlDataGrid));

        // Provide CLR event wrapper
        public event RoutedEventHandler SelectionChanged
        {
            add { AddHandler(SelectionChangedEvent, value); }
            remove { RemoveHandler(SelectionChangedEvent, value); }
        }

        private void dtg_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // For now, assume that we never need selection on checkbox grids.
            if (checkBox)
                dtg.UnselectAllCells(); // For some reason, UnselectAll() doesn't do the trick.
            else
                // When the DataGrid's selection changes, raise the custom event.
                RaiseEvent(new RoutedEventArgs(SelectionChangedEvent));
        }

        #endregion

        #region Custom Double Click Event

        public static RoutedEvent CustomDoubleClickEvent = EventManager.RegisterRoutedEvent(
            "CustomDoubleClick", RoutingStrategy.Bubble, typeof(MouseButtonEventHandler), typeof(SqlDataGrid));

        public event MouseButtonEventHandler CustomDoubleClick
        {
            add { AddHandler(CustomDoubleClickEvent, value); }
            remove { RemoveHandler(CustomDoubleClickEvent, value); }
        }

        private void dtg_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            MouseButtonEventArgs newE = new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, e.ChangedButton)
            {
                RoutedEvent = CustomDoubleClickEvent
            };

            bool IsClickOnScrollbar(DependencyObject originalSource)
            {
                while (originalSource != null)
                {
                    if (originalSource is System.Windows.Controls.Primitives.ScrollBar ||
                        originalSource is System.Windows.Controls.Primitives.DataGridColumnHeader)
                        return true;
                    originalSource = VisualTreeHelper.GetParent(originalSource);
                }
                return false;
            }

            if (!IsClickOnScrollbar((DependencyObject)e.OriginalSource))
                RaiseEvent(newE);
        }

        #endregion

        #region Prevent Horizontal Auto-Scrolling On Cell Selection

        double previousHorizontalScroll = -1;
        private void scrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // This prevents horizontal scrolling to view the selected cell in entirity.

            if (previousHorizontalScroll != -1 && scrollViewer != null)
            {
                scrollViewer.ScrollToHorizontalOffset(previousHorizontalScroll);
                previousHorizontalScroll = -1;
                e.Handled = true;
            }

            // This code basically adds rows that were scrolled out of view back into SelectedItems in order to force
            // them to redraw. I've tried a million solutions, this is the only one that worked, but it's quite slow
            // at high selected counts.

            // Optimise this when you get a sec.

            if (dtg.SelectionMode == DataGridSelectionMode.Single)
                return; // The below is not needed in this case.

            int first = -1;
            int last = -1;

            for (int i = 0; i < dtg.Items.Count; i++)
            {
                var container = dtg.ItemContainerGenerator.ContainerFromIndex(i) as DataGridRow;
                if (container != null)
                {
                    first = i;
                    break;
                }
            }

            for (int i = dtg.Items.Count - 1; i >= 0; i--)
            {
                var container = dtg.ItemContainerGenerator.ContainerFromIndex(i) as DataGridRow;
                if (container != null)
                {
                    last = i;
                    break;
                }
            }

            if (first == -1 || last == -1)
                return;

            List<Row> visibleRows = new();
            // Iterate through the visible range and collect the rows
            for (int i = first; i <= last; i++)
                visibleRows.Add((Row)dtg.Items[i]);

            // Iterate through the selected items
            foreach (var item in visibleRows)
                if (dtg.SelectedItems.Contains(item))
                {
                    dtg.SelectedItems.Remove(item);
                    dtg.SelectedItems.Add(item);
                }
        }

        private void dtg_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            if (scrollViewer != null)
                previousHorizontalScroll = scrollViewer.HorizontalOffset;
        }

        #endregion

        private void dtg_ColumnReordered(object sender, DataGridColumnEventArgs e)
        {
            StoreViewSettings();
        }

        private void dtg_ColumnResized(object sender, EventArgs e)
        {
            StoreViewSettings();
        }

        private void contextShowHideColumns_Loaded(object sender, RoutedEventArgs e)
        {
            if (identity == -1 || !canHideColumns)
                return;

            List<string> order = App.us.dataOrder[identity];
            List<bool> hidden = App.us.dataHidden[identity];
            List<double> widths = App.us.dataWidths[identity];

            menuShowHideColumns.Items.Clear();
            var cols = dtg.Columns.OrderBy(i => i.DisplayIndex);
            foreach (DataGridColumn col in cols)
            {
                MenuItem item = new MenuItem();
                item.Header = col.Header;
                int index = order.IndexOf((string)item.Header);
                if (index != -1)
                {
                    if (!hidden[index])
                        item.Icon = tickIcon.LoadContent();
                }

                item.Click += contextShowHideColumnsToggle;
                item.StaysOpenOnClick = true;
                menuShowHideColumns.Items.Add(item);
            }
        }

        public MenuItem AddContextMenuItem(string name, bool top, RoutedEventHandler function)
        {
            MenuItem menuItem = new() { Header = name };
            menuItem.Click += function;
            if (top)
                mnuData.Items.Insert(0, menuItem);
            else
                mnuData.Items.Add(menuItem);
            return menuItem;
        }
        public void AddContextMenuItem(MenuItem item, bool top)
        {
            if (top)
                mnuData.Items.Insert(0, item);
            else
                mnuData.Items.Add(item);
        }
        public void AddSeparator(bool top)
        {
            if (top)
                mnuData.Items.Insert(0, new Separator());
            else
                mnuData.Items.Add(new Separator());
        }
        public void ToggleContextMenuItem(string text, bool enabled)
        {
            foreach (object o in mnuData.Items)
                if (o is MenuItem mi && (string)mi.Header == text)
                {
                    mi.IsEnabled = enabled;
                    return;
                }
        }

        private void mnuCopy_Click(object sender, RoutedEventArgs e)
        {
            dtg.ClipboardCopyMode = DataGridClipboardCopyMode.ExcludeHeader;
            ApplicationCommands.Copy.Execute(null, dtg);
        }
        private void mnuCopyWithHeaders_Click(object sender, RoutedEventArgs e)
        {
            dtg.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
            ApplicationCommands.Copy.Execute(null, dtg);
        }

        private void dtg_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C ||
                e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift)
            {
                if (Keyboard.IsKeyDown(Key.C) &&
                    (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) &&
                    (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
                {
                    dtg.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
                    ApplicationCommands.Copy.Execute(null, dtg);
                    dtg.ClipboardCopyMode = DataGridClipboardCopyMode.ExcludeHeader;
                }
            }
            else if (e.Key == Key.Escape)
            {
                if (dtg.SelectionMode == DataGridSelectionMode.Extended)
                    dtg.SelectedItems.Clear();
                else
                    dtg.SelectedItem = null;
            }
        }

        private void mnuExportSpreadsheet_Click(object sender, RoutedEventArgs e)
        {
            // Save as...
            string fileName;
            if (!FileExport.GetSaveFileName(out fileName))
                return;

            // Create a new workbook.
            XLWorkbook xl = new();
            IXLWorksheet sheet = xl.AddWorksheet("Data Export");
            sheet.Name = "Exported Data";

            // Add headers.
            IXLCell cell = sheet.Cell(1, 1);
            int[] order = new int[dtg.Columns.Count];
            int n = 0;
            foreach (DataGridColumn col in dtg.Columns.OrderBy(c => c.DisplayIndex))
            {
                order[n] = dtg.Columns.IndexOf(col);
                cell.Value = col.Header.ToString();
                cell = cell.CellRight();
                ++n;
            }

            int columnCount = n;

            // Add rows.
            cell = sheet.Cell(2, 1);
            if (dtg.SelectionMode == DataGridSelectionMode.Extended)
            {
                List<Row> selectedOrdered = dtg.SelectedItems.Cast<Row>().ToList();
                object?[] rowObjects = new object?[dtg.Columns.Count];
                foreach (Row row in selectedOrdered.OrderBy(r => dtg.Items.IndexOf(r)))
                {
                    for (n = 0; n < row.items.Count; ++n)
                        rowObjects[n] = row.items[order[n]];

                    cell.InsertData(rowObjects, true);
                    cell = cell.CellBelow();
                }
            }
            else // Export whole table if multi-select isn't an option.
            {
                foreach (Row row in dtg.Items)
                {
                    object?[] rowObjects = new object?[dtg.Columns.Count];
                    for (n = 0; n < row.items.Count; ++n)
                        rowObjects[n] = row.items[order[n]];

                    cell.InsertData(rowObjects, true);
                    cell = cell.CellBelow();
                }
            }

            // Apply suitable column widths.
            FileExport.AutoWidthColumns(columnCount, sheet);

            // Write file to disk.
            FileExport.SaveFile(xl, fileName);
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (dtg == null) return;

            // Iterate through the selected items
            foreach (var selectedItem in dtg.SelectedItems)
            {
                // Get the DataGridRow for the selected item
                var row = dtg.ItemContainerGenerator.ContainerFromItem(selectedItem) as DataGridRow;

                if (row != null)
                {
                    // Force the row to update its visual state
                    row.InvalidateVisual();
                }
            }
        }

        private void mnuSelectNone_Click(object sender, RoutedEventArgs e)
        {
            if (dtg.SelectionMode == DataGridSelectionMode.Extended)
                dtg.SelectedItems.Clear();
            else
                dtg.SelectedItem = null;
        }

        private void dtg_LayoutUpdated(object sender, EventArgs e)
        {
            // If the the MaxWidth is still 250 here, it means that it didn't have a custom width applied from the
            // user's settings, and needed capping to 250 in order to stop it expanding indefinitely.
            foreach (DataGridColumn col in dtg.Columns)
                if (col.MaxWidth == 250)
                {
                    if (col.ActualWidth == 250)
                        col.Width = 250;
                    col.MaxWidth = float.PositiveInfinity;
                }
        }

        public List<Row> GetCheckedRows()
        {
            List<Row> rows = new();

            if (checkBox)
                for (int i = 0; i < dtg.Items.Count; ++i)
                {
                    Row row = (Row)dtg.Items[i];
                    if (row.IsChecked == true)
                        rows.Add(row);
                }

            return rows;
        }
    }
}
