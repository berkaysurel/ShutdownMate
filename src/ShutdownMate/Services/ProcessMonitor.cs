using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ShutdownMate.Services
{
    /// <summary>
    /// Bir programı süreç adına göre izler. Aynı adı taşıyan tüm süreçler (ör. tüm "chrome" süreçleri)
    /// toplanarak işlemci, veri aktarımı (disk + ağ) ve ekran kartı kullanımı hesaplanır.
    /// </summary>
    internal sealed class ProcessMonitor : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessIoCounters(IntPtr hProcess, out IO_COUNTERS counters);

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        private readonly Stopwatch _stopwatch = new Stopwatch();
        private double _lastSeconds = -1;

        // Süreç kimliğine göre bir önceki örnekteki değerler
        private Dictionary<int, TimeSpan> _lastCpu = new Dictionary<int, TimeSpan>();
        private Dictionary<int, ulong> _lastIo = new Dictionary<int, ulong>();

        private bool _disposed;

        public ProcessMonitor(string processName)
        {
            string name = (processName ?? "").Trim();
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 4);
            ProcessName = name;
            CpuPercent = -1;
            IoBytesPerSecond = -1;
            GpuPercent = -1;
        }

        /// <summary>İzlenen süreç adı (.exe olmadan).</summary>
        public string ProcessName { get; }

        /// <summary>Programın toplam işlemci kullanımı (0–100), ilk örnekte -1.</summary>
        public double CpuPercent { get; private set; }

        /// <summary>Programın saniyedeki veri aktarımı (okuma + yazma + diğer, bayt/sn), ilk örnekte -1.</summary>
        public double IoBytesPerSecond { get; private set; }

        /// <summary>Programın ekran kartı kullanımı (0–100), ölçülemezse -1.</summary>
        public double GpuPercent { get; private set; }

        /// <summary>Bu adla en az bir süreç çalışıyor mu.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Saniyede bir çağrılır. GPU değeri için aynı turda örneklenmiş GpuMonitor verilmelidir.</summary>
        public void Sample(GpuMonitor gpu)
        {
            if (_disposed) return;

            if (!_stopwatch.IsRunning) _stopwatch.Start();
            double nowSeconds = _stopwatch.Elapsed.TotalSeconds;
            double elapsed = _lastSeconds >= 0 ? nowSeconds - _lastSeconds : 0;

            Process[] processes;
            try
            {
                processes = string.IsNullOrEmpty(ProcessName)
                    ? new Process[0]
                    : Process.GetProcessesByName(ProcessName);
            }
            catch
            {
                processes = new Process[0];
            }

            var newCpu = new Dictionary<int, TimeSpan>();
            var newIo = new Dictionary<int, ulong>();
            var pids = new HashSet<int>();
            double cpuDeltaSeconds = 0;
            double ioDeltaBytes = 0;

            try
            {
                foreach (Process p in processes)
                {
                    int pid;
                    try
                    {
                        pid = p.Id;
                    }
                    catch
                    {
                        continue;
                    }
                    pids.Add(pid);

                    // İşlemci süresi (erişim reddedilirse atla)
                    try
                    {
                        TimeSpan total = p.TotalProcessorTime;
                        newCpu[pid] = total;
                        TimeSpan previous;
                        if (_lastCpu.TryGetValue(pid, out previous))
                        {
                            double d = (total - previous).TotalSeconds;
                            if (d > 0) cpuDeltaSeconds += d;
                        }
                    }
                    catch
                    {
                    }

                    // G/Ç sayaçları (erişim reddedilirse atla)
                    try
                    {
                        IO_COUNTERS counters;
                        if (GetProcessIoCounters(p.Handle, out counters))
                        {
                            ulong bytes = counters.ReadTransferCount + counters.WriteTransferCount + counters.OtherTransferCount;
                            newIo[pid] = bytes;
                            ulong previous;
                            if (_lastIo.TryGetValue(pid, out previous) && bytes >= previous)
                                ioDeltaBytes += bytes - previous;
                        }
                    }
                    catch
                    {
                    }
                }
            }
            finally
            {
                foreach (Process p in processes)
                {
                    try { p.Dispose(); } catch { }
                }
            }

            IsRunning = pids.Count > 0;

            if (_lastSeconds >= 0 && elapsed > 0)
            {
                int cores = Math.Max(1, Environment.ProcessorCount);
                double cpu = 100.0 * cpuDeltaSeconds / (elapsed * cores);
                CpuPercent = Math.Max(0, Math.Min(100, cpu));
                IoBytesPerSecond = Math.Max(0, ioDeltaBytes / elapsed);
            }
            else
            {
                CpuPercent = -1;
                IoBytesPerSecond = -1;
            }

            // Kapanan süreçler yeni sözlüklere eklenmediği için kendiliğinden düşer
            _lastCpu = newCpu;
            _lastIo = newIo;
            _lastSeconds = nowSeconds;

            if (gpu != null && gpu.IsAvailable)
                GpuPercent = gpu.UsageForProcesses(pids);
            else
                GpuPercent = -1;
        }

        public void Dispose()
        {
            // Process nesneleri her örnekten sonra bırakılır; burada yalnızca durum temizlenir.
            _disposed = true;
            _stopwatch.Stop();
            _lastCpu.Clear();
            _lastIo.Clear();
        }
    }
}
