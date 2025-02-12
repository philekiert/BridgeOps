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
    public partial class NewVisit : CustomWindow
    {
        bool edit = false;
        public string id = "";
        public bool changeMade = false;
        List<string> multiRefs = new(); //  Used to add a visit to multiple tasks at once.

        public NewVisit()
        {
            InitializeComponent();

            dit.headers = ColumnRecord.visitHeaders;
            dit.Initialise(ColumnRecord.orderedVisit, "Visit");

            txtNotes.MaxLength = Glo.Fun.LongToInt(ColumnRecord.GetColumn(ColumnRecord.visit,
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

            cmbType.ItemsSource = ColumnRecord.GetColumn(ColumnRecord.visit, Glo.Tab.VISIT_TYPE).allowed;
        }

        public NewVisit(string id) : this()
        {
            edit = true;
            this.id = id;

            btnDelete.Visibility = Visibility.Visible;

            Title = "Visit";
        }

        public NewVisit(List<string> taskRefs) : this()
        {
            multiRefs = taskRefs;
            Title = "New Visits";

            txtTaskRef!.Visibility = Visibility.Collapsed;
            grd.RowDefinitions[0].Height = new(0);
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

                if (App.SendUpdate(Glo.CLIENT_UPDATE_VISIT, visit, this))
                {
                    if (MainWindow.pageDatabase != null)
                        MainWindow.pageDatabase.RepeatSearches((int)UserSettings.TableIndex.Visit);
                    changeMade = true;
                    Close();
                }
            }
            else
            {
                List<SendReceiveClasses.Visit> visits = new();
                if (multiRefs.Count == 0)
                    visits.Add(visit);
                else // if adding to multiple tasks at once.
                    foreach (string s in multiRefs)
                        visits.Add(visit.Clone(s));

                if (App.SendInsert(Glo.CLIENT_NEW_VISIT, visits, out id, this))
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

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!App.DeleteConfirm(false, this))
                return;

            if (App.SendDelete("Visit", Glo.Tab.VISIT_ID, id, false, this))
            {
                changeMade = true;
                if (MainWindow.pageDatabase != null)
                    MainWindow.pageDatabase.RepeatSearches((int)UserSettings.TableIndex.Visit);
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
