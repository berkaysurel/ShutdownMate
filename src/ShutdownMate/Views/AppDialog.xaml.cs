using ShutdownMate.Services;
using ShutdownMate.Helpers;
using System;
using System.Windows;
using Wpf.Ui.Controls;

namespace ShutdownMate.Views
{
    /// <summary>
    /// Uygulamanın temasına uyan uyarı penceresi; Windows'un standart MessageBox'ı yerine kullanılır.
    /// Onay gerektiren pencerelerde Enter ve Esc güvenli seçeneği (vazgeç) çalıştırır.
    /// </summary>
    public partial class AppDialog : FluentWindow
    {
        private bool _accepted;

        private AppDialog(Window owner, string title, string message, AppDialogKind kind, string primaryText, string secondaryText, string highlight, string note)
        {
            InitializeComponent();
            Loaded += (s, e) => { if (Content is UIElement root) Motion.Appear(root); };

            if (owner != null && owner.IsVisible) Owner = owner;
            else WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true; // Tam ekran göstergenin arkasında kalmasın
            Title = title;
            TitleText.Text = title;
            MessageText.Text = message ?? "";
            MessageText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
            PrimaryButton.Content = primaryText;

            if (!string.IsNullOrEmpty(highlight))
            {
                HighlightText.Text = highlight;
                HighlightBox.Visibility = Visibility.Visible;
            }
            if (!string.IsNullOrEmpty(note))
            {
                NoteText.Text = note;
                NoteText.Visibility = Visibility.Visible;
            }

            switch (kind)
            {
                case AppDialogKind.Danger:
                    KindIcon.Symbol = SymbolRegular.ShieldError24;
                    KindIcon.SetResourceReference(ForegroundProperty, "SystemFillColorCriticalBrush");
                    PrimaryButton.Appearance = ControlAppearance.Danger;
                    break;
                case AppDialogKind.Warning:
                    KindIcon.Symbol = SymbolRegular.Warning24;
                    KindIcon.SetResourceReference(ForegroundProperty, "SystemFillColorCautionBrush");
                    break;
                default:
                    KindIcon.Symbol = SymbolRegular.Info24;
                    KindIcon.SetResourceReference(ForegroundProperty, "AccentTextFillColorPrimaryBrush");
                    break;
            }

            if (secondaryText == null)
            {
                SecondaryButton.Visibility = Visibility.Collapsed;
                ButtonRow.Columns = 1;
                PrimaryButton.Margin = new Thickness(0);
                PrimaryButton.IsDefault = true;
                PrimaryButton.IsCancel = true;
                Loaded += (s, e) => PrimaryButton.Focus();
            }
            else
            {
                SecondaryButton.Content = secondaryText;
                SecondaryButton.IsDefault = true;
                SecondaryButton.IsCancel = true;
                Loaded += (s, e) => SecondaryButton.Focus();
            }

            FitHeightToContent();
        }

        /// <summary>
        /// SizeToContent, pencere çerçevesi kaldırılmış WPF pencerelerinde yüksekliği yanlış hesaplayabilir;
        /// bu yüzden yükseklik içerik ölçülerek açıkça verilir (ortalama da bu değere göre yapılır).
        /// </summary>
        private void FitHeightToContent()
        {
            DialogRoot.Measure(new Size(Width, double.PositiveInfinity));
            Height = Math.Ceiling(DialogRoot.DesiredSize.Height);
        }

        /// <summary>Bilgi verir; tek düğmesi vardır.</summary>
        public static void Inform(Window owner, string title, string message, AppDialogKind kind = AppDialogKind.Info)
        {
            new AppDialog(owner, title, message, kind, Loc.T("AppDialog_Tamam"), null, null, null).ShowDialog();
        }

        // Varsayılan ikinci düğme metni (Vazgeç) dile göre çalışma anında seçilir; null hâlâ "düğme yok" demektir
        private const string DefaultSecondary = "<varsayilan>";

        /// <summary>Onay ister; yalnızca birinci düğmeye basılırsa true döner.</summary>
        public static bool Ask(Window owner, string title, string message, AppDialogKind kind, string primaryText,
            string secondaryText = DefaultSecondary, string highlight = null, string note = null)
        {
            if (secondaryText == DefaultSecondary) secondaryText = Loc.T("AppDialog_Vazgec");
            var dialog = new AppDialog(owner, title, message, kind, primaryText, secondaryText, highlight, note);
            dialog.ShowDialog();
            return dialog._accepted;
        }

        private void PrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            _accepted = true;
            Close();
        }

        private void SecondaryButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
