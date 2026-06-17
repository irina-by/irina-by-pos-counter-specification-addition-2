using System;
using System.Collections.Generic;
using System.Linq;

namespace PosCounter.Net.SpecGrid
{
    /// <summary>Политика CMD-диагностики без привязки к номерам марок.</summary>
    internal static class SpecDiagPolicy
    {
        private const int MaxNameTracesPerScope = 12;
        private const int MaxMarkTracesPerScope = 8;
        private const int MaxGeoTracesPerScope = 10;
        private const int MaxSampleKeys = 6;

        private static readonly Dictionary<int, int> NameTraceCount = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> MarkTraceCount = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> GeoTraceCount = new Dictionary<int, int>();
        private static HashSet<int> _emptyNameKeys = new HashSet<int>();
        private static HashSet<int> _missingQtyKeys = new HashSet<int>();

        public static void ResetSession()
        {
            NameTraceCount.Clear();
            MarkTraceCount.Clear();
            GeoTraceCount.Clear();
            _emptyNameKeys = new HashSet<int>();
            _missingQtyKeys = new HashSet<int>();
        }

        public static void RegisterEmptyNameKeys(IEnumerable<int> keys)
        {
            _emptyNameKeys = new HashSet<int>(keys ?? Enumerable.Empty<int>());
        }

        public static void RegisterMissingQtyKeys(IEnumerable<int> keys)
        {
            _missingQtyKeys = new HashSet<int>(keys ?? Enumerable.Empty<int>());
        }

        public static bool ShouldTraceName(ScopeGridResult scope, int key, bool emptyName, int parts, int texts)
        {
            if (scope == null || key < 1)
            {
                return false;
            }

            var scopeIdx = scope.ScopeIndex;
            if (!NameTraceCount.ContainsKey(scopeIdx))
            {
                NameTraceCount[scopeIdx] = 0;
            }

            if (NameTraceCount[scopeIdx] >= MaxNameTracesPerScope)
            {
                return false;
            }

            var trace = emptyName
                || (parts == 0 && texts > 0)
                || _emptyNameKeys.Contains(key)
                || _missingQtyKeys.Contains(key)
                || IsSampleKey(scope, key);
            if (trace)
            {
                NameTraceCount[scopeIdx]++;
            }

            return trace;
        }

        public static bool ShouldTraceMark(ScopeGridResult scope)
        {
            if (scope == null)
            {
                return false;
            }

            var scopeIdx = scope.ScopeIndex;
            if (!MarkTraceCount.ContainsKey(scopeIdx))
            {
                MarkTraceCount[scopeIdx] = 0;
            }

            if (MarkTraceCount[scopeIdx] >= MaxMarkTracesPerScope)
            {
                return false;
            }

            MarkTraceCount[scopeIdx]++;
            return true;
        }

        public static bool ShouldTraceGeo(ScopeGridResult scope)
        {
            if (scope == null)
            {
                return false;
            }

            var scopeIdx = scope.ScopeIndex;
            if (!GeoTraceCount.ContainsKey(scopeIdx))
            {
                GeoTraceCount[scopeIdx] = 0;
            }

            if (GeoTraceCount[scopeIdx] >= MaxGeoTracesPerScope)
            {
                return false;
            }

            GeoTraceCount[scopeIdx]++;
            return true;
        }

        public static bool IsSampleKey(ScopeGridResult scope, int key)
        {
            var keys = scope?.KeyToRowMark?.Keys.OrderBy(k => k).ToList();
            if (keys == null || keys.Count == 0)
            {
                return false;
            }

            if (keys.Count <= MaxSampleKeys)
            {
                return keys.Contains(key);
            }

            var head = keys.Take(3);
            var tail = keys.Skip(Math.Max(0, keys.Count - 3));
            return head.Contains(key) || tail.Contains(key);
        }

        public static string FormatKeyToRowMarkSample(ScopeGridResult scope)
        {
            if (scope?.KeyToRowMark == null || scope.KeyToRowMark.Count == 0)
            {
                return string.Empty;
            }

            var keys = scope.KeyToRowMark.Keys.OrderBy(k => k).ToList();
            IEnumerable<int> sample;
            if (keys.Count <= MaxSampleKeys)
            {
                sample = keys;
            }
            else
            {
                sample = keys.Take(3).Concat(keys.Skip(keys.Count - 3));
            }

            var parts = sample.Select(k => $"{k}→row{scope.KeyToRowMark[k]}");
            return $"KeyToRowMark count={keys.Count} sample: {string.Join(", ", parts)}";
        }
    }
}
