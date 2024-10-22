using DocumentFormat.OpenXml.Office2010.Excel;
using System;
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
using System.Windows.Shapes;
using SendReceiveClasses;

namespace BridgeOpsClient.DialogWindows
{
    public partial class AdjustConferenceConnections : CustomWindow
    {
        List<string> ids;

        public AdjustConferenceConnections(List<string> ids)
        {
            this.ids = ids;

            if (ids.Count == 0)
            {
                App.DisplayError("No conferences selected.");
                Close();
            }

            InitializeComponent();

            string dialNoFriendly = ColumnRecord.GetPrintName(Glo.Tab.DIAL_NO,
                                        (ColumnRecord.Column)ColumnRecord.organisation[Glo.Tab.DIAL_NO]!);
            string orgRefFriendly = ColumnRecord.GetPrintName(Glo.Tab.DIAL_NO,
                                        (ColumnRecord.Column)ColumnRecord.organisation[Glo.Tab.DIAL_NO]!);
            string orgNameFriendly = ColumnRecord.GetPrintName(Glo.Tab.DIAL_NO,
                                        (ColumnRecord.Column)ColumnRecord.organisation[Glo.Tab.DIAL_NO]!);

            SelectResult res;
            if (!App.SendConnectionSelectRequest(ids, out res))
                Close();

            res.columnNames = new() { "Test", dialNoFriendly, orgRefFriendly, orgNameFriendly, "Presence" };
            dtgRemove.Update(res.columnNames, res.rows);
        }

        private bool Adjust()
        {
            SendReceiveClasses.ConferenceAdjustment req = new();

            req.ids = ids.Select(int.Parse).ToList();
            req.intent = SendReceiveClasses.ConferenceAdjustment.Intent.Connections;

            if (App.SendConferenceAdjustment(req))
            {
                Close();
                return true;
            }
            else
                return false;
        }

        private void btnAdjust_Click(object sender, RoutedEventArgs e)
        {
            Adjust();
        }

        public class Connection
        {
            public Button btnRemove;
            public Button btnUp;
            public Button btnDown;
            public TextBox txtSearch;
            public CheckBox chkIsTest;
            public Button btnOrgSummary;
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

                Grid.SetColumnSpan(txtSearch, 4);

                Grid.SetColumn(btnRemove, 0);
                Grid.SetColumn(btnUp, 1);
                Grid.SetColumn(btnDown, 1);
                Grid.SetColumn(txtSearch, 2);
                Grid.SetColumn(chkIsTest, 2);
                Grid.SetColumn(btnOrgSummary, 5);

                grd.RowDefinitions.Add(new() { Height = GridLength.Auto });
                grd.Children.Add(btnRemove);
                grd.Children.Add(btnUp);
                grd.Children.Add(btnDown);
                grd.Children.Add(txtSearch);
                grd.Children.Add(chkIsTest);
                grd.Children.Add(btnOrgSummary);

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
                    btnOrgSummary.Content = tbDialNo;

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
                    btnOrgSummary.Visibility = Visibility.Hidden;
                }
                else
                {
                    txtSearch.Visibility = Visibility.Hidden;
                    chkIsTest.Visibility = Visibility.Visible;
                    btnOrgSummary.Visibility = Visibility.Visible;
                }
            }

            public void SetRow(int row, int connectionCount)
            {
                Grid.SetRow(btnRemove, row);
                Grid.SetRow(btnUp, row);
                Grid.SetRow(btnDown, row);
                Grid.SetRow(txtSearch, row);
                Grid.SetRow(chkIsTest, row);
                Grid.SetRow(btnOrgSummary, row);

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
                grid.Children.Remove(btnOrgSummary);
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

            connection.btnRemove.Style = (Style)FindResource("minus-button");

            connections.Add(connection);

            connection.txtSearch.Focus();

            ResetConnectionGridRows();
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
                        App.DisplayError("Cannot add duplicate dial numbers to a conference.", "Duplicate Dial No");
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
                SearchSite(sender, e);
            else if (e.Key == Key.Escape)
            {
                Connection connection = connections[Grid.GetRow((TextBox)sender)];
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
            int index = Grid.GetRow((UIElement)sender);
            Connection connection = connections[index];

            // If the user lacks edit permissions and can't make changes anyway, just load the organisation.
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) ||
                !App.sd.editPermissions[Glo.PERMISSION_CONFERENCES])
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
    }
}
