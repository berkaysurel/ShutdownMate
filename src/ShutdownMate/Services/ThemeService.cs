using System;
using System.Windows;
using System.Windows.Media;
using ShutdownMate.Models;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ShutdownMate.Services
{
    /// <summary>Tema, vurgu rengi ve pencere arka plan efektini uygular.</summary>
    internal static class ThemeService
    {
        /// <summary>Vurgu dolgusu üzerindeki beyaz yazı için hedeflenen en düşük kontrast (WCAG AA).</summary>
        private const double MinContrastWithWhite = 4.5;

        /// <summary>Vurgu dolgusu üzerinde yazı kullanan kaynaklar; hepsi beyaza çevrilir.</summary>
        private static readonly string[] TextOnAccentBrushKeys =
        {
            "TextOnAccentFillColorPrimaryBrush", "TextOnAccentFillColorSelectedTextBrush", "BadgeForeground",
            "AccentButtonForeground", "AccentButtonForegroundPointerOver", "CalendarViewTodayForeground",
            "CheckBoxCheckGlyphForeground", "ListBoxItemSelectedForegroundThemeBrush", "RadioButtonCheckGlyphFill",
            "ToggleButtonForegroundChecked", "ToggleSwitchKnobFillOn", "ToggleSwitchKnobFillOnPointerOver"
        };

        private static readonly string[] TextOnAccentSecondaryBrushKeys =
        {
            "TextOnAccentFillColorSecondaryBrush", "AccentButtonForegroundPressed",
            "ToggleButtonForegroundCheckedPressed", "ToggleSwitchKnobFillOnPressed"
        };

        private static bool _watching;
        private static bool _subscribed;
        private static string _accentHex = "";

        public static void Apply(FluentWindow window, AppSettings settings)
        {
            if (!_subscribed)
            {
                // Sistem teması değişip kütüphane vurgu rengini yeniden yazdığında okunabilirliği koru
                ApplicationThemeManager.Changed += (theme, accent) => ApplyAccent(_accentHex);
                _subscribed = true;
            }

            bool systemAccent = string.IsNullOrEmpty(settings.AccentColor);
            if (_watching && window.IsLoaded)
            {
                try { SystemThemeWatcher.UnWatch(window); } catch { }
            }
            _watching = false;

            switch (settings.Theme)
            {
                case AppThemeMode.Light:
                    ApplicationThemeManager.Apply(ApplicationTheme.Light, settings.Backdrop, false);
                    break;
                case AppThemeMode.Dark:
                    ApplicationThemeManager.Apply(ApplicationTheme.Dark, settings.Backdrop, false);
                    break;
                default:
                    ApplicationThemeManager.ApplySystemTheme(false);
                    SystemThemeWatcher.Watch(window, settings.Backdrop, systemAccent);
                    _watching = true;
                    break;
            }

            ApplyAccent(settings.AccentColor);
            ApplyBackdrop(window, settings.Backdrop);
        }

        public static void ApplyAccent(string hex)
        {
            _accentHex = hex ?? "";
            Color color;
            bool custom = TryParseColor(_accentHex, out color);
            if (custom)
                ApplicationAccentColorManager.Apply(color, ApplicationThemeManager.GetAppTheme());
            else
            {
                ApplicationAccentColorManager.ApplySystemAccent();
                color = ApplicationAccentColorManager.GetColorizationColor();
            }
            ApplyReadableAccentFill(color);
        }

        /// <summary>
        /// Koyu temada kütüphane vurgu dolgusunu açık mavi, üstündeki yazıyı siyah yapar; bu göz yorar.
        /// Dolgu, beyaz yazıyla en az 4.5:1 kontrast verecek kadar koyulaştırılır ve yazılar beyaz yapılır.
        /// Koyu zemin üzerindeki vurgu metinleri (AccentTextFillColor*) açık kalır.
        /// </summary>
        private static void ApplyReadableAccentFill(Color accent)
        {
            var resources = Application.Current != null ? Application.Current.Resources : null;
            if (resources == null) return;

            Color fill = accent;
            for (int i = 0; i < 40 && Contrast(fill, Colors.White) < MinContrastWithWhite; i++)
                fill = Mix(fill, Colors.Black, 0.06);

            resources["AccentFillColorDefault"] = fill;
            resources["AccentFillColorSecondary"] = Color.FromArgb(0xE6, fill.R, fill.G, fill.B);
            resources["AccentFillColorTertiary"] = Color.FromArgb(0xCC, fill.R, fill.G, fill.B);
            resources["AccentFillColorDefaultBrush"] = Frozen(fill);
            resources["AccentFillColorSecondaryBrush"] = Frozen(Color.FromArgb(0xE6, fill.R, fill.G, fill.B));
            resources["AccentFillColorTertiaryBrush"] = Frozen(Color.FromArgb(0xCC, fill.R, fill.G, fill.B));

            Color white = Colors.White;
            Color whiteSoft = Color.FromArgb(0xD9, 0xFF, 0xFF, 0xFF);
            resources["TextOnAccentFillColorPrimary"] = white;
            resources["TextOnAccentFillColorSecondary"] = whiteSoft;
            foreach (string key in TextOnAccentBrushKeys) resources[key] = Frozen(white);
            foreach (string key in TextOnAccentSecondaryBrushKeys) resources[key] = Frozen(whiteSoft);
        }

        public static void ApplyBackdrop(FluentWindow window, WindowBackdropType backdrop)
        {
            // Mica/Akrilik desteklenmeyen sistemlerde (Windows 10) pencere saydam kalmasın, düz arka plana düşülür
            if (!WindowBackdrop.IsSupported(backdrop)) backdrop = WindowBackdropType.None;

            window.WindowBackdropType = backdrop;
            if (backdrop == WindowBackdropType.None)
                window.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "ApplicationBackgroundBrush");
            else
                window.Background = Brushes.Transparent;
        }

        public static bool TryParseColor(string hex, out Color color)
        {
            color = default(Color);
            if (string.IsNullOrWhiteSpace(hex)) return false;
            try
            {
                color = (Color)ColorConverter.ConvertFromString(hex);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ---------------- Renk yardımcıları ----------------

        private static SolidColorBrush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Color Mix(Color a, Color b, double amount)
        {
            return Color.FromRgb(
                (byte)(a.R + (b.R - a.R) * amount),
                (byte)(a.G + (b.G - a.G) * amount),
                (byte)(a.B + (b.B - a.B) * amount));
        }

        private static double Luminance(Color c)
        {
            Func<byte, double> channel = v =>
            {
                double s = v / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            };
            return 0.2126 * channel(c.R) + 0.7152 * channel(c.G) + 0.0722 * channel(c.B);
        }

        private static double Contrast(Color a, Color b)
        {
            double la = Luminance(a), lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }
    }
}
