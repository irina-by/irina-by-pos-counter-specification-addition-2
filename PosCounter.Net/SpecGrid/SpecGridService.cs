using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using PosCounter.Net.Models;

namespace PosCounter.Net.SpecGrid
{
    internal sealed class SpecPickResult
    {
        public bool Success;
        public string Error;
        public int QtyWritten;
        public int QtySkipped;
        /// <summary>Марки в таблице, для которых в палитре нет количества (§19.16).</summary>
        public List<int> MissingQtyMarks = new List<int>();
    }

    /// <summary>§19.15: оформление штатного текста «Кол.» (не примечание инженера).</summary>
    internal sealed class QtyTableTextAppearance
    {
        public bool Found;
        public ObjectId TextStyleId = ObjectId.Null;
        public string Layer;
        /// <summary>§19.19: цвет с штатного текста таблицы (не ByLayer слоя примечания).</summary>
        public Color EntityColor = Color.FromColorIndex(ColorMethod.ByLayer, 256);
        public bool HasEntityColor;
        public LineWeight LineWeight = LineWeight.ByLayer;
        /// <summary>Высота из текста спецификации (ColQty → тело таблицы).</summary>
        public double TextHeight;
        public bool HasTextHeight;
    }

    internal static class SpecGridService
    {
        /// <summary>Запасная высота «Кол.», если в таблице нет образца текста.</summary>
        public const double QtyTextHeightFallback = 2.5;

        public static SpecPickResult RunSelectSpecification(Document doc, IReadOnlyDictionary<int, int> qtyByKey, SpecGridLog log)
        {
            var result = new SpecPickResult();
            if (doc == null)
            {
                result.Error = "Нет активного документа";
                return result;
            }

            SpecGridLog.ResetDiagSession(doc);
            SpecDiagPolicy.ResetSession();
            SpecGridLog.WriteDiag(doc, SpecGridLog.FormatDllBuildStamp());
            var paletteKeyCount = qtyByKey?.Count ?? 0;
            SpecGridLog.WriteDiag(doc, $"Палитра qty: ключей={paletteKeyCount} (перед спецификацией)");

            if (!TryPickAllSpecificationTables(doc, out var tablePicks, out var pickError))
            {
                result.Error = pickError;
                return result;
            }

            using (doc.LockDocument())
            {
                try
                {
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        SpecGridSession.ClearScopes();
                        SpecGridSession.SharedGridLayer = null;
                        var builtScopes = new List<ScopeGridResult>();
                        for (var i = 0; i < tablePicks.Count; i++)
                        {
                            var hint = i > 0 ? SpecGridSession.SharedGridLayer : null;
                            var scope = TableGridBuilder.Build(i, tablePicks[i], tr, hint, log);
                            var scopeNum = i + 1;
                            SpecGridLog.WriteDiag(
                                doc,
                                $"Таблица {scopeNum} pass1: ColMark={scope.Pass1ColMark} ColName={scope.Pass1ColName} ColQty={scope.Pass1ColQty} topBand={scope.Pass1HeaderDetectedByTopTextBand}");

                            if (i == 0)
                            {
                                if (TableGridBuilder.TryLockColumnSchema(scope, log))
                                {
                                    if (scope.ColQty >= 0 && !string.IsNullOrWhiteSpace(scope.ColQtySource))
                                    {
                                        SpecGridLog.WriteDiag(
                                            doc,
                                            $"Таблица {scopeNum} ColQty={scope.ColQty} источник={scope.ColQtySource}");
                                    }
                                }
                                else if (scope.Valid
                                    && (scope.ColMark < 0 || scope.ColName < 0)
                                    && TableGridBuilder.TryInferColumnsFromData(scope, qtyByKey, log))
                                {
                                    ProcessInferColumnsFallback(doc, scope, scopeNum, log);
                                }
                                else if (tablePicks.Count > 1)
                                {
                                    SpecGridLog.WriteCommandLine(
                                        doc,
                                        "[POSC] Шапка не найдена в первой таблице — выделите лист с подписями Поз./Наименование/Кол.");
                                }
                            }
                            else if (SpecGridSession.ColumnSchema?.IsLocked == true)
                            {
                                if (TableGridBuilder.TryApplyInheritedColumnSchema(
                                        scope,
                                        SpecGridSession.ColumnSchema,
                                        log,
                                        out var inheritFailReason))
                                {
                                    TableGridBuilder.RebindScopeKeysAndNames(
                                        scope,
                                        scope.HorizForBind,
                                        log,
                                        passLabel: "inherited-schema");
                                    TableGridBuilder.FillMarkNamesFromMergeGroupsPublic(scope, log);
                                    TableGridBuilder.ApplyStandardColumnLayout(scope, log);
                                    SpecGridLog.WriteDiag(
                                        doc,
                                        $"Таблица {scopeNum} ColQty={scope.ColQty} источник={scope.ColQtySource}");
                                }
                                else
                                {
                                    var reason = string.IsNullOrWhiteSpace(inheritFailReason)
                                        ? "unknown"
                                        : inheritFailReason;
                                    SpecGridLog.WriteDiag(
                                        doc,
                                        $"[SCHEMA] inherit-fail scope={scope.ScopeIndex} reason={reason}");

                                    var objCount = scope.PickedObjectIds?.Count ?? scope.LineCount;
                                    if (TableGridBuilder.IsContinuationPickTooSmall(scope))
                                    {
                                        SpecGridLog.WriteCommandLine(
                                            doc,
                                            $"[POSC] Рамка продолжения слишком мала ({objCount} объектов) — выделите весь лист «Продолжение» (~200+ объектов)");
                                    }
                                    else if (scope.Valid
                                        && TableGridBuilder.TryInferColumnsFromData(scope, qtyByKey, log))
                                    {
                                        SpecGridLog.WriteDiag(doc, "[SCHEMA] inherit-fail → fallback infer-data");
                                        ProcessInferColumnsFallback(doc, scope, scopeNum, log);
                                    }
                                    else
                                    {
                                        SpecGridLog.WriteCommandLine(
                                            doc,
                                            $"[POSC] Схема столбцов не применена к таблице {scopeNum} — проверьте рамку продолжения.");
                                    }
                                }
                            }
                            else if (scope.Valid
                                && (scope.ColMark < 0 || scope.ColName < 0)
                                && TableGridBuilder.TryInferColumnsFromData(scope, qtyByKey, log))
                            {
                                ProcessInferColumnsFallback(doc, scope, scopeNum, log);
                            }

                            TableGridBuilder.BuildKeyToRowMarkSampleDiag(scope);
                            if (!string.IsNullOrWhiteSpace(scope.KeyToRowMarkSampleDiag))
                            {
                                SpecGridLog.WriteTrace("MARK", $"табл.{scopeNum} {scope.KeyToRowMarkSampleDiag}");
                            }

                            ReportUnassignedTextsDiagnostic(doc, scope, scopeNum);
                            TableGridBuilder.ReportHeaderTraceDiagnostic(scope, scopeNum);
                            TableGridBuilder.ReportMarkNamesDiagnostic(doc, scope, scopeNum);

                            builtScopes.Add(scope);
                            if (i == 0 && !string.IsNullOrWhiteSpace(scope.GridLayer))
                            {
                                SpecGridSession.SharedGridLayer = scope.GridLayer;
                            }
                        }

                        SpecGridSession.SetScopes(builtScopes);

                        ReportGridBuildWarnings(doc, builtScopes);
                        ReportDetectedHeader(doc, builtScopes);
                        ReportEmptyMarkColumnWarnings(doc, builtScopes);

                        foreach (var scope in builtScopes)
                        {
                            if (scope?.Valid == true)
                            {
                                TableGridBuilder.ApplyStandardColumnLayout(scope, log);
                            }
                        }

                        result.QtyWritten = WriteQtyInTransaction(tr, qtyByKey, log, out var skipped);
                        result.QtySkipped = skipped;
                        result.MissingQtyMarks = CollectMissingQtyMarks(qtyByKey);
                        SpecDiagPolicy.RegisterMissingQtyKeys(result.MissingQtyMarks);
                        ReportWriteQtyDiagnostic(doc, builtScopes, result.QtyWritten, skipped);
                        ReportScopeSummaryDiagnostic(doc, builtScopes);
                        var combinedNames = BuildCombinedMarkNames();
                        ReportPaletteVsScopeNamesDiagnostic(doc, qtyByKey, combinedNames);
                        ReportKvSummaryDiagnostic(doc, builtScopes, qtyByKey, result.QtyWritten);
                        tr.Commit();
                    }

                    ReportMissingQtyMarks(doc, result.MissingQtyMarks);

                    try
                    {
                        doc.Editor?.Regen();
                    }
                    catch
                    {
                        // ignore
                    }
                }
                catch (Exception ex)
                {
                    result.Error = ex.Message;
                    return result;
                }
            }

            result.Success = true;
            return result;
        }

        private static void ProcessInferColumnsFallback(
            Document doc,
            ScopeGridResult scope,
            int scopeNum,
            SpecGridLog log)
        {
            SpecGridLog.WriteDiag(
                doc,
                $"Таблица {scopeNum}: fallback «столбцы по данным» (pass1: ColMark={scope.Pass1ColMark} ColName={scope.Pass1ColName} ColQty={scope.Pass1ColQty})");
            if (!string.IsNullOrWhiteSpace(scope.InferenceColQtyScoresSummary))
            {
                SpecGridLog.WriteDiag(doc, scope.InferenceColQtyScoresSummary);
            }

            if (!string.IsNullOrWhiteSpace(scope.DbTextHeaderBandSummary))
            {
                SpecGridLog.WriteDiag(doc, scope.DbTextHeaderBandSummary);
            }

            if (scope.ColQty < 0)
            {
                TableGridBuilder.TryResolveMissingColQty(scope, log);
            }

            TableGridBuilder.ApplyStandardColumnLayout(scope, log);

            if (scope.ColQty >= 0 && !string.IsNullOrWhiteSpace(scope.ColQtySource))
            {
                SpecGridLog.WriteDiag(
                    doc,
                    $"Таблица {scopeNum} ColQty={scope.ColQty} источник={scope.ColQtySource}");
            }
            else if (!string.IsNullOrWhiteSpace(scope.ColQtyFallbackDiag))
            {
                SpecGridLog.WriteDiag(doc, $"ColQty fallback: {scope.ColQtyFallbackDiag}");
            }

            TableGridBuilder.RebindScopeKeysAndNames(
                scope,
                scope.HorizForBind,
                log,
                passLabel: "infer-data");
            TableGridBuilder.FillMarkNamesFromMergeGroupsPublic(scope, log);
        }

        private static void ReportDetectedHeader(Document doc, IReadOnlyList<ScopeGridResult> scopes)
        {
            if (doc == null || scopes == null)
            {
                return;
            }

            for (var i = 0; i < scopes.Count; i++)
            {
                var scope = scopes[i];
                if (scope == null)
                {
                    continue;
                }

                var scopeNum = scope.ScopeIndex + 1;
                if (scope.IsNativeAcadTable)
                {
                    var rows = scope.CellText?.GetLength(0) ?? 0;
                    var cols = scope.CellText?.GetLength(1) ?? 0;
                    SpecGridLog.WriteCommandLine(
                        doc,
                        $"[POSC] Таблица AutoCAD ({rows}×{cols} ячеек), источник=Table");
                }

                if (scope.MixedTableLineSelection)
                {
                    SpecGridLog.WriteCommandLine(
                        doc,
                        "[POSC] Mixed selection (Table + Line), using LINE path");
                }

                var mark = DescribeHeaderColumn(scope, scope.ColMark);
                var name = DescribeHeaderColumn(scope, scope.ColName);
                var qty = DescribeHeaderColumn(scope, scope.ColQty);
                var inferred = scope.ColumnsInheritedFromSchema
                    ? " (столбцы от эталона)"
                    : scope.ColumnsInferredFromData
                        ? " (столбцы по данным)"
                        : string.Empty;
                var msg =
                    $"[POSC] Распознана шапка (таблица {scopeNum}){inferred}: " +
                    $"Марка — {(scope.ColMark >= 0 ? $"столбец {scope.ColMark} «{mark}»" : "не найдена")}; " +
                    $"Наименование — {(scope.ColName >= 0 ? $"столбец {scope.ColName} «{name}»" : "не найдено")}; " +
                    $"Кол. — {(scope.ColQty >= 0 ? $"столбец {scope.ColQty} «{qty}»" : "не найдена")}.";
                SpecGridLog.WriteCommandLine(doc, msg);

                if (!string.IsNullOrWhiteSpace(scope.GridLayer))
                {
                    SpecGridLog.WriteCommandLine(
                        doc,
                        $"[POSC] Сетка таблицы {scopeNum}: gridLayer=«{scope.GridLayer}» merged={scope.GridAxesMergedFromMixedLayers}");
                }

                if (scope.UnassignedTextCountAfterDataPass > 0)
                {
                    SpecGridLog.WriteCommandLine(
                        doc,
                        $"[POSC] Текстов вне ячеек сетки (таблица {scopeNum}): {scope.UnassignedTextCountAfterDataPass} из {scope.TextCount}");
                }

                if (scope.HeaderEndRow > 0 || scope.RowDataStart > 0)
                {
                    SpecGridLog.WriteCommandLine(
                        doc,
                        $"[POSC] Граница шапки/данных: HeaderEndRow={scope.HeaderEndRow} RowDataStart={scope.RowDataStart}");
                }

                var refineMsg = TableGridBuilder.FormatColMarkRefineMessage(scope);
                if (!string.IsNullOrWhiteSpace(refineMsg))
                {
                    SpecGridLog.WriteCommandLine(doc, refineMsg);
                }

                if (scope.Valid && scope.ColMark >= 0)
                {
                    var markCounts = TableGridBuilder.FormatDataMarkCountsDiagnostic(scope);
                    if (!string.IsNullOrWhiteSpace(markCounts))
                    {
                        SpecGridLog.WriteCommandLine(
                            doc,
                            $"[POSC] Марок в данных по столбцам: {markCounts} (ColMark={scope.ColMark})");
                    }

                    if (scope.KeyToRowMark.Count > 0)
                    {
                        var keyMap = string.Join(
                            ", ",
                            scope.KeyToRowMark.OrderBy(x => x.Key).Select(x => $"{x.Key}→row{x.Value}"));
                        SpecGridLog.WriteCommandLine(doc, $"[POSC] KeyToRowMark: {keyMap}");
                    }

                    var missingKey1 = TableGridBuilder.FormatMissingKeyOneDiagnostic(scope);
                    if (!string.IsNullOrWhiteSpace(missingKey1))
                    {
                        SpecGridLog.WriteCommandLine(doc, missingKey1);
                    }
                }

                if (!scope.Valid)
                {
                    continue;
                }

                if (scope.ColMark < 0)
                {
                    SpecGridLog.WriteCommandLine(
                        doc,
                        "[POSC] Колонка «Поз./Марка» не распознана — проверьте заголовок в шапке таблицы.");
                }
                else if (mark == "—")
                {
                    SpecGridLog.WriteCommandLine(
                        doc,
                        $"[POSC] Заголовок столбца Марка не прочитан (столбец {scope.ColMark}) — проверьте MText/слой в шапке.");
                }

                if (scope.ColQty < 0)
                {
                    SpecGridLog.WriteCommandLine(
                        doc,
                        "[POSC] Колонка «Кол.» не распознана — проверьте заголовок (MText) и строку шапки в таблице.");
                }
                else if (qty == "—")
                {
                    SpecGridLog.WriteCommandLine(
                        doc,
                        $"[POSC] Заголовок столбца «Кол.» не прочитан (столбец {scope.ColQty}) — проверьте MText/слой в шапке.");
                }

                if (scope.ColName < 0)
                {
                    SpecGridLog.WriteCommandLine(
                        doc,
                        "[POSC] Колонка «Наименование» не распознана — проверьте заголовок в шапке таблицы.");
                }
                else if (name == "—")
                {
                    SpecGridLog.WriteCommandLine(
                        doc,
                        $"[POSC] Заголовок столбца Наименование не прочитан (столбец {scope.ColName}) — проверьте MText/слой в шапке.");
                }

                var needsHeaderDiag = scope.ColMark < 0
                    || scope.ColName < 0
                    || scope.ColumnsInferredFromData
                    || scope.ColQty < 0;

                if (needsHeaderDiag)
                {
                    var dataHint = TableGridBuilder.FormatHeaderDataBandHint(scope);
                    if (!string.IsNullOrWhiteSpace(dataHint))
                    {
                        SpecGridLog.WriteCommandLine(doc, dataHint);
                    }

                    var diag = TableGridBuilder.BuildHeaderDiagnosticMessage(scope);
                    if (!string.IsNullOrWhiteSpace(diag))
                    {
                        SpecGridLog.WriteCommandLine(
                            doc,
                            $"[POSC] Диагностика шапки (таблица {scopeNum}): {diag}");
                        SpecGridLog.WriteDiag(doc, $"Таблица {scopeNum} шапка: {diag}");
                    }

                    foreach (var line in TableGridBuilder.BuildHeaderTopBandDiagnostic(scope))
                    {
                        SpecGridLog.WriteCommandLine(doc, line);
                    }

                    if (scope.ColumnsInferredFromData || scope.ColQty < 0)
                    {
                        foreach (var line in TableGridBuilder.BuildHeaderTopBandDiagnosticHeaderCoords(scope))
                        {
                            SpecGridLog.WriteDiag(doc, $"Таблица {scopeNum} {line}");
                        }
                    }

                    if (!scope.HeaderDetectedByTopTextBand && TableGridBuilder.IsAllHeaderGeomZero(scope))
                    {
                        foreach (var line in TableGridBuilder.BuildHeaderExtendedDiagnostic(scope))
                        {
                            SpecGridLog.WriteCommandLine(doc, line);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(scope.DbTextHeaderBandSummary))
                    {
                        SpecGridLog.WriteDiag(doc, scope.DbTextHeaderBandSummary);
                    }

                    if (scope.ColQty >= 0 && !string.IsNullOrWhiteSpace(scope.ColQtySource))
                    {
                        SpecGridLog.WriteDiag(
                            doc,
                            $"Таблица {scopeNum} ColQty={scope.ColQty} источник={scope.ColQtySource}");
                    }
                    else if (!string.IsNullOrWhiteSpace(scope.ColQtyFallbackDiag))
                    {
                        SpecGridLog.WriteDiag(doc, $"ColQty fallback: {scope.ColQtyFallbackDiag}");
                    }
                }
            }
        }

        private static void ReportUnassignedTextsDiagnostic(Document doc, ScopeGridResult scope, int scopeNum)
        {
            if (doc == null || scope == null)
            {
                return;
            }

            if (scope.UnassignedNameFixLines != null && scope.UnassignedNameFixLines.Count > 0)
            {
                foreach (var line in scope.UnassignedNameFixLines.Take(5))
                {
                    SpecGridLog.WriteDiag(doc, $"Таблица {scopeNum} {line}");
                }
            }

            if (scope.UnassignedTextCountAfterDataPass <= 0)
            {
                return;
            }

            SpecGridLog.WriteDiag(
                doc,
                $"Таблица {scopeNum} вне сетки: {scope.UnassignedTextCountAfterDataPass}/{scope.TextCount}");
            foreach (var line in TableGridBuilder.BuildUnassignedTextSamples(scope))
            {
                SpecGridLog.WriteDiag(doc, $"  {line}");
            }
        }

        private static void ReportWriteQtyDiagnostic(
            Document doc,
            IReadOnlyList<ScopeGridResult> scopes,
            int qtyWritten,
            int skipped)
        {
            if (doc == null || scopes == null)
            {
                return;
            }

            foreach (var scope in scopes)
            {
                if (scope == null || !scope.Valid)
                {
                    continue;
                }

                if (scope.ColQty < 0)
                {
                    SpecGridLog.WriteDiagTail(
                        doc,
                        $"Таблица {scope.ScopeIndex + 1} WriteQty пропущен: ColQty=-1");
                }
            }

            foreach (var scope in scopes)
            {
                if (scope?.WriteQtyDiagLines == null || scope.WriteQtyDiagLines.Count == 0)
                {
                    continue;
                }

                foreach (var line in scope.WriteQtyDiagLines.Take(15))
                {
                    SpecGridLog.WriteDiagTail(doc, $"Таблица {scope.ScopeIndex + 1} {line}");
                }
            }

            SpecGridLog.WriteDiagTail(doc, $"WriteQty итог: записано={qtyWritten}, пропущено={skipped}");
        }

        private static void ReportScopeSummaryDiagnostic(Document doc, IReadOnlyList<ScopeGridResult> scopes)
        {
            if (doc == null || scopes == null)
            {
                return;
            }

            foreach (var scope in scopes)
            {
                if (scope == null)
                {
                    continue;
                }

                var scopeNum = scope.ScopeIndex + 1;
                var namesFilled = scope.MarkNamePairs?.Count(kv => !string.IsNullOrWhiteSpace(kv.Value)) ?? 0;
                var qtySource = string.IsNullOrWhiteSpace(scope.ColQtySource)
                    ? string.Empty
                    : $" источник={scope.ColQtySource}";
                var layoutFix = string.IsNullOrWhiteSpace(scope.ColQtyLayoutFixDiag)
                    ? string.Empty
                    : $" layout={scope.ColQtyLayoutFixDiag}";
                var colQtyWarn = scope.ColQty < 0 ? " ВНИМАНИЕ: ColQty не найден — WriteQty пропущен" : string.Empty;
                SpecGridLog.WriteDiagTail(
                    doc,
                    $"Таблица {scopeNum} ИТОГ: ColMark={scope.ColMark} ColName={scope.ColName} ColQty={scope.ColQty}{qtySource}{layoutFix}{colQtyWarn} | KeyToRowMark={scope.KeyToRowMark?.Count ?? 0} | имена={namesFilled}");
            }

            var combined = BuildCombinedMarkNames();
            SpecGridLog.WriteDiagTail(doc, $"Имена в палитру (всего ключей): {combined.Count}");
            SpecGridLog.WriteDiagTail(doc, SpecGridLog.FormatDllBuildStamp());
        }

        private static void ReportPaletteVsScopeNamesDiagnostic(
            Document doc,
            IReadOnlyDictionary<int, int> qtyByKey,
            Dictionary<int, string> namesFromTables)
        {
            if (doc == null)
            {
                return;
            }

            var paletteKeyCount = qtyByKey?.Count ?? 0;
            var namesCount = namesFromTables?.Count ?? 0;
            if (paletteKeyCount <= 0 || namesCount >= paletteKeyCount)
            {
                return;
            }

            var nameKeys = new HashSet<int>(namesFromTables?.Keys ?? Enumerable.Empty<int>());
            var missingList = (qtyByKey?.Keys ?? Enumerable.Empty<int>())
                .Where(k => !nameKeys.Contains(k))
                .OrderBy(k => k)
                .Take(12)
                .ToList();

            SpecGridLog.WriteCommandLine(
                doc,
                $"[POSC] Палитра: ключей={paletteKeyCount}, имён из выбранных таблиц={namesCount}.");
            if (missingList.Count > 0)
            {
                SpecGridLog.WriteCommandLine(
                    doc,
                    $"[POSC] Без имени (нет на выделенных листах): {string.Join(", ", missingList)}");
            }

            SpecGridLog.WriteCommandLine(
                doc,
                "[POSC] Выделите все листы спецификации, если нужны имена для всех марок палитры.");
        }

        private static void ReportKvSummaryDiagnostic(
            Document doc,
            IReadOnlyList<ScopeGridResult> scopes,
            IReadOnlyDictionary<int, int> qtyByKey,
            int totalQtyWritten)
        {
            if (doc == null || scopes == null)
            {
                return;
            }

            var writtenByScope = new Dictionary<int, int>();
            foreach (var scope in scopes)
            {
                if (scope?.WriteQtyDiagLines == null)
                {
                    continue;
                }

                var count = scope.WriteQtyDiagLines.Count(
                    l => l.IndexOf("action=update", StringComparison.OrdinalIgnoreCase) >= 0
                        || l.IndexOf("action=create", StringComparison.OrdinalIgnoreCase) >= 0);
                writtenByScope[scope.ScopeIndex] = count;
            }

            var allEmpty = scopes
                .Where(s => s?.EmptyNameKeys != null)
                .SelectMany(s => s.EmptyNameKeys)
                .Distinct()
                .OrderBy(k => k)
                .ToList();
            SpecDiagPolicy.RegisterEmptyNameKeys(allEmpty);

            foreach (var scope in scopes)
            {
                if (scope == null)
                {
                    continue;
                }

                var scopeNum = scope.ScopeIndex + 1;
                var keys = scope.KeyToRowMark?.Count ?? 0;
                var names = scope.MarkNamePairs?.Count(kv => !string.IsNullOrWhiteSpace(kv.Value)) ?? 0;
                var empty = scope.EmptyNameKeys?.Count ?? 0;
                var emptyList = empty > 0
                    ? string.Join(",", scope.EmptyNameKeys.OrderBy(k => k).Take(12))
                    : string.Empty;
                var headerPath = string.IsNullOrWhiteSpace(scope.HeaderPath) ? "?" : scope.HeaderPath;
                var schema = string.IsNullOrWhiteSpace(scope.SchemaSource)
                    ? (scope.ColumnsInheritedFromSchema ? "inherited" : scope.ColumnsInferredFromData ? "infer" : "grid")
                    : scope.SchemaSource;
                var outside = scope.UnassignedTextCountAfterDataPass;
                var qtyWritten = writtenByScope.TryGetValue(scope.ScopeIndex, out var qw) ? qw : 0;
                var colDes = scope.ColDesignation >= 0 ? scope.ColDesignation.ToString(CultureInfo.InvariantCulture) : "-";
                SpecGridLog.WriteDiagTail(
                    doc,
                    $"[KV-SUMMARY] табл.{scopeNum} ColM/N/Q/D={scope.ColMark}/{scope.ColName}/{scope.ColQty}/{colDes} headerPath={headerPath} keys={keys} names={names} empty={empty} emptyKeys={emptyList} qtyWritten={qtyWritten} outside={outside} schema={schema}");
            }
        }

        private static string DescribeHeaderColumn(ScopeGridResult scope, int col)
        {
            if (scope == null || col < 0)
            {
                return "—";
            }

            var s = MTextPlainText.SanitizeRawContents(TableGridBuilder.BuildHeaderOnlyColumnText(scope, col));
            if (string.IsNullOrWhiteSpace(s))
            {
                if (scope.ColumnsInferredFromData)
                {
                    return "— (продолжение)";
                }

                s = MTextPlainText.SanitizeRawContents(TableGridBuilder.BuildHeaderTextForColumn(scope, col));
            }

            if (string.IsNullOrWhiteSpace(s))
            {
                return "—";
            }

            s = s.Trim();
            return s.Length > 24 ? s.Substring(0, 24) + "…" : s;
        }

        public static Dictionary<int, string> BuildCombinedMarkNames()
        {
            var map = new Dictionary<int, string>();
            foreach (var scope in SpecGridSession.Scopes)
            {
                MergeScopeNames(map, scope);
            }

            return map;
        }

        private static void MergeScopeNames(Dictionary<int, string> map, ScopeGridResult scope)
        {
            if (scope == null || !scope.Valid)
            {
                return;
            }

            foreach (var key in scope.KeyToRowMark.Keys)
            {
                string merged = null;
                if (scope.MarkNamePairs.TryGetValue(key, out var cached) && !string.IsNullOrWhiteSpace(cached))
                {
                    merged = cached;
                }
                else
                {
                    merged = TableGridBuilder.ResolveNameForKey(scope, key);
                }

                if (string.IsNullOrWhiteSpace(merged))
                {
                    continue;
                }

                if (!map.ContainsKey(key) || string.IsNullOrWhiteSpace(map[key]))
                {
                    map[key] = merged;
                }
            }
        }

        /// <summary>§19.18: 1..N таблиц — Enter без выделения завершает цикл.</summary>
        private static bool TryPickAllSpecificationTables(Document doc, out List<ObjectId[]> picks, out string error)
        {
            picks = new List<ObjectId[]>();
            error = null;
            var ed = doc?.Editor;
            if (ed == null)
            {
                error = "Нет редактора чертежа";
                return false;
            }

            var tableNum = 1;
            const int maxTables = 50;
            while (tableNum <= maxTables)
            {
                var prompt = tableNum == 1
                    ? "\nВыделите рамкой объекты первой таблицы. Для завершения нажмите Enter без выделения: "
                    : $"\nВыделите рамкой объекты таблицы {tableNum}. Для завершения нажмите Enter без выделения: ";
                var selOpts = new PromptSelectionOptions
                {
                    MessageForAdding = prompt
                };
                var psr = ed.GetSelection(selOpts);
                if (psr.Status == PromptStatus.Cancel)
                {
                    if (picks.Count == 0)
                    {
                        error = "Выбор отменён";
                        return false;
                    }

                    break;
                }

                var ids = ArrayCompat.Empty<ObjectId>();
                if (psr.Status == PromptStatus.OK && psr.Value != null)
                {
                    ids = psr.Value.GetObjectIds()
                        .Where(id => !id.IsNull && id.IsValid && !id.IsErased)
                        .Distinct()
                        .ToArray();
                }

                // §19.18: пустой Enter (None) или 0 объектов — сразу конец выбора, без второго Enter.
                if (psr.Status == PromptStatus.None || ids.Length == 0)
                {
                    if (picks.Count == 0)
                    {
                        error = "Не выбрано ни одного объекта таблицы";
                        return false;
                    }

                    break;
                }

                if (psr.Status != PromptStatus.OK)
                {
                    if (picks.Count == 0)
                    {
                        error = "Выбор таблиц не выполнен";
                        return false;
                    }

                    break;
                }

                picks.Add(ids);
                SpecGridLog.WriteCommandLine(
                    doc,
                    $"[INFO] Выбрана таблица {picks.Count} (объектов={ids.Length})");
                tableNum++;
            }

            if (picks.Count > 0)
            {
                SpecGridLog.WriteCommandLine(
                    doc,
                    $"[INFO] Всего выбрано таблиц: {picks.Count}. Начинаем обработку...");
            }

            return picks.Count > 0;
        }

        private static int WriteQtyInTransaction(
            Transaction tr,
            IReadOnlyDictionary<int, int> qtyByKey,
            SpecGridLog log,
            out int skipped)
        {
            skipped = 0;
            if (qtyByKey == null || qtyByKey.Count == 0)
            {
                return 0;
            }

            var written = 0;
            foreach (var scope in SpecGridSession.Scopes)
            {
                written += WriteQtyScope(tr, scope, qtyByKey, log, ref skipped);
            }

            return written;
        }

        private static int WriteQtyScope(
            Transaction tr,
            ScopeGridResult scope,
            IReadOnlyDictionary<int, int> qtyByKey,
            SpecGridLog log,
            ref int skipped)
        {
            if (scope == null || !scope.Valid || scope.ColQty < 0)
            {
                return 0;
            }

            if (scope.IsNativeAcadTable)
            {
                return WriteQtyScopeNativeTable(tr, scope, qtyByKey, log, ref skipped);
            }

            var btr = ResolveOwnerBlock(tr, scope);
            if (btr == null)
            {
                return 0;
            }

            // Стиль/цвет/толщина линии — только из своей таблицы (scope 0, scope 1, …), не общий по всем pick.
            var appearanceCache = new Dictionary<int, QtyTableTextAppearance>();

            scope.WriteQtyDiagLines.Clear();
            var written = 0;
            foreach (var key in scope.KeyToRowTopSub.Keys.OrderBy(k => k))
            {
                if (!qtyByKey.TryGetValue(key, out var qty))
                {
                    continue;
                }

                if (!scope.KeyToRowTopSub.TryGetValue(key, out var rowTop))
                {
                    skipped++;
                    continue;
                }

                var col = scope.ColQty;
                if (rowTop < 0 || col < 0 || rowTop >= scope.GridYs.Count - 1 || col >= scope.GridXs.Count - 1)
                {
                    skipped++;
                    continue;
                }

                if (!appearanceCache.TryGetValue(rowTop, out var appearance))
                {
                    appearance = ResolveQtyTableTextAppearanceForScope(tr, scope, rowTop);
                    appearanceCache[rowTop] = appearance;
                }

                var rowMark = scope.KeyToRowMark.TryGetValue(key, out var rm) ? rm : -1;
                var rowBottomEx = ResolveQtyCellRowBottomExByColQtyGrid(scope, rowTop, col, key);
                var point = ResolveQtyInsertPoint(scope, rowTop, rowBottomEx, col);
                var text = qty.ToString(CultureInfo.InvariantCulture);
                try
                {
                    UpsertQtyText(tr, btr, scope, rowTop, rowBottomEx, col, point, text, appearance, log, scope.ScopeIndex, key);
                    scope.CellText[rowTop, col] = text;
                    written++;
                    if (scope.WriteQtyDiagLines.Count < 20
                        && (SpecDiagPolicy.IsSampleKey(scope, key) || written <= 5))
                    {
                        var styleFrom = appearance.Found ? "colName-rowTop" : "fallback";
                        var h = appearance.HasTextHeight ? appearance.TextHeight.ToString("F1", CultureInfo.InvariantCulture) : "?";
                        var layer = appearance.Layer ?? "?";
                        var mergedSpan = rowBottomEx > rowTop + 1;
                        scope.WriteQtyDiagLines.Add(
                            $"WriteQty key={key} rowTop={rowTop} rowMark={rowMark} rowBottomEx={rowBottomEx} merged={mergedSpan} colQty={col} qty={qty} Y={point.Y:F1} style=h={h} layer={layer} from={styleFrom} action=update (палитра)");
                    }
                }
                catch
                {
                    skipped++;
                }
            }

            return written;
        }

        private static int WriteQtyScopeNativeTable(
            Transaction tr,
            ScopeGridResult scope,
            IReadOnlyDictionary<int, int> qtyByKey,
            SpecGridLog log,
            ref int skipped)
        {
            if (scope.NativeTableId.IsNull || !scope.NativeTableId.IsValid)
            {
                return 0;
            }

            Table table;
            try
            {
                table = tr.GetObject(scope.NativeTableId, OpenMode.ForWrite, false) as Table;
            }
            catch
            {
                return 0;
            }

            if (table == null)
            {
                return 0;
            }

            var appearance = ResolveQtyTableTextAppearanceForScope(tr, scope);
            var written = 0;
            foreach (var key in scope.KeyToRowTopSub.Keys.OrderBy(k => k))
            {
                if (!qtyByKey.TryGetValue(key, out var qty))
                {
                    continue;
                }

                if (!scope.KeyToRowTopSub.TryGetValue(key, out var rowTop))
                {
                    skipped++;
                    continue;
                }

                var col = scope.ColQty;
                var tableRows = (int)table.Rows.Count;
                var tableCols = (int)table.Columns.Count;
                if (rowTop < 0 || col < 0 || rowTop >= tableRows || col >= tableCols)
                {
                    skipped++;
                    continue;
                }

                try
                {
                    UpsertQtyInAcadTable(table, scope, rowTop, col, qty, appearance, log, scope.ScopeIndex, key);
                    written++;
                }
                catch
                {
                    skipped++;
                }
            }

            return written;
        }

        private static void UpsertQtyInAcadTable(
            Table table,
            ScopeGridResult scope,
            int rowTop,
            int col,
            int qty,
            QtyTableTextAppearance appearance,
            SpecGridLog log,
            int scopeIndex,
            int key)
        {
            var current = ReadAcadTableCellText(table, rowTop, col);
            var newText = ReplaceQtyNumberInCellText(current, qty);
            table.Cells[rowTop, col].TextString = newText;
            if (scope.CellText != null
                && rowTop < scope.CellText.GetLength(0)
                && col < scope.CellText.GetLength(1))
            {
                scope.CellText[rowTop, col] = newText;
            }

            log.Debug($"QTY-WRITE: scope={scopeIndex} key={key} native-table row={rowTop} col={col} text=\"{newText}\"");
        }

        private static string ReadAcadTableCellText(Table table, int row, int col)
        {
            try
            {
                return MTextPlainText.SanitizeRawContents(table.Cells[row, col].TextString ?? string.Empty);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>Заменить только числовую часть («5 шт.» → «12 шт.»).</summary>
        private static string ReplaceQtyNumberInCellText(string cellText, int qty)
        {
            var qtyText = qty.ToString(CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(cellText))
            {
                return qtyText;
            }

            var match = Regex.Match(cellText, @"\d+(?:[.,]\d+)?");
            if (!match.Success)
            {
                return qtyText;
            }

            return cellText.Substring(0, match.Index)
                + qtyText
                + cellText.Substring(match.Index + match.Length);
        }

        private static BlockTableRecord ResolveOwnerBlock(Transaction tr, ScopeGridResult scope)
        {
            if (!scope.OwnerBlockId.IsNull && scope.OwnerBlockId.IsValid)
            {
                return tr.GetObject(scope.OwnerBlockId, OpenMode.ForWrite, false) as BlockTableRecord;
            }

            return null;
        }

        private static void TraceWriteQty(
            int scopeIndex,
            int key,
            string action,
            double targetY,
            double entityY,
            string text,
            int rowTop,
            int col)
        {
            if (key > 7 && (key < 60 || key > 70))
            {
                return;
            }

            var entY = double.IsNaN(entityY) ? "—" : entityY.ToString("F1", CultureInfo.InvariantCulture);
            SpecGridLog.WriteTrace(
                "WRITEQTY",
                $"scope={scopeIndex} key={key} {action} rowTop={rowTop} col={col} qty={text} targetY={targetY:F1} entY={entY}");
        }

        private static void UpsertQtyText(
            Transaction tr,
            BlockTableRecord btr,
            ScopeGridResult scope,
            int rowTop,
            int rowBottomEx,
            int col,
            Point3d point,
            string text,
            QtyTableTextAppearance tableAppearance,
            SpecGridLog log,
            int scopeIndex,
            int key)
        {
            var existing = FindQtyTextInCell(tr, scope, rowTop, rowBottomEx, col, point.Y);
            var existingY = existing != null ? GetEntityTextPoint(existing).Y : double.NaN;
            var halfRowStep = ResolveHalfRowStepY(scope, rowTop);
            if (existing != null && !double.IsNaN(existingY) && Math.Abs(existingY - point.Y) > halfRowStep)
            {
                TraceWriteQty(scopeIndex, key, "skip-far-entY", point.Y, existingY, text, rowTop, col);
                existing = null;
                existingY = double.NaN;
            }

            string action;
            if (existing is DBText db)
            {
                action = "update DBText";
                db.UpgradeOpen();
                db.TextString = text;
                ApplyQtyTableTextStyle(db, tableAppearance, point);
                TraceWriteQty(scopeIndex, key, action, point.Y, existingY, text, rowTop, col);
                return;
            }

            if (existing is MText mt)
            {
                action = "update MText";
                mt.UpgradeOpen();
                mt.Contents = text;
                mt.TextHeight = ResolveQtyTextHeight(tableAppearance);
                mt.Location = point;
                ApplyQtyTableTextStyle(mt, tableAppearance);
                ApplyQtyCenterAlignmentForMText(mt, point);
                TraceWriteQty(scopeIndex, key, action, point.Y, existingY, text, rowTop, col);
                return;
            }

            action = "create DBText";
            TraceWriteQty(scopeIndex, key, action, point.Y, existingY, text, rowTop, col);

            // §19.15: новый текст — стиль штатной колонки «Кол.» (слой + TextStyle), не примечания.
            var dbText = new DBText
            {
                Position = point,
                Height = ResolveQtyTextHeight(tableAppearance),
                TextString = text,
                Layer = tableAppearance.Found && !string.IsNullOrWhiteSpace(tableAppearance.Layer)
                    ? tableAppearance.Layer
                    : scope.GridLayer ?? "0"
            };
            ApplyQtyTableTextStyle(dbText, tableAppearance, point);
            btr.AppendEntity(dbText);
            tr.AddNewlyCreatedDBObject(dbText, true);
        }

        /// <summary>
        /// Стиль «Кол.» для одной таблицы (scope 0, 1, …): цвет/линия/шрифт как у **основного текста таблицы** (NAME),
        /// не как у сносок инженера в ячейке «Кол.».
        /// </summary>
        private static QtyTableTextAppearance ResolveQtyTableTextAppearanceForScope(
            Transaction tr,
            ScopeGridResult scope,
            int rowTop = -1)
        {
            if (scope == null || !scope.Valid || scope.ColQty < 0)
            {
                return new QtyTableTextAppearance();
            }

            var samples = new List<QtyAppearanceSample>();
            if (rowTop >= 0)
            {
                AppendTableBodyStyleFromColumnAtRow(tr, scope, scope.ColName, rowTop, samples, requireNameText: true);
                AppendTableBodyStyleFromColumnAtRow(tr, scope, scope.ColMark, rowTop, samples, requireNameText: false);
            }

            if (samples.Count == 0)
            {
                AppendQtyStyleSamplesFromQtyColumn(tr, scope, samples, headerOnly: true);
                AppendQtyDigitStyleSamplesFromScope(tr, scope, samples);
            }

            if (samples.Count == 0)
            {
                AppendTableBodyStyleSamplesFromScope(tr, scope, samples);
            }

            if (samples.Count == 0)
            {
                AppendTableTextHeightFallbackSamples(tr, scope, samples);
            }

            return BuildQtyTableTextAppearanceFromSamples(samples);
        }

        private static double ResolveQtyTextHeight(QtyTableTextAppearance appearance)
        {
            if (appearance != null && appearance.HasTextHeight && appearance.TextHeight > 0)
            {
                return appearance.TextHeight;
            }

            return QtyTextHeightFallback;
        }

        /// <summary>Преобладающий TextStyle, слой, цвет, толщина линии по образцам одного scope.</summary>
        private static QtyTableTextAppearance BuildQtyTableTextAppearanceFromSamples(List<QtyAppearanceSample> samples)
        {
            if (samples == null || samples.Count == 0)
            {
                return new QtyTableTextAppearance();
            }

            var styleGroup = samples
                .GroupBy(s => (s.TextStyleId, s.Layer ?? string.Empty))
                .OrderByDescending(g => g.Count())
                .First();

            var colorGroup = styleGroup
                .GroupBy(s => s.ColorKey)
                .OrderByDescending(g => g.Count())
                .First()
                .First();

            var lineWeight = styleGroup
                .GroupBy(s => s.LineWeight)
                .OrderByDescending(g => g.Count())
                .First()
                .Key;

            var heightGroup = samples
                .Where(s => s.TextHeight > 0)
                .GroupBy(s => s.TextHeight)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            return new QtyTableTextAppearance
            {
                Found = true,
                TextStyleId = styleGroup.Key.TextStyleId,
                Layer = styleGroup.Key.Item2,
                EntityColor = colorGroup.EntityColor,
                HasEntityColor = colorGroup.HasEntityColor,
                LineWeight = lineWeight,
                TextHeight = heightGroup?.Key ?? 0,
                HasTextHeight = heightGroup != null && heightGroup.Key > 0
            };
        }

        private sealed class QtyAppearanceSample
        {
            public ObjectId TextStyleId = ObjectId.Null;
            public string Layer;
            public Color EntityColor = Color.FromColorIndex(ColorMethod.ByLayer, 256);
            public bool HasEntityColor;
            public LineWeight LineWeight = LineWeight.ByLayer;
            public string ColorKey = string.Empty;
            public double TextHeight;
        }

        private static void AppendTableBodyStyleFromColumnAtRow(
            Transaction tr,
            ScopeGridResult scope,
            int col,
            int row,
            List<QtyAppearanceSample> samples,
            bool requireNameText)
        {
            if (col < 0 || row < scope.RowDataStart)
            {
                return;
            }

            foreach (var id in scope.PickedObjectIds)
            {
                Entity ent;
                try
                {
                    ent = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                }
                catch
                {
                    continue;
                }

                if (ent is not DBText and not MText)
                {
                    continue;
                }

                var pt = GetEntityTextPoint(ent);
                if (!CellIndex.TryGetCellIndex(pt.X, pt.Y, scope.GridXs, scope.GridYs, out var entRow, out var cellCol)
                    || cellCol != col
                    || entRow != row)
                {
                    continue;
                }

                if (!PassesTableBodyLayerForQtyStyle(scope, ent.Layer, allowAnyTableContentLayer: false))
                {
                    continue;
                }

                var plain = GetEntityPlainText(ent);
                if (requireNameText)
                {
                    if (!MTextPlainText.HasLetter(plain) || plain.Trim().Length < 4)
                    {
                        continue;
                    }
                }
                else if (string.IsNullOrWhiteSpace(plain))
                {
                    continue;
                }

                samples.Add(CreateQtyAppearanceSample(ent));
            }
        }

        /// <summary>Образцы стиля из штатного текста таблицы: «Наименование», «Марка» (слои основного содержимого).</summary>
        private static void AppendTableBodyStyleSamplesFromScope(
            Transaction tr,
            ScopeGridResult scope,
            List<QtyAppearanceSample> samples)
        {
            AppendTableBodyStyleFromColumn(tr, scope, scope.ColName, samples, requireNameText: true);
            AppendTableBodyStyleFromColumn(tr, scope, scope.ColMark, samples, requireNameText: false);

            if (samples.Count > 0)
            {
                return;
            }

            AppendTableBodyStyleFromColumn(tr, scope, scope.ColName, samples, requireNameText: true, allowAnyTableContentLayer: true);
        }

        private static void AppendTableBodyStyleFromColumn(
            Transaction tr,
            ScopeGridResult scope,
            int col,
            List<QtyAppearanceSample> samples,
            bool requireNameText,
            bool allowAnyTableContentLayer = false)
        {
            if (col < 0)
            {
                return;
            }

            foreach (var id in scope.PickedObjectIds)
            {
                Entity ent;
                try
                {
                    ent = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                }
                catch
                {
                    continue;
                }

                if (ent is not DBText and not MText)
                {
                    continue;
                }

                var pt = GetEntityTextPoint(ent);
                if (!CellIndex.TryGetCellIndex(pt.X, pt.Y, scope.GridXs, scope.GridYs, out var row, out var cellCol)
                    || cellCol != col
                    || row < scope.RowDataStart)
                {
                    continue;
                }

                if (!PassesTableBodyLayerForQtyStyle(scope, ent.Layer, allowAnyTableContentLayer))
                {
                    continue;
                }

                var plain = GetEntityPlainText(ent);
                if (requireNameText)
                {
                    if (!MTextPlainText.HasLetter(plain) || plain.Trim().Length < 4)
                    {
                        continue;
                    }
                }
                else if (string.IsNullOrWhiteSpace(plain))
                {
                    continue;
                }

                samples.Add(CreateQtyAppearanceSample(ent));
            }
        }

        /// <summary>Тексты в колонке «Кол.» (шапка и/или данные).</summary>
        private static void AppendQtyStyleSamplesFromQtyColumn(
            Transaction tr,
            ScopeGridResult scope,
            List<QtyAppearanceSample> samples,
            bool headerOnly)
        {
            var colQty = scope.ColQty;
            if (colQty < 0)
            {
                return;
            }

            foreach (var id in scope.PickedObjectIds)
            {
                Entity ent;
                try
                {
                    ent = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                }
                catch
                {
                    continue;
                }

                if (ent is not DBText and not MText)
                {
                    continue;
                }

                var pt = GetEntityTextPoint(ent);
                if (!CellIndex.TryGetCellIndex(pt.X, pt.Y, scope.GridXs, scope.GridYs, out var row, out var col)
                    || col != colQty)
                {
                    continue;
                }

                if (headerOnly && row > 1)
                {
                    continue;
                }

                if (!headerOnly && row < scope.RowDataStart)
                {
                    continue;
                }

                if (!PassesTableBodyLayerForQtyStyle(scope, ent.Layer, allowAnyTableContentLayer: headerOnly))
                {
                    continue;
                }

                if (!headerOnly && !IsLikelyQtyCellText(GetEntityPlainText(ent)))
                {
                    continue;
                }

                samples.Add(CreateQtyAppearanceSample(ent));
            }
        }

        /// <summary>Запас: короткие цифры в «Кол.» только на слое основного текста (не сноска на чужом слое).</summary>
        private static void AppendQtyDigitStyleSamplesFromScope(
            Transaction tr,
            ScopeGridResult scope,
            List<QtyAppearanceSample> samples)
        {
            AppendQtyStyleSamplesFromQtyColumn(tr, scope, samples, headerOnly: false);
        }

        private static void AppendTableTextHeightFallbackSamples(
            Transaction tr,
            ScopeGridResult scope,
            List<QtyAppearanceSample> samples)
        {
            foreach (var id in scope.PickedObjectIds)
            {
                Entity ent;
                try
                {
                    ent = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                }
                catch
                {
                    continue;
                }

                if (ent is not DBText and not MText)
                {
                    continue;
                }

                if (!PassesTableBodyLayerForQtyStyle(scope, ent.Layer, allowAnyTableContentLayer: true))
                {
                    continue;
                }

                var sample = CreateQtyAppearanceSample(ent);
                if (sample.TextHeight > 0)
                {
                    samples.Add(sample);
                }
            }
        }

        /// <summary>Слой для образца стиля qty: PrimaryNameLayer / ExtraNameLayers, не пометки и не «левые» Allowed-слои.</summary>
        private static bool PassesTableBodyLayerForQtyStyle(
            ScopeGridResult scope,
            string layer,
            bool allowAnyTableContentLayer)
        {
            var l = layer ?? string.Empty;
            if (IsExcludedAnnotationLayer(scope, l))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(scope.PrimaryNameLayer))
            {
                if (string.Equals(l, scope.PrimaryNameLayer, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (scope.ExtraNameLayers.Contains(l))
                {
                    return true;
                }

                if (allowAnyTableContentLayer && IsTableContentLayer(scope, l))
                {
                    return true;
                }

                return false;
            }

            return IsTableContentLayer(scope, l);
        }

        private static QtyAppearanceSample CreateQtyAppearanceSample(Entity ent)
        {
            var sample = new QtyAppearanceSample();
            if (ent is DBText db)
            {
                sample.TextStyleId = db.TextStyleId;
                sample.Layer = db.Layer;
                sample.EntityColor = db.Color;
                sample.HasEntityColor = true;
                sample.LineWeight = db.LineWeight;
                sample.TextHeight = db.Height;
            }
            else if (ent is MText mt)
            {
                sample.TextStyleId = mt.TextStyleId;
                sample.Layer = mt.Layer;
                sample.EntityColor = mt.Color;
                sample.HasEntityColor = true;
                sample.LineWeight = mt.LineWeight;
                sample.TextHeight = mt.TextHeight;
            }

            sample.ColorKey = sample.HasEntityColor
                ? $"{sample.EntityColor.ColorMethod}:{sample.EntityColor.ColorIndex}"
                : "bylayer";
            return sample;
        }

        private static void CopyQtyAppearanceFromEntity(DBText source, QtyTableTextAppearance result)
        {
            result.Found = true;
            result.TextStyleId = source.TextStyleId;
            result.Layer = source.Layer;
            result.EntityColor = source.Color;
            result.HasEntityColor = true;
            result.LineWeight = source.LineWeight;
            if (source.Height > 0)
            {
                result.TextHeight = source.Height;
                result.HasTextHeight = true;
            }
        }

        /// <summary>§19.19: если в «Кол.» нет образца — стиль/цвет из колонки «Наименование» (основной текст).</summary>
        private static void TryFillQtyAppearanceFromNameColumn(
            Transaction tr,
            ScopeGridResult scope,
            QtyTableTextAppearance result)
        {
            if (scope.ColName < 0)
            {
                return;
            }

            DBText best = null;
            var bestLen = 0;
            foreach (var id in scope.PickedObjectIds)
            {
                if (tr.GetObject(id, OpenMode.ForRead, false) is not DBText db)
                {
                    continue;
                }

                var pt = GetEntityTextPoint(db);
                if (!CellIndex.TryGetCellIndex(pt.X, pt.Y, scope.GridXs, scope.GridYs, out var row, out var col)
                    || col != scope.ColName
                    || row < scope.RowDataStart)
                {
                    continue;
                }

                if (IsExcludedAnnotationLayer(scope, db.Layer) || !IsTableContentLayer(scope, db.Layer))
                {
                    continue;
                }

                var plain = GetEntityPlainText(db);
                if (!MTextPlainText.HasLetter(plain) || plain.Length < 8)
                {
                    continue;
                }

                if (best == null || plain.Length > bestLen)
                {
                    best = db;
                    bestLen = plain.Length;
                }
            }

            if (best == null)
            {
                return;
            }

            if (!result.Found)
            {
                CopyQtyAppearanceFromEntity(best, result);
                return;
            }

            if (result.TextStyleId.IsNull && !best.TextStyleId.IsNull)
            {
                result.TextStyleId = best.TextStyleId;
            }

            if (!result.HasEntityColor)
            {
                result.EntityColor = best.Color;
                result.HasEntityColor = true;
            }

            if (string.IsNullOrWhiteSpace(result.Layer))
            {
                result.Layer = best.Layer;
            }

            result.LineWeight = best.LineWeight;
        }

        private static void ApplyQtyTableTextStyle(DBText dbText, QtyTableTextAppearance appearance, Point3d point)
        {
            dbText.Height = ResolveQtyTextHeight(appearance);
            if (appearance.Found)
            {
                if (!appearance.TextStyleId.IsNull)
                {
                    dbText.TextStyleId = appearance.TextStyleId;
                }

                if (!string.IsNullOrWhiteSpace(appearance.Layer))
                {
                    dbText.Layer = appearance.Layer;
                }

                dbText.LineWeight = appearance.LineWeight;
                try
                {
                    // §19.19: явный цвет штатного текста, не «По слою» слоя с примечанием.
                    dbText.Color = appearance.HasEntityColor
                        ? appearance.EntityColor
                        : Color.FromColorIndex(ColorMethod.ByLayer, 256);
                }
                catch
                {
                    // ignore
                }
            }
            else
            {
                dbText.LineWeight = LineWeight.ByLayer;
            }

            ApplyQtyCenterAlignment(dbText, point);
        }

        private static void ApplyQtyTableTextStyle(MText mt, QtyTableTextAppearance appearance)
        {
            if (!appearance.Found)
            {
                return;
            }

            if (!appearance.TextStyleId.IsNull)
            {
                mt.TextStyleId = appearance.TextStyleId;
            }

            if (!string.IsNullOrWhiteSpace(appearance.Layer))
            {
                mt.Layer = appearance.Layer;
            }

            mt.LineWeight = appearance.LineWeight;
            try
            {
                mt.Color = appearance.HasEntityColor
                    ? appearance.EntityColor
                    : Color.FromColorIndex(ColorMethod.ByLayer, 256);
            }
            catch
            {
                // ignore
            }
        }

        private static void ApplyQtyCenterAlignmentForMText(MText mt, Point3d point)
        {
            mt.Attachment = AttachmentPoint.MiddleCenter;
            mt.Location = point;
        }

        private static bool IsPointInQtyColumn(Point3d pt, ScopeGridResult scope, int col)
        {
            if (col < 0 || col >= scope.GridXs.Count - 1 || scope.GridYs.Count < 2)
            {
                return false;
            }

            var xL = scope.GridXs[col] - CellIndex.CellIndexEps;
            var xR = scope.GridXs[col + 1] + CellIndex.CellIndexEps;
            if (pt.X < xL || pt.X > xR)
            {
                return false;
            }

            var yTop = scope.GridYs[scope.RowDataStart];
            var yBottom = scope.GridYs[scope.GridYs.Count - 1];
            var yLo = Math.Min(yTop, yBottom) - CellIndex.CellIndexEps;
            var yHi = Math.Max(yTop, yBottom) + CellIndex.CellIndexEps;
            return pt.Y >= yLo && pt.Y <= yHi;
        }

        private static List<int> CollectMissingQtyMarks(IReadOnlyDictionary<int, int> qtyByKey)
        {
            var missing = new HashSet<int>();
            foreach (var scope in SpecGridSession.Scopes)
            {
                CollectMissingQtyMarksForScope(scope, qtyByKey, missing);
            }

            return missing.OrderBy(k => k).ToList();
        }

        private static void CollectMissingQtyMarksForScope(
            ScopeGridResult scope,
            IReadOnlyDictionary<int, int> qtyByKey,
            HashSet<int> missing)
        {
            if (scope == null || !scope.Valid)
            {
                return;
            }

            foreach (var key in scope.KeyToRowMark.Keys)
            {
                if (qtyByKey == null || !qtyByKey.ContainsKey(key))
                {
                    missing.Add(key);
                }
            }
        }

        /// <summary>Нижняя граница ячейки «Кол.» (exclusive) по H-линиям в полосе X ColQty; не KeyToMarkBlockEnd.</summary>
        private static int ResolveQtyCellRowBottomExByColQtyGrid(ScopeGridResult scope, int rowTop, int colQty, int key)
        {
            var fallback = rowTop + 1;
            if (scope?.GridYs == null || scope.GridYs.Count < 2)
            {
                return fallback;
            }

            if (rowTop < 0 || rowTop >= scope.GridYs.Count - 1)
            {
                return fallback;
            }

            if (colQty < 0 || colQty >= scope.GridXs.Count - 1)
            {
                return fallback;
            }

            var horiz = scope.HorizontalLines;
            if (horiz == null || horiz.Count == 0)
            {
                return fallback;
            }

            var xL = scope.GridXs[colQty];
            var xR = scope.GridXs[colQty + 1];
            var rowBottomEx = rowTop + 1;
            while (rowBottomEx < scope.GridYs.Count - 1)
            {
                if (TableGridBuilder.HasHBorderAt(
                        scope.GridYs[rowBottomEx],
                        xL,
                        xR,
                        horiz,
                        borderEps: TableGridBuilder.EpsAxis * 3.0))
                {
                    break;
                }

                rowBottomEx++;
            }

            rowBottomEx = Math.Min(rowBottomEx, ResolveNextKeyRowTopEx(scope, key));
            rowBottomEx = Math.Min(rowBottomEx, scope.GridYs.Count - 1);
            return rowBottomEx <= rowTop ? fallback : rowBottomEx;
        }

        private static int ResolveNextKeyRowTopEx(ScopeGridResult scope, int key)
        {
            if (scope?.KeyToRowTopSub == null || scope.GridYs == null || scope.GridYs.Count < 2)
            {
                return scope?.GridYs?.Count - 1 ?? 1;
            }

            foreach (var kv in scope.KeyToRowTopSub.OrderBy(x => x.Key))
            {
                if (kv.Key > key)
                {
                    return kv.Value;
                }
            }

            return scope.GridYs.Count - 1;
        }

        /// <summary>Y центра первой суб-строки блока марки (rowTop..rowTop+1), не середина всего span.</summary>
        private static double ResolveQtyInsertY(ScopeGridResult scope, int rowTop)
        {
            if (scope?.GridYs == null || scope.GridYs.Count < 2 || rowTop < 0)
            {
                return 0;
            }

            var rowBottomForY = Math.Min(rowTop + 1, scope.GridYs.Count - 1);
            return (scope.GridYs[rowTop] + scope.GridYs[rowBottomForY]) * 0.5;
        }

        private static double ResolveHalfRowStepY(ScopeGridResult scope, int rowTop)
        {
            if (scope?.GridYs == null || scope.GridYs.Count < 2 || rowTop < 0)
            {
                return 1.0;
            }

            var rowBottomForY = Math.Min(rowTop + 1, scope.GridYs.Count - 1);
            return Math.Abs(scope.GridYs[rowTop] - scope.GridYs[rowBottomForY]) * 0.5;
        }

        private static Point3d ResolveQtyInsertPoint(ScopeGridResult scope, int rowTop, int rowBottomEx, int colQty)
        {
            var y = ResolveQtyInsertY(scope, rowTop);
            var x = ResolveVisualQtyColumnCenterX(scope, colQty);
            return new Point3d(x, y, 0);
        }

        private static double ResolveVisualQtyColumnCenterX(ScopeGridResult scope, int colQty)
        {
            if (colQty < 0 || colQty >= scope.GridXs.Count - 1)
            {
                return 0;
            }

            var gridCenter = (scope.GridXs[colQty] + scope.GridXs[colQty + 1]) * 0.5;
            // Центрирование «Кол.» должно быть строго по геометрическим границам ячейки сетки,
            // а не по координатам Location MText (Justify Top left смещает t.X).
            return gridCenter;
        }

        private static void ReportGridBuildWarnings(Document doc, IReadOnlyList<ScopeGridResult> scopes)
        {
            if (doc == null || scopes == null)
            {
                return;
            }

            foreach (var scope in scopes)
            {
                if (scope == null)
                {
                    continue;
                }

                if (scope.GridAxesMergedFromMixedLayers
                    && !string.IsNullOrWhiteSpace(scope.GridMergeLayerNote))
                {
                    SpecGridLog.WriteCommandLine(
                        doc,
                        $"[POSC] Сетка таблицы: линии на разных слоях — оси дополнены ({scope.GridMergeLayerNote}).");
                }

                if (!scope.Valid)
                {
                    SpecGridLog.WriteCommandLine(
                        doc,
                        "[POSC] Не удалось построить сетку таблицы — проверьте линии в рамке выбора.");
                }
            }
        }

        private static void ReportEmptyMarkColumnWarnings(Document doc, IReadOnlyList<ScopeGridResult> scopes)
        {
            if (doc == null || scopes == null)
            {
                return;
            }

            foreach (var scope in scopes)
            {
                if (scope == null || !scope.Valid || scope.ColMark < 0)
                {
                    continue;
                }

                if (scope.KeyToRowMark.Count == 0)
                {
                    var markColLabel = scope.ColMark >= 0 ? $"столбец {scope.ColMark}" : "не найден";
                    SpecGridLog.WriteCommandLine(
                        doc,
                        $"[POSC] Марки в данных не найдены (ColMark={markColLabel}, RowDataStart={scope.RowDataStart}) — проверьте цифры в «Поз.».");
                    var markCounts = TableGridBuilder.FormatDataMarkCountsDiagnostic(scope);
                    if (!string.IsNullOrWhiteSpace(markCounts))
                    {
                        SpecGridLog.WriteCommandLine(
                            doc,
                            $"[POSC] Марок в данных по столбцам: {markCounts}");
                    }
                }
            }
        }

        /// <summary>§19.16: марка в таблице есть, в палитре количества нет — только CMD, без Enter.</summary>
        private static void ReportMissingQtyMarks(Document doc, List<int> missingMarks)
        {
            if (missingMarks == null || missingMarks.Count == 0)
            {
                return;
            }

            var list = string.Join(", ", missingMarks);
            var msg = $"[POSC] Количество не найдено в палитре для марок: {list}";
            SpecGridLog.WriteCommandLine(doc, msg);
        }

        /// <summary>§7.1.3: только штатное количество (короткие цифры), не примечания инженера.</summary>
        private static Entity FindQtyTextInCell(
            Transaction tr,
            ScopeGridResult scope,
            int rowTop,
            int rowBottomEx,
            int col,
            double targetCenterY)
        {
            var mergedSpan = rowBottomEx > rowTop + 1;
            var searchRowBottomEx = mergedSpan ? rowTop + 1 : rowBottomEx;
            Entity best = null;
            var bestScore = -1;
            var bestDist = double.MaxValue;
            foreach (var id in scope.PickedObjectIds)
            {
                Entity ent;
                try
                {
                    ent = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                }
                catch
                {
                    continue;
                }

                if (ent == null || !(ent is DBText || ent is MText))
                {
                    continue;
                }

                var pt = GetEntityTextPoint(ent);
                if (mergedSpan)
                {
                    if (!IsPointInQtyCellSpan(pt, scope, rowTop, searchRowBottomEx, col))
                    {
                        continue;
                    }
                }
                else if (!IsPointInCell(pt, scope, rowTop, col))
                {
                    continue;
                }

                if (IsExcludedAnnotationLayer(scope, ent.Layer))
                {
                    continue;
                }

                if (!PassesTableBodyLayerForQtyStyle(scope, ent.Layer, allowAnyTableContentLayer: false))
                {
                    continue;
                }

                var plain = GetEntityPlainText(ent);
                if (!IsLikelyQtyCellText(plain))
                {
                    continue;
                }

                if (mergedSpan)
                {
                    var dist = Math.Abs(pt.Y - targetCenterY);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = ent;
                    }
                }
                else
                {
                    var score = plain.Trim().Length;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = ent;
                    }
                }
            }

            return best;
        }

        private static string GetEntityPlainText(Entity ent)
        {
            if (ent is DBText db)
            {
                return MTextPlainText.SanitizeRawContents(db.TextString ?? string.Empty);
            }

            if (ent is MText mt)
            {
                return MTextPlainText.SanitizeRawContents(mt.Contents ?? string.Empty);
            }

            return string.Empty;
        }

        /// <summary>Короткое число количества; не примечание с буквами.</summary>
        private static bool IsLikelyQtyCellText(string plain)
        {
            if (string.IsNullOrWhiteSpace(plain))
            {
                return false;
            }

            var s = plain.Trim();
            if (s.Length > 10)
            {
                return false;
            }

            if (MTextPlainText.HasLetter(s))
            {
                return false;
            }

            var digits = 0;
            foreach (var c in s)
            {
                if (char.IsDigit(c))
                {
                    digits++;
                }
                else if (c != ' ' && c != '.' && c != ',')
                {
                    return false;
                }
            }

            return digits > 0 && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v >= 0 && v <= 100000;
        }

        private static bool IsExcludedAnnotationLayer(ScopeGridResult scope, string layer)
        {
            if (scope.ExcludedAnnotationLayers.Count == 0)
            {
                return false;
            }

            return scope.ExcludedAnnotationLayers.Contains(layer ?? string.Empty);
        }

        private static bool IsTableContentLayer(ScopeGridResult scope, string layer)
        {
            if (scope.AllowedTableTextLayers.Count == 0)
            {
                return true;
            }

            return scope.AllowedTableTextLayers.Contains(layer ?? string.Empty);
        }

        private static bool IsPointInCell(Point3d pt, ScopeGridResult scope, int row, int col)
        {
            if (col < 0 || row < 0 || col >= scope.GridXs.Count - 1 || row >= scope.GridYs.Count - 1)
            {
                return false;
            }

            var xL = scope.GridXs[col] - CellIndex.CellIndexEps;
            var xR = scope.GridXs[col + 1] + CellIndex.CellIndexEps;
            var yA = scope.GridYs[row];
            var yB = scope.GridYs[row + 1];
            var yLo = Math.Min(yA, yB) - CellIndex.CellIndexEps;
            var yHi = Math.Max(yA, yB) + CellIndex.CellIndexEps;
            return pt.X >= xL && pt.X <= xR && pt.Y >= yLo && pt.Y <= yHi;
        }

        private static bool IsPointInQtyCellSpan(
            Point3d pt,
            ScopeGridResult scope,
            int rowTop,
            int rowBottomEx,
            int col)
        {
            if (scope?.GridYs == null || scope.GridXs == null || scope.GridYs.Count < 2)
            {
                return false;
            }

            if (col < 0 || rowTop < 0 || col >= scope.GridXs.Count - 1 || rowTop >= scope.GridYs.Count - 1)
            {
                return false;
            }

            rowBottomEx = Math.Min(Math.Max(rowBottomEx, rowTop + 1), scope.GridYs.Count - 1);

            var xL = scope.GridXs[col] - CellIndex.CellIndexEps;
            var xR = scope.GridXs[col + 1] + CellIndex.CellIndexEps;
            var yTop = scope.GridYs[rowTop];
            var yBottom = scope.GridYs[rowBottomEx];
            var yLo = Math.Min(yTop, yBottom) - CellIndex.CellIndexEps;
            var yHi = Math.Max(yTop, yBottom) + CellIndex.CellIndexEps;
            return pt.X >= xL && pt.X <= xR && pt.Y >= yLo && pt.Y <= yHi;
        }

        private static Point3d GetEntityTextPoint(Entity ent)
        {
            if (ent is DBText db)
            {
                try
                {
                    if (db.AlignmentPoint != Point3d.Origin)
                    {
                        return db.AlignmentPoint;
                    }
                }
                catch
                {
                    // ignore
                }

                return db.Position;
            }

            if (ent is MText mt)
            {
                return mt.Location;
            }

            return Point3d.Origin;
        }

        public static void ApplyQtyCenterAlignment(DBText dbText, Point3d point)
        {
            dbText.HorizontalMode = TextHorizontalMode.TextCenter;
            dbText.VerticalMode = TextVerticalMode.TextVerticalMid;
            dbText.AlignmentPoint = point;
            dbText.Position = point;
            try
            {
                dbText.AdjustAlignment(dbText.Database);
            }
            catch
            {
                // ignore
            }
        }

    }
}
