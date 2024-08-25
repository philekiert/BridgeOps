﻿using DocumentFormat.OpenXml.Drawing.Wordprocessing;
using System;
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

namespace BridgeOpsClient
{
    public partial class ReorderColumns : Window
    {
        public ReorderColumns()
        {
            InitializeComponent();

            GetEntryLists();
            ChangeTable();

            cmbTable.Focus();
        }

        class Entry
        {
            public int index;
            public int displayIndex;
            public string name;
            public string Name { get { return name; } }
            public enum Kind { column, header, integral }
            public Kind kind;
            public bool Greyed { get { return kind == Kind.integral; } }
            public bool Bold { get { return kind == Kind.header; } }
            public Entry(int index, int displayIndex, string name, Kind kind)
            {
                this.index = index;
                this.displayIndex = displayIndex;
                this.name = name;
                this.kind = kind;
            }
        }
        // 0: Organisation
        // 1: Asset
        // 2: Contact
        // 3: Conference
        List<Entry>[] entryLists = new List<Entry>[4];
        List<Entry>[] entryListOriginals = new List<Entry>[4];
        List<Entry> entries = new();
        List<Entry> entriesOriginal = new();

        int upperLimit = 0;
        int lowerLimit = 0;

        private void GetEntryLists()
        {
            for (int i = 0; i < entryLists.Length; ++i)
            {
                entryLists[i] = new();
                entryListOriginals[i] = new();
            }

            Dictionary<string, ColumnRecord.Column> dictionary;
            List<int> order;
            for (int n = 0; n < 4; ++n)
            {
                if (n == 0)
                {
                    dictionary = ColumnRecord.organisation;
                    order = ColumnRecord.organisationOrder;
                }
                else if (n == 1)
                {
                    dictionary = ColumnRecord.asset;
                    order = ColumnRecord.assetOrder;
                }
                else if (n == 2)
                {
                    dictionary = ColumnRecord.contact;
                    order = ColumnRecord.contactOrder;
                }
                else // if 3
                {
                    dictionary = ColumnRecord.conference;
                    order = ColumnRecord.conferenceOrder;
                }

                string[] table = new string[] { "Organisation", "Asset", "Contact", "Conference" };
                int i = 0;
                foreach (KeyValuePair<string, ColumnRecord.Column> kvp in dictionary)
                {
                    string printName = ColumnRecord.GetPrintName(kvp);
                    Entry.Kind type = Glo.Fun.ColumnRemovalAllowed(table[n], kvp.Key) ? Entry.Kind.column :
                                                                                        Entry.Kind.integral;
                    entryLists[n].Add(new Entry(i, order[i], printName, type));
                    entryListOriginals[n].Add(new Entry(i, order[i], printName, type));
                    ++i;
                }

                entryLists[n] = entryLists[n].OrderBy(e => e.displayIndex).ToList();
                entryListOriginals[n] = entryListOriginals[n].OrderBy(e => e.displayIndex).ToList();
            }
        }

        private void ChangeTable()
        {
            if (lstColumns == null)
                return;

            entries = entryLists[cmbTable.SelectedIndex];
            entriesOriginal = entryListOriginals[cmbTable.SelectedIndex];

            lstColumns.ItemsSource = entries;

            if (cmbTable.SelectedIndex == 0)
                lowerLimit = Glo.Tab.ORGANISATION_STATIC_COUNT;
            else if (cmbTable.SelectedIndex == 1)
                lowerLimit = Glo.Tab.ASSET_STATIC_COUNT;
            else if (cmbTable.SelectedIndex == 2)
                lowerLimit = Glo.Tab.CONTACT_STATIC_COUNT;
            else // if 4
                lowerLimit = Glo.Tab.CONFERENCE_STATIC_COUNT;

            upperLimit = lstColumns.Items.Count - 1;
        }

        private void btnUp_Click(object sender, RoutedEventArgs e) { MoveItem(false); }
        private void btnDown_Click(object sender, RoutedEventArgs e) { MoveItem(true); }

        private void MoveItem(bool down)
        {
            List<Entry> selectedItems = new();
            foreach (Entry entry in lstColumns.SelectedItems)
                selectedItems.Add(entry);

            if (selectedItems.Count == 0)
                return;

            List<int> selectedIndices = SelectedIndices();
            int start = selectedIndices[0];
            int length = selectedIndices.Count;

            if (down && start + length > upperLimit)
                return;
            else if (!down && start == lowerLimit)
                return;

            // Displace the item either above or below the set. The user's selection is restricted to contiguous items.
            Entry e = entries[down ? start + length : start - 1];
            entries.RemoveAt(down ? start + length : start - 1);
            entries.Insert(down ? start : start + length - 1, e);

            // Update each entry's ordered index.
            for (int i = 0; i < entries.Count; ++i)
                (entries[i]).displayIndex = i;

            ReapplyIndices();
            QuickRefreshList();

            // Reselect
            for (int i = 0; i < selectedIndices.Count; ++i)
                lstColumns.SelectedItems.Add(lstColumns.Items[selectedIndices[i] + (down ? 1 : -1)]);

            // Mark the table in bold if changes have been made.
            bool changed = false;
            for (int i = 0; i < entriesOriginal.Count && i < entries.Count; ++i)
                if (entries[i].displayIndex != entriesOriginal[i].displayIndex ||
                    entries[i].name != entriesOriginal[i].name)
                {
                    ((ComboBoxItem)cmbTable.Items[cmbTable.SelectedIndex]).FontWeight = FontWeights.Bold;
                    cmbTable.FontWeight = FontWeights.Bold;
                    changed = true;
                    break;
                }

            if (!changed)
            {
                ((ComboBoxItem)cmbTable.Items[cmbTable.SelectedIndex]).FontWeight = FontWeights.Normal;
                cmbTable.FontWeight = FontWeights.Normal;
            }

            // To see if anything changed, we can just check to see if any tables are bold.
            for (int n = 0; n < 4; ++n)
                if (((ComboBoxItem)cmbTable.Items[n]).FontWeight == FontWeights.Bold)
                {
                    btnApply.IsEnabled = true;
                    return;
                }
            btnApply.IsEnabled = false;
        }

        bool updatingSelection = false;
        List<object> lastSelection = new();
        private void lstColumns_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!updatingSelection)
            {
                List<int> indices = SelectedIndices();

                if (lstColumns.SelectedItems.Count > 1)
                {
                    // Cancel the selection if the user tries to select something that isn't adjacent.
                    for (int i = 0; i < indices.Count - 1; ++i)
                    {
                        if (indices[i] != indices[i + 1] - 1)
                        {
                            updatingSelection = true;
                            lstColumns.SelectedItems.Clear();
                            try
                            {
                                foreach (object o in lastSelection)
                                    lstColumns.SelectedItems.Add(o);
                            }
                            catch { } // No need for a catch, just clear the selected items.
                            updatingSelection = false;
                            return;
                        }
                    }
                }

                btnUp.IsEnabled = indices.Count > 0 && indices[0] > lowerLimit;
                btnDown.IsEnabled = indices.Count > 0 && indices[0] > lowerLimit - 1 &&
                                    indices[indices.Count - 1] < upperLimit;

                lastSelection.Clear();
                foreach (object o in lstColumns.SelectedItems)
                    lastSelection.Add(o);
            }

            // Enable/disable the header remove button.
            bool allheaders = true;
            if (lstColumns.SelectedItems.Count > 0)
                foreach (Entry entry in lstColumns.SelectedItems)
                    if (entry.kind != Entry.Kind.header)
                    {
                        allheaders = false;
                        break;
                    }
            btnRemoveHeader.IsEnabled = allheaders;
        }

        private List<int> SelectedIndices()
        {
            List<int> indices = new();
            foreach (object o in lstColumns.SelectedItems)
                indices.Add(lstColumns.Items.IndexOf(o));
            indices.Sort();
            return indices;
        }

        private void cmbTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ChangeTable();

            cmbTable.FontWeight = ((ComboBoxItem)cmbTable.Items[cmbTable.SelectedIndex]).FontWeight;
        }

        public bool changeMade = false;
        private void btnApply_Click(object sender, RoutedEventArgs e)
        {
            // Legal values are checked rigorously by the agent, so we don't need to worry so much about that here.
            // The button will also not be enabled if there have been no changes.

            // Start by assembling the lists of orderings and headers.

            List<int> organisationOrder = new();
            List<int> assetOrder = new();
            List<int> contactOrder = new();
            List<int> conferenceOrder = new();

            List<SendReceiveClasses.ColumnOrdering.Header> organisationHeaders = new();
            List<SendReceiveClasses.ColumnOrdering.Header> assetHeaders = new();
            List<SendReceiveClasses.ColumnOrdering.Header> contactHeaders = new();
            List<SendReceiveClasses.ColumnOrdering.Header> conferenceHeaders = new();

            for (int n = 0; n < 4; ++n)
            {
                List<int> order;
                List<SendReceiveClasses.ColumnOrdering.Header> headers;
                if (n == 0)
                {
                    order = organisationOrder;
                    headers = organisationHeaders;
                }
                else if (n == 1)
                {
                    order = assetOrder;
                    headers = assetHeaders;
                }
                else if (n == 2)
                {
                    order = contactOrder;
                    headers = contactHeaders;
                }
                else // if 3
                {
                    order = conferenceOrder;
                    headers = conferenceHeaders;
                }

                int i = 0;
                int headersFound = 0; // Used as a modifier to make sure we're referencing correct indices.
                foreach (Entry entry in entryLists[n])
                {
                    if (entry.kind == Entry.Kind.header)
                    {
                        headers.Add(new(entry.displayIndex - headersFound, entry.name));
                        ++headersFound;
                    }
                    else
                        order.Add(entry.index); // orderIndex has been calculated ignoring headers.
                    ++i;
                }
            }

            lock (App.streamLock)
            {
                NetworkStream? stream = App.sr.NewClientNetworkStream(App.sd.ServerEP);
                try
                {
                    if (stream != null)
                    {
                        stream.WriteByte(Glo.CLIENT_COLUMN_ORDER_UPDATE);

                        SendReceiveClasses.ColumnOrdering newOrdering = new(App.sd.sessionID,
                                                                            ColumnRecord.columnRecordID,
                                                                            organisationOrder,
                                                                            assetOrder,
                                                                            contactOrder,
                                                                            conferenceOrder);

                        App.sr.WriteAndFlush(stream, App.sr.Serialise(newOrdering));
                        int response = stream.ReadByte();
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                        {
                            changeMade = true;
                            Close();
                            return;
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
                            MessageBox.Show("Only admins can reorder columns.");
                            return;
                        }
                        else
                            throw new Exception();
                    }
                    else
                        MessageBox.Show("Could not create network stream.");
                }
                catch
                {
                    MessageBox.Show("Could not apply new column order.");
                    return;
                }
                finally
                {
                    if (stream != null) stream.Close();
                }
            }
        }

        private void btnAddHeader_Click(object sender, RoutedEventArgs e)
        {
            Entry newColumn = new(entries.Count, entries.Count, "New Column", Entry.Kind.header);
            entries.Add(newColumn);
            ReapplyIndices();
            QuickRefreshList();
            lstColumns.SelectedItem = newColumn;
            lstColumns.ScrollIntoView(newColumn);
        }

        private void lstColumns_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstColumns.SelectedItems.Count != 1)
                return;
            if (((Entry)lstColumns.SelectedItem).kind != Entry.Kind.header)
                return;

            Entry entry = (Entry)lstColumns.SelectedItem;

            ReorderColumnsRenameHeader renameHeader = new(entry.name);
            if (renameHeader.ShowDialog() == false)
                return;
            entries[entry.displayIndex].name = renameHeader.name;
            QuickRefreshList();
            lstColumns.SelectedItem = entry;
        }

        private void ReapplyIndices()
        {
            for (int i = 0; i < entries.Count; ++i)
                entries[i].displayIndex = i;
        }

        private void QuickRefreshList()
        {
            lstColumns.ItemsSource = null;
            lstColumns.ItemsSource = entries;
        }

        private void btnRemoveHeader_Click(object sender, RoutedEventArgs e)
        {
            if (lstColumns.SelectedItems.Count == 0)
                return;

            List<Entry> selected = lstColumns.SelectedItems.Cast<Entry>().ToList();
            foreach (Entry entry in selected)
                entries.Remove(entry);

            ReapplyIndices();
            QuickRefreshList();
        }
    }
}