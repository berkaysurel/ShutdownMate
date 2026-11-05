using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ShutdownMate.Services;
using ShutdownMate.Views;

// Sistem DLL'leri yalnızca System32'den yüklenir; program klasörüne bırakılan sahte DLL yüklenmez
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace ShutdownMate
{
    public partial class App : Application
    {
        private const string InstanceId = "ShutdownMate-BSRL-SingleInstance";
        private Mutex _instanceMutex;
        private EventWaitHandle _showSignal;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Aynı anda tek zamanlayıcı: ikinci kez açılırsa mevcut pencere öne gelir.
            bool isFirst;
            _instanceMutex = new Mutex(true, InstanceId, out isFirst);
            if (!isFirst && Array.IndexOf(e.Args, "--restart") >= 0)
            {
                // Dil değişince kendini yeniden açan örnek: eskisinin kapanmasını en çok 5 sn bekler
                try { isFirst = _instanceMutex.WaitOne(5000); }
                catch (AbandonedMutexException) { isFirst = true; }
            }
            if (!isFirst)
            {
                try { EventWaitHandle.OpenExisting(InstanceId + "-show").Set(); } catch { }
                Shutdown();
                return;
            }

            _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceId + "-show");
            var listener = new Thread(() =>
            {
                while (_showSignal.WaitOne())
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var window = MainWindow as MainWindow;
                        if (window != null) window.BringToFront();
                    }));
                }
            });
            listener.IsBackground = true;
            listener.Start();

            DispatcherUnhandledException += OnDispatcherUnhandledException;

            base.OnStartup(e);
            Loc.Init(SettingsService.PeekLanguage());
            var main = new MainWindow();
            MainWindow = main;
            main.Show();
        }

        /// <summary>
        /// Beklenmeyen bir hata uygulamayı düşürmez: çalışan zamanlayıcı ve iptal düğmesi ayakta kalır,
        /// kısılan sesler askıda kalmaz. Hata ayar klasöründeki error.log dosyasına yazılır.
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(SettingsService.FolderPath);
                string logPath = Path.Combine(SettingsService.FolderPath, "error.log");

                // Tekrarlayan bir hata diski doldurmasın: 1 MB'yi aşınca eski kayıt tek yedek olarak kalır
                if (File.Exists(logPath) && new FileInfo(logPath).Length > 1024 * 1024)
                    File.Move(logPath, logPath + ".old", true);

                File.AppendAllText(logPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + e.Exception + Environment.NewLine);
            }
            catch { }

            // Ana pencere açılmadan oluşan hata yutulmaz: yoksa uygulama penceresiz, görünmez biçimde açık kalır
            e.Handled = MainWindow != null && MainWindow.IsLoaded;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            PowerService.SetKeepAwake(false);
            base.OnExit(e);
        }
    }
}
