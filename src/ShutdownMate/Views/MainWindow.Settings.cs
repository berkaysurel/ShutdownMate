using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ShutdownMate.Helpers;
using ShutdownMate.Models;
using ShutdownMate.Services;
using WinControls = System.Windows.Controls;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;
using WindowBackdropType = Wpf.Ui.Controls.WindowBackdropType;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace ShutdownMate.Views
{
    public partial class MainWindow
    {
        // =====================================================================
        // Ayarların arayüze yüklenmesi / okunması
        // =====================================================================

        private void LoadSetupFromSettings()
        {
            bool hibernate = PowerService.IsHibernateAvailable;
            ActionHibernate.IsEnabled = hibernate;
            ActionHibernate.ToolTip = hibernate ? Loc.T("MainWindowSettings_AcikIsleriDiskeYazip")
                : Loc.T("MainWindowSettings_BuBilgisayardaHazirdaBekle");

            PowerAction action = _settings.LastAction;
            if (action == PowerAction.Hibernate && !hibernate) action = PowerAction.Shutdown;
            ActionButton(action).IsChecked = true;
            ModeButton(_settings.LastMode).IsChecked = true;

            UpdateDurationLimits();
            DurationHours.Value = _settings.DurationHours;
            DurationMinutes.Value = _settings.DurationMinutes;
            SetClockToEarliest(DateTime.Now);

            UseNetworkCheck.IsChecked = _settings.UseNetwork;
            NetworkDirectionCombo.SelectedIndex = (int)_settings.NetworkDirection;
            NetworkThreshold.Value = _settings.NetworkThresholdKBps;
            UseCpuCheck.IsChecked = _settings.UseCpu;
            CpuThreshold.Value = _settings.CpuThresholdPercent;
            UseGpuCheck.IsChecked = _settings.UseGpu && _metrics.GpuAvailable;
            GpuThreshold.Value = _settings.GpuThresholdPercent;
            UseTransferCheck.IsChecked = _settings.UseTransfer && _metrics.Transfer.IsAvailable;
            TransferThreshold.Value = _settings.TransferThresholdMBps;
            UseProcessCheck.IsChecked = false; // program seçimi her seferinde yeniden yapılır
            CalmCheck.IsChecked = _settings.CalmEnabled;
            CalmMinutes.Value = _settings.CalmMinutes;
            WaitForStartCheck.IsChecked = _settings.WaitForStart;
            DeadlineCheck.IsChecked = _settings.DeadlineEnabled;
            DeadlineHours.Value = _settings.DeadlineHours;
            GraceCheck.IsChecked = _settings.GraceEnabled;
            GraceMinutes.Value = _settings.GraceMinutes;

            ForceCloseToggle.IsChecked = false; // Güvenlik: her açılışta kapalı, açık kaldığı fark edilmesin diye hatırlanmaz
            PreventSleepToggle.IsChecked = _settings.PreventSleep;
        }

        private void SaveSetupToSettings()
        {
            _settings.LastAction = SelectedAction;
            _settings.LastMode = SelectedMode;
            _settings.DurationHours = ReadInt(DurationHours, 0);
            _settings.DurationMinutes = ReadInt(DurationMinutes, 30);
            _settings.ClockHour = ReadInt(ClockHour, 23);
            _settings.ClockMinute = ReadInt(ClockMinute, 0);
            _settings.UseNetwork = UseNetworkCheck.IsChecked == true;
            _settings.NetworkDirection = SelectedNetworkDirection;
            _settings.NetworkThresholdKBps = ReadInt(NetworkThreshold, 50);
            _settings.UseCpu = UseCpuCheck.IsChecked == true;
            _settings.CpuThresholdPercent = ReadInt(CpuThreshold, 10);
            _settings.UseGpu = UseGpuCheck.IsChecked == true;
            _settings.GpuThresholdPercent = ReadInt(GpuThreshold, 15);
            _settings.UseTransfer = UseTransferCheck.IsChecked == true;
            _settings.TransferThresholdMBps = ReadInt(TransferThreshold, 5);
            _settings.TransferSource = SelectedProgramName(TransferSourceCombo) ?? "";
            _settings.UseProcess = UseProcessCheck.IsChecked == true;
            _settings.NetworkProcessName = SelectedProgramName(NetworkProgramCombo) ?? "";
            _settings.CpuProcessName = SelectedProgramName(CpuProgramCombo) ?? "";
            _settings.GpuProcessName = SelectedProgramName(GpuProgramCombo) ?? "";
            _settings.CalmEnabled = CalmCheck.IsChecked == true;
            _settings.CalmMinutes = ReadInt(CalmMinutes, 2);
            _settings.WaitForStart = WaitForStartCheck.IsChecked == true;
            _settings.DeadlineEnabled = DeadlineCheck.IsChecked == true;
            _settings.DeadlineHours = ReadInt(DeadlineHours, 8);
            _settings.GraceEnabled = GraceCheck.IsChecked == true;
            _settings.GraceMinutes = ReadInt(GraceMinutes, 5);
            _settings.ForceCloseApps = ForceCloseToggle.IsChecked == true;
            _settings.PreventSleep = PreventSleepToggle.IsChecked == true;
        }

        private void LoadPreferencesFromSettings()
        {
            bool wasLoading = _loading;
            _loading = true;

            switch (_settings.DisplayStyle)
            {
                case DisplayStyle.Classic: StyleClassic.IsChecked = true; break;
                case DisplayStyle.Digital: StyleDigital.IsChecked = true; break;
                case DisplayStyle.Minimal: StyleMinimal.IsChecked = true; break;
                default: StyleModern.IsChecked = true; break;
            }

            PrefFsBackground.SelectedIndex = (int)_settings.FullscreenBackground;
            SelectByTag(PrefFsBlur, _settings.FullscreenBlur);
            CheckSwatch(FsColorSwatches, _settings.FullscreenColor, 0);
            PrefFsBlackout.IsChecked = _settings.FullscreenBlackout;
            PrefFsBlackoutStyle.SelectedIndex = _settings.FullscreenBlackoutDim ? 1 : 0;
            PrefFsCards.SelectedIndex = _settings.FullscreenCardStyle;
            SelectByTag(PrefFsMetricSize, _settings.FullscreenMetricSize);
            CheckSwatch(FsAccentSwatches, _settings.FullscreenAccent, 0);
            PrefFullscreenOnStart.IsChecked = _settings.FullscreenOnStart;
            PrefFullscreenClock.IsChecked = _settings.FullscreenShowClock;
            PrefFullscreenCursor.IsChecked = _settings.FullscreenHideCursor;
            SelectByTag(PrefFsSize, _settings.FullscreenDisplaySize);
            PrefFsProgress.IsChecked = _settings.FullscreenShowProgress;
            PrefFsTarget.IsChecked = _settings.FullscreenShowTarget;
            PrefFsLock.IsChecked = _settings.FullscreenShowLock;
            ShowMessageToggle.IsChecked = _settings.ShowLockMessage;
            MessageIconCheck.IsChecked = _settings.ShowLockMessageIcon;
            MessageIconCheck.Visibility = _settings.ShowLockMessage ? Visibility.Visible : Visibility.Collapsed;
            SetupMessageEditor.Visibility = _settings.ShowLockMessage ? Visibility.Visible : Visibility.Collapsed;
            SetupMessageEditor.Message = _settings.LockMessage;
            SetupMessageEditor.SetPresets(_settings.LockMessagePresets);
            LockPresets.Presets = _settings.LockMessagePresets;
            PrefFsAutoHide.IsChecked = _settings.FullscreenAutoHide;
            PrefFsMetrics.IsChecked = _settings.FullscreenShowMetrics;
            PrefFsCpu.IsChecked = _settings.FullscreenMetricCpu;
            PrefFsGpu.IsChecked = _settings.FullscreenMetricGpu;
            PrefFsDownload.IsChecked = _settings.FullscreenMetricDownload;
            PrefFsUpload.IsChecked = _settings.FullscreenMetricUpload;
            PrefFsGraph.IsChecked = _settings.FullscreenMetricGraph;
            PrefFsTransfer.IsChecked = _settings.FullscreenMetricTransfer;
            UpdateFullscreenOptionPanels();

            BuildQuickChips();
            SelectByTag(PrefSnooze, _settings.SnoozeMinutes);
            SelectByTag(PrefMaxDuration, _settings.GetMaxDurationHours());
            SelectByTag(PrefMinLead, _settings.GetMinLeadMinutes());
            UpdateDurationLimits();
            PrefStartConfirm.IsChecked = _settings.RequireStartConfirm;
            PrefForceConfirm.IsChecked = _settings.ConfirmForceClose;
            PrefEjectUsb.IsChecked = _settings.EjectUsbBeforePower;
            PrefPostponeActive.IsChecked = _settings.PostponeWhileActive;
            PrefLockOnStart.IsChecked = _settings.LockOnStart;
            SelectByTag(PrefLockTimer, _settings.LockTimerMode);
            PrefLockTarget.IsChecked = _settings.LockShowTarget;
            PrefLockClock.IsChecked = _settings.LockShowClock;
            PrefLockLabel.IsChecked = _settings.LockShowLabel;
            PrefLockMetrics.IsChecked = _settings.LockShowMetrics;
            PrefLockCpu.IsChecked = _settings.LockMetricCpu;
            PrefLockGpu.IsChecked = _settings.LockMetricGpu;
            PrefLockDownload.IsChecked = _settings.LockMetricDownload;
            PrefLockUpload.IsChecked = _settings.LockMetricUpload;
            PrefLockGraph.IsChecked = _settings.LockMetricGraph;
            PrefLockTransfer.IsChecked = _settings.LockMetricTransfer;
            PrefLockExitPosition.SelectedIndex = _settings.LockExitPanelTop ? 1 : 0;
            SelectByTag(PrefExitHold, _settings.ExitHoldMilliseconds);
            PrefDaylightSaving.IsChecked = _settings.FollowDaylightSaving;
            // SupportsDaylightSavingTime eski kuralları da sayar (Türkiye'de true döner); bu yüzden ocak ve temmuz karşılaştırılır
            int year = DateTime.Now.Year;
            PrefDaylightInfo.Text = TimeZoneInfo.Local.GetUtcOffset(new DateTime(year, 1, 15)) != TimeZoneInfo.Local.GetUtcOffset(new DateTime(year, 7, 15))
                ?Loc.T("MainWindowSettings_BuBilgisayarinSaatDilimi")
                : Loc.T("MainWindowSettings_BuBilgisayarinSaatDilimi2");
            PrefRemember.IsChecked = _settings.RememberLastChoices;

            SelectByTag(PrefWarning, _settings.WarningSeconds);
            PrefWarningSound.IsChecked = _settings.WarningSound;
            PrefWarningFront.IsChecked = _settings.WarningBringToFront;
            SelectByTag(PrefFinalAlert, _settings.FinalAlertSeconds);
            PrefMuteOthers.IsChecked = _settings.MuteOtherApps;

            PrefTheme.SelectedIndex = (int)_settings.Theme;
            PrefLanguage.SelectedIndex = _settings.Language == "tr" ? 1 : _settings.Language == "en" ? 2 : 0;
            PrefBackdrop.SelectedIndex = _settings.Backdrop == WindowBackdropType.Acrylic ? 1
                : _settings.Backdrop == WindowBackdropType.None ? 2 : 0;
            PrefAlwaysOnTop.IsChecked = _settings.AlwaysOnTop;
            PrefAnimations.IsChecked = _settings.AnimationsEnabled;
            Motion.Enabled = _settings.AnimationsEnabled;
            PrefShowToolTips.IsChecked = _settings.ShowToolTips;
            ToolTipSwitch.Enabled = _settings.ShowToolTips;
            if (!CheckSwatch(AccentSwatches, _settings.AccentColor, -1) && _systemSwatch != null)
                _systemSwatch.IsChecked = true;

            SnoozeButton.Content = _settings.SnoozeMinutes + Loc.T("FullscreenWindow_DkErtele");
            BuildHomeItems();
            ApplyHomeLayout();
            _loading = wasLoading;
        }

        private static bool CheckSwatch(WinControls.Panel panel, string hex, int fallbackIndex)
        {
            var buttons = panel.Children.OfType<WinControls.RadioButton>().ToList();
            var match = buttons.FirstOrDefault(b => string.Equals((string)b.Tag, hex ?? "", StringComparison.OrdinalIgnoreCase));
            if (match == null && fallbackIndex >= 0 && fallbackIndex < buttons.Count) match = buttons[fallbackIndex];
            if (match == null) return false;
            match.IsChecked = true;
            return true;
        }

        private WinControls.RadioButton CreateSwatch(string group, string name, string hex, Color color)
        {
            var button = new WinControls.RadioButton
            {
                GroupName = group,
                Style = (Style)FindResource("ColorSwatch"),
                Tag = hex,
                ToolTip = name,
                Background = new SolidColorBrush(color)
            };
            return button;
        }

        private void BuildAccentSwatches()
        {
            AccentSwatches.Children.Clear();
            foreach (var preset in AccentPresets)
            {
                Color color;
                bool isSystem = !ThemeService.TryParseColor(preset.Value, out color);
                if (isSystem) color = Wpf.Ui.Appearance.ApplicationAccentColorManager.GetColorizationColor();

                var button = CreateSwatch("Accent", preset.Key, preset.Value, color);
                if (isSystem)
                {
                    button.Content = new SymbolIcon { Symbol = SymbolRegular.Desktop24, FontSize = 12, Foreground = Brushes.White };
                    _systemSwatch = button;
                }
                button.Checked += Accent_Checked;
                AccentSwatches.Children.Add(button);
            }
        }

        private void BuildFullscreenSwatches()
        {
            FsColorSwatches.Children.Clear();
            foreach (var preset in FullscreenColorPresets)
            {
                Color color;
                ThemeService.TryParseColor(preset.Value, out color);
                var button = CreateSwatch("FullscreenColor", preset.Key, preset.Value, color);
                button.BorderBrush = Brushes.Gray;
                button.Checked += FullscreenColor_Checked;
                FsColorSwatches.Children.Add(button);
            }
        }

        /// <summary>Tam ekran vurgu rengi: ilk renk (boş) uygulamanın vurgusunu izler.</summary>
        private void BuildFullscreenAccentSwatches()
        {
            FsAccentSwatches.Children.Clear();
            foreach (var preset in FullscreenAccentPresets)
            {
                Color color;
                bool isApp = !ThemeService.TryParseColor(preset.Value, out color);
                if (isApp) color = Wpf.Ui.Appearance.ApplicationAccentColorManager.GetColorizationColor();
                var button = CreateSwatch("FullscreenAccent", preset.Key, preset.Value, color);
                if (isApp) button.Content = new SymbolIcon { Symbol = SymbolRegular.Desktop24, FontSize = 12, Foreground = Brushes.White };
                button.Checked += FullscreenAccent_Checked;
                FsAccentSwatches.Children.Add(button);
            }
        }

        private void BuildQuickDurationButtons()
        {
            QuickDurationsPanel.Children.Clear();
            foreach (int minutes in _settings.GetQuickDurations().Where(m => m >= _settings.GetMinLeadMinutes() && m <= _settings.GetMaxDurationHours() * 60))
            {
                var button = new WpfUiButton
                {
                    Content = TextFormat.Minutes(minutes, true),
                    Tag = minutes,
                    Height = 38,
                    MinWidth = 72,
                    FontSize = 14,
                    Margin = new Thickness(3, 0, 3, 8),
                    ToolTip = Loc.T("MainWindowSettings_Sureyi") + TextFormat.Minutes(minutes, true) + Loc.T("MainWindowSettings_OlarakAyarlar")
                };
                button.Click += QuickDuration_Click;
                QuickDurationsPanel.Children.Add(button);
            }
        }
    }
}
