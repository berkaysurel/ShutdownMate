using System;
using System.Linq;
using ShutdownMate.Controls;
using ShutdownMate.Models;
using WinControls = System.Windows.Controls;
using NumberBox = Wpf.Ui.Controls.NumberBox;

namespace ShutdownMate.Views
{
    public partial class MainWindow
    {
        // =====================================================================
        // Seçim yardımcıları
        // =====================================================================

        private PowerAction SelectedAction
        {
            get
            {
                if (ActionRestart.IsChecked == true) return PowerAction.Restart;
                if (ActionSleep.IsChecked == true) return PowerAction.Sleep;
                if (ActionHibernate.IsChecked == true) return PowerAction.Hibernate;
                return PowerAction.Shutdown;
            }
        }

        private TriggerMode SelectedMode
        {
            get
            {
                if (ModeClock.IsChecked == true) return TriggerMode.Clock;
                if (ModeActivity.IsChecked == true) return TriggerMode.Activity;
                return TriggerMode.Duration;
            }
        }

        private NetworkDirection SelectedNetworkDirection
        {
            get
            {
                int i = NetworkDirectionCombo.SelectedIndex;
                return i == 1 ? NetworkDirection.Download : i == 2 ? NetworkDirection.Upload : NetworkDirection.Both;
            }
        }

        private WinControls.RadioButton ActionButton(PowerAction a)
        {
            switch (a)
            {
                case PowerAction.Restart: return ActionRestart;
                case PowerAction.Sleep: return ActionSleep;
                case PowerAction.Hibernate: return ActionHibernate;
                default: return ActionShutdown;
            }
        }

        private WinControls.RadioButton ModeButton(TriggerMode m)
        {
            switch (m)
            {
                case TriggerMode.Clock: return ModeClock;
                case TriggerMode.Activity: return ModeActivity;
                default: return ModeDuration;
            }
        }

        private static int ReadInt(TimeSpinner spinner, int fallback)
        {
            return spinner.Value;
        }

        private static int ReadInt(NumberBox box, int fallback)
        {
            int value;
            string text = (box.Text ?? "").Trim();
            if (!int.TryParse(text, out value))
                value = box.Value.HasValue ? (int)Math.Round(box.Value.Value) : fallback;
            return Math.Max((int)box.Minimum, Math.Min((int)box.Maximum, value));
        }

        private static void SelectByTag(WinControls.ComboBox combo, int value)
        {
            foreach (var item in combo.Items.OfType<WinControls.ComboBoxItem>())
            {
                if (Convert.ToInt32(item.Tag) == value) { combo.SelectedItem = item; return; }
            }
            combo.SelectedIndex = 1;
        }

        private static int ReadTag(WinControls.ComboBox combo, int fallback)
        {
            var item = combo.SelectedItem as WinControls.ComboBoxItem;
            return item != null ? Convert.ToInt32(item.Tag) : fallback;
        }

        private DateTime ComputeTarget(DateTime now, TriggerMode mode)
        {
            if (mode == TriggerMode.Clock)
            {
                DateTime day = ClockTomorrow.IsChecked == true ? now.Date.AddDays(1) : now.Date;
                return day.AddHours(ReadInt(ClockHour, 23)).AddMinutes(ReadInt(ClockMinute, 0));
            }
            return now.AddHours(ReadInt(DurationHours, 0)).AddMinutes(ReadInt(DurationMinutes, 30));
        }

        private int EnabledConditionCount
        {
            get
            {
                int n = 0;
                if (UseNetworkCheck.IsChecked == true) n++;
                if (UseCpuCheck.IsChecked == true) n++;
                if (UseGpuCheck.IsChecked == true) n++;
                if (UseTransferCheck.IsChecked == true) n++;
                if (UseProcessCheck.IsChecked == true) n++;
                return n;
            }
        }
    }
}
