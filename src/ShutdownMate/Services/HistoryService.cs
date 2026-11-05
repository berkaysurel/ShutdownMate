using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ShutdownMate.Helpers;
using ShutdownMate.Models;

namespace ShutdownMate.Services
{
    /// <summary>
    /// Zamanlayıcı geçmişini %APPDATA%\ShutdownMate\history.json dosyasında tutar.
    /// Böylece ertesi gün işin bitip bitmediği ve bilgisayarın neden kapandığı görülebilir.
    /// </summary>
    internal static class HistoryService
    {
        private const int MaxEntries = 50;

        private static string FilePath
        {
            get { return Path.Combine(SettingsService.FolderPath, "history.json"); }
        }

        public static List<HistoryEntry> Load()
        {
            return FileGuard.ReadOrQuarantine(FilePath, t => JsonSerializer.Deserialize<List<HistoryEntry>>(t)) ?? new List<HistoryEntry>();
        }

        public static void Add(string summary, string sessionId = null)
        {
            var list = Load();
            if (FileGuard.IsUnreadable(FilePath)) return; // geçmiş okunamadı; önceki kayıtlar ezilmesin
            list.Insert(0, new HistoryEntry { Time = DateTime.Now, Summary = summary, SessionId = sessionId });
            Save(list.Take(MaxEntries).ToList());
        }

        public static void Clear()
        {
            FileGuard.ClearUnreadable(FilePath); // kullanıcı isteği: her zaman yazılır
            Save(new List<HistoryEntry>());
        }

        private static void Save(List<HistoryEntry> list)
        {
            try
            {
                Directory.CreateDirectory(SettingsService.FolderPath);
                SettingsService.WriteAtomic(FilePath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) { FileGuard.LogError(SettingsService.FolderPath, "Geçmiş kaydedilemedi: " + ex.Message); }
        }
    }
}
