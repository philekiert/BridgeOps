﻿using System;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BridgeOpsClient.CustomControls
{
    public partial class NumberEntry : UserControl
    {
        // Inclusive.
        int min = int.MinValue;
        int max = int.MaxValue;

        public NumberEntry()
        {
            InitializeComponent();
        }

        // The buttons are enabled or disabled when the value is changed, so make
        // sure the permissions are taken into account.
        bool allowIncrement = true;
        bool allowDecrement = true;
        public void ToggleEnabled(bool enabled)
        {
            txtNumber.IsReadOnly = !enabled;
            txtNumber.IsEnabled = enabled;
            allowIncrement = enabled;
            allowDecrement = enabled;

            long value;
            bool isNumber = long.TryParse(txtNumber.Text, out value);
            btnDecrement.IsEnabled = (!isNumber || value > min) && allowDecrement;
            btnIncrement.IsEnabled = (!isNumber || value < max) && allowIncrement;
        }

        public string Text { get { return txtNumber.Text; } set { txtNumber.Text = value; } }

        public void SetMinMaxToType(string type)
        {
            if (type == "TINYINT")
            {
                min = 0;
                max = 255;
            }
            else if (type == "SMALLINT")
            {
                min = Int16.MinValue;
                max = Int16.MaxValue;
            }
            else if (type == "INT")
            {
                min = Int32.MinValue;
                max = Int32.MaxValue;
            }

            int i;
            if (int.TryParse(txtNumber.Text, out i))
                if (i > max)
                    txtNumber.Text = max.ToString();
                else if (i < min)
                    txtNumber.Text = min.ToString();
        }
        
        public void SetMinMax(int min, int max)
        {
            if (min > max)
                max = min;
            this.min = min;
            this.max = max;
        }

        public int? GetNumber()
        {
            // If the user has somehow entered a value above or below the max, don't fix it here as an error will be
            // thrown when the insert takes place.

            int i;
            if (int.TryParse(txtNumber.Text, out i))
                return i;
            else
                return null;
        }

        private void btnIncrement_Click(object sender, RoutedEventArgs e)
        {
            if (txtNumber.Text == "" || txtNumber.Text == "-")
                txtNumber.Text = max < 1 ? max.ToString() : "1";
            else
            {
                int i;
                if (int.TryParse(txtNumber.Text.ToString(), out i))
                {
                    ++i;
                    txtNumber.Text = i > max ? max.ToString() : i.ToString();
                }
            }

        }

        private void btnDecrement_Click(object sender, RoutedEventArgs e)
        {
            if (txtNumber.Text == "" || txtNumber.Text == "-")
                txtNumber.Text = min > 0 ? min.ToString() : "0";
            else
            {
                int i;
                if (int.TryParse(txtNumber.Text.ToString(), out i))
                {
                    --i;
                    txtNumber.Text = i < min ? min.ToString() : i.ToString();
                }
            }
        }

        string lastVal = "";
        bool updating = false;
        private void txtNumber_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (updating)
                return;
            updating = true;

            // Disallow and cancel if the change would make the text alphanumeric.

            long value;
            int selectionStart = txtNumber.SelectionStart;

            if (txtNumber.Text == "" || txtNumber.Text == "-")
                lastVal = txtNumber.Text;
            else if (!long.TryParse(txtNumber.Text, out value))
            {
                txtNumber.Text = lastVal;
                if (selectionStart <= txtNumber.Text.Length)
                    txtNumber.SelectionStart = selectionStart;
                else
                    txtNumber.SelectionStart = txtNumber.Text.Length;
            }
            else
            {
                if (value < min)
                    value = min;
                else if (value > max)
                    value = max;
                txtNumber.Text = value.ToString();
                lastVal = txtNumber.Text;
                if (selectionStart <= txtNumber.Text.Length)
                    txtNumber.SelectionStart = selectionStart;
                else
                    txtNumber.SelectionStart = txtNumber.Text.Length;
            }

            bool isNumber = long.TryParse(txtNumber.Text, out value);
            btnDecrement.IsEnabled = (!isNumber || value > min) && allowDecrement;
            btnIncrement.IsEnabled = (!isNumber || value < max) && allowIncrement;

            updating = false;
        }

        private void txtNumber_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!char.IsDigit(e.Text[0]) && e.Text != "-")
                e.Handled = true;
        }
    }
}
