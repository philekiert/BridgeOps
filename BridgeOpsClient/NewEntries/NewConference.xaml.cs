using SendReceiveClasses;
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

namespace BridgeOpsClient
{
    public partial class NewConference : Window
    {
        public NewConference(PageConferenceView.ResourceInfo? resource, DateTime start)
        {
            MaxHeight = 400;

            InitializeComponent();

            dtpStart.datePicker.SelectedDateChanged += ToggleConnectionDates;
            dtpEnd.datePicker.SelectedDateChanged += ToggleConnectionDates;

            dtpStart.SetDateTime(start);
            dtpEnd.SetDateTime(start.AddHours(1));

            ToggleConnectionDates(null, null);

            // Populate available resources and select whichever one the user clicked on in the schedule view.
            cmbResource.ItemsSource = PageConferenceView.resourceRowNames;
            if (resource == null)
                App.DisplayError("Could not determine resource from selected row, please set manually.");
            else
                cmbResource.SelectedIndex = resource.SelectedRowTotal;

            ditConference.headers = ColumnRecord.organisationHeaders;
            ditConference.Initialise(ColumnRecord.orderedOrganisation, "Organisation");
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
                    Content = "-",
                    Width = 24,
                    Height = 24,
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

        private void btnAddConnection_Click(object sender, RoutedEventArgs e)
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

                App.Select("Organisation", new() { Glo.Tab.DIAL_NO,
                                                   Glo.Tab.ORGANISATION_REF,
                                                   Glo.Tab.ORGANISATION_NAME,
                                                   Glo.Tab.ORGANISATION_ID },
                                           new() { Glo.Tab.ORGANISATION_REF, Glo.Tab.DIAL_NO },
                                           new() { txt.Text,
                                                   txt.Text },
                                           new() { Conditional.Equals, Conditional.Equals },
                                           out _, out rows,
                                           false, false);

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
        }

        private void btnSummary_Click(object sender, EventArgs e)
        {
            int index = Grid.GetRow((UIElement)sender) - 1;
            Connection connection = connections[index];

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                connection.ToggleSearch(true);
                connection.txtSearch.Focus();
            }
            else if (connection.orgId != null)
            {
                App.EditOrganisation(connection.orgId.ToString()!);
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
                grdConnections.ColumnDefinitions[3].Width = new GridLength(175);
                grdConnections.ColumnDefinitions[4].Width = new GridLength(175);
                grdConnections.ColumnDefinitions[3].MaxWidth = 175;
                grdConnections.ColumnDefinitions[4].MaxWidth = 175;
            }
        }
    }
}
