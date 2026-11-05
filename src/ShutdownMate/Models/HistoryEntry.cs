using System;

namespace ShutdownMate.Models
{
    /// <summary>İşlem geçmişindeki tek kayıt.</summary>
    public sealed class HistoryEntry
    {
        public DateTime Time { get; set; }

        /// <summary>Örnek: "Kapatıldı · Seçilen işler bitti".</summary>
        public string Summary { get; set; }

        /// <summary>Bu çalışmanın gözetim kaydı (WatchSession.Id); kayıt yoksa null.</summary>
        public string SessionId { get; set; }
    }
}
