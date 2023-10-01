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

namespace BridgeOpsClient
{
    /// <summary>
    /// Interaction logic for NewContact.xaml
    /// </summary>
    public partial class NewOrganisation : Window
    {
        public NewOrganisation()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (ditOrganisation.ScoopValues())
            {
                Organisation no = new Organisation();

                no.sessionID = App.sd.sessionID;

                no.organisationID = txtOrgID.Text;
                no.parentOrgID = cmbOrgParentID.Text.Length == 0 ? null : cmbOrgParentID.Text;
                no.dialNo = txtDialNo.Text.Length == 0 ? null : txtDialNo.Text;
                no.notes = txtNotes.Text.Length == 0 ? null : txtNotes.Text;

                ditOrganisation.ExtractValues(out no.additionalCols, out no.additionalVals);

                if (App.SendInsert(Glo.CLIENT_NEW_ORGANISATION, no))
                    Close();
            }
            else
            {

            }
        }
    }
}
