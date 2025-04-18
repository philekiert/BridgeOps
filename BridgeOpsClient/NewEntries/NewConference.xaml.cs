﻿using DocumentFormat.OpenXml.Office.Word;
using SendReceiveClasses;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
    public partial class NewConference : CustomWindow
    {
        string id = "";
        int? recurrenceID = null;
        bool edit = false;
        bool cancelled = false;

        public NewConference()
        {
            InitializeComponent();

            // Get the closure options. Currently these are static, but there are plans to add options later on and I
            // have built the feature to accommodate that when the time comes.
            ColumnRecord.Column col = (ColumnRecord.Column)ColumnRecord.conference[Glo.Tab.CONFERENCE_CLOSURE]!;
            cmbClosure.Items.Add("");
            foreach (string s in col.allowed)
                cmbClosure.Items.Add(s);

            cmbResource.ItemsSource = PageConferenceView.resourceRowNames;

            txtTitle.MaxLength = (int)((ColumnRecord.Column)ColumnRecord.conference[Glo.Tab.CONFERENCE_TITLE]!)
                .restriction;
            txtNotes.MaxLength = (int)((ColumnRecord.Column)ColumnRecord.conference[Glo.Tab.NOTES]!)
                .restriction;

            // This gets called again in the inheriting constructors since various values get set.
            RecordOriginalValues();
            AnyInteraction(null, null);

            ditConference.ValueChangedHandler = () => { AnyInteraction(null, null); return true; } ;

            txtTitle.TextChanged += AnyInteraction;
            dtpStart.datePicker.SelectedDateChanged += AnyInteraction;
            dtpStart.timePicker.txt.TextChanged += AnyInteraction;
            dtpEnd.datePicker.SelectedDateChanged += AnyInteraction;
            dtpEnd.timePicker.txt.TextChanged += AnyInteraction;
            cmbResource.SelectionChanged += AnyInteraction;
            cmbClosure.SelectionChanged += AnyInteraction;
            // Recurrence button doesn't need this, as it saves on change rather than on clicking save.
            txtTitle.TextChanged += AnyInteraction;
            btnAddConnection.Click += AnyInteraction;
        }

        public NewConference(PageConferenceView.ResourceInfo? resource, DateTime start) : this()
        {
            // No idea why App.sd.username isn't accessible from here, but we have to pass it in.
            lblCreatedBy.Content = "Created by " + App.sd.username;

            dtpStart.SetDateTime(start);
            dtpEnd.SetDateTime(start.AddHours(1));

            ToggleConnectionDates(null, null);

            dtpStart.datePicker.SelectedDateChanged += ToggleConnectionDates;
            dtpEnd.datePicker.SelectedDateChanged += ToggleConnectionDates;

            // Populate available resources and select whichever one the user clicked on in the schedule view.
            if (resource != null)
                cmbResource.SelectedIndex = resource.SelectedRowTotal;

            ditConference.headers = ColumnRecord.conferenceHeaders;
            ditConference.Initialise(ColumnRecord.orderedConference, "Conference");

            btnSave.IsEnabled = App.sd.createPermissions[Glo.PERMISSION_CONFERENCES];

            RecordOriginalValues();
            AnyInteraction(null, null);
        }

        public NewConference(Conference conf) : this()
        {
            StringBuilder str = new("Created by ");
            str.Append(conf.createdUsername == null ? "[user deleted]" : conf.createdUsername);
            str.Append(" on ");
            str.Append(conf.createTime == null ? "[date missing]" :
                                                 ((DateTime)conf.createTime).ToString("dd/MM/yyyy HH:mm"));
            lblCreatedBy.Content = str;
            if (conf.editTime != null)
            {
                str = new("Edited by ");
                str.Append(conf.editedUsername == null ? "[user deleted]" : conf.editedUsername);
                str.Append(" on ");
                str.Append(((DateTime)conf.editTime).ToString("dd/MM/yyyy HH:mm"));
                lblEditedBy.Content = str;
            }
            if (conf.recurrenceID != null)
            {
                string recID = "R-" + conf.recurrenceID.ToString()!;
                if (conf.recurrenceName != null)
                    btnRecurrence.Content = $"{conf.recurrenceName} ({recID})";
                else
                    btnRecurrence.Content = recID;
                recurrenceID = conf.recurrenceID;
                btnRecurrence.IsEnabled = true;
            }
            else
            {
                btnRecurrence.Content = "Add to Existing";
                btnRecurrence.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_CONFERENCES];
            }

            edit = true;
            id = conf.conferenceID.ToString()!;
            txtTitle.Text = conf.title;
            txtNotes.Text = conf.notes;

            cancelled = conf.cancelled == true;
            if (cancelled)
                grdMain.RowDefinitions[0].Height = new(20);

            cmbClosure.Text = conf.closure;

            // Apply conference start and end times.
            if (conf.start != null)
                dtpStart.SetDateTime((DateTime)conf.start);
            if (conf.end != null)
                dtpEnd.SetDateTime((DateTime)conf.end);

            // Build the resource name for the dropdown.
            string resourceName = conf.resourceName == null ? "" : conf.resourceName;
            resourceName += " " + (conf.resourceRow + 1).ToString();

            cmbResource.Text = resourceName;
            if (cmbResource.SelectedIndex == -1)
                App.DisplayError("Could not determine resource name from conference record.", this);

            // Add rows for all connections.
            for (int i = 0; i < conf.connections.Count; ++i)
            {
                Conference.Connection connection = conf.connections[i];
                btnAddConnection_Click(null, null);
                connections[i].txtSearch.Text = connection.dialNo;
                connections[i].chkIsTest.IsChecked = connection.isTest == true;
                if (connection.connected != null)
                    connections[i].dtpConnected.SetDateTime((DateTime)connection.connected);
                if (connection.disconnected != null)
                    connections[i].dtpDisconnected.SetDateTime((DateTime)connection.disconnected);
                if (connection.isManaged && connection.orgReference != null)
                    connections[i].ApplySite(connection.dialNo, connection.orgReference,
                                             connection.orgName, connection.orgId);
                else
                    connections[i].ApplySite(connection.dialNo);
                connections[i].ToggleSearch(false);
                connections[i].connectionId = connection.connectionID;
            }

            UpdateConnectionIndicators();

            // Set up the data input table.
            ditConference.headers = ColumnRecord.conferenceHeaders;
            ditConference.Initialise(ColumnRecord.orderedConference, "Conference");
            ditConference.Populate(conf.additionalValObjects);
            ditConference.RememberStartingValues();

            // Apply permissions.
            if (!App.sd.editPermissions[Glo.PERMISSION_CONFERENCES])
            {
                btnSave.IsEnabled = false;
                btnAddConnection.IsEnabled = false;
                txtTitle.IsReadOnly = true;
                dtpStart.ToggleEnabled(false);
                dtpEnd.ToggleEnabled(false);
                cmbResource.IsEnabled = false;
                txtNotes.IsReadOnly = true;
                foreach (Connection connection in connections)
                {
                    connection.btnDown.IsEnabled = false;
                    connection.btnUp.IsEnabled = false;
                    connection.btnRemove.IsEnabled = false;
                }

                ditConference.ToggleFieldsEnabled(false);
            }
            else
                btnSave.IsEnabled = true;

            // Add methods to create space for date pickers if needed.
            ToggleConnectionDates(null, null);
            dtpStart.datePicker.SelectedDateChanged += ToggleConnectionDates;
            dtpEnd.datePicker.SelectedDateChanged += ToggleConnectionDates;

            Title = "Conference C-" + id;

            RecordOriginalValues();
            AnyInteraction(null, null);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (WindowState != WindowState.Maximized)
            {
                Settings.Default.ConfWinSizeX = Width;
                Settings.Default.ConfWinSizeY = Height;
                Settings.Default.Save();
                App.WindowClosed();
            }
        }

        string originalTitle = "";
        DateTime? originalStart = new();
        DateTime? originalEnd = new();
        string originalResource = "";
        string originalClosure = "";
        string originalNotes = "";

        List<(bool, DateTime?, DateTime?, string, int?, string?, string?)> originalConnections = new();

        public void RecordOriginalValues()
        {
            originalTitle = txtTitle.Text;
            originalStart = dtpStart.GetDateTime();
            originalEnd = dtpEnd.GetDateTime();
            originalResource = (string?)cmbResource.SelectedItem ?? "";
            originalClosure = (string?)cmbClosure.SelectedItem ?? "";
            originalNotes = txtNotes.Text;

            ditConference.RememberStartingValues();

            originalConnections.Clear();
            foreach (Connection c in connections)
                originalConnections.Add((c.chkIsTest.IsChecked == true,
                                         ConvertConnectionTime(c.dtpConnected),
                                         ConvertConnectionTime(c.dtpDisconnected),
                                         c.dialNo,
                                         c.orgId,
                                         c.orgName,
                                         c.orgRef));
        }
        private bool DetectEdit()
        {
            if (originalTitle != txtTitle.Text ||
                originalStart != dtpStart.GetDateTime() ||
                originalEnd != dtpEnd.GetDateTime() ||
                originalResource != ((string?)cmbResource.SelectedItem ?? "") ||
                originalClosure != ((string?)cmbClosure.SelectedItem ?? "") ||
                originalNotes != txtNotes.Text)
                return true;

            if (connections.Count != originalConnections.Count)
                return true;

            for (int i = 0; i < originalConnections.Count && i < connections.Count; ++i)
            {
                Connection c = connections[i];
                if (originalConnections[i] != (c.chkIsTest.IsChecked == true,
                                               ConvertConnectionTime(c.dtpConnected),
                                               ConvertConnectionTime(c.dtpDisconnected),
                                               c.dialNo,
                                               c.orgId,
                                               c.orgName,
                                               c.orgRef))
                    return true;
            }

            if (ditConference.CheckForValueChanges())
                return true;

            return false;
        }
        private void AnyInteraction(object? o, EventArgs? e)
        {
            changesMade = DetectEdit();
            // Only enable the save button if changes have been made and if the user has the relevant permissions.
            btnSave.IsEnabled = changesMade &&
                                ((edit && App.sd.editPermissions[Glo.PERMISSION_CONFERENCES]) ||
                                (!edit && App.sd.createPermissions[Glo.PERMISSION_CONFERENCES]));
        }

        public class Connection
        {
            public Button btnRemove;
            public Button btnUp;
            public Button btnDown;
            public TextBox txtSearch;
            public CheckBox chkIsTest;
            public CustomControls.DateTimePicker dtpConnected;
            public CustomControls.DateTimePicker dtpDisconnected;
            public Button btnOrgSummary;
            public Border bdrIndicator;
            public int? connectionId;
            public int row;
            public string dialNo;
            public string? orgRef;
            public string? orgName;
            public int? orgId;

            public Connection(Grid grd)
            {
                dialNo = "";

                btnRemove = new Button()
                {
                    Margin = new Thickness(10, 5, 0, 5),
                    VerticalContentAlignment = VerticalAlignment.Center
                };

                btnUp = new Button()
                {
                    Margin = new Thickness(5, 5, 5, 17),
                    Height = 12,
                    Width = 19,
                    Content = "▲",
                    Padding = new Thickness(0, -1, 0, 0),
                    BorderThickness = new Thickness(1, 1, 1, 0),
                    FontSize = 8
                };

                btnDown = new Button()
                {
                    Margin = new Thickness(5, 17, 5, 5),
                    Height = 12,
                    Width = 19,
                    Content = "▼",
                    Padding = new Thickness(0, -1, 0, 0),
                    FontSize = 8
                };

                txtSearch = new TextBox()
                {
                    Height = 24,
                    Margin = new Thickness(3, 5, 5, 5),
                    VerticalContentAlignment = VerticalAlignment.Center
                };

                chkIsTest = new CheckBox()
                {
                    Margin = new Thickness(7, 5, 3, 5),
                    VerticalAlignment = VerticalAlignment.Center
                };

                dtpConnected = new()
                {
                    Height = 24,
                    Margin = new Thickness(5, 5, 5, 5)
                };

                dtpDisconnected = new()
                {
                    Height = 24,
                    Margin = new Thickness(5, 5, 5, 5)
                };

                btnOrgSummary = new()
                {
                    Height = 24,
                    Background = Brushes.Transparent,
                    BorderThickness = new(0),
                    Padding = new(6, 0,
                    6, 0),
                    Margin = new Thickness(-1, 5, 10, 5),
                    HorizontalContentAlignment = HorizontalAlignment.Left
                };

                bdrIndicator = new()
                {
                    Background = (Brush)Application.Current.TryFindResource("brushConferenceCheck"),
                    Margin = new(4, 0, 5, 0),
                    CornerRadius = new(7)
                };
                ToolTipService.SetInitialShowDelay(bdrIndicator, 200);

                Grid.SetColumnSpan(txtSearch, 4);
                Grid.SetColumnSpan(bdrIndicator, 6);

                Grid.SetColumn(bdrIndicator, 0);
                Grid.SetColumn(btnRemove, 0);
                Grid.SetColumn(btnUp, 1);
                Grid.SetColumn(btnDown, 1);
                Grid.SetColumn(txtSearch, 2);
                Grid.SetColumn(chkIsTest, 2);
                Grid.SetColumn(dtpConnected, 3);
                Grid.SetColumn(dtpDisconnected, 4);
                Grid.SetColumn(btnOrgSummary, 5);

                grd.RowDefinitions.Add(new() { Height = GridLength.Auto });
                grd.Children.Add(bdrIndicator);
                grd.Children.Add(btnRemove);
                grd.Children.Add(btnUp);
                grd.Children.Add(btnDown);
                grd.Children.Add(txtSearch);
                grd.Children.Add(chkIsTest);
                grd.Children.Add(dtpConnected);
                grd.Children.Add(dtpDisconnected);
                grd.Children.Add(btnOrgSummary);

                ToggleSearch(true);
            }

            public void ApplySite(string dialNo) { ApplySite(dialNo, null, null, null); }
            public void ApplySite(string dialNo, string? orgRef, string? orgName, int? orgId)
            {
                TextBlock tbDialNo = new()
                { Text = dialNo, FontWeight = FontWeights.Bold };

                if (orgId != null)
                {
                    TextBlock tbOrgRef = new()
                    { Margin = new(15, 0, 15, 0), Text = orgRef ?? "" };
                    TextBlock tbOrgName = new()
                    { Text = orgName ?? "", FontStyle = FontStyles.Italic };

                    btnOrgSummary.Content = new StackPanel()
                    { Children = { tbDialNo, tbOrgRef, tbOrgName }, Orientation = Orientation.Horizontal };

                    bdrIndicator.Visibility = Grid.GetRow(bdrIndicator) == 0 ? Visibility.Visible : Visibility.Hidden;
                }
                else
                {
                    btnOrgSummary.Content = tbDialNo;

                    bdrIndicator.Visibility = Visibility.Hidden;
                }

                this.dialNo = dialNo;
                this.orgRef = orgRef;
                this.orgName = orgName;
                this.orgId = orgId;


            }

            public void UpdateConnectionIndicator(DateTime? confStart, DateTime? confEnd, string id)
            {
                bdrIndicator.ToolTip = null;

                if (txtSearch.IsVisible)
                {
                    bdrIndicator.Visibility = Visibility.Hidden;
                    return;
                }

                // Add a border for the host.
                bdrIndicator.Visibility = Grid.GetRow(bdrIndicator) == 0 ? Visibility.Visible : Visibility.Hidden;
                bdrIndicator.Background = (Brush)Application.Current.TryFindResource("brushConferenceCheck");

                // Add borders if the dial number clashes with another conference.
                if (confStart != null && confEnd != null)
                    foreach (PageConferenceView.Conference c in PageConferenceView.conferences.Values)
                        if (c.end > confStart && c.start < confEnd && c.id.ToString() != id && !c.cancelled)
                        {
                            // Print only the required information, and usually dates will not be necessary.
                            string s = "";
                            if (c.start.Date == c.end.Date)
                                if (confStart.Value.Date == confEnd.Value.Date) // Must be the same as c.start and c.end.
                                    s = $"{c.start.ToString("HH:mm")} - {c.end.ToString("HH:mm")}";
                                else
                                    s = $"{c.start.ToString("dd/MM/yyyy HH:mm")} - {c.end.ToString("HH:mm")}";
                            else
                                s = $"{c.start.ToString("dd/MM/yyyy HH:mm")} - {c.end.ToString("dd/MM/yyyy HH:mm")}";
                            s += "  " + c.title;

                            if (c.dialNos.Contains(dialNo))
                            {
                                if (bdrIndicator.ToolTip != null)
                                    bdrIndicator.ToolTip += "\n" + s;
                                else
                                    bdrIndicator.ToolTip = s;
                                bdrIndicator.Visibility = Visibility.Visible;
                                bdrIndicator.Background = (Brush)Application.Current.TryFindResource("brushConferenceWarning");
                            }
                        }
            }

            public void ToggleSearch(bool search)
            {
                if (search)
                {
                    txtSearch.Visibility = Visibility.Visible;
                    chkIsTest.Visibility = Visibility.Hidden;
                    dtpConnected.Visibility = Visibility.Hidden;
                    dtpDisconnected.Visibility = Visibility.Hidden;
                    btnOrgSummary.Visibility = Visibility.Hidden;
                }
                else
                {
                    txtSearch.Visibility = Visibility.Hidden;
                    chkIsTest.Visibility = Visibility.Visible;
                    dtpConnected.Visibility = Visibility.Visible;
                    dtpDisconnected.Visibility = Visibility.Visible;
                    btnOrgSummary.Visibility = Visibility.Visible;
                }
            }

            public void ToggleDateVisible(bool visible)
            {
                dtpConnected.ToggleDatePicker(visible);
                dtpDisconnected.ToggleDatePicker(visible);
            }

            public void SetRow(int row, int connectionCount)
            {
                Grid.SetRow(btnRemove, row);
                Grid.SetRow(btnUp, row);
                Grid.SetRow(btnDown, row);
                Grid.SetRow(txtSearch, row);
                Grid.SetRow(chkIsTest, row);
                Grid.SetRow(dtpConnected, row);
                Grid.SetRow(dtpDisconnected, row);
                Grid.SetRow(btnOrgSummary, row);
                Grid.SetRow(bdrIndicator, row);

                btnUp.IsEnabled = row > 0;
                btnDown.IsEnabled = row < connectionCount - 1;

                this.row = row;
            }

            public void Remove(Grid grid)
            {
                grid.Children.Remove(btnRemove);
                grid.Children.Remove(btnUp);
                grid.Children.Remove(btnDown);
                grid.Children.Remove(txtSearch);
                grid.Children.Remove(chkIsTest);
                grid.Children.Remove(dtpConnected);
                grid.Children.Remove(dtpDisconnected);
                grid.Children.Remove(btnOrgSummary);
                grid.Children.Remove(bdrIndicator);
            }
        }
        public List<Connection> connections = new();

        private void btnAddConnection_Click(object? sender, RoutedEventArgs? e)
        {
            if (connections.Count >= ColumnRecord.GetColumn(ColumnRecord.connection,
                                                            Glo.Tab.CONNECTION_ROW).restriction - 1)
                return;

            Connection connection = new(grdConnections);
            connection.btnRemove.Click += RemoveConnection;
            connection.btnUp.Click += btnUp_Click;
            connection.btnDown.Click += btnDown_Click;
            connection.txtSearch.KeyDown += txtSearch_KeyDown;
            connection.btnOrgSummary.Click += btnSummary_Click;

            connection.btnRemove.Click += AnyInteraction;
            connection.btnUp.Click += AnyInteraction;
            connection.btnDown.Click += AnyInteraction;
            connection.chkIsTest.Click += AnyInteraction;
            connection.dtpConnected.datePicker.SelectedDateChanged += AnyInteraction;
            connection.dtpConnected.timePicker.txt.TextChanged += AnyInteraction;
            connection.dtpDisconnected.datePicker.SelectedDateChanged += AnyInteraction;
            connection.dtpDisconnected.timePicker.txt.TextChanged += AnyInteraction;
            // AnyInteraction for site search is implemented in txtSearch_KeyDown.

            connection.btnRemove.Style = (Style)FindResource("minus-button");

            connections.Add(connection);

            connection.txtSearch.Focus();

            ResetConnectionGridRows();
            ToggleConnectionDates(null, null);
        }

        private void RemoveConnection(object sender, EventArgs e)
        {
            int index = Grid.GetRow((Button)sender);

            connections[index].Remove(grdConnections);
            connections.RemoveAt(index);
            ResetConnectionGridRows();

            if (grdConnections.RowDefinitions.Count > 1)
                grdConnections.RowDefinitions.RemoveAt(1);
        }

        private void ResetConnectionGridRows()
        {
            for (int i = 0; i < connections.Count;)
                connections[i].SetRow(i++, connections.Count);

            UpdateConnectionIndicators();
        }

        private void SearchSite(object sender, EventArgs e)
        {
            if (sender is TextBox txt && txt.Text.Length > 0)
            {
                int index = Grid.GetRow(txt);
                Connection connection = connections[index];

                foreach (Connection c in connections)
                    if (c.dialNo == connection.txtSearch.Text && c != connection)
                    {
                        App.DisplayError("Cannot add duplicate dial numbers to a conference.", "Duplicate Dial No",
                                         this);
                        return;
                    }

                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    connection.ToggleSearch(false);
                    connection.ApplySite(txt.Text);
                    return;
                }

                List<List<object?>> rows;

                // This could really use its own agent command, but will do for now.

                SelectRequest req = new(App.sd.sessionID, ColumnRecord.columnRecordID,
                                        "Organisation", false, new(), new(), new(), new(),
                                        new() { Glo.Tab.DIAL_NO,
                                                Glo.Tab.ORGANISATION_REF,
                                                Glo.Tab.ORGANISATION_NAME,
                                                Glo.Tab.ORGANISATION_ID }, new() { "", "", "", "" },
                                        new() { Glo.Tab.ORGANISATION_REF,
                                                Glo.Tab.DIAL_NO,
                                                Glo.Tab.ORGANISATION_AVAILABLE },
                                        new() { "=", "=", "=" },
                                        new() { txt.Text,
                                                txt.Text,
                                                "1"},
                                        new() { true, true, false },
                                        new(), new(), new() { "OR", "AND" },
                                        new(), new());
                App.SendSelectRequest(req, out _, out rows, this);

                if (rows.Count == 0 || rows[0].Count != 4)
                {
                    connection.ToggleSearch(false);
                    connection.ApplySite(txt.Text);
                }
                else if (rows[0][0] is string d)
                {
                    connection.ToggleSearch(false);
                    try { connection.ApplySite(d, (string?)rows[0][1], (string?)rows[0][2], (int?)rows[0][3]); }
                    catch { connection.ApplySite(d, null, null, null); }
                }
            }
        }

        private void txtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SearchSite(sender, e);
                UpdateConnectionIndicators();
                AnyInteraction(null, null);
            }
            else if (e.Key == Key.Escape)
            {
                Connection connection = connections[Grid.GetRow((TextBox)sender)];
                if (connection.dialNo != "")
                    connection.ToggleSearch(false);
                UpdateConnectionIndicators();
            }
            else
                return;

            bool foundNewFocus = false;
            int row = Grid.GetRow((TextBox)sender);
            for (int i = row; i < connections.Count; ++i)
            {
                if (connections[i].txtSearch.Visibility == Visibility.Visible)
                {
                    connections[i].txtSearch.Focus();
                    foundNewFocus = true;
                    break;
                }
            }
            if (!foundNewFocus)
                for (int i = 0; i < row; ++i)
                {
                    if (connections[i].txtSearch.Visibility == Visibility.Visible)
                    {
                        connections[i].txtSearch.Focus();
                        break;
                    }
                }
        }

        private void btnSummary_Click(object sender, EventArgs e)
        {
            int index = Grid.GetRow((UIElement)sender);
            Connection connection = connections[index];

            // If the user lacks edit permissions and can't make changes anyway, just load the organisation.
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) ||
                !App.sd.editPermissions[Glo.PERMISSION_CONFERENCES])
            {
                if (connection.orgId != null)
                    App.EditOrganisation(connection.orgId.ToString()!, App.mainWindow);
            }
            else
            {
                connection.ToggleSearch(true);
                connection.txtSearch.Focus();
            }

            UpdateConnectionIndicators();
        }

        private void btnUp_Click(object sender, EventArgs e)
        {
            int index = Grid.GetRow((Button)sender);
            if (index <= 0)
                return;
            Connection move = connections[index];
            connections.RemoveAt(index);
            connections.Insert(index - 1, move);
            ResetConnectionGridRows();
        }

        private void btnDown_Click(object sender, EventArgs e)
        {
            int index = Grid.GetRow((Button)sender);
            if (index >= connections.Count - 1)
                return;
            Connection move = connections[index];
            connections.RemoveAt(index);
            connections.Insert(index + 1, move);
            ResetConnectionGridRows();
        }

        public bool SingleDay { get { return dtpStart.GetDate() == dtpEnd.GetDate(); } }
        public const double CrossDayPreferredWidth = 1100;
        private void ToggleConnectionDates(object? sender, SelectionChangedEventArgs? e)
        {
            bool singleDay = true;
            foreach (var c in connections)
            {
                DateTime? connected = c.dtpConnected.datePicker.SelectedDate;
                DateTime? disconnected = c.dtpDisconnected.datePicker.SelectedDate;
                DateTime? confStart = dtpStart.datePicker.SelectedDate;
                DateTime? confEnd = dtpEnd.datePicker.SelectedDate;
                if ((connected != null && (connected.Value.Date != confStart ||
                                           connected.Value.Date != confEnd)) ||
                    (disconnected != null && (disconnected.Value.Date != confStart ||
                                              disconnected.Value.Date != confEnd)))
                {
                    singleDay = false;
                    break;
                }

            }
            if (singleDay)
                singleDay = SingleDay;

            foreach (Connection c in connections)
                c.ToggleDateVisible(!singleDay);
            if (singleDay)
            {
                grdHeaders.ColumnDefinitions[3].Width = new GridLength(72);
                grdHeaders.ColumnDefinitions[4].Width = new GridLength(85);
                grdHeaders.ColumnDefinitions[3].MaxWidth = 72;
                grdHeaders.ColumnDefinitions[4].MaxWidth = 85;
                grdConnections.ColumnDefinitions[3].Width = new GridLength(72);
                grdConnections.ColumnDefinitions[4].Width = new GridLength(85);
                grdConnections.ColumnDefinitions[3].MaxWidth = 72;
                grdConnections.ColumnDefinitions[4].MaxWidth = 85;
            }
            else
            {
                // Widen the window or the user will certainly need to scroll to read site names.
                if (Width < CrossDayPreferredWidth)
                    Width = CrossDayPreferredWidth;
                grdHeaders.ColumnDefinitions[3].Width = new GridLength(175);
                grdHeaders.ColumnDefinitions[4].Width = new GridLength(175);
                grdHeaders.ColumnDefinitions[3].MaxWidth = 175;
                grdHeaders.ColumnDefinitions[4].MaxWidth = 175;
                grdConnections.ColumnDefinitions[3].Width = new GridLength(175);
                grdConnections.ColumnDefinitions[4].Width = new GridLength(175);
                grdConnections.ColumnDefinitions[3].MaxWidth = 175;
                grdConnections.ColumnDefinitions[4].MaxWidth = 175;
            }
        }

        private void UpdateConnectionIndicators()
        {
            DateTime? start = dtpStart.GetDateTime();
            DateTime? end = dtpEnd.GetDateTime();
            foreach (Connection c in connections)
                c.UpdateConnectionIndicator(start, end, id);
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (cmbResource.Text == "")
            {
                App.DisplayError("You must select a resource.", this);
                return;
            }

            if (Save())
            {
                if (MainWindow.pageDatabase != null)
                    MainWindow.pageDatabase.RepeatSearches(7);
                Close();
            }
        }

        // If the datepicker on a connection is invisible, GetDateTime() will resolve to null, so we need to build it
        // from the conference's start date and connection time (or disconnection time).
        private DateTime? ConvertConnectionTime(CustomControls.DateTimePicker dtp)
        {
            if (!dtp.DateVisible && dtp.GetTime() != null)
                return new DateTime(dtpStart.GetDate()!.Value.Ticks + dtp.GetTime()!.Value.Ticks);
            return dtp.GetDateTime();
        }

        private bool Save()
        {
            // Vet all inputs.

            if (txtTitle.Text == null || txtTitle.Text == "")
                return App.Abort("Must enter a conference title.", this);
            DateTime? start = dtpStart.GetDateTime();
            if (start == null)
                return App.Abort("Must select a start date and time.", this);
            DateTime? end = dtpEnd.GetDateTime();
            if (end == null)
                return App.Abort("Must select an end date and time.", this);
            if (start >= end)
                return App.Abort("End time must be greater than the start time.", this);
            int indexResourceNameSplit = cmbResource.Text.LastIndexOf(' ');
            string resourceName;
            int resourceID = -1;
            int resourceRow = -1;
            if (indexResourceNameSplit > 0 && indexResourceNameSplit < cmbResource.Text.Length - 1)
            {
                resourceName = cmbResource.Text.Remove(indexResourceNameSplit);
                if (resourceName == "" || !int.TryParse(cmbResource.Text.Substring(indexResourceNameSplit + 1),
                                                                                   out resourceRow))
                    return App.Abort("Must select a valid resource.", this);
                --resourceRow; // Row names add 1 to the row number for user readability.
                bool foundResource = false;
                foreach (PageConferenceView.ResourceInfo ri in PageConferenceView.resources.Values)
                {
                    if (ri.name == resourceName && resourceRow >= 0 && resourceRow < ri.rowsTotal)
                    {
                        foundResource = true;
                        resourceID = ri.id;
                        break;
                    }
                }
                if (!foundResource)
                    return App.Abort("Must select a valid resource.", this);
            }

            // Set the ID as null if we're creating, otherwise use the ID.
            int? conferenceIdNullable;
            int conferenceId;
            if (!edit)
                conferenceIdNullable = null;
            else if (!int.TryParse(id, out conferenceId))
                return App.Abort("Could not determine conference ID.", this);
            else
                conferenceIdNullable = conferenceId;

            List<Conference.Connection> conferenceConnections = new();
            foreach (Connection c in connections)
            {
                if (c.dialNo == "" || c.txtSearch.Visibility == Visibility.Visible)
                    return App.Abort("All connection rows must have a dial number selected.", this);

                Conference.Connection confC = new Conference.Connection(conferenceIdNullable,
                                                                        c.dialNo, c.orgRef != null,
                                                                        c.dtpConnected.GetDateTime(),
                                                                        c.dtpDisconnected.GetDateTime(),
                                                                        c.row + 1, c.chkIsTest.IsChecked == true);
                confC.connectionID = c.connectionId;

                // If the date pickers aren't visible and times are set, automatically set them to the day of the
                // conference.
                confC.connected = ConvertConnectionTime(c.dtpConnected);
                confC.disconnected = ConvertConnectionTime(c.dtpDisconnected);

                if (confC.connected != null && confC.disconnected != null && confC.connected >= confC.disconnected)
                    return App.Abort("Disconnection times must be later than connection times.", this);

                conferenceConnections.Add(confC);
            }

            Conference conference = new(App.sd.sessionID, ColumnRecord.columnRecordID,
                                        resourceID, resourceRow, txtTitle.Text, (DateTime)start, (DateTime)end,
                                        App.sd.loginID, txtNotes.Text, new(), new(), new(), conferenceConnections);
            conference.conferenceID = conferenceIdNullable;
            conference.closure = cmbClosure.Text == "" ? null : cmbClosure.Text;
            conference.cancelled = cancelled;

            // Note that when making an update, the creation login is ignored by Conference.SqlUpdate.
            if (edit)
                conference.editLoginID = App.sd.loginID;

            // Get the any changed data from the DataInputTable.
            ditConference.ScoopValues();
            ditConference.ExtractValues(out conference.additionalCols, out conference.additionalVals);
            if (edit)
            {
                List<int> toRemove = new();
                for (int i = 0; i < conference.additionalCols.Count; ++i)
                    if (conference.additionalVals[i] == ditConference.startingValues[i])
                        toRemove.Add(i);
                int mod = 0; // Each one we remove, we need to take into account that the list is now 1 less.
                foreach (int i in toRemove)
                {
                    conference.additionalCols.RemoveAt(i - mod);
                    conference.additionalVals.RemoveAt(i - mod);
                    ++mod;
                }
            }

            // Obtain types and determine whether or not quotes will be needed.
            conference.additionalNeedsQuotes = new();
            foreach (string c in conference.additionalCols)
                conference.additionalNeedsQuotes.Add(
                    SqlAssist.NeedsQuotes(ColumnRecord.GetColumn(ColumnRecord.conference, c).type));

            if (edit)
                return App.SendUpdate(Glo.CLIENT_UPDATE_CONFERENCE, conference, false, false, false, this);
            else
                return App.SendInsert(Glo.CLIENT_NEW_CONFERENCE, new List<Conference>() { conference }, this);
        }

        // Don't scroll to fit in the summary button if the user clicks one that extends out of view.
        bool dontScroll = false;
        private void ScrollViewer_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        { dontScroll = true; }
        double lastScroll = 0;
        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (dontScroll)
            {
                ((ScrollViewer)sender).ScrollToHorizontalOffset(lastScroll);
                dontScroll = false;
            }
            lastScroll = ((ScrollViewer)sender).HorizontalOffset;
        }

        private void CustomWindow_KeyDown(object sender, KeyEventArgs e)
        {
            foreach (Connection c in connections)
                if (Keyboard.FocusedElement == c.txtSearch)
                    return;

            if (e.Key == Key.Escape)
                Close();
        }

        private void btnRecurrence_Click(object sender, RoutedEventArgs e)
        {
            if (recurrenceID != null)
            {
                App.EditRecurrence((int)recurrenceID);
            }
            else
            {
                LinkRecord lr = new("Recurrence", ColumnRecord.recurrence, "Select Recurrence");
                lr.Owner = this;
                lr.ShowDialog();
                int recID;
                if (lr.id == "" || !int.TryParse(lr.id, out recID))
                    return;

                UpdateRequest req = new(App.sd.sessionID, ColumnRecord.columnRecordID, App.sd.loginID, "Conference",
                                        new() { Glo.Tab.RECURRENCE_ID }, new() { lr.id }, new() { false },
                                        Glo.Tab.CONFERENCE_ID, new List<string> { id }, false);
                if (App.SendUpdate(req, true, true, true, this)) // Override all warnings as we're not moving anything.
                {
                    MainWindow.RepeatSearches(7);
                    recurrenceID = recID;
                    List<List<object?>> rows;
                    if (App.Select("Recurrence", new() { Glo.Tab.RECURRENCE_NAME },
                                   new() { Glo.Tab.RECURRENCE_ID }, new() { lr.id }, new() { Conditional.Equals },
                                   out _, out rows, false, false, this))
                    {
                        try
                        {
                            string? recName = (string?)rows[0][0];

                            if (recName != null)
                                btnRecurrence.Content = $"{recName} (R-{recID})";
                            else
                                btnRecurrence.Content = $"R-{recID}";
                            btnRecurrence.IsEnabled = true;
                        }
                        catch
                        {
                            App.DisplayError("Something went wrong. The recurrence can not be found.", this);
                            btnRecurrence.Content = "";
                            btnRecurrence.IsEnabled = false;
                        }
                        AnyInteraction(null, null);
                    }
                }
            }
        }
    }
}