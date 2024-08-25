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
    public partial class ReorderColumnsRenameHeader : Window
    {
        public string name = "";

        public ReorderColumnsRenameHeader(string name)
        {
            InitializeComponent();

            txtName.Text = name;
            txtName.Focus();
            txtName.SelectAll();
        }

        private void Confirm()
        {
            if (txtName.Text == "")
                return;

            name = txtName.Text;
            DialogResult = true;
        }

        private void txtName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Confirm();
        }

        private void btnConfirm_Click(object sender, RoutedEventArgs e)
        {
            Confirm();
        }

        private void txtName_TextChanged(object sender, TextChangedEventArgs e)
        {
            btnConfirm.IsEnabled = txtName.Text.Length > 0;
        }
    }
}
