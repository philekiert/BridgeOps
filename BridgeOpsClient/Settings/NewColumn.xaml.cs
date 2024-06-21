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
    public partial class NewColumn : Window
    {
        // Add
        public NewColumn()
        {
            InitializeComponent();
        }
        // Edit
        public NewColumn(string table, string column, string friendly, string type, string limit, string[] allowed)
        {

        }

        private void cmbType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string newSel = (string)((ComboBoxItem)cmbType.Items[cmbType.SelectedIndex]).Content;
            if (newSel != "TEXT")
            {
                txtLimit.IsEnabled = false;
                txtAllowed.IsEnabled = false;
            }
            switch (newSel)
            {
                case "TEXT":
                    txtLimit.IsEnabled = true;
                    txtLimit.Text = "";
                    txtLimit.MaxLength = 5;
                    txtAllowed.IsEnabled = true;
                    break;
                case "TINYINT":
                    txtLimit.Text = "255";
                    break;
                case "SMALLINT":
                    txtLimit.Text = "32767";
                    break;
                case "INT":
                    txtLimit.Text = "2,147,483,647";
                    break;
                case "BIGINT":
                    txtLimit.Text = "9,223,372,036,854,775,807";
                    break;
                default: // DATE or BOOLEAN
                    txtLimit.Text = "";
                    break;
            }
        }
    }
}
