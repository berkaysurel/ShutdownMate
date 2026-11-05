using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using ShutdownMate.Helpers;
using ShutdownMate.Models;
using ShutdownMate.Services;
using ShutdownMate.Views;
using Wpf.Ui.Controls;
using WinButton = System.Windows.Controls.Button;
using WinTextBlock = System.Windows.Controls.TextBlock;

namespace ShutdownMate.Controls
{
    /// <summary>
    /// Ayarlar > Geçmiş sekmesi: geçmiş listesi (gözetim kaydı olan satırlar açılır) ve gözetim kaydı ayarları.
    /// Ayar değişiklikleri doğrudan kaydedilir.
    /// </summary>
    public partial class HistoryView : UserControl
    {
        private const int MaxRows = 20;

        private AppSettings _settings;
        private bool _loading;

        /// <summary>Geçmiş temizlenince tetiklenir (ana ekrandaki "Son:" satırı yenilensin).</summary>
        public event EventHandler HistoryCleared;

        public HistoryView()
        {
            InitializeComponent();
        }

        /// <summary>Ayarları denetimlere yükler ve listeyi yeniler. Ayarlar sıfırlanınca yeniden çağrılır.</summary>
        public void Load(AppSettings settings)
        {
            _settings = settings;
            _loading = true;
            WatchEnabled.IsChecked = settings.WatchRecordingEnabled;
            WatchScreenshots.IsChecked = settings.RecordScreenshots;
            WatchArea.SelectedIndex = settings.ScreenshotWindowOnly ? 1 : 0;
            SelectByTag(WatchInterval, settings.ScreenshotMinutes);
            SelectByTag(WatchKeep, settings.WatchKeepSessions);
            SelectByTag(WatchMaxSize, settings.WatchMaxMegabytes);
            WatchSystemErrors.IsChecked = settings.WatchIncludeSystemErrors;
            WatchMinimized.IsChecked = settings.CaptureMinimizedProgram;
            WatchSerial.IsChecked = settings.WatchSerialNames;
            StampEnabled.IsChecked = settings.StampEnabled;
            StampDate.IsChecked = settings.StampDate;
            StampTime.IsChecked = settings.StampTime;
            StampMode.IsChecked = settings.StampMode;
            StampProgram.IsChecked = settings.StampProgram;
            StampNote.Text = settings.StampNote;
            _loading = false;
            UpdateFolderText();
            UpdateMinimizedCard();
            WatchOptions.Visibility = settings.WatchRecordingEnabled ? Visibility.Visible : Visibility.Collapsed;
            Refresh();
        }

        // ---------------- Ayarlar ----------------

        // Açarken onay, anahtar değişmeden önce sorulur: Vazgeç'te anahtar hiç açık görünmez.
        // Fare ve klavye (Boşluk, Enter) burada yakalanır; anahtar yalnızca kabul edilince açılır.
        private void WatchEnabled_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (WatchEnabled.IsChecked == true) return;
            e.Handled = true;
            TurnOnWithConsent();
        }

        private void WatchEnabled_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (WatchEnabled.IsChecked == true || (e.Key != Key.Space && e.Key != Key.Enter)) return;
            e.Handled = true;
            if (!e.IsRepeat) TurnOnWithConsent();
        }

        private void TurnOnWithConsent()
        {
            if (_settings == null || !AskConsent()) return;
            SetEnabledSilently(true);
            Apply(true);
        }

        private bool AskConsent()
        {
            return AppDialog.Ask(Window.GetWindow(this), Loc.T("HistoryView_GozetimKaydiAcilsinMi"),
                Loc.T("HistoryView_ZamanlayiciCalisirkenSunla")
                + Loc.T("HistoryView_TumEkraninGoruntuleriAcik")
                + Loc.T("HistoryView_IzlenenPrograminAdiVe")
                + Loc.T("HistoryView_InternetBaglantisininKopup")
                + Loc.T("HistoryView_IslemciEkranKartiVe")
                + Loc.T("HistoryView_WindowsOlayGunlugundekiHat")
                + Loc.T("HistoryView_KayitlarYalnizcaBuBilgisay"),
                AppDialogKind.Warning, Loc.T("HistoryView_KabulEdiyorumAc"),
                note: Loc.T("HistoryView_EkranGoruntusunuVeHata"));
        }

        private void WatchEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            bool on = WatchEnabled.IsChecked == true;

            // Fare ve klavye dışından açılırsa (ör. ekran okuyucu) onay yine sorulur
            if (on && !AskConsent())
            {
                Dispatcher.BeginInvoke(new Action(() => SetEnabledSilently(false)), DispatcherPriority.Input);
                return;
            }

            if (!on && WatchRecorderHasData() && AppDialog.Ask(Window.GetWindow(this), Loc.T("HistoryView_MevcutKayitlarSilinsinMi"),
                Loc.T("HistoryView_GozetimKaydiKapatildiSimdi"),
                AppDialogKind.Warning, Loc.T("HistoryView_KayitlariSil"), Loc.T("HistoryView_Kalsin")))
            {
                WatchRecorder.DeleteAll();
            }

            Apply(on);
        }

        private void Apply(bool on)
        {
            _settings.WatchRecordingEnabled = on;
            if (on) _settings.WatchConsentTime = DateTime.Now;
            WatchOptions.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            SettingsService.Save(_settings);
            Refresh();
        }

        private void SetEnabledSilently(bool value)
        {
            _loading = true;
            WatchEnabled.IsChecked = value;
            _loading = false;
        }

        private static bool WatchRecorderHasData()
        {
            int count;
            long bytes;
            WatchRecorder.Usage(out count, out bytes);
            return count > 0;
        }

        private void Option_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            _settings.RecordScreenshots = WatchScreenshots.IsChecked == true;
            _settings.ScreenshotWindowOnly = WatchArea.SelectedIndex == 1;
            _settings.CaptureMinimizedProgram = WatchMinimized.IsChecked == true;
            UpdateMinimizedCard();
            _settings.ScreenshotMinutes = ReadTag(WatchInterval, 15);
            _settings.WatchKeepSessions = ReadTag(WatchKeep, 30);
            _settings.WatchMaxMegabytes = ReadTag(WatchMaxSize, 500);
            _settings.WatchIncludeSystemErrors = WatchSystemErrors.IsChecked == true;
            _settings.WatchSerialNames = WatchSerial.IsChecked == true;
            _settings.StampEnabled = StampEnabled.IsChecked == true;
            _settings.StampDate = StampDate.IsChecked == true;
            _settings.StampTime = StampTime.IsChecked == true;
            _settings.StampMode = StampMode.IsChecked == true;
            _settings.StampProgram = StampProgram.IsChecked == true;
            _settings.StampNote = StampNote.Text;
            StampPanel.Visibility = _settings.StampEnabled ? Visibility.Visible : Visibility.Collapsed;
            SettingsService.Save(_settings);
        }

        private void UpdateFolderText()
        {
            FolderText.Text = WatchRecorder.RootPath;
            StampPanel.Visibility = _settings.StampEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        // Yalnızca "Yalnızca program" görüntü alanında anlamlıdır; diğerinde devre dışı ve nedeni ToolTip'te
        private void UpdateMinimizedCard()
        {
            bool programOnly = WatchArea.SelectedIndex == 1 && WatchScreenshots.IsChecked == true;
            MinimizedCard.IsEnabled = programOnly;
            MinimizedCard.ToolTip = programOnly ? null : Loc.T("HistoryView_YalnizcaGoruntuAlaniYalniz");
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(WatchRecorder.RootPath);
            OpenInExplorer(WatchRecorder.RootPath);
        }

        private void DeleteRecordings_Click(object sender, RoutedEventArgs e)
        {
            if (!AppDialog.Ask(Window.GetWindow(this), Loc.T("HistoryView_GozetimKayitlariSilinsinMi"),
                Loc.T("HistoryView_TumGozetimKayitlariVe"),
                AppDialogKind.Warning, Loc.T("HistoryView_KayitlariSil"))) return;
            WatchRecorder.DeleteAll();
            Refresh();
        }

        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            if (!AppDialog.Ask(Window.GetWindow(this), Loc.T("HistoryView_GecmisSilinsinMi"),
                Loc.T("HistoryView_TumGecmisKayitlariGozetim"),
                AppDialogKind.Warning, Loc.T("HistoryView_GecmisiSil"))) return;
            HistoryService.Clear();
            WatchRecorder.DeleteAll();
            Refresh();
            if (HistoryCleared != null) HistoryCleared(this, EventArgs.Empty);
        }

        // ---------------- Liste ----------------

        /// <summary>Geçmiş listesini ve kayıtların kapladığı alanı yeniden çizer.</summary>
        public void Refresh()
        {
            List<HistoryEntry> entries = HistoryService.Load();
            HistoryList.Children.Clear();
            foreach (HistoryEntry entry in entries.Take(MaxRows))
            {
                string line = TextFormat.DateTimeShort(entry.Time) + "  ·  " + entry.Summary;
                WatchSession session = WatchRecorder.Load(entry.SessionId);
                HistoryList.Children.Add(session == null ? PlainRow(line) : SessionRow(line, session));
            }
            if (entries.Count == 0)
                HistoryList.Children.Add(new WinTextBlock { Text = Loc.T("HistoryView_HenuzKayitYok"), FontSize = 12, Opacity = 0.7 });

            int count;
            long bytes;
            WatchRecorder.Usage(out count, out bytes);
            DeleteRecordingsButton.IsEnabled = count > 0;
            UsageText.Text = count == 0 ? Loc.T("HistoryView_KayitliGozetimKaydiYok")
                : Loc.T(count == 1 ? "HistoryView_Usage1" : "HistoryView_Usage", count, (bytes / (1024.0 * 1024.0)).ToString("0.#"));
        }

        private static UIElement PlainRow(string text)
        {
            return new WinTextBlock
            {
                Text = text,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 4),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
        }

        private UIElement SessionRow(string text, WatchSession session)
        {
            int problems = session.ProblemCount;
            bool cut = !session.End.HasValue;
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            var icon = new SymbolIcon { Symbol = problems > 0 || cut ? SymbolRegular.Warning24 : SymbolRegular.CheckmarkCircle24, FontSize = 14, Margin = new Thickness(0, 0, 6, 0) };
            if (problems > 0 || cut) icon.SetResourceReference(SymbolIcon.ForegroundProperty, "SystemFillColorCautionBrush");
            header.Children.Add(icon);
            string suffix = cut ? Loc.T("HistoryView_KayitYaridaKaldi") : problems > 0 ? "  ·  " + problems + Loc.T(problems == 1 ? "HistoryView_Sorun1" : "HistoryView_Sorun") : "";
            header.Children.Add(new WinTextBlock { Text = text + suffix, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });

            // Hazır genişletici yerine kendi satırımız: yükseklik tek animasyonla değişir, alttakiler takılmaz
            var chevron = new SymbolIcon { Symbol = SymbolRegular.ChevronDown24, FontSize = 14, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform(0) };
            var headerRow = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(chevron, Dock.Right);
            headerRow.Children.Add(chevron);
            headerRow.Children.Add(header);
            var button = new WinButton
            {
                Content = headerRow,
                Padding = new Thickness(12, 8, 12, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                ToolTip = Loc.T("HistoryView_OlaylariVeEkranGoruntuleri")
            };
            var body = new Border { Height = 0, ClipToBounds = true };
            var stack = new StackPanel();
            stack.Children.Add(button);
            stack.Children.Add(body);
            var card = new Border { Margin = new Thickness(0, 0, 0, 4), CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1), Child = stack };
            card.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
            card.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");

            bool open = false;
            button.Click += (s, e) =>
            {
                open = !open;
                double from = body.Height == 0 ? 0 : (double.IsNaN(body.Height) ? body.ActualHeight : body.Height);
                double to = 0;
                if (open)
                {
                    // Ayrıntı ve küçük resimler yalnızca ilk açılışta yüklenir
                    if (body.Child == null) body.Child = SessionDetail(session);
                    body.Child.Measure(new Size(Math.Max(card.ActualWidth - 2, 1), double.PositiveInfinity));
                    to = body.Child.DesiredSize.Height;
                }
                Motion.AnimateHeight(body, from, to, open);
                var arrow = (RotateTransform)chevron.RenderTransform;
                if (Motion.Enabled)
                    arrow.BeginAnimation(RotateTransform.AngleProperty, new System.Windows.Media.Animation.DoubleAnimation(open ? 180 : 0, TimeSpan.FromMilliseconds(180)));
                else
                {
                    arrow.BeginAnimation(RotateTransform.AngleProperty, null);
                    arrow.Angle = open ? 180 : 0;
                }
            };
            return card;
        }

        private UIElement SessionDetail(WatchSession session)
        {
            string folder = WatchRecorder.FolderOf(session.Id);
            var panel = new StackPanel { Margin = new Thickness(4, 6, 0, 8) };

            var facts = new List<string> { session.Mode };
            if (session.End.HasValue) facts.Add(Loc.T("HistoryView_Sure") + TextFormat.Friendly(session.End.Value - session.Start));
            if (session.Program != null) facts.Add(Loc.T("HistoryView_Izlenen") + session.Program);
            panel.Children.Add(Caption(string.Join("  ·  ", facts.Where(f => !string.IsNullOrEmpty(f)))));
            if (!session.End.HasValue)
                panel.Children.Add(Caption(Loc.T("HistoryView_KayitYaridaKaldiBilgisayar"), true));

            if (session.Samples.Count >= 2) panel.Children.Add(Chart(session.Samples, session.Program != null));

            foreach (WatchEvent ev in session.Events) panel.Children.Add(EventRow(ev, folder));

            var open = new Wpf.Ui.Controls.Button
            {
                Content = Loc.T("HistoryView_BuKaydinKlasorunuAc"),
                Icon = new SymbolIcon { Symbol = SymbolRegular.FolderOpen24 },
                Appearance = ControlAppearance.Transparent,
                Margin = new Thickness(-4, 6, 0, 0),
                ToolTip = Loc.T("HistoryView_BuCalismaninEkranGoruntule")
            };
            open.Click += (s, e) => { if (folder != null && Directory.Exists(folder)) OpenInExplorer(folder); };
            panel.Children.Add(open);
            return panel;
        }

        private static WinTextBlock Caption(string text, bool caution = false)
        {
            var block = new WinTextBlock { Text = text, Margin = new Thickness(0, 0, 0, 6) };
            block.SetResourceReference(StyleProperty, "Caption");
            if (caution) block.SetResourceReference(WinTextBlock.ForegroundProperty, "SystemFillColorCautionBrush");
            return block;
        }

        private UIElement EventRow(WatchEvent ev, string folder)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var time = new WinTextBlock { Text = ev.Time.ToString("HH:mm:ss"), FontSize = 12, Opacity = 0.7 };
            row.Children.Add(time);

            var body = new StackPanel();
            Grid.SetColumn(body, 1);
            var text = new WinTextBlock { Text = (ev.Problem ? "⚠ " : "") + ev.Text, FontSize = 12, TextWrapping = TextWrapping.Wrap };
            if (ev.Problem)
            {
                text.FontWeight = FontWeights.SemiBold;
                text.SetResourceReference(WinTextBlock.ForegroundProperty, "SystemFillColorCautionBrush");
            }
            body.Children.Add(text);

            string image = ImagePath(folder, ev.Image);
            if (image != null) body.Children.Add(Thumbnail(image));
            row.Children.Add(body);
            return row;
        }

        // Dosya adı kayıttan gelir: klasör dışına çıkamaması için yalnızca adı alınır
        private static string ImagePath(string folder, string image)
        {
            if (folder == null || string.IsNullOrEmpty(image)) return null;
            string name = System.IO.Path.GetFileName(image);
            if (!name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)) return null;
            string path = System.IO.Path.Combine(folder, name);
            return File.Exists(path) ? path : null;
        }

        private static UIElement Thumbnail(string path)
        {
            var button = new WinButton
            {
                Margin = new Thickness(0, 4, 0, 4),
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand,
                ToolTip = Loc.T("HistoryView_BuyukGormekIcinTiklayin")
            };
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path);
                bitmap.DecodePixelWidth = 320;
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // dosya kilitlenmesin, silinebilsin
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.EndInit();
                bitmap.Freeze();
                button.Content = new System.Windows.Controls.Image { Source = bitmap, Width = 240, Stretch = Stretch.Uniform };
            }
            catch
            {
                button.Content = Loc.T("HistoryView_GoruntuAcilamadi");
            }
            button.Click += (s, e) => OpenWithShell(path);
            return button;
        }

        /// <summary>Dakikalık işlemci (vurgu rengi) ve ekran kartı (gri) kullanımı, 0–100 ölçeğinde.</summary>
        private static UIElement Chart(List<WatchSample> samples, bool program)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 2, 0, 8) };
            panel.Children.Add(Caption((program ? Loc.T("HistoryView_Programin") : Loc.T("HistoryView_Sistemin")) + Loc.T("HistoryView_DakikalikKullanimiIslemciR")));

            var canvas = new Canvas { Height = 60, ClipToBounds = true };
            canvas.SetResourceReference(Panel.BackgroundProperty, "ControlFillColorDefaultBrush");
            canvas.SizeChanged += (s, e) =>
            {
                canvas.Children.Clear();
                canvas.Children.Add(Line(samples.Select(x => x.Gpu).ToList(), canvas.ActualWidth, canvas.ActualHeight, "TextFillColorTertiaryBrush"));
                canvas.Children.Add(Line(samples.Select(x => x.Cpu).ToList(), canvas.ActualWidth, canvas.ActualHeight, "AccentFillColorDefaultBrush"));
            };
            panel.Children.Add(canvas);
            return panel;
        }

        private static Polyline Line(List<double> values, double width, double height, string brush)
        {
            var line = new Polyline { StrokeThickness = 1.5 };
            line.SetResourceReference(Shape.StrokeProperty, brush);
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] < 0) continue; // ölçülemeyen dakika
                double x = values.Count == 1 ? 0 : i * width / (values.Count - 1);
                double y = height - Math.Min(100, values[i]) / 100 * (height - 2) - 1;
                line.Points.Add(new Point(x, y));
            }
            return line;
        }

        // ---------------- Yardımcılar ----------------

        private static void OpenInExplorer(string folder)
        {
            try
            {
                Process.Start(new ProcessStartInfo(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), "\"" + AppPackage.ToVisiblePath(folder) + "\"") { UseShellExecute = false });
            }
            catch { }
        }

        private static void OpenWithShell(string path)
        {
            try { Process.Start(new ProcessStartInfo(AppPackage.ToVisiblePath(path)) { UseShellExecute = true }); }
            catch { }
        }

        private static void SelectByTag(ComboBox combo, int value)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Tag != null && item.Tag.ToString() == value.ToString()) { combo.SelectedItem = item; return; }
            }
            combo.SelectedIndex = 0;
        }

        private static int ReadTag(ComboBox combo, int fallback)
        {
            var item = combo.SelectedItem as ComboBoxItem;
            int value;
            return item != null && item.Tag != null && int.TryParse(item.Tag.ToString(), out value) ? value : fallback;
        }
    }
}
