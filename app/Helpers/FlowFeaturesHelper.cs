using GHelper.Battery;
using GHelper.Display;
using GHelper.Mode;
using GHelper.UI;
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
        FanRpm = 4
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
                CleanupCustomIcon();
                Program.settingsForm?.VisualiseIcon(themeChange: true, force: true);
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
            // Capture undo snapshot before applying first scenario
            _snapMode = Modes.GetCurrent();
            _snapMiniled = AppConfig.Get("miniled", 1);
            _snapGamut = AppConfig.Get("gamut", (int)VisualControl.GetDefaultGamut());
            _snapTouch = TouchscreenHelper.GetMode();
            _hasSnapshot = true;

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

    public class MainSparklinePanel : Panel
    {
        private int _displayMode = 0; // 0 = CPU & GPU Temps, 1 = Battery Wattage

        public MainSparklinePanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 36;
            Dock = DockStyle.Top;
            Margin = new Padding(6, 2, 6, 4);
            Cursor = Cursors.Hand;

            Click += (s, e) =>
            {
                if (e is MouseEventArgs me && me.Button == MouseButtons.Right)
                {
                    AppConfig.Set("show_main_sparkline", 0);
                    Visible = false;
                    Program.settingsForm?.SetContextMenu();
                    return;
                }
                _displayMode = (_displayMode + 1) % 2;
                Invalidate();
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
