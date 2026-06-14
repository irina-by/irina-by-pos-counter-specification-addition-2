using System.Collections.Generic;
using System.Linq;

namespace PosCounter.Net.SpecGrid
{
    /// <summary>Сессия распознанных таблиц спецификации (§19.18: N таблиц).</summary>
    internal static class SpecGridSession
    {
        private static readonly List<ScopeGridResult> ScopesInternal = new List<ScopeGridResult>();

        public static IReadOnlyList<ScopeGridResult> Scopes => ScopesInternal;

        /// <summary>Обратная совместимость (первая таблица).</summary>
        public static ScopeGridResult Scope0 => ScopesInternal.FirstOrDefault();

        /// <summary>Обратная совместимость (вторая таблица).</summary>
        public static ScopeGridResult Scope1 => ScopesInternal.Count > 1 ? ScopesInternal[1] : null;

        public static string SharedGridLayer;

        /// <summary>Эталон ColMark/ColName/ColQty с первой таблицы (реальная шапка).</summary>
        public static SpecColumnSchema ColumnSchema;

        public static void ClearScopes()
        {
            ScopesInternal.Clear();
            SharedGridLayer = null;
            ColumnSchema = null;
        }

        public static void SetScopes(IEnumerable<ScopeGridResult> scopes)
        {
            ScopesInternal.Clear();
            if (scopes == null)
            {
                return;
            }

            ScopesInternal.AddRange(scopes.Where(s => s != null));
        }
    }
}
