using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using ShutdownMate.Models;

namespace ShutdownMate.Services
{
    /// <summary>
    /// Windows olay günlüğünden (Uygulama ve Sistem) belirli zaman aralığındaki hataları okur:
    /// program çökmesi / donması, sürücü, disk ve donanım hataları. Yönetici izni gerekmez.
    /// </summary>
    internal static class SystemErrorLog
    {
        private const int MaxEvents = 30;

        // Her bilgisayarda sürekli yazılan, sorunla ilgisi olmayan kaynaklar
        private static readonly string[] Noise = { "Microsoft-Windows-DistributedCOM", "DCOM" };

        public static List<WatchEvent> Read(DateTime from, DateTime to)
        {
            var result = new List<WatchEvent>();
            string time = "TimeCreated[@SystemTime>='" + from.ToUniversalTime().ToString("o") + "' and @SystemTime<='" + to.ToUniversalTime().ToString("o") + "']";

            // Uygulama: kritik ve hata. Sistem: kritik, hata ve ekran kartı sürücüsünün yeniden başlaması (Display 4101, uyarı düzeyinde)
            ReadLog("Application", "*[System[(Level=1 or Level=2) and " + time + "]]", result);
            ReadLog("System", "*[System[(Level=1 or Level=2 or (Provider[@Name='Display'] and EventID=4101)) and " + time + "]]", result);

            return result.OrderBy(e => e.Time).Take(MaxEvents).ToList();
        }

        private static void ReadLog(string log, string query, List<WatchEvent> result)
        {
            try
            {
                using (var reader = new EventLogReader(new EventLogQuery(log, PathType.LogName, query)))
                {
                    for (EventRecord record = reader.ReadEvent(); record != null && result.Count < MaxEvents * 2; record = reader.ReadEvent())
                    {
                        using (record)
                        {
                            string source = record.ProviderName ?? "";
                            if (Noise.Contains(source, StringComparer.OrdinalIgnoreCase)) continue;
                            result.Add(new WatchEvent
                            {
                                Time = record.TimeCreated ?? DateTime.Now,
                                Kind = WatchEventKind.SystemError,
                                Problem = true,
                                Text = source + " (" + record.Id + "): " + FirstLine(record)
                            });
                        }
                    }
                }
            }
            catch
            {
                // Günlük okunamazsa (kapalı hizmet, kısıtlı hesap) kayıt onsuz devam eder
            }
        }

        private static string FirstLine(EventRecord record)
        {
            string text;
            try { text = record.FormatDescription(); }
            catch { text = null; }
            if (string.IsNullOrWhiteSpace(text)) return Loc.T("SystemErrorLog_AyrintiYok");
            text = text.Trim().Split('\n')[0].Trim();
            return text.Length > 500 ? text.Substring(0, 500) + "…" : text;
        }
    }
}
