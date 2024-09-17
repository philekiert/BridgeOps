using SendReceiveClasses;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace BridgeOpsClient
{
    public partial class NewUser : CustomWindow
    {
        public bool didSomething = false;
        public int id = 0;
        string originalUsername = "";
        bool originalAdmin = false;
        int originalCreate = 0;
        int originalEdit = 0;
        int originalDelete = 0;
        bool originalEnabled = true;
        int currentCreate = 0;
        int currentEdit = 0;
        int currentDelete = 0;

        public NewUser()
        {
            InitializeComponent();
            InitialiseFields();
            GetCheckBoxArray();

            btnResetPassword.Visibility = Visibility.Collapsed;

            txtUsername.Focus();
        }
        public NewUser(int id)
        {
            this.id = id;

            this.Title = "Edit User";

            InitializeComponent();
            InitialiseFields();
            GetCheckBoxArray();

            btnAdd.Visibility = Visibility.Hidden;
            btnEdit.Visibility = Visibility.Visible;
            btnEdit.IsEnabled = false;
            btnDelete.Visibility = Visibility.Visible;
            if (id == 1)
            {
                btnEdit.IsEnabled = false;
                btnDelete.IsEnabled = false;
            }
            if (!App.sd.editPermissions[Glo.PERMISSION_USER_ACC_MGMT])
            {
                btnEdit.IsEnabled = false;
                btnResetPassword.IsEnabled = false;
            }
            if (!App.sd.deletePermissions[Glo.PERMISSION_USER_ACC_MGMT] ||
                id == App.sd.loginID)
            {
                btnDelete.IsEnabled = false;
            }

            lblPassword.Visibility = Visibility.Collapsed;
            txtPassword.Visibility = Visibility.Collapsed;
            lblPasswordConfirm.Visibility = Visibility.Collapsed;
            txtPasswordConfirm.Visibility = Visibility.Collapsed;
        }

        private void InitialiseFields()
        {
            // Implement max length. Max lengths in the DataInputTable are set automatically.
            txtUsername.MaxLength = Glo.Fun.LongToInt(ColumnRecord.GetColumn(ColumnRecord.login,
                                                                             Glo.Tab.LOGIN_USERNAME).restriction);
            txtPassword.MaxLength = Glo.Fun.LongToInt(ColumnRecord.GetColumn(ColumnRecord.login,
                                                                             Glo.Tab.LOGIN_PASSWORD).restriction);
            txtPasswordConfirm.MaxLength = Glo.Fun.LongToInt(
                ColumnRecord.GetColumn(ColumnRecord.login,Glo.Tab.LOGIN_PASSWORD).restriction);

            // I don't think there's any reason to implement friendly names for user accounts. If they're added, they
            // just won't do anything, and I think that's fine.
        }

        CheckBox[,] permissionsGrid = new CheckBox[4, 6];
        private void GetCheckBoxArray()
        {
            foreach (UIElement checkBox in grdPermissions.Children)
                if (checkBox is CheckBox)
                    permissionsGrid[Grid.GetColumn(checkBox) - 1, Grid.GetRow(checkBox) - 1] = (CheckBox)checkBox;
        }

#pragma warning disable CS8601
#pragma warning disable CS8602
#pragma warning disable CS8605
        public void Populate(List<object?> data)
        {
            // EDIT
            // This method will not be called if the data has a different Count than expected.
            if (data[1] != null && data[1].ToString() != null)
            {
                originalUsername = data[1].ToString();
                txtUsername.Text = originalUsername;
            }
            if (data[3] != null && data[3].GetType() == typeof(bool))
            {
                originalAdmin = (bool)data[3];
                chkAdmin.IsChecked = originalAdmin;
            }
            if (data[4] != null && data[4].GetType() == typeof(int) &&
                data[5] != null && data[5].GetType() == typeof(int) &&
                data[6] != null && data[6].GetType() == typeof(int))
            {
                originalCreate = (int)data[4];
                originalEdit = (int)data[5];
                originalDelete = (int)data[6];
                currentCreate = originalCreate;
                currentEdit = originalEdit;
                currentDelete = originalDelete;
                ApplyWriteEditDelete();
            }
            if (data[7] != null && data[7].GetType() == typeof(bool))
            {
                originalEnabled = (bool)data[7];
                chkEnabled.IsChecked = originalEnabled;
            }

            if (originalAdmin)
                grdPermissions.Visibility = Visibility.Collapsed;

            if (id == 1)
            {
                txtUsername.IsEnabled = false;
                chkAdmin.IsEnabled = false;
                grdPermissions.Visibility = Visibility.Collapsed;
            }
        }
#pragma warning restore CS8601
#pragma warning restore CS8602
#pragma warning restore CS8605

        private Login GetLoginFromForm()
        {
            Login login = new Login();

            login.sessionID = App.sd.sessionID;
            login.columnRecordID = ColumnRecord.columnRecordID;

            login.loginID = id;
            login.username = txtUsername.Text;
            login.password = txtPassword.Password;
            if (chkAdmin.IsChecked != null && chkAdmin.IsChecked == true)
            {
                login.admin = true;
                login.createPermissions = Glo.PERMISSIONS_MAX_VALUE;
                login.editPermissions = Glo.PERMISSIONS_MAX_VALUE;
                login.deletePermissions = Glo.PERMISSIONS_MAX_VALUE;
            }
            else
            {
                login.admin = false;
                UpdateCurrentEditDelete();
                login.createPermissions = currentCreate;
                login.editPermissions = currentEdit;
                login.deletePermissions = currentDelete;
            }
            if (chkEnabled.IsChecked != null)
                login.enabled = (bool)chkEnabled.IsChecked;

            return login;
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {

            if (txtUsername.Text == "")
            {
                App.DisplayError("You must input a value for Username ID");
                return;
            }
            else if (txtPassword.Password != txtPasswordConfirm.Password)
            {
                App.DisplayError("Passwords do not match.");
                return;
            }


            if (App.SendInsert(Glo.CLIENT_NEW_LOGIN, GetLoginFromForm()))
            {
                didSomething = true;
                Close();
            }
            else
                App.DisplayError("Could not create new user.");
        }

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (txtUsername.Text == "")
            {
                App.DisplayError("You must input a value for Username ID");
                return;
            }
            else if (txtPassword.Password != txtPasswordConfirm.Password)
            {
                App.DisplayError("Passwords do not match.");
                return;
            }

            if (App.SendUpdate(Glo.CLIENT_UPDATE_LOGIN, GetLoginFromForm()))
            {
                didSomething = true;
                Close();
            }
            else
                App.DisplayError("Could not update user.");
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!App.DeleteConfirm(false))
                return;

            if (App.SendDelete("Login", Glo.Tab.LOGIN_ID, id.ToString(), false))
            {
                didSomething = true;
                Close();
            }
            else
                App.DisplayError("Could not delete contact.");
        }

        // Check for changes whenever the user interacts with a control.
        private void ValueChanged(object sender, EventArgs e) { AnyInteraction(); }
        public void AnyInteraction()
        {
            UpdateCurrentEditDelete();

            btnEdit.IsEnabled = (originalUsername != txtUsername.Text ||
                                 originalAdmin != chkAdmin.IsChecked ||
                                 chkAdmin.IsChecked == false && (originalCreate != currentCreate ||
                                                                 originalEdit != currentEdit ||
                                                                 originalDelete != currentDelete) ||
                                 originalEnabled != chkEnabled.IsChecked)
                                 && App.sd.editPermissions[Glo.PERMISSION_USER_ACC_MGMT];
        }

        private void chkAdmin_Clicked(object sender, RoutedEventArgs e)
        {
            if (chkAdmin.IsChecked == true)
                grdPermissions.Visibility = Visibility.Collapsed;
            else
                grdPermissions.Visibility = Visibility.Visible;

            AnyInteraction();
        }

        // Handle the All column when switching boxes on and off.
        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            int gridX = Grid.GetColumn((CheckBox)sender) - 1;
            int gridY = Grid.GetRow((CheckBox)sender) - 1;

            if (gridX == 3)
            {
                bool toSet = true;
                if (permissionsGrid[0, gridY].IsChecked == true &&
                    permissionsGrid[1, gridY].IsChecked == true &&
                    permissionsGrid[2, gridY].IsChecked == true)
                    toSet = false;
                permissionsGrid[0, gridY].IsChecked = toSet;
                permissionsGrid[1, gridY].IsChecked = toSet;
                permissionsGrid[2, gridY].IsChecked = toSet;
            }
            else
            {
                if (permissionsGrid[0, gridY].IsChecked == true &&
                    permissionsGrid[1, gridY].IsChecked == true &&
                    permissionsGrid[2, gridY].IsChecked == true)
                    permissionsGrid[3, gridY].IsChecked = true;
                else
                    permissionsGrid[3, gridY].IsChecked = false;
            }

            AnyInteraction();
        }
        private void UpdateCurrentEditDelete()
        {
            for (int x = 0; x < 3; ++x) // No need to go over the "All" box.
            {
                int permissions = 0;
                for (int y = 0; y < 6; ++y)
                {
                    if (permissionsGrid[x, y].IsChecked == true)
                        permissions += 1 << y;
                }
                if (x == 0)
                    currentCreate = permissions;
                else if (x == 1)
                    currentEdit = permissions;
                else
                    currentDelete = permissions;
            }
        }
        private void ApplyWriteEditDelete()
        {
            for (int x = 0; x < 3; ++x)
            {
                int permissions = currentCreate;
                if (x == 1)
                    permissions = currentEdit;
                else if (x == 2)
                    permissions = currentDelete;

                for (int y = 0; y < 6; ++y)
                {
                    permissionsGrid[x, y].IsChecked = (permissions & (1 << y)) != 0;
                    // Check All box if all others are checked.
                    if (x == 2)
                        permissionsGrid[3, y].IsChecked = (permissionsGrid[0, y].IsChecked == true &&
                                                           permissionsGrid[1, y].IsChecked == true &&
                                                           permissionsGrid[2, y].IsChecked == true);
                }
            }
        }

        private void btnResetPassword_Click(object sender, RoutedEventArgs e)
        {
            PasswordChange pc = new(id, true);
            pc.ShowDialog();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            App.WindowClosed();
        }
    }
}
