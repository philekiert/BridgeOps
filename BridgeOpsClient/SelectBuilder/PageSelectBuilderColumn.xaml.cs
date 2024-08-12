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

namespace BridgeOpsClient
{
    public partial class PageSelectBuilderColumn : Page
    {
        public SelectBuilder selectBuilder;
        public Frame frame;

        public PageSelectBuilderColumn(SelectBuilder selectBuilder, Frame frame)
        {
            InitializeComponent();

            if (selectBuilder.columns.Count == 0)
            {
                btnUp.IsEnabled = false;
                btnDown.IsEnabled = false;
            }

            this.selectBuilder = selectBuilder;
            this.frame = frame;
        }

        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            selectBuilder.RemoveRow(this);
        }

        private void btnUp_Click(object sender, RoutedEventArgs e)
        {
            selectBuilder.MoveRow(this, frame, true);
        }

        private void btnDown_Click(object sender, RoutedEventArgs e)
        {
            selectBuilder.MoveRow(this, frame, false);
        }

        public void ToggleUpDownButtons()
        {
            btnUp.IsEnabled = Grid.GetRow(frame) > 0;
            btnDown.IsEnabled = Grid.GetRow(frame) < selectBuilder.columns.Count - 1;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            selectBuilder.UpdateColumns();
        }
    }
}
