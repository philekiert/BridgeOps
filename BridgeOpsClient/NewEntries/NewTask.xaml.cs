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

        }

        public void PopulateVisits()
        {

        }

        public void PopulateDocuments()
        {

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
    }
}
