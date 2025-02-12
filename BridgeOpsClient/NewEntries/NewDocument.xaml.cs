using DocumentFormat.OpenXml.Wordprocessing;
using SendReceiveClasses;
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
    public partial class NewDocument : CustomWindow
    {
        bool edit = false;
        public string id = "";
        public bool changeMade = false;
        List<string> multiRefs = new(); //  Used to add a visit to multiple tasks at once.

        public NewDocument()
        {
            InitializeComponent();

            dit.headers = ColumnRecord.documentHeaders;
            dit.Initialise(ColumnRecord.orderedDocument, "Document");

            txtNotes.MaxLength = Glo.Fun.LongToInt(ColumnRecord.GetColumn(ColumnRecord.document,
                                                                          Glo.Tab.NOTES).restriction);
            // cmbTaskRef max length is set in cmbTaskRef_Loaded() further down once it's loaded as it's not as simple.

            // List all known task references.
            List<string> taskRefs;
            App.GetAllTaskRefs(out taskRefs, this);
            cmbTaskRef.ItemsSource = taskRefs;

            // Store all task references of actual tasks.
            List<List<object?>> rows;
            if (App.Select("Task", new() { Glo.Tab.TASK_REFERENCE }, out _, out rows, false, this))
                knownTaskRefs = rows.Select(i => (string)i[0]!).ToHashSet(); // Type is NOT NULL in database.

            cmbType.ItemsSource = ColumnRecord.GetColumn(ColumnRecord.document, Glo.Tab.DOCUMENT_TYPE).allowed;
        }

        public NewDocument(string id) : this()
        {
            InitializeComponent();

            edit = true;
            this.id = id;

            btnDelete.Visibility = Visibility.Visible;

            Title = "Document";
        }

        public NewDocument(List<string> taskRefs) : this()
        {
            multiRefs = taskRefs;
            Title = "New Documents";
            grd.RowDefinitions[0].Height = new(0);
            cmbTaskRef.Visibility = Visibility.Collapsed;
        }

        public void Populate(List<object?> data)
        {
            if (data.Count >= 5) // Task will run this method with empty data in order to enable the task button.
            {
                cmbTaskRef.Text = (string?)data[1];
                dat.SelectedDate = (DateTime?)data[2];
                cmbType.Text = (string?)data[3];
                txtNotes.Text = (string?)data[4];

                dit.Populate(data.GetRange(5, data.Count - 5));
            }

            // Won't trigger when set above for some reason.
            if (cmbTaskRef.Text != null)
                btnTask.IsEnabled = knownTaskRefs.Contains(cmbTaskRef.Text);
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!dit.ScoopValues())
            {
                App.Abort("Something went wrong when extracting the data from the input fields.", this);
                return;
            }

            string? taskRef = cmbTaskRef.Text == "" ? null : cmbTaskRef.Text;
            string? type = cmbType.Text == "" ? null : cmbType.Text;
            string? notes = txtNotes.Text == "" ? null : txtNotes.Text;

            SendReceiveClasses.Document doc = new(App.sd.sessionID, ColumnRecord.columnRecordID, cmbTaskRef.Text,
                                                  cmbType.Text, dat.SelectedDate, notes);
            dit.ExtractValues(out doc.additionalCols, out doc.additionalVals);
            doc.additionalNeedsQuotes = dit.GetNeedsQuotes();

            if (edit)
            {
                if (!int.TryParse(id, out doc.documentID))
                {
                    App.Abort("Could not ascertain the document's ID.", this);
                    return;
                }

                if (App.SendUpdate(Glo.CLIENT_UPDATE_DOCUMENT, doc, this))
                {
                    if (MainWindow.pageDatabase != null)
                        MainWindow.pageDatabase.RepeatSearches((int)UserSettings.TableIndex.Document);
                    changeMade = true;
                    Close();
                }
            }
            else
            {
                List<SendReceiveClasses.Document> docs = new();
                if (multiRefs.Count == 0)
                    docs.Add(doc);
                else // if adding to multiple tasks at once.
                    foreach (string s in multiRefs)
                        docs.Add(doc.Clone(s));

                if (App.SendInsert(Glo.CLIENT_NEW_DOCUMENT, docs, out id, this))
                {
                    if (multiRefs.Count > 0)
                        App.DisplayError("Visit addition successful.", this);
                    changeMade = true;
                    // Not need to call pageDatabase.RepeatSearches() here, as it can't possibly affected any other
                    // table in the application but the Organisation that added it, if there was one.
                    if (MainWindow.pageDatabase != null)
                        MainWindow.pageDatabase.RepeatSearches(13);
                    Close();
                }
            }
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!App.DeleteConfirm(false, this))
                return;

            if (App.SendDelete("Document", Glo.Tab.DOCUMENT_ID, id, false, this))
            {
                changeMade = true;
                if (MainWindow.pageDatabase != null)
                    MainWindow.pageDatabase.RepeatSearches((int)UserSettings.TableIndex.Document);
                Close();
            }
        }

        // Used to store actual task references for enabling/disabling task button.
        HashSet<string> knownTaskRefs = new();

        private void txtTaskRef_TextChanged(object sender, TextChangedEventArgs e)
        {
            btnTask.IsEnabled = knownTaskRefs.Contains(txtTaskRef!.Text);
        }

        TextBox? txtTaskRef;
        private void cmbTaskRef_Loaded(object sender, RoutedEventArgs e)
        {
            if (cmbTaskRef.Template.FindName("PART_EditableTextBox", cmbTaskRef) is TextBox txt)
            {
                txt.MaxLength = Glo.Fun.LongToInt(ColumnRecord.GetColumn(ColumnRecord.visit,
                                                                         Glo.Tab.TASK_REFERENCE).restriction);
                txtTaskRef = txt;
                txtTaskRef.TextChanged += txtTaskRef_TextChanged;
            }
        }

        private void btnTask_Click(object sender, RoutedEventArgs e)
        {
            // Get the ID for the reference, then edit.
            List<List<object?>> rows;
            if (App.Select("Task", new() { Glo.Tab.TASK_ID },
                           new() { Glo.Tab.TASK_REFERENCE }, new() { txtTaskRef!.Text },
                           new() { SendReceiveClasses.Conditional.Equals }, out _, out rows, false, false, this))
            {
                if (rows.Count != 1)
                    App.DisplayError("Could not find the requested task.", this);
                App.EditTask(rows[0][0]!.ToString()!, this); // Primary key, won't be null.
            }
        }
    }
}
