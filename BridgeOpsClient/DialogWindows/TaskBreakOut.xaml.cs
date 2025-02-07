using DocumentFormat.OpenXml.Drawing.Charts;
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
    public partial class TaskBreakOut : CustomWindow
    {
        string originalTaskRef;
        bool displayOrgRefs = false;

        DataTemplate btnRemoveTemplate;
        DataTemplate txtTemplate;

        int maxTaskRefLength;
        int maxOrgRefLength;

        public TaskBreakOut(string taskRef, string? orgRef)
        {
            originalTaskRef = taskRef;
            displayOrgRefs = orgRef != null;

            InitializeComponent();
            btnRemoveTemplate = (DataTemplate)FindResource("fieldRemoveButton");
            txtTemplate = (DataTemplate)FindResource("fieldTextBox");

            maxTaskRefLength = Glo.Fun.LongToInt(
                ((ColumnRecord.Column)ColumnRecord.task[Glo.Tab.TASK_REFERENCE]!).restriction);
            maxOrgRefLength = Glo.Fun.LongToInt(
                ((ColumnRecord.Column)ColumnRecord.organisation[Glo.Tab.ORGANISATION_REF]!).restriction);

            // Make one initial row that will use the original refs.
            btnAdd_Click(null, null);
            btnAdd_Click(null, null);

            if (displayOrgRefs)
                referencePairs[0].txtOrganisation.Text = orgRef;
        }

        struct ReferencePair
        {
            public TextBox txtTask;
            public TextBox txtOrganisation;
            public Button btnRemove;

            public void RemoveFromGrid(Grid grd)
            {
                grd.Children.Remove(txtTask);
                grd.Children.Remove(txtOrganisation);
                grd.Children.Remove(btnRemove);
            }

            public void SetGridRow(int row)
            {
                Grid.SetRow(btnRemove, row);
                Grid.SetRow(txtTask, row);
                Grid.SetRow(txtOrganisation, row);
            }
        }
        List<ReferencePair> referencePairs = new();

        public void ValueChanged(object sender, TextChangedEventArgs e)
        {
            btnBreakOut.IsEnabled = CheckForLegalValues();
        }
        public bool CheckForLegalValues()
        {
            // Cycle through the text fields and disable the Break Out button if a duplicate or empty value is found.

            HashSet<string> taskRefs = new();
            HashSet<string> orgRefs = new();

            foreach (ReferencePair pair in referencePairs)
            {
                if (pair.txtTask.Text.Length == 0 || pair.txtOrganisation.Text.Length == 0 ||
                    taskRefs.Contains(pair.txtTask.Text) || orgRefs.Contains(pair.txtOrganisation.Text))
                {
                    btnBreakOut.IsEnabled = false;
                    return false;
                }
                taskRefs.Add(pair.txtTask.Text);
                orgRefs.Add(pair.txtOrganisation.Text);
            }
            return true;
        }

        private void btnAdd_Click(object? sender, RoutedEventArgs? e)
        {
            int row = referencePairs.Count;

            grdFields.RowDefinitions.Add(new RowDefinition() { Height = new(29) });

            ReferencePair refPair = new();
            refPair.btnRemove = (btnRemoveTemplate.LoadContent() as Button)!;
            refPair.txtTask = (txtTemplate.LoadContent() as TextBox)!;
            refPair.txtOrganisation = (txtTemplate.LoadContent() as TextBox)!;
            refPair.txtTask.MaxLength = maxTaskRefLength;
            refPair.txtOrganisation.MaxLength = maxOrgRefLength;
            refPair.SetGridRow(row);
            Grid.SetColumn(refPair.txtTask, 1);
            Grid.SetColumn(refPair.txtOrganisation, 2);

            refPair.txtTask.Text = $"{originalTaskRef}-{row + 1}";

            referencePairs.Add(refPair);

            grdFields.Children.Add(refPair.btnRemove);
            grdFields.Children.Add(refPair.txtTask);
            grdFields.Children.Add(refPair.txtOrganisation);

            if (referencePairs.Count > 2)
            {
                referencePairs[0].btnRemove.IsEnabled = true;
                referencePairs[1].btnRemove.IsEnabled = true;
            }
        }

        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            int index = Grid.GetRow((Button)sender);

            referencePairs[index].RemoveFromGrid(grdFields);
            referencePairs.RemoveAt(index);
            for (int i = index; i < referencePairs.Count; ++i)
                referencePairs[i].SetGridRow(i);

            grdFields.Children.RemoveAt(0);


            if (referencePairs.Count <= 2)
            {
                referencePairs[0].btnRemove.IsEnabled = false;
                referencePairs[1].btnRemove.IsEnabled = false;
            }
        }

        private void btnBreakOut_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckForLegalValues())
            {
                App.DisplayError("All task and organisation references must be present and unique.", this);
                return;
            }

            lock (App.streamLock)
            {
                using NetworkStream? stream = App.sr.NewClientNetworkStream(App.sd.ServerEP);
                {
                    try
                    {
                        if (stream != null)
                        {
                            stream.WriteByte(Glo.CLIENT_TASK_BREAKOUT);

                            App.sr.WriteAndFlush(stream, App.sd.sessionID);
                            // No need to send the column record ID we only care about the task and organisation refs.

                            // Send the task reference to be broken out, then the new desired references.
                            App.sr.WriteAndFlush(stream, originalTaskRef);
                            App.sr.WriteAndFlush(stream,
                                App.sr.Serialise(referencePairs.Select(i => i.txtTask.Text).ToList()));
                            App.sr.WriteAndFlush(stream,
                                App.sr.Serialise(referencePairs.Select(i => i.txtOrganisation.Text).ToList()));
                        }
                        throw new Exception();
                    }
                    catch
                    {
                        App.DisplayError("Could not create new tasks and organisations.", this);
                    }
                    finally
                    {
                        if (stream != null) stream.Close();
                    }
                }
            }
        }
    }
}