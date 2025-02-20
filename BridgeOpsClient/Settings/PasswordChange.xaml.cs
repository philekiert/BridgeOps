﻿using System;
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
using SendReceiveClasses;

namespace BridgeOpsClient
{
    public partial class PasswordChange : CustomWindow
    {
        int id;
        bool adminReset = false; // Switched on if the window was opened by the admin for another user.
        public PasswordChange(int id, bool adminReset)
        {
            InitializeComponent();
            InitialiseFields();

            this.id = id;
            this.adminReset = adminReset;

            // If this is from the user settings menu, we don't need the original password and only an admin can
            // passwords from here.
            if (adminReset)
            {
                lblCurrent.Visibility = Visibility.Collapsed;
                pwdCurrent.Visibility = Visibility.Collapsed;
            }

            pwdCurrent.Focus();
        }

        private void InitialiseFields()
        {
            // Implement max length. Max lengths in the DataInputTa/ble are set automatically.
            pwdCurrent.MaxLength = Glo.Fun.LongToInt(
                ((ColumnRecord.Column)ColumnRecord.login["Password"]!).restriction);
            pwdNew.MaxLength = pwdCurrent.MaxLength;
            pwdConfirm.MaxLength = pwdCurrent.MaxLength;
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            if (pwdNew.Password != pwdConfirm.Password)
            {
                App.DisplayError("New passwords must match.", this);
                return;
            }

            PasswordResetRequest req = new(App.sd.sessionID, id, pwdCurrent.Password, pwdNew.Password, adminReset);

            lock (App.streamLock)
            {
                NetworkStream? stream = App.sr.NewClientNetworkStream(App.sd.ServerEP);
                try
                {
                    if (stream == null)
                        throw new Exception(App.NO_NETWORK_STREAM);
                    stream.WriteByte(Glo.CLIENT_PASSWORD_RESET);
                    App.sr.WriteAndFlush(stream, App.sr.Serialise(req));

                    int result = App.sr.ReadByte(stream);
                    switch (result)
                    {
                        case Glo.CLIENT_SESSION_INVALID:
                            App.SessionInvalidated();
                            return;
                        case Glo.CLIENT_INSUFFICIENT_PERMISSIONS:
                            App.DisplayError(App.PERMISSION_DENIED, this);
                            return;
                        case Glo.CLIENT_REQUEST_FAILED:
                            App.DisplayError("Incorrect password.", this);
                            return;
                        case Glo.CLIENT_REQUEST_SUCCESS:
                            App.DisplayError("Password changed successfully.", this);
                            Close();
                            break;
                        default:
                            App.DisplayError("Something went wrong.", this);
                            break;
                    }
                }
                catch { }
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                btnReset_Click(sender, e);
        }
    }
}
