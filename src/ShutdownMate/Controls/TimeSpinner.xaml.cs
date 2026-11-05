using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ShutdownMate.Controls
{
    /// <summary>
    /// Büyük rakamlı sayı seçici (Apple / Windows 11 tarzı). Saat ve dakika için
    /// yan yana çift olarak kullanılır. Ok düğmeleri basılı tutulunca tekrarlar;
    /// klavye (yukarı/aşağı, PageUp/PageDown) ve fare tekerleği ile de değiştirilir.
    /// </summary>
    public partial class TimeSpinner : UserControl
    {
        // ---------------- Bağımlılık özellikleri ----------------

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value),
            typeof(int),
            typeof(TimeSpinner),
            new FrameworkPropertyMetadata(
                0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValuePropertyChanged,
                CoerceValueProperty));

        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
            nameof(Minimum),
            typeof(int),
            typeof(TimeSpinner),
            new PropertyMetadata(0, OnRangePropertyChanged));

        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
            nameof(Maximum),
            typeof(int),
            typeof(TimeSpinner),
            new PropertyMetadata(59, OnRangePropertyChanged));

        public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
            nameof(Step),
            typeof(int),
            typeof(TimeSpinner),
            new PropertyMetadata(1));

        public static readonly DependencyProperty LargeStepProperty = DependencyProperty.Register(
            nameof(LargeStep),
            typeof(int),
            typeof(TimeSpinner),
            new PropertyMetadata(10));

        public static readonly DependencyProperty WrapProperty = DependencyProperty.Register(
            nameof(Wrap),
            typeof(bool),
            typeof(TimeSpinner),
            new PropertyMetadata(false));

        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label),
            typeof(string),
            typeof(TimeSpinner),
            new PropertyMetadata(string.Empty, OnLabelPropertyChanged));

        /// <summary>Değer gerçekten değiştiğinde (arayüzden ya da koddan) tetiklenir.</summary>
        public event EventHandler ValueChanged;

        /// <summary>Değer zaten en üstteyken artırılmak istendiğinde tetiklenir (Wrap kapalıyken).</summary>
        public event EventHandler BeyondMaximum;

        /// <summary>Değer zaten en alttayken azaltılmak istendiğinde tetiklenir (Wrap kapalıyken).</summary>
        public event EventHandler BeyondMinimum;

        public TimeSpinner()
        {
            InitializeComponent();

            ValueBox.PreviewTextInput += OnValueBoxPreviewTextInput;
            ValueBox.PreviewKeyDown += OnValueBoxPreviewKeyDown;
            ValueBox.GotKeyboardFocus += OnValueBoxGotKeyboardFocus;
            ValueBox.LostKeyboardFocus += OnValueBoxLostKeyboardFocus;
            ValueBox.PreviewMouseLeftButtonDown += OnValueBoxPreviewMouseLeftButtonDown;
            DataObject.AddPastingHandler(ValueBox, OnValueBoxPasting);

            UpdateMaxLength();
            RefreshText();
            RefreshLabel();
        }

        public int Value
        {
            get { return (int)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public int Minimum
        {
            get { return (int)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }

        public int Maximum
        {
            get { return (int)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        public int Step
        {
            get { return (int)GetValue(StepProperty); }
            set { SetValue(StepProperty, value); }
        }

        public int LargeStep
        {
            get { return (int)GetValue(LargeStepProperty); }
            set { SetValue(LargeStepProperty, value); }
        }

        public bool Wrap
        {
            get { return (bool)GetValue(WrapProperty); }
            set { SetValue(WrapProperty, value); }
        }

        public string Label
        {
            get { return (string)GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }

        // ---------------- Özellik geri çağrıları ----------------

        private static object CoerceValueProperty(DependencyObject d, object baseValue)
        {
            TimeSpinner spinner = (TimeSpinner)d;
            return spinner.Clamp((int)baseValue);
        }

        private static void OnValuePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TimeSpinner spinner = (TimeSpinner)d;
            spinner.RefreshText();
            EventHandler handler = spinner.ValueChanged;
            if (handler != null)
            {
                handler(spinner, EventArgs.Empty);
            }
        }

        private static void OnRangePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TimeSpinner spinner = (TimeSpinner)d;
            spinner.UpdateMaxLength();
            spinner.CoerceValue(ValueProperty);
            spinner.RefreshText();
        }

        private static void OnLabelPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TimeSpinner)d).RefreshLabel();
        }

        // ---------------- Yardımcılar ----------------

        private int Clamp(int value)
        {
            int min = Minimum;
            int max = Math.Max(Minimum, Maximum);
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }

        private void RefreshText()
        {
            if (ValueBox == null)
            {
                return;
            }
            string text = Value.ToString("D2", CultureInfo.InvariantCulture);
            if (ValueBox.Text != text)
            {
                ValueBox.Text = text;
            }
            if (ValueBox.IsKeyboardFocusWithin)
            {
                ValueBox.SelectAll();
            }
        }

        private void RefreshLabel()
        {
            if (LabelText == null)
            {
                return;
            }
            string label = Label ?? string.Empty;
            LabelText.Text = label;
            LabelText.Visibility = label.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void UpdateMaxLength()
        {
            if (ValueBox == null)
            {
                return;
            }
            int max = Math.Abs(Math.Max(Minimum, Maximum));
            int digits = max.ToString(CultureInfo.InvariantCulture).Length;
            ValueBox.MaxLength = Math.Max(2, digits);
        }

        /// <summary>Değeri verilen miktar kadar değiştirir; Wrap açıksa uçlarda döner.</summary>
        private void ChangeBy(int delta)
        {
            if (delta == 0)
            {
                return;
            }

            // Yazılmış ama onaylanmamış bir değer varsa önce onu uygula.
            CommitText();

            int min = Minimum;
            int max = Math.Max(Minimum, Maximum);
            long next = (long)Value + delta;

            if (!Wrap && next > max && Value >= max)
            {
                BeyondMaximum?.Invoke(this, EventArgs.Empty);
                return;
            }
            if (!Wrap && next < min && Value <= min)
            {
                BeyondMinimum?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (next > max)
            {
                next = Wrap ? min : max;
            }
            else if (next < min)
            {
                next = Wrap ? max : min;
            }

            Value = (int)next;
            RefreshText();
        }

        /// <summary>Kutudaki metni değere çevirir; geçersizse eski değere döner.</summary>
        private void CommitText()
        {
            int parsed;
            string text = ValueBox.Text == null ? string.Empty : ValueBox.Text.Trim();
            if (text.Length > 0 && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out parsed))
            {
                Value = Clamp(parsed);
            }
            RefreshText();
        }

        private static bool IsAllDigits(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            foreach (char c in text)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }
            return true;
        }

        private void SetFocusVisual(bool focused)
        {
            if (focused)
            {
                RootBorder.SetResourceReference(Border.BorderBrushProperty, "AccentFillColorDefaultBrush");
                RootBorder.BorderThickness = new Thickness(2);
                RootBorder.Padding = new Thickness(5); // toplam boyut sabit kalsın
            }
            else
            {
                RootBorder.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
                RootBorder.BorderThickness = new Thickness(1);
                RootBorder.Padding = new Thickness(6);
            }
        }

        // ---------------- Olay işleyicileri ----------------

        private void OnUpClick(object sender, RoutedEventArgs e)
        {
            ChangeBy(Step);
        }

        private void OnDownClick(object sender, RoutedEventArgs e)
        {
            ChangeBy(-Step);
        }

        private void OnValueBoxPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Yalnızca rakam kabul et.
            e.Handled = !IsAllDigits(e.Text);
        }

        private void OnValueBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.UnicodeText))
            {
                string text = e.DataObject.GetData(DataFormats.UnicodeText) as string;
                if (IsAllDigits(text == null ? null : text.Trim()))
                {
                    return;
                }
            }
            e.CancelCommand();
        }

        private void OnValueBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up:
                    ChangeBy(Step);
                    e.Handled = true;
                    break;
                case Key.Down:
                    ChangeBy(-Step);
                    e.Handled = true;
                    break;
                case Key.PageUp:
                    ChangeBy(LargeStep);
                    e.Handled = true;
                    break;
                case Key.PageDown:
                    ChangeBy(-LargeStep);
                    e.Handled = true;
                    break;
                case Key.Enter:
                    CommitText();
                    ValueBox.SelectAll();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    // Yazılanı geri al.
                    RefreshText();
                    ValueBox.SelectAll();
                    e.Handled = true;
                    break;
            }
        }

        private void OnValueBoxGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ValueBox.SelectAll();
            SetFocusVisual(true);
        }

        private void OnValueBoxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            CommitText();
            SetFocusVisual(false);
        }

        private void OnValueBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // İlk tıklamada imleç konumlanıp seçimi bozmasın; odakla ve tümünü seç.
            if (!ValueBox.IsKeyboardFocusWithin)
            {
                ValueBox.Focus();
                e.Handled = true;
            }
        }

        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            base.OnPreviewMouseWheel(e);
            if (e.Handled || e.Delta == 0)
            {
                return;
            }
            ChangeBy(e.Delta > 0 ? Step : -Step);
            e.Handled = true;
        }
    }
}
