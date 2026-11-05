using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ShutdownMate.Services;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;
using WinControls = System.Windows.Controls;

namespace ShutdownMate.Views
{
    public partial class MainWindow
    {
        // =====================================================================
        // Ana ekranı kişiselleştirme (Ayarlar > Kişiselleştirme > Ana ekran)
        // =====================================================================

        private sealed class HomeItem
        {
            public string Key;
            public string Label;
            public string Group;
            public SymbolRegular Icon;
        }

        private static readonly string GroupActions = Loc.T("MainWindowHome_NeYapilsinSecenekleri");
        private static readonly string GroupModes = Loc.T("MainWindowHome_NeZamanSecenekleri");
        private static readonly string GroupConditions = Loc.T("MainWindowHome_IslerBitinceKosullari");
        private static readonly string GroupExtras = Loc.T("MainWindowHome_Diger");

        private static readonly HomeItem[] HomeItems =
        {
            new HomeItem { Key = "action:shutdown", Icon = SymbolRegular.Power24, Label = Loc.T("MainWindowHome_Kapat"), Group = GroupActions },
            new HomeItem { Key = "action:restart", Icon = SymbolRegular.ArrowClockwise24, Label = Loc.T("MainWindowHome_YenidenBaslat"), Group = GroupActions },
            new HomeItem { Key = "action:sleep", Icon = SymbolRegular.WeatherMoon24, Label = Loc.T("MainWindowHome_Uyku"), Group = GroupActions },
            new HomeItem { Key = "action:hibernate", Icon = SymbolRegular.Snooze24, Label = Loc.T("MainWindowHome_HazirdaBeklet"), Group = GroupActions },
            new HomeItem { Key = "mode:duration", Icon = SymbolRegular.Timer24, Label = Loc.T("MainWindowHome_SureSonra"), Group = GroupModes },
            new HomeItem { Key = "mode:clock", Icon = SymbolRegular.Clock24, Label = Loc.T("MainWindowHome_BelirliSaatte"), Group = GroupModes },
            new HomeItem { Key = "mode:activity", Icon = SymbolRegular.CheckmarkCircle24, Label = Loc.T("MainWindowHome_IslerBitince"), Group = GroupModes },
            new HomeItem { Key = "cond:network", Icon = SymbolRegular.ArrowSync24, Label = Loc.T("MainWindowHome_IndirmeYukleme"), Group = GroupConditions },
            new HomeItem { Key = "cond:cpu", Icon = SymbolRegular.DeveloperBoard24, Label = Loc.T("FullscreenWindow_Islemci"), Group = GroupConditions },
            new HomeItem { Key = "cond:transfer", Icon = SymbolRegular.Copy24, Label = Loc.T("MainWindowHome_DosyaAktarimi"), Group = GroupConditions },
            new HomeItem { Key = "cond:gpu", Icon = SymbolRegular.Desktop24, Label = Loc.T("FullscreenWindow_EkranKarti"), Group = GroupConditions },
            new HomeItem { Key = "cond:process", Icon = SymbolRegular.Apps24, Label = Loc.T("MainWindowHome_ProgramKapanmasi"), Group = GroupConditions },
            new HomeItem { Key = "home:lastrun", Icon = SymbolRegular.History24, Label = Loc.T("MainWindowHome_SonCalistirmaYazisi"), Group = GroupExtras },
            new HomeItem { Key = "home:title", Icon = SymbolRegular.Home24, Label = Loc.T("MainWindowHome_AnaEkranBasligi"), Group = GroupExtras },
            new HomeItem { Key = "home:preview", Icon = SymbolRegular.Info24, Label = Loc.T("MainWindowHome_BaslatmaCubuguOzeti"), Group = GroupExtras },
            new HomeItem { Key = "home:lockbar", Icon = SymbolRegular.LockClosed24, Label = Loc.T("MainWindowHome_EkraniKilitleDugmeleri"), Group = GroupExtras },
            new HomeItem { Key = "home:quick", Icon = SymbolRegular.Flash24, Label = Loc.T("MainWindowHome_HizliSureDugmeleri"), Group = GroupExtras },
        };

        private static readonly Dictionary<string, SymbolRegular> GroupIcons = new Dictionary<string, SymbolRegular>
        {
            { GroupActions, SymbolRegular.Power24 },
            { GroupModes, SymbolRegular.Timer24 },
            { GroupConditions, SymbolRegular.CheckmarkCircle24 },
            { GroupExtras, SymbolRegular.Options24 },
        };

        private readonly List<KeyValuePair<WinControls.TextBlock, WinControls.Panel>> _homeGroups =
            new List<KeyValuePair<WinControls.TextBlock, WinControls.Panel>>();

        private bool IsHomeHidden(string key)
        {
            return _settings.HiddenHomeItems != null && _settings.HiddenHomeItems.Contains(key);
        }

        private static Visibility ShownIf(bool shown)
        {
            return shown ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Ayar listesini kurar: her grup, kaç öğenin görünür olduğunu başlığında gösteren açılır bir kart.</summary>
        private void BuildHomeItems()
        {
            HomeItemsPanel.Children.Clear();
            _homeGroups.Clear();
            foreach (var group in HomeItems.GroupBy(i => i.Group))
            {
                var grid = new WinControls.Primitives.UniformGrid { Columns = 2, Margin = new Thickness(16, 0, 16, 12) };
                foreach (HomeItem item in group)
                {
                    var label = new WinControls.StackPanel { Orientation = WinControls.Orientation.Horizontal };
                    label.Children.Add(new SymbolIcon { Symbol = item.Icon, FontSize = 16, Margin = new Thickness(0, 0, 8, 0) });
                    label.Children.Add(new WinControls.TextBlock { Text = item.Label, VerticalAlignment = VerticalAlignment.Center });
                    var box = new WinControls.CheckBox
                    {
                        Content = label,
                        Tag = item,
                        Margin = new Thickness(0, 2, 8, 2),
                        IsChecked = !IsHomeHidden(item.Key),
                        ToolTip = Loc.T("MainWindowHome_IsaretliyseAnaEkrandaGorun"),
                    };
                    box.Checked += HomeItem_Changed;
                    box.Unchecked += HomeItem_Changed;
                    grid.Children.Add(box);
                }

                var count = new WinControls.TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = 12, Margin = new Thickness(0, 0, 10, 0) };
                count.SetResourceReference(WinControls.TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
                var chevron = new SymbolIcon { Symbol = SymbolRegular.ChevronDown24, FontSize = 14, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform(0) };
                var headerRow = new WinControls.DockPanel { LastChildFill = true };
                WinControls.DockPanel.SetDock(chevron, WinControls.Dock.Right);
                WinControls.DockPanel.SetDock(count, WinControls.Dock.Right);
                var icon = new SymbolIcon { Symbol = GroupIcons[group.Key], FontSize = 20, Margin = new Thickness(0, 0, 14, 0) };
                WinControls.DockPanel.SetDock(icon, WinControls.Dock.Left);
                headerRow.Children.Add(chevron);
                headerRow.Children.Add(count);
                headerRow.Children.Add(icon);
                headerRow.Children.Add(new WinControls.TextBlock { Text = group.Key, VerticalAlignment = VerticalAlignment.Center });

                var header = new WinControls.Button
                {
                    Content = headerRow,
                    Padding = new Thickness(16, 12, 16, 12),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    ToolTip = Loc.T("MainWindowHome_GrubuAcKapat"),
                };
                var body = new WinControls.Border { Height = 0, ClipToBounds = true, Child = grid };
                var stack = new WinControls.StackPanel();
                stack.Children.Add(header);
                stack.Children.Add(body);
                var card = new WinControls.Border { Margin = new Thickness(0, 0, 0, 4), CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1), Child = stack };
                card.SetResourceReference(WinControls.Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
                card.SetResourceReference(WinControls.Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
                header.Click += (s, e) => ToggleHomeGroup(body, grid, (RotateTransform)chevron.RenderTransform, card.ActualWidth);
                HomeItemsPanel.Children.Add(card);
                _homeGroups.Add(new KeyValuePair<WinControls.TextBlock, WinControls.Panel>(count, grid));
            }
            UpdateHomeCounts();
        }

        /// <summary>Grubu yumuşakça açar / kapatır; yükseklik tek animasyonla değişir (hazır genişletici şablonundaki sıçrama olmaz).</summary>
        private static void ToggleHomeGroup(WinControls.Border body, WinControls.Panel grid, RotateTransform arrow, double width)
        {
            bool open = double.IsNaN(body.Height) || body.Height > 0;
            double from = double.IsNaN(body.Height) ? body.ActualHeight : body.Height;
            double to = 0;
            if (!open)
            {
                grid.Measure(new Size(Math.Max(width, 1), double.PositiveInfinity));
                to = grid.DesiredSize.Height;
            }
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(180);
            body.BeginAnimation(FrameworkElement.HeightProperty, null);
            body.Height = from;
            var animation = new DoubleAnimation(from, to, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop };
            animation.Completed += (s, e) => body.Height = to;
            body.BeginAnimation(FrameworkElement.HeightProperty, animation);
            arrow.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(open ? 0 : 180, duration) { EasingFunction = ease });
        }

        /// <summary>Grup başlıklarındaki "3 / 4 görünür" yazısını günceller.</summary>
        private void UpdateHomeCounts()
        {
            foreach (var group in _homeGroups)
            {
                var boxes = group.Value.Children.OfType<WinControls.CheckBox>().ToList();
                group.Key.Text = boxes.Count(b => b.IsChecked == true) + " / " + boxes.Count + Loc.T("MainWindowHome_Gorunur");
            }
        }

        private void HomeItem_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            var box = (WinControls.CheckBox)sender;
            var item = (HomeItem)box.Tag;

            // Her grupta (Diğer hariç) en az biri görünür kalır
            if (box.IsChecked != true && item.Group != GroupExtras)
            {
                bool otherVisible = HomeItems.Any(i => i.Group == item.Group && i != item && !IsHomeHidden(i.Key));
                if (!otherVisible)
                {
                    _loading = true;
                    box.IsChecked = true;
                    _loading = false;
                    return;
                }
            }

            _settings.HiddenHomeItems.RemoveAll(k => k == item.Key);
            if (box.IsChecked != true) _settings.HiddenHomeItems.Add(item.Key);
            UpdateHomeCounts();
            ApplyHomeLayout();
            UpdateModePanels();
            UpdatePreview();
            SettingsService.Save(_settings);
        }

        /// <summary>Gizli öğeleri ana ekrandan kaldırır, kalanları düzenler.</summary>
        private void ApplyHomeLayout()
        {
            bool wasLoading = _loading;
            _loading = true;
            LayoutChoiceCards(new[] { ActionShutdown, ActionRestart, ActionSleep, ActionHibernate },
                new[] { "action:shutdown", "action:restart", "action:sleep", "action:hibernate" }, true);
            LayoutChoiceCards(new[] { ModeDuration, ModeClock, ModeActivity },
                new[] { "mode:duration", "mode:clock", "mode:activity" }, false);
            _loading = wasLoading;

            var cards = new[] { CondNetworkCard, CondCpuCard, CondTransferCard, CondGpuCard, CondProcessCard };
            var keys = new[] { "cond:network", "cond:cpu", "cond:transfer", "cond:gpu", "cond:process" };
            var checks = new[] { UseNetworkCheck, UseCpuCheck, UseTransferCheck, UseGpuCheck, UseProcessCheck };
            for (int i = 0; i < cards.Length; i++)
            {
                bool hidden = IsHomeHidden(keys[i]);
                cards[i].Visibility = ShownIf(!hidden);
                if (hidden && _phase == RunPhase.Idle) checks[i].IsChecked = false; // gizli koşul sessizce çalışmasın
            }

            LastRunText.Visibility = ShownIf(!IsHomeHidden("home:lastrun"));
            QuickDurationsPanel.Visibility = ShownIf(!IsHomeHidden("home:quick"));
            HomeTitle.Visibility = ShownIf(!IsHomeHidden("home:title"));
            PreviewTitle.Parent.SetValue(UIElement.VisibilityProperty, ShownIf(!IsHomeHidden("home:preview")));
            IdleLockBar.Visibility = _phase != RunPhase.Idle || IsHomeHidden("home:lockbar") ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>Kartların görünürlüğünü ayarlar; seçili kart gizlendiyse ilk görünür karta geçer.</summary>
        private void LayoutChoiceCards(WinControls.RadioButton[] cards, string[] keys, bool skipDisabled)
        {
            var visible = new List<WinControls.RadioButton>();
            for (int i = 0; i < cards.Length; i++)
            {
                bool shown = !IsHomeHidden(keys[i]);
                cards[i].Visibility = ShownIf(shown);
                if (shown) visible.Add(cards[i]);
            }
            if (visible.Count == 0) return;

            if (!visible.Any(c => c.IsChecked == true))
            {
                var pick = visible.FirstOrDefault(c => !skipDisabled || c.IsEnabled) ?? visible[0];
                pick.IsChecked = true;
            }

            // Kartlar eşit genişlikte dolsun; kenar boşlukları görünür sıraya göre yeniden verilir
            var grid = (WinControls.Primitives.UniformGrid)cards[0].Parent;
            grid.Columns = visible.Count;
            for (int i = 0; i < visible.Count; i++)
                visible[i].Margin = new Thickness(i == 0 ? 0 : 3, 0, i == visible.Count - 1 ? 0 : 3, 0);
        }
    }
}
