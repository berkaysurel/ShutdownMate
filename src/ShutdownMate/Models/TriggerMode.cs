namespace ShutdownMate.Models
{
    /// <summary>İşlemin ne zaman tetikleneceği.</summary>
    public enum TriggerMode
    {
        /// <summary>Belirli bir süre sonra.</summary>
        Duration,

        /// <summary>Belirli bir saatte.</summary>
        Clock,

        /// <summary>Seçilen işler (ağ, işlemci, ekran kartı, program) bitince.</summary>
        Activity
    }
}
