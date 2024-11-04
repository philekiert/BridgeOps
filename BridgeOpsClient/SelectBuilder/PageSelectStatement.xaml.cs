using System;
using System.Collections;
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
using SendReceiveClasses;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace BridgeOpsClient
{
    public partial class PageSelectStatement : Page
    {
        public PageSelectStatement()
        {
            InitializeComponent();

            dtgOutput.EnableMultiSelect();
            dtgOutput.AddWipeButton();
            dtgOutput.WipeCallback = WipeCallback;

            btnUpdateSelected = dtgOutput.AddContextMenuItem("Update Selected", false, btnUpdate_Click);
            btnDeleteSelected = dtgOutput.AddContextMenuItem("Delete Selected", false, btnDelete_Click);
            btnUpdateSelected.IsEnabled = false;
            btnDeleteSelected.IsEnabled = false;
        }

        MenuItem btnUpdateSelected;
        MenuItem btnDeleteSelected;

        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (dtgOutput.dtg.SelectedItems.Count < 1)
            {
                App.DisplayError("You must select at least one item to update.");
                return;
            }

            string table;
            string idColumn;
            int identity;
            if (relevantTable == RelevantTable.Organisation)
            {
                table = "Organisation";
                idColumn = Glo.Tab.ORGANISATION_ID;
                identity = 0;
            }
            else if (relevantTable == RelevantTable.Asset)
            {
                table = "Asset";
                idColumn = Glo.Tab.ASSET_ID;
                identity = 1;
            }
            else if (relevantTable == RelevantTable.Contact)
            {
                table = "Contact";
                idColumn = Glo.Tab.CONTACT_ID;
                identity = 2;
            }
            else
                return;

            var columns = ColumnRecord.GetDictionary(table, true);
            if (columns == null)
                return;

            UpdateMultiple updateMultiple = new(identity, table, columns,
                                                idColumn, dtgOutput.GetCurrentlySelectedIDs(), true);
            if (updateMultiple.ShowDialog() == true)
                Run(out _, out _, out _);
            // Error message for failed updates are displayed by the UpdateMultiple window.
        }
        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!App.DeleteConfirm(dtgOutput.dtg.SelectedItems.Count > 1))
                return;

            string table;
            string idColumn;
            int identity;
            if (relevantTable == RelevantTable.Organisation)
            {
                table = "Organisation";
                idColumn = Glo.Tab.ORGANISATION_ID;
                identity = 0;
            }
            else if (relevantTable == RelevantTable.Asset)
            {
                table = "Asset";
                idColumn = Glo.Tab.ASSET_ID;
                identity = 1;
            }
            else if (relevantTable == RelevantTable.Contact)
            {
                table = "Contact";
                idColumn = Glo.Tab.CONTACT_ID;
                identity = 2;
            }
            else
                return;

            if (App.SendDelete(table, idColumn, dtgOutput.GetCurrentlySelectedIDs(), true) &&
                MainWindow.pageDatabase != null)
            {
                MainWindow.pageDatabase.RepeatSearches(identity);
                Run(out _, out _, out _);
            }
        }

        public struct FrameContent
        {
            // Frame.Content isn't accessible immediately after assignment, so can't be called right away. Use this
            // struct to store the associated content so it can be called right away.
            public Frame frame;
            public object page;
            public FrameContent(Frame frame, object page)
            {
                this.frame = frame;
                this.page = page;
                frame.Content = page;
            }
        }
        public List<FrameContent> joins = new();
        public List<FrameContent> columns = new();
        public List<FrameContent> wheres = new();
        public List<FrameContent> orderBys = new();

        // This is used to allow the opening of relevant windows by double clicking on the SqlDataGrid when a
        // compatible query has been run, i.e. ID as the first column.
        enum RelevantTable { None, Organisation, Asset, Contact, Conference, Recurrence, Resource }
        RelevantTable relevantTable = RelevantTable.None;

        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            Run(out _, out _, out _);
        }

        public bool Run(out List<string?> columnNames, out List<string?> columnTypes, out List<List<object?>> rows)
        {
            columnNames = new();
            columnTypes = new();
            rows = new();
            if (!App.SendSelectStatement(txtStatement.Text, out columnNames, out rows, out columnTypes))
                return false;

            HashSet<int> dateCols = new();
            for (int i = 0; i < columnTypes.Count; ++i)
                if (columnTypes[i] == "Date")
                    dateCols.Add(i);

            try
            {
                relevantTable = RelevantTable.None;
                dtgOutput.Update(columnNames, rows, dateCols);
                int permissionRelevancy = -1;
                if (cmbRelevancy.Text == "Organisation")
                {
                    relevantTable = RelevantTable.Organisation;
                    permissionRelevancy = Glo.PERMISSION_RECORDS;
                }
                else if (cmbRelevancy.Text == "Asset")
                {
                    relevantTable = RelevantTable.Asset;
                    permissionRelevancy = Glo.PERMISSION_RECORDS;
                }
                else if (cmbRelevancy.Text == "Contact")
                {
                    relevantTable = RelevantTable.Contact;
                    permissionRelevancy = Glo.PERMISSION_RECORDS;
                }
                else if (cmbRelevancy.Text == "Conference")
                {
                    relevantTable = RelevantTable.Conference;
                    permissionRelevancy = Glo.PERMISSION_RECORDS;
                }
                else if (cmbRelevancy.Text == "Recurrence")
                {
                    relevantTable = RelevantTable.Recurrence;
                    permissionRelevancy = Glo.PERMISSION_RECORDS;
                }
                else if (cmbRelevancy.Text == "Resource")
                {
                    relevantTable = RelevantTable.Resource;
                    permissionRelevancy = Glo.PERMISSION_RECORDS;
                }
                btnDeleteSelected.IsEnabled = permissionRelevancy > -1 &&
                                              App.sd.deletePermissions[permissionRelevancy];
                btnUpdateSelected.IsEnabled = permissionRelevancy > -1 &&
                                              App.sd.editPermissions[permissionRelevancy];
                SetStatusBar(rows.Count, columnNames.Count);
            }
            catch (Exception e)
            {
                App.DisplayError("Unable to update SqlDataGrid. See error:\n\n" + e.Message);
                SetStatusBar();
                return false;
            }
            return true;
        }

        private void WipeCallback()
        {
            SetStatusBar();
        }

        private void SetStatusBar(params int[] vals)
        {
            if (vals.Length != 2)
            {
                lblRows.Content = "";
                lblColumns.Content = "";
                lblSelected.Content = "";
                return;
            }

            lblRows.Content = "Rows: " + vals[0].ToString();
            lblColumns.Content = "Columns: " + vals[1].ToString();

            lblRelevancy.Content = "Relevancy: " + cmbRelevancy.Text;

            dtgResults_SelectionChanged(dtgOutput, null);
        }
        // Updated the selected row count.
        private void dtgResults_SelectionChanged(object sender, RoutedEventArgs? e)
        {
            if (sender is CustomControls.SqlDataGrid sqlDataGrid && sqlDataGrid.dtg.Items.Count > 0)
            {
                lblSelected.Content = "Selected: " + sqlDataGrid.dtg.SelectedItems.Count;
                lblSelected.FontWeight = sqlDataGrid.dtg.SelectedItems.Count > 0 ? FontWeights.SemiBold :
                                                                                   FontWeights.Normal;
            }
        }

        private void dtgOutput_CustomDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (relevantTable == RelevantTable.None)
                return;

            if (relevantTable == RelevantTable.Organisation)
                App.EditOrganisation(dtgOutput.GetCurrentlySelectedID());
            else if (relevantTable == RelevantTable.Asset)
                App.EditAsset(dtgOutput.GetCurrentlySelectedID());
            else if (relevantTable == RelevantTable.Contact)
                App.EditContact(dtgOutput.GetCurrentlySelectedID());
            else if (relevantTable == RelevantTable.Conference)
            {
                int id;
                if (!int.TryParse(dtgOutput.GetCurrentlySelectedID(), out id))
                    return;
                App.EditConference(id);
            }
            else if (relevantTable == RelevantTable.Recurrence)
            {
                int id;
                if (!int.TryParse(dtgOutput.GetCurrentlySelectedID(), out id))
                    return;
                EditRecurrence editRec = new(id);
                editRec.Show();
            }
            else if (relevantTable == RelevantTable.Resource)
                App.EditResource(dtgOutput.GetCurrentlySelectedID());
        }
    }
}
