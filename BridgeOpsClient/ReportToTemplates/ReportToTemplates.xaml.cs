using ClosedXML.Excel;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Office2010.ExcelAc;
using DocumentFormat.OpenXml.Wordprocessing;
using SendReceiveClasses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml;

namespace BridgeOpsClient
{
    public partial class ReportToTemplates : CustomWindow
    {
        public ReportToTemplates(bool startWithBuilder)
        {
            InitializeComponent();

            // Configure preset bar.
            PresetLoad(true);
            cmbPresets.SelectedIndex = 0;
            btnAddPreset.IsEnabled = App.sd.createPermissions[Glo.PERMISSION_REPORTS];
        }

        public static Dictionary<string, object?> storedVariables = new();

        private string GetNewPresetName() { return GetNewPresetName(""); }
        private string GetNewPresetName(string rename)
        {
            DialogWindows.NameObject nameObject;
            while (true)
            {
                if (rename == "")
                    nameObject = new("Preset Name");
                else
                    nameObject = new("Preset Name", cmbPresets.Text);
                nameObject.Owner = this;

                nameObject.ShowDialog();
                if (nameObject.DialogResult == false || nameObject.txtName.Text == "")
                    return "";
                if (!cmbPresets.Items.Contains(nameObject.txtName.Text))
                    break;
                App.DisplayError("This name is already in use.", this);
            }

            return nameObject.txtName.Text;
        }

        private void ApplyLoadedPreset(string jsonString)
        {
            try
            {
                // A lot could go wrong in terms of trying to read potentially null objects, but if that happens, the file
                // has been corrupted and an exception should be thrown anyway.
#pragma warning disable CS8602

                JsonNode? node = JsonNode.Parse(jsonString);
                JsonObject json = JsonNode.Parse(jsonString).AsObject();

                // APPLY PRESET HERE.

#pragma warning restore CS8602
            }
            catch
            {
                App.DisplayError("JSON file could not be read. It could be corrupted.", this);
            }
        }

        private void SavePreset(string name)
        {
            if (cmbPresets.Text != "<New>" && !savingNew &&
                !App.DisplayQuestion("Overwrite current preset?", "Save Changes",
                                     DialogWindows.DialogBox.Buttons.YesNo, this))
                return;

            // This section should never run in the current design due to name parsing in NameObject.xaml.cs
            HashSet<char> illegalCharacters = name.Where(c => System.IO.Path.GetInvalidFileNameChars().Contains(c)).ToHashSet();
            if (illegalCharacters.Any())
            {
                App.DisplayError("The chosen preset name contains the following illegal characters:\n\n" +
                                 string.Join(", ", illegalCharacters), this);
                return;
            }

            JsonObject json = new();
            json["Name"] = name;
            json["Context"] = Glo.CLIENT_PRESET_CONTEXT_REPORT_TO_TEMPLATES;

            if (App.SendJsonObject(Glo.CLIENT_PRESET_SAVE, json, this))
            {
                skipPresetLoad = true;
                PresetLoad(true);
                cmbPresets.SelectedItem = name;
                skipPresetLoad = false;
                if (savingNew)
                    App.DisplayError("Saved successfully.", this);
                else
                    App.DisplayError("Changes saved successfully.", this);
            }
        }

        private void PresetLoad(bool list)
        {
            try
            {
                lock (App.streamLock)
                {
                    using NetworkStream? stream = App.sr.NewClientNetworkStream(App.sd.ServerEP);
                    {
                        if (stream == null)
                        {
                            App.DisplayError(App.NO_NETWORK_STREAM, this);
                            return;
                        }
                        stream.WriteByte(Glo.CLIENT_PRESET_LOAD);
                        App.sr.WriteAndFlush(stream, App.sd.sessionID);
                        if (list) // if loading the preset list
                            App.sr.WriteAndFlush(stream, "/");
                        else // if loading a specific preset
                            App.sr.WriteAndFlush(stream, (string)cmbPresets.Items[cmbPresets.SelectedIndex]);
                        App.sr.WriteAndFlush(stream, Glo.FOLDER_REPORT_TO_TEMPLATES_PRESETS);

                        int response = App.sr.ReadByte(stream);
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                        {
                            if (list)
                            {
                                List<string> presets = new() { "<New>" };
                                foreach (string s in App.sr.ReadString(stream).Split('/'))
                                    if (s.EndsWith(".pre"))
                                        presets.Add(s.Remove(s.Length - 4));
                                cmbPresets.ItemsSource = presets;
                            }
                            else
                            {
                                string jsonString = App.sr.ReadString(stream);
                                ApplyLoadedPreset(jsonString);
                            }
                            return;
                        }
                        if (response == Glo.CLIENT_SESSION_INVALID)
                        {
                            App.SessionInvalidated();
                            return;
                        }
                        if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                        {
                            // We can slip the preset load when reverting back to the last selection, since it was already loaded.
                            skipPresetLoad = true;
                            cmbPresets.SelectedIndex = lastSelectedIndex;
                            skipPresetLoad = false;
                            throw new Exception(App.sr.ReadString(stream));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                App.DisplayError(App.ErrorConcat("Could not retrieve preset list.", e.Message), this);
            }
        }


        /// <summary> Reset the window to a blank configuration. </summary>
        private void ClearDown()
        {
        }

        bool skipPresetLoad = false;
        int lastSelectedIndex = 0;
        private void cmbPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Clear down.
            if (cmbPresets.SelectedIndex <= 0 && !skipPresetLoad)
            {
                btnRemovePreset.IsEnabled = false;
                btnRenamePreset.IsEnabled = false;
                btnSaveChanges.IsEnabled = false;

                if (lastSelectedIndex > 0)
                    ClearDown();
            }

            // Load preset.
            else if (cmbPresets.SelectedIndex > 0)
            {
                btnRemovePreset.IsEnabled = App.sd.deletePermissions[Glo.PERMISSION_REPORTS];
                btnRenamePreset.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_REPORTS];
                btnSaveChanges.IsEnabled = App.sd.editPermissions[Glo.PERMISSION_REPORTS];

                if (!skipPresetLoad)
                    PresetLoad(false);
            }
            lastSelectedIndex = cmbPresets.SelectedIndex;
        }

        bool savingNew = false;
        private void btnAddPreset_Click(object sender, RoutedEventArgs e)
        {
            string name = GetNewPresetName();
            if (name == "")
                return;

            savingNew = true;
            SavePreset(name);
            savingNew = false;
        }

        private void btnRemovePreset_Click(object sender, RoutedEventArgs e)
        {
            if (!App.DeleteConfirm(false, this))
                return;

            try
            {
                lock (App.streamLock)
                {
                    using NetworkStream? stream = App.sr.NewClientNetworkStream(App.sd.ServerEP);
                    {
                        if (stream == null)
                        {
                            App.DisplayError(App.NO_NETWORK_STREAM, this);
                            return;
                        }
                        stream.WriteByte(Glo.CLIENT_PRESET_DELETE);
                        App.sr.WriteAndFlush(stream, App.sd.sessionID);
                        string preset = (string)cmbPresets.Items[cmbPresets.SelectedIndex];
                        App.sr.WriteAndFlush(stream, preset);
                        App.sr.WriteAndFlush(stream, Glo.FOLDER_REPORT_TO_TEMPLATES_PRESETS);

                        int response = App.sr.ReadByte(stream);
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                        {
                            skipPresetLoad = true;
                            PresetLoad(true);
                            cmbPresets.SelectedIndex = 0;
                            skipPresetLoad = false;
                            return;
                        }
                        if (response == Glo.CLIENT_SESSION_INVALID)
                        {
                            App.SessionInvalidated();
                            return;
                        }
                        if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                        {
                            throw new Exception(App.sr.ReadString(stream));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.DisplayError(App.ErrorConcat("Could not remove the selected preset.", ex.Message), this);
            }
            finally
            {
                btnRemovePreset.IsEnabled = cmbPresets.Items.Count > 1 && cmbPresets.Text != "<New>" &&
                                            App.sd.deletePermissions[Glo.PERMISSION_REPORTS];
                btnSaveChanges.IsEnabled = false;
            }
        }

        private void btnRenamePreset_Click(object sender, RoutedEventArgs e)
        {
            string newName = GetNewPresetName(cmbPresets.Text);
            if (newName == "")
                return;

            try
            {
                lock (App.streamLock)
                {
                    using NetworkStream? stream = App.sr.NewClientNetworkStream(App.sd.ServerEP);
                    {
                        if (stream == null)
                        {
                            App.DisplayError(App.NO_NETWORK_STREAM, this);
                            return;
                        }
                        stream.WriteByte(Glo.CLIENT_PRESET_RENAME);
                        App.sr.WriteAndFlush(stream, App.sd.sessionID);
                        string preset = (string)cmbPresets.Items[cmbPresets.SelectedIndex];
                        preset += "/" + newName;
                        App.sr.WriteAndFlush(stream, preset);
                        App.sr.WriteAndFlush(stream, Glo.FOLDER_REPORT_TO_TEMPLATES_PRESETS);

                        int response = App.sr.ReadByte(stream);
                        if (response == Glo.CLIENT_REQUEST_SUCCESS)
                        {
                            skipPresetLoad = true;
                            PresetLoad(true);
                            cmbPresets.SelectedItem = newName;
                            skipPresetLoad = false;
                            return;
                        }
                        if (response == Glo.CLIENT_SESSION_INVALID)
                        {
                            App.SessionInvalidated();
                            return;
                        }
                        if (response == Glo.CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW)
                        {
                            throw new Exception(App.sr.ReadString(stream));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.DisplayError(App.ErrorConcat("Could not rename the selected preset.", ex.Message), this);
            }
        }

        private void btnSaveChanges_Click(object sender, RoutedEventArgs e)
        {
            SavePreset(cmbPresets.Text);
        }

        private void CustomWindow_Closed(object sender, EventArgs e)
        {
            if (WindowState != WindowState.Maximized)
            {
                Settings.Default.QueryWinSizeX = Width;
                Settings.Default.QueryWinSizeY = Height;
                Settings.Default.Save();
                App.WindowClosed();
            }
        }

        private void btnAddFile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new();
            dialog.Multiselect = true;
            string[] files;
            if (dialog.ShowDialog() == false)
                return;

            files = dialog.FileNames;
            foreach (string f in files)
                lstFiles.Items.Add(f);
        }

        private void btnRemoveFile_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < lstFiles.Items.Count; ++i)
                if (lstFiles.SelectedItems.Contains(lstFiles.Items[i]))
                {
                    lstFiles.Items.RemoveAt(i);
                    --i;
                }
        }

        private void btnFileUp_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnFileDown_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
