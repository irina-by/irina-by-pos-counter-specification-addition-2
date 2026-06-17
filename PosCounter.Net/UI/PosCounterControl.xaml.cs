using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using AcAp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Autodesk.AutoCAD.DatabaseServices;
using PosCounter.Net.Engine;
using PosCounter.Net.Models;
using PosCounter.Net.Services;
using PosCounter.Net.State;
using PosCounter.Net;
using PosCounter.Net.SpecGrid;

namespace PosCounter.Net.UI
{
    public partial class PosCounterControl : UserControl
    {
        private readonly PosCounterEngine _engine = new PosCounterEngine();
        private readonly ExportService _exportService = new ExportService();

        private readonly HashSet<string> _filterText = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _filterName = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _filterCount = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _filterLayer = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<int, string> _lastMarkNames;
        private List<PosRow> _lastCountRows = new List<PosRow>();

        private ObservableCollection<PosRowVm> _rowsAll = new ObservableCollection<PosRowVm>();
        private ObservableCollection<GridRowModel> _gridRows = new ObservableCollection<GridRowModel>();
        private bool _isInternalUpdate;

        public PosCounterControl()
        {
            InitializeComponent();
            LoadSettingsToUi();
            InitGridView();
            SetStatus("Готово. Нажмите ЗАПУСТИТЬ.");
        }

        public void RefreshFromDocument()
        {
            // Legacy API kept for PaletteHost. New UI does not depend on document layers list.
        }

        private void LoadSettingsToUi()
        {
            try
            {
                var s = PosSettingsStore.Current;
                ChkAllInModel.IsChecked = s.CountAllInModel;
            }
            catch
            {
                // ignore
            }
        }

        private PosSettings PullSettingsFromUi()
        {
            var settings = PosSettingsStore.Current;
            settings.CountAllInModel = ChkAllInModel.IsChecked == true;
            PosSettingsStore.Save(settings);
            return settings;
        }

        private void ChkAllInModel_OnChanged(object sender, RoutedEventArgs e)
        {
            PullSettingsFromUi();
            RecomputeStats();
        }

        private void InitGridView()
        {
            _gridRows = new ObservableCollection<GridRowModel>();
            GridResults.ItemsSource = _gridRows;
            GridResults.CommandBindings.Add(new CommandBinding(
                ApplicationCommands.Copy,
                OnCopyPalettePlainText,
                (_, e) => e.CanExecute = true));
            GridResults.PreviewKeyDown += GridResults_OnPreviewKeyDown;
            RebuildFiltersUi();
            RefreshGridRows();
        }

        private void GridResults_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.C || (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            {
                return;
            }

            var namesOnly = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            if (namesOnly ? CopyPaletteNamesOnlyToClipboard() : CopyPaletteTableToClipboard())
            {
                e.Handled = true;
            }
        }

        private void OnCopyPalettePlainText(object sender, ExecutedRoutedEventArgs e)
        {
            if (CopyPaletteTableToClipboard())
            {
                e.Handled = true;
            }
        }

        /// <summary>§19.7 PALETTE-FULL-COPY: TSV — марка, наименование, количество, слой.</summary>
        private bool CopyPaletteTableToClipboard()
        {
            var gridRows = GetPaletteRowsForCopy().ToList();
            if (gridRows.Count == 0)
            {
                SetStatus("Нечего копировать: нет видимых строк в таблице.");
                return false;
            }

            var lines = new List<string>
            {
                string.Join("\t", "Марка", "Наименование", "Количество", "Слой")
            };
            foreach (var gr in gridRows)
            {
                var mark = EscapeTsvCell(gr.Text ?? string.Empty);
                var name = EscapeTsvCell(MTextPlainText.FormatForPaletteDisplay(gr.Data?.NameFromSpec ?? gr.NameFromSpec ?? string.Empty));
                var qty = gr.Count.ToString(CultureInfo.InvariantCulture);
                var layer = EscapeTsvCell(gr.Layer ?? string.Empty);
                lines.Add(string.Join("\t", mark, name, qty, layer));
            }

            try
            {
                Clipboard.SetText(string.Join(Environment.NewLine, lines));
                SetStatus($"Скопировано строк таблицы: {gridRows.Count} (Ctrl+V в Excel/Блокнот). Ctrl+Shift+C — только наименования.");
                return true;
            }
            catch (Exception ex)
            {
                SetStatus($"Не удалось скопировать: {ex.Message}");
                return false;
            }
        }

        /// <summary>§19.7: одна колонка наименований (Ctrl+Shift+C).</summary>
        private bool CopyPaletteNamesOnlyToClipboard()
        {
            var gridRows = GetPaletteRowsForCopy().ToList();
            var lines = new List<string>();
            foreach (var gr in gridRows)
            {
                var name = MTextPlainText.FormatForPaletteDisplay(gr.Data?.NameFromSpec ?? gr.NameFromSpec ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    lines.Add(name);
                }
            }

            if (lines.Count == 0)
            {
                SetStatus("Нечего копировать: нет наименований.");
                return false;
            }

            try
            {
                Clipboard.SetText(string.Join(Environment.NewLine, lines));
                SetStatus($"Скопировано наименований: {lines.Count} (Ctrl+Shift+C).");
                return true;
            }
            catch (Exception ex)
            {
                SetStatus($"Не удалось скопировать: {ex.Message}");
                return false;
            }
        }

        private IEnumerable<GridRowModel> GetPaletteRowsForCopy()
        {
            var selected = GridResults.SelectedItems
                .Cast<object>()
                .OfType<GridRowModel>()
                .Where(g => !g.IsSummary)
                .ToList();
            if (selected.Count > 0)
            {
                return selected;
            }

            return (_gridRows ?? new ObservableCollection<GridRowModel>()).Where(g => !g.IsSummary);
        }

        private static string EscapeTsvCell(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }

        private bool PassesFilter(PosRowVm row)
        {
            if (row == null)
            {
                return false;
            }

            if (_filterText.Count > 0 && !_filterText.Contains(row.Text ?? string.Empty))
            {
                return false;
            }

            if (_filterName.Count > 0 && !_filterName.Contains(row.NameFromSpec ?? string.Empty))
            {
                return false;
            }

            if (_filterCount.Count > 0 && !_filterCount.Contains(row.Count.ToString()))
            {
                return false;
            }

            if (_filterLayer.Count > 0 && !_filterLayer.Contains(row.Layer ?? string.Empty))
            {
                return false;
            }

            return true;
        }

        private IEnumerable<PosRowVm> GetVisibleDataRows()
        {
            return (_rowsAll ?? new ObservableCollection<PosRowVm>())
                .Where(PassesFilter)
                .OrderBy(r => r.Key ?? int.MaxValue)
                .ThenBy(r => r.Layer, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Text, StringComparer.OrdinalIgnoreCase);
        }

        private void RefreshGridRows()
        {
            if (_gridRows == null)
            {
                return;
            }

            _gridRows.Clear();
            var visible = GetVisibleDataRows().ToList();
            foreach (var r in visible)
            {
                _gridRows.Add(GridRowModel.FromDataRow(r));
            }

            if (visible.Count > 1)
            {
                var forFooter = visible.Select(r => r.ToRow()).ToList();
                foreach (var f in ExportService.CreateFooterRows(forFooter, "ВСЕГО (видимые позиции)"))
                {
                    _gridRows.Add(GridRowModel.FromFooterRow(f));
                }
            }

            RecomputeStats();
        }

        private void BtnRun_OnClick(object sender, RoutedEventArgs e)
        {
            var doc = AcAp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                SetStatus("[ERROR] Нет активного документа.");
                return;
            }

            var countAllInModel = PullSettingsFromUi().CountAllInModel;
            if (countAllInModel)
            {
                SetStatus("Идёт подсчёт... (если долго — значит AutoCAD обрабатывает лист/видовой экран)");
            }
            else
            {
                SetStatus("Идёт подсчёт по выделенным объектам...");
            }

            try { BtnRun.IsEnabled = false; } catch { /* ignore */ }
            PaletteHost.RequestRun(countAllInModel);
        }

        internal void ApplyRunResult(PosCounterEngine.PosCountResult res)
        {
            try
            {
                _isInternalUpdate = true;
                try
                {
                    var rows = res?.Rows ?? ArrayCompat.Empty<PosRow>();
                    _lastCountRows = rows.Select(r => new PosRow
                    {
                        Text = r.Text,
                        Layer = r.Layer,
                        Count = r.Count,
                        Key = ParseKey(r.Text),
                        NameFromSpec = r.NameFromSpec,
                        NameSource = r.NameSource,
                        SourceHandles = r.SourceHandles != null ? new List<string>(r.SourceHandles) : new List<string>()
                    }).ToList();
                    _rowsAll = new ObservableCollection<PosRowVm>(_lastCountRows.Select(r => new PosRowVm(r)));
                    _filterLayer.Clear();
                    if (TxtSearchLayer != null)
                    {
                        TxtSearchLayer.Text = string.Empty;
                    }

                    InitGridView();
                    GridResults.UnselectAll();

                    if (_rowsAll.Count == 0)
                    {
                        var src = res?.SourceDescription;
                        if (!string.IsNullOrWhiteSpace(src))
                        {
                            if (string.Equals(src, "нет выделения", StringComparison.OrdinalIgnoreCase))
                            {
                                SetStatus("Нет выделения и галочка выключена – выделите объекты или включите галочку");
                            }
                            else
                            {
                                SetStatus(string.Equals(src, "активный viewport", StringComparison.OrdinalIgnoreCase)
                                    ? "Подсчёт по активному видовому экрану"
                                    : "Подсчёт всей модели (галочка включена)");
                            }
                        }
                        else
                        {
                            SetStatus("Подсчёт...");
                        }

                        return;
                    }

                    if (res != null && string.Equals(res.SourceDescription, "выделение", StringComparison.OrdinalIgnoreCase))
                    {
                        SetStatus("Подсчёт по выделенным объектам");
                        return;
                    }

                    if (res != null && string.Equals(res.SourceDescription, "активный viewport", StringComparison.OrdinalIgnoreCase))
                    {
                        SetStatus("Подсчёт по активному видовому экрану");
                        return;
                    }

                    SetStatus("Подсчёт всей модели (галочка включена)");
                }
                finally
                {
                    _isInternalUpdate = false;
                    try { BtnRun.IsEnabled = true; } catch { /* ignore */ }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "POS COUNTER", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("[ERROR] Ошибка обновления таблицы.");
                try { BtnRun.IsEnabled = true; } catch { /* ignore */ }
            }
        }

        private void BtnSelectSpec_OnClick(object sender, RoutedEventArgs e)
        {
            var doc = AcAp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                SetStatus("[ERROR] Нет активного документа.");
                return;
            }

            SetStatus("Выберите 2 рамки таблицы в AutoCAD...");
            PaletteHost.RequestSelectSpec();
        }

        private void BtnReset_OnClick(object sender, RoutedEventArgs e)
        {
            ResetPaletteState();
        }

        private void ResetPaletteState()
        {
            _isInternalUpdate = true;
            try
            {
                _lastCountRows = new List<PosRow>();
                _rowsAll = new ObservableCollection<PosRowVm>();
                _lastMarkNames = new Dictionary<int, string>();

                SpecGridSession.ClearScopes();

                _filterText.Clear();
                _filterName.Clear();
                _filterCount.Clear();
                _filterLayer.Clear();
                TxtSearchText.Text = string.Empty;
                TxtSearchName.Text = string.Empty;
                TxtSearchCount.Text = string.Empty;
                TxtSearchLayer.Text = string.Empty;

                PopupFilterText.IsOpen = false;
                PopupFilterName.IsOpen = false;
                PopupFilterCount.IsOpen = false;
                PopupFilterLayer.IsOpen = false;

                InitGridView();
                GridResults.UnselectAll();

                Commands.ClearDrawingHighlight();
                SetStatus("Сброшено. Нажмите ЗАПУСТИТЬ.");
            }
            finally
            {
                _isInternalUpdate = false;
            }
        }

        internal void ApplySpecResult(PaletteHost.SpecApplyPayload payload)
        {
            if (payload == null)
            {
                return;
            }

            if (!payload.Success)
            {
                SetStatus(string.IsNullOrWhiteSpace(payload.Error)
                    ? "[ERROR] Выбор спецификации не выполнен."
                    : $"[ERROR] {payload.Error}");
                return;
            }

            _lastMarkNames = payload.MarkNames != null
                ? new Dictionary<int, string>(payload.MarkNames)
                : new Dictionary<int, string>();
            ApplyMarkNamesToPalette(_lastMarkNames);
            var missingQty = payload.MissingQtyMarks != null && payload.MissingQtyMarks.Count > 0
                ? $" Нет количества для марок: {string.Join(", ", payload.MissingQtyMarks)}."
                : string.Empty;
            var namesCount = payload.MarkNames?.Count ?? 0;
            var paletteCount = payload.PaletteKeyCount > 0 ? payload.PaletteKeyCount : namesCount;
            var namesGap = paletteCount > namesCount
                ? $" Имён={namesCount} из {paletteCount} ключей палитры — выделите все листы спецификации."
                : string.Empty;
            SetStatus($"[OK] Спецификация: имён={namesCount}, Кол. записано={payload.QtyWritten}, пропущено={payload.QtySkipped}.{missingQty}{namesGap}");
            RefreshGridRows();
        }

        internal bool TryBuildQtyByKeyFromVisibleRows(out Dictionary<int, int> qtyByKey)
        {
            qtyByKey = new Dictionary<int, int>();
            foreach (var row in GetVisibleDataRows())
            {
                var key = row.Key ?? ParseKey(row.Text);
                if (!key.HasValue)
                {
                    continue;
                }

                if (!qtyByKey.ContainsKey(key.Value))
                {
                    qtyByKey[key.Value] = 0;
                }

                qtyByKey[key.Value] += row.Count;
            }

            return qtyByKey.Count > 0;
        }

        internal Dictionary<int, int> TryBuildQtyByKeyFromAllRowsSnapshot()
        {
            var qtyByKey = new Dictionary<int, int>();
            foreach (var row in _lastCountRows ?? new List<PosRow>())
            {
                var key = row.Key ?? ParseKey(row.Text);
                if (!key.HasValue)
                {
                    continue;
                }

                if (!qtyByKey.ContainsKey(key.Value))
                {
                    qtyByKey[key.Value] = 0;
                }

                qtyByKey[key.Value] += row.Count;
            }

            return qtyByKey;
        }

        private void ReapplyMarkNamesForLayerFilter()
        {
            if (_lastMarkNames == null || _lastMarkNames.Count == 0)
            {
                return;
            }

            ApplyMarkNamesToPalette(_lastMarkNames);
        }

        private void ApplyMarkNamesToPalette(Dictionary<int, string> markNames)
        {
            if (markNames == null || markNames.Count == 0 || _rowsAll == null)
            {
                return;
            }

            var visibleLayers = _filterLayer.Count == 0
                ? null
                : new HashSet<string>(_filterLayer, StringComparer.OrdinalIgnoreCase);

            foreach (var row in _rowsAll)
            {
                var key = row.Key ?? ParseKey(row.Text);
                if (!key.HasValue)
                {
                    continue;
                }

                if (string.Equals(row.NameSource, "user", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (visibleLayers != null && !visibleLayers.Contains(row.Layer ?? string.Empty))
                {
                    row.SetNameFromProgram(string.Empty, "not_found");
                    continue;
                }

                if (markNames.TryGetValue(key.Value, out var name))
                {
                    var display = MTextPlainText.FormatForPaletteDisplay(name);
                    row.SetNameFromProgram(
                        display,
                        string.IsNullOrWhiteSpace(display) ? "not_found" : "grid-lines");
                }
                else
                {
                    row.SetNameFromProgram(string.Empty, "not_found");
                }
            }
        }

        private static int? ParseKey(string text)
        {
            return MTextPlainText.TryParseMarkKey(text ?? string.Empty, out var key) ? key : (int?)null;
        }

        private void BtnExportAll_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = PullSettingsFromUi();
                var exportPath = _exportService.ExportRowsAll(_rowsAll.Select(r => r.ToRow()), settings);
                SetStatus(!string.IsNullOrWhiteSpace(exportPath) ? $"[OK] Экспорт: {exportPath}" : "Экспорт отменен.");
            }
            catch (Exception ex)
            {
                SetStatus($"[ERROR] Ошибка экспорта: {ex.Message}");
            }
        }

        private void BtnExportVisible_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = PullSettingsFromUi();
                var visible = GetVisibleDataRows().Select(r => r.ToRow());
                var exportPath = _exportService.ExportRowsVisible(visible, settings);
                SetStatus(!string.IsNullOrWhiteSpace(exportPath) ? $"[OK] Экспорт: {exportPath}" : "Экспорт отменен.");
            }
            catch (Exception ex)
            {
                SetStatus($"[ERROR] Ошибка экспорта: {ex.Message}");
            }
        }

        private void BtnExportSelected_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = PullSettingsFromUi();
                var selected = _rowsAll.Where(r => r.IsChecked).Select(r => r.ToRow());
                var exportPath = _exportService.ExportRowsSelected(selected, settings);
                SetStatus(!string.IsNullOrWhiteSpace(exportPath) ? $"[OK] Экспорт: {exportPath}" : "Экспорт отменен.");
            }
            catch (Exception ex)
            {
                SetStatus($"[ERROR] Ошибка экспорта: {ex.Message}");
            }
        }

        private const int PaletteLongNameThreshold = 42;

        private void GridResults_OnLoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (!(e.Row.Item is GridRowModel gr) || gr.IsSummary)
            {
                return;
            }

            var name = gr.Data?.NameFromSpec ?? gr.NameFromSpec ?? string.Empty;
            var hasNewline = name.IndexOf('\n') >= 0;
            var isLong = name.Length >= PaletteLongNameThreshold || hasNewline;
            if (!isLong)
            {
                e.Row.MinHeight = 28;
                return;
            }

            var lineCount = Math.Max(1, (name.Length / PaletteLongNameThreshold) + (hasNewline ? name.Split('\n').Length : 1));
            e.Row.MinHeight = Math.Min(120, Math.Max(32, lineCount * 20));
        }

        private void GridResults_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInternalUpdate)
            {
                return;
            }

            RecomputeStats();
        }

        private void BtnShowOnDrawing_OnClick(object sender, RoutedEventArgs e)
        {
            var doc = AcAp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                SetStatus("[ERROR] Нет активного документа.");
                return;
            }

            var rows = GetRowsForShowOnDrawing().ToList();
            if (rows.Count == 0)
            {
                SetStatus("Отметьте строки галочками и/или выделите строки в таблице (Ctrl+клик), затем «Показать на чертеже».");
                return;
            }

            var allHandles = rows
                .SelectMany(r => r.SourceHandles ?? Enumerable.Empty<string>())
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (allHandles.Count == 0)
            {
                SetStatus("[INFO] У выбранных строк нет привязанных объектов на чертеже.");
                return;
            }

            // Execute highlight inside AutoCAD command context (more stable than calling SetImpliedSelection from WPF click).
            _pendingHighlightRowCount = rows.Count;
            _pendingHighlightSum = rows.Sum(r => r.Count);
            SetStatus("Подсветка...");
            PaletteHost.RequestHighlightByHandles(allHandles);
        }

        private int _pendingHighlightRowCount;
        private int _pendingHighlightSum;

        internal void ApplyHighlightResult(int highlighted, string error)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                SetStatus($"[ERROR] Подсветка: {error}");
                return;
            }

            if (highlighted > 0)
            {
                SetStatus(_pendingHighlightRowCount > 1
                    ? $"[OK] Строк: {_pendingHighlightRowCount}, Σ в таблице: {_pendingHighlightSum}, подсвечено объектов: {highlighted}"
                    : $"[OK] Подсвечено объектов: {highlighted}");
            }
            else
            {
                SetStatus("[INFO] Не удалось разрешить объекты по сохранённым handle (возможно, другой чертёж или объекты удалены).");
            }
        }

        /// <summary>
        /// Objects to highlight: union of rows with checkbox checked (как для экспорта) and rows blue-selected in the grid (Ctrl/Shift).
        /// </summary>
        private IEnumerable<PosRowVm> GetRowsForShowOnDrawing()
        {
            var set = new HashSet<PosRowVm>();
            foreach (var r in _rowsAll ?? Enumerable.Empty<PosRowVm>())
            {
                if (r != null && r.IsChecked)
                {
                    set.Add(r);
                }
            }

            foreach (var r in GetDataGridSelectedRows())
            {
                set.Add(r);
            }

            return set;
        }

        /// <summary>Rows currently selected in the DataGrid (Ctrl/Shift multi-select).</summary>
        private IEnumerable<PosRowVm> GetDataGridSelectedRows()
        {
            if (GridResults?.SelectedItems == null)
            {
                yield break;
            }

            foreach (var item in GridResults.SelectedItems)
            {
                if (item is GridRowModel gr && !gr.IsSummary && gr.Data != null)
                {
                    yield return gr.Data;
                }
            }
        }

        // Highlight now runs inside POSC2_HIGHLIGHT_INTERNAL command context (see Commands.cs).

        private void ChkSelectAllVisible_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(sender is CheckBox cb))
            {
                return;
            }

            var shouldCheck = cb.IsChecked == true;
            _isInternalUpdate = true;
            try
            {
                foreach (var row in GetVisibleDataRows())
                {
                    row.IsChecked = shouldCheck;
                }
            }
            finally
            {
                _isInternalUpdate = false;
            }

            RecomputeStats();
        }

        private void RowCheckBox_OnClick(object sender, RoutedEventArgs e)
        {
            RecomputeStats();
        }

        private void RecomputeStats()
        {
            try
            {
                var total = _rowsAll?.Count ?? 0;
                var visible = GetVisibleDataRows().Count();
                var selectedVisible = GetVisibleDataRows().Count(r => r.IsChecked);
                var gridSel = GetDataGridSelectedRows().ToList();
                var tableSelCount = gridSel.Count;
                var tableSelSum = gridSel.Sum(r => r.Count);

                LblTotalRows.Text = total.ToString();
                LblVisibleRows.Text = visible.ToString();
                LblSelectedRows.Text = selectedVisible.ToString();
                LblTableSelectionRows.Text = tableSelCount.ToString();
                LblTableSelectionSum.Text = tableSelSum.ToString();
            }
            catch
            {
                // ignore
            }
        }

        private void RebuildFiltersUi()
        {
            BuildFilterList(ListFilterText, GetDistinctTexts(), _filterText, TxtSearchText?.Text);
            BuildFilterList(ListFilterName, GetDistinctNames(), _filterName, TxtSearchName?.Text);
            BuildFilterList(ListFilterCount, GetDistinctCounts(), _filterCount, TxtSearchCount?.Text);
            BuildFilterList(ListFilterLayer, GetDistinctLayers(), _filterLayer, TxtSearchLayer?.Text, onFilterChanged: ReapplyMarkNamesForLayerFilter);
            RefreshAllCheckboxes();
        }

        private void RefreshAllCheckboxes()
        {
            try
            {
                if (ChkFilterTextAll != null)
                    ChkFilterTextAll.IsChecked = IsAllSelectedForSearch(_filterText, GetDistinctTexts(), TxtSearchText?.Text);
                if (ChkFilterNameAll != null)
                    ChkFilterNameAll.IsChecked = IsAllSelectedForSearch(_filterName, GetDistinctNames(), TxtSearchName?.Text);
                if (ChkFilterCountAll != null)
                    ChkFilterCountAll.IsChecked = IsAllSelectedForSearch(_filterCount, GetDistinctCounts(), TxtSearchCount?.Text);
                if (ChkFilterLayerAll != null)
                    ChkFilterLayerAll.IsChecked = IsAllSelectedForSearch(_filterLayer, GetDistinctLayers(), TxtSearchLayer?.Text);
            }
            catch
            {
                // ignore
            }
        }

        private static bool IsAllSelectedForSearch(HashSet<string> activeSet, IEnumerable<string> allValues, string search)
        {
            try
            {
                if (activeSet == null)
                {
                    return true;
                }

                var filter = search ?? string.Empty;
                foreach (var v in (allValues ?? Enumerable.Empty<string>())
                             .Where(v => (v ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    if (!activeSet.Contains(v ?? string.Empty))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private IEnumerable<string> GetDistinctTexts()
        {
            return (_rowsAll ?? new ObservableCollection<PosRowVm>())
                .Select(r => r.Text ?? string.Empty)
                .Distinct()
                .OrderBy(s =>
                {
                    if (MTextPlainText.TryParseMarkKey(s, out var k))
                    {
                        return k.ToString("D8");
                    }

                    return s;
                }, StringComparer.OrdinalIgnoreCase);
        }

        private IEnumerable<string> GetDistinctNames()
        {
            return (_rowsAll ?? new ObservableCollection<PosRowVm>())
                .Select(r => r.NameFromSpec ?? string.Empty)
                .Distinct()
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);
        }

        private IEnumerable<string> GetDistinctCounts()
        {
            return (_rowsAll ?? new ObservableCollection<PosRowVm>())
                .Select(r => r.Count.ToString())
                .Distinct()
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);
        }

        private IEnumerable<string> GetDistinctLayers()
        {
            return (_rowsAll ?? new ObservableCollection<PosRowVm>())
                .Select(r => r.Layer ?? string.Empty)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);
        }

        private void BuildFilterList(
            ItemsControl host,
            IEnumerable<string> values,
            HashSet<string> activeSet,
            string search,
            Action onFilterChanged = null)
        {
            if (host == null)
            {
                return;
            }

            host.Items.Clear();
            var filter = search ?? string.Empty;
            foreach (var val in values.Where(v => v.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                var cb = new CheckBox
                {
                    Content = val,
                    IsChecked = activeSet.Contains(val),
                    Margin = new Thickness(0, 2, 0, 2)
                };
                cb.Checked += (s, e) =>
                {
                    activeSet.Add(val);
                    onFilterChanged?.Invoke();
                    RefreshGridRows();
                };
                cb.Unchecked += (s, e) =>
                {
                    activeSet.Remove(val);
                    onFilterChanged?.Invoke();
                    RefreshGridRows();
                };
                host.Items.Add(cb);
            }
        }

        private void BtnFilterText_OnClick(object sender, RoutedEventArgs e)
        {
            RebuildFiltersUi();
            PopupFilterText.IsOpen = true;
        }

        private void BtnFilterCount_OnClick(object sender, RoutedEventArgs e)
        {
            RebuildFiltersUi();
            PopupFilterCount.IsOpen = true;
        }

        private void BtnFilterLayer_OnClick(object sender, RoutedEventArgs e)
        {
            RebuildFiltersUi();
            PopupFilterLayer.IsOpen = true;
        }

        private void BtnFilterName_OnClick(object sender, RoutedEventArgs e)
        {
            RebuildFiltersUi();
            PopupFilterName.IsOpen = true;
        }

        private void TxtSearchText_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            RebuildFiltersUi();
        }

        private void TxtSearchCount_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            RebuildFiltersUi();
        }

        private void TxtSearchLayer_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            RebuildFiltersUi();
        }

        private void TxtSearchName_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            RebuildFiltersUi();
        }

        private void ChkFilterTextAll_OnClick(object sender, RoutedEventArgs e)
        {
            ApplyAllToggleForFilter(_filterText, GetDistinctTexts(), TxtSearchText?.Text, ChkFilterTextAll?.IsChecked == true);
        }

        private void ChkFilterCountAll_OnClick(object sender, RoutedEventArgs e)
        {
            ApplyAllToggleForFilter(_filterCount, GetDistinctCounts(), TxtSearchCount?.Text, ChkFilterCountAll?.IsChecked == true);
        }

        private void ChkFilterLayerAll_OnClick(object sender, RoutedEventArgs e)
        {
            ApplyAllToggleForFilter(
                _filterLayer,
                GetDistinctLayers(),
                TxtSearchLayer?.Text,
                ChkFilterLayerAll?.IsChecked == true,
                onFilterChanged: ReapplyMarkNamesForLayerFilter);
        }

        private void ChkFilterNameAll_OnClick(object sender, RoutedEventArgs e)
        {
            ApplyAllToggleForFilter(_filterName, GetDistinctNames(), TxtSearchName?.Text, ChkFilterNameAll?.IsChecked == true);
        }

        private void ApplyAllToggleForFilter(
            HashSet<string> activeSet,
            IEnumerable<string> allValues,
            string search,
            bool selectAll,
            Action onFilterChanged = null)
        {
            if (activeSet == null)
            {
                return;
            }

            var filter = search ?? string.Empty;
            var visible = (allValues ?? Enumerable.Empty<string>())
                .Where(v => (v ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(v => v ?? string.Empty)
                .ToList();

            if (selectAll)
            {
                foreach (var v in visible)
                {
                    activeSet.Add(v);
                }
            }
            else
            {
                // Excel-like: uncheck "All" clears the current visible subset.
                foreach (var v in visible)
                {
                    activeSet.Remove(v);
                }
            }

            onFilterChanged?.Invoke();
            RefreshGridRows();
            RebuildFiltersUi();
        }

        private void SetStatus(string message)
        {
            // Status line is implemented as a Run inside a wrapping TextBlock.
            StatusText.Text = message ?? string.Empty;
        }

        internal void SetStatusFromHost(string message) => SetStatus(message);

        private sealed class PosRowVm : INotifyPropertyChanged
        {
            private bool _isChecked;
            private string _nameFromSpec;
            private string _nameSource;
            private bool _nameFromProgram;

            public PosRowVm(PosRow row)
            {
                Text = row?.Text ?? string.Empty;
                Layer = row?.Layer ?? string.Empty;
                Count = row?.Count ?? 0;
                Key = row?.Key ?? ParseKey(Text);
                NameFromSpec = row?.NameFromSpec ?? string.Empty;
                NameSource = row?.NameSource ?? string.Empty;
                SourceHandles = row?.SourceHandles != null
                    ? new List<string>(row.SourceHandles)
                    : new List<string>();
            }

            public string Text { get; }
            public int? Key { get; }
            public int Count { get; }
            public string Layer { get; }
            public List<string> SourceHandles { get; }

            public string NameFromSpec
            {
                get => _nameFromSpec;
                set
                {
                    if (_nameFromSpec == value)
                    {
                        return;
                    }

                    _nameFromSpec = value ?? string.Empty;
                    if (!_nameFromProgram)
                    {
                        _nameSource = "user";
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameSource)));
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameFromSpec)));
                }
            }

            internal void SetNameFromProgram(string value, string source)
            {
                _nameFromProgram = true;
                try
                {
                    _nameFromSpec = value ?? string.Empty;
                    _nameSource = source ?? string.Empty;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameFromSpec)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameSource)));
                }
                finally
                {
                    _nameFromProgram = false;
                }
            }

            public string NameSource
            {
                get => _nameSource;
                set
                {
                    if (_nameSource == value)
                    {
                        return;
                    }

                    _nameSource = value ?? string.Empty;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameSource)));
                }
            }

            public bool IsChecked
            {
                get => _isChecked;
                set
                {
                    if (_isChecked == value) return;
                    _isChecked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                }
            }

            public PosRow ToRow()
            {
                return new PosRow
                {
                    Text = Text,
                    Layer = Layer,
                    Count = Count,
                    Key = Key,
                    NameFromSpec = NameFromSpec,
                    NameSource = NameSource,
                    IsTotalLine = false,
                    SourceHandles = SourceHandles != null
                        ? new List<string>(SourceHandles)
                        : new List<string>()
                };
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        /// <summary>Строка таблицы: данные позиции или итог по слою / общий итог.</summary>
        private sealed class GridRowModel
        {
            private GridRowModel(PosRowVm data, string summaryText, int summaryCount, string summaryLayer)
            {
                if (data != null)
                {
                    IsSummary = false;
                    Data = data;
                    Text = data.Text;
                    NameFromSpec = data.NameFromSpec ?? string.Empty;
                    Count = data.Count;
                    Layer = data.Layer ?? string.Empty;
                }
                else
                {
                    IsSummary = true;
                    Data = null;
                    Text = summaryText ?? string.Empty;
                    NameFromSpec = string.Empty;
                    Count = summaryCount;
                    Layer = summaryLayer ?? string.Empty;
                }
            }

            public bool IsSummary { get; }
            public PosRowVm Data { get; }
            public string Text { get; }
            public string NameFromSpec { get; }
            public int Count { get; }
            public string Layer { get; }

            public static GridRowModel FromDataRow(PosRowVm row) => new GridRowModel(row, null, 0, null);

            public static GridRowModel FromFooterRow(PosRow footer) =>
                new GridRowModel(null, footer?.Text, footer?.Count ?? 0, footer?.Layer);
        }
    }
}
