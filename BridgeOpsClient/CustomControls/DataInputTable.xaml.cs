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

namespace BridgeOpsClient.CustomControls
{
    public partial class DataInputTable : UserControl
    {
        public DataInputTable()
        {
            InitializeComponent();
        }


        public struct ColValue
        {
            public string name;
            public string type;
            public long restriction;
            public string? value = "";

            public ColValue(string name, string type, long restriction)
            {
                this.name = name;
                this.type = type;
                this.restriction = restriction;
            }
        }
        public List<ColValue> colValues = new();

        public void Initialise(Dictionary<string, ColumnRecord.Column> columns, string table)
        {
            int i = 0;

            foreach (KeyValuePair<string, ColumnRecord.Column> col in columns)
            {
                bool skip = false;

                /* This is very very botch, it should be done more elegantly elsewhere but things are a bit tight
                   at the moment. It's here because the affected column names have dedicated fields. */
                if (table == "Contact")
                {
                    if (col.Key == "Contact_ID" ||
                        col.Key == "Notes")
                        skip = true;
                }
                else if (table == "Organisation")
                {
                    if (col.Key == "Organisation_ID" ||
                        col.Key == "Parent_ID" ||
                        col.Key == "Dial_No" ||
                        col.Key == "Notes")
                        skip = true;
                }
                else if (table == "Asset")
                {
                    if (col.Key == "Asset_ID" ||
                        col.Key == "Organisation_ID" ||
                        col.Key == "Notes")
                        skip = true;
                }

                if (!skip)
                {
                    grdMain.RowDefinitions.Add(new RowDefinition());

                    // Add the column name.
                    Label lblName = new Label();
                    lblName.Content = ColumnRecord.GetPrintName(col);
                    lblName.SetValue(Grid.ColumnProperty, 0);
                    lblName.SetValue(Grid.RowProperty, i);
                    grdMain.Children.Add(lblName);

                    ColValue colBinding = new(col.Key, col.Value.type, col.Value.restriction);

                    // Add the input field.
                    if (col.Value.type == "DATE")
                    {
                        DatePicker dtpInput = new();
                        dtpInput.SetValue(Grid.ColumnProperty, 1);
                        dtpInput.SetValue(Grid.RowProperty, i);
#pragma warning disable CS8622
                        dtpInput.SelectedDateChanged += GenericValueChangedHandler;
#pragma warning restore CS8622
                        grdMain.Children.Add(dtpInput);
                    }
                    else if (col.Value.type == "BIT" || col.Value.type == "BOOLEAN")
                    {
                        ComboBox cmbInput = new();
                        cmbInput.SetValue(Grid.ColumnProperty, 1);
                        cmbInput.SetValue(Grid.RowProperty, i);
                        cmbInput.ItemsSource = new List<string>() { "", "Yes", "No" };
                        cmbInput.SelectedIndex = 0;
#pragma warning disable CS8622
                        cmbInput.SelectionChanged += GenericValueChangedHandler;
#pragma warning restore CS8622
                        grdMain.Children.Add(cmbInput);
                    }
                    else
                    {
                        if (col.Value.allowed.Length == 0)
                        {
                            // If simple text value.
                            TextBox txtInput = new();
                            txtInput.SetValue(Grid.ColumnProperty, 1);
                            txtInput.SetValue(Grid.RowProperty, i);
                            if (ColumnRecord.IsTypeString(col.Value))
                                txtInput.MaxLength = Glo.Fun.LongToInt(col.Value.restriction);
#pragma warning disable CS8622
                            txtInput.TextChanged += GenericValueChangedHandler;
#pragma warning restore CS8622
                            // else must be an INT type and will be checked against restriction in ScoopValues().
                            grdMain.Children.Add(txtInput);
                        }
                        else
                        {
                            // If a constraint is in place.
                            ComboBox cmbInput = new();
                            cmbInput.SetValue(Grid.ColumnProperty, 1);
                            cmbInput.SetValue(Grid.RowProperty, i);
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
                }
                ++i;
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
                }
                ++i;
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
                        else if (ColumnRecord.IsTypeString(cv.type))
                            cv.value = input;
                        else // Some sort of INT and needs checking against restriction.
                        {
                            long value;
                            if (!long.TryParse(input, out value) || value < 0 || value > cv.restriction)
                                disallowed.Add(cv.name.Replace('_', ' ') + " must be a whole number lower than " +
                                               cv.restriction.ToString() + ".");
                            else
                                cv.value = input;
                        }
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
                            cv.value = dt.Value.ToString("yyyy-MM-dd");
                    }
                    colValues[curIndex] = cv;
                }
                ++i;
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
    }
}
