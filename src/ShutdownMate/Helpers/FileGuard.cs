using System;
using System.Collections.Generic;
using System.IO;

namespace ShutdownMate.Helpers
{
    /// <summary>Bozuk ayar / geçmiş dosyasını silmeden kenara alır ve hataları error.log'a yazar.</summary>
    internal static class FileGuard
    {
        // Okunamayan (kilitli vb.) dosyalar: varsayılanla açılınca üzerine yazılıp veri kaybolmasın
        private static readonly HashSet<string> Unreadable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Dosya son okumada var ama okunamadıysa true; bu durumda üzerine yazılmamalı.</summary>
        public static bool IsUnreadable(string path)
        {
            lock (Unreadable) return Unreadable.Contains(path);
        }

        /// <summary>Kullanıcı isteğiyle dosyanın üzerine yazılacaksa okunamadı işaretini kaldırır.</summary>
        public static void ClearUnreadable(string path)
        {
            lock (Unreadable) Unreadable.Remove(path);
        }

        /// <summary>
        /// Dosyayı okuyup ayrıştırır. Dosya yoksa null döner. Okunamazsa (kilitli vb.) dosyaya dokunulmaz,
        /// hata error.log'a yazılır, null döner. Yalnızca ayrıştırılamazsa dosya
        /// yanına ".bozuk" adıyla taşınır (varsa üzerine yazılır; taşıma hatası yutulur) ve null döner.
        /// </summary>
        public static T ReadOrQuarantine<T>(string path, Func<string, T> parse) where T : class
        {
            lock (Unreadable) Unreadable.Remove(path);
            if (!File.Exists(path)) return null;
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception ex)
            {
                lock (Unreadable) Unreadable.Add(path);
                // Geçici okuma hatası (başka program dosyayı tutuyor olabilir): sağlam dosya taşınmaz
                LogError(Path.GetDirectoryName(path), "Okunamadı, dosyaya dokunulmadı: " + path + " - " + ex.Message);
                return null;
            }
            try
            {
                return parse(text);
            }
            catch
            {
                try { File.Move(path, path + ".bozuk", true); } catch { }
                return null;
            }
        }

        /// <summary>Hatayı klasördeki error.log'a ekler; yazım hatası asla istisna fırlatmaz.</summary>
        public static void LogError(string folder, string message)
        {
            try
            {
                Directory.CreateDirectory(folder);
                File.AppendAllText(Path.Combine(folder, "error.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
            }
            catch { }
        }
    }
}
