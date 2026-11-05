using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shell;
using ShutdownMate.Helpers;
using ShutdownMate.Services;
using WinControls = System.Windows.Controls;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;

namespace ShutdownMate.Views
{
    public partial class MainWindow
    {
        // =====================================================================
        // Ana döngü
        // =====================================================================

        private string _displayMain = "00:00:00";
        private string _displayCaption = Loc.T("Count_Kalan");

        private void SetDisplay(string main, string caption, double progress, bool critical)
        {
            _displayMain = main;
            _displayCaption = caption;
            _progress = progress;
            _critical = critical;
            Display.Update(main, caption, TextFormat.ActionIcon(_action), progress, critical);
        }

        private void Tick()
        {
            DateTime now = _phase == RunPhase.Idle ? DateTime.Now : _clock.Now;

            // Tıklar arasında uzun boşluk: bilgisayar uykudan döndü ya da sistem saati ileri alındı
            _tickGapDetected = (now - _lastTick).TotalSeconds > 30;
            _lastTick = now;

            long ticks = Environment.TickCount64;
            if (ticks - _lastSampleTicks >= 1000)
            {
                _lastSampleTicks = ticks;
                SampleMetricsInBackground();
            }

            if (_phase != RunPhase.Idle && _settings.MuteOtherApps && ticks - _lastMuteScanTicks >= 5000)
            {
                // Sonradan ses çalmaya başlayan uygulamaları da kıs
                _lastMuteScanTicks = ticks;
                _audio.MuteOthers();
            }

            if (_phase == RunPhase.Idle)
            {
                UpdateClockLimits(now);
                UpdatePreviewFullscreen();
                if (_view == ViewKind.Setup) UpdatePreview();
                return;
            }

            if (_watcher != null) _watcher.Evaluate(now, _metrics);
            if (_recorder != null) _recorder.Tick();

            if (_phase == RunPhase.Waiting) WaitingTick(now);
            else CountdownTick(now);

            if (_phase == RunPhase.Idle) return;

            UpdateMetricChips();
            if (RunningBanner.Visibility == Visibility.Visible)
                RunningBannerText.Text = _displayMain + " · " + CountdownTarget.Text;
            PushFullscreenState();
        }

        /// <summary>
        /// Performans sayaçları ve süreç listesi yavaş olabilir; arayüz takılmasın diye arka planda okunur.
        /// Önceki ölçüm bitmeden yenisi başlatılmaz.
        /// </summary>
        private async void SampleMetricsInBackground()
        {
            if (_sampling) return;
            _sampling = true;
            ActivityWatcher watcher = _watcher;
            WatchRecorder recorder = _recorder;
            await Task.Run(() =>
            {
                try
                {
                    _metrics.Sample();
                    if (watcher != null) watcher.SamplePrograms(_metrics);
                    if (recorder != null) recorder.Sample(_metrics);
                }
                catch
                {
                    // Bu turdaki ölçüm atlanır (ör. izleyici o sırada kapatıldı)
                }
            });
            _sampling = false;
            UpdateLiveTexts();
        }

        private void UpdateLiveTexts()
        {
            if (ActivityPanel.Visibility != Visibility.Visible) return;
            NetworkLiveText.Text = "↓ " + TextFormat.Speed(_metrics.DownloadBps) + "   ↑ " + TextFormat.Speed(_metrics.UploadBps);
            CpuLiveText.Text = Loc.T("MainWindowLoop_SuAn") + TextFormat.Percent(_metrics.Cpu);
            string drive = TransferMonitor.DriveOf(SelectedProgramName(TransferSourceCombo));
            TransferLiveText.Text = Loc.T("MainWindowLoop_SuAn") + TextFormat.Speed(drive != null ? _metrics.Transfer.RateOf(drive) : _metrics.Transfer.MaxRate());
            GpuLiveText.Text = _metrics.GpuAvailable ? Loc.T("MainWindowLoop_SuAn") + TextFormat.Percent(_metrics.Gpu) : Loc.T("MainWindowLoop_Okunamiyor");
        }

        private void UpdateMetricChips()
        {
            if (_watcher == null) return;
            UpdateChip(ChipNetwork, ChipNetworkIcon, ChipNetworkText, _watcher.UseNetwork, _watcher.NetworkActive, _watcher.NetworkLabel(_metrics));
            UpdateChip(ChipCpu, ChipCpuIcon, ChipCpuText, _watcher.UseCpu, _watcher.CpuActive, _watcher.CpuLabel());
            UpdateChip(ChipTransfer, ChipTransferIcon, ChipTransferText, _watcher.UseTransfer, _watcher.TransferActive, _watcher.TransferLabel());
            UpdateChip(ChipGpu, ChipGpuIcon, ChipGpuText, _watcher.UseGpu, _watcher.GpuActive, _watcher.GpuLabel());
            UpdateChip(ChipProcess, ChipProcessIcon, ChipProcessText, _watcher.UseProcess, _watcher.ProcessRunning,
                (_watcher.ProcessName ?? "") + (_watcher.ProcessRunning ? Loc.T("MainWindowFullscreen_Acik") : Loc.T("WatchRecorder_Kapandi")));
        }

        private static void UpdateChip(WinControls.Border chip, SymbolIcon icon, WinControls.TextBlock text, bool enabled, bool active, string label)
        {
            chip.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            if (!enabled) return;
            text.Text = label;
            chip.SetResourceReference(WinControls.Border.BackgroundProperty, active ? "AccentFillColorDefaultBrush" : "ControlFillColorDefaultBrush");
            string fg = active ? "TextOnAccentFillColorPrimaryBrush" : "TextFillColorPrimaryBrush";
            text.SetResourceReference(WinControls.TextBlock.ForegroundProperty, fg);
            icon.SetResourceReference(WinControls.Control.ForegroundProperty, fg);
            chip.ToolTip = active ? Loc.T("MainWindowLoop_Suruyor") : Loc.T("MainWindowLoop_Sakin");
        }

        private void CountdownTick(DateTime now)
        {
            // Koşul sonrası beklemede işlerden biri yeniden başladıysa beklemeye dön
            if (_fromCondition && !_graceLocked && _watcher != null && _watcher.ResumableActive)
            {
                ReturnToWaiting(Loc.T("MainWindowLoop_BirIsYenidenBasladi"));
                return;
            }

            TimeSpan remaining = _target - now;

            // Bilgisayar sayaç sırasında uyuduysa ve saat çoktan geçtiyse uyanınca aniden işlem yapma
            WaitRules.ClockJump jump = WaitRules.AfterClockJump(remaining.TotalSeconds, _tickGapDetected, _settings.FinalAlertSeconds);
            if (jump == WaitRules.ClockJump.Cancel)
            {
                StopTimer(true, Loc.T("MainWindowLoop_AtlandiBilgisayarPlanlanan"));
                ShowInfo(Loc.T("MainWindowLoop_PlanlananSaatBilgisayarUyk"));
                return;
            }

            // Uykudan dönünce uyarısız işlem yapma: kullanıcıya en az uyarı süresi kadar zaman tanı
            int minNotice = WaitRules.NoticeSeconds(_settings.FinalAlertSeconds);
            if (jump == WaitRules.ClockJump.Extend)
            {
                _target = now.AddSeconds(minNotice);
                _total = _target - _phaseStart;
                _finalAlertShown = false;
                _warned = false;
                remaining = _target - now;
            }

            if (remaining <= TimeSpan.Zero)
            {
                // Bilgisayar kullanılıyorsa işlemi yapma, ötele
                if (_settings.PostponeWhileActive && PowerService.SecondsSinceLastInput() < 60)
                {
                    HistoryService.Add(Loc.T("MainWindowLoop_ErtelendiBilgisayarKullani"));
                    RefreshHistory();
                    // Sessiz erteleme "bozuk mu?" düşündürür; neden durum satırında kalır
                    _postponeNote = Loc.T("Loop_PostponeNote", DateTime.Now.ToString("HH:mm"), _settings.SnoozeMinutes);
                    Snooze_Click(this, new RoutedEventArgs());
                    return;
                }
                Execute();
                return;
            }

            double ratio = _total.TotalSeconds > 0 ? Math.Max(0, Math.Min(1, remaining.TotalSeconds / _total.TotalSeconds)) : 0;
            bool warning = remaining.TotalSeconds <= _settings.WarningSeconds;
            string clock = TextFormat.Clock(remaining);

            SetDisplay(clock, _fromCondition ? Loc.T("MainWindowLoop_SonBekleme") : Loc.T("Count_Kalan"), ratio, warning);
            CountdownTarget.Text = TextFormat.ActionFuture(_action) + " · " + _clock.ToLocal(_target).ToString("HH:mm") + " (" + TextFormat.DayLabel(_clock.ToLocal(_target), DateTime.Now) + ")";
            SnoozeButton.Visibility = Visibility.Visible;
            SetTitle(clock);
            TaskbarInfo.ProgressState = warning ? TaskbarItemProgressState.Error : TaskbarItemProgressState.Normal;
            TaskbarInfo.ProgressValue = ratio;

            if (warning)
            {
                string notice = ActiveNoticeText();
                CountdownStatus.Text = (_conditionReason != null ? _conditionReason + " · " : "") + Loc.T("MainWindowLoop_VazgecmekIcinIptalEt")
                    + (notice != null ? " " + notice : "");
                if (!_warned)
                {
                    _warned = true;
                    if (_settings.WarningBringToFront && _fullscreen == null)
                    {
                        ShowView(ViewKind.Countdown);
                        BringToFront();
                        Topmost = true;
                    }
                    if (_settings.WarningSound) AlertSound.PlayWarning();
                }
            }
            else
            {
                CountdownStatus.Text = _postponeNote ?? _conditionReason ?? OptionsNote();
            }

            // Bilgisayar kapandıktan sonra da görülebilsin diye son durumun görüntüsü
            if (remaining.TotalSeconds <= 5 && _recorder != null) _recorder.CaptureFinal();
            UpdateFinalAlert(remaining);
        }

        /// <summary>"Bilgisayar kullanılıyorsa işlem yapma" açıksa uyarıda gösterilecek canlı açıklama; kapalıysa null.</summary>
        private string ActiveNoticeText()
        {
            if (!_settings.PostponeWhileActive) return null;
            string snooze = _settings.SnoozeMinutes + Loc.T("MainWindowLoop_DkErtelenir");
            return PowerService.SecondsSinceLastInput() < 60
                ? Loc.T("MainWindowLoop_BilgisayarKullaniliyorSure") + snooze + "."
                : Loc.T("MainWindowLoop_BilgisayarKullaniliyorsaIs") + snooze + ".";
        }

        /// <summary>
        /// İşlemden ayarlanan saniye kadar önce tam ekrana geçip son uyarıyı gösterir.
        /// Masadaki kullanıcı tek tıkla iptal edebilir ya da erteleyebilir.
        /// </summary>
        private void UpdateFinalAlert(TimeSpan remaining)
        {
            int alertSeconds = _settings.FinalAlertSeconds;
            _finalAlertActive = alertSeconds > 0 && remaining.TotalSeconds <= alertSeconds;
            if (!_finalAlertActive) return;

            _finalSecondsLeft = (int)Math.Ceiling(remaining.TotalSeconds);
            _finalProgress = Math.Max(0, Math.Min(1, remaining.TotalSeconds / alertSeconds));

            if (!_finalAlertShown)
            {
                _finalAlertShown = true;
                if (_fullscreen == null)
                {
                    _fullscreenOpenedByAlert = true;
                    EnterFullscreen();
                }
            }

            // Uyarı sesi: başta ve her 10 saniyede bir
            if (_settings.WarningSound && _finalSecondsLeft != _lastAlertBeepSecond
                && (_lastAlertBeepSecond < 0 || _finalSecondsLeft % 10 == 0))
            {
                _lastAlertBeepSecond = _finalSecondsLeft;
                AlertSound.PlayFinal();
            }
        }

        private void WaitingTick(DateTime now)
        {
            SnoozeButton.Visibility = Visibility.Collapsed;
            _finalAlertActive = false;

            if (_watcher.IsComplete(now))
            {
                ConditionMet(Loc.T("MainWindowLoop_SecilenIslerBitti"), false);
                return;
            }
            if (_deadline.HasValue && now >= _deadline.Value)
            {
                ConditionMet(Loc.T("MainWindowLoop_UstSureSinirinaUlasildi"), true);
                return;
            }

            TimeSpan calm = _watcher.Calm(now);
            double ratio = Math.Min(1, calm.TotalSeconds / _watcher.CalmDuration.TotalSeconds);
            string caption = !_watcher.Armed ? Loc.T("MainWindowLoop_IslerinBaslamasiBekleniyor")
                : _watcher.AnyActive ? Loc.T("MainWindowLoop_IslerSuruyor")
                : (CalmCheck.IsChecked == true ? Loc.T("MainWindowLoop_SakinSureHedef") : Loc.T("MainWindowLoop_SakinSureEnAz")) + TextFormat.MinSec(_watcher.CalmDuration);

            SetDisplay(TextFormat.MinSec(calm), caption, ratio, false);

            string target = Loc.T("MainWindowLoop_IslerBitince") + TextFormat.ActionFutureLower(_action) + (_settings.GraceEnabled ? " (" + _settings.GraceMinutes + Loc.T("MainWindowLoop_DkBeklemeIle") : "");
            if (_deadline.HasValue) target += Loc.T("Loop_Latest", _clock.ToLocal(_deadline.Value).ToString("HH:mm"));
            CountdownTarget.Text = target;

            string status = _watcher.Describe(_metrics);
            CountdownStatus.Text = string.IsNullOrEmpty(_resumeNote) ? status : _resumeNote + " · " + status;

            SetTitle(_watcher.AnyActive ? Loc.T("MainWindowLoop_Suruyor") : TextFormat.MinSec(calm));
            TaskbarInfo.ProgressState = TaskbarItemProgressState.Paused;
            TaskbarInfo.ProgressValue = ratio;
        }

        /// <summary>Koşul gerçekleşti: işlemden önce ayarlanan bekleme süresi kadar geri say.</summary>
        private void ConditionMet(string reason, bool locked)
        {
            DateTime now = _clock.Now;
            _phase = RunPhase.Countdown;
            _fromCondition = true;
            _graceLocked = locked;
            _conditionReason = reason;
            _resumeNote = null;
            _phaseStart = now;
            _total = _grace;
            _target = now + _total;
            _warned = false;
            CountdownTick(now);
        }

        private void ReturnToWaiting(string note)
        {
            _phase = RunPhase.Waiting;
            _fromCondition = false;
            _conditionReason = null;
            _resumeNote = note;
            _warned = false;
            _finalAlertShown = false;
            _lastAlertBeepSecond = -1;
            if (_fullscreenOpenedByAlert) { _fullscreenOpenedByAlert = false; ExitFullscreen(true); }
            _watcher.ResetCalm();
            Topmost = _settings.AlwaysOnTop;
            WaitingTick(_clock.Now);
        }

        private string OptionsNote()
        {
            var parts = new List<string>();
            if (_forceClose) parts.Add(Loc.T("MainWindowLoop_UygulamalarZorlaKapatilaca"));
            if (_settings.PreventSleep) parts.Add(Loc.T("MainWindowLoop_BilgisayarKendiligindenUyu"));
            if (parts.Count == 0) return "";
            string s = string.Join(", ", parts);
            return char.ToUpper(s[0]) + s.Substring(1);
        }
    }
}
