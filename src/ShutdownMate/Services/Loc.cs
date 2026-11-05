using System;
using System.Globalization;
#if !CHECKS
using System.Windows;
#endif

namespace ShutdownMate.Services
{
    /// <summary>
    /// Arayüz dili. Metinler Assets\Strings.tr.xaml ve Strings.en.xaml sözlüklerindedir;
    /// seçilen dil Türkçe sözlüğün üstüne eklenir, çevirisi eksik anahtar Türkçe görünür.
    /// </summary>
    public static class Loc
    {
        public static string Code { get; private set; } = "tr";

        /// <summary>Tarih ve sayı biçimleri için seçili dilin kültürü.</summary>
        public static CultureInfo Culture { get { return CultureInfo.GetCultureInfo(Code == "en" ? "en-GB" : "tr-TR"); } }
#if CHECKS
        private static System.Collections.Generic.Dictionary<string, string> _checkTexts;
#endif

        /// <summary>"auto" (Windows dili Türkçe ise tr, değilse en), "tr" ya da "en".</summary>
        public static string Resolve(string setting)
        {
            if (setting == "tr" || setting == "en") return setting;
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr" ? "tr" : "en";
        }

#if !CHECKS
        /// <summary>Uygulama açılırken, pencereler oluşmadan önce bir kez çağrılır.</summary>
        public static void Init(string setting)
        {
            Code = Resolve(setting);
            var dicts = Application.Current.Resources.MergedDictionaries;
            dicts.Add(Load("tr"));
            if (Code != "tr") dicts.Add(Load(Code));
        }

        private static ResourceDictionary Load(string code)
        {
            return new ResourceDictionary { Source = new Uri("pack://application:,,,/Assets/Strings." + code + ".xaml") };
        }
#endif

        /// <summary>Anahtarın metnini döndürür; bulunamazsa anahtarın kendisi görünür (eksik çeviri hemen fark edilir).</summary>
        public static string T(string key)
        {
#if CHECKS
            // Checks projesinde WPF yok: Türkçe sözlük dosyası doğrudan okunur
            if (_checkTexts == null)
            {
                _checkTexts = new System.Collections.Generic.Dictionary<string, string>();
                string dir = AppContext.BaseDirectory;
                while (!string.IsNullOrEmpty(dir))
                {
                    string file = System.IO.Path.Combine(dir, "src", "ShutdownMate", "Assets", "Strings.tr.xaml");
                    if (System.IO.File.Exists(file))
                    {
                        var rx = new System.Text.RegularExpressions.Regex("<sys:String x:Key=\"([^\"]+)\"[^>]*>(.*?)</sys:String>", System.Text.RegularExpressions.RegexOptions.Singleline);
                        foreach (System.Text.RegularExpressions.Match m in rx.Matches(System.IO.File.ReadAllText(file)))
                            _checkTexts[m.Groups[1].Value] = System.Net.WebUtility.HtmlDecode(m.Groups[2].Value);
                        break;
                    }
                    dir = System.IO.Path.GetDirectoryName(dir);
                }
            }
            string text;
            return _checkTexts.TryGetValue(key, out text) ? text.Replace('⎵', ' ') : key;
#else
            string value = Application.Current == null ? null : Application.Current.TryFindResource(key) as string;
            return value == null ? key : value.Replace('⎵', ' '); // ⎵: baştaki/sondaki boşluk işareti (XAML boşlukları kırpar)
#endif
        }

        public static string T(string key, params object[] args)
        {
            return string.Format(Culture, T(key), args);
        }
    }
}
