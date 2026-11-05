using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ShutdownMate.Services
{
    /// <summary>
    /// Sürücülerin okuma+yazma hızını (bayt/sn) ölçer. Sürücü anahtarı: "drive:D:". Yalnızca diskler; internet trafiği
    /// "İndirme / yükleme", tek program ise program ölçümü kapsamındadır. Sayaçlar Windows performans sayaçlarından (PDH)
    /// bir kez açılır ve SystemMetrics ile aynı turda örneklenir; karar mantığı TransferTracker'dadır.
    /// </summary>
    internal sealed class TransferMonitor : IDisposable
    {
        [StructLayout(LayoutKind.Explicit)]
        private struct PdhValue
        {
            [FieldOffset(0)] public uint Status;
            [FieldOffset(8)] public double Value;
        }

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern uint PdhOpenQuery(string dataSource, IntPtr userData, out IntPtr query);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern uint PdhAddEnglishCounter(IntPtr query, string counterPath, IntPtr userData, out IntPtr counter);

        [DllImport("pdh.dll")]
        private static extern uint PdhCollectQueryData(IntPtr query);

        [DllImport("pdh.dll")]
        private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, out PdhValue value);

        [DllImport("pdh.dll")]
        private static extern uint PdhCloseQuery(IntPtr query);

        private const uint PdhFmtDouble = 0x00000200;

        private readonly object _lock = new object();
        private readonly Dictionary<string, IntPtr> _counters = new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase);
        private IntPtr _query;
        private List<string> _drives = new List<string>();
        private long _drivesTicks = -60000;
        private Dictionary<string, double> _rates = new Dictionary<string, double>();

        public bool IsAvailable { get; private set; }

        /// <summary>Her tam örnekte artar; tüketici aynı örneği iki kez saymasın diye.</summary>
        public long SampleId { get; private set; }

        public TransferMonitor()
        {
            try
            {
                if (PdhOpenQuery(null, IntPtr.Zero, out _query) != 0) return;
                IsAvailable = AddCounter(@"\LogicalDisk(_Total)\Disk Bytes/sec") != IntPtr.Zero; // yalnızca sayaçların var olduğunu yoklar
                if (IsAvailable) PdhCollectQueryData(_query);
            }
            catch
            {
                IsAvailable = false;
            }
        }

        private IntPtr AddCounter(string path)
        {
            IntPtr counter;
            if (_counters.TryGetValue(path, out counter)) return counter;
            if (PdhAddEnglishCounter(_query, path, IntPtr.Zero, out counter) != 0) return IntPtr.Zero; // başarısız sonuç saklanmaz, sonraki turda yeniden denenir
            _counters[path] = counter;
            return counter;
        }

        public static double MegabytesToBytes(int megabytes)
        {
            return megabytes * 1024.0 * 1024.0;
        }

        /// <summary>"drive:D:" -> "D:"; otomatik ("") ya da geçersiz anahtar -> null.</summary>
        public static string DriveOf(string key)
        {
            return !string.IsNullOrEmpty(key) && key.StartsWith("drive:") && key.Length > 6 ? key.Substring(6) : null;
        }

        /// <summary>Son örnekteki sürücü hızları (bayt/sn); yeni bir kopyadır.</summary>
        public Dictionary<string, double> Rates()
        {
            lock (_lock) return new Dictionary<string, double>(_rates);
        }

        public double RateOf(string drive)
        {
            lock (_lock)
            {
                double v;
                return drive != null && _rates.TryGetValue(drive, out v) ? v : -1;
            }
        }

        public double MaxRate()
        {
            lock (_lock) return _rates.Count == 0 ? -1 : _rates.Values.Max();
        }

        public void Sample()
        {
            lock (_lock)
            {
                var rates = new Dictionary<string, double>();
                try
                {
                    if (IsAvailable)
                    {
                        var counters = Drives().ToDictionary(d => d, d => AddCounter(@"\LogicalDisk(" + d + @")\Disk Bytes/sec"));
                        PdhCollectQueryData(_query);
                        foreach (var kv in counters)
                        {
                            uint type;
                            PdhValue v;
                            if (kv.Value != IntPtr.Zero && PdhGetFormattedCounterValue(kv.Value, PdhFmtDouble, out type, out v) == 0)
                                rates[kv.Key] = Math.Max(0, v.Value);
                        }
                    }
                }
                catch
                {
                    rates.Clear();
                }
                _rates = rates;
                SampleId++;
            }
        }

        /// <summary>Yerel ve çıkarılabilir sürücü harfleri ("C:"); yeni takılanlar görünsün diye yarım dakikada bir yenilenir.</summary>
        private List<string> Drives()
        {
            if (Environment.TickCount64 - _drivesTicks > 30000)
            {
                _drivesTicks = Environment.TickCount64;
                _drives = ListDrives().Select(kv => kv.Key.Substring(6)).ToList();
            }
            return _drives;
        }

        /// <summary>Seçilebilecek sürücüler (anahtar, görünen ad): yerel ve çıkarılabilir.</summary>
        public static List<KeyValuePair<string, string>> ListDrives()
        {
            var list = new List<KeyValuePair<string, string>>();
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    if (!d.IsReady || (d.DriveType != DriveType.Fixed && d.DriveType != DriveType.Removable)) continue;
                    string letter = d.Name.Substring(0, 2);
                    string label = string.IsNullOrWhiteSpace(d.VolumeLabel) ? "" : " " + d.VolumeLabel;
                    string kind = d.DriveType == DriveType.Removable ? Loc.T("TransferMonitor_Cikarilabilir") : Loc.T("TransferMonitor_Yerel");
                    list.Add(new KeyValuePair<string, string>("drive:" + letter, Loc.T("TransferMonitor_Surucu") + letter + label + " (" + kind + ")"));
                }
            }
            catch { }
            return list;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_query != IntPtr.Zero) { try { PdhCloseQuery(_query); } catch { } _query = IntPtr.Zero; }
                IsAvailable = false;
            }
        }
    }
}
