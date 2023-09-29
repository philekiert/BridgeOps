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
    /// <summary>
    /// Interaction logic for DataInputTable.xaml
    /// </summary>
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
            public string value = "";

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

                /* This is very very botch, it should be done elsewhere but things are a bit tight
                   at the moment. It's here because the below column names have dedicated fields. */
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
                    if (col.Value.type == "Text")
                    {
                        TextBox txtInput = new TextBox();
                        txtInput.SetValue(Grid.ColumnProperty, 1);
                        txtInput.SetValue(Grid.RowProperty, i);
                        txtInput.MaxLength = col.Value.restriction;

                        grdMain.Children.Add(txtInput);

                    }

                    colValues.Add(colBinding);

                    ++i;
                }
            }
        }

        // Update all columns, return false if any values are invalid.
        public bool ScoopValues()
        {
            int i = 0;
            foreach (object child in grdMain.Children)
            {
                if (i % 2 == 1)
                {
                    Type t = child.GetType();
                    int curIndex = i / 2;
                    ColValue cv = colValues[curIndex];
                    if (t == typeof(TextBox))
                        cv.value = ((TextBox)child).Text;
                    colValues[curIndex] = cv;
                }

                ++i;
            }

            return true;
        }

        // Return the two lists that the database table structs need.
        public bool ExtractValues(out List<string> names, out List<string> values)
        {
            List<string> colNames = new();
            List<string> colVals = new();

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
