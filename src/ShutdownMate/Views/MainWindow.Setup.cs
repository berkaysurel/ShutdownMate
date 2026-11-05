using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using ShutdownMate.Helpers;
using ShutdownMate.Models;
using ShutdownMate.Services;
using WinControls = System.Windows.Controls;

namespace ShutdownMate.Views
{
    public partial class MainWindow
    {
        // =====================================================================
        // Kurulum ekranı olayları
        // =====================================================================

        private void Selection_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if (sender == ModeClock) SetClockToEarliest(DateTime.Now);
            UpdateModePanels();
            UpdatePreview();
        }

        private void Value_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            UpdatePreview();
        }

        private void PrefTab_Changed(object sender, RoutedEventArgs e)
        {
            if (HistoryPanel == null) return; // InitializeComponent sırasında

            SetPanelVisible(TimerPanel, PrefTabTimer);
            SetPanelVisible(SecurityPanel, PrefTabSecurity);
            SetPanelVisible(AlertsPanel, PrefTabAlerts);
            SetPanelVisible(FullscreenPanel, PrefTabFullscreen);
            SetPanelVisible(LockPanel, PrefTabLock);
            SetPanelVisible(DisplayStylePanel, PrefTabAppearance);
            SetPanelVisible(AppearancePanel, PrefTabAppearance);
            SetPanelVisible(HistoryPanel, PrefTabHistory);
            SetPanelVisible(AboutPanel, PrefTabAbout);
            PrefScroll.ScrollToTop();
        }

        private static void SetPanelVisible(UIElement panel, WinControls.Primitives.ToggleButton tab)
        {
            Motion.SetVisible(panel, tab.IsChecked == true);
        }

        private void Spinner_Changed(object sender, EventArgs e)
        {
            UpdateDurationLimits();
            UpdateClockLimits(DateTime.Now);
            if (_loading) return;
            UpdatePreview();
        }

        private void ClockDay_Changed(object sender, RoutedEventArgs e)
        {
            UpdateClockLimits(DateTime.Now);
            if (_loading) return;
            UpdatePreview();
        }

        /// <summary>Şu andan en kısa süre kadar sonrası; bir sonraki tam dakikaya yuvarlanır.</summary>
        private DateTime EarliestClockTime(DateTime now)
        {
            DateTime t = now.AddMinutes(_settings.GetMinLeadMinutes());
            DateTime whole = new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0);
            return whole < t ? whole.AddMinutes(1) : whole;
        }

        /// <summary>Belirli saat seçicisini seçilebilecek en erken saate getirir.</summary>
        private void SetClockToEarliest(DateTime now)
        {
            DateTime earliest = EarliestClockTime(now);
            _clockBaseDate = now.Date;
            if (earliest.Date == now.Date) ClockToday.IsChecked = true;
            else ClockTomorrow.IsChecked = true;
            ClockHour.Value = earliest.Hour;
            ClockMinute.Value = earliest.Minute;
            UpdateClockLimits(now);
        }

        /// <summary>En erken saatten öncesine gidilemez; zaman ilerledikçe sınır da ilerler.</summary>
        private void UpdateClockLimits(DateTime now)
        {
            // Gece yarısı geçtiyse "yarın" artık bugündür; seçilen gerçek tarih korunur.
            if (_clockBaseDate != now.Date)
            {
                _clockBaseDate = now.Date;
                if (ClockTomorrow.IsChecked == true) ClockToday.IsChecked = true;
            }

            DateTime earliest = EarliestClockTime(now);
            bool todayAllowed = earliest.Date == now.Date;
            ClockToday.IsEnabled = todayAllowed;
            if (!todayAllowed && ClockToday.IsChecked == true) ClockTomorrow.IsChecked = true;

            DateTime selected = ClockTomorrow.IsChecked == true ? now.Date.AddDays(1) : now.Date;
            bool limited = selected == earliest.Date;
            ClockHour.Minimum = limited ? earliest.Hour : 0;
            ClockMinute.Minimum = limited && ClockHour.Value == earliest.Hour ? earliest.Minute : 0;
        }

        private void ClockHour_BeyondMaximum(object sender, EventArgs e)
        {
            if (ClockToday.IsChecked != true) return;
            ClockTomorrow.IsChecked = true;
            ClockHour.Value = 0;
        }

        private void ClockHour_BeyondMinimum(object sender, EventArgs e)
        {
            if (ClockTomorrow.IsChecked != true || !ClockToday.IsEnabled) return;
            ClockHour.Value = 23;
            ClockToday.IsChecked = true;
        }

        /// <summary>
        /// Süre seçicilerini üst sınıra göre ayarlar. Sınır toplam süre içindir:
        /// saat sınıra ulaşınca dakika 0'da kalır (24 saat sınırında 24:30 kurulamaz).
        /// </summary>
        private void UpdateDurationLimits()
        {
            int maxHours = _settings.GetMaxDurationHours();
            int minLead = _settings.GetMinLeadMinutes();
            DurationHours.Minimum = minLead / 60;
            DurationHours.Maximum = maxHours;
            DurationMinutes.Minimum = DurationHours.Value <= minLead / 60 ? minLead % 60 : 0;
            DurationMinutes.Maximum = DurationHours.Value >= maxHours ? 0 : 59;
            DurationHours.ToolTip = Loc.T("MainWindowSetup_EnFazla") + maxHours + Loc.T("MainWindowSetup_SaatSinirAyarlarDan");
        }

        private void ProgramCombo_Changed(object sender, WinControls.SelectionChangedEventArgs e)
        {
            if (_loading) return;
            UpdatePreview();
        }

        private void Option_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            UpdatePreview();
        }

        private void Condition_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            UpdateModePanels();
            UpdatePreview();
        }

        private void Combo_Changed(object sender, WinControls.SelectionChangedEventArgs e)
        {
            if (_loading) return;
            UpdatePreview();
        }

        private void UpdateModePanels()
        {
            TriggerMode m = SelectedMode;
            Motion.SetVisible(DurationPanel, m == TriggerMode.Duration);
            Motion.SetVisible(ClockPanel, m == TriggerMode.Clock);
            Motion.SetVisible(ActivityPanel, m == TriggerMode.Activity, 100);

            Motion.SetVisible(NetworkOptions, UseNetworkCheck.IsChecked == true, 100, true);
            Motion.SetVisible(CpuOptions, UseCpuCheck.IsChecked == true, 100, true);
            Motion.SetVisible(GpuOptions, UseGpuCheck.IsChecked == true, 100, true);
            Motion.SetVisible(TransferOptions, UseTransferCheck.IsChecked == true, 100, true);
            Motion.SetVisible(ProcessOptions, UseProcessCheck.IsChecked == true, 100, true);

            ForceCloseCard.IsEnabled = PowerService.SupportsForceClose(SelectedAction);

            if (UseProcessCheck.IsChecked == true && ProcessList.Items.Count == 0) LoadProcesses();
        }

        private void QuickDuration_Click(object sender, RoutedEventArgs e)
        {
            int minutes = (int)((FrameworkElement)sender).Tag;
            _loading = true;
            DurationHours.Value = minutes / 60;
            DurationMinutes.Value = minutes % 60;
            ModeDuration.IsChecked = true;
            _loading = false;
            UpdateModePanels();
            UpdatePreview();
        }

        private void RefreshProcesses_Click(object sender, RoutedEventArgs e)
        {
            LoadProcesses();
            LoadProgramChoices();
        }

        /// <summary>Koşulları tek bir programa bağlamak için açık program adlarını (benzersiz) listeler.</summary>
        private void LoadProgramChoices()
        {
            bool wasLoading = _loading;
            _loading = true;

            string net = SelectedProgramName(NetworkProgramCombo) ?? _settings.NetworkProcessName;
            string cpu = SelectedProgramName(CpuProgramCombo) ?? _settings.CpuProcessName;
            string gpu = SelectedProgramName(GpuProgramCombo) ?? _settings.GpuProcessName;
            string transfer = SelectedProgramName(TransferSourceCombo) ?? _settings.TransferSource;

            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (ProcessItem program in WindowedPrograms())
            {
                if (!names.ContainsKey(program.Name))
                    names[program.Name] = Shorten(program.Display, 40) + "  (" + program.Name + ")";
            }
            foreach (string saved in new[] { net, cpu, gpu })
            {
                if (!string.IsNullOrEmpty(saved) && !names.ContainsKey(saved))
                    names[saved] = saved + Loc.T("MainWindowSetup_SuAnAcikDegil");
            }

            var items = new List<ProgramChoice> { new ProgramChoice { Display = Loc.T("MainWindowSetup_TumSistem"), Name = "" } };
            items.AddRange(names.OrderBy(kv => kv.Value, StringComparer.CurrentCultureIgnoreCase)
                .Select(kv => new ProgramChoice { Display = kv.Value, Name = kv.Key }));

            SetProgramCombo(NetworkProgramCombo, items, net);
            SetProgramCombo(CpuProgramCombo, items, cpu);
            SetProgramCombo(GpuProgramCombo, items, gpu);

            // Aktarım kaynağı: tüm diskler, sürücüler, ağ bağdaştırıcıları, programlar
            var sources = new List<ProgramChoice> { new ProgramChoice { Display = Loc.T("MainWindowSetup_OtomatikAktarimYapanSurucu"), Name = "" } };
            sources.AddRange(TransferMonitor.ListDrives().Select(kv => new ProgramChoice { Display = kv.Value, Name = kv.Key }));
            if (!string.IsNullOrEmpty(transfer) && !sources.Any(i => i.Name == transfer))
                sources.Add(new ProgramChoice { Display = TransferMonitor.DriveOf(transfer) + Loc.T("MainWindowSetup_SuAnYok"), Name = transfer });
            SetProgramCombo(TransferSourceCombo, sources, transfer);
            _loading = wasLoading;
        }

        private static void SetProgramCombo(WinControls.ComboBox combo, List<ProgramChoice> items, string selectedName)
        {
            combo.ItemsSource = items;
            combo.SelectedItem = items.FirstOrDefault(i => string.Equals(i.Name, selectedName ?? "", StringComparison.OrdinalIgnoreCase)) ?? items[0];
        }

        private static string SelectedProgramName(WinControls.ComboBox combo)
        {
            var item = combo.SelectedItem as ProgramChoice;
            return item != null ? item.Name : null;
        }

        /// <summary>Penceresi olan programlar (kendimiz ve Gezgin hariç). Display alanı ham pencere başlığıdır.</summary>
        private static List<ProcessItem> WindowedPrograms()
        {
            int self = Environment.ProcessId;
            var list = new List<ProcessItem>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == self || p.MainWindowHandle == IntPtr.Zero) continue;
                    string title = p.MainWindowTitle;
                    if (string.IsNullOrWhiteSpace(title) || p.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase)) continue;
                    list.Add(new ProcessItem { Display = title, Pid = p.Id, Name = p.ProcessName });
                }
                catch { }
                finally { p.Dispose(); }
            }
            return list;
        }

        private static string Shorten(string title, int max)
        {
            return title.Length > max ? title.Substring(0, max - 3) + "…" : title;
        }

        private void LoadProcesses()
        {
            var selected = ProcessList.SelectedItem as ProcessItem;
            int? selectedPid = selected != null ? (int?)selected.Pid : null;

            var items = WindowedPrograms();
            foreach (ProcessItem item in items) item.Display = Shorten(item.Display, 60) + "  (" + item.Name + ")";

            items = items.OrderBy(x => x.Display, StringComparer.CurrentCultureIgnoreCase).ToList();
            ProcessList.ItemsSource = items;
            if (selectedPid.HasValue) ProcessList.SelectedItem = items.FirstOrDefault(x => x.Pid == selectedPid.Value);
            if (ProcessList.SelectedItem == null && items.Count > 0) ProcessList.SelectedIndex = 0;
        }

        private void UpdatePreview()
        {
            if (_loading || PreviewTitle == null) return;
            PowerAction action = SelectedAction;

            switch (SelectedMode)
            {
                case TriggerMode.Duration:
                case TriggerMode.Clock:
                {
                    // Başlatınca uygulanacak hesabın aynısı: onaylanan saat, işlemin yapılacağı saattir
                    var clock = new TimelineClock(_settings.FollowDaylightSaving);
                    DateTime now = clock.Now;
                    DateTime target = ComputeStartTarget(clock, now, SelectedMode);
                    TimeSpan diff = target - now;
                    DateTime localTarget = clock.ToLocal(target);
                    PreviewTitle.Text = TextFormat.ActionFuture(action) + ": " + localTarget.ToString("HH:mm") + " (" + TextFormat.DayLabel(localTarget, DateTime.Now) + ")";
                    PreviewSubtitle.Text = diff.TotalSeconds < 30 ? Loc.T("MainWindowSetup_LutfenBirSureGirin") : TextFormat.Friendly(diff) + Loc.T("MainWindowSetup_Sonra");
                    break;
                }
                case TriggerMode.Activity:
                {
                    var names = new List<string>();
                    if (UseNetworkCheck.IsChecked == true) names.Add(Loc.T("MainWindowSetup_Ag"));
                    if (UseCpuCheck.IsChecked == true) names.Add(Loc.T("MainWindowSetup_Islemci"));
                    if (UseGpuCheck.IsChecked == true) names.Add(Loc.T("MainWindowSetup_EkranKarti"));
                    if (UseTransferCheck.IsChecked == true) names.Add(Loc.T("MainWindowSetup_DosyaAktarimi"));
                    if (UseProcessCheck.IsChecked == true)
                    {
                        var item = ProcessList.SelectedItem as ProcessItem;
                        names.Add(item != null ? item.Name : "program");
                    }
                    PreviewTitle.Text = Loc.T("MainWindowLoop_IslerBitince") + TextFormat.ActionFutureLower(action);
                    PreviewSubtitle.Text = names.Count == 0
                        ? Loc.T("MainWindowSetup_EnAzBirIs")
                        : string.Join(" + ", names)
                            + (CalmCheck.IsChecked == true ? " · " + ReadInt(CalmMinutes, 2) + Loc.T("MainWindowSetup_DkSakin") : "")
                            + (GraceCheck.IsChecked == true ? Loc.T("MainWindowSetup_Ardindan") + ReadInt(GraceMinutes, 5) + Loc.T("TextFormat_Dk") : "");
                    break;
                }
            }
        }
    }
}
