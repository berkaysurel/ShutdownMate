using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ShutdownMate.Models;

namespace ShutdownMate.Services
{
    /// <summary>Kapatma, yeniden başlatma, uyku ve hazırda bekletme işlemleri.</summary>
    internal static class PowerService
    {
        // powrprof işlevleri 1 baytlık BOOLEAN kullanır
        [DllImport("powrprof.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool SetSuspendState(
            [MarshalAs(UnmanagedType.U1)] bool hibernate,
            [MarshalAs(UnmanagedType.U1)] bool forceCritical,
            [MarshalAs(UnmanagedType.U1)] bool disableWakeEvent);

        [DllImport("powrprof.dll")]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool IsPwrHibernateAllowed();

        [DllImport("kernel32.dll")]
        private static extern uint SetThreadExecutionState(uint esFlags);

        private const uint EsContinuous = 0x80000000;
        private const uint EsSystemRequired = 0x00000001;

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const uint WmSysCommand = 0x0112;
        private const int ScMonitorPower = 0xF170;
        private static readonly IntPtr HwndBroadcast = new IntPtr(0xFFFF);

        [StructLayout(LayoutKind.Sequential)]
        private struct LastInputInfo
        {
            public uint Size;
            public uint Time;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LastInputInfo info);

        /// <summary>Son klavye / fare hareketinden bu yana geçen saniye; okunamazsa çok büyük bir değer döner.</summary>
        public static double SecondsSinceLastInput()
        {
            var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
            if (!GetLastInputInfo(ref info)) return double.MaxValue;
            return unchecked((uint)Environment.TickCount - info.Time) / 1000.0;
        }

        /// <summary>Ekranı kapatır (enerji tasarrufu). Fare ya da klavye hareketiyle yeniden açılır.</summary>
        public static void TurnOffDisplay()
        {
            try { PostMessage(HwndBroadcast, WmSysCommand, new IntPtr(ScMonitorPower), new IntPtr(2)); } catch { }
        }

        public static bool IsHibernateAvailable
        {
            get { try { return IsPwrHibernateAllowed(); } catch { return true; } }
        }

        /// <summary>
        /// Açıkken Windows'un boşta kalma nedeniyle kendiliğinden uyumasını engeller.
        /// Ekran yine kapanabilir. Uygulama kapanınca etkisi kendiliğinden kalkar.
        /// </summary>
        public static void SetKeepAwake(bool keepAwake)
        {
            try { SetThreadExecutionState(keepAwake ? (EsContinuous | EsSystemRequired) : EsContinuous); } catch { }
        }

        public static bool SupportsForceClose(PowerAction action)
        {
            return action == PowerAction.Shutdown || action == PowerAction.Restart;
        }

        /// <summary>shutdown.exe'ye verilen argümanlar (yalnızca Kapat / Yeniden başlat için).</summary>
        internal static string ShutdownArguments(PowerAction action, bool forceCloseApps)
        {
            return (action == PowerAction.Shutdown ? "/s" : "/r") + " /t 0" + (forceCloseApps ? " /f" : "");
        }

        /// <summary>İşlemi uygular. Başarılıysa null, değilse kullanıcıya gösterilecek hata metni döner.</summary>
        public static string Execute(PowerAction action, bool forceCloseApps)
        {
            try
            {
                switch (action)
                {
                    case PowerAction.Shutdown:
                    case PowerAction.Restart:
                    {
                        string args = ShutdownArguments(action, forceCloseApps);
                        // Tam yol: program klasörüne bırakılmış sahte bir shutdown.exe çalıştırılamaz
                        string exe = Path.Combine(Environment.SystemDirectory, "shutdown.exe");
                        var psi = new ProcessStartInfo(exe, args) { CreateNoWindow = true, UseShellExecute = false };
                        using (var p = Process.Start(psi))
                        {
                            if (p != null && p.WaitForExit(10000) && p.ExitCode != 0)
                                return Loc.T("PowerService_WindowsIstegiReddettiHata") + p.ExitCode + Loc.T("PowerService_BaskaBirKapatmaIslemi");
                        }
                        return null;
                    }
                    case PowerAction.Sleep:
                        return SetSuspendState(false, false, false)
                            ? null
                            : Loc.T("PowerService_UykuModunaGecilemediBu");
                    case PowerAction.Hibernate:
                        return SetSuspendState(true, false, false)
                            ? null
                            : Loc.T("PowerService_HazirdaBekletmeModunaGecil");
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            return null;
        }
    }
}
