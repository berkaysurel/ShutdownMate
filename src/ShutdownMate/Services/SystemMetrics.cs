using System;
using ShutdownMate.Models;

namespace ShutdownMate.Services
{
    /// <summary>İşlemci, ekran kartı ve ağ ölçümlerini tek yerden saniyede bir günceller.</summary>
    internal sealed class SystemMetrics : IDisposable
    {
        private readonly CpuMonitor _cpu = new CpuMonitor();
        private readonly GpuMonitor _gpu = new GpuMonitor();
        private readonly NetworkMonitor _network = new NetworkMonitor();

        /// <summary>Dosya aktarımı hızı (disk, sürücü ya da ağ bağdaştırıcısı).</summary>
        public TransferMonitor Transfer { get; } = new TransferMonitor();

        /// <summary>İşlemci kullanımı (0–100), ölçüm yoksa -1.</summary>
        public double Cpu { get; private set; } = -1;

        /// <summary>Ekran kartı kullanımı (0–100), ölçüm yoksa -1.</summary>
        public double Gpu { get; private set; } = -1;

        public bool GpuAvailable { get { return _gpu.IsAvailable; } }

        /// <summary>Süreç bazlı ekran kartı ölçümü için (ProcessMonitor.Sample) paylaşılan GPU izleyicisi.</summary>
        public GpuMonitor GpuMonitor { get { return _gpu; } }

        public double DownloadBps { get { return _network.DownloadBps; } }

        public double UploadBps { get { return _network.UploadBps; } }

        public double NetworkRate(NetworkDirection direction)
        {
            return _network.RateFor(direction);
        }

        public void Sample()
        {
            double cpu = _cpu.Sample();
            if (cpu >= 0) Cpu = cpu;
            double gpu = _gpu.Sample();
            if (gpu >= 0) Gpu = gpu;
            _network.Sample();
            Transfer.Sample();
        }

        public void Dispose()
        {
            _gpu.Dispose();
            Transfer.Dispose();
        }
    }
}
