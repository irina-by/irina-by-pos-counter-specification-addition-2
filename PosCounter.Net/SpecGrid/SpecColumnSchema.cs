using System.Collections.Generic;

namespace PosCounter.Net.SpecGrid
{
    /// <summary>Эталон столбцов с первой таблицы (шапка Поз./Наимен./Кол.).</summary>
    internal sealed class SpecColumnSchema
    {
        public bool IsLocked;
        public int ColMark = -1;
        public int ColName = -1;
        public int ColQty = -1;
        public int ColDesignation = -1;
        public List<double> AnchorGridXs = new List<double>();
        public int HeaderEndRow;
        /// <summary>Число объектов в рамке эталонной таблицы (относительный порог продолжения).</summary>
        public int AnchorObjectCount;
    }
}
