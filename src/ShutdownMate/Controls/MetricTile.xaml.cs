using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;
using UserControl = System.Windows.Controls.UserControl;

namespace ShutdownMate.Controls
{
    /// <summary>
    /// Tam ekranda bir sistem verisini (işlemci, ekran kartı, indirme, yükleme) gösteren kart:
    /// ikon, etiket, anlık değer ve son bir dakikanın eğrisi.
    /// </summary>
    public partial class MetricTile : UserControl
    {
        // 59 adım görünür; sol uçtaki fazladan nokta kayma sırasında boşluk kalmasın diye tutulur
        private const int VisibleSteps = 59;
        private const int HistoryLength = 61;
        private const int SampleIntervalMs = 1000;

        private readonly List<double> _history = new List<double>();

        public MetricTile()
        {
            InitializeComponent();
        }

        public SymbolRegular Symbol
        {
            get { return TileIcon.Symbol; }
            set { TileIcon.Symbol = value; }
        }

        public string Label
        {
            get { return TileLabel.Text; }
            set { TileLabel.Text = value; }
        }

        /// <summary>Kapalıysa eğri çizilmez; kartta yalnızca ikon, etiket ve sayı kalır.</summary>
        public bool ShowGraph
        {
            get { return SparkHost.Visibility == Visibility.Visible; }
            set { SparkHost.Visibility = value ? Visibility.Visible : Visibility.Collapsed; }
        }

        /// <summary>Eğrinin üst sınırı (ör. yüzde için 100). 0 ise görünen en yüksek değere göre ölçeklenir.</summary>
        public double FixedMaximum { get; set; }

        /// <summary>Yeni ölçümü ekler. Değer negatifse ölçüm yok sayılır, yalnızca metin güncellenir.</summary>
        public void Push(double value, string text)
        {
            TileValue.Text = text;
            if (value < 0) return;

            _history.Add(value);
            if (_history.Count > HistoryLength) _history.RemoveAt(0);
            DrawSparkline();
            Scroll();
        }

        /// <summary>Yeni nokta sağ kenarın dışında başlar ve bir adım sola süzülür: eğri kesintisiz akar.</summary>
        private void Scroll()
        {
            double width = SparkHost.ActualWidth;
            if (width <= 0 || _history.Count < 2) return;
            var slide = new DoubleAnimation(width / VisibleSteps, 0, TimeSpan.FromMilliseconds(SampleIntervalMs));
            slide.Freeze();
            SparkShift.BeginAnimation(TranslateTransform.XProperty, slide);
        }

        private void SparkHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawSparkline();
        }

        private void DrawSparkline()
        {
            double width = SparkHost.ActualWidth;
            double height = SparkHost.ActualHeight;
            if (width <= 0 || height <= 0 || _history.Count < 2) return;

            double maximum = FixedMaximum;
            if (maximum <= 0)
            {
                foreach (double value in _history) maximum = Math.Max(maximum, value);
                if (maximum <= 0) maximum = 1;
            }

            // Eğri sağa yaslıdır; yeni ölçümler sağdan girer
            double step = width / VisibleSteps;
            double left = width - step * (_history.Count - 1);
            var points = new List<Point>(_history.Count);
            for (int i = 0; i < _history.Count; i++)
            {
                double y = height - 2 - (height - 4) * Math.Min(1, _history[i] / maximum);
                points.Add(new Point(left + step * i, y));
            }

            var line = new StreamGeometry();
            using (StreamGeometryContext context = line.Open())
            {
                context.BeginFigure(points[0], false, false);
                context.PolyLineTo(points.GetRange(1, points.Count - 1), true, true);
            }
            line.Freeze();
            SparkLine.Data = line;

            var area = new StreamGeometry();
            using (StreamGeometryContext context = area.Open())
            {
                context.BeginFigure(new Point(left, height), true, true);
                context.PolyLineTo(points, true, true);
                context.LineTo(new Point(width, height), true, true);
            }
            area.Freeze();
            SparkFill.Data = area;
        }
    }
}
