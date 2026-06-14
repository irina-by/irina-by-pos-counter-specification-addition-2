using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PosCounter.Net.SpecGrid
{
    internal static class MTextPlainText
    {
        private static readonly Regex ControlStrip = new Regex(
            @"\\[A-Za-z][^;\\]*;|\\\{|\\\}|\\P|\\~|\\S|\\L|\\O|\\K|\\Q|\\W|\\[A-Za-z]",
            RegexOptions.Compiled);

        public static string SanitizeRawContents(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            // net46: без перегрузок Replace/Contains(..., StringComparison) — только Ordinal по умолчанию
            var s = raw.Replace("\\P", "\n");
            s = ControlStrip.Replace(s, string.Empty);
            s = s.Replace("{", string.Empty)
                .Replace("}", string.Empty);
            s = s.Replace("\r\n", "\n")
                .Replace('\r', '\n');
            while (s.IndexOf("\n\n", StringComparison.Ordinal) >= 0)
            {
                s = s.Replace("\n\n", "\n");
            }

            return s.Trim();
        }

        public static string PreferDisplayNameFromSpec(string plain)
        {
            if (string.IsNullOrWhiteSpace(plain))
            {
                return string.Empty;
            }

            var s = plain.Trim();
            if (LooksLikeNumericCallout(s))
            {
                return string.Empty;
            }

            return s;
        }

        /// <summary>AutoCAD: %%D → °, %%C → Ø, %%P → ± (для палитры и экспорта).</summary>
        public static string DecodeAutocadPercentCodes(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var s = text;
            s = ReplaceIgnoreCase(s, "%%D", "°");
            s = ReplaceIgnoreCase(s, "%%C", "Ø");
            s = ReplaceIgnoreCase(s, "%%P", "±");
            return s;
        }

        private static string ReplaceIgnoreCase(string source, string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(oldValue))
            {
                return source ?? string.Empty;
            }

            var idx = 0;
            var sb = new StringBuilder(source.Length);
            while (idx < source.Length)
            {
                var found = source.IndexOf(oldValue, idx, StringComparison.OrdinalIgnoreCase);
                if (found < 0)
                {
                    sb.Append(source, idx, source.Length - idx);
                    break;
                }

                sb.Append(source, idx, found - idx);
                sb.Append(newValue);
                idx = found + oldValue.Length;
            }

            return sb.ToString();
        }

        /// <summary>Очистка MText/DBText + отбор имени + символы AutoCAD для отображения.</summary>
        public static string FormatForPaletteDisplay(string rawOrPlain)
        {
            var s = PreferDisplayNameFromSpec(SanitizeRawContents(rawOrPlain ?? string.Empty));
            return DecodeAutocadPercentCodes(s);
        }

        /// <summary>§19.13: отдельные строки наименования (в т.ч. MText с \P / \n).</summary>
        public static IEnumerable<string> EnumerateDisplayNameLines(string rawOrPlain)
        {
            var s = PreferDisplayNameFromSpec(SanitizeRawContents(rawOrPlain ?? string.Empty));
            if (string.IsNullOrWhiteSpace(s))
            {
                yield break;
            }

            if (s.IndexOf('\n') < 0)
            {
                yield return s.Trim();
                yield break;
            }

            foreach (var line in s.Split('\n'))
            {
                var t = line.Trim();
                if (string.IsNullOrWhiteSpace(t) || LooksLikeNumericCallout(t))
                {
                    continue;
                }

                yield return t;
            }
        }

        /// <summary>Заголовок раздела без марки (например «Хозяйственно-питьевой водопровод (В1)»).</summary>
        public static bool LooksLikeSectionHeaderLine(string display)
        {
            if (string.IsNullOrWhiteSpace(display))
            {
                return false;
            }

            var t = display.Trim();
            if (t.Length < 25 || !HasLetter(t))
            {
                return false;
            }

            if (HasProductDimensionPattern(t))
            {
                return false;
            }

            if (t.IndexOf("SDR", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            if (t.IndexOf("МПа", StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("MPa", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            if (t.IndexOf("(В1)", StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("(В2)", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (t.IndexOf("водопровод", StringComparison.OrdinalIgnoreCase) >= 0 && t.IndexOf('(') >= 0)
            {
                return true;
            }

            return false;
        }

        /// <summary>Самостоятельное изделие (полное наименование в одной строке).</summary>
        public static bool IsStandaloneProductName(string display)
        {
            if (string.IsNullOrWhiteSpace(display))
            {
                return false;
            }

            var t = display.Trim();
            return NameScore(t) >= 6 && t.Length >= 20 && HasLetter(t);
        }

        private static bool HasProductDimensionPattern(string t)
        {
            for (var i = 0; i < t.Length - 2; i++)
            {
                if (!char.IsDigit(t[i]))
                {
                    continue;
                }

                var j = i;
                while (j < t.Length && char.IsDigit(t[j]))
                {
                    j++;
                }

                if (j >= t.Length)
                {
                    continue;
                }

                var sep = t[j];
                if (sep != 'x' && sep != 'X' && sep != 'х' && sep != '×')
                {
                    continue;
                }

                j++;
                if (j < t.Length && char.IsDigit(t[j]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Короткая строка-продолжение наименования (хвост «с гайкой…»).</summary>
        public static bool IsAcceptableNameContinuation(string display)
        {
            if (string.IsNullOrWhiteSpace(display))
            {
                return false;
            }

            if (LooksLikeSectionHeaderLine(display))
            {
                return false;
            }

            if (NameScore(display) > 0)
            {
                return true;
            }

            var t = display.Trim();
            return t.Length >= 5 && HasLetter(t);
        }

        /// <summary>§11A.3.1 ETALON-CLEAN-NAME-COMPARE — единая очистка для сверки.</summary>
        public static string CleanNameForCompare(string rawOrPlain)
        {
            return PreferDisplayNameFromSpec(SanitizeRawContents(rawOrPlain ?? string.Empty)).Trim();
        }

        public static bool LooksLikeNumericCallout(string s)
        {
            if (string.IsNullOrWhiteSpace(s) || s.Length > 8)
            {
                return false;
            }

            var t = s.Trim();
            if (t.Length <= 4 && t.IndexOfAny(new[] { '.', ',', '/' }) >= 0)
            {
                var digits = 0;
                foreach (var c in t)
                {
                    if (char.IsDigit(c))
                    {
                        digits++;
                    }
                }

                if (digits > 0 && !HasLetter(t))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasLetter(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }

            foreach (var c in s)
            {
                if (char.IsLetter(c))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsExactDigitMark(string mark)
        {
            if (string.IsNullOrWhiteSpace(mark))
            {
                return false;
            }

            var t = mark.Trim();
            if (t.Length == 0 || t.Length > 6)
            {
                return false;
            }

            foreach (var c in t)
            {
                if (!char.IsDigit(c))
                {
                    return false;
                }
            }

            return int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var v) && v >= 1 && v <= 10000;
        }

        public static bool TryParseMarkKey(string text, out int key)
        {
            return MarkKeyParser.TryParse(text, out key);
        }

        public static int NameScore(string plain)
        {
            if (string.IsNullOrWhiteSpace(plain))
            {
                return 0;
            }

            var s = plain.Trim();
            if (LooksLikeNumericCallout(s) || IsNumericCalloutStrict(s))
            {
                return 0;
            }

            if (s.Length <= 4 && !HasLetter(s))
            {
                return 0;
            }

            var score = 0;
            if (s.Length >= 10)
            {
                score += 2;
            }

            if (s.Length >= 20)
            {
                score += 2;
            }

            if (HasLetter(s))
            {
                score += 3;
            }

            if (s.IndexOf(' ') >= 0 || s.IndexOf('-') >= 0)
            {
                score += 1;
            }

            return score;
        }

        /// <summary>Цифровые выноски 1.11 / 1.23 — score ~0 для AllowedNameLayers.</summary>
        public static bool IsNumericCalloutStrict(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return false;
            }

            var t = s.Trim();
            var letters = 0;
            var digits = 0;
            var spaces = 0;
            var other = 0;
            foreach (var c in t)
            {
                if (char.IsLetter(c))
                {
                    letters++;
                }
                else if (char.IsDigit(c))
                {
                    digits++;
                }
                else if (char.IsWhiteSpace(c))
                {
                    spaces++;
                }
                else
                {
                    other++;
                }
            }

            return letters == 0 && digits > 0 && digits + other >= t.Length - spaces;
        }

        public static string NormalizeLayer(string layer)
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

        public static string ResolveLayer(string entityLayer, string insertLayer)
        {
            if (string.Equals(entityLayer, "0", StringComparison.OrdinalIgnoreCase))
            {
                return insertLayer ?? "0";
            }

            return entityLayer ?? string.Empty;
        }
    }
}
