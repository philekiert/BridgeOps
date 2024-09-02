﻿using SendReceiveClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace BridgeOpsClient
{
    public partial class NewConference : Window
    {
        public NewConference(PageConferenceView.ResourceInfo? resource, DateTime start)
        {
            InitializeComponent();

            tmpBuffer.SetMaxValues(99, 59);
            dtpStart.SetDateTime(start);
            dtpEnd.SetDateTime(start.AddHours(1));

            // Populate available resources and select whichever one the user clicked on in the schedule view.
            cmbResource.ItemsSource = PageConferenceView.resourceRowNames;
            if (resource == null)
                App.DisplayError("Could not determine resource from selected row, please set manually.");
            else
                cmbResource.SelectedIndex = resource.SelectedRowTotal;

        }

        private void Window_Closed(object sender, EventArgs e)
        {
            App.WindowClosed();
        }
    }
}
