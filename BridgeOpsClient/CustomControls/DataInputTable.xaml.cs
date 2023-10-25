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
            public int restriction;
            public string? value = "";

            public ColValue(string name, string type, int restriction)
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
                        grdMain.Children.Add(dtpInput);
                    }
                    else
                    {
                        if (col.Value.allowed.Length == 0)
                        {
                            // If simple text value.
                            TextBox txtInput = new();
                            txtInput.SetValue(Grid.ColumnProperty, 1);
                            txtInput.SetValue(Grid.RowProperty, i);
                            if (col.Value.type == "TEXT")
                                txtInput.MaxLength = col.Value.restriction;
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
                            options.Insert(0, " "); // Wedge a blank option at the beginning.
                            cmbInput.ItemsSource = options;
                            cmbInput.SelectedIndex = 0;
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
                        ((ComboBox)child).Text = d == null ? null : d.ToString();
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
            if (!ExtractValues(out _, out startingValues))
                MessageBox.Show("Some data appears to  be missing or corrupted. " +
                                "Editing should fix this, " +
                                "but be careful to make sure all known data is present before saving.");
        }

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
                        else if (cv.type == "TEXT")
                            cv.value = input;
                        else // Some sort of INT and needs checking against restriction.
                        {
                            int value;
                            bool isNumber = int.TryParse(input, out value);
                            if (isNumber || value < 0 || value > cv.restriction)
                                disallowed.Add(cv.name.Replace('_', ' ') + " must be a whole number lower than " +
                                               cv.restriction.ToString() + ";");
                        }
                    }
                    else if (t == typeof(ComboBox))
                    {
                        if (((ComboBox)child).SelectedItem == null)
                            cv.value = null;
                        else
                            cv.value = ((ComboBox)child).Text;
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
