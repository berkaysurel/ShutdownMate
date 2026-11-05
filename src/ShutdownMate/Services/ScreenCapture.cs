using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShutdownMate.Services
{
    /// <summary>Ekran görüntüsü alır: bulanık tam ekran arka planı ve gözetim kaydı için.</summary>
    internal static class ScreenCapture
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Rect { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public Rect Monitor;
            public Rect Work;
            public uint Flags;
        }

        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr dest, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool GetWindowPlacement(IntPtr hwnd, ref WindowPlacement placement);
        [DllImport("user32.dll")] private static extern bool SetWindowPlacement(IntPtr hwnd, ref WindowPlacement placement);

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowPlacement
        {
            public int Length;
            public int Flags;
            public int ShowCmd;
            public int MinX, MinY, MaxX, MaxY;
            public Rect Normal;
        }

        private const int SwShowNoActivate = 4;
        private const int SwShowMinNoActive = 7;
        private const int WpfRestoreToMaximized = 2;
        private const int RestorePaintMilliseconds = 500;
        [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hwnd, IntPtr dc, uint flags);
        [DllImport("user32.dll")] private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

        private const uint MonitorDefaultToNearest = 2;
        private const uint SrcCopy = 0x00CC0020;
        private const uint CaptureBlt = 0x40000000;
        private const uint PwRenderFullContent = 2;
        private const uint WdaExcludeFromCapture = 0x11;
        private const int SmXVirtualScreen = 76, SmYVirtualScreen = 77, SmCxVirtualScreen = 78, SmCyVirtualScreen = 79;
        private const int MaxSavedWidth = 1920;
        private const int MinWindowSize = 120; // daha küçüğü araç penceresi ya da yalnızca başlık çubuğudur

        /// <summary>
        /// Pencereyi ekran görüntülerinden gizler (Windows 10 2004+): tam ekran ve kilit açıkken gözetim kaydı
        /// pencerenin arkasındaki asıl ekranı görür. Eski Windows'ta etkisizdir.
        /// </summary>
        public static void ExcludeFromCapture(Window window)
        {
            SetWindowDisplayAffinity(new WindowInteropHelper(window).Handle, WdaExcludeFromCapture);
        }

        /// <summary>Pencerenin bulunduğu ekranın fiziksel piksel sınırları.</summary>
        public static Int32Rect MonitorBounds(Window window)
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            if (!GetMonitorInfo(MonitorFromWindow(hwnd, MonitorDefaultToNearest), ref info)) return Int32Rect.Empty;
            return new Int32Rect(info.Monitor.Left, info.Monitor.Top,
                info.Monitor.Right - info.Monitor.Left, info.Monitor.Bottom - info.Monitor.Top);
        }

        /// <summary>
        /// Verilen ekran bölgesinin görüntüsünü alır ve bulanıklaştırma için küçültür (dörtte bir).
        /// Başarısız olursa null döner.
        /// </summary>
        public static BitmapSource Capture(Int32Rect bounds)
        {
            BitmapSource full = Grab(bounds, IntPtr.Zero);
            if (full == null) return null;
            var small = new TransformedBitmap(full, new ScaleTransform(0.25, 0.25));
            var frozen = new WriteableBitmap(small);
            frozen.Freeze();
            return frozen;
        }

        /// <summary>
        /// Tüm ekranların görüntüsünü JPEG olarak kaydeder (gözetim kaydı için). Ekran kilitliyse ya da görüntü
        /// siyahsa yedek olarak verilen pencerenin kendi görüntüsü alınır. Kaydedemezse false döner.
        /// </summary>
        public static bool SaveScreen(string path, IntPtr fallbackWindow, string[] stamp = null)
        {
            var screen = new Int32Rect(GetSystemMetrics(SmXVirtualScreen), GetSystemMetrics(SmYVirtualScreen),
                GetSystemMetrics(SmCxVirtualScreen), GetSystemMetrics(SmCyVirtualScreen));
            if (Save(Grab(screen, IntPtr.Zero), path, stamp)) return true;
            return fallbackWindow != IntPtr.Zero && SaveWindow(path, fallbackWindow, false, stamp);
        }

        /// <summary>Pencere görev çubuğuna küçültülmüş mü (içeriği çizilmez, görüntüsü alınamaz).</summary>
        public static bool IsMinimized(IntPtr window)
        {
            return window != IntPtr.Zero && IsIconic(window);
        }

        /// <summary>
        /// Yalnızca verilen pencerenin görüntüsünü (üstü kapalı olsa da) JPEG olarak kaydeder.
        /// Küçültülmüş pencere görüntü için kısa süre ekran dışında açılır, sonra yeniden küçültülür.
        /// </summary>
        public static bool SaveWindow(string path, IntPtr window, bool captureMinimized = false, string[] stamp = null)
        {
            try
            {
                if (window == IntPtr.Zero) return false;
                if (IsIconic(window)) return captureMinimized && Save(GrabMinimized(window), path, stamp);
                return Save(GrabWindow(window), path, stamp);
            }
            catch
            {
                return false;
            }
        }

        private static BitmapSource GrabWindow(IntPtr window)
        {
            Rect rect;
            if (!GetWindowRect(window, out rect)) return null;
            int width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
            if (width < MinWindowSize || height < MinWindowSize) return null;
            return Grab(new Int32Rect(0, 0, width, height), window);
        }

        /// <summary>
        /// Küçültülmüş pencere çizilmez; görüntü için ekranların dışına, odak almadan açılır ve
        /// görüntü alınınca eski yerleşimiyle yeniden küçültülür. Ekranda görünmez, programın işine dokunmaz.
        /// </summary>
        private static BitmapSource GrabMinimized(IntPtr window)
        {
            var original = new WindowPlacement { Length = Marshal.SizeOf(typeof(WindowPlacement)) };
            if (!GetWindowPlacement(window, ref original)) return null;

            var hidden = original;
            int width = original.Normal.Right - original.Normal.Left, height = original.Normal.Bottom - original.Normal.Top;
            int left = GetSystemMetrics(SmXVirtualScreen) - width - 200;
            hidden.Normal = new Rect { Left = left, Top = 0, Right = left + width, Bottom = height };
            hidden.Flags &= ~WpfRestoreToMaximized; // büyütülmüş açılırsa ekrana gelirdi
            hidden.ShowCmd = SwShowNoActivate;
            try
            {
                if (!SetWindowPlacement(window, ref hidden)) return null;
                // Programın yeniden çizmesi için kısa bekleme
                System.Threading.Thread.Sleep(RestorePaintMilliseconds);
                return GrabWindow(window);
            }
            finally
            {
                original.ShowCmd = SwShowMinNoActive;
                SetWindowPlacement(window, ref original);
            }
        }

        private static bool Save(BitmapSource image, string path, string[] stamp = null)
        {
            try
            {
                if (image == null || IsBlack(image)) return false;
                // Büyük çok ekranlı masaüstleri küçültülür; okunaklı kalır, görüntü başına birkaç yüz KB tutar
                double scale = Math.Min(1, MaxSavedWidth / (double)image.PixelWidth);
                BitmapSource output = scale < 1 ? new TransformedBitmap(image, new ScaleTransform(scale, scale)) : image;
                if (stamp != null && stamp.Length > 0) output = Stamp(output, stamp);
                var encoder = new JpegBitmapEncoder { QualityLevel = 75 };
                encoder.Frames.Add(BitmapFrame.Create(output));
                using (var stream = File.Create(path)) encoder.Save(stream);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Satırları görüntünün sağ altına koyu zemin üstüne beyaz, okunaklı yazı olarak işler.</summary>
        private static BitmapSource Stamp(BitmapSource image, string[] lines)
        {
            int width = image.PixelWidth, height = image.PixelHeight;
            double size = Math.Max(14, Math.Min(32, width / 75.0));
            var face = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            var texts = new FormattedText[lines.Length];
            double boxWidth = 0, boxHeight = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                texts[i] = new FormattedText(lines[i], System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, size, Brushes.White, 1.0);
                texts[i].MaxTextWidth = Math.Max(50, width * 0.6);
                texts[i].MaxLineCount = 1;
                texts[i].Trimming = TextTrimming.CharacterEllipsis;
                boxWidth = Math.Max(boxWidth, texts[i].Width);
                boxHeight += texts[i].Height;
            }
            double pad = size * 0.5, margin = size * 0.6;
            double left = width - boxWidth - pad * 2 - margin, top = height - boxHeight - pad * 2 - margin;
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawImage(image, new System.Windows.Rect(0, 0, width, height));
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(190, 0, 0, 0)), null,
                    new System.Windows.Rect(Math.Max(0, left), Math.Max(0, top), boxWidth + pad * 2, boxHeight + pad * 2), 4, 4);
                double y = Math.Max(0, top) + pad;
                foreach (FormattedText text in texts)
                {
                    dc.DrawText(text, new Point(Math.Max(0, left) + pad, y));
                    y += text.Height;
                }
            }
            var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            target.Render(visual);
            return target;
        }

        // Kilitli ekranda BitBlt hata vermeden siyah görüntü döndürebilir
        private static bool IsBlack(BitmapSource image)
        {
            var tiny = new FormatConvertedBitmap(new TransformedBitmap(image,
                new ScaleTransform(64.0 / image.PixelWidth, 64.0 / image.PixelHeight)), PixelFormats.Bgr32, null, 0);
            var pixels = new byte[tiny.PixelWidth * tiny.PixelHeight * 4];
            tiny.CopyPixels(pixels, tiny.PixelWidth * 4, 0);
            foreach (byte b in pixels) if (b > 8) return false;
            return true;
        }

        /// <summary>
        /// Tam çözünürlükte görüntü alır: window boşsa ekran bölgesini, değilse o pencerenin kendisini
        /// (üstü kapalı olsa da). Başarısız olursa null döner.
        /// </summary>
        private static BitmapSource Grab(Int32Rect bounds, IntPtr window)
        {
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) return null;

            IntPtr screenDc = IntPtr.Zero, memoryDc = IntPtr.Zero, bitmap = IntPtr.Zero, previous = IntPtr.Zero;
            try
            {
                screenDc = GetDC(IntPtr.Zero);
                memoryDc = CreateCompatibleDC(screenDc);
                bitmap = CreateCompatibleBitmap(screenDc, bounds.Width, bounds.Height);
                previous = SelectObject(memoryDc, bitmap);
                bool ok = window != IntPtr.Zero
                    ? PrintWindow(window, memoryDc, PwRenderFullContent)
                    : BitBlt(memoryDc, 0, 0, bounds.Width, bounds.Height, screenDc, bounds.X, bounds.Y, SrcCopy | CaptureBlt);
                SelectObject(memoryDc, previous);
                if (!ok) return null;

                BitmapSource full = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                full.Freeze();
                return full;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                if (memoryDc != IntPtr.Zero) DeleteDC(memoryDc);
                if (screenDc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
    }
}
