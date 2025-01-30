﻿using DocumentFormat.OpenXml.Wordprocessing;
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

        public NewDocument()
        {
            InitializeComponent();

            dit.headers = ColumnRecord.documentHeaders;
            dit.Initialise(ColumnRecord.orderedDocument, "Document");

            txtNotes.MaxLength = Glo.Fun.LongToInt(ColumnRecord.GetColumn(ColumnRecord.document,
                                                                          Glo.Tab.NOTES).restriction);
            // cmbTaskRef max length is set in cmbTaskRef_Loaded() further down once it's loaded as it's not as simple.

            cmbType.ItemsSource = ColumnRecord.GetColumn(ColumnRecord.document, Glo.Tab.DOCUMENT_TYPE).allowed;
        }

        public NewDocument(string id) : this()
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
            }
            else
            {
                if (App.SendInsert(Glo.CLIENT_NEW_DOCUMENT, doc, out id, this))
                {
                    changeMade = true;
                    // Not need to call pageDatabase.RepeatSearches() here, as it can't possibly affected any other
                    // table in the application but the Organisation that added it, if there was one.
                    if (MainWindow.pageDatabase != null)
                        MainWindow.pageDatabase.RepeatSearches(13);
                    Close();
                }
            }
        }

        private void cmbType_Loaded(object sender, RoutedEventArgs e)
        {
            if (cmbTaskRef.Template.FindName("PART_EditableTextBox", cmbTaskRef) is TextBox txt)
                txt.MaxLength = Glo.Fun.LongToInt(ColumnRecord.GetColumn(ColumnRecord.document,
                                                                         Glo.Tab.TASK_REFERENCE).restriction);
        }
    }
}
