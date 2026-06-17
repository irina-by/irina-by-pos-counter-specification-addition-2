using System;
using System.Globalization;
using System.Linq;

namespace PosCounter.Net.SpecGrid
{
    /// <summary>Универсальный парсер марки (key) для спецификации — префиксы как в PosCounterEngine.</summary>
    internal static class MarkKeyParser
    {
        private static readonly string[] Prefixes =
        {
            "Позиция", "позиция", "Номер", "номер", "Марка", "марка",
            "Поз.", "Поз", "поз.", "поз",
            "POS", "Pos", "pos", "Pos.", "pos.",
            "Item", "item", "N.", "n.", "№", "N", "n", "P", "p"
        };

        public static bool TryParse(string text, out int key, out string matchedPrefix)
        {
            key = 0;
            matchedPrefix = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var t = text.Trim();
            foreach (var prefix in Prefixes.OrderByDescending(p => p.Length))
            {
                if (t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    matchedPrefix = prefix;
                    t = t.Substring(prefix.Length).Trim();
                    break;
                }
            }

            while (t.Length > 0)
            {
                var last = t[t.Length - 1];
                if (last == '.' || last == ',' || last == ';' || last == ':' || last == ')' || last == ']')
                {
                    t = t.Substring(0, t.Length - 1).TrimEnd();
                    continue;
                }

                break;
            }

            if (!MTextPlainText.IsExactDigitMark(t))
            {
                return false;
            }

            if (!int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out key))
            {
                return false;
            }

            return key >= 1 && key <= 10000;
        }

        public static bool TryParse(string text, out int key) => TryParse(text, out key, out _);
    }
}
