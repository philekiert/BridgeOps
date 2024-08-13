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
    /// <summary>
    /// Interaction logic for SelectBuilderBracketsAddition.xaml
    /// </summary>
    public partial class SelectBuilderBracketsAddition : Window
    {
        public SelectBuilderBracketsAddition(string code)
        {
            InitializeComponent();

            txtSQL.Text = code;
        }

        // Restrict input to brackets.
        private void txtSQL_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = e.Text != "(" && e.Text != ")";
        }

        // Prevent the user from pasting in other text.
        private void txtSQL_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();
        }
    }
}
