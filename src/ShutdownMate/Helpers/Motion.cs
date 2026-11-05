using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ShutdownMate.Helpers
{
    /// <summary>
    /// Yumuşak açılış ve basma animasyonları. Yalnızca opaklık ve kaydırma/ölçek (GPU) kullanır, bekleyen
    /// kaynak tutmaz. Ayarlardan ya da Windows'ta animasyonlar kapalıysa hiçbir şey animasyonlanmaz.
    /// </summary>
    internal static class Motion
    {
        private static bool _userEnabled = true;

        public static bool Enabled
        {
            get { return _userEnabled && SystemParameters.ClientAreaAnimation; }
            set { _userEnabled = value; }
        }

        static Motion()
        {
            // Düğmelere basma hissi (düğmeler ve seçim kartları)
            EventManager.RegisterClassHandler(typeof(ButtonBase), UIElement.PreviewMouseLeftButtonDownEvent,
                new RoutedEventHandler((s, e) => Press((ButtonBase)s, 0.97, 70)));
            EventManager.RegisterClassHandler(typeof(ButtonBase), UIElement.MouseLeftButtonUpEvent,
                new RoutedEventHandler((s, e) => Press((ButtonBase)s, 1, 120)), true);
            EventManager.RegisterClassHandler(typeof(ButtonBase), UIElement.MouseLeaveEvent,
                new RoutedEventHandler((s, e) => Press((ButtonBase)s, 1, 120)));

            // İşaret kutusu: işaret kalkarken, konulurkenkinin tersi oynar
            EventManager.RegisterClassHandler(typeof(CheckBox), ToggleButton.UncheckedEvent,
                new RoutedEventHandler((s, e) => FadeCheckMark((CheckBox)s)));
            EventManager.RegisterClassHandler(typeof(CheckBox), ToggleButton.CheckedEvent,
                new RoutedEventHandler((s, e) => ResetCheckMark((CheckBox)s)));

            // Açılır listeler (açılış ve kapanış)
            EventManager.RegisterClassHandler(typeof(ComboBox), FrameworkElement.LoadedEvent,
                new RoutedEventHandler((s, e) =>
                {
                    var combo = (ComboBox)s;
                    // Loaded birden çok kez gelebilir; işleyici bir kez bağlı kalır
                    combo.DropDownOpened -= ComboOpened;
                    combo.DropDownOpened += ComboOpened;
                    combo.DropDownClosed -= ComboClosed;
                    combo.DropDownClosed += ComboClosed;
                }));
        }

        private static void ComboOpened(object sender, EventArgs e) { RememberOrigin((ComboBox)sender); }

        private static void ComboClosed(object sender, EventArgs e) { OnDropDownClosed((ComboBox)sender); }

        // Kapanma animasyonu süren elemanlar (yeniden açılırsa iptal edilir)
        private static readonly HashSet<UIElement> Hiding = new HashSet<UIElement>();

        /// <summary>
        /// Görünürlüğü değiştirir. expand=true: panel açılırken yükseklik ve opaklıkla büyür, kapanırken küçülüp solar
        /// (altındaki içerik yumuşakça kayar). expand=false: açılırken solarak belirir, kapanırken anında kalkar;
        /// yan yana geçişlerde (sekme, mod) eski panel solarsa altındaki içerik sıçrar.
        /// </summary>
        public static void SetVisible(UIElement element, bool visible, int delayMs = 0, bool expand = false)
        {
            if (visible)
            {
                if (Hiding.Remove(element))
                {
                    // Küçülürken yeniden açıldı: kapanmayı bırak, baştan aç
                    Reset(element);
                    Appear(element, delayMs, expand);
                    return;
                }
                if (element.Visibility == Visibility.Visible) return;
                element.Visibility = Visibility.Visible;
                Appear(element, delayMs, expand);
                return;
            }

            if (element.Visibility != Visibility.Visible || Hiding.Contains(element)) return;
            var fe = element as FrameworkElement;
            if (!expand || fe == null || !Enabled || !element.IsVisible)
            {
                element.Visibility = Visibility.Collapsed;
                return;
            }

            Hiding.Add(element);
            var scale = new ScaleTransform(1, 1);
            fe.LayoutTransform = scale;
            fe.RenderTransformOrigin = new Point(0, 0);
            var shrink = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
                FillBehavior = FillBehavior.HoldEnd
            };
            shrink.Completed += (s, e) =>
            {
                if (!Hiding.Remove(element)) return;
                element.Visibility = Visibility.Collapsed;
                Reset(element);
            };
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
            element.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(140)) { FillBehavior = FillBehavior.HoldEnd });
        }

        /// <summary>Animasyon izlerini temizler, eleman normal haline döner.</summary>
        private static void Reset(UIElement element)
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = 1;
            element.ClearValue(FrameworkElement.LayoutTransformProperty);
            element.ClearValue(UIElement.RenderTransformProperty);
        }

        /// <summary>Opaklık 0→1; expand ise yükseklik 0→1, değilse hafif yukarı kayma.</summary>
        public static void Appear(UIElement element, int delayMs = 0, bool expand = false)
        {
            if (!Enabled || !element.IsVisible) return;

            TimeSpan delay = TimeSpan.FromMilliseconds(delayMs);

            // Gecikme boyunca eleman görünmesin diye başlangıç değeri yerel olarak 0 tutulur
            element.Opacity = 0;
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)) { BeginTime = delay, FillBehavior = FillBehavior.Stop };
            fade.Completed += (s, e) => element.Opacity = 1;
            element.BeginAnimation(UIElement.OpacityProperty, fade);

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var fe = element as FrameworkElement;
            if (expand && fe != null)
            {
                var scale = new ScaleTransform(1, 0);
                fe.LayoutTransform = scale;
                fe.RenderTransformOrigin = new Point(0, 0);
                var grow = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { BeginTime = delay, EasingFunction = ease };
                grow.Completed += (s, e) => fe.ClearValue(FrameworkElement.LayoutTransformProperty);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            }
            else if (element.RenderTransform == null || element.RenderTransform == Transform.Identity)
            {
                var move = new TranslateTransform(0, 8);
                element.RenderTransform = move;
                var slide = new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(200)) { BeginTime = delay, EasingFunction = ease };
                slide.Completed += (s, e) => element.ClearValue(UIElement.RenderTransformProperty);
                move.BeginAnimation(TranslateTransform.YProperty, slide);
            }
        }

        // Wpf.Ui şablonu işareti 0→1 (Tag) ile çiziyor, kaldırırken anında gizliyor; burada aynı çizimi 1→0 oynatıyoruz
        private static FrameworkElement CheckMark(CheckBox box)
        {
            return box.Template == null ? null : box.Template.FindName("ControlIcon", box) as FrameworkElement;
        }

        private static void FadeCheckMark(CheckBox box)
        {
            var icon = CheckMark(box);
            if (icon == null || !Enabled || !box.IsVisible) return;

            icon.Visibility = Visibility.Visible; // yerel değer, şablonun gizlemesini geçer
            var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
                FillBehavior = FillBehavior.HoldEnd
            };
            anim.Completed += (s, e) => ResetCheckMark(box);
            icon.BeginAnimation(FrameworkElement.TagProperty, anim);
        }

        private static void ResetCheckMark(CheckBox box)
        {
            var icon = CheckMark(box);
            if (icon == null) return;
            icon.BeginAnimation(FrameworkElement.TagProperty, null);
            icon.ClearValue(UIElement.VisibilityProperty);
            icon.Tag = 1.0;
        }

        /// <summary>Yüksekliği animasyonla değiştirir; açıkken bitince otomatik yüksekliğe döner.</summary>
        public static void AnimateHeight(FrameworkElement body, double from, double to, bool autoWhenDone)
        {
            body.BeginAnimation(FrameworkElement.HeightProperty, null);
            if (!Enabled)
            {
                body.Height = autoWhenDone ? double.NaN : to;
                return;
            }
            body.Height = from;
            var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            anim.Completed += (s, e) => body.Height = autoWhenDone ? double.NaN : to;
            body.BeginAnimation(FrameworkElement.HeightProperty, anim);
        }

        /// <summary>Listeden eleman çıkarırken küçülüp solar, sonra onDone çalışır (animasyon kapalıysa hemen).</summary>
        public static void Remove(FrameworkElement element, Action onDone)
        {
            if (!Enabled || !element.IsVisible)
            {
                onDone();
                return;
            }
            var scale = new ScaleTransform(1, 1);
            element.LayoutTransform = scale;
            element.RenderTransformOrigin = new Point(0, 0);
            var shrink = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
                FillBehavior = FillBehavior.HoldEnd
            };
            shrink.Completed += (s, e) => onDone();
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
            element.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(120)) { FillBehavior = FillBehavior.HoldEnd });
        }

        private static void Press(ButtonBase button, double scale, int ms)
        {
            // Yalnızca düğmeler ve seçim kartları (anahtar satırları küçülmez). Bırakma her zaman çalışır: basılıyken animasyon kapatılırsa düğme küçük kalmasın
            if (!(button is Button || button is RadioButton) || (!Enabled && scale != 1)) return;

            var transform = button.RenderTransform as ScaleTransform;
            if (transform == null)
            {
                if (scale == 1 || (button.RenderTransform != null && button.RenderTransform != Transform.Identity)) return;
                transform = new ScaleTransform(1, 1);
                button.RenderTransform = transform;
                button.RenderTransformOrigin = new Point(0.5, 0.5);
            }

            var anim = new DoubleAnimation(scale, TimeSpan.FromMilliseconds(ms));
            if (scale == 1)
            {
                // Bitince dönüşümü kaldır ki düğme normal çizime dönsün
                anim.Completed += (s, e) =>
                {
                    if (transform.ScaleX == 1) button.ClearValue(UIElement.RenderTransformProperty);
                };
            }
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }

        // Listenin açıldığı andaki ekran konumu (piksel); hayalet tam aynı yere konur
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ComboBox, object> Origins =
            new System.Runtime.CompilerServices.ConditionalWeakTable<ComboBox, object>();

        private static void RememberOrigin(ComboBox combo)
        {
            combo.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                var popup = combo.Template.FindName("Popup", combo) as Popup;
                var content = popup == null ? null : popup.Child as FrameworkElement;
                if (content == null || !content.IsVisible) return;
                Origins.Remove(combo);
                Origins.Add(combo, content.PointToScreen(new Point(0, 0)));
            }));
        }

        // Wpf.Ui şablonu açılışı kaydırıyor ama kapanışı animasyonlamıyor; popup kapanınca son görüntüsü
        // bir "hayalet" popup'ta solarak kaybolur.
        private static void OnDropDownClosed(ComboBox combo)
        {
            if (!Enabled || !combo.IsVisible) return;
            var popup = combo.Template.FindName("Popup", combo) as Popup;
            var content = popup == null ? null : popup.Child as FrameworkElement;
            if (content == null || content.ActualWidth < 1 || content.ActualHeight < 1) return;

            object origin;
            if (!Origins.TryGetValue(combo, out origin)) return;
            Point at = (Point)origin;


            // Render görseli kenar boşluğu (gölge payı) kadar içeriden çizer; boşluk da bitmap'e katılır, hayalet o kadar geriden başlar
            Thickness m = content.Margin;
            double width = content.ActualWidth + m.Left + m.Right, height = content.ActualHeight + m.Top + m.Bottom;
            DpiScale dpi = VisualTreeHelper.GetDpi(combo);
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                (int)Math.Ceiling(width * dpi.DpiScaleX), (int)Math.Ceiling(height * dpi.DpiScaleY),
                96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
            bitmap.Render(content);

            var image = new Image { Source = bitmap, Width = width, Height = height, IsHitTestVisible = false };
            var ghost = new Popup
            {
                AllowsTransparency = true,
                StaysOpen = true,
                Focusable = false,
                Placement = PlacementMode.Absolute,
                HorizontalOffset = at.X / dpi.DpiScaleX - m.Left,
                VerticalOffset = at.Y / dpi.DpiScaleY - m.Top,
                Child = image,
                IsOpen = true
            };
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(140));
            fade.Completed += (s, e) => ghost.IsOpen = false;
            image.BeginAnimation(UIElement.OpacityProperty, fade);
        }
    }
}
