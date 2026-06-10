using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Forms = System.Windows.Forms;
using PosCounter.Net.Models;
using PosCounter.Net.SpecGrid;

namespace PosCounter.Net.Services
{
    public class ExportService
    {
        public string ExportRowsAll(IEnumerable<PosRow> rows, PosSettings settings)
        {
            return ExportRowsCore(rows, settings, BuildDefaultFilenameForRows("POS_All"), true, "ВСЕГО");
        }

        public string ExportRowsVisible(IEnumerable<PosRow> rows, PosSettings settings)
        {
            return ExportRowsCore(rows, settings, BuildDefaultFilenameForRows("POS_Visible"), true, "ВСЕГО (видимые позиции)");
        }

        public string ExportRowsSelected(IEnumerable<PosRow> rows, PosSettings settings)
        {
            return ExportRowsCore(rows, settings, BuildDefaultFilenameForRows("POS_Selected"), true, "ВСЕГО (выделенные позиции)");
        }

        /// <summary>
        /// Итоговые строки для таблицы и Excel: «Всего по слою …»; если слоёв несколько — строка с общей суммой.
        /// </summary>
        public static List<PosRow> CreateFooterRows(IReadOnlyList<PosRow> data, string grandTotalLabel)
        {
            var list = new List<PosRow>();
            if (data == null)
            {
                return list;
            }

            var rowsOnly = data.Where(r => r != null && !r.IsTotalLine).ToList();
            if (rowsOnly.Count <= 1)
            {
                return list;
            }

            var grand = rowsOnly.Sum(r => r.Count);
            var groups = rowsOnly
                .GroupBy(r => r.Layer ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var g in groups)
            {
                var sum = g.Sum(r => r.Count);
                var layerLabel = string.IsNullOrEmpty(g.Key) ? "(без слоя)" : g.Key;
                list.Add(new PosRow
                {
                    Text = $"Всего по слою «{layerLabel}»",
                    Count = sum,
                    Layer = g.Key ?? string.Empty,
                    IsTotalLine = true
                });
            }

            if (groups.Count > 1)
            {
                list.Add(new PosRow
                {
                    Text = string.IsNullOrWhiteSpace(grandTotalLabel) ? "ВСЕГО" : grandTotalLabel,
                    Count = grand,
                    Layer = string.Empty,
                    IsTotalLine = true
                });
            }

            return list;
        }

        public string ExportResult(CountComputationResult result, string selectedLayer, PosSettings settings)
        {
            if (result == null || !result.Success)
            {
                return null;
            }

            var defaultFileName = BuildDefaultFilename(result, selectedLayer);
            var savePath = AskSavePath(defaultFileName);
            if (string.IsNullOrWhiteSpace(savePath))
            {
                return null;
            }

            var targetXlsx = EnsureExtension(savePath, ".xlsx");
            string outputPath;
            try
            {
                ExportToExcelInterop(result, targetXlsx);
                outputPath = targetXlsx;
            }
            catch
            {
                var csvPath = Path.ChangeExtension(targetXlsx, ".csv");
                ExportToCsv(result, csvPath);
                outputPath = csvPath;
            }

            if (File.Exists(outputPath) && settings.AutoOpenExcel)
            {
                var ask = System.Windows.MessageBox.Show(
                    $"Экспорт выполнен.\n\nФайл:\n{outputPath}\n\nОткрыть файл сейчас?",
                    "POS COUNTER v4.0",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (ask == MessageBoxResult.Yes)
                {
                    TryOpenFile(outputPath);
                }
            }

            return outputPath;
        }

        private static string BuildDefaultFilenameForRows(string prefix)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture);
            return $"{prefix}_{timestamp}.xlsx";
        }

        private string ExportRowsCore(IEnumerable<PosRow> rows, PosSettings settings, string defaultFileName, bool addFooter, string grandTotalLabel)
        {
            var safeRows = (rows ?? Enumerable.Empty<PosRow>())
                .Where(r => r != null && !r.IsTotalLine)
                .Select(r => new PosRow
                {
                    Text = r.Text ?? string.Empty,
                    Layer = r.Layer ?? string.Empty,
                    Count = r.Count,
                    Key = r.Key,
                    NameFromSpec = r.NameFromSpec ?? string.Empty,
                    NameSource = r.NameSource ?? string.Empty,
                    IsTotalLine = false,
                    SourceHandles = r.SourceHandles != null
                        ? new List<string>(r.SourceHandles)
                        : new List<string>()
                })
                .ToList();

            if (safeRows.Count == 0)
            {
                return null;
            }

            if (addFooter && safeRows.Count > 1)
            {
                safeRows.AddRange(CreateFooterRows(safeRows, grandTotalLabel));
            }

            var savePath = AskSavePath(defaultFileName);
            if (string.IsNullOrWhiteSpace(savePath))
            {
                return null;
            }

            var targetXlsx = EnsureExtension(savePath, ".xlsx");
            string outputPath;
            try
            {
                ExportRowsToExcelInterop(safeRows, targetXlsx);
                outputPath = targetXlsx;
            }
            catch
            {
                var csvPath = Path.ChangeExtension(targetXlsx, ".csv");
                ExportRowsToCsv(safeRows, csvPath);
                outputPath = csvPath;
            }

            if (File.Exists(outputPath) && (settings?.AutoOpenExcel ?? false))
            {
                var ask = System.Windows.MessageBox.Show(
                    $"Экспорт выполнен.\n\nФайл:\n{outputPath}\n\nОткрыть файл сейчас?",
                    "POS COUNTER",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (ask == MessageBoxResult.Yes)
                {
                    TryOpenFile(outputPath);
                }
            }

            return outputPath;
        }

        private static string BuildDefaultFilename(CountComputationResult result, string selectedLayer)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture);
            if (result.ViewMode == "viewport")
            {
                return $"POS_Viewport_Результаты_{timestamp}.xlsx";
            }

            if (result.Scope == "all")
            {
                return $"POS_Все_слои_{timestamp}.xlsx";
            }

            var layer = SanitizeFileName(selectedLayer);
            return $"POS_Результаты_{layer}_{timestamp}.xlsx";
        }

        private static string AskSavePath(string defaultFileName)
        {
            using (var dialog = new Forms.SaveFileDialog())
            {
                dialog.Filter = "Excel Workbook (*.xlsx)|*.xlsx|CSV UTF-8 (*.csv)|*.csv";
                dialog.FileName = defaultFileName;
                dialog.OverwritePrompt = true;
                dialog.AddExtension = true;
                return dialog.ShowDialog() == Forms.DialogResult.OK ? dialog.FileName : null;
            }
        }

        private static void ExportToExcelInterop(CountComputationResult result, string path)
        {
            Type excelType = Type.GetTypeFromProgID("Excel.Application");
            if (excelType == null)
            {
                throw new InvalidOperationException("Excel is not installed.");
            }

            object excelObj = null;
            object workbookObj = null;
            object worksheetObj = null;

            try
            {
                excelObj = Activator.CreateInstance(excelType);
                dynamic excel = excelObj;
                excel.Visible = false;
                excel.DisplayAlerts = false;

                workbookObj = excel.Workbooks.Add();
                dynamic workbook = workbookObj;
                worksheetObj = workbook.ActiveSheet;
                dynamic worksheet = worksheetObj;

                if (result.ViewMode == "viewport")
                {
                    worksheet.Name = "Viewports";
                    WriteViewportSheet(worksheet, result);
                }
                else if (result.Scope == "all")
                {
                    worksheet.Name = "Все слои";
                    WriteAllLayersSheet(worksheet, result.AllLayerPositions);
                }
                else
                {
                    worksheet.Name = "Результаты подсчета";
                    WriteCurrentLayerSheet(worksheet, result.SelectedLayer, result.CurrentLayerPositions);
                }

                worksheet.Columns.AutoFit();
                workbook.SaveAs(path, 51);
                workbook.Close(false);
                excel.Quit();
            }
            finally
            {
                ReleaseCom(worksheetObj);
                ReleaseCom(workbookObj);
                ReleaseCom(excelObj);
            }
        }

        private static void ExportRowsToExcelInterop(List<PosRow> rows, string path)
        {
            Type excelType = Type.GetTypeFromProgID("Excel.Application");
            if (excelType == null)
            {
                throw new InvalidOperationException("Excel is not installed.");
            }

            object excelObj = null;
            object workbookObj = null;
            object worksheetObj = null;

            try
            {
                excelObj = Activator.CreateInstance(excelType);
                dynamic excel = excelObj;
                excel.Visible = false;
                excel.DisplayAlerts = false;

                workbookObj = excel.Workbooks.Add();
                dynamic workbook = workbookObj;
                worksheetObj = workbook.ActiveSheet;
                dynamic ws = worksheetObj;

                ws.Name = "PosCounter";
                WriteRowsSheet(ws, rows);

                ws.Columns.AutoFit();
                workbook.SaveAs(path, 51);
                workbook.Close(false);
                excel.Quit();
            }
            finally
            {
                ReleaseCom(worksheetObj);
                ReleaseCom(workbookObj);
                ReleaseCom(excelObj);
            }
        }

        private static void WriteRowsSheet(dynamic ws, List<PosRow> rows)
        {
            ws.Cells[1, 1].Value2 = "Марка";
            ws.Cells[1, 2].Value2 = "Наименование";
            ws.Cells[1, 3].Value2 = "Количество";
            ws.Cells[1, 4].Value2 = "Слой";
            ws.Range["A1:D1"].Font.Bold = true;

            var r = 2;
            foreach (var row in rows)
            {
                ws.Cells[r, 1].Value2 = row.Text ?? string.Empty;
                ws.Cells[r, 2].Value2 = MTextPlainText.FormatForPaletteDisplay(row.NameFromSpec ?? string.Empty);
                ws.Cells[r, 3].Value2 = row.Count.ToString(CultureInfo.InvariantCulture);
                ws.Cells[r, 4].Value2 = row.Layer ?? string.Empty;
                if (row.IsTotalLine)
                {
                    ws.Range[$"A{r}:D{r}"].Font.Bold = true;
                }

                r++;
            }
        }

        private static void ExportRowsToCsv(List<PosRow> rows, string path)
        {
            var utf8Bom = new UTF8Encoding(true);
            using (var writer = new StreamWriter(path, false, utf8Bom))
            {
                writer.WriteLine("Марка;Наименование;Количество;Слой");
                foreach (var row in rows)
                {
                    var name = MTextPlainText.FormatForPaletteDisplay(row.NameFromSpec ?? string.Empty);
                    writer.WriteLine($"{EscapeCsv(row.Text)};{EscapeCsv(name)};{row.Count.ToString(CultureInfo.InvariantCulture)};{EscapeCsv(row.Layer)}");
                }
            }
        }

        private static string EscapeCsv(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            // Keep existing delimiter ';' compatible with RU locales, quote if needed.
            var needsQuotes = value.Contains(";") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r");
            if (!needsQuotes)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static void WriteCurrentLayerSheet(dynamic ws, string layerName, List<PositionCount> positions)
        {
            ws.Cells[1, 1].Value2 = "Номер позиции";
            ws.Cells[1, 2].Value2 = "Количество";
            ws.Cells[1, 3].Value2 = "Слой";
            ws.Cells[1, 4].Value2 = "Процент";
            ws.Range["A1:D1"].Font.Bold = true;

            var total = positions.Sum(p => p.Count);
            if (total == 0)
            {
                total = 1;
            }

            var row = 2;
            foreach (var item in positions)
            {
                var percent = 100.0 * item.Count / total;
                ws.Cells[row, 1].Value2 = item.Position.ToString(CultureInfo.InvariantCulture);
                ws.Cells[row, 2].Value2 = item.Count.ToString(CultureInfo.InvariantCulture);
                ws.Cells[row, 3].Value2 = layerName;
                ws.Cells[row, 4].Value2 = percent.ToString("0.00", CultureInfo.InvariantCulture) + "%";
                row++;
            }

            ws.Cells[row, 1].Value2 = "ИТОГО";
            ws.Cells[row, 2].Value2 = positions.Sum(p => p.Count).ToString(CultureInfo.InvariantCulture);
            ws.Cells[row, 3].Value2 = layerName;
            ws.Cells[row, 4].Value2 = "100.00%";
            ws.Range[$"A{row}:D{row}"].Font.Bold = true;
        }

        private static void WriteAllLayersSheet(dynamic ws, List<LayerResult> layers)
        {
            ws.Cells[1, 1].Value2 = "Номер позиции";
            ws.Cells[1, 2].Value2 = "Количество";
            ws.Cells[1, 3].Value2 = "Слой";
            ws.Cells[1, 4].Value2 = "Процент";
            ws.Range["A1:D1"].Font.Bold = true;

            var row = 2;
            foreach (var layer in layers)
            {
                var total = layer.Positions.Sum(p => p.Count);
                if (total == 0)
                {
                    total = 1;
                }

                foreach (var item in layer.Positions)
                {
                    var percent = 100.0 * item.Count / total;
                    ws.Cells[row, 1].Value2 = item.Position.ToString(CultureInfo.InvariantCulture);
                    ws.Cells[row, 2].Value2 = item.Count.ToString(CultureInfo.InvariantCulture);
                    ws.Cells[row, 3].Value2 = layer.LayerName;
                    ws.Cells[row, 4].Value2 = percent.ToString("0.00", CultureInfo.InvariantCulture) + "%";
                    row++;
                }

                ws.Cells[row, 1].Value2 = "ИТОГО";
                ws.Cells[row, 2].Value2 = layer.Positions.Sum(p => p.Count).ToString(CultureInfo.InvariantCulture);
                ws.Cells[row, 3].Value2 = layer.LayerName;
                ws.Cells[row, 4].Value2 = "100.00%";
                ws.Range[$"A{row}:D{row}"].Font.Bold = true;
                row += 2;
            }
        }

        private static void WriteViewportSheet(dynamic ws, CountComputationResult result)
        {
            ws.Cells[1, 1].Value2 = "Лист";
            ws.Cells[1, 2].Value2 = "Viewport";
            ws.Cells[1, 3].Value2 = "Слой";
            ws.Cells[1, 4].Value2 = "Номер позиции";
            ws.Cells[1, 5].Value2 = "Количество";
            ws.Cells[1, 6].Value2 = "Процент";
            ws.Range["A1:F1"].Font.Bold = true;

            var row = 2;
            foreach (var viewport in result.ViewportResults)
            {
                if (result.Scope == "all")
                {
                    foreach (var layer in viewport.AllLayerPositions)
                    {
                        var total = layer.Positions.Sum(p => p.Count);
                        if (total == 0)
                        {
                            total = 1;
                        }

                        foreach (var item in layer.Positions)
                        {
                            var percent = 100.0 * item.Count / total;
                            ws.Cells[row, 1].Value2 = viewport.LayoutName;
                            ws.Cells[row, 2].Value2 = viewport.ViewportHandle;
                            ws.Cells[row, 3].Value2 = layer.LayerName;
                            ws.Cells[row, 4].Value2 = item.Position.ToString(CultureInfo.InvariantCulture);
                            ws.Cells[row, 5].Value2 = item.Count.ToString(CultureInfo.InvariantCulture);
                            ws.Cells[row, 6].Value2 = percent.ToString("0.00", CultureInfo.InvariantCulture) + "%";
                            row++;
                        }

                        ws.Cells[row, 1].Value2 = viewport.LayoutName;
                        ws.Cells[row, 2].Value2 = viewport.ViewportHandle;
                        ws.Cells[row, 3].Value2 = "ИТОГО по слою " + layer.LayerName;
                        ws.Cells[row, 5].Value2 = layer.Positions.Sum(p => p.Count).ToString(CultureInfo.InvariantCulture);
                        ws.Cells[row, 6].Value2 = "100.00%";
                        ws.Range[$"A{row}:F{row}"].Font.Bold = true;
                        row += 2;
                    }
                }
                else
                {
                    var total = viewport.CurrentLayerPositions.Sum(p => p.Count);
                    if (total == 0)
                    {
                        total = 1;
                    }

                    foreach (var item in viewport.CurrentLayerPositions)
                    {
                        var percent = 100.0 * item.Count / total;
                        ws.Cells[row, 1].Value2 = viewport.LayoutName;
                        ws.Cells[row, 2].Value2 = viewport.ViewportHandle;
                        ws.Cells[row, 3].Value2 = viewport.LayerName;
                        ws.Cells[row, 4].Value2 = item.Position.ToString(CultureInfo.InvariantCulture);
                        ws.Cells[row, 5].Value2 = item.Count.ToString(CultureInfo.InvariantCulture);
                        ws.Cells[row, 6].Value2 = percent.ToString("0.00", CultureInfo.InvariantCulture) + "%";
                        row++;
                    }

                    ws.Cells[row, 1].Value2 = viewport.LayoutName;
                    ws.Cells[row, 2].Value2 = viewport.ViewportHandle;
                    ws.Cells[row, 3].Value2 = "ИТОГО по слою " + viewport.LayerName;
                    ws.Cells[row, 5].Value2 = viewport.CurrentLayerPositions.Sum(p => p.Count).ToString(CultureInfo.InvariantCulture);
                    ws.Cells[row, 6].Value2 = "100.00%";
                    ws.Range[$"A{row}:F{row}"].Font.Bold = true;
                    row += 2;
                }
            }
        }

        private static void ExportToCsv(CountComputationResult result, string path)
        {
            var utf8Bom = new UTF8Encoding(true);
            using (var writer = new StreamWriter(path, false, utf8Bom))
            {
                if (result.ViewMode == "viewport")
                {
                    writer.WriteLine("Лист;Viewport;Слой;Номер позиции;Количество;Процент");
                    foreach (var viewport in result.ViewportResults)
                    {
                        if (result.Scope == "all")
                        {
                            foreach (var layer in viewport.AllLayerPositions)
                            {
                                var total = layer.Positions.Sum(p => p.Count);
                                if (total == 0)
                                {
                                    total = 1;
                                }

                                foreach (var item in layer.Positions)
                                {
                                    var percent = 100.0 * item.Count / total;
                                    writer.WriteLine($"{viewport.LayoutName};{viewport.ViewportHandle};{layer.LayerName};{item.Position};{item.Count};{percent:0.00}%");
                                }

                                writer.WriteLine($"{viewport.LayoutName};{viewport.ViewportHandle};ИТОГО по слою {layer.LayerName};;{layer.Positions.Sum(p => p.Count)};100.00%");
                                writer.WriteLine();
                            }
                        }
                        else
                        {
                            var total = viewport.CurrentLayerPositions.Sum(p => p.Count);
                            if (total == 0)
                            {
                                total = 1;
                            }

                            foreach (var item in viewport.CurrentLayerPositions)
                            {
                                var percent = 100.0 * item.Count / total;
                                writer.WriteLine($"{viewport.LayoutName};{viewport.ViewportHandle};{viewport.LayerName};{item.Position};{item.Count};{percent:0.00}%");
                            }

                            writer.WriteLine($"{viewport.LayoutName};{viewport.ViewportHandle};ИТОГО по слою {viewport.LayerName};;{viewport.CurrentLayerPositions.Sum(p => p.Count)};100.00%");
                            writer.WriteLine();
                        }
                    }
                }
                else if (result.Scope == "all")
                {
                    writer.WriteLine("Номер позиции;Количество;Слой;Процент");
                    foreach (var layer in result.AllLayerPositions)
                    {
                        var total = layer.Positions.Sum(p => p.Count);
                        if (total == 0)
                        {
                            total = 1;
                        }

                        foreach (var item in layer.Positions)
                        {
                            var percent = 100.0 * item.Count / total;
                            writer.WriteLine($"{item.Position};{item.Count};{layer.LayerName};{percent:0.00}%");
                        }

                        writer.WriteLine($"ИТОГО;{layer.Positions.Sum(p => p.Count)};{layer.LayerName};100.00%");
                        writer.WriteLine();
                    }
                }
                else
                {
                    writer.WriteLine("Номер позиции;Количество;Слой;Процент");
                    var total = result.CurrentLayerPositions.Sum(p => p.Count);
                    if (total == 0)
                    {
                        total = 1;
                    }

                    foreach (var item in result.CurrentLayerPositions)
                    {
                        var percent = 100.0 * item.Count / total;
                        writer.WriteLine($"{item.Position};{item.Count};{result.SelectedLayer};{percent:0.00}%");
                    }

                    writer.WriteLine($"ИТОГО;{result.CurrentLayerPositions.Sum(p => p.Count)};{result.SelectedLayer};100.00%");
                }
            }
        }

        private static string EnsureExtension(string path, string extension)
        {
            if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            return Path.ChangeExtension(path, extension.TrimStart('.'));
        }

        private static string SanitizeFileName(string fileNamePart)
        {
            if (string.IsNullOrWhiteSpace(fileNamePart))
            {
                return "Layer";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var safe = new string(fileNamePart.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return safe.Trim();
        }

        private static void TryOpenFile(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path)
                {
                    UseShellExecute = true
                });
            }
            catch
            {
                // The file is already created; open failures are non-fatal.
            }
        }

        private static void ReleaseCom(object comObject)
        {
            if (comObject != null && Marshal.IsComObject(comObject))
            {
                Marshal.FinalReleaseComObject(comObject);
            }
        }
    }
}
