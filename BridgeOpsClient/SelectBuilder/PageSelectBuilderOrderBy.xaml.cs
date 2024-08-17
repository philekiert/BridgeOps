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
    /// <summary>
    /// Interaction logic for PageSelectBuilderOrderBy.xaml
    /// </summary>
    public partial class PageSelectBuilderOrderBy : Page
    {
        public PageSelectBuilder selectBuilder;
        public Frame frame;

        public PageSelectBuilderOrderBy(PageSelectBuilder selectBuilder, Frame frame)
        {
            InitializeComponent();

            if (selectBuilder.orderBys.Count == 0)
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
            selectBuilder.MoveRow(new(frame, this), true);
        }

        private void btnDown_Click(object sender, RoutedEventArgs e)
        {
            selectBuilder.MoveRow(new(frame, this), false);
        }

        public void ToggleUpDownButtons()
        {
            btnUp.IsEnabled = Grid.GetRow(frame) > 0;
            btnDown.IsEnabled = Grid.GetRow(frame) < selectBuilder.orderBys.Count - 1;
        }
    }
}
