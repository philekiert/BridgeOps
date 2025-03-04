﻿using BridgeOpsClient.DialogWindows;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using BridgeOpsClient.CustomControls;
using System.Diagnostics;

namespace BridgeOpsClient
{
    public partial class SetParameters : CustomWindow
    {
        DataTemplate lblTemplate;
        DataTemplate txtTemplate;
        DataTemplate cmbTemplate;
        DataTemplate lstTemplate;
        DataTemplate numTemplate;
        DataTemplate dtmTemplate;
        DataTemplate datTemplate;
        DataTemplate timTemplate;
        DataTemplate chkTemplate;

        List<PageSelectStatement.Param> paramList;
        Dictionary<string, object?> duplicateParams = new();

        class FieldRow
        {
            public StackPanel stack;
            public Label description;
            public object value;
            public string? variableName;

            public FieldRow(Label description, object value)
            {
                this.description = description;
                this.value = value;

                stack = new StackPanel();
                stack.Children.Add(description);
                stack.Children.Add((UIElement)value);
            }
        }
        List<FieldRow> rows = new List<FieldRow>();

        public SetParameters(string? pageNme, List<PageSelectStatement.Param> paramList)
        {
            InitializeComponent();

            if (pageNme != null)
                lblTitle.Content = $"{pageNme} Parameters";

            this.paramList = paramList;

            lblTemplate = (DataTemplate)FindResource("fieldLbl");
            txtTemplate = (DataTemplate)FindResource("fieldTxt");
            cmbTemplate = (DataTemplate)FindResource("fieldCmb");
            lstTemplate = (DataTemplate)FindResource("fieldLst");
            numTemplate = (DataTemplate)FindResource("fieldNum");
            dtmTemplate = (DataTemplate)FindResource("fieldDtm");
            datTemplate = (DataTemplate)FindResource("fieldDat");
            timTemplate = (DataTemplate)FindResource("fieldTim");
            chkTemplate = (DataTemplate)FindResource("fieldChk");

            foreach (var param in paramList)
            {
                // If the value isn't null here, it means the user has already selected a value for this variable name
                // under another page. Don't ask for a value again.
                if (param.value != null)
                    continue;

                Label lbl = (Label)lblTemplate.LoadContent();
                lbl.Content = param.name;
                object field;
                if (param.type == PageSelectStatement.Param.Type.Text)
                    field = (TextBox)txtTemplate.LoadContent();
                else if (param.type == PageSelectStatement.Param.Type.Dropdown)
                {
                    field = (ComboBox)cmbTemplate.LoadContent();
                    ((ComboBox)field).ItemsSource = param.allowed;
                }
                else if (param.type == PageSelectStatement.Param.Type.Checklist)
                {
                    ScrollViewer scrl = (ScrollViewer)lstTemplate.LoadContent();
                    Grid grd = (Grid)scrl.Content;
                    if (param.allowed != null)
                    {
                        int i = 0;
                        foreach (string s in param.allowed)
                        {
                            grd.RowDefinitions.Add(new() { Height = new(24) });
                            CheckBox chk = new()
                            {
                                Margin = new(5, 4, 5, 0)
                            };
                            Label option = new()
                            {
                                VerticalAlignment = VerticalAlignment.Center,
                                Padding = new(5, 0, 5, 0),
                                Content = s
                            };
                            Grid.SetColumn(chk, 0);
                            Grid.SetColumn(option, 1);
                            Grid.SetRow(chk, i);
                            Grid.SetRow(option, i);
                            grd.Children.Add(chk);
                            grd.Children.Add(option);
                            ++i;
                        }
                    }
                    field = scrl;
                }
                else if (param.type == PageSelectStatement.Param.Type.Number)
                    field = (NumberEntry)numTemplate.LoadContent();
                else if (param.type == PageSelectStatement.Param.Type.DateTime)
                    field = (DateTimePicker)dtmTemplate.LoadContent();
                else if (param.type == PageSelectStatement.Param.Type.Date)
                    field = (DatePicker)datTemplate.LoadContent();
                else if (param.type == PageSelectStatement.Param.Type.Time)
                    field = (TimePicker)timTemplate.LoadContent();
                else if (param.type == PageSelectStatement.Param.Type.Bool)
                    field = (CheckBox)chkTemplate.LoadContent();
                else
                    continue;

                FieldRow fieldRow = new FieldRow(lbl, field!);
                rows.Add(fieldRow);

                if (param.variableName != null)
                {
                    fieldRow.variableName = param.variableName;

                    if (!duplicateParams.ContainsKey(param.variableName))
                    {
                        stkParams.Children.Add(fieldRow.stack);
                        duplicateParams.Add(param.variableName, null);
                    }
                }
                else
                    stkParams.Children.Add(fieldRow.stack);
            }
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            if (AssembleValues())
            {
                DialogResult = true;
                Close();
            }
        }

        private bool AssembleValues()
        {
            bool Abort(string message)
            {
                App.DisplayError(message, this);
                return false;
            }

            int i = 0;

            foreach (FieldRow row in rows)
            {
                object value;

                if (row.variableName != null &&
                    duplicateParams.ContainsKey(row.variableName) && duplicateParams[row.variableName] != null)
                    value = duplicateParams[row.variableName]!;
                else
                {
                    if (row.value == null)
                        return Abort($"You must select a value for all parameters.");

                    if (row.value is TextBox txt)
                    {
                        if (txt.Text == "")
                            return Abort($"You must select a value for all parameters.");
                        value = "'" + txt.Text.Replace("'", "''") + "'";
                    }
                    else if (row.value is ComboBox cmb)
                    {
                        if (cmb.SelectedIndex < 0)
                            return Abort($"You must select a value for all parameters.");
                        value = "'" + cmb.Text.Replace("'", "''") + "'";
                    }
                    else if (row.value is ScrollViewer scrl)
                    {
                        Grid grd = (Grid)scrl.Content;
                        List<string> values = new();
                        for (int r = 0; r < grd.Children.Count; r += 2)
                        {
                            if (((CheckBox)grd.Children[r]).IsChecked == true)
                                values.Add(((string)((Label)grd.Children[r + 1]).Content).Replace("'", "''"));
                        }
                        if (values.Count == 0)
                            return Abort($"Checkbox lists must have at least one item selected.");
                        value = $"('{string.Join("', '", values)}')";
                    }
                    else if (row.value is NumberEntry num)
                    {
                        int? number = num.GetNumber();
                        if (number == null)
                            return Abort($"You must select a value for all parameters.");
                        value = (int)number;
                    }
                    else if (row.value is DateTimePicker dtm)
                    {
                        DateTime? dt = dtm.GetDateTime();
                        if (dt == null)
                            return Abort($"You must select a value for all parameters.");
                        value = (DateTime)dt;
                    }
                    else if (row.value is DatePicker dat)
                    {
                        if (dat.SelectedDate == null)
                            return Abort($"You must select a value for all parameters.");
                        value = (DateTime)dat.SelectedDate;
                    }
                    else if (row.value is TimePicker tim)
                    {
                        TimeSpan? ts = tim.GetTime();
                        if (ts == null)
                            return Abort($"You must select a value for all parameters.");
                        value = (TimeSpan)ts;
                    }
                    else if (row.value is CheckBox chk)
                    {
                        if (chk.IsChecked == null)
                            return Abort($"You must select a value for all parameters.");
                        value = chk.IsChecked == true;
                    }
                    else
                        return Abort($"You must select a value for all parameters.");

                    if (row.variableName != null && duplicateParams.ContainsKey(row.variableName))
                        duplicateParams[row.variableName] = value;
                }

                paramList[i].value = value;

                ++i;
            }

            return true;
        }
    }
}
