using System;
using System.Linq;

namespace ShutdownMate.Models
{
    /// <summary>Gözetim kaydı ayarlarının seçilebilen değerleri; ayar dosyasındaki değer en yakın geçerli değere çekilir.</summary>
    public static class WatchOptions
    {
        /// <summary>Ara görüntü sıklığı (dakika); 0 kapalı.</summary>
        public static readonly int[] ScreenshotMinutes = { 0, 5, 15, 30, 60 };

        /// <summary>Saklanan en fazla kayıt sayısı.</summary>
        public static readonly int[] KeepSessions = { 10, 30, 50 };

        /// <summary>Kayıtların kullanabileceği en fazla disk alanı (MB).</summary>
        public static readonly int[] MaxMegabytes = { 250, 500, 1000, 2000 };

        public static int Snap(int value, int[] allowed)
        {
            return allowed.OrderBy(a => Math.Abs((long)a - value)).First();
        }
    }
}
