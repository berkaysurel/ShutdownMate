using System;
using ShutdownMate.Models;

namespace ShutdownMate.Services
{
    /// <summary>
    /// İzlenen programın uzun süre boşta kalmasını fark eder: işlemci, ekran kartı ve veri aktarımı
    /// birlikte eşiğin altındaysa program boştadır. Bu, işin bittiğini ya da programın takıldığını
    /// (ör. bir onay penceresinde beklediğini) gösterir; hangisi olduğu ekran görüntüsünden anlaşılır.
    /// Program en az bir kez çalışırken görülmeden uyarı verilmez.
    /// </summary>
    internal sealed class IdleDetector
    {
        public const double CpuThreshold = 2;
        public const double GpuThreshold = 2;
        public const double IoThresholdBps = 64 * 1024;

        private readonly TimeSpan _idleAfter;
        private DateTime? _lastActive;
        private bool _reported;

        public IdleDetector(TimeSpan idleAfter)
        {
            _idleAfter = idleAfter;
        }

        /// <summary>Son çalışma anından bu yana geçen süre; program hiç çalışırken görülmediyse sıfır.</summary>
        public TimeSpan IdleFor { get; private set; }

        /// <summary>
        /// Saniyede bir çağrılır. Ölçülemeyen değer -1 verilir ve hesaba katılmaz.
        /// Boşta kalma başladıysa Idle, sonra yeniden çalıştıysa Resumed, aksi halde null döner.
        /// </summary>
        public WatchEventKind? Feed(DateTime now, bool running, double cpu, double gpu, double ioBps)
        {
            if (!running)
            {
                _lastActive = null;
                _reported = false;
                IdleFor = TimeSpan.Zero;
                return null;
            }

            bool active = cpu >= CpuThreshold || gpu >= GpuThreshold || ioBps >= IoThresholdBps;
            if (active)
            {
                _lastActive = now;
                IdleFor = TimeSpan.Zero;
                if (!_reported) return null;
                _reported = false;
                return WatchEventKind.Resumed;
            }

            if (!_lastActive.HasValue) return null;
            IdleFor = now - _lastActive.Value;
            if (_reported || IdleFor < _idleAfter) return null;
            _reported = true;
            return WatchEventKind.Idle;
        }
    }
}
