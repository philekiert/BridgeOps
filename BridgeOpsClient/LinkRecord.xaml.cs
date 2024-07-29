using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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
    public partial class LinkRecord : Window
    {
        string table;
        Dictionary<string, ColumnRecord.Column> columns;
        public string? id = null;

        public LinkRecord(string table, Dictionary<string, ColumnRecord.Column> columns)
        {
            InitializeComponent();

            this.table = table;
            this.columns = columns;

            Populate();
        }

        public void Populate()
        {
            // Error message is displayed by App.SelectAll() if something goes wrong.
            List<string?> columnNames;
            List<List<object?>> rows;
            if (App.SelectAll(table, out columnNames, out rows))
            {
                dtg.Update(columns, columnNames, rows);
            }
            dtg.CustomDoubleClick += dtg_DoubleClick;
        }

        private void dtg_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            id = dtg.GetCurrentlySelectedID();
            Close();
        }
    }
}
