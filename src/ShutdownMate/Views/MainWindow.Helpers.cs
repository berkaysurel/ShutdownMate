using System;
using System.ComponentModel;
using System.Diagnostics;
using ShutdownMate.Services;

namespace ShutdownMate.Views
{
    public partial class MainWindow
    {
        // =====================================================================
        // Yardımcılar
        // =====================================================================

        private void SetTitle(string prefix)
        {
            Title = prefix + " · " + AppTitle;
            AppTitleBar.Title = Title;
        }

        private void ShowInfo(string message)
        {
            AppDialog.Inform(this, Loc.T("MainWindowHelpers_Bilgi"), message);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_phase != RunPhase.Idle)
            {
                if (!AppDialog.Ask(this, Loc.T("MainWindowHelpers_ZamanlayiciCalisiyor"),
                    Loc.T("MainWindowHelpers_PencereyiKapatirsanizZaman"),
                    AppDialogKind.Warning, Loc.T("MainWindowHelpers_KapatVeIptalEt")))
                {
                    e.Cancel = true;
                    return;
                }
                string sessionId = null;
                if (_recorder != null)
                {
                    sessionId = _recorder.Id;
                    _recorder.Finish(Loc.T("MainWindowHelpers_IptalEdildiUygulamaKapatil"));
                    _recorder = null;
                }
                HistoryService.Add(Loc.T("MainWindowHelpers_IptalEdildiUygulamaKapatil"), sessionId);
            }
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            // Pencere kapanınca her şey biter: Windows'a önceden komut gönderilmediği için
            // arkada bekleyen bir kapatma / uyku işlemi kalmaz.
            _timer.Stop();
            _phase = RunPhase.Idle;
            _hiddenForLock = false; // Kapanmış pencere yeniden gösterilemez
            ExitFullscreen(false);
            if (_previewFullscreen != null) _previewFullscreen.CloseFromOwner();
            if (_watcher != null) _watcher.Dispose();
            _metrics.Dispose();
            _audio.Dispose();
            PowerService.SetKeepAwake(false);
            if (_settings.RememberLastChoices) SaveSetupToSettings();
            SettingsService.Save(_settings);

            // shortcut: çıkıştaki takılmanın kök nedeni bulunmadı, görülürse süreç yığını alınıp giderilmeli.
            // Takılı süreç tek kopya kilidini tuttuğu için uygulama yeniden açılamaz; her şey kaydedildi, süreç sonlandırılır.
            var exitGuard = new System.Threading.Thread(() =>
            {
                System.Threading.Thread.Sleep(5000);
                try
                {
                    System.IO.File.AppendAllText(System.IO.Path.Combine(SettingsService.FolderPath, "error.log"),
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  Kapanista takilma: surec zorla sonlandirildi" + Environment.NewLine);
                }
                catch { }
                Process.GetCurrentProcess().Kill();
            });
            exitGuard.IsBackground = true;
            exitGuard.Start();

            base.OnClosed(e);
        }
    }
}
