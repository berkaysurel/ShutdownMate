using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using ShutdownMate.Helpers;
using ShutdownMate.Models;
using ShutdownMate.Services;
using WinControls = System.Windows.Controls;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;
using WindowBackdropType = Wpf.Ui.Controls.WindowBackdropType;
using WpfUiButton = Wpf.Ui.Controls.Button;
using ControlAppearance = Wpf.Ui.Controls.ControlAppearance;

namespace ShutdownMate.Views
{
    public partial class MainWindow
    {
        // =====================================================================
        // Ayarlar ekranı olayları
        // =====================================================================

        private void OpenPreferences_Click(object sender, RoutedEventArgs e)
        {
            if (_view != ViewKind.Preferences) _viewBeforePreferences = _view;
            RefreshHistory();
            ShowView(ViewKind.Preferences);
        }

        private void ClosePreferences_Click(object sender, RoutedEventArgs e)
        {
            ClosePreferences();
        }

        private void ClosePreferences()
        {
            ShowView(_phase != RunPhase.Idle && _viewBeforePreferences == ViewKind.Countdown ? ViewKind.Countdown : ViewKind.Setup);
        }

        private void Preference_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;

            AppThemeMode oldTheme = _settings.Theme;
            WindowBackdropType oldBackdrop = _settings.Backdrop;
            string oldLanguage = _settings.Language;

            _settings.Theme = (AppThemeMode)Math.Max(0, PrefTheme.SelectedIndex);
            _settings.Language = PrefLanguage.SelectedIndex == 1 ? "tr" : PrefLanguage.SelectedIndex == 2 ? "en" : "auto";
            _settings.Backdrop = PrefBackdrop.SelectedIndex == 1 ? WindowBackdropType.Acrylic
                : PrefBackdrop.SelectedIndex == 2 ? WindowBackdropType.None : WindowBackdropType.Mica;
            _settings.AlwaysOnTop = PrefAlwaysOnTop.IsChecked == true;
            _settings.AnimationsEnabled = PrefAnimations.IsChecked == true;
            Motion.Enabled = _settings.AnimationsEnabled;
            _settings.SnoozeMinutes = ReadTag(PrefSnooze, 10);
            _settings.MaxDurationHours = ReadTag(PrefMaxDuration, AppSettings.DefaultMaxDurationHours);
            _settings.MinLeadMinutes = ReadTag(PrefMinLead, AppSettings.DefaultMinLeadMinutes);
            _settings.RequireStartConfirm = PrefStartConfirm.IsChecked == true;
            _settings.ConfirmForceClose = PrefForceConfirm.IsChecked == true;
            _settings.EjectUsbBeforePower = PrefEjectUsb.IsChecked == true;
            _settings.PostponeWhileActive = PrefPostponeActive.IsChecked == true;
            _settings.LockOnStart = PrefLockOnStart.IsChecked == true;
            _settings.LockTimerMode = ReadTag(PrefLockTimer, 1);
            _settings.LockShowTarget = PrefLockTarget.IsChecked == true;
            _settings.LockShowClock = PrefLockClock.IsChecked == true;
            _settings.LockShowLabel = PrefLockLabel.IsChecked == true;
            _settings.LockShowMetrics = PrefLockMetrics.IsChecked == true;
            _settings.LockMetricCpu = PrefLockCpu.IsChecked == true;
            _settings.LockMetricGpu = PrefLockGpu.IsChecked == true;
            _settings.LockMetricDownload = PrefLockDownload.IsChecked == true;
            _settings.LockMetricUpload = PrefLockUpload.IsChecked == true;
            _settings.LockMetricGraph = PrefLockGraph.IsChecked == true;
            _settings.LockMetricTransfer = PrefLockTransfer.IsChecked == true;
            _settings.LockExitPanelTop = PrefLockExitPosition.SelectedIndex == 1;
            _settings.ExitHoldMilliseconds = ReadTag(PrefExitHold, 1500);
            _settings.FollowDaylightSaving = PrefDaylightSaving.IsChecked == true;
            UpdateDurationLimits();
            UpdateClockLimits(DateTime.Now);
            BuildQuickDurationButtons();
            _settings.ShowToolTips = PrefShowToolTips.IsChecked == true;
            ToolTipSwitch.Enabled = _settings.ShowToolTips;
            _settings.RememberLastChoices = PrefRemember.IsChecked == true;
            _settings.WarningSeconds = ReadTag(PrefWarning, 60);
            _settings.WarningSound = PrefWarningSound.IsChecked == true;
            _settings.WarningBringToFront = PrefWarningFront.IsChecked == true;
            _settings.FinalAlertSeconds = ReadTag(PrefFinalAlert, 30);
            bool muteWas = _settings.MuteOtherApps;
            _settings.MuteOtherApps = PrefMuteOthers.IsChecked == true;
            if (_phase != RunPhase.Idle && muteWas != _settings.MuteOtherApps)
            {
                if (_settings.MuteOtherApps) _audio.MuteOthers(); else _audio.RestoreAll();
            }
            _settings.FullscreenBackground = (FullscreenBackground)Math.Max(0, PrefFsBackground.SelectedIndex);
            _settings.FullscreenBlur = ReadTag(PrefFsBlur, 40);
            _settings.FullscreenBlackout = PrefFsBlackout.IsChecked == true;
            _settings.FullscreenBlackoutDim = PrefFsBlackoutStyle.SelectedIndex == 1;
            _settings.FullscreenCardStyle = Math.Max(0, PrefFsCards.SelectedIndex);
            _settings.FullscreenMetricSize = ReadTag(PrefFsMetricSize, 2);
            _settings.FullscreenOnStart = PrefFullscreenOnStart.IsChecked == true;
            _settings.FullscreenShowClock = PrefFullscreenClock.IsChecked == true;
            _settings.FullscreenHideCursor = PrefFullscreenCursor.IsChecked == true;
            _settings.FullscreenDisplaySize = ReadTag(PrefFsSize, 2);
            _settings.FullscreenShowProgress = PrefFsProgress.IsChecked == true;
            _settings.FullscreenShowTarget = PrefFsTarget.IsChecked == true;
            _settings.FullscreenShowLock = PrefFsLock.IsChecked == true;
            _settings.FullscreenAutoHide = PrefFsAutoHide.IsChecked == true;
            _settings.FullscreenShowMetrics = PrefFsMetrics.IsChecked == true;
            _settings.FullscreenMetricCpu = PrefFsCpu.IsChecked == true;
            _settings.FullscreenMetricGpu = PrefFsGpu.IsChecked == true;
            _settings.FullscreenMetricDownload = PrefFsDownload.IsChecked == true;
            _settings.FullscreenMetricUpload = PrefFsUpload.IsChecked == true;
            _settings.FullscreenMetricGraph = PrefFsGraph.IsChecked == true;
            _settings.FullscreenMetricTransfer = PrefFsTransfer.IsChecked == true;

            if (oldTheme != _settings.Theme || oldBackdrop != _settings.Backdrop)
                ThemeService.Apply(this, _settings);

            if (!_warned) Topmost = _settings.AlwaysOnTop;
            SnoozeButton.Content = _settings.SnoozeMinutes + Loc.T("FullscreenWindow_DkErtele");
            UpdateFullscreenOptionPanels();
            SettingsService.Save(_settings);
            if (oldLanguage != _settings.Language) Dispatcher.BeginInvoke(new Action(OfferLanguageRestart));
        }

        /// <summary>
        /// Dil, açılışta yüklendiği için değişiklik yeniden açılınca geçerli olur; kullanıcıya tek tıkla yeniden başlatma sunulur.
        /// Zamanlayıcı çalışırken yeniden başlatılmaz (zamanlayıcı iptal olurdu).
        /// </summary>
        private void OfferLanguageRestart()
        {
            if (Loc.Resolve(_settings.Language) == Loc.Code) return;
            if (_phase != RunPhase.Idle) { ShowInfo(Loc.T("Lang_RunningNote")); return; }
            if (!AppDialog.Ask(this, Loc.T("Lang_RestartTitle"), Loc.T("Lang_RestartAsk"),
                AppDialogKind.Info, Loc.T("Lang_RestartNow"), Loc.T("Lang_Later"))) return;
            Process.Start(new ProcessStartInfo(Environment.ProcessPath, "--restart") { UseShellExecute = false });
            Application.Current.Shutdown();
        }

        private void LockMessage_Changed(object sender, EventArgs e)
        {
            if (_loading) return;
            _settings.LockMessage = SetupMessageEditor.Message;
            SettingsService.Save(_settings);
        }

        /// <summary>Kapalıyken kutu ve hazır mesaj düğmesi gizlenir; yazılan mesaj silinmez.</summary>
        private void ShowMessage_Changed(object sender, RoutedEventArgs e)
        {
            bool show = ShowMessageToggle.IsChecked == true;
            Motion.SetVisible(SetupMessageEditor, show, 0, true);
            Motion.SetVisible(MessageIconCheck, show, 0, true);
            if (_loading) return;
            _settings.ShowLockMessage = show;
            SettingsService.Save(_settings);
        }

        private void MessageIcon_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _settings.ShowLockMessageIcon = MessageIconCheck.IsChecked == true;
            SettingsService.Save(_settings);
        }

        private void LockPresets_Changed(object sender, EventArgs e)
        {
            if (_loading) return;
            _settings.LockMessagePresets = LockPresets.Presets;
            SetupMessageEditor.SetPresets(_settings.LockMessagePresets);
            SettingsService.Save(_settings);
        }

        private void PreferenceCombo_Changed(object sender, WinControls.SelectionChangedEventArgs e)
        {
            Preference_Changed(sender, e);
        }

        private void UpdateFullscreenOptionPanels()
        {
            Motion.SetVisible(FsMetricOptions, PrefFsMetrics.IsChecked == true, 0, true);
            Motion.SetVisible(LockMetricOptions, PrefLockMetrics.IsChecked == true, 0, true);
            int mode = PrefFsBackground.SelectedIndex;
            Motion.SetVisible(FsColorSwatches, mode == (int)FullscreenBackground.Solid, 0, true);
            Motion.SetVisible(FsBlurOptions, mode == (int)FullscreenBackground.Blur, 0, true);
            Motion.SetVisible(FsBlackoutStyleCard, PrefFsBlackout.IsChecked == true, 0, true);
        }

        private void DisplayStyle_Checked(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if (StyleClassic.IsChecked == true) _settings.DisplayStyle = DisplayStyle.Classic;
            else if (StyleDigital.IsChecked == true) _settings.DisplayStyle = DisplayStyle.Digital;
            else if (StyleMinimal.IsChecked == true) _settings.DisplayStyle = DisplayStyle.Minimal;
            else _settings.DisplayStyle = DisplayStyle.Modern;
            Display.DisplayStyle = _settings.DisplayStyle;
            SettingsService.Save(_settings);
            if (_phase != RunPhase.Idle) Tick();
        }

        private void Accent_Checked(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _settings.AccentColor = (string)((FrameworkElement)sender).Tag ?? "";
            ThemeService.ApplyAccent(_settings.AccentColor);
            SettingsService.Save(_settings);
        }

        private void FullscreenColor_Checked(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _settings.FullscreenColor = (string)((FrameworkElement)sender).Tag ?? "#000000";
            SettingsService.Save(_settings);
        }

        private void FullscreenAccent_Checked(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _settings.FullscreenAccent = (string)((FrameworkElement)sender).Tag ?? "";
            SettingsService.Save(_settings);
        }

        // ---------------- Hızlı süre düzenleyici ----------------

        private void BuildQuickChips()
        {
            QuickChips.Children.Clear();
            List<int> list = _settings.GetQuickDurations();
            foreach (int minutes in list)
            {
                var remove = new WpfUiButton
                {
                    Appearance = ControlAppearance.Transparent,
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Dismiss12, FontSize = 12 },
                    Padding = new Thickness(4),
                    Margin = new Thickness(4, 0, -4, 0),
                    Tag = minutes,
                    ToolTip = Loc.T("MainWindowPreferences_Kaldir"),
                    IsEnabled = list.Count > 1
                };
                remove.Click += RemoveQuickDuration_Click;

                var row = new WinControls.StackPanel { Orientation = WinControls.Orientation.Horizontal };
                row.Children.Add(new WinControls.TextBlock { Text = TextFormat.Minutes(minutes, true), VerticalAlignment = VerticalAlignment.Center });
                row.Children.Add(remove);

                var chip = new WinControls.Border
                {
                    Child = row,
                    Padding = new Thickness(12, 2, 6, 2),
                    Margin = new Thickness(0, 0, 6, 6),
                    CornerRadius = new CornerRadius(14),
                    BorderThickness = new Thickness(1)
                };
                chip.SetResourceReference(WinControls.Border.BackgroundProperty, "ControlFillColorDefaultBrush");
                chip.SetResourceReference(WinControls.Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
                QuickChips.Children.Add(chip);
            }

            AddQuickButton.IsEnabled = list.Count < AppSettings.MaxQuickDurations;
            NewQuickHours.Value = 0;
            NewQuickMinutes.Value = 45;
        }

        private void AddQuickDuration_Click(object sender, RoutedEventArgs e)
        {
            int minutes = ReadInt(NewQuickHours, 0) * 60 + ReadInt(NewQuickMinutes, 0);
            if (minutes < 1) { ShowInfo(Loc.T("MainWindowPreferences_LutfenEnAz1")); return; }
            if (minutes < _settings.GetMinLeadMinutes())
            {
                ShowInfo(Loc.T("MainWindowPreferences_BuSureGuvenlikSekmesindeki") + TextFormat.Minutes(_settings.GetMinLeadMinutes(), true) + Loc.T("MainWindowPreferences_KisaOlduguIcinAna"));
                return;
            }

            var list = _settings.GetQuickDurations();
            if (list.Contains(minutes)) { ShowInfo(TextFormat.Minutes(minutes, true) + Loc.T("MainWindowPreferences_ZatenListede")); return; }
            list.Add(minutes);
            _settings.QuickDurationMinutes = list.OrderBy(m => m).ToList();
            QuickDurationsChanged();
        }

        private void RemoveQuickDuration_Click(object sender, RoutedEventArgs e)
        {
            int minutes = (int)((FrameworkElement)sender).Tag;
            var list = _settings.GetQuickDurations();
            if (list.Count <= 1) return;
            list.Remove(minutes);
            _settings.QuickDurationMinutes = list;
            QuickDurationsChanged();
        }

        private void ResetQuickDurations_Click(object sender, RoutedEventArgs e)
        {
            _settings.ResetQuickDurations();
            QuickDurationsChanged();
        }

        private void QuickDurationsChanged()
        {
            SettingsService.Save(_settings);
            BuildQuickChips();
            BuildQuickDurationButtons();
        }

        // ---------------- Geçmiş ----------------

        private void RefreshHistory()
        {
            HistoryPanel.Refresh();
            List<HistoryEntry> entries = HistoryService.Load();
            LastRunText.Text = entries.Count > 0
                ? Loc.T("MainWindowPreferences_Son") + TextFormat.DateTimeShort(entries[0].Time) + " · " + entries[0].Summary
                : "";
        }

        private void ResetPreferences_Click(object sender, EventArgs e)
        {
            if (!AppDialog.Ask(this, Loc.T("MainWindowPreferences_VarsayilanlaraDonulsunMu"),
                Loc.T("MainWindowPreferences_TumAyarlarVeSon"),
                AppDialogKind.Warning, Loc.T("LockPresetEditor_VarsayilanlaraDon"))) return;

            string oldLanguage = _settings.Language;
            _settings = new AppSettings();
            SettingsService.Save(_settings);
            if (oldLanguage != _settings.Language) Dispatcher.BeginInvoke(new Action(OfferLanguageRestart));
            ThemeService.Apply(this, _settings);
            Topmost = _settings.AlwaysOnTop;
            Display.DisplayStyle = _settings.DisplayStyle;

            _loading = true;
            if (_phase == RunPhase.Idle) LoadSetupFromSettings();
            LoadPreferencesFromSettings();
            BuildQuickDurationButtons();
            HistoryPanel.Load(_settings);
            _loading = false;
            UpdateModePanels();
            UpdatePreview();
        }
    }
}
