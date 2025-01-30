using DocumentFormat.OpenXml.Wordprocessing;
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
    public partial class NewVisit : CustomWindow
    {
        bool edit = false;
        public string id = "";
        public bool changeMade = false;

        public NewVisit()
        {
            InitializeComponent();

            dit.headers = ColumnRecord.visitHeaders;
            dit.Initialise(ColumnRecord.orderedVisit, "Visit");

            txtNotes.MaxLength = Glo.Fun.LongToInt(ColumnRecord.GetColumn(ColumnRecord.visit,
                                                                          Glo.Tab.NOTES).restriction);
            // cmbTaskRef max length is set in cmbTaskRef_Loaded() further down once it's loaded as it's not as simple.

            cmbType.ItemsSource = ColumnRecord.GetColumn(ColumnRecord.visit, Glo.Tab.VISIT_TYPE).allowed;
        }

        public NewVisit(string id) : this()
        {
            InitializeComponent();

            edit = true;
            this.id = id;

            btnDelete.Visibility = Visibility.Visible;
        }

        public void Populate(List<object> data)
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

            string? taskRef = cmbTaskRef.Text == "" ? null : cmbTaskRef.Text;
            string? type = cmbType.Text == "" ? null : cmbType.Text;
            string? notes = txtNotes.Text == "" ? null : txtNotes.Text;

            SendReceiveClasses.Visit visit = new(App.sd.sessionID, ColumnRecord.columnRecordID, cmbTaskRef.Text,
                                                 cmbType.Text, dat.SelectedDate, notes);
            dit.ExtractValues(out visit.additionalCols, out visit.additionalVals);
            visit.additionalNeedsQuotes = dit.GetNeedsQuotes();

            if (edit)
            {
                if (!int.TryParse(id, out visit.visitID))
                {
                    App.Abort("Could not ascertain the visit's ID.", this);
                    return;
                }
            }
            else
            {
                if (App.SendInsert(Glo.CLIENT_NEW_VISIT, visit, out id, this))
                {
                    changeMade = true;
                    // Not need to call pageDatabase.RepeatSearches() here, as it can't possibly affected any other
                    // table in the application but the Organisation that added it, if there was one.
                    if (MainWindow.pageDatabase != null)
                        MainWindow.pageDatabase.RepeatSearches(12);
                    Close();
                }
            }
        }

        private void cmbType_Loaded(object sender, RoutedEventArgs e)
        {
            if (cmbTaskRef.Template.FindName("PART_EditableTextBox", cmbTaskRef) is TextBox txt)
                txt.MaxLength = Glo.Fun.LongToInt(ColumnRecord.GetColumn(ColumnRecord.visit,
                                                                         Glo.Tab.TASK_REFERENCE).restriction);
        }
    }
}
