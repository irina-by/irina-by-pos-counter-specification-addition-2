using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using PosCounter.Net.Models;
using PosCounter.Net;

namespace PosCounter.Net.Engine
{
    public sealed class PosCounterEngine
    {
        // Legacy "Pos_counter" behavior: count positional numbers, not raw text.
        // MLeader is intentionally disabled on first stage (legacy did not use it).
        private static readonly string[] Prefixes =
        {
            "Позиция", "позиция", "Номер", "номер", "Марка", "марка",
            "Поз.", "Поз", "поз.", "поз",
            "POS", "Pos", "pos", "Pos.", "pos.",
            "Item", "item", "N.", "n.", "№", "N", "n", "P", "p"
        };

        private static readonly char[] InvalidChars = { '.', ',', '+', '-', 'x', 'X', 'х', 'Х', '/' };

        public sealed class PosCountResult
        {
            public IReadOnlyList<PosRow> Rows { get; set; } = ArrayCompat.Empty<PosRow>();
            public bool UsedViewportSelection { get; set; }
            public bool IncludedPaperSpace { get; set; }
            public string SourceDescription { get; set; }
            public int SourceObjectCount { get; set; }
            public string DebugSummary { get; set; }
            public int GeoCircleCount { get; set; }
            public long CountElapsedMs { get; set; }
            public int SeenDigits { get; set; }
            public int RejectC4 { get; set; }
            public int LayerCount { get; set; }
            public string LayerSample { get; set; }

            /// <summary>
            /// Deep-copy rows for cross-thread UI handoff (no shared list instances with the engine run).
            /// </summary>
            public PosCountResult CloneDetached()
            {
                var list = new List<PosRow>();
                foreach (var r in Rows ?? ArrayCompat.Empty<PosRow>())
                {
                    if (r == null)
                    {
                        continue;
                    }

                    list.Add(new PosRow
                    {
                        Text = r.Text ?? string.Empty,
                        Layer = r.Layer ?? string.Empty,
                        Count = r.Count,
                        Key = r.Key,
                        NameFromSpec = r.NameFromSpec ?? string.Empty,
                        NameSource = r.NameSource ?? string.Empty,
                        SourceHandles = r.SourceHandles != null
                            ? new List<string>(r.SourceHandles)
                            : new List<string>()
                    });
                }

                return new PosCountResult
                {
                    Rows = list,
                    UsedViewportSelection = UsedViewportSelection,
                    IncludedPaperSpace = IncludedPaperSpace,
                    SourceDescription = SourceDescription,
                    SourceObjectCount = SourceObjectCount,
                    DebugSummary = DebugSummary,
                    GeoCircleCount = GeoCircleCount,
                    CountElapsedMs = CountElapsedMs,
                    SeenDigits = SeenDigits,
                    RejectC4 = RejectC4,
                    LayerCount = LayerCount,
                    LayerSample = LayerSample
                };
            }
        }

        private sealed class CountDiagnostics
        {
            public readonly CalloutMarkGate.GateStats GateStats = new CalloutMarkGate.GateStats();
            public readonly Dictionary<string, int> AcceptedByLayer = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public int SeenDigits;

            public void RecordAccepted(string layer)
            {
                var key = layer ?? string.Empty;
                if (!AcceptedByLayer.TryGetValue(key, out var n))
                {
                    n = 0;
                }

                AcceptedByLayer[key] = n + 1;
            }
        }

        public IReadOnlyList<PosRow> Count(bool countAllInModel)
        {
            return CountWithInfo(countAllInModel).Rows;
        }

        public PosCountResult CountWithInfo(bool countAllInModel, bool extractNumbersOnly = true)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return new PosCountResult { Rows = ArrayCompat.Empty<PosRow>() };
            }

            var result = new PosCountResult();
            Accumulator acc = null;
            var typeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var samples = new List<string>();
            var sw = Stopwatch.StartNew();

            using (doc.LockDocument())
            {
                ObjectId[] sourceIds;

                // IMPORTANT UX RULE:
                // If user explicitly selected objects in AutoCAD (PickFirst / implied selection),
                // we ALWAYS count only those objects, regardless of the "all in model" checkbox.
                // Checkbox is only used when there is NO selection.
                ObjectId[] impliedSelection;
                try
                {
                    if (PaletteHost.TryConsumePendingPickFirst(doc, out var cached) && cached != null && cached.Length > 0)
                    {
                        impliedSelection = cached;
                    }
                    else
                    {
                        impliedSelection = TryResolveSelection(doc.Editor);
                    }
                }
                catch
                {
                    impliedSelection = ArrayCompat.Empty<ObjectId>();
                }

                // Palette-driven UX (required):
                // - If checkbox OFF: count ONLY already selected objects (PickFirst). Never prompt selection interactively.
                // - If checkbox ON: auto-scan like legacy project:
                //   - In Model tab: scan ModelSpace
                //   - In Layout with active viewport: select by active viewport polygon
                if (impliedSelection != null && impliedSelection.Length > 0)
                {
                    sourceIds = impliedSelection;
                    result.SourceDescription = "выделение";
                    result.SourceObjectCount = sourceIds.Length;
                }
                else if (countAllInModel)
                {
                    if (IsInLayoutViewport())
                    {
                        var vp = TrySelectInViewportPolygon(doc.Editor);
                        sourceIds = vp.Item1 ?? ArrayCompat.Empty<ObjectId>();
                        result.UsedViewportSelection = vp.Item2;
                        result.IncludedPaperSpace = false;
                        result.SourceDescription = "активный viewport";
                        result.SourceObjectCount = sourceIds.Length;
                    }
                    else
                    {
                        sourceIds = CollectModelSpaceEntityIds(doc.Database);
                        result.SourceDescription = "вся модель";
                        result.SourceObjectCount = sourceIds.Length;
                    }
                }
                else
                {
                    sourceIds = impliedSelection ?? ArrayCompat.Empty<ObjectId>();
                    if (sourceIds.Length > 0)
                    {
                        result.SourceDescription = "выделение";
                        result.SourceObjectCount = sourceIds.Length;
                    }
                    else
                    {
                        result.SourceDescription = "нет выделения";
                        result.SourceObjectCount = 0;
                        result.Rows = ArrayCompat.Empty<PosRow>();
                        sw.Stop();
                        result.CountElapsedMs = sw.ElapsedMilliseconds;
                        return result;
                    }
                }

                if (sourceIds.Length == 0)
                {
                    result.Rows = ArrayCompat.Empty<PosRow>();
                    sw.Stop();
                    result.CountElapsedMs = sw.ElapsedMilliseconds;
                    return result;
                }

                acc = new Accumulator();
                var stackGuard = new HashSet<ObjectId>();
                var diag = new CountDiagnostics();

                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var geoIndex = CalloutMarkGate.BuildIndex(tr, sourceIds);
                    if (result.UsedViewportSelection)
                    {
                        var vpPolygon = BuildActiveViewportPolygon(doc.Editor);
                        CalloutMarkGate.PopulateViewportGeometry(doc.Editor, tr, geoIndex, vpPolygon);
                    }

                    foreach (var id in sourceIds.Distinct())
                    {
                        if (!IsUsableId(id))
                        {
                            continue;
                        }

                        if (!IsObjectIdFromActiveDocument(id, doc))
                        {
                            // Common hard-crash case: selection contains ids from another database (xref overlay, etc.)
                            continue;
                        }

                        // Legacy Pos_counter: open every entity in the source set; ProcessEntity only handles text/block types.
                        Entity entity;
                        try
                        {
                            entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                        }
                        catch
                        {
                            continue;
                        }

                        if (entity == null)
                        {
                            continue;
                        }

                        try
                        {
                            var tn = entity.GetType().Name;
                            if (!typeCounts.ContainsKey(tn)) typeCounts[tn] = 0;
                            typeCounts[tn]++;

                            if (samples.Count < 5)
                            {
                                var s = TryGetTextForDebug(entity, tr);
                                if (!string.IsNullOrWhiteSpace(s))
                                {
                                    samples.Add(s);
                                }
                            }
                        }
                        catch
                        {
                            // ignore debug collection
                        }

                        ProcessEntity(entity, entity.Layer, acc, tr, stackGuard, extractNumbersOnly, geoIndex, diag);
                    }

                    result.GeoCircleCount = geoIndex.Circles.Count;
                    ApplyDiagnostics(result, diag);

                    tr.Commit();
                }
            }

            sw.Stop();
            result.CountElapsedMs = sw.ElapsedMilliseconds;

            // Outside LockDocument: ToRows / UI prep only touches managed heaps; avoids host crashes seen right after a heavy command+lock.
            if (acc != null)
            {
                result.Rows = acc.ToRows();
            }

            if (result.Rows.Count == 0 && typeCounts.Count > 0)
            {
                var top = typeCounts.OrderByDescending(k => k.Value).Take(6).Select(k => $"{k.Key}={k.Value}");
                var samplePart = samples.Count > 0
                    ? $" | примеры: {string.Join(" ; ", samples.Select(s => s.Replace("\r", "").Replace("\n", "\\n")).Select(s => s.Length > 40 ? s.Substring(0, 40) + "…" : s))}"
                    : string.Empty;
                result.DebugSummary = $"типы: {string.Join(", ", top)}{samplePart}";
            }

            return result;
        }

        private static string TryGetTextForDebug(Entity entity, Transaction tr)
        {
            try
            {
                if (entity is DBText t)
                {
                    return t.TextString;
                }

                if (entity is MText mt)
                {
                    return mt.Contents;
                }

                if (entity is AttributeReference ar)
                {
                    return ar.TextString;
                }

                if (entity is BlockReference br)
                {
                    foreach (ObjectId aid in br.AttributeCollection)
                    {
                        var attr = tr.GetObject(aid, OpenMode.ForRead, false) as AttributeReference;
                        if (attr != null && !string.IsNullOrWhiteSpace(attr.TextString))
                        {
                            return attr.TextString;
                        }
                    }

                    return null;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static ObjectId[] TryResolveSelection(Editor editor)
        {
            try
            {
                try
                {
                    // Ensure preselection is enabled in AutoCAD.
                    // Some profiles disable it, which makes SelectImplied always empty.
                    Application.SetSystemVariable("PICKFIRST", 1);
                }
                catch
                {
                    // ignore
                }

                var implied = editor.SelectImplied();
                if (implied.Status == PromptStatus.OK)
                {
                    return implied.Value
                        .GetObjectIds()
                        .Where(IsUsableId)
                        .ToArray();
                }
            }
            catch
            {
                // ignored
            }

            return ArrayCompat.Empty<ObjectId>();
        }

        private static bool IsUsableId(ObjectId id)
        {
            try
            {
                if (id.IsNull || !id.IsValid)
                {
                    return false;
                }

                return !id.IsErased;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsObjectIdFromActiveDocument(ObjectId id, Document doc)
        {
            try
            {
                if (doc == null)
                {
                    return false;
                }

                return id.Database == doc.Database;
            }
            catch
            {
                return false;
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

        private static bool IsInLayoutViewport()
        {
            try
            {
                var layoutName = Convert.ToString(Application.GetSystemVariable("CTAB"));
                if (string.IsNullOrWhiteSpace(layoutName) || string.Equals(layoutName, "Model", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var cvport = Convert.ToInt32(Application.GetSystemVariable("CVPORT"));
                return cvport > 1;
            }
            catch
            {
                return false;
            }
        }

        private static Tuple<ObjectId[], bool> TrySelectInViewportPolygon(Editor editor)
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
                    // Legacy filter (NO MLEADER)
                    // Geometry for C1/C4 is loaded once per run (see CalloutMarkGate.PopulateViewportGeometry).
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

        private static void ProcessEntity(
            Entity entity,
            string effectiveLayer,
            Accumulator acc,
            Transaction tr,
            HashSet<ObjectId> stackGuard,
            bool extractNumbersOnly,
            CalloutMarkGate.GeoIndex geoIndex,
            CountDiagnostics diag)
        {
            switch (entity)
            {
                case AttributeReference attr:
                    ProcessTextValue(attr.TextString, effectiveLayer, acc, attr.ObjectId, extractNumbersOnly, geoIndex, attr.Position, attr.Height, diag);
                    break;
                case DBText dbText:
                    ProcessTextValue(dbText.TextString, effectiveLayer, acc, dbText.ObjectId, extractNumbersOnly, geoIndex, dbText.Position, dbText.Height, diag);
                    break;
                case MText mText:
                    // Legacy behavior: use Contents directly
                    ProcessTextValue(mText.Contents, effectiveLayer, acc, mText.ObjectId, extractNumbersOnly, geoIndex, mText.Location, mText.TextHeight, diag);
                    break;
                case BlockReference br:
                    ProcessBlockReference(br, effectiveLayer, acc, tr, stackGuard, extractNumbersOnly, geoIndex, diag);
                    break;
            }
        }

        private static void ProcessEntity(
            Entity entity,
            string effectiveLayer,
            Accumulator acc,
            Transaction tr,
            HashSet<ObjectId> stackGuard,
            bool extractNumbersOnly,
            ObjectId highlightSourceId,
            CalloutMarkGate.GeoIndex geoIndex,
            CountDiagnostics diag)
        {
            switch (entity)
            {
                case AttributeReference attr:
                    // AttributeReference belongs to the block instance; its own handle is safe to highlight.
                    ProcessTextValue(attr.TextString, effectiveLayer, acc, attr.ObjectId, extractNumbersOnly, geoIndex, attr.Position, attr.Height, diag);
                    break;
                case DBText dbText:
                    ProcessTextValue(dbText.TextString, effectiveLayer, acc, highlightSourceId, extractNumbersOnly, geoIndex, dbText.Position, dbText.Height, diag);
                    break;
                case MText mText:
                    // Legacy behavior: use Contents directly
                    ProcessTextValue(mText.Contents, effectiveLayer, acc, highlightSourceId, extractNumbersOnly, geoIndex, mText.Location, mText.TextHeight, diag);
                    break;
                case BlockReference br:
                    // BlockReference inside a block *definition* is not a real instance on the drawing.
                    // Keep binding highlight to the outer instance (highlightSourceId).
                    ProcessBlockDefinition(br.BlockTableRecord, effectiveLayer, acc, tr, stackGuard, extractNumbersOnly, highlightSourceId, geoIndex, diag);
                    break;
            }
        }

        private static void ProcessBlockReference(
            BlockReference blockRef,
            string insertLayer,
            Accumulator acc,
            Transaction tr,
            HashSet<ObjectId> stackGuard,
            bool extractNumbersOnly,
            CalloutMarkGate.GeoIndex geoIndex,
            CountDiagnostics diag)
        {
            foreach (ObjectId attrId in blockRef.AttributeCollection)
            {
                var attr = tr.GetObject(attrId, OpenMode.ForRead, false) as AttributeReference;
                if (attr == null)
                {
                    continue;
                }

                var attrLayer = ResolveLayer(attr.Layer, insertLayer);
                ProcessTextValue(attr.TextString, attrLayer, acc, attr.ObjectId, extractNumbersOnly, geoIndex, attr.Position, attr.Height, diag);
            }

            // IMPORTANT for highlight: entities inside block definition don't have usable handles for selection.
            // Bind all definition-derived matches to the *instance* (blockRef.ObjectId).
            ProcessBlockDefinition(blockRef.BlockTableRecord, insertLayer, acc, tr, stackGuard, extractNumbersOnly, blockRef.ObjectId, geoIndex, diag);
        }

        private static void ProcessBlockDefinition(
            ObjectId blockRecordId,
            string insertLayer,
            Accumulator acc,
            Transaction tr,
            HashSet<ObjectId> stackGuard,
            bool extractNumbersOnly,
            ObjectId highlightSourceId,
            CalloutMarkGate.GeoIndex geoIndex,
            CountDiagnostics diag)
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
                        ProcessTextValue(attDef.TextString, nestedLayer, acc, highlightSourceId, extractNumbersOnly, geoIndex, attDef.Position, attDef.Height, diag);
                        continue;
                    }

                    if (entity is BlockReference nestedRef)
                    {
                        ProcessBlockDefinition(nestedRef.BlockTableRecord, nestedLayer, acc, tr, stackGuard, extractNumbersOnly, highlightSourceId, geoIndex, diag);
                        continue;
                    }

                    ProcessEntity(entity, nestedLayer, acc, tr, stackGuard, extractNumbersOnly, highlightSourceId, geoIndex, diag);
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

        private static void ProcessTextValue(
            string textValue,
            string entityLayer,
            Accumulator acc,
            ObjectId sourceId,
            bool extractNumbersOnly,
            CalloutMarkGate.GeoIndex geoIndex,
            Point3d textPos,
            double textHeight,
            CountDiagnostics diag)
        {
            if (extractNumbersOnly)
            {
                var sanitized = SpecGrid.MTextPlainText.SanitizeRawContents(textValue);
                if (string.IsNullOrWhiteSpace(sanitized))
                {
                    return;
                }

                var position = ExtractPositionNumber(sanitized);
                if (!position.HasValue)
                {
                    return;
                }

                if (diag != null)
                {
                    diag.SeenDigits++;
                }

                var layer = GetBaseLayer(entityLayer);
                var pt = new Point2d(textPos.X, textPos.Y);
                var markText = position.Value.ToString(CultureInfo.InvariantCulture);
                if (!CalloutMarkGate.ShouldCountAsCalloutMark(pt, textHeight, geoIndex, diag?.GateStats))
                {
                    return;
                }

                if (diag != null)
                {
                    diag.RecordAccepted(layer);
                }

                acc.Increment(layer, markText, sourceId);
                return;
            }

            var text = NormalizeText(textValue);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var layer2 = GetBaseLayer(entityLayer);
            acc.Increment(layer2, text, sourceId);
        }

        private static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var s = text.Trim();
            // MTEXT uses "\P" to represent newline.
            s = s.Replace("\\P", "\n");
            s = s.Replace("\r\n", "\n").Replace("\r", "\n");
            // Collapse repeated whitespace lines lightly (keeps intent, avoids accidental duplicates).
            while (s.Contains("\n\n"))
            {
                s = s.Replace("\n\n", "\n");
            }

            return s.Trim();
        }

        private static int? ExtractPositionNumber(string text)
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

        private static void ApplyDiagnostics(PosCountResult result, CountDiagnostics diag)
        {
            if (result == null || diag == null)
            {
                return;
            }

            result.SeenDigits = diag.SeenDigits;
            result.RejectC4 = diag.GateStats.RejectC4;
            result.LayerCount = diag.AcceptedByLayer.Count;
            result.LayerSample = FormatLayerSample(diag.AcceptedByLayer);
        }

        private static string FormatLayerSample(Dictionary<string, int> acceptedByLayer)
        {
            if (acceptedByLayer == null || acceptedByLayer.Count == 0)
            {
                return string.Empty;
            }

            var parts = acceptedByLayer
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .Select(kv => kv.Key + ":" + kv.Value.ToString(CultureInfo.InvariantCulture));
            return string.Join(",", parts);
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

        private sealed class Accumulator
        {
            // key: (layer,text)
            private readonly Dictionary<RowKey, RowAcc> _rows = new Dictionary<RowKey, RowAcc>();

            public void Increment(string layer, string text, ObjectId sourceId)
            {
                var key = new RowKey(layer ?? string.Empty, text ?? string.Empty);
                if (!_rows.TryGetValue(key, out var acc))
                {
                    acc = new RowAcc { Layer = key.Layer, Text = key.Text };
                    _rows[key] = acc;
                }

                acc.Count++;
                if (!sourceId.IsNull)
                {
                    var h = sourceId.Handle.ToString();
                    if (!string.IsNullOrWhiteSpace(h))
                    {
                        acc.SourceHandles.Add(h);
                    }
                }
            }

            public IReadOnlyList<PosRow> ToRows()
            {
                return _rows.Values
                    .OrderBy(r => r.Layer, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(r => r.Text, StringComparer.OrdinalIgnoreCase)
                    .Select(r => new PosRow
                    {
                        Layer = r.Layer,
                        Text = r.Text,
                        Count = r.Count,
                        SourceHandles = r.SourceHandles.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                    })
                    .ToList();
            }

            private readonly struct RowKey : IEquatable<RowKey>
            {
                public readonly string Layer;
                public readonly string Text;

                public RowKey(string layer, string text)
                {
                    Layer = layer ?? string.Empty;
                    Text = text ?? string.Empty;
                }

                public bool Equals(RowKey other)
                {
                    return string.Equals(Layer, other.Layer, StringComparison.OrdinalIgnoreCase)
                           && string.Equals(Text, other.Text, StringComparison.Ordinal);
                }

                public override bool Equals(object obj)
                {
                    return obj is RowKey other && Equals(other);
                }

                public override int GetHashCode()
                {
                    unchecked
                    {
                        var h1 = StringComparer.OrdinalIgnoreCase.GetHashCode(Layer ?? string.Empty);
                        var h2 = StringComparer.Ordinal.GetHashCode(Text ?? string.Empty);
                        return (h1 * 397) ^ h2;
                    }
                }
            }

            private sealed class RowAcc
            {
                public string Text;
                public string Layer;
                public int Count;
                public List<string> SourceHandles = new List<string>();
            }
        }
    }
}

