using SendReceiveClasses;
using System;
using System.Windows;

namespace BridgeOpsClient
{
    public partial class NewResource : Window
    {
        int connMax = Int32.MaxValue;
        int confMax = Int16.MaxValue;
        int rowsMax = Int16.MaxValue;

        public NewResource()
        {
            InitializeComponent();

            timeAvailableFrom.SetDateTime(new DateTime(2024, 7, 1));
            timeAvailableTo.SetDateTime(new DateTime(2024, 7, 31));

            txtResourceName.Focus();

            numCapacityConnection.SetMinMax(1, connMax);
            numCapacityConference.SetMinMax(1, confMax);
            numRowsAdditional.SetMinMax(0, rowsMax);
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            Resource nr = new Resource();

            nr.sessionID = App.sd.sessionID;
            nr.columnRecordID = ColumnRecord.columnRecordID;

            DateTime? from = timeAvailableFrom.GetDateTime();
            DateTime? to = timeAvailableTo.GetDateTime();

            if (from == null || to == null)
            {
                App.DisplayError("Must select start and end dates and times.");
                return;
            }

            if (to < from)
            {
                App.DisplayError("Availability end before it begins.");
                return;
            }
            else if (to == from)
            {
                App.DisplayError("Start and end dates and times cannot be the same.");
                return;
            }

            int? connCap = numCapacityConnection.GetNumber();
            int? confCap = numCapacityConference.GetNumber();
            int? rowsAdd = numRowsAdditional.GetNumber();
            if (connCap == null || confCap == null || rowsAdd == null)
            {
                App.DisplayError("Must input capacity values.");
                return;
            }
            if (connCap > connMax || connCap < 1)
            {
                App.DisplayError("Connection capacity must be between 1 and " + connMax + ".");
                return;
            }
            if (confCap > confMax || confCap < 1)
            {
                App.DisplayError("Conference capacity must be between 1 and " + confMax + ".");
                return;
            }
            if (rowsAdd > rowsMax || rowsAdd < 0)
            {
                App.DisplayError("Additional placement rows must be between 0 and " + rowsMax + ".");
                return;
            }

            nr.name = txtResourceName.Text.Length > 0 ? txtResourceName.Text : null;
            nr.availableFrom = (DateTime)from;
            nr.availableTo = (DateTime)to;
            nr.connectionCapacity = (int)connCap;
            nr.conferenceCapacity = (int)confCap;
            nr.rowsAdditional = (int)rowsAdd;

            if (App.SendInsert(Glo.CLIENT_NEW_RESOURCE, nr))
            {
                Close();
                App.PullResourceInformation();
            }
            else
                App.DisplayError("Could not create resource.");
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            App.WindowClosed();
        }
    }
}
