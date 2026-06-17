using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace PosCounter.Net.Engine
{
    /// <summary>Единственный фильтр выносок при ЗАПУСТИТЬ: C4 — цифра внутри круга не считается.</summary>
    internal static class CalloutMarkGate
    {
        internal const double BucketCellSize = 15.0;

        internal sealed class CircleAnchor
        {
            public Point2d Center;
            public double Radius;
        }

        internal sealed class GateStats
        {
            public int RejectC4;
        }

        internal sealed class GeoIndex
        {
            public readonly List<CircleAnchor> Circles = new List<CircleAnchor>();

            private readonly Dictionary<long, List<CircleAnchor>> _circleBuckets = new Dictionary<long, List<CircleAnchor>>();

            public void AddCircle(CircleAnchor c)
            {
                if (c == null)
                {
                    return;
                }

                Circles.Add(c);
                BucketPoint(c.Center.X, c.Center.Y, c);
            }

            public IEnumerable<CircleAnchor> QueryCircleNeighbors(Point2d pt, double radius)
            {
                return QueryBuckets(_circleBuckets, pt, radius);
            }

            private static IEnumerable<CircleAnchor> QueryBuckets(
                Dictionary<long, List<CircleAnchor>> buckets,
                Point2d pt,
                double radius)
            {
                if (buckets == null || buckets.Count == 0)
                {
                    yield break;
                }

                var seen = new HashSet<CircleAnchor>();
                foreach (var key in EnumerateCellKeys(pt, radius))
                {
                    if (!buckets.TryGetValue(key, out var list) || list == null)
                    {
                        continue;
                    }

                    foreach (var item in list)
                    {
                        if (item != null && seen.Add(item))
                        {
                            yield return item;
                        }
                    }
                }
            }

            private static IEnumerable<long> EnumerateCellKeys(Point2d pt, double radius)
            {
                var minCx = (int)Math.Floor((pt.X - radius) / BucketCellSize);
                var maxCx = (int)Math.Floor((pt.X + radius) / BucketCellSize);
                var minCy = (int)Math.Floor((pt.Y - radius) / BucketCellSize);
                var maxCy = (int)Math.Floor((pt.Y + radius) / BucketCellSize);

                for (var cx = minCx; cx <= maxCx; cx++)
                {
                    for (var cy = minCy; cy <= maxCy; cy++)
                    {
                        yield return PackCell(cx, cy);
                    }
                }
            }

            private void BucketPoint(double x, double y, CircleAnchor item)
            {
                var cx = (int)Math.Floor(x / BucketCellSize);
                var cy = (int)Math.Floor(y / BucketCellSize);
                var key = PackCell(cx, cy);
                if (!_circleBuckets.TryGetValue(key, out var list))
                {
                    list = new List<CircleAnchor>();
                    _circleBuckets[key] = list;
                }

                list.Add(item);
            }

            private static long PackCell(int cx, int cy)
            {
                return ((long)cx << 32) ^ (uint)cy;
            }
        }

        public static GeoIndex BuildIndex(Transaction tr, IEnumerable<ObjectId> sourceIds)
        {
            var idx = new GeoIndex();
            var stackGuard = new HashSet<ObjectId>();
            foreach (var id in sourceIds)
            {
                if (id.IsNull || !id.IsValid)
                {
                    continue;
                }

                Entity e;
                try
                {
                    e = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                }
                catch
                {
                    continue;
                }

                if (e == null)
                {
                    continue;
                }

                try
                {
                    CollectEntityTree(idx, tr, e, stackGuard);
                }
                catch
                {
                    // ignore broken entities
                }
            }

            return idx;
        }

        public static void PopulateViewportGeometry(
            Editor editor,
            Transaction tr,
            GeoIndex idx,
            Point3dCollection viewportPolygon)
        {
            if (editor == null || idx == null || viewportPolygon == null || viewportPolygon.Count < 3)
            {
                return;
            }

            try
            {
                var filter = new SelectionFilter(new[]
                {
                    new TypedValue((int)DxfCode.Start, "CIRCLE")
                });

                ObjectId[] ids;
                var crossing = editor.SelectCrossingPolygon(viewportPolygon, filter);
                if (crossing.Status == PromptStatus.OK)
                {
                    ids = crossing.Value.GetObjectIds();
                }
                else
                {
                    var window = editor.SelectWindowPolygon(viewportPolygon, filter);
                    if (window.Status != PromptStatus.OK)
                    {
                        return;
                    }

                    ids = window.Value.GetObjectIds();
                }

                if (ids == null || ids.Length == 0)
                {
                    return;
                }

                foreach (var id in ids)
                {
                    if (id.IsNull || !id.IsValid)
                    {
                        continue;
                    }

                    Entity e;
                    try
                    {
                        e = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                    }
                    catch
                    {
                        continue;
                    }

                    if (e == null)
                    {
                        continue;
                    }

                    try
                    {
                        CollectEntity(idx, e);
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        public static bool ShouldCountAsCalloutMark(
            Point2d textPt,
            double textHeight,
            GeoIndex idx,
            GateStats stats = null)
        {
            if (idx == null)
            {
                return true;
            }

            if (IsDigitInsideCircleMarker(textPt, textHeight, idx))
            {
                if (stats != null)
                {
                    stats.RejectC4++;
                }

                return false;
            }

            return true;
        }

        private static void CollectEntityTree(GeoIndex idx, Transaction tr, Entity e, HashSet<ObjectId> stackGuard)
        {
            if (idx == null || e == null)
            {
                return;
            }

            CollectEntity(idx, e);

            if (e is BlockReference blockRef)
            {
                CollectBlockDefinition(idx, tr, blockRef.BlockTableRecord, stackGuard);
            }
        }

        private static void CollectBlockDefinition(
            GeoIndex idx,
            Transaction tr,
            ObjectId blockRecordId,
            HashSet<ObjectId> stackGuard)
        {
            if (idx == null || blockRecordId.IsNull || !blockRecordId.IsValid || stackGuard == null)
            {
                return;
            }

            if (!stackGuard.Add(blockRecordId))
            {
                return;
            }

            try
            {
                var btr = tr.GetObject(blockRecordId, OpenMode.ForRead, false) as BlockTableRecord;
                if (btr == null)
                {
                    return;
                }

                foreach (ObjectId entId in btr)
                {
                    Entity entity;
                    try
                    {
                        entity = tr.GetObject(entId, OpenMode.ForRead, false) as Entity;
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
                        CollectEntityTree(idx, tr, entity, stackGuard);
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
            finally
            {
                stackGuard.Remove(blockRecordId);
            }
        }

        private static void CollectEntity(GeoIndex idx, Entity e)
        {
            if (e is Circle c)
            {
                idx.AddCircle(new CircleAnchor
                {
                    Center = new Point2d(c.Center.X, c.Center.Y),
                    Radius = c.Radius
                });
            }
        }

        private static bool IsDigitInsideCircleMarker(Point2d textPt, double textHeight, GeoIndex idx)
        {
            var h = textHeight > 0 ? textHeight : 2.5;
            var maxR = Math.Max(4 * h, 20);
            var minR = h * 0.5;
            var eps = Math.Max(h * 0.2, 0.5);
            var searchR = Math.Max(5 * h, 12);

            foreach (var c in idx.QueryCircleNeighbors(textPt, searchR + maxR))
            {
                if (c == null)
                {
                    continue;
                }

                if (c.Radius < minR || c.Radius > maxR)
                {
                    continue;
                }

                if (c.Center.GetDistanceTo(textPt) <= c.Radius + eps)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
