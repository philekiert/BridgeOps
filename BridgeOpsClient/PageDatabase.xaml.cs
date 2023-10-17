﻿using System;
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
    public partial class PageDatabase : Page
    {
        MainWindow containingWindow;

        // Store the views for easy toggling of +/- buttons.
        List<PageDatabaseView> views = new();

        // Set up the page with only one frame - the user can then add more if needed.
        public int paneCount = 0;
        public PageDatabase(MainWindow containingWindow)
        {
            this.containingWindow = containingWindow;
            InitializeComponent();
            AddPane(-1);
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
                frame.SetValue(Grid.RowProperty, paneCount);
                PageDatabaseView view = new PageDatabaseView(this, frame);
                frame.Content = view;
                views.Add(view);
                ++paneCount;

                grdPanes.Children.Insert(index + 1, frame);

                // Reset all row indexes.
                for (int i = 0; i < grdPanes.Children.Count; ++i)
                {
                    grdPanes.Children[i].SetValue(Grid.RowProperty, i);
                    // Children of this grid will only ever be Frames containing PageDatabaseViews.
                }

                EnforceMinimumSize();
                SwitchButtonsOnOrOff();
            }
        }

        public void RemovePan(Frame element)
        {
            int index = Grid.GetRow(element);
            if (paneCount > 1)
            {
                grdPanes.RowDefinitions.RemoveAt(index);
                grdPanes.Children.RemoveAt(index);
                views.Remove((PageDatabaseView)element.Content);


                for (int i = 0; i < grdPanes.Children.Count; ++i)
                    grdPanes.Children[i].SetValue(Grid.RowProperty, i);

                --paneCount;

                EnforceMinimumSize();
                SwitchButtonsOnOrOff();
            }
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
            // I happen to know that th title bar and menu bar add up to 63 here, but this needs reworking some time.
            containingWindow.MinHeight = 180 * paneCount + 63;
        }
    }
}
