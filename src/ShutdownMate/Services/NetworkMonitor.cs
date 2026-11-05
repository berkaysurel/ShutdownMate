using System.Diagnostics;
using System.Net.NetworkInformation;
using ShutdownMate.Models;

namespace ShutdownMate.Services
{
    /// <summary>Tüm etkin ağ bağdaştırıcılarının toplam indirme/yükleme hızını ölçer.</summary>
    internal sealed class NetworkMonitor
    {
        private readonly Stopwatch _clock = new Stopwatch();
        private long _lastReceived = -1, _lastSent = -1;

        /// <summary>Son ölçülen indirme hızı (bayt/sn). Ölçüm yoksa -1.</summary>
        public double DownloadBps { get; private set; } = -1;

        /// <summary>Son ölçülen yükleme hızı (bayt/sn). Ölçüm yoksa -1.</summary>
        public double UploadBps { get; private set; } = -1;

        public bool HasSample { get { return DownloadBps >= 0; } }

        public double RateFor(NetworkDirection direction)
        {
            if (!HasSample) return -1;
            switch (direction)
            {
                case NetworkDirection.Download: return DownloadBps;
                case NetworkDirection.Upload: return UploadBps;
                default: return DownloadBps + UploadBps;
            }
        }

        public void Sample()
        {
            long received = 0, sent = 0;
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                    var stats = nic.GetIPStatistics();
                    received += stats.BytesReceived;
                    sent += stats.BytesSent;
                }
            }
            catch
            {
                return;
            }

            double seconds = _clock.Elapsed.TotalSeconds;
            _clock.Restart();

            // Bağdaştırıcı eklenip çıkarıldığında sayaçlar geriye gidebilir; o örneği atla.
            if (_lastReceived >= 0 && seconds > 0.2 && received >= _lastReceived && sent >= _lastSent)
            {
                DownloadBps = (received - _lastReceived) / seconds;
                UploadBps = (sent - _lastSent) / seconds;
            }
            _lastReceived = received;
            _lastSent = sent;
        }
    }
}
