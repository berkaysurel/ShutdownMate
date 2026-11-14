using System;
using System.IO;

namespace ShutdownMate.Services
{
    /// <summary>
    /// "Dosya / klasör" koşulu: seçilen dosya ya da klasör büyümeyi/değişmeyi bırakınca iş bitmiş sayılır.
    /// Disk okuma (<see cref="Sample"/>) arka planda yapılır; karar mantığı (<see cref="Apply"/>, <see cref="IsActive"/>) saftır.
    /// Güvenli hata: yol okunamıyorsa (silindi, USB çıktı, ağ koptu, erişim yok) ya da ölçüm bayatladıysa "sakin" SAYILMAZ.
    /// </summary>
    internal sealed class FileActivityMonitor
    {
        /// <summary>shortcut: klasörde en çok bu kadar dosya taranır; aşılırsa yalnızca ilk dosyaların ve klasörün en yeni yazma zamanına bakılır, boyut gösterilmez. Çok büyük klasörler için dosya değişim bildirimi (FileSystemWatcher) gerekir.</summary>
        public const int MaxFiles = 5000;

        /// <summary>Son değişiklikten sonra bu kadar saniye "sürüyor" kalır: yazma aralarındaki kısa duraklamalar bitiş sayılmaz.</summary>
        public const int HoldSeconds = 10;

        /// <summary>Bu süredir yeni ölçüm gelmediyse (ör. ağ sürücüsü takıldı) yol okunamıyor sayılır.</summary>
        public const int StaleSeconds = 15;

        private readonly object _lock = new object();
        private bool _hasSample, _readable, _everChanged, _capped;
        private long _size = -1, _stamp;
        private DateTime _lastChange = DateTime.MinValue, _lastSample = DateTime.MinValue;

        public string Path { get; private set; }

        public FileActivityMonitor(string path)
        {
            Path = path ?? "";
        }

        /// <summary>Ölçümü diskten okur (arka plan iş parçacığında çağrılır).</summary>
        public void Sample(DateTime now)
        {
            bool ok = false, capped = false;
            long size = -1, stamp = 0;
            try
            {
                if (File.Exists(Path))
                {
                    var info = new FileInfo(Path);
                    size = info.Length;
                    stamp = info.LastWriteTimeUtc.Ticks;
                    ok = true;
                }
                else if (Directory.Exists(Path))
                {
                    var dir = new DirectoryInfo(Path);
                    var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
                    long total = 0, newest = dir.LastWriteTimeUtc.Ticks;
                    int count = 0;
                    foreach (FileInfo f in dir.EnumerateFiles("*", options))
                    {
                        if (++count > MaxFiles) { capped = true; break; }
                        total += f.Length;
                        if (f.LastWriteTimeUtc.Ticks > newest) newest = f.LastWriteTimeUtc.Ticks;
                    }
                    size = capped ? -1 : total;
                    stamp = newest;
                    ok = true;
                }
            }
            catch
            {
                ok = false;
            }
            Apply(ok, size, stamp, capped, now);
        }

        /// <summary>Bir ölçümü işler (saf mantık). Değer önceki ölçümden farklıysa değişiklik sayılır.</summary>
        public void Apply(bool readable, long size, long stamp, bool capped, DateTime now)
        {
            lock (_lock)
            {
                _lastSample = now;
                _readable = readable;
                if (!readable) return;
                _capped = capped;
                if (_hasSample && (size != _size || stamp != _stamp))
                {
                    _lastChange = now;
                    _everChanged = true;
                }
                _hasSample = true; // ilk ölçüm yalnızca başlangıç değeridir
                _size = size;
                _stamp = stamp;
            }
        }

        /// <summary>Okunamıyor: yol yok / erişilemiyor ya da ölçüm bayatladı. İlk ölçüm gelene kadar okunamıyor sayılmaz (bekleme).</summary>
        public bool IsUnreadable(DateTime now)
        {
            lock (_lock)
            {
                if (_lastSample == DateTime.MinValue) return false;
                return !_readable || (now - _lastSample).TotalSeconds > StaleSeconds;
            }
        }

        /// <summary>İlk ölçüm henüz alınmadı.</summary>
        public bool IsPending
        {
            get { lock (_lock) return _lastSample == DateTime.MinValue; }
        }

        /// <summary>Başlangıçtan beri en az bir değişiklik görüldü ("işlerin başlamasını bekle" için).</summary>
        public bool EverChanged
        {
            get { lock (_lock) return _everChanged; }
        }

        /// <summary>Sürüyor: yakın zamanda değişti ya da okunamıyor (okunamayan ölçüm sakin sayılmaz).</summary>
        public bool IsActive(DateTime now)
        {
            if (IsUnreadable(now)) return true;
            lock (_lock) return _everChanged && (now - _lastChange).TotalSeconds < HoldSeconds;
        }

        /// <summary>Son değişiklikten bu yana geçen süre; hiç değişmediyse null.</summary>
        public TimeSpan? SinceChange(DateTime now)
        {
            lock (_lock) return _everChanged ? now - _lastChange : (TimeSpan?)null;
        }

        /// <summary>Son okunan boyut (bayt); bilinmiyorsa ya da klasör çok büyükse -1.</summary>
        public long Size
        {
            get { lock (_lock) return _size; }
        }

        public bool Capped
        {
            get { lock (_lock) return _capped; }
        }
    }
}
