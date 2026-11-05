using ShutdownMate.Services;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ShutdownMate.Models;

namespace ShutdownMate.Controls
{
    /// <summary>Ekran kilidi ve tam ekranda görünecek mesajın yazıldığı ya da hazır listeden seçildiği kutu.</summary>
    public partial class LockMessageEditor : UserControl
    {
        private bool _updating;

        /// <summary>Kullanıcı metni değiştirdiğinde tetiklenir (kod ile atamada tetiklenmez).</summary>
        public event EventHandler MessageChanged;

        public LockMessageEditor()
        {
            InitializeComponent();
            UpdateRemaining();
        }

        /// <summary>Hazır mesaj menüsünü verilen listeyle kurar; liste boşsa düğme kapanır.</summary>
        public void SetPresets(IEnumerable<string> presets)
        {
            PresetsMenu.Items.Clear();
            foreach (string preset in presets)
            {
                var item = new MenuItem { Header = preset };
                item.Click += (s, e) => Box.Text = preset;
                PresetsMenu.Items.Add(item);
            }
            PresetsButton.IsEnabled = PresetsMenu.Items.Count > 0;
        }

        public string Message
        {
            get { return AppSettings.CleanLockMessage(Box.Text); }
            set
            {
                if (Message == value) return; // yazarken imleç yerinde kalsın
                _updating = true;
                Box.Text = value ?? "";
                _updating = false;
            }
        }

        private void Box_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateRemaining();
            if (_updating) return;
            var handler = MessageChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void PresetsButton_Click(object sender, RoutedEventArgs e)
        {
            PresetsMenu.PlacementTarget = PresetsButton;
            PresetsMenu.Placement = PlacementMode.Bottom;
            PresetsMenu.IsOpen = true;
        }

        private void UpdateRemaining()
        {
            int left = AppSettings.MaxLockMessageLength - Box.Text.Length;
            RemainingText.Text = left + Loc.T(left == 1 ? "LockMessageEditor_KarakterKaldi1" : "LockMessageEditor_KarakterKaldi");
        }
    }
}
