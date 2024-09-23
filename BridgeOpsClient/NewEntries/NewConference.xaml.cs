﻿using SendReceiveClasses;
using System;
using System.Collections.Generic;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace BridgeOpsClient
{
    public partial class NewConference : CustomWindow
    {
        string id = "";

        public NewConference(PageConferenceView.ResourceInfo? resource, DateTime start)
        {
            MaxHeight = 400;

            InitializeComponent();

            dtpStart.SetDateTime(start);
            dtpEnd.SetDateTime(start.AddHours(1));

            ToggleConnectionDates(null, null);

            dtpStart.datePicker.SelectedDateChanged += ToggleConnectionDates;
            dtpEnd.datePicker.SelectedDateChanged += ToggleConnectionDates;

            // Populate available resources and select whichever one the user clicked on in the schedule view.
            cmbResource.ItemsSource = PageConferenceView.resourceRowNames;
            if (resource == null)
                App.DisplayError("Could not determine resource from selected row, please set manually.");
            else
                cmbResource.SelectedIndex = resource.SelectedRowTotal;

            ditConference.headers = ColumnRecord.organisationHeaders;
            ditConference.Initialise(ColumnRecord.orderedOrganisation, "Organisation");

            btnSave.IsEnabled = App.sd.createPermissions[Glo.PERMISSION_CONFERENCES];
        }

        public NewConference(Conference conf)
        {
            id = conf.conferenceID.ToString()!;

            MaxHeight = 400;

            InitializeComponent();

            txtTitle.Text = conf.title;

            if (conf.start != null)
                dtpStart.SetDateTime((DateTime)conf.start);
            if (conf.end != null)
                dtpEnd.SetDateTime((DateTime)conf.end);

            ToggleConnectionDates(null, null);

            dtpStart.datePicker.SelectedDateChanged += ToggleConnectionDates;
            dtpEnd.datePicker.SelectedDateChanged += ToggleConnectionDates;

            string resourceName = "";
            foreach (PageConferenceView.ResourceInfo rInfo in PageConferenceView.resources)
                if (rInfo.id == conf.resourceID)
                {
                    resourceName = rInfo.name;
                    break;
                }
            resourceName += " " + (conf.resourceRow + 1).ToString();

            cmbResource.ItemsSource = PageConferenceView.resourceRowNames;
            cmbResource.Text = resourceName;
            if (cmbResource.SelectedIndex == -1)
                App.DisplayError("Could not determine resource name from conference record.");

            ditConference.headers = ColumnRecord.organisationHeaders;
            ditConference.Initialise(ColumnRecord.orderedOrganisation, "Organisation");

            txtNotes.Text = conf.notes;

            for (int i = 0; i < conf.connections.Count; ++i)
            {
                Conference.Connection connection = conf.connections[i];
                btnAddConnection_Click(null, null);
                connections[i].txtSearch.Text = connection.dialNo;
                connections[i].chkIsTest.IsChecked = connection.isTest == true;
                if (connection.connected != null)
                    connections[i].dtpConnected.SetDateTime((DateTime)connection.connected);
                if (connection.disconnected != null)
                    connections[i].dtpConnected.SetDateTime((DateTime)connection.disconnected);
                if (connection.isManaged && connection.orgReference != null)
                {
                    connections[i].ApplySite(connection.dialNo, connection.orgReference,
                                             connection.orgName, connection.orgId);
                }
                else
                    connections[i].ApplySite(connection.dialNo);
                connections[i].ToggleSearch(false);
            }

            btnDelete.IsEnabled = App.sd.deletePermissions[Glo.PERMISSION_CONFERENCES];
            if (!App.sd.editPermissions[Glo.PERMISSION_CONFERENCES])
            {
                btnSave.IsEnabled = false;
                btnCancel.IsEnabled = false;
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
            }
            else
                btnSave.IsEnabled = true;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            App.WindowClosed();
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            MaxHeight = double.PositiveInfinity;
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
            public int connectionId; // -1 if booking is new
            public int row;
            public string dialNo;
            public string? orgRef;
            public string? orgName;
            public int? orgId;

            public Connection()
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
                    Background = Brushes.White,
                    BorderThickness = new(0),
                    Padding = new(6, 0,
                    6, 0),
                    Margin = new Thickness(-1, 5, 5, 5),
                    HorizontalContentAlignment = HorizontalAlignment.Left
                };

                Grid.SetColumnSpan(txtSearch, 4);

                Grid.SetColumn(btnRemove, 0);
                Grid.SetColumn(btnUp, 1);
                Grid.SetColumn(btnDown, 1);
                Grid.SetColumn(txtSearch, 2);
                Grid.SetColumn(chkIsTest, 2);
                Grid.SetColumn(dtpConnected, 3);
                Grid.SetColumn(dtpDisconnected, 4);
                Grid.SetColumn(btnOrgSummary, 5);

                ToggleSearch(true);
            }

            public void ApplySite(string dialNo) { ApplySite(dialNo, null, null, null); }
            public void ApplySite(string dialNo, string? orgRef, string? orgName, int? orgId)
            {
                TextBlock tbDialNo = new()
                { Text = dialNo, FontWeight = FontWeights.Bold };

                if (orgRef != null && orgName != null)
                {
                    TextBlock tbOrgRef = new()
                    { Margin = new(15, 0, 15, 0), Text = orgRef };
                    TextBlock tbOrgName = new()
                    { Text = orgName, FontStyle = FontStyles.Italic };

                    btnOrgSummary.Content = new StackPanel()
                    { Children = { tbDialNo, tbOrgRef, tbOrgName }, Orientation = Orientation.Horizontal };
                }
                else
                {
                    btnOrgSummary.Content = tbDialNo;
                }

                this.dialNo = dialNo;
                this.orgRef = orgRef;
                this.orgName = orgName;
                this.orgId = orgId;
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

                btnUp.IsEnabled = row > 1;
                btnDown.IsEnabled = row < connectionCount;

                this.row = row - 1;
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
            }
        }
        public List<Connection> connections = new();

        private void btnAddConnection_Click(object? sender, RoutedEventArgs? e)
        {
            if (connections.Count >= ColumnRecord.GetColumn(ColumnRecord.connection,
                                                            Glo.Tab.CONNECTION_ROW).restriction - 1)
                return;

            Connection connection = new();
            connection.btnRemove.Click += RemoveConnection;
            connection.btnUp.Click += btnUp_Click;
            connection.btnDown.Click += btnDown_Click;
            connection.txtSearch.KeyDown += txtSearch_KeyDown;
            connection.btnOrgSummary.Click += btnSummary_Click;

            connection.btnRemove.Style = (Style)FindResource("minus-button");

            grdConnections.RowDefinitions.Add(new() { Height = GridLength.Auto });
            grdConnections.Children.Add(connection.btnRemove);
            grdConnections.Children.Add(connection.btnUp);
            grdConnections.Children.Add(connection.btnDown);
            grdConnections.Children.Add(connection.txtSearch);
            grdConnections.Children.Add(connection.chkIsTest);
            grdConnections.Children.Add(connection.dtpConnected);
            grdConnections.Children.Add(connection.dtpDisconnected);
            grdConnections.Children.Add(connection.btnOrgSummary);

            connections.Add(connection);

            connection.txtSearch.Focus();

            ResetConnectionGridRows();
            ToggleConnectionDates(null, null);
        }

        private void RemoveConnection(object sender, EventArgs e)
        {
            int index = Grid.GetRow((Button)sender) - 1;

            connections[index].Remove(grdConnections);
            connections.RemoveAt(index);
            ResetConnectionGridRows();

            if (grdConnections.RowDefinitions.Count > 1)
                grdConnections.RowDefinitions.RemoveAt(1);
        }

        private void ResetConnectionGridRows()
        {
            for (int i = 0; i < connections.Count;)
                connections[i].SetRow(++i, connections.Count);
        }

        private void SearchSite(object sender, EventArgs e)
        {
            if (sender is TextBox txt && txt.Text.Length > 0)
            {
                int index = Grid.GetRow(txt) - 1;
                Connection connection = connections[index];

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
                App.SendSelectRequest(req, out _, out rows);

                if (rows.Count == 0 || rows[0].Count != 4)
                {
                    connection.ToggleSearch(false);
                    connection.ApplySite(txt.Text);
                }
                else if (rows[0][0] is string d && rows[0][1] is string r &&
                         rows[0][2] is string n && rows[0][3] is int i)
                {
                    connection.ToggleSearch(false);
                    connection.ApplySite(d, r, n, i);
                }

            }
        }

        private void txtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SearchSite(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                Connection connection = connections[Grid.GetRow((TextBox)sender) - 1];
                if (connection.dialNo != "")
                    connection.ToggleSearch(false);
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
            int index = Grid.GetRow((UIElement)sender) - 1;
            Connection connection = connections[index];

            // If the user lacks edit permissions and can't make changes anyway, just load the organisation.
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) ||
                !App.sd.editPermissions[Glo.Tab.CONFERENCE_STATIC_COUNT])
            {
                if (connection.orgId != null)
                    App.EditOrganisation(connection.orgId.ToString()!);
            }
            else
            {
                connection.ToggleSearch(true);
                connection.txtSearch.Focus();
            }
        }

        private void btnUp_Click(object sender, EventArgs e)
        {
            int index = Grid.GetRow((Button)sender) - 1;
            if (index <= 0)
                return;
            Connection move = connections[index];
            connections.RemoveAt(index);
            connections.Insert(index - 1, move);
            ResetConnectionGridRows();
        }

        private void btnDown_Click(object sender, EventArgs e)
        {
            int index = Grid.GetRow((Button)sender) - 1;
            if (index >= connections.Count - 1)
                return;
            Connection move = connections[index];
            connections.RemoveAt(index);
            connections.Insert(index + 1, move);
            ResetConnectionGridRows();
        }

        bool SingleDay { get { return dtpStart.GetDate() == dtpEnd.GetDate(); } }
        private void ToggleConnectionDates(object? sender, SelectionChangedEventArgs? e)
        {
            bool singleDay = SingleDay;
            foreach (Connection c in connections)
                c.ToggleDateVisible(!singleDay);
            if (singleDay)
            {
                grdConnections.ColumnDefinitions[3].Width = new GridLength(72);
                grdConnections.ColumnDefinitions[4].Width = new GridLength(85);
                grdConnections.ColumnDefinitions[3].MaxWidth = 72;
                grdConnections.ColumnDefinitions[4].MaxWidth = 85;
            }
            else
            {
                if (Width < 1000) // Widen the window or the user will certainly need to scroll to read site names.
                    Width = 1000;
                grdConnections.ColumnDefinitions[3].Width = new GridLength(175);
                grdConnections.ColumnDefinitions[4].Width = new GridLength(175);
                grdConnections.ColumnDefinitions[3].MaxWidth = 175;
                grdConnections.ColumnDefinitions[4].MaxWidth = 175;
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (Save())
                Close();
        }

        private bool Save()
        {
            // Vet all inputs.

            bool Abort(string message)
            {
                App.DisplayError(message);
                return false;
            }

            if (txtTitle.Text == null || txtTitle.Text == "")
                return Abort("Must enter a conference title.");
            DateTime? start = dtpStart.GetDateTime();
            if (start == null)
                return Abort("Must select a start date and time.");
            DateTime? end = dtpEnd.GetDateTime();
            if (end == null)
                return Abort("Must select an end date and time.");
            if (start >= end)
                return Abort("End cannot be before the start date and time.");
            int indexResourceNameSplit = cmbResource.Text.LastIndexOf(' ');
            string resourceName;
            int resourceID = -1;
            int resourceRow = -1;
            if (indexResourceNameSplit > 0 && indexResourceNameSplit < cmbResource.Text.Length - 1)
            {
                resourceName = cmbResource.Text.Remove(indexResourceNameSplit);
                if (resourceName == "" || !int.TryParse(cmbResource.Text.Substring(indexResourceNameSplit + 1),
                                                                                   out resourceRow))
                    return Abort("Must select a valid resource.");
                --resourceRow; // Row names add 1 to the row number for user readability.
                bool foundResource = false;
                foreach (PageConferenceView.ResourceInfo ri in PageConferenceView.resources)
                {
                    if (ri.name == resourceName && resourceRow >= 0 && resourceRow < ri.rowsTotal)
                    {
                        foundResource = true;
                        resourceID = ri.id;
                        break;
                    }
                }
                if (!foundResource)
                    return Abort("Must select a valid resource.");
            }

            List<Conference.Connection> conferenceConnections = new();
            foreach (Connection c in connections)
            {
                if (c.dialNo == "" || c.txtSearch.Visibility == Visibility.Visible)
                    return Abort("All connection rows must have a dial number selected.");

                conferenceConnections.Add(new Conference.Connection(null, c.dialNo, c.orgRef != null,
                                                                    c.dtpConnected.GetDateTime(),
                                                                    c.dtpDisconnected.GetDateTime(),
                                                                    c.row, c.chkIsTest.IsChecked == true));
            }

            Conference conference = new(App.sd.sessionID, ColumnRecord.columnRecordID,
                                        resourceID, resourceRow, txtTitle.Text, (DateTime)start, (DateTime)end,
                                        App.sd.loginID, txtNotes.Text, new(), new(), new(), conferenceConnections);

            ditConference.ExtractValues(out conference.additionalCols, out conference.additionalVals);

            // Obtain types and determine whether or not quotes will be needed.
            conference.additionalNeedsQuotes = new();
            foreach (string c in conference.additionalCols)
                conference.additionalNeedsQuotes.Add(
                    SqlAssist.NeedsQuotes(ColumnRecord.GetColumn(ColumnRecord.organisation, c).type));

            return App.SendInsert(Glo.CLIENT_NEW_CONFERENCE, conference);
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

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (App.SendDelete("Conference", Glo.Tab.CONFERENCE_ID, id, false))
                Close();
        }
    }
}
