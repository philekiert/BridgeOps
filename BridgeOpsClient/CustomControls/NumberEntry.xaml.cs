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
    public partial class NumberEntry : UserControl
    {
        int min = int.MinValue;
        int max = int.MaxValue;

        public NumberEntry()
        {
            InitializeComponent();
        }

        public string Text { get { return txtNumber.Text; } set { txtNumber.Text = value; } }

        private int GetNumber()
        {
            // If the user has somehow entered a value above or below the max, don't fix it here as an error will be
            // thrown when the insert takes place.

            int i;
            if (int.TryParse(txtNumber.Text, out i))
                return i;
            else
                return min > 0 ? min : 0;
        }

        private void btnIncrement_Click(object sender, RoutedEventArgs e)
        {
            if (txtNumber.Text == "")
                txtNumber.Text = max < 1 ? max.ToString() : "1";

        }

        private void btnDecrement_Click(object sender, RoutedEventArgs e)
        {
            if (txtNumber.Text == "")
                txtNumber.Text = min > 0 ? min.ToString() : "0";
            else
            {
                int i;
                if (int.TryParse(txtNumber.Text.ToString(), out i))
                {
                    --i;
                    txtNumber.Text = i < min ? min.ToString() : i.ToString();
                }
            }
        }

        string lastVal = "";
        private void txtNumber_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Disallow and cancel if the change would make the text alphanumeric.
            int value;
            if (!int.TryParse(txtNumber.Text, out value) && txtNumber.Text != "")
                txtNumber.Text = lastVal;
            else
                lastVal = txtNumber.Text;
        }
    }
}
