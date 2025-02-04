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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace BridgeOpsClient
{
    public partial class NewTask : CustomWindow
    {
        bool edit = false;
        public string id = "";
        public bool changeMade = false;

        public NewTask()
        {
            InitializeComponent();

            dit.headers = ColumnRecord.taskHeaders;
            dit.Initialise(ColumnRecord.orderedTask, "Task");

            txtTaskRef.MaxLength = Glo.Fun.LongToInt(ColumnRecord.GetColumn(ColumnRecord.task,
                                                                            Glo.Tab.TASK_REFERENCE).restriction);
            txtNotes.MaxLength = Glo.Fun.LongToInt(ColumnRecord.GetColumn(ColumnRecord.contact,
                                                                          Glo.Tab.NOTES).restriction);

            dtgDocs.identity = (int)UserSettings.TableIndex.TaskDocument;
            dtgVisits.identity = (int)UserSettings.TableIndex.TaskVisit;
        }

        public NewTask(string id) : this()
        {
            InitializeComponent();

            edit = true;
            this.id = id;

            btnAddDoc.IsEnabled = true;
            btnAddVisit.IsEnabled = true;
            btnDelete.Visibility = Visibility.Visible;
        }

        public void Populate(List<object?> data)
        {
            txtTaskRef.Text = (string?)data[1];
            datOpened.SelectedDate = (DateTime?)data[2];
            datClosed.SelectedDate = (DateTime?)data[3];
            txtNotes.Text = (string?)data[4];

            dit.Populate(data.GetRange(5, data.Count - 5));

            PopulateDocuments();
            PopulateVisits();
        }

        public void PopulateVisits() { PopulateTable("Visit"); }
        public void PopulateDocuments() { PopulateTable("Document"); }
        public void PopulateTable(string table)
        {
            if (txtTaskRef.Text == null)
                return;

            bool doc = table == "Document";

            var dict = doc ? ColumnRecord.orderedDocument : ColumnRecord.orderedVisit;

            List<string?> colNames;
            List<List<object?>> rows;
            SelectRequest req = new(App.sd.sessionID, ColumnRecord.columnRecordID, table, false,
                                    new(), new(), new(), new(),
                                    dict.Keys.Cast<string>().ToList(), Enumerable.Repeat(string.Empty, dict.Count).ToList(),
                                    new() { Glo.Tab.TASK_REFERENCE }, new() { "=" },
                                    new() { txtTaskRef.Text }, new() { true }, new(), new(), new(),
                                    new() { doc ? Glo.Tab.DOCUMENT_DATE : Glo.Tab.VISIT_DATE },
                                    new() { true });
            if (App.SendSelectRequest(req, out colNames, out rows, this))
                (doc ? dtgDocs : dtgVisits).Update(dict, colNames, rows);
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!dit.ScoopValues())
            {
                App.Abort("Something went wrong when extracting the data from the input fields.", this);
                return;
            }

            if (txtTaskRef.Text == "")
            {
                App.Abort("You must select a task reference.", this);
                return;
            }

            string? notes = txtNotes.Text == "" ? null : txtNotes.Text;

            SendReceiveClasses.Task task = new(App.sd.sessionID, ColumnRecord.columnRecordID, txtTaskRef.Text,
                                               datOpened.SelectedDate, datClosed.SelectedDate, notes);
            dit.ExtractValues(out task.additionalCols, out task.additionalVals);
            task.additionalNeedsQuotes = dit.GetNeedsQuotes();

            if (edit)
            {
                if (!int.TryParse(id, out task.taskID))
                {
                    App.Abort("Could not ascertain the task's ID.", this);
                    return;
                }

                if (App.SendUpdate(Glo.CLIENT_UPDATE_TASK, task, this))
                {
                    if (MainWindow.pageDatabase != null)
                        MainWindow.pageDatabase.RepeatSearches((int)UserSettings.TableIndex.Task);
                    changeMade = true;
                    Close();
                }
            }
            else
            {
                if (App.SendInsert(Glo.CLIENT_NEW_TASK, task, out id, this))
                {
                    changeMade = true;
                    // Not need to call pageDatabase.RepeatSearches() here, as it can't possibly affected any other
                    // table in the application but the Organisation that added it, if there was one.
                    if (MainWindow.pageDatabase != null)
                        MainWindow.pageDatabase.RepeatSearches(11);
                    Close();
                }
            }
        }

        private void btnAddDoc_Click(object sender, RoutedEventArgs e)
        {
            NewDocument newTask = new();
            newTask.cmbTaskRef.Text = txtTaskRef.Text;
            newTask.Show();
        }

        private void btnAddVisit_Click(object sender, RoutedEventArgs e)
        {
            NewDocument newVisit = new();
            newVisit.cmbTaskRef.Text = txtTaskRef.Text;
            newVisit.Show();
        }

        private void dtg_CustomDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender == dtgDocs)
                App.EditDocument(dtgDocs.GetCurrentlySelectedID(), App.mainWindow);
            else
                App.EditVisit(dtgVisits.GetCurrentlySelectedID(), App.mainWindow);
        }
    }
}
