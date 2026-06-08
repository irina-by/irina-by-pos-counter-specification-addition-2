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

            // log [CELL-PICK] отключён

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
            foreach (var c in candidates)
            {
                if (string.Equals(c.Plain, plain, StringComparison.OrdinalIgnoreCase))
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
    }
}
