using Autodesk.AutoCAD.ApplicationServices;

namespace PosCounter.Net.SpecGrid
{
    /// <summary>
    /// В CMD: подсказки инженеру (<see cref="WriteCommandLine"/>) и диагностика AC 2016 (<see cref="WriteDiag"/>).
    /// Файловые логи отключены; Info/Debug — no-op.
    /// </summary>
    internal sealed class SpecGridLog
    {
        private const int DefaultDiagBudget = 55;
        private static int _diagRemaining;

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
            _diagRemaining = maxLines > 0 ? maxLines : DefaultDiagBudget;
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
