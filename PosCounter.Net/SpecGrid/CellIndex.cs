using System;
using System.Collections.Generic;
using System.Linq;

namespace PosCounter.Net.SpecGrid
{
    /// <summary>
    /// Привязка текста к ячейкам сетки (§3.2 CELL-INDEX-EPS).
    /// </summary>
    internal static class CellIndex
    {
        /// <summary>Допуск для точек на оси сетки (не путать с epsAxis кластеризации).</summary>
        public const double CellIndexEps = 2.0;

        /// <summary>Дубликат «текст на текст» в одной точке (§18.3 ONE-TEXT-PER-CELL).</summary>
        public const double TextOverlapEps = 150.0;

        public static bool TryGetCellIndex(
            double x,
            double y,
            IReadOnlyList<double> xs,
            IReadOnlyList<double> ys,
            out int rowIdx,
            out int colIdx)
        {
            rowIdx = -1;
            colIdx = -1;
            if (xs == null || ys == null || xs.Count < 2 || ys.Count < 2)
            {
                return false;
            }

            const double eps = CellIndexEps;
            for (var i = 0; i < xs.Count - 1; i++)
            {
                if (x >= xs[i] - eps && x < xs[i + 1] + eps)
                {
                    colIdx = i;
                    break;
                }
            }

            for (var j = 0; j < ys.Count - 1; j++)
            {
                if (y <= ys[j] + eps && y > ys[j + 1] - eps)
                {
                    rowIdx = j;
                    break;
                }
            }

            return rowIdx >= 0 && colIdx >= 0;
        }

        public static string GetCellText(IEnumerable<TextSample> samples)
        {
            return GetCellText(samples, null, -1, -1, -1);
        }

        /// <summary>§18.3: одна ячейка — один текст (без склейки наложенных объектов).</summary>
        public static string GetCellText(
            IEnumerable<TextSample> samples,
            SpecGridLog log,
            int scopeIndex,
            int row,
            int col)
        {
            return GetCellText(samples, log, scopeIndex, row, col, false);
        }

        /// <param name="preferMarkColumn">ColMark: приоритет коротким цифрам-маркам.</param>
        public static string GetCellText(
            IEnumerable<TextSample> samples,
            SpecGridLog log,
            int scopeIndex,
            int row,
            int col,
            bool preferMarkColumn)
        {
            var list = samples?.ToList() ?? new List<TextSample>();
            if (list.Count == 0)
            {
                return string.Empty;
            }

            var candidates = new List<(TextSample Sample, string Plain, int Score)>();
            foreach (var t in list)
            {
                var plain = MTextPlainText.PreferDisplayNameFromSpec(
                    MTextPlainText.SanitizeRawContents(t.Plain ?? string.Empty));
                if (string.IsNullOrWhiteSpace(plain))
                {
                    continue;
                }

                if (IsDuplicateCandidate(candidates, t, plain))
                {
                    continue;
                }

                candidates.Add((t, plain, MTextPlainText.NameScore(plain)));
            }

            if (candidates.Count == 0)
            {
                return string.Empty;
            }

            if (preferMarkColumn)
            {
                var markHits = candidates
                    .Where(c => MTextPlainText.TryParseMarkKey(c.Plain, out _))
                    .OrderBy(c => c.Plain.Trim().Length)
                    .ThenByDescending(c => c.Sample.DataY)
                    .ToList();
                if (markHits.Count > 0)
                {
                    return markHits[0].Plain;
                }
            }

            var best = candidates
                .OrderByDescending(c => c.Score)
                .ThenByDescending(c => c.Plain.Length)
                .ThenByDescending(c => c.Sample.Y)
                .First();

            return best.Plain;
        }

        private static bool IsDuplicateCandidate(
            List<(TextSample Sample, string Plain, int Score)> candidates,
            TextSample t,
            string plain)
        {
            // Important: In ColMark cells we may have overlapping texts (digit mark + designation like "21 ОСТ ...").
            // The digit must not be dropped as "duplicate by proximity", otherwise markAnchor/data rows break.
            // We still dedupe exact same digit strings.
            if (MTextPlainText.IsExactDigitMark(plain))
            {
                foreach (var c0 in candidates)
                {
                    if (string.Equals(c0.Plain, plain, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            foreach (var c in candidates)
            {
                if (string.Equals(c.Plain, plain, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var overlapX = Math.Abs(c.Sample.X - t.X) <= TextOverlapEps * 4;
                var overlapY = Math.Abs(c.Sample.Y - t.Y) <= TextOverlapEps * 4;
                if (overlapX && overlapY)
                {
                    return true;
                }

                if (Math.Abs(c.Sample.X - t.X) <= TextOverlapEps
                    && Math.Abs(c.Sample.Y - t.Y) <= TextOverlapEps)
                {
                    if (c.Plain.IndexOf(plain, StringComparison.OrdinalIgnoreCase) >= 0
                        || plain.IndexOf(c.Plain, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Строка с максимальным overlap [yMin,yMax] с полосами gridYs.</summary>
        public static bool TryGetRowByExtent(
            double yMin,
            double yMax,
            IReadOnlyList<double> gridYs,
            double minOverlapFraction,
            out int row,
            out double overlapFraction)
        {
            row = -1;
            overlapFraction = 0;
            if (gridYs == null || gridYs.Count < 2)
            {
                return false;
            }

            var bestOverlap = 0.0;
            for (var r = 0; r < gridYs.Count - 1; r++)
            {
                var bandTop = gridYs[r];
                var bandBottom = gridYs[r + 1];
                var bandH = Math.Abs(bandTop - bandBottom);
                if (bandH < 1.0)
                {
                    bandH = 1.0;
                }

                var overlap = Math.Min(yMax, bandTop) - Math.Max(yMin, bandBottom);
                if (overlap <= 0)
                {
                    continue;
                }

                var fraction = overlap / bandH;
                if (fraction >= minOverlapFraction && fraction > bestOverlap + 1e-9)
                {
                    bestOverlap = fraction;
                    row = r;
                    overlapFraction = fraction;
                }
            }

            return row >= 0;
        }

        public static int GetDominantRow(TextSample t, IReadOnlyList<double> gridYs, ScopeGridResult result)
        {
            if (t == null || gridYs == null || gridYs.Count < 2)
            {
                return -1;
            }

            var yMin = t.YMin;
            var yMax = t.YMax;
            var hasExtent = Math.Abs(yMax - yMin) > 1e-6;
            if (!hasExtent)
            {
                var halfH = ResolveEffectiveHalfHeight(t, result);
                yMin = t.DataY - halfH;
                yMax = t.DataY + halfH;
            }

            var minFraction = t.IsMText && hasExtent ? 0.30 : 0.50;
            if (!TryGetRowByExtent(yMin, yMax, gridYs, minFraction, out var row, out _))
            {
                return -1;
            }

            var bestOverlap = 0.0;
            var bestRow = row;
            for (var r = 0; r < gridYs.Count - 1; r++)
            {
                var bandTop = gridYs[r];
                var bandBottom = gridYs[r + 1];
                var bandH = Math.Abs(bandTop - bandBottom);
                if (bandH < 1.0)
                {
                    bandH = 1.0;
                }

                var overlap = Math.Min(yMax, bandTop) - Math.Max(yMin, bandBottom);
                if (overlap <= 0)
                {
                    continue;
                }

                var fraction = overlap / bandH;
                if (fraction < minFraction)
                {
                    continue;
                }

                if (fraction > bestOverlap + 1e-9)
                {
                    bestOverlap = fraction;
                    bestRow = r;
                }
                else if (Math.Abs(fraction - bestOverlap) < 1e-9)
                {
                    var curMid = (gridYs[bestRow] + gridYs[bestRow + 1]) * 0.5;
                    var newMid = (gridYs[r] + gridYs[r + 1]) * 0.5;
                    if (Math.Abs(t.DataY - newMid) < Math.Abs(t.DataY - curMid))
                    {
                        bestRow = r;
                    }
                }
            }

            return bestRow;
        }

        private static double ResolveEffectiveHalfHeight(TextSample t, ScopeGridResult result)
        {
            if (t.TextHeight > 1e-6)
            {
                return t.TextHeight * 0.5;
            }

            if (result != null && result.PrimaryNameTextHeight > 1e-6)
            {
                return result.PrimaryNameTextHeight * 0.5;
            }

            if (result != null && result.MedianRowStep > 1e-6)
            {
                return result.MedianRowStep * 0.375;
            }

            return SpecGridService.QtyTextHeightFallback * 0.5;
        }
    }
}
