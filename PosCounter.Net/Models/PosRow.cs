using System.Collections.Generic;

namespace PosCounter.Net.Models
{
    public sealed class PosRow
    {
        public string Text { get; set; }
        public string Layer { get; set; }
        public int Count { get; set; }

        /// <summary>Нормализованный ключ марки (цифра), если удалось распознать.</summary>
        public int? Key { get; set; }

        /// <summary>Наименование из спецификации (grid-lines).</summary>
        public string NameFromSpec { get; set; }

        /// <summary>Источник имени: grid-lines | fallback | not_found.</summary>
        public string NameSource { get; set; }

        /// <summary>Строка итога (в таблице/экспорте), не объект чертежа.</summary>
        public bool IsTotalLine { get; set; }
        /// <summary>
        /// Hex handles (without 0x) of contributing entities in the active drawing database.
        /// Stored as strings to keep WPF ViewModels free of native ObjectId wrappers.
        /// </summary>
        public List<string> SourceHandles { get; set; } = new List<string>();
    }
}

