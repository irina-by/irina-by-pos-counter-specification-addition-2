using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace PosCounter.Net.SpecGrid
{
    internal sealed class GridLineSeg
    {
        public double Y;
        public double X1;
        public double X2;
        public double Y1;
        public double Y2;
        public string Layer;
        public bool IsHorizontal;

        public double SegmentLength =>
            IsHorizontal ? Math.Abs(X2 - X1) : Math.Abs(Y2 - Y1);
    }

    internal sealed class TextSample
    {
        public int Row;
        public int Col;
        public string Layer;
        public string Plain;
        public double X;
        public double Y;
        public string Raw;
        /// <summary>§19.6: true для MText, false для DBText.</summary>
        public bool IsMText;
        /// <summary>ExtentsCenter — шапка, pass-1 AssignCells.</summary>
        public double HeaderX;
        public double HeaderY;
        /// <summary>Position/Location — KV-пайплайн.</summary>
        public double DataX;
        public double DataY;
        /// <summary>Вертикальный экстент (GeometricExtents).</summary>
        public double YMin;
        public double YMax;
        public double TextHeight;
        /// <summary>Индекс в AllTexts — дедуп spanning MText.</summary>
        public int SourceIndex;
        /// <summary>Строка с max overlap экстента (pass-2 data).</summary>
        public int DominantRow = -1;
    }

    internal sealed class ScopeGridResult
    {
        public int ScopeIndex;
        public bool Valid;
        public string GridLayer;
        public List<double> GridXs = new List<double>();
        public List<double> GridYs = new List<double>();
        public int ColMark = -1;
        public int ColName = -1;
        public int ColQty = -1;
        /// <summary>Нижняя граница шапки (exclusive): шапка = строки 0 .. HeaderEndRow-1.</summary>
        public int HeaderEndRow;
        public int RowDataStart;
        public int RowDataEnd;
        public string[,] CellText;
        public Dictionary<int, string> MarkNamePairs = new Dictionary<int, string>();
        public Dictionary<int, int> KeyToRowMark = new Dictionary<int, int>();
        /// <summary>Обратный индекс row → key (BindKeysFromProperties).</summary>
        public Dictionary<int, int> RowToKeyMark = new Dictionary<int, int>();
        public Dictionary<int, int> KeyToRowTopSub = new Dictionary<int, int>();
        /// <summary>Нижняя граница блока объединённой марки (exclusive), по следующей цифре в ColMark.</summary>
        public Dictionary<int, int> KeyToMarkBlockEnd = new Dictionary<int, int>();
        /// <summary>Слои штатного содержимого таблицы (§1.6.1 TableContentLayers).</summary>
        public HashSet<string> AllowedTableTextLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Слои пометок специалистов — не в CellText (§1.6.1).</summary>
        public HashSet<string> ExcludedAnnotationLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Доминирующий слой колонки NAME per scope (§1.6.2).</summary>
        public string PrimaryNameLayer;

        /// <summary>Доп. слои NAME с текстом наименования (§17.9.4).</summary>
        public HashSet<string> ExtraNameLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public List<GridLineSeg> HorizontalLines = new List<GridLineSeg>();
        public List<GridLineSeg> VerticalLines = new List<GridLineSeg>();
        public Extents3d? PickBounds;
        public int LineCount;
        public int TextCount;
        public ObjectId OwnerBlockId = ObjectId.Null;
        public List<ObjectId> PickedObjectIds = new List<ObjectId>();
        public List<TextSample> AllTexts = new List<TextSample>();
        /// <summary>Оси сетки дополнены линиями с других слоёв (виртуальный merge).</summary>
        public bool GridAxesMergedFromMixedLayers;
        /// <summary>Слои, участвовавшие в merge (например TM_OCH + 0).</summary>
        public string GridMergeLayerNote;
        /// <summary>maxY текстов выборки (верх таблицы по текстам).</summary>
        public double HeaderTopMaxY;
        public double HeaderTopBandLo;
        public double HeaderTopBandHi;
        /// <summary>Столбцы шапки найдены по верхней полосе текстов (maxY−500).</summary>
        public bool HeaderDetectedByTopTextBand;
        /// <summary>ColMark уточнён по цифрам в данных (до → после EnsureUniqueHeaderColumns).</summary>
        public int ColMarkRefinedFrom = -1;
        public int ColMarkRefinedTo = -1;
        /// <summary>Медиана шага строк сетки (EstimateRowStep).</summary>
        public double MedianRowStep;
        /// <summary>Медиана TextHeight текстов PrimaryNameLayer в ColName.</summary>
        public double PrimaryNameTextHeight;
        /// <summary>Кэш текстов ColName по строке (pass-2 data).</summary>
        public Dictionary<int, List<TextSample>> TextsByRow = new Dictionary<int, List<TextSample>>();
    }

    internal static class TableGridBuilder
    {
        public const double EpsLine = 1.0;
        public const double EpsAxis = 1.5;
        public const double MinGridLineLen = 5000.0;
        public const int MaxLines = 20000;
        public const int MaxTexts = 20000;
        public const int MaxCells = 5000;

        public static ScopeGridResult Build(
            int scopeIndex,
            IReadOnlyList<ObjectId> objectIds,
            Transaction tr,
            string sharedGridLayer,
            SpecGridLog log)
        {
            var result = new ScopeGridResult { ScopeIndex = scopeIndex };
            if (objectIds == null || objectIds.Count == 0)
            {
                log.Warn($"TABLE-GRID: scope={scopeIndex} empty pick");
                return result;
            }

            var horiz = new List<GridLineSeg>();
            var vert = new List<GridLineSeg>();
            var texts = new List<TextSample>();
            Extents3d? bounds = null;
            var lineCount = 0;

            foreach (var id in objectIds)
            {
                if (id.IsNull || !id.IsValid)
                {
                    continue;
                }

                Entity ent;
                try
                {
                    ent = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                }
                catch
                {
                    continue;
                }

                if (ent == null)
                {
                    continue;
                }

                try
                {
                    var ex = ent.GeometricExtents;
                    bounds = bounds.HasValue ? Union(bounds.Value, ex) : ex;
                }
                catch
                {
                    // ignore
                }

                if (ent is Line line)
                {
                    lineCount++;
                    if (lineCount > MaxLines)
                    {
                        log.Warn($"TABLE-GRID: scope={scopeIndex} lines>{MaxLines} → fallback");
                        return result;
                    }

                    AddLine(line, horiz, vert);
                    continue;
                }

                if (ent is DBText db)
                {
                    if (texts.Count >= MaxTexts)
                    {
                        log.Warn($"TABLE-GRID: scope={scopeIndex} texts>{MaxTexts} → fallback");
                        return result;
                    }

                    texts.Add(CreateTextSampleFromDbText(db, texts.Count, scopeIndex, log));
                    continue;
                }

                if (ent is MText mt)
                {
                    if (texts.Count >= MaxTexts)
                    {
                        log.Warn($"TABLE-GRID: scope={scopeIndex} texts>{MaxTexts} → fallback");
                        return result;
                    }

                    texts.Add(CreateTextSampleFromMText(mt, texts.Count, scopeIndex, log));
                }
            }

            _cellAssignLogCount = 0;
            result.PickBounds = bounds;
            result.LineCount = lineCount;
            result.TextCount = texts.Count;
            result.HorizontalLines = horiz;
            result.VerticalLines = vert;
            result.AllTexts = texts;
            result.PickedObjectIds = objectIds.Where(id => !id.IsNull && id.IsValid).Distinct().ToList();
            try
            {
                if (objectIds.Count > 0)
                {
                    var first = tr.GetObject(objectIds[0], OpenMode.ForRead, false) as Entity;
                    if (first != null && !first.OwnerId.IsNull)
                    {
                        result.OwnerBlockId = first.OwnerId;
                    }
                }
            }
            catch
            {
                // ignore
            }

            var gridLayer = sharedGridLayer;
            if (string.IsNullOrWhiteSpace(gridLayer))
            {
                gridLayer = AutoDetectGridLayer(horiz, vert, log, scopeIndex);
            }

            result.GridLayer = gridLayer;

            var merged = BuildMergedGridAxes(horiz, vert, gridLayer, scopeIndex, log);
            var filteredH = merged.HorizForBind;
            var filteredV = merged.VertForBind;
            var xs = merged.Xs;
            var ys = merged.Ys;
            result.GridXs = xs;
            result.GridYs = ys;
            result.GridAxesMergedFromMixedLayers = merged.MergedFromMixedLayers;
            result.GridMergeLayerNote = merged.MergeLayerNote ?? string.Empty;

            log.Info($"TABLE-GRID: scope={scopeIndex} lines(h/v)={filteredH.Count}/{filteredV.Count} layer={gridLayer ?? "*"} merged={merged.MergedFromMixedLayers}");
            if (xs.Count > 0)
            {
                log.Debug($"TABLE-GRID: scope={scopeIndex} Xs: {string.Join(", ", xs.Select(v => v.ToString("F2", CultureInfo.InvariantCulture)))}");
            }

            if (ys.Count > 0)
            {
                log.Debug($"TABLE-GRID: scope={scopeIndex} Ys: {string.Join(", ", ys.Select(v => v.ToString("F2", CultureInfo.InvariantCulture)))}");
            }

            result.Valid = xs.Count >= 4 && ys.Count >= 4;
            if (!result.Valid)
            {
                log.Warn($"TABLE-GRID: scope={scopeIndex} invalid grid (Xs={xs.Count}, Ys={ys.Count}) → fallback");
                return result;
            }

            var rows = ys.Count - 1;
            var cols = xs.Count - 1;
            if (rows * cols > MaxCells)
            {
                log.Warn($"TABLE-GRID: scope={scopeIndex} cells>{MaxCells} → fallback");
                result.Valid = false;
                return result;
            }

            result.RowDataEnd = rows - 1;
            var xsArr = xs.ToArray();
            var ysArr = ys.ToArray();
            AssignCellsHeader(texts, xsArr, ysArr);
            // Pass 1: all layers → header + layer statistics.
            result.CellText = BuildCellMatrix(texts, rows, cols, result, log, filterTableLayers: false);
            EstimateHeaderEndRow(result, filteredH, log);
            DetectHeader(result, log);
            ComputeRowDataStart(result, null, log);
            BuildPrimaryNameLayer(texts, result, log);
            BuildTableContentLayers(texts, result, log);
            result.MedianRowStep = EstimateRowStep(ysArr, result.RowDataStart);
            result.PrimaryNameTextHeight = ComputePrimaryNameTextHeight(texts, result);
            // Pass 2: data-координаты (точка DataX/Y); split ColName после pass-2.
            AssignCellsData(texts, xsArr, ysArr, result);
            SplitNameColumnRowsData(texts, ysArr, result, log);
            BuildTextsByRow(result);
            result.CellText = BuildCellMatrix(texts, rows, cols, result, log, filterTableLayers: true);
            ComputeRowDataStart(result, filteredH, log);
            BindKeysFromProperties(result, log);
            BindKeys(result, filteredH, log);
            AlignRowDataStartToFirstMark(result, log);
            FillMarkNamesFromMergeGroups(result, log);
            log.Grid($"scope={scopeIndex} lines={lineCount} h={filteredH.Count} v={filteredV.Count} layer=\"{gridLayer}\" Xs={xs.Count} Ys={ys.Count} valid=YES");
            return result;
        }

        private static string[,] BuildCellMatrix(
            List<TextSample> texts,
            int rows,
            int cols,
            ScopeGridResult result,
            SpecGridLog log,
            bool filterTableLayers)
        {
            var cells = new string[rows, cols];
            var buckets = new Dictionary<(int r, int c), List<TextSample>>();
            foreach (var t in texts)
            {
                if (t.Row < 0 || t.Col < 0 || t.Row >= rows || t.Col >= cols)
                {
                    continue;
                }

                if (filterTableLayers && !PassesCellLayerFilter(t, result))
                {
                    continue;
                }

                var key = (t.Row, t.Col);
                if (!buckets.TryGetValue(key, out var list))
                {
                    list = new List<TextSample>();
                    buckets[key] = list;
                }

                list.Add(t);
            }

            foreach (var kv in buckets)
            {
                cells[kv.Key.r, kv.Key.c] = CellIndex.GetCellText(kv.Value, log, result.ScopeIndex, kv.Key.r, kv.Key.c);
            }

            return cells;
        }

        private static bool PassesTableTextLayer(TextSample t, ScopeGridResult result)
        {
            if (result.AllowedTableTextLayers.Count == 0)
            {
                return true;
            }

            return result.AllowedTableTextLayers.Contains(t.Layer ?? string.Empty);
        }

        private static bool PassesCellLayerFilter(TextSample t, ScopeGridResult result)
        {
            if (result.ColName >= 0 && t.Col == result.ColName)
            {
                var layer = t.Layer ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(result.PrimaryNameLayer)
                    && string.Equals(layer, result.PrimaryNameLayer, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (result.ExtraNameLayers.Count > 0 && result.ExtraNameLayers.Contains(layer))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(result.PrimaryNameLayer))
                {
                    return false;
                }
            }

            return PassesTableTextLayer(t, result);
        }

        private static void BuildPrimaryNameLayer(List<TextSample> texts, ScopeGridResult result, SpecGridLog log)
        {
            result.PrimaryNameLayer = null;
            if (result.ColName < 0)
            {
                return;
            }

            var layerCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in texts)
            {
                if (t.Row < result.RowDataStart || t.Col != result.ColName || string.IsNullOrWhiteSpace(t.Plain))
                {
                    continue;
                }

                var layer = t.Layer ?? string.Empty;
                if (!layerCounts.ContainsKey(layer))
                {
                    layerCounts[layer] = 0;
                }

                layerCounts[layer]++;
            }

            if (layerCounts.Count == 0)
            {
                return;
            }

            var ordered = layerCounts.OrderByDescending(kv => kv.Value).ToList();
            result.PrimaryNameLayer = ordered[0].Key;

            var total = ordered.Sum(x => x.Value);
            foreach (var kv in ordered.Skip(1))
            {
                var allowExtra = false;
                if (total > 0 && (double)kv.Value / total >= 0.05)
                {
                    allowExtra = texts.Any(t =>
                        t.Row >= result.RowDataStart
                        && t.Col == result.ColName
                        && string.Equals(t.Layer ?? string.Empty, kv.Key, StringComparison.OrdinalIgnoreCase)
                        && MTextPlainText.NameScore(t.Plain ?? string.Empty) >= 3);
                }

                if (allowExtra)
                {
                    result.ExtraNameLayers.Add(kv.Key);
                    log.Info($"[NAME-LAYER-EXTRA] scope={result.ScopeIndex} layer=\"{kv.Key}\" count={kv.Value} reason=continuation");
                }
                else if (!result.ExcludedAnnotationLayers.Contains(kv.Key))
                {
                    result.ExcludedAnnotationLayers.Add(kv.Key);
                }
            }

            var top = string.Join(", ", ordered.Take(5).Select(x => $"{x.Key}:{x.Value}"));
            log.Info($"[PRIMARY-NAME-LAYER] scope={result.ScopeIndex} layer=\"{result.PrimaryNameLayer}\" count={ordered[0].Value} top=\"{top}\" extra=\"{string.Join(",", result.ExtraNameLayers)}\"");
        }

        private static void BuildTableContentLayers(List<TextSample> texts, ScopeGridResult result, SpecGridLog log)
        {
            result.AllowedTableTextLayers.Clear();
            result.ExcludedAnnotationLayers.Clear();

            var layerCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var allLayersInCells = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var t in texts)
            {
                if (t.Row < 0 || string.IsNullOrWhiteSpace(t.Plain))
                {
                    continue;
                }

                var layer = t.Layer ?? string.Empty;
                allLayersInCells.Add(layer);

                if (t.Row < result.RowDataStart)
                {
                    result.AllowedTableTextLayers.Add(layer);
                    continue;
                }

                if (!IsSpecDataColumn(t.Col, result))
                {
                    continue;
                }

                if (!layerCounts.ContainsKey(layer))
                {
                    layerCounts[layer] = 0;
                }

                layerCounts[layer]++;
            }

            var total = layerCounts.Values.Sum();
            if (total > 0)
            {
                var covered = 0;
                foreach (var kv in layerCounts.OrderByDescending(x => x.Value).Take(5))
                {
                    result.AllowedTableTextLayers.Add(kv.Key);
                    covered += kv.Value;
                    if (covered >= total * 0.9)
                    {
                        break;
                    }
                }
            }

            foreach (var layer in allLayersInCells)
            {
                if (!result.AllowedTableTextLayers.Contains(layer))
                {
                    result.ExcludedAnnotationLayers.Add(layer);
                }
            }

            var top = layerCounts.OrderByDescending(kv => kv.Value).Take(5).ToList();
            var topStr = string.Join(", ", top.Select(x => $"{x.Key}:{x.Value}"));
            var dropped = texts.Count(t => t.Row >= 0
                && !string.IsNullOrWhiteSpace(t.Plain)
                && !PassesTableTextLayer(t, result));
            var content = string.Join(",", result.AllowedTableTextLayers);
            var excluded = string.Join(",", result.ExcludedAnnotationLayers);
            log.Info($"[TXT-LAYER] scope={result.ScopeIndex} content=\"{content}\" excluded=\"{excluded}\" top=\"{topStr}\" dropped={dropped}");
        }

        private static bool IsSpecDataColumn(int col, ScopeGridResult result)
        {
            if (col < 0)
            {
                return false;
            }

            return col == result.ColMark || col == result.ColName || col == result.ColQty;
        }

        private const int HeaderScanMaxRow = 3;
        private const int MaxHeaderBorderScanRow = 12;
        /// <summary>Верхняя полоса шапки по текстам: от maxY вниз (СПДС: «Спецификация» + строка заголовков).</summary>
        private const double HeaderTopBandHeight = 2000.0;

        /// <summary>Минимум score для назначения столбца (одно совпадение токена в ScoreHeader).</summary>
        private const int MinHeaderScore = 10;
        /// <summary>Минимум уникальных цифр марки в столбце данных для подтверждения ColMark.</summary>
        private const int MinDataMarkKeysForColMark = 2;

        private static bool TryGetHeaderTopTextBandY(ScopeGridResult result, out double yLo, out double yHi)
        {
            yLo = 0;
            yHi = 0;
            if (result?.AllTexts == null || result.AllTexts.Count == 0)
            {
                return false;
            }

            var maxY = double.NegativeInfinity;
            foreach (var t in result.AllTexts)
            {
                if (t != null && t.Y > maxY)
                {
                    maxY = t.Y;
                }
            }

            if (double.IsNegativeInfinity(maxY))
            {
                return false;
            }

            yLo = maxY - HeaderTopBandHeight;
            yHi = maxY + CellIndex.CellIndexEps;
            result.HeaderTopMaxY = maxY;
            result.HeaderTopBandLo = yLo;
            result.HeaderTopBandHi = yHi;
            return true;
        }

        private static int ResolveColumnIndexByX(IReadOnlyList<double> gridXs, double x)
        {
            if (gridXs == null || gridXs.Count < 2)
            {
                return -1;
            }

            const double eps = CellIndex.CellIndexEps;
            for (var i = 0; i < gridXs.Count - 1; i++)
            {
                if (x >= gridXs[i] - eps && x < gridXs[i + 1] + eps)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void CollectHeaderTextFromTopBand(
            ScopeGridResult result,
            int col,
            List<string> parts,
            HashSet<string> seen)
        {
            if (result == null || col < 0 || !TryGetHeaderTopTextBandY(result, out var yLo, out var yHi))
            {
                return;
            }

            foreach (var t in result.AllTexts ?? new List<TextSample>())
            {
                if (t == null || t.Y < yLo || t.Y > yHi)
                {
                    continue;
                }

                if (ResolveColumnIndexByX(result.GridXs, t.X) != col)
                {
                    continue;
                }

                AppendHeaderTextPart(parts, seen, !string.IsNullOrWhiteSpace(t.Raw) ? t.Raw : (t.Plain ?? string.Empty));
            }
        }

        internal static string BuildHeaderTextForColumn(ScopeGridResult result, int col)
        {
            if (result == null || col < 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (result.HeaderDetectedByTopTextBand)
            {
                CollectHeaderTextFromTopBand(result, col, parts, seen);
            }

            if (parts.Count == 0)
            {
                CollectHeaderTextForColumn(result, col, parts, seen);
            }
            if (parts.Count > 0)
            {
                return string.Join(" ", parts);
            }

            if (result.CellText != null)
            {
                var sb = string.Empty;
                var rows = result.CellText.GetLength(0);
                var headerEndRow = ResolveHeaderEndRow(result);
                for (var r = 0; r < headerEndRow && r < rows; r++)
                {
                    var cell = result.CellText[r, col] ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(cell))
                    {
                        sb = (sb + " " + cell).Trim();
                    }
                }

                return sb;
            }

            return string.Empty;
        }

        private static void CollectHeaderTextForColumn(
            ScopeGridResult result,
            int col,
            List<string> parts,
            HashSet<string> seen)
        {
            var headerEndRow = ResolveHeaderEndRow(result);
            var allTexts = result.AllTexts ?? new List<TextSample>();

            foreach (var t in allTexts)
            {
                if (t == null || t.Col != col || t.Row < 0 || t.Row >= headerEndRow)
                {
                    continue;
                }

                AppendHeaderTextPart(parts, seen, !string.IsNullOrWhiteSpace(t.Raw) ? t.Raw : (t.Plain ?? string.Empty));
            }

            foreach (var t in allTexts)
            {
                if (t == null || !IsTextInHeaderColumnBand(result, col, t.X, t.Y))
                {
                    continue;
                }

                AppendHeaderTextPart(parts, seen, !string.IsNullOrWhiteSpace(t.Raw) ? t.Raw : (t.Plain ?? string.Empty));
            }

            if (result.CellText != null)
            {
                var rows = result.CellText.GetLength(0);
                for (var r = 0; r < headerEndRow && r < rows; r++)
                {
                    var cell = result.CellText[r, col] ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(cell))
                    {
                        AppendHeaderTextPart(parts, seen, cell);
                    }
                }
            }
        }

        internal static int CountGeomHeaderTextsForColumn(ScopeGridResult result, int col)
        {
            if (result == null || col < 0)
            {
                return 0;
            }

            var count = 0;
            foreach (var t in result.AllTexts ?? new List<TextSample>())
            {
                if (t != null && IsTextInHeaderColumnBand(result, col, t.X, t.Y))
                {
                    count++;
                }
            }

            return count;
        }

        internal static string BuildHeaderDiagnosticMessage(ScopeGridResult scope)
        {
            if (scope == null || scope.GridXs == null || scope.GridXs.Count < 2)
            {
                return string.Empty;
            }

            var cols = scope.GridXs.Count - 1;
            var withGeom = new List<string>();
            var withoutGeom = new List<string>();
            for (var c = 0; c < cols; c++)
            {
                var geom = CountGeomHeaderTextsForColumn(scope, c);
                var label = FormatHeaderDiagnosticLabel(BuildHeaderTextForColumn(scope, c));
                var entry = $"col{c} geom={geom} «{label}»";
                if (geom > 0)
                {
                    withGeom.Add(entry);
                }
                else
                {
                    withoutGeom.Add(entry);
                }
            }

            var selected = new List<string>();
            selected.AddRange(withGeom.Take(6));
            if (selected.Count < 8)
            {
                selected.AddRange(withoutGeom.Take(8 - selected.Count));
            }

            return selected.Count == 0 ? string.Empty : string.Join("; ", selected);
        }

        internal static bool IsAllHeaderGeomZero(ScopeGridResult scope)
        {
            if (scope?.GridXs == null || scope.GridXs.Count < 2)
            {
                return false;
            }

            var cols = scope.GridXs.Count - 1;
            for (var c = 0; c < cols; c++)
            {
                if (CountGeomHeaderTextsForColumn(scope, c) > 0)
                {
                    return false;
                }
            }

            return cols > 0;
        }

        internal static IEnumerable<string> BuildHeaderExtendedDiagnostic(ScopeGridResult scope)
        {
            var lines = new List<string>();
            if (scope == null)
            {
                return lines;
            }

            if (TryGetHeaderBandY(scope, out var yLo, out var yHi))
            {
                var headerEndRow = scope.HeaderEndRow > 0 ? scope.HeaderEndRow : ResolveHeaderEndRow(scope);
                lines.Add(
                    $"[POSC] Диагностика шапки: Y полоса = {yLo:F1} .. {yHi:F1} (headerEndRow={headerEndRow})");
            }

            var texts = (scope.AllTexts ?? new List<TextSample>())
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Plain ?? t.Raw))
                .OrderBy(t => t.IsMText)
                .Take(3)
                .ToList();

            var idx = 1;
            foreach (var t in texts)
            {
                var kind = t.IsMText ? "MText" : "DBText";
                var plain = MTextPlainText.SanitizeRawContents(
                    !string.IsNullOrWhiteSpace(t.Raw) ? t.Raw : (t.Plain ?? string.Empty));
                var colHit = ResolveColLabelByX(scope, t.X);
                var yInBand = TryGetHeaderBandY(scope, out var yLo2, out var yHi2)
                    && t.Y >= yLo2
                    && t.Y <= yHi2;
                lines.Add(
                    $"[POSC] Текст {idx}: {kind} «{TrimForLog(plain, 24)}» ({t.X:F1}, {t.Y:F1}) {colHit} Y в полосе? {(yInBand ? "да" : "нет")}");
                idx++;
            }

            return lines;
        }

        private static string ResolveColLabelByX(ScopeGridResult result, double x)
        {
            if (result?.GridXs == null || result.GridXs.Count < 2)
            {
                return "col? нет";
            }

            for (var c = 0; c < result.GridXs.Count - 1; c++)
            {
                var xL = result.GridXs[c] - CellIndex.CellIndexEps;
                var xR = result.GridXs[c + 1] + CellIndex.CellIndexEps;
                if (x >= xL && x <= xR)
                {
                    return $"col{c} да";
                }
            }

            return "col? нет";
        }

        private static int ResolveHeaderEndRow(ScopeGridResult result)
        {
            if (result?.HeaderEndRow > 0)
            {
                return result.HeaderEndRow;
            }

            return HeaderScanMaxRow + 1;
        }

        private static string FormatHeaderDiagnosticLabel(string header)
        {
            var s = MTextPlainText.SanitizeRawContents(header ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(s))
            {
                return "—";
            }

            return s.Length > 16 ? s.Substring(0, 16) + "…" : s;
        }

        private static void AppendHeaderTextPart(List<string> parts, HashSet<string> seen, string rawOrPlain)
        {
            var plain = MTextPlainText.SanitizeRawContents(rawOrPlain);
            if (string.IsNullOrWhiteSpace(plain))
            {
                return;
            }

            plain = plain.Trim();
            if (seen.Add(plain))
            {
                parts.Add(plain);
            }
        }

        private static bool TryGetHeaderBandY(ScopeGridResult result, out double yLo, out double yHi)
        {
            yLo = 0;
            yHi = 0;
            if (result?.GridYs == null || result.GridYs.Count < 2)
            {
                return false;
            }

            var endRow = Math.Min(ResolveHeaderEndRow(result), result.GridYs.Count - 1);
            var yA = result.GridYs[0];
            var yB = result.GridYs[endRow];
            var yTop = Math.Max(yA, yB);
            var yBottom = Math.Min(yA, yB);
            yLo = yBottom - CellIndex.CellIndexEps;
            yHi = yTop + CellIndex.CellIndexEps;
            return true;
        }

        private static bool IsTextInHeaderColumnBand(ScopeGridResult result, int col, double x, double y)
        {
            if (result == null || col < 0 || col >= result.GridXs.Count - 1)
            {
                return false;
            }

            if (!TryGetHeaderBandY(result, out var yLo, out var yHi))
            {
                return false;
            }

            var xL = result.GridXs[col] - CellIndex.CellIndexEps;
            var xR = result.GridXs[col + 1] + CellIndex.CellIndexEps;
            return x >= xL && x <= xR && y >= yLo && y <= yHi;
        }

        /// <summary>Текст в полосе X столбца — как в BindKeysFromMarkColumnTexts (Col или X в [xL,xR]).</summary>
        private static bool IsTextInColumnXBand(ScopeGridResult result, int col, TextSample t)
        {
            if (result == null || t == null || col < 0 || col >= result.GridXs.Count - 1)
            {
                return false;
            }

            const double eps = CellIndex.CellIndexEps;
            var xL = result.GridXs[col];
            var xR = result.GridXs[col + 1];
            return t.Col == col
                || (t.DataX >= xL - eps && t.DataX <= xR + eps)
                || (t.X >= xL - eps && t.X <= xR + eps);
        }

        /// <summary>
        /// Уникальные марки, которые реально сможет привязать BindKeys для столбца col
        /// (Row≥0, не секционная строка, X-полоса столбца, ниже шапки по Y).
        /// </summary>
        private static int CountDataMarkKeysInColumn(ScopeGridResult result, int col)
        {
            if (result == null || col < 0 || result.GridXs == null || result.GridXs.Count < 2)
            {
                return 0;
            }

            TryGetHeaderTopTextBandY(result, out _, out _);
            var yCutoff = result.HeaderTopBandLo;
            var keys = new HashSet<int>();

            foreach (var t in result.AllTexts ?? new List<TextSample>())
            {
                if (t == null || t.Row < 0)
                {
                    continue;
                }

                if (IsSectionHeaderRow(result, t.Row))
                {
                    continue;
                }

                if (yCutoff != 0 && t.DataY >= yCutoff)
                {
                    continue;
                }

                if (!IsTextInColumnXBand(result, col, t))
                {
                    continue;
                }

                if (MTextPlainText.TryParseMarkKey(t.Raw ?? t.Plain ?? string.Empty, out var key))
                {
                    keys.Add(key);
                }
            }

            if (result.CellText != null && col < result.CellText.GetLength(1))
            {
                var rows = result.CellText.GetLength(0);
                for (var r = 0; r < rows; r++)
                {
                    if (IsSectionHeaderRow(result, r))
                    {
                        continue;
                    }

                    var cell = result.CellText[r, col] ?? string.Empty;
                    if (MTextPlainText.TryParseMarkKey(cell, out var key))
                    {
                        keys.Add(key);
                    }
                }
            }

            return keys.Count;
        }

        private static void RefineColMarkByDataMarks(
            ScopeGridResult result,
            int[] markScores,
            int[] nameScores,
            int[] qtyScores,
            SpecGridLog log)
        {
            result.ColMarkRefinedFrom = -1;
            result.ColMarkRefinedTo = -1;

            var cols = markScores?.Length ?? 0;
            if (cols <= 0 || result.ColMark < 0)
            {
                return;
            }

            var current = result.ColMark;
            var currentCount = CountDataMarkKeysInColumn(result, current);
            if (currentCount >= MinDataMarkKeysForColMark)
            {
                log?.Info(
                    $"TABLE-GRID: scope={result.ScopeIndex} ColMark={current} dataMarks={currentCount} OK");
                return;
            }

            var bestCol = -1;
            var bestCount = -1;
            var bestMarkScore = -1;
            for (var c = 0; c < cols; c++)
            {
                if (c == result.ColQty)
                {
                    continue;
                }

                var cnt = CountDataMarkKeysInColumn(result, c);
                var ms = markScores[c];
                var better = cnt > bestCount
                    || (cnt == bestCount && cnt >= MinDataMarkKeysForColMark && ms > bestMarkScore)
                    || (cnt >= MinDataMarkKeysForColMark && bestCount < MinDataMarkKeysForColMark);
                if (better)
                {
                    bestCol = c;
                    bestCount = cnt;
                    bestMarkScore = ms;
                }
            }

            if (bestCol < 0 || bestCount < MinDataMarkKeysForColMark || bestCol == current)
            {
                log?.Info(
                    $"TABLE-GRID: scope={result.ScopeIndex} ColMark={current} bindableMarks={currentCount} — refine skipped (best col{bestCol}={bestCount})");
                return;
            }

            if (markScores[bestCol] < MinHeaderScore && markScores[current] >= MinHeaderScore)
            {
                log?.Info(
                    $"TABLE-GRID: scope={result.ScopeIndex} ColMark refine: col{bestCol} bindable={bestCount} but header score low — still switching from col{current} (bindable={currentCount})");
            }

            result.ColMarkRefinedFrom = current;
            var maxScore = 0;
            for (var c = 0; c < cols; c++)
            {
                if (markScores[c] > maxScore)
                {
                    maxScore = markScores[c];
                }
            }

            markScores[bestCol] = Math.Max(markScores[bestCol], maxScore) + MinHeaderScore;
            EnsureUniqueHeaderColumns(result, markScores, nameScores, qtyScores, log);
            result.ColMarkRefinedTo = result.ColMark;
            log?.Info(
                $"TABLE-GRID: scope={result.ScopeIndex} ColMark refined {current} -> {result.ColMark} (dataMarks col{bestCol}={bestCount})");
        }

        internal static string FormatDataMarkCountsDiagnostic(ScopeGridResult scope)
        {
            if (scope?.GridXs == null || scope.GridXs.Count < 2)
            {
                return string.Empty;
            }

            var cols = scope.GridXs.Count - 1;
            var parts = new List<string>(cols);
            for (var c = 0; c < cols; c++)
            {
                parts.Add($"col{c}={CountDataMarkKeysInColumn(scope, c)}");
            }

            return string.Join(", ", parts);
        }

        internal static string FormatColMarkRefineMessage(ScopeGridResult scope)
        {
            if (scope == null
                || scope.ColMarkRefinedFrom < 0
                || scope.ColMarkRefinedTo < 0
                || scope.ColMarkRefinedFrom == scope.ColMarkRefinedTo)
            {
                return string.Empty;
            }

            var counts = FormatDataMarkCountsDiagnostic(scope);
            return $"[POSC] Столбец «Поз.» уточнён: {scope.ColMarkRefinedFrom} → {scope.ColMarkRefinedTo} (марок в данных: {counts})";
        }

        private static void DetectHeader(ScopeGridResult result, SpecGridLog log)
        {
            result.HeaderDetectedByTopTextBand = false;
            if (DetectHeaderByTopTextBand(result, log))
            {
                return;
            }

            DetectHeaderByColumns(result, log);
        }

        /// <summary>Шапка по верхней полосе текстов maxY−500..maxY, столбец только по X.</summary>
        private static bool DetectHeaderByTopTextBand(ScopeGridResult result, SpecGridLog log)
        {
            var cols = result.GridXs.Count - 1;
            if (cols <= 0)
            {
                result.ColMark = -1;
                result.ColName = -1;
                result.ColQty = -1;
                return false;
            }

            if (!TryGetHeaderTopTextBandY(result, out var yLo, out var yHi))
            {
                return false;
            }

            var markScores = new int[cols];
            var nameScores = new int[cols];
            var qtyScores = new int[cols];
            var bandTextCount = 0;

            foreach (var t in result.AllTexts ?? new List<TextSample>())
            {
                if (t == null || t.Y < yLo || t.Y > yHi)
                {
                    continue;
                }

                bandTextCount++;
                var col = ResolveColumnIndexByX(result.GridXs, t.X);
                if (col < 0)
                {
                    continue;
                }

                var header = MTextPlainText.SanitizeRawContents(
                    !string.IsNullOrWhiteSpace(t.Raw) ? t.Raw : (t.Plain ?? string.Empty)).ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(header))
                {
                    continue;
                }

                markScores[col] += ScoreHeader(header, "марка", "поз", "поз.", "mark", "п/п", "№", "номер", "item");
                nameScores[col] += ScoreHeader(header, "наимен", "name", "назван", "наименование");
                qtyScores[col] += ScoreHeader(header, "кол", "qty", "quantity", "кол-во", "к-во", "ед");
            }

            EnsureUniqueHeaderColumns(result, markScores, nameScores, qtyScores, log);
            RefineColMarkByDataMarks(result, markScores, nameScores, qtyScores, log);
            log?.Info(
                $"TABLE-GRID: scope={result.ScopeIndex} HeaderTopBand maxY={result.HeaderTopMaxY:F1} yLo={yLo:F1} texts={bandTextCount} MARK={result.ColMark} QTY={result.ColQty} NAME={result.ColName}");

            if (result.ColMark >= 0 && result.ColQty >= 0)
            {
                result.HeaderDetectedByTopTextBand = true;
                return true;
            }

            return false;
        }

        /// <summary>Fallback: шапка по столбцам сетки (BuildHeaderTextForColumn + GridYs).</summary>
        private static void DetectHeaderByColumns(ScopeGridResult result, SpecGridLog log)
        {
            var cols = result.GridXs.Count - 1;
            if (cols <= 0)
            {
                result.ColMark = -1;
                result.ColName = -1;
                result.ColQty = -1;
                return;
            }

            var markScores = new int[cols];
            var nameScores = new int[cols];
            var qtyScores = new int[cols];

            for (var c = 0; c < cols; c++)
            {
                var header = BuildHeaderTextForColumn(result, c);
                header = MTextPlainText.SanitizeRawContents(header).ToLowerInvariant();
                markScores[c] = ScoreHeader(header, "марка", "поз", "поз.", "mark", "п/п", "№", "номер", "item");
                nameScores[c] = ScoreHeader(header, "наимен", "name", "назван", "наименование");
                qtyScores[c] = ScoreHeader(header, "кол", "qty", "quantity", "кол-во", "к-во", "ед");
            }

            EnsureUniqueHeaderColumns(result, markScores, nameScores, qtyScores, log);
            RefineColMarkByDataMarks(result, markScores, nameScores, qtyScores, log);
            log?.Info(
                $"TABLE-GRID: scope={result.ScopeIndex} header cols (column fallback): MARK={result.ColMark} QTY={result.ColQty} NAME={result.ColName}");
        }

        internal static IEnumerable<string> BuildHeaderTopBandDiagnostic(ScopeGridResult scope)
        {
            var lines = new List<string>();
            if (scope == null || !TryGetHeaderTopTextBandY(scope, out var yLo, out var yHi))
            {
                lines.Add("[POSC] Шапка по текстам: нет текстов в выборке");
                return lines;
            }

            var bandTexts = (scope.AllTexts ?? new List<TextSample>())
                .Where(t => t != null && t.Y >= yLo && t.Y <= yHi)
                .ToList();

            lines.Add(
                $"[POSC] Шапка по текстам: maxY={scope.HeaderTopMaxY:F1} полоса {yLo:F1}..{yHi:F1} (текстов={bandTexts.Count})");

            foreach (var t in bandTexts.Take(12))
            {
                var kind = t.IsMText ? "MText" : "DBText";
                var plain = MTextPlainText.SanitizeRawContents(
                    !string.IsNullOrWhiteSpace(t.Raw) ? t.Raw : (t.Plain ?? string.Empty));
                var col = ResolveColumnIndexByX(scope.GridXs, t.X);
                var colLabel = col >= 0 ? $"col{col}" : "col?";
                var header = plain.ToLowerInvariant();
                var score = Math.Max(
                    ScoreHeader(header, "марка", "поз", "поз.", "mark", "п/п", "№", "номер", "item"),
                    Math.Max(
                        ScoreHeader(header, "наимен", "name", "назван", "наименование"),
                        ScoreHeader(header, "кол", "qty", "quantity", "кол-во", "к-во", "ед")));
                lines.Add(
                    $"[POSC]   {kind} «{TrimForLog(plain, 24)}» X={t.X:F1} Y={t.Y:F1} → {colLabel} score={score}");
            }

            return lines;
        }

        /// <summary>Марка → Кол. → Наименование: каждый столбец назначается не более одной роли.</summary>
        internal static void EnsureUniqueHeaderColumns(
            ScopeGridResult result,
            int[] markScores,
            int[] nameScores,
            int[] qtyScores,
            SpecGridLog log)
        {
            if (result == null || markScores == null || nameScores == null || qtyScores == null)
            {
                return;
            }

            var taken = new HashSet<int>();

            result.ColMark = PickBestHeaderColumn(markScores, taken);
            if (result.ColMark >= 0)
            {
                taken.Add(result.ColMark);
            }

            result.ColQty = PickBestHeaderColumn(qtyScores, taken);
            if (result.ColQty >= 0)
            {
                taken.Add(result.ColQty);
            }

            result.ColName = PickBestHeaderColumn(nameScores, taken);

            log?.Info(
                $"TABLE-GRID: scope={result.ScopeIndex} header cols (unique): MARK={result.ColMark} QTY={result.ColQty} NAME={result.ColName}");
            if (result.ColName >= 0 && result.ColName + 1 < result.GridXs.Count)
            {
                log?.Info(
                    $"TABLE-GRID: scope={result.ScopeIndex} NAME X=[{result.GridXs[result.ColName]:F2}, {result.GridXs[result.ColName + 1]:F2}] QTY X=[{(result.ColQty >= 0 ? result.GridXs[result.ColQty].ToString("F2", CultureInfo.InvariantCulture) : "?")}, {(result.ColQty >= 0 && result.ColQty + 1 < result.GridXs.Count ? result.GridXs[result.ColQty + 1].ToString("F2", CultureInfo.InvariantCulture) : "?")}]");
            }
        }

        private static int PickBestHeaderColumn(int[] scores, HashSet<int> excluded)
        {
            if (scores == null || scores.Length == 0)
            {
                return -1;
            }

            var bestCol = -1;
            var bestScore = -1;
            for (var c = 0; c < scores.Length; c++)
            {
                if (excluded != null && excluded.Contains(c))
                {
                    continue;
                }

                if (scores[c] > bestScore)
                {
                    bestScore = scores[c];
                    bestCol = c;
                }
            }

            return bestScore >= MinHeaderScore ? bestCol : -1;
        }

        /// <summary>
        /// Первая строка данных: первая цифра марки в ColMark **после** границы шапки (LINE под строками 0–1).
        /// Не жёсткая строка 2 — ищем по сетке и по марке.
        /// </summary>
        private static void ComputeRowDataStart(ScopeGridResult result, List<GridLineSeg> horiz, SpecGridLog log)
        {
            var rows = result.CellText?.GetLength(0) ?? 0;
            var passLabel = horiz != null && horiz.Count > 0 ? "pass2(filtered CellText+lines)" : "pass1(raw CellText)";
            var rowDataStartBefore = result.RowDataStart;

            if (result.ColMark < 0 || rows == 0)
            {
                result.RowDataStart = 0;
                log?.RowDataDiag($"[ROW-DATA] scope={result.ScopeIndex} {passLabel}: ColMark<0 or no rows → RowDataStart=0");
                return;
            }

            var isPass2 = horiz != null && horiz.Count > 0;
            var searchFrom = FindFirstDataRowAfterHeaderBoundary(result, horiz);
            log?.RowDataDiag(
                $"[ROW-DATA] scope={result.ScopeIndex} {passLabel}: searchFrom={searchFrom} horiz={(horiz == null ? "null" : horiz.Count.ToString(CultureInfo.InvariantCulture))} RowDataStart_before={rowDataStartBefore}");
            LogMarkRowsAfterHeader(result, searchFrom, rows, passLabel, log);

            for (var r = searchFrom; r < rows; r++)
            {
                var mark = result.CellText[r, result.ColMark] ?? string.Empty;
                if (MTextPlainText.TryParseMarkKey(mark, out var key))
                {
                    result.RowDataStart = r;
                    log?.RowDataDiag(
                        $"[ROW-DATA] scope={result.ScopeIndex} {passLabel}: HIT CellText row={r} key={key} → RowDataStart={r}");
                    RejectBadPass2RowDataStart(result, rowDataStartBefore, isPass2, searchFrom, passLabel, log);
                    LogRowDataStartChange(result, rowDataStartBefore, passLabel, log);
                    return;
                }
            }

            log?.RowDataDiag($"[ROW-DATA] scope={result.ScopeIndex} {passLabel}: CellText — нет TryParseMarkKey с row>={searchFrom}, смотрим AllTexts…");

            var textRow = FindFirstMarkRowFromAllTexts(result, searchFrom);
            if (textRow >= 0)
            {
                result.RowDataStart = textRow;
                LogAllTextsMarkOnRow(result, textRow, passLabel, log);
                log?.RowDataDiag(
                    $"[ROW-DATA] scope={result.ScopeIndex} {passLabel}: HIT AllTexts row={textRow} → RowDataStart={textRow}");
                RejectBadPass2RowDataStart(result, rowDataStartBefore, isPass2, searchFrom, passLabel, log);
                LogRowDataStartChange(result, rowDataStartBefore, passLabel, log);
                return;
            }

            log?.RowDataDiag($"[ROW-DATA] scope={result.ScopeIndex} {passLabel}: AllTexts — нет марки, пробуем любой непустой ColMark…");

            for (var r = searchFrom; r < rows; r++)
            {
                var mark = (result.CellText[r, result.ColMark] ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(mark) || IsHeaderLabelInMarkCell(mark))
                {
                    continue;
                }

                result.RowDataStart = r;
                log?.RowDataDiag(
                    $"[ROW-DATA] scope={result.ScopeIndex} {passLabel}: HIT non-header text row={r} mark={DescribeMarkCellForLog(mark)} → RowDataStart={r}");
                RejectBadPass2RowDataStart(result, rowDataStartBefore, isPass2, searchFrom, passLabel, log);
                LogRowDataStartChange(result, rowDataStartBefore, passLabel, log);
                return;
            }

            result.RowDataStart = Math.Min(searchFrom, rows - 1);
            log?.RowDataDiag(
                $"[ROW-DATA] scope={result.ScopeIndex} {passLabel}: FALLBACK RowDataStart={result.RowDataStart} (searchFrom={searchFrom}, марка не найдена)");
            RejectBadPass2RowDataStart(result, rowDataStartBefore, isPass2, searchFrom, passLabel, log);
            LogRowDataStartChange(result, rowDataStartBefore, passLabel, log);
        }

        /// <summary>Pass2 не должен уводить RowDataStart в середину таблицы из-за внутренней LINE (searchFrom=9 и т.п.).</summary>
        private static void RejectBadPass2RowDataStart(
            ScopeGridResult result,
            int rowDataStartBefore,
            bool isPass2,
            int searchFrom,
            string passLabel,
            SpecGridLog log)
        {
            if (!isPass2 || rowDataStartBefore <= 0)
            {
                return;
            }

            if (result.RowDataStart <= rowDataStartBefore + 1)
            {
                return;
            }

            var firstMark = FindFirstMarkRowFromAllTexts(result, Math.Min(rowDataStartBefore, searchFrom));
            if (firstMark >= 0 && firstMark < result.RowDataStart)
            {
                log?.RowDataDiag(
                    $"[ROW-DATA] scope={result.ScopeIndex} {passLabel}: ОТКАТ RowDataStart {result.RowDataStart} → {rowDataStartBefore} (ошибочный searchFrom={searchFrom}, первая марка row={firstMark})");
                result.RowDataStart = rowDataStartBefore;
            }
        }

        private static void LogRowDataStartChange(ScopeGridResult result, int before, string passLabel, SpecGridLog log)
        {
            // Диагностика [ROW-DATA] отключена.
        }

        private static void LogMarkRowsAfterHeader(ScopeGridResult result, int searchFrom, int rows, string passLabel, SpecGridLog log)
        {
            // Диагностика [ROW-DATA] отключена.
        }

        private static void LogAllTextsMarkOnRow(ScopeGridResult result, int row, string passLabel, SpecGridLog log)
        {
            // Диагностика [ROW-DATA] отключена.
        }

        private static string DescribeMarkCellForLog(string mark)
        {
            if (string.IsNullOrWhiteSpace(mark))
            {
                return "(empty)";
            }

            if (MTextPlainText.TryParseMarkKey(mark, out var key))
            {
                return $"key={key} len={mark.Length} «{TrimForLog(mark, 36)}»";
            }

            if (IsHeaderLabelInMarkCell(mark))
            {
                return $"header len={mark.Length} «{TrimForLog(mark, 36)}»";
            }

            return $"text len={mark.Length} «{TrimForLog(mark, 36)}» no-key";
        }

        private static string DescribeShortCell(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "(empty)";
            }

            return $"len={text.Length} «{TrimForLog(text.Trim(), 28)}»";
        }

        private static string TrimForLog(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var t = text.Replace("\r", " ").Replace("\n", " ");
            return t.Length <= maxLen ? t : t.Substring(0, maxLen) + "…";
        }

        private static string DescribeAllTextsMarkOnRow(ScopeGridResult result, int row)
        {
            if (result.ColMark < 0 || result.GridXs.Count < result.ColMark + 2)
            {
                return "n/a";
            }

            var xL = result.GridXs[result.ColMark];
            var xR = result.GridXs[result.ColMark + 1];
            const double eps = 2.0;
            var parts = new List<string>();
            foreach (var t in result.AllTexts)
            {
                if (t.Row != row)
                {
                    continue;
                }

                var inMarkCol = t.Col == result.ColMark
                    || (t.X >= xL - eps && t.X <= xR + eps);
                if (!inMarkCol || string.IsNullOrWhiteSpace(t.Plain))
                {
                    continue;
                }

                var layer = t.Layer ?? "?";
                var keyPart = MTextPlainText.TryParseMarkKey(t.Plain, out var k)
                    ? $"key={k}"
                    : "no-key";
                parts.Add($"{keyPart} layer={layer} «{TrimForLog(t.Plain, 24)}»");
            }

            return parts.Count == 0 ? "(none)" : string.Join("; ", parts);
        }

        private static void EstimateHeaderEndRow(ScopeGridResult result, List<GridLineSeg> horiz, SpecGridLog log)
        {
            var rows = result.CellText?.GetLength(0) ?? (result.GridYs?.Count - 1 ?? 0);
            if (rows <= 0 || result.GridXs == null || result.GridXs.Count < 2)
            {
                result.HeaderEndRow = Math.Min(6, Math.Max(0, rows - 1));
                return;
            }

            var xL = result.GridXs[0];
            var xR = result.GridXs[result.GridXs.Count - 1];
            var lastBorderRow = FindHeaderBoundaryRow(result, horiz, xL, xR, MaxHeaderBorderScanRow);
            int headerEndRow;
            if (lastBorderRow >= 0)
            {
                headerEndRow = lastBorderRow;
            }
            else
            {
                var markRow = FindFirstMarkRowFromCellText(result, minRow: 2);
                headerEndRow = markRow >= 0 ? markRow : Math.Min(6, rows - 1);
            }

            result.HeaderEndRow = headerEndRow;
            log?.Info($"TABLE-GRID: scope={result.ScopeIndex} HeaderEndRow={headerEndRow}");
        }

        private static int FindFirstMarkRowFromCellText(ScopeGridResult result, int minRow)
        {
            if (result.CellText == null)
            {
                return -1;
            }

            var rows = result.CellText.GetLength(0);
            var cols = result.CellText.GetLength(1);
            for (var r = minRow; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    var cell = result.CellText[r, c] ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(cell))
                    {
                        continue;
                    }

                    if (MTextPlainText.TryParseMarkKey(cell, out _) && !IsHeaderLabelInMarkCell(cell))
                    {
                        return r;
                    }
                }
            }

            return -1;
        }

        private static int FindHeaderBoundaryRow(
            ScopeGridResult result,
            List<GridLineSeg> horiz,
            double xL,
            double xR,
            int maxScanRow)
        {
            var rows = result.CellText?.GetLength(0) ?? (result.GridYs?.Count - 1 ?? 0);
            if (rows <= 0 || horiz == null || horiz.Count == 0 || result.GridYs == null || result.GridYs.Count < 2)
            {
                return -1;
            }

            var lastBorderRow = -1;
            for (var r = 1; r <= Math.Min(rows - 1, maxScanRow); r++)
            {
                if (r >= result.GridYs.Count)
                {
                    break;
                }

                var y = result.GridYs[r];
                if (HasHBorderAt(y, xL, xR, horiz, borderEps: EpsAxis * 3.0))
                {
                    lastBorderRow = r;
                }
            }

            return lastBorderRow;
        }

        /// <summary>
        /// Строка, с которой начинается поиск данных: сразу под первой H-линией шапки в ColMark
        /// (типично: строки 0–1 — подписи, LINE, с r=2 — позиции). Без привязки к номеру марки.
        /// </summary>
        private static int FindFirstDataRowAfterHeaderBoundary(ScopeGridResult result, List<GridLineSeg> horiz)
        {
            var rows = result.CellText?.GetLength(0) ?? 0;
            if (rows <= 0)
            {
                return 0;
            }

            var searchFrom = Math.Min(2, rows - 1);
            if (result.ColMark < 0 || result.GridXs.Count <= result.ColMark + 1 || result.GridYs.Count < 3)
            {
                return searchFrom;
            }

            if (horiz == null || horiz.Count == 0)
            {
                return searchFrom;
            }

            var xL = result.GridXs[result.ColMark];
            var xR = result.GridXs[result.ColMark + 1];
            // Только полоса шапки (строки 1–4), не вся таблица — иначе lastBorderRow=9 и данные с r=10.
            const int headerBandMaxRow = 4;
            var lastBorderRow = FindHeaderBoundaryRow(result, horiz, xL, xR, headerBandMaxRow);

            if (lastBorderRow >= 0)
            {
                searchFrom = lastBorderRow;
            }

            if (rows >= 4 && searchFrom < 2)
            {
                searchFrom = 2;
            }

            return Math.Min(searchFrom, rows - 1);
        }

        private static bool IsHeaderLabelInMarkCell(string mark)
        {
            if (MTextPlainText.TryParseMarkKey(mark, out _))
            {
                return false;
            }

            var header = MTextPlainText.SanitizeRawContents(mark).ToLowerInvariant();
            return ScoreHeader(header, "марка", "поз", "mark", "№", "п/п", "наимен", "кол") > 0;
        }

        private static int FindFirstMarkRowFromAllTexts(ScopeGridResult result, int minRow = 0)
        {
            if (result.ColMark < 0 || result.GridXs.Count < 2)
            {
                return -1;
            }

            var xL = result.GridXs[result.ColMark];
            var xR = result.GridXs[result.ColMark + 1];
            const double eps = 2.0;
            var minRowFound = int.MaxValue;
            foreach (var t in result.AllTexts)
            {
                if (t.Row < minRow)
                {
                    continue;
                }

                if (t.Row < 0)
                {
                    continue;
                }

                var inMarkCol = t.Col == result.ColMark
                    || (t.X >= xL - eps && t.X <= xR + eps);
                if (!inMarkCol)
                {
                    continue;
                }

                if (!MTextPlainText.TryParseMarkKey(t.Plain ?? string.Empty, out _))
                {
                    continue;
                }

                if (t.Row < minRowFound)
                {
                    minRowFound = t.Row;
                }
            }

            return minRowFound == int.MaxValue ? -1 : minRowFound;
        }

        private static void AlignRowDataStartToFirstMark(ScopeGridResult result, SpecGridLog log)
        {
            if (result.KeyToRowMark.Count == 0)
            {
                return;
            }

            var firstKeyRow = result.KeyToRowMark.Values.Min();
            if (firstKeyRow < result.RowDataStart)
            {
                log?.RowDataDiag(
                    $"[ROW-DATA] scope={result.ScopeIndex} AlignRowDataStartToFirstMark: {result.RowDataStart} → {firstKeyRow} (min KeyToRowMark)");
                log.Info($"TABLE-GRID: scope={result.ScopeIndex} RowDataStart {result.RowDataStart} -> {firstKeyRow} (first bound key)");
                result.RowDataStart = firstKeyRow;
            }
            else
            {
                log?.RowDataDiag(
                    $"[ROW-DATA] scope={result.ScopeIndex} AlignRowDataStartToFirstMark: RowDataStart={result.RowDataStart} firstKeyRow={firstKeyRow} (без сдвига)");
            }
        }

        private static int ScoreHeader(string header, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                return 0;
            }

            var score = 0;
            foreach (var t in tokens)
            {
                if (header.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 10;
                }
            }

            return score;
        }

        private static void FillMarkNamesFromMergeGroups(ScopeGridResult result, SpecGridLog log)
        {
            result.MarkNamePairs.Clear();
            if (result.ColMark < 0)
            {
                return;
            }

            log.Info($"TABLE-ROWS: scope={result.ScopeIndex} startRow={result.RowDataStart} endRow={result.RowDataEnd} colMark={result.ColMark} colName={result.ColName}");
            var missing = 0;
            foreach (var kv in result.KeyToRowMark.OrderBy(x => x.Key))
            {
                var name = ResolveNameFromMergeGroup(result, kv.Key, log, out var joinMeta);
                result.MarkNamePairs[kv.Key] = name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    missing++;
                    log.Info($"TABLE-ROWS: key={kv.Key} mark row={kv.Value} → name=\"\" MISSING_NAME");
                    LogNameLayerDiagnostics(result, kv.Key, kv.Value, log);
                    LogNameDebugKeys(result, kv.Key, kv.Value, log, joinMeta.BoundaryReason, joinMeta.RowEndExclusive);
                }
                else
                {
                    log.Info($"TABLE-ROWS: key={kv.Key} → name=\"{name}\" (merge-join)");
                    if (joinMeta.Parts > 0)
                    {
                        log.Info($"[NAME-JOIN] scope={result.ScopeIndex} key={kv.Key} parts={joinMeta.Parts} texts={joinMeta.Texts} rows={joinMeta.RowTop}..{joinMeta.RowEnd}");
                        log.Info($"[KV-PAIR] scope={result.ScopeIndex} key={kv.Key} texts={joinMeta.Texts} parts={joinMeta.Parts} value=\"{TrimForLog(name, 80)}\"");
                    }
                }
            }

            log.Success($"TABLE-ROWS: scope={result.ScopeIndex} pairs={result.MarkNamePairs.Count} missingName={missing} duplicates=0");
        }

        private static void LogNameDebugKeys(
            ScopeGridResult result,
            int key,
            int rowMark,
            SpecGridLog log,
            string boundaryReason,
            int rowEndExclusive)
        {
            // Диагностика [NAME-DBG] отключена.
        }

        private readonly struct NameJoinMeta
        {
            public NameJoinMeta(int rowTop, int rowEnd, int parts, int rowEndExclusive, string boundaryReason, int texts)
            {
                RowTop = rowTop;
                RowEnd = rowEnd;
                Parts = parts;
                RowEndExclusive = rowEndExclusive;
                BoundaryReason = boundaryReason ?? string.Empty;
                Texts = texts;
            }

            public int RowTop { get; }
            public int RowEnd { get; }
            public int Parts { get; }
            public int RowEndExclusive { get; }
            public string BoundaryReason { get; }
            public int Texts { get; }
        }

        private static string GetCellNameAtRow(ScopeGridResult result, int row)
        {
            if (result.ColName < 0 || row < 0 || row >= result.CellText.GetLength(0))
            {
                return string.Empty;
            }

            return result.CellText[row, result.ColName] ?? string.Empty;
        }

        private static void LogNameLayerDiagnostics(ScopeGridResult result, int key, int rowMark, SpecGridLog log)
        {
            // Диагностика [NAME-LAYER] отключена.
        }

        private static void BindKeyToRow(ScopeGridResult result, int key, int row)
        {
            if (!result.KeyToRowMark.ContainsKey(key))
            {
                result.KeyToRowMark[key] = row;
                return;
            }

            var existing = result.KeyToRowMark[key];
            var existingName = result.ColName >= 0 ? result.CellText[existing, result.ColName] : string.Empty;
            var newName = result.ColName >= 0 ? result.CellText[row, result.ColName] : string.Empty;
            if (string.IsNullOrWhiteSpace(existingName) && !string.IsNullOrWhiteSpace(newName))
            {
                result.KeyToRowMark[key] = row;
            }
            else if (row < existing)
            {
                result.KeyToRowMark[key] = row;
            }
        }

        private static void BindKeysFromProperties(ScopeGridResult result, SpecGridLog log)
        {
            result.KeyToRowMark.Clear();
            result.RowToKeyMark.Clear();
            if (result.ColMark < 0 || result.GridXs.Count < 2)
            {
                return;
            }

            TryGetHeaderTopTextBandY(result, out _, out _);
            var yCutoff = result.HeaderTopBandLo;
            var rowCandidates = new Dictionary<int, List<(int Key, TextSample Sample)>>();

            foreach (var t in result.AllTexts ?? new List<TextSample>())
            {
                if (t.Row < 0 || IsSectionHeaderRow(result, t.Row))
                {
                    continue;
                }

                if (yCutoff != 0 && t.DataY >= yCutoff)
                {
                    continue;
                }

                if (!IsTextInColumnXBand(result, result.ColMark, t))
                {
                    continue;
                }

                if (!MTextPlainText.TryParseMarkKey(t.Raw ?? t.Plain ?? string.Empty, out var key))
                {
                    continue;
                }

                if (!rowCandidates.TryGetValue(t.Row, out var list))
                {
                    list = new List<(int, TextSample)>();
                    rowCandidates[t.Row] = list;
                }

                list.Add((key, t));
            }

            var rowWinners = new Dictionary<int, int>();
            foreach (var kv in rowCandidates)
            {
                var row = kv.Key;
                var keys = kv.Value.Select(x => x.Key).Distinct().ToList();
                if (keys.Count == 1)
                {
                    rowWinners[row] = keys[0];
                    continue;
                }

                var winner = keys[0];
                var cellMark = result.CellText != null && row < result.CellText.GetLength(0)
                    ? result.CellText[row, result.ColMark] ?? string.Empty
                    : string.Empty;
                if (MTextPlainText.TryParseMarkKey(cellMark, out var cellKey) && keys.Contains(cellKey))
                {
                    winner = cellKey;
                }
                else
                {
                    winner = kv.Value.OrderByDescending(x => x.Sample.DataY).First().Key;
                }

                rowWinners[row] = winner;
                log?.Info($"[POSC] Марка: наложение в ячейке row={row} keys={string.Join(",", keys.OrderBy(x => x))} → выбран {winner}");
            }

            foreach (var kv in rowWinners)
            {
                BindKeyToRow(result, kv.Value, kv.Key);
                result.RowToKeyMark[kv.Key] = kv.Value;
                if (kv.Value <= 3)
                {
                    log?.RowDataDiag(
                        $"[ROW-DATA] scope={result.ScopeIndex} BindKeysFromProperties: key={kv.Value} row={kv.Key}");
                }

                log.Debug($"TABLE-GRID: scope={result.ScopeIndex} bind key={kv.Value} row={kv.Key} source=Properties");
            }
        }

        private static void BindKeys(ScopeGridResult result, List<GridLineSeg> horiz, SpecGridLog log)
        {
            result.KeyToRowTopSub.Clear();
            result.KeyToMarkBlockEnd.Clear();
            if (result.ColMark < 0)
            {
                return;
            }

            foreach (var kv in result.KeyToRowMark)
            {
                var rowMark = kv.Value;
                var rowTop = FindRowTopSub(result, horiz, rowMark);
                result.KeyToRowTopSub[kv.Key] = rowTop;
                var blockEnd = GetMarkBlockEndExclusive(result, rowTop, kv.Key);
                result.KeyToMarkBlockEnd[kv.Key] = blockEnd;
                var span = blockEnd - rowTop;
                if (kv.Key <= 3)
                {
                    log?.RowDataDiag(
                        $"[ROW-DATA] scope={result.ScopeIndex} key={kv.Key} rowMark={rowMark} rowTop={rowTop} blockEnd={blockEnd} RowDataStart={result.RowDataStart}");
                }

                log.Debug($"MERGE-BLOCK: key={kv.Key} rowMark={rowMark} rowTop={rowTop} blockEndEx={blockEnd} span={span}");
            }
        }

        public static string ResolveNameFromMergeGroup(ScopeGridResult grid, int key)
        {
            return ResolveNameFromMergeGroup(grid, key, null, out _);
        }

        private static string ResolveNameFromMergeGroup(
            ScopeGridResult grid,
            int key,
            SpecGridLog log,
            out NameJoinMeta meta)
        {
            meta = new NameJoinMeta(0, 0, 0, 0, string.Empty, 0);
            if (!grid.KeyToRowMark.TryGetValue(key, out var rowMark) || grid.ColName < 0)
            {
                return string.Empty;
            }

            var rowTop = grid.KeyToRowTopSub.TryGetValue(key, out var rt) ? rt : rowMark;
            var markBlockEnd = grid.KeyToMarkBlockEnd.TryGetValue(key, out var be)
                ? be
                : GetMarkBlockEndExclusive(grid, rowTop, key);
            var isMerged = markBlockEnd > rowTop + 1 || rowTop < rowMark;
            // Имя: все строки от верха блока марки до верха следующей непустой марки (3–5+ строк NAME).
            var rowEndExclusive = GetNextKeyRowExclusive(grid, key);
            var boundaryReason = $"nameRows {rowTop}..{rowEndExclusive - 1} markBlockEnd={markBlockEnd} merged={isMerged}";

            rowEndExclusive = Math.Min(Math.Max(rowEndExclusive, rowTop + 1), grid.GridYs.Count);

            var parts = new List<string>();
            var textCount = CollectNamePartsForPositionRange(grid, key, rowTop, rowEndExclusive, parts, log);
            textCount += SupplementNamePartsInVerticalBand(grid, key, rowTop, rowEndExclusive, parts, log);

            var rowEndInclusive = rowEndExclusive > rowTop ? rowEndExclusive - 1 : rowTop;
            meta = new NameJoinMeta(rowTop, rowEndInclusive, parts.Count, rowEndExclusive, boundaryReason, textCount);

            var shouldLogBoundary = parts.Count == 0 || key == 1 || key == 4 || key == 5 || key == 45 || key == 52 || key == 98
                || key <= 10 || key == 51 || key == 57 || key == 70
                || key == 104 || key == 105 || key >= 53 && key <= 56 || key >= 106 && key <= 109;
            var joined = MTextPlainText.FormatForPaletteDisplay(string.Join(" ", parts).Trim());
            if (log != null && shouldLogBoundary)
            {
                log.Info(
                    $"[NAME-BOUNDARY] scope={grid.ScopeIndex} key={key} rowTop={rowTop} rowMark={rowMark} merged={isMerged} rowEndEx={rowEndExclusive} reason={boundaryReason} parts={parts.Count} texts={textCount} value=\"{TrimForLog(joined, 80)}\"");
            }

            return joined;
        }

        /// <summary>Верхняя граница следующей позиции (rowTopSub), чтобы не захватывать её имя.</summary>
        private static int GetNextKeyRowExclusive(ScopeGridResult grid, int key)
        {
            var rows = grid.CellText.GetLength(0);
            foreach (var kv in grid.KeyToRowMark.OrderBy(x => x.Key))
            {
                if (kv.Key <= key)
                {
                    continue;
                }

                if (grid.KeyToRowTopSub.TryGetValue(kv.Key, out var nextTop))
                {
                    return Math.Min(nextTop, rows);
                }

                return Math.Min(kv.Value, rows);
            }

            return rows;
        }

        /// <summary>
        /// Нижняя граница блока марки: до следующей цифры в ColMark.
        /// Линии сетки в «Наименовании» не режут блок (иначе merge только на 2 строки).
        /// </summary>
        private static int GetMarkBlockEndExclusive(ScopeGridResult grid, int rowTop, int key)
        {
            var rows = grid.CellText.GetLength(0);
            if (grid.ColMark < 0 || rowTop < 0 || rowTop >= rows)
            {
                return Math.Min(rowTop + 1, rows);
            }

            var r = rowTop + 1;
            while (r < rows)
            {
                var otherKey = ResolveMarkKeyAtRow(grid, r);
                if (otherKey > 0 && otherKey != key)
                {
                    return r;
                }

                var mark = (grid.CellText[r, grid.ColMark] ?? string.Empty).Trim();
                if (otherKey <= 0 && MTextPlainText.TryParseMarkKey(mark, out otherKey) && otherKey != key)
                {
                    return r;
                }

                r++;
            }

            var end = rows;
            end = Math.Max(end, ExtendMarkBlockEndByMarkTextY(grid, rowTop, key, end));
            return Math.Min(end, rows);
        }

        private static int ExtendMarkBlockEndByMarkTextY(ScopeGridResult grid, int rowTop, int key, int currentEnd)
        {
            if (grid.ColMark < 0 || grid.GridYs.Count < 2)
            {
                return currentEnd;
            }

            var xL = grid.GridXs[grid.ColMark];
            var xR = grid.GridXs[grid.ColMark + 1];
            const double eps = 2.0;
            var maxRow = rowTop;
            foreach (var t in grid.AllTexts)
            {
                if (t.Col != grid.ColMark && (t.DataX < xL - eps || t.DataX > xR + eps))
                {
                    continue;
                }

                if (!MTextPlainText.TryParseMarkKey(t.Raw ?? t.Plain ?? string.Empty, out var tk) || tk != key)
                {
                    continue;
                }

                if (!CellIndex.TryGetCellIndex(t.DataX, t.DataY, grid.GridXs, grid.GridYs, out var row, out _))
                {
                    continue;
                }

                if (row > maxRow)
                {
                    maxRow = row;
                }
            }

            return Math.Max(currentEnd, maxRow + 1);
        }

        private static int ResolveMarkKeyAtRow(ScopeGridResult grid, int row)
        {
            if (grid.RowToKeyMark != null && grid.RowToKeyMark.TryGetValue(row, out var key))
            {
                return key;
            }

            foreach (var kv in grid.KeyToRowMark)
            {
                if (kv.Value == row)
                {
                    return kv.Key;
                }
            }

            if (grid.ColMark >= 0 && grid.CellText != null && row >= 0 && row < grid.CellText.GetLength(0))
            {
                var mark = grid.CellText[row, grid.ColMark] ?? string.Empty;
                if (MTextPlainText.TryParseMarkKey(mark, out var cellKey))
                {
                    return cellKey;
                }
            }

            return 0;
        }

        private static int GetDominantRowForText(TextSample t, ScopeGridResult grid)
        {
            if (t.DominantRow >= 0)
            {
                return t.DominantRow;
            }

            return CellIndex.GetDominantRow(t, grid.GridYs, grid);
        }

        private static int ResolveOwnerMarkKeyForNameText(ScopeGridResult grid, TextSample t)
        {
            var markAtPoint = t.Row >= 0 ? ResolveMarkKeyAtRow(grid, t.Row) : 0;
            var domRow = GetDominantRowForText(t, grid);
            var markAtDom = domRow >= 0 ? ResolveMarkKeyAtRow(grid, domRow) : 0;

            if (markAtPoint > 0 && markAtDom > 0)
            {
                if (markAtPoint == markAtDom)
                {
                    return markAtPoint;
                }

                var upperRow = t.Row >= 0 && domRow >= 0 ? Math.Min(t.Row, domRow) : (t.Row >= 0 ? t.Row : domRow);
                return ResolveMarkKeyAtRow(grid, upperRow);
            }

            if (markAtPoint > 0)
            {
                return markAtPoint;
            }

            if (markAtDom > 0)
            {
                return markAtDom;
            }

            return 0;
        }

        private static bool NameTextBelongsToMarkKey(
            ScopeGridResult grid,
            int key,
            int rowTop,
            int rowEndExclusive,
            TextSample t,
            SpecGridLog log)
        {
            var owner = ResolveOwnerMarkKeyForNameText(grid, t);
            if (owner > 0)
            {
                if (owner != key && ShouldLogForeignSkip(key))
                {
                    log?.Debug(
                        $"[NAME-FOREIGN-SKIP] scope={grid.ScopeIndex} key={key} owner={owner} src={t.SourceIndex} tRow={t.Row} display=\"{TrimForLog(GetDisplayText(t), 40)}\"");
                }

                return owner == key;
            }

            return t.Row >= rowTop && t.Row < rowEndExclusive;
        }

        private static bool ShouldLogForeignSkip(int key)
        {
            return key == 1 || key == 3 || key == 4 || key == 5 || key == 52 || key == 98;
        }

        private static bool IsUpstreamBleedFromForeignMark(
            ScopeGridResult grid,
            int key,
            int row,
            double yTop,
            double yBottom,
            TextSample t,
            SpecGridLog log)
        {
            var markAtRow = ResolveMarkKeyAtRow(grid, row);
            if (TextPointInRowBand(t, yTop, yBottom) && (markAtRow == key || markAtRow == 0))
            {
                return false;
            }

            if (t.Row == row && (markAtRow == key || markAtRow == 0))
            {
                return false;
            }

            var dominantRow = GetDominantRowForText(t, grid);
            if (dominantRow < 0 || dominantRow >= row)
            {
                return false;
            }

            var markAtDom = ResolveMarkKeyAtRow(grid, dominantRow);
            if (markAtDom > 0 && markAtDom != key && !TextPointInRowBand(t, yTop, yBottom))
            {
                log?.Info(
                    $"[NAME-BLEED] scope={grid.ScopeIndex} key={key} row={row} domRow={dominantRow} foreignMark={markAtDom} src={t.SourceIndex}");
                return true;
            }

            return false;
        }

        private static bool TextPointInRowBand(TextSample t, double yTop, double yBottom)
        {
            const double eps = CellIndex.CellIndexEps;
            return t.DataY <= yTop + eps && t.DataY > yBottom - eps;
        }

        private static bool ShouldLogNameRow(int key)
        {
            return key == 1 || key == 4 || key == 5 || key == 52 || key == 98;
        }

        private static bool AddNamePartsFromTextSample(
            ScopeGridResult grid,
            int key,
            int row,
            TextSample winner,
            List<string> parts,
            SpecGridLog log)
        {
            var addedAny = false;
            foreach (var line in MTextPlainText.EnumerateDisplayNameLines(winner.Raw ?? winner.Plain))
            {
                if (MTextPlainText.LooksLikeSectionHeaderLine(line))
                {
                    log?.Info($"[NAME-SECTION] scope={grid.ScopeIndex} key={key} row={row} line=\"{TrimForLog(line, 40)}\"");
                    continue;
                }

                if (!MTextPlainText.IsAcceptableNameContinuation(line))
                {
                    continue;
                }

                var decoded = MTextPlainText.DecodeAutocadPercentCodes(line);
                if (string.IsNullOrWhiteSpace(decoded))
                {
                    continue;
                }

                TryAddNamePartExact(parts, decoded);
                addedAny = true;
            }

            return addedAny;
        }

        private static bool PartsContainStandalone(IReadOnlyList<string> parts)
        {
            return parts.Any(MTextPlainText.IsStandaloneProductName);
        }

        private static int CollectNamePartsForPositionRange(
            ScopeGridResult grid,
            int key,
            int rowTop,
            int rowEndExclusive,
            List<string> parts,
            SpecGridLog log)
        {
            if (grid.ColName < 0 || grid.GridXs.Count <= grid.ColName + 1 || grid.GridYs.Count == 0)
            {
                return 0;
            }

            if (rowTop < 0 || rowTop >= grid.GridYs.Count)
            {
                return 0;
            }

            rowEndExclusive = Math.Min(rowEndExclusive, grid.GridYs.Count);
            if (rowEndExclusive <= rowTop)
            {
                rowEndExclusive = Math.Min(rowTop + 1, grid.GridYs.Count);
            }

            var consumedSources = new HashSet<int>();
            var textCount = 0;

            for (var r = rowTop; r < rowEndExclusive; r++)
            {
                var rowKey = ResolveMarkKeyAtRow(grid, r);
                if (rowKey > 0 && rowKey != key)
                {
                    continue;
                }

                if (IsSectionHeaderRow(grid, r))
                {
                    continue;
                }

                if (CollectNamePartsFromNameCell(grid, key, rowTop, rowEndExclusive, r, parts, consumedSources, log, ref textCount))
                {
                    return textCount;
                }
            }

            return textCount;
        }

        /// <summary>Pass 1: все тексты строки с overlap. true = stop (second standalone).</summary>
        private static bool CollectNamePartsFromNameCell(
            ScopeGridResult grid,
            int key,
            int rowTop,
            int rowEndExclusive,
            int row,
            List<string> parts,
            HashSet<int> consumedSources,
            SpecGridLog log,
            ref int textCount)
        {
            var yTop = grid.GridYs[row];
            var yBottom = row + 1 < grid.GridYs.Count ? grid.GridYs[row + 1] : yTop - 1.0;

            var rowHits = (grid.AllTexts ?? new List<TextSample>())
                .Where(t => PassesCellLayerFilter(t, grid))
                .Where(t => IsTextInColumnXBand(grid, grid.ColName, t))
                .Where(t => TextOverlapsRowBand(t, yTop, yBottom, grid))
                .Where(t => NameTextBelongsToMarkKey(grid, key, rowTop, rowEndExclusive, t, log))
                .OrderByDescending(t => t.DataY)
                .ThenBy(t => t.DataX)
                .ToList();

            foreach (var t in rowHits)
            {
                if (string.IsNullOrWhiteSpace(t.Raw ?? t.Plain))
                {
                    continue;
                }

                if (ShouldLogNameRow(key))
                {
                    log?.Info(
                        $"[NAME-ROW] scope={grid.ScopeIndex} key={key} row={row} src={t.SourceIndex} type={(t.IsMText ? "MText" : "DBText")} display=\"{TrimForLog(GetDisplayText(t), 40)}\"");
                }

                var display = GetDisplayText(t);
                if (PartsContainStandalone(parts) && MTextPlainText.IsStandaloneProductName(display))
                {
                    log?.Info($"[NAME-STOP] scope={grid.ScopeIndex} key={key} row={row} second standalone");
                    return true;
                }

                if (consumedSources.Contains(t.SourceIndex))
                {
                    continue;
                }

                consumedSources.Add(t.SourceIndex);
                textCount++;
                AddNamePartsFromTextSample(grid, key, row, t, parts, log);
            }

            return false;
        }

        private static int SupplementNamePartsInVerticalBand(
            ScopeGridResult grid,
            int key,
            int rowTop,
            int rowEndExclusive,
            List<string> parts,
            SpecGridLog log)
        {
            if (grid.ColName < 0 || grid.GridXs.Count <= grid.ColName + 1 || grid.GridYs.Count == 0)
            {
                return 0;
            }

            if (rowTop < 0 || rowTop >= grid.GridYs.Count)
            {
                return 0;
            }

            rowEndExclusive = Math.Min(rowEndExclusive, grid.GridYs.Count);
            if (rowEndExclusive <= rowTop)
            {
                rowEndExclusive = Math.Min(rowTop + 1, grid.GridYs.Count);
            }

            var yTop = grid.GridYs[rowTop];
            var yBottom = rowEndExclusive < grid.GridYs.Count
                ? grid.GridYs[rowEndExclusive]
                : grid.GridYs[grid.GridYs.Count - 1];
            const double eps = CellIndex.CellIndexEps;
            var supplementCount = 0;

            var hits = (grid.AllTexts ?? new List<TextSample>())
                .Where(t => PassesCellLayerFilter(t, grid))
                .Where(t => IsTextInColumnXBand(grid, grid.ColName, t))
                .Where(t => t.DataY <= yTop + eps && t.DataY > yBottom - eps)
                .Where(t => NameTextBelongsToMarkKey(grid, key, rowTop, rowEndExclusive, t, log))
                .OrderByDescending(t => t.DataY)
                .ThenBy(t => t.DataX)
                .ToList();

            foreach (var t in hits)
            {
                if (string.IsNullOrWhiteSpace(t.Raw ?? t.Plain))
                {
                    continue;
                }

                var textRow = t.Row;
                if (textRow < 0 && grid.GridXs.Count >= 2)
                {
                    CellIndex.TryGetCellIndex(t.DataX, t.DataY, grid.GridXs, grid.GridYs, out textRow, out _);
                }

                if (textRow >= 0 && IsSectionHeaderRow(grid, textRow))
                {
                    continue;
                }

                var beforeCount = parts.Count;
                AddNamePartsFromTextSample(grid, key, textRow >= 0 ? textRow : rowTop, t, parts, log);
                if (parts.Count > beforeCount)
                {
                    supplementCount++;
                    if (ShouldLogNameRow(key))
                    {
                        log?.Info(
                            $"[NAME-SUPPLEMENT] scope={grid.ScopeIndex} key={key} src={t.SourceIndex} display=\"{TrimForLog(GetDisplayText(t), 40)}\"");
                    }
                }
            }

            return supplementCount;
        }

        private static string GetDisplayText(TextSample t)
        {
            return MTextPlainText.PreferDisplayNameFromSpec(
                MTextPlainText.SanitizeRawContents(t.Raw ?? t.Plain ?? string.Empty));
        }

        private static bool TextOverlapsRowBand(TextSample t, double yTop, double yBottom, ScopeGridResult grid)
        {
            return TryGetRowOverlapFraction(t, yTop, yBottom, grid, out _);
        }

        private static bool TextOverlapsRowBand(TextSample t, double yTop, double yBottom, double minOverlapFraction = 0.42)
        {
            var bandH = Math.Abs(yTop - yBottom);
            if (bandH < 1.0)
            {
                bandH = 1.0;
            }

            var yMin = t.YMin;
            var yMax = t.YMax;
            if (Math.Abs(yMax - yMin) < 1e-6)
            {
                const double eps = 2.0;
                if (t.DataY <= yTop + eps && t.DataY > yBottom - eps)
                {
                    return true;
                }

                var textHalfH = bandH * 0.38;
                var textBottom = t.DataY - textHalfH;
                var textTop = t.DataY + textHalfH * 0.25;
                var overlap = Math.Min(textTop, yTop) - Math.Max(textBottom, yBottom);
                return overlap > 0 && overlap / bandH >= minOverlapFraction;
            }

            var overlapExt = Math.Min(yMax, yTop) - Math.Max(yMin, yBottom);
            return overlapExt > 0 && overlapExt / bandH >= minOverlapFraction;
        }

        private static void TryAddNamePartExact(List<string> parts, string part)
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                return;
            }

            if (parts.Any(p => string.Equals(p, part, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            parts.Add(part);
        }

        private static string GetTrimmedNameAtRow(ScopeGridResult grid, int row)
        {
            if (grid.ColName < 0 || row < 0 || row >= grid.CellText.GetLength(0))
            {
                return string.Empty;
            }

            return (grid.CellText[row, grid.ColName] ?? string.Empty).Trim();
        }

        private static bool IsSectionHeaderRow(ScopeGridResult grid, int row)
        {
            if (grid.ColMark < 0 || grid.ColName < 0)
            {
                return false;
            }

            var mark = grid.CellText[row, grid.ColMark] ?? string.Empty;
            if (MTextPlainText.TryParseMarkKey(mark, out _))
            {
                return false;
            }

            var name = GetTrimmedNameAtRow(grid, row);
            if (string.IsNullOrWhiteSpace(name) || name.Length < 25 || !MTextPlainText.HasLetter(name))
            {
                return false;
            }

            if (grid.ColQty >= 0)
            {
                var qty = (grid.CellText[row, grid.ColQty] ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(qty))
                {
                    return false;
                }
            }

            return true;
        }

        private static int FindRowTopSub(ScopeGridResult grid, List<GridLineSeg> horiz, int rowMark)
        {
            if (grid.ColMark < 0 || grid.GridXs.Count < 2 || horiz == null || horiz.Count == 0)
            {
                return rowMark;
            }

            var xL = grid.GridXs[grid.ColMark];
            var xR = grid.GridXs[grid.ColMark + 1];
            var row = rowMark;
            var guard = 0;
            while (row > 0 && guard++ < 500)
            {
                var yTop = grid.GridYs[row];
                if (HasHBorderAt(yTop, xL, xR, horiz, borderEps: EpsAxis * 3.0))
                {
                    break;
                }

                row--;
            }

            return row < 0 ? rowMark : row;
        }

        public static bool HasHBorderAt(double y, double xL, double xR, List<GridLineSeg> horiz, double borderEps = -1)
        {
            if (borderEps < 0)
            {
                borderEps = EpsAxis;
            }

            return HasHBorderAtCore(y, xL, xR, horiz, borderEps);
        }

        private static bool HasHBorderAtCore(double y, double xL, double xR, List<GridLineSeg> horiz, double borderEps)
        {
            foreach (var l in horiz)
            {
                if (!l.IsHorizontal)
                {
                    continue;
                }

                if (Math.Abs(l.Y - y) > borderEps)
                {
                    continue;
                }

                var minX = Math.Min(l.X1, l.X2);
                var maxX = Math.Max(l.X1, l.X2);
                if (minX <= xL + borderEps && maxX >= xR - borderEps)
                {
                    return true;
                }
            }

            return false;
        }

        private static int _cellAssignLogCount;

        private static void LogCellAssign(SpecGridLog log, int scopeIndex, string kind, string method, Point3d pt, int plainLen)
        {
            if (log == null || _cellAssignLogCount >= 20)
            {
                return;
            }

            log.Info(
                $"[CELL-ASSIGN] scope={scopeIndex} kind={kind} method={method} x={pt.X:0.##} y={pt.Y:0.##} plainLen={plainLen}");
            _cellAssignLogCount++;
        }

        private static void AssignCellsHeader(List<TextSample> texts, double[] xs, double[] ys)
        {
            foreach (var t in texts)
            {
                t.X = t.HeaderX;
                t.Y = t.HeaderY;
                if (CellIndex.TryGetCellIndex(t.HeaderX, t.HeaderY, xs, ys, out var row, out var col))
                {
                    t.Row = row;
                    t.Col = col;
                }
                else
                {
                    t.Row = -1;
                    t.Col = -1;
                }
            }
        }

        private static void AssignCellsData(List<TextSample> texts, double[] xs, double[] ys, ScopeGridResult result)
        {
            foreach (var t in texts)
            {
                t.X = t.DataX;
                t.Y = t.DataY;

                if (!CellIndex.TryGetCellIndex(t.DataX, t.DataY, xs, ys, out var rowByPoint, out var col))
                {
                    col = ResolveColumnByX(t.DataX, xs);
                    rowByPoint = -1;
                }

                t.Col = col;

                var rowByExtent = FindBestRowByExtent(t, ys, result);
                if (rowByPoint >= 0)
                {
                    t.Row = rowByPoint;
                }
                else if (rowByExtent >= 0)
                {
                    t.Row = rowByExtent;
                }
                else
                {
                    t.Row = -1;
                }

                t.DominantRow = CellIndex.GetDominantRow(t, ys, result);
            }
        }

        private static int ResolveColumnByX(double x, double[] xs)
        {
            const double eps = CellIndex.CellIndexEps;
            for (var i = 0; i < xs.Length - 1; i++)
            {
                if (x >= xs[i] - eps && x < xs[i + 1] + eps)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindBestRowByExtent(TextSample t, double[] ys, ScopeGridResult result)
        {
            return CellIndex.GetDominantRow(t, ys, result);
        }

        private static bool TryGetRowOverlapFraction(
            TextSample t,
            double bandTop,
            double bandBottom,
            ScopeGridResult result,
            out double fraction)
        {
            fraction = 0;
            var bandH = Math.Abs(bandTop - bandBottom);
            if (bandH < 1.0)
            {
                bandH = 1.0;
            }

            var yMin = t.YMin;
            var yMax = t.YMax;
            if (Math.Abs(yMax - yMin) < 1e-6)
            {
                var halfH = GetEffectiveTextHeight(t, result) * 0.5;
                yMin = t.DataY - halfH;
                yMax = t.DataY + halfH;
            }

            var overlap = Math.Min(yMax, bandTop) - Math.Max(yMin, bandBottom);
            if (overlap <= 0)
            {
                return false;
            }

            var minFraction = t.IsMText && Math.Abs(t.YMax - t.YMin) > 1e-6 ? 0.30 : 0.50;
            fraction = overlap / bandH;
            return fraction >= minFraction;
        }

        private static double GetEffectiveTextHeight(TextSample t, ScopeGridResult result)
        {
            if (t.TextHeight > 1e-6)
            {
                return t.TextHeight;
            }

            if (result != null && result.PrimaryNameTextHeight > 1e-6)
            {
                return result.PrimaryNameTextHeight;
            }

            if (result != null && result.MedianRowStep > 1e-6)
            {
                return result.MedianRowStep * 0.75;
            }

            return SpecGridService.QtyTextHeightFallback;
        }

        private static double ComputePrimaryNameTextHeight(List<TextSample> texts, ScopeGridResult result)
        {
            if (result.ColName < 0 || string.IsNullOrWhiteSpace(result.PrimaryNameLayer))
            {
                return 0;
            }

            var heights = texts
                .Where(t => t.Col == result.ColName
                    && t.Row >= result.RowDataStart
                    && string.Equals(t.Layer ?? string.Empty, result.PrimaryNameLayer, StringComparison.OrdinalIgnoreCase)
                    && t.TextHeight > 1e-6)
                .Select(t => t.TextHeight)
                .OrderBy(h => h)
                .ToList();
            if (heights.Count == 0)
            {
                return 0;
            }

            return heights[heights.Count / 2];
        }

        private static void BuildTextsByRow(ScopeGridResult result)
        {
            result.TextsByRow = new Dictionary<int, List<TextSample>>();
            if (result.ColName < 0)
            {
                return;
            }

            foreach (var t in result.AllTexts ?? new List<TextSample>())
            {
                if (t.Row < result.RowDataStart || !IsTextInColumnXBand(result, result.ColName, t))
                {
                    continue;
                }

                if (!PassesCellLayerFilter(t, result))
                {
                    continue;
                }

                if (!result.TextsByRow.TryGetValue(t.Row, out var list))
                {
                    list = new List<TextSample>();
                    result.TextsByRow[t.Row] = list;
                }

                list.Add(t);
            }
        }

        private static double GetSortY(TextSample t)
        {
            return t.IsMText && Math.Abs(t.YMax - t.YMin) > 1e-6 ? t.YMax : t.DataY;
        }

        private static void SplitNameColumnRowsData(
            List<TextSample> texts,
            double[] ys,
            ScopeGridResult result,
            SpecGridLog log)
        {
            if (result.ColName < 0 || ys == null || ys.Length < 3)
            {
                return;
            }

            var rowStep = result.MedianRowStep > 1e-6
                ? result.MedianRowStep
                : EstimateRowStep(ys, result.RowDataStart);
            if (rowStep <= 0)
            {
                return;
            }

            var halfStep = rowStep * 0.45;
            var xs = result.GridXs.ToArray();
            var byRow = texts
                .Where(t => t.Col == result.ColName && t.Row >= result.RowDataStart && !string.IsNullOrWhiteSpace(t.Plain))
                .GroupBy(t => t.Row)
                .ToList();

            foreach (var group in byRow)
            {
                var ordered = group.OrderByDescending(t => t.DataY).ToList();
                if (ordered.Count < 2)
                {
                    continue;
                }

                for (var i = 1; i < ordered.Count; i++)
                {
                    var upper = ordered[i - 1];
                    var lower = ordered[i];
                    if (Math.Abs(upper.DataY - lower.DataY) < halfStep)
                    {
                        continue;
                    }

                    if (!CellIndex.TryGetCellIndex(lower.DataX, lower.DataY, xs, ys, out var newRow, out var newCol)
                        || newCol != result.ColName
                        || newRow == lower.Row)
                    {
                        continue;
                    }

                    log.Debug(
                        $"[CELL-SPLIT-DATA] scope={result.ScopeIndex} row {lower.Row}->{newRow} dy={Math.Abs(upper.DataY - lower.DataY):0}");
                    lower.Row = newRow;
                }
            }
        }

        private static void SplitNameColumnRows(
            List<TextSample> texts,
            double[] ys,
            ScopeGridResult result,
            SpecGridLog log)
        {
            if (result.ColName < 0 || ys == null || ys.Length < 3)
            {
                return;
            }

            var rowStep = EstimateRowStep(ys, result.RowDataStart);
            if (rowStep <= 0)
            {
                return;
            }

            var halfStep = rowStep * 0.45;
            var xs = result.GridXs.ToArray();
            var byRow = texts
                .Where(t => t.Col == result.ColName && t.Row >= result.RowDataStart && !string.IsNullOrWhiteSpace(t.Plain))
                .GroupBy(t => t.Row)
                .ToList();

            foreach (var group in byRow)
            {
                var ordered = group.OrderByDescending(t => t.Y).ToList();
                if (ordered.Count < 2)
                {
                    continue;
                }

                for (var i = 1; i < ordered.Count; i++)
                {
                    var upper = ordered[i - 1];
                    var lower = ordered[i];
                    if (Math.Abs(upper.Y - lower.Y) < halfStep)
                    {
                        continue;
                    }

                    if (!CellIndex.TryGetCellIndex(lower.X, lower.Y, xs, ys, out var newRow, out var newCol)
                        || newCol != result.ColName
                        || newRow == lower.Row)
                    {
                        continue;
                    }

                    log.Debug($"[CELL-SPLIT] scope={result.ScopeIndex} row {lower.Row}->{newRow} dy={Math.Abs(upper.Y - lower.Y):0}");
                    lower.Row = newRow;
                }
            }
        }

        private static double EstimateRowStep(double[] ys, int rowDataStart)
        {
            if (ys.Length < 3)
            {
                return 0;
            }

            var steps = new List<double>();
            for (var j = Math.Max(0, rowDataStart); j < ys.Length - 1 && steps.Count < 20; j++)
            {
                var d = Math.Abs(ys[j] - ys[j + 1]);
                if (d > 10)
                {
                    steps.Add(d);
                }
            }

            return steps.Count == 0 ? 0 : steps.OrderBy(x => x).ElementAt(steps.Count / 2);
        }

        private static TextSample CreateTextSampleFromMText(MText mt, int sourceIndex, int scopeIndex, SpecGridLog log)
        {
            var dataPt = mt.Location;
            var headerPt = dataPt;
            var yMin = dataPt.Y;
            var yMax = dataPt.Y;
            var method = "Location";
            try
            {
                var ex = mt.GeometricExtents;
                headerPt = new Point3d(
                    (ex.MinPoint.X + ex.MaxPoint.X) * 0.5,
                    (ex.MinPoint.Y + ex.MaxPoint.Y) * 0.5,
                    mt.Location.Z);
                yMin = ex.MinPoint.Y;
                yMax = ex.MaxPoint.Y;
                dataPt = new Point3d(mt.Location.X, ex.MaxPoint.Y, mt.Location.Z);
                method = "ExtentsTop";
            }
            catch
            {
                // fallback Location
            }

            var sample = CreateTextSample(mt, mt.Contents, headerPt, dataPt, yMin, yMax, mt.TextHeight, sourceIndex);
            LogCellAssign(log, scopeIndex, "MText", method, headerPt, (sample.Plain ?? string.Empty).Length);
            return sample;
        }

        private static TextSample CreateTextSampleFromDbText(DBText db, int sourceIndex, int scopeIndex, SpecGridLog log)
        {
            var dataPt = GetDbTextPoint(db);
            var headerPt = dataPt;
            var yMin = dataPt.Y;
            var yMax = dataPt.Y;
            var method = "AlignmentPoint";
            try
            {
                var ex = db.GeometricExtents;
                headerPt = new Point3d(
                    (ex.MinPoint.X + ex.MaxPoint.X) * 0.5,
                    (ex.MinPoint.Y + ex.MaxPoint.Y) * 0.5,
                    db.Position.Z);
                method = "ExtentsCenter";
            }
            catch
            {
                // пустой TEXT или GeometricExtents недоступен — GetDbTextPoint
            }

            var sample = CreateTextSample(db, db.TextString, headerPt, dataPt, yMin, yMax, db.Height, sourceIndex);
            LogCellAssign(log, scopeIndex, "DBText", method, headerPt, (sample.Plain ?? string.Empty).Length);
            return sample;
        }

        private static TextSample CreateTextSample(
            Entity ent,
            string raw,
            Point3d headerPt,
            Point3d dataPt,
            double yMin,
            double yMax,
            double textHeight,
            int sourceIndex)
        {
            var plain = MTextPlainText.SanitizeRawContents(raw);
            return new TextSample
            {
                Layer = MTextPlainText.NormalizeLayer(ent.Layer),
                Plain = plain,
                Raw = raw ?? string.Empty,
                HeaderX = headerPt.X,
                HeaderY = headerPt.Y,
                DataX = dataPt.X,
                DataY = dataPt.Y,
                X = headerPt.X,
                Y = headerPt.Y,
                YMin = yMin,
                YMax = yMax,
                TextHeight = textHeight,
                SourceIndex = sourceIndex,
                IsMText = ent is MText
            };
        }

        private static Point3d GetDbTextPoint(DBText db)
        {
            try
            {
                var ap = db.AlignmentPoint;
                if (ap != Point3d.Origin || db.HorizontalMode != TextHorizontalMode.TextLeft)
                {
                    return ap;
                }
            }
            catch
            {
                // ignore
            }

            return db.Position;
        }

        private static List<GridLineSeg> FilterLines(List<GridLineSeg> lines, string gridLayer)
        {
            if (string.IsNullOrWhiteSpace(gridLayer))
            {
                return lines;
            }

            return lines.Where(l => string.Equals(l.Layer, gridLayer, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private sealed class MergedGridBuildResult
        {
            public List<double> Xs = new List<double>();
            public List<double> Ys = new List<double>();
            public List<GridLineSeg> HorizForBind = new List<GridLineSeg>();
            public List<GridLineSeg> VertForBind = new List<GridLineSeg>();
            public bool MergedFromMixedLayers;
            public string MergeLayerNote;
        }

        private static MergedGridBuildResult BuildMergedGridAxes(
            List<GridLineSeg> horiz,
            List<GridLineSeg> vert,
            string gridLayer,
            int scope,
            SpecGridLog log)
        {
            var result = new MergedGridBuildResult();
            if (string.IsNullOrWhiteSpace(gridLayer))
            {
                result.Xs = ClusterAxis(vert.Select(l => l.X1), true);
                result.Ys = ClusterAxis(horiz.Where(l => l.IsHorizontal).Select(l => l.Y), false);
                result.HorizForBind = horiz;
                result.VertForBind = vert;
                log.Info($"TABLE-GRID: scope={scope} merged axes all layers (no dominant layer)");
                return result;
            }

            var dominantH = FilterLines(horiz, gridLayer);
            var dominantV = FilterLines(vert, gridLayer);
            var minorityH = horiz
                .Where(l => !string.Equals(l.Layer, gridLayer, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var minorityV = vert
                .Where(l => !string.Equals(l.Layer, gridLayer, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var xs = ClusterAxis(dominantV.Select(l => l.X1), true);
            var ys = ClusterAxis(dominantH.Where(l => l.IsHorizontal).Select(l => l.Y), false);
            var hadMinority = minorityH.Count > 0 || minorityV.Count > 0;
            if (hadMinority)
            {
                xs = MergeAxisClusters(xs, minorityV.Select(l => l.X1), sortAsc: true);
                ys = MergeAxisClusters(ys, minorityH.Where(l => l.IsHorizontal).Select(l => l.Y), sortAsc: false);
                result.MergedFromMixedLayers = true;
                var layers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { gridLayer };
                foreach (var seg in minorityH.Concat(minorityV))
                {
                    layers.Add(seg.Layer ?? "0");
                }

                result.MergeLayerNote = string.Join(" + ", layers.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
                log.Info($"TABLE-GRID: scope={scope} merged axes from mixed layers: {result.MergeLayerNote}");
            }

            result.Xs = xs;
            result.Ys = ys;
            result.HorizForBind = hadMinority ? horiz : dominantH;
            result.VertForBind = hadMinority ? vert : dominantV;
            return result;
        }

        private static List<double> MergeAxisClusters(List<double> primary, IEnumerable<double> extra, bool sortAsc)
        {
            var merged = new List<double>(primary);
            foreach (var v in extra)
            {
                if (double.IsNaN(v) || double.IsInfinity(v))
                {
                    continue;
                }

                if (merged.Any(m => Math.Abs(m - v) <= EpsAxis))
                {
                    continue;
                }

                merged.Add(v);
            }

            merged = sortAsc
                ? merged.OrderBy(v => v).ToList()
                : merged.OrderByDescending(v => v).ToList();
            return ClusterAxis(merged, sortAsc);
        }

        private static string AutoDetectGridLayer(List<GridLineSeg> horiz, List<GridLineSeg> vert, SpecGridLog log, int scope)
        {
            var candidates = horiz.Concat(vert).Where(IsGridCandidate).ToList();
            if (candidates.Count == 0)
            {
                log.Warn($"TABLE-GRID: scope={scope} auto grid layer ambiguous → no-layer-filter");
                return null;
            }

            var groups = candidates.GroupBy(c => c.Layer ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { Layer = g.Key, Count = g.Count(), Len = g.Sum(l => l.SegmentLength) })
                .OrderByDescending(x => x.Count)
                .ToList();
            var top = groups[0];
            var share = (double)top.Count / candidates.Count;
            if (share < 0.3)
            {
                log.Warn($"TABLE-GRID: scope={scope} auto grid layer ambiguous → no-layer-filter");
                return null;
            }

            log.Info($"TABLE-GRID: auto grid layer={top.Layer} candidates={candidates.Count} share={share:0.00}");
            return top.Layer;
        }

        private static bool IsGridCandidate(GridLineSeg l)
        {
            return l.SegmentLength >= MinGridLineLen;
        }

        private static void AddLine(Line line, List<GridLineSeg> horiz, List<GridLineSeg> vert)
        {
            var p1 = line.StartPoint;
            var p2 = line.EndPoint;
            var seg = new GridLineSeg
            {
                Layer = line.Layer ?? string.Empty,
                X1 = p1.X,
                X2 = p2.X,
                Y1 = p1.Y,
                Y2 = p2.Y,
                Y = p1.Y
            };

            if (Math.Abs(p1.Y - p2.Y) <= EpsLine)
            {
                seg.IsHorizontal = true;
                seg.Y = (p1.Y + p2.Y) * 0.5;
                seg.X1 = Math.Min(p1.X, p2.X);
                seg.X2 = Math.Max(p1.X, p2.X);
                seg.Y1 = seg.Y2 = seg.Y;
                horiz.Add(seg);
                return;
            }

            if (Math.Abs(p1.X - p2.X) <= EpsLine)
            {
                var x = (p1.X + p2.X) * 0.5;
                var yLo = Math.Min(p1.Y, p2.Y);
                var yHi = Math.Max(p1.Y, p2.Y);
                vert.Add(new GridLineSeg
                {
                    IsHorizontal = false,
                    Layer = seg.Layer,
                    X1 = x,
                    X2 = x,
                    Y1 = yLo,
                    Y2 = yHi,
                    Y = (yLo + yHi) * 0.5
                });
            }
        }

        private static List<double> ClusterAxis(IEnumerable<double> values, bool sortAsc)
        {
            var list = values.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).OrderBy(v => v).ToList();
            if (list.Count == 0)
            {
                return new List<double>();
            }

            var clusters = new List<double> { list[0] };
            for (var i = 1; i < list.Count; i++)
            {
                if (Math.Abs(list[i] - clusters[clusters.Count - 1]) <= EpsAxis)
                {
                    continue;
                }

                clusters.Add(list[i]);
            }

            return sortAsc ? clusters : clusters.OrderByDescending(v => v).ToList();
        }

        private static Extents3d Union(Extents3d a, Extents3d b)
        {
            return new Extents3d(
                new Point3d(Math.Min(a.MinPoint.X, b.MinPoint.X), Math.Min(a.MinPoint.Y, b.MinPoint.Y), 0),
                new Point3d(Math.Max(a.MaxPoint.X, b.MaxPoint.X), Math.Max(a.MaxPoint.Y, b.MaxPoint.Y), 0));
        }
    }
}
