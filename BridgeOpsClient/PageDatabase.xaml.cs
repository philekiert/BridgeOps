using DocumentFormat.OpenXml.Drawing.Charts;
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
using System.Xml.Linq;

namespace BridgeOpsClient
{
    public partial class PageDatabase : Page
    {
        MainWindow containingWindow;

        // When settings are first read, MainWindow hasn't been initialised yet, so we need to store the settings and
        // apply them below when the constructor is called.
        public static string storedViewSettingsToApply = "";
        public void ApplyViewSettings(string settings)
        {
            RemoveAllPanes();

            string[] dataViewPanes = settings.Split(';');
            for (int i = 0; i < dataViewPanes.Length - 1; i += 2)
            {
                int row = i / 2;
                AddPane(row - 1);
                views[row].cmbTable.Text = dataViewPanes[i];
                double height;
                if (double.TryParse(dataViewPanes[i + 1], out height))
                    grdPanes.RowDefinitions[row].Height = new(height, GridUnitType.Star);
            }

            // This will be the case if user settings were blank or failed to be read.
            if (grdPanes.Children.Count == 0)
                AddPane(-1);
        }

        // Store the views for easy toggling of +/- buttons.
        public static List<PageDatabaseView> views = new();

        // Set up the page with only one frame - the user can then add more if needed.
        public int paneCount = 0;
        public PageDatabase(MainWindow containingWindow)
        {
            this.containingWindow = containingWindow;
            InitializeComponent();

            // If settings are blank, the method will add a pane.
            ApplyViewSettings(storedViewSettingsToApply);
        }

        public void AddPane(Frame element)
        {
            AddPane(Grid.GetRow(element));
        }
        public void AddPane(int index)
        {
            if (paneCount < 3)
            {
                // Add a new Grid.RowDefinition.
                RowDefinition rowDef = new();
                grdPanes.RowDefinitions.Add(rowDef);

                // Add the new frame.
                Frame frame = new();
                frame.SetValue(Grid.RowProperty, index + 1);

                PageDatabaseView view = new PageDatabaseView(this, frame);
                frame.Content = view;
                views.Add(view);
                ++paneCount;

                grdPanes.Children.Insert(index + 1, frame);

                if (paneCount > 1)
                {
                    GridSplitter splitter = new GridSplitter();
                    splitter.Background = Brushes.Transparent;
                    frame.SetValue(Grid.RowProperty, index);
                    grdPanes.Children.Add(splitter);
                }

                // Reset all row indexes and bottom-padding to accomodate splitters.
                int splitterIndex = 0;
                int paneIndex = 0;
                for (int i = 0; i < grdPanes.Children.Count; ++i)
                {
                    if (grdPanes.Children[i].GetType() != typeof(GridSplitter))
                    {
                        grdPanes.Children[i].SetValue(Grid.RowProperty, paneIndex);
                        ++paneIndex;
                    }
                    else
                    {
                        grdPanes.Children[i].SetValue(Grid.RowProperty, splitterIndex);
                        ++splitterIndex;
                    }
                }

                EnforceMinimumSize();
                SwitchButtonsOnOrOff();
            }

            ResetTabIndices();
        }

        // Never call this function and leave it that way, always add a pane after.
        public void RemoveAllPanes()
        {
            grdPanes.RowDefinitions.Clear();
            grdPanes.Children.Clear();
            views.Clear();
            paneCount = 0;
        }
        public void RemovePane(Frame element)
        {
            int index = Grid.GetRow(element);
            if (paneCount > 1)
            {
                grdPanes.RowDefinitions.RemoveAt(index);
                grdPanes.Children.RemoveAt(index);
                views.Remove((PageDatabaseView)element.Content);

                // Reset all row indexes and remove one splitter
                bool deletedSplitter = false;
                int splitterIndex = 0;
                int paneIndex = 0;
                for (int i = 0; i < grdPanes.Children.Count; ++i)
                {
                    if (grdPanes.Children[i].GetType() != typeof(GridSplitter))
                    {
                        grdPanes.Children[i].SetValue(Grid.RowProperty, paneIndex);
                        ++paneIndex;
                    }
                    else
                    {
                        if (deletedSplitter)
                        {
                            grdPanes.Children[i].SetValue(Grid.RowProperty, splitterIndex);
                            ++splitterIndex;
                        }
                        else
                        {
                            grdPanes.Children.RemoveAt(i);
                            --i;
                            deletedSplitter = true;
                        }
                    }
                }

                --paneCount;

                EnforceMinimumSize();
                SwitchButtonsOnOrOff();
            }

            ResetTabIndices();
        }

        private void SwitchButtonsOnOrOff()
        {
            // Switch - and + buttons on and off as needed.
            if (paneCount == 1)
            {
                foreach (PageDatabaseView v in views)
                    v.btnAddPane.IsEnabled = true;
                foreach (PageDatabaseView v in views)
                    v.btnRemovePane.IsEnabled = false;
            }
            else if (paneCount == 2)
            {
                foreach (PageDatabaseView v in views)
                    v.btnAddPane.IsEnabled = true;
                foreach (PageDatabaseView v in views)
                    v.btnRemovePane.IsEnabled = true;
            }
            else if (paneCount == 3)
            {
                foreach (PageDatabaseView v in views)
                    v.btnAddPane.IsEnabled = false;
                foreach (PageDatabaseView v in views)
                    v.btnRemovePane.IsEnabled = true;
            }
        }

        private void EnforceMinimumSize()
        {
            double minHeight = 182 * paneCount;
            if (paneCount > 1)
                minHeight += 4 * (paneCount - 1); // No idea why, could be grid splitters.

            if (App.mainWindow != null)
                for (int i = 0; i < App.mainWindow.grdMain.RowDefinitions.Count - 1; ++i)
                    minHeight += App.mainWindow.grdMain.RowDefinitions[i].Height.Value;

            minHeight += CustomWindow.titleBarHeight;

            minHeight += Margin.Top;
            minHeight += 1; // Border compensation.
            minHeight += 10; // Margin somewhere.

            containingWindow.MinHeight = minHeight;
        }

        public void ClearSqlDataGrids()
        {
            foreach (PageDatabaseView view in views)
                view.dtgResults.Wipe();
        }

        public void RepeatSearches(int identity)
        {
            // Force any open organisation windows to refresh their asset and contact lists just in case they're
            // effected by a change.
            if (identity == 1)
            {
                foreach (Window window in Application.Current.Windows)
                    if (window is NewOrganisation org)
                        org.PopulateAssets();
            }
            else if (identity == 2)
            {
                foreach (Window window in Application.Current.Windows)
                    if (window is NewOrganisation org)
                        org.PopulateContacts();
            }

            foreach (PageDatabaseView view in views)
                view.RepeatSearch(identity);
        }

        public void ReflectPermissions()
        {
            // When logging in, it could be that a PageDatabaseView is already open. If that's the case some buttons
            // may need updating.
            foreach (PageDatabaseView view in views)
            {
                view.dtgResults.ToggleContextMenuItem("Update Selected",
                                                      App.sd.editPermissions[Glo.PERMISSION_RECORDS]);
                view.dtgResults.ToggleContextMenuItem("Delete Selected",
                                                      App.sd.deletePermissions[Glo.PERMISSION_RECORDS]);
            }
        }

        private void ResetTabIndices()
        {
            // This doesn't work quite right, come back to it.

            int tabStop = 2;
            foreach (PageDatabaseView view in views.OrderBy(view => Grid.GetRow(view)).ToList())
            {
                view.cmbTable.TabIndex = ++tabStop;
                view.cmbColumn.TabIndex = ++tabStop;
                view.btnClear.TabIndex = ++tabStop;
                view.cmbSearchType.TabIndex = ++tabStop;
                view.txtSearch.TabIndex = ++tabStop;
                view.btnSearch.TabIndex = ++tabStop;
                view.btnRemovePane.TabIndex = ++tabStop;
                view.btnAddPane.TabIndex = ++tabStop;
            }
        }
    }
}
