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
        bool hideOrgRefs = false;

        DataTemplate btnRemoveTemplate;
        DataTemplate txtTemplate;

        int maxTaskRefLength;
        int maxOrgRefLength;

        NewTask taskWindow; // For feeding back the new reference to the task window.

        public TaskBreakOut(string taskRef, string? orgRef, NewTask taskWindow)
        {
            this.taskWindow = taskWindow;

            originalTaskRef = taskRef;
            hideOrgRefs = orgRef == null;

            InitializeComponent();
            btnRemoveTemplate = (DataTemplate)FindResource("fieldRemoveButton");
            txtTemplate = (DataTemplate)FindResource("fieldTextBox");

            if (hideOrgRefs)
            {
                grdMain.ColumnDefinitions[2].Width = new(0);
                grdFields.ColumnDefinitions[2].Width = new(0);
            }

            maxTaskRefLength = Glo.Fun.LongToInt(
                ((ColumnRecord.Column)ColumnRecord.task[Glo.Tab.TASK_REFERENCE]!).restriction);
            maxOrgRefLength = Glo.Fun.LongToInt(
                ((ColumnRecord.Column)ColumnRecord.organisation[Glo.Tab.ORGANISATION_REF]!).restriction);

            // Make one initial row that will use the original refs.
            btnAdd_Click(null, null);
            btnAdd_Click(null, null);

            if (!hideOrgRefs)
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
                grd.RowDefinitions.RemoveAt(0);
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
                if (pair.txtTask.Text.Length == 0 || taskRefs.Contains(pair.txtTask.Text) || 
                    (!hideOrgRefs && // Only consider organisation references if they're visible.
                     (pair.txtOrganisation.Text.Length == 0 || orgRefs.Contains(pair.txtOrganisation.Text))))
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
            if (hideOrgRefs)
                refPair.txtOrganisation.Visibility = Visibility.Collapsed;
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
                                App.sr.Serialise(hideOrgRefs ? new List<string>() : // Empty if task only.
                                                 referencePairs.Select(i => i.txtOrganisation.Text).ToList()));

                            int result = App.sr.ReadByte(stream);
                            if (result == Glo.CLIENT_REQUEST_SUCCESS)
                            {
                                App.DisplayError("Task broken out successfully.", this);
                                if (MainWindow.pageDatabase != null)
                                {
                                    taskWindow.Title = "Task - " + referencePairs[0].txtTask.Text;
                                    taskWindow.txtTaskRef.Text = referencePairs[0].txtTask.Text;
                                    MainWindow.pageDatabase.RepeatSearches((int)UserSettings.TableIndex.Task);
                                    MainWindow.pageDatabase.RepeatSearches((int)UserSettings.TableIndex.Organisation);
                                }
                                Close();
                                return;
                            }
                            else if (result == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                            {
                                App.DisplayError(App.ErrorConcat("Unable to break out task.",
                                                 App.sr.ReadString(stream)), this);
                                return;
                            }
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