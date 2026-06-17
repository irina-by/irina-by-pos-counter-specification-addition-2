using System;
using System.Globalization;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;

namespace PosCounter.Net.SpecGrid
{
    /// <summary>
    /// CMD для инженера: краткие [POSC]/[INFO] и предупреждения.
    /// Разработческая диагностика <see cref="WriteDiag"/> / <see cref="WriteTrace"/> отключена (закомментирована).
    /// </summary>
    internal sealed class SpecGridLog
    {
        private const int DefaultDiagBudget = 120;
        private static int _diagRemaining;
        private static Document _diagDocument;

        public void SetCommandLineDocument(Document doc)
        {
        }

        public void RowDataDiag(string message)
        {
        }

        public void ResetCmdBudget(int maxLines = 200)
        {
            _diagRemaining = DefaultDiagBudget;
        }

        public void Info(string message)
        {
        }

        public void Debug(string message)
        {
        }

        public void Warn(string message)
        {
        }

        public void Success(string message)
        {
        }

        public void Grid(string message)
        {
        }

        public void Name(string message)
        {
        }

        public void Qty(string message)
        {
        }

        public void Layer(string message)
        {
        }

        public void Write(string tag, string message)
        {
        }

        public static void ResetDiagSession(Document doc, int maxLines = DefaultDiagBudget)
        {
            _diagDocument = doc;
            _diagRemaining = maxLines > 0 ? maxLines : DefaultDiagBudget;
        }

        public static string FormatDllBuildStamp()
        {
#if NET8_0
            const string targetLabel = "net8.0-windows";
#else
            const string targetLabel = "net452";
#endif
            var asm = Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? asm.GetName().Version?.ToString()
                ?? "?";
            return $"DLL {targetLabel} v{info} build={TryGetAssemblyBuildStamp(asm)}";
        }

        private static string TryGetAssemblyBuildStamp(Assembly asm)
        {
            try
            {
                var path = asm?.Location;
                if (!string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
                {
                    return System.IO.File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                }
            }
            catch
            {
                // ignore
            }

            return "?";
        }

        /// <summary>Разработческая диагностика [POSC-DIAG] — отключена.</summary>
        public static void WriteDiag(Document doc, string message)
        {
            // Инженеру: только WriteCommandLine / WriteDiagTail (краткий whitelist).
            /*
            if (doc == null || string.IsNullOrWhiteSpace(message) || _diagRemaining <= 0)
            {
                return;
            }

            _diagRemaining--;
            WriteCommandLine(doc, "[POSC-DIAG] " + message.Trim());
            */
        }

        /// <summary>Краткий итог для инженера — префикс [POSC].</summary>
        public static void WriteEngineerSummary(Document doc, string message)
        {
            if (doc == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            WriteCommandLine(doc, "[POSC] " + message.Trim());
        }

        /// <summary>Итоговые строки для инженера (без [POSC-DIAG]).</summary>
        public static void WriteDiagTail(Document doc, string message)
        {
            if (!IsEngineerTailMessage(message))
            {
                return;
            }

            WriteEngineerSummary(doc, message);
        }

        private static bool IsEngineerTailMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            var m = message.Trim();
            if (m.StartsWith("count source=", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (m.StartsWith("WriteQty итог:", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (m.IndexOf("WriteQty пропущен", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (m.StartsWith("[KV-SUMMARY]", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (m.IndexOf("ВНИМАНИЕ:", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        /// <summary>Трассировка [COLQTY]/[HEADER]/… — отключена.</summary>
        public static void WriteTrace(string category, string message)
        {
            // WriteDiag(_diagDocument, $"[{category}] {message?.Trim()}");
        }

        /// <summary>Сообщение в командную строку AutoCAD (без Enter и без файлов).</summary>
        public static void WriteCommandLine(Document doc, string message)
        {
            if (doc == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            try
            {
                doc.Editor?.WriteMessage("\n" + message);
            }
            catch
            {
                // ignore
            }
        }
    }
}
