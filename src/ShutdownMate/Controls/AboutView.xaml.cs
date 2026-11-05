using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ShutdownMate.Services;

namespace ShutdownMate.Controls
{
    /// <summary>Ayarlar > Hakkında sekmesi: sürüm, güncelleme bağlantıları, gizlilik, kısayollar ve lisanslar.</summary>
    public partial class AboutView : UserControl
    {
        private const string ReleasesUrl = "https://github.com/berkaysurel/shutdownmate/releases";
        private const string ProjectUrl = "https://github.com/berkaysurel/shutdownmate";

        /// <summary>"Varsayılanlara dön" tıklandı; onay ve sıfırlama ana pencerede yapılır.</summary>
        public event EventHandler ResetRequested;

        public AboutView()
        {
            InitializeComponent();
            // Microsoft Store sürümü güncellemeleri Store üzerinden alır; GitHub bağlantısı gösterilmez
            if (ShutdownMate.Helpers.AppPackage.IsPackaged) UpdateButton.Visibility = Visibility.Collapsed;
            try
            {
                BuildText.Text = Loc.T("About_Build") + File.GetLastWriteTime(Environment.ProcessPath).ToString("d MMMM yyyy", Loc.Culture);
            }
            catch
            {
                BuildText.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>Örnek: "ShutdownMate 2.6.0".</summary>
        public string Version
        {
            get { return VersionText.Text; }
            set { VersionText.Text = value; }
        }

        private void Releases_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl(ReleasesUrl);
        }

        private void Project_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl(ProjectUrl);
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            SettingsService.OpenFolder();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (ResetRequested != null) ResetRequested(this, EventArgs.Empty);
        }

        // Varsayılan tarayıcıda açılır; adres sabittir, dışarıdan gelmez
        private static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { }
        }
    }
}
