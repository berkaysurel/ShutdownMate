using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Shapes;
using ShutdownMate.Models;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace ShutdownMate.Controls
{
    /// <summary>
    /// Geri sayım göstergesi. Dört görünümü vardır (Modern, Klasik, Dijital, Minimal);
    /// ana pencerede ve tam ekranda aynı denetim kullanılır.
    /// </summary>
    public partial class CountdownDisplay : UserControl
    {
        private const double DialCenterX = 160;
        private const double DialCenterY = 124;
        private const double TickOuterRadius = 110;
        private const double DigitalBarWidth = 300;
        private const double MinimalBarWidth = 250;

        private static readonly Regex TimePattern = new Regex(@"^\d{1,2}(:\d{2}){1,2}$");

        private DisplayStyle _style = DisplayStyle.Modern;
        private static readonly SolidColorBrush LedAmber = Led(0xFF, 0xB0, 0x00);
        private static readonly SolidColorBrush LedRed = Led(0xFF, 0x4D, 0x3D);

        private bool _glide;
        private string _lastBrushKey;

        public CountdownDisplay()
        {
            InitializeComponent();
            BuildDial();
            ApplyStyle();
        }

        public DisplayStyle DisplayStyle
        {
            get { return _style; }
            set { _style = value; ApplyStyle(); }
        }

        /// <summary>Göstergeyi günceller.</summary>
        /// <param name="mainText">Ana metin (genellikle 00:00:00).</param>
        /// <param name="caption">Alt açıklama (kalan, sakin süre ...).</param>
        /// <param name="icon">Eylem simgesi.</param>
        /// <param name="progress">0–1 arası doluluk; negatifse gizlenir.</param>
        /// <param name="critical">Son uyarı (kırmızı).</param>
        public void Update(string mainText, string caption, SymbolRegular icon, double progress, bool critical)
        {
            double p = progress < 0 ? -1 : Math.Max(0, Math.Min(1, progress));
            string brushKey = critical ? "SystemFillColorCriticalBrush" : "AccentFillColorDefaultBrush";
            string textKey = critical ? "SystemFillColorCriticalBrush" : "TextFillColorPrimaryBrush";

            switch (_style)
            {
                case DisplayStyle.Modern:
                    ModernText.Text = mainText;
                    ModernCaption.Text = caption;
                    ModernIcon.Symbol = icon;
                    ModernArc.Visibility = p > 0.002 ? Visibility.Visible : Visibility.Hidden;
                    Glide(ModernArc, Wpf.Ui.Controls.Arc.EndAngleProperty, Math.Max(0.5, 359.9 * Math.Max(0, p)));
                    ModernText.SetResourceReference(TextBlock.ForegroundProperty, textKey);
                    break;

                case DisplayStyle.Classic:
                    ClassicText.Text = mainText;
                    ClassicCaption.Text = caption;
                    ClassicArc.Visibility = p > 0.002 ? Visibility.Visible : Visibility.Hidden;
                    Glide(ClassicArc, Wpf.Ui.Controls.Arc.EndAngleProperty, Math.Max(0.5, 359.9 * Math.Max(0, p)));
                    Glide(ClassicHandRotation, RotateTransform.AngleProperty, 360 * Math.Max(0, p));
                    ClassicText.SetResourceReference(TextBlock.ForegroundProperty, textKey);
                    ClassicHand.SetResourceReference(Shape.StrokeProperty, critical ? "SystemFillColorCriticalBrush" : "AccentTextFillColorPrimaryBrush");
                    break;

                case DisplayStyle.Digital:
                    bool isTime = TimePattern.IsMatch(mainText ?? "");
                    DigitalBoxes.Visibility = isTime ? Visibility.Visible : Visibility.Collapsed;
                    DigitalPlain.Visibility = isTime ? Visibility.Collapsed : Visibility.Visible;
                    DigitalText.Text = mainText;
                    DigitalPlain.Text = mainText;
                    // Sönük segmentler: her rakamın yerinde 8 (tüm segmentler)
                    DigitalGhost.Text = Regex.Replace(mainText ?? "", @"\d", "8");
                    DigitalCaption.Text = caption;
                    Glide(DigitalBar, WidthProperty, DigitalBarWidth * Math.Max(0, p));
                    DigitalBarHost.Visibility = p < 0 ? Visibility.Hidden : Visibility.Visible;
                    DigitalText.Foreground = critical ? LedRed : LedAmber;
                    DigitalGlow.Color = critical ? LedRed.Color : LedAmber.Color;
                    DigitalPlain.SetResourceReference(TextBlock.ForegroundProperty, textKey);
                    break;

                case DisplayStyle.Minimal:
                    MinimalText.Text = mainText;
                    MinimalCaption.Text = caption;
                    Glide(MinimalBar, WidthProperty, MinimalBarWidth * Math.Max(0, p));
                    MinimalBarHost.Visibility = p < 0 ? Visibility.Hidden : Visibility.Visible;
                    MinimalText.SetResourceReference(TextBlock.ForegroundProperty, textKey);
                    break;
            }

            _glide = true;

            if (brushKey != _lastBrushKey)
            {
                _lastBrushKey = brushKey;
                ModernArc.SetResourceReference(Shape.StrokeProperty, brushKey);
                ClassicArc.SetResourceReference(Shape.StrokeProperty, brushKey);
                DigitalBar.SetResourceReference(Shape.FillProperty, brushKey);
                MinimalBar.SetResourceReference(Shape.FillProperty, brushKey);
            }
        }

        private static SolidColorBrush Led(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        /// <summary>İlk güncelleme değeri doğrudan yazar; sonrakiler saniyede bir sıçramasın diye yumuşakça kayar.</summary>
        private void Glide(IAnimatable target, DependencyProperty property, double to)
        {
            if (!_glide) { target.BeginAnimation(property, null); ((DependencyObject)target).SetValue(property, to); return; }
            target.BeginAnimation(property, new DoubleAnimation(to, TimeSpan.FromMilliseconds(450)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }

        /// <summary>
        /// Tam ekranda gösterge çok büyütülür; ikincil yazılar ve çizgiler aynı oranda büyürse
        /// rakamları ezer. Bu yüzden tasarım boyutları küçültülür (ana pencere etkilenmez).
        /// </summary>
        public void UseFullscreenProportions()
        {
            MinimalCaption.FontSize = 8;
            MinimalBarHost.Height = 2;
            MinimalBarHost.Margin = new Thickness(0, 8, 0, 8);
            DigitalCaption.FontSize = 8;
            DigitalBarHost.Height = 3;
            DigitalBarHost.Margin = new Thickness(0, 12, 0, 8);
        }

        private void ApplyStyle()
        {
            ModernView.Visibility = _style == DisplayStyle.Modern ? Visibility.Visible : Visibility.Collapsed;
            ClassicView.Visibility = _style == DisplayStyle.Classic ? Visibility.Visible : Visibility.Collapsed;
            DigitalView.Visibility = _style == DisplayStyle.Digital ? Visibility.Visible : Visibility.Collapsed;
            MinimalView.Visibility = _style == DisplayStyle.Minimal ? Visibility.Visible : Visibility.Collapsed;
        }

        // ---------------- Klasik kadran ----------------

        /// <summary>On iki çizgi: çeyrekler biraz daha uzun ve belirgin.</summary>
        private void BuildDial()
        {
            for (int i = 0; i < 12; i++)
            {
                bool quarter = i % 3 == 0;
                double angle = i * 30 * Math.PI / 180;
                double inner = TickOuterRadius - (quarter ? 14 : 8);
                var tick = new Line
                {
                    X1 = DialCenterX + inner * Math.Sin(angle),
                    Y1 = DialCenterY - inner * Math.Cos(angle),
                    X2 = DialCenterX + TickOuterRadius * Math.Sin(angle),
                    Y2 = DialCenterY - TickOuterRadius * Math.Cos(angle),
                    StrokeThickness = quarter ? 2.5 : 1.5,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                tick.SetResourceReference(Shape.StrokeProperty, quarter ? "TextFillColorPrimaryBrush" : "TextFillColorTertiaryBrush");
                ClassicTicks.Children.Add(tick);
            }
        }
    }
}
