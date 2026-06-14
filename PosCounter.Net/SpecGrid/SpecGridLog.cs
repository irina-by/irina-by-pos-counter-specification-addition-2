using System;
using System.Globalization;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;

namespace PosCounter.Net.SpecGrid
{
    /// <summary>
    /// В CMD: подсказки инженеру (<see cref="WriteCommandLine"/>) и диагностика AC 2016 (<see cref="WriteDiag"/>).
    /// Файловые логи отключены; критичные трассировки — через <see cref="WriteTrace"/> / <see cref="WriteDiag"/>.
    /// </summary>
    internal sealed class SpecGridLog
    {
        private const int DefaultDiagBudget = 120;
        private static int _diagRemaining;
        private static Document _diagDocument;

        /// <summary>Раньше включало [ROW-DATA] в CMD — отключено.</summary>
        public void SetCommandLineDocument(Document doc)
        {
        }

        /// <summary>Диагностика RowDataStart — отключена.</summary>
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

        /// <summary>Сброс лимита [POSC-DIAG] на одну операцию «Выбрать спецификацию».</summary>
        public static void ResetDiagSession(Document doc, int maxLines = DefaultDiagBudget)
        {
            _diagDocument = doc;
            _diagRemaining = maxLines > 0 ? maxLines : DefaultDiagBudget;
        }

        /// <summary>Метка DLL для первой строки диагностики спецификации.</summary>
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

        /// <summary>Диагностика в CMD с префиксом [POSC-DIAG] (без файлов, с лимитом строк).</summary>
        public static void WriteDiag(Document doc, string message)
        {
            if (doc == null || string.IsNullOrWhiteSpace(message) || _diagRemaining <= 0)
            {
                return;
            }

            _diagRemaining--;
            WriteCommandLine(doc, "[POSC-DIAG] " + message.Trim());
        }

        /// <summary>Итоговые строки — всегда в CMD, без учёта бюджета.</summary>
        public static void WriteDiagTail(Document doc, string message)
        {
            if (doc == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            WriteCommandLine(doc, "[POSC-DIAG] " + message.Trim());
        }

        /// <summary>Трассировка метода: [POSC-DIAG] [COLQTY] … / [HEADER] / [NAME] / [WRITEQTY].</summary>
        public static void WriteTrace(string category, string message)
        {
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var doc = _diagDocument;
            if (doc == null)
            {
                return;
            }

            WriteDiag(doc, $"[{category}] {message.Trim()}");
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
