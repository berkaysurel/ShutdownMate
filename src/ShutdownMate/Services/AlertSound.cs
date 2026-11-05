using System;
using System.IO;
using System.Media;
using System.Text;

namespace ShutdownMate.Services;

/// <summary>
/// Kendi işlemimizden çalınan yumuşak uyarı sesleri (diğer oturumlar kısıkken de duyulur).
/// SystemSounds kullanılmaz; çünkü onlar kıstığımız sistem sesleri oturumundan çalar.
/// </summary>
internal static class AlertSound
{
    private const int SampleRate = 44100;
    private const double Amplitude = 0.35;

    private static readonly object Sync = new object();

    // Çalma sırasında çöp toplayıcının nesneyi toplamaması için referans tutulur
    private static SoundPlayer _currentPlayer;

    // Uyarı: iki notalı yumuşak çan (880 Hz, ardından 1320 Hz)
    private static readonly Lazy<byte[]> WarningWav = new Lazy<byte[]>(() => BuildWav(
        new[] { 880.0, 1320.0 },
        new[] { 0.0, 0.15 },
        0.12,
        0.55));

    // Son uyarı: üç notalı, biraz daha aciliyet hissi veren desen
    private static readonly Lazy<byte[]> FinalWav = new Lazy<byte[]>(() => BuildWav(
        new[] { 988.0, 1319.0, 1760.0 },
        new[] { 0.0, 0.13, 0.26 },
        0.11,
        0.5));

    public static void PlayWarning()
    {
        Play(WarningWav);
    }

    public static void PlayFinal()
    {
        Play(FinalWav);
    }

    private static void Play(Lazy<byte[]> wav)
    {
        try
        {
            byte[] bytes = wav.Value;
            lock (Sync)
            {
                SoundPlayer previous = _currentPlayer;
                SoundPlayer player = new SoundPlayer(new MemoryStream(bytes, false));
                _currentPlayer = player;
                player.Play(); // Eşzamansız, engellemez
                if (previous != null)
                {
                    try
                    {
                        previous.Dispose();
                    }
                    catch
                    {
                        // Yoksay
                    }
                }
            }
        }
        catch
        {
            // Ses hataları uygulamayı asla çökertmemeli
        }
    }

    /// <summary>
    /// Verilen notaları sinüs + hafif üst harmonik, kısa atak ve üstel sönümle sentezler,
    /// 16 bit mono PCM WAV baytları döndürür.
    /// </summary>
    private static byte[] BuildWav(double[] frequencies, double[] startTimes, double decayTau, double noteLength)
    {
        double totalSeconds = 0;
        for (int n = 0; n < frequencies.Length; n++)
        {
            totalSeconds = Math.Max(totalSeconds, startTimes[n] + noteLength);
        }

        int totalSamples = (int)(totalSeconds * SampleRate);
        double[] mix = new double[totalSamples];
        double attack = 0.006; // 6 ms atak

        for (int n = 0; n < frequencies.Length; n++)
        {
            int start = (int)(startTimes[n] * SampleRate);
            int length = (int)(noteLength * SampleRate);
            double freq = frequencies[n];

            for (int i = 0; i < length && start + i < totalSamples; i++)
            {
                double t = (double)i / SampleRate;
                double env = t < attack
                    ? t / attack
                    : Math.Exp(-(t - attack) / decayTau);

                // Notanın sonunda tıklama olmaması için kısa son kararma
                double remaining = noteLength - t;
                if (remaining < 0.02)
                {
                    env *= Math.Max(0.0, remaining / 0.02);
                }

                double phase = 2.0 * Math.PI * freq * t;
                double sample = Math.Sin(phase) + 0.18 * Math.Sin(2.0 * phase) + 0.05 * Math.Sin(3.0 * phase);
                mix[start + i] += sample * env;
            }
        }

        // Tepe değeri hedef genliğe normalize et
        double peak = 0;
        for (int i = 0; i < totalSamples; i++)
        {
            peak = Math.Max(peak, Math.Abs(mix[i]));
        }
        double scale = peak > 0 ? Amplitude / peak : 0;

        int dataLength = totalSamples * 2;
        using (MemoryStream ms = new MemoryStream(44 + dataLength))
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            // RIFF/WAVE başlığı
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataLength);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);                    // fmt parça boyutu
            writer.Write((short)1);              // PCM
            writer.Write((short)1);              // mono
            writer.Write(SampleRate);            // örnekleme hızı
            writer.Write(SampleRate * 2);        // bayt hızı
            writer.Write((short)2);              // blok hizalama
            writer.Write((short)16);             // örnek başına bit
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataLength);

            for (int i = 0; i < totalSamples; i++)
            {
                double v = mix[i] * scale;
                if (v > 1.0) v = 1.0;
                if (v < -1.0) v = -1.0;
                writer.Write((short)Math.Round(v * short.MaxValue));
            }

            writer.Flush();
            return ms.ToArray();
        }
    }
}
