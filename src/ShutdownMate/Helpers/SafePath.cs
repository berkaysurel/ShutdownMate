using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ShutdownMate.Helpers
{
    /// <summary>
    /// Gözetim kaydı klasör adları ve silme güvenliği. Klasör adı başlangıç anıdır: saat_gün_ay_yıl
    /// (ör. 160657_02_11_2026). Yalnızca kayıt klasörünün doğrudan altındaki, adı bu biçimde olan klasörler
    /// silinebilir; eski sürümün adları (20261102-160657) da tanınır.
    /// </summary>
    public static class SafePath
    {
        public const string NameFormat = "HHmmss_dd_MM_yyyy";
        private const string OldNameFormat = "yyyyMMdd-HHmmss";

        private static readonly Regex SerialPrefix = new Regex(@"^\d{4,6}_(?=\d{6}_)");

        /// <summary>Klasör adının başındaki sıra numarası; yoksa 0.</summary>
        public static int Serial(string name)
        {
            return name != null && SerialPrefix.IsMatch(name) ? int.Parse(name.Substring(0, name.IndexOf('_'))) : 0;
        }

        /// <summary>Klasör ya da dosya adı için zaman damgası: "160657_02_11_2026".</summary>
        public static string Name(DateTime time)
        {
            return time.ToString(NameFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>Klasör adındaki başlangıç anı; ad kayıt biçiminde değilse null.</summary>
        public static DateTime? SessionTime(string name)
        {
            // Başta isteğe bağlı sıra numarası: 0007_160657_02_11_2026
            if (name != null && SerialPrefix.IsMatch(name)) name = name.Substring(name.IndexOf('_') + 1);
            DateTime time;
            if (DateTime.TryParseExact(name, new[] { NameFormat, OldNameFormat }, CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
                return time;
            return null;
        }

        public static bool IsSessionFolder(string root, string path)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
                string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
                if (!fullRoot.EndsWith(Path.Combine("ShutdownMate", "Gecmis"), StringComparison.OrdinalIgnoreCase)) return false;
                return string.Equals(Path.GetDirectoryName(full), fullRoot, StringComparison.OrdinalIgnoreCase)
                    && SessionTime(Path.GetFileName(full)).HasValue;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Kök altındaki kayıt klasörlerini yeniden eskiye gezer; en yeni keep kayıt ve toplam maxBytes alan kalır.
        /// keepName (sürmekte olan kaydın klasör adı) saat damgası ne olursa olsun hiç silinmez.
        /// </summary>
        public static void Prune(string root, int keep, long maxBytes, string keepName)
        {
            try
            {
                if (!Directory.Exists(root)) return;
                long total = 0;
                int index = 0;
                foreach (string dir in Directory.GetDirectories(root).Where(d => IsSessionFolder(root, d))
                    .OrderByDescending(d => SessionTime(Path.GetFileName(d))))
                {
                    long size = 0;
                    try { size = new DirectoryInfo(dir).GetFiles().Sum(f => f.Length); } catch { }
                    total += size;
                    bool current = string.Equals(Path.GetFileName(dir), keepName, StringComparison.OrdinalIgnoreCase);
                    if (!current && (index >= keep || total > maxBytes))
                    {
                        try { Directory.Delete(dir, true); } catch { }
                    }
                    index++;
                }
            }
            catch { }
        }
    }
}
