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
using static BridgeOpsClient.CustomControls.SqlDataGrid;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

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

        public struct Row
        {
            public List<object?> items { get; set; }

            public Row(List<object?> items)
            {
                this.items = items;
            }
        }

        public void Wipe()
        {
            identity = -1;
            dtg.ItemsSource = null;
            dtg.Columns.Clear();
        }

        List<Row> rowsBinder = new();

        // I have allowed more than one dictionary to be fed to this array, because sometimes you want a select result
        // that joins two tables. The downside is that tables sharing column names need to use the same type and
        // friendly name. I can't see this being an issue, but note it for future reference.
        public void Update(List<string?> columnNames, List<List<object?>> rows, params string[] omitColumns)
        {
            Update(new List<Dictionary<string, ColumnRecord.Column>>(), columnNames, rows, omitColumns);
        }
        public void Update(Dictionary<string, ColumnRecord.Column> tableColDefs,
                           List<string?> columnNames, List<List<object?>> rows, params string[] omitColumns)
        {
            Update(new List<Dictionary<string, ColumnRecord.Column>>() { tableColDefs },
                   columnNames, rows, omitColumns);
        }
        public void Update(List<Dictionary<string, ColumnRecord.Column>> tableColDefs,
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

            DataGridTextColumn? notes = null; // We want to add this one last.
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
                    header.MaxWidth = 256;

                if (s == null)
                    header.Header = "";
                else
                {
                    ColumnRecord.Column col = new ColumnRecord.Column();
                    foreach (Dictionary<string, ColumnRecord.Column> defs in tableColDefs)
                    {
                        if (defs.ContainsKey(s))
                        {
                            col = defs[s];
                            break;
                        }
                    }

                    header.Header = ColumnRecord.GetPrintName(s, col);
                    header.IsReadOnly = true;
                    if (col.type == "DATE")
                    {
                        header.Binding = new Binding(string.Format("items[{0}]", count));
                        header.Binding.StringFormat = "{0:dd/MM/yyyy}";
                    }
                    else if (col.type == "DATETIME")
                    {
                        header.Binding = new Binding(string.Format("items[{0}]", count));
                        header.Binding.StringFormat = "{0:dd/MM/yyyy HH:mm}";
                    }
                    else
                        header.Binding = new Binding(string.Format("items[{0}]", count));
                }
                if (s == Glo.Tab.NOTES) // We want notes at the end at the moment.
                    notes = header;
                else if (s != null && columnstoOmit.ContainsKey(s))
                    header.Visibility = Visibility.Hidden;
                else
                    dtg.Columns.Add(header);
                ++count;
            }

            // Move the notes column to the end.
            if (notes != null)
                dtg.Columns.Add(notes);

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
            foreach (DataGridTextColumn column in dtg.Columns)
            {
                // Courtesy of Copilot.
                System.ComponentModel.DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty,
                    typeof(DataGridColumn)).AddValueChanged(column, dtg_ColumnResized!);
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
            // A mousedown on the DataGrid won't be triggered if it's on a data row or header.
            if (dtg.Items.Count != 0 && dtg.SelectedIndex == -1)
            {
                dtg.SelectedIndex = 0;
                dtg.Focus(); // Otherwise the item highlight is greyed out.
            }


            // Select a row even if the columns don't extend that far.
            var hit = VisualTreeHelper.HitTest(dtg, e.GetPosition(dtg));
            if (hit != null)
            {
                DependencyObject obj = hit.VisualHit;
                while (obj != null && !(obj is DataGridRow))
                    obj = VisualTreeHelper.GetParent(obj);

                if (obj is DataGridRow row)
                    dtg.SelectedItem = row.Item;
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
            // When the DataGrid's selection changes, raise the custom event.
            RaiseEvent(new RoutedEventArgs(SelectionChangedEvent));

            //dtg.SelectedIndex = dtg.SelectedIndex; // Not idea why. Commenting out to see if anything breaks.
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
            if (previousHorizontalScroll != -1 && scrollViewer != null)
            {
                scrollViewer.ScrollToHorizontalOffset(previousHorizontalScroll);
                previousHorizontalScroll = -1;
                e.Handled = true;
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
        }
    }
}
