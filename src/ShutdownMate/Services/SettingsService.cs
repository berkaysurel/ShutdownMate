using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShutdownMate.Helpers;
using ShutdownMate.Models;

namespace ShutdownMate.Services
{
    /// <summary>Ayarları %APPDATA%\ShutdownMate\settings.json dosyasında saklar.</summary>
    internal static class SettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static string FolderPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShutdownMate"); }
        }

        public static string FilePath
        {
            get { return Path.Combine(FolderPath, "settings.json"); }
        }

        /// <summary>Dili ayar dosyasından, ayarlar yüklenmeden önce okur (varsayılan ayarlar da dile göre metin taşır).</summary>
        public static string PeekLanguage()
        {
            try
            {
                if (!File.Exists(FilePath)) return "auto";
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(FilePath)))
                {
                    JsonElement value;
                    if (doc.RootElement.TryGetProperty("Language", out value) && value.ValueKind == JsonValueKind.String) return value.GetString();
                }
            }
            catch { } // Bozuk dosya: Load() onu zaten karantinaya alır; dil otomatik kalır
            return "auto";
        }

        public static AppSettings Load()
        {
            AppSettings settings = FileGuard.ReadOrQuarantine(FilePath, t => JsonSerializer.Deserialize<AppSettings>(t, JsonOptions));
            settings = settings ?? new AppSettings();
            settings.Sanitize();
            if (!settings.RememberLastChoices) settings.ResetLastChoices();
            return settings;
        }

        public static void Save(AppSettings settings)
        {
            if (FileGuard.IsUnreadable(FilePath)) return; // dosya okunamadı, varsayılanla çalışıyoruz; gerçek ayarlar ezilmesin
            try
            {
                Directory.CreateDirectory(FolderPath);
                WriteAtomic(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
            }
            catch (Exception ex) { FileGuard.LogError(FolderPath, "Ayarlar kaydedilemedi: " + ex.Message); }
        }

        /// <summary>Önce geçici dosyaya yazar; yazma yarıda kesilirse eski dosya bozulmaz.</summary>
        public static void WriteAtomic(string path, string content)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, content);
            File.Move(temp, path, true);
        }

        public static void OpenFolder()
        {
            try
            {
                Directory.CreateDirectory(FolderPath);
                Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), "\"" + AppPackage.ToVisiblePath(FolderPath) + "\"") { UseShellExecute = false });
            }
            catch { }
        }
    }
}
