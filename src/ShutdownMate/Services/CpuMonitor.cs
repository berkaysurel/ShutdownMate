using System;
using System.Runtime.InteropServices;

namespace ShutdownMate.Services
{
    /// <summary>Toplam işlemci kullanımını iki örnek arasındaki farktan hesaplar.</summary>
    internal sealed class CpuMonitor
    {
        [DllImport("kernel32.dll")]
        private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

        private long _lastIdle, _lastTotal;

        /// <summary>Son ölçümden bu yana kullanım yüzdesi (0–100). İlk çağrıda -1.</summary>
        public double Sample()
        {
            long idle, kernel, user;
            if (!GetSystemTimes(out idle, out kernel, out user)) return -1;
            long total = kernel + user; // kernel süresi boşta süresini de içerir
            double result = -1;
            if (_lastTotal != 0)
            {
                long dTotal = total - _lastTotal, dIdle = idle - _lastIdle;
                if (dTotal > 0) result = Math.Max(0, Math.Min(100, 100.0 * (dTotal - dIdle) / dTotal));
            }
            _lastIdle = idle;
            _lastTotal = total;
            return result;
        }
    }
}
