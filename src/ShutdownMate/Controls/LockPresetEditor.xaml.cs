using ShutdownMate.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ShutdownMate.Helpers;
using ShutdownMate.Models;
using ShutdownMate.Views;
using ControlAppearance = Wpf.Ui.Controls.ControlAppearance;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;
using WpfUiButton = Wpf.Ui.Controls.Button;
using WpfUiTextBox = Wpf.Ui.Controls.TextBox;

namespace ShutdownMate.Controls
{
    /// <summary>Ana ekrandaki hazır mesaj listesini düzenler: değiştirme, ekleme, çıkarma, varsayılana dönme.</summary>
    public partial class LockPresetEditor : UserControl
    {
        private bool _updating;

        /// <summary>Kullanıcı listeyi değiştirdiğinde tetiklenir (kod ile atamada tetiklenmez).</summary>
        public event EventHandler PresetsChanged;

        public LockPresetEditor()
        {
            InitializeComponent();
        }

        /// <summary>Temizlenmiş liste; yazılırken boş ya da tekrar eden satırlar buraya girmez.</summary>
        public List<string> Presets
        {
            get { return AppSettings.CleanLockMessagePresets(RowBoxes().Select(b => b.Text)); }
            set
            {
                _updating = true;
                Rows.Children.Clear();
                foreach (string preset in value ?? new List<string>()) AddRow(preset);
                _updating = false;
                UpdateAddButton();
                UpdateResetButton();
            }
        }

        private IEnumerable<WpfUiTextBox> RowBoxes()
        {
            return Rows.Children.Cast<Grid>().Where(row => row.Tag == null).Select(row => (WpfUiTextBox)row.Children[0]);
        }

        private WpfUiTextBox AddRow(string text)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var box = new WpfUiTextBox
            {
                Text = text,
                MaxLength = AppSettings.MaxLockMessageLength,
                TextWrapping = TextWrapping.Wrap,
                ClearButtonEnabled = false,
                PlaceholderText = Loc.T("LockPresetEditor_HazirMesaj")
            };
            box.TextChanged += (s, e) => RaiseChanged();
            box.LostFocus += (s, e) => DropIfUnused(row, box);
            row.Children.Add(box);

            var remove = new WpfUiButton
            {
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Appearance = ControlAppearance.Transparent,
                Icon = new SymbolIcon { Symbol = SymbolRegular.Delete24 },
                ToolTip = Loc.T("LockPresetEditor_BuMesajiListedenCikarir")
            };
            remove.Click += (s, e) => RemoveRow(row);
            Grid.SetColumn(remove, 1);
            row.Children.Add(remove);

            Rows.Children.Add(row);
            if (!_updating) Motion.Appear(row, 0, true);
            return box;
        }

        /// <summary>Boş bırakılan ya da üstteki bir satırla aynı olan satır, odaktan çıkınca kaldırılır.</summary>
        private void DropIfUnused(Grid row, WpfUiTextBox box)
        {
            string text = AppSettings.CleanLockMessage(box.Text);
            bool duplicate = RowBoxes().TakeWhile(b => b != box).Any(b => AppSettings.CleanLockMessage(b.Text) == text);
            if (text.Length == 0 || duplicate)
            {
                row.Tag = "removing";
                UpdateAddButton();
                Motion.Remove(row, () => Rows.Children.Remove(row));
            }
        }

        private void RemoveRow(Grid row)
        {
            row.Tag = "removing"; // sayım ve kayıt dışında kalsın, çıkış animasyonu sürerken bile
            UpdateAddButton();
            RaiseChanged();
            Motion.Remove(row, () => Rows.Children.Remove(row));
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            WpfUiTextBox box = AddRow("");
            UpdateAddButton();
            box.Focus();
        }

        /// <summary>Liste zaten varsayılan halindeyse düğme geçersizdir: yeniden yazılıp animasyon tekrarlanmaz.</summary>
        private bool IsDefault()
        {
            return Presets.SequenceEqual(AppSettings.CleanLockMessagePresets(AppSettings.GetDefaultLockMessagePresets()));
        }

        private void UpdateResetButton()
        {
            ResetButton.IsEnabled = !IsDefault();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsDefault()) return;
            if (!AppDialog.Ask(Window.GetWindow(this), Loc.T("LockPresetEditor_HazirMesajlarIlkHaline"),
                Loc.T("LockPresetEditor_EklediginizYaDaDegistirdig"),
                AppDialogKind.Warning, Loc.T("LockPresetEditor_VarsayilanlaraDon"))) return;
            Presets = AppSettings.GetDefaultLockMessagePresets();
            int order = 0;
            foreach (UIElement row in Rows.Children) Motion.Appear(row, 70 * order++, true);
            RaiseChanged();
        }

        private void UpdateAddButton()
        {
            AddButton.IsEnabled = RowBoxes().Count() < AppSettings.MaxLockMessagePresets;
        }

        private void RaiseChanged()
        {
            if (_updating) return;
            UpdateResetButton();
            var handler = PresetsChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
