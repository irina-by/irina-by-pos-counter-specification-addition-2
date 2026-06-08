using Autodesk.AutoCAD.ApplicationServices;

namespace PosCounter.Net.SpecGrid
{
    /// <summary>
    /// Диагностика ([ROW-DATA], TABLE-GRID, …) отключена.
    /// В CMD остаются только подсказки инженеру: <see cref="WriteCommandLine"/>.
    /// </summary>
    internal sealed class SpecGridLog
    {
        /// <summary>Раньше включало [ROW-DATA] в CMD — отключено.</summary>
        public void SetCommandLineDocument(Document doc)
        {
        }

        /// <summary>Диагностика RowDataStart — отключена.</summary>
        public void RowDataDiag(string message)
        {
            // Диагностика отключена: [ROW-DATA] больше не выводится в CMD.
        }

        public void ResetCmdBudget(int maxLines = 200)
        {
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
