using ShutdownMate.Services;
using System;
using System.Globalization;
using ShutdownMate.Models;
using Wpf.Ui.Controls;

namespace ShutdownMate.Helpers
{
    /// <summary>Arayüzde gösterilen Türkçe metin biçimleri.</summary>
    internal static class TextFormat
    {
        /// <summary>00:00:00 biçiminde süre (yukarı yuvarlanır).</summary>
        public static string Clock(TimeSpan ts)
        {
            if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
            long total = (long)Math.Ceiling(ts.TotalSeconds);
            return string.Format("{0:00}:{1:00}:{2:00}", total / 3600, (total % 3600) / 60, total % 60);
        }

        /// <summary>00:00 biçiminde dakika:saniye.</summary>
        public static string MinSec(TimeSpan ts)
        {
            if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
            long total = (long)Math.Floor(ts.TotalSeconds);
            return string.Format("{0:00}:{1:00}", total / 60, total % 60);
        }

        /// <summary>"45 dakika", "2 saat", "1 sa 30 dk".</summary>
        public static string Friendly(TimeSpan ts)
        {
            int minutes = (int)Math.Ceiling(ts.TotalMinutes);
            return Minutes(minutes, false);
        }

        /// <summary>Dakikayı okunur hale getirir. short: "15 dk" / "1 saat" / "1 sa 30 dk".</summary>
        public static string Minutes(int minutes, bool shortForm)
        {
            if (minutes < 60) return minutes + (shortForm ? Loc.T("TextFormat_Dk") : Loc.T(minutes == 1 ? "TextFormat_Dakika1" : "TextFormat_Dakika"));
            int h = minutes / 60, m = minutes % 60;
            return m == 0 ? h + Loc.T(h == 1 ? "TextFormat_Saat1" : "TextFormat_Saat") : h + Loc.T("TextFormat_Sa") + m + Loc.T("TextFormat_Dk");
        }

        public static string DayLabel(DateTime target, DateTime now)
        {
            if (target.Date == now.Date) return Loc.T("TextFormat_Bugun");
            if (target.Date == now.Date.AddDays(1)) return Loc.T("TextFormat_Yarin");
            return target.ToString("d MMMM", Loc.Culture);
        }

        /// <summary>Bayt/sn değerini "850 KB/sn", "2,4 MB/sn" biçimine çevirir.</summary>
        public static string Speed(double bytesPerSecond)
        {
            if (bytesPerSecond < 0) return "–";
            double kb = bytesPerSecond / 1024.0;
            if (kb < 1000) return kb.ToString(kb < 10 ? "0.0" : "0", Loc.Culture) + Loc.T("TextFormat_KbSn");
            return (kb / 1024.0).ToString("0.0", Loc.Culture) + Loc.T("TextFormat_MbSn");
        }

        public static string Percent(double value)
        {
            bool after = Loc.Code == "en"; // İngilizce'de işaret sayıdan sonra: 13%
            string number = value < 0 ? "–" : Math.Round(value).ToString(Loc.Culture);
            return after ? number + "%" : "%" + number;
        }

        /// <summary>Geçmiş kaydı için: "Kapatıldı", "Uykuya alındı" ...</summary>
        public static string ActionPast(PowerAction action)
        {
            switch (action)
            {
                case PowerAction.Restart: return Loc.T("TextFormat_YenidenBaslatildi");
                case PowerAction.Sleep: return Loc.T("TextFormat_UykuyaAlindi");
                case PowerAction.Hibernate: return Loc.T("TextFormat_HazirdaBekletildi");
                default: return Loc.T("TextFormat_Kapatildi");
            }
        }

        /// <summary>Kapanış uyarısı başlığı: "Bilgisayar kapanıyor" ...</summary>
        public static string ActionHeadline(PowerAction action)
        {
            switch (action)
            {
                case PowerAction.Restart: return Loc.T("TextFormat_BilgisayarYenidenBaslatili");
                case PowerAction.Sleep: return Loc.T("TextFormat_BilgisayarUykuyaGeciyor");
                case PowerAction.Hibernate: return Loc.T("TextFormat_BilgisayarHazirdaBekletili");
                default: return Loc.T("TextFormat_BilgisayarKapaniyor");
            }
        }

        /// <summary>Elle işlem düğmesi: "Şimdi kapat" ...</summary>
        public static string ActionNow(PowerAction action)
        {
            switch (action)
            {
                case PowerAction.Restart: return Loc.T("TextFormat_SimdiYenidenBaslat");
                case PowerAction.Sleep: return Loc.T("TextFormat_SimdiUyut");
                case PowerAction.Hibernate: return Loc.T("TextFormat_SimdiHazirdaBeklet");
                default: return Loc.T("TextFormat_SimdiKapat");
            }
        }

        /// <summary>"31 Ekim 03:12".</summary>
        public static string DateTimeShort(DateTime time)
        {
            return time.ToString("d MMMM HH:mm", Loc.Culture);
        }

        public static string ActionFuture(PowerAction action)
        {
            switch (action)
            {
                case PowerAction.Restart: return Loc.T("TextFormat_YenidenBaslayacak");
                case PowerAction.Sleep: return Loc.T("TextFormat_UykuyaGececek");
                case PowerAction.Hibernate: return Loc.T("TextFormat_HazirdaBekletilecek");
                default: return Loc.T("TextFormat_Kapanacak");
            }
        }

        public static string ActionFutureLower(PowerAction action)
        {
            string s = ActionFuture(action);
            return char.ToLower(s[0], Loc.Culture) + s.Substring(1);
        }

        public static SymbolRegular ActionIcon(PowerAction action)
        {
            switch (action)
            {
                case PowerAction.Restart: return SymbolRegular.ArrowClockwise24;
                case PowerAction.Sleep: return SymbolRegular.WeatherMoon24;
                case PowerAction.Hibernate: return SymbolRegular.Snooze24;
                default: return SymbolRegular.Power24;
            }
        }
    }
}
