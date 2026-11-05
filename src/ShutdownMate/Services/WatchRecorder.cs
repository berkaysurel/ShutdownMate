using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using ShutdownMate.Helpers;
using ShutdownMate.Models;

namespace ShutdownMate.Services
{
    /// <summary>
    /// Bir zamanlayıcı çalışmasının gözetim kaydını tutar. Kullanıcı masadan ayrıldığında olanları
    /// (program takıldı mı, kapandı mı, internet koptu mu, Windows hata verdi mi) ekran görüntüleriyle saklar;
    /// bilgisayar kapandıktan sonra da Geçmiş'te görülebilir.
    /// Kayıt her olayda diske yazılır: elektrik kesilse de o ana kadarki kayıt kalır.
    /// </summary>
    internal sealed class WatchRecorder : IDisposable
    {
        [DllImport("user32.dll")] private static extern bool IsHungAppWindow(IntPtr hwnd);

        private const int MaxImages = 200;
        private static readonly TimeSpan ProblemImageGap = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan IdleAfter = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan HungAfter = TimeSpan.FromSeconds(10);

        private readonly WatchSession _session;
        private readonly string _folder;
        private readonly bool _screenshots, _windowOnly, _captureMinimized, _systemErrors;
        private readonly int _keepSessions;
        private readonly long _maxBytes;
        private readonly TimeSpan _periodic;
        private readonly bool _exitExpected;
        private readonly ProcessMonitor _program;
        private readonly IdleDetector _idle = new IdleDetector(IdleAfter);

        // Arka planda (Sample) güncellenir, arayüz iş parçacığında (Tick) okunur
        private double _cpu = -1, _gpu = -1, _io = -1;
        private bool _running;
        private volatile bool _networkUp = true;
        private IntPtr _window;
        private DateTime? _hungSince;
        private int _sampleCount;

        private bool _wasRunning, _hungReported, _lastNetworkUp = true, _finished;
        private DateTime _lastSecond, _lastPeriodic, _lastProblemImage = DateTime.MinValue, _lastFinal = DateTime.MinValue;
        private double _sumCpu, _sumGpu, _sumIo;
        private int _sumCount, _imageCount;
        private DateTime _minuteStart;
        private readonly AppSettings _stampSettings;

        /// <summary>Gözetim kayıtlarının ana klasörü: %APPDATA%\ShutdownMate\Gecmis.</summary>
        public static string RootPath
        {
            get { return Path.Combine(SettingsService.FolderPath, "Gecmis"); }
        }

        private static bool CanWrite(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
                string probe = Path.Combine(folder, ".yaz");
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string NextId(DateTime now, bool serial)
        {
            string name = SafePath.Name(now);
            if (!serial) return name;
            int next = 1;
            foreach (string dir in SessionFolders()) next = Math.Max(next, SafePath.Serial(Path.GetFileName(dir)) + 1);
            return next.ToString("D4") + "_" + name;
        }

        /// <param name="mode">Örnek: "Süre sonra kapat".</param>
        /// <param name="programName">İzlenecek program; null ise yalnızca tüm sistem ölçülür.</param>
        /// <param name="exitExpected">Programın kapanması beklenen sonuçsa (program kapanınca modu) sorun sayılmaz.</param>
        /// <param name="settings">Gözetim kaydı ayarları başlangıçta okunur; çalışırken değişirse sonraki kayıtta geçerli olur.</param>
        public WatchRecorder(string mode, string programName, bool exitExpected, AppSettings settings)
        {
            DateTime now = DateTime.Now;
            _session = new WatchSession { Id = NextId(now, settings.WatchSerialNames), Start = now, Mode = mode };
            _stampSettings = settings;
            _folder = Path.Combine(RootPath, _session.Id);
            // Klasör oluşturulamaz/yazılamazsa uygulama çalışmaya devam eder; kayda tek not düşülür
            bool cannotWrite = !CanWrite(_folder);
            _screenshots = settings.RecordScreenshots;
            _windowOnly = settings.ScreenshotWindowOnly;
            _captureMinimized = settings.CaptureMinimizedProgram;
            _systemErrors = settings.WatchIncludeSystemErrors;
            _keepSessions = settings.WatchKeepSessions;
            _maxBytes = settings.WatchMaxMegabytes * 1024L * 1024L;
            _periodic = settings.ScreenshotMinutes > 0 ? TimeSpan.FromMinutes(settings.ScreenshotMinutes) : TimeSpan.Zero;
            _exitExpected = exitExpected;
            _lastSecond = _lastPeriodic = _minuteStart = now;

            if (!string.IsNullOrWhiteSpace(programName))
            {
                _program = new ProcessMonitor(programName);
                _session.Program = _program.ProcessName;
                // "Yalnızca program penceresi" seçiliyse ilk görüntü de tüm ekran olmasın
                CheckWindow();
            }

            Prune();
            Add(WatchEventKind.Started, _session.Program != null ? _session.Program + Loc.T("WatchRecorder_Izleniyor") : Loc.T("WatchRecorder_ZamanlayiciBasladi"), false, true);
            if (cannotWrite) Add(WatchEventKind.Started, Loc.T("WatchRecorder_KayitKlasoruneYazilamiyorG"), true, false);
        }

        public string Id { get { return _session.Id; } }

        /// <summary>Arka plan iş parçacığında saniyede bir çağrılır (SystemMetrics.Sample ile aynı turda).</summary>
        public void Sample(SystemMetrics metrics)
        {
            if (_finished) return;
            _sampleCount++;

            if (_program != null)
            {
                _program.Sample(metrics.GpuMonitor);
                _running = _program.IsRunning;
                _cpu = _program.CpuPercent;
                _gpu = _program.GpuPercent;
                _io = _program.IoBytesPerSecond;
                if (_sampleCount % 5 == 1) CheckWindow();
            }
            else
            {
                _cpu = metrics.Cpu;
                _gpu = metrics.Gpu;
                _io = metrics.DownloadBps + metrics.UploadBps;
            }

            if (_sampleCount % 5 == 1)
            {
                try { _networkUp = NetworkInterface.GetIsNetworkAvailable(); } catch { }
            }
        }

        // Programın ana penceresi: yanıt vermiyor denetimi ve kilitli ekranda yedek görüntü için
        private void CheckWindow()
        {
            IntPtr window = IntPtr.Zero;
            Process[] processes = new Process[0];
            try
            {
                processes = Process.GetProcessesByName(_program.ProcessName);
                foreach (Process p in processes)
                {
                    try
                    {
                        if (p.MainWindowHandle != IntPtr.Zero) { window = p.MainWindowHandle; break; }
                    }
                    catch { }
                }
            }
            catch { }
            finally
            {
                foreach (Process p in processes) { try { p.Dispose(); } catch { } }
            }

            _window = window;
            if (window != IntPtr.Zero && IsHungAppWindow(window))
                _hungSince = _hungSince ?? DateTime.Now;
            else
                _hungSince = null;
        }

        /// <summary>Arayüz iş parçacığında, zamanlayıcının her tıkında çağrılır; saniyede bir değerlendirir.</summary>
        public void Tick()
        {
            if (_finished) return;
            DateTime now = DateTime.Now;
            if ((now - _lastSecond).TotalSeconds < 1) return;
            _lastSecond = now;

            if (_program != null)
            {
                string name = _program.ProcessName;
                if (_wasRunning && !_running)
                    Add(WatchEventKind.ProgramExited, name + Loc.T("WatchRecorder_Kapandi"), !_exitExpected, true);
                _wasRunning = _running;

                WatchEventKind? idle = _idle.Feed(now, _running, _cpu, _gpu, _io);
                if (idle == WatchEventKind.Idle)
                    Add(WatchEventKind.Idle, name + " " + (int)_idle.IdleFor.TotalMinutes + Loc.T("WatchRecorder_DakikadirBostaIsBitti"), false, true);
                else if (idle == WatchEventKind.Resumed)
                    Add(WatchEventKind.Resumed, name + Loc.T("WatchRecorder_YenidenCalisiyor"), false, false);

                bool hung = _hungSince.HasValue && now - _hungSince.Value >= HungAfter;
                if (hung && !_hungReported)
                {
                    _hungReported = true;
                    Add(WatchEventKind.NotResponding, name + Loc.T("WatchRecorder_YanitVermiyor"), true, true);
                }
                else if (!_hungSince.HasValue && _hungReported)
                {
                    _hungReported = false;
                    Add(WatchEventKind.Resumed, name + Loc.T("WatchRecorder_YenidenYanitVeriyor"), false, false);
                }
            }

            bool networkUp = _networkUp;
            if (networkUp != _lastNetworkUp)
            {
                _lastNetworkUp = networkUp;
                if (networkUp) Add(WatchEventKind.NetworkBack, Loc.T("WatchRecorder_InternetBaglantisiGeriGeld"), false, false);
                else Add(WatchEventKind.NetworkLost, Loc.T("WatchRecorder_InternetBaglantisiKoptu"), true, true);
            }

            if (_periodic > TimeSpan.Zero && now - _lastPeriodic >= _periodic)
            {
                _lastPeriodic = now;
                Add(WatchEventKind.Periodic, Loc.T("WatchRecorder_AraGoruntu"), false, true);
            }

            AccumulateSample(now);
        }

        private void AccumulateSample(DateTime now)
        {
            if (_cpu >= 0) _sumCpu += _cpu;
            if (_gpu >= 0) _sumGpu += _gpu;
            if (_io >= 0) _sumIo += _io;
            _sumCount++;
            if ((now - _minuteStart).TotalMinutes < 1) return;

            _session.Samples.Add(new WatchSample
            {
                Time = _minuteStart,
                Cpu = Math.Round(_sumCpu / _sumCount, 1),
                Gpu = _gpu < 0 ? -1 : Math.Round(_sumGpu / _sumCount, 1),
                Io = Math.Round(_sumIo / _sumCount)
            });
            _sumCpu = _sumGpu = _sumIo = 0;
            _sumCount = 0;
            _minuteStart = now;
            Save();
        }

        /// <summary>İşlemden hemen önceki son ekranı kaydeder. Ertelemeden sonra yeniden alınabilir (dakikada en çok bir kez).</summary>
        public void CaptureFinal()
        {
            if (_finished || DateTime.Now - _lastFinal < TimeSpan.FromMinutes(1)) return;
            _lastFinal = DateTime.Now;
            Add(WatchEventKind.Final, Loc.T("WatchRecorder_IslemdenOncekiSonEkran"), false, true);
        }

        /// <summary>Kayda görüntüsüz bir olay ekler (ör. USB sürücülerin çıkarılma sonucu).</summary>
        public void Note(WatchEventKind kind, string text, bool problem)
        {
            if (!_finished) Add(kind, text, problem, false);
        }

        /// <summary>Kaydı kapatır: sonucu ve bu süredeki Windows hatalarını ekler, diske yazar.</summary>
        public void Finish(string result)
        {
            if (_finished) return;
            _finished = true;
            DateTime now = DateTime.Now;
            _session.Result = result;
            _session.End = now;
            if (_systemErrors) _session.Events.AddRange(SystemErrorLog.Read(_session.Start, now));
            _session.Events.Add(new WatchEvent { Time = now, Kind = WatchEventKind.Finished, Text = result ?? Loc.T("WatchRecorder_Bitti") });
            _session.Events.Sort((a, b) => a.Time.CompareTo(b.Time));
            Save();
            if (_program != null) _program.Dispose();
            Prune();
        }

        private void Add(WatchEventKind kind, string text, bool problem, bool withImage)
        {
            DateTime now = DateTime.Now;
            var e = new WatchEvent { Time = now, Kind = kind, Text = text, Problem = problem };

            // Sorun olayları art arda gelirse (ör. bağlantı gidip gelirse) her birine görüntü alınmaz
            bool throttled = problem && now - _lastProblemImage < ProblemImageGap;
            if (withImage && _screenshots && _imageCount < MaxImages && !throttled)
            {
                // Ad saat_gün_ay_yıl; aynı saniyede ikinci görüntü "_2" alır
                string file = SafePath.Name(now) + ".jpg";
                for (int n = 2; File.Exists(Path.Combine(_folder, file)); n++) file = SafePath.Name(now) + "_" + n + ".jpg";
                string path = Path.Combine(_folder, file);
                // Program seçilmediyse "yalnızca program penceresi" ayarında da tüm ekran alınır
                bool windowOnly = _windowOnly && _program != null;
                string[] stamp = StampLines(now);
                if (windowOnly ? ScreenCapture.SaveWindow(path, _window, _captureMinimized, stamp) : ScreenCapture.SaveScreen(path, _window, stamp))
                {
                    e.Image = file;
                    _imageCount++;
                    if (problem) _lastProblemImage = now;
                }
                else
                {
                    e.Text += !windowOnly ? Loc.T("WatchRecorder_EkranGoruntusuAlinamadiEkr")
                        : ScreenCapture.IsMinimized(_window) ? Loc.T("WatchRecorder_ProgramKucultulmusPenceres")
                        : Loc.T("WatchRecorder_ProgramPenceresininGoruntu");
                }
            }

            _session.Events.Add(e);
            Save();
        }

        /// <summary>Görüntünün sağ altına yazılacak satırlar; damga kapalıysa ya da seçili öğe yoksa null.</summary>
        private string[] StampLines(DateTime now)
        {
            AppSettings s = _stampSettings;
            if (!s.StampEnabled) return null;
            var lines = new List<string>();
            string when = (s.StampDate ? now.ToString("dd.MM.yyyy") : "") + (s.StampDate && s.StampTime ? "  " : "") + (s.StampTime ? now.ToString("HH:mm:ss") : "");
            if (when.Length > 0) lines.Add(when);
            if (s.StampMode && !string.IsNullOrEmpty(_session.Mode)) lines.Add(_session.Mode);
            if (s.StampProgram && _program != null) lines.Add(Loc.T("WatchRecorder_Izlenen") + _program);
            if (!string.IsNullOrWhiteSpace(s.StampNote)) lines.Add(s.StampNote.Trim());
            return lines.Count == 0 ? null : lines.ToArray();
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(_folder);
                SettingsService.WriteAtomic(Path.Combine(_folder, "kayit.json"),
                    JsonSerializer.Serialize(_session, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        /// <summary>Bir kaydın klasörü; kimlik geçersizse null.</summary>
        public static string FolderOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            string folder = Path.Combine(RootPath, id);
            return SafePath.IsSessionFolder(RootPath, folder) ? folder : null;
        }

        /// <summary>Kaydı okur; yoksa ya da bozuksa null.</summary>
        public static WatchSession Load(string id)
        {
            try
            {
                string folder = FolderOf(id);
                string file = folder == null ? null : Path.Combine(folder, "kayit.json");
                if (file == null || !File.Exists(file)) return null;
                return JsonSerializer.Deserialize<WatchSession>(File.ReadAllText(file));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Kayıt sayısı ve kapladığı alan (bayt).</summary>
        public static void Usage(out int count, out long bytes)
        {
            count = 0;
            bytes = 0;
            foreach (string dir in SessionFolders())
            {
                count++;
                bytes += FolderSize(dir);
            }
        }

        /// <summary>Tüm gözetim kayıtlarını ve ekran görüntülerini siler (onaydan sonra çağrılır).</summary>
        public static void DeleteAll()
        {
            foreach (string dir in SessionFolders()) SafeDelete(dir);
        }

        // Sürmekte olan kaydın klasörü kimlikle korunur (saat geri alınsa da silinmez)
        private void Prune()
        {
            SafePath.Prune(RootPath, _keepSessions, _maxBytes, _session.Id);
        }

        // Yalnızca kayıt klasörünün altındaki tarih adlı klasörler, yeniden eskiye
        private static List<string> SessionFolders()
        {
            try
            {
                if (!Directory.Exists(RootPath)) return new List<string>();
                return Directory.GetDirectories(RootPath)
                    .Where(d => SafePath.IsSessionFolder(RootPath, d))
                    .OrderByDescending(d => SafePath.SessionTime(Path.GetFileName(d)))
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static long FolderSize(string dir)
        {
            try { return new DirectoryInfo(dir).GetFiles().Sum(f => f.Length); }
            catch { return 0; }
        }

        private static void SafeDelete(string dir)
        {
            if (!SafePath.IsSessionFolder(RootPath, dir)) return;
            try { Directory.Delete(dir, true); } catch { }
        }
        public void Dispose()
        {
            if (_program != null) _program.Dispose();
        }
    }
}
