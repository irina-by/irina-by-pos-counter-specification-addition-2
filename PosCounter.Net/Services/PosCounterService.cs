using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using PosCounter.Net.Models;

namespace PosCounter.Net.Services
{
    public class PosCounterService
    {
        private static readonly string[] Prefixes =
        {
            "Позиция", "позиция", "Номер", "номер", "Марка", "марка",
            "Поз.", "Поз", "поз.", "поз",
            "POS", "Pos", "pos", "Pos.", "pos.",
            "Item", "item", "N.", "n.", "№", "N", "n", "P", "p"
        };

        private static readonly char[] InvalidChars = { '.', ',', '+', '-', 'x', 'X', 'х', 'Х', '/' };

        public List<string> GetAllLayers()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return new List<string>();
            }

            var layers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var layerTable = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId layerId in layerTable)
                {
                    var layer = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
                    layers.Add(layer.Name);
                }

                tr.Commit();
            }

            // IMPORTANT:
            // - Positions are often stored in blocks/attributes, not as top-level DBText/MText in ModelSpace.
            // - Returning only "text layers" leads to a single layer "0" in many drawings.
            // So we always expose ALL layers to the UI, and (optionally) could prioritize text layers later.
            var result = layers.ToList();
            result.Sort(StringComparer.OrdinalIgnoreCase);
            // Normalize common naming: some projects use zero "0. В выноски", others letter "O. В выноски".
            // Prefer whichever exists in the drawing.
            if (result.Contains("0. В выноски") && result[0] != "0. В выноски")
            {
                result.Remove("0. В выноски");
                result.Insert(0, "0. В выноски");
            }
            else if (result.Contains("O. В выноски") && result[0] != "O. В выноски")
            {
                result.Remove("O. В выноски");
                result.Insert(0, "O. В выноски");
            }

            return result;
        }

        public CountComputationResult Count(PosSettings settings, string selectedLayer)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return new CountComputationResult { Success = false, StatusMessage = "[ERROR] Нет активного документа." };
            }

            var result = new CountComputationResult
            {
                ViewMode = settings.ViewMode ?? "dwg",
                Scope = settings.LayerScope ?? "current",
                SelectedLayer = selectedLayer ?? string.Empty
            };

            return result.ViewMode == "viewport"
                ? CountByViewport(doc, settings, result)
                : CountByDwg(doc, settings, result);
        }

        public IEnumerable<string> BuildDisplayLines(CountComputationResult result)
        {
            if (result == null || !result.Success)
            {
                return Enumerable.Empty<string>();
            }

            if (result.ViewMode == "viewport")
            {
                return BuildViewportLines(result);
            }

            if (result.Scope == "all")
            {
                return BuildAllLayerLines(result.AllLayerPositions);
            }

            return BuildCurrentLayerLines(result.SelectedLayer, result.CurrentLayerPositions);
        }

        public int ShowOnDrawing(CountComputationResult result, PosSettings settings)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || result == null || !result.Success)
            {
                return 0;
            }

            if ((settings?.ViewMode ?? "dwg") != "dwg")
            {
                doc.Editor.WriteMessage("\n[POS] [INFO] Функция ПОКАЗАТЬ НА ЧЕРТЕЖЕ доступна только в режиме По DWG.");
                return 0;
            }

            using (doc.LockDocument())
            {
                var ids = result.HighlightObjectIds
                    .Where(id => !id.IsNull && id.IsValid)
                    .Distinct()
                    .ToArray();

                if (ids.Length == 0)
                {
                    doc.Editor.SetImpliedSelection(ArrayCompat.Empty<ObjectId>());
                    return 0;
                }

                doc.Editor.SetImpliedSelection(ids);
                return ids.Length;
            }
        }

        private CountComputationResult CountByDwg(Document doc, PosSettings settings, CountComputationResult result)
        {
            using (doc.LockDocument())
            {
                var selected = TryResolveSelection(doc.Editor);
                var mode = settings.Mode ?? "layer";

                if (mode == "selection")
                {
                    if (selected == null || selected.Length == 0)
                    {
                        result.Success = false;
                        result.StatusMessage = "[INFO] Включен режим по выделению, но объекты не выбраны.";
                        return result;
                    }

                    var counts = RunCount(doc, selected, result.SelectedLayer, result.Scope, includeModelSpaceFallback: false);
                    if (HasAnyCounts(counts))
                    {
                        FillResultFromAccumulator(result, counts);
                        result.Success = true;
                        result.StatusMessage = "[OK] Подсчет по выделенным объектам выполнен.";
                        return result;
                    }

                    // LISP-compatible behavior: if selection exists but gives no positions, fallback to full DWG scan.
                    result.WarningMessage = "В выделении нет подходящих позиций. Выполнен переход к подсчету по всему DWG.";
                }

                var allModelIds = CollectModelSpaceEntityIds(doc.Database);
                var dwgCounts = RunCount(doc, allModelIds, result.SelectedLayer, result.Scope, includeModelSpaceFallback: true);
                FillResultFromAccumulator(result, dwgCounts);
                result.Success = HasAnyCounts(dwgCounts);
                result.StatusMessage = result.Success
                    ? (result.Scope == "all"
                        ? "[OK] Подсчет по всем слоям выполнен."
                        : $"[OK] Найдены позиции на слое {result.SelectedLayer}.")
                    : $"[ERROR] На слое {result.SelectedLayer} подходящие позиции не найдены.";

                return result;
            }
        }

        private CountComputationResult CountByViewport(Document doc, PosSettings settings, CountComputationResult result)
        {
            using (doc.LockDocument())
            {
                var vpInfo = GetActiveViewportInfo(doc);
                if (vpInfo == null)
                {
                    result.Success = false;
                    result.StatusMessage = "[INFO] Active viewport not found. Activate viewport and retry.";
                    return result;
                }

                var sourceIds = ArrayCompat.Empty<ObjectId>();
                var mode = settings.Mode ?? "layer";
                if (mode == "selection")
                {
                    sourceIds = TryResolveSelection(doc.Editor);
                    if (sourceIds == null || sourceIds.Length == 0)
                    {
                        var prompt = new PromptSelectionOptions
                        {
                            MessageForAdding = "\nВыберите объекты в активном viewport:"
                        };
                        var selRes = doc.Editor.GetSelection(prompt);
                        if (selRes.Status != PromptStatus.OK)
                        {
                            result.Success = false;
                            result.StatusMessage = "[INFO] Выделение для режима viewport отменено.";
                            return result;
                        }

                        sourceIds = selRes.Value.GetObjectIds();
                    }
                }
                else
                {
                    var selectedByPolygon = TrySelectInViewportPolygon(doc.Editor);
                    sourceIds = selectedByPolygon.Item1;
                    result.UsedViewportPolygonSelection = selectedByPolygon.Item2;
                    if (sourceIds.Length == 0)
                    {
                        result.WarningMessage = "Полигональный отбор viewport не вернул объекты. Результаты могут быть пустыми.";
                    }
                }

                var counts = RunCount(doc, sourceIds, result.SelectedLayer, result.Scope, includeModelSpaceFallback: false);
                if (!HasAnyCounts(counts))
                {
                    result.Success = false;
                    result.StatusMessage = "[INFO] В активном viewport подходящие позиции не найдены.";
                    return result;
                }

                var vpResult = new ViewportResult
                {
                    LayoutName = vpInfo.LayoutName,
                    ViewportHandle = vpInfo.Handle
                };

                if (result.Scope == "all")
                {
                    vpResult.AllLayerPositions = ConvertAllLayerCounts(counts.AllCounts);
                }
                else
                {
                    vpResult.LayerName = result.SelectedLayer;
                    vpResult.CurrentLayerPositions = ConvertCurrentCounts(counts.CurrentCounts);
                }

                result.ViewportResults = new List<ViewportResult> { vpResult };
                result.HighlightObjectIds = counts.HighlightObjectIds.ToList();
                result.Success = true;
                result.StatusMessage = "[OK] Подсчет для активного viewport выполнен.";

                if (!result.UsedViewportPolygonSelection && mode == "layer")
                {
                    result.WarningMessage = "Использован приближенный фильтр viewport. Рекомендуется визуальная проверка.";
                }

                return result;
            }
        }

        private static ObjectId[] CollectModelSpaceEntityIds(Database db)
        {
            var ids = new List<ObjectId>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                ids.AddRange(model.Cast<ObjectId>());
                tr.Commit();
            }

            return ids.ToArray();
        }

        private static bool HasAnyCounts(CountAccumulator counts)
        {
            return counts != null
                && (counts.CurrentCounts.Count > 0 || counts.AllCounts.Count > 0);
        }

        private static ObjectId[] TryResolveSelection(Editor editor)
        {
            var implied = editor.SelectImplied();
            if (implied.Status == PromptStatus.OK)
            {
                return implied.Value.GetObjectIds();
            }

            return ArrayCompat.Empty<ObjectId>();
        }

        private CountAccumulator RunCount(
            Document doc,
            IEnumerable<ObjectId> sourceIds,
            string selectedLayer,
            string scope,
            bool includeModelSpaceFallback)
        {
            var accumulator = new CountAccumulator();
            var stackGuard = new HashSet<ObjectId>();

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (var id in sourceIds.Distinct())
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (entity == null)
                    {
                        continue;
                    }

                    ProcessEntity(entity, entity.Layer, selectedLayer, scope, accumulator, tr, stackGuard);
                }

                // Selection sets may skip nested entities inside blocks in some cases; model-space fallback keeps behavior stable.
                if (includeModelSpaceFallback)
                {
                    // No-op: explicit fallback is handled by caller through model-space sourceIds.
                }

                tr.Commit();
            }

            return accumulator;
        }

        private void ProcessEntity(
            Entity entity,
            string effectiveLayer,
            string selectedLayer,
            string scope,
            CountAccumulator accumulator,
            Transaction tr,
            HashSet<ObjectId> stackGuard)
        {
            switch (entity)
            {
                case AttributeReference attr:
                    ProcessTextValue(attr.TextString, effectiveLayer, selectedLayer, scope, accumulator, attr.ObjectId);
                    break;
                case DBText dbText:
                    ProcessTextValue(dbText.TextString, effectiveLayer, selectedLayer, scope, accumulator, dbText.ObjectId);
                    break;
                case MText mText:
                    ProcessTextValue(mText.Contents, effectiveLayer, selectedLayer, scope, accumulator, mText.ObjectId);
                    break;
                case BlockReference br:
                    ProcessBlockReference(br, effectiveLayer, selectedLayer, scope, accumulator, tr, stackGuard);
                    break;
            }
        }

        private void ProcessBlockReference(
            BlockReference blockRef,
            string insertLayer,
            string selectedLayer,
            string scope,
            CountAccumulator accumulator,
            Transaction tr,
            HashSet<ObjectId> stackGuard)
        {
            // Attribute references on each block insert can contain position numbers.
            foreach (ObjectId attrId in blockRef.AttributeCollection)
            {
                var attr = tr.GetObject(attrId, OpenMode.ForRead, false) as AttributeReference;
                if (attr == null)
                {
                    continue;
                }

                var attrLayer = ResolveLayer(attr.Layer, insertLayer);
                ProcessTextValue(attr.TextString, attrLayer, selectedLayer, scope, accumulator, attr.ObjectId);
            }

            ProcessBlockDefinition(blockRef.BlockTableRecord, insertLayer, selectedLayer, scope, accumulator, tr, stackGuard);
        }

        private void ProcessBlockDefinition(
            ObjectId blockRecordId,
            string insertLayer,
            string selectedLayer,
            string scope,
            CountAccumulator accumulator,
            Transaction tr,
            HashSet<ObjectId> stackGuard)
        {
            if (blockRecordId.IsNull || stackGuard.Contains(blockRecordId))
            {
                return;
            }

            stackGuard.Add(blockRecordId);
            try
            {
                var btr = tr.GetObject(blockRecordId, OpenMode.ForRead, false) as BlockTableRecord;
                if (btr == null)
                {
                    return;
                }

                foreach (ObjectId entId in btr)
                {
                    var entity = tr.GetObject(entId, OpenMode.ForRead, false) as Entity;
                    if (entity == null)
                    {
                        continue;
                    }

                    var nestedLayer = ResolveLayer(entity.Layer, insertLayer);
                    if (entity is AttributeDefinition attDef)
                    {
                        ProcessTextValue(attDef.TextString, nestedLayer, selectedLayer, scope, accumulator, ObjectId.Null);
                        continue;
                    }

                    if (entity is BlockReference nestedRef)
                    {
                        ProcessBlockDefinition(nestedRef.BlockTableRecord, nestedLayer, selectedLayer, scope, accumulator, tr, stackGuard);
                        continue;
                    }

                    ProcessEntity(entity, nestedLayer, selectedLayer, scope, accumulator, tr, stackGuard);
                }
            }
            finally
            {
                stackGuard.Remove(blockRecordId);
            }
        }

        private static string ResolveLayer(string entityLayer, string insertLayer)
        {
            if (string.Equals(entityLayer, "0", StringComparison.OrdinalIgnoreCase))
            {
                return insertLayer ?? "0";
            }

            return entityLayer ?? string.Empty;
        }

        private void ProcessTextValue(
            string textValue,
            string entityLayer,
            string selectedLayer,
            string scope,
            CountAccumulator accumulator,
            ObjectId sourceId)
        {
            var position = ExtractPositionNumber(textValue);
            if (!position.HasValue)
            {
                return;
            }

            if (scope == "all")
            {
                var baseLayer = GetBaseLayer(entityLayer);
                if (!accumulator.AllCounts.TryGetValue(baseLayer, out var layerMap))
                {
                    layerMap = new Dictionary<int, int>();
                    accumulator.AllCounts[baseLayer] = layerMap;
                }

                IncrementCount(layerMap, position.Value);
                if (!sourceId.IsNull)
                {
                    accumulator.HighlightObjectIds.Add(sourceId);
                }

                return;
            }

            if (!LayerMatches(entityLayer, selectedLayer))
            {
                return;
            }

            IncrementCount(accumulator.CurrentCounts, position.Value);
            if (!sourceId.IsNull)
            {
                accumulator.HighlightObjectIds.Add(sourceId);
            }
        }

        private static void IncrementCount(IDictionary<int, int> map, int position)
        {
            if (!map.ContainsKey(position))
            {
                map[position] = 0;
            }

            map[position]++;
        }

        public int? ExtractPositionNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var clean = text.Trim();
            foreach (var prefix in Prefixes.OrderByDescending(p => p.Length))
            {
                if (clean.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    clean = clean.Substring(prefix.Length).Trim();
                    break;
                }
            }

            if (clean.Length == 0 || clean.IndexOfAny(InvalidChars) >= 0)
            {
                return null;
            }

            if (!clean.All(char.IsDigit))
            {
                return null;
            }

            if (!int.TryParse(clean, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                return null;
            }

            return value >= 1 && value <= 10000 ? value : (int?)null;
        }

        private static bool LayerMatches(string entityLayer, string selectedLayer)
        {
            return string.Equals(GetBaseLayer(entityLayer), selectedLayer, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetBaseLayer(string layer)
        {
            if (string.IsNullOrWhiteSpace(layer))
            {
                return string.Empty;
            }

            var pipe = layer.IndexOf('|');
            return pipe >= 0 && pipe < layer.Length - 1
                ? layer.Substring(pipe + 1)
                : layer;
        }

        private static void FillResultFromAccumulator(CountComputationResult result, CountAccumulator counts)
        {
            if (result.Scope == "all")
            {
                result.AllLayerPositions = ConvertAllLayerCounts(counts.AllCounts);
            }
            else
            {
                result.CurrentLayerPositions = ConvertCurrentCounts(counts.CurrentCounts);
            }

            result.HighlightObjectIds = counts.HighlightObjectIds.Distinct().ToList();
        }

        private static List<LayerResult> ConvertAllLayerCounts(Dictionary<string, Dictionary<int, int>> all)
        {
            return all
                .OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
                .Select(k => new LayerResult
                {
                    LayerName = k.Key,
                    Positions = ConvertCurrentCounts(k.Value)
                })
                .ToList();
        }

        private static List<PositionCount> ConvertCurrentCounts(Dictionary<int, int> current)
        {
            return current
                .OrderBy(k => k.Key)
                .Select(k => new PositionCount { Position = k.Key, Count = k.Value })
                .ToList();
        }

        private static IEnumerable<string> BuildCurrentLayerLines(string layerName, List<PositionCount> positions)
        {
            var lines = new List<string>();
            var total = positions.Sum(p => p.Count);
            if (total == 0)
            {
                total = 1;
            }

            lines.Add($"РЕЗУЛЬТАТЫ ДЛЯ СЛОЯ: {layerName}");
            lines.Add("========================================");
            lines.Add("Позиция   Кол-во   Слой                 Процент");
            lines.Add("----------------------------------------------");
            foreach (var item in positions)
            {
                var percent = 100.0 * item.Count / total;
                lines.Add($"{item.Position,6}   {item.Count,5}   {layerName,-20} {percent,6:0.00}%");
            }

            lines.Add("----------------------------------------------");
            lines.Add($"ИТОГО: {positions.Sum(p => p.Count)}");
            return lines;
        }

        private static IEnumerable<string> BuildAllLayerLines(List<LayerResult> layers)
        {
            var lines = new List<string>();
            foreach (var layer in layers)
            {
                var total = layer.Positions.Sum(p => p.Count);
                if (total == 0)
                {
                    total = 1;
                }

                lines.Add($"СЛОЙ: {layer.LayerName}");
                lines.Add("Позиция   Кол-во   Слой                 Процент");
                lines.Add("----------------------------------------------");
                foreach (var item in layer.Positions)
                {
                    var percent = 100.0 * item.Count / total;
                    lines.Add($"{item.Position,6}   {item.Count,5}   {layer.LayerName,-20} {percent,6:0.00}%");
                }

                lines.Add("----------------------------------------------");
                lines.Add($"ИТОГО ПО СЛОЮ '{layer.LayerName}': {layer.Positions.Sum(p => p.Count)}");
                lines.Add(string.Empty);
            }

            return lines;
        }

        private static IEnumerable<string> BuildViewportLines(CountComputationResult result)
        {
            var lines = new List<string>();
            foreach (var viewport in result.ViewportResults)
            {
                lines.Add($"ЛИСТ: {viewport.LayoutName} | VIEWPORT: {viewport.ViewportHandle}");
                lines.Add("========================================");
                if (result.Scope == "all")
                {
                    lines.AddRange(BuildAllLayerLines(viewport.AllLayerPositions));
                }
                else
                {
                    lines.AddRange(BuildCurrentLayerLines(viewport.LayerName, viewport.CurrentLayerPositions));
                }

                lines.Add("==================================================");
                lines.Add(string.Empty);
            }

            return lines;
        }

        private ViewportInfo GetActiveViewportInfo(Document doc)
        {
            var layoutName = Convert.ToString(Application.GetSystemVariable("CTAB"));
            var cvport = Convert.ToInt32(Application.GetSystemVariable("CVPORT"));
            if (cvport <= 1 || string.IsNullOrWhiteSpace(layoutName))
            {
                return null;
            }

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var viewport = FindViewportByNumber(doc.Database, tr, layoutName, cvport);
                if (viewport == null)
                {
                    return null;
                }

                var info = new ViewportInfo
                {
                    LayoutName = layoutName,
                    Cvport = cvport,
                    Handle = viewport.Handle.ToString()
                };
                tr.Commit();
                return info;
            }
        }

        private static Viewport FindViewportByNumber(Database db, Transaction tr, string layoutName, int cvport)
        {
            var layoutDict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
            if (!layoutDict.Contains(layoutName))
            {
                return null;
            }

            var layout = (Layout)tr.GetObject(layoutDict.GetAt(layoutName), OpenMode.ForRead);
            var btr = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);
            foreach (ObjectId id in btr)
            {
                if (!(tr.GetObject(id, OpenMode.ForRead) is Viewport viewport))
                {
                    continue;
                }

                if (viewport.Number == cvport)
                {
                    return viewport;
                }
            }

            return null;
        }

        private Tuple<ObjectId[], bool> TrySelectInViewportPolygon(Editor editor)
        {
            try
            {
                var polygon = BuildActiveViewportPolygon(editor);
                if (polygon == null || polygon.Count < 3)
                {
                    return Tuple.Create(ArrayCompat.Empty<ObjectId>(), false);
                }

                var filter = new SelectionFilter(new[]
                {
                    new TypedValue((int)DxfCode.Start, "TEXT,MTEXT,INSERT,ATTRIB")
                });

                var crossing = editor.SelectCrossingPolygon(polygon, filter);
                if (crossing.Status == PromptStatus.OK)
                {
                    return Tuple.Create(crossing.Value.GetObjectIds(), true);
                }

                var window = editor.SelectWindowPolygon(polygon, filter);
                if (window.Status == PromptStatus.OK)
                {
                    return Tuple.Create(window.Value.GetObjectIds(), true);
                }

                return Tuple.Create(ArrayCompat.Empty<ObjectId>(), false);
            }
            catch
            {
                return Tuple.Create(ArrayCompat.Empty<ObjectId>(), false);
            }
        }

        private static Point3dCollection BuildActiveViewportPolygon(Editor editor)
        {
            using (var view = editor.GetCurrentView())
            {
                var halfWidth = view.Width * 0.5;
                var halfHeight = view.Height * 0.5;
                var c = view.CenterPoint;
                var dcs = new[]
                {
                    new Point3d(c.X - halfWidth, c.Y - halfHeight, 0),
                    new Point3d(c.X + halfWidth, c.Y - halfHeight, 0),
                    new Point3d(c.X + halfWidth, c.Y + halfHeight, 0),
                    new Point3d(c.X - halfWidth, c.Y + halfHeight, 0),
                    new Point3d(c.X - halfWidth, c.Y - halfHeight, 0)
                };

                var matrix = DcsToWcs(view);
                var points = new Point3dCollection();
                foreach (var p in dcs)
                {
                    points.Add(p.TransformBy(matrix));
                }

                return points;
            }
        }

        private static Matrix3d DcsToWcs(ViewTableRecord view)
        {
            var wcsToDcs =
                Matrix3d.WorldToPlane(view.ViewDirection) *
                Matrix3d.Displacement(Point3d.Origin - view.Target) *
                Matrix3d.Rotation(view.ViewTwist, view.ViewDirection, view.Target);

            return wcsToDcs.Inverse();
        }

        private sealed class CountAccumulator
        {
            public Dictionary<int, int> CurrentCounts { get; } = new Dictionary<int, int>();
            public Dictionary<string, Dictionary<int, int>> AllCounts { get; } = new Dictionary<string, Dictionary<int, int>>(StringComparer.OrdinalIgnoreCase);
            public List<ObjectId> HighlightObjectIds { get; } = new List<ObjectId>();
        }

        private sealed class ViewportInfo
        {
            public string LayoutName { get; set; }
            public int Cvport { get; set; }
            public string Handle { get; set; }
        }
    }
}
