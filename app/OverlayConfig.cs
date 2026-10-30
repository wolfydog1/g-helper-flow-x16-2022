using GHelper.Overlay;
using GHelper.UI;

namespace GHelper
{
    public partial class OverlayConfig : RForm
    {
        RButton[] modeButtons;
        (RCheckBox box, string key)[] blocks;
        int mode;
        bool updating;

        public OverlayConfig()
        {
            InitializeComponent();
            InitTheme(true);

            Text = "Overlay Settings & Telemetry HUD";
            checkEnable.Text = "Enable In-Game Overlay";
            checkGameOnly.Text = "Show Only in Games";
            buttonLight.Text = Properties.Strings.OverlayModeLight;
            buttonDefault.Text = Properties.Strings.Default;
            buttonFull.Text = Properties.Strings.OverlayModeFull;
            buttonComplete.Text = Properties.Strings.OverlayModeComplete;
            checkFps.Text = "FPS Counter";
            checkTemp.Text = "CPU / GPU Temps";
            checkFans.Text = "Main Fan Speeds";
            checkChart.Text = "Load History Graph";
            checkPower.Text = "Power Draw (W)";
            checkUsage.Text = "Hardware Load %";
            checkRam.Text = "RAM Usage (GB)";
            checkBattery.Text = "Battery Wattage";
            checkLabels.Text = "Component Labels";
            check1PercentLow.Text = "1% Low FPS";
            checkJitter.Text = "Frame Pacing";
            checkShadow.Text = "Text Drop Shadow";
            checkHdrBadge.Text = "HDR / 512Z Badge";
            checkFrametime.Text = "Frametime (ms)";
            checkClock.Text = "System Clock";
            checkVram.Text = "VRAM Usage (GB)";
            checkMidFan.Text = "Mid System Fan";
            labelSizeTitle.Text = "HUD Scale Size:";
            labelAlphaTitle.Text = "HUD Transparency:";
            labelFontTitle.Text = "HUD Typography Font:";
            labelStyleTitle.Text = "HUD Theme & Backdrop Style:";
            labelHotkeys.Text = "•  Ctrl + Shift + Alt + O - Toggle overlay\n•  Ctrl + Shift + Alt + Mouse Drag - Move overlay\n•  Ctrl + Shift + Alt + Mouse Click - Switch mode\n•  Ctrl + Shift + Alt + Wheel - Resize overlay\n•  Ctrl + Shift + Alt + Wheel Click - Reset size";
            buttonCpuColor.Text = "CPU " + Properties.Strings.Color;
            buttonGpuColor.Text = "GPU " + Properties.Strings.Color;
            buttonReset.Text = Properties.Strings.Reset;

            buttonTopLeft.Text = "↖ Top-Left";
            buttonTopRight.Text = "↗ Top-Right";
            buttonBottomLeft.Text = "↙ Bottom-Left";
            buttonBottomRight.Text = "↘ Bottom-Right";

            buttonTopLeft.Click += (s, e) => { Program.hardwareOverlay?.SnapToCorner(OverlayCorner.TopLeft); UpdateCornerButtons(); };
            buttonTopRight.Click += (s, e) => { Program.hardwareOverlay?.SnapToCorner(OverlayCorner.TopRight); UpdateCornerButtons(); };
            buttonBottomLeft.Click += (s, e) => { Program.hardwareOverlay?.SnapToCorner(OverlayCorner.BottomLeft); UpdateCornerButtons(); };
            buttonBottomRight.Click += (s, e) => { Program.hardwareOverlay?.SnapToCorner(OverlayCorner.BottomRight); UpdateCornerButtons(); };

            modeButtons = new[] { buttonDefault, buttonLight, buttonFull, buttonComplete, buttonBar };
            buttonBar.Text = "Strip";

            blocks = new[] {
                (checkFps, "overlay_show_fps"),
                (checkTemp, "overlay_show_temp"),
                (checkFans, "overlay_show_fans"),
                (checkChart, "overlay_show_chart"),
                (checkPower, "overlay_show_power"),
                (checkUsage, "overlay_show_usage"),
                (checkRam, "overlay_show_ram"),
                (checkLabels, "overlay_names"),
                (check1PercentLow, "overlay_show_1pct_low"),
                (checkJitter, "overlay_show_jitter"),
                (checkShadow, "overlay_text_shadow"),
                (checkHdrBadge, "overlay_show_hdr_badge"),
                (checkFrametime, "overlay_show_frametime"),
                (checkClock, "overlay_show_clock"),
                (checkVram, "overlay_show_vram"),
                (checkMidFan, "overlay_show_mid_fan"),
            };

            checkEnable.Checked = AppConfig.IsOverlay();
            checkGameOnly.Checked = AppConfig.IsOverlayGameOnly();

            mode = Math.Clamp(AppConfig.Get("overlay_mode", 0), 0, modeButtons.Length - 1);
            VisualiseBlocks();
            UpdateCornerButtons();

            trackScale.Value = Math.Clamp(AppConfig.Get("overlay_scale_percent", 100), trackScale.Minimum, trackScale.Maximum);
            trackAlpha.Value = Math.Clamp(AppConfig.Get("overlay_alpha", 128), trackAlpha.Minimum, trackAlpha.Maximum);
            labelSize.Text = trackScale.Value + "%";
            labelAlpha.Text = AlphaText();

            var fontList = new Dictionary<string, string>
            {
                { "Consolas", "Consolas (Tech Monospace)" },
                { "Cascadia Code", "Cascadia Code (Modern Coding)" },
                { "Cascadia Mono", "Cascadia Mono (Clean Terminal)" },
                { "Segoe UI", "Segoe UI (Fluent Sans)" },
                { "Segoe UI Variable Display", "Segoe UI Variable (Win11)" },
                { "Bahnschrift", "Bahnschrift (Compact Sans)" },
                { "Trebuchet MS", "Trebuchet MS (High Contrast)" },
                { "Arial", "Arial (Universal Clean)" }
            };
            comboFont.Items.Clear();
            foreach (var kv in fontList) comboFont.Items.Add(new ComboOption(kv.Key, kv.Value));
            SelectComboKey(comboFont, AppConfig.GetString("overlay_font", "Consolas"));
            comboFont.SelectionChangeCommitted += (s, e) =>
            {
                if (comboFont.SelectedItem is ComboOption opt)
                {
                    AppConfig.Set("overlay_font", opt.Key);
                    Program.hardwareOverlay?.RefreshSettings();
                }
            };

            var styleList = new Dictionary<string, string>
            {
                { "translucent_dark", "Translucent Stealth Dark" },
                { "frosted_glass", "Frosted Glassmorphism (Cyan)" },
                { "neon_cyber", "ROG Cyber Red Glow" },
                { "minimal_frameless", "Minimal Frameless (HUD)" },
                { "solid_black", "Solid Contrast Black (Esports)" },
                { "nebula_tint", "Nebula Sapphire Tint (Blue)" }
            };
            comboStyle.Items.Clear();
            foreach (var kv in styleList) comboStyle.Items.Add(new ComboOption(kv.Key, kv.Value));
            SelectComboKey(comboStyle, AppConfig.GetString("overlay_style", "translucent_dark"));
            comboStyle.SelectionChangeCommitted += (s, e) =>
            {
                if (comboStyle.SelectedItem is ComboOption opt)
                {
                    AppConfig.Set("overlay_style", opt.Key);
                    Program.hardwareOverlay?.RefreshSettings();
                }
            };

            buttonCpuColor.SwatchColor = HardwareOverlay.CpuColor;
            buttonGpuColor.SwatchColor = HardwareOverlay.GpuColor;

            checkEnable.CheckedChanged += (s, e) => { if (checkEnable.Checked != AppConfig.IsOverlay()) Program.settingsForm.ToggleOverlay(); };
            checkGameOnly.CheckedChanged += (s, e) => { if (checkGameOnly.Checked != AppConfig.IsOverlayGameOnly()) Program.settingsForm.ToggleOverlayGameOnly(); };

            buttonLight.BorderColor = colorEco;
            buttonDefault.BorderColor = colorStandard;
            buttonFull.BorderColor = colorTurbo;
            buttonComplete.BorderColor = colorCustom;
            buttonBar.BorderColor = colorStandard;

            foreach (var button in modeButtons) button.Click += ButtonMode_Click;
            foreach (var (box, _) in blocks) box.CheckedChanged += Block_CheckedChanged;
            checkBattery.CheckStateChanged += Battery_CheckStateChanged;

            trackScale.ValueChanged += TrackScale_ValueChanged;
            trackAlpha.ValueChanged += TrackAlpha_ValueChanged;

            buttonCpuColor.Click += (s, e) => PickColor("overlay_color_cpu", buttonCpuColor, HardwareOverlay.CpuColor);
            buttonGpuColor.Click += (s, e) => PickColor("overlay_color_gpu", buttonGpuColor, HardwareOverlay.GpuColor);
            buttonReset.Click += ButtonReset_Click;

            Shown += (s, e) => { FormPosition(); UpdateCornerButtons(); };
        }

        public void FormPosition()
        {
            Top = Program.settingsForm.Top;
            Left = Math.Max(10, Program.settingsForm.Left - Width - 5);
        }

        private void UpdateCornerButtons()
        {
            var current = HardwareOverlay.CurrentCorner;
            buttonTopLeft.Activated = current == OverlayCorner.TopLeft;
            buttonTopRight.Activated = current == OverlayCorner.TopRight;
            buttonBottomLeft.Activated = current == OverlayCorner.BottomLeft;
            buttonBottomRight.Activated = current == OverlayCorner.BottomRight;
        }

        private string AlphaText() => Math.Round(trackAlpha.Value * 100.0 / 255) + "%";

        private void ButtonReset_Click(object? sender, EventArgs e)
        {
            for (int m = 0; m < modeButtons.Length; m++)
            {
                foreach (var (_, key) in blocks) AppConfig.Remove(HardwareOverlay.ModeKey(key, m));
                AppConfig.Remove(HardwareOverlay.ModeKey("overlay_show_battery", m));
            }
            AppConfig.Remove("overlay_scale_percent");
            AppConfig.Remove("overlay_alpha");
            AppConfig.Remove("overlay_color_cpu");
            AppConfig.Remove("overlay_color_gpu");
            AppConfig.Remove("overlay_font");
            AppConfig.Remove("overlay_style");
            SelectComboKey(comboFont, "Consolas");
            SelectComboKey(comboStyle, "translucent_dark");

            VisualiseBlocks();
            trackScale.Value = 100;
            trackAlpha.Value = 128;
            buttonCpuColor.SwatchColor = HardwareOverlay.CpuColor;
            buttonGpuColor.SwatchColor = HardwareOverlay.GpuColor;
            Program.hardwareOverlay?.RefreshSettings();
        }

        private void VisualiseBlocks()
        {
            updating = true;
            for (int i = 0; i < modeButtons.Length; i++) modeButtons[i].Activated = i == mode;
            foreach (var (box, key) in blocks) box.Checked = HardwareOverlay.BlockShown(key, mode);
            int battery = HardwareOverlay.BatteryState(mode);
            checkBattery.CheckState = battery < 0 ? CheckState.Indeterminate : (CheckState)battery;
            updating = false;
        }

        private void ButtonMode_Click(object? sender, EventArgs e)
        {
            mode = Array.IndexOf(modeButtons, sender);
            AppConfig.Set("overlay_mode", mode);
            VisualiseBlocks();
            Program.hardwareOverlay?.RefreshSettings();
        }

        private void Block_CheckedChanged(object? sender, EventArgs e)
        {
            if (updating) return;
            foreach (var (box, key) in blocks)
                if (box == sender) AppConfig.Set(HardwareOverlay.ModeKey(key, mode), box.Checked ? 1 : 0);
            Program.hardwareOverlay?.RefreshSettings();
        }

        private void Battery_CheckStateChanged(object? sender, EventArgs e)
        {
            if (updating) return;
            int state = checkBattery.CheckState == CheckState.Indeterminate ? -1 : (int)checkBattery.CheckState;
            AppConfig.Set(HardwareOverlay.ModeKey("overlay_show_battery", mode), state);
            Program.hardwareOverlay?.RefreshSettings();
        }

        private void TrackScale_ValueChanged(object? sender, EventArgs e)
        {
            labelSize.Text = trackScale.Value + "%";
            AppConfig.Set("overlay_scale_percent", trackScale.Value);
            Program.hardwareOverlay?.RefreshSettings();
        }

        private void TrackAlpha_ValueChanged(object? sender, EventArgs e)
        {
            labelAlpha.Text = AlphaText();
            AppConfig.Set("overlay_alpha", trackAlpha.Value);
            Program.hardwareOverlay?.RefreshSettings();
        }

        private void PickColor(string key, RColorButton button, Color initial)
        {
            RColorPicker colorDlg = new RColorPicker(initial);
            colorDlg.ColorChanged += c =>
            {
                AppConfig.Set(key, $"{c.R:X2}{c.G:X2}{c.B:X2}");
                button.SwatchColor = c;
                Program.hardwareOverlay?.RefreshSettings();
            };
            colorDlg.ShowDialog(this);
        }

        private sealed class ComboOption
        {
            public string Key { get; }
            public string Label { get; }
            public ComboOption(string key, string label) { Key = key; Label = label; }
            public override string ToString() => Label;
        }

        private static void SelectComboKey(ComboBox cb, string key)
        {
            for (int i = 0; i < cb.Items.Count; i++)
            {
                if (cb.Items[i] is ComboOption opt && string.Equals(opt.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    if (cb.SelectedIndex != i) cb.SelectedIndex = i;
                    return;
                }
            }
            if (cb.Items.Count > 0 && cb.SelectedIndex < 0) cb.SelectedIndex = 0;
        }
    }
}
