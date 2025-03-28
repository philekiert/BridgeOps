﻿using DocumentFormat.OpenXml.Drawing;
using SendReceiveClasses;
using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
using System.Xml.Linq;
using static BridgeOpsClient.CustomControls.SqlDataGrid;

namespace BridgeOpsClient
{
    public partial class PageDatabaseView : Page
    {
        PageDatabase containingPage;
        Frame containingFrame;

        // Values need to be stored for multi-field searches.
        List<string> fieldValues = new();

        Separator sepConf;
        MenuItem btnCancel;
        MenuItem btnAdjustTime;
        MenuItem btnAdjustConns;
        MenuItem btnSetHost;
        MenuItem btnGoTo;
        Separator sepRec;
        MenuItem btnConfCreateRecurrence;
        MenuItem btnConfAddToRecurrence;
        MenuItem btnConfRemoveFromRecurrence;
        MenuItem btnConfEditRecurrence;

        MenuItem btnUpdate;
        MenuItem btnDelete;

        Separator sepTask;
        MenuItem btnAddVisits;
        MenuItem btnAddDocument;

        public PageDatabaseView(PageDatabase containingPage, Frame containingFrame)
        {
            InitializeComponent();

            this.containingPage = containingPage;
            this.containingFrame = containingFrame;

            // This will trigger cmbTableSelectionchanged(), which will PopulateColumnComboBox().
            cmbTable.SelectedIndex = 0;

            dtgResults.canHideColumns = true;
            dtgResults.CustomDoubleClick += dtg_DoubleClick;
            dtgResults.EnableMultiSelect();

            txtSearch.Focus();

            dtgResults.AddWipeButton();
            dtgResults.WipeCallback = WipeCallback;

            btnUpdate = dtgResults.AddContextMenuItem("Update Selected", false, btnUpdate_Click);
            btnDelete = dtgResults.AddContextMenuItem("Delete Selected", false, btnDelete_Click);
            sepConf = dtgResults.AddSeparator(false);
            btnCancel = dtgResults.AddContextMenuItem("Cancel", false, btnCancel_Click);
            btnAdjustTime = dtgResults.AddContextMenuItem("Adjust Time", false, btnAdjustTime_Click);
            btnAdjustConns = dtgResults.AddContextMenuItem("Adjust Connections", false, btnAdjustConns_Click);
            btnSetHost = dtgResults.AddContextMenuItem("Set Host", false, btnSetHost_Click);
            btnGoTo = dtgResults.AddContextMenuItem("Go To", false, btnGoTo_Click);
            sepRec = dtgResults.AddSeparator(false);
            btnConfAddToRecurrence = dtgResults.AddContextMenuItem("Add To Recurrence",
                                                                   false, btnConfAddToRecurrence_Click);
            btnConfCreateRecurrence = dtgResults.AddContextMenuItem("Create Recurrence",
                                                                    false, btnConfCreateRecurrence_Click);
            btnConfRemoveFromRecurrence = dtgResults.AddContextMenuItem("Remove From Recurrence",
                                                                        false, btnConfRemoveFromRecurrence_Click);
            btnConfEditRecurrence = dtgResults.AddContextMenuItem("View Recurrence",
                                                                  false, btnConfEditRecurrence_Click);
            sepTask = dtgResults.AddSeparator(false);
            btnAddVisits = dtgResults.AddContextMenuItem("Add Visit", false, btnAddVisit_Click);
            btnAddDocument = dtgResults.AddContextMenuItem("Add Document", false, btnAddDocument_Click);

            for (int n = 0; n < 7; ++n)
            {
                DayLabel(n).MouseDown += DayMouseDown;
                DayBorder(n).MouseDown += DayMouseDown;
                daysOn[n] = true;
            }
            ResetDays();
        }

        public void EnforcePermissions()
        {
            // Baseline
            btnUpdate.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_RECORDS];
            btnDelete.IsEnabled = App.sd.deletePermissions[Glo.PERMISSION_RECORDS];
            btnConfAddToRecurrence.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_CONFERENCES];
            btnConfRemoveFromRecurrence.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_CONFERENCES];
            btnConfCreateRecurrence.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_CONFERENCES] &&
                                                App.sd.createPermissions[Glo.PERMISSION_CONFERENCES];
            btnAddVisits.IsEnabled = App.sd.createPermissions[Glo.PERMISSION_TASKS];
            btnAddDocument.IsEnabled = App.sd.createPermissions[Glo.PERMISSION_TASKS];

            // Special Cases
            if (dtgResults.identity == -1)
            {
                btnUpdate.IsEnabled = false;
                btnDelete.IsEnabled = false;
            }
            else if (dtgResults.identity == 7 || dtgResults.identity == 8)
            {
                btnUpdate.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_CONFERENCES];
                btnDelete.IsEnabled = App.sd.deletePermissions[Glo.PERMISSION_CONFERENCES];
            }
            else if (dtgResults.identity == 9)
            {
                btnUpdate.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_RESOURCES];
                btnDelete.IsEnabled = App.sd.deletePermissions[Glo.PERMISSION_RESOURCES];
            }
            else if (dtgResults.identity >= (int)UserSettings.TableIndex.Task)
            {
                btnUpdate.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_TASKS];
                btnDelete.IsEnabled = App.sd.deletePermissions[Glo.PERMISSION_TASKS];
            }
        }

        public RowDefinition GetRowDefinitionForFrame()
        {
            return containingPage.grdPanes.RowDefinitions[Grid.GetRow(containingFrame)];
        }

        private void WipeCallback()
        {
            SetStatusBar();
            ResetDays();
        }

        public void PopulateColumnComboBox()
        {
            OrderedDictionary table;
            if (cmbTable.SelectedIndex == 0)
                table = ColumnRecord.orderedOrganisation;
            else if (cmbTable.SelectedIndex == 1)
                table = ColumnRecord.orderedAsset;
            else if (cmbTable.SelectedIndex == 2)
                table = ColumnRecord.orderedContact;
            else if (cmbTable.SelectedIndex == 3)
                table = ColumnRecord.orderedConference;
            else if (cmbTable.SelectedIndex == 4)
                table = ColumnRecord.recurrence;
            else if (cmbTable.SelectedIndex == 5)
                table = ColumnRecord.resource;
            // 6 is the seperator.
            else if (cmbTable.SelectedIndex == 7)
                table = ColumnRecord.task;
            else if (cmbTable.SelectedIndex == 8)
                table = ColumnRecord.visit;
            else if (cmbTable.SelectedIndex == 9)
                table = ColumnRecord.document;
            else
            {
                App.Abort("Unable to populate column list.", App.mainWindow);
                return;
            }

            cmbColumn.Items.Clear();
            fieldValues.Clear();

            btnClear.IsEnabled = false;

            // Add a few extras for conferences, handled separately in the search functions.
            if (cmbTable.SelectedIndex == 3)
            {
                cmbColumn.Items.Add(new ComboBoxItem()
                { Content = ColumnRecord.GetPrintName(Glo.Tab.DIAL_NO, ColumnRecord.organisation) });
                cmbColumn.Items.Add(new ComboBoxItem()
                { Content = ColumnRecord.GetPrintName(Glo.Tab.ORGANISATION_REF, ColumnRecord.organisation) });
                cmbColumn.Items.Add(new ComboBoxItem()
                { Content = ColumnRecord.GetPrintName(Glo.Tab.ORGANISATION_NAME, ColumnRecord.organisation) });
                cmbColumn.Items.Add(new ComboBoxItem()
                { Content = ColumnRecord.GetPrintName(Glo.Tab.CONFERENCE_ID, ColumnRecord.conference) });
                fieldValues.AddRange(new string[] { "", "", "", "" });
            }
            // Same for recurrences.
            if (cmbTable.SelectedIndex == 4)
            {
                cmbColumn.Items.Add(new ComboBoxItem()
                { Content = ColumnRecord.GetPrintName(Glo.Tab.DIAL_NO, ColumnRecord.organisation) });
                cmbColumn.Items.Add(new ComboBoxItem()
                { Content = ColumnRecord.GetPrintName(Glo.Tab.ORGANISATION_REF, ColumnRecord.organisation) });
                cmbColumn.Items.Add(new ComboBoxItem()
                { Content = ColumnRecord.GetPrintName(Glo.Tab.ORGANISATION_NAME, ColumnRecord.organisation) });
                cmbColumn.Items.Add(new ComboBoxItem()
                { Content = ColumnRecord.GetPrintName(Glo.Tab.CONFERENCE_ID, ColumnRecord.conference) });
                cmbColumn.Items.Add(new ComboBoxItem()
                { Content = ColumnRecord.GetPrintName(Glo.Tab.CONFERENCE_TITLE, ColumnRecord.conference) });
                cmbColumn.Items.Add(new ComboBoxItem()
                { Content = ColumnRecord.GetPrintName(Glo.Tab.RECURRENCE_ID, ColumnRecord.recurrence) });
                fieldValues.AddRange(new string[] { "", "", "", "", "", "" });
            }


            foreach (DictionaryEntry de in table)
            {
                // Anything that's TEXT type. Could add date and int search in the future, but it's just not urgent
                // as you can just sort by date or int in the results.
                if (ColumnRecord.IsTypeString((ColumnRecord.Column)de.Value!))
                {
                    ComboBoxItem item = new ComboBoxItem();
                    item.Content = ColumnRecord.GetPrintName(de);
                    cmbColumn.Items.Add(item);
                    fieldValues.Add("");
                }
            }

            cmbColumn.SelectedIndex = 0;

            txtSearch.IsEnabled = cmbColumn.Items.Count > 0;
            btnSearch.IsEnabled = cmbColumn.Items.Count > 0;
            txtSearch.Text = "";
        }

        struct Row
        {
            public List<object?> items { get; set; }
            public Row(List<object?> items)
            {
                this.items = items;
            }
        }

        // Last search variables for updating the SqlDataGrid when a change is made by the user.
        bool lastSearchWide = false;
        bool lastSearchHistorical = false;
        List<string> lastSearchColumns = new();
        List<string> lastSearchValues = new();
        List<Conditional> lastSearchConditionals = new();
        string lastWideValue = "";
        OrderedDictionary lastColumnDefinitions = new();
        public void RepeatSearch(int identity)
        {
            if (identity != dtgResults.identity)
                return;

            string table = "Organisation";
            if (dtgResults.identity == 1)
                table = "Asset";
            else if (dtgResults.identity == 2)
                table = "Contact";
            else if (dtgResults.identity == 7)
                table = "Conference";
            else if (dtgResults.identity == 8)
                table = "Recurrence";
            else if (dtgResults.identity == 9)
                table = "Resource";
            else if (dtgResults.identity == (int)UserSettings.TableIndex.Task)
                table = "Task";
            else if (dtgResults.identity == (int)UserSettings.TableIndex.Visit)
                table = "Visit";
            else if (dtgResults.identity == (int)UserSettings.TableIndex.Document)
                table = "Document";

            if (dtgResults.identity == 7 && conferenceSelectRequest != null)
            {
                List<string?> colNames = new();
                List<List<object?>> rows = new();
                if (App.SendSelectRequest((SelectRequest)conferenceSelectRequest, out colNames, out rows,
                                          App.mainWindow))
                    PopulateConferencesFromSelect(rows);
                else
                    SetStatusBar();
            }
            else if (dtgResults.identity == 8 && recurrenceSelectRequest != null)
            {
                List<string?> colNames = new();
                List<List<object?>> rows = new();
                if (App.SendSelectRequest((SelectRequest)recurrenceSelectRequest, out colNames, out rows,
                                          App.mainWindow))
                    PopulateRecurrencesFromSelect(colNames, rows);
                else
                    SetStatusBar();
            }
            else if (dtgResults.identity != -1)
            {
                List<string?> columnNames;
                List<List<object?>> rows;
                if (lastSearchWide && App.SelectWide(table, lastWideValue,
                                                     out columnNames, out rows, lastSearchHistorical, App.mainWindow))
                {
                    ResetDays();
                    dtgResults.Update(lastColumnDefinitions, columnNames, rows);
                    SetStatusBar(rows.Count, columnNames.Count, -1, lastSearchHistorical ? 1 : -1);
                }
                else if (App.Select(cmbTable.Text,
                                    new List<string> { "*" },
                                    lastSearchColumns, lastSearchValues, lastSearchConditionals,
                                    out columnNames, out rows, true, lastSearchHistorical, App.mainWindow))
                {
                    ResetDays();
                    dtgResults.Update(lastColumnDefinitions, columnNames, rows);
                    SetStatusBar(rows.Count, columnNames.Count, lastSearchColumns.Count,
                                 lastSearchHistorical ? 1 : -1);
                }
                else
                    SetStatusBar();

            }
        }

        int lastConferenceSearchSelectCount = 0;
        SelectRequest? conferenceSelectRequest = null;
        int lastRecurrenceSearchSelectCount = 0;
        SelectRequest? recurrenceSelectRequest = null;

        bool daySelect = false;
        bool daySwitch = false;
        int dayCurrentlyOver = -1;
        bool[] daysPresent = new bool[7];
        bool[] daysOn = new bool[7];
        private Label DayLabel(int day)
        {
            switch (day)
            {
                case 0: return mon;
                case 1: return tue;
                case 2: return wed;
                case 3: return thu;
                case 4: return fri;
                case 5: return sat;
                default: return sun;
            }
        }
        private Border DayBorder(int day)
        {
            switch (day)
            {
                case 0: return monPr;
                case 1: return tuePr;
                case 2: return wedPr;
                case 3: return thuPr;
                case 4: return friPr;
                case 5: return satPr;
                default: return sunPr;
            }
        }
        private int Day()
        {
            int x = (int)Mouse.GetPosition(mon).X;
            x = x / 36; // Button width is 37, but -1 margin.
            if (x < 0 || x > 7)
                return -1;
            if (x == 7) // Extended slighly further right.
                return 6;
            return x;
        }
        private void DayMouseDown(object sender, MouseEventArgs e)
        {
            daySelect = true;
            int day = Day();
            Label l = DayLabel(day);
            daySwitch = !daysOn[day];
            DayMouseMove(sender, e);
        }
        public void DayMouseMove(object? sender, MouseEventArgs? e)
        {
            if (!daySelect)
                return;

            if (Mouse.LeftButton == MouseButtonState.Released)
            {
                daySelect = false;
                return;
            }

            int day = Day();
            if (day == -1)
            {
                dayCurrentlyOver = -1;
                return;
            }

            if (daysOn[day] != daySwitch)
            {
                dayCurrentlyOver = day;
                Label l = DayLabel(day);
                if (daySwitch)
                    daysOn[day] = true;
                else
                    daysOn[day] = false;
                RefreshDayColours();
            }
        }
        private void ResetDays()
        {
            for (int n = 0; n < 7; ++n)
                daysPresent[n] = false;
            RefreshDayColours();
        }
        private void RefreshDayColours()
        {
            for (int i = 0; i < 7; ++i)
            {
                Label l = DayLabel(i);
                if (daysOn[i])
                    l.Style = (Style)l.FindResource("dayOn");
                else
                    l.Style = (Style)l.FindResource("day");
                Border b = DayBorder(i);
                if (daysPresent[i])
                    b.Style = (Style)l.FindResource("dayPresent");
                else
                    b.Style = (Style)l.FindResource("dayAbsent");
            }

        }

        private void cmbTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Show/hide the conference dates panel
            grdDates.Visibility = cmbTable.SelectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
            if (cmbTable.SelectedIndex < 2)
                cmbSearchType.ItemsSource = new List<string>() { "Search All", "Search Columns",
                                                                 "Search All (historical)",
                                                                 "Search Columns (historical)" };
            else
                cmbSearchType.ItemsSource = new List<string>() { "Search All", "Search Columns" };
            if (cmbSearchType.SelectedIndex == -1)
                cmbSearchType.SelectedIndex = 0;

            PopulateColumnComboBox();
        }

        private void btnSearch_Click(object? sender, RoutedEventArgs? e)
        {
            // Narrow Search
            if (cmbSearchType.SelectedIndex % 2 == 1)
                Search(cmbTable.SelectedIndex);
            // Wide Search
            else
            {
                int identity = cmbTable.SelectedIndex;
                // This is because some identities aren't used in the table select and only for column layouts.
                if (identity > 2)
                    identity += 4; // Nudge past the organisation and settings identities.
                // No need to nudge further past the recurrence conference list identity, since the separator adds one anyway.

                // SelectedIndex of 2 is a historical wide search (historical narrow is not currently possible).
                WideSearch(identity, cmbSearchType.SelectedIndex == 2);
            }
        }
        private void Search(int identity)
        {
            OrderedDictionary tableColDefs;
            Dictionary<string, string> nameReversals;
            if (cmbTable.Text == "Organisation")
            {
                tableColDefs = ColumnRecord.organisation;
                nameReversals = ColumnRecord.organisationFriendlyNameReversal;
            }
            else if (cmbTable.Text == "Asset")
            {
                identity = 1;
                tableColDefs = ColumnRecord.asset;
                nameReversals = ColumnRecord.assetFriendlyNameReversal;
            }
            else if (cmbTable.Text == "Contact")
            {
                identity = 2;
                tableColDefs = ColumnRecord.contact;
                nameReversals = ColumnRecord.contactFriendlyNameReversal;
            }
            else if (cmbTable.Text == "Conference")
            {
                // Special case further down.
                identity = 7;
                // Not used, but set to keep the Visual Studio happy with assignments.
                tableColDefs = ColumnRecord.conference;
                nameReversals = ColumnRecord.conferenceFriendlyNameReversal;
            }
            else if (cmbTable.Text == "Recurrence")
            {
                // Special case further down.
                identity = 8;
                // Not used, but set to keep the Visual Studio happy with assignments.
                tableColDefs = ColumnRecord.recurrence;
                nameReversals = ColumnRecord.conferenceRecurrenceFriendlyNameReversal;
            }
            else if (cmbTable.Text == "Resource")
            {
                identity = 9;
                tableColDefs = ColumnRecord.resource;
                nameReversals = ColumnRecord.resourceFriendlyNameReversal;
            }
            else if (cmbTable.Text == "Task")
            {
                identity = (int)UserSettings.TableIndex.Task;
                tableColDefs = ColumnRecord.task;
                nameReversals = ColumnRecord.taskFriendlyNameReversal;
            }
            else if (cmbTable.Text == "Visit")
            {
                identity = (int)UserSettings.TableIndex.Visit;
                tableColDefs = ColumnRecord.visit;
                nameReversals = ColumnRecord.visitFriendlyNameReversal;
            }
            else // if Document
            {
                identity = (int)UserSettings.TableIndex.Document;
                tableColDefs = ColumnRecord.document;
                nameReversals = ColumnRecord.documentFriendlyNameReversal;
            }

            if (identity != 7 && identity != 8) // Conference and recurrence searches are a tad more complicated.
            {
                List<string> selectColumns = new();
                List<string> selectValues = new();
                for (int n = 0; n < fieldValues.Count; ++n)
                {
                    if (fieldValues[n] != "")
                    {
                        // Should only trigger on no selection.
                        string colName = (string)((ComboBoxItem)cmbColumn.Items[n]).Content;
                        if (!nameReversals.ContainsKey(colName))
                        {
                            App.DisplayError("Searched column is not legal.", App.mainWindow);
                            return;
                        }

                        selectColumns.Add(nameReversals[colName]);
                        selectValues.Add(fieldValues[n]);
                    }
                }

                List<Conditional> conditionals = new();
                for (int i = 0; i < selectColumns.Count; ++i)
                    conditionals.Add(Conditional.Like);

                bool historical = cmbSearchType.SelectedIndex > 1;

                // Error message is displayed by App.SelectAll() if something goes wrong.
                List<string?> columnNames;
                List<List<object?>> rows;
                if (App.Select(cmbTable.Text, // Needs changing in RepeatSearch() as well if adjusted.
                               new List<string> { "*" },
                               selectColumns, selectValues, conditionals,
                               out columnNames, out rows, true, historical, App.mainWindow))
                {
                    lastSearchWide = false;
                    lastSearchColumns = selectColumns;
                    lastSearchValues = selectValues;
                    lastSearchConditionals = conditionals;
                    lastColumnDefinitions = tableColDefs;

                    lastSearchHistorical = historical;
                    dtgResults.identity = identity;
                    ResetDays();
                    dtgResults.Update(tableColDefs, columnNames, rows);

                    SetStatusBar(rows.Count, columnNames.Count, selectColumns.Count, lastSearchHistorical ? 1 : -1);
                }
                else
                    SetStatusBar();
            }

            // Special case if conference or recurrence search:
            else
            {
                try
                {
                    if (identity == 7)
                        SearchConferences(false);
                    else if (identity == 8)
                        SearchRecurrences(false);
                }
                catch
                {
                    App.DisplayError("Something went wrong. Try reloading the application and searching again.",
                                     App.mainWindow);
                }
            }
        }

        private void SearchConferences(bool wide)
        {
            // Although we're technically starting with the connection table, we need to use the column reversal for
            // organisations as the Dial No friendly name isn't stored for connections in the column record.
            string prefix = "Organisation.";
            Dictionary<string, string> nameReversals = ColumnRecord.organisationFriendlyNameReversal;
            List<string> selectColumns = new();
            List<string?> selectValues = new();
            List<string> operators = new();
            List<bool> needsQuotes = new();
            List<string> andOrs = new();

            for (int n = 0; n < fieldValues.Count; ++n)
            {
                if (n == 1)
                {
                    prefix = "Organisation.";
                    nameReversals = ColumnRecord.organisationFriendlyNameReversal;
                }
                if (n == 3)
                {
                    prefix = "Conference.";
                    nameReversals = ColumnRecord.conferenceFriendlyNameReversal;
                }

                if (wide || fieldValues[n] != "")
                {
                    // Should only trigger on no selection.
                    string colName = (string)((ComboBoxItem)cmbColumn.Items[n]).Content;

                    selectColumns.Add(prefix + nameReversals[colName]);
                    selectValues.Add("%" + (wide ? txtSearch.Text : fieldValues[n]) + "%");
                    operators.Add("LIKE");
                    needsQuotes.Add(true);
                    andOrs.Add(wide ? "OR" : "AND");
                }
            }

            if (chkConfFromTo.IsChecked == true)
            {
                if (datFrom.SelectedDate == null || datTo.SelectedDate == null ||
                    datTo.SelectedDate < datFrom.SelectedDate)
                {
                    App.DisplayError("Must select a start and end date when the box is checked." +
                                     "\n\nThe end date must be prior to the start date.", App.mainWindow);
                    return;
                }
                selectColumns.Add("Conference." + Glo.Tab.CONFERENCE_START);
                selectColumns.Add("Conference." + Glo.Tab.CONFERENCE_START); // NB: This is correct :)
                selectValues.Add(SqlAssist.DateTimeToSQL((DateTime)datFrom.SelectedDate));
                selectValues.Add(SqlAssist.DateTimeToSQL(((DateTime)datTo.SelectedDate).AddDays(1)));
                operators.Add(">=");
                operators.Add("<");
                needsQuotes.Add(true);
                needsQuotes.Add(true);
                andOrs.Add("AND");
                andOrs.Add("AND");
            }

            if (andOrs.Count > 0)
                andOrs.RemoveAt(0); // This needs to be one fewer than the rest.

            SelectRequest req = new(App.sd.sessionID, ColumnRecord.columnRecordID, "Conference", true,
                                    new() { "Connection", "Organisation" },
                                    new() { "Connection." + Glo.Tab.CONFERENCE_ID,
                                            "Organisation." + Glo.Tab.DIAL_NO},
                                    new() { "Conference." + Glo.Tab.CONFERENCE_ID,
                                            "Connection." + Glo.Tab.DIAL_NO},
                                    new() { "LEFT", "LEFT" },
                                    new() { "Conference." + Glo.Tab.CONFERENCE_ID }, new() { "" },
                                    selectColumns, operators, selectValues, needsQuotes, new(), new(), andOrs,
                                    new(), new());
            req.autoConfIdPrefix = wide || selectColumns.Contains("Conference." + Glo.Tab.CONFERENCE_ID);

            List<string?> colNames = new();
            List<List<object?>> rows = new();
            if (App.SendSelectRequest(req, out colNames, out rows, App.mainWindow))
            {
                lastConferenceSearchSelectCount = selectColumns.Count;
                lastSearchWide = true;
                PopulateConferencesFromSelect(rows);
                conferenceSelectRequest = req;
            }
            else
                SetStatusBar();
        }
        private void PopulateConferencesFromSelect(List<List<object?>> rows)
        {
            List<string> ids = new();
            foreach (List<object?> row in rows)
                ids.Add(((int)row[0]!).ToString());
            List<Conference> conferences;

            if (!App.SendConferenceSelectRequest(ids, out conferences, App.mainWindow))
            {
                App.DisplayError("Could not complete conference search, please try again.", App.mainWindow);
                btnClear_Click(null, null); // Clears the status bars automatically.
                return;
            }

            List<string?> colNames = new()
            {
                Glo.Tab.CONFERENCE_ID,
                ColumnRecord.GetPrintName(Glo.Tab.CONFERENCE_TITLE, ColumnRecord.conference),
                "Recurrence",
                "Day",
                "Start",
                "End",
                "Duration",
                "Host No",
                "Host Ref",
                "Connections",
                "Closure",
                "Cancelled",
                "Test",
                "Resource",
                ColumnRecord.GetPrintName(Glo.Tab.NOTES, ColumnRecord.conference)
            };
            List<List<object?>> data = new();
            DateTime start;
            DateTime end;

            // Add user-added columns.
            if (conferences.Count > 0)
                colNames.AddRange(conferences[0].additionalCols);

            bool[] newDaysPresent = new bool[7];

            foreach (Conference c in conferences)
            {
                bool test = false;
                foreach (Conference.Connection n in c.connections)
                    if (n.isTest)
                    {
                        test = true;
                        break;
                    }

                start = (DateTime)c.start!;
                end = (DateTime)c.end!;

                newDaysPresent[(int)start.DayOfWeek - 1] = true;
                if (!daysOn[(int)start.DayOfWeek - 1])
                    continue;
                List<object?> newRow = new()
                {
                    c.conferenceID,
                                 c.title,
                                 c.recurrenceID == null ? null :
                                    (c.recurrenceName == null ? ("R-" + c.recurrenceID.ToString()) :
                                        $"{c.recurrenceName} (R-{c.recurrenceID})"),
                                 start.DayOfWeek.ToString(),
                                 c.start,
                                 c.end,
                                 c.end - c.start,
                                 c.connections.Count == 0 ? null : c.connections[0].dialNo,
                                 c.connections.Count == 0 ? null : c.connections[0].orgReference,
                                 c.connections.Count,
                                 c.closure,
                                 c.cancelled,
                                 test,
                                 c.resourceName == null ? (c.resourceRow + 1).ToString() :
                                                          $"{c.resourceName} {c.resourceRow + 1}",
                                 c.notes
                };
                // Add user-added columns.
                newRow.AddRange(c.additionalValObjects);

                data.Add(newRow);
            }

            lastSearchHistorical = cmbSearchType.SelectedIndex > 1;
            dtgResults.identity = 7;
            ResetDays();
            dtgResults.Update(colNames, data);
            daysPresent = newDaysPresent;
            RefreshDayColours();

            if (lastSearchWide)
                SetStatusBar(rows.Count, lastConferenceSearchSelectCount, -1, lastSearchHistorical ? 1 : -1);
            else
                SetStatusBar(rows.Count, colNames.Count, lastConferenceSearchSelectCount,
                             lastSearchHistorical ? 1 : -1);
        }

        private void SearchRecurrences(bool wide)
        {
            Dictionary<string, string> nameReversals = ColumnRecord.organisationFriendlyNameReversal;
            List<string> selectColumns = new();
            List<string?> selectValues = new();
            List<string> operators = new();
            List<bool> needsQuotes = new();
            List<string> andOrs = new();
            string prefix = "Organisation.";

            for (int n = 0; n < fieldValues.Count; ++n)
            {
                if (n == 3)
                {
                    prefix = "Conference.";
                    nameReversals = ColumnRecord.conferenceFriendlyNameReversal;
                }
                if (n == 5)
                {
                    prefix = "Recurrence.";
                    nameReversals = ColumnRecord.conferenceRecurrenceFriendlyNameReversal;
                }

                if (wide || fieldValues[n] != "")
                {
                    // Should only trigger on no selection.
                    string colName = (string)((ComboBoxItem)cmbColumn.Items[n]).Content;

                    selectColumns.Add(prefix + nameReversals[colName]);
                    selectValues.Add("%" + (wide ? txtSearch.Text : fieldValues[n]) + "%");
                    operators.Add("LIKE");
                    needsQuotes.Add(true);
                    andOrs.Add(wide ? "OR" : "AND");
                }
            }

            if (andOrs.Count > 0)
                andOrs.RemoveAt(0); // This needs to be one fewer than the rest.

            SelectRequest req = new(App.sd.sessionID, ColumnRecord.columnRecordID, "Recurrence", true,
                                    new() { "Conference", "Connection", "Organisation" },
                                    new() { "Conference." + Glo.Tab.RECURRENCE_ID,
                                            "Connection." + Glo.Tab.CONFERENCE_ID,
                                            "Organisation." + Glo.Tab.DIAL_NO },
                                    new() { "Recurrence." + Glo.Tab.RECURRENCE_ID,
                                            "Conference." + Glo.Tab.CONFERENCE_ID,
                                            "Connection." + Glo.Tab.DIAL_NO },
                                    new() { "LEFT", "LEFT", "LEFT" },
                                    new() { "Recurrence." + Glo.Tab.RECURRENCE_ID,
                                            "Recurrence." + Glo.Tab.RECURRENCE_NAME,
                                            "Recurrence." + Glo.Tab.NOTES }, new() { "", "", "" },
                                    selectColumns, operators, selectValues, needsQuotes, new(), new(), andOrs,
                                    new(), new());
            req.autoConfIdPrefix = wide || selectColumns.Contains("Conference." + Glo.Tab.CONFERENCE_ID);
            req.autoRecIdPrefix = wide || selectColumns.Contains("Recurrence." + Glo.Tab.RECURRENCE_ID);

            List<string?> colNames = new();
            List<List<object?>> rows = new();
            if (App.SendSelectRequest(req, out colNames, out rows, App.mainWindow))
            {
                lastRecurrenceSearchSelectCount = selectColumns.Count;
                lastSearchWide = true;
                PopulateRecurrencesFromSelect(colNames, rows);
                recurrenceSelectRequest = req;
            }
            else
                SetStatusBar();
        }
        private void PopulateRecurrencesFromSelect(List<string?> colNames, List<List<object?>> rows)
        {
            lastSearchHistorical = cmbSearchType.SelectedIndex > 1;
            dtgResults.identity = 8;
            ResetDays();
            dtgResults.Update(colNames, rows);

            if (lastSearchWide)
                SetStatusBar(rows.Count, lastConferenceSearchSelectCount, -1, lastSearchHistorical ? 1 : -1);
            else
                SetStatusBar(rows.Count, colNames.Count, lastConferenceSearchSelectCount,
                             lastSearchHistorical ? 1 : -1);
        }

        // Wide search on either enter or click.
        private void WideSearch(int identity, bool historical)
        {
            OrderedDictionary tableColDefs;
            Dictionary<string, string> nameReversals;
            if (identity == 0) // Organisation
            {
                tableColDefs = ColumnRecord.organisation;
                nameReversals = ColumnRecord.organisationFriendlyNameReversal;
            }
            else if (identity == 1) // Asset
            {
                tableColDefs = ColumnRecord.asset;
                nameReversals = ColumnRecord.assetFriendlyNameReversal;
            }
            else if (identity == 2) // Contact
            {
                tableColDefs = ColumnRecord.contact;
                nameReversals = ColumnRecord.contactFriendlyNameReversal;
            }
            else if (identity == 7) // Conference
            {
                tableColDefs = ColumnRecord.conference;
                nameReversals = ColumnRecord.conferenceRecurrenceFriendlyNameReversal;
            }
            else if (identity == 8) // Recurrence
            {
                tableColDefs = ColumnRecord.recurrence;
                nameReversals = ColumnRecord.conferenceRecurrenceFriendlyNameReversal;
            }
            else if (identity == 9) // Recurrence
            {
                tableColDefs = ColumnRecord.resource;
                nameReversals = ColumnRecord.resourceFriendlyNameReversal;
            }
            else if (identity == (int)UserSettings.TableIndex.Task)
            {
                tableColDefs = ColumnRecord.task;
                nameReversals = ColumnRecord.taskFriendlyNameReversal;
            }
            else if (identity == (int)UserSettings.TableIndex.Visit)
            {
                tableColDefs = ColumnRecord.visit;
                nameReversals = ColumnRecord.visitFriendlyNameReversal;
            }
            else // if Document
            {
                tableColDefs = ColumnRecord.document;
                nameReversals = ColumnRecord.documentFriendlyNameReversal;
            }

            if (identity != 7 && identity != 8) // Conference and recurrence searches are a tad more complicated.
            {
                // Error message is displayed by App.SelectAll() if something goes wrong.
                List<string?> columnNames;
                List<List<object?>> rows;
                if (App.SelectWide(cmbTable.Text, txtSearch.Text, // Also needs changing in RepeatSearch() if adjusted.
                                   out columnNames, out rows, historical, App.mainWindow))
                {
                    lastSearchWide = true;
                    lastWideValue = txtSearch.Text;
                    lastColumnDefinitions = tableColDefs;

                    lastSearchHistorical = historical;
                    dtgResults.identity = identity;
                    ResetDays();
                    dtgResults.Update(tableColDefs, columnNames, rows);

                    SetStatusBar(rows.Count, columnNames.Count, -1, lastSearchHistorical ? 1 : -1);
                }
                else
                    SetStatusBar();
            }
            else
            {
                if (identity == 7)
                    SearchConferences(true);
                else if (identity == 8)
                    SearchRecurrences(true);
            }
        }
        private void txtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                btnSearch_Click(null, null);
        }

        private void SetStatusBar(params int[] vals)
        {
            if (vals.Length != 4)
            {
                lblRows.Content = "";
                lblColumns.Content = "";
                lblColumnsSearched.Content = "";
                lblSelected.Content = "";
                lblTable.Content = "";
                return;
            }

            // Set all labels to updated values.
            lblRows.Content = "Rows: " + vals[0].ToString();
            lblColumns.Content = "Columns: " + vals[1].ToString();
            bool historical = vals[3] == 1;
            string searched = vals[2] == -1 ? (historical ? "Historical wide search" : "Wide search") :
                                              ((historical ? "Historical f" : "F") + $"ields searched: {vals[2]}");
            lblColumnsSearched.Content = searched;
            dtgResults_SelectionChanged(dtgResults, null);

            string tableSearched = "Organisations";
            if (dtgResults.identity == 1)
                tableSearched = "Assets";
            else if (dtgResults.identity == 2)
                tableSearched = "Contacts";
            else if (dtgResults.identity == 7)
                tableSearched = "Conferences";
            else if (dtgResults.identity == 8)
                tableSearched = "Recurrences";
            else if (dtgResults.identity == 9)
                tableSearched = "Resources";
            else if (dtgResults.identity == (int)UserSettings.TableIndex.Task)
                tableSearched = "Task";
            else if (dtgResults.identity == (int)UserSettings.TableIndex.Visit)
                tableSearched = "Visit";
            else if (dtgResults.identity == (int)UserSettings.TableIndex.Document)
                tableSearched = "Document";
            lblTable.Content = tableSearched;

            // Highlight searches where more than one field was searched in case the user was not aware.
            lblColumnsSearched.FontWeight = vals[2] > 1 ? FontWeights.SemiBold : FontWeights.Normal;
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

        // Highlight fields with values, and reload those values when selecting fields.
        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            fieldValues[cmbColumn.SelectedIndex] = txtSearch.Text;
            ((ComboBoxItem)cmbColumn.Items[cmbColumn.SelectedIndex]).FontWeight = txtSearch.Text == "" ?
                                                                                  FontWeights.Normal :
                                                                                  FontWeights.Bold;

            // Only enabled btnClear when there's actually text to clear.
            for (int n = 0; n < fieldValues.Count; ++n)
                if (fieldValues[n] != "")
                {
                    btnClear.IsEnabled = true;
                    return;
                }
            btnClear.IsEnabled = false;
        }
        private void cmbColumn_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbColumn.SelectedIndex != -1 && fieldValues.Count > 0)
                txtSearch.Text = fieldValues[cmbColumn.SelectedIndex];
        }

        // Wipe all stored field search strings.
        private void btnClear_Click(object? sender, RoutedEventArgs? e)
        {
            for (int n = 0; n < fieldValues.Count; ++n)
            {
                if (fieldValues[n] != "")
                {
                    fieldValues[n] = "";
                    ((ComboBoxItem)cmbColumn.Items[n]).FontWeight = FontWeights.Normal;
                }
            }

            txtSearch.Text = "";

            btnClear.IsEnabled = false;
        }

        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (dtgResults.dtg.SelectedItems.Count < 1)
            {
                App.DisplayError("You must select at least one item to update.", App.mainWindow);
                return;
            }
            
            string table = "Organisation";
            string idColumn = Glo.Tab.ORGANISATION_ID;
            bool needsQuotes = false; ;
            if (dtgResults.identity == 1)
            {
                needsQuotes = false;
                table = "Asset";
                idColumn = Glo.Tab.ASSET_ID;
            }
            else if (dtgResults.identity == 2)
            {
                needsQuotes = false;
                table = "Contact";
                idColumn = Glo.Tab.CONTACT_ID;
            }
            else if (dtgResults.identity == 7)
            {
                needsQuotes = false;
                table = "Conference";
                idColumn = Glo.Tab.CONFERENCE_ID;
            }
            else if (dtgResults.identity == 8)
            {
                needsQuotes = false;
                table = "Recurrence";
                idColumn = Glo.Tab.RECURRENCE_ID;
            }
            else if (dtgResults.identity == 9)
            {
                needsQuotes = false;
                table = "Resource";
                idColumn = Glo.Tab.RESOURCE_ID;
            }
            else if (dtgResults.identity == (int)UserSettings.TableIndex.Task)
            {
                needsQuotes = false;
                table = "Task";
                idColumn = Glo.Tab.TASK_ID;
            }
            else if (dtgResults.identity == (int)UserSettings.TableIndex.Visit)
            {
                needsQuotes = false;
                table = "Visit";
                idColumn = Glo.Tab.VISIT_ID;
            }
            else if (dtgResults.identity == (int)UserSettings.TableIndex.Document)
            {
                needsQuotes = false;
                table = "Document";
                idColumn = Glo.Tab.DOCUMENT_ID;
            }

            var columns = ColumnRecord.GetDictionary(table, true);
            if (columns == null)
                return;

            UpdateMultiple updateMultiple = new(dtgResults.identity, table, columns,
                                                idColumn, dtgResults.GetCurrentlySelectedIDs(), needsQuotes);
            updateMultiple.Owner = App.mainWindow;
            updateMultiple.ShowDialog();
        }
        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dtgResults.dtg.SelectedItems.Count < 1)
            {
                App.DisplayError("You must select at least one item to delete.", App.mainWindow);
                return;
            }

            if (!App.DeleteConfirm(dtgResults.dtg.SelectedItems.Count > 1, App.mainWindow))
                return;

            bool needsQuotes = false; ;
            string table = "Organisation";
            string column = Glo.Tab.ORGANISATION_ID;
            if (dtgResults.identity == 1)
            {
                needsQuotes = false; ;
                table = "Asset";
                column = Glo.Tab.ASSET_ID;
            }
            else if (dtgResults.identity == 2)
            {
                needsQuotes = false;
                table = "Contact";
                column = Glo.Tab.CONTACT_ID;
            }
            else if (dtgResults.identity == 7)
            {
                needsQuotes = false;
                table = "Conference";
                column = Glo.Tab.CONFERENCE_ID;
            }
            else if (dtgResults.identity == 8)
            {
                needsQuotes = false;
                table = "Recurrence";
                column = Glo.Tab.RECURRENCE_ID;
            }
            else if (dtgResults.identity == 9)
            {
                needsQuotes = false;
                table = "Resource";
                column = Glo.Tab.RESOURCE_ID;
            }
            else if (dtgResults.identity == (int)UserSettings.TableIndex.Task)
            {
                needsQuotes = false;
                table = "Task";
                column = Glo.Tab.TASK_ID;
            }
            else if (dtgResults.identity == (int)UserSettings.TableIndex.Visit)
            {
                needsQuotes = false;
                table = "Visit";
                column = Glo.Tab.VISIT_ID;
            }
            else if (dtgResults.identity == (int)UserSettings.TableIndex.Document)
            {
                needsQuotes = false;
                table = "Document";
                column = Glo.Tab.DOCUMENT_ID;
            }

            if (App.SendDelete(table, column, dtgResults.GetCurrentlySelectedIDs(), needsQuotes, App.mainWindow) &&
                MainWindow.pageDatabase != null)
                MainWindow.pageDatabase.RepeatSearches(dtgResults.identity);
        }

        // Bring up selected organisation on double-click.
        private void dtg_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            string currentID = dtgResults.GetCurrentlySelectedID();

            if (currentID != "")
            {
                if (dtgResults.identity == 0)
                    App.EditOrganisation(currentID, App.mainWindow);
                else if (dtgResults.identity == 1)
                    App.EditAsset(currentID, App.mainWindow);
                else if (dtgResults.identity == 2)
                    App.EditContact(currentID, App.mainWindow);
                else if (dtgResults.identity == 7)
                {
                    int id;
                    if (!int.TryParse(currentID, out id))
                        return;
                    App.EditConference(id, App.mainWindow);
                }
                else if (dtgResults.identity == 8)
                {
                    int id;
                    if (!int.TryParse(currentID, out id))
                        return;
                    App.EditRecurrence(id);
                }
                else if (dtgResults.identity == 9)
                    App.EditResource(currentID, App.mainWindow);
                else if (dtgResults.identity == (int)UserSettings.TableIndex.Task)
                    App.EditTask(currentID.ToString(), App.mainWindow);
                else if (dtgResults.identity == (int)UserSettings.TableIndex.Visit)
                    App.EditVisit(currentID.ToString(), App.mainWindow);
                else if (dtgResults.identity == (int)UserSettings.TableIndex.Document)
                    App.EditDocument(currentID.ToString(), App.mainWindow);
            }
        }

        private void btnAddPane_Click(object sender, RoutedEventArgs e)
        {
            containingPage.AddPane(containingFrame);
        }

        private void btnRemovePane_Click(object sender, RoutedEventArgs e)
        {
            containingPage.RemovePane(containingFrame);
        }

        private void dtgResults_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            EnforcePermissions();

            bool selectionMade = dtgResults.dtg.SelectedItems.Count > 0;
            btnDelete.IsEnabled = btnDelete.IsEnabled && selectionMade;
            btnUpdate.IsEnabled = btnUpdate.IsEnabled && selectionMade;

            if (dtgResults.identity == 7)
            {
                // Figure out cancel button text.
                btnCancel.Header = "Uncancel";
                foreach (CustomControls.SqlDataGrid.Row row in dtgResults.dtg.SelectedItems)
                    if ((bool?)row.items[11] != true)
                    {
                        btnCancel.Header = "Cancel";
                        break;
                    }

                List<string?> recNames = new();
                foreach (CustomControls.SqlDataGrid.Row row in dtgResults.dtg.SelectedItems)
                    recNames.Add((string?)row.items[2]);

                sepConf.Visibility = Visibility.Visible;
                btnCancel.Visibility = Visibility.Visible;
                btnAdjustTime.Visibility = Visibility.Visible;
                btnAdjustConns.Visibility = Visibility.Visible;
                btnSetHost.Visibility = Visibility.Visible;
                btnGoTo.Visibility = Visibility.Visible;

                btnCancel.IsEnabled = selectionMade;
                if (!selectionMade) btnCancel.Header = "Cancel";
                btnAdjustTime.IsEnabled = selectionMade;
                btnAdjustConns.IsEnabled = selectionMade;
                btnSetHost.IsEnabled = selectionMade;
                btnGoTo.IsEnabled = recNames.Count == 1;

                sepRec.Visibility = Visibility.Collapsed;

                // Enable or disable the the edit recurrence button.
                bool addToRec = selectionMade;
                bool removeFromRec = false;
                bool createRec = selectionMade;
                bool editRec = selectionMade;

                if (selectionMade)
                {
                    sepRec.Visibility = Visibility.Visible;
                    string? cur;
                    int idIndex;
                    for (int n = 0; n < recNames.Count; ++n)
                    {
                        cur = recNames[n];
                        if (cur != null)
                        {
                            // extract the ID.
                            if (cur.EndsWith(')'))
                            {
                                idIndex = cur.LastIndexOf('(') + 3; // Get rid of the "(R-"
                                recNames[n] = cur.Substring(idIndex, cur.Length - (idIndex + 1));
                            }
                        }
                    }

                    foreach (string? s in recNames)
                        if (s != null)
                        {
                            addToRec = false;
                            createRec = false;
                            removeFromRec = true;
                            break;
                        }

                    if (recNames[0] == null)
                    {
                        editRec = false;
                        recID = null;
                    }
                    else
                    {
                        recID = recNames[0];
                        for (int i = 1; i < recNames.Count; ++i)
                            if (recNames[i] != recID)
                            {
                                editRec = false;
                                break;
                            }
                    }
                }

                btnConfAddToRecurrence.Visibility = addToRec ? Visibility.Visible : Visibility.Collapsed;
                btnConfCreateRecurrence.Visibility = createRec ? Visibility.Visible : Visibility.Collapsed;
                btnConfRemoveFromRecurrence.Visibility = removeFromRec ? Visibility.Visible : Visibility.Collapsed;
                btnConfEditRecurrence.Visibility = editRec ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                sepConf.Visibility = Visibility.Collapsed;
                btnCancel.Visibility = Visibility.Collapsed;
                btnAdjustTime.Visibility = Visibility.Collapsed;
                btnAdjustConns.Visibility = Visibility.Collapsed;
                btnSetHost.Visibility = Visibility.Collapsed;
                btnGoTo.Visibility = Visibility.Collapsed;
                sepRec.Visibility = Visibility.Collapsed;
                btnConfAddToRecurrence.Visibility = Visibility.Collapsed;
                btnConfCreateRecurrence.Visibility = Visibility.Collapsed;
                btnConfRemoveFromRecurrence.Visibility = Visibility.Collapsed;
                btnConfEditRecurrence.Visibility = Visibility.Collapsed;
            }

            // Task button visibility and permissions
            Visibility visTask = dtgResults.identity == (int)UserSettings.TableIndex.Task ? Visibility.Visible :
                                                                                            Visibility.Collapsed;
            sepTask.Visibility = visTask;
            btnAddVisits.Visibility = visTask;
            btnAddDocument.Visibility = visTask;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool uncancel = btnCancel.Header.ToString() == "Uncancel";

                List<string> ids = dtgResults.GetCurrentlySelectedIDs();
                if (ids.Count == 0)
                    return;
                if (!App.DisplayQuestion("Are you sure?", "Cancel", DialogWindows.DialogBox.Buttons.YesNo,
                                         App.mainWindow))
                    return;

                UpdateRequest req = new(App.sd.sessionID, ColumnRecord.columnRecordID, App.sd.loginID,
                                        "Conference", new() { Glo.Tab.CONFERENCE_CANCELLED },
                                        new() { uncancel ? "0" : "1" },
                                        new() { false }, Glo.Tab.CONFERENCE_ID, ids, false);
                if (App.SendUpdate(req, App.mainWindow))
                    MainWindow.RepeatSearches(7);
            }
            catch { } // No catch required due to intended inactivity on a conference disappearing and error
                      // messages in App.Update().
        }
        private void btnAdjustTime_Click(object sender, RoutedEventArgs e)
        {
            List<string> ids = dtgResults.GetCurrentlySelectedIDs();
            if (ids.Count == 0)
                return;
            DialogWindows.AdjustConferenceTimes adjust = new(ids);
            adjust.Owner = App.mainWindow;
            adjust.ShowDialog();
        }
        private void btnAdjustConns_Click(object sender, RoutedEventArgs e)
        {
            List<string> ids = dtgResults.GetCurrentlySelectedIDs();
            if (ids.Count == 0)
                return;
            DialogWindows.AdjustConferenceConnections adjust = new(ids);
            adjust.Owner = App.mainWindow;
            adjust.ShowDialog();
        }
        private void btnSetHost_Click(object sender, RoutedEventArgs e)
        {
            List<string> idsStr = dtgResults.GetCurrentlySelectedIDs();
            if (idsStr.Count == 0)
                return;
            List<int> ids = new();
            try
            {
                foreach (string s in idsStr)
                    ids.Add(int.Parse(s));
            }
            catch { return; }

            SelectResult res;
            if (App.SendConnectionSelectRequest(ids.Select(i => i.ToString()).ToList(), out res, App.mainWindow))
            {
                string dialNoFriendly = ColumnRecord.GetPrintName(Glo.Tab.DIAL_NO,
                                            (ColumnRecord.Column)ColumnRecord.organisation[Glo.Tab.DIAL_NO]!);
                string orgRefFriendly = ColumnRecord.GetPrintName(Glo.Tab.ORGANISATION_REF,
                                            (ColumnRecord.Column)ColumnRecord.organisation[Glo.Tab.ORGANISATION_REF]!);
                string orgNameFriendly = ColumnRecord.GetPrintName(Glo.Tab.ORGANISATION_NAME,
                                            (ColumnRecord.Column)ColumnRecord.organisation[Glo.Tab.ORGANISATION_NAME]!);
                res.columnNames = new() { dialNoFriendly, orgRefFriendly, orgNameFriendly, "Test", "Host", "Presence" };

                LinkRecord lr = new(res.columnNames, res.rows, 0, "Set Host");
                lr.Owner = App.mainWindow;
                lr.ShowDialog();

                if (lr.id == null)
                    return;

                ConferenceAdjustment ca = new();
                ca.intent = ConferenceAdjustment.Intent.Host;
                ca.dialHost = lr.id;
                ca.ids = ids;

                // Error will display in the below function if it fails.
                if (App.SendConferenceAdjustment(ca, App.mainWindow))
                    MainWindow.RepeatSearches(7);
            }
        }
        private void btnGoTo_Click(object sender, RoutedEventArgs e)
        {
            string id = dtgResults.GetCurrentlySelectedID();
            List<Conference> selectResult;
            if (!App.SendConferenceSelectRequest(new() { id }, out selectResult, App.mainWindow))
                return;
            if (selectResult.Count != 1)
                return;

            Conference c = selectResult[0];

            foreach (PageConferenceView view in MainWindow.pageConferenceViews)
            {
                ScheduleView sv = view.schView;
                sv.scheduleTime = (DateTime)c.start!;
                sv.scrollResource = sv.GetScrollYfromResource(c.resourceID, c.resourceRow) -
                                                              (sv.ActualHeight * .5d - sv.zoomResourceCurrent * .5d);
                sv.EnforceResourceScrollLimits();
                view.updateScrollBar = true;
            }
        }

        string? recID = "";
        private void btnConfCreateRecurrence_Click(object sender, RoutedEventArgs e)
        {
            List<string> ids = dtgResults.GetCurrentlySelectedIDs();
            if (ids.Count == 0)
                return;

            NewRecurrence newRec = new();
            newRec.Owner = App.mainWindow;
            newRec.ShowDialog();
            if (newRec.DialogResult == true && newRec.returnID != "")
            {
                UpdateRequest req = new(App.sd.sessionID, ColumnRecord.columnRecordID, App.sd.loginID, "Conference",
                                        new() { Glo.Tab.RECURRENCE_ID }, new() { newRec.returnID }, new() { false },
                                        Glo.Tab.CONFERENCE_ID, ids, false);
                // Override all warnings as we're not moving anything.
                if (App.SendUpdate(req, true, true, true, App.mainWindow))
                    MainWindow.RepeatSearches(7);
            }
        }
        private void btnConfAddToRecurrence_Click(object sender, RoutedEventArgs e)
        {
            List<string> ids = dtgResults.GetCurrentlySelectedIDs();
            if (ids.Count == 0)
                return;

            LinkRecord lr = new("Recurrence", ColumnRecord.recurrence, "Select Recurrence");
            lr.Owner = App.mainWindow;
            lr.ShowDialog();
            if (lr.id == "") // Error will display in LinkRecord if it couldn't get the ID.
            {
                App.DisplayError("ID could not be ascertained from the record.", App.mainWindow);
                return;
            }

            UpdateRequest req = new(App.sd.sessionID, ColumnRecord.columnRecordID, App.sd.loginID, "Conference",
                                    new() { Glo.Tab.RECURRENCE_ID }, new() { lr.id }, new() { false },
                                    Glo.Tab.CONFERENCE_ID, ids, false);
            // Override all warnings as we're not moving anything.
            if (App.SendUpdate(req, true, true, true, App.mainWindow))
                MainWindow.RepeatSearches(7);
        }
        private void btnConfRemoveFromRecurrence_Click(object sender, RoutedEventArgs e)
        {
            List<string> ids = dtgResults.GetCurrentlySelectedIDs();
            if (ids.Count == 0)
                return;

            UpdateRequest req = new(App.sd.sessionID, ColumnRecord.columnRecordID, App.sd.loginID, "Conference",
                                    new() { Glo.Tab.RECURRENCE_ID }, new() { null }, new() { false },
                                    Glo.Tab.CONFERENCE_ID, ids, false);
            // Override all warnings as we're not moving anything.
            if (App.SendUpdate(req, true, true, true, App.mainWindow))
                MainWindow.RepeatSearches(7);
        }
        private void btnConfEditRecurrence_Click(object sender, RoutedEventArgs e)
        {
            if (recID == null)
                return;

            try
            {
                App.EditRecurrence(int.Parse(recID));
            }
            catch { }
        }

        private void btnAddVisit_Click(object sender, RoutedEventArgs e)
        {
            new NewVisit(dtgResults.GetCurrentlySelectedCells(1)).ShowDialog();
        }
        private void btnAddDocument_Click(object sender, RoutedEventArgs e)
        {
            new NewDocument(dtgResults.GetCurrentlySelectedCells(1)).ShowDialog();
        }

        private void cmbSearchType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbSearchType.SelectedIndex % 2 == 1)
            {
                cmbColumn.Visibility = Visibility.Visible;
                btnClear.Visibility = Visibility.Visible;
            }
            else
            {
                cmbColumn.Visibility = Visibility.Collapsed;
                btnClear.Visibility = Visibility.Collapsed;
            }
        }
    }
}
