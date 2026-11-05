using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ShutdownMate.Helpers
{
    /// <summary>
    /// Uygulamanın Microsoft Store (MSIX) paketi olarak çalışıp çalışmadığı. Paketli uygulamada %APPDATA%'ya yazılanlar
    /// paketin özel klasörüne yönlendirilir; Gezgin ve görüntüleyici gibi dış programlara gerçek yol verilmelidir.
    /// </summary>
    internal static class AppPackage
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetCurrentPackageFamilyName(ref int length, StringBuilder name);

        private const int ErrorInsufficientBuffer = 122;

        private static string _family;
        private static bool _checked;

        /// <summary>Paket ailesi adı (ör. "Yayinci.ShutdownMate_abc123"); paketli değilse null.</summary>
        public static string FamilyName
        {
            get
            {
                if (_checked) return _family;
                _checked = true;
                try
                {
                    int length = 0;
                    if (GetCurrentPackageFamilyName(ref length, null) != ErrorInsufficientBuffer) return null;
                    var name = new StringBuilder(length);
                    if (GetCurrentPackageFamilyName(ref length, name) == 0) _family = name.ToString();
                }
                catch { }
                return _family;
            }
        }

        public static bool IsPackaged
        {
            get { return FamilyName != null; }
        }

        /// <summary>
        /// Paketliyken %APPDATA% altındaki bir yolu dış programların görebileceği gerçek yola çevirir;
        /// paketli değilse ya da yol %APPDATA% altında değilse aynen döner.
        /// </summary>
        public static string ToVisiblePath(string path)
        {
            if (!IsPackaged || string.IsNullOrEmpty(path)) return path;
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!path.StartsWith(roaming, StringComparison.OrdinalIgnoreCase)) return path;
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string real = Path.Combine(local, "Packages", FamilyName, "LocalCache", "Roaming") + path.Substring(roaming.Length);
            return real;
        }
    }
}
