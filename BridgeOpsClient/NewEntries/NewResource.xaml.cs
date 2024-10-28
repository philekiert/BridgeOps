using SendReceiveClasses;
using System;
using System.Collections.Generic;
using System.Windows;

namespace BridgeOpsClient
{
    public partial class NewResource : CustomWindow
    {
        int id = -1;

        int connMax = Int32.MaxValue;
        int confMax = Int16.MaxValue;
        int rowsMax = Int16.MaxValue;

        public NewResource()
        {
            InitializeComponent();

            txtResourceName.Focus();

            numCapacityConnection.SetMinMax(1, connMax);
            numCapacityConference.SetMinMax(1, confMax);
            numRowsAdditional.SetMinMax(0, rowsMax);
        }

        public NewResource(string id)
        {
            if (!int.TryParse(id, out this.id))
            {
                App.DisplayError("Could not discern resource ID.");
            }

            InitializeComponent();

            btnAdd.Content = "Save";
            btnDelete.Visibility = Visibility.Visible;

            txtResourceName.Focus();

            numCapacityConnection.SetMinMax(1, connMax);
            numCapacityConference.SetMinMax(1, confMax);
            numRowsAdditional.SetMinMax(0, rowsMax);

            txtResourceName.IsReadOnly = !App.sd.editPermissions[Glo.PERMISSION_RESOURCES];
            numCapacityConnection.ToggleEnabled(App.sd.editPermissions[Glo.PERMISSION_RESOURCES]);
            numCapacityConference.ToggleEnabled(App.sd.editPermissions[Glo.PERMISSION_RESOURCES]);
            numRowsAdditional.ToggleEnabled(App.sd.editPermissions[Glo.PERMISSION_RESOURCES]);

            btnAdd.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_RESOURCES];
            btnDelete.IsEnabled = App.sd.deletePermissions[Glo.PERMISSION_RESOURCES];
        }

        public void Populate(List<object?> data)
        {
            if (data[1] != null)
                txtResourceName.Text = (string)data[1]!;
            if (data[2] != null)
                numCapacityConnection.Text = Glo.Fun.GetInt32FromNullableObject(data[2]).ToString()!;
            if (data[3] != null)
                numCapacityConference.Text = Glo.Fun.GetInt32FromNullableObject(data[3]).ToString()!;
            if (data[4] != null)
                numRowsAdditional.Text = Glo.Fun.GetInt32FromNullableObject(data[4]).ToString()!;
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            Resource nr = new Resource();

            nr.sessionID = App.sd.sessionID;
            nr.columnRecordID = ColumnRecord.columnRecordID;

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
            nr.connectionCapacity = (int)connCap;
            nr.conferenceCapacity = (int)confCap;
            nr.rowsAdditional = (int)rowsAdd;

            bool success = false;
            if (id < 0)
                success = App.SendInsert(Glo.CLIENT_NEW_RESOURCE, nr);
            else
            {
                nr.resourceID = id;
                success = App.SendUpdate(Glo.CLIENT_UPDATE_RESOURCE, nr);
            }

            if (success)
            {
                Close();
                App.PullResourceInformation();
                if (MainWindow.pageDatabase != null)
                    MainWindow.pageDatabase.RepeatSearches(9);
            }
            // Errors presented in SendInsert() or SendUpdate().
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            App.WindowClosed();
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!App.DeleteConfirm(false))
                return;

            if (id < 0)
            {
                App.DisplayError("Could not discern ID from resource.");
                return;
            }

            if (App.SendDelete("Resource", Glo.Tab.RESOURCE_ID, id.ToString(), false))
            {
                Close();
                if (MainWindow.pageDatabase != null)
                    MainWindow.pageDatabase.RepeatSearches(9);
            }
        }
    }
}
