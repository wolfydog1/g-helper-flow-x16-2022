using GHelper.Battery;
using GHelper.Display;
using GHelper.Input;
using GHelper.Mode;
using GHelper.UI;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

namespace GHelper.Helpers
{
    public enum TrayTelemetryMode
    {
        DefaultLogo = 0,
        CpuTemp = 1,
        BatteryWattage = 2,
        GpuTemp = 3,
        FanRpm = 4,
        RamUsage = 5,
        BatteryCapacity = 6,
        CpuPower = 7,
        MidFanRpm = 8
    }

    public enum ScenarioPreset
    {
        Gaming = 0,
        CreatorHDR = 1,
        SilentStudy = 2,
        TabletNote = 3
    }

    public static class FlowFeaturesHelper
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static IntPtr _lastCustomTrayHIcon = IntPtr.Zero;
        private static string _lastRenderedTrayText = "";
        private static Color _lastRenderedTrayColor = Color.Empty;

        // Snapshot for Undo Last Scenario
        private static bool _hasSnapshot = false;
        private static int _snapMode = 0;
        private static int _snapMiniled = 0;
        private static int _snapGamut = 0;
        private static TouchPenMode _snapTouch = TouchPenMode.TouchAndPen;
        private static string _activeScenarioName = "";

        public static bool HasScenarioUndo => _hasSnapshot;
        public static string ActiveScenarioName => _activeScenarioName;

        // Thermal & Battery Alert state
        private static int _highTempStreak = 0;
        private static DateTime _lastThermalAlert = DateTime.MinValue;
        private static bool _batteryTargetNotifiedThisCycle = false;
        private static float _lastBatteryPercent = -1f;

        // Rolling history for Main Window Sparkline (60 samples)
        public const int HistoryLength = 60;
        private static readonly float[] _cpuTempHist = new float[HistoryLength];
        private static readonly float[] _gpuTempHist = new float[HistoryLength];
        private static readonly float[] _batWattHist = new float[HistoryLength];
        private static int _histHead = 0;
        private static int _histCount = 0;
        private static readonly object _histLock = new();

        public static TrayTelemetryMode GetTrayMode() =>
            (TrayTelemetryMode)Math.Clamp(AppConfig.Get("tray_icon_mode", 0), 0, 4);

        public static bool NeedsBackgroundSensorPolling() =>
            GetTrayMode() != TrayTelemetryMode.DefaultLogo ||
            AppConfig.Is("thermal_alert_enabled") ||
            AppConfig.Is("battery_alert_enabled");

        public static void SetTrayMode(TrayTelemetryMode mode)
        {
            AppConfig.Set("tray_icon_mode", (int)mode);
            _lastRenderedTrayText = "";
            if (mode == TrayTelemetryMode.DefaultLogo)
            {
                Program.settingsForm?.VisualiseIcon(themeChange: true, force: true);
                CleanupCustomIcon();
            }
            else
            {
                if (SettingsForm.sensorTimer != null) SettingsForm.sensorTimer.Enabled = true;
                UpdateLiveTrayIcon();
            }
        }

        private static void CleanupCustomIcon()
        {
            if (_lastCustomTrayHIcon != IntPtr.Zero)
            {
                try { DestroyIcon(_lastCustomTrayHIcon); } catch { }
                _lastCustomTrayHIcon = IntPtr.Zero;
            }
        }

        public static void RecordSensorSampleAndUpdate()
        {
            float cpuT = HardwareControl.cpuTemp ?? 0f;
            float gpuT = HardwareControl.gpuTemp ?? 0f;
            float batW = (float)Math.Abs(HardwareControl.batteryRate ?? 0m);

            lock (_histLock)
            {
                _cpuTempHist[_histHead] = cpuT;
                _gpuTempHist[_histHead] = gpuT;
                _batWattHist[_histHead] = batW;
                _histHead = (_histHead + 1) % HistoryLength;
                if (_histCount < HistoryLength) _histCount++;
            }

            BatterySessionTracker.RecordSample();

            if (GetTrayMode() != TrayTelemetryMode.DefaultLogo)
            {
                UpdateLiveTrayIcon();
            }

            CheckSmartAlerts(cpuT, gpuT);
        }

        public static void GetHistorySnapshot(out float[] cpu, out float[] gpu, out float[] bat, out int count)
        {
            lock (_histLock)
            {
                count = _histCount;
                cpu = new float[count];
                gpu = new float[count];
                bat = new float[count];
                int start = (_histHead - count + HistoryLength) % HistoryLength;
                for (int i = 0; i < count; i++)
                {
                    int idx = (start + i) % HistoryLength;
                    cpu[i] = _cpuTempHist[idx];
                    gpu[i] = _gpuTempHist[idx];
                    bat[i] = _batWattHist[idx];
                }
            }
        }

        public static void UpdateLiveTrayIcon()
        {
            if (Program.trayIcon == null) return;
            var mode = GetTrayMode();
            if (mode == TrayTelemetryMode.DefaultLogo) return;

            string topText = "";
            string bottomText = "";
            Color accent = Color.FromArgb(58, 174, 239);

            switch (mode)
            {
                case TrayTelemetryMode.CpuTemp:
                {
                    int t = (int)Math.Round(HardwareControl.cpuTemp ?? 0);
                    topText = t > 0 ? t.ToString() : "--";
                    bottomText = "°C";
                    accent = t >= 85 ? Color.FromArgb(255, 70, 70)
                           : t >= 70 ? Color.FromArgb(255, 190, 40)
                           : Color.FromArgb(50, 225, 150);
                    break;
                }
                case TrayTelemetryMode.GpuTemp:
                {
                    int t = (int)Math.Round(HardwareControl.gpuTemp ?? 0);
                    topText = t > 0 ? t.ToString() : "--";
                    bottomText = "GPU";
                    accent = t >= 82 ? Color.FromArgb(255, 70, 70)
                           : t >= 68 ? Color.FromArgb(255, 190, 40)
                           : Color.FromArgb(60, 210, 255);
                    break;
                }
                case TrayTelemetryMode.BatteryWattage:
                {
                    decimal rate = HardwareControl.batteryRate ?? 0m;
                    int w = (int)Math.Round(Math.Abs(rate));
                    topText = w.ToString();
                    bottomText = rate > 0 ? "+W" : "W";
                    accent = rate > 0 ? Color.FromArgb(50, 230, 140)
                           : w <= 15 ? Color.FromArgb(80, 210, 255)
                           : w <= 30 ? Color.FromArgb(255, 190, 40)
                           : Color.FromArgb(255, 80, 80);
                    break;
                }
                case TrayTelemetryMode.FanRpm:
                {
                    int rpm = Math.Max(HardwareControl.cpuFanRPM ?? 0, HardwareControl.gpuFanRPM ?? 0);
                    topText = rpm > 0 ? (rpm / 1000.0).ToString("0.0") : "0";
                    bottomText = "kRPM";
                    accent = rpm >= 4800 ? Color.FromArgb(255, 90, 90)
                           : rpm >= 3200 ? Color.FromArgb(255, 195, 50)
                           : Color.FromArgb(90, 220, 170);
                    break;
                }
                case TrayTelemetryMode.RamUsage:
                {
                    int ram = HardwareControl.ramUsage ?? 0;
                    topText = ram > 0 ? ram.ToString() : "--";
                    bottomText = "RAM";
                    accent = ram >= 85 ? Color.FromArgb(255, 80, 80)
                           : ram >= 70 ? Color.FromArgb(255, 190, 40)
                           : Color.FromArgb(120, 200, 255);
                    break;
                }
                case TrayTelemetryMode.BatteryCapacity:
                {
                    int cap = (int)Math.Round(HardwareControl.batteryCapacity);
                    topText = cap > 0 ? cap.ToString() : "--";
                    bottomText = "%";
                    accent = cap <= 20 ? Color.FromArgb(255, 75, 75)
                           : cap <= 40 ? Color.FromArgb(255, 190, 40)
                           : Color.FromArgb(50, 225, 140);
                    break;
                }
                case TrayTelemetryMode.CpuPower:
                {
                    int pwr = (int)Math.Round(HardwareControl.cpuPower ?? 0);
                    topText = pwr > 0 ? pwr.ToString() : "--";
                    bottomText = "CPW";
                    accent = pwr >= 65 ? Color.FromArgb(255, 80, 80)
                           : pwr >= 45 ? Color.FromArgb(255, 190, 40)
                           : Color.FromArgb(60, 210, 255);
                    break;
                }
                case TrayTelemetryMode.MidFanRpm:
                {
                    int rpm = HardwareControl.midFanRPM ?? 0;
                    topText = rpm > 0 ? (rpm / 1000.0).ToString("0.0") : "0";
                    bottomText = "MID";
                    accent = rpm >= 4800 ? Color.FromArgb(255, 90, 90)
                           : rpm >= 3200 ? Color.FromArgb(255, 195, 50)
                           : Color.FromArgb(90, 220, 170);
                    break;
                }
            }

            string cacheKey = $"{topText}|{bottomText}";
            if (cacheKey == _lastRenderedTrayText && accent == _lastRenderedTrayColor && _lastCustomTrayHIcon != IntPtr.Zero)
                return;

            try
            {
                using var bmp = new Bitmap(32, 32);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    g.Clear(Color.Transparent);

                    using (var bgBrush = new SolidBrush(Color.FromArgb(235, 20, 22, 26)))
                    using (var borderPen = new Pen(accent, 2f))
                    using (var path = RComboBox.RoundedRect(new Rectangle(1, 1, 30, 30), 6, 6))
                    {
                        g.FillPath(bgBrush, path);
                        g.DrawPath(borderPen, path);
                    }

                    using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    using var topFont = new Font("Segoe UI", topText.Length >= 3 ? 10.5f : 12f, FontStyle.Bold, GraphicsUnit.Pixel);
                    using var botFont = new Font("Segoe UI", 8.5f, FontStyle.Bold, GraphicsUnit.Pixel);
                    using var whiteBrush = new SolidBrush(Color.White);
                    using var accentBrush = new SolidBrush(accent);

                    g.DrawString(topText, topFont, whiteBrush, new RectangleF(0, 2, 32, 18), sf);
                    g.DrawString(bottomText, botFont, accentBrush, new RectangleF(0, 18, 32, 12), sf);
                }

                IntPtr newHIcon = bmp.GetHicon();
                IntPtr oldHIcon = _lastCustomTrayHIcon;
                _lastCustomTrayHIcon = newHIcon;
                _lastRenderedTrayText = cacheKey;
                _lastRenderedTrayColor = accent;

                Program.trayIcon.Icon = Icon.FromHandle(newHIcon);
                if (oldHIcon != IntPtr.Zero)
                {
                    try { DestroyIcon(oldHIcon); } catch { }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("UpdateLiveTrayIcon error: " + ex.Message);
            }
        }

        private static void CheckSmartAlerts(float cpuTemp, float gpuTemp)
        {
            // 1. Thermal Alert (Optional)
            if (AppConfig.Is("thermal_alert_enabled"))
            {
                int threshold = AppConfig.Get("thermal_alert_threshold", 92);
                if (cpuTemp >= threshold || gpuTemp >= (threshold - 4))
                {
                    _highTempStreak++;
                    if (_highTempStreak >= 3 && (DateTime.Now - _lastThermalAlert).TotalMinutes >= 5)
                    {
                        _lastThermalAlert = DateTime.Now;
                        _highTempStreak = 0;
                        string msg = cpuTemp >= threshold
                            ? $"Thermal Alert: CPU at {Math.Round(cpuTemp)}°C (≥{threshold}°C)"
                            : $"Thermal Alert: GPU at {Math.Round(gpuTemp)}°C";
                        Program.toast?.RunToast(msg);
                    }
                }
                else
                {
                    _highTempStreak = 0;
                }
            }

            // 2. Battery Charge Limit Reached Alert (Optional)
            if (AppConfig.Is("battery_alert_enabled"))
            {
                bool online = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online;
                float pct = SystemInformation.PowerStatus.BatteryLifePercent * 100f;
                int limit = BatteryControl.chargeFull ? 100 : AppConfig.Get("charge_limit", 85);

                if (!online || pct < limit - 3)
                {
                    _batteryTargetNotifiedThisCycle = false;
                }
                else if (online && !_batteryTargetNotifiedThisCycle && _lastBatteryPercent >= 0 && pct >= limit && _lastBatteryPercent <= limit)
                {
                    _batteryTargetNotifiedThisCycle = true;
                    Program.toast?.RunToast($"Battery Reached Target ({limit}%) • Ready", ToastIcon.Battery);
                }
                _lastBatteryPercent = pct;
            }
        }

        public static void ApplyScenario(ScenarioPreset preset)
        {
            // Capture undo snapshot before applying first scenario (preserve original baseline across scenario switches)
            if (!_hasSnapshot)
            {
                _snapMode = Modes.GetCurrent();
                _snapMiniled = AppConfig.Get("miniled", 1);
                _snapGamut = AppConfig.Get("gamut", (int)VisualControl.GetDefaultGamut());
                _snapTouch = TouchscreenHelper.GetMode();
                _hasSnapshot = true;
            }

            switch (preset)
            {
                case ScenarioPreset.Gaming:
                    _activeScenarioName = "Gaming Unleashed";
                    Program.modeControl.SetPerformanceMode(AsusACPI.PerformanceTurbo);
                    AppConfig.Set("miniled", 1);
                    ScreenControl.SetMiniled(1);
                    ScreenControl.SetScreen(frequency: ScreenControl.MAX_REFRESH, overdrive: 1);
                    VisualControl.SetGamut((int)VisualControl.GetDefaultGamut());
                    TouchscreenHelper.SetMode(TouchPenMode.TouchAndPen);
                    Program.settingsForm?.SyncTouchPenMode(TouchPenMode.TouchAndPen);
                    Program.toast?.RunToast("Scenario: Gaming (Turbo • Max Hz • 512Z Mini-LED)", ToastIcon.Controller);
                    break;

                case ScenarioPreset.CreatorHDR:
                    _activeScenarioName = "HDR Creator Studio";
                    Program.modeControl.SetPerformanceMode(AsusACPI.PerformanceBalanced);
                    if (!ScreenCCD.GetHDRStatus()) ScreenCCD.SetHDRStatus(true);
                    AppConfig.Set("miniled", 1);
                    ScreenControl.SetMiniled(1);
                    ScreenControl.SetScreen(frequency: ScreenControl.MAX_REFRESH, overdrive: 1);
                    VisualControl.SetGamut((int)SplendidGamut.DCIP3);
                    Program.toast?.RunToast("Scenario: Creator (Balanced • HDR • 512Z • DCI-P3)", ToastIcon.BrightnessUp);
                    break;

                case ScenarioPreset.SilentStudy:
                    _activeScenarioName = "Silent Study / Eco";
                    Program.modeControl.SetPerformanceMode(AsusACPI.PerformanceSilent);
                    if (ScreenCCD.GetHDRStatus()) ScreenCCD.SetHDRStatus(false);
                    AppConfig.Set("miniled", 0);
                    ScreenControl.SetMiniled(0);
                    ScreenControl.SetScreen(frequency: ScreenControl.MIN_RATE, overdrive: 0);
                    Program.toast?.RunToast("Scenario: Silent Study (Silent • 60Hz • 1Z Clarity)", ToastIcon.Battery);
                    break;

                case ScenarioPreset.TabletNote:
                    _activeScenarioName = "Tablet / Stylus Note";
                    Program.modeControl.SetPerformanceMode(AsusACPI.PerformanceSilent);
                    AppConfig.Set("miniled", 0);
                    ScreenControl.SetMiniled(0);
                    TouchscreenHelper.SetMode(TouchPenMode.PenOnly);
                    Program.settingsForm?.SyncTouchPenMode(TouchPenMode.PenOnly);
                    if (AutoRotationControl.IsSupported()) AutoRotationControl.SetAutoRotation(true);
                    Program.toast?.RunToast("Scenario: Stylus Note (Silent • Pen-Only • 1Z)", ToastIcon.Touchpad);
                    break;
            }

            ScreenControl.InitScreen();
            Program.settingsForm?.SetContextMenu();
        }

        public static void UndoScenario()
        {
            if (!_hasSnapshot) return;
            _hasSnapshot = false;
            _activeScenarioName = "";

            Program.modeControl.SetPerformanceMode(_snapMode);
            AppConfig.Set("miniled", _snapMiniled);
            ScreenControl.SetMiniled(_snapMiniled);
            VisualControl.SetGamut(_snapGamut);
            TouchscreenHelper.SetMode(_snapTouch);
            Program.settingsForm?.SyncTouchPenMode(_snapTouch);
            ScreenControl.AutoScreen(true);
            Program.settingsForm?.SetContextMenu();
            Program.toast?.RunToast("Scenario Reverted to Previous Settings");
        }

        public static void ExportSettingsBackup(Form? owner)
        {
            try
            {
                string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GHelper", "config.json");
                if (!File.Exists(configPath))
                {
                    MessageBox.Show(owner, "Configuration file not found.", "Export Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using var sfd = new SaveFileDialog
                {
                    Title = "Export G-Helper Settings & Profiles Backup",
                    Filter = "G-Helper Backup (*.json)|*.json",
                    FileName = $"GHelper_FlowX16_Backup_{DateTime.Now:yyyy-MM-dd}.json"
                };

                if (sfd.ShowDialog(owner) == DialogResult.OK)
                {
                    File.Copy(configPath, sfd.FileName, overwrite: true);
                    Program.toast?.RunToast("Settings Backup Exported!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, "Failed to export backup: " + ex.Message, "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static void ImportSettingsBackup(Form? owner)
        {
            try
            {
                using var ofd = new OpenFileDialog
                {
                    Title = "Restore G-Helper Settings & Profiles Backup",
                    Filter = "G-Helper Backup (*.json)|*.json|All Files (*.*)|*.*"
                };

                if (ofd.ShowDialog(owner) != DialogResult.OK) return;

                string json = File.ReadAllText(ofd.FileName);
                var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (parsed == null || parsed.Count == 0)
                {
                    MessageBox.Show(owner, "The selected file is not a valid G-Helper configuration backup.", "Restore Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GHelper", "config.json");
                File.WriteAllText(configPath, json);
                AppConfig.Reload();

                RForm.RefreshAllThemes();
                Program.modeControl?.SetPerformanceMode();
                ScreenControl.AutoScreen(true);
                Program.settingsForm?.SetContextMenu();
                Program.toast?.RunToast("Settings Backup Restored Successfully!");
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, "Failed to restore backup: " + ex.Message, "Restore Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // =========================================================================
    // FEATURE 1: External Monitor DDC/CI Brightness Control & Sync
    // Strict constraint: ONLY visible in UI/menus when an external display is connected
    // =========================================================================
    public static class ExternalDisplayBrightness
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, [In] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetMonitorBrightness(IntPtr hMonitor, out uint pdwMinimumBrightness, out uint pdwCurrentBrightness, out uint pdwMaximumBrightness);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool SetMonitorBrightness(IntPtr hMonitor, uint dwNewBrightness);

        private static int _lastKnownBrightness = -1;
        private static long _lastSetTick = 0;

        /// <summary>
        /// Returns true ONLY when an external display is connected (either multiple displays active,
        /// or external display active while internal laptop panel is turned off / in clamshell mode).
        /// </summary>
        public static bool HasExternalDisplayConnected()
        {
            try
            {
                var screens = Screen.AllScreens;
                if (screens.Length > 1) return true;
                if (screens.Length == 1)
                {
                    string? internalScreen = ScreenNative.FindLaptopScreen(true);
                    if (!string.IsNullOrEmpty(internalScreen) &&
                        !string.Equals(screens[0].DeviceName, internalScreen, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static List<IntPtr> GetExternalHMonitors()
        {
            var result = new List<IntPtr>();
            string? internalDevice = null;
            try { internalDevice = ScreenNative.FindLaptopScreen(true); } catch { }

            var allMonitors = new List<(IntPtr hMon, string device, bool isPrimary)>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data) =>
            {
                var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
                if (GetMonitorInfo(hMon, ref mi))
                {
                    bool primary = (mi.dwFlags & 1) != 0;
                    allMonitors.Add((hMon, mi.szDevice ?? "", primary));
                }
                return true;
            }, IntPtr.Zero);

            foreach (var m in allMonitors)
            {
                if (!string.IsNullOrEmpty(internalDevice))
                {
                    if (!string.Equals(m.device, internalDevice, StringComparison.OrdinalIgnoreCase))
                        result.Add(m.hMon);
                }
                else if (allMonitors.Count > 1 && !m.isPrimary)
                {
                    result.Add(m.hMon);
                }
            }

            if (result.Count == 0 && allMonitors.Count > 1)
            {
                foreach (var m in allMonitors)
                {
                    if (!m.isPrimary) result.Add(m.hMon);
                }
            }

            return result;
        }

        public static int Get()
        {
            if (!HasExternalDisplayConnected()) return -1;
            if (Environment.TickCount64 - _lastSetTick < 1200 && _lastKnownBrightness >= 0)
                return _lastKnownBrightness;

            try
            {
                foreach (IntPtr hMon in GetExternalHMonitors())
                {
                    if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out uint count) || count == 0)
                        continue;

                    var phys = new PHYSICAL_MONITOR[count];
                    if (GetPhysicalMonitorsFromHMONITOR(hMon, count, phys))
                    {
                        try
                        {
                            for (int i = 0; i < count; i++)
                            {
                                if (GetMonitorBrightness(phys[i].hPhysicalMonitor, out uint min, out uint cur, out uint max) && max > min)
                                {
                                    int pct = (int)Math.Round((double)(cur - min) / (max - min) * 100.0);
                                    _lastKnownBrightness = Math.Clamp(pct, 0, 100);
                                    return _lastKnownBrightness;
                                }
                            }
                        }
                        finally
                        {
                            DestroyPhysicalMonitors(count, phys);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ExternalDisplayBrightness.Get error: " + ex.Message);
            }

            return _lastKnownBrightness >= 0 ? _lastKnownBrightness : AppConfig.Get("ext_brightness", 80);
        }

        public static bool Set(int percent)
        {
            if (!HasExternalDisplayConnected()) return false;
            percent = Math.Clamp(percent, 0, 100);
            _lastKnownBrightness = percent;
            _lastSetTick = Environment.TickCount64;
            AppConfig.Set("ext_brightness", percent);

            bool anySuccess = false;
            try
            {
                foreach (IntPtr hMon in GetExternalHMonitors())
                {
                    if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out uint count) || count == 0)
                        continue;

                    var phys = new PHYSICAL_MONITOR[count];
                    if (GetPhysicalMonitorsFromHMONITOR(hMon, count, phys))
                    {
                        try
                        {
                            for (int i = 0; i < count; i++)
                            {
                                uint min = 0, cur = 0, max = 100;
                                GetMonitorBrightness(phys[i].hPhysicalMonitor, out min, out cur, out max);
                                if (max <= min) { min = 0; max = 100; }
                                uint target = (uint)Math.Round(min + (percent / 100.0) * (max - min));
                                if (SetMonitorBrightness(phys[i].hPhysicalMonitor, target))
                                {
                                    anySuccess = true;
                                }
                            }
                        }
                        finally
                        {
                            DestroyPhysicalMonitors(count, phys);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ExternalDisplayBrightness.Set error: " + ex.Message);
            }
            return anySuccess;
        }

        public static void SetAsync(int percent)
        {
            int target = Math.Clamp(percent, 0, 100);
            _lastKnownBrightness = target;
            _lastSetTick = Environment.TickCount64;
            Task.Run(() => Set(target));
        }
    }

    // =========================================================================
    // FEATURE 4: Wi-Fi Power-Saving & Low-Latency Gaming Toggle
    // Eliminates MediaTek / Intel Wi-Fi ping spikes by setting Wireless Adapter
    // Power Saving Mode to Maximum Performance (0) + WLAN Media Streaming Mode
    // =========================================================================
    public static class WifiLatencyHelper
    {
        // Wireless Adapter Settings Subgroup: 19cbb8fa-5279-450e-9fac-8a3d5fedd0c1
        private static readonly Guid GUID_WIRELESS_SUBGROUP = new("19cbb8fa-5279-450e-9fac-8a3d5fedd0c1");
        // Power Saving Mode Setting: 12bbebe6-58d6-4636-95bb-3217ef867c1a
        // 0 = Maximum Performance (Low Latency), 1 = Low Power Saving, 2 = Medium Power Saving, 3 = Maximum Power Saving
        private static readonly Guid GUID_WIRELESS_POWERMODE = new("12bbebe6-58d6-4636-95bb-3217ef867c1a");

        [DllImport("PowrProf.dll", CharSet = CharSet.Unicode)]
        private static extern uint PowerGetActiveScheme(IntPtr UserPowerKey, out IntPtr ActivePolicyGuid);

        [DllImport("PowrProf.dll", CharSet = CharSet.Unicode)]
        private static extern uint PowerReadACValueIndex(IntPtr RootPowerKey, [MarshalAs(UnmanagedType.LPStruct)] Guid SchemeGuid, [MarshalAs(UnmanagedType.LPStruct)] Guid SubGroupGuid, [MarshalAs(UnmanagedType.LPStruct)] Guid SettingGuid, out IntPtr AcValueIndex);

        [DllImport("PowrProf.dll", CharSet = CharSet.Unicode)]
        private static extern uint PowerReadDCValueIndex(IntPtr RootPowerKey, [MarshalAs(UnmanagedType.LPStruct)] Guid SchemeGuid, [MarshalAs(UnmanagedType.LPStruct)] Guid SubGroupGuid, [MarshalAs(UnmanagedType.LPStruct)] Guid SettingGuid, out IntPtr DcValueIndex);

        [DllImport("PowrProf.dll", CharSet = CharSet.Unicode)]
        private static extern uint PowerWriteACValueIndex(IntPtr RootPowerKey, [MarshalAs(UnmanagedType.LPStruct)] Guid SchemeGuid, [MarshalAs(UnmanagedType.LPStruct)] Guid SubGroupGuid, [MarshalAs(UnmanagedType.LPStruct)] Guid SettingGuid, int AcValueIndex);

        [DllImport("PowrProf.dll", CharSet = CharSet.Unicode)]
        private static extern uint PowerWriteDCValueIndex(IntPtr RootPowerKey, [MarshalAs(UnmanagedType.LPStruct)] Guid SchemeGuid, [MarshalAs(UnmanagedType.LPStruct)] Guid SubGroupGuid, [MarshalAs(UnmanagedType.LPStruct)] Guid SettingGuid, int DcValueIndex);

        [DllImport("PowrProf.dll", CharSet = CharSet.Unicode)]
        private static extern uint PowerSetActiveScheme(IntPtr RootPowerKey, [MarshalAs(UnmanagedType.LPStruct)] Guid SchemeGuid);

        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern uint WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern uint WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern uint WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern void WlanFreeMemory(IntPtr pMemory);

        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern uint WlanSetInterface(IntPtr hClientHandle, [MarshalAs(UnmanagedType.LPStruct)] Guid pInterfaceGuid, uint OpCode, uint dwDataSize, ref int pData, IntPtr pReserved);

        private const uint wlan_intf_opcode_media_streaming_mode = 3;

        private static Guid GetActiveSchemeGuid()
        {
            if (PowerGetActiveScheme(IntPtr.Zero, out IntPtr ptr) == 0 && ptr != IntPtr.Zero)
            {
                try
                {
                    return Marshal.PtrToStructure<Guid>(ptr);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
            return new Guid("381b4222-f694-41f0-9685-ff5bb260df2e");
        }

        public static bool IsLowLatencyActive()
        {
            try
            {
                Guid scheme = GetActiveSchemeGuid();
                bool onBattery = SystemInformation.PowerStatus.PowerLineStatus != PowerLineStatus.Online;
                if (onBattery)
                {
                    if (PowerReadDCValueIndex(IntPtr.Zero, scheme, GUID_WIRELESS_SUBGROUP, GUID_WIRELESS_POWERMODE, out IntPtr dcVal) == 0)
                        return dcVal.ToInt32() == 0;
                }
                else
                {
                    if (PowerReadACValueIndex(IntPtr.Zero, scheme, GUID_WIRELESS_SUBGROUP, GUID_WIRELESS_POWERMODE, out IntPtr acVal) == 0)
                        return acVal.ToInt32() == 0;
                }
            }
            catch { }
            return AppConfig.Is("wifi_low_latency");
        }

        public static void SetLowLatency(bool enable, bool showToast = false)
        {
            AppConfig.Set("wifi_low_latency", enable ? 1 : 0);
            try
            {
                Guid scheme = GetActiveSchemeGuid();
                // 0 = Maximum Performance (no power-saving ping spikes)
                // 2 = Medium Power Saving on battery when disabled
                int acMode = enable ? 0 : 0;
                int dcMode = enable ? 0 : 2;
                if (!enable) acMode = 1; // Low Power Saving when user explicitly turns off Low-Latency mode

                PowerWriteACValueIndex(IntPtr.Zero, scheme, GUID_WIRELESS_SUBGROUP, GUID_WIRELESS_POWERMODE, acMode);
                PowerWriteDCValueIndex(IntPtr.Zero, scheme, GUID_WIRELESS_SUBGROUP, GUID_WIRELESS_POWERMODE, dcMode);
                PowerSetActiveScheme(IntPtr.Zero, scheme);
            }
            catch (Exception ex)
            {
                Logger.WriteLine("WifiLatencyHelper PowerWrite error: " + ex.Message);
            }

            // Also toggle WLAN media streaming mode for lowest packet latency on MediaTek/Intel Wi-Fi 6E
            try
            {
                if (WlanOpenHandle(2, IntPtr.Zero, out _, out IntPtr hClient) == 0 && hClient != IntPtr.Zero)
                {
                    try
                    {
                        if (WlanEnumInterfaces(hClient, IntPtr.Zero, out IntPtr pList) == 0 && pList != IntPtr.Zero)
                        {
                            try
                            {
                                int count = Marshal.ReadInt32(pList, 0);
                                for (int i = 0; i < count; i++)
                                {
                                    // Each WLAN_INTERFACE_INFO is 532 bytes: 16-byte GUID + 512-byte WCHAR description + 4-byte state
                                    IntPtr itemPtr = IntPtr.Add(pList, 8 + i * 532);
                                    Guid ifGuid = Marshal.PtrToStructure<Guid>(itemPtr);
                                    int streaming = enable ? 1 : 0;
                                    WlanSetInterface(hClient, ifGuid, wlan_intf_opcode_media_streaming_mode, sizeof(int), ref streaming, IntPtr.Zero);
                                }
                            }
                            finally
                            {
                                WlanFreeMemory(pList);
                            }
                        }
                    }
                    finally
                    {
                        WlanCloseHandle(hClient, IntPtr.Zero);
                    }
                }
            }
            catch { }

            if (showToast)
            {
                Program.toast?.RunToast(enable
                    ? "Wi-Fi: Low-Latency Gaming Mode (Max Performance)"
                    : "Wi-Fi: Eco Power-Saving Mode");
            }
        }

        public static bool Toggle(bool showToast = true)
        {
            bool next = !IsLowLatencyActive();
            SetLowLatency(next, showToast);
            return next;
        }
    }

    // =========================================================================
    // FEATURE 5: Interactive Battery Charge Estimator & Session Stats
    // Tracks session Wh used/charged, average session power draw, time-to-target
    // charge limit, and live runtime comparison (Current vs 60Hz+1Z Eco)
    // =========================================================================
    public static class BatterySessionTracker
    {
        private static readonly DateTime _sessionStart = DateTime.Now;
        private static DateTime _lastSampleTime = DateTime.MinValue;
        private static double _dischargedWh = 0.0;
        private static double _chargedWh = 0.0;
        private static double _dischargeSeconds = 0.0;
        private static double _chargeSeconds = 0.0;

        public static void RecordSample()
        {
            DateTime now = DateTime.Now;
            if (_lastSampleTime == DateTime.MinValue)
            {
                _lastSampleTime = now;
                return;
            }

            double dtSec = (now - _lastSampleTime).TotalSeconds;
            _lastSampleTime = now;
            if (dtSec <= 0.05 || dtSec > 30.0) return;

            double rateW = (double)(HardwareControl.batteryRate ?? 0m);
            if (rateW < -0.3)
            {
                double absW = -rateW;
                _dischargedWh += absW * (dtSec / 3600.0);
                _dischargeSeconds += dtSec;
            }
            else if (rateW > 0.3)
            {
                _chargedWh += rateW * (dtSec / 3600.0);
                _chargeSeconds += dtSec;
            }
        }

        public static string GetSparklineSummaryLine1()
        {
            decimal rate = HardwareControl.batteryRate ?? 0m;
            decimal pct = HardwareControl.batteryCapacity;
            int limit = BatteryControl.chargeFull ? 100 : AppConfig.Get("charge_limit", 85);

            if (rate > 0.4m && HardwareControl.fullCapacity > 0 && HardwareControl.chargeCapacity > 0)
            {
                decimal targetWh = ((decimal)HardwareControl.fullCapacity / 1000m) * (limit / 100m);
                decimal curWh = (decimal)HardwareControl.chargeCapacity / 1000m;
                decimal remWh = Math.Max(0m, targetWh - curWh);
                if (remWh > 0.1m)
                {
                    int mins = (int)Math.Round((double)(remWh / rate * 60m));
                    string eta = mins >= 60 ? $"~{mins / 60}h {mins % 60}m" : $"~{mins}m";
                    return $"Charging (+{rate:0.0}W)  •  {eta} to {limit}% target";
                }
                return $"Target {limit}% Reached (+{rate:0.0}W)";
            }

            if (rate < -0.4m && HardwareControl.chargeCapacity > 0)
            {
                decimal absW = -rate;
                decimal curWh = (decimal)HardwareControl.chargeCapacity / 1000m;
                int nowMins = (int)Math.Round((double)(curWh / absW * 60m));
                string nowStr = nowMins >= 60 ? $"~{nowMins / 60}h {nowMins % 60}m" : $"~{nowMins}m";

                // Estimate savings if switching to 60Hz + 1-Zone Mini-LED
                decimal potSavingsW = 0m;
                if (AppConfig.Get("frequency", 165) > 60) potSavingsW += 2.2m;
                if (AppConfig.Get("miniled", 1) == 1) potSavingsW += 3.3m;

                if (potSavingsW > 0.5m && absW > potSavingsW + 4m)
                {
                    decimal ecoW = absW - potSavingsW;
                    int ecoMins = (int)Math.Round((double)(curWh / ecoW * 60m));
                    string ecoStr = ecoMins >= 60 ? $"~{ecoMins / 60}h {ecoMins % 60}m" : $"~{ecoMins}m";
                    return $"Est: {nowStr} now  •  {ecoStr} in 60Hz+1Z";
                }
                return $"Est Runtime: {nowStr} left at -{absW:0.0}W";
            }

            return $"Battery {Math.Round(pct)}% (AC Bypass • Target {limit}%)";
        }

        public static string GetSparklineSummaryLine2()
        {
            TimeSpan up = DateTime.Now - _sessionStart;
            string upStr = up.TotalHours >= 1 ? $"{(int)up.TotalHours}h {up.Minutes}m" : $"{Math.Max(1, up.Minutes)}m";

            if (_dischargeSeconds > 10 && _dischargedWh > 0.01)
            {
                double avgW = _dischargedWh / (_dischargeSeconds / 3600.0);
                return $"Session ({upStr}): {_dischargedWh:0.1} Wh used  •  Avg Draw: {avgW:0.1}W";
            }
            if (_chargeSeconds > 10 && _chargedWh > 0.01)
            {
                double avgW = _chargedWh / (_chargeSeconds / 3600.0);
                return $"Session ({upStr}): +{_chargedWh:0.1} Wh charged  •  Avg Rate: +{avgW:0.1}W";
            }
            return $"Session ({upStr}): AC Bypass Active (0.0 Wh cycled)";
        }

        public static string GetMenuStatusSummary()
        {
            return $"{GetSparklineSummaryLine1()}  |  {GetSparklineSummaryLine2()}";
        }
    }

    // =========================================================================
    // FEATURE 6: Convertible Posture OSD Pill (PostureOsdForm)
    // Sleek, non-activating touch bar that pops up when folding into Tablet/Tent
    // mode (or on demand) with 1-tap controls for Touch/Pen, Rotate, Mode, 1Z/512Z, Brightness
    // =========================================================================
    public class PostureOsdForm : Form
    {
        private static PostureOsdForm? _instance;
        private readonly System.Windows.Forms.Timer _hideTimer;
        private readonly Label _titleLabel;
        private readonly RButton _btnTouch;
        private readonly RButton _btnRotate;
        private readonly RButton _btnMode;
        private readonly RButton _btnMiniled;
        private readonly RButton _btnBrightDown;
        private readonly RButton _btnBrightUp;
        private readonly RButton _btnClose;

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_SHOWNOACTIVATE = 4;

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                const int WS_EX_NOACTIVATE = 0x08000000;
                const int WS_EX_TOOLWINDOW = 0x00000080;
                const int WS_EX_TOPMOST = 0x00000008;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
                return cp;
            }
        }

        public static void ShowPostureBar(string postureName = "")
        {
            if (Program.settingsForm == null || Program.settingsForm.IsDisposed || !Program.settingsForm.IsHandleCreated) return;
            if (Program.settingsForm.InvokeRequired)
            {
                Program.settingsForm.BeginInvoke(() => ShowPostureBar(postureName));
                return;
            }

            if (_instance == null || _instance.IsDisposed)
            {
                _instance = new PostureOsdForm();
            }
            _instance.PresentBar(postureName);
        }

        private PostureOsdForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(24, 26, 32);
            ForeColor = Color.White;
            Size = new Size(650, 52);
            Padding = new Padding(10, 6, 10, 6);

            _hideTimer = new System.Windows.Forms.Timer { Interval = 6500 };
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer.Stop();
                Hide();
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 8,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36F));

            _titleLabel = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(RForm.CurrentFontFamily, 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 210, 255),
                Margin = new Padding(4, 0, 8, 0),
                Text = "Posture"
            };

            RButton MakeBtn(string text, EventHandler onClick)
            {
                var btn = new RButton
                {
                    Text = text,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(3, 2, 3, 2),
                    Secondary = true,
                    BorderRadius = 8,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(38, 42, 50),
                    ForeColor = Color.White,
                    BorderColor = Color.FromArgb(58, 174, 239),
                    Font = new Font(RForm.CurrentFontFamily, 8.5f, FontStyle.Bold)
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.Click += (s, e) =>
                {
                    ResetAutoHide();
                    onClick(s, e);
                    RefreshButtonStates();
                };
                return btn;
            }

            _btnTouch = MakeBtn("Touch + Pen", (s, e) =>
            {
                var cur = TouchscreenHelper.GetMode();
                var next = cur == TouchPenMode.PenOnly ? TouchPenMode.TouchAndPen : TouchPenMode.PenOnly;
                TouchscreenHelper.SetMode(next);
                Program.settingsForm?.SyncTouchPenMode(next);
            });

            _btnRotate = MakeBtn("Rotate", (s, e) =>
            {
                AutoRotationControl.Toggle();
                Program.settingsForm?.VisualiseRotation();
            });

            _btnMode = MakeBtn("Mode", (s, e) =>
            {
                int cur = Modes.GetCurrent();
                int next = cur == AsusACPI.PerformanceSilent ? AsusACPI.PerformanceBalanced : AsusACPI.PerformanceSilent;
                Program.modeControl?.SetPerformanceMode(next);
            });

            _btnMiniled = MakeBtn("Mini-LED", (s, e) =>
            {
                int cur = AppConfig.Get("miniled", 1);
                int next = cur == 1 ? 0 : 1;
                AppConfig.Set("miniled", next);
                ScreenControl.SetMiniled(next);
                ScreenControl.InitScreen();
            });

            _btnBrightDown = MakeBtn("☀️-", (s, e) =>
            {
                int b = ScreenBrightness.Adjust(-10);
                Program.settingsForm?.UpdateBrightnessSlider(b);
            });

            _btnBrightUp = MakeBtn("☀️+", (s, e) =>
            {
                int b = ScreenBrightness.Adjust(10);
                Program.settingsForm?.UpdateBrightnessSlider(b);
            });

            _btnClose = MakeBtn("✕", (s, e) =>
            {
                _hideTimer.Stop();
                Hide();
            });
            _btnClose.BorderColor = Color.FromArgb(120, 120, 130);

            layout.Controls.Add(_titleLabel, 0, 0);
            layout.Controls.Add(_btnTouch, 1, 0);
            layout.Controls.Add(_btnRotate, 2, 0);
            layout.Controls.Add(_btnMode, 3, 0);
            layout.Controls.Add(_btnMiniled, 4, 0);
            layout.Controls.Add(_btnBrightDown, 5, 0);
            layout.Controls.Add(_btnBrightUp, 6, 0);
            layout.Controls.Add(_btnClose, 7, 0);

            Controls.Add(layout);
        }

        private void ResetAutoHide()
        {
            _hideTimer.Stop();
            _hideTimer.Start();
        }

        public void PresentBar(string postureName)
        {
            if (string.IsNullOrEmpty(postureName))
            {
                postureName = InputDispatcher.tabletMode ? "Tablet" : (InputDispatcher.tentMode ? "Tent" : "Convertible");
            }
            _titleLabel.Text = $"🔄 {postureName}";
            RefreshButtonStates();

            Rectangle workArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            Left = workArea.Left + (workArea.Width - Width) / 2;
            Top = workArea.Top + 18;

            ShowWindow(Handle, SW_SHOWNOACTIVATE);
            Invalidate();
            ResetAutoHide();
        }

        private void RefreshButtonStates()
        {
            var touch = TouchscreenHelper.GetMode();
            _btnTouch.Text = touch == TouchPenMode.PenOnly ? "✍️ Pen Only" : "👆 Touch+Pen";
            _btnTouch.Activated = touch == TouchPenMode.PenOnly;

            bool rot = AutoRotationControl.IsEnabled();
            _btnRotate.Text = rot ? "🔓 Rotate On" : "🔒 Rotate Lock";
            _btnRotate.Activated = rot;

            int mode = Modes.GetCurrent();
            _btnMode.Text = mode == AsusACPI.PerformanceSilent ? "🍃 Silent" : $"⚡ {Modes.GetCurrentName()}";
            _btnMode.Activated = mode == AsusACPI.PerformanceSilent;

            int mled = AppConfig.Get("miniled", 1);
            _btnMiniled.Text = mled == 1 ? "🔆 512-Zone" : "🔅 1-Zone";
            _btnMiniled.Activated = mled == 1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            using var pen = new Pen(Color.FromArgb(58, 174, 239), 1.8f);
            using var path = RComboBox.RoundedRect(rect, 12, 12);
            e.Graphics.DrawPath(pen, path);
        }
    }

    public class MainSparklinePanel : Panel
    {
        // 0 = CPU & GPU Temps, 1 = Battery Wattage, 2 = Session Battery Estimator & Stats
        private int _displayMode = 0;

        public MainSparklinePanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 36;
            Dock = DockStyle.Top;
            Margin = new Padding(6, 2, 6, 4);
            Cursor = Cursors.Hand;

            MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    AppConfig.Set("show_main_sparkline", 0);
                    Program.settingsForm?.SetMainSparklineVisible(false);
                    Program.settingsForm?.SetContextMenu();
                    return;
                }
                if (e.Button == MouseButtons.Left)
                {
                    _displayMode = (_displayMode + 1) % 3;
                    Invalidate();
                }
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(1, 1, Width - 3, Height - 3);
            if (rect.Width <= 10 || rect.Height <= 6) return;

            using (var bgBrush = new SolidBrush(RForm.buttonSecond))
            using (var borderPen = new Pen(RForm.borderMain, 1f))
            using (var path = RComboBox.RoundedRect(rect, Math.Min(RForm.CurrentButtonRadius, 8), Math.Min(RForm.CurrentButtonRadius, 8)))
            {
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
            }

            if (_displayMode == 2)
            {
                using var f1 = new Font(RForm.CurrentFontFamily, 7.8f, FontStyle.Bold);
                using var f2 = new Font(RForm.CurrentFontFamily, 7.4f, FontStyle.Regular);
                using var b1 = new SolidBrush(RForm.foreMain);
                using var b2 = new SolidBrush(Color.FromArgb(190, RForm.foreMain));
                g.DrawString(BatterySessionTracker.GetSparklineSummaryLine1(), f1, b1, new PointF(8f, 3f));
                g.DrawString(BatterySessionTracker.GetSparklineSummaryLine2(), f2, b2, new PointF(8f, 18f));
                return;
            }

            FlowFeaturesHelper.GetHistorySnapshot(out float[] cpu, out float[] gpu, out float[] bat, out int count);

            string summary;
            if (_displayMode == 0)
            {
                int cT = (int)Math.Round(HardwareControl.cpuTemp ?? 0);
                int gT = (int)Math.Round(HardwareControl.gpuTemp ?? 0);
                summary = $"Live Temps (60s)  •  CPU: {(cT > 0 ? cT + "°C" : "--")}   GPU: {(gT > 0 ? gT + "°C" : "--")}";
            }
            else
            {
                decimal rate = HardwareControl.batteryRate ?? 0m;
                summary = $"Power Draw (60s)  •  {(rate >= 0 ? "+" : "")}{rate:0.0}W";
            }

            using var font = new Font(RForm.CurrentFontFamily, 8f, FontStyle.Regular);
            using var textBrush = new SolidBrush(RForm.foreMain);
            g.DrawString(summary, font, textBrush, new PointF(8f, 3f));

            if (count < 2) return;

            int chartLeft = 8;
            int chartRight = Width - 10;
            int chartW = chartRight - chartLeft;
            int chartTop = 18;
            int chartBottom = Height - 5;
            int chartH = Math.Max(4, chartBottom - chartTop);

            if (_displayMode == 0)
            {
                DrawLineSeries(g, cpu, count, 35f, 95f, chartLeft, chartTop, chartW, chartH, Color.FromArgb(220, 58, 174, 239));
                DrawLineSeries(g, gpu, count, 35f, 95f, chartLeft, chartTop, chartW, chartH, Color.FromArgb(220, 6, 180, 138));
            }
            else
            {
                DrawLineSeries(g, bat, count, 0f, 65f, chartLeft, chartTop, chartW, chartH, Color.FromArgb(230, 255, 170, 30));
            }
        }

        private static void DrawLineSeries(Graphics g, float[] data, int count, float minVal, float maxVal, int left, int top, int width, int height, Color color)
        {
            PointF[] pts = new PointF[count];
            float range = Math.Max(1f, maxVal - minVal);
            for (int i = 0; i < count; i++)
            {
                float x = left + (width * (i / (float)Math.Max(1, FlowFeaturesHelper.HistoryLength - 1)));
                float norm = Math.Clamp((data[i] - minVal) / range, 0f, 1f);
                float y = top + height - (norm * height);
                pts[i] = new PointF(x, y);
            }
            using var pen = new Pen(color, 1.6f);
            g.DrawLines(pen, pts);
        }
    }
}
