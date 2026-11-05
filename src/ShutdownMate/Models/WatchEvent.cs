using System;

namespace ShutdownMate.Models
{
    /// <summary>Gözetim kaydındaki tek olay; varsa ekran görüntüsü kayıt klasöründeki dosya adıdır.</summary>
    public sealed class WatchEvent
    {
        public DateTime Time { get; set; }
        public WatchEventKind Kind { get; set; }

        /// <summary>Örnek: "Blender yanıt vermiyor".</summary>
        public string Text { get; set; }

        /// <summary>Ekran görüntüsünün dosya adı (ör. "20-15-04.jpg"); görüntü yoksa null.</summary>
        public string Image { get; set; }

        /// <summary>Geçmişte sorun olarak işaretlenir (ör. beklenen program kapanması sorun değildir).</summary>
        public bool Problem { get; set; }
    }
}
