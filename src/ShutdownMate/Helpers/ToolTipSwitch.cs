using System.Windows;
using System.Windows.Controls;

namespace ShutdownMate.Helpers
{
    /// <summary>
    /// Düğme açıklamalarını uygulama genelinde açıp kapatır. Kapalıyken yalnızca
    /// devre dışı denetimlerin açıklaması (neden kullanılamadığı) görünür.
    /// </summary>
    internal static class ToolTipSwitch
    {
        public static bool Enabled { get; set; } = true;

        static ToolTipSwitch()
        {
            EventManager.RegisterClassHandler(
                typeof(FrameworkElement),
                FrameworkElement.ToolTipOpeningEvent,
                new ToolTipEventHandler(OnToolTipOpening));
        }

        private static void OnToolTipOpening(object sender, ToolTipEventArgs e)
        {
            if (!Enabled && ((FrameworkElement)sender).IsEnabled)
            {
                e.Handled = true;
            }
        }
    }
}
