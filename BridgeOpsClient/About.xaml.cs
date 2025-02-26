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
using System.Windows.Shapes;

namespace BridgeOpsClient
{
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();

            lblVersion.Content = Glo.VersionNumber;
        }

        bool closing = false;
        private void CustomWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            closing = true;
            this.Owner = null;
            Close();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (!closing)
            {
                this.Owner = null;
                Close();
            }
        }
    }
}
