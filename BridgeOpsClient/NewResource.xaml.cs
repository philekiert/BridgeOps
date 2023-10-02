﻿using SendReceiveClasses;
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
using System.Windows.Threading;

namespace BridgeOpsClient
{
    public partial class NewResource : Window
    {
        public NewResource()
        {
            InitializeComponent();
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            Resource nr = new Resource();

            nr.sessionID = App.sd.sessionID;

            DateTime from = timeAvailableFrom.GetDateTime();
            DateTime to = timeAvailableTo.GetDateTime();

            if (to < from)
            {
                MessageBox.Show("Availability end before it begins.");
                return;
            }
            else if (to == from)
            {
                MessageBox.Show("Start and end dates and times cannot be the same.");
                return;
            }

            int capacity;
            if (!(int.TryParse(txtCapacity.Text, out capacity) &&
                  capacity > ColumnRecord.resource["Capacity"].restriction &&
                  capacity < 1))
            {
                MessageBox.Show("Capacity must be above 0 and less than " +
                                ColumnRecord.resource["Capacity"].restriction.ToString() + ".");
                return;
            }

            nr.name = txtResourceName.Text;
            nr.availableFrom = timeAvailableFrom.GetDateTime();
            nr.availableTo = timeAvailableFrom.GetDateTime();

            if (App.SendInsert(Glo.CLIENT_NEW_RESOURCE, nr))
                Close();
            else
                MessageBox.Show("Could not create resource.");
}
    }
}
