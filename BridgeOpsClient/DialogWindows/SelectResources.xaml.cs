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

using SendReceiveClasses;

namespace BridgeOpsClient
{
    public partial class SelectResources : CustomWindow
    {
        List<ResourceRow> resources = new();

        public SelectResources(List<int> resourceOrder)
        {
            InitializeComponent();

            HashSet<int> resourceHash = resourceOrder.ToHashSet();

            List<List<object?>> rows;
            if (!App.Select("Resource", new() { Glo.Tab.RESOURCE_ID, Glo.Tab.RESOURCE_NAME },
                            out _, out rows, false, this))
                // An error message will have been presented in the above function.
                return;

            foreach (var row in rows)
                if (row[0] is int i && row[1] is string s)
                    resources.Add(new(resourceOrder.IndexOf(i), i, s));
            
            // Sort by order first, then name in case of loose rows.
            resources = resources.OrderBy(r => r.order).ThenBy(r => r.name).ToList();

            int n = 0;
            foreach (ResourceRow r in resources)
            {
                grd.RowDefinitions.Add(new() { Height = new(24) });
                Grid row = new() { Height = 24 };
                row.ColumnDefinitions.Add(new() { Width = GridLength.Auto } );
                row.ColumnDefinitions.Add(new() { Width = new(24) } );
                row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) } );
                Button up = new()
                {
                    Width = 24,
                    Height = 12,
                    VerticalAlignment = VerticalAlignment.Top,
                    FontSize = 8,
                    Content = "▲",
                    Margin = new(0, 0, 15, 0),
                    Padding = new(0, -1, 0, 0),
                    BorderThickness = new(1, 1, 1, 0)
                };
                Button down = new()
                {
                    Width = 24,
                    Height = 12,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    FontSize = 8,
                    Content = "▼",
                    Margin = new(0, 0, 15, 0),
                    Padding = new(0, -1, 0, 0)
                };
                CheckBox chk = new()
                {
                    VerticalAlignment = VerticalAlignment.Center
                };
                Label name = new()
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new(5, 0, 0, 0),
                    Padding = new(5, 0, 5, 0),
                    Content = r.name
                };

                up.Click += btnMove_Click;
                down.Click += btnMove_Click;

                Grid.SetColumn(up, 0);
                Grid.SetColumn(down, 0);
                Grid.SetColumn(chk, 1);
                Grid.SetColumn(name, 2);
                row.Children.Add(up);
                row.Children.Add(down);
                row.Children.Add(chk);
                row.Children.Add(name);

                Grid.SetRow(row, n);
                grd.Children.Add(row);

                ++n;
            }
        }

        public void btnMove_Click(object sender, RoutedEventArgs e)
        {
            bool up = (string)((Button)sender).Content == "▲";

            Grid toMove = (Grid)((Button)sender).Parent;
            int index = Grid.GetRow(toMove);
            if (up && index == 0)
                return;
            if (!up && index == resources.Count - 1)
                return;
            
            // YOU WERE HERE :) SWAP ROWS AROUND.
        }

        private void btnSet_Click(object sender, RoutedEventArgs e)
        {

        }
    }

    class ResourceRow
    {
        public int order;
        public int id;
        public string name;
        public ResourceRow(int order, int id, string name) { this.order = order; this.id = id; this.name = name; }
    }
}
