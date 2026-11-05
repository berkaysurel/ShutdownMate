using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ShutdownMate.Services;

/// <summary>
/// Zamanlayıcı çalışırken diğer uygulamaların sesini kısar (mute), kendi işlemimizi duyulur bırakır,
/// bittiğinde yalnızca kendi kıstığı oturumları geri açar. UI (STA) iş parçacığından çağrılmalıdır.
/// </summary>
internal sealed class AudioSessionService : IDisposable
{
    // eRender = 0, eMultimedia = 1
    private const int ERender = 0;
    private const int EMultimedia = 1;
    // CLSCTX_ALL = INPROC_SERVER | INPROC_HANDLER | LOCAL_SERVER | REMOTE_SERVER
    private const int ClsCtxAll = 0x17;

    private static readonly Guid IidAudioSessionManager2 = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");

    // Bizim kıstığımız oturumlar: anahtar = oturum örnek kimliği (session instance identifier)
    private readonly Dictionary<string, TrackedSession> _muted = new Dictionary<string, TrackedSession>(StringComparer.OrdinalIgnoreCase);
    private Guid _eventContext = Guid.Empty;
    private bool _isActive;

    public bool IsActive
    {
        get { return _isActive; }
    }

    /// <summary>
    /// Varsayılan çıkış cihazındaki tüm oturumları dolaşır; bizim işlemimiz dışındaki ve
    /// halihazırda kısık olmayan oturumları kısar ve takibe alır. Tekrar çağrılabilir.
    /// </summary>
    public void MuteOthers()
    {
        _isActive = true;

        object enumeratorObj = null;
        object deviceObj = null;
        object managerObj = null;
        object sessionEnumObj = null;

        try
        {
            enumeratorObj = new MMDeviceEnumeratorComObject();
            IMMDeviceEnumerator enumerator = enumeratorObj as IMMDeviceEnumerator;
            if (enumerator == null)
            {
                return;
            }

            IMMDevice device;
            int hr = enumerator.GetDefaultAudioEndpoint(ERender, EMultimedia, out device);
            if (hr != 0 || device == null)
            {
                return;
            }
            deviceObj = device;

            Guid iid = IidAudioSessionManager2;
            hr = device.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out managerObj);
            IAudioSessionManager2 manager = managerObj as IAudioSessionManager2;
            if (hr != 0 || manager == null)
            {
                return;
            }

            IAudioSessionEnumerator sessionEnum;
            hr = manager.GetSessionEnumerator(out sessionEnum);
            if (hr != 0 || sessionEnum == null)
            {
                return;
            }
            sessionEnumObj = sessionEnum;

            int count;
            if (sessionEnum.GetCount(out count) != 0)
            {
                return;
            }

            int ownPid = Environment.ProcessId;

            for (int i = 0; i < count; i++)
            {
                object sessionObj = null;
                bool keep = false;
                try
                {
                    if (sessionEnum.GetSession(i, out sessionObj) != 0 || sessionObj == null)
                    {
                        continue;
                    }

                    IAudioSessionControl2 control2 = sessionObj as IAudioSessionControl2;
                    ISimpleAudioVolume volume = sessionObj as ISimpleAudioVolume;
                    if (control2 == null || volume == null)
                    {
                        continue;
                    }

                    // Sistem sesleri oturumu (S_OK = 0) da kısılır; işlem kimliği kontrolü onu etkilemez.
                    bool isSystem = control2.IsSystemSoundsSession() == 0;

                    if (!isSystem)
                    {
                        uint pid;
                        control2.GetProcessId(out pid);
                        if (pid == (uint)ownPid)
                        {
                            continue; // Kendi sesimiz duyulur kalmalı
                        }
                    }

                    string instanceId;
                    if (control2.GetSessionInstanceIdentifier(out instanceId) != 0 || string.IsNullOrEmpty(instanceId))
                    {
                        continue;
                    }

                    if (_muted.ContainsKey(instanceId))
                    {
                        continue; // Zaten takipte; çift takip yok
                    }

                    int isMuted;
                    if (volume.GetMute(out isMuted) != 0)
                    {
                        continue;
                    }
                    if (isMuted != 0)
                    {
                        continue; // Kullanıcı zaten kısmış; dokunma
                    }

                    if (volume.SetMute(1, ref _eventContext) == 0)
                    {
                        _muted[instanceId] = new TrackedSession(sessionObj, volume);
                        keep = true;
                        SaveLeftovers(_muted.Keys);
                    }
                }
                catch
                {
                    // Tek bir oturumdaki hata diğerlerini engellememeli
                }
                finally
                {
                    if (!keep)
                    {
                        SafeRelease(sessionObj);
                    }
                }
            }
        }
        catch
        {
            // Ses hataları uygulamayı asla çökertmemeli
        }
        finally
        {
            SafeRelease(sessionEnumObj);
            SafeRelease(managerObj);
            SafeRelease(deviceObj);
            SafeRelease(enumeratorObj);
        }
    }

    /// <summary>
    /// Yalnızca bu servisin kıstığı ve hâlâ kısık olan oturumların sesini açar,
    /// COM nesnelerini bırakır ve takibi temizler.
    /// </summary>
    public void RestoreAll()
    {
        bool hadMuted = _muted.Count > 0;
        try
        {
            foreach (TrackedSession session in _muted.Values)
            {
                try
                {
                    int isMuted;
                    if (session.Volume.GetMute(out isMuted) == 0 && isMuted != 0)
                    {
                        session.Volume.SetMute(0, ref _eventContext);
                    }
                }
                catch
                {
                    // Oturum kapanmış olabilir; yoksay
                }
                finally
                {
                    SafeRelease(session.ComObject);
                }
            }
        }
        catch
        {
            // Yoksay
        }
        finally
        {
            _muted.Clear();
            _isActive = false;
            if (hadMuted) SaveLeftovers(_muted.Keys);
        }
    }

    // ---------------- Beklenmedik kapanışa karşı ----------------

    private static string LeftoversPath
    {
        get { return Path.Combine(SettingsService.FolderPath, "muted-sessions.json"); }
    }

    private static void SaveLeftovers(IEnumerable<string> instanceIds)
    {
        try
        {
            Directory.CreateDirectory(SettingsService.FolderPath);
            SettingsService.WriteAtomic(LeftoversPath, JsonSerializer.Serialize(new List<string>(instanceIds)));
        }
        catch
        {
            // Yoksay
        }
    }

    /// <summary>
    /// Uygulama önceki çalışmasında sesleri kısıkken beklenmedik biçimde kapandıysa (Görev Yöneticisi,
    /// elektrik kesintisi), o oturumların sesini açar. Yalnızca bu uygulamanın kıstığı oturumlara dokunur.
    /// </summary>
    public static void RestoreLeftovers()
    {
        try
        {
            if (!File.Exists(LeftoversPath)) return;
            var ids = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(LeftoversPath));
            if (ids == null || ids.Count == 0) return;
            var wanted = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
            Guid context = Guid.Empty;

            object enumeratorObj = new MMDeviceEnumeratorComObject();
            object deviceObj = null, managerObj = null, sessionEnumObj = null;
            try
            {
                IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)enumeratorObj;
                IMMDevice device;
                if (enumerator.GetDefaultAudioEndpoint(ERender, EMultimedia, out device) != 0 || device == null) goto Done;
                deviceObj = device;
                Guid iid = IidAudioSessionManager2;
                if (device.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out managerObj) != 0) goto Done;
                IAudioSessionEnumerator sessions;
                if (((IAudioSessionManager2)managerObj).GetSessionEnumerator(out sessions) != 0 || sessions == null) goto Done;
                sessionEnumObj = sessions;
                int count;
                if (sessions.GetCount(out count) != 0) goto Done;

                for (int i = 0; i < count; i++)
                {
                    object sessionObj = null;
                    try
                    {
                        if (sessions.GetSession(i, out sessionObj) != 0 || sessionObj == null) continue;
                        string instanceId;
                        if (((IAudioSessionControl2)sessionObj).GetSessionInstanceIdentifier(out instanceId) == 0 && wanted.Contains(instanceId ?? ""))
                            ((ISimpleAudioVolume)sessionObj).SetMute(0, ref context);
                    }
                    catch
                    {
                        // Tek bir oturumdaki hata diğerlerini engellememeli
                    }
                    finally
                    {
                        SafeRelease(sessionObj);
                    }
                }
            }
            finally
            {
                SafeRelease(sessionEnumObj);
                SafeRelease(managerObj);
                SafeRelease(deviceObj);
                SafeRelease(enumeratorObj);
            }
            // Oturum bulunamadıysa (aygıt yok/değişti) kayıt tutulmaz: eski kimlikler yeniden denense de eşleşmez
            Done:
            SaveLeftovers(new string[0]);
        }
        catch
        {
            // Ses hataları uygulamayı asla çökertmemeli
        }
    }

    public void Dispose()
    {
        RestoreAll();
    }

    // Aynı COM nesnesi aynı RCW'ye eşlendiği için Final yerine sayaçlı bırakma kullanılır;
    // böylece takipteki referans yanlışlıkla geçersiz kalmaz.
    private static void SafeRelease(object comObject)
    {
        try
        {
            if (comObject != null && Marshal.IsComObject(comObject))
            {
                Marshal.ReleaseComObject(comObject);
            }
        }
        catch
        {
            // Yoksay
        }
    }

    private sealed class TrackedSession
    {
        public TrackedSession(object comObject, ISimpleAudioVolume volume)
        {
            ComObject = comObject;
            Volume = volume;
        }

        public object ComObject { get; }
        public ISimpleAudioVolume Volume { get; }
    }

    // ---------------- COM birlikte çalışma tanımları (Windows SDK başlıklarındaki vtable sırasıyla) ----------------

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject
    {
    }

    // mmdeviceapi.h
    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);

        [PreserveSig]
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);

        [PreserveSig]
        int RegisterEndpointNotificationCallback(IntPtr client);

        [PreserveSig]
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    // mmdeviceapi.h
    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);

        [PreserveSig]
        int OpenPropertyStore(int access, out IntPtr properties);

        [PreserveSig]
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

        [PreserveSig]
        int GetState(out int state);
    }

    // audiopolicy.h — IAudioSessionManager metotları önce, ardından IAudioSessionManager2 metotları
    [ComImport]
    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        // IAudioSessionManager
        [PreserveSig]
        int GetAudioSessionControl(IntPtr audioSessionGuid, int streamFlags, out IntPtr sessionControl);

        [PreserveSig]
        int GetSimpleAudioVolume(IntPtr audioSessionGuid, int streamFlags, out IntPtr audioVolume);

        // IAudioSessionManager2
        [PreserveSig]
        int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnum);

        [PreserveSig]
        int RegisterSessionNotification(IntPtr sessionNotification);

        [PreserveSig]
        int UnregisterSessionNotification(IntPtr sessionNotification);

        [PreserveSig]
        int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId, IntPtr duckNotification);

        [PreserveSig]
        int UnregisterDuckNotification(IntPtr duckNotification);
    }

    // audiopolicy.h
    [ComImport]
    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig]
        int GetCount(out int sessionCount);

        [PreserveSig]
        int GetSession(int sessionIndex, [MarshalAs(UnmanagedType.IUnknown)] out object session);
    }

    // audiopolicy.h — IAudioSessionControl metotları önce, ardından IAudioSessionControl2 metotları
    [ComImport]
    [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        // IAudioSessionControl
        [PreserveSig]
        int GetState(out int state);

        [PreserveSig]
        int GetDisplayName(out IntPtr displayName);

        [PreserveSig]
        int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid eventContext);

        [PreserveSig]
        int GetIconPath(out IntPtr iconPath);

        [PreserveSig]
        int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid eventContext);

        [PreserveSig]
        int GetGroupingParam(out Guid groupingParam);

        [PreserveSig]
        int SetGroupingParam(ref Guid overrideValue, ref Guid eventContext);

        [PreserveSig]
        int RegisterAudioSessionNotification(IntPtr newNotifications);

        [PreserveSig]
        int UnregisterAudioSessionNotification(IntPtr newNotifications);

        // IAudioSessionControl2
        [PreserveSig]
        int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string sessionIdentifier);

        [PreserveSig]
        int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string sessionInstanceIdentifier);

        [PreserveSig]
        int GetProcessId(out uint processId);

        [PreserveSig]
        int IsSystemSoundsSession();

        [PreserveSig]
        int SetDuckingPreference(int optOut);
    }

    // audioclient.h
    [ComImport]
    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        [PreserveSig]
        int SetMasterVolume(float level, ref Guid eventContext);

        [PreserveSig]
        int GetMasterVolume(out float level);

        [PreserveSig]
        int SetMute(int mute, ref Guid eventContext);

        [PreserveSig]
        int GetMute(out int mute);
    }
}
