using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using ShutdownMate.Controls;
using ShutdownMate.Helpers;
using ShutdownMate.Models;
using ShutdownMate.Services;
using WinControls = System.Windows.Controls;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace ShutdownMate.Views
{
    /// <summary>Program seçim listesindeki öğe.</summary>
    public sealed class ProcessItem
    {
        public string Display { get; set; }
        public int Pid { get; set; }
        public string Name { get; set; }
    }

    /// <summary>Koşulun bağlanabileceği program (süreç adına göre).</summary>
    public sealed class ProgramChoice
    {
        public string Display { get; set; }
        public string Name { get; set; }
    }

    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
    {
        private enum ViewKind { Setup, Countdown, Preferences }

        private enum RunPhase { Idle, Waiting, Countdown }

        private const string AppName = "ShutdownMate";
        private const string AppTitle = AppName;

        private static readonly KeyValuePair<string, string>[] AccentPresets =
        {
            new KeyValuePair<string, string>(Loc.T("MainWindow_WindowsVurguRengi"), ""),
            new KeyValuePair<string, string>(Loc.T("MainWindow_Mavi"), "#0067C0"),
            new KeyValuePair<string, string>(Loc.T("MainWindow_Mor"), "#8764B8"),
            new KeyValuePair<string, string>(Loc.T("MainWindow_Pembe"), "#E3008C"),
            new KeyValuePair<string, string>(Loc.T("MainWindow_Kirmizi"), "#C42B1C"),
            new KeyValuePair<string, string>(Loc.T("MainWindow_Turuncu"), "#CA5010"),
            new KeyValuePair<string, string>(Loc.T("MainWindow_Sari"), "#C19C00"),
            new KeyValuePair<string, string>(Loc.T("MainWindow_Yesil"), "#107C10"),
            new KeyValuePair<string, string>(Loc.T("MainWindow_Turkuaz"), "#038387"),
            new KeyValuePair<string, string>(Loc.T("MainWindow_Gri"), "#69797E"),
        };

        // Boş değer: uygulamanın vurgu rengi
        private static readonly KeyValuePair<string, string>[] FullscreenAccentPresets =
        {
            new KeyValuePair<string, string>(Loc.T("MainWindow_UygulamaRengi"), ""),
            new KeyValuePair<string, string>(Loc.T("MainWindow_Mavi"), "#4CC2FF"),
            new KeyValuePair<string, string>(Loc.T("MainWindow_Yesil"), "#6CCB5F"),
            new KeyValuePair<string, string>(Loc.T("MainWindow_Mor"), "#B4A0FF"),
            new KeyValuePair<string, string>(Loc.T("MainWindow_Turuncu"), "#FFB347"),
            new KeyValuePair<string, string>(Loc.T("MainWindow_Pembe"), "#FF8FB1"),
            new KeyValuePair<string, string>(Loc.T("MainWindow_Beyaz"), "#FFFFFF"),
        };

        private static readonly KeyValuePair<string, string>[] FullscreenColorPresets =
        {
            new KeyValuePair<string, string>(Loc.T("MainWindow_Siyah"), "#000000"),
            new KeyValuePair<string, string>(Loc.T("MainWindow_Antrasit"), "#1C1C1E"),
            new KeyValuePair<string, string>(Loc.T("MainWindow_GeceMavisi"), "#0B1426"),
            new KeyValuePair<string, string>(Loc.T("MainWindow_Lacivert"), "#14213D"),
            new KeyValuePair<string, string>(Loc.T("MainWindow_KoyuYesil"), "#0E1F17"),
            new KeyValuePair<string, string>(Loc.T("MainWindow_Bordo"), "#2A0E12"),
            new KeyValuePair<string, string>(Loc.T("MainWindow_Murdum"), "#1B1030"),
            new KeyValuePair<string, string>(Loc.T("MainWindow_Kahve"), "#1F1712"),
        };

        private readonly DispatcherTimer _timer;
        private readonly SystemMetrics _metrics = new SystemMetrics();
        private AppSettings _settings;
        private bool _loading = true;
        private ViewKind _view = ViewKind.Setup;
        private ViewKind _viewBeforePreferences = ViewKind.Setup;
        private FullscreenWindow _fullscreen;
        private FullscreenWindow _previewFullscreen;
        private long? _idleLockStart; // Environment.TickCount64: sistem saati değişse de geçen süre bozulmaz
        private bool _sampling;
        private bool _hiddenForLock;
        private bool _openingFullscreen;
        private WinControls.RadioButton _systemSwatch;
        private long _lastSampleTicks; // Environment.TickCount64: sistem saati değişse de aralık bozulmaz

        // Çalışma durumu
        private RunPhase _phase = RunPhase.Idle;
        private PowerAction _action;
        private TriggerMode _mode;
        private bool _forceClose;
        private TimelineClock _clock = new TimelineClock(false);
        private DateTime _target;
        private TimeSpan _total;
        private DateTime _phaseStart;
        private DateTime? _deadline;
        private bool _warned;
        private bool _critical;
        private double _progress = -1;
        private bool _fromCondition;
        private bool _graceLocked;
        private string _conditionReason;
        private string _resumeNote;
        private string _postponeNote;
        private TimeSpan _grace;
        private ActivityWatcher _watcher;
        private WatchRecorder _recorder;
        private readonly AudioSessionService _audio = new AudioSessionService();
        private long _lastMuteScanTicks;
        private bool _finalAlertShown;
        private bool _fullscreenOpenedByAlert;
        private int _lastAlertBeepSecond = -1;
        private DateTime _clockBaseDate = DateTime.Today;
        private DateTime _lastTick = DateTime.Now;
        private bool _tickGapDetected;

        public MainWindow()
        {
            _settings = SettingsService.Load();
            AudioSessionService.RestoreLeftovers();
            InitializeComponent();
            // Stil kartlarının önizlemesi gerçek göstergedir; sabit örnek değerle çizilir
            foreach (CountdownDisplay d in new[] { PreviewModern, PreviewClassic, PreviewDigital, PreviewMinimal })
                d.Update("01:23:45", Loc.T("Count_Kalan"), SymbolRegular.Power24, 0.65, false);
            FitToWorkArea();
            ThemeService.Apply(this, _settings);
            PrefTab_Changed(this, null);

            BuildAccentSwatches();
            BuildFullscreenSwatches();
            BuildFullscreenAccentSwatches();
            LoadSetupFromSettings();
            LoadProgramChoices();
            LoadPreferencesFromSettings();
            BuildQuickDurationButtons();
            HistoryPanel.Load(_settings);
            HistoryPanel.HistoryCleared += (s, e) => RefreshHistory();
            RefreshHistory();
            Topmost = _settings.AlwaysOnTop;
            Display.DisplayStyle = _settings.DisplayStyle;

            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            AboutPanel.Version = AppName + " " + v.Major + "." + v.Minor + "." + v.Build;

            if (!_metrics.GpuAvailable)
            {
                UseGpuCheck.IsEnabled = false;
                UseGpuCheck.ToolTip = Loc.T("MainWindow_BuBilgisayardaEkranKarti");
            }
            if (!_metrics.Transfer.IsAvailable)
            {
                UseTransferCheck.IsEnabled = false;
                UseTransferCheck.ToolTip = Loc.T("MainWindow_BuBilgisayardaDiskHizi");
            }
            _metrics.Sample();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _timer.Tick += (s, e) => Tick();
            _timer.Start();

            PreviewKeyDown += Window_PreviewKeyDown;
            _loading = false;
            UpdateModePanels();
            UpdatePreview();
        }

        /// <summary>Küçük ekranlarda (ör. 1366x768) pencere görev çubuğunun altına taşmaz; içerik kaydırılır.</summary>
        private void FitToWorkArea()
        {
            Rect area = SystemParameters.WorkArea;
            if (Height > area.Height) Height = area.Height;
            if (Width > area.Width) Width = area.Width;
            if (MinHeight > Height) MinHeight = Height;
            if (MinWidth > Width) MinWidth = Width;
        }
    }
}
