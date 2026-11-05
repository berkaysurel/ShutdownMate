using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ShutdownMate.Controls;
using ShutdownMate.Helpers;
using ShutdownMate.Models;
using ShutdownMate.Services;
using Microsoft.Win32;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace ShutdownMate.Views
{
    /// <summary>Göstergenin anlık durumu; MainWindow her tikte gönderir.</summary>
    public sealed class CountdownState
    {
        public string MainText;
        public string Caption;
        public string Target;
        public string Status;
        /// <summary>0–1 arası doluluk; negatifse gizlenir.</summary>
        public double Progress;
        public bool Critical;
        public SymbolRegular Icon;
        /// <summary>Alt satırdaki ölçüm çipleri; boş ya da null ise yerine durum satırı gösterilir.</summary>
        public List<MetricChipState> Chips;
        /// <summary>Son kapanış uyarısı modu.</summary>
        public bool FinalAlert;
        /// <summary>Zamanlayıcısız kilit: MainText geçen süredir, hedef satırı yoktur.</summary>
        public bool IdleLock;
        /// <summary>Son uyarıda kalan saniye.</summary>
        public int FinalSecondsLeft;
        /// <summary>Son uyarı süresinin kalan oranı (0–1).</summary>
        public double FinalProgress;
        public string FinalHeadline;
        public string FinalDetail;
        /// <summary>Kullanılıyorsa ertele açıkken gösterilen bilgi; null ise kutu ve "Şimdi ..." düğmesi gizlenir.</summary>
        public string FinalNotice;
        /// <summary>"Şimdi kapat" gibi elle işlem düğmesi.</summary>
        public string FinalNowLabel;
        public string FinalConfirmText;
        public string FinalConfirmLabel;
        /// <summary>Sistem verileri (ölçüm yoksa negatif).</summary>
        public double Cpu = -1;
        public double Gpu = -1;
        public double DownloadBps = -1;
        public double UploadBps = -1;
        public double TransferBps = -1;
    }

    /// <summary>
    /// Girdi kilitli tam ekran geri sayım. Klavye ve fare tıklamaları yok sayılır;
    /// yalnızca Esc tuşu veya çıkış düğmesini basılı tutmak pencereden çıkar.
    /// Son uyarı modunda yalnızca "İptal et" ve "Ertele" düğmeleri (ve Enter/Esc/E tuşları) çalışır.
    /// Zamanlayıcıyı durdurmaz.
    /// </summary>
    public partial class FullscreenWindow : Window
    {
        // Referans yerleşim boyutu (DIP)
        private const double ReferenceWidth = 1920;
        private const double ReferenceHeight = 1080;

        private static readonly TimeSpan CursorHideDelay = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan ChromeHideDelay = TimeSpan.FromSeconds(4);
        private static readonly TimeSpan BlackoutRevealDuration = TimeSpan.FromSeconds(5);
        private const double DimOpacity = 0.22; // soluk karartmada öndeki öğelerin görünürlüğü

        private static readonly FontFamily TextFont = new FontFamily("Segoe UI Variable Text, Segoe UI");

        private readonly DispatcherTimer _uiTimer;
        private readonly AppSettings _settings;
        private readonly FrameworkElement[] _chrome;
        private readonly bool _lockOnly;
        private KeyboardBlocker _keyboard;
        private long _lastMetricTicks; // Environment.TickCount64: sistem saati değişse de aralık bozulmaz

        private DateTime? _holdStart;
        private DateTime _lastActivity = DateTime.Now;
        private Point _lastMousePoint;
        private bool _allowClose;
        private bool _critical;
        private bool _finalAlert;
        private bool _confirming;
        private bool _sceneVisible = true;
        private bool _chromeVisible = true;
        private bool _cursorHidden;

        private string _chipSignature;
        private string _clockShown;
        private string _dateShown;
        private int _lastFinalSeconds = -1;

        /// <summary>Kullanıcı tam ekrandan çıkmak istediğinde (Esc / basılı tutma) tetiklenir.</summary>
        public event EventHandler ExitRequested;

        /// <summary>Son uyarıda "İptal et" seçildiğinde tetiklenir.</summary>
        public event EventHandler CancelRequested;

        /// <summary>Son uyarıda "Ertele" seçildiğinde tetiklenir.</summary>
        public event EventHandler SnoozeRequested;

        /// <summary>Son uyarıda işlem elle, ikinci onayla seçildiğinde tetiklenir.</summary>
        public event EventHandler ExecuteRequested;

        /// <param name="lockOnly">Ekran kilidi: pencere saydamdır, arkadaki ekran görünür; girdi kilidi ve çıkış aynıdır.</param>
        public FullscreenWindow(Window owner, AppSettings settings, BitmapSource desktopSnapshot, bool lockOnly = false)
        {
            InitializeComponent();
            _settings = settings;
            _lockOnly = lockOnly;

            // Ana pencerenin bulunduğu ekranda açıl, sonra ekranı kapla
            if (owner != null)
            {
                // Simge durumundaki pencerenin konumu ekran dışıdır (-32000); son normal konum kullanılır
                Rect bounds = owner.WindowState == WindowState.Minimized ? owner.RestoreBounds : new Rect(owner.Left, owner.Top, 1, 1);
                if (!bounds.IsEmpty)
                {
                    Left = bounds.Left + 20;
                    Top = bounds.Top + 20;
                }
            }
            SourceInitialized += (s, e) =>
            {
                WindowState = WindowState.Maximized;
                // Gözetim kaydının ekran görüntüleri bu pencereyi değil arkasındaki asıl ekranı göstersin
                ScreenCapture.ExcludeFromCapture(this);
                // Pencere gerçekten açılınca kurulur; kurulamazsa kilit yine açılır, yalnızca Windows tuşu ve Alt+Tab engellenmez
                try { _keyboard = new KeyboardBlocker(); }
                catch (Win32Exception) { }
            };

            // Çözünürlük ya da ekran düzeni tam ekrandayken değişirse pencere yeni ekrana yeniden oturur
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

            ApplyDisplayLayout();
            ApplyMetricTiles();

            ClockPanel.Visibility = settings.FullscreenShowClock ? Visibility.Visible : Visibility.Collapsed;
            LockPill.Visibility = settings.FullscreenShowLock ? Visibility.Visible : Visibility.Collapsed;
            MessageText.Text = settings.LockMessage;
            MessageIcon.Visibility = settings.ShowLockMessageIcon ? Visibility.Visible : Visibility.Collapsed;
            MessageCard.Visibility = settings.ShowLockMessage &&!string.IsNullOrWhiteSpace(settings.LockMessage)
                ? Visibility.Visible : Visibility.Collapsed;
            SnoozeButton.Content = "+" + settings.SnoozeMinutes.ToString(CultureInfo.InvariantCulture) + Loc.T("FullscreenWindow_DkErtele");
            bool hold = settings.ExitHoldMilliseconds > 0;
            ExitTitleText.Text = hold ? Loc.T("FullscreenWindow_CikmakIcinBasiliTutun") : Loc.T("FullscreenWindow_CikmakIcinTiklayin");
            ExitHintText.Text = hold
                ? Loc.T(settings.ExitHoldMilliseconds == 1000 ? "Full_HoldSecondOrEsc" : "Full_HoldSecondsOrEsc", (settings.ExitHoldMilliseconds / 1000.0).ToString("0.#", Loc.Culture))
                : Loc.T("FullscreenWindow_YaDaEscTusuna");
            ExitButton.ToolTip = Loc.T("FullscreenWindow_Cikis") + (hold ? Loc.T("FullscreenWindow_BasiliTutun") : Loc.T("FullscreenWindow_Tiklayin")) + Loc.T("FullscreenWindow_EscTusuDaCikar");

            // Sistem verileri bilerek bu listede değil: açıldıysa hareketsizken de görünür kalır
            _chrome = new FrameworkElement[] { ClockPanel, LockPill, BottomCenter, ExitPanel };

            if (lockOnly) ApplyLockLayout();
            else ApplyBackground(desktopSnapshot);
            ApplyDesign();
            UpdateClock(DateTime.Now);

            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _uiTimer.Tick += (s, e) => UiTick();
            _uiTimer.Start();

            Loaded += (s, e) =>
            {
                Activate();
                Focus();
                PlayEntrance();
            };
        }

        // =====================================================================
        // Kişiselleştirme
        // =====================================================================

        /// <summary>Ana penceredeki gösterge denetimi kullanılır; boyutu ayardan, oranı gösterge stilinden gelir.</summary>
        private void ApplyDisplayLayout()
        {
            DisplayStyle style = _settings.DisplayStyle;
            Display.DisplayStyle = style;
            Display.UseFullscreenProportions();
            int size = _settings.FullscreenDisplaySize;

            // Boyutlar sabittir: referans alanında (1080 yükseklik) ölçülür, ekranla orantılı büyür ama boş alanı doldurmaz
            if (style == DisplayStyle.Digital || style == DisplayStyle.Minimal)
            {
                // Yatay göstergeler: kare tasarım alanı kırpılır
                Display.Height = style == DisplayStyle.Digital ? 150 : 130;
                StyledBox.Width = size == 1 ? 450 : size == 3 ? 900 : 675;
            }
            else
            {
                StyledBox.Width = size == 1 ? 260 : size == 3 ? 520 : 390;
            }
        }

        /// <summary>
        /// Ekran kilidi görünümü: neredeyse tam saydam pencere tıklamaları yakalar (tam saydam olsaydı alttaki
        /// programa geçerdi). Masaüstü her renkte olabileceği için kartlar koyu ve opak yapılır.
        /// </summary>
        private void ApplyLockLayout()
        {
            AllowsTransparency = true;
            Background = Frozen(Color.FromArgb(0x01, 0x00, 0x00, 0x00));
            BackgroundLayer.Visibility = Visibility.Collapsed;
            BottomCenter.Visibility = Visibility.Collapsed;

            int timer = _settings.LockTimerMode;
            bool big = timer == 2;
            HeroBlock.Visibility = big ? Visibility.Visible : Visibility.Collapsed;
            HeroBackdrop.Visibility = big ? Visibility.Visible : Visibility.Collapsed;
            LockTimeText.Visibility = timer == 1 ? Visibility.Visible : Visibility.Collapsed;
            LockTargetText.Visibility = _settings.LockShowTarget ? Visibility.Visible : Visibility.Collapsed;
            LockStatusCard.Visibility = !big && (timer == 1 || _settings.LockShowTarget) ? Visibility.Visible : Visibility.Collapsed;
            LockPill.Visibility = _settings.LockShowLabel ? Visibility.Visible : Visibility.Collapsed;
            ClockPanel.Visibility = _settings.LockShowClock ? Visibility.Visible : Visibility.Collapsed;

            UseLightText();
            Scene.Resources["CardBackgroundFillColorDefaultBrush"] = Frozen(Color.FromArgb(0xF2, 0x20, 0x20, 0x20));
            Scene.Resources["CardBackgroundFillColorSecondaryBrush"] = Frozen(Color.FromArgb(0xF2, 0x32, 0x32, 0x32));
            ClockPanel.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
            ClockPanel.Padding = new Thickness(16, 8, 16, 8);

            if (_settings.LockExitPanelTop)
            {
                Grid.SetRow(ExitPanel, 0);
                ExitPanel.Margin = new Thickness(0);
            }
        }

        /// <summary>
        /// Kişiselleştirme (tam ekran ve ekran kilidi ortak): vurgu rengi, kart görünümü, boyut ve gösterge yeri.
        /// Arka plan ayarlandıktan sonra çağrılır; kartlar o anki renklerinin saydamlığı değiştirilerek elde edilir.
        /// </summary>
        private void ApplyDesign()
        {
            AppSettings s = _settings;

            Color accent;
            if (ThemeService.TryParseColor(s.FullscreenAccent, out accent))
            {
                Scene.Resources["AccentFillColorDefaultBrush"] = Frozen(accent);
                Scene.Resources["AccentTextFillColorPrimaryBrush"] = Frozen(accent);
            }

            // 0 cam: kilitte arka plan karışık olabileceği için biraz daha opak; 1 düz: opak; 2 kartsız
            // Koyu temada kart zemini düşük saydamlıkta beyazdır; "düz" için önce arka planın üstüne karıştırılır
            Color under = (SceneBackground.Fill as SolidColorBrush)?.Color ?? Colors.Black;
            foreach (string key in new[] { "CardBackgroundFillColorDefaultBrush", "CardBackgroundFillColorSecondaryBrush" })
            {
                var brush = Scene.TryFindResource(key) as SolidColorBrush;
                if (brush == null) continue;
                Color c = brush.Color;
                if (s.FullscreenCardStyle == 1)
                {
                    double a = c.A / 255.0;
                    c = Color.FromArgb(0xF2, (byte)(c.R * a + under.R * (1 - a)), (byte)(c.G * a + under.G * (1 - a)), (byte)(c.B * a + under.B * (1 - a)));
                }
                else if (s.FullscreenCardStyle == 2) c.A = 0;
                else if (_lockOnly) c.A = 0xB8;
                Scene.Resources[key] = Frozen(c);
            }
            if (s.FullscreenCardStyle == 2)
            {
                Scene.Resources["CardStrokeColorDefaultBrush"] = Brushes.Transparent;
                // Çıkış düğmesi kartsızda da seçilebilir kalır
                ExitCircle.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "ControlFillColorDefaultBrush");
                ExitCircle.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "ControlStrokeColorDefaultBrush");
                // Kilitte arka plan karışık olabilir: yazılar gölgeyle okunur kalır
                if (_lockOnly)
                    NormalLayer.Effect = new System.Windows.Media.Effects.DropShadowEffect { ShadowDepth = 0, BlurRadius = 14, Opacity = 0.85, Color = Colors.Black };
            }

            // Boyut: gösterge boyutuyla birlikte yardımcı öğeler de ölçeklenir
            double factor = s.FullscreenDisplaySize == 1 ? 0.7 : s.FullscreenDisplaySize == 3 ? 1.2 : 0.9;
            if (factor != 1.0)
            {
                foreach (FrameworkElement element in new FrameworkElement[] { ClockPanel, LockPill, BottomCenter, LockStatusCard, ExitPanel })
                    element.LayoutTransform = new ScaleTransform(factor, factor);
            }

            // Mesaj gösterge boyutuna bağlıdır ve sayaçtan küçük kalır (hiyerarşi: sayaç > mesaj > yardımcı yazılar)
            int size = s.FullscreenDisplaySize;
            MessageText.FontSize = size == 1 ? 16 : size == 3 ? 26 : 20;
            MessageIcon.FontSize = size == 1 ? 18 : size == 3 ? 24 : 20;

            // Sistem verisi kartları kendi boyut ayarını da taşır (genel boyutla çarpılır)
            double metricFactor = factor * (s.FullscreenMetricSize == 1 ? 0.8 : s.FullscreenMetricSize == 3 ? 1.3 : 1.0);
            if (metricFactor != 1.0) MetricsPanel.LayoutTransform = new ScaleTransform(metricFactor, metricFactor);
        }

        private void ApplyMetricTiles()
        {
            AppSettings s = _settings;
            MetricsPanel.Visibility = (_lockOnly ? s.LockShowMetrics : s.FullscreenShowMetrics) ? Visibility.Visible : Visibility.Collapsed;

            ConfigureTile(CpuTile, SymbolRegular.DeveloperBoard24, Loc.T("FullscreenWindow_Islemci"), 100, _lockOnly ? s.LockMetricCpu : s.FullscreenMetricCpu);
            ConfigureTile(GpuTile, SymbolRegular.VideoClip24, Loc.T("FullscreenWindow_EkranKarti"), 100, _lockOnly ? s.LockMetricGpu : s.FullscreenMetricGpu);
            ConfigureTile(DownloadTile, SymbolRegular.ArrowDownload24, Loc.T("FullscreenWindow_Indirme"), 0, _lockOnly ? s.LockMetricDownload : s.FullscreenMetricDownload);
            ConfigureTile(UploadTile, SymbolRegular.ArrowUpload24, Loc.T("FullscreenWindow_Yukleme"), 0, _lockOnly ? s.LockMetricUpload : s.FullscreenMetricUpload);
            ConfigureTile(TransferTile, SymbolRegular.FolderArrowRight24, Loc.T("FullscreenWindow_Aktarim"), 0, _lockOnly ? s.LockMetricTransfer : s.FullscreenMetricTransfer);
            bool graph = _lockOnly ? s.LockMetricGraph : s.FullscreenMetricGraph;
            foreach (MetricTile tile in new[] { CpuTile, GpuTile, DownloadTile, UploadTile, TransferTile }) tile.ShowGraph = graph;
        }

        private static void ConfigureTile(MetricTile tile, SymbolRegular symbol, string label, double fixedMaximum, bool visible)
        {
            tile.Symbol = symbol;
            tile.Label = label;
            tile.FixedMaximum = fixedMaximum;
            tile.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Ölçümler saniyede bir güncellenir; durum daha sık gelse de eğri düzgün ilerler.</summary>
        private void UpdateMetrics(CountdownState state)
        {
            if (MetricsPanel.Visibility != Visibility.Visible) return;
            long ticks = Environment.TickCount64;
            if (ticks - _lastMetricTicks < 950) return;
            _lastMetricTicks = ticks;

            CpuTile.Push(state.Cpu, TextFormat.Percent(state.Cpu));
            GpuTile.Push(state.Gpu, TextFormat.Percent(state.Gpu));
            DownloadTile.Push(state.DownloadBps, TextFormat.Speed(state.DownloadBps));
            UploadTile.Push(state.UploadBps, TextFormat.Speed(state.UploadBps));
            TransferTile.Push(state.TransferBps, TextFormat.Speed(state.TransferBps));
        }

        // =====================================================================
        // Arka plan
        // =====================================================================

        private void ApplyBackground(BitmapSource snapshot)
        {
            FullscreenBackground mode = _settings.FullscreenBackground;
            if (mode == FullscreenBackground.Blur && snapshot == null) mode = FullscreenBackground.Solid;

            switch (mode)
            {
                case FullscreenBackground.Solid:
                {
                    Color color;
                    if (!ThemeService.TryParseColor(_settings.FullscreenColor, out color)) color = Colors.Black;
                    SceneBackground.Fill = Frozen(color);
                    UseLightText();
                    break;
                }
                case FullscreenBackground.Blur:
                    SceneBackground.Fill = Brushes.Black;
                    BackdropImage.Source = snapshot;
                    BackdropImage.Visibility = Visibility.Visible;
                    BackdropBlur.Radius = Math.Max(5, _settings.FullscreenBlur);
                    BackdropDim.Visibility = Visibility.Visible;
                    UseLightText();
                    break;
            }
        }

        /// <summary>Koyu arka planlarda (düz renk, bulanık) temadan bağımsız açık renk yazı kullanılır.</summary>
        private void UseLightText()
        {
            Scene.Resources["TextFillColorPrimaryBrush"] = Frozen(Color.FromRgb(0xFF, 0xFF, 0xFF));
            Scene.Resources["TextFillColorSecondaryBrush"] = Frozen(Color.FromArgb(0xC8, 0xFF, 0xFF, 0xFF));
            Scene.Resources["TextFillColorTertiaryBrush"] = Frozen(Color.FromArgb(0x8A, 0xFF, 0xFF, 0xFF));
            Scene.Resources["ControlStrokeColorDefaultBrush"] = Frozen(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
            Scene.Resources["ControlFillColorDefaultBrush"] = Frozen(Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF));
            Scene.Resources["CardBackgroundFillColorDefaultBrush"] = Frozen(Color.FromArgb(0x59, 0x00, 0x00, 0x00));
            Scene.Resources["CardBackgroundFillColorSecondaryBrush"] = Frozen(Color.FromArgb(0x80, 0x00, 0x00, 0x00));
            Scene.Resources["CardStrokeColorDefaultBrush"] = Frozen(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
        }

        /// <summary>
        /// Ön plan her ekrana uyar: dar kenar 1920x1080 referansına ölçeklenir, yerleşim alanı ekranın
        /// oranına göre genişler. Böylece 4:3, ultra geniş ve dikey ekranlarda da köşeler ekranın köşesinde
        /// kalır, hiçbir şey taşmaz. Windows'ta hangi çözünürlük ve ölçek geçerliyse o kullanılır; uygulama bunları değiştirmez.
        /// </summary>
        private void Scene_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double w = Scene.ActualWidth;
            double h = Scene.ActualHeight;
            if (w <= 0 || h <= 0) return;
            double scale = Math.Min(w / ReferenceWidth, h / ReferenceHeight);
            ScaleRoot.Width = w / scale;
            ScaleRoot.Height = h / scale;
            RootScale.ScaleX = scale;
            RootScale.ScaleY = scale;
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            // Sistem olayı başka iş parçacığından gelebilir
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_allowClose) return;
                WindowState = WindowState.Normal;
                WindowState = WindowState.Maximized;
            }));
        }

        // =====================================================================
        // Durum güncelleme
        // =====================================================================

        public void Update(CountdownState state)
        {
            if (state == null) return;
            _critical = state.Critical;

            UpdateNormal(state);
            SetFinalMode(state.FinalAlert);
            if (state.FinalAlert) UpdateFinal(state);
        }

        private void UpdateNormal(CountdownState state)
        {
            if (_lockOnly)
            {
                LockTimeText.Text = state.MainText ?? "";
                LockTargetText.Text = state.IdleLock ? state.Caption : state.Target ?? "";
                if (state.IdleLock)
                {
                    // "kilitli" başlığı işlem satırı ayarına bağlı değildir; süre kapalıysa kart hiç görünmez
                    LockTargetText.Visibility = Visibility.Visible;
                    LockStatusCard.Visibility = _settings.LockTimerMode == 1 ? Visibility.Visible : Visibility.Collapsed;
                }
                if (state.Critical) LockTimeText.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
                else LockTimeText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
                if (_settings.LockTimerMode != 2)
                {
                    UpdateMetrics(state);
                    return;
                }
            }

            double progress = _settings.FullscreenShowProgress ? state.Progress : -1;
            Display.Update(state.MainText, state.Caption, state.Icon, progress, state.Critical);

            TargetText.Text = state.Target ?? "";
            TargetText.Visibility = (_lockOnly ? _settings.LockShowTarget : _settings.FullscreenShowTarget) && !string.IsNullOrEmpty(state.Target)
                ? Visibility.Visible : Visibility.Collapsed;

            UpdateChips(state);
            UpdateMetrics(state);
        }

        // ---------------- Çipler / durum satırı ----------------

        private void UpdateChips(CountdownState state)
        {
            List<MetricChipState> chips = state.Chips;
            bool hasChips = chips != null && chips.Count > 0;

            if (!hasChips)
            {
                if (_chipSignature != null)
                {
                    _chipSignature = null;
                    ChipsPanel.Children.Clear();
                }
                ChipsPanel.Visibility = Visibility.Collapsed;
                StatusText.Text = state.Status ?? "";
                StatusText.Visibility = string.IsNullOrEmpty(state.Status) ? Visibility.Collapsed : Visibility.Visible;
                return;
            }

            // Çipler varken durum satırı gösterilmez
            StatusText.Visibility = Visibility.Collapsed;
            ChipsPanel.Visibility = Visibility.Visible;

            var sig = new StringBuilder();
            foreach (MetricChipState chip in chips)
            {
                if (chip == null) continue;
                sig.Append(chip.Label).Append('\u001F').Append(chip.Active ? '1' : '0').Append('\u001F')
                   .Append((int)chip.Icon).Append('\u001E');
            }
            string signature = sig.ToString();
            if (signature == _chipSignature) return;
            _chipSignature = signature;

            ChipsPanel.Children.Clear();
            foreach (MetricChipState chip in chips)
            {
                if (chip == null) continue;
                FrameworkElement element = CreateChip(chip);
                if (ChipsPanel.Children.Count > 0) element.Margin = new Thickness(8, 0, 0, 0);
                ChipsPanel.Children.Add(element);
            }
        }

        private static FrameworkElement CreateChip(MetricChipState chip)
        {
            var icon = new SymbolIcon
            {
                Symbol = chip.Icon,
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center
            };
            var label = new TextBlock
            {
                Text = chip.Label ?? "",
                Margin = new Thickness(8, 0, 0, 0),
                FontFamily = TextFont,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (chip.Active) label.FontWeight = FontWeights.SemiBold;

            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(icon);
            row.Children.Add(label);

            var border = new Border
            {
                Height = 36,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 8, 14, 8),
                BorderThickness = new Thickness(1),
                Child = row
            };
            border.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");

            if (chip.Active)
            {
                border.SetResourceReference(Border.BackgroundProperty, "AccentFillColorDefaultBrush");
                border.SetResourceReference(TextElement.ForegroundProperty, "TextOnAccentFillColorPrimaryBrush");
            }
            else
            {
                border.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
                border.SetResourceReference(TextElement.ForegroundProperty, "TextFillColorSecondaryBrush");
            }
            return border;
        }

        // ---------------- Son uyarı ----------------

        private void UpdateFinal(CountdownState state)
        {
            FinalHeadlineText.Text = state.FinalHeadline ?? "";
            FinalDetailText.Text = state.FinalDetail ?? "";
            FinalDetailText.Visibility = string.IsNullOrEmpty(state.FinalDetail) ? Visibility.Collapsed : Visibility.Visible;

            bool notice = !string.IsNullOrEmpty(state.FinalNotice);
            if (!notice) SetConfirming(false);
            ActiveNoticeText.Text = state.FinalNotice ?? "";
            NowButton.Content = state.FinalNowLabel ?? "";
            ConfirmText.Text = state.FinalConfirmText ?? "";
            ConfirmYesButton.Content = state.FinalConfirmLabel ?? "";
            ActiveNotice.Visibility = notice && !_confirming ? Visibility.Visible : Visibility.Collapsed;

            int seconds = Math.Max(0, state.FinalSecondsLeft);
            if (seconds != _lastFinalSeconds)
            {
                bool tick = _lastFinalSeconds >= 0 && seconds <= 10;
                _lastFinalSeconds = seconds;
                FinalSecondsText.Text = seconds.ToString(CultureInfo.InvariantCulture);
                FinalSecondsLabel.Text = Loc.T(seconds == 1 ? "Full_Saniye1" : "Full_Saniye");
                if (tick) PlayNumberTick();
            }

            double p = Math.Max(0, Math.Min(1, state.FinalProgress));
            FinalRing.Visibility = p > 0.002 ? Visibility.Visible : Visibility.Hidden;
            FinalRing.EndAngle = Math.Max(0.5, 359.9 * p);
        }

        private void SetFinalMode(bool on)
        {
            if (on == _finalAlert) return;
            _finalAlert = on;
            SetConfirming(false);

            if (on)
            {
                CancelHold();
                NormalLayer.Visibility = Visibility.Collapsed;
                FinalLayer.Visibility = Visibility.Visible;
                FinalWash.Visibility = Visibility.Visible;
                _lastFinalSeconds = -1;

                // Uyarı her zaman görünür ve imleç açık olmalı
                SetSceneVisible(true);
                if (_cursorHidden)
                {
                    _cursorHidden = false;
                    Mouse.OverrideCursor = null;
                }

                PlayFinalEntrance();
                Topmost = true;
                Activate();
            }
            else
            {
                FinalRing.BeginAnimation(OpacityProperty, null);
                FinalNumberScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                FinalNumberScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                FinalWash.BeginAnimation(OpacityProperty, null);
                FinalWash.Opacity = 0;
                FinalWash.Visibility = Visibility.Collapsed;
                FinalLayer.Visibility = Visibility.Collapsed;
                NormalLayer.Visibility = Visibility.Visible;

                RegisterActivity();
                _chromeVisible = false;
                SetChromeVisible(true);
            }
        }

        /// <summary>Ana pencere tarafından kapatılır (zamanlayıcı bitti / iptal edildi).</summary>
        public void CloseFromOwner()
        {
            _allowClose = true;
            Close();
        }

        // =====================================================================
        // Hareket
        // =====================================================================

        private static IEasingFunction CreateEase()
        {
            var ease = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 7 };
            ease.Freeze();
            return ease;
        }

        private static DoubleAnimation Anim(double from, double to, int durationMs, int delayMs, IEasingFunction ease)
        {
            var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(durationMs));
            anim.BeginTime = TimeSpan.FromMilliseconds(delayMs);
            if (ease != null) anim.EasingFunction = ease;
            anim.Freeze();
            return anim;
        }

        private void PlayEntrance()
        {
            BackgroundLayer.BeginAnimation(OpacityProperty, Anim(0, 1, 400, 0, null));

            IEasingFunction ease = CreateEase();
            HeroBlock.BeginAnimation(OpacityProperty, Anim(0, 1, 500, 100, ease));
            HeroScale.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(0.96, 1, 500, 100, ease));
            HeroScale.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(0.96, 1, 500, 100, ease));
            HeroShift.BeginAnimation(TranslateTransform.YProperty, Anim(12, 0, 500, 100, ease));
        }

        private void PlayFinalEntrance()
        {
            IEasingFunction ease = CreateEase();

            FinalWash.BeginAnimation(OpacityProperty, Anim(0, 1, 400, 0, null));
            FinalContent.BeginAnimation(OpacityProperty, Anim(0, 1, 400, 0, ease));
            FinalScale.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(1.04, 1, 400, 0, ease));
            FinalScale.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(1.04, 1, 400, 0, ease));

            // Düğmeler 150 ms sonra 16 px aşağıdan kayarak gelir (gecikme süresince taban değerler geçerli)
            FinalButtons.Opacity = 0;
            FinalButtonsShift.Y = 16;
            FinalButtons.BeginAnimation(OpacityProperty, Anim(0, 1, 400, 150, ease));
            FinalButtonsShift.BeginAnimation(TranslateTransform.YProperty, Anim(16, 0, 400, 150, ease));

            // Halka nabzı: 2 sn'lik sinüs döngüsü
            var pulse = new DoubleAnimation(1.0, 0.6, TimeSpan.FromSeconds(1))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            pulse.Freeze();
            FinalRing.BeginAnimation(OpacityProperty, pulse);
        }

        /// <summary>Son 10 saniyede sayı her saniye hafifçe büyüyüp geri döner.</summary>
        private void PlayNumberTick()
        {
            var tick = new DoubleAnimation(1.0, 1.03, TimeSpan.FromMilliseconds(150))
            {
                AutoReverse = true,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            tick.Freeze();
            FinalNumberScale.BeginAnimation(ScaleTransform.ScaleXProperty, tick);
            FinalNumberScale.BeginAnimation(ScaleTransform.ScaleYProperty, tick);
        }

        private void SetChromeVisible(bool visible)
        {
            if (visible == _chromeVisible) return;
            _chromeVisible = visible;
            foreach (FrameworkElement element in _chrome)
            {
                // Çıkış düğmesi hareketsizlikte kaybolmaz, soluklaşır: kullanıcı çıkışı her zaman görür
                double idleOpacity = element == ExitPanel ? 0.6 : 0;
                var fade = new DoubleAnimation(visible ? 1 : idleOpacity, TimeSpan.FromMilliseconds(visible ? 167 : 600));
                fade.Freeze();
                element.BeginAnimation(OpacityProperty, fade);
            }
        }

        private void SetSceneVisible(bool visible)
        {
            if (visible == _sceneVisible) return;
            _sceneVisible = visible;
            // Soluk karartmada arka plan yerinde kalır, yalnızca öndeki yazı ve göstergeler soluklaşır
            bool dim = _settings.FullscreenBlackoutDim;
            var fade = new DoubleAnimation(visible ? 1 : dim ? DimOpacity : 0, TimeSpan.FromMilliseconds(visible ? 250 : 900));
            fade.Freeze();
            (dim ? (UIElement)ScaleRoot : Scene).BeginAnimation(OpacityProperty, fade);
        }

        // =====================================================================
        // Arayüz zamanlayıcısı
        // =====================================================================

        private void UiTick()
        {
            DateTime now = DateTime.Now;
            UpdateClock(now);

            // Basılı tutma süresi dolunca çık (dolgu animasyonu MouseDown'da başlar)
            if (_holdStart.HasValue)
            {
                _lastActivity = now;
                double ratio = (now - _holdStart.Value).TotalMilliseconds / _settings.ExitHoldMilliseconds;
                if (ratio >= 1)
                {
                    CancelHold();
                    RequestExit();
                    return;
                }
            }

            TimeSpan idle = now - _lastActivity;

            // Hareketsizlikte üst hap, çipler ve çıkış denetimi solar
            if (!_finalAlert)
                SetChromeVisible(_lockOnly || !_settings.FullscreenAutoHide || idle < ChromeHideDelay || _holdStart.HasValue);

            // Karartma: hareket yoksa ekran siyah kalır; son uyarıda her zaman görünür
            if (_settings.FullscreenBlackout && !_lockOnly)
                SetSceneVisible(_critical || _finalAlert || idle < BlackoutRevealDuration);

            // Hareketsiz imleci gizle (son uyarıda düğmeler için imleç hep açık)
            bool hideCursor = !_finalAlert && !_lockOnly &&
                ((_settings.FullscreenHideCursor && idle > CursorHideDelay) || !_sceneVisible);
            if (hideCursor != _cursorHidden)
            {
                _cursorHidden = hideCursor;
                Mouse.OverrideCursor = hideCursor ? Cursors.None : null;
            }
        }

        private void UpdateClock(DateTime now)
        {
            if (ClockPanel.Visibility != Visibility.Visible) return;
            string clock = now.ToString("HH:mm", Loc.Culture);
            if (clock != _clockShown)
            {
                _clockShown = clock;
                ClockText.Text = clock;
            }
            string date = now.ToString(Loc.Code == "en" ? "dddd d MMMM" : "d MMMM dddd", Loc.Culture); // İngilizce'de gün adı başta
            if (date != _dateShown)
            {
                _dateShown = date;
                DateText.Text = date;
            }
        }

        private void RegisterActivity()
        {
            _lastActivity = DateTime.Now;
        }

        private void RequestExit()
        {
            Mouse.OverrideCursor = null;
            _cursorHidden = false;
            var handler = ExitRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void RaiseCancel()
        {
            var handler = CancelRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void RaiseSnooze()
        {
            var handler = SnoozeRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        // =====================================================================
        // Girdi kilidi
        // =====================================================================

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            RegisterActivity();

            if (_finalAlert && _confirming)
            {
                // Onayda Enter bilerek çalışmaz (yanlışlıkla onay olmasın); Esc geri döner
                if (!e.IsRepeat && e.Key == Key.Escape) SetConfirming(false);
                e.Handled = true;
                return;
            }

            if (_finalAlert)
            {
                if (!e.IsRepeat)
                {
                    Key key = e.Key == Key.System ? e.SystemKey : e.Key;
                    if (key == Key.Enter || key == Key.Escape) RaiseCancel();
                    else if (key == Key.E || key == Key.P) RaiseSnooze(); // E (Ertele) ve İngilizce arayüzde P (Postpone)
                }
                e.Handled = true; // diğer tüm tuşlar yok sayılır
                return;
            }

            if (e.Key == Key.Escape && !e.IsRepeat) RequestExit();
            e.Handled = true; // diğer tüm tuşlar (Alt, Enter, Boşluk vb.) yok sayılır
        }

        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            e.Handled = true;
        }

        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            e.Handled = true;
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            RegisterActivity();
            var source = e.OriginalSource as DependencyObject;
            bool allowed;

            if (_finalAlert)
            {
                // Son uyarıda yalnızca kartın düğmeleri tıklanabilir
                allowed = e.ChangedButton == MouseButton.Left &&
                          (IsInside(source, CancelButton) || IsInside(source, SnoozeButton) || IsInside(source, NowButton)
                           || IsInside(source, ConfirmYesButton) || IsInside(source, ConfirmNoButton));
            }
            else
            {
                // Karartılmış ekranda ilk tık yalnızca göstergeyi açar; çıkış düğmesi görünürken çalışır
                allowed = _sceneVisible && e.ChangedButton == MouseButton.Left && IsInside(source, ExitButton);
            }

            if (!allowed) e.Handled = true;
            else base.OnPreviewMouseDown(e);
        }

        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            RegisterActivity();
            e.Handled = true;
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            Point p = e.GetPosition(this);
            if (Math.Abs(p.X - _lastMousePoint.X) > 2 || Math.Abs(p.Y - _lastMousePoint.Y) > 2)
            {
                _lastMousePoint = p;
                RegisterActivity();
            }
            base.OnPreviewMouseMove(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Alt+F4 vb. ile kapatılamaz; yalnızca ana pencere kapatabilir
            if (!_allowClose) e.Cancel = true;
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _uiTimer.Stop();
            if (_keyboard != null) _keyboard.Dispose();
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            Mouse.OverrideCursor = null;
            base.OnClosed(e);
        }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            if (!_allowClose) Topmost = true;
        }

        // ---------------- Çıkış düğmesi (basılı tut) ----------------

        private void ExitButton_MouseEnter(object sender, MouseEventArgs e)
        {
            SetExitFill("CardBackgroundFillColorSecondaryBrush");
        }

        private void ExitButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_finalAlert) return;
            if (_settings.ExitHoldMilliseconds <= 0)
            {
                e.Handled = true;
                RequestExit();
                return;
            }
            _holdStart = DateTime.Now;
            // Dolgu kare kare değil, doğrudan animasyonla dolar; çıkış kararı yine zamanlayıcıdadır
            ExitFill.BeginAnimation(WidthProperty, new DoubleAnimation(0, ExitButton.ActualWidth, TimeSpan.FromMilliseconds(_settings.ExitHoldMilliseconds)));
            ExitButton.CaptureMouse();
            e.Handled = true;
        }

        private void ExitButton_MouseUp(object sender, MouseButtonEventArgs e)
        {
            CancelHold();
        }

        private void ExitButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!ExitButton.IsMouseCaptured)
            {
                SetExitFill("CardBackgroundFillColorDefaultBrush");
                CancelHold();
            }
        }

        private void CancelHold()
        {
            _holdStart = null;
            // Bırakılınca dolgu bulunduğu yerden yumuşakça geri çekilir
            ExitFill.BeginAnimation(WidthProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(180)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
            if (ExitButton.IsMouseCaptured) ExitButton.ReleaseMouseCapture();
            if (!ExitButton.IsMouseOver) SetExitFill("CardBackgroundFillColorDefaultBrush");
        }

        private void SetExitFill(string brushKey)
        {
            ExitCircle.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, brushKey);
        }

        // ---------------- Son uyarı düğmeleri ----------------

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseCancel();
        }

        private void SnoozeButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseSnooze();
        }

        private void NowButton_Click(object sender, RoutedEventArgs e)
        {
            SetConfirming(true);
        }

        private void ConfirmNoButton_Click(object sender, RoutedEventArgs e)
        {
            SetConfirming(false);
        }

        private void ConfirmYesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_confirming) return;
            SetConfirming(false);
            var handler = ExecuteRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <summary>Elle işlemde ikinci onay: soru, alttaki iptal/ertele düğmelerinin yerine geçer.</summary>
        private void SetConfirming(bool on)
        {
            _confirming = on;
            ConfirmPanel.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            FinalButtons.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
            KeyHintText.Visibility = FinalButtons.Visibility; // onayda Enter çalışmaz, ipucu yanıltmasın
            if (on) ActiveNotice.Visibility = Visibility.Collapsed;
        }

        // ---------------- Yardımcılar ----------------

        private static bool IsInside(DependencyObject element, DependencyObject container)
        {
            while (element != null)
            {
                if (element == container) return true;
                element = element is Visual || element is System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(element)
                    : LogicalTreeHelper.GetParent(element);
            }
            return false;
        }

        private static SolidColorBrush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
