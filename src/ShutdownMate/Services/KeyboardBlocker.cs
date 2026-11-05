using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ShutdownMate.Services
{
    /// <summary>
    /// Kilit açıkken Windows'un pencereye ulaşmadan işlediği tuşları yok sayar:
    /// Windows tuşu (Başlat menüsü ve Win+ kısayolları), Alt+Tab, Alt+Esc, Ctrl+Esc.
    /// Ctrl+Alt+Del ve Win+L Windows'a aittir, engellenemez.
    /// </summary>
    internal sealed class KeyboardBlocker : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int VK_TAB = 0x09;
        private const int VK_CONTROL = 0x11;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int LLKHF_ALTDOWN = 0x20;

        [StructLayout(LayoutKind.Sequential)]
        private struct KbdLlHookStruct
        {
            public int VkCode;
            public int ScanCode;
            public int Flags;
            public int Time;
            public IntPtr ExtraInfo;
        }

        private delegate IntPtr HookProc(int code, IntPtr wParam, ref KbdLlHookStruct data);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc proc, IntPtr module, uint threadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, ref KbdLlHookStruct data);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string name);

        private readonly HookProc _proc; // alanda tutulur; çöp toplayıcı temsilciyi silerse kanca çöker
        private IntPtr _hook;

        /// <summary>Kancayı kurar; çağıran iş parçacığının ileti döngüsü olmalıdır (WPF arayüz iş parçacığı).</summary>
        public KeyboardBlocker()
        {
            _proc = OnKey;
            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
            if (_hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        /// <summary>Bu tuş vuruşu engellenmeli mi? (Kontroller için ayrı tutulur.)</summary>
        internal static bool ShouldBlock(int vk, bool altDown, bool ctrlDown)
        {
            if (vk == VK_LWIN || vk == VK_RWIN) return true;
            if (altDown && (vk == VK_TAB || vk == VK_ESCAPE)) return true;
            return ctrlDown && vk == VK_ESCAPE;
        }

        private IntPtr OnKey(int code, IntPtr wParam, ref KbdLlHookStruct data)
        {
            if (code >= 0)
            {
                bool alt = (data.Flags & LLKHF_ALTDOWN) != 0;
                bool ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                if (ShouldBlock(data.VkCode, alt, ctrl)) return (IntPtr)1;
            }
            return CallNextHookEx(_hook, code, wParam, ref data);
        }

        public void Dispose()
        {
            if (_hook == IntPtr.Zero) return;
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
