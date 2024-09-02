using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ClosedXML.Excel;

namespace BridgeOpsClient
{
    internal class FileExport
    {
        public static bool GetSaveFileName(out string fileName)
        {
            Microsoft.Win32.SaveFileDialog saveDialog = new();
            DateTime now = DateTime.Now;
            saveDialog.FileName = $"Data Export {now.ToString("yyyy-MM-dd HHmmss")}.xlsx";
            saveDialog.DefaultExt = ".xlsx";
            saveDialog.Filter = "Excel Workbook|*.xlsx|Excel Macro-Enabled Workbook|*.xlsm";
            bool? result = saveDialog.ShowDialog();
            if (result != true)
            {
                fileName = "";
                return false;
            }
            if (!saveDialog.FileName.EndsWith(".xlsx") && !saveDialog.FileName.EndsWith(".xlsm"))
            {
                App.DisplayError("Invalid file path");
                fileName = "";
                return false;
            }

            fileName = saveDialog.FileName;
            return true;
        }

        public static bool SaveFile(XLWorkbook xl, string fileName)
        {
            try
            {
                xl.SaveAs(fileName);
                return true;
            }
            catch (Exception err)
            {
                App.DisplayError("Could not save file, see error: " + err.Message);
                return false;
            }
        }

        public static void AutoWidthColumns(int columnCount, IXLWorksheet sheet)
        {
            // Width is defined only as 'number of characters'. God know what that means in real terms, but 50 seems
            // okay I guess?
            AutoWidthColumns(columnCount, sheet, 60);
        }
        public static void AutoWidthColumns(int columnCount, IXLWorksheet sheet, int maxWidth)
        {
            IXLColumn column = sheet.Column(1);
            for (int i = 0; i < columnCount; ++i)
            {
                column.AdjustToContents();
                if (column.Width > maxWidth)
                    column.Width = maxWidth;
                column = column.ColumnRight();
            }
        }
    }
}
