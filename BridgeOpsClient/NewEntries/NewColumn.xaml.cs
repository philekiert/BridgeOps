using System;
using System.Collections.Generic;
using System.IO;
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
    public partial class NewColumn : CustomWindow
    {
        ColumnRecord.Column columnDetails;
        bool edit = false;
        bool integral = false; // Tracks whether or not this column can be edited save for type and friendly name.

        // Add
        public NewColumn()
        {
            InitializeComponent();

            txtColumnName.Focus();
        }
        // Edit
        public NewColumn(string table, string column, ColumnRecord.Column columnDetails)
        {
            InitializeComponent();

            Title = "Edit Column";
            btnAdd.Content = "Edit";

            edit = true;
            integral = !Glo.Fun.ColumnRemovalAllowed(table, column);

            this.columnDetails = columnDetails;
            cmbTable.Text = table;
            cmbTable.IsEnabled = false;
            txtColumnName.Text = column;
            txtColumnName.IsEnabled = !integral;
            txtFriendlyName.IsEnabled = integral;
            txtFriendlyName.Text = columnDetails.friendlyName;
            txtLimit.Text = columnDetails.restriction.ToString();
            txtAllowed.Text = string.Join("\r\n", columnDetails.allowed);
            chkSoftDuplicate.IsChecked = columnDetails.softDuplicateCheck;
            chkUnique.IsChecked = columnDetails.unique;

            if (columnDetails.type == "VARCHAR" && columnDetails.restriction == Int32.MaxValue)
                cmbType.SelectedIndex = 1;
            else
                cmbType.Text = columnDetails.type == "BIT" ? "BOOLEAN" : columnDetails.type;

            if (integral) // If integral to the database's structure, not as in integer
            {
                // Soft duplicates are only allowed for added columns.
                chkSoftDuplicate.IsEnabled = false;
                chkUnique.IsEnabled = false;

                if (columnDetails.type.Contains("INT"))
                {
                    updatingTypeOptions = true;
                    // Restrict to only other INT types.
                    cmbType.Items.RemoveAt(0);
                    cmbType.Items.RemoveAt(0);
                    cmbType.Items.RemoveAt(3);
                    cmbType.Items.RemoveAt(3);
                    cmbType.Items.RemoveAt(3);
                    cmbType.Items.RemoveAt(3);
                    cmbType.Text = columnDetails.type;
                    cmbType.SelectedIndex = cmbType.SelectedIndex;
                    updatingTypeOptions = false;
                }
                else if (columnDetails.type.StartsWith("VARCHAR"))
                {
                    updatingTypeOptions = true;
                    cmbType.Items.RemoveAt(2);
                    cmbType.Items.RemoveAt(2);
                    cmbType.Items.RemoveAt(2);
                    cmbType.Items.RemoveAt(2);
                    cmbType.Items.RemoveAt(2);
                    cmbType.Items.RemoveAt(2);
                    cmbType.Items.RemoveAt(2);

                    if ((table == "Organisation" && column == Glo.Tab.ORGANISATION_REF) ||
                        (table == "Asset" && column == Glo.Tab.ASSET_REF) ||
                        (table == "Login" && column == Glo.Tab.LOGIN_USERNAME))
                    {
                        cmbType.Items.RemoveAt(1);
                        cmbType.IsEnabled = false;
                    }
                    if ((table == "Organisation" && column == Glo.Tab.PARENT_REF) ||
                        (table == "Conference" && column == Glo.Tab.PARENT_REF))
                    {
                        cmbType.IsEnabled = false;
                        txtLimit.IsEnabled = false;
                        txtAllowed.IsEnabled = false;
                    }

                    updatingTypeOptions = false;
                    if (columnDetails.restriction == Int32.MaxValue)
                        cmbType.SelectedIndex = 1;
                }
                else
                    cmbType.IsEnabled = false;
            }

            if (table == "Login")
            {
                cmbTable.IsEditable = true;
                cmbTable.Text = "Login";
            }

            StoreOriginalValues();
            btnAdd.IsEnabled = false;
        }

        bool updatingTypeOptions = false;
        private void cmbType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (updatingTypeOptions)
                return;

            string newSel = (string)((ComboBoxItem)cmbType.Items[cmbType.SelectedIndex]).Content;
            if (newSel != "VARCHAR")
            {
                txtLimit.IsEnabled = false;
                txtAllowed.IsEnabled = false;
            }
            switch (newSel)
            {
                case "VARCHAR":
                    txtLimit.IsEnabled = true;
                    txtLimit.Text = "";
                    txtAllowed.IsEnabled = !integral;
                    if (columnDetails.type == "VARCHAR" && columnDetails.restriction <= 65535)
                        txtLimit.Text = columnDetails.restriction.ToString();
                    break;
                case "VARCHAR(MAX)":
                    txtLimit.Text = "2^31-1";
                    break;
                case "TINYINT":
                    txtLimit.Text = "255";
                    break;
                case "SMALLINT":
                    txtLimit.Text = "32767";
                    break;
                case "INT":
                    txtLimit.Text = "2,147,483,647";
                    break;
                case "BIGINT":
                    txtLimit.Text = "9,223,372,036,854,775,807";
                    break;
                default: // DATE, TIME, DATETIME or BOOLEAN
                    txtLimit.Text = "";
                    break;
            }

            InputHandler(sender, e);
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmLegalValues())
                return;

            List<string> allowed = new();
            if (txtAllowed.Text.Length > 0 && cmbType.Text == "VARCHAR")
                allowed = txtAllowed.Text.Split("\r\n").ToList();

            if (!edit) // If adding.
            {
                SendReceiveClasses.TableModification mod = new(App.sd.sessionID, ColumnRecord.columnRecordID,
                                                               cmbTable.Text, ColumnName,
                                                               cmbType.Text, allowed, chkUnique.IsChecked == true);
                mod.softDuplicateCheck = chkSoftDuplicate.IsChecked == true;

                VARCHAR(ref mod);
                SendToServer(mod);
            }
            else // If editing.
            {
                int limit;
                int.TryParse(txtLimit.Text, out limit);
                bool varcharMaxChanged = originalType == "VARCHAR" && cmbType.Text == "VARCHAR" &&
                                            limit != originalMax;

                SendReceiveClasses.TableModification mod =
                    new(App.sd.sessionID,
                        ColumnRecord.columnRecordID,
                        cmbTable.Text,
                        originalColumn,
                        ColumnName != originalColumn ? ColumnName : null,
                        txtFriendlyName.Text != originalFriendly ? txtFriendlyName.Text : null,
                        cmbType.Text != originalType || varcharMaxChanged ? cmbType.Text : null,
                        allowed, chkUnique.IsChecked == true);

                if (!integral)
                    mod.softDuplicateCheck = chkSoftDuplicate.IsChecked == true;

                VARCHAR(ref mod);
                SendToServer(mod);
            }
        }

        private void VARCHAR(ref SendReceiveClasses.TableModification mod)
        {
            if (mod.columnType != "VARCHAR")
                return;

            int limit;
            int.TryParse(txtLimit.Text, out limit);
            if (limit < 1)
                limit = 1;
            if (limit <= 8000)
                mod.columnType = $"VARCHAR({limit})";
        }
        private string ColumnName { get { return txtColumnName.Text.Replace(' ', '_'); } }

        private bool ConfirmLegalValues()
        {
            if (cmbTable.Text == "")
            {
                App.DisplayError("Must select a table.");
                return false;
            }
            if (txtColumnName.IsEnabled == true && txtColumnName.Text.Length == 0)
            {
                App.DisplayError("Must input a column name.");
                return false;
            }
            if (cmbType.IsEnabled == true && cmbType.SelectedIndex == -1)
            {
                App.DisplayError("Must select a column type.");
                return false;
            }
            if (txtLimit.IsEnabled == true)
            {
                int limit;
                if (int.TryParse(txtLimit.Text, out limit))
                {
                    if (limit < 1)
                    {
                        App.DisplayError("Max value must be greater than 0.");
                        return false;
                    }
                    if (limit > 65535)
                    {
                        App.DisplayError("Max value must be less than 65536.");
                        return false;
                    }
                }
                else
                {
                    App.DisplayError("Invalid max value.");
                    return false;
                }
            }
            return true;
        }

        string originalTable = "";
        string originalColumn = "";
        string originalFriendly = "";
        string originalType = "";
        int originalMax = 0;
        string originalAllowed = "";
        bool originalSoftDuplicate = false;
        bool originalUnique = false;

        private void StoreOriginalValues()
        {
            originalTable = cmbTable.Text;
            originalColumn = ColumnName;
            originalFriendly = txtFriendlyName.Text;
            originalType = cmbType.Text;
            int.TryParse(txtLimit.Text, out originalMax);
            originalAllowed = txtAllowed.Text;
            originalSoftDuplicate = chkSoftDuplicate.IsChecked == true;
            originalUnique = chkUnique.IsChecked == true;
        }
        private bool DetectAlterations()
        {
            int newMax;
            int.TryParse(txtLimit.Text, out newMax);

            bool altered = originalTable != cmbTable.Text ||
                           originalColumn != ColumnName ||
                           originalFriendly != txtFriendlyName.Text ||
                           originalType != (string)((ComboBoxItem)cmbType.Items[cmbType.SelectedIndex]).Content ||
                           originalMax != newMax ||
                           originalAllowed != txtAllowed.Text ||
                           originalSoftDuplicate != (chkSoftDuplicate.IsChecked == true) ||
                           originalUnique != (chkUnique.IsChecked == true);

            btnAdd.IsEnabled = altered;
            return altered;
        }

        public bool changeMade = false;
        private void SendToServer(SendReceiveClasses.TableModification mod)
        {
            lock (App.streamLock)
            {
                NetworkStream? stream = App.sr.NewClientNetworkStream(App.sd.ServerEP);
                try
                {
                    if (stream != null)
                    {
                        stream.WriteByte(Glo.CLIENT_TABLE_MODIFICATION);
                        App.sr.WriteAndFlush(stream, App.sr.Serialise(mod));
                        int response = stream.ReadByte();
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                        {
                            changeMade = true;
                            Close();
                            return;
                        }
                        else if (response == Glo.CLIENT_CONFIRM)
                        {
                            if (App.DisplayQuestion("This column contains data, either current or historical. " +
                                                    "Changing the type of a column can lead to data loss if the new " +
                                                    "type is unable to represent the currently held data. It is highly " +
                                                    "recommended that you back up the database before making this " +
                                                    "change, as if the change is successful, any data loss will be " +
                                                    "irreversible." +
                                                    "\n\nAre you sure you wish to proceed?",
                                                    "Change Type", DialogWindows.DialogBox.Buttons.OKCancel))
                            {
                                stream.WriteByte(Glo.CLIENT_CONFIRM);
                                response = stream.ReadByte();
                                if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                                {
                                    App.DisplayError("The column could not be removed. See SQL error:\n\n" +
                                                    App.sr.ReadString(stream));
                                    return;
                                }
                                else if (response == Glo.CLIENT_REQUEST_SUCCESS)
                                {
                                    changeMade = true;
                                    Close();
                                    return;
                                }
                                else
                                    throw new Exception();
                            }
                            else
                                stream.WriteByte(Glo.CLIENT_CANCEL);
                        }
                        else if (response == Glo.CLIENT_SESSION_INVALID)
                        {
                            App.SessionInvalidated();
                            Close();
                            return;
                        }
                        else if (response == Glo.CLIENT_INSUFFICIENT_PERMISSIONS)
                        {
                            // Shouldn't ever arrive here.
                            App.DisplayError("Only admins can make table alterations.");
                            return;
                        }
                        else if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                        {
                            App.DisplayError("The table alteration could not be made. See SQL error:\n\n" +
                                            App.sr.ReadString(stream));
                            return;
                        }
                        else
                            throw new Exception();
                    }
                    else
                        App.DisplayError("Could not create network stream.");
                }
                catch
                {
                    App.DisplayError("Could not run table alteration.");
                    return;
                }
                finally
                {
                    if (stream != null) stream.Close();
                }
            }
        }

        private void InputHandler(object? sender, EventArgs? e)
        {
            if (edit)
                DetectAlterations();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            App.WindowClosed();
        }

        private void chk_Click(object sender, RoutedEventArgs e)
        {
            InputHandler(null, null);
        }
    }
}
