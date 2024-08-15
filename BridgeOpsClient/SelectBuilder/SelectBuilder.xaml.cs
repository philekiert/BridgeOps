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
    public partial class SelectBuilder : Window
    {
        public SelectBuilder()
        {
            InitializeComponent();

            AddTab();
        }

        private void AddTab()
        {
            PageSelectBuilder pageSelectBuilder = new();
            Frame frame = new() { Content = pageSelectBuilder };
            TabItem tabItem = new()
            {
                Content = frame,
                Header = "SQL Query"
            };
            tabControl.Items.Add(tabItem);
            tabControl.SelectedItem = tabItem;
        }

        private void btnAddTab_Click(object sender, RoutedEventArgs e)
        {
            AddTab();
            if (tabControl.Items.Count > 1)
                btnRemoveTab.IsEnabled = true;

            ToggleMoveButtons();
        }

        private void btnRemoveTab_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = tabControl.SelectedIndex;

            blockTabInfoUpdate = true;
            tabControl.Items.Remove(tabControl.SelectedItem);
            if (tabControl.Items.Count == 1)
                btnRemoveTab.IsEnabled = false;
            blockTabInfoUpdate = true;

            tabControl.SelectedIndex = selectedIndex < tabControl.Items.Count ? selectedIndex : selectedIndex - 0;

            ToggleMoveButtons();
        }

        private void btnMoveLeft_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = tabControl.SelectedIndex;

            if (selectedIndex <= 0)
                return;

            blockTabInfoUpdate = true;
            TabItem tabItem = (TabItem)tabControl.SelectedItem;
            tabControl.Items.Remove(tabItem);
            tabControl.Items.Insert(selectedIndex - 1, tabItem);
            blockTabInfoUpdate = false;
            tabControl.SelectedItem = tabItem;

            ToggleMoveButtons();
        }

        private void btnMoveRight_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = tabControl.SelectedIndex;

            if (selectedIndex == -1 || selectedIndex >= tabControl.Items.Count - 1)
                return;

            blockTabInfoUpdate = true;
            TabItem tabItem = (TabItem)tabControl.SelectedItem;
            tabControl.Items.Remove(tabItem);
            tabControl.Items.Insert(selectedIndex + 1, tabItem);
            blockTabInfoUpdate = false;
            tabControl.SelectedItem = tabItem;

            ToggleMoveButtons();
        }

        private void ToggleMoveButtons()
        {
            btnMoveLeft.IsEnabled = tabControl.SelectedIndex > 0;
            btnMoveRight.IsEnabled = tabControl.SelectedIndex < tabControl.Items.Count - 1;
        }

        bool blockTabInfoUpdate = false;
        private void tabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (blockTabInfoUpdate)
                return;

            ToggleMoveButtons();
            txtTabName.Text = (string)((TabItem)tabControl.Items[tabControl.SelectedIndex]).Header;
        }

        private void txtTabName_TextChanged(object sender, TextChangedEventArgs e)
        {
            ((TabItem)tabControl.SelectedItem).Header = txtTabName.Text;
        }
    }
}
