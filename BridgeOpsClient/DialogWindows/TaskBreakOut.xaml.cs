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

namespace BridgeOpsClient
{
    public partial class TaskBreakOut : CustomWindow
    {
        DataTemplate btnRemoveTemplate;
        DataTemplate txtTemplate;

        int maxTaskRefLength;
        int maxOrgRefLength;

        public TaskBreakOut(string taskRef, string? orgRef)
        {
            InitializeComponent();
            btnRemoveTemplate = (DataTemplate)FindResource("fieldRemoveButton");
            txtTemplate = (DataTemplate)FindResource("fieldTextBox");

            maxTaskRefLength = Glo.Fun.LongToInt(
                ((ColumnRecord.Column)ColumnRecord.task[Glo.Tab.TASK_REFERENCE]!).restriction);
            maxOrgRefLength = Glo.Fun.LongToInt(
                ((ColumnRecord.Column)ColumnRecord.organisation[Glo.Tab.ORGANISATION_REF]!).restriction);

            // Make one initial row that will use the original refs.
            btnAdd_Click(null, null);
            referencePairs[0].txtTask.Text = taskRef;
            referencePairs[0].txtOrganisation.Text = orgRef;
            referencePairs[0].btnRemove.IsEnabled = false;
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

        private void btnAdd_Click(object? sender, RoutedEventArgs? e)
        {
            int row = referencePairs.Count;

            grdFields.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

            ReferencePair refPair = new();
            refPair.btnRemove = (btnRemoveTemplate.LoadContent() as Button)!;
            refPair.txtTask = (txtTemplate.LoadContent() as TextBox)!;
            refPair.txtOrganisation = (txtTemplate.LoadContent() as TextBox)!;
            refPair.txtTask.MaxLength = maxTaskRefLength;
            refPair.txtOrganisation.MaxLength = maxOrgRefLength;
            refPair.SetGridRow(row);
            Grid.SetColumn(refPair.txtTask, 1);
            Grid.SetColumn(refPair.txtOrganisation, 2);

            referencePairs.Add(refPair);

            grdFields.Children.Add(refPair.btnRemove);
            grdFields.Children.Add(refPair.txtTask);
            grdFields.Children.Add(refPair.txtOrganisation);

            if (referencePairs.Count == 2)
                referencePairs[0].btnRemove.IsEnabled = true;
        }

        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            int index = Grid.GetRow((Button)sender);

            referencePairs[index].RemoveFromGrid(grdFields);
            referencePairs.RemoveAt(index);
            for (int i = index; i < referencePairs.Count; ++i)
                referencePairs[i].SetGridRow(i);

            if (referencePairs.Count == 1)
                referencePairs[0].btnRemove.IsEnabled = false;
        }

        private void btnBreakOut_Click(object sender, RoutedEventArgs e)
        {
            
        }
    }
}
