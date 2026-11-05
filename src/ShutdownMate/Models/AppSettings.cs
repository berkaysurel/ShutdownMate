using ShutdownMate.Services;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Wpf.Ui.Controls;

namespace ShutdownMate.Models
{
    /// <summary>
    /// Kalıcı uygulama ayarları. <see cref="Services.SettingsService"/> tarafından
    /// %APPDATA%\ShutdownMate\settings.json dosyasına yazılır.
    /// </summary>
    public sealed class AppSettings
    {
        public const int MaxLockMessageLength = 120;
        public const int MaxLockMessagePresets = 12;
        public const int MaxQuickDurations = 8;
        public const int MaxQuickDurationMinutes = 24 * 60 - 1;
        public const int DefaultMaxDurationHours = 24;
        private static readonly int[] AllowedMaxDurationHours = { 6, 12, 24, 48, 72, 99 };
        public const int DefaultMinLeadMinutes = 5;
        private static readonly int[] AllowedMinLeadMinutes = { 1, 2, 5, 10, 15, 30, 60 };
        private static readonly int[] DefaultQuickDurations = { 15, 30, 60, 120, 180 };

        // ---------------- Son seçimler (RememberLastChoices açıksa geri yüklenir) ----------------
        public PowerAction LastAction { get; set; } = PowerAction.Shutdown;
        public TriggerMode LastMode { get; set; } = TriggerMode.Duration;
        public int DurationHours { get; set; } = 0;
        public int DurationMinutes { get; set; } = 30;
        public int ClockHour { get; set; } = 23;
        public int ClockMinute { get; set; } = 0;

        // "İşler bitince" koşulları
        public bool UseNetwork { get; set; } = true;
        public NetworkDirection NetworkDirection { get; set; } = NetworkDirection.Both;
        public int NetworkThresholdKBps { get; set; } = 50;
        public bool UseCpu { get; set; } = false;
        public int CpuThresholdPercent { get; set; } = 10;
        public bool UseGpu { get; set; } = false;
        public int GpuThresholdPercent { get; set; } = 15;
        public bool UseTransfer { get; set; } = false;
        public int TransferThresholdMBps { get; set; } = 5;

        /// <summary>Aktarım kaynağı: boş = otomatik (aktarım yapan sürücüler), drive:D: = tek sürücü.</summary>
        public string TransferSource { get; set; } = "";
        public bool UseProcess { get; set; } = false;

        /// <summary>Koşulu yalnızca bu programa göre ölç (süreç adı, boş = tüm sistem).</summary>
        public string NetworkProcessName { get; set; } = "";
        public string CpuProcessName { get; set; } = "";
        public string GpuProcessName { get; set; } = "";
        public bool CalmEnabled { get; set; } = true;
        public int CalmMinutes { get; set; } = 2;
        public bool WaitForStart { get; set; } = true;
        public bool DeadlineEnabled { get; set; } = false;
        public int DeadlineHours { get; set; } = 8;
        public bool GraceEnabled { get; set; } = true;
        public int GraceMinutes { get; set; } = 5;

        public bool ForceCloseApps { get; set; } = false;
        public bool PreventSleep { get; set; } = true;

        // ---------------- Tercihler (Ayarlar sayfası) ----------------
        public bool RememberLastChoices { get; set; } = true;
        public AppThemeMode Theme { get; set; } = AppThemeMode.System;

        /// <summary>Arayüz dili: "auto" (Windows diline göre), "tr" ya da "en". Değişiklik yeniden açılınca geçerli olur.</summary>
        public string Language { get; set; } = "auto";

        /// <summary>Boş: Windows vurgu rengi. Aksi halde "#RRGGBB".</summary>
        public string AccentColor { get; set; } = "";

        public WindowBackdropType Backdrop { get; set; } = WindowBackdropType.Mica;
        public bool AlwaysOnTop { get; set; } = false;

        /// <summary>Ana ekranda gizlenen öğeler (anahtar adları: bkz. MainWindow.Home.cs). Boş: hepsi görünür.</summary>
        public List<string> HiddenHomeItems { get; set; } = new List<string>();

        /// <summary>Menü, panel ve düğme animasyonları.</summary>
        public bool AnimationsEnabled { get; set; } = true;

        /// <summary>Hızlı süre düğmeleri (dakika).</summary>
        public List<int> QuickDurationMinutes { get; set; } = DefaultQuickDurations.ToList();

        /// <summary>"Süre sonra" modunda kurulabilecek en uzun süre (saat).</summary>
        public int MaxDurationHours { get; set; } = DefaultMaxDurationHours;

        /// <summary>İşlemin en erken kaç dakika sonraya kurulabileceği (süre ve saat modları).</summary>
        public int MinLeadMinutes { get; set; } = DefaultMinLeadMinutes;

        /// <summary>Başlat düğmesi, onay kutusu işaretlenmeden çalışmaz.</summary>
        public bool RequireStartConfirm { get; set; } = true;

        /// <summary>Zorla kapatma seçiliyse başlatmadan önce ek onay istenir.</summary>
        public bool ConfirmForceClose { get; set; } = true;

        /// <summary>Kapat ve Yeniden başlat'tan önce USB depolama aygıtları "Donanımı güvenle kaldır" ile çıkarılır.</summary>
        public bool EjectUsbBeforePower { get; set; } = true;

        /// <summary>Süre dolduğunda bilgisayar kullanılıyorsa işlem yapılmaz, ertelenir.</summary>
        public bool PostponeWhileActive { get; set; } = true;

        /// <summary>
        /// Gözetim kaydı: zamanlayıcı çalışırken olayları ve ekran görüntülerini %APPDATA%\ShutdownMate\Gecmis'e yazar.
        /// Kişisel bilgi içerebileceği için varsayılan kapalıdır; açarken kullanıcıdan onay alınır.
        /// </summary>
        public bool WatchRecordingEnabled { get; set; }

        /// <summary>Kullanıcının gözetim kaydı bilgilendirmesini son onayladığı an.</summary>
        public System.DateTime? WatchConsentTime { get; set; }

        /// <summary>Gözetim kaydı ekran görüntüsü alır; kapalıyken yalnızca olaylar yazılır.</summary>
        public bool RecordScreenshots { get; set; } = false;

        /// <summary>Görüntüde tüm ekran yerine yalnızca izlenen programın penceresi (program seçiliyse).</summary>
        public bool ScreenshotWindowOnly { get; set; }

        /// <summary>Deneysel: küçültülmüş programın penceresi görüntü için ekran dışında kısa süre açılır.</summary>
        public bool CaptureMinimizedProgram { get; set; }

        /// <summary>Gözetim kaydının ara görüntü sıklığı (dakika); 0 kapalı.</summary>
        public int ScreenshotMinutes { get; set; } = 15;

        /// <summary>Kayıt klasörü adının başına sıra numarası eklenir (0007_160657_02_11_2026).</summary>
        public bool WatchSerialNames { get; set; }

        /// <summary>Ekran görüntüsünün sağ altına bilgi damgası yazılır.</summary>
        public bool StampEnabled { get; set; } = true;
        public bool StampDate { get; set; } = true;
        public bool StampTime { get; set; } = true;
        public bool StampMode { get; set; } = true;
        public bool StampProgram { get; set; } = true;

        /// <summary>Damgaya eklenecek serbest not (ör. bilgisayar adı); boşsa yazılmaz.</summary>
        public string StampNote { get; set; } = "";

        public int WatchKeepSessions { get; set; } = 30;
        public int WatchMaxMegabytes { get; set; } = 500;

        /// <summary>Kayda bu süredeki Windows olay günlüğü hataları eklenir.</summary>
        public bool WatchIncludeSystemErrors { get; set; } = true;

        /// <summary>Zamanlayıcı başlayınca ekran kilidi açılır (tam ekran açık değilse).</summary>
        public bool LockOnStart { get; set; } = false;

        /// <summary>Ekran kilidinde sayaç: 0 gizli, 1 küçük kart, 2 büyük gösterge.</summary>
        public int LockTimerMode { get; set; } = 1;
        public bool LockShowTarget { get; set; } = true;
        public bool LockShowClock { get; set; } = false;
        public bool LockShowLabel { get; set; } = true;
        public bool LockShowMetrics { get; set; } = false;
        public bool LockMetricCpu { get; set; } = true;
        public bool LockMetricGpu { get; set; } = true;
        public bool LockMetricDownload { get; set; } = true;
        public bool LockMetricUpload { get; set; } = true;
        /// <summary>Kapalıysa sistem veri kartlarında eğri çizilmez, yalnızca sayı görünür.</summary>
        public bool LockMetricGraph { get; set; } = true;
        /// <summary>Dosya aktarımı kartı: kaynak otomatik (en yüksek hızlı sürücü); İşler sekmesinde kaynak seçiliyse o.</summary>
        public bool LockMetricTransfer { get; set; } = true;
        public bool LockExitPanelTop { get; set; } = false;

        /// <summary>Ekran kilidi ve tam ekranda görünen mesaj; boşsa hiçbir şey çizilmez.</summary>
        public string LockMessage { get; set; } = "";
        public bool ShowLockMessage { get; set; } = true;

        /// <summary>Ekran mesajının üstündeki üçgen ünlem ikonu.</summary>
        public bool ShowLockMessageIcon { get; set; } = true;

        /// <summary>Ana ekrandaki hazır mesaj listesi; silinen mesaj seçili mesajı değiştirmez.</summary>
        public List<string> LockMessagePresets { get; set; } = GetDefaultLockMessagePresets();

        /// <summary>Tam ekran ve kilitten çıkışta basılı tutma süresi (ms); en az 300: tek tıkla yanlışlıkla çıkış olmasın (eski 0 ayarı 300'e yükselir).</summary>
        public int ExitHoldMilliseconds { get; set; } = 1500;

        /// <summary>Geri sayım gerçek geçen süreyle yapılır, yaz/kış saati geçişinden etkilenmez.</summary>
        public bool FollowDaylightSaving { get; set; } = true;

        /// <summary>Düğmelerin üzerinde bekleyince kısa açıklama gösterilir.</summary>
        public bool ShowToolTips { get; set; } = true;

        public int SnoozeMinutes { get; set; } = 10;
        public int WarningSeconds { get; set; } = 60;
        public bool WarningSound { get; set; } = true;
        public bool WarningBringToFront { get; set; } = true;

        /// <summary>İşlemden bu kadar saniye önce tam ekran kapanış uyarısı gösterilir (0 = kapalı).</summary>
        public int FinalAlertSeconds { get; set; } = 30;

        /// <summary>Zamanlayıcı çalışırken diğer uygulamaların sesini kıs (yalnızca bu uygulamanın uyarıları duyulur).</summary>
        public bool MuteOtherApps { get; set; } = false;

        public DisplayStyle DisplayStyle { get; set; } = DisplayStyle.Modern;

        public bool FullscreenOnStart { get; set; } = false;
        public bool FullscreenShowClock { get; set; } = true;
        public bool FullscreenHideCursor { get; set; } = true;
        public FullscreenBackground FullscreenBackground { get; set; } = FullscreenBackground.Theme;
        public string FullscreenColor { get; set; } = "#000000";

        /// <summary>Bulanıklık yarıçapı: 20 hafif, 40 orta, 70 güçlü.</summary>
        public int FullscreenBlur { get; set; } = 40;

        /// <summary>Ekran siyah kalır, fare hareket edince gösterge görünür.</summary>
        public bool FullscreenBlackout { get; set; } = false;

        /// <summary>Karartma biçimi: false ekran tamamen siyah, true yalnızca ekrandaki öğeler soluklaşır.</summary>
        public bool FullscreenBlackoutDim { get; set; } = false;

        /// <summary>Gösterge boyutu: 1 küçük, 2 orta, 3 büyük.</summary>
        public int FullscreenDisplaySize { get; set; } = 2;
        /// <summary>Kart görünümü (tam ekran ve ekran kilidi): 0 cam (yarı saydam), 1 düz koyu, 2 kartsız.</summary>
        public int FullscreenCardStyle { get; set; } = 0;

        /// <summary>Tam ekran ve ekran kilidi vurgu rengi (#RRGGBB); boşsa uygulamanın vurgusu.</summary>
        public string FullscreenAccent { get; set; } = "";

        /// <summary>Sistem verisi kartlarının boyutu: 1 küçük, 2 orta, 3 büyük.</summary>
        public int FullscreenMetricSize { get; set; } = 2;

        public bool FullscreenShowProgress { get; set; } = true;
        public bool FullscreenShowTarget { get; set; } = true;
        public bool FullscreenShowLock { get; set; } = true;

        /// <summary>Saat, durum ve çıkış düğmesi hareketsizlikte solar.</summary>
        public bool FullscreenAutoHide { get; set; } = true;

        /// <summary>Tam ekranda sistem verisi kartları.</summary>
        public bool FullscreenShowMetrics { get; set; } = false;
        public bool FullscreenMetricCpu { get; set; } = true;
        public bool FullscreenMetricGpu { get; set; } = true;
        public bool FullscreenMetricDownload { get; set; } = true;
        public bool FullscreenMetricUpload { get; set; } = true;
        public bool FullscreenMetricGraph { get; set; } = true;
        public bool FullscreenMetricTransfer { get; set; } = true;

        // ---------------- Yardımcılar ----------------

        /// <summary>Geçerli üst süre; ayar dosyasındaki bilinmeyen değerler varsayılana döner.</summary>
        public int GetMaxDurationHours()
        {
            return AllowedMaxDurationHours.Contains(MaxDurationHours) ? MaxDurationHours : DefaultMaxDurationHours;
        }

        /// <summary>Geçerli en kısa süre; ayar dosyasındaki bilinmeyen değerler varsayılana döner.</summary>
        public int GetMinLeadMinutes()
        {
            return AllowedMinLeadMinutes.Contains(MinLeadMinutes) ? MinLeadMinutes : DefaultMinLeadMinutes;
        }

        public static List<string> GetDefaultLockMessagePresets()
        {
            return new List<string>
            {
                Loc.T("Preset_Render"),
                Loc.T("Preset_Transfer"),
                Loc.T("Preset_Busy"),
                Loc.T("Preset_Backup"),
                Loc.T("Preset_Update"),
                Loc.T("Preset_Away")
            };
        }

        /// <summary>Her mesajı temizler; boşları ve tekrarları atar, sınırı aşanları kırpar. Boş liste geçerlidir.</summary>
        public static List<string> CleanLockMessagePresets(IEnumerable<string> presets)
        {
            return (presets ?? Enumerable.Empty<string>())
                .Select(CleanLockMessage).Where(p => p.Length > 0)
                .Distinct().Take(MaxLockMessagePresets).ToList();
        }

        /// <summary>Satır sonlarını boşluğa çevirir, kenar boşluklarını atar, uzunluğu sınıra kırpar.</summary>
        public static string CleanLockMessage(string text)
        {
            string clean = Regex.Replace(text ?? "", @"[\p{Cc}  ]+", " ").Trim();
            if (clean.Length <= MaxLockMessageLength) return clean;
            // Emojinin iki parçası arasından kesilmesin
            int length = char.IsHighSurrogate(clean[MaxLockMessageLength - 1]) ? MaxLockMessageLength - 1 : MaxLockMessageLength;
            return clean.Substring(0, length).TrimEnd();
        }

        /// <summary>Elle değiştirilmiş ya da bozuk ayar dosyasındaki değerleri güvenli aralığa çeker.</summary>
        public void Sanitize()
        {
            LockMessage = CleanLockMessage(LockMessage);
            LockMessagePresets = CleanLockMessagePresets(LockMessagePresets);
            MinLeadMinutes = GetMinLeadMinutes();
            MaxDurationHours = GetMaxDurationHours();
            FullscreenDisplaySize = System.Math.Clamp(FullscreenDisplaySize, 1, 3);
            LockTimerMode = System.Math.Clamp(LockTimerMode, 0, 2);
            FullscreenCardStyle = System.Math.Clamp(FullscreenCardStyle, 0, 2);
            FullscreenMetricSize = System.Math.Clamp(FullscreenMetricSize, 1, 3);
            if (FullscreenAccent == null || !System.Text.RegularExpressions.Regex.IsMatch(FullscreenAccent, "^#[0-9A-Fa-f]{6}$")) FullscreenAccent = "";
            ExitHoldMilliseconds = System.Math.Clamp(ExitHoldMilliseconds, 300, 5000);
            FullscreenBlur = System.Math.Clamp(FullscreenBlur, 20, 70); // Ekran görüntüsü okunabilir kalmasın

            // Tanımsız enum değerleri (elle bozulmuş dosya) varsayılana döner
            if (!System.Enum.IsDefined(LastAction)) LastAction = PowerAction.Shutdown;
            if (!System.Enum.IsDefined(LastMode)) LastMode = TriggerMode.Duration;
            if (!System.Enum.IsDefined(NetworkDirection)) NetworkDirection = NetworkDirection.Both;
            TransferThresholdMBps = System.Math.Clamp(TransferThresholdMBps, 1, 500);
            // Yalnızca sürücüler: boş (otomatik) ya da drive:X:; eski nic:/proc: kayıtları otomatiğe döner
            if (TransferSource == null || !TransferSource.StartsWith("drive:")) TransferSource = "";
            if (!System.Enum.IsDefined(Theme)) Theme = AppThemeMode.System;
            if (Language != "tr" && Language != "en") Language = "auto";
            if (!System.Enum.IsDefined(Backdrop)) Backdrop = WindowBackdropType.Mica;
            if (!System.Enum.IsDefined(DisplayStyle)) DisplayStyle = DisplayStyle.Modern;
            if (!System.Enum.IsDefined(FullscreenBackground)) FullscreenBackground = FullscreenBackground.Theme;
            SnoozeMinutes = System.Math.Clamp(SnoozeMinutes, 1, 120);
            ScreenshotMinutes = WatchOptions.Snap(ScreenshotMinutes, WatchOptions.ScreenshotMinutes);
            WatchKeepSessions = WatchOptions.Snap(WatchKeepSessions, WatchOptions.KeepSessions);
            WatchMaxMegabytes = WatchOptions.Snap(WatchMaxMegabytes, WatchOptions.MaxMegabytes);
            if (StampNote == null) StampNote = "";
            else if (StampNote.Length > 60) StampNote = StampNote.Substring(0, 60);
            // Onay kaydı olmadan açık gelen ayar (elle düzenlenmiş dosya) kapatılır
            if (WatchRecordingEnabled && !WatchConsentTime.HasValue) WatchRecordingEnabled = false;
            WarningSeconds = System.Math.Clamp(WarningSeconds, 10, 600);
            FinalAlertSeconds = System.Math.Clamp(FinalAlertSeconds, 0, 120);
            CalmMinutes = System.Math.Clamp(CalmMinutes, 1, 120);
            GraceMinutes = System.Math.Clamp(GraceMinutes, 1, 120);
            DeadlineHours = System.Math.Clamp(DeadlineHours, 1, 72);
        }

        /// <summary>Geçerli, sıralı ve tekrarsız hızlı süre listesi.</summary>
        public List<int> GetQuickDurations()
        {
            var list = (QuickDurationMinutes ?? new List<int>())
                .Where(m => m >= 1 && m <= MaxQuickDurationMinutes)
                .Distinct().OrderBy(m => m).Take(MaxQuickDurations).ToList();
            return list.Count > 0 ? list : DefaultQuickDurations.ToList();
        }

        public void ResetQuickDurations()
        {
            QuickDurationMinutes = DefaultQuickDurations.ToList();
        }

        /// <summary>Son seçim alanlarını varsayılan değerlere döndürür (tercihlere dokunmaz).</summary>
        public void ResetLastChoices()
        {
            var d = new AppSettings();
            LastAction = d.LastAction;
            LastMode = d.LastMode;
            DurationHours = d.DurationHours;
            DurationMinutes = d.DurationMinutes;
            ClockHour = d.ClockHour;
            ClockMinute = d.ClockMinute;
            UseNetwork = d.UseNetwork;
            NetworkDirection = d.NetworkDirection;
            NetworkThresholdKBps = d.NetworkThresholdKBps;
            UseCpu = d.UseCpu;
            CpuThresholdPercent = d.CpuThresholdPercent;
            UseGpu = d.UseGpu;
            GpuThresholdPercent = d.GpuThresholdPercent;
            UseTransfer = d.UseTransfer;
            TransferThresholdMBps = d.TransferThresholdMBps;
            TransferSource = d.TransferSource;
            UseProcess = d.UseProcess;
            NetworkProcessName = d.NetworkProcessName;
            CpuProcessName = d.CpuProcessName;
            GpuProcessName = d.GpuProcessName;
            CalmEnabled = d.CalmEnabled;
            CalmMinutes = d.CalmMinutes;
            WaitForStart = d.WaitForStart;
            DeadlineEnabled = d.DeadlineEnabled;
            DeadlineHours = d.DeadlineHours;
            GraceEnabled = d.GraceEnabled;
            GraceMinutes = d.GraceMinutes;
            ForceCloseApps = d.ForceCloseApps;
            PreventSleep = d.PreventSleep;
        }
    }
}
