using System.Windows;
using System.Windows.Input;
using ShutdownMate.Helpers;
using ShutdownMate.Services;
using WinControls = System.Windows.Controls;
using ControlAppearance = Wpf.Ui.Controls.ControlAppearance;

namespace ShutdownMate.Views
{
    public partial class MainWindow
    {
        // =====================================================================
        // Ekran geçişleri
        // =====================================================================

        private void ShowView(ViewKind view)
        {
            _view = view;
            Motion.SetVisible(SetupView, view == ViewKind.Setup);
            Motion.SetVisible(CountdownView, view == ViewKind.Countdown);
            Motion.SetVisible(PreferencesView, view == ViewKind.Preferences);

            bool running = _phase != RunPhase.Idle;
            RunningBanner.Visibility = running && view == ViewKind.Setup ? Visibility.Visible : Visibility.Collapsed;
            IdleLockBar.Visibility = running || IsHomeHidden("home:lockbar") ? Visibility.Collapsed : Visibility.Visible;
            StartButton.Content = running ? Loc.T("MainWindowRun_YenidenKur") : Loc.T("MainWindowRun_Baslat");

            // Çalışırken iptal öne çıkar; yeniden kur küçük ve ikincil kalır
            StartButton.Appearance = running ? ControlAppearance.Secondary : ControlAppearance.Primary;
            StartButton.Height = running ? 36 : 48;
            StartButton.MinWidth = running ? 120 : 150;
            StartButton.FontSize = running ? 14 : 16;
            StartBar.Padding = running ? new Thickness(24, 10, 24, 10) : new Thickness(24, 14, 24, 14);
            PreviewTitle.FontSize = running ? 14 : 18;
            PreviewSubtitle.FontSize = running ? 12 : 14;
            if (view == ViewKind.Countdown) CancelButton.Focus();
            UpdatePreview();
        }

        private void BackToSetup_Click(object sender, RoutedEventArgs e)
        {
            ShowView(ViewKind.Setup);
        }

        private void ShowCountdown_Click(object sender, RoutedEventArgs e)
        {
            if (_phase != RunPhase.Idle) ShowView(ViewKind.Countdown);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (_view == ViewKind.Countdown) { ShowView(ViewKind.Setup); e.Handled = true; }
                else if (_view == ViewKind.Preferences) { ClosePreferences(); e.Handled = true; }
            }
            else if (e.Key == Key.F11 && _phase != RunPhase.Idle)
            {
                EnterFullscreen();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter || (e.Key == Key.Space && e.IsRepeat))
            {
                // Enter hiçbir düğmeyi tetiklemez; basılı tutulan tuş zamanlayıcıyı art arda başlatıp durduramaz.
                // Sayı kutularında Enter, yazılan değeri onaylamaya devam eder.
                if (!(e.OriginalSource is WinControls.Primitives.TextBoxBase)) e.Handled = true;
            }
        }

        public void BringToFront()
        {
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Show();
            Activate();
            Topmost = true;
            if (!_warned) Topmost = _settings.AlwaysOnTop;
            Focus();
        }
    }
}
