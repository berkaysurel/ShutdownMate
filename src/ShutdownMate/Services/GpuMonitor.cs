using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ShutdownMate.Services
{
    /// <summary>
    /// Ekran kartı kullanımını Windows performans sayaçlarından ("GPU Engine") okur.
    /// Görev Yöneticisi ile aynı yöntem: her motorun süreç değerleri toplanır, en yüksek motor alınır.
    /// </summary>
    internal sealed class GpuMonitor : IDisposable
    {
        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern uint PdhOpenQuery(string dataSource, IntPtr userData, out IntPtr query);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern uint PdhAddEnglishCounter(IntPtr query, string counterPath, IntPtr userData, out IntPtr counter);

        [DllImport("pdh.dll")]
        private static extern uint PdhCollectQueryData(IntPtr query);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern uint PdhGetFormattedCounterArray(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr itemBuffer);

        [DllImport("pdh.dll")]
        private static extern uint PdhCloseQuery(IntPtr query);

        private const uint PdhFmtDouble = 0x00000200;
        private const uint PdhFmtNoCap100 = 0x00008000;
        private const uint PdhMoreData = 0x800007D2;

        private IntPtr _query;
        private IntPtr _counter;

        // Son örnekteki örnek (instance) verileri: süreç kimliği, motor anahtarı, değer
        private readonly List<int> _lastPids = new List<int>();
        private readonly List<string> _lastKeys = new List<string>();
        private readonly List<double> _lastValues = new List<double>();

        public bool IsAvailable { get; private set; }

        public GpuMonitor()
        {
            try
            {
                if (PdhOpenQuery(null, IntPtr.Zero, out _query) != 0) return;
                if (PdhAddEnglishCounter(_query, @"\GPU Engine(*)\Utilization Percentage", IntPtr.Zero, out _counter) != 0) return;
                PdhCollectQueryData(_query);
                IsAvailable = true;
            }
            catch
            {
                IsAvailable = false;
            }
        }

        /// <summary>Anlık ekran kartı kullanımı (0–100). Okunamazsa -1.</summary>
        public double Sample()
        {
            _lastPids.Clear();
            _lastKeys.Clear();
            _lastValues.Clear();
            if (!IsAvailable) return -1;
            IntPtr buffer = IntPtr.Zero;
            try
            {
                if (PdhCollectQueryData(_query) != 0) return -1;

                uint size = 0, count;
                uint status = PdhGetFormattedCounterArray(_counter, PdhFmtDouble | PdhFmtNoCap100, ref size, out count, IntPtr.Zero);
                if (status != PdhMoreData) return status == 0 ? 0 : -1;

                buffer = Marshal.AllocHGlobal((int)size);
                status = PdhGetFormattedCounterArray(_counter, PdhFmtDouble | PdhFmtNoCap100, ref size, out count, buffer);
                if (status != 0) return -1;

                // PDH_FMT_COUNTERVALUE_ITEM_W: [ad işaretçisi][CStatus (4) + dolgu (4)][double]
                int itemSize = IntPtr.Size + 16;
                var perEngine = new Dictionary<string, double>();
                for (int i = 0; i < count; i++)
                {
                    int offset = i * itemSize;
                    string name = Marshal.PtrToStringUni(Marshal.ReadIntPtr(buffer, offset)) ?? "";
                    int cstatus = Marshal.ReadInt32(buffer, offset + IntPtr.Size);
                    if (cstatus != 0 && cstatus != 1) continue;
                    double value = BitConverter.Int64BitsToDouble(Marshal.ReadInt64(buffer, offset + IntPtr.Size + 8));

                    // "pid_1234_luid_..._phys_0_eng_3_engtype_3D" -> motor anahtarı "luid_..._engtype_3D"
                    int luid = name.IndexOf("luid", StringComparison.OrdinalIgnoreCase);
                    string key = luid >= 0 ? name.Substring(luid) : name;
                    double sum;
                    perEngine.TryGetValue(key, out sum);
                    perEngine[key] = sum + value;

                    // Süreç bazlı sorgular için örneği sakla
                    _lastPids.Add(ParsePid(name));
                    _lastKeys.Add(key);
                    _lastValues.Add(value);
                }

                double max = 0;
                foreach (double v in perEngine.Values) if (v > max) max = v;
                return Math.Min(100, max);
            }
            catch
            {
                return -1;
            }
            finally
            {
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>
        /// Son örnekte, verilen süreçlere ait motor kullanımı (0–100).
        /// Her motorun değerleri yalnızca bu süreçler için toplanır, en yüksek motor alınır. Veri yoksa -1.
        /// </summary>
        public double UsageForProcesses(ICollection<int> processIds)
        {
            if (!IsAvailable || processIds == null || _lastKeys.Count == 0) return -1;
            var perEngine = new Dictionary<string, double>();
            for (int i = 0; i < _lastKeys.Count; i++)
            {
                int pid = _lastPids[i];
                if (pid < 0 || !processIds.Contains(pid)) continue;
                double sum;
                perEngine.TryGetValue(_lastKeys[i], out sum);
                perEngine[_lastKeys[i]] = sum + _lastValues[i];
            }

            double max = 0;
            foreach (double v in perEngine.Values) if (v > max) max = v;
            return Math.Max(0, Math.Min(100, max));
        }

        /// <summary>"pid_1234_..." önekinden süreç kimliğini çıkarır; bulunamazsa -1.</summary>
        private static int ParsePid(string name)
        {
            if (string.IsNullOrEmpty(name) || !name.StartsWith("pid_", StringComparison.OrdinalIgnoreCase)) return -1;
            int start = 4;
            int end = name.IndexOf('_', start);
            if (end < 0) end = name.Length;
            int pid;
            if (int.TryParse(name.Substring(start, end - start), out pid)) return pid;
            return -1;
        }

        public void Dispose()
        {
            if (_query != IntPtr.Zero)
            {
                PdhCloseQuery(_query);
                _query = IntPtr.Zero;
            }
            IsAvailable = false;
        }
    }
}
