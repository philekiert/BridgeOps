﻿using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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

namespace BridgeOpsClient.CustomControls
{
    public partial class DataInputTable : UserControl
    {
        public DataInputTable()
        {
            InitializeComponent();
        }

        public List<SendReceiveClasses.ColumnOrdering.Header> headers = new();

        int relevantChildCount = 0;

        public struct ColValue
        {
            public string name;
            public string type;
            public long restriction;
            public string? value;

            public ColValue(string name, string type, long restriction)
            {
                this.name = name;
                this.type = type;
                this.restriction = restriction;
                value = "";
            }
        }
        public List<ColValue> colValues = new();

        OrderedDictionary columns = new();

        public void Initialise(OrderedDictionary columns, string table)
        {
            this.columns = columns;

            int i = 0;
            int iTotal = 0;

            HashSet<int> headerRows = new();
            foreach (var header in headers)
                headerRows.Add(header.position);
            int headerBump = 0; // Used to space for headers where needed.

            var entryList = ColumnRecord.GenerateKvpList(columns);

            foreach (KeyValuePair<string, ColumnRecord.Column> col in entryList)
            {
                bool skip = false;

                // The user may have consecutive headers with the same position integer.
                headerBump += headers.Count(h => h.position == iTotal);

                // Filter out core columns.
                if (!Glo.Fun.ColumnRemovalAllowed("Organisation", col.Key))
                    skip = true;
                else if (!Glo.Fun.ColumnRemovalAllowed("Asset", col.Key))
                        skip = true;
                else if(!Glo.Fun.ColumnRemovalAllowed("Contact", col.Key))
                    skip = true;
                else if (!Glo.Fun.ColumnRemovalAllowed("Conference", col.Key))
                    skip = true;
                else if (!Glo.Fun.ColumnRemovalAllowed("Task", col.Key))
                    skip = true;
                else if (!Glo.Fun.ColumnRemovalAllowed("Visit", col.Key))
                    skip = true;
                else if (!Glo.Fun.ColumnRemovalAllowed("Document", col.Key))
                    skip = true;

                if (!skip)
                {
                    grdMain.RowDefinitions.Add(new());

                    // Add the column name.
                    Label lblName = new Label();
                    lblName.Content = ColumnRecord.GetPrintName(col);
                    lblName.SetValue(Grid.ColumnProperty, 0);
                    lblName.SetValue(Grid.RowProperty, i + headerBump);
                    grdMain.Children.Add(lblName);

                    ColValue colBinding = new(col.Key, col.Value.type, col.Value.restriction);

                    // Add the input field.
                    if (col.Value.type == "DATE")
                    {
                        DatePicker datInput = new();
                        datInput.Height = 24;
                        datInput.Width = 100;
                        datInput.HorizontalAlignment = HorizontalAlignment.Left;
                        datInput.SetValue(Grid.ColumnProperty, 1);
                        datInput.SetValue(Grid.RowProperty, i + headerBump);
#pragma warning disable CS8622
                        datInput.SelectedDateChanged += GenericValueChangedHandler;
#pragma warning restore CS8622
                        grdMain.Children.Add(datInput);
                    }
                    else if (col.Value.type == "DATETIME")
                    {
                        DateTimePicker dtpInput = new();
                        dtpInput.Margin = new Thickness(0, 0, 0, 0);
                        dtpInput.SetValue(Grid.ColumnProperty, 1);
                        dtpInput.SetValue(Grid.RowProperty, i + headerBump);
#pragma warning disable CS8622
                        dtpInput.datePicker.SelectedDateChanged += GenericValueChangedHandler;
                        dtpInput.timePicker.txt.TextChanged += GenericValueChangedHandler;
#pragma warning restore CS8622
                        grdMain.Children.Add(dtpInput);
                    }
                    else if (col.Value.type == "TIME")
                    {
                        TimePicker timInput = new();
                        timInput.SetValue(Grid.ColumnProperty, 1);
                        timInput.SetValue(Grid.RowProperty, i + headerBump);
#pragma warning disable CS8622
                        timInput.txt.TextChanged += GenericValueChangedHandler;
#pragma warning restore CS8622
                        grdMain.Children.Add(timInput);
                    }
                    else if (col.Value.type == "BIT" || col.Value.type == "BOOLEAN")
                    {
                        ComboBox cmbInput = new();
                        cmbInput.SetValue(Grid.ColumnProperty, 1);
                        cmbInput.SetValue(Grid.RowProperty, i + headerBump);
                        cmbInput.ItemsSource = new List<string>() { "", "Yes", "No" };
                        cmbInput.SelectedIndex = 0;
#pragma warning disable CS8622
                        cmbInput.SelectionChanged += GenericValueChangedHandler;
#pragma warning restore CS8622
                        grdMain.Children.Add(cmbInput);
                    }
                    else if (ColumnRecord.IsTypeInt(col.Value.type))
                    {
                        NumberEntry numberEntry = new();
                        numberEntry.SetMinMaxToType(col.Value.type);
                        numberEntry.SetValue(Grid.ColumnProperty, 1);
                        numberEntry.SetValue(Grid.RowProperty, i + headerBump);
#pragma warning disable CS8622
                        numberEntry.txtNumber.TextChanged += GenericValueChangedHandler;
#pragma warning restore CS8622
                        grdMain.Children.Add(numberEntry);
                    }
                    else
                    {
                        if (col.Value.allowed.Length == 0)
                        {
                            // If simple text value.
                            TextBox txtInput = new();
                            txtInput.SetValue(Grid.ColumnProperty, 1);
                            txtInput.SetValue(Grid.RowProperty, i + headerBump);
                            txtInput.MaxLength = Glo.Fun.LongToInt(col.Value.restriction);
#pragma warning disable CS8622
                            txtInput.TextChanged += GenericValueChangedHandler;
#pragma warning restore CS8622
                            grdMain.Children.Add(txtInput);

                            // For VARCHAR(MAX), allow multi-line input.
                            if (col.Value.restriction == Int32.MaxValue)
                            {
                                txtInput.MinHeight = 60;
                                txtInput.MaxHeight = 60;
                                txtInput.VerticalContentAlignment = VerticalAlignment.Top;
                                txtInput.Padding = new Thickness(txtInput.Padding.Left, 2,
                                                                 txtInput.Padding.Right,
                                                                 txtInput.Padding.Bottom);
                                txtInput.AcceptsReturn = true;
                                txtInput.TextWrapping = TextWrapping.Wrap;
                                txtInput.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                            }
                        }
                        else
                        {
                            // If a constraint is in place.
                            ComboBox cmbInput = new();
                            cmbInput.SetValue(Grid.ColumnProperty, 1);
                            cmbInput.SetValue(Grid.RowProperty, i + headerBump);
                            List<string> options = col.Value.allowed.ToList();
                            options.Insert(0, ""); // Wedge a blank option at the beginning.
                            cmbInput.ItemsSource = options;
                            cmbInput.SelectedIndex = 0;
#pragma warning disable CS8622
                            cmbInput.SelectionChanged += GenericValueChangedHandler;
#pragma warning restore CS8622
                            grdMain.Children.Add(cmbInput);
                        }
                    }

                    colValues.Add(colBinding);

                    ++i;
                }

                ++iTotal;
            }

            relevantChildCount = i * 2;

            // Headers need adding last, because ScoopValues() relies on the grid's child order.
            int it = 0;
            foreach (var header in headers)
            {
                // iTotal - i because that's the number of skipped data rows, and + it because each header nudges the
                int row = (header.position - (iTotal - i)) + it;
                if (row == -1)
                {
                    ++it;
                    continue;
                }

                Label label = new()
                {
                    Content = header.name,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold
                };

                if (header.name == "")
                    label.Margin = new Thickness(-5, 0, 10, 15);
                else
                {
                    label.Margin = row == 0 ? new Thickness(-5, 10, 10, 10) : new Thickness(-5, 15, 10, 10);
                    label.BorderBrush = Brushes.LightSteelBlue;
                    label.BorderThickness = new Thickness(0, 0, 0, 1);
                }

                if (header.name == "")
                    label.Height = 14;

                // others below down 1.
                Grid.SetRow(label, row);
                Grid.SetColumnSpan(label, 2);
                grdMain.Children.Add(label);

                grdMain.RowDefinitions.Add(new());
                ++it;
            }
        }

        public void Populate(List<object?> data)
        {
            int i = 0;
            foreach (object child in grdMain.Children)
            {
                if (i % 2 == 1)
                {
                    object? d = data[i / 2];
                    Type t = child.GetType();
                    if (t == typeof(TextBox))
                        ((TextBox)child).Text = d == null ? null : d.ToString();
                    else if (t == typeof(ComboBox))
                    {
                        if (d != null && d.GetType() == typeof(bool))
                            ((ComboBox)child).Text = (bool)d ? "Yes" : "No";
                        else
                            ((ComboBox)child).Text = d == null ? null : d.ToString();
                    }
                    else if (t == typeof(DatePicker))
                        ((DatePicker)child).SelectedDate = d == null ? null : (DateTime)d;
                    else if (t == typeof(DateTimePicker))
                    {
                        if (d != null)
                            ((DateTimePicker)child).SetDateTime((DateTime)d);
                    }
                    else if (t == typeof(TimePicker))
                    {
                        if (d != null)
                            ((TimePicker)child).SetTime((TimeSpan)d);
                    }
                    else if (t == typeof(NumberEntry))
                        ((NumberEntry)child).txtNumber.Text = d == null ? "" : ((int)d).ToString();
                }
                ++i;

                if (i >= relevantChildCount)
                    break;
            }
        }

        public void ToggleFieldsEnabled(bool enabled)
        {
            int i = 0;
            foreach (object child in grdMain.Children)
            {
                if (i % 2 == 1)
                {
                    Type t = child.GetType();
                    if (t == typeof(TextBox))
                        ((TextBox)child).IsReadOnly = !enabled;
                    else if (t == typeof(ComboBox))
                        ((ComboBox)child).IsEnabled = enabled;
                    else if (t == typeof(DatePicker))
                        ((DatePicker)child).IsEnabled = enabled;
                    else if (t == typeof(DateTimePicker))
                        ((DateTimePicker)child).ToggleEnabled(enabled);
                    else if (t == typeof(TimePicker))
                        ((TimePicker)child).ToggleEnabled(enabled);
                    else if (t == typeof(NumberEntry))
                        ((NumberEntry)child).ToggleEnabled(enabled);
                }
                ++i;

                if (i >= relevantChildCount)
                    break;
            }
        }

        // Store the starting values in edit mode. This helps reduce the size of the data being sent over the network.
        // It is checked in the code-behind for the window containing the DataInputTable.
        public List<string?> startingValues = new();
        public void RememberStartingValues()
        {
            ScoopValues();
            ExtractValues(out _, out startingValues);
        }
        public bool CheckForValueChanges()
        {
            ScoopValues();
            List<string?> currentValues;
            ExtractValues(out _, out currentValues);

            for (int n = 0; n < startingValues.Count; ++n)
                if (currentValues[n] != startingValues[n])
                    return true;

            return false;
        }
        private void GenericValueChangedHandler(object sender, EventArgs e)
        {
            if (ValueChangedHandler != null)
                ValueChangedHandler();
        }
        public Func<bool>? ValueChangedHandler;

        public List<string> disallowed = new();
        // Update all columns, return false if any values are invalid. Error messages can be found in disallowed List.
        public bool ScoopValues()
        {
            disallowed.Clear();

            int i = 0;
            foreach (object child in grdMain.Children)
            {
                if (i % 2 == 1)
                {
                    Type t = child.GetType();
                    int curIndex = i / 2;
                    ColValue cv = colValues[curIndex];
                    if (t == typeof(TextBox))
                    {
                        string input = ((TextBox)child).Text;
                        if (input == "")
                            cv.value = null;
                        else
                            cv.value = input;
                    }
                    else if (t == typeof(ComboBox))
                    {
                        ComboBox temp = (ComboBox)child;
                        if (temp.SelectedItem == null)
                            cv.value = null;
                        else
                        {
                            string? val = (string?)temp.SelectedItem;
                            if (cv.type == "BIT" || cv.type == "BOOLEAN")
                            {
                                if (val == "Yes") cv.value = "1";
                                else if (val == "No") cv.value = "0";
                                else cv.value = "";
                            }
                            else
                                cv.value = (string?)temp.SelectedItem;
                        }
                        if (cv.value == "")
                            cv.value = null;
                    }
                    else if (t == typeof(DatePicker))
                    {
                        DateTime? dt = ((DatePicker)child).SelectedDate;
                        if (dt == null)
                            cv.value = null;
                        else
                            cv.value = SendReceiveClasses.SqlAssist.DateTimeToSQL((DateTime)dt, true);
                    }
                    else if (t == typeof(DateTimePicker))
                    {
                        DateTime? dt = ((DateTimePicker)child).GetDateTime();
                        if (dt == null)
                            cv.value = null;
                        else
                            cv.value = SendReceiveClasses.SqlAssist.DateTimeToSQL((DateTime)dt, false);
                    }
                    else if (t == typeof(TimePicker))
                    {
                        TimeSpan? ts = ((TimePicker)child).GetTime();
                        if (ts == null)
                            cv.value = null;
                        else
                            cv.value = SendReceiveClasses.SqlAssist.TimeSpanToSQL((TimeSpan)ts);
                    }
                    else if (t == typeof(NumberEntry))
                    {
                        int? iVal = ((NumberEntry)child).GetNumber();
                        cv.value = iVal == null ? null : ((NumberEntry)child).GetNumber().ToString();
                    }
                    colValues[curIndex] = cv;
                }
                ++i;

                if (i >= relevantChildCount)
                    break;
            }

            if (disallowed.Count > 0)
                return false;
            return true;
        }

        // Return the two lists that the database table structs need.
        public bool ExtractValues(out List<string> names, out List<string?> values)
        {
            List<string> colNames = new();
            List<string?> colVals = new();

            foreach (ColValue cv in colValues)
            {
                colNames.Add(cv.name);
                colVals.Add(cv.value);
            }

            names = colNames;
            values = colVals;

            return true;
        }
        public List<bool> GetNeedsQuotes()
        {
            List<bool> additionalNeedsQuotes = new List<bool>();
            foreach (string c in colValues.Select(i => i.name))
                additionalNeedsQuotes.Add(
                    SendReceiveClasses.SqlAssist.NeedsQuotes(ColumnRecord.GetColumn(columns, c).type));
            return additionalNeedsQuotes;
        }
    }
}
