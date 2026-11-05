using System;
using System.Collections.Generic;
using System.Linq;

namespace ShutdownMate.Models
{
    /// <summary>
    /// Bir zamanlayıcı çalışmasının gözetim kaydı: olaylar, ekran görüntüleri ve kullanım grafiği.
    /// %APPDATA%\ShutdownMate\Gecmis\&lt;Id&gt;\kayit.json dosyasında tutulur.
    /// </summary>
    public sealed class WatchSession
    {
        /// <summary>Klasör adı, başlangıç zamanından (saat_gün_ay_yıl): "201504_02_11_2026".</summary>
        public string Id { get; set; }

        public DateTime Start { get; set; }

        /// <summary>Zamanlayıcı bitmeden bilgisayar kapandıysa (elektrik kesintisi, çökme) null kalır.</summary>
        public DateTime? End { get; set; }

        /// <summary>Örnek: "Süre sonra kapat".</summary>
        public string Mode { get; set; }

        /// <summary>İzlenen program (.exe olmadan); izlenmiyorsa null.</summary>
        public string Program { get; set; }

        /// <summary>Geçmişe yazılan sonuç, ör. "Kapatıldı · Seçilen işler bitti".</summary>
        public string Result { get; set; }

        public List<WatchEvent> Events { get; set; } = new List<WatchEvent>();
        public List<WatchSample> Samples { get; set; } = new List<WatchSample>();

        public int ProblemCount
        {
            get { return Events.Count(e => e.Problem); }
        }
    }
}
