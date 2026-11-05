using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace ShutdownMate.Services
{
    /// <summary>USB'ye bağlı bir depolama sürücüsü (listeleme sonucu).</summary>
    internal sealed class UsbDrive
    {
        public string Letter;      // "E:"
        public string Name;        // "SanDisk Ultra" ya da birim etiketi
        public int DiskNumber;
        public uint BusType;
        public bool OnSystemDisk;
        public uint Parent;        // fiziksel USB aygıtının düğümü; aynı aygıtın yuvaları tek kez çıkarılır
    }

    /// <summary>Kapatma ve yeniden başlatmadan önce USB depolama aygıtlarını "Donanımı güvenle kaldır" ile çıkarır.</summary>
    internal static class UsbEjectService
    {
        private const uint BusTypeUsb = 7;
        private static readonly TimeSpan PerDevice = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan Total = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Çıkarılacak sürücüler: USB veri yolundaki depolama birimleri; Windows'un bulunduğu disk asla.
        /// Saf işlev: ShutdownMate.Checks bunu doğrular.
        /// </summary>
        public static List<UsbDrive> Select(IEnumerable<UsbDrive> all)
        {
            return all.Where(d => d.BusType == BusTypeUsb && !d.OnSystemDisk).ToList();
        }

        /// <summary>Çıkarılacak sürücüler; hata olursa boş liste.</summary>
        public static List<UsbDrive> List()
        {
            var all = new List<UsbDrive>();
            try
            {
                int systemDisk = DiskNumberOf(Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\'), out _, out _);
                foreach (var di in DriveInfo.GetDrives())
                {
                    if (di.DriveType != DriveType.Fixed && di.DriveType != DriveType.Removable) continue;
                    string letter = di.Name.Substring(0, 2);
                    int disk = DiskNumberOf(letter, out uint bus, out string product);
                    if (disk < 0) continue;
                    if (di.DriveType == DriveType.Removable && !di.IsReady) continue; // boş kart yuvası
                    string label = null;
                    try { if (di.IsReady) label = di.VolumeLabel; } catch { }
                    all.Add(new UsbDrive
                    {
                        Letter = letter,
                        Name = string.IsNullOrWhiteSpace(label) ? product : label,
                        DiskNumber = disk,
                        BusType = bus,
                        OnSystemDisk = disk == systemDisk,
                        Parent = ParentOf(disk)
                    });
                }
            }
            catch { }
            return Select(all);
        }

        /// <summary>"E: (SanDisk), F: (Yedek)" biçiminde kısa metin.</summary>
        public static string Describe(IEnumerable<UsbDrive> drives)
        {
            return string.Join(", ", drives.Select(d => string.IsNullOrWhiteSpace(d.Name) ? d.Letter : d.Letter + " (" + d.Name + ")"));
        }

        /// <summary>
        /// Sürücüleri çıkarır (disk başına bir kez). Her aygıt için 5 sn, toplam 15 sn sınırı vardır.
        /// Dönen satırlar geçmiş / gözetim kaydı içindir; başarısızlar "güvenle çıkarılamadı" ile başlar.
        /// </summary>
        public static List<string> EjectAll(List<UsbDrive> drives, out int ok)
        {
            var lines = new List<string>();
            int done = 0;
            DateTime end = DateTime.UtcNow + Total;
            foreach (var group in drives.GroupBy(d => d.Parent != 0 ? d.Parent : (long)-1 - d.DiskNumber))
            {
                string names = Describe(group);
                TimeSpan left = end - DateTime.UtcNow;
                if (left <= TimeSpan.Zero) { lines.Add(names + Loc.T("UsbEjectService_GuvenleCikarilamadiSureDol")); continue; }
                string result = null;
                var task = Task.Run(() => result = EjectDisk(group.ToList()));
                bool finished = false;
                try { finished = task.Wait(left < PerDevice ? left : PerDevice); } catch { result = Loc.T("UsbEjectService_BeklenmeyenHata"); finished = true; }
                if (!finished) result = Loc.T("UsbEjectService_ZamanAsimiCikarmaArka");
                if (result == null) done++;
                else lines.Add(names + Loc.T("UsbEjectService_GuvenleCikarilamadi") + result);
            }
            ok = done;
            return lines;
        }

        /// <summary>Sonucu tek cümleye çevirir: "2 USB sürücü güvenle çıkarıldı" ya da başarısızlık nedenleri.</summary>
        public static string Summarize(int ok, List<string> failed)
        {
            if (failed.Count == 0) return ok + Loc.T("UsbEjectService_UsbSurucuGuvenleCikarildi");
            return (ok > 0 ? ok + Loc.T("UsbEjectService_UsbSurucuGuvenleCikarildi2") : Loc.T("UsbEjectService_Usb")) + string.Join("; ", failed);
        }

        private static uint ParentOf(int disk)
        {
            try
            {
                uint inst = FindDiskDevInst(disk);
                return inst != 0 && CM_Get_Parent(out uint parent, inst, 0) == 0 ? parent : 0;
            }
            catch { return 0; }
        }

        // Başarılıysa null, değilse nedeni döner
        private static string EjectDisk(List<UsbDrive> group)
        {
            try
            {
                var handles = new List<SafeFileHandle>();
                try
                {
                    // Birimi kilitle: açık dosya varsa kilit alınamaz, gerçekten kullanımdadır. Kilit yoksa (yetki) yalnızca boşaltılır.
                    foreach (var d in group)
                    {
                        var h = CreateFileW(DevicePath(d.Letter), 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                        if (h.IsInvalid) { h.Dispose(); continue; }
                        handles.Add(h);
                        bool locked = IoCtl(h, 0x90018);
                        if (!locked) { System.Threading.Thread.Sleep(500); locked = IoCtl(h, 0x90018); }
                        if (!locked) return Loc.T("UsbEjectService_KullanimdaAcikDosyaYa");
                    }
                    // Hepsi kilitlendi: biri kullanımdaysa yukarıda hiçbiri ayrılmadan dönülür
                    foreach (var h in handles)
                    {
                        FlushFileBuffers(h);
                        IoCtl(h, 0x90020); // dosya sistemini ayır
                    }
                }
                finally { foreach (var h in handles) h.Dispose(); }

                if (group[0].Parent == 0) return Loc.T("UsbEjectService_AygitBulunamadi");
                string veto = null;
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    var sb = new StringBuilder(260);
                    int cr = CM_Request_Device_EjectW(group[0].Parent, out int vetoType, sb, 260, 0);
                    if (cr == 0) return null;
                    veto = VetoText(vetoType, sb.ToString());
                    System.Threading.Thread.Sleep(700);
                }
                // Kart okuyucular gibi aygıt düğümü çıkarılamıyorsa ortamı (medyayı) çıkar
                foreach (var d in group)
                    if (!EjectMedia(d.Letter)) return veto;
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>Sürücü harfinin aygıt yolu (\\.\E:). Tek yerde tutulur; \.\ gibi eksik yazım sessizce başarısız olur.</summary>
        internal static string DevicePath(string letter)
        {
            return @"\\.\" + letter;
        }

        private static bool IoCtl(SafeFileHandle h, uint code)
        {
            return DeviceIoControl(h, code, null, 0, null, 0, out _, IntPtr.Zero);
        }

        private static bool EjectMedia(string letter)
        {
            using (var h = CreateFileW(DevicePath(letter), 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
            {
                if (h.IsInvalid) return false;
                var prevent = new byte[1]; // PreventMediaRemoval = false
                DeviceIoControl(h, 0x2D4804, prevent, 1, null, 0, out _, IntPtr.Zero);
                return IoCtl(h, 0x2D4808);
            }
        }

        // Windows'un engel türü; ad yalnızca yazdırılabilir karakterlerle (tampondaki artıklar atılır)
        private static string VetoText(int type, string name)
        {
            var clean = new StringBuilder();
            foreach (char c in name) { if (c == '\0') break; if (c >= ' ' && c < 0x250) clean.Append(c); }
            string what;
            switch (type)
            {
                case 2: what = Loc.T("UsbEjectService_PencereKapanmayiBekliyor"); break;
                case 3: what = Loc.T("UsbEjectService_AcikUygulama"); break;
                case 4: what = Loc.T("UsbEjectService_CalisanHizmet"); break;
                case 5: what = Loc.T("UsbEjectService_AcikDosya"); break;
                case 6: what = Loc.T("UsbEjectService_Aygit"); break;
                case 7: what = Loc.T("UsbEjectService_Surucu"); break;
                case 1: case 11: what = Loc.T("UsbEjectService_EskiAygitYaDa"); break;
                case 9: what = Loc.T("UsbEjectService_YetersizGuc"); break;
                case 10: case 8: what = Loc.T("UsbEjectService_AygitCikarilamaz"); break;
                case 12: what = Loc.T("UsbEjectService_YetkiYok"); break;
                default: what = Loc.T("UsbEjectService_BilinmeyenNeden"); break;
            }
            return Loc.T("UsbEjectService_Kullanimda") + what + (clean.Length > 0 ? ": " + clean : "") + ")";
        }

        // Sürücü harfinin fiziksel disk numarası, veri yolu ve ürün adı; bulunamazsa -1
        private static int DiskNumberOf(string letter, out uint busType, out string product)
        {
            busType = 0; product = null;
            using (var h = CreateFileW(DevicePath(letter), 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
            {
                if (h.IsInvalid) return -1;
                int num = DeviceNumber(h);
                if (num < 0) return -1;
                var q = new byte[12]; // PropertyId 0 = StorageDeviceProperty, QueryType 0
                var buf = new byte[1024];
                if (DeviceIoControl(h, 0x2D1400, q, q.Length, buf, buf.Length, out _, IntPtr.Zero))
                {
                    busType = BitConverter.ToUInt32(buf, 28);
                    int off = (int)BitConverter.ToUInt32(buf, 12);
                    if (off > 0 && off < buf.Length)
                    {
                        int len = Array.IndexOf(buf, (byte)0, off) - off;
                        if (len > 0) product = Encoding.ASCII.GetString(buf, off, len).Trim();
                    }
                }
                return num;
            }
        }

        private static int DeviceNumber(SafeFileHandle h)
        {
            var buf = new byte[12]; // DeviceType, DeviceNumber, PartitionNumber
            return DeviceIoControl(h, 0x2D1080, null, 0, buf, buf.Length, out _, IntPtr.Zero)
                ? BitConverter.ToInt32(buf, 4) : -1;
        }

        // Disk arabirimlerini gezip numarası eşleşenin aygıt düğümünü bulur
        private static uint FindDiskDevInst(int diskNumber)
        {
            Guid diskGuid = new Guid("53f56307-b6bf-11d0-94f2-00a0c91efb8b");
            IntPtr set = SetupDiGetClassDevsW(ref diskGuid, null, IntPtr.Zero, 0x12); // PRESENT | DEVICEINTERFACE
            if (set == new IntPtr(-1)) return 0;
            try
            {
                for (uint i = 0; ; i++)
                {
                    var ifd = new SpDeviceInterfaceData { Size = Marshal.SizeOf<SpDeviceInterfaceData>() };
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref diskGuid, i, ref ifd)) return 0;
                    var info = new SpDevInfoData { Size = Marshal.SizeOf<SpDevInfoData>() };
                    SetupDiGetDeviceInterfaceDetailW(set, ref ifd, IntPtr.Zero, 0, out int need, IntPtr.Zero);
                    IntPtr detail = Marshal.AllocHGlobal(need);
                    try
                    {
                        // cbSize: 64 bit'te 8, 32 bit'te 6
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetailW(set, ref ifd, detail, need, out _, ref info)) continue;
                        string path = Marshal.PtrToStringUni(detail + 4);
                        using (var h = CreateFileW(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
                        {
                            if (!h.IsInvalid && DeviceNumber(h) == diskNumber) return info.DevInst;
                        }
                    }
                    finally { Marshal.FreeHGlobal(detail); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SpDeviceInterfaceData
        {
            public int Size; public Guid Class; public int Flags; public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SpDevInfoData
        {
            public int Size; public Guid Class; public uint DevInst; public IntPtr Reserved;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushFileBuffers(SafeFileHandle h);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(SafeFileHandle h, uint code, byte[] inBuf, int inSize, byte[] outBuf, int outSize, out int returned, IntPtr ov);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevsW(ref Guid cls, string enumerator, IntPtr parent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid cls, uint index, ref SpDeviceInterfaceData data);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr set, ref SpDeviceInterfaceData data, IntPtr detail, int size, out int required, IntPtr info);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr set, ref SpDeviceInterfaceData data, IntPtr detail, int size, out int required, ref SpDevInfoData info);

        [DllImport("setupapi.dll")]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

        [DllImport("cfgmgr32.dll")]
        private static extern int CM_Get_Parent(out uint parent, uint devInst, uint flags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Request_Device_EjectW(uint devInst, out int vetoType, StringBuilder vetoName, int len, uint flags);
    }
}
