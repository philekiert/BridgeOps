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
        public NumberEntry()
        {
            InitializeComponent();
        }

        public string Text { get { return txtNumber.Text; } set { txtNumber.Text = value; } }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Disallow and cancel if the change would make the text alphanumeric.
            int value;
            if (!int.TryParse(e.Text, out value))
                e.Handled = true;
        }
    }
}
