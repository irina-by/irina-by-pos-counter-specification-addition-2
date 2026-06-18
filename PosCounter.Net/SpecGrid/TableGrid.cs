using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
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
        /// <summary>Метод координат: ExtentsTop, Location, AlignmentPoint и т.д.</summary>
        public string BoundsMethod;
        /// <summary>Точка вставки DBText (AlignmentPoint/Position) — fallback AssignCellsHeader.</summary>
        public double AlignX;
        public double AlignY;
    }

    internal sealed class ScopeGridResult
    {
        public int ScopeIndex;
        public bool Valid;
        public string GridLayer;
        public List<double> GridXs = new List<double>();
        public List<double> GridYs = new List<double>();
        public int ColMark = -1;
        public int ColDesignation = -1;
        public int ColName = -1;
        public int ColQty = -1;
        /// <summary>Нижняя граница шапки (exclusive): шапка = строки 0 .. HeaderEndRow-1.</summary>
        public int HeaderEndRow;
        /// <summary>Граница токенов шапки (exclusive), до раздувания H-линией; для BuildHeaderOnlyColumnText.</summary>
        public int HeaderTokenEndRow;
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
        /// <summary>H-линии для BindKeys (после merge/filter по слою сетки).</summary>
        public List<GridLineSeg> HorizForBind = new List<GridLineSeg>();
        /// <summary>Столбцы назначены fallback TryInferColumnsFromData.</summary>
        public bool ColumnsInferredFromData;

        /// <summary>Столбцы унаследованы от SpecColumnSchema (продолжение без шапки).</summary>
        public bool ColumnsInheritedFromSchema;
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
        /// <summary>Источник — явная AutoCAD Table (без LINE-сетки).</summary>
        public bool IsNativeAcadTable;
        public ObjectId NativeTableId = ObjectId.Null;
        /// <summary>В выборке были Table и Line — использован LINE path.</summary>
        public bool MixedTableLineSelection;
        /// <summary>Текстов без Row/Col после AssignCellsData (pass2).</summary>
        public int UnassignedTextCountAfterDataPass;
        /// <summary>Столбцы после pass1 DetectHeader (до pass2 / inference).</summary>
        public int Pass1ColMark = -1;
        public int Pass1ColName = -1;
        public int Pass1ColQty = -1;
        public bool Pass1HeaderDetectedByTopTextBand;
        /// <summary>Scores ColQty при inference (для [POSC-DIAG]).</summary>
        public string InferenceColQtyScoresSummary;
        /// <summary>Источник ColQty: grid, topBand, dbTextBand, inference, simple01, allTexts, numeric.</summary>
        public string ColQtySource = string.Empty;
        /// <summary>[POSC-DIAG] DBText в полосе GridYs шапки.</summary>
        public string DbTextHeaderBandSummary;
        /// <summary>[POSC-DIAG] причины провала ColQty fallback.</summary>
        public string ColQtyFallbackDiag;
        /// <summary>[POSC-DIAG] KeyToRowMark sample (первые/последние ключи scope).</summary>
        public string KeyToRowMarkSampleDiag;
        /// <summary>[POSC-DIAG] строки WriteQty (rowTop, col, qty из палитры).</summary>
        public List<string> WriteQtyDiagLines = new List<string>();
        /// <summary>[POSC-DIAG] ColQty layout fix (например 4→3).</summary>
        public string ColQtyLayoutFixDiag;
        /// <summary>[POSC-DIAG] диагностика имён в ColName (sample keys scope).</summary>
        public List<string> NameCol2DiagLines = new List<string>();
        /// <summary>[POSC-DIAG] тексты вне сетки, привязанные к ColName по DataX.</summary>
        public List<string> UnassignedNameFixLines = new List<string>();
        /// <summary>Источник шапки: gridTokens, topBand, dbTextBand, infer-data, …</summary>
        public string HeaderPath = string.Empty;
        /// <summary>Ключи с пустым именем после FillMarkNames.</summary>
        public List<int> EmptyNameKeys = new List<int>();
        /// <summary>Источник схемы столбцов: locked, inherited, infer.</summary>
        public string SchemaSource = string.Empty;
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

            Table nativeTable = null;
            var nativeTableCount = 0;
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

                if (ent is Table tbl)
                {
                    nativeTableCount++;
                    nativeTable = tbl;
                }
                else if (ent is Line)
                {
                    lineCount++;
                }
            }

            if (nativeTable != null && lineCount == 0)
            {
                return BuildFromAcadTable(scopeIndex, nativeTable, objectIds, tr, log);
            }

            if (nativeTableCount > 0 && lineCount > 0)
            {
                result.MixedTableLineSelection = true;
                log.Warn($"TABLE-GRID: scope={scopeIndex} Mixed selection (Table + Line), using LINE path");
            }

            var horiz = new List<GridLineSeg>();
            var vert = new List<GridLineSeg>();
            var texts = new List<TextSample>();
            Extents3d? bounds = null;
            lineCount = 0;

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

            var gridLayer = ResolveGridLayerForScope(horiz, vert, sharedGridLayer, log, scopeIndex);

            result.GridLayer = gridLayer;

            var merged = BuildMergedGridAxes(horiz, vert, gridLayer, scopeIndex, log);
            var filteredH = merged.HorizForBind;
            result.HorizForBind = filteredH;
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
            AssignCellsHeader(texts, xsArr, ysArr, log, scopeIndex);
            // Pass 1: all layers → header + layer statistics.
            result.CellText = BuildCellMatrix(texts, rows, cols, result, log, filterTableLayers: false);
            EstimateHeaderEndRow(result, filteredH, log);
            ApplyHeaderBoundaryFromGridScan(result, log);
            DetectHeader(result, log);
            result.Pass1ColMark = result.ColMark;
            result.Pass1ColName = result.ColName;
            result.Pass1ColQty = result.ColQty;
            result.Pass1HeaderDetectedByTopTextBand = result.HeaderDetectedByTopTextBand;
            ComputeRowDataStart(result, null, log);
            BuildPrimaryNameLayer(texts, result, log);
            BuildTableContentLayers(texts, result, log);
            result.MedianRowStep = EstimateRowStep(ysArr, result.RowDataStart);
            result.PrimaryNameTextHeight = ComputePrimaryNameTextHeight(texts, result);
            // Pass 2: data-координаты (точка DataX/Y); split ColName после pass-2.
            AssignCellsData(texts, xsArr, ysArr, result);
            AssignUnassignedTextsToNameColumn(texts, xsArr, ysArr, result, log);
            result.UnassignedTextCountAfterDataPass = texts.Count(t => t.Row < 0 || t.Col < 0);
            if (result.UnassignedTextCountAfterDataPass > 0)
            {
                log?.Info(
                    $"TABLE-GRID: scope={scopeIndex} AssignCellsData: {result.UnassignedTextCountAfterDataPass}/{texts.Count} texts without row/col");
            }

            SplitNameColumnRowsData(texts, ysArr, result, log);
            BuildTextsByRow(result);
            result.CellText = BuildCellMatrix(texts, rows, cols, result, log, filterTableLayers: true);
            RebindScopeKeysAndNames(result, filteredH, log, passLabel: "pass2");
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
                var cellTexts = kv.Value;
                if (result.ColName >= 0 && kv.Key.c == result.ColName)
                {
                    cellTexts = kv.Value.Where(t => t.Col == result.ColName).ToList();
                }

                var preferMark = result.ColMark >= 0 && kv.Key.c == result.ColMark;
                cells[kv.Key.r, kv.Key.c] = CellIndex.GetCellText(
                    cellTexts, log, result.ScopeIndex, kv.Key.r, kv.Key.c, preferMark);
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
        /// <summary>Верхняя полоса шапки по текстам (fallback CMD): от maxY вниз.</summary>
        private const double HeaderTopBandHeight = 2000.0;
        /// <summary>CellText достаточен — не дублировать AllTexts pass.</summary>
        private const int CellTextOnlyNameMinLength = 20;

        private static readonly string[] MarkHeaderTokens =
        {
            "марка", "марка поз", "поз", "поз.", "mark", "mark it", "п/п", "№", "номер", "item"
        };

        private static readonly string[] NameHeaderTokens =
        {
            "наимен", "name", "назван", "наименование", "list of materials", "специфика"
        };

        private static readonly string[] DesignationHeaderTokens =
        {
            "обознач", "designation", "обозначение", "гост", "тип", "type"
        };

        private const int HeaderScanMaxRows = 5;
        private const int MinContinuationPickObjects = 80;

        /// <summary>Минимум score для назначения столбца (одно совпадение токена в ScoreHeader).</summary>
        private const int MinHeaderScore = 10;
        /// <summary>Минимум score ScoreQtyHeader для simple01 (OLD-стиль строки 0–1).</summary>
        private const int SimpleRowsQtyMinScore = 20;
        /// <summary>Минимум ячеек с числом в столбце для numeric fallback ColQty.</summary>
        private const int MinNumericQtyCells = 3;
        /// <summary>Макс. длина текста подписи шапки (DBText band).</summary>
        private const int DbTextHeaderMaxPlainLen = 60;
        /// <summary>Отсев Y вне таблицы: |Y-median| &lt; factor * rowStep.</summary>
        private const double HeaderBandYMedianFactor = 8.0;
        /// <summary>Минимум уникальных цифр марки в столбце данных для подтверждения ColMark.</summary>
        private const int MinDataMarkKeysForColMark = 2;

        private static bool TryGetHeaderTopTextBandY(ScopeGridResult result, out double yLo, out double yHi)
        {
            yLo = 0;
            yHi = 0;
            if (result == null)
            {
                return false;
            }

            double anchorY;
            if (result.GridYs != null && result.GridYs.Count >= 2 && result.Valid)
            {
                var headerEnd = Math.Min(ResolveHeaderEndRow(result), result.GridYs.Count - 1);
                anchorY = Math.Max(result.GridYs[0], result.GridYs[headerEnd]);
            }
            else if (result.AllTexts != null && result.AllTexts.Count > 0)
            {
                anchorY = double.NegativeInfinity;
                foreach (var t in result.AllTexts)
                {
                    if (t == null)
                    {
                        continue;
                    }

                    if (t.Y > anchorY)
                    {
                        anchorY = t.Y;
                    }
                }

                if (double.IsNegativeInfinity(anchorY))
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            yLo = anchorY - HeaderTopBandHeight;
            yHi = anchorY + CellIndex.CellIndexEps;
            result.HeaderTopMaxY = anchorY;
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

            var headerEndRow = ResolveHeaderEndRow(result);
            foreach (var t in result.AllTexts ?? new List<TextSample>())
            {
                if (t == null || t.Y < yLo || t.Y > yHi)
                {
                    continue;
                }

                if (t.Row >= 0 && t.Row >= headerEndRow)
                {
                    continue;
                }

                if (ResolveColumnIndexByX(result.GridXs, t.X) != col)
                {
                    continue;
                }

                var plain = !string.IsNullOrWhiteSpace(t.Raw) ? t.Raw : (t.Plain ?? string.Empty);
                if (MTextPlainText.TryParseMarkKey(plain, out _))
                {
                    continue;
                }

                AppendHeaderTextPart(parts, seen, plain);
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

        private static int ResolveGridRowCount(ScopeGridResult result)
        {
            if (result?.CellText != null)
            {
                return result.CellText.GetLength(0);
            }

            return result?.GridYs?.Count ?? 0;
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

            foreach (var line in plain.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                if (seen.Add(trimmed))
                {
                    parts.Add(trimmed);
                }
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

        /// <summary>Текст строго в столбце NAME (ColName) — без bleed из Обозначения.</summary>
        private static bool IsTextInNameColumn(ScopeGridResult result, TextSample t)
        {
            if (result == null || t == null || result.ColName < 0)
            {
                return false;
            }

            if (result.ColDesignation >= 0 && t.Col == result.ColDesignation)
            {
                return false;
            }

            if (t.Col != result.ColName)
            {
                return false;
            }

            return !LooksLikeDesignationText(GetDisplayText(t));
        }

        /// <summary>Короткие коды «Обозначение» (ГОСТ, TB100) — не в наименование.</summary>
        private static bool LooksLikeDesignationText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var t = text.Trim();
            if (t.Length > 45)
            {
                return false;
            }

            var lower = t.ToLowerInvariant();
            if (lower.IndexOf("гост", StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            if (lower.StartsWith("gost", StringComparison.Ordinal))
            {
                return true;
            }

            if (t.Length <= 12 && System.Text.RegularExpressions.Regex.IsMatch(t, @"^TB\d", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return true;
            }

            if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^GOST[\d\-\.]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return true;
            }

            return false;
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

        /// <summary>Y-граница данных: верх первой строки данных (не maxY−2000).</summary>
        private static double ResolveDataYCutoff(ScopeGridResult result)
        {
            if (result?.GridYs == null || result.GridYs.Count < 2)
            {
                return double.NegativeInfinity;
            }

            if (result.RowDataStart > 0 && result.RowDataStart < result.GridYs.Count)
            {
                return result.GridYs[result.RowDataStart];
            }

            var headerEnd = ResolveHeaderEndRow(result);
            if (headerEnd > 0 && headerEnd < result.GridYs.Count)
            {
                return result.GridYs[headerEnd];
            }

            return double.NegativeInfinity;
        }

        /// <summary>Текст в зоне данных (не шапка): по RowDataStart, иначе по Y-cutoff сетки.</summary>
        private static bool IsBindableDataText(ScopeGridResult result, TextSample t)
        {
            if (result == null || t == null)
            {
                return false;
            }

            if (t.Row < 0)
            {
                return false;
            }

            if (IsSectionHeaderRow(result, t.Row))
            {
                return false;
            }

            if (result.RowDataStart > 0)
            {
                return t.Row >= result.RowDataStart;
            }

            var yCutoff = ResolveDataYCutoff(result);
            if (double.IsNegativeInfinity(yCutoff))
            {
                return true;
            }

            return t.DataY < yCutoff;
        }

        /// <summary>
        /// Уникальные марки, которые реально сможет привязать BindKeys для столбца col
        /// (Row≥0, не секционная строка, X-полоса столбца, строка данных).
        /// </summary>
        private static int CountDataMarkKeysInColumn(ScopeGridResult result, int col)
        {
            if (result == null || col < 0)
            {
                return 0;
            }

            if (result.IsNativeAcadTable)
            {
                return CountDataMarkKeysInColumnFromCellText(result, col);
            }

            if (result.GridXs == null || result.GridXs.Count < 2)
            {
                return 0;
            }

            var keys = new HashSet<int>();

            foreach (var t in result.AllTexts ?? new List<TextSample>())
            {
                if (t == null || !IsBindableDataText(result, t))
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

            AddMarkKeysFromCellText(result, col, keys);
            return keys.Count;
        }

        private static int CountDataMarkKeysInColumnFromCellText(ScopeGridResult result, int col)
        {
            if (result?.CellText == null || col < 0 || col >= result.CellText.GetLength(1))
            {
                return 0;
            }

            var keys = new HashSet<int>();
            AddMarkKeysFromCellText(result, col, keys);
            return keys.Count;
        }

        private static void AddMarkKeysFromCellText(ScopeGridResult result, int col, HashSet<int> keys)
        {
            if (result?.CellText == null || col < 0 || col >= result.CellText.GetLength(1))
            {
                return;
            }

            var rows = result.CellText.GetLength(0);
            var dataStart = result.RowDataStart > 0 ? result.RowDataStart : ResolveHeaderEndRow(result);
            for (var r = dataStart; r < rows; r++)
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
            if (scope?.CellText == null)
            {
                return string.Empty;
            }

            var cols = scope.CellText.GetLength(1);
            if (cols <= 0)
            {
                if (scope.GridXs == null || scope.GridXs.Count < 2)
                {
                    return string.Empty;
                }

                cols = scope.GridXs.Count - 1;
            }

            var parts = new List<string>(cols);
            for (var c = 0; c < cols; c++)
            {
                parts.Add($"col{c}={CountDataMarkKeysInColumn(scope, c)}");
            }

            return string.Join(", ", parts);
        }

        /// <summary>Текст заголовка столбца только из строк шапки (не данные).</summary>
        internal static string BuildHeaderOnlyColumnText(ScopeGridResult result, int col)
        {
            if (result?.CellText == null || col < 0 || col >= result.CellText.GetLength(1))
            {
                return string.Empty;
            }

            var parts = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var headerEnd = ResolveHeaderOnlyEndRow(result);
            var rows = result.CellText.GetLength(0);
            for (var r = 0; r < headerEnd && r < rows; r++)
            {
                var cell = (result.CellText[r, col] ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(cell))
                {
                    continue;
                }

                if (result.ColumnsInferredFromData
                    && (CellLooksLikeNameData(cell) || MTextPlainText.NameScore(cell) >= 4)
                    && ScoreQtyHeader(MTextPlainText.SanitizeRawContents(cell).ToLowerInvariant()) < MinHeaderScore
                    && ScoreHeader(
                        MTextPlainText.SanitizeRawContents(cell).ToLowerInvariant(),
                        "марка", "поз", "поз.", "наимен", "кол", "name", "qty") < MinHeaderScore)
                {
                    continue;
                }

                AppendHeaderTextPart(parts, seen, cell);
            }

            return parts.Count > 0 ? string.Join(" ", parts) : string.Empty;
        }

        /// <summary>Верхняя граница текста шапки (exclusive): токены и/или первая цифра ColMark.</summary>
        private static int ResolveHeaderOnlyEndRow(ScopeGridResult result)
        {
            if (result == null)
            {
                return HeaderScanMaxRows + 1;
            }

            var end = result.HeaderTokenEndRow > 0
                ? result.HeaderTokenEndRow
                : (result.RowDataStart > 0 ? result.RowDataStart : ResolveHeaderEndRow(result));
            var firstMarkRow = FindFirstMarkRowInColMark(result, 0);
            if (firstMarkRow >= 0)
            {
                end = Math.Min(end, firstMarkRow);
            }

            return Math.Max(1, end);
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

        internal static string FormatMissingKeyOneDiagnostic(ScopeGridResult scope)
        {
            if (scope?.CellText == null || scope.ColMark < 0)
            {
                return string.Empty;
            }

            var rows = scope.CellText.GetLength(0);
            var scanFrom = scope.RowDataStart > 0 ? scope.RowDataStart : 0;
            var scanTo = Math.Min(rows, scanFrom + 4);
            for (var r = scanFrom; r < scanTo; r++)
            {
                var cell = scope.CellText[r, scope.ColMark] ?? string.Empty;
                if (MTextPlainText.TryParseMarkKey(cell, out var key)
                    && key >= 1
                    && !scope.KeyToRowMark.ContainsKey(key))
                {
                    return $"[POSC] Марка {key} в CellText row={r}, но KeyToRowMark не содержит key={key} (RowDataStart={scope.RowDataStart})";
                }
            }

            return string.Empty;
        }

        private static bool HeaderColumnsSufficientForNames(ScopeGridResult result) =>
            result != null && result.ColMark >= 0 && result.ColName >= 0;

        /// <summary>Зафиксировать эталон столбцов после таблицы 1 с реальной шапкой (pass1 или pass2).</summary>
        internal static bool TryLockColumnSchema(ScopeGridResult scope, SpecGridLog log)
        {
            if (scope == null || !scope.Valid || scope.ScopeIndex != 0)
            {
                return false;
            }

            if (scope.Pass1ColMark < 0 || scope.Pass1ColName < 0)
            {
                if (!CanLockColumnSchemaFromPass2(scope))
                {
                    log?.Info(
                        $"TABLE-GRID: scope=0 schema lock skip pass1 Mark={scope.Pass1ColMark} Name={scope.Pass1ColName} pass2 Mark={scope.ColMark} Name={scope.ColName}");
                    return false;
                }

                log?.Info(
                    $"TABLE-GRID: scope=0 schema lock via pass2 Mark={scope.ColMark} Name={scope.ColName} Qty={scope.ColQty} src={scope.ColQtySource}");
            }

            ApplyStandardColumnLayout(scope, log);
            if (scope.ColQty < 0 && scope.ColMark >= 0 && scope.ColName >= 0)
            {
                TryResolveMissingColQty(scope, log);
            }

            ApplyStandardColumnLayout(scope, log);

            if (scope.ColMark < 0 || scope.ColName < 0 || scope.ColQty < 0)
            {
                return false;
            }

            var schema = new SpecColumnSchema
            {
                IsLocked = true,
                ColMark = scope.ColMark,
                ColName = scope.ColName,
                ColQty = scope.ColQty,
                ColDesignation = scope.ColDesignation,
                HeaderEndRow = scope.HeaderEndRow > 0 ? scope.HeaderEndRow : scope.RowDataStart,
                AnchorGridXs = scope.GridXs?.ToList() ?? new List<double>(),
                AnchorObjectCount = scope.PickedObjectIds?.Count ?? scope.LineCount
            };
            scope.SchemaSource = "locked";
            SpecGridSession.ColumnSchema = schema;
            SpecGridLog.WriteTrace(
                "SCHEMA",
                $"locked scope=0 Mark={schema.ColMark} Name={schema.ColName} Qty={schema.ColQty} Designation={schema.ColDesignation} headerEnd={schema.HeaderEndRow}");
            log?.Info(
                $"TABLE-GRID: scope=0 column schema locked MARK={schema.ColMark} NAME={schema.ColName} QTY={schema.ColQty}");
            return true;
        }

        private static bool CanLockColumnSchemaFromPass2(ScopeGridResult scope)
        {
            if (scope == null || scope.ColMark < 0 || scope.ColName < 0 || scope.ColQty < 0)
            {
                return false;
            }

            if (scope.ColumnsInferredFromData
                && scope.ColMark >= 0
                && IsSpuriousDigitOnlyMarkHeader(BuildHeaderOnlyColumnText(scope, scope.ColMark), scope, scope.ColMark))
            {
                return false;
            }

            var src = scope.ColQtySource ?? string.Empty;
            return string.Equals(src, "grid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(src, "dbTextBand", StringComparison.OrdinalIgnoreCase)
                || string.Equals(src, "layout", StringComparison.OrdinalIgnoreCase)
                || string.Equals(src, "topBand", StringComparison.OrdinalIgnoreCase)
                || string.Equals(src, "simple01", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsContinuationPickTooSmall(ScopeGridResult scope)
        {
            if (scope == null)
            {
                return true;
            }

            var objCount = scope.PickedObjectIds?.Count ?? scope.LineCount;
            var anchorObj = SpecGridSession.ColumnSchema?.AnchorObjectCount ?? 0;
            var minObjects = anchorObj > 0
                ? Math.Max(30, (int)(anchorObj * 0.25))
                : MinContinuationPickObjects;
            return objCount < minObjects
                || scope.TextCount < 30
                || (scope.TextCount > 0 && scope.UnassignedTextCountAfterDataPass * 2 > scope.TextCount);
        }

        private const double SchemaAlignToleranceMm = 50.0;

        /// <summary>Сопоставить столбцы продолжения с эталоном по центрам X-полос (со сдвигом левого края сетки).</summary>
        internal static bool TryAlignScopeColumnsToAnchorSchema(
            ScopeGridResult scope,
            SpecColumnSchema schema,
            out int alignedMark,
            out int alignedDesignation,
            out int alignedName,
            out int alignedQty,
            out double maxDx,
            out string failReason)
        {
            alignedMark = -1;
            alignedDesignation = -1;
            alignedName = -1;
            alignedQty = -1;
            maxDx = 0;
            failReason = "invalid";

            if (scope == null || schema == null || !scope.Valid || scope.GridXs == null || schema.AnchorGridXs == null)
            {
                return false;
            }

            var anchor = schema.AnchorGridXs;
            var scopeXs = scope.GridXs;
            if (anchor.Count < 2 || scopeXs.Count < 2)
            {
                return false;
            }

            var gridCols = scopeXs.Count - 1;
            if (gridCols <= schema.ColQty || gridCols <= schema.ColName || gridCols <= schema.ColMark)
            {
                failReason = "gridCols";
                return false;
            }

            var offset = scopeXs[0] - anchor[0];
            var tolerance = Math.Max(EpsAxis * 20, SchemaAlignToleranceMm);

            int AlignOne(int anchorColIndex, out double dx)
            {
                dx = double.MaxValue;
                if (anchorColIndex < 0 || anchorColIndex + 1 >= anchor.Count)
                {
                    return -1;
                }

                var targetCenter = (anchor[anchorColIndex] + anchor[anchorColIndex + 1]) * 0.5 + offset;
                var bestCol = -1;
                var bestDx = double.MaxValue;
                for (var c = 0; c < gridCols; c++)
                {
                    var scopeCenter = (scopeXs[c] + scopeXs[c + 1]) * 0.5;
                    var d = Math.Abs(scopeCenter - targetCenter);
                    if (d < bestDx)
                    {
                        bestDx = d;
                        bestCol = c;
                    }
                }

                dx = bestDx;
                return bestDx <= tolerance ? bestCol : -1;
            }

            alignedMark = AlignOne(schema.ColMark, out var dxMark);
            alignedDesignation = AlignOne(schema.ColDesignation, out var dxDesignation);
            alignedName = AlignOne(schema.ColName, out var dxName);
            alignedQty = AlignOne(schema.ColQty, out var dxQty);

            maxDx = new[] { dxMark, dxDesignation, dxName, dxQty }
                .Where(d => d < double.MaxValue)
                .DefaultIfEmpty(0)
                .Max();

            if (alignedMark < 0 || alignedName < 0 || alignedQty < 0)
            {
                failReason = "X-mismatch";
                return false;
            }

            if (schema.ColDesignation >= 0 && alignedDesignation < 0)
            {
                failReason = "X-mismatch";
                return false;
            }

            var required = new List<int> { alignedMark, alignedName, alignedQty };
            if (schema.ColDesignation >= 0)
            {
                required.Add(alignedDesignation);
            }

            if (required.Distinct().Count() != required.Count)
            {
                failReason = "X-mismatch";
                return false;
            }

            failReason = null;
            return true;
        }

        /// <summary>Применить эталон столбцов к таблице-продолжению (без поиска шапки).</summary>
        internal static bool TryApplyInheritedColumnSchema(
            ScopeGridResult scope,
            SpecColumnSchema schema,
            SpecGridLog log,
            out string failReason)
        {
            failReason = "invalid";
            if (scope == null || schema == null || !schema.IsLocked || !scope.Valid)
            {
                return false;
            }

            if (scope.TextCount > 0 && scope.UnassignedTextCountAfterDataPass * 2 > scope.TextCount)
            {
                failReason = "texts-outside";
                log?.Warn(
                    $"TABLE-GRID: scope={scope.ScopeIndex} inherited schema skip texts-outside {scope.UnassignedTextCountAfterDataPass}/{scope.TextCount}");
                return false;
            }

            if (!TryAlignScopeColumnsToAnchorSchema(
                    scope,
                    schema,
                    out var alignedMark,
                    out var alignedDesignation,
                    out var alignedName,
                    out var alignedQty,
                    out var maxDx,
                    out failReason))
            {
                log?.Warn(
                    $"TABLE-GRID: scope={scope.ScopeIndex} inherited schema align fail reason={failReason} maxDx={maxDx:F1}");
                return false;
            }

            var sameIndices = alignedMark == schema.ColMark
                && alignedName == schema.ColName
                && alignedQty == schema.ColQty
                && (schema.ColDesignation < 0 || alignedDesignation == schema.ColDesignation);

            scope.ColMark = alignedMark;
            scope.ColName = alignedName;
            scope.ColQty = alignedQty;
            scope.ColDesignation = schema.ColDesignation >= 0 ? alignedDesignation : schema.ColDesignation;
            scope.ColumnsInheritedFromSchema = true;
            scope.ColumnsInferredFromData = false;
            scope.SchemaSource = "inherited";
            scope.ColQtySource = "inherited";
            if (schema.HeaderEndRow > 0 && scope.GridYs != null && scope.GridYs.Count > 0)
            {
                scope.HeaderEndRow = Math.Min(schema.HeaderEndRow, scope.GridYs.Count - 1);
            }

            var traceVerb = sameIndices ? "inherited" : "aligned";
            SpecGridLog.WriteTrace(
                "SCHEMA",
                $"{traceVerb} scope={scope.ScopeIndex} Mark={scope.ColMark} Name={scope.ColName} Qty={scope.ColQty} dx={maxDx:F1}");
            log?.Info(
                $"TABLE-GRID: scope={scope.ScopeIndex} {traceVerb} column schema MARK={scope.ColMark} NAME={scope.ColName} QTY={scope.ColQty} dx={maxDx:F1}");
            failReason = null;
            return true;
        }

        internal static void RebindScopeKeysAndNames(
            ScopeGridResult result,
            List<GridLineSeg> horiz,
            SpecGridLog log,
            string passLabel)
        {
            if (result == null)
            {
                return;
            }

            log?.Info($"TABLE-GRID: scope={result.ScopeIndex} rebind keys/names ({passLabel})");
            if (!result.ColumnsInferredFromData && !result.ColumnsInheritedFromSchema)
            {
                ApplyHeaderBoundaryFromGridScan(result, log);
                DetectHeader(result, log);
            }
            else
            {
                var reason = result.ColumnsInheritedFromSchema ? "ColumnsInheritedFromSchema" : "ColumnsInferredFromData";
                log?.Info(
                    $"TABLE-GRID: scope={result.ScopeIndex} skip DetectHeader ({reason}, ColMark={result.ColMark} ColName={result.ColName})");
                if (result.RowDataStart <= 0)
                {
                    ApplyHeaderBoundaryFromGridScan(result, log);
                }
            }

            ComputeRowDataStart(result, horiz, log);
            BindKeysFromProperties(result, log);
            BindKeys(result, horiz, log);
            AlignRowDataStartToFirstMark(result, log);
            if (result.RowDataStart > 0)
            {
                result.HeaderEndRow = result.HeaderEndRow > 0
                    ? Math.Min(result.HeaderEndRow, result.RowDataStart)
                    : result.RowDataStart;
            }

            LogHeaderDataRowRebindSummary(result, horiz);

            ApplyStandardColumnLayout(result, log);
            BuildKeyToRowMarkSampleDiag(result);

            if (result.ColQty < 0 && result.ColMark >= 0 && result.ColName >= 0)
            {
                TryResolveMissingColQty(result, log);
            }

            ApplyStandardColumnLayout(result, log);

            var unassignedFixed = AssignUnassignedTextsToNameColumn(
                result.AllTexts,
                result.GridXs?.ToArray() ?? ArrayCompat.Empty<double>(),
                result.GridYs?.ToArray() ?? ArrayCompat.Empty<double>(),
                result,
                log);
            if (unassignedFixed > 0 && result.GridXs != null && result.GridYs != null
                && result.GridXs.Count >= 2 && result.GridYs.Count >= 2)
            {
                var rows = result.GridYs.Count - 1;
                var cols = result.GridXs.Count - 1;
                result.CellText = BuildCellMatrix(result.AllTexts, rows, cols, result, log, filterTableLayers: true);
                result.UnassignedTextCountAfterDataPass = result.AllTexts.Count(t => t.Row < 0 || t.Col < 0);
            }
        }

        /// <summary>ColDesignation + предпочтение ColQty = ColName+1.</summary>
        internal static void ApplyStandardColumnLayout(ScopeGridResult result, SpecGridLog log)
        {
            if (result == null)
            {
                return;
            }

            SpecGridLog.WriteTrace(
                "COLQTY",
                $"scope={result.ScopeIndex} ApplyStandardColumnLayout enter ColMark={result.ColMark} ColName={result.ColName} ColQty={result.ColQty}");

            if (result.ColMark >= 0 && result.ColName == result.ColMark + 2)
            {
                result.ColDesignation = result.ColMark + 1;
            }
            else if (result.ColDesignation < 0)
            {
                result.ColDesignation = DetectDesignationColumn(result);
            }

            TryPreferQtyColumnAfterName(result, log);
        }

        private static int DetectDesignationColumn(ScopeGridResult result)
        {
            if (result?.CellText == null || result.ColMark < 0)
            {
                return -1;
            }

            var cols = result.CellText.GetLength(1);
            var headerEnd = Math.Max(ResolveHeaderEndRow(result), 1);
            for (var c = 0; c < cols; c++)
            {
                if (c == result.ColMark || c == result.ColName || c == result.ColQty)
                {
                    continue;
                }

                for (var r = 0; r < headerEnd && r < result.CellText.GetLength(0); r++)
                {
                    var header = MTextPlainText.SanitizeRawContents(result.CellText[r, c] ?? string.Empty).ToLowerInvariant();
                    if (header.IndexOf("обознач", StringComparison.Ordinal) >= 0)
                    {
                        return c;
                    }
                }
            }

            return result.ColName == result.ColMark + 2 ? result.ColMark + 1 : -1;
        }

        private static void TryPreferQtyColumnAfterName(ScopeGridResult result, SpecGridLog log)
        {
            if (result.ColName < 0 || result.GridXs == null || result.ColName + 1 >= result.GridXs.Count - 1)
            {
                SpecGridLog.WriteTrace(
                    "COLQTY",
                    $"scope={result.ScopeIndex} TryPrefer skip: ColName={result.ColName} gridCols={result.GridXs?.Count ?? 0}");
                return;
            }

            var expected = result.ColName + 1;
            var isStandardSchema = result.ColMark >= 0 && result.ColName == result.ColMark + 2;
            SpecGridLog.WriteTrace(
                "COLQTY",
                $"scope={result.ScopeIndex} TryPrefer ColMark={result.ColMark} ColName={result.ColName} ColQty={result.ColQty} expected={expected} standard={isStandardSchema}");

            if (result.ColQty == expected)
            {
                SpecGridLog.WriteTrace("COLQTY", $"scope={result.ScopeIndex} TryPrefer ok ColQty={expected}");
                return;
            }

            var expectedQty = CountQtyLikeInColumn(result, expected);
            if (result.ColQty >= 0 && result.ColQty != expected)
            {
                var fix = HeaderTextLooksLikeMassColumn(result, result.ColQty)
                    || ColumnLooksLikeMassData(result, result.ColQty)
                    || string.Equals(result.ColQtySource, "numeric", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(result.ColQtySource, "inference", StringComparison.OrdinalIgnoreCase);
                if (fix)
                {
                    SetColQtyLayoutFix(result, result.ColQty, expected, log, "mass-column");
                }
            }
            else if (result.ColQty < 0 && expectedQty >= MinNumericQtyCells)
            {
                SetColQtyLayoutFix(result, -1, expected, log, "numeric-evidence");
            }
            else if (result.ColQty < 0 && isStandardSchema && expectedQty > 0)
            {
                SetColQtyLayoutFix(result, -1, expected, log, "layout-evidence");
            }
        }

        private static void SetColQtyLayoutFix(ScopeGridResult result, int prevCol, int expected, SpecGridLog log, string reason)
        {
            var diag = prevCol >= 0 ? $"{prevCol}→{expected}" : $"→{expected}";
            result.ColQtyLayoutFixDiag = diag;
            result.ColQty = expected;
            result.ColQtySource = "layout-evidence";
            SpecGridLog.WriteTrace(
                "COLQTY",
                $"scope={result.ScopeIndex} layout fix {diag} reason={reason} ColMark={result.ColMark} ColName={result.ColName}");
            log?.Info($"TABLE-GRID: scope={result.ScopeIndex} ColQty layout fix: {diag} ({reason})");
        }

        internal static void BuildKeyToRowMarkSampleDiag(ScopeGridResult result)
        {
            result.KeyToRowMarkSampleDiag = SpecDiagPolicy.FormatKeyToRowMarkSample(result);
        }

        /// <summary>ColQty: simple01 → allTexts → numeric (AC 2016, когда шапка не в CellText).</summary>
        internal static void TryResolveMissingColQty(ScopeGridResult result, SpecGridLog log)
        {
            if (result == null || result.ColQty >= 0 || !result.Valid)
            {
                return;
            }

            SpecGridLog.WriteTrace("COLQTY", $"scope={result.ScopeIndex} TryResolveMissingColQty enter");

            var simpleOk = DetectHeaderSimpleRows01(result, log, onlyQty: true);
            if (result.ColQty >= 0)
            {
                SpecGridLog.WriteTrace("COLQTY", $"scope={result.ScopeIndex} resolved simple01 col={result.ColQty}");
                return;
            }

            var allTextsOk = DetectColQtyFromAllTexts(result, log);
            if (result.ColQty >= 0)
            {
                SpecGridLog.WriteTrace("COLQTY", $"scope={result.ScopeIndex} resolved allTexts col={result.ColQty}");
                return;
            }

            TryInferColQtyFromNumericColumn(result, log);
            if (result.ColQty >= 0)
            {
                SpecGridLog.WriteTrace(
                    "COLQTY",
                    $"scope={result.ScopeIndex} resolved numeric col={result.ColQty} src={result.ColQtySource}");
            }

            ApplyStandardColumnLayout(result, log);
            if (result.ColQty < 0)
            {
                result.ColQtyFallbackDiag = BuildColQtyFallbackDiagnostic(result, simpleOk, allTextsOk);
                SpecGridLog.WriteTrace("COLQTY", $"scope={result.ScopeIndex} fallback failed: {result.ColQtyFallbackDiag}");
                log?.Info($"TABLE-GRID: scope={result.ScopeIndex} ColQty fallback failed: {result.ColQtyFallbackDiag}");
            }
        }

        /// <summary>OLD-стиль: шапка только из строк 0–1 CellText.</summary>
        private static bool DetectHeaderSimpleRows01(ScopeGridResult result, SpecGridLog log, bool onlyQty)
        {
            if (result?.CellText == null)
            {
                return false;
            }

            var cols = result.CellText.GetLength(1);
            var rows = result.CellText.GetLength(0);
            if (cols <= 0 || rows <= 0)
            {
                return false;
            }

            var savedMark = result.ColMark;
            var savedName = result.ColName;
            var bestMark = -1;
            var bestName = -1;
            var bestQty = -1;
            var markScore = -1;
            var nameScore = -1;
            var qtyScore = -1;

            for (var c = 0; c < cols; c++)
            {
                var header = string.Empty;
                for (var r = 0; r <= 1 && r < rows; r++)
                {
                    var cell = result.CellText[r, c] ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(cell))
                    {
                        header = (header + " " + cell).Trim();
                    }
                }

                header = MTextPlainText.SanitizeRawContents(header).ToLowerInvariant();
                var ms = ScoreHeader(header, "марка", "поз", "поз.", "mark", "п/п", "№", "номер", "item");
                var ns = ScoreHeader(header, "наимен", "name", "назван", "наименование");
                var qs = ScoreQtyHeader(header);
                if (!onlyQty && ms > markScore)
                {
                    markScore = ms;
                    bestMark = c;
                }

                if (!onlyQty && ns > nameScore)
                {
                    nameScore = ns;
                    bestName = c;
                }

                if (qs > qtyScore)
                {
                    qtyScore = qs;
                    bestQty = c;
                }
            }

            var changed = false;
            if (!onlyQty && result.ColMark < 0 && bestMark >= 0 && markScore >= MinHeaderScore)
            {
                result.ColMark = bestMark;
                changed = true;
            }

            if (!onlyQty && result.ColName < 0 && bestName >= 0 && nameScore >= MinHeaderScore)
            {
                result.ColName = bestName;
                changed = true;
            }

            if (result.ColQty < 0 && bestQty >= 0 && qtyScore >= SimpleRowsQtyMinScore)
            {
                result.ColQty = bestQty;
                result.ColQtySource = "simple01";
                changed = true;
                log?.Info(
                    $"TABLE-GRID: scope={result.ScopeIndex} ColQty simple01 rows0-1: col={bestQty} score={qtyScore}");
            }

            if (onlyQty)
            {
                result.ColMark = savedMark;
                result.ColName = savedName;
            }

            return result.ColQty >= 0 || changed;
        }

        /// <summary>Поиск «Кол.» в AllTexts в зоне шапки (вне CellText).</summary>
        private static bool DetectColQtyFromAllTexts(ScopeGridResult result, SpecGridLog log)
        {
            if (result?.AllTexts == null || result.AllTexts.Count == 0 || result.GridXs == null || result.GridXs.Count < 2)
            {
                return false;
            }

            if (result.ColQty >= 0)
            {
                return true;
            }

            var cols = result.GridXs.Count - 1;
            var qtyScores = new int[cols];
            var headerEndRow = ResolveHeaderEndRow(result);
            var bandUsed = TryGetHeaderBandY(result, out var yLo, out var yHi)
                || TryGetHeaderRegionY(result, out yLo, out yHi);

            foreach (var t in result.AllTexts)
            {
                if (t == null)
                {
                    continue;
                }

                var textY = t.HeaderY;
                if (!IsTextYPlausibleForHeaderBand(result, textY))
                {
                    textY = t.AlignY;
                    if (!IsTextYPlausibleForHeaderBand(result, textY))
                    {
                        continue;
                    }
                }

                var inHeaderRow = t.Row >= 0 && t.Row < headerEndRow;
                var inHeaderY = bandUsed && textY >= yLo && textY <= yHi;
                if (!inHeaderRow && !inHeaderY)
                {
                    continue;
                }

                if (t.Row >= 0 && t.Row >= headerEndRow && headerEndRow > 0)
                {
                    continue;
                }

                var plain = MTextPlainText.SanitizeRawContents(
                    !string.IsNullOrWhiteSpace(t.Raw) ? t.Raw : (t.Plain ?? string.Empty)).ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(plain))
                {
                    continue;
                }

                if (MTextPlainText.TryParseMarkKey(plain, out _))
                {
                    continue;
                }

                var score = ScoreQtyHeader(plain);
                if (score < MinHeaderScore)
                {
                    continue;
                }

                var col = ResolveColumnIndexByX(result.GridXs, t.HeaderX);
                if (col < 0)
                {
                    col = ResolveColumnIndexByX(result.GridXs, t.AlignX);
                }

                if (col < 0)
                {
                    col = ResolveColumnIndexByX(result.GridXs, t.X);
                }

                if (col < 0 || col >= cols)
                {
                    continue;
                }

                if (col == result.ColMark || col == result.ColName)
                {
                    continue;
                }

                qtyScores[col] += score;
            }

            var bestCol = -1;
            var bestScore = -1;
            for (var c = 0; c < cols; c++)
            {
                if (c == result.ColMark || c == result.ColName)
                {
                    continue;
                }

                if (qtyScores[c] > bestScore)
                {
                    bestScore = qtyScores[c];
                    bestCol = c;
                }
            }

            if (bestCol < 0 || bestScore < MinHeaderScore)
            {
                return false;
            }

            result.ColQty = bestCol;
            result.ColQtySource = "allTexts";
            log?.Info(
                $"TABLE-GRID: scope={result.ScopeIndex} ColQty allTexts: col={bestCol} score={bestScore} band={bandUsed}");
            return true;
        }

        /// <summary>Столбец «Кол.» по числам в данных (когда заголовок не читается на AC 2016).</summary>
        private static bool TryInferColQtyFromNumericColumn(ScopeGridResult result, SpecGridLog log)
        {
            if (result?.CellText == null || result.ColQty >= 0)
            {
                return false;
            }

            var cols = result.CellText.GetLength(1);
            var rows = result.CellText.GetLength(0);
            var dataStart = result.RowDataStart > 0
                ? result.RowDataStart
                : Math.Max(ResolveHeaderEndRow(result), 0);

            var bestCol = -1;
            var bestScore = 0;
            var bestQtyCount = 0;
            var expectedQtyCol = result.ColName >= 0 ? result.ColName + 1 : -1;
            var isStandardSchema = result.ColMark >= 0 && result.ColName == result.ColMark + 2;
            if (isStandardSchema
                && expectedQtyCol >= 0
                && expectedQtyCol < cols
                && CountQtyLikeInColumn(result, expectedQtyCol) >= MinNumericQtyCells)
            {
                result.ColQty = expectedQtyCol;
                result.ColQtySource = "numeric";
                log?.Info(
                    $"TABLE-GRID: scope={result.ScopeIndex} ColQty numeric prefer col3 (standard 0/1/2/3): col={expectedQtyCol}");
                return true;
            }

            for (var c = 0; c < cols; c++)
            {
                if (c == result.ColMark || c == result.ColName)
                {
                    continue;
                }

                if (result.ColDesignation >= 0 && c == result.ColDesignation)
                {
                    continue;
                }

                if (CountDataMarkKeysInColumn(result, c) >= MinDataMarkKeysForColMark)
                {
                    continue;
                }

                if (HeaderTextLooksLikeMassColumn(result, c) || ColumnLooksLikeMassData(result, c))
                {
                    continue;
                }

                var qtyCellCount = 0;
                var qtyTextCount = CountQtyValuesInColumnTexts(result, c);
                var nameCount = 0;
                var markCount = 0;
                var nonEmpty = 0;
                for (var r = dataStart; r < rows; r++)
                {
                    if (IsSectionHeaderRow(result, r))
                    {
                        continue;
                    }

                    var cell = (result.CellText[r, c] ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(cell))
                    {
                        continue;
                    }

                    nonEmpty++;
                    if (CellLooksLikeIntegerQtyValue(cell))
                    {
                        qtyCellCount++;
                    }
                    else if (MTextPlainText.TryParseMarkKey(cell, out _))
                    {
                        markCount++;
                    }
                    else if (CellLooksLikeNameData(cell) || MTextPlainText.NameScore(cell) >= 4)
                    {
                        nameCount++;
                    }
                }

                var qtyCount = Math.Max(qtyCellCount, qtyTextCount);
                if (qtyCount < MinNumericQtyCells)
                {
                    continue;
                }

                if (nameCount > qtyCount / 2)
                {
                    continue;
                }

                if (markCount > 2)
                {
                    continue;
                }

                var score = qtyCount * 100 + (nonEmpty > 0 ? qtyCount * 100 / nonEmpty : 0);
                if (c == expectedQtyCol)
                {
                    score += 50000;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCol = c;
                    bestQtyCount = qtyCount;
                }
            }

            if (bestCol < 0)
            {
                return false;
            }

            result.ColQty = bestCol;
            result.ColQtySource = "numeric";
            log?.Info(
                $"TABLE-GRID: scope={result.ScopeIndex} ColQty numeric fallback: col={bestCol} qtyCount={bestQtyCount} score={bestScore}");
            return true;
        }

        private static int CountQtyValuesInColumnTexts(ScopeGridResult result, int col)
        {
            if (result?.AllTexts == null || col < 0)
            {
                return 0;
            }

            var count = 0;
            foreach (var t in result.AllTexts)
            {
                if (t == null || !IsBindableDataText(result, t) || !IsTextInColumnXBand(result, col, t))
                {
                    continue;
                }

                var plain = MTextPlainText.SanitizeRawContents(t.Plain ?? t.Raw ?? string.Empty).Trim();
                if (CellLooksLikeIntegerQtyValue(plain))
                {
                    count++;
                }
            }

            return count;
        }

        internal static string BuildColQtyFallbackDiagnostic(ScopeGridResult result, bool simpleOk, bool allTextsOk)
        {
            if (result?.CellText == null)
            {
                return "ColQty fallback: нет CellText";
            }

            var cols = result.CellText.GetLength(1);
            var parts = new List<string>
            {
                $"simple01={(simpleOk ? "да" : "нет")}",
                $"allTexts={(allTextsOk ? "да" : "нет")}",
                "numeric:"
            };

            var dataStart = result.RowDataStart > 0
                ? result.RowDataStart
                : Math.Max(ResolveHeaderEndRow(result), 0);
            var rows = result.CellText.GetLength(0);
            for (var c = 0; c < cols; c++)
            {
                if (c == result.ColMark || c == result.ColName)
                {
                    continue;
                }

                var qtyCell = 0;
                for (var r = dataStart; r < rows; r++)
                {
                    if (IsSectionHeaderRow(result, r))
                    {
                        continue;
                    }

                    var cell = (result.CellText[r, c] ?? string.Empty).Trim();
                    if (CellLooksLikeQtyValue(cell))
                    {
                        qtyCell++;
                    }
                }

                var qtyText = CountQtyValuesInColumnTexts(result, c);
                parts.Add($"col{c} qty={Math.Max(qtyCell, qtyText)} cell={qtyCell} text={qtyText}");
            }

            return string.Join(" ", parts);
        }

        /// <summary>Зона шапки: GridYs (расширенная) или top-band 2000 мм.</summary>
        private static bool TryGetHeaderRegionY(ScopeGridResult result, out double yLo, out double yHi)
        {
            yLo = 0;
            yHi = 0;
            if (result?.GridYs != null && result.GridYs.Count >= 2)
            {
                var headerEnd = ResolveHeaderEndRow(result);
                if (headerEnd > 0 && headerEnd < result.GridYs.Count)
                {
                    yHi = result.GridYs[0] + CellIndex.CellIndexEps;
                    yLo = result.GridYs[Math.Min(headerEnd, result.GridYs.Count - 1)] - CellIndex.CellIndexEps;
                    result.HeaderTopBandLo = yLo;
                    result.HeaderTopBandHi = yHi;
                    return true;
                }
            }

            return TryGetHeaderTopTextBandY(result, out yLo, out yHi);
        }

        /// <summary>Fallback: ColMark/ColName по данным ячеек и пересечению с палитрой.</summary>
        internal static bool TryInferColumnsFromData(
            ScopeGridResult result,
            IReadOnlyDictionary<int, int> paletteKeys,
            SpecGridLog log)
        {
            if (result?.CellText == null || !result.Valid || result.GridXs == null || result.GridXs.Count < 2)
            {
                return false;
            }

            if (HeaderColumnsSufficientForNames(result))
            {
                return false;
            }

            var cols = result.CellText.GetLength(1);
            if (cols <= 0)
            {
                return false;
            }

            var dataStart = result.RowDataStart > 0
                ? result.RowDataStart
                : Math.Max(ResolveHeaderEndRow(result), 0);

            var bestMarkCol = -1;
            var bestMarkScore = -1;
            for (var c = 0; c < cols; c++)
            {
                var bindable = CountDataMarkKeysInColumn(result, c);
                var overlap = CountPaletteKeyOverlapInColumn(result, c, paletteKeys);
                var score = overlap * 1000 + bindable;
                if (score <= bestMarkScore)
                {
                    continue;
                }

                if (bindable < MinDataMarkKeysForColMark && overlap < 1)
                {
                    continue;
                }

                bestMarkScore = score;
                bestMarkCol = c;
            }

            if (bestMarkCol < 0)
            {
                log?.Info($"TABLE-GRID: scope={result.ScopeIndex} infer columns: no ColMark candidate");
                return false;
            }

            var bestNameCol = -1;
            var bestNameScore = 0;
            for (var c = 0; c < cols; c++)
            {
                if (c == bestMarkCol)
                {
                    continue;
                }

                var nameScore = SumNameScoreInDataColumn(result, c, dataStart);
                if (nameScore > bestNameScore)
                {
                    bestNameScore = nameScore;
                    bestNameCol = c;
                }
            }

            if (bestNameCol < 0 || bestNameScore < 4)
            {
                log?.Info($"TABLE-GRID: scope={result.ScopeIndex} infer columns: ColMark={bestMarkCol} but no ColName (score={bestNameScore})");
                return false;
            }

            result.ColMark = bestMarkCol;
            result.ColName = bestNameCol;
            result.ColumnsInferredFromData = true;
            result.HeaderPath = "infer-data";
            result.SchemaSource = "infer";
            SpecGridLog.WriteTrace("HEADER-SCAN", $"scope={result.ScopeIndex} path=infer-data");

            var qtyScores = new int[cols];
            var bestQtyCol = -1;
            var bestQtyScore = -1;
            for (var c = 0; c < cols; c++)
            {
                if (c == bestMarkCol || c == bestNameCol)
                {
                    continue;
                }

                if (HeaderTextLooksLikeMassColumn(result, c))
                {
                    continue;
                }

                var header = MTextPlainText.SanitizeRawContents(BuildHeaderOnlyColumnText(result, c)).ToLowerInvariant();
                var qtyScore = ScoreQtyHeader(header);
                qtyScores[c] = qtyScore;
                if (qtyScore > bestQtyScore)
                {
                    bestQtyScore = qtyScore;
                    bestQtyCol = c;
                }
            }

            result.ColQty = bestQtyScore >= MinHeaderScore ? bestQtyCol : -1;
            if (result.ColQty >= 0)
            {
                result.ColQtySource = "inference";
            }

            ApplyStandardColumnLayout(result, log);

            result.InferenceColQtyScoresSummary = FormatInferenceColQtyScores(result, bestMarkCol, bestNameCol, qtyScores);
            SpecGridLog.WriteTrace(
                "COLQTY",
                $"scope={result.ScopeIndex} infer-data Mark={result.ColMark} Name={result.ColName} Qty={result.ColQty} src={result.ColQtySource} layout={result.ColQtyLayoutFixDiag ?? "—"}");
            log?.Info(
                $"TABLE-GRID: scope={result.ScopeIndex} infer columns from data: MARK={result.ColMark} NAME={result.ColName} QTY={result.ColQty} paletteOverlap={CountPaletteKeyOverlapInColumn(result, bestMarkCol, paletteKeys)}");
            if (result.ColQty < 0)
            {
                TryResolveMissingColQty(result, log);
            }

            return true;
        }

        private static string FormatInferenceColQtyScores(
            ScopeGridResult result,
            int markCol,
            int nameCol,
            int[] qtyScores)
        {
            if (qtyScores == null || qtyScores.Length == 0)
            {
                return "inference ColQty: нет столбцов";
            }

            var parts = new List<string>();
            for (var c = 0; c < qtyScores.Length; c++)
            {
                if (c == markCol || c == nameCol)
                {
                    continue;
                }

                parts.Add($"col{c}={qtyScores[c]}");
            }

            return parts.Count > 0
                ? $"inference ColQty scores (min {MinHeaderScore}): {string.Join(", ", parts)}"
                : "inference ColQty: нет кандидатов кроме Mark/Name";
        }

        private static int CountPaletteKeyOverlapInColumn(
            ScopeGridResult result,
            int col,
            IReadOnlyDictionary<int, int> paletteKeys)
        {
            if (result?.CellText == null || col < 0 || paletteKeys == null || paletteKeys.Count == 0)
            {
                return 0;
            }

            var keys = new HashSet<int>();
            var rows = result.CellText.GetLength(0);
            var dataStart = result.RowDataStart > 0 ? result.RowDataStart : ResolveHeaderEndRow(result);
            for (var r = dataStart; r < rows; r++)
            {
                if (IsSectionHeaderRow(result, r))
                {
                    continue;
                }

                var cell = result.CellText[r, col] ?? string.Empty;
                if (MTextPlainText.TryParseMarkKey(cell, out var key) && paletteKeys.ContainsKey(key))
                {
                    keys.Add(key);
                }
            }

            return keys.Count;
        }

        private static int SumNameScoreInDataColumn(ScopeGridResult result, int col, int dataStart)
        {
            if (result?.CellText == null || col < 0)
            {
                return 0;
            }

            var rows = result.CellText.GetLength(0);
            var total = 0;
            for (var r = dataStart; r < rows; r++)
            {
                if (IsSectionHeaderRow(result, r))
                {
                    continue;
                }

                var cell = result.CellText[r, col] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(cell))
                {
                    continue;
                }

                total += MTextPlainText.NameScore(MTextPlainText.SanitizeRawContents(cell));
            }

            return total;
        }

        internal static string FormatHeaderDataBandHint(ScopeGridResult scope)
        {
            if (scope == null || !TryGetHeaderTopTextBandY(scope, out var yLo, out var yHi))
            {
                return string.Empty;
            }

            if (HeaderColumnsSufficientForNames(scope))
            {
                return string.Empty;
            }

            var bandTexts = (scope.AllTexts ?? new List<TextSample>())
                .Where(t => t != null && t.Y >= yLo && t.Y <= yHi)
                .ToList();
            if (bandTexts.Count == 0)
            {
                return string.Empty;
            }

            var dataLike = 0;
            foreach (var t in bandTexts)
            {
                var plain = MTextPlainText.SanitizeRawContents(t.Raw ?? t.Plain ?? string.Empty);
                if (string.IsNullOrWhiteSpace(plain))
                {
                    continue;
                }

                var lower = plain.ToLowerInvariant();
                if (ScoreHeader(lower, "марка", "поз", "поз.", "наимен", "кол") > 0)
                {
                    continue;
                }

                if (MTextPlainText.NameScore(plain) >= 4
                    || plain.IndexOf("гост", StringComparison.OrdinalIgnoreCase) >= 0
                    || MTextPlainText.TryParseMarkKey(plain, out _))
                {
                    dataLike++;
                }
            }

            if (dataLike < Math.Max(2, bandTexts.Count / 2))
            {
                return string.Empty;
            }

            return "[POSC] Строка шапки не найдена в выделении — выделите таблицу с заголовками «Поз.»/«Наименование»/«Кол.» или сначала ЗАПУСТИТЬ для подбора столбца марок.";
        }

        private static string ResolveGridLayerForScope(
            List<GridLineSeg> horiz,
            List<GridLineSeg> vert,
            string sharedGridLayerHint,
            SpecGridLog log,
            int scopeIndex)
        {
            var detected = AutoDetectGridLayer(horiz, vert, log, scopeIndex);
            if (string.IsNullOrWhiteSpace(sharedGridLayerHint))
            {
                return detected;
            }

            if (string.IsNullOrWhiteSpace(detected))
            {
                return null;
            }

            if (string.Equals(sharedGridLayerHint, detected, StringComparison.OrdinalIgnoreCase))
            {
                return detected;
            }

            var candidates = (horiz ?? new List<GridLineSeg>()).Concat(vert ?? new List<GridLineSeg>());
            var hintCount = candidates.Count(l => string.Equals(l.Layer, sharedGridLayerHint, StringComparison.OrdinalIgnoreCase));
            if (hintCount == 0)
            {
                log?.Info($"TABLE-GRID: scope={scopeIndex} shared grid layer hint \"{sharedGridLayerHint}\" not in pick — using {detected}");
                return detected;
            }

            log?.Info($"TABLE-GRID: scope={scopeIndex} own grid layer={detected} (hint was {sharedGridLayerHint})");
            return detected;
        }

        private static void DetectHeader(ScopeGridResult result, SpecGridLog log)
        {
            result.HeaderDetectedByTopTextBand = false;
            if (DetectHeaderBoundaryAndColumns(result, log))
            {
                if (result.ColQty >= 0 && string.IsNullOrEmpty(result.ColQtySource))
                {
                    result.ColQtySource = "grid";
                }

                return;
            }

            if (DetectHeaderByTopGridRows(result, log))
            {
                if (result.ColQty >= 0 && string.IsNullOrEmpty(result.ColQtySource))
                {
                    result.ColQtySource = "grid";
                }

                return;
            }

            if (DetectHeaderByGridRows(result, log))
            {
                if (result.ColQty >= 0 && string.IsNullOrEmpty(result.ColQtySource))
                {
                    result.ColQtySource = "grid";
                }

                return;
            }

            DetectHeaderByColumns(result, log);
            if (HeaderColumnsSufficientForNames(result))
            {
                if (result.ColQty >= 0 && string.IsNullOrEmpty(result.ColQtySource))
                {
                    result.ColQtySource = "grid";
                }

                return;
            }

            if (DetectHeaderByDbTextHeaderBand(result, log))
            {
                if (result.ColQty >= 0 && string.IsNullOrEmpty(result.ColQtySource))
                {
                    result.ColQtySource = "dbTextBand";
                }

                if (HeaderColumnsSufficientForNames(result))
                {
                    return;
                }
            }

            if (DetectHeaderByTopTextBand(result, log))
            {
                result.HeaderPath = "topBand fallback";
                SpecGridLog.WriteTrace("HEADER-SCAN", $"scope={result.ScopeIndex} path=topBand fallback");
                if (result.ColQty >= 0 && string.IsNullOrEmpty(result.ColQtySource))
                {
                    result.ColQtySource = "topBand";
                }

                return;
            }

            DetectHeaderSimpleRows01(result, log, onlyQty: false);
            result.HeaderPath = "simple01";
            if (result.ColQty >= 0 && string.IsNullOrEmpty(result.ColQtySource))
            {
                result.ColQtySource = "simple01";
            }
        }

        /// <summary>Скан строк 0..HeaderScanMaxRows по токенам шапки; граница данных без maxY−2000 как primary.</summary>
        private static bool DetectHeaderBoundaryAndColumns(ScopeGridResult result, SpecGridLog log)
        {
            var cols = result.GridXs?.Count - 1 ?? 0;
            if (cols <= 0 || result?.CellText == null)
            {
                return false;
            }

            var rows = result.CellText.GetLength(0);
            var scanMax = Math.Min(HeaderScanMaxRows, rows);
            var markScores = new int[cols];
            var nameScores = new int[cols];
            var qtyScores = new int[cols];
            var desScores = new int[cols];
            var lastHeaderRow = -1;

            for (var r = 0; r < scanMax; r++)
            {
                var rowMark = 0;
                var rowName = 0;
                var rowQty = 0;
                var rowDes = 0;
                var hasDigitMark = false;

                for (var c = 0; c < cols; c++)
                {
                    var cell = MTextPlainText.SanitizeRawContents(result.CellText[r, c] ?? string.Empty).ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(cell))
                    {
                        continue;
                    }

                    if (MTextPlainText.TryParseMarkKey(cell, out _) && !IsHeaderLabelInMarkCell(cell))
                    {
                        hasDigitMark = true;
                    }

                    var ms = ScoreMarkHeader(cell);
                    var ns = ScoreNameHeader(cell);
                    var qs = ScoreQtyHeader(cell);
                    var ds = ScoreDesignationHeader(cell);
                    rowMark = Math.Max(rowMark, ms);
                    rowName = Math.Max(rowName, ns);
                    rowQty = Math.Max(rowQty, qs);
                    rowDes = Math.Max(rowDes, ds);
                    markScores[c] = Math.Max(markScores[c], ms);
                    nameScores[c] = Math.Max(nameScores[c], ns);
                    qtyScores[c] = Math.Max(qtyScores[c], qs);
                    desScores[c] = Math.Max(desScores[c], ds);
                }

                var isHeaderRow = !hasDigitMark
                    && (rowMark >= MinHeaderScore || rowName >= MinHeaderScore || rowQty >= MinHeaderScore || rowDes >= MinHeaderScore);
                if (isHeaderRow)
                {
                    lastHeaderRow = r;
                }

                SpecGridLog.WriteTrace(
                    "HEADER-SCAN",
                    $"scope={result.ScopeIndex} row={r} scan: mark={rowMark} name={rowName} qty={rowQty} des={rowDes} header={isHeaderRow}");
            }

            if (lastHeaderRow < 0)
            {
                return false;
            }

            result.HeaderEndRow = lastHeaderRow + 1;
            var tokenHeaderEnd = result.HeaderEndRow;
            result.HeaderTokenEndRow = tokenHeaderEnd;
            var hLineBoundary = FindHeaderEndRowByHorizontalBorders(result, result.HorizontalLines);
            if (hLineBoundary >= 0)
            {
                result.HeaderEndRow = Math.Max(result.HeaderEndRow, hLineBoundary);
            }

            var firstDataRow = FindFirstDataRowByGridScan(result);
            if (firstDataRow >= 0)
            {
                result.HeaderEndRow = Math.Max(result.HeaderEndRow, firstDataRow);
                result.RowDataStart = firstDataRow;
            }
            else
            {
                result.RowDataStart = result.HeaderEndRow;
            }

            if (hLineBoundary >= 0)
            {
                result.RowDataStart = Math.Max(result.RowDataStart, hLineBoundary);
                SpecGridLog.WriteTrace(
                    "HEADER-SCAN",
                    $"scope={result.ScopeIndex} hLineBoundary={hLineBoundary} tokenHeaderEnd={tokenHeaderEnd} headerEndRow={result.HeaderEndRow} rowDataStart={result.RowDataStart}");
            }

            SpecGridLog.WriteTrace(
                "HEADER-DATA-ROW",
                $"scope={result.ScopeIndex} lastHeaderRow={lastHeaderRow} tokenEnd={tokenHeaderEnd} firstGridData={firstDataRow} hLine={hLineBoundary} final HeaderEndRow={result.HeaderEndRow} RowDataStart={result.RowDataStart}");

            SanitizeMarkScoresForDigitOnlyHeaders(result, markScores, log);
            EnsureUniqueHeaderColumns(result, markScores, nameScores, qtyScores, log);
            var taken = new HashSet<int>();
            if (result.ColMark >= 0)
            {
                taken.Add(result.ColMark);
            }

            if (result.ColQty >= 0)
            {
                taken.Add(result.ColQty);
            }

            if (result.ColName >= 0)
            {
                taken.Add(result.ColName);
            }

            result.ColDesignation = PickBestHeaderColumn(desScores, taken);
            SanitizeColQtyColumn(result, qtyScores, log);
            RefineColMarkByDataMarks(result, markScores, nameScores, qtyScores, log);
            ApplyMarkAnchoredHeaderBoundary(result, result.HorizontalLines, log);
            result.HeaderPath = "gridTokens";
            SpecGridLog.WriteTrace(
                "HEADER-SCAN",
                $"scope={result.ScopeIndex} headerEndRow={result.HeaderEndRow} rowDataStart={result.RowDataStart} path=gridTokens ColMark={result.ColMark} ColName={result.ColName} ColQty={result.ColQty} ColDes={result.ColDesignation} source=grid");

            log?.Info(
                $"TABLE-GRID: scope={result.ScopeIndex} header boundary (gridTokens): HeaderEndRow={result.HeaderEndRow} RowDataStart={result.RowDataStart} MARK={result.ColMark} NAME={result.ColName} QTY={result.ColQty}");
            return HeaderColumnsSufficientForNames(result);
        }

        /// <summary>Граница шапки/данных: скан строк сетки сверху вниз (единый алгоритм для всех таблиц).</summary>
        private static void ApplyHeaderBoundaryFromGridScan(ScopeGridResult result, SpecGridLog log)
        {
            var firstDataRow = FindFirstDataRowByGridScan(result);
            if (firstDataRow < 0)
            {
                log?.Info(
                    $"TABLE-GRID: scope={result.ScopeIndex} gridScan: data row not found, HeaderEndRow={result.HeaderEndRow} (H-lines fallback)");
                return;
            }

            result.HeaderEndRow = firstDataRow;
            result.RowDataStart = firstDataRow;
            ApplyMarkAnchoredHeaderBoundary(result, result.HorizontalLines, log);
            log?.Info(
                $"TABLE-GRID: scope={result.ScopeIndex} gridScan: firstDataRow={firstDataRow} → HeaderEndRow/RowDataStart={firstDataRow}");
        }

        private sealed class GridScanRowVerdict
        {
            public bool IsData;
            public bool HasMark;
            public bool HasName;
            public bool HasQty;
            public string ColMarkPreview = string.Empty;
            public string ColNamePreview = string.Empty;
        }

        private static GridScanRowVerdict DescribeGridScanRowVerdict(ScopeGridResult result, int row)
        {
            var verdict = new GridScanRowVerdict();
            if (result?.CellText == null || row < 0 || row >= result.CellText.GetLength(0))
            {
                return verdict;
            }

            if (result.ColMark >= 0 && result.ColMark < result.CellText.GetLength(1))
            {
                verdict.ColMarkPreview = TrimForLog(result.CellText[row, result.ColMark] ?? string.Empty, 24);
            }

            if (result.ColName >= 0 && result.ColName < result.CellText.GetLength(1))
            {
                verdict.ColNamePreview = TrimForLog(result.CellText[row, result.ColName] ?? string.Empty, 32);
            }

            var cols = result.CellText.GetLength(1);
            if (result.ColMark >= 0 && result.ColMark < cols)
            {
                var markCell = (result.CellText[row, result.ColMark] ?? string.Empty).Trim();
                if (CellLooksLikeDataMarkInColMark(result, result.ColMark, markCell))
                {
                    verdict.HasMark = true;
                    verdict.IsData = true;
                    return verdict;
                }
            }

            var hasNameData = false;
            var hasQtyOrMarkHint = false;
            for (var c = 0; c < cols; c++)
            {
                var cell = (result.CellText[row, c] ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(cell))
                {
                    continue;
                }

                if (!IsGridScanMarkOrQtyColumn(result, c))
                {
                    if (c == result.ColName && CellLooksLikeNameData(cell))
                    {
                        hasNameData = true;
                        verdict.HasName = true;
                    }

                    continue;
                }

                if (CellLooksLikeDataMarkInColMark(result, c, cell))
                {
                    hasQtyOrMarkHint = true;
                    verdict.HasMark = true;
                }
                else if (c == result.ColQty && CellLooksLikeQtyValue(cell))
                {
                    hasQtyOrMarkHint = true;
                    verdict.HasQty = true;
                }
                else if (c == result.ColName && CellLooksLikeNameData(cell))
                {
                    hasNameData = true;
                    verdict.HasName = true;
                }
            }

            verdict.IsData = hasNameData && hasQtyOrMarkHint;
            return verdict;
        }

        private static void LogHeaderDataRowScan(ScopeGridResult result)
        {
            if (result?.CellText == null)
            {
                return;
            }

            var rows = result.CellText.GetLength(0);
            var headerEnd = result.HeaderEndRow > 0 ? result.HeaderEndRow : ResolveHeaderEndRow(result);
            var scanTo = Math.Min(rows, Math.Max(headerEnd + 4, HeaderScanMaxRows + 1));
            for (var r = 0; r < scanTo; r++)
            {
                var v = DescribeGridScanRowVerdict(result, r);
                SpecGridLog.WriteTrace(
                    "HEADER-DATA-ROW",
                    $"scope={result.ScopeIndex} r={r} isData={v.IsData} hasMark={v.HasMark} hasName={v.HasName} hasQty={v.HasQty} colMark=\"{v.ColMarkPreview}\" colName=\"{v.ColNamePreview}\"");
            }
        }

        private static int FindFirstDataRowByGridScan(ScopeGridResult result)
        {
            if (result?.CellText == null)
            {
                return -1;
            }

            LogHeaderDataRowScan(result);

            var rows = result.CellText.GetLength(0);
            for (var r = 0; r < rows; r++)
            {
                if (IsGridScanDataRow(result, r))
                {
                    SpecGridLog.WriteTrace(
                        "HEADER-DATA-ROW",
                        $"scope={result.ScopeIndex} gridScanHit r={r}");
                    return r;
                }
            }

            return -1;
        }

        private static bool IsGridScanDataRow(ScopeGridResult result, int row)
        {
            if (result?.CellText == null || row < 0 || row >= result.CellText.GetLength(0))
            {
                return false;
            }

            if (result.ColMark >= 0 && result.ColMark < result.CellText.GetLength(1))
            {
                var markCell = (result.CellText[row, result.ColMark] ?? string.Empty).Trim();
                if (CellLooksLikeDataMarkInColMark(result, result.ColMark, markCell))
                {
                    return true;
                }
            }

            var cols = result.CellText.GetLength(1);
            var hasNameData = false;
            var hasQtyOrMarkHint = false;
            for (var c = 0; c < cols; c++)
            {
                var cell = (result.CellText[row, c] ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(cell))
                {
                    continue;
                }

                if (!IsGridScanMarkOrQtyColumn(result, c))
                {
                    if (c == result.ColName && CellLooksLikeNameData(cell))
                    {
                        hasNameData = true;
                    }

                    continue;
                }

                if (CellLooksLikeDataMarkInColMark(result, c, cell))
                {
                    hasQtyOrMarkHint = true;
                }
                else if (c == result.ColQty && CellLooksLikeQtyValue(cell))
                {
                    hasQtyOrMarkHint = true;
                }
            }

            if (!hasNameData && result.ColName >= 0 && result.ColName < cols)
            {
                var nameCell = (result.CellText[row, result.ColName] ?? string.Empty).Trim();
                if (CellLooksLikeNameData(nameCell))
                {
                    hasNameData = true;
                }
            }

            return hasNameData && hasQtyOrMarkHint;
        }

        private static bool CellLooksLikeNameData(string cell)
        {
            var text = MTextPlainText.SanitizeRawContents(cell ?? string.Empty).Trim();
            if (text.Length < 8 || !MTextPlainText.HasLetter(text))
            {
                return false;
            }

            var lower = text.ToLowerInvariant();
            if (ScoreHeader(
                    lower,
                    "марка", "поз", "поз.", "mark", "п/п", "№", "номер", "item",
                    "наимен", "name", "назван", "наименование",
                    "кол", "кол.", "кол-во", "к-во", "масса", "ед") > 0)
            {
                return false;
            }

            return MTextPlainText.NameScore(text) >= 4;
        }

        private static bool CellLooksLikeQtyValue(string cell)
        {
            var t = (cell ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(t) || t.Length > 8)
            {
                return false;
            }

            if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            {
                return n >= 0 && n < 100000;
            }

            return double.TryParse(
                t.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var d)
                && d >= 0
                && d < 100000;
        }

        private static bool CellLooksLikeIntegerQtyValue(string cell)
        {
            var t = (cell ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(t) || t.Length > 8)
            {
                return false;
            }

            if (t.IndexOf(',') >= 0 || t.IndexOf('.') >= 0)
            {
                return false;
            }

            return int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                && n >= 0
                && n < 100000;
        }

        private static bool CellLooksLikeMassValue(string cell)
        {
            var t = (cell ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(t) || t.Length > 12)
            {
                return false;
            }

            if (t.IndexOf(',') < 0 && t.IndexOf('.') < 0)
            {
                return false;
            }

            return double.TryParse(
                t.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var d)
                && d > 0
                && d < 10000;
        }

        private static int CountQtyLikeInColumn(ScopeGridResult result, int col)
        {
            if (result?.CellText == null || col < 0)
            {
                return 0;
            }

            var rows = result.CellText.GetLength(0);
            var dataStart = result.RowDataStart > 0
                ? result.RowDataStart
                : Math.Max(ResolveHeaderEndRow(result), 0);
            var count = 0;
            for (var r = dataStart; r < rows; r++)
            {
                if (IsSectionHeaderRow(result, r))
                {
                    continue;
                }

                var cell = (result.CellText[r, col] ?? string.Empty).Trim();
                if (CellLooksLikeIntegerQtyValue(cell))
                {
                    count++;
                }
            }

            count = Math.Max(count, CountQtyValuesInColumnTexts(result, col));
            return count;
        }

        private static bool ColumnLooksLikeMassData(ScopeGridResult result, int col)
        {
            if (result?.CellText == null || col < 0)
            {
                return false;
            }

            if (HeaderTextLooksLikeMassColumn(result, col))
            {
                return true;
            }

            var rows = result.CellText.GetLength(0);
            var dataStart = result.RowDataStart > 0
                ? result.RowDataStart
                : Math.Max(ResolveHeaderEndRow(result), 0);
            var massCount = 0;
            var qtyCount = 0;
            for (var r = dataStart; r < rows; r++)
            {
                if (IsSectionHeaderRow(result, r))
                {
                    continue;
                }

                var cell = (result.CellText[r, col] ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(cell))
                {
                    continue;
                }

                if (CellLooksLikeMassValue(cell))
                {
                    massCount++;
                }
                else if (CellLooksLikeIntegerQtyValue(cell))
                {
                    qtyCount++;
                }
            }

            return massCount >= 3 && massCount > qtyCount;
        }

        /// <summary>Шапка по верхним 1–2 строкам сетки (двуязычные подписи ГТ).</summary>
        private static bool DetectHeaderByTopGridRows(ScopeGridResult result, SpecGridLog log)
        {
            var cols = result.GridXs?.Count - 1 ?? 0;
            if (cols <= 0 || result?.CellText == null)
            {
                return false;
            }

            var maxRow = Math.Min(2, ResolveHeaderEndRow(result));
            if (maxRow <= 0)
            {
                return false;
            }

            var markScores = new int[cols];
            var nameScores = new int[cols];
            var qtyScores = new int[cols];
            var rows = result.CellText.GetLength(0);

            for (var r = 0; r < maxRow && r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    var cell = MTextPlainText.SanitizeRawContents(result.CellText[r, c] ?? string.Empty).ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(cell))
                    {
                        continue;
                    }

                    markScores[c] = Math.Max(markScores[c], ScoreMarkHeader(cell));
                    nameScores[c] = Math.Max(nameScores[c], ScoreNameHeader(cell));
                    qtyScores[c] = Math.Max(qtyScores[c], ScoreQtyHeader(cell));
                }
            }

            SanitizeMarkScoresForDigitOnlyHeaders(result, markScores, log);
            EnsureUniqueHeaderColumns(result, markScores, nameScores, qtyScores, log);
            SanitizeColQtyColumn(result, qtyScores, log);
            RefineColMarkByDataMarks(result, markScores, nameScores, qtyScores, log);

            if (!HeaderColumnsSufficientForNames(result))
            {
                result.ColMark = -1;
                result.ColName = -1;
                result.ColQty = -1;
                return false;
            }

            log?.Info(
                $"TABLE-GRID: scope={result.ScopeIndex} header cols (top rows 0..{maxRow - 1}): MARK={result.ColMark} QTY={result.ColQty} NAME={result.ColName}");
            return true;
        }

        /// <summary>Шапка по строкам сетки 0..HeaderEndRow-1 (primary path).</summary>
        private static bool DetectHeaderByGridRows(ScopeGridResult result, SpecGridLog log)
        {
            var cols = result.GridXs.Count - 1;
            if (cols <= 0)
            {
                result.ColMark = -1;
                result.ColName = -1;
                result.ColQty = -1;
                return false;
            }

            var headerEnd = ResolveHeaderEndRow(result);
            if (headerEnd <= 0)
            {
                return false;
            }

            var markScores = new int[cols];
            var nameScores = new int[cols];
            var qtyScores = new int[cols];

            for (var c = 0; c < cols; c++)
            {
                var header = MTextPlainText.SanitizeRawContents(BuildHeaderOnlyColumnText(result, c)).ToLowerInvariant();
                markScores[c] = ScoreMarkHeader(header);
                nameScores[c] = ScoreNameHeader(header);
                qtyScores[c] = ScoreQtyHeader(header);
            }

            SanitizeMarkScoresForDigitOnlyHeaders(result, markScores, log);
            EnsureUniqueHeaderColumns(result, markScores, nameScores, qtyScores, log);
            SanitizeColQtyColumn(result, qtyScores, log);
            RefineColMarkByDataMarks(result, markScores, nameScores, qtyScores, log);
            log?.Info(
                $"TABLE-GRID: scope={result.ScopeIndex} header cols (grid rows 0..{headerEnd - 1}): MARK={result.ColMark} QTY={result.ColQty} NAME={result.ColName}");

            return HeaderColumnsSufficientForNames(result);
        }

        /// <summary>Шапка DBText/MText в полосе GridYs (не maxY−2000) — типично AC 2016 TEXT.</summary>
        private static bool DetectHeaderByDbTextHeaderBand(ScopeGridResult result, SpecGridLog log)
        {
            var cols = result.GridXs.Count - 1;
            if (cols <= 0 || result.AllTexts == null || result.AllTexts.Count == 0)
            {
                return false;
            }

            if (!TryGetHeaderBandY(result, out var yLo, out var yHi))
            {
                return false;
            }

            var markScores = new int[cols];
            var nameScores = new int[cols];
            var qtyScores = new int[cols];
            var bandTextCount = 0;
            var headerEndRow = ResolveHeaderEndRow(result);
            var diagHits = new List<string>();

            foreach (var t in result.AllTexts)
            {
                if (t == null)
                {
                    continue;
                }

                var textY = t.HeaderY;
                if (!IsTextYPlausibleForHeaderBand(result, textY))
                {
                    textY = t.AlignY;
                    if (!IsTextYPlausibleForHeaderBand(result, textY))
                    {
                        continue;
                    }
                }

                if (textY < yLo || textY > yHi)
                {
                    continue;
                }

                if (t.Row >= 0 && t.Row >= headerEndRow && headerEndRow > 0)
                {
                    continue;
                }

                var raw = !string.IsNullOrWhiteSpace(t.Raw) ? t.Raw : (t.Plain ?? string.Empty);
                var plain = MTextPlainText.SanitizeRawContents(raw);
                if (string.IsNullOrWhiteSpace(plain) || plain.Length > DbTextHeaderMaxPlainLen)
                {
                    continue;
                }

                var header = plain.ToLowerInvariant();
                if (MTextPlainText.TryParseMarkKey(header, out _)
                    || LooksLikeDesignationText(plain))
                {
                    continue;
                }

                var col = ResolveColumnIndexByX(result.GridXs, t.HeaderX);
                if (col < 0)
                {
                    col = ResolveColumnIndexByX(result.GridXs, t.AlignX);
                }

                if (col < 0)
                {
                    col = ResolveColumnIndexByX(result.GridXs, t.X);
                }

                if (col < 0 || col >= cols)
                {
                    continue;
                }

                bandTextCount++;
                var ms = ScoreMarkHeader(header);
                var ns = ScoreNameHeader(header);
                var qs = ScoreQtyHeader(header);
                markScores[col] += ms;
                nameScores[col] += ns;
                qtyScores[col] += qs;

                if (ms >= MinHeaderScore || ns >= MinHeaderScore || qs >= MinHeaderScore)
                {
                    var kind = t.IsMText ? "MText" : "DBText";
                    var score = Math.Max(ms, Math.Max(ns, qs));
                    diagHits.Add($"{kind} col{col} «{plain.Trim()}» score={score}");
                    log?.Info(
                        $"TABLE-GRID: scope={result.ScopeIndex} шапка {kind}: col{col} «{plain.Trim()}» score={score}");
                }
            }

            if (bandTextCount == 0)
            {
                return false;
            }

            SanitizeMarkScoresForDigitOnlyHeaders(result, markScores, log);
            EnsureUniqueHeaderColumns(result, markScores, nameScores, qtyScores, log);
            SanitizeColQtyColumn(result, qtyScores, log);
            RefineColMarkByDataMarks(result, markScores, nameScores, qtyScores, log);

            if (result.ColQty >= 0)
            {
                result.ColQtySource = "dbTextBand";
            }

            result.DbTextHeaderBandSummary = diagHits.Count > 0
                ? $"DBText в полосе шапки (GridYs): {string.Join("; ", diagHits)}"
                : $"DBText band: texts={bandTextCount} MARK={result.ColMark} NAME={result.ColName} QTY={result.ColQty}";

            log?.Info(
                $"TABLE-GRID: scope={result.ScopeIndex} HeaderDbTextBand yLo={yLo:F1} yHi={yHi:F1} texts={bandTextCount} MARK={result.ColMark} QTY={result.ColQty} NAME={result.ColName}");

            return HeaderColumnsSufficientForNames(result) || result.ColQty >= 0;
        }

        private static bool IsTextYPlausibleForHeaderBand(ScopeGridResult result, double y)
        {
            if (result?.GridYs == null || result.GridYs.Count < 2)
            {
                return true;
            }

            var ys = result.GridYs;
            var median = ys[ys.Count / 2];
            var rowStep = Math.Abs(ys[1] - ys[0]);
            if (rowStep < 1.0 && ys.Count >= 3)
            {
                rowStep = Math.Abs(ys[2] - ys[0]) * 0.5;
            }

            if (rowStep < 1.0)
            {
                rowStep = Math.Abs(ys[ys.Count - 1] - ys[0]) / Math.Max(1, ys.Count - 1);
            }

            if (rowStep < 1.0)
            {
                rowStep = 100.0;
            }

            var span = rowStep * HeaderBandYMedianFactor;
            if (Math.Abs(y - median) <= span)
            {
                return true;
            }

            return TryGetHeaderBandY(result, out var yLo, out var yHi)
                && y >= yLo - rowStep
                && y <= yHi + rowStep;
        }

        /// <summary>Fallback: шапка по верхней Y-полосе текстов, столбец по X.</summary>
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
            var headerEndRow = ResolveHeaderEndRow(result);

            foreach (var t in result.AllTexts ?? new List<TextSample>())
            {
                if (t == null || t.Y < yLo || t.Y > yHi)
                {
                    continue;
                }

                if (t.Row >= 0 && t.Row >= headerEndRow)
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

                if (MTextPlainText.TryParseMarkKey(header, out _))
                {
                    continue;
                }

                markScores[col] += ScoreHeader(header, "марка", "поз", "поз.", "mark", "п/п", "№", "номер", "item");
                nameScores[col] += ScoreHeader(header, "наимен", "name", "назван", "наименование");
                qtyScores[col] += ScoreQtyHeader(header);
            }

            EnsureUniqueHeaderColumns(result, markScores, nameScores, qtyScores, log);
            SanitizeColQtyColumn(result, qtyScores, log);
            RefineColMarkByDataMarks(result, markScores, nameScores, qtyScores, log);
            log?.Info(
                $"TABLE-GRID: scope={result.ScopeIndex} HeaderTopBand maxY={result.HeaderTopMaxY:F1} yLo={yLo:F1} texts={bandTextCount} MARK={result.ColMark} QTY={result.ColQty} NAME={result.ColName}");

            if (HeaderColumnsSufficientForNames(result))
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
                qtyScores[c] = ScoreQtyHeader(header);
            }

            EnsureUniqueHeaderColumns(result, markScores, nameScores, qtyScores, log);
            SanitizeColQtyColumn(result, qtyScores, log);
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
                        ScoreQtyHeader(header)));
                lines.Add(
                    $"[POSC]   {kind} «{TrimForLog(plain, 24)}» X={t.X:F1} Y={t.Y:F1} → {colLabel} score={score}");
            }

            return lines;
        }

        /// <summary>Шапка по верхней полосе — координаты HeaderX/Y (для диагностики после inference).</summary>
        internal static IEnumerable<string> BuildHeaderTopBandDiagnosticHeaderCoords(ScopeGridResult scope)
        {
            var lines = new List<string>();
            if (scope == null || !TryGetHeaderTopTextBandY(scope, out var yLo, out var yHi))
            {
                lines.Add("шапка по HeaderXY: нет текстов в выборке");
                return lines;
            }

            var bandTexts = (scope.AllTexts ?? new List<TextSample>())
                .Where(t => t != null && t.HeaderY >= yLo && t.HeaderY <= yHi)
                .ToList();

            lines.Add(
                $"шапка HeaderXY: maxY={scope.HeaderTopMaxY:F1} полоса {yLo:F1}..{yHi:F1} (текстов={bandTexts.Count})");

            foreach (var t in bandTexts.Take(10))
            {
                var kind = t.IsMText ? "MText" : "DBText";
                var plain = MTextPlainText.SanitizeRawContents(
                    !string.IsNullOrWhiteSpace(t.Raw) ? t.Raw : (t.Plain ?? string.Empty));
                var col = ResolveColumnIndexByX(scope.GridXs, t.HeaderX);
                var colLabel = col >= 0 ? $"col{col}" : "col?";
                var header = plain.ToLowerInvariant();
                var score = Math.Max(
                    ScoreHeader(header, "марка", "поз", "поз.", "mark", "п/п", "№", "номер", "item"),
                    Math.Max(
                        ScoreHeader(header, "наимен", "name", "назван", "наименование"),
                        ScoreQtyHeader(header)));
                var method = string.IsNullOrWhiteSpace(t.BoundsMethod) ? "?" : t.BoundsMethod;
                lines.Add(
                    $"  {kind}/{method} «{TrimForLog(plain, 24)}» H=({t.HeaderX:F0},{t.HeaderY:F0}) → {colLabel} score={score}");
            }

            return lines;
        }

        internal static IEnumerable<string> BuildUnassignedTextSamples(ScopeGridResult scope, int maxSamples = 5)
        {
            if (scope?.AllTexts == null)
            {
                yield break;
            }

            foreach (var t in scope.AllTexts.Where(x => x != null && (x.Row < 0 || x.Col < 0)).Take(maxSamples))
            {
                var kind = t.IsMText ? "MText" : "DBText";
                var method = string.IsNullOrWhiteSpace(t.BoundsMethod) ? "?" : t.BoundsMethod;
                var plain = TrimForLog(MTextPlainText.SanitizeRawContents(t.Plain ?? string.Empty), 30);
                yield return
                    $"#{t.SourceIndex} {kind} {method} Header=({t.HeaderX:F0},{t.HeaderY:F0}) Data=({t.DataX:F0},{t.DataY:F0}) «{plain}»";
            }
        }

        internal static void ReportHeaderTraceDiagnostic(ScopeGridResult scope, int scopeNum)
        {
            if (scope == null)
            {
                return;
            }

            SpecGridLog.WriteTrace(
                "HEADER",
                $"табл.{scopeNum} HeaderEndRow={scope.HeaderEndRow} RowDataStart={scope.RowDataStart} inferred={scope.ColumnsInferredFromData} ColMark={scope.ColMark} ColName={scope.ColName} ColQty={scope.ColQty}");

            if (scope.CellText == null)
            {
                return;
            }

            var cols = Math.Min(scope.CellText.GetLength(1), 6);
            for (var c = 0; c < cols; c++)
            {
                var header = MTextPlainText.SanitizeRawContents(BuildHeaderOnlyColumnText(scope, c));
                var lower = header.ToLowerInvariant();
                var preview = TrimForLog(header, 30);
                var ms = ScoreHeader(lower, "марка", "поз", "поз.", "mark", "п/п", "№", "номер", "item");
                var ns = ScoreHeader(lower, "наимен", "name", "назван", "наименование");
                var qs = ScoreQtyHeader(lower);
                SpecGridLog.WriteTrace("HEADER", $"табл.{scopeNum} col{c} «{preview}» scores mark={ms} name={ns} qty={qs}");
            }
        }

        internal static void ReportMarkNamesDiagnostic(Document doc, ScopeGridResult scope, int scopeNum)
        {
            if (doc == null || scope == null)
            {
                return;
            }

            var pairs = scope.MarkNamePairs ?? new Dictionary<int, string>();
            var total = pairs.Count;
            var emptyKeys = pairs.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key).ToList();
            var filled = total - emptyKeys.Count;
            SpecGridLog.WriteDiag(
                doc,
                $"Таблица {scopeNum} имена: MarkNamePairs={total}, заполнено={filled}, пустых={emptyKeys.Count}");

            if (emptyKeys.Count > 0)
            {
                var sample = string.Join(
                    ", ",
                    emptyKeys.OrderBy(k => k).Take(10).Select(k => k.ToString(CultureInfo.InvariantCulture)));
                SpecGridLog.WriteDiag(doc, $"Таблица {scopeNum} пустые имена (ключи): {sample}");
            }

            if (!string.IsNullOrWhiteSpace(scope.ColQtyLayoutFixDiag))
            {
                SpecGridLog.WriteDiag(doc, $"Таблица {scopeNum} ColQty layout: {scope.ColQtyLayoutFixDiag}");
            }

            if (scope.NameCol2DiagLines != null)
            {
                foreach (var line in scope.NameCol2DiagLines.Take(7))
                {
                    SpecGridLog.WriteDiag(doc, $"Таблица {scopeNum} {line}");
                }
            }

            ReportMergeBoundaryBleedWarnings(doc, scope, scopeNum);
        }

        /// <summary>Только [POSC] ВНИМАНИЕ при bleed — имя следующей марки попало в текущую.</summary>
        private static void ReportMergeBoundaryBleedWarnings(Document doc, ScopeGridResult scope, int scopeNum)
        {
            if (doc == null || scope == null || scope.ColName < 0)
            {
                return;
            }

            foreach (var kv in scope.KeyToRowMark.OrderBy(x => x.Key))
            {
                var key = kv.Key;
                if (!scope.KeyToRowTopSub.TryGetValue(key, out var rowTop))
                {
                    continue;
                }

                var markBlockEnd = scope.KeyToMarkBlockEnd.TryGetValue(key, out var be)
                    ? be
                    : GetMarkBlockEndExclusive(scope, rowTop, key);
                var nextKeyTop = GetNextKeyRowExclusive(scope, key);
                var nextMarkRow = GetNextKeyRowMarkExclusive(scope, key);
                var gridRows = ResolveGridRowCount(scope);
                var rowEndEx = markBlockEnd;
                var boundary = ResolveNextMarkBoundaryExclusive(rowTop, nextKeyTop, nextMarkRow, gridRows);
                if (boundary > rowTop)
                {
                    rowEndEx = Math.Min(rowEndEx, boundary);
                }

                var nameLeadCap = CapRowEndBeforeNextMarkNameLead(scope, key, rowTop, rowEndEx);
                if (nameLeadCap < rowEndEx)
                {
                    rowEndEx = nameLeadCap;
                }

                rowEndEx = Math.Max(rowEndEx, rowTop + 1);
                rowEndEx = Math.Min(rowEndEx, gridRows);

                if (nextKeyTop <= rowTop || rowEndEx <= nextKeyTop)
                {
                    continue;
                }

                var bleedTo = nextMarkRow > rowTop ? Math.Min(rowEndEx, nextMarkRow) : rowEndEx;
                var hasForeignName = false;
                for (var r = nextKeyTop; r < bleedTo && r < gridRows; r++)
                {
                    if (!string.IsNullOrWhiteSpace(GetTrimmedNameAtRow(scope, r)))
                    {
                        hasForeignName = true;
                        break;
                    }
                }

                if (!hasForeignName)
                {
                    continue;
                }

                var nextKey = GetNextMarkKeyAfter(scope, key);
                SpecGridLog.TryWriteMergeBoundaryLine(
                    doc,
                    $"ВНИМАНИЕ Табл.{scopeNum} марка {key}: захвачено {bleedTo - nextKeyTop} лишн. строк ({nextKeyTop}..{bleedTo - 1})" +
                    (nextKey > 0 ? $" — имя марки {nextKey} до её цифры" : " — имя следующей марки до её цифры"));
            }
        }

        private static int GetNextMarkKeyAfter(ScopeGridResult scope, int key)
        {
            foreach (var kv in scope.KeyToRowMark.OrderBy(x => x.Key))
            {
                if (kv.Key > key)
                {
                    return kv.Key;
                }
            }

            return 0;
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

        private static void SanitizeColQtyColumn(ScopeGridResult result, int[] qtyScores, SpecGridLog log)
        {
            if (result == null || result.ColQty < 0 || qtyScores == null)
            {
                return;
            }

            if (!HeaderTextLooksLikeMassColumn(result, result.ColQty))
            {
                return;
            }

            var badCol = result.ColQty;
            var taken = new HashSet<int>();
            if (result.ColMark >= 0)
            {
                taken.Add(result.ColMark);
            }

            taken.Add(badCol);
            result.ColQty = PickBestHeaderColumn(qtyScores, taken);
            log?.Warn(
                $"TABLE-GRID: scope={result.ScopeIndex} ColQty sanitized {badCol} (mass) -> {result.ColQty}");
        }

        private static bool HeaderTextLooksLikeMassColumn(ScopeGridResult result, int col)
        {
            if (result == null || col < 0)
            {
                return false;
            }

            var header = MTextPlainText.SanitizeRawContents(BuildHeaderOnlyColumnText(result, col)).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(header))
            {
                header = MTextPlainText.SanitizeRawContents(BuildHeaderTextForColumn(result, col)).ToLowerInvariant();
            }

            return header.IndexOf("масса", StringComparison.Ordinal) >= 0
                || header.IndexOf("масс", StringComparison.Ordinal) >= 0;
        }

        private static int ScoreQtyHeader(string header)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                return 0;
            }

            var h = header.ToLowerInvariant();
            var score = 0;
            if (h.IndexOf("масса", StringComparison.Ordinal) >= 0
                || h.IndexOf("масс", StringComparison.Ordinal) >= 0
                || h.IndexOf("обознач", StringComparison.Ordinal) >= 0
                || h.IndexOf("примеч", StringComparison.Ordinal) >= 0)
            {
                score -= 50;
            }

            if (h.IndexOf("кол.", StringComparison.Ordinal) >= 0
                || h.IndexOf("кол,", StringComparison.Ordinal) >= 0)
            {
                score += 30;
            }
            else if (h.IndexOf("кол-во", StringComparison.Ordinal) >= 0
                || h.IndexOf("к-во", StringComparison.Ordinal) >= 0)
            {
                score += 25;
            }
            else if (h.IndexOf("кол", StringComparison.Ordinal) >= 0)
            {
                score += 20;
            }

            if (h.IndexOf("qty", StringComparison.Ordinal) >= 0
                || h.IndexOf("quantity", StringComparison.Ordinal) >= 0)
            {
                score += 15;
            }

            if (h.IndexOf("unit", StringComparison.Ordinal) >= 0
                || h.IndexOf("ед", StringComparison.Ordinal) >= 0
                || h.IndexOf("note", StringComparison.Ordinal) >= 0)
            {
                score -= 20;
            }

            if (h.IndexOf("designation", StringComparison.Ordinal) >= 0
                || h.IndexOf("обознач", StringComparison.Ordinal) >= 0)
            {
                score -= 30;
            }

            return score;
        }

        private static int ScoreMarkHeader(string header) => ScoreHeader(header, MarkHeaderTokens);

        private static int ScoreNameHeader(string header) => ScoreHeader(header, NameHeaderTokens);

        private static int ScoreDesignationHeader(string header) => ScoreHeader(header, DesignationHeaderTokens);

        private static void SanitizeMarkScoresForDigitOnlyHeaders(
            ScopeGridResult result,
            int[] markScores,
            SpecGridLog log)
        {
            if (result == null || markScores == null)
            {
                return;
            }

            for (var c = 0; c < markScores.Length; c++)
            {
                if (markScores[c] <= 0)
                {
                    continue;
                }

                var header = BuildHeaderOnlyColumnText(result, c);
                if (!IsSpuriousDigitOnlyMarkHeader(header, result, c))
                {
                    continue;
                }

                markScores[c] = 0;
                log?.Info(
                    $"TABLE-GRID: scope={result.ScopeIndex} mark score zeroed col{c} digit-only «{TrimForLog(header, 20)}»");
            }
        }

        private static bool IsSpuriousDigitOnlyMarkHeader(string header, ScopeGridResult result, int col)
        {
            var text = MTextPlainText.SanitizeRawContents(header ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text) || MTextPlainText.HasLetter(text))
            {
                return false;
            }

            if (!MTextPlainText.TryParseMarkKey(text, out var key) || key < 1 || key > 99)
            {
                return false;
            }

            return CountDataMarkKeysInColumn(result, col) >= 10;
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
        /// Первая строка данных: первая цифра марки в ColMark после границы шапки (grid scan / H-lines).
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
            var searchFrom = rowDataStartBefore > 0
                ? rowDataStartBefore
                : FindFirstDataRowAfterHeaderBoundary(result, horiz);
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
                    ClampRowDataStartToGridScan(result, rowDataStartBefore, passLabel, log);
                    RejectBadPass2RowDataStart(result, rowDataStartBefore, isPass2, searchFrom, passLabel, log);
                    LogRowDataStartChange(result, rowDataStartBefore, passLabel, log);
                    ApplyMarkAnchoredHeaderBoundary(result, horiz, log);
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
                ClampRowDataStartToGridScan(result, rowDataStartBefore, passLabel, log);
                RejectBadPass2RowDataStart(result, rowDataStartBefore, isPass2, searchFrom, passLabel, log);
                LogRowDataStartChange(result, rowDataStartBefore, passLabel, log);
                ApplyMarkAnchoredHeaderBoundary(result, horiz, log);
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
                ClampRowDataStartToGridScan(result, rowDataStartBefore, passLabel, log);
                RejectBadPass2RowDataStart(result, rowDataStartBefore, isPass2, searchFrom, passLabel, log);
                LogRowDataStartChange(result, rowDataStartBefore, passLabel, log);
                ApplyMarkAnchoredHeaderBoundary(result, horiz, log);
                return;
            }

            result.RowDataStart = rowDataStartBefore > 0 ? rowDataStartBefore : Math.Min(searchFrom, rows - 1);
            log?.RowDataDiag(
                $"[ROW-DATA] scope={result.ScopeIndex} {passLabel}: FALLBACK RowDataStart={result.RowDataStart} (searchFrom={searchFrom}, марка не найдена)");
            ClampRowDataStartToGridScan(result, rowDataStartBefore, passLabel, log);
            RejectBadPass2RowDataStart(result, rowDataStartBefore, isPass2, searchFrom, passLabel, log);
            LogRowDataStartChange(result, rowDataStartBefore, passLabel, log);
            ApplyMarkAnchoredHeaderBoundary(result, horiz, log);
        }

        /// <summary>Не поднимать RowDataStart выше границы grid scan, если марка уже на более ранней строке.</summary>
        private static void ClampRowDataStartToGridScan(
            ScopeGridResult result,
            int rowDataStartBefore,
            string passLabel,
            SpecGridLog log)
        {
            if (rowDataStartBefore <= 0 || result.RowDataStart <= rowDataStartBefore)
            {
                return;
            }

            if (result.ColMark < 0 || result.CellText == null || rowDataStartBefore >= result.CellText.GetLength(0))
            {
                return;
            }

            var mark = result.CellText[rowDataStartBefore, result.ColMark] ?? string.Empty;
            if (!MTextPlainText.TryParseMarkKey(mark, out _))
            {
                return;
            }

            log?.Info(
                $"TABLE-GRID: scope={result.ScopeIndex} {passLabel}: Clamp RowDataStart {result.RowDataStart} → {rowDataStartBefore} (grid scan mark at row {rowDataStartBefore})");
            result.RowDataStart = rowDataStartBefore;
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
            var hLineBoundary = FindHeaderEndRowByHorizontalBorders(result, horiz);
            int headerEndRow;
            if (hLineBoundary >= 0)
            {
                headerEndRow = hLineBoundary;
            }
            else
            {
                var lastBorderRow = FindHeaderBoundaryRow(result, horiz, xL, xR, MaxHeaderBorderScanRow);
                if (lastBorderRow >= 0)
                {
                    headerEndRow = lastBorderRow;
                }
                else
                {
                    var markRow = FindFirstMarkRowFromCellText(result, minRow: 0);
                    headerEndRow = markRow >= 0 ? markRow : Math.Min(6, rows - 1);
                }
            }

            result.HeaderEndRow = headerEndRow;
            if (hLineBoundary >= 0)
            {
                log?.Info(
                    $"TABLE-GRID: scope={result.ScopeIndex} HeaderEndRow={headerEndRow} (hLineBoundary={hLineBoundary})");
            }
            else
            {
                log?.Info($"TABLE-GRID: scope={result.ScopeIndex} HeaderEndRow={headerEndRow}");
            }
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

        /// <summary>Первая строка с номером марки в ColMark (не подпись «поз.»).</summary>
        private static int FindFirstMarkRowInColMark(ScopeGridResult result, int minRow)
        {
            if (result?.CellText == null || result.ColMark < 0)
            {
                return -1;
            }

            var rows = result.CellText.GetLength(0);
            if (minRow < 0)
            {
                minRow = 0;
            }

            for (var r = minRow; r < rows; r++)
            {
                if (IsSectionHeaderRow(result, r))
                {
                    continue;
                }

                var mark = (result.CellText[r, result.ColMark] ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(mark) || IsHeaderLabelInMarkCell(mark))
                {
                    continue;
                }

                if (MTextPlainText.TryParseMarkKey(mark, out _))
                {
                    return r;
                }
            }

            var fromTexts = FindFirstMarkRowFromAllTexts(result, minRow);
            return fromTexts;
        }

        /// <summary>Цифра в ColMark = выход из шапки; H-линии не могут отодвинуть старт данных ниже.</summary>
        private static void ApplyMarkAnchoredHeaderBoundary(
            ScopeGridResult result,
            List<GridLineSeg> horiz,
            SpecGridLog log)
        {
            var firstMarkRow = FindFirstMarkRowInColMark(result, 0);
            if (firstMarkRow < 0)
            {
                return;
            }

            var blockTop = firstMarkRow;
            var lines = horiz;
            if ((lines == null || lines.Count == 0) && result.HorizForBind != null && result.HorizForBind.Count > 0)
            {
                lines = result.HorizForBind;
            }

            if (lines != null && lines.Count > 0)
            {
                blockTop = FindRowTopSub(result, lines, firstMarkRow);
            }

            var rowDataBefore = result.RowDataStart;
            var headerEndBefore = result.HeaderEndRow;
            result.RowDataStart = blockTop;
            if (result.HeaderEndRow > blockTop)
            {
                result.HeaderEndRow = blockTop;
            }
            else if (result.HeaderEndRow <= 0)
            {
                result.HeaderEndRow = blockTop;
            }

            SpecGridLog.WriteTrace(
                "HEADER-DATA-ROW",
                $"scope={result.ScopeIndex} markAnchor firstMarkRow={firstMarkRow} blockTop={blockTop} rule=colMark-digit RowDataStart={rowDataBefore}→{result.RowDataStart} HeaderEndRow={headerEndBefore}→{result.HeaderEndRow}");
            log?.Info(
                $"TABLE-GRID: scope={result.ScopeIndex} markAnchor colMark-digit firstMarkRow={firstMarkRow} blockTop={blockTop} RowDataStart={result.RowDataStart}");
        }

        private static bool IsGridScanMarkOrQtyColumn(ScopeGridResult result, int col)
        {
            if (result == null)
            {
                return false;
            }

            return col == result.ColMark || col == result.ColQty;
        }

        private static bool CellLooksLikeDataMarkInColMark(ScopeGridResult result, int col, string cell)
        {
            if (result.ColMark < 0 || col != result.ColMark)
            {
                return false;
            }

            var trimmed = (cell ?? string.Empty).Trim();
            return !string.IsNullOrWhiteSpace(trimmed)
                && !IsHeaderLabelInMarkCell(trimmed)
                && MTextPlainText.TryParseMarkKey(trimmed, out _);
        }

        /// <summary>Граница шапки/данных: 2-я полноширинная H-линия от верха (bilingual RU+EN шапка).</summary>
        private static int FindHeaderEndRowByHorizontalBorders(
            ScopeGridResult result,
            List<GridLineSeg> horiz)
        {
            if (result?.GridXs == null || result.GridXs.Count < 2
                || result.GridYs == null || result.GridYs.Count < 2
                || horiz == null || horiz.Count == 0)
            {
                return -1;
            }

            var xL = result.GridXs[0];
            var xR = result.GridXs[result.GridXs.Count - 1];
            var rows = result.CellText?.GetLength(0) ?? (result.GridYs.Count - 1);
            var firstMarkRow = FindFirstMarkRowInColMark(result, 0);
            var scanMaxRow = Math.Min(rows - 1, MaxHeaderBorderScanRow);
            if (firstMarkRow >= 0)
            {
                scanMaxRow = Math.Min(scanMaxRow, firstMarkRow - 1);
            }

            var borders = new List<int>();
            for (var r = 1; r <= scanMaxRow; r++)
            {
                if (r >= result.GridYs.Count)
                {
                    break;
                }

                var y = result.GridYs[r];
                if (HasHBorderAt(y, xL, xR, horiz, borderEps: EpsAxis * 3.0))
                {
                    borders.Add(r);
                }
            }

            if (borders.Count >= 2)
            {
                var chosen = borders[1];
                SpecGridLog.WriteTrace(
                    "HEADER-DATA-ROW",
                    $"scope={result.ScopeIndex} firstMarkRow={firstMarkRow} hBorders=[{string.Join(",", borders)}] chosen={chosen} rule=second-line cap=before-first-mark");
                return chosen;
            }

            if (borders.Count == 1)
            {
                SpecGridLog.WriteTrace(
                    "HEADER-DATA-ROW",
                    $"scope={result.ScopeIndex} firstMarkRow={firstMarkRow} hBorders=[{borders[0]}] chosen={borders[0]} rule=first-line cap=before-first-mark");
                return borders[0];
            }

            if (firstMarkRow >= 0)
            {
                SpecGridLog.WriteTrace(
                    "HEADER-DATA-ROW",
                    $"scope={result.ScopeIndex} firstMarkRow={firstMarkRow} hBorders=[] chosen=-1 cap=before-first-mark");
            }

            return -1;
        }

        private static string PreviewColNameAtRow(ScopeGridResult result, int row)
        {
            if (result?.CellText == null || result.ColName < 0
                || row < 0 || row >= result.CellText.GetLength(0)
                || result.ColName >= result.CellText.GetLength(1))
            {
                return string.Empty;
            }

            return TrimForLog(result.CellText[row, result.ColName] ?? string.Empty, 28);
        }

        private static void LogHeaderDataRowRebindSummary(ScopeGridResult result, List<GridLineSeg> horiz)
        {
            if (result?.KeyToRowMark == null || result.KeyToRowMark.Count == 0)
            {
                return;
            }

            var minKey = result.KeyToRowMark.Keys.Min();
            var rowMark = result.KeyToRowMark[minKey];
            var rowTop = result.KeyToRowTopSub.TryGetValue(minKey, out var rt) ? rt : -1;
            var rowTopRaw = -1;
            if (horiz != null && horiz.Count > 0 && result.ColMark >= 0)
            {
                rowTopRaw = FindRowTopSub(result, horiz, rowMark);
            }

            var h = result.HeaderEndRow > 0 ? result.HeaderEndRow : result.RowDataStart;
            if (h < 0)
            {
                h = 0;
            }

            SpecGridLog.WriteTrace(
                "HEADER-DATA-ROW",
                $"scope={result.ScopeIndex} HeaderEndRow={result.HeaderEndRow} RowDataStart={result.RowDataStart} minKey={minKey} rowMark={rowMark} rowTopRaw={rowTopRaw} rowTop={rowTop} row[{h - 1}]=\"{PreviewColNameAtRow(result, h - 1)}\" row[{h}]=\"{PreviewColNameAtRow(result, h)}\" row[{h + 1}]=\"{PreviewColNameAtRow(result, h + 1)}\"");
        }

        /// <summary>
        /// Строка, с которой начинается поиск данных: HeaderEndRow (grid scan) или H-line под шапкой.
        /// </summary>
        private static int FindFirstDataRowAfterHeaderBoundary(ScopeGridResult result, List<GridLineSeg> horiz)
        {
            var rows = result.CellText?.GetLength(0) ?? 0;
            if (rows <= 0)
            {
                return 0;
            }

            var searchFrom = ResolveHeaderEndRow(result);
            if (searchFrom < 0)
            {
                searchFrom = 0;
            }

            var searchFromInitial = searchFrom;
            if (result.ColMark < 0 || result.GridXs.Count <= result.ColMark + 1 || result.GridYs.Count < 3)
            {
                var outEarly = Math.Min(searchFrom, rows - 1);
                SpecGridLog.WriteTrace(
                    "HEADER-DATA-ROW",
                    $"scope={result.ScopeIndex} searchFrom={searchFromInitial} hLineBoundary=-1 out={outEarly} reason=no-colMark-grid");
                return outEarly;
            }

            if (horiz == null || horiz.Count == 0)
            {
                var outEarly = Math.Min(searchFrom, rows - 1);
                SpecGridLog.WriteTrace(
                    "HEADER-DATA-ROW",
                    $"scope={result.ScopeIndex} searchFrom={searchFromInitial} hLineBoundary=-1 out={outEarly} reason=no-horiz");
                return outEarly;
            }

            var hLineBoundary = FindHeaderEndRowByHorizontalBorders(result, horiz);
            if (hLineBoundary >= 0)
            {
                searchFrom = Math.Max(searchFrom, hLineBoundary);
            }
            else if (result.GridXs.Count > result.ColMark + 1)
            {
                var xL = result.GridXs[result.ColMark];
                var xR = result.GridXs[result.ColMark + 1];
                var lastBorderRow = FindHeaderBoundaryRow(result, horiz, xL, xR, MaxHeaderBorderScanRow);
                if (lastBorderRow >= 0)
                {
                    searchFrom = Math.Max(searchFrom, lastBorderRow);
                }
            }

            var outRow = Math.Min(searchFrom, rows - 1);
            var firstMarkRow = FindFirstMarkRowInColMark(result, 0);
            if (firstMarkRow >= 0 && outRow > firstMarkRow)
            {
                outRow = firstMarkRow;
            }

            SpecGridLog.WriteTrace(
                "HEADER-DATA-ROW",
                $"scope={result.ScopeIndex} searchFrom={searchFromInitial} hLineBoundary={hLineBoundary} firstMarkRow={firstMarkRow} out={outRow}");
            return outRow;
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

            var firstKeyRow = result.KeyToRowTopSub.Count > 0
                ? result.KeyToRowTopSub.Values.Min()
                : result.KeyToRowMark.Values.Min();
            var source = result.KeyToRowTopSub.Count > 0 ? "min KeyToRowTopSub" : "min KeyToRowMark";
            if (firstKeyRow < result.RowDataStart)
            {
                log?.RowDataDiag(
                    $"[ROW-DATA] scope={result.ScopeIndex} AlignRowDataStartToFirstMark: {result.RowDataStart} → {firstKeyRow} ({source})");
                log.Info($"TABLE-GRID: scope={result.ScopeIndex} RowDataStart {result.RowDataStart} -> {firstKeyRow} (first block top)");
                result.RowDataStart = firstKeyRow;
            }
            else
            {
                log?.RowDataDiag(
                    $"[ROW-DATA] scope={result.ScopeIndex} AlignRowDataStartToFirstMark: RowDataStart={result.RowDataStart} firstBlockTop={firstKeyRow} ({source}, без сдвига)");
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

        internal static void FillMarkNamesFromMergeGroupsPublic(ScopeGridResult result, SpecGridLog log) =>
            FillMarkNamesFromMergeGroups(result, log);

        private static void FillMarkNamesFromMergeGroups(ScopeGridResult result, SpecGridLog log)
        {
            result.MarkNamePairs.Clear();
            result.EmptyNameKeys.Clear();
            if (result.ColMark < 0)
            {
                return;
            }

            log.Info($"TABLE-ROWS: scope={result.ScopeIndex} startRow={result.RowDataStart} endRow={result.RowDataEnd} colMark={result.ColMark} colName={result.ColName}");
            var missing = 0;
            foreach (var kv in result.KeyToRowMark.OrderBy(x => x.Key))
            {
                var name = ResolveNameForKey(result, kv.Key, log, out var joinMeta);
                result.MarkNamePairs[kv.Key] = name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    missing++;
                    result.EmptyNameKeys.Add(kv.Key);
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

            var rowCandidates = new Dictionary<int, List<(int Key, TextSample Sample)>>();

            foreach (var t in result.AllTexts ?? new List<TextSample>())
            {
                if (!IsBindableDataText(result, t))
                {
                    continue;
                }

                if (!IsTextInColumnXBand(result, result.ColMark, t))
                {
                    continue;
                }

                var raw = t.Raw ?? t.Plain ?? string.Empty;
                if (!MarkKeyParser.TryParse(raw, out var key, out var prefix))
                {
                    continue;
                }

                if (SpecDiagPolicy.ShouldTraceMark(result))
                {
                    SpecGridLog.WriteTrace(
                        "MARK",
                        $"scope={result.ScopeIndex} parse raw=«{TrimForLog(raw, 24)}» → key={key} prefix={prefix}");
                }

                if (t.Col != result.ColMark && raw.Trim().Length > 4)
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
                if (SpecDiagPolicy.ShouldTraceMark(result))
                {
                    SpecGridLog.WriteTrace(
                        "MARK",
                        $"scope={result.ScopeIndex} bind key={kv.Value} row={kv.Key} col={result.ColMark}");
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
                var rowTopRaw = FindRowTopSub(result, horiz, rowMark);
                var rowTop = ResolveNameRowTopForKey(result, kv.Key, rowTopRaw);
                result.KeyToRowTopSub[kv.Key] = rowTop;
                var blockEnd = GetMarkBlockEndExclusive(result, rowTop, kv.Key);
                result.KeyToMarkBlockEnd[kv.Key] = blockEnd;
                var span = blockEnd - rowTop;
                if (SpecDiagPolicy.IsSampleKey(result, kv.Key))
                {
                    log?.RowDataDiag(
                        $"[ROW-DATA] scope={result.ScopeIndex} key={kv.Key} rowMark={rowMark} rowTopRaw={rowTopRaw} rowTop={rowTop} blockEnd={blockEnd} RowDataStart={result.RowDataStart}");
                }

                log.Debug($"MERGE-BLOCK: key={kv.Key} rowMark={rowMark} rowTop={rowTop} blockEndEx={blockEnd} span={span}");
            }
        }

        /// <summary>Универсальный key/value: имя с верхней строки марки (LINE, native Table, N scopes).</summary>
        public static string ResolveNameForKey(ScopeGridResult grid, int key)
        {
            return ResolveNameForKey(grid, key, null, out _);
        }

        public static string ResolveNameFromMergeGroup(ScopeGridResult grid, int key)
        {
            return ResolveNameForKey(grid, key, null, out _);
        }

        private static string ResolveNameForKey(
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

            var rowTop = ResolveNameRowTopForKey(grid, key);
            var markBlockEnd = grid.KeyToMarkBlockEnd.TryGetValue(key, out var be)
                ? be
                : GetMarkBlockEndExclusive(grid, rowTop, key);
            var isMerged = markBlockEnd > rowTop + 1 || rowTop < rowMark;
            var nextMarkRow = GetNextKeyRowMarkExclusive(grid, key);
            var nextKeyTop = GetNextKeyRowExclusive(grid, key);
            var gridRows = ResolveGridRowCount(grid);
            var rowEndExclusive = markBlockEnd;
            var nextBoundary = ResolveNextMarkBoundaryExclusive(rowTop, nextKeyTop, nextMarkRow, gridRows);
            if (nextBoundary > rowTop)
            {
                rowEndExclusive = Math.Min(rowEndExclusive, nextBoundary);
            }

            var nameLeadCap = CapRowEndBeforeNextMarkNameLead(grid, key, rowTop, rowEndExclusive);
            if (nameLeadCap < rowEndExclusive)
            {
                rowEndExclusive = nameLeadCap;
            }

            rowEndExclusive = Math.Max(rowEndExclusive, rowTop + 1);
            rowEndExclusive = Math.Min(rowEndExclusive, ResolveGridRowCount(grid));
            var boundaryReason =
                $"nameRows {rowTop}..{rowEndExclusive - 1} nextMarkRow={nextMarkRow} nextKeyTop={nextKeyTop} markBlockEnd={markBlockEnd} rowEndEx={rowEndExclusive} nameLeadCap={nameLeadCap} merged={isMerged}";

            var cellParts = new List<string>();
            CollectNamePartsFromCellText(grid, key, rowTop, rowEndExclusive, cellParts);

            var cellJoined = string.Join(" ", cellParts).Trim();
            var textParts = new List<string>();
            var textCount = 0;
            var allRowsHaveCellText = AllNameRowsHaveCellText(grid, key, rowTop, rowEndExclusive);
            var useCellTextOnly = cellJoined.Length >= CellTextOnlyNameMinLength && allRowsHaveCellText;
            var enumeratedLineCount = CountNameLinesInCellRange(grid, key, rowTop, rowEndExclusive);
            var useCellTextFromBlock = useCellTextOnly;
            var cellOnlyOffReason = string.Empty;
            if (isMerged && enumeratedLineCount > cellParts.Count && enumeratedLineCount > 1)
            {
                useCellTextFromBlock = false;
                cellOnlyOffReason = "merged-block";
            }
            else if (useCellTextFromBlock
                && !ContainsCyrillic(cellJoined)
                && HasCyrillicInMarkBlock(grid, key, rowTop, markBlockEnd))
            {
                useCellTextFromBlock = false;
                cellOnlyOffReason = "missing-cyrillic";
            }
            else if (useCellTextFromBlock
                && ((!ContainsCyrillic(cellJoined) && HasCyrillicNameTextsInBand(grid, key, rowTop, rowEndExclusive))
                    || (enumeratedLineCount > cellParts.Count && enumeratedLineCount > 1)))
            {
                useCellTextFromBlock = false;
                cellOnlyOffReason = string.IsNullOrEmpty(cellOnlyOffReason) ? "band-cyrillic" : cellOnlyOffReason;
            }

            if (!grid.IsNativeAcadTable && grid.GridYs != null && grid.GridYs.Count >= 2 && !useCellTextFromBlock)
            {
                var nameRowEnd = ResolveContinuationNameRowEnd(
                    grid, key, rowTop, rowEndExclusive, markBlockEnd, cellParts);
                textCount = CollectNamePartsForPositionRange(grid, key, rowTop, nameRowEnd, textParts, log);
                if (cellJoined.Length < CellTextOnlyNameMinLength)
                {
                    textCount += SupplementNamePartsInVerticalBand(grid, key, rowTop, nameRowEnd, textParts, log);
                }

                textParts = FilterTextPartsNotInCellText(textParts, cellJoined);
            }
            else if (useCellTextFromBlock && log != null)
            {
                log.Info($"[NAME-DEDUPE] scope={grid.ScopeIndex} key={key} reason=cell-only len={cellJoined.Length}");
            }

            var parts = useCellTextFromBlock
                ? cellParts
                : MergeNamePartsPreferCellText(cellParts, textParts);
            var rowEndInclusive = rowEndExclusive > rowTop ? rowEndExclusive - 1 : rowTop;
            meta = new NameJoinMeta(rowTop, rowEndInclusive, parts.Count, rowEndExclusive, boundaryReason, textCount);

            var joined = CollapseDuplicateNamePhrase(
                MTextPlainText.FormatForPaletteDisplay(string.Join(" ", parts).Trim()));
            RecordNameCol2Diagnostic(grid, key, parts, joined);
            var isEmpty = string.IsNullOrWhiteSpace(joined);
            if (isEmpty)
            {
                var cellAtMark = GetTrimmedNameAtRow(grid, rowMark);
                var path = grid.IsNativeAcadTable ? "Table" : "LINE";
                log?.Info(
                    $"[KV-ANCHOR] scope={grid.ScopeIndex} key={key} rowMark={rowMark} rowTop={rowTop} cellName=\"{TrimForLog(cellAtMark, 40)}\" path={path}");
                if (!string.IsNullOrWhiteSpace(cellAtMark))
                {
                    joined = CollapseDuplicateNamePhrase(MTextPlainText.FormatForPaletteDisplay(cellAtMark));
                }
                else
                {
                    joined = CollapseDuplicateNamePhrase(ResolveNameFromNeighborColumns(grid, rowMark));
                }

                isEmpty = string.IsNullOrWhiteSpace(joined);
            }

            if (SpecDiagPolicy.ShouldTraceName(grid, key, isEmpty, parts.Count, textCount))
            {
                var label = isEmpty ? "empty" : TrimForLog(joined, 60);
                var mode = useCellTextFromBlock ? "cellOnly" : "cell+all";
                var reasonSuffix = string.IsNullOrEmpty(cellOnlyOffReason)
                    ? string.Empty
                    : $" reason={cellOnlyOffReason} lines={enumeratedLineCount}/{cellParts.Count}";
                SpecGridLog.WriteTrace(
                    "NAME",
                    $"scope={grid.ScopeIndex} key={key} mode={mode} cellOnly={useCellTextFromBlock} rowTop={rowTop} rowEndEx={rowEndExclusive} nextKeyTop={nextKeyTop} nameLeadCap={nameLeadCap} parts={parts.Count} texts={textCount}{reasonSuffix} «{label}»");
                if (isEmpty && textCount > 0)
                {
                    SpecGridLog.WriteTrace(
                        "NAME",
                        $"scope={grid.ScopeIndex} key={key} empty texts={textCount} parts=0");
                }
            }

            return joined;
        }

        /// <summary>Верхняя строка блока марки: не шапка; не сдвигать rowTop, если ColName непустой над цифрой в merged ColMark.</summary>
        private static int ResolveNameRowTopForKey(ScopeGridResult grid, int key, int? rawRowTop = null)
        {
            if (!grid.KeyToRowMark.TryGetValue(key, out var rowMark))
            {
                return 0;
            }

            var rowTop = rawRowTop
                ?? (grid.KeyToRowTopSub.TryGetValue(key, out var rt) ? rt : rowMark);
            rowTop = Math.Max(rowTop, ResolveHeaderEndRow(grid));
            if (grid.RowDataStart > 0)
            {
                var beforeRowDataStart = rowTop;
                rowTop = Math.Max(rowTop, grid.RowDataStart);
                if (SpecDiagPolicy.IsSampleKey(grid, key)
                    && rawRowTop.HasValue
                    && rawRowTop.Value < grid.RowDataStart
                    && rowTop == grid.RowDataStart
                    && beforeRowDataStart < grid.RowDataStart)
                {
                    SpecGridLog.WriteTrace(
                        "HEADER-DATA-ROW",
                        $"scope={grid.ScopeIndex} key={key} rowTop clamped RowDataStart={grid.RowDataStart} rowTopBefore={beforeRowDataStart} rowTopAfter={rowTop} rowTopRaw={rawRowTop.Value}");
                }
            }

            while (rowTop < rowMark && IsSectionHeaderRow(grid, rowTop))
            {
                if (!string.IsNullOrWhiteSpace(GetTrimmedNameAtRow(grid, rowTop)))
                {
                    break;
                }

                rowTop++;
            }

            if (SpecDiagPolicy.IsSampleKey(grid, key) && rawRowTop.HasValue && rawRowTop.Value != rowTop)
            {
                SpecGridLog.WriteTrace(
                    "ROW-DATA",
                    $"scope={grid.ScopeIndex} key={key} rowMark={rowMark} rowTopRaw={rawRowTop.Value} rowTop={rowTop} (section-skip stopped: name above mark)");
            }

            return rowTop;
        }

        private static void CollectNamePartsFromCellText(
            ScopeGridResult grid,
            int key,
            int rowTop,
            int rowEndExclusive,
            List<string> parts)
        {
            if (grid?.CellText == null || grid.ColName < 0)
            {
                return;
            }

            var rows = grid.CellText.GetLength(0);
            for (var r = rowTop; r < rowEndExclusive && r < rows; r++)
            {
                var rowKey = ResolveMarkKeyAtRow(grid, r);
                if (rowKey > 0 && rowKey != key && !HasNameTextOwnedByKey(grid, key, r, rowTop, rowEndExclusive))
                {
                    continue;
                }

                if (IsSectionHeaderRow(grid, r) && !IsNameContinuationRow(grid, key, r))
                {
                    LogNameSectionRowSkip(grid, key, r, parts.Count);
                    continue;
                }

                var text = GetTrimmedNameAtRow(grid, r);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    foreach (var line in MTextPlainText.EnumerateDisplayNameLines(text))
                    {
                        if (LooksLikeDesignationText(line))
                        {
                            continue;
                        }

                        TryAddNamePartExact(parts, line);
                    }
                }
            }
        }

        /// <summary>Для листов-продолжений: расширить диапазон col2 до markBlockEnd, если col2 пуст и нет standalone у следующей марки.</summary>
        private static int ResolveContinuationNameRowEnd(
            ScopeGridResult grid,
            int key,
            int rowTop,
            int rowEndExclusive,
            int markBlockEnd,
            List<string> cellParts)
        {
            if (grid == null || !grid.ColumnsInferredFromData || markBlockEnd <= rowTop + 1)
            {
                return rowEndExclusive;
            }

            if (cellParts != null && cellParts.Count > 0)
            {
                return rowEndExclusive;
            }

            if (NextMarkHasStandaloneNameLead(grid, key, rowTop, rowEndExclusive))
            {
                return rowEndExclusive;
            }

            var expandedEnd = Math.Min(markBlockEnd, ResolveGridRowCount(grid));
            return Math.Max(rowEndExclusive, expandedEnd);
        }

        private static bool NextMarkHasStandaloneNameLead(
            ScopeGridResult grid,
            int key,
            int rowTop,
            int rowEndExclusive)
        {
            var nextMarkRow = GetNextKeyRowMarkExclusive(grid, key);
            if (nextMarkRow <= rowTop + 1 || nextMarkRow > rowEndExclusive)
            {
                return false;
            }

            var leadRow = nextMarkRow - 1;
            if (leadRow < rowTop)
            {
                return false;
            }

            var leadName = GetTrimmedNameAtRow(grid, leadRow);
            return !string.IsNullOrWhiteSpace(leadName)
                && MTextPlainText.IsStandaloneProductName(leadName);
        }

        /// <summary>Не включать лидирующую строку col2 следующей марки (цифра ниже по сетке).</summary>
        private static int CapRowEndBeforeNextMarkNameLead(
            ScopeGridResult grid,
            int key,
            int rowTop,
            int rowEndExclusive)
        {
            var nextMarkRow = GetNextKeyRowMarkExclusive(grid, key);
            if (nextMarkRow <= rowTop + 1 || nextMarkRow > rowEndExclusive)
            {
                return rowEndExclusive;
            }

            var leadRow = nextMarkRow - 1;
            if (leadRow < rowTop)
            {
                return rowEndExclusive;
            }

            if (ResolveMarkKeyAtRow(grid, leadRow) == key)
            {
                return rowEndExclusive;
            }

            if (grid.ColMark >= 0 && grid.CellText != null && leadRow < grid.CellText.GetLength(0))
            {
                var markCell = (grid.CellText[leadRow, grid.ColMark] ?? string.Empty).Trim();
                if (MTextPlainText.TryParseMarkKey(markCell, out var mk) && mk == key)
                {
                    return rowEndExclusive;
                }
            }

            var nameAtLead = GetTrimmedNameAtRow(grid, leadRow);
            if (string.IsNullOrWhiteSpace(nameAtLead))
            {
                return rowEndExclusive;
            }

            var foreignKey = ResolveMarkKeyAtRow(grid, leadRow);
            if (foreignKey > 0 && foreignKey != key)
            {
                return Math.Min(rowEndExclusive, leadRow);
            }

            if (foreignKey <= 0)
            {
                return Math.Min(rowEndExclusive, leadRow);
            }

            return rowEndExclusive;
        }

        private static bool ContainsCyrillic(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            foreach (var ch in text)
            {
                if (ch >= '\u0400' && ch <= '\u04FF')
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasCyrillicNameTextsInBand(
            ScopeGridResult grid,
            int key,
            int rowTop,
            int rowEndExclusive)
        {
            if (grid?.AllTexts == null || grid.ColName < 0)
            {
                return false;
            }

            foreach (var t in grid.AllTexts)
            {
                if (!IsTextInNameColumn(grid, t))
                {
                    continue;
                }

                if (t.Row >= 0 && (t.Row < rowTop || t.Row >= rowEndExclusive))
                {
                    continue;
                }

                if (!NameTextBelongsToMarkKey(grid, key, rowTop, rowEndExclusive, t, null))
                {
                    continue;
                }

                var display = GetDisplayText(t);
                if (ContainsCyrillic(display))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasCyrillicInMarkBlock(
            ScopeGridResult grid,
            int key,
            int rowTop,
            int markBlockEnd)
        {
            if (grid?.CellText == null)
            {
                return false;
            }

            var rows = grid.CellText.GetLength(0);
            var end = Math.Min(markBlockEnd, rows);
            for (var r = rowTop; r < end; r++)
            {
                if (grid.ColName >= 0 && r < rows)
                {
                    var nameCell = grid.CellText[r, grid.ColName] ?? string.Empty;
                    if (ContainsCyrillic(nameCell))
                    {
                        return true;
                    }
                }

                if (grid.ColDesignation >= 0 && r < rows)
                {
                    var desCell = grid.CellText[r, grid.ColDesignation] ?? string.Empty;
                    if (ContainsCyrillic(desCell))
                    {
                        return true;
                    }
                }
            }

            if (grid.AllTexts != null)
            {
                foreach (var t in grid.AllTexts)
                {
                    if (t.Row >= rowTop && t.Row < markBlockEnd
                        && (IsTextInNameColumn(grid, t) || IsTextInDesignationColumn(grid, t))
                        && NameTextBelongsToMarkKey(grid, key, rowTop, markBlockEnd, t, null)
                        && ContainsCyrillic(GetDisplayText(t)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasNameTextOwnedByKey(
            ScopeGridResult grid,
            int key,
            int row,
            int rowTop,
            int rowEndExclusive)
        {
            if (grid?.CellText == null || grid.ColName < 0)
            {
                return false;
            }

            if (grid.AllTexts != null)
            {
                foreach (var t in grid.AllTexts)
                {
                    if (t.Row != row || !IsTextInNameColumn(grid, t))
                    {
                        continue;
                    }

                    if (NameTextBelongsToMarkKey(grid, key, rowTop, rowEndExclusive, t, null))
                    {
                        return true;
                    }
                }
            }

            var nameCell = GetTrimmedNameAtRow(grid, row);
            if (string.IsNullOrWhiteSpace(nameCell) || row < rowTop || row >= rowEndExclusive)
            {
                return false;
            }

            var markKey = ResolveMarkKeyAtRow(grid, row);
            if (markKey <= 0 || markKey == key)
            {
                return true;
            }

            if (grid.KeyToRowMark.TryGetValue(markKey, out var foreignMarkRow) && row == foreignMarkRow)
            {
                var prevName = row > rowTop ? GetTrimmedNameAtRow(grid, row - 1) : string.Empty;
                return !string.IsNullOrWhiteSpace(prevName);
            }

            return false;
        }

        private static bool IsTextInDesignationColumn(ScopeGridResult grid, TextSample t)
        {
            if (grid == null || grid.ColDesignation < 0 || t == null)
            {
                return false;
            }

            return t.Col == grid.ColDesignation || IsTextInColumnXBand(grid, grid.ColDesignation, t);
        }

        private static int CountNameLinesInCellRange(
            ScopeGridResult grid,
            int key,
            int rowTop,
            int rowEndExclusive)
        {
            if (grid?.CellText == null || grid.ColName < 0)
            {
                return 0;
            }

            var rows = grid.CellText.GetLength(0);
            var count = 0;
            for (var r = rowTop; r < rowEndExclusive && r < rows; r++)
            {
                var rowKey = ResolveMarkKeyAtRow(grid, r);
                if (rowKey > 0 && rowKey != key)
                {
                    continue;
                }

                var text = GetTrimmedNameAtRow(grid, r);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                foreach (var line in MTextPlainText.EnumerateDisplayNameLines(text))
                {
                    if (!LooksLikeDesignationText(line))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static void RecordNameCol2Diagnostic(
            ScopeGridResult grid,
            int key,
            List<string> parts,
            string joined)
        {
            if (grid == null || grid.ColDesignation < 0 || !SpecDiagPolicy.IsSampleKey(grid, key))
            {
                return;
            }

            var excluded = 0;
            if (grid.AllTexts != null)
            {
                foreach (var t in grid.AllTexts)
                {
                    if (t == null || t.Col != grid.ColDesignation)
                    {
                        continue;
                    }

                    var display = GetDisplayText(t);
                    if (!string.IsNullOrWhiteSpace(display) && joined.IndexOf(display, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        excluded++;
                    }
                }
            }

            var line = $"имя key={key} col2-lines={parts.Count} excluded-designation={excluded} «{TrimForLog(joined, 60)}»";
            if (grid.NameCol2DiagLines == null)
            {
                grid.NameCol2DiagLines = new List<string>();
            }

            if (grid.NameCol2DiagLines.Count < 10)
            {
                grid.NameCol2DiagLines.Add(line);
            }
        }

        private static List<string> FilterTextPartsNotInCellText(List<string> textParts, string cellJoined)
        {
            if (textParts == null || textParts.Count == 0)
            {
                return textParts ?? new List<string>();
            }

            if (string.IsNullOrWhiteSpace(cellJoined))
            {
                return textParts;
            }

            var filtered = new List<string>();
            foreach (var p in textParts)
            {
                var t = (p ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(t))
                {
                    continue;
                }

                if (cellJoined.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0
                    || t.IndexOf(cellJoined, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                filtered.Add(t);
            }

            return filtered;
        }

        private static string CollapseDuplicateNamePhrase(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            text = text.Trim();
            if (text.Length < 10)
            {
                return text;
            }

            for (var sep = 0; sep <= 2; sep++)
            {
                var len = text.Length;
                var aLen = (len - sep) / 2;
                if (aLen < 5)
                {
                    continue;
                }

                var bStart = aLen + sep;
                if (bStart + aLen > len)
                {
                    continue;
                }

                var a = text.Substring(0, aLen).Trim();
                var b = text.Substring(bStart, aLen).Trim();
                if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                {
                    return a;
                }
            }

            return text;
        }

        private static string ResolveNameFromNeighborColumns(ScopeGridResult grid, int rowMark)
        {
            // Не подтягивать «Обозначение» (col1) — только ColName.
            return string.Empty;
        }

        private static List<string> MergeNamePartsPreferCellText(List<string> cellParts, List<string> textParts)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var merged = new List<string>();
            foreach (var p in cellParts.Concat(textParts))
            {
                var t = (p ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(t) || !seen.Add(t))
                {
                    continue;
                }

                merged.Add(t);
            }

            return merged;
        }

        /// <summary>
        /// Exclusive row: name rows at or after this index belong to the next mark (merge top or digit row).
        /// </summary>
        private static int ResolveNextMarkBoundaryExclusive(
            int rowTop,
            int nextKeyTop,
            int nextMarkRow,
            int gridRowCount)
        {
            var hasKeyTop = nextKeyTop > rowTop && nextKeyTop < gridRowCount;
            var hasMarkRow = nextMarkRow > rowTop && nextMarkRow < gridRowCount;

            if (hasKeyTop && hasMarkRow)
            {
                return Math.Min(nextKeyTop, nextMarkRow);
            }

            if (hasKeyTop)
            {
                return nextKeyTop;
            }

            if (hasMarkRow)
            {
                return nextMarkRow;
            }

            return gridRowCount;
        }

        private static int FinalizeMarkBlockEndExclusive(ScopeGridResult grid, int rowTop, int key, int blockEnd)
        {
            var rows = ResolveGridRowCount(grid);
            blockEnd = Math.Min(blockEnd, rows);
            var nextKeyTop = GetNextKeyRowExclusive(grid, key);
            var nextMarkRow = GetNextKeyRowMarkExclusive(grid, key);
            var boundary = ResolveNextMarkBoundaryExclusive(rowTop, nextKeyTop, nextMarkRow, rows);
            if (boundary > rowTop)
            {
                blockEnd = Math.Min(blockEnd, boundary);
            }

            return Math.Max(blockEnd, Math.Min(rowTop + 1, rows));
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

        /// <summary>Строка цифры следующей марки (KeyToRowMark), не rowTopSub — для границы склейки имени.</summary>
        private static int GetNextKeyRowMarkExclusive(ScopeGridResult grid, int key)
        {
            var rows = ResolveGridRowCount(grid);
            foreach (var kv in grid.KeyToRowMark.OrderBy(x => x.Key))
            {
                if (kv.Key > key)
                {
                    return Math.Min(kv.Value, rows);
                }
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
                return FinalizeMarkBlockEndExclusive(grid, rowTop, key, Math.Min(rowTop + 1, rows));
            }

            var r = rowTop + 1;
            while (r < rows)
            {
                var otherKey = ResolveMarkKeyAtRow(grid, r);
                if (otherKey > 0 && otherKey != key)
                {
                    return FinalizeMarkBlockEndExclusive(grid, rowTop, key, r);
                }

                var mark = (grid.CellText[r, grid.ColMark] ?? string.Empty).Trim();
                if (otherKey <= 0 && MTextPlainText.TryParseMarkKey(mark, out otherKey) && otherKey != key)
                {
                    return FinalizeMarkBlockEndExclusive(grid, rowTop, key, r);
                }

                r++;
            }

            var end = rows;
            end = Math.Max(end, ExtendMarkBlockEndByMarkTextY(grid, rowTop, key, end));
            return FinalizeMarkBlockEndExclusive(grid, rowTop, key, end);
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
                if (owner != key && SpecDiagPolicy.ShouldTraceName(grid, key, false, 0, 1))
                {
                    SpecGridLog.WriteTrace(
                        "NAME",
                        $"scope={grid.ScopeIndex} key={key} skip=foreignMark owner={owner} src={t.SourceIndex}");
                    log?.Debug(
                        $"[NAME-FOREIGN-SKIP] scope={grid.ScopeIndex} key={key} owner={owner} src={t.SourceIndex} tRow={t.Row} display=\"{TrimForLog(GetDisplayText(t), 40)}\"");
                }

                return owner == key;
            }

            return t.Row >= rowTop && t.Row < rowEndExclusive;
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
                if (SpecDiagPolicy.ShouldTraceName(grid, key, false, 0, 1))
                {
                    SpecGridLog.WriteTrace(
                        "NAME",
                        $"scope={grid.ScopeIndex} key={key} bleed-upstream domRow={dominantRow} foreign={markAtDom} src={t.SourceIndex}");
                }

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

        private static bool ShouldLogNameRejectReason(ScopeGridResult grid, int key, int partCount) =>
            SpecDiagPolicy.ShouldTraceName(grid, key, false, partCount, 1);

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
                    if (ShouldLogNameRejectReason(grid, key, parts.Count))
                    {
                        var rowMarkHint = grid.KeyToRowMark.TryGetValue(key, out var rm) ? rm : -1;
                        var rowTopHint = ResolveNameRowTopForKey(grid, key);
                        var blockEndHint = grid.KeyToMarkBlockEnd.TryGetValue(key, out var beHint)
                            ? beHint
                            : GetMarkBlockEndExclusive(grid, rowTopHint, key);
                        SpecGridLog.WriteTrace(
                            "NAME",
                            $"scope={grid.ScopeIndex} key={key} skip=section-line row={row} rowTop={rowTopHint} rowMark={rowMarkHint} blockEnd={blockEndHint} «{TrimForLog(line, 40)}»");
                    }

                    log?.Info($"[NAME-SECTION] scope={grid.ScopeIndex} key={key} row={row} line=\"{TrimForLog(line, 40)}\"");
                    continue;
                }

                if (grid.ColDesignation >= 0 && LooksLikeDesignationText(line))
                {
                    if (ShouldLogNameRejectReason(grid, key, parts.Count))
                    {
                        SpecGridLog.WriteTrace(
                            "NAME",
                            $"scope={grid.ScopeIndex} key={key} skip=designation «{TrimForLog(line, 40)}»");
                    }

                    continue;
                }

                if (!MTextPlainText.IsAcceptableNameContinuation(line))
                {
                    if (ShouldLogNameRejectReason(grid, key, parts.Count))
                    {
                        SpecGridLog.WriteTrace(
                            "NAME",
                            $"scope={grid.ScopeIndex} key={key} skip=not-acceptable «{TrimForLog(line, 40)}»");
                    }

                    continue;
                }

                var decoded = MTextPlainText.DecodeAutocadPercentCodes(line);
                if (string.IsNullOrWhiteSpace(decoded))
                {
                    if (ShouldLogNameRejectReason(grid, key, parts.Count))
                    {
                        SpecGridLog.WriteTrace(
                            "NAME",
                            $"scope={grid.ScopeIndex} key={key} skip=empty-decode «{TrimForLog(line, 40)}»");
                    }

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

                if (IsSectionHeaderRow(grid, r) && !IsNameContinuationRow(grid, key, r))
                {
                    LogNameSectionRowSkip(grid, key, r, parts.Count);
                    continue;
                }

                if (CollectNamePartsFromNameCell(grid, key, rowTop, rowEndExclusive, r, parts, consumedSources, log, ref textCount))
                {
                    // bleed на строке — пропуск строки, не обрыв диапазона
                }
            }

            return textCount;
        }

        private static TextSample PickBestNameTextForRow(List<TextSample> hits)
        {
            if (hits == null || hits.Count == 0)
            {
                return null;
            }

            if (hits.Count == 1)
            {
                return hits[0];
            }

            return hits
                .OrderByDescending(t => MTextPlainText.NameScore(GetDisplayText(t)))
                .ThenByDescending(t => (GetDisplayText(t) ?? string.Empty).Length)
                .ThenByDescending(t => t.YMax)
                .ThenBy(t => t.DataX)
                .First();
        }

        /// <summary>Один текст на строку (pick-best) после фильтров overlap/owner.</summary>
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
                .Where(t => IsTextInNameColumn(grid, t))
                .Where(t => TextOverlapsRowBand(t, yTop, yBottom, grid))
                .Where(t => NameTextBelongsToMarkKey(grid, key, rowTop, rowEndExclusive, t, log))
                .Where(t => !IsUpstreamBleedFromForeignMark(grid, key, row, yTop, yBottom, t, log))
                .ToList();

            var winner = PickBestNameTextForRow(rowHits);
            if (winner == null || string.IsNullOrWhiteSpace(winner.Raw ?? winner.Plain))
            {
                return false;
            }

            if (PartsContainStandalone(parts) && MTextPlainText.IsStandaloneProductName(GetDisplayText(winner)))
            {
                var foreignMark = ResolveMarkKeyAtRow(grid, row);
                if (foreignMark > 0 && foreignMark != key)
                {
                    if (SpecDiagPolicy.ShouldTraceName(grid, key, false, parts.Count, rowHits.Count))
                    {
                        SpecGridLog.WriteTrace(
                            "NAME",
                            $"scope={grid.ScopeIndex} key={key} skip=foreignMark owner={foreignMark} row={row}");
                    }

                    return true;
                }
            }

            if (consumedSources.Contains(winner.SourceIndex))
            {
                return false;
            }

            consumedSources.Add(winner.SourceIndex);
            textCount++;
            var rejected = rowHits.Count - 1;
            if (SpecDiagPolicy.ShouldTraceName(grid, key, false, parts.Count, rowHits.Count))
            {
                SpecGridLog.WriteTrace(
                    "NAME",
                    $"scope={grid.ScopeIndex} key={key} pick-best row={row} winner={(winner.IsMText ? "MText" : "DBText")} score={MTextPlainText.NameScore(GetDisplayText(winner))} rejected={rejected}");
            }

            AddNamePartsFromTextSample(grid, key, row, winner, parts, log);
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
            var nextKeyTop = GetNextKeyRowExclusive(grid, key);
            var yNextKeyTop = nextKeyTop > rowTop && nextKeyTop < grid.GridYs.Count
                ? grid.GridYs[nextKeyTop]
                : double.NaN;

            var hits = (grid.AllTexts ?? new List<TextSample>())
                .Where(t => PassesCellLayerFilter(t, grid))
                .Where(t => IsTextInNameColumn(grid, t))
                .Where(t => double.IsNaN(yNextKeyTop) || t.DataY > yNextKeyTop - eps)
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

                if (textRow >= 0 && IsSectionHeaderRow(grid, textRow) && !IsNameContinuationRow(grid, key, textRow))
                {
                    LogNameSectionRowSkip(grid, key, textRow, parts.Count);
                    continue;
                }

                var beforeCount = parts.Count;
                AddNamePartsFromTextSample(grid, key, textRow >= 0 ? textRow : rowTop, t, parts, log);
                if (parts.Count > beforeCount)
                {
                    supplementCount++;
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

        /// <summary>Все строки блока имеют непустой CellText в ColName — можно не ходить в AllTexts.</summary>
        private static bool AllNameRowsHaveCellText(ScopeGridResult grid, int key, int rowTop, int rowEndExclusive)
        {
            if (grid?.CellText == null || grid.ColName < 0 || rowEndExclusive <= rowTop)
            {
                return false;
            }

            var rows = grid.CellText.GetLength(0);
            for (var r = rowTop; r < rowEndExclusive && r < rows; r++)
            {
                if (IsSectionHeaderRow(grid, r) && !IsNameContinuationRow(grid, key, r))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(GetTrimmedNameAtRow(grid, r)))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Строка продолжения наименования внутри блока марки [rowTop, blockEnd) — в т.ч. над цифрой в merged ColMark.</summary>
        private static bool IsNameContinuationRow(ScopeGridResult grid, int key, int row)
        {
            if (!grid.KeyToRowMark.TryGetValue(key, out var rowMark))
            {
                return false;
            }

            var rowTop = ResolveNameRowTopForKey(grid, key);
            var blockEnd = grid.KeyToMarkBlockEnd.TryGetValue(key, out var be)
                ? be
                : GetMarkBlockEndExclusive(grid, rowTop, key);
            if (row < rowTop || row >= blockEnd)
            {
                return false;
            }

            if (row < rowMark)
            {
                var rowKeyAbove = ResolveMarkKeyAtRow(grid, row);
                if (rowKeyAbove > 0 && rowKeyAbove != key)
                {
                    return HasNameTextOwnedByKey(grid, key, row, rowTop, blockEnd);
                }

                return !string.IsNullOrWhiteSpace(GetTrimmedNameAtRow(grid, row));
            }

            if (row == rowMark)
            {
                return false;
            }

            var rowKey = ResolveMarkKeyAtRow(grid, row);
            if (rowKey > 0 && rowKey != key)
            {
                return HasNameTextOwnedByKey(grid, key, row, rowTop, blockEnd);
            }

            return true;
        }

        private static void LogNameSectionRowSkip(
            ScopeGridResult grid,
            int key,
            int row,
            int partCount)
        {
            if (!SpecDiagPolicy.ShouldTraceName(grid, key, false, partCount, 0))
            {
                return;
            }

            if (!grid.KeyToRowMark.TryGetValue(key, out var rowMark))
            {
                return;
            }

            var rowTop = ResolveNameRowTopForKey(grid, key);
            var blockEnd = grid.KeyToMarkBlockEnd.TryGetValue(key, out var be)
                ? be
                : GetMarkBlockEndExclusive(grid, rowTop, key);
            SpecGridLog.WriteTrace(
                "NAME",
                $"scope={grid.ScopeIndex} key={key} skip=section-row r={row} rowTop={rowTop} rowMark={rowMark} blockEnd={blockEnd}");
        }

        private static bool IsSectionHeaderRow(ScopeGridResult grid, int row)
        {
            if (grid.ColMark < 0 || grid.ColName < 0)
            {
                return false;
            }

            if (grid.CellText != null && row >= 0 && row < grid.CellText.GetLength(0))
            {
                var cols = grid.CellText.GetLength(1);
                for (var c = 0; c < cols; c++)
                {
                    var cell = (grid.CellText[row, c] ?? string.Empty).ToLowerInvariant();
                    if (cell.IndexOf("продолжен", StringComparison.Ordinal) >= 0
                        || cell.IndexOf("continuation", StringComparison.Ordinal) >= 0)
                    {
                        return true;
                    }
                }
            }

            var mark = grid.CellText[row, grid.ColMark] ?? string.Empty;
            if (MTextPlainText.TryParseMarkKey(mark, out _))
            {
                return false;
            }

            var name = GetTrimmedNameAtRow(grid, row);
            if (string.IsNullOrWhiteSpace(name) || name.Length < 8 || !MTextPlainText.HasLetter(name))
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

        private static void AssignCellsHeader(List<TextSample> texts, double[] xs, double[] ys, SpecGridLog log, int scopeIndex)
        {
            var alignFallback = 0;
            var extentsFallback = 0;
            foreach (var t in texts)
            {
                t.X = t.HeaderX;
                t.Y = t.HeaderY;
                if (CellIndex.TryGetCellIndex(t.HeaderX, t.HeaderY, xs, ys, out var row, out var col))
                {
                    t.Row = row;
                    t.Col = col;
                }
                else if (CellIndex.TryGetCellIndex(t.AlignX, t.AlignY, xs, ys, out row, out col))
                {
                    t.X = t.AlignX;
                    t.Y = t.AlignY;
                    t.Row = row;
                    t.Col = col;
                    alignFallback++;
                }
                else if (CellIndex.TryGetCellIndex(t.DataX, t.DataY, xs, ys, out row, out col))
                {
                    t.X = t.DataX;
                    t.Y = t.DataY;
                    t.Row = row;
                    t.Col = col;
                    extentsFallback++;
                }
                else
                {
                    t.Row = -1;
                    t.Col = -1;
                }
            }

            if (alignFallback > 0 || extentsFallback > 0)
            {
                log?.Info(
                    $"TABLE-GRID: scope={scopeIndex} AssignCellsHeader fallback: AlignmentPoint={alignFallback} ExtentsTop={extentsFallback}");
            }
        }

        private static void AssignCellsData(List<TextSample> texts, double[] xs, double[] ys, ScopeGridResult result)
        {
            foreach (var t in texts)
            {
                var bindX = t.AlignX;
                var bindY = t.AlignY;
                if (!CellIndex.TryGetCellIndex(bindX, bindY, xs, ys, out var rowByPoint, out var col))
                {
                    bindX = t.DataX;
                    bindY = t.DataY;
                    if (!CellIndex.TryGetCellIndex(bindX, bindY, xs, ys, out rowByPoint, out col))
                    {
                        col = ResolveColumnByX(bindX, xs);
                        rowByPoint = -1;
                    }
                }

                t.X = bindX;
                t.Y = bindY;
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
                if (t.Row >= 0 && SpecDiagPolicy.ShouldTraceGeo(result))
                {
                    var kind = t.IsMText ? "MText" : "DBText";
                    var bindYMethod = t.IsMText ? "ExtentsTop" : "AlignmentPoint";
                    SpecGridLog.WriteTrace(
                        "GEO",
                        $"scope={result.ScopeIndex} {kind} src={t.SourceIndex} bindY={bindYMethod} row={t.Row} col={t.Col}");
                }
            }
        }

        /// <summary>MText/DBText вне сетки: привязка по DataX в полосе ColName и DataY к строке.</summary>
        private static int AssignUnassignedTextsToNameColumn(
            List<TextSample> texts,
            double[] xs,
            double[] ys,
            ScopeGridResult result,
            SpecGridLog log)
        {
            if (texts == null || result == null || result.ColName < 0 || xs == null || ys == null || xs.Length < 2 || ys.Length < 2)
            {
                return 0;
            }

            var dataStart = result.RowDataStart > 0 ? result.RowDataStart : ResolveHeaderEndRow(result);
            var fixedCount = 0;
            foreach (var t in texts)
            {
                if (t == null || (t.Row >= 0 && t.Col >= 0))
                {
                    continue;
                }

                if (!IsTextInColumnXBand(result, result.ColName, t))
                {
                    continue;
                }

                if (LooksLikeDesignationText(GetDisplayText(t)))
                {
                    continue;
                }

                var row = t.Row;
                if (row < 0)
                {
                    if (!CellIndex.TryGetCellIndex(t.DataX, t.DataY, xs, ys, out row, out _))
                    {
                        row = FindBestRowByExtent(t, ys, result);
                    }

                    if (row < 0 && t.DominantRow >= 0)
                    {
                        row = t.DominantRow;
                    }
                }

                if (row < dataStart)
                {
                    continue;
                }

                t.Col = result.ColName;
                t.Row = row;
                t.DominantRow = CellIndex.GetDominantRow(t, ys, result);
                fixedCount++;
                var fixLine =
                    $"unassigned→name col={result.ColName} row={row} «{TrimForLog(GetDisplayText(t), 40)}»";
                log?.Info($"TABLE-GRID: scope={result.ScopeIndex} {fixLine}");
                if (SpecDiagPolicy.ShouldTraceGeo(result))
                {
                    SpecGridLog.WriteTrace("GEO", $"scope={result.ScopeIndex} {fixLine}");
                }
                if (result.UnassignedNameFixLines.Count < 10)
                {
                    result.UnassignedNameFixLines.Add(fixLine);
                }
            }

            return fixedCount;
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
                if (t.Row < result.RowDataStart || !IsTextInNameColumn(result, t))
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
            if (TryGetMTextBounds(mt, out headerPt, out dataPt, out yMin, out yMax, out method))
            {
                // headerPt / dataPt / yMin / yMax set by helper
            }

            var sample = CreateTextSample(mt, mt.Contents, headerPt, dataPt, yMin, yMax, mt.TextHeight, sourceIndex);
            sample.BoundsMethod = method;
            LogCellAssign(log, scopeIndex, "MText", method, headerPt, (sample.Plain ?? string.Empty).Length);
            return sample;
        }

        /// <summary>Границы MText: GeometricExtents, затем GetBoundingPoints (AC 2016).</summary>
        private static bool TryGetMTextBounds(
            MText mt,
            out Point3d headerPt,
            out Point3d dataPt,
            out double yMin,
            out double yMax,
            out string method)
        {
            headerPt = mt.Location;
            dataPt = mt.Location;
            yMin = mt.Location.Y;
            yMax = mt.Location.Y;
            method = "Location";

            dataPt = mt.Location;

            try
            {
                var ex = mt.GeometricExtents;
                yMin = ex.MinPoint.Y;
                yMax = ex.MaxPoint.Y;
                headerPt = new Point3d(
                    (ex.MinPoint.X + ex.MaxPoint.X) * 0.5,
                    yMax,
                    mt.Location.Z);
                dataPt = new Point3d(
                    (ex.MinPoint.X + ex.MaxPoint.X) * 0.5,
                    yMax,
                    mt.Location.Z);
                method = "ExtentsTop";
                return true;
            }
            catch
            {
                // fall through — часто на AC 2016 для отдельных MText
            }

            try
            {
                var pts = mt.GetBoundingPoints();
                if (pts == null || pts.Count < 1)
                {
                    return false;
                }

                var minX = pts[0].X;
                var maxX = pts[0].X;
                yMin = pts[0].Y;
                yMax = pts[0].Y;
                for (var i = 1; i < pts.Count; i++)
                {
                    var p = pts[i];
                    if (p.X < minX)
                    {
                        minX = p.X;
                    }

                    if (p.X > maxX)
                    {
                        maxX = p.X;
                    }

                    if (p.Y < yMin)
                    {
                        yMin = p.Y;
                    }

                    if (p.Y > yMax)
                    {
                        yMax = p.Y;
                    }
                }

                headerPt = new Point3d((minX + maxX) * 0.5, yMax, mt.Location.Z);
                dataPt = new Point3d((minX + maxX) * 0.5, yMax, mt.Location.Z);
                method = "ExtentsTop+BoundingPoints";
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static TextSample CreateTextSampleFromDbText(DBText db, int sourceIndex, int scopeIndex, SpecGridLog log)
        {
            var alignPt = GetDbTextPoint(db);
            var headerPt = alignPt;
            // pass2 row/col — по точке вставки (как OLD 2016); ExtentsTop только для YMin/YMax overlap.
            var dataPt = alignPt;
            var yMin = alignPt.Y;
            var yMax = alignPt.Y;
            const string method = "AlignmentPoint";
            try
            {
                var ex = db.GeometricExtents;
                yMin = ex.MinPoint.Y;
                yMax = ex.MaxPoint.Y;
            }
            catch
            {
                // пустой TEXT или GeometricExtents недоступен — только AlignmentPoint
            }

            var sample = CreateTextSample(db, db.TextString, headerPt, dataPt, yMin, yMax, db.Height, sourceIndex);
            sample.AlignX = alignPt.X;
            sample.AlignY = alignPt.Y;
            sample.BoundsMethod = method;
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
                AlignX = dataPt.X,
                AlignY = dataPt.Y,
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
                .ThenByDescending(x => x.Len)
                .ThenBy(x => x.Layer, StringComparer.OrdinalIgnoreCase)
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

        private static ScopeGridResult BuildFromAcadTable(
            int scopeIndex,
            Table table,
            IReadOnlyList<ObjectId> objectIds,
            Transaction tr,
            SpecGridLog log)
        {
            var result = new ScopeGridResult
            {
                ScopeIndex = scopeIndex,
                IsNativeAcadTable = true,
                NativeTableId = table.ObjectId
            };

            var rows = (int)table.Rows.Count;
            var cols = (int)table.Columns.Count;
            if (rows <= 0 || cols <= 0)
            {
                log.Warn($"TABLE-GRID: scope={scopeIndex} native Table empty ({rows}x{cols})");
                return result;
            }

            if (rows * cols > MaxCells)
            {
                log.Warn($"TABLE-GRID: scope={scopeIndex} native Table cells>{MaxCells}");
                return result;
            }

            result.CellText = new string[rows, cols];
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    result.CellText[r, c] = GetAcadTableCellText(table, r, c);
                }
            }

            result.RowDataEnd = rows - 1;
            result.HeaderEndRow = Math.Min(HeaderScanMaxRow + 1, rows);
            DetectHeaderFromCellMatrix(result, log);
            result.RowDataStart = FindFirstMarkRowInCellMatrix(result);
            if (result.RowDataStart > 0)
            {
                result.HeaderEndRow = result.RowDataStart;
            }

            BindKeysFromAcadTableCellMatrix(result, table, log);
            FillMarkNamesFromAcadTableCells(result, log);

            result.PickedObjectIds = objectIds.Where(id => !id.IsNull && id.IsValid).Distinct().ToList();
            if (!table.OwnerId.IsNull)
            {
                result.OwnerBlockId = table.OwnerId;
            }

            result.Valid = result.ColMark >= 0 && result.KeyToRowMark.Count > 0;
            log.Info(
                $"TABLE-GRID: scope={scopeIndex} native AcadTable {rows}x{cols} MARK={result.ColMark} NAME={result.ColName} QTY={result.ColQty} keys={result.KeyToRowMark.Count}");
            return result;
        }

        private static string GetAcadTableCellText(Table table, int row, int col)
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

        private static void DetectHeaderFromCellMatrix(ScopeGridResult result, SpecGridLog log)
        {
            if (result?.CellText == null)
            {
                return;
            }

            var rows = result.CellText.GetLength(0);
            var colCount = result.CellText.GetLength(1);
            if (colCount <= 0)
            {
                return;
            }

            var markScores = new int[colCount];
            var nameScores = new int[colCount];
            var qtyScores = new int[colCount];
            var headerRows = Math.Min(HeaderScanMaxRow + 1, rows);

            for (var r = 0; r < headerRows; r++)
            {
                for (var c = 0; c < colCount; c++)
                {
                    var header = MTextPlainText.SanitizeRawContents(result.CellText[r, c] ?? string.Empty)
                        .ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(header))
                    {
                        continue;
                    }

                    markScores[c] += ScoreHeader(header, "марка", "поз", "поз.", "mark", "п/п", "№", "номер", "item");
                    nameScores[c] += ScoreHeader(header, "наимен", "name", "назван", "наименование");
                    qtyScores[c] += ScoreQtyHeader(header);
                }
            }

            EnsureUniqueHeaderColumns(result, markScores, nameScores, qtyScores, log);
            SanitizeColQtyColumn(result, qtyScores, log);
            RefineColMarkByDataMarks(result, markScores, nameScores, qtyScores, log);
            log?.Info(
                $"TABLE-GRID: scope={result.ScopeIndex} native header cols: MARK={result.ColMark} QTY={result.ColQty} NAME={result.ColName}");
        }

        private static int FindFirstMarkRowInCellMatrix(ScopeGridResult result)
        {
            if (result?.CellText == null || result.ColMark < 0)
            {
                return 0;
            }

            var rows = result.CellText.GetLength(0);
            for (var r = 0; r < rows; r++)
            {
                var mark = result.CellText[r, result.ColMark] ?? string.Empty;
                if (MTextPlainText.TryParseMarkKey(mark, out _))
                {
                    return r;
                }
            }

            return Math.Min(1, rows - 1);
        }

        private static void BindKeysFromAcadTableCellMatrix(ScopeGridResult result, Table table, SpecGridLog log)
        {
            result.KeyToRowMark.Clear();
            result.RowToKeyMark.Clear();
            result.KeyToRowTopSub.Clear();
            result.KeyToMarkBlockEnd.Clear();
            if (result.ColMark < 0 || result.CellText == null)
            {
                return;
            }

            var rows = result.CellText.GetLength(0);
            for (var r = result.RowDataStart; r < rows; r++)
            {
                if (IsSectionHeaderRow(result, r))
                {
                    continue;
                }

                var cell = result.CellText[r, result.ColMark] ?? string.Empty;
                if (!MTextPlainText.TryParseMarkKey(cell, out var key))
                {
                    continue;
                }

                BindKeyToRow(result, key, r);
                result.RowToKeyMark[r] = key;

                var rowTopRaw = r;
                TryGetAcadTableMergeTopRow(table, r, result.ColMark, out rowTopRaw);
                result.KeyToRowTopSub[key] = ResolveNameRowTopForKey(result, key, rowTopRaw);
                log.Debug($"TABLE-GRID: scope={result.ScopeIndex} native bind key={key} row={r} rowTop={result.KeyToRowTopSub[key]}");
            }

            foreach (var key in result.KeyToRowMark.Keys.ToList())
            {
                var rowTop = result.KeyToRowTopSub.TryGetValue(key, out var rt) ? rt : result.KeyToRowMark[key];
                result.KeyToMarkBlockEnd[key] = GetMarkBlockEndExclusive(result, rowTop, key);
            }
        }

        private static void FillMarkNamesFromAcadTableCells(ScopeGridResult result, SpecGridLog log)
        {
            result.MarkNamePairs.Clear();
            if (result.ColMark < 0 || result.CellText == null)
            {
                return;
            }

            log.Info(
                $"TABLE-ROWS: scope={result.ScopeIndex} native startRow={result.RowDataStart} colMark={result.ColMark} colName={result.ColName}");
            var missing = 0;
            foreach (var kv in result.KeyToRowMark.OrderBy(x => x.Key))
            {
                var key = kv.Key;
                var joined = ResolveNameForKey(result, key, log, out _);
                result.MarkNamePairs[key] = joined;
                if (string.IsNullOrWhiteSpace(joined))
                {
                    missing++;
                    log.Info($"TABLE-ROWS: key={key} → name=\"\" MISSING_NAME");
                }
                else
                {
                    log.Info($"TABLE-ROWS: key={key} → name=\"{joined}\" (native-table)");
                    log.Info($"[KV-PAIR] scope={result.ScopeIndex} key={key} value=\"{TrimForLog(joined, 80)}\"");
                }
            }

            log.Success(
                $"TABLE-ROWS: scope={result.ScopeIndex} native pairs={result.MarkNamePairs.Count} missingName={missing}");
        }

        private static bool TryGetAcadTableMergeTopRow(Table table, int row, int col, out int topRow)
        {
            topRow = row;
            if (table == null || row < 0 || col < 0)
            {
                return false;
            }

            try
            {
                var range = table.Cells[row, col].GetMergeRange();
                topRow = range.TopRow;
                return true;
            }
            catch
            {
                return true;
            }
        }
    }
}
