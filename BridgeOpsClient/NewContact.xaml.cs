using SendReceiveClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
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
    /// Interaction logic for NewContact.xaml
    /// </summary>
    public partial class NewContact : Window
    {
        public NewContact()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (ditContact.ScoopValues())
            {
                Contact nc = new Contact();

                nc.sessionID = App.sd.sessionID;
                if (txtNotes.Text.Length == 0)
                    nc.notes = null;
                else
                    nc.notes = txtNotes.Text;

                ditContact.ExtractValues(out nc.additionalCols, out nc.additionalVals);

                if (App.SendInsert(Glo.CLIENT_NEW_CONTACT, nc))
                    Close();
            }
        }
    }
}
