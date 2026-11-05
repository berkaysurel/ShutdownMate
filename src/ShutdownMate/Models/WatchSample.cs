using System;

namespace ShutdownMate.Models
{
    /// <summary>Kullanım grafiği için dakikalık ortalama. Program seçilmediyse tüm sisteme aittir; ölçülemeyen değer -1.</summary>
    public sealed class WatchSample
    {
        public DateTime Time { get; set; }
        public double Cpu { get; set; }
        public double Gpu { get; set; }

        /// <summary>Programın disk + ağ aktarımı (bayt/sn); tüm sistemde ağ indirme + yükleme.</summary>
        public double Io { get; set; }
    }
}
