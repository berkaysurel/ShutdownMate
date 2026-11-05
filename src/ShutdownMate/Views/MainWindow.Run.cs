using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shell;
using ShutdownMate.Helpers;
using ShutdownMate.Models;
using ShutdownMate.Services;

namespace ShutdownMate.Views
{
    public partial class MainWindow
    {
        // =====================================================================
        // Başlat / durdur
        // =====================================================================

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            // İkinci kopya açılışı gizli pencereyi öne getirebilir; kilit sürerken zamanlayıcı başlamaz
            if (_idleLockStart.HasValue) return;
            var clock = new TimelineClock(_settings.FollowDaylightSaving);
            DateTime now = clock.Now;
            PowerAction action = SelectedAction;
            TriggerMode mode = SelectedMode;

            if (action == PowerAction.Hibernate && !PowerService.IsHibernateAvailable)
            {
                ShowInfo(Loc.T("MainWindowRun_BuBilgisayardaHazirdaBekle"));
                return;
            }

            DateTime target = DateTime.MinValue;
            ActivityWatcher watcher = null;

            if (mode == TriggerMode.Duration || mode == TriggerMode.Clock)
            {
                if (!TryComputeStartTarget(clock, mode, now, out target)) return;
            }
            else
            {
                if (EnabledConditionCount == 0)
                {
                    ShowInfo(Loc.T("MainWindowRun_LutfenBeklenecekEnAz"));
                    return;
                }

                watcher = new ActivityWatcher
                {
                    UseNetwork = UseNetworkCheck.IsChecked == true,
                    Direction = SelectedNetworkDirection,
                    NetworkThresholdBps = ReadInt(NetworkThreshold, 50) * 1024.0,
                    UseCpu = UseCpuCheck.IsChecked == true,
                    CpuThreshold = ReadInt(CpuThreshold, 10),
                    UseGpu = UseGpuCheck.IsChecked == true,
                    GpuThreshold = ReadInt(GpuThreshold, 15),
                    UseTransfer = UseTransferCheck.IsChecked == true,
                    TransferThresholdBps = TransferMonitor.MegabytesToBytes(ReadInt(TransferThreshold, 5)),
                    CalmDuration = WaitRules.Calm(CalmCheck.IsChecked == true, ReadInt(CalmMinutes, 2)),
                    WaitForStart = WaitForStartCheck.IsChecked == true
                };
                if (watcher.UseNetwork) watcher.SetNetworkProgram(SelectedProgramName(NetworkProgramCombo));
                if (watcher.UseCpu) watcher.SetCpuProgram(SelectedProgramName(CpuProgramCombo));
                if (watcher.UseGpu) watcher.SetGpuProgram(SelectedProgramName(GpuProgramCombo));
                if (watcher.UseTransfer)
                {
                    watcher.SetTransferSource(SelectedProgramName(TransferSourceCombo));
                }

                if (UseProcessCheck.IsChecked == true)
                {
                    var item = ProcessList.SelectedItem as ProcessItem;
                    if (item == null) { ShowInfo(Loc.T("MainWindowRun_LutfenIzlenecekProgramiSec")); return; }
                    try
                    {
                        watcher.WatchedProcess = Process.GetProcessById(item.Pid);
                        if (watcher.WatchedProcess.HasExited) throw new InvalidOperationException();
                        watcher.ProcessName = item.Name;
                    }
                    catch
                    {
                        watcher.Dispose();
                        ShowInfo(Loc.T("MainWindowRun_SecilenProgramArtikCalismi"));
                        LoadProcesses();
                        return;
                    }
                }
            }

            bool forced = ForceCloseToggle.IsChecked == true && PowerService.SupportsForceClose(action);
            if (_settings.RequireStartConfirm || (forced && _settings.ConfirmForceClose))
            {
                bool running = _phase != RunPhase.Idle;
                UpdatePreview();
                string summary = PreviewSubtitle.Text;
                if (running) summary += Loc.T("MainWindowRun_CalisanZamanlayiciIptalEdi");
                if (forced) summary += Loc.T("MainWindowRun_AcikUygulamalarKaydetmeSor");
                string note = _settings.RequireStartConfirm
                    ? Loc.T("MainWindowRun_BuOnayPenceresiniAyarlar")
                    : Loc.T("MainWindowRun_ZorlaKapatmaOnayiniAyarlar");

                bool accepted = AppDialog.Ask(this, running ? Loc.T("MainWindowRun_YenidenKurulsunMu") : Loc.T("MainWindowRun_BaslatilsinMi"), summary,
                    forced ? AppDialogKind.Danger : AppDialogKind.Info, running ? Loc.T("MainWindowRun_YenidenKur") : Loc.T("MainWindowRun_Baslat"),
                    highlight: PreviewTitle.Text, note: note);
                if (!accepted)
                {
                    if (watcher != null) watcher.Dispose();
                    return;
                }

                // Onay penceresi uzun süre açık kalmış olabilir: hedef geçmişte kalıp işlem hemen uygulanmasın
                now = clock.Now;
                if (mode != TriggerMode.Activity && !TryComputeStartTarget(clock, mode, now, out target)) return;
            }

            _clock = clock;
            SaveSetupToSettings();
            SettingsService.Save(_settings);
            StopTimer(false, null);

            _action = action;
            _mode = mode;
            _forceClose = _settings.ForceCloseApps && PowerService.SupportsForceClose(action);
            _grace = WaitRules.Grace(_settings.GraceEnabled, _settings.GraceMinutes, _settings.FinalAlertSeconds);
            _watcher = watcher;
            // Gözetim kaydı yalnızca kullanıcı onaylayıp açtıysa; kapalıyken hiçbir şey yazılmaz
            if (_settings.WatchRecordingEnabled)
            {
                _recorder = new WatchRecorder(TextFormat.ActionFuture(action) + " · " + ModeLabel(),
                    watcher == null ? null : watcher.ProcessName ?? watcher.CpuProgramName ?? watcher.GpuProgramName ?? watcher.NetworkProgramName,
                    watcher != null && watcher.UseProcess, _settings);
            }
            WatchNote.Visibility = _recorder != null ? Visibility.Visible : Visibility.Collapsed;
            _warned = false;
            _fromCondition = false;
            _graceLocked = false;
            _conditionReason = null;
            _resumeNote = null;
            _postponeNote = null;
            _phaseStart = now;
            _deadline = mode == TriggerMode.Activity && _settings.DeadlineEnabled ? now.AddHours(_settings.DeadlineHours) : (DateTime?)null;

            if (mode == TriggerMode.Activity)
            {
                _phase = RunPhase.Waiting;
            }
            else
            {
                _phase = RunPhase.Countdown;
                _target = target;
                _total = target - now;
            }

            _finalAlertShown = false;
            _fullscreenOpenedByAlert = false;
            _lastAlertBeepSecond = -1;
            PowerService.SetKeepAwake(_settings.PreventSleep);
            if (_settings.MuteOtherApps) { _audio.MuteOthers(); _lastMuteScanTicks = Environment.TickCount64; }
            MetricChips.Visibility = mode == TriggerMode.Activity ? Visibility.Visible : Visibility.Collapsed;
            ShowView(ViewKind.Countdown);
            Tick();
            if (_settings.FullscreenOnStart) EnterFullscreen();
            else if (_settings.LockOnStart) EnterFullscreen(true);
        }

        private DateTime ComputeStartTarget(TimelineClock clock, DateTime now, TriggerMode mode)
        {
            DateTime target = ComputeTarget(now, mode);
            return mode == TriggerMode.Clock ? clock.FromLocal(target) : target;
        }

        /// <summary>Hedefi çizelgeye çevirip en kısa süre sınırına uyduğunu denetler; uymuyorsa kullanıcıya bildirir.</summary>
        private bool TryComputeStartTarget(TimelineClock clock, TriggerMode mode, DateTime now, out DateTime target)
        {
            target = ComputeStartTarget(clock, now, mode);

            // Birkaç saniyelik pay: saat seçicisinin sınırı en geç bir sonraki tıkta güncellenir
            int minLead = _settings.GetMinLeadMinutes();
            if ((target - now).TotalSeconds >= minLead * 60 - 5) return true;

            ShowInfo(Loc.T("MainWindowRun_IslemEnErken") + TextFormat.Minutes(minLead, true) + Loc.T("MainWindowRun_SonrayaKurulabilirBuSiniri"));
            return false;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            StopTimer(true, Loc.T("MainWindowRun_IptalEdildi") + ModeLabel());
        }

        /// <summary>Zamanlayıcıyı durdurur. historyNote verilirse geçmişe yazılır.</summary>
        private void StopTimer(bool showSetup, string historyNote)
        {
            bool wasRunning = _phase != RunPhase.Idle;
            if (_ejecting) _ejectAborted = true;
            _phase = RunPhase.Idle;
            _warned = false;
            _fromCondition = false;
            if (_watcher != null) { _watcher.Dispose(); _watcher = null; }
            string sessionId = null;
            if (_recorder != null)
            {
                sessionId = _recorder.Id;
                _recorder.Finish(historyNote ?? Loc.T("MainWindowRun_YenidenKuruldu"));
                _recorder = null;
            }
            WatchNote.Visibility = Visibility.Collapsed;
            _audio.RestoreAll();
            _finalAlertShown = false;
            _fullscreenOpenedByAlert = false;
            PowerService.SetKeepAwake(false);
            Topmost = _settings.AlwaysOnTop;
            ExitFullscreen(false);
            TaskbarInfo.ProgressState = TaskbarItemProgressState.None;
            Title = AppTitle;
            AppTitleBar.Title = AppTitle;
            RunningBanner.Visibility = Visibility.Collapsed;
            _critical = false;

            if (wasRunning && historyNote != null)
            {
                HistoryService.Add(historyNote, sessionId);
                RefreshHistory();
            }
            if (showSetup) ShowView(ViewKind.Setup);
            UpdatePreview();
        }

        private void Snooze_Click(object sender, RoutedEventArgs e)
        {
            if (_phase != RunPhase.Countdown) return;
            if (_ejecting) _ejectAborted = true;
            DateTime now = _clock.Now;
            if (_target < now) _target = now;
            _target = _target.AddMinutes(_settings.SnoozeMinutes);
            _total = _target - _phaseStart;
            _warned = false;
            _finalAlertShown = false;
            _lastAlertBeepSecond = -1;
            Topmost = _settings.AlwaysOnTop;
            if (_fullscreenOpenedByAlert) { _fullscreenOpenedByAlert = false; ExitFullscreen(true); }
            Tick();
        }

        private bool _ejecting, _ejectAborted;

        private async void Execute(string reason = null)
        {
            PowerAction action = _action;
            bool force = _forceClose;
            reason = reason ?? _conditionReason ?? (_mode == TriggerMode.Clock ? Loc.T("MainWindowFullscreen_PlanlananSaatGeldi") : Loc.T("MainWindowFullscreen_SureDoldu"));
            if (_recorder != null) _recorder.CaptureFinal();
            if (_ejecting) return;
            string usb = null;
            if (_settings.EjectUsbBeforePower && PowerService.SupportsForceClose(action))
            {
                // Arka planda çıkar; arayüz donmaz. Başarısızlık işlemi engellemez (Windows kapanırken önbelleği zaten boşaltır).
                var drives = UsbEjectService.List();
                if (drives.Count > 0)
                {
                    _ejecting = true;
                    int ok = 0;
                    List<string> failed = await Task.Run(() => UsbEjectService.EjectAll(drives, out ok));
                    _ejecting = false;
                    // Çıkarma sürerken İptal Et / Ertele denmişse güç komutu verilmez
                    if (_ejectAborted) { _ejectAborted = false; return; }
                    usb = UsbEjectService.Summarize(ok, failed);
                    if (_recorder != null) _recorder.Note(WatchEventKind.Usb, usb, failed.Count > 0);
                }
            }
            StopTimer(true, TextFormat.ActionPast(action) + " · " + reason + (usb != null ? " · " + usb : ""));
            string error = PowerService.Execute(action, force);
            if (error != null)
            {
                HistoryService.Add(Loc.T("MainWindowRun_Basarisiz") + error);
                RefreshHistory();
                ShowInfo(error);
            }
        }

        // Son uyarıda bilgi satırı; her karede aygıt aranmasın diye uyarı başına bir kez okunur
        private string _usbNote;
        private string UsbNote()
        {
            if (_ejecting) return Loc.T("MainWindowRun_UsbSuruculerGuvenleCikaril");
            if (!_finalAlertActive || !_settings.EjectUsbBeforePower || !PowerService.SupportsForceClose(_action)) { _usbNote = null; return ""; }
            if (_usbNote == null)
            {
                var d = UsbEjectService.List();
                _usbNote = d.Count == 0 ? "" : Loc.T("MainWindowRun_IslemdenOnceGuvenleCikaril") + UsbEjectService.Describe(d) + ".";
            }
            return _usbNote;
        }

        private string ModeLabel()
        {
            switch (_mode)
            {
                case TriggerMode.Clock: return Loc.T("MainWindowRun_BelirliSaatte");
                case TriggerMode.Activity: return Loc.T("MainWindowRun_IslerBitince");
                default: return Loc.T("MainWindowRun_SureSonra");
            }
        }

        private void TurnOffDisplay_Click(object sender, RoutedEventArgs e)
        {
            PowerService.TurnOffDisplay();
        }
    }
}
