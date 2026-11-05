using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using ShutdownMate.Helpers;
using ShutdownMate.Models;
using ShutdownMate.Services;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace ShutdownMate.Views
{
    public partial class MainWindow
    {
        // =====================================================================
        // Tam ekran
        // =====================================================================

        private static string LowerFirst(string text)
        {
            return string.IsNullOrEmpty(text) ? text : char.ToLower(text[0], Loc.Culture) + text.Substring(1);
        }

        private void EnterFullscreen_Click(object sender, RoutedEventArgs e)
        {
            EnterFullscreen();
        }

        private void LockScreen_Click(object sender, RoutedEventArgs e)
        {
            EnterFullscreen(true);
        }

        /// <param name="lockOnly">Tam ekran yerine ekran kilidi: arkadaki ekran görünür kalır, girdiler yok sayılır.</param>
        private async void EnterFullscreen(bool lockOnly = false)
        {
            if (_phase == RunPhase.Idle || _openingFullscreen) return;
            if (_fullscreen != null) { _fullscreen.Activate(); return; }
            _openingFullscreen = true;

            BitmapSource snapshot = lockOnly ? null : await CaptureBackdrop();

            _openingFullscreen = false;
            if (_phase == RunPhase.Idle) return;

            _fullscreen = new FullscreenWindow(this, _settings, snapshot, lockOnly);
            _fullscreen.ExitRequested += (s, e) => ExitFullscreen(true);
            _fullscreen.CancelRequested += (s, e) => StopTimer(true, Loc.T("MainWindowFullscreen_IptalEdildiKapanisUyarisin"));
            _fullscreen.SnoozeRequested += (s, e) => Snooze_Click(this, new RoutedEventArgs());
            _fullscreen.ExecuteRequested += (s, e) =>
            {
                // Onay ekranı açıkken uyarı bitmiş olabilir; yalnızca uyarı sürerken yapılır
                if (_phase == RunPhase.Countdown && _finalAlertActive) Execute(Loc.T("MainWindowFullscreen_KapanisUyarisindaElleOnayl"));
            };
            _fullscreen.Show();
            if (lockOnly) HideForLock();
            PushFullscreenState();
        }

        /// <summary>Kilit açıkken ana pencere gizlenir; zamanlayıcı içinde çalıştığı için kapatılamaz.</summary>
        private void HideForLock()
        {
            _hiddenForLock = true;
            Hide();
        }

        private void ShowAfterLock()
        {
            if (!_hiddenForLock) return;
            _hiddenForLock = false;
            Show();
        }

        private async Task<BitmapSource> CaptureBackdrop()
        {
            if (_settings.FullscreenBackground != FullscreenBackground.Blur) return null;

            // Ana pencere görüntüye girmesin diye kısa süre gizlenir.
            // Hata olursa düz arka planla devam edilir: kapanış uyarısı her koşulda açılabilmeli.
            try
            {
                if (WindowState == WindowState.Minimized) return null;
                Int32Rect bounds = ScreenCapture.MonitorBounds(this);
                Hide();
                await Task.Delay(250);
                return ScreenCapture.Capture(bounds);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (!IsVisible && WindowState != WindowState.Minimized) Show();
            }
        }

        /// <summary>Ayarların sonucunu örnek bir sayaçla gösterir; zamanlayıcıyla ilgisi yoktur, hiçbir işlem yapılmaz.</summary>
        private async void PreviewFullscreen_Click(object sender, RoutedEventArgs e)
        {
            if (_phase != RunPhase.Idle)
            {
                ShowInfo(Loc.T("MainWindowFullscreen_ZamanlayiciCalisirkenOnizl"));
                return;
            }
            OpenPreviewFullscreen(sender == PreviewLockButton || sender == PreviewStyleLockButton);
        }

        /// <summary>Zamanlayıcısız kilit: önizlemeyle aynı pencere. _phase Idle kalır, Execute'a giden tik dalı hiç çalışmaz.</summary>
        private void IdleLock_Click(object sender, RoutedEventArgs e)
        {
            if (_phase != RunPhase.Idle || _previewFullscreen != null || _openingFullscreen) return;
            _idleLockStart = Environment.TickCount64;
            OpenPreviewFullscreen(sender == IdleLockButton);
        }

        private async void OpenPreviewFullscreen(bool lockOnly)
        {
            if (_previewFullscreen != null || _openingFullscreen) return;
            _openingFullscreen = true;
            BitmapSource snapshot = lockOnly ? null : await CaptureBackdrop();
            _openingFullscreen = false;

            _previewFullscreen = new FullscreenWindow(this, _settings, snapshot, lockOnly);
            _previewFullscreen.ExitRequested += (s, args) => ClosePreviewFullscreen();
            _previewFullscreen.Show();
            if (lockOnly || _idleLockStart.HasValue) HideForLock();
            UpdatePreviewFullscreen();
        }

        private void ClosePreviewFullscreen()
        {
            if (_previewFullscreen == null) return;
            _previewFullscreen.CloseFromOwner();
            _previewFullscreen = null;
            _idleLockStart = null;
            ShowAfterLock();
            BringToFront();
        }

        private void UpdatePreviewFullscreen()
        {
            if (_previewFullscreen == null) return;
            if (_idleLockStart.HasValue)
            {
                _previewFullscreen.Update(new CountdownState
                {
                    MainText = TextFormat.Clock(TimeSpan.FromMilliseconds(Environment.TickCount64 - _idleLockStart.Value)),
                    Caption = Loc.T("Count_Kilitli"),
                    IdleLock = true,
                    Progress = -1,
                    Icon = SymbolRegular.LockClosed24,
                    Cpu = _metrics.Cpu,
                    Gpu = _metrics.Gpu,
                    DownloadBps = _metrics.DownloadBps,
                    UploadBps = _metrics.UploadBps,
                    TransferBps = CurrentTransferRate()
                });
                return;
            }
            _previewFullscreen.Update(new CountdownState
            {
                MainText = "01:23:45",
                Caption = Loc.T("Count_Kalan"),
                Target = Loc.T("MainWindowFullscreen_Onizleme") + TextFormat.ActionFuture(SelectedAction),
                Status = Loc.T("MainWindowFullscreen_BuBirOnizlemedirZamanlayic"),
                Progress = 0.65,
                Icon = TextFormat.ActionIcon(SelectedAction),
                Cpu = _metrics.Cpu,
                Gpu = _metrics.Gpu,
                DownloadBps = _metrics.DownloadBps,
                UploadBps = _metrics.UploadBps,
                TransferBps = CurrentTransferRate()
            });
        }

        /// <summary>
        /// Dosya aktarımı kartının değeri: İşler sekmesinde bir sürücü seçiliyse o, değilse otomatik kipteki gibi
        /// en yüksek hızlı sürücü. Tam ekranda kaynak ayrıca seçilmez.
        /// </summary>
        private double CurrentTransferRate()
        {
            string drive = TransferMonitor.DriveOf(SelectedProgramName(TransferSourceCombo));
            return drive != null ? _metrics.Transfer.RateOf(drive) : _metrics.Transfer.MaxRate();
        }

        private void ExitFullscreen(bool showCountdown)
        {
            if (_fullscreen == null) return;
            _fullscreen.CloseFromOwner();
            _fullscreen = null;
            ShowAfterLock();
            if (showCountdown && _phase != RunPhase.Idle)
            {
                ShowView(ViewKind.Countdown);
                BringToFront();
            }
        }

        private void PushFullscreenState()
        {
            if (_fullscreen == null) return;
            _fullscreen.Update(new CountdownState
            {
                MainText = _displayMain,
                Caption = _displayCaption,
                Target = CountdownTarget.Text,
                Status = CountdownStatus.Text,
                Progress = _progress,
                Critical = _critical,
                Icon = TextFormat.ActionIcon(_action),
                Chips = BuildChipStates(),
                FinalAlert = _finalAlertActive,
                FinalSecondsLeft = _finalSecondsLeft,
                FinalProgress = _finalProgress,
                FinalHeadline = TextFormat.ActionHeadline(_action),
                FinalDetail = (_conditionReason ?? (_mode == TriggerMode.Clock ? Loc.T("MainWindowFullscreen_PlanlananSaatGeldi") : Loc.T("MainWindowFullscreen_SureDoldu")))
                    + Loc.T("MainWindowFullscreen_MasadaysanizIptalEdebilirY")
                    + UsbNote(),
                FinalNotice = ActiveNoticeText(),
                FinalNowLabel = TextFormat.ActionNow(_action),
                FinalConfirmText = Loc.T("MainWindowFullscreen_EminMisinizIslemHemen"),
                FinalConfirmLabel = Loc.T("MainWindowFullscreen_EvetS", LowerFirst(TextFormat.ActionNow(_action))),
                Cpu = _metrics.Cpu,
                Gpu = _metrics.Gpu,
                DownloadBps = _metrics.DownloadBps,
                UploadBps = _metrics.UploadBps,
                TransferBps = CurrentTransferRate()
            });
        }

        private bool _finalAlertActive;
        private int _finalSecondsLeft;
        private double _finalProgress;

        private List<MetricChipState> BuildChipStates()
        {
            var list = new List<MetricChipState>();
            if (_watcher == null) return list;
            if (_watcher.UseNetwork) list.Add(new MetricChipState { Label = _watcher.NetworkLabel(_metrics), Active = _watcher.NetworkActive, Icon = SymbolRegular.ArrowSwap24 });
            if (_watcher.UseCpu) list.Add(new MetricChipState { Label = _watcher.CpuLabel(), Active = _watcher.CpuActive, Icon = SymbolRegular.DeveloperBoard24 });
            if (_watcher.UseTransfer) list.Add(new MetricChipState { Label = _watcher.TransferLabel(), Active = _watcher.TransferActive, Icon = SymbolRegular.Folder24 });
            if (_watcher.UseGpu) list.Add(new MetricChipState { Label = _watcher.GpuLabel(), Active = _watcher.GpuActive, Icon = SymbolRegular.VideoClip24 });
            if (_watcher.UseProcess)
                list.Add(new MetricChipState { Label = (_watcher.ProcessName ?? "") + (_watcher.ProcessRunning ? Loc.T("MainWindowFullscreen_Acik") : Loc.T("WatchRecorder_Kapandi")), Active = _watcher.ProcessRunning, Icon = SymbolRegular.AppGeneric24 });
            return list;
        }
    }
}
