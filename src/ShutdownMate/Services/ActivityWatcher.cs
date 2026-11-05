using System;
using System.Collections.Generic;
using System.Diagnostics;
using ShutdownMate.Helpers;
using ShutdownMate.Models;

namespace ShutdownMate.Services
{
    /// <summary>
    /// "İşler bitince" tetikleyicisi. Seçilen tüm koşullar (ağ, işlemci, ekran kartı, program)
    /// aynı anda belirlenen süre boyunca sakin kalınca tamamlanmış sayılır.
    /// Ağ, işlemci ve ekran kartı koşulları isteğe bağlı olarak tek bir programa göre ölçülebilir.
    /// </summary>
    internal sealed class ActivityWatcher : IDisposable
    {
        public bool UseNetwork;
        public NetworkDirection Direction;
        public double NetworkThresholdBps;
        public bool UseCpu;
        public int CpuThreshold;
        public bool UseGpu;
        public int GpuThreshold;
        public bool UseTransfer;
        public double TransferThresholdBps;
        /// <summary>Aktarım kaynağı: "" otomatik, "drive:D:" tek sürücü.</summary>
        public string TransferSource = "";
        public Process WatchedProcess;
        public string ProcessName;
        public TimeSpan CalmDuration;
        public bool WaitForStart;

        // Koşul başına isteğe bağlı program izleyicileri (null = tüm sistem)
        private ProcessMonitor _networkProgram, _cpuProgram, _gpuProgram;
        private readonly Dictionary<string, ProcessMonitor> _programs =
            new Dictionary<string, ProcessMonitor>(StringComparer.OrdinalIgnoreCase);

        public bool UseProcess { get { return WatchedProcess != null; } }

        /// <summary>İşlerden en az biri bir kez etkin görüldü (ya da beklenmiyor).</summary>
        public bool Armed { get; private set; }

        public bool NetworkActive { get; private set; }
        public bool CpuActive { get; private set; }
        public bool GpuActive { get; private set; }
        public bool ProcessRunning { get; private set; }
        public bool TransferActive { get; private set; }

        /// <summary>Son değerlendirmedeki ölçümler (program seçildiyse o programa ait).</summary>
        public double NetworkValue { get; private set; } = -1;
        public double CpuValue { get; private set; } = -1;
        public double GpuValue { get; private set; } = -1;
        public double TransferValue { get; private set; } = -1;

        public string NetworkProgramName { get { return _networkProgram != null ? _networkProgram.ProcessName : null; } }
        public string CpuProgramName { get { return _cpuProgram != null ? _cpuProgram.ProcessName : null; } }
        public string GpuProgramName { get { return _gpuProgram != null ? _gpuProgram.ProcessName : null; } }

        public bool AnyActive { get { return NetworkActive || CpuActive || GpuActive || TransferActive || ProcessRunning; } }

        /// <summary>Yeniden başlayabilen bir iş (ağ, işlemci, ekran kartı) etkin mi.</summary>
        public bool ResumableActive { get { return NetworkActive || CpuActive || GpuActive || TransferActive; } }

        private readonly TransferTracker _transfer = new TransferTracker();
        private long _transferSampleId = -1;

        private DateTime? _calmSince;

        /// <summary>Koşulu tek bir programa bağlar. Boş ad: tüm sistem.</summary>
        public void SetNetworkProgram(string processName) { _networkProgram = GetProgram(processName); }
        public void SetCpuProgram(string processName) { _cpuProgram = GetProgram(processName); }
        public void SetGpuProgram(string processName) { _gpuProgram = GetProgram(processName); }

        /// <summary>Aktarım kaynağını ayarlar: "drive:D:" tek sürücü, boş = otomatik (aktarım yapan sürücüler).</summary>
        public void SetTransferSource(string key)
        {
            TransferSource = key ?? "";
            _transfer.SetDrive(TransferMonitor.DriveOf(TransferSource));
        }

        private ProcessMonitor GetProgram(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return null;
            ProcessMonitor monitor;
            if (!_programs.TryGetValue(processName, out monitor))
            {
                monitor = new ProcessMonitor(processName);
                _programs[processName] = monitor;
            }
            return monitor;
        }

        /// <summary>Program izleyicilerini örnekler; SystemMetrics.Sample() çağrısından hemen sonra, saniyede bir.</summary>
        public void SamplePrograms(SystemMetrics metrics)
        {
            foreach (var monitor in _programs.Values) monitor.Sample(metrics.GpuMonitor);
        }

        public void Evaluate(DateTime now, SystemMetrics metrics)
        {
            NetworkValue = _networkProgram != null ? _networkProgram.IoBytesPerSecond : metrics.NetworkRate(Direction);
            CpuValue = _cpuProgram != null ? _cpuProgram.CpuPercent : metrics.Cpu;
            GpuValue = _gpuProgram != null ? _gpuProgram.GpuPercent : metrics.Gpu;

            if (UseTransfer && metrics.Transfer.SampleId != _transferSampleId)
            {
                _transferSampleId = metrics.Transfer.SampleId;
                _transfer.Threshold = TransferThresholdBps;
                _transfer.Add(metrics.Transfer.Rates());
            }
            TransferValue = _transfer.Instant;
            TransferActive = UseTransfer && _transfer.Active;

            NetworkActive = UseNetwork && NetworkValue >= 0 && NetworkValue >= NetworkThresholdBps;
            CpuActive = UseCpu && CpuValue >= 0 && CpuValue >= CpuThreshold;
            GpuActive = UseGpu && GpuValue >= 0 && GpuValue >= GpuThreshold;

            bool exited;
            try { exited = WatchedProcess == null || WatchedProcess.HasExited; }
            catch { exited = true; }
            ProcessRunning = UseProcess && !exited;

            if (!WaitForStart || AnyActive) Armed = true;
            if (!Armed) { _calmSince = null; return; }
            _calmSince = AnyActive ? (DateTime?)null : (_calmSince ?? now);
        }

        public TimeSpan Calm(DateTime now)
        {
            return _calmSince.HasValue ? now - _calmSince.Value : TimeSpan.Zero;
        }

        public bool IsComplete(DateTime now)
        {
            return Armed && Calm(now) >= CalmDuration;
        }

        public void ResetCalm()
        {
            _calmSince = null;
        }

        public string NetworkLabel(SystemMetrics metrics)
        {
            if (_networkProgram != null)
                return _networkProgram.ProcessName + Loc.T("ActivityWatcher_Aktarim") + TextFormat.Speed(NetworkValue);
            return "↓ " + TextFormat.Speed(metrics.DownloadBps) + "  ↑ " + TextFormat.Speed(metrics.UploadBps);
        }

        public string TransferLabel()
        {
            if (_transfer.Tracked.Count == 0) return Loc.T("ActivityWatcher_AktarimBekleniyor");
            return Loc.T("ActivityWatcher_Aktarim2") + string.Join(", ", _transfer.Tracked) + ") " + TextFormat.Speed(TransferValue);
        }

        public string CpuLabel()
        {
            return (_cpuProgram != null ? _cpuProgram.ProcessName + Loc.T("ActivityWatcher_Islemci") : Loc.T("ActivityWatcher_Islemci2")) + TextFormat.Percent(CpuValue);
        }

        public string GpuLabel()
        {
            return (_gpuProgram != null ? _gpuProgram.ProcessName + Loc.T("ActivityWatcher_EkranKarti") : Loc.T("ActivityWatcher_EkranKarti2")) + TextFormat.Percent(GpuValue);
        }

        /// <summary>Durum satırı: "Ağ ↓ 2,4 MB/sn ↑ 80 KB/sn (sürüyor) · İşlemci %63 (sakin)".</summary>
        public string Describe(SystemMetrics metrics)
        {
            var parts = new List<string>();
            if (UseNetwork) parts.Add(NetworkLabel(metrics) + State(NetworkActive));
            if (UseCpu) parts.Add(CpuLabel() + State(CpuActive));
            if (UseGpu) parts.Add(GpuLabel() + State(GpuActive));
            if (UseTransfer) parts.Add(TransferLabel() + State(TransferActive));
            if (UseProcess) parts.Add(ProcessName + (ProcessRunning ? Loc.T("ActivityWatcher_Acik") : Loc.T("ActivityWatcher_Kapandi")));
            return string.Join("  ·  ", parts);
        }

        private static string State(bool active)
        {
            return active ? Loc.T("ActivityWatcher_Suruyor") : Loc.T("ActivityWatcher_Sakin");
        }

        public void Dispose()
        {
            if (WatchedProcess != null)
            {
                WatchedProcess.Dispose();
                WatchedProcess = null;
            }
            foreach (var monitor in _programs.Values) monitor.Dispose();
            _programs.Clear();
        }
    }
}
