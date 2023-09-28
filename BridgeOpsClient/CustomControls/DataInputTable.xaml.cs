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

        public void Initialise(Dictionary<string, ColumnRecord.Column> columns)
        {
            int i = 0;
            foreach (KeyValuePair<string, ColumnRecord.Column> col in columns)
            {
                grdMain.RowDefinitions.Add(new RowDefinition());

                // Add the column name.
                Label lblName = new Label();
                lblName.Content = ColumnRecord.GetPrintName(col);
                lblName.SetValue(Grid.ColumnProperty, 0);
                lblName.SetValue(Grid.RowProperty, i);
                grdMain.Children.Add(lblName);

                // Add the input field.
                if (col.Value.type == "Text")
                {
                    TextBox txtInput = new TextBox();
                    txtInput.SetValue(Grid.ColumnProperty, 1);
                    txtInput.SetValue(Grid.RowProperty, i);
                    txtInput.MaxLength = col.Value.restriction;
                    grdMain.Children.Add(txtInput);
                }

                ++i;
            }
        }
    }
}
