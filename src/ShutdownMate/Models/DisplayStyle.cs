namespace ShutdownMate.Models
{
    /// <summary>Geri sayım göstergesinin görünümü.</summary>
    public enum DisplayStyle
    {
        /// <summary>İnce halka ve hafif yazı.</summary>
        Modern,

        /// <summary>Analog kadran ve yelkovan.</summary>
        Classic,

        /// <summary>Yedi segmentli LED ekran.</summary>
        Digital,

        /// <summary>Yalnızca büyük rakamlar ve ince çizgi.</summary>
        Minimal
    }
}
