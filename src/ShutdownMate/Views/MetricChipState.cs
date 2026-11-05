using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace ShutdownMate.Views
{
    /// <summary>Tam ekranın alt satırındaki ölçüm çipinin durumu (ağ, CPU, GPU, işlem ...).</summary>
    public sealed class MetricChipState
    {
        /// <summary>Çip metni, örn. "Ağ 1,2 MB/sn".</summary>
        public string Label;

        /// <summary>Ölçüm şu an etkin mi (eşik üstünde); etkin çip vurgu rengiyle çizilir.</summary>
        public bool Active;

        /// <summary>Çip simgesi.</summary>
        public SymbolRegular Icon;
    }
}
