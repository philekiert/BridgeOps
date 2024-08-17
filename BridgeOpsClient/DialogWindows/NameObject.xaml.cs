using SendReceiveClasses;
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
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace BridgeOpsClient.DialogWindows
{
    public partial class NameObject : Window
    {
        public NameObject(string title)
        {
            InitializeComponent();
            Title = title;
        }

        private void btnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckLegal())
                return;

            DialogResult = true;
            Close();
        }

        private void txtName_TextChanged(object sender, TextChangedEventArgs e)
        {
            btnSubmit.IsEnabled = CheckLegal();
        }

        bool CheckLegal()
        {
            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();

            if (txtName.Text == "")
                return false;

            foreach(char c in txtName.Text)
            {
                if (invalidChars.Contains(c))
                    return false;
            }
            return true;
        }
    }
}
