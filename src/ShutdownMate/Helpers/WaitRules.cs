using System;

namespace ShutdownMate.Helpers
{
    /// <summary>"İşler bitince" bekleme sürelerinin hesabı (anahtarlar kapalıyken de güvenli süreler).</summary>
    internal static class WaitRules
    {
        /// <summary>Sakin kalma kapalıyken değerlerin eşiğin altında kalması gereken güvenlik payı.</summary>
        public static readonly TimeSpan CalmMargin = TimeSpan.FromSeconds(30);

        /// <summary>Ek bekleme kapalıyken işlemden önce verilen en kısa son uyarı süresi (sn).</summary>
        public const int MinNoticeSeconds = 60;

        public enum ClockJump { None, Cancel, Extend }

        /// <summary>Uyarısız işlem olmasın diye en az bu kadar saniye uyarı verilir.</summary>
        public static int NoticeSeconds(int finalAlertSeconds)
        {
            return Math.Max(MinNoticeSeconds, finalAlertSeconds);
        }

        /// <summary>Sayaç sırasında bilgisayar uyuduysa: saat 90 sn'den fazla geçtiyse iptal; aksi halde uyarı süresi dolmuşsa uzat.</summary>
        public static ClockJump AfterClockJump(double remainingSeconds, bool gapDetected, int finalAlertSeconds)
        {
            if (remainingSeconds < -90) return ClockJump.Cancel;
            if (gapDetected && remainingSeconds < NoticeSeconds(finalAlertSeconds)) return ClockJump.Extend;
            return ClockJump.None;
        }

        public static TimeSpan Calm(bool enabled, int minutes)
        {
            return enabled ? TimeSpan.FromMinutes(Math.Max(1, minutes)) : CalmMargin;
        }

        /// <summary>Ek bekleme kapalıyken de kapanış uyarısı ve en kısa süre kuralı için kısa bir son geri sayım kalır.</summary>
        public static TimeSpan Grace(bool enabled, int minutes, int finalAlertSeconds)
        {
            return enabled
                ? TimeSpan.FromMinutes(Math.Max(1, minutes))
                : TimeSpan.FromSeconds(Math.Max(MinNoticeSeconds, finalAlertSeconds));
        }
    }
}
