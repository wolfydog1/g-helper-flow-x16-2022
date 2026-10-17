using GHelper.Ally;
using GHelper.AnimeMatrix;
using GHelper.AutoUpdate;
using GHelper.Battery;
using GHelper.Display;
using GHelper.Fan;
using GHelper.Gpu;
using GHelper.Helpers;
using GHelper.Input;
using GHelper.Mode;
using GHelper.Peripherals;
using GHelper.Peripherals.Keyboard;
using GHelper.Peripherals.Mouse;
using GHelper.Properties;
using GHelper.UI;
using GHelper.USB;
using System.Diagnostics;
using System.Timers;

namespace GHelper
{
    public partial class SettingsForm : RForm
    {
        ContextMenuStrip contextMenuStrip = new CustomContextMenu();
        ToolStripMenuItem menuEco, menuStandard, menuUltimate, menuOptimized;
        DonateControl donateControl;

        public GPUModeControl gpuControl;
        public AllyControl allyControl;
        AutoUpdateControl updateControl;

        AsusMouseSettings? mouseSettings;
        AsusKeyboardSettings? keyboardSettings;

        public AniMatrixControl matrixControl;

        public static System.Timers.Timer sensorTimer = default!;
        private static readonly bool sensorsAlways = AppConfig.Is("sensors_always");
        private readonly System.Windows.Forms.Timer batteryTimer = new() { Interval = 200 };

        public Matrix? matrixForm;
        public Slash? slashForm;
        public Fans? fansForm;
        public Extra? extraForm;
        public Updates? updatesForm;
        public Handheld? handheldForm;
        public OverlayConfig? overlayForm;

        static long lastRefresh;
        static long lastBatteryRefresh;
        static long lastPeripheralCheck;
        static long lastLostFocus;

        bool isGpuSection = true;
        bool isMuxGpu = true;
        bool isEcoGpu = true;

        bool batteryMouseOver = false;
        bool batteryFullMouseOver = false;

        bool sliderGammaIgnore = false;
        bool isProgrammaticBrightnessChange = false;
        bool activateCheck = false;
        bool _displayOff = false;

        private GHelper.UI.RButton? buttonAutoScreen;
        private GHelper.UI.RButton? buttonAutoKeyboard;

        private Windows.Devices.Sensors.LightSensor? ambientLightSensor;
        private System.Windows.Forms.Timer? ambientLightPollTimer;
        private float _smoothedLux = -1f;
        public static float CurrentLux { get; private set; } = -1f;
        private static int[][]? _cachedScreenCurve = null;
        private static int[][]? _cachedKeyboardCurve = null;

        public static void InvalidateCurveCache()
        {
            _cachedScreenCurve = null;
            _cachedKeyboardCurve = null;
        }

        private void InitAmbientLightSensor()
        {
            try
            {
                ambientLightSensor = Windows.Devices.Sensors.LightSensor.GetDefault();
                if (ambientLightSensor != null)
                {
                    ambientLightSensor.ReportInterval = Math.Max(ambientLightSensor.MinimumReportInterval, 1000);
                    ambientLightSensor.ReadingChanged += LightSensor_ReadingChanged;
                    ambientLightPollTimer = new System.Windows.Forms.Timer { Interval = 2000 };
                    ambientLightPollTimer.Tick += (s, e) => PollLightSensor();
                    ambientLightPollTimer.Start();
                }
            }
            catch { }
        }

        private int InterpolateCurve(string key, float lux, int defaultMax)
        {
            int[][]? points = key == "screen_curve" ? _cachedScreenCurve : _cachedKeyboardCurve;
            if (points == null)
            {
                string? saved = AppConfig.GetString(key);
                if (!string.IsNullOrEmpty(saved))
                {
                    try { points = System.Text.Json.JsonSerializer.Deserialize<int[][]>(saved); } catch { }
                }
                if (points == null || points.Length == 0)
                {
                    if (defaultMax == 100)
                        points = new int[][] { new int[] { 0, 20 }, new int[] { 100, 50 }, new int[] { 500, 100 } };
                    else
                        points = new int[][] { new int[] { 0, 3 }, new int[] { 50, 2 }, new int[] { 200, 1 }, new int[] { 500, 0 } };
                }
                if (key == "screen_curve") _cachedScreenCurve = points;
                else _cachedKeyboardCurve = points;
            }

            if (lux <= points[0][0]) return points[0][1];
            if (lux >= points[points.Length - 1][0]) return points[points.Length - 1][1];

            for (int i = 0; i < points.Length - 1; i++)
            {
                if (lux >= points[i][0] && lux <= points[i + 1][0])
                {
                    int span = points[i + 1][0] - points[i][0];
                    if (span <= 0) return points[i][1];
                    float t = (lux - points[i][0]) / (float)span;
                    return (int)Math.Round(points[i][1] + t * (points[i + 1][1] - points[i][1]));
                }
            }
            return points[0][1];
        }

        public void TriggerAlsUpdate()
        {
            PollLightSensor(force: true);
        }

        private bool ShouldSkipAlsPoll(bool force)
        {
            if (force) return false;
            if (InputDispatcher.lidClose || Aura.sessionLock || _displayOff) return true;
            if (!Visible && AppConfig.Get("auto_screen_brightness") != 1 && AppConfig.Get("auto_keyboard_brightness") != 1) return true;
            return false;
        }

        private void PollLightSensor(bool force = false)
        {
            if (ambientLightSensor == null || ShouldSkipAlsPoll(force)) return;
            try
            {
                var reading = ambientLightSensor.GetCurrentReading();
                if (reading != null)
                {
                    ProcessLightReading(reading.IlluminanceInLux, force);
                }
            }
            catch { }
        }

        private void LightSensor_ReadingChanged(Windows.Devices.Sensors.LightSensor sender, Windows.Devices.Sensors.LightSensorReadingChangedEventArgs args)
        {
            if (ShouldSkipAlsPoll(false)) return;
            try
            {
                if (args?.Reading != null)
                {
                    ProcessLightReading(args.Reading.IlluminanceInLux, force: false);
                }
            }
            catch { }
        }

        private void ProcessLightReading(float rawLux, bool force)
        {
            CurrentLux = rawLux;
            if (_smoothedLux < 0 || force)
                _smoothedLux = rawLux;
            else
                _smoothedLux = (_smoothedLux * 0.65f) + (rawLux * 0.35f);

            float lux = _smoothedLux;
            if (!IsHandleCreated) return;

            this.BeginInvoke(new Action(() =>
            {
                if (AppConfig.Get("auto_screen_brightness") == 1)
                {
                    int newScreenBright = Math.Clamp(InterpolateCurve("screen_curve", lux, 100), 0, 100);
                    int diff = Math.Abs(trackScreenBrightness.Value - newScreenBright);
                    // Require >= 2% change (hysteresis) unless forced or hitting 0%/100% endpoints
                    if (force || diff >= 2 || (diff >= 1 && (newScreenBright == 0 || newScreenBright == 100)))
                    {
                        UpdateBrightnessSlider(newScreenBright);
                        ScreenBrightness.SetAsync(newScreenBright);
                    }
                }
                if (AppConfig.Get("auto_keyboard_brightness") == 1)
                {
                    // Do not turn keyboard backlight back on when folded in Tablet/Tent mode, lid closed, or idle-off
                    if (!InputDispatcher.tabletMode && !InputDispatcher.tentMode && !InputDispatcher.lidClose && InputDispatcher.backlightActivity)
                    {
                        int newKbBright = Math.Clamp(InterpolateCurve("keyboard_curve", lux, 3), 0, 3);
                        if (trackKeyboardBrightness.Value != newKbBright)
                        {
                            try
                            {
                                isProgrammaticBrightnessChange = true;
                                trackKeyboardBrightness.Value = newKbBright;
                            }
                            finally
                            {
                                isProgrammaticBrightnessChange = false;
                            }
                        }
                    }
                }
                UpdateScreenHeaderLabel();
            }));
        }

        private string _baseScreenTitle = "";

        public void UpdateScreenHeaderLabel()
        {
            if (string.IsNullOrEmpty(_baseScreenTitle))
                _baseScreenTitle = Properties.Strings.LaptopScreen;

            string posture = InputDispatcher.tabletMode ? "Tablet" : (InputDispatcher.tentMode ? "Tent" : "Laptop");
            string luxPart = CurrentLux >= 0 ? $" | {Math.Round(CurrentLux)} Lux" : "";
            SetLabelText(labelSreen, $"{_baseScreenTitle}  •  {posture}{luxPart}");
        }

        private static void SetLabelText(Label label, string text)
        {
            if (label.Text != text) label.Text = text;
        }

        public SettingsForm()
        {

            InitializeComponent();
            InitAmbientLightSensor();

            if (ambientLightSensor == null)
            {
                AppConfig.Set("auto_screen_brightness", 0);
                AppConfig.Set("auto_keyboard_brightness", 0);
            }
            else
            {
                Task.Run(() => PollLightSensor());
            }

            this.trackScreenBrightness = new GHelper.UI.Slider();
            this.labelScreenBrightness = new System.Windows.Forms.Label();
            this.trackKeyboardBrightness = new GHelper.UI.Slider();
            this.labelKeyboardBrightness = new System.Windows.Forms.Label();
            this.checkLimitCpuBoost = new System.Windows.Forms.CheckBox() { Visible = false };

            this.trackScreenBrightness.Max = 100;
            this.trackScreenBrightness.Step = 1;
            this.trackScreenBrightness.Min = 0;

            this.trackKeyboardBrightness.Max = 3;
            this.trackKeyboardBrightness.Step = 1;
            this.trackKeyboardBrightness.Min = 0;

            int minLabelWidth = TextRenderer.MeasureText("Brightness: 100%  ", this.Font).Width;
            int sliderRowHeight = Math.Max(28, sliderBattery.Height);
            int smallBtnWidth = Math.Max(56, TextRenderer.MeasureText("Curve  ", this.Font).Width);
            int smallBtnHeight = Math.Max(24, sliderRowHeight - 4);

            // ==== Screen Second Row Switches (Touch/Pen, Rotation, HDR, Auto HDR, Overlay) ====
            buttonTouchscreen.Click += (s, e) =>
            {
                var currentMode = _cachedTouchPenMode ?? TouchPenMode.TouchAndPen;
                var nextMode = currentMode switch
                {
                    TouchPenMode.TouchAndPen => TouchPenMode.PenOnly,
                    TouchPenMode.PenOnly => TouchPenMode.Off,
                    _ => TouchPenMode.TouchAndPen
                };
                ApplyTouchPenModeUI(nextMode);
            };
            buttonTouchscreen.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    var currentMode = _cachedTouchPenMode ?? TouchPenMode.TouchAndPen;
                    var nextMode = currentMode == TouchPenMode.PenOnly
                        ? TouchPenMode.TouchAndPen
                        : TouchPenMode.PenOnly;
                    ApplyTouchPenModeUI(nextMode);
                }
            };
            toolTip.SetToolTip(buttonTouchscreen, "Left-click: Cycle Touch: On → Pen Only (Palm Rejection) → Touch: Off  |  Right-click: Quick Pen Only Mode");

            buttonRotation.Click += (s, e) =>
            {
                bool unlocked = AutoRotationControl.Toggle();
                VisualiseRotation();
                Program.toast.RunToast(unlocked ? "Screen Rotation: Unlocked" : "Screen Rotation: Locked", ToastIcon.Touchpad);
            };
            buttonRotation.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    int nextAuto = AppConfig.Get("auto_tablet_rotation", 1) == 1 ? 0 : 1;
                    AppConfig.Set("auto_tablet_rotation", nextAuto);
                    VisualiseRotation();
                    Program.toast.RunToast(nextAuto == 1 ? "Auto Tablet Rotation: On" : "Auto Tablet Rotation: Off", ToastIcon.Touchpad);
                }
            };
            toolTip.SetToolTip(buttonRotation, "Left-click: Toggle Screen Auto-Rotation Lock  |  Right-click: Toggle Auto-Unlock in Tablet/Tent mode");


            buttonHDR.Click += (s, e) =>
            {
                bool nextHdr = !buttonHDR.Activated;
                buttonHDR.Activated = nextHdr;
                Program.toast.RunToast(nextHdr ? "HDR: On" : "HDR: Off", nextHdr ? ToastIcon.BrightnessUp : ToastIcon.BrightnessDown);
                Task.Run(() =>
                {
                    ScreenCCD.ToggleHDR();
                    ScreenControl.InitScreen();
                });
            };
            toolTip.SetToolTip(buttonHDR, "Toggle Windows Display HDR");

            buttonAutoHDR.Click += (s, e) =>
            {
                bool now = ScreenCCD.ToggleAutoHDR();
                buttonAutoHDR.Activated = now;
                Program.toast.RunToast(now ? "Auto HDR: On" : "Auto HDR: Off", now ? ToastIcon.BrightnessUp : ToastIcon.BrightnessDown);
            };
            toolTip.SetToolTip(buttonAutoHDR, "Toggle Windows 11 Auto HDR for games (DirectX)");

            toolTip.SetToolTip(buttonOverdrive, "Toggle Panel Overdrive (faster pixel response time)");
            toolTip.SetToolTip(buttonOverlay, "Left-click: Configure Overlay  |  Right-click: Toggle Overlay On/Off");

            // ==== Screen Brightness Layout ====
            TableLayoutPanel panelScreenBrightnessContainer = new TableLayoutPanel();
            panelScreenBrightnessContainer.AutoSize = true;
            panelScreenBrightnessContainer.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelScreenBrightnessContainer.Dock = DockStyle.Top;
            panelScreenBrightnessContainer.Margin = new Padding(0);
            panelScreenBrightnessContainer.Padding = new Padding(4, 0, 4, 0);
            panelScreenBrightnessContainer.ColumnCount = 4;
            panelScreenBrightnessContainer.RowCount = 1;
            panelScreenBrightnessContainer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panelScreenBrightnessContainer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panelScreenBrightnessContainer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panelScreenBrightnessContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            panelScreenBrightnessContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, sliderRowHeight));

            labelScreenBrightness.AutoSize = true;
            labelScreenBrightness.MinimumSize = new Size(minLabelWidth, smallBtnHeight);
            labelScreenBrightness.Dock = DockStyle.Fill;
            labelScreenBrightness.Margin = new Padding(4, 0, 6, 0);
            labelScreenBrightness.TextAlign = ContentAlignment.MiddleLeft;
            labelScreenBrightness.Text = "Brightness:";

            buttonAutoScreen = new GHelper.UI.RButton();
            buttonAutoScreen.Size = new Size(smallBtnWidth, smallBtnHeight);
            buttonAutoScreen.Margin = new Padding(2, 2, 2, 2);
            buttonAutoScreen.BorderRadius = 4;
            buttonAutoScreen.Secondary = true;
            buttonAutoScreen.BorderColor = colorEco;
            buttonAutoScreen.Text = "Auto";
            buttonAutoScreen.Activated = ambientLightSensor != null && AppConfig.Get("auto_screen_brightness") == 1;
            toolTip.SetToolTip(buttonAutoScreen, "Toggle Ambient Light Sensor (ALS) automatic screen brightness (dragging slider manually overrides Auto)");

            GHelper.UI.RButton buttonScreenCurve = new GHelper.UI.RButton();
            buttonScreenCurve.Text = "Curve";
            buttonScreenCurve.Size = new Size(smallBtnWidth, smallBtnHeight);
            buttonScreenCurve.Margin = new Padding(2, 2, 6, 2);
            buttonScreenCurve.BorderRadius = 4;
            buttonScreenCurve.Secondary = true;
            buttonScreenCurve.BorderColor = colorStandard;
            toolTip.SetToolTip(buttonScreenCurve, "Customize Ambient Light Sensor (Lux) response curves for Screen & Keyboard");
            buttonScreenCurve.Click += (s, e) => { new BrightnessCurve().ShowDialog(this); };

            trackScreenBrightness.Dock = DockStyle.Fill;
            trackScreenBrightness.Margin = new Padding(0, 1, 0, 1);
            trackScreenBrightness.Size = new Size(200, sliderRowHeight - 2);
            trackScreenBrightness.Enabled = true;
            trackScreenBrightness.accentColor = buttonAutoScreen.Activated ? colorEco : colorStandard;

            buttonAutoScreen.Click += (s, e) => {
                if (ambientLightSensor == null) {
                    MessageBox.Show("No Ambient Light Sensor detected by Windows on this device.", "Sensor Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                int next = AppConfig.Get("auto_screen_brightness") == 1 ? 0 : 1;
                AppConfig.Set("auto_screen_brightness", next);
                buttonAutoScreen.Activated = next == 1;
                buttonAutoScreen.Invalidate();
                trackScreenBrightness.accentColor = buttonAutoScreen.Activated ? colorEco : colorStandard;
                trackScreenBrightness.Invalidate();
                if (buttonAutoScreen.Activated) Task.Run(() => PollLightSensor(force: true));
            };

            if (ambientLightSensor == null) {
                buttonAutoScreen.Visible = false;
                buttonScreenCurve.Visible = false;
                trackScreenBrightness.Enabled = true;
            }
            panelScreenBrightnessContainer.Controls.Add(labelScreenBrightness, 0, 0);
            panelScreenBrightnessContainer.Controls.Add(buttonAutoScreen, 1, 0);
            panelScreenBrightnessContainer.Controls.Add(buttonScreenCurve, 2, 0);
            panelScreenBrightnessContainer.Controls.Add(trackScreenBrightness, 3, 0);

            this.panelScreen.Controls.Add(panelScreenBrightnessContainer);
            this.panelScreen.Controls.SetChildIndex(panelScreenBrightnessContainer, 0);

            // ==== Keyboard Brightness Layout ====
            TableLayoutPanel panelKeyboardBrightnessContainer = new TableLayoutPanel();
            panelKeyboardBrightnessContainer.AutoSize = true;
            panelKeyboardBrightnessContainer.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelKeyboardBrightnessContainer.Dock = DockStyle.Top;
            panelKeyboardBrightnessContainer.Margin = new Padding(0);
            panelKeyboardBrightnessContainer.Padding = new Padding(4, 0, 4, 0);
            panelKeyboardBrightnessContainer.ColumnCount = 4;
            panelKeyboardBrightnessContainer.RowCount = 1;
            panelKeyboardBrightnessContainer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panelKeyboardBrightnessContainer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panelKeyboardBrightnessContainer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panelKeyboardBrightnessContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            panelKeyboardBrightnessContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, sliderRowHeight));

            labelKeyboardBrightness.AutoSize = true;
            labelKeyboardBrightness.MinimumSize = new Size(minLabelWidth, smallBtnHeight);
            labelKeyboardBrightness.Dock = DockStyle.Fill;
            labelKeyboardBrightness.Margin = new Padding(4, 0, 6, 0);
            labelKeyboardBrightness.TextAlign = ContentAlignment.MiddleLeft;
            labelKeyboardBrightness.Text = "Backlight:";
            labelKeyboardBrightness.Cursor = Cursors.Hand;
            labelKeyboardBrightness.Visible = true;
            toolTip.SetToolTip(labelKeyboardBrightness, "Right-click: Cycle Keyboard Backlight Idle Auto-Off Timer (Off / 30s / 60s / 120s)");
            labelKeyboardBrightness.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    bool onBat = SystemInformation.PowerStatus.PowerLineStatus != PowerLineStatus.Online;
                    int curTimeout = onBat ? AppConfig.Get("keyboard_timeout", 60) : AppConfig.Get("keyboard_ac_timeout", 0);
                    int nextTimeout = curTimeout switch
                    {
                        0 => 30,
                        <= 30 => 60,
                        <= 60 => 120,
                        _ => 0
                    };
                    AppConfig.Set("keyboard_timeout", nextTimeout);
                    AppConfig.Set("keyboard_ac_timeout", nextTimeout);
                    Program.inputDispatcher?.InitBacklightTimer();
                    UpdateKeyboardBrightnessLabel(trackKeyboardBrightness.Value);
                    Program.toast.RunToast(nextTimeout > 0 ? $"Keyboard Idle Auto-Off: {nextTimeout}s" : "Keyboard Idle Auto-Off: Off", ToastIcon.BacklightUp);
                }
            };

            buttonAutoKeyboard = new GHelper.UI.RButton();
            buttonAutoKeyboard.Text = "Auto";
            buttonAutoKeyboard.Size = new Size(smallBtnWidth, smallBtnHeight);
            buttonAutoKeyboard.Margin = new Padding(2, 2, 2, 2);
            buttonAutoKeyboard.BorderRadius = 4;
            buttonAutoKeyboard.Secondary = true;
            buttonAutoKeyboard.BorderColor = colorEco;
            buttonAutoKeyboard.Activated = ambientLightSensor != null && AppConfig.Get("auto_keyboard_brightness") == 1;
            toolTip.SetToolTip(buttonAutoKeyboard, "Toggle Ambient Light Sensor (ALS) automatic keyboard backlight (dragging slider manually overrides Auto)");

            GHelper.UI.RButton buttonKeyboardCurve = new GHelper.UI.RButton();
            buttonKeyboardCurve.Text = "Curve";
            buttonKeyboardCurve.Size = new Size(smallBtnWidth, smallBtnHeight);
            buttonKeyboardCurve.Margin = new Padding(2, 2, 6, 2);
            buttonKeyboardCurve.BorderRadius = 4;
            buttonKeyboardCurve.Secondary = true;
            buttonKeyboardCurve.BorderColor = colorStandard;
            toolTip.SetToolTip(buttonKeyboardCurve, "Customize Ambient Light Sensor (Lux) response curves for Screen & Keyboard");
            buttonKeyboardCurve.Click += (s, e) => { new BrightnessCurve().ShowDialog(this); };

            trackKeyboardBrightness.Dock = DockStyle.Fill;
            trackKeyboardBrightness.Margin = new Padding(0, 1, 0, 1);
            trackKeyboardBrightness.Size = new Size(200, sliderRowHeight - 2);
            trackKeyboardBrightness.Enabled = true;
            trackKeyboardBrightness.accentColor = buttonAutoKeyboard.Activated ? colorEco : colorStandard;

            buttonAutoKeyboard.Click += (s, e) => {
                if (ambientLightSensor == null) {
                    MessageBox.Show("No Ambient Light Sensor detected by Windows on this device.", "Sensor Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                int next = AppConfig.Get("auto_keyboard_brightness") == 1 ? 0 : 1;
                AppConfig.Set("auto_keyboard_brightness", next);
                buttonAutoKeyboard.Activated = next == 1;
                buttonAutoKeyboard.Invalidate();
                trackKeyboardBrightness.accentColor = buttonAutoKeyboard.Activated ? colorEco : colorStandard;
                trackKeyboardBrightness.Invalidate();
                if (buttonAutoKeyboard.Activated) Task.Run(() => PollLightSensor(force: true));
            };

            if (ambientLightSensor == null) {
                buttonAutoKeyboard.Visible = false;
                buttonKeyboardCurve.Visible = false;
                trackKeyboardBrightness.Enabled = true;
            }
            panelKeyboardBrightnessContainer.Controls.Add(labelKeyboardBrightness, 0, 0);
            panelKeyboardBrightnessContainer.Controls.Add(buttonAutoKeyboard, 1, 0);
            panelKeyboardBrightnessContainer.Controls.Add(buttonKeyboardCurve, 2, 0);
            panelKeyboardBrightnessContainer.Controls.Add(trackKeyboardBrightness, 3, 0);

            this.panelKeyboard.Controls.Add(panelKeyboardBrightnessContainer);
            this.panelKeyboard.Controls.SetChildIndex(panelKeyboardBrightnessContainer, 1);

            // ==== Battery Longevity & Power-Saver Bar (5 Buttons matching Screen Buttons) ====
            toolTip.SetToolTip(buttonPauseCharge, "Stop charging when plugged in (sets EC limit to 40% for AC bypass when battery > 40%)");
            buttonPauseCharge.Click += (s, e) =>
            {
                BatteryControl.ToggleChargePause();
                VisualiseBatteryOptions();
            };

            toolTip.SetToolTip(buttonSmartCharge, "Left-click: Toggle Smart Charge Threshold (only charge when battery drops below threshold; stays on AC bypass above it)  |  Right-click: Cycle threshold (<50% / <60% / <70%)");
            buttonSmartCharge.Click += (s, e) =>
            {
                BatteryControl.ToggleSmartCharge();
                VisualiseBatteryOptions();
            };
            buttonSmartCharge.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    BatteryControl.CycleSmartChargeMin();
                    VisualiseBatteryOptions();
                }
            };

            toolTip.SetToolTip(buttonBatteryBoost, "Left-click: Disable CPU Turbo Boost on battery  |  Right-click: Toggle Ultra-Eco Travel Mode (Silent + Eco GPU + 60Hz + 1-Zone + No Boost + Power Saver)");
            buttonBatteryBoost.Click += (s, e) =>
            {
                int next = AppConfig.Is("limit_cpu_boost_battery") ? 0 : 1;
                AppConfig.Set("limit_cpu_boost_battery", next);
                VisualiseBatteryOptions();
                Task.Run(() =>
                {
                    if (Program.modeControl != null) Program.modeControl.SetPerformanceMode();
                });
            };
            buttonBatteryBoost.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    TravelMode.Toggle();
                }
            };

            toolTip.SetToolTip(buttonBatteryMiniled, "Automatically switch Mini-LED to Single-Zone on battery (saves 4–6W) and Multi-Zone on AC");
            buttonBatteryMiniled.Click += (s, e) =>
            {
                int next = AppConfig.Is("auto_miniled") ? 0 : 1;
                AppConfig.Set("auto_miniled", next);
                VisualiseBatteryOptions();
                Task.Run(() => ScreenControl.AutoScreen());
            };

            toolTip.SetToolTip(buttonBatteryHDR, "Automatically disable Windows HDR on battery (saves 5–8W) and restore HDR on AC");
            buttonBatteryHDR.Click += (s, e) =>
            {
                int next = AppConfig.Is("hdr_battery_off") ? 0 : 1;
                AppConfig.Set("hdr_battery_off", next);
                VisualiseBatteryOptions();
                Task.Run(() => ScreenControl.AutoScreen());
            };

            InitTheme(true);
            checkStartup.BackColor = RForm.formBack;
            VisualiseBatteryOptions();

            gpuControl = new GPUModeControl(this);
            updateControl = new AutoUpdateControl(this);
            matrixControl = new AniMatrixControl(this);
            allyControl = new AllyControl(this);

            buttonSilent.Text = Properties.Strings.Silent;
            buttonBalanced.Text = Properties.Strings.Balanced;
            buttonTurbo.Text = Properties.Strings.Turbo;
            buttonFans.Text = Properties.Strings.FansPower;

            buttonEco.Text = Properties.Strings.EcoMode;
            buttonUltimate.Text = Properties.Strings.UltimateMode;
            buttonStandard.Text = Properties.Strings.StandardMode;
            buttonOptimized.Text = Properties.Strings.Optimized;
            buttonStopGPU.Text = Properties.Strings.StopGPUApps;

            buttonScreenAuto.Text = Properties.Strings.AutoMode;
            buttonOverdrive.Text = Properties.Strings.Overdrive;
            buttonMiniled.Text = Properties.Strings.Multizone;

            buttonKeyboardColor.Text = Properties.Strings.Color;
            buttonKeyboard.Text = Properties.Strings.Extra;

            labelPerf.Text = Properties.Strings.PerformanceMode;
            labelGPU.Text = Properties.Strings.GPUMode;
            labelSreen.Text = Properties.Strings.LaptopScreen;
            UpdateKeyboardLabel();
            labelMatrix.Text = Properties.Strings.AnimeMatrix;
            labelBatteryTitle.Text = Properties.Strings.BatteryChargeLimit;

            checkStartup.Text = Properties.Strings.RunOnStartup;

            buttonMatrix.Text = "Matrix";
            buttonQuit.Text = Properties.Strings.Quit;
            buttonUpdates.Text = Properties.Strings.Updates;
            buttonDonate.Text = Properties.Strings.Donate;

            buttonController.Text = Properties.Strings.Controller;
            labelAlly.Text = Properties.Strings.AllyController;

            // Accessible Labels

            panelMatrix.AccessibleName = Properties.Strings.AnimeMatrix;
            sliderBattery.AccessibleName = Properties.Strings.BatteryChargeLimit;
            buttonQuit.AccessibleName = Properties.Strings.Quit;
            buttonUpdates.AccessibleName = Properties.Strings.BiosAndDriverUpdates;
            panelPerformance.AccessibleName = Properties.Strings.PerformanceMode;
            buttonSilent.AccessibleName = Properties.Strings.Silent;
            buttonBalanced.AccessibleName = Properties.Strings.Balanced;
            buttonTurbo.AccessibleName = Properties.Strings.Turbo;
            buttonFans.AccessibleName = Properties.Strings.FansAndPower;
            panelGPU.AccessibleName = Properties.Strings.GPUMode;
            buttonEco.AccessibleName = Properties.Strings.EcoMode;
            buttonStandard.AccessibleName = Properties.Strings.StandardMode;
            buttonOptimized.AccessibleName = Properties.Strings.Optimized;
            buttonUltimate.AccessibleName = Properties.Strings.UltimateMode;
            panelScreen.AccessibleName = Properties.Strings.LaptopScreen;

            buttonScreenAuto.AccessibleName = Properties.Strings.AutoMode;

            panelKeyboard.AccessibleName = Properties.Strings.LaptopKeyboard;
            buttonKeyboard.AccessibleName = Properties.Strings.ExtraSettings;
            buttonKeyboardColor.AccessibleName = Properties.Strings.LaptopKeyboard + " " + Properties.Strings.Color;
            comboKeyboard.AccessibleName = Properties.Strings.LaptopBacklight;

            FormClosing += SettingsForm_FormClosing;
            Deactivate += SettingsForm_LostFocus;
            Activated += SettingsForm_Focused;

            buttonSilent.BorderColor = colorEco;
            buttonBalanced.BorderColor = colorStandard;
            buttonTurbo.BorderColor = colorTurbo;
            buttonFans.BorderColor = colorCustom;

            buttonEco.BorderColor = colorEco;
            buttonStandard.BorderColor = colorStandard;
            buttonUltimate.BorderColor = colorTurbo;
            buttonOptimized.BorderColor = colorEco;
            buttonXGM.BorderColor = colorTurbo;

            button60Hz.BorderColor = colorGray;
            button120Hz.BorderColor = colorGray;
            buttonScreenAuto.BorderColor = colorGray;
            buttonOverdrive.BorderColor = colorTurbo;
            buttonMiniled.BorderColor = colorTurbo;

            buttonTouchscreen.BorderColor = colorEco;
            buttonRotation.BorderColor = colorStandard;
            buttonHDR.BorderColor = colorTurbo;
            buttonAutoHDR.BorderColor = colorStandard;
            buttonOverlay.BorderColor = colorEco;

            buttonFnLock.BorderColor = colorStandard;
            buttonMicMute.BorderColor = colorTurbo;
            buttonPowerSaver.BorderColor = colorEco;

            buttonPauseCharge.BorderColor = colorStandard;
            buttonSmartCharge.BorderColor = colorStandard;
            buttonBatteryBoost.BorderColor = colorEco;
            buttonBatteryMiniled.BorderColor = colorEco;
            buttonBatteryHDR.BorderColor = colorEco;

            buttonEnergySaver.BackColor = colorEco;
            buttonEnergySaver.ForeColor = SystemColors.ControlLightLight;
            buttonEnergySaver.Click += ButtonEnergySaver_Click;

            buttonAmdOled.BackColor = colorTurbo;
            buttonAmdOled.ForeColor = SystemColors.ControlLightLight;
            buttonAmdOled.Click += ButtonAmdOled_Click;

            buttonArmoury.BackColor = colorTurbo;
            buttonArmoury.ForeColor = SystemColors.ControlLightLight;
            buttonArmoury.Click += ButtonArmoury_Click;

            buttonSilent.Click += ButtonSilent_Click;
            toolTip.SetToolTip(buttonSilent, "Left-click: Silent Mode  |  Right-click: Toggle Auto-Silent Mode when folded into Tablet Mode");
            buttonSilent.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    int next = AppConfig.Get("auto_tablet_silent", 1) == 1 ? 0 : 1;
                    AppConfig.Set("auto_tablet_silent", next);
                    Program.toast.RunToast(next == 1 ? "Auto Tablet Silent Mode: On" : "Auto Tablet Silent Mode: Off", ToastIcon.Touchpad);
                }
            };
            buttonBalanced.Click += ButtonBalanced_Click;
            buttonTurbo.Click += ButtonTurbo_Click;

            buttonEco.Click += ButtonEco_Click;
            buttonStandard.Click += ButtonStandard_Click;
            buttonUltimate.Click += ButtonUltimate_Click;
            buttonOptimized.Click += ButtonOptimized_Click;
            buttonStopGPU.Click += ButtonStopGPU_Click;
            pictureGPU.Click += PictureGPU_Click;

            VisibleChanged += SettingsForm_VisibleChanged;

            button60Hz.Click += Button60Hz_Click;
            button120Hz.Click += Button120Hz_Click;
            buttonScreenAuto.Click += ButtonScreenAuto_Click;
            buttonOverdrive.Click += ButtonOverdrive_Click;
            buttonMiniled.Click += ButtonMiniled_Click;
            buttonFHD.Click += ButtonFHD_Click;
            buttonHDRControl.Click += ButtonHDRControl_Click;

            buttonQuit.Click += ButtonQuit_Click;

            buttonKeyboardColor.Click += ButtonKeyboardColor_Click;
            buttonKeyboardColor.Swatch2Click += ButtonKeyboardColor2_Click;

            buttonFans.Click += ButtonFans_Click;
            buttonKeyboard.Click += ButtonKeyboard_Click;
            buttonController.Click += ButtonHandheld_Click;

            labelCPUFan.Click += LabelCPUFan_Click;
            labelGPUFan.Click += LabelCPUFan_Click;
            labelMidFan.Click += LabelCPUFan_Click;
            labelXGMFan.Click += LabelCPUFan_Click;
            tableFans.Click += LabelCPUFan_Click;

            string fanTip = "Left-click: Toggle Fan RPM / %  |  Right-click: Trim Standby RAM";
            toolTip.SetToolTip(labelCPUFan, fanTip);
            toolTip.SetToolTip(labelGPUFan, fanTip);
            toolTip.SetToolTip(labelMidFan, fanTip);

            MouseEventHandler fanRightClick = (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    Task.Run(() =>
                    {
                        long freedMB = MemoryCleaner.TrimWorkingSet();
                        Program.toast.RunToast($"RAM Trimmed: Freed ~{freedMB} MB");
                    });
                }
            };
            labelCPUFan.MouseDown += fanRightClick;
            labelGPUFan.MouseDown += fanRightClick;
            labelMidFan.MouseDown += fanRightClick;
            labelXGMFan.MouseDown += fanRightClick;
            tableFans.MouseDown += fanRightClick;

            comboMatrix.DropDownStyle = ComboBoxStyle.DropDownList;
            comboMatrixRunning.DropDownStyle = ComboBoxStyle.DropDownList;

            comboMatrix.DropDownClosed += ComboMatrix_SelectedValueChanged;
            comboMatrixRunning.DropDownClosed += ComboMatrixRunning_SelectedValueChanged;

            buttonMatrix.Click += ButtonMatrix_Click;

            checkStartup.Checked = Startup.IsScheduled();
            checkStartup.CheckedChanged += CheckStartup_CheckedChanged;

            labelVersion.Click += LabelVersion_Click;
            labelVersion.ForeColor = Color.FromArgb(128, Color.Gray);

            toolTip.SetToolTip(buttonOptimized, Properties.Strings.OptimizedGPUTooltip);
            toolTip.SetToolTip(buttonEco, Properties.Strings.EcoGPUTooltip);
            toolTip.SetToolTip(buttonStandard, Properties.Strings.StandardGPUTooltip);
            toolTip.SetToolTip(buttonUltimate, Properties.Strings.UltimateGPUTooltip);
            toolTip.SetToolTip(buttonScreenAuto, Properties.Strings.AutoRefreshTooltip.Replace("60", ScreenControl.MIN_RATE.ToString()));
            toolTip.SetToolTip(button60Hz, Properties.Strings.MinRefreshTooltip.Replace("60", ScreenControl.MIN_RATE.ToString()));
            toolTip.SetToolTip(button120Hz, Properties.Strings.MaxRefreshTooltip);

            buttonOptimized.MouseMove += ButtonOptimized_MouseHover;
            buttonOptimized.MouseLeave += ButtonGPU_MouseLeave;

            buttonEco.MouseMove += ButtonEco_MouseHover;
            buttonEco.MouseLeave += ButtonGPU_MouseLeave;

            buttonStandard.MouseMove += ButtonStandard_MouseHover;
            buttonStandard.MouseLeave += ButtonGPU_MouseLeave;

            buttonUltimate.MouseMove += ButtonUltimate_MouseHover;
            buttonUltimate.MouseLeave += ButtonGPU_MouseLeave;

            tableGPU.MouseMove += ButtonXGM_MouseMove;
            tableGPU.MouseLeave += ButtonGPU_MouseLeave;

            buttonXGM.Click += ButtonXGM_Click;

            buttonScreenAuto.MouseMove += ButtonScreenAuto_MouseHover;
            buttonScreenAuto.MouseLeave += ButtonScreen_MouseLeave;

            button60Hz.MouseMove += Button60Hz_MouseHover;
            button60Hz.MouseLeave += ButtonScreen_MouseLeave;

            button120Hz.MouseMove += Button120Hz_MouseHover;
            button120Hz.MouseLeave += ButtonScreen_MouseLeave;

            buttonFHD.MouseMove += ButtonFHD_MouseHover;
            buttonFHD.MouseLeave += ButtonScreen_MouseLeave;

            buttonUpdates.Click += ButtonUpdates_Click;

            sliderBattery.MouseUp += SliderBattery_MouseUp;
            sliderBattery.KeyUp += SliderBattery_KeyUp;
            sliderBattery.ValueChanged += SliderBattery_ValueChanged;
            batteryTimer.Tick += (_, _) => { batteryTimer.Stop(); BatteryControl.SetBatteryChargeLimit(sliderBattery.Value); };
            if (AppConfig.IsChargeLimit6080()) sliderBattery.supportedValues = new() { 60, 65, 70, 75, 80, 100 };

            sensorTimer = new System.Timers.Timer(AppConfig.Get("sensor_timer", 1000));
            sensorTimer.Elapsed += OnTimedEvent;
            sensorTimer.Enabled = sensorsAlways;

            labelCharge.MouseEnter += PanelBattery_MouseEnter;
            labelCharge.MouseLeave += PanelBattery_MouseLeave;
            labelBattery.Click += LabelBattery_Click;

            buttonPeripheral1.Click += ButtonPeripheral_Click;
            buttonPeripheral2.Click += ButtonPeripheral_Click;
            buttonPeripheral3.Click += ButtonPeripheral_Click;

            buttonPeripheral1.MouseEnter += ButtonPeripheral_MouseEnter;
            buttonPeripheral2.MouseEnter += ButtonPeripheral_MouseEnter;
            buttonPeripheral3.MouseEnter += ButtonPeripheral_MouseEnter;

            buttonBatteryFull.MouseEnter += ButtonBatteryFull_MouseEnter;
            buttonBatteryFull.MouseLeave += ButtonBatteryFull_MouseLeave;
            buttonBatteryFull.Click += ButtonBatteryFull_Click;

            buttonControllerMode.Click += ButtonControllerMode_Click;
            buttonBacklight.Click += ButtonBacklight_Click;

            int initialBrightness = ScreenBrightness.Get();
            if (initialBrightness >= 0 && initialBrightness <= 100)
            {
                isProgrammaticBrightnessChange = true;
                trackScreenBrightness.Value = initialBrightness;
                labelScreenBrightness.Text = $"{Properties.Strings.Brightness}: {initialBrightness}%";
                isProgrammaticBrightnessChange = false;
                AppConfig.Set("screen_brightness", initialBrightness);
            }
            else
            {
                int savedBrightness = AppConfig.Get("screen_brightness", -1);
                if (savedBrightness >= 0)
                {
                    isProgrammaticBrightnessChange = true;
                    trackScreenBrightness.Value = savedBrightness;
                    labelScreenBrightness.Text = $"{Properties.Strings.Brightness}: {savedBrightness}%";
                    isProgrammaticBrightnessChange = false;
                }
            }

            int initialKb = InputDispatcher.GetBacklight();
            trackKeyboardBrightness.Value = Math.Clamp(initialKb, 0, 3);
            UpdateKeyboardBrightnessLabel(initialKb);

            
            trackScreenBrightness.ValueChanged += TrackScreenBrightness_ValueChanged;
            trackKeyboardBrightness.ValueChanged += TrackKeyboardBrightness_ValueChanged;

            buttonFPS.Click += ButtonFPS_Click;
            buttonOverlay.Click += ButtonOverlay_Click;
            buttonOverlay.MouseUp += (s, e) => { if (e.Button == MouseButtons.Right) ToggleOverlay(); };
            buttonOverlay.Text = Properties.Strings.Overlay;
            VisualiseOverlay();

            buttonAutoTDP.Click += ButtonAutoTDP_Click;
            buttonAutoTDP.BorderColor = colorTurbo;

            Text = "G-Helper " + (ProcessHelper.IsUserAdministrator() ? "—" : "-") + " " + AppConfig.GetModelShort();
            TopMost = AppConfig.Is("topmost");

            //This will auto position the window again when it resizes. Might mess with position if people drag the window somewhere else.
            this.Resize += SettingsForm_Resize;

            VisualiseFnLock();
            buttonFnLock.Click += ButtonFnLock_Click;

            toolTip.SetToolTip(buttonMicMute, "Left-click: Toggle Microphone Mute (+ ASUS Mic LED)  |  Right-click: Cycle Audio Output Device (Speakers / Headphones)");
            buttonMicMute.Click += ButtonMicMute_Click;
            buttonMicMute.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    Task.Run(() =>
                    {
                        string? nextOut = Audio.CycleAudioOutput();
                        if (nextOut != null)
                        {
                            Program.toast.RunToast("Audio: " + nextOut, ToastIcon.Microphone);
                            if (IsHandleCreated) BeginInvoke(VisualiseMicAndPowerSaver);
                        }
                    });
                }
            };

            toolTip.SetToolTip(buttonPowerSaver, "Left-click: Toggle Windows Power Saver & Energy Saver  |  Right-click: Cycle Keep Awake / Caffeine Timer (30m → 1h → 2h → ∞ → Off)");
            buttonPowerSaver.Click += ButtonPowerSaver_Click;
            buttonPowerSaver.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    string msg = PowerNative.CycleKeepAwake();
                    Program.toast.RunToast(msg, ToastIcon.Battery);
                    VisualiseMicAndPowerSaver();
                    SetContextMenu();
                }
            };
            VisualiseMicAndPowerSaver();

            labelVisual.Click += LabelVisual_Click;
            labelCharge.Click += LabelCharge_Click;

            donateControl = new DonateControl(this, buttonDonate);
            donateControl.Init();

            labelBacklight.ForeColor = colorStandard;
            labelBacklight.Click += LabelBacklight_Click;

            panelPerformance.Focus();
            InitVisual();
        }

        private void ButtonArmoury_Click(object? sender, EventArgs e)
        {
            var dialogResult = ShowMessage("Armoury Crate is active, download official uninstaller app?", "Armoury Crate", MessageBoxButtons.YesNo);
            if (dialogResult == DialogResult.Yes) AsusService.RunArmouryUninstaller();
        }


        private void ButtonAmdOled_Click(object? sender, EventArgs e)
        {
            AmdDisplay.RunAdrenaline();
            activateCheck = true;
        }

        private void LabelBattery_Click(object? sender, EventArgs e)
        {
            HardwareControl.chargeWatt = !HardwareControl.chargeWatt;
            RefreshSensors(true);
        }

        private void ButtonEnergySaver_Click(object? sender, EventArgs e)
        {
            KeyboardHook.KeyKeyPress(Keys.LWin, Keys.A);
        }

        private void LabelBacklight_Click(object? sender, EventArgs e)
        {
            if (AppConfig.IsDynamicLighting() && DynamicLightingHelper.IsEnabled()) DynamicLightingHelper.OpenSettings();
        }

        private void ButtonFHD_Click(object? sender, EventArgs e)
        {
            ScreenControl.ToogleFHD();
        }

        private void ButtonHDRControl_Click(object? sender, EventArgs e)
        {
            ScreenControl.ToogleHDRControl();
        }

        private void SliderBattery_ValueChanged(object? sender, EventArgs e)
        {
            VisualiseBatteryTitle(sliderBattery.Value);
        }

        private void SliderBattery_KeyUp(object? sender, KeyEventArgs e)
        {
            batteryTimer.Stop();
            batteryTimer.Start();
        }

        private void SliderBattery_MouseUp(object? sender, MouseEventArgs e)
        {
            batteryTimer.Stop();
            batteryTimer.Start();
        }

        private void ButtonAutoTDP_Click(object? sender, EventArgs e)
        {
            allyControl.ToggleAutoTDP();
        }

        private void LabelCharge_Click(object? sender, EventArgs e)
        {
            BatteryControl.BatteryReport();
        }

        private void LabelVisual_Click(object? sender, EventArgs e)
        {
            labelVisual.Visible = false;
            tableVisual.Visible = true;
            VisualControl.forceVisual = true;
        }

        public void InitVisual()
        {

            if (AppConfig.Is("hide_visual")) return;

            if (AppConfig.IsOLED())
            {
                panelGamma.Visible = true;
                panelGammaTitle.Visible = true;
                sliderGamma.Visible = true;
                labelGammaTitle.Text = Properties.Strings.FlickerFreeDimming + " / " + Properties.Strings.VisualMode;

                VisualiseBrightness();

                sliderGamma.ValueChanged += SliderGamma_ValueChanged;
                sliderGamma.MouseUp += SliderGamma_ValueChanged;

            }
            else
            {
                panelGammaTitle.Visible = false;
                labelGammaTitle.Text = Properties.Strings.VisualMode;
            }

            var gamuts = VisualControl.GetGamutModes();

            // Color profiles exist
            if (gamuts.Count > 0)
            {
                tableVisual.ColumnCount = 3;
                buttonInstallColor.Visible = false;
            }
            else
            {
                // If it's possible to retrieve color profiles
                if (ColorProfileHelper.ProfileExists())
                {
                    tableVisual.ColumnCount = 2;

                    buttonInstallColor.Text = Properties.Strings.DownloadColorProfiles;
                    buttonInstallColor.Visible = true;
                    buttonInstallColor.Click += ButtonInstallColorProfile_Click;

                    panelGamma.Visible = true;
                    tableVisual.Visible = true;
                }

                return;
            }

            panelGamma.Visible = true;
            tableVisual.Visible = true;

            var visualValue = (SplendidCommand)AppConfig.Get("visual", (int)VisualControl.GetDefaultVisualMode());
            var colorTempValue = AppConfig.Get("color_temp", VisualControl.DefaultColorTemp);

            comboVisual.DropDownStyle = ComboBoxStyle.DropDownList;
            comboVisual.DataSource = new BindingSource(VisualControl.GetVisualModes(), null);
            comboVisual.DisplayMember = "Value";
            comboVisual.ValueMember = "Key";
            comboVisual.SelectedValue = visualValue;

            comboColorTemp.DropDownStyle = ComboBoxStyle.DropDownList;
            comboColorTemp.DataSource = new BindingSource(VisualControl.GetTemperatures(), null);
            comboColorTemp.DisplayMember = "Value";
            comboColorTemp.ValueMember = "Key";
            comboColorTemp.SelectedValue = colorTempValue;

            VisualControl.SetVisual(visualValue, colorTempValue, true);

            comboVisual.SelectedValueChanged += ComboVisual_SelectedValueChanged;
            comboVisual.Visible = true;
            VisualiseDisabled();

            comboColorTemp.SelectedValueChanged += ComboVisual_SelectedValueChanged;
            comboColorTemp.Visible = true;

            if (gamuts.Count <= 1) return;

            comboGamut.DropDownStyle = ComboBoxStyle.DropDownList;
            comboGamut.DataSource = new BindingSource(gamuts, null);
            comboGamut.DisplayMember = "Value";
            comboGamut.ValueMember = "Key";
            comboGamut.SelectedValue = (SplendidGamut)AppConfig.Get("gamut", (int)VisualControl.GetDefaultGamut());

            comboGamut.SelectedValueChanged += ComboGamut_SelectedValueChanged;
            comboGamut.Visible = true;

        }

        public void CycleVisualMode(int delta)
        {

            if (comboVisual.Items.Count < 1) return;

            if (delta > 0)
            {
                if (comboVisual.SelectedIndex < comboVisual.Items.Count - 1)
                    comboVisual.SelectedIndex += 1;
                else
                    comboVisual.SelectedIndex = 0;
            }
            else
            {
                if (comboVisual.SelectedIndex > 0)
                    comboVisual.SelectedIndex -= 1;
                else
                    comboVisual.SelectedIndex = comboVisual.Items.Count - 1;
            }

            Program.toast.RunToast(comboVisual.GetItemText(comboVisual.SelectedItem), ToastIcon.BrightnessUp);
        }

        private async void ButtonInstallColorProfile_Click(object? sender, EventArgs e)
        {
            await ColorProfileHelper.InstallProfile();
            InitVisual();
        }

        private void ComboGamut_SelectedValueChanged(object? sender, EventArgs e)
        {
            VisualControl.SetGamut((int)comboGamut.SelectedValue);
        }

        private void ComboVisual_SelectedValueChanged(object? sender, EventArgs e)
        {
            VisualControl.SetVisual((SplendidCommand)comboVisual.SelectedValue, (int)comboColorTemp.SelectedValue);
            VisualiseDisabled();
        }

        public void VisualiseBrightness()
        {
            if (InvokeRequired) { Invoke(VisualiseBrightness); return; }
            sliderGammaIgnore = true;
            sliderGamma.Value = VisualControl.GetBrightness();
            labelGamma.Text = sliderGamma.Value + "%";
            sliderGammaIgnore = false;
        }

        public void VisualiseAmdOled(bool status = false)
        {
            if (InvokeRequired) { Invoke(() => VisualiseAmdOled(status)); return; }
            buttonAmdOled.Visible = status;
        }

        public void VisualiseArmoury(bool status = false)
        {
            if (InvokeRequired) { Invoke(() => VisualiseArmoury(status)); return; }
            buttonArmoury.Visible = status;
        }

        public void VisualiseDisabled()
        {
            comboGamut.Enabled = comboColorTemp.Enabled = (SplendidCommand)AppConfig.Get("visual") != SplendidCommand.Disabled;
        }

        public void VisualiseGamut()
        {
            if (InvokeRequired) { Invoke(VisualiseGamut); return; }
            if (comboGamut.Items.Count > 0) comboGamut.SelectedIndex = 0;
        }

        private void SliderGamma_ValueChanged(object? sender, EventArgs e)
        {
            if (sliderGammaIgnore) return;
            VisualControl.SetBrightness(sliderGamma.Value);
        }

        private void ButtonOverlay_Click(object? sender, EventArgs e)
        {
            if (overlayForm == null || overlayForm.Text == "")
            {
                overlayForm = new OverlayConfig();
                AddOwnedForm(overlayForm);
            }

            if (overlayForm.Visible)
                overlayForm.Close();
            else
                overlayForm.Show();
        }

        private void ButtonHandheld_Click(object? sender, EventArgs e)
        {
            if (handheldForm == null || handheldForm.Text == "")
            {
                handheldForm = new Handheld();
                AddOwnedForm(handheldForm);
            }

            if (handheldForm.Visible)
            {
                handheldForm.Close();
            }
            else
            {
                //handheldForm.FormPosition();
                handheldForm.Show();
            }
        }

        private void ButtonFPS_Click(object? sender, EventArgs e)
        {
            allyControl.ToggleFPSLimit();
        }

        private void ButtonBacklight_Click(object? sender, EventArgs e)
        {
            allyControl.ToggleBacklight();
        }

        private void ButtonControllerMode_Click(object? sender, EventArgs e)
        {
            allyControl.ToggleMode();
        }

        public void VisualiseAlly(bool visible = false)
        {
            if (!visible) return;
            if (InvokeRequired) { Invoke(() => VisualiseAlly(visible)); return; }

            panelAlly.Visible = true;
            panelKeyboardTitle.Visible = false;
            panelKeyboard.Padding = new Padding(panelKeyboard.Padding.Left, 0, panelKeyboard.Padding.Right, panelKeyboard.Padding.Bottom);

            tableAMD.Visible = true;
        }

        public void VisualiseController(ControllerMode mode)
        {
            switch (mode)
            {
                case ControllerMode.Gamepad:
                    buttonControllerMode.Text = "Gamepad";
                    break;
                case ControllerMode.Mouse:
                    buttonControllerMode.Text = "Mouse";
                    break;
                case ControllerMode.Skip:
                    buttonControllerMode.Text = "Skip";
                    break;
                default:
                    buttonControllerMode.Text = "Auto";
                    break;
            }
        }

        public void VisualiseBacklight(int backlight)
        {
            if (InvokeRequired) { Invoke(() => VisualiseBacklight(backlight)); return; }
            buttonBacklight.Text = Math.Round((double)backlight * 33.33).ToString() + "%";
            trackKeyboardBrightness.ValueChanged -= TrackKeyboardBrightness_ValueChanged;
            trackKeyboardBrightness.Value = Math.Clamp(backlight, 0, 3);
            trackKeyboardBrightness.ValueChanged += TrackKeyboardBrightness_ValueChanged;
            UpdateKeyboardBrightnessLabel(backlight);
        }

        private void UpdateKeyboardBrightnessLabel(int val)
        {
            string[] backlightNames = [Properties.Strings.BacklightOff, Properties.Strings.BacklightLow, Properties.Strings.BacklightMid, Properties.Strings.BacklightMax];
            string name = (val >= 0 && val < backlightNames.Length) ? backlightNames[val] : val.ToString();
            bool onBat = SystemInformation.PowerStatus.PowerLineStatus != PowerLineStatus.Online;
            int timeout = onBat ? AppConfig.Get("keyboard_timeout", 60) : AppConfig.Get("keyboard_ac_timeout", 0);
            string timeoutStr = timeout > 0 ? $" ({timeout}s)" : "";
            labelKeyboardBrightness.Text = $"Backlight: {name}{timeoutStr}";
        }

        private void TrackKeyboardBrightness_ValueChanged(object? sender, EventArgs e)
        {
            if (!isProgrammaticBrightnessChange && AppConfig.Get("auto_keyboard_brightness") == 1)
            {
                AppConfig.Set("auto_keyboard_brightness", 0);
                if (buttonAutoKeyboard != null)
                {
                    buttonAutoKeyboard.Activated = false;
                    buttonAutoKeyboard.Invalidate();
                }
                trackKeyboardBrightness.accentColor = colorStandard;
                trackKeyboardBrightness.Invalidate();
            }

            int val = Math.Clamp(trackKeyboardBrightness.Value, 0, 3);
            bool onBattery = SystemInformation.PowerStatus.PowerLineStatus != PowerLineStatus.Online;
            if (onBattery)
                AppConfig.Set("keyboard_brightness_ac", val);
            else
                AppConfig.Set("keyboard_brightness", val);

            InputDispatcher.backlightActivity = true;
            UpdateKeyboardBrightnessLabel(val);
            buttonBacklight.Text = Math.Round((double)val * 33.33).ToString() + "%";
            if (extraForm != null && extraForm.Text != "") extraForm.VisualiseBacklight(val);
            Task.Run(() => Aura.ApplyBrightness(val, "Slider"));
        }

        public void UpdateBrightnessSlider(int brightness)
        {
            if (InvokeRequired) { Invoke(() => UpdateBrightnessSlider(brightness)); return; }
            if (brightness < 0 || brightness > 100) return;
            try
            {
                isProgrammaticBrightnessChange = true;
                trackScreenBrightness.Value = brightness;
                labelScreenBrightness.Text = $"{Properties.Strings.Brightness}: {brightness}%";
            }
            catch (Exception ex)
            {
                Logger.WriteLine("UpdateBrightnessSlider error: " + ex.Message);
            }
            finally
            {
                isProgrammaticBrightnessChange = false;
            }
        }

        private void DisengageAutoScreenIfManual()
        {
            if (!isProgrammaticBrightnessChange && AppConfig.Get("auto_screen_brightness") == 1)
            {
                AppConfig.Set("auto_screen_brightness", 0);
                if (buttonAutoScreen != null)
                {
                    buttonAutoScreen.Activated = false;
                    buttonAutoScreen.Invalidate();
                }
                trackScreenBrightness.accentColor = colorStandard;
                trackScreenBrightness.Invalidate();
            }
        }

        private void TrackScreenBrightness_Scroll(object? sender, EventArgs e)
        {
            DisengageAutoScreenIfManual();
            int val = trackScreenBrightness.Value;
            labelScreenBrightness.Text = $"{Properties.Strings.Brightness}: {val}%";
            ScreenBrightness.SetAsync(val);
        }

        private void TrackScreenBrightness_ValueChanged(object? sender, EventArgs e)
        {
            if (isProgrammaticBrightnessChange) return;
            DisengageAutoScreenIfManual();
            int val = trackScreenBrightness.Value;
            labelScreenBrightness.Text = $"{Properties.Strings.Brightness}: {val}%";
            ScreenBrightness.SetAsync(val);
        }

        public void VisualiseFPSLimit(int limit)
        {
            if (InvokeRequired) { Invoke(() => VisualiseFPSLimit(limit)); return; }
            buttonFPS.Text = "FPS Limit " + ((limit > 0 && limit <= 120) ? limit : "OFF");
        }

        public void VisualiseAutoTDP(bool status)
        {
            Logger.WriteLine($"Auto TDP: {status}");
            buttonAutoTDP.Activated = status;
        }

        private void SettingsForm_Focused(object? sender, EventArgs e)
        {
            if (activateCheck)
            {
                buttonAmdOled.Visible = AmdDisplay.IsOledPowerOptimization();
                activateCheck = false;
            }
        }
        private void SettingsForm_LostFocus(object? sender, EventArgs e)
        {
            lastLostFocus = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        private void ButtonBatteryFull_Click(object? sender, EventArgs e)
        {
            BatteryControl.ToggleBatteryLimitFull();
        }

        private void ButtonBatteryFull_MouseLeave(object? sender, EventArgs e)
        {
            batteryFullMouseOver = false;
            RefreshSensors(true);
        }

        private void ButtonBatteryFull_MouseEnter(object? sender, EventArgs e)
        {
            batteryFullMouseOver = true;
            labelCharge.Text = Properties.Strings.BatteryLimitFull;
        }

        private void SettingsForm_Resize(object? sender, EventArgs e)
        {
            if (WindowState != FormWindowState.Normal)
            {
                WindowState = FormWindowState.Normal;
                return;
            }

            Left = Screen.FromControl(this).WorkingArea.Width - 10 - Width;
            Top = Screen.FromControl(this).WorkingArea.Height - 10 - Height;
        }

        private void PanelBattery_MouseEnter(object? sender, EventArgs e)
        {
            batteryMouseOver = true;
            ShowBatteryWear();
        }

        private void PanelBattery_MouseLeave(object? sender, EventArgs e)
        {
            batteryMouseOver = false;
            RefreshSensors(true);
        }

        private async void ShowBatteryWear()
        {
            //Refresh again only after 15 Minutes since the last refresh
            if (lastBatteryRefresh == 0 || Math.Abs(DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastBatteryRefresh) > 15 * 60_000)
            {
                lastBatteryRefresh = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                await Task.Run(HardwareControl.RefreshBatteryHealth);
            }

            if (batteryMouseOver && HardwareControl.batteryHealth != -1)
            {
                labelCharge.Text = !string.IsNullOrEmpty(HardwareControl.batteryHealthStr)
                    ? HardwareControl.batteryHealthStr
                    : (Properties.Strings.BatteryHealth + ": " + Math.Round(HardwareControl.batteryHealth, 1) + "%");
            }
        }

        private void SettingsForm_VisibleChanged(object? sender, EventArgs e)
        {
            sensorTimer.Interval = this.Visible ? AppConfig.Get("sensor_timer_active", 500) : AppConfig.Get("sensor_timer", 2000);
            sensorTimer.Enabled = this.Visible || sensorsAlways;
            if (this.Visible)
            {
                Task.Run(() =>
                {
                    int liveBrightness = ScreenBrightness.Get();
                    if (liveBrightness >= 0) UpdateBrightnessSlider(liveBrightness);
                    if (lastBatteryRefresh == 0 || Math.Abs(DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastBatteryRefresh) > 15 * 60_000)
                    {
                        lastBatteryRefresh = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                        HardwareControl.RefreshBatteryHealth();
                    }
                });
                VisualiseScreenSwitches(ScreenCCD.GetHDRStatus());
                VisualiseBatteryOptions();
                Task.Run((Action)RefreshPeripheralsBattery);
                updateControl.CheckForUpdates();
                RefreshSensors(true);
            }
        }

        private void RefreshPeripheralsBattery()
        {
            PeripheralsProvider.RefreshBatteryForAllDevices(true);
        }

        private void ButtonUpdates_Click(object? sender, EventArgs e)
        {
            if (updatesForm == null || updatesForm.Text == "")
            {
                updatesForm = new Updates();
                AddOwnedForm(updatesForm);
            }

            if (updatesForm.Visible)
            {
                updatesForm.Close();
            }
            else
            {
                updatesForm.Show();
            }
        }

        public void VisualiseMatrixPicture(string image)
        {
            if (matrixForm == null || matrixForm.Text == "") return;
            matrixForm.VisualiseMatrix(image);
        }

        protected override void WndProc(ref Message m)
        {

            if (m.Msg == NativeMethods.WM_POWERBROADCAST && m.WParam == (IntPtr)NativeMethods.PBT_APMSUSPEND)
            {
                Logger.WriteLine("System Suspend");
                GPUModeControl.suspended = true;
                Program.modeControl.SleepReset();
                m.Result = (IntPtr)1;
            }

            if (m.Msg == NativeMethods.WM_POWERBROADCAST && m.WParam == (IntPtr)NativeMethods.PBT_APMRESUMEAUTOMATIC)
            {
                Logger.WriteLine("System Resume");
                GPUModeControl.suspended = false;
                BatteryControl.AutoBattery();
                m.Result = (IntPtr)1;
            }

            if (m.Msg == NativeMethods.WM_POWERBROADCAST && m.WParam == (IntPtr)NativeMethods.PBT_POWERSETTINGCHANGE)
            {
                var settings = (NativeMethods.POWERBROADCAST_SETTING)m.GetLParam(typeof(NativeMethods.POWERBROADCAST_SETTING));
                if (settings.PowerSetting == NativeMethods.PowerSettingGuid.LIDSWITCH_STATE_CHANGE)
                {
                    switch (settings.Data)
                    {
                        case 0:
                            Logger.WriteLine("Lid Closed");
                            BatteryControl.AutoBattery();
                            InputDispatcher.lidClose = AniMatrixControl.lidClose = true;
                            Aura.ApplyBrightness(0, "Lid");
                            matrixControl.SetLidMode();
                            break;
                        case 1:
                            Logger.WriteLine("Lid Open");
                            InputDispatcher.InitFNLock();
                            InputDispatcher.lidClose = AniMatrixControl.lidClose = false;
                            Aura.ApplyBrightness(InputDispatcher.GetBacklight(), "Lid");
                            matrixControl.SetLidMode();
                            Task.Run(() => PollLightSensor(force: true));
                            break;
                    }

                }
                else if (settings.PowerSetting == NativeMethods.PowerSettingGuid.EnergySaverStatus)
                {
                    Logger.WriteLine("Battery Saver: " + settings.Data);
                    buttonEnergySaver.Visible = settings.Data != 0;
                }
                else
                {
                    switch (settings.Data)
                    {
                        case 0:
                            Logger.WriteLine("Monitor Power Off");
                            _displayOff = true;
                            Aura.SleepBrightness();
                            XGM.NotifyShutdown();
                            Program.hardwareOverlay?.SuspendForDisplayOff();
                            break;
                        case 1:
                            Logger.WriteLine("Monitor Power On");
                            _displayOff = false;
                            GPUModeControl.suspended = false;
                            if (!Program.SetAutoModes(wakeup: true)) BatteryControl.AutoBattery();
                            Program.hardwareOverlay?.ResumeForDisplayOn();
                            Task.Run(() => PollLightSensor(force: true));
                            break;
                        case 2:
                            Logger.WriteLine("Monitor Dimmed");
                            break;
                    }
                }
                m.Result = (IntPtr)1;
            }

            if (m.Msg == NativeMethods.WM_DEVICECHANGE)
            {
                int eventType = m.WParam.ToInt32();
                if (eventType == NativeMethods.DBT_DEVICEARRIVAL || eventType == NativeMethods.DBT_DEVICEREMOVECOMPLETE)
                {
                    DebounceDeviceCheck();
                }
            }

            if (m.Msg == Program.WM_TASKBARCREATED)
            {
                Logger.WriteLine("Taskbar created, re-creating tray icon");
                if (Program.trayIcon is not null) Program.trayIcon.Visible = true;
            }

            try
            {
                base.WndProc(ref m);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        private System.Timers.Timer? xgmDebounceTimer;
        private bool wasXgmConnected = false;

        public void DebounceDeviceCheck()
        {
            if (xgmDebounceTimer is null)
            {
                wasXgmConnected = Program.acpi.IsXGConnected() || XGM.IsConnected();
                xgmDebounceTimer = new System.Timers.Timer(1000) { AutoReset = false };
                xgmDebounceTimer.Elapsed += (s, e) =>
                {
                    BeginInvoke(() =>
                    {
                        VisualizeXGM();
                        
                        bool isXgm = Program.acpi.IsXGConnected() || XGM.IsConnected();
                        if (isXgm && !wasXgmConnected)
                        {
                            Battery.BatteryControl.OnXgmConnected();
                        }
                        else if (!isXgm && wasXgmConnected)
                        {
                            Battery.BatteryControl.OnXgmDisconnected();
                        }
                        wasXgmConnected = isXgm;

                        if (XGM.IsConnected())
                        {
                            XGM.Init();
                            if (Program.acpi.IsXGConnected())
                            {
                                if (AppConfig.IsApplyFans())
                                    Program.modeControl.AutoFans();
                                else
                                    XGM.Reset();
                            }
                        }
                        if (fansForm is not null && fansForm.Text != "") fansForm.InitFans();
                    });
                };
            }
            xgmDebounceTimer.Stop();
            xgmDebounceTimer.Start();
        }

        public void SetContextMenu()
        {
            var currentMode = Modes.GetCurrent();

            foreach (ToolStripItem item in contextMenuStrip.Items.Cast<ToolStripItem>().ToList())
            {
                if (item is ToolStripMenuItem menuItem) menuItem.Dispose();
            }
            contextMenuStrip.Items.Clear();
            contextMenuStrip.ShowCheckMargin = true;
            contextMenuStrip.ImageScalingSize = new Size(16, 16);
            contextMenuStrip.ShowImageMargin = false;
            Padding padding = new Padding(5, 5, 5, 5);

            var title = new ToolStripMenuItem(Properties.Strings.PerformanceMode);
            title.Margin = padding;
            title.Enabled = false;
            contextMenuStrip.Items.Add(title);

            foreach (var mode in Modes.GetDictonary())
            {
                var menuMode = new ToolStripMenuItem(mode.Value);
                menuMode.Tag = mode.Key;
                menuMode.Click += (sender, args) => { Program.modeControl.SetPerformanceMode(mode.Key); };
                menuMode.Margin = padding;
                menuMode.Checked = (mode.Key == currentMode);
                contextMenuStrip.Items.Add(menuMode);
            }

            contextMenuStrip.Items.Add("-");

            if (isGpuSection)
            {
                var titleGPU = new ToolStripMenuItem(Properties.Strings.GPUMode);
                titleGPU.Margin = padding;
                titleGPU.Enabled = false;
                contextMenuStrip.Items.Add(titleGPU);

                menuEco = new ToolStripMenuItem(Properties.Strings.EcoMode);
                menuEco.Click += ButtonEco_Click;
                menuEco.Margin = padding;
                menuEco.Checked = buttonEco.Activated;
                contextMenuStrip.Items.Add(menuEco);

                menuStandard = new ToolStripMenuItem(Properties.Strings.StandardMode);
                menuStandard.Click += ButtonStandard_Click;
                menuStandard.Margin = padding;
                menuStandard.Checked = buttonStandard.Activated;
                contextMenuStrip.Items.Add(menuStandard);

                menuUltimate = new ToolStripMenuItem(Properties.Strings.UltimateMode);
                menuUltimate.Click += ButtonUltimate_Click;
                menuUltimate.Margin = padding;
                menuUltimate.Checked = buttonUltimate.Activated;
                menuUltimate.Visible = isMuxGpu;
                contextMenuStrip.Items.Add(menuUltimate);

                menuOptimized = new ToolStripMenuItem(Properties.Strings.Optimized);
                menuOptimized.Click += ButtonOptimized_Click;
                menuOptimized.Margin = padding;
                menuOptimized.Checked = buttonOptimized.Activated;
                contextMenuStrip.Items.Add(menuOptimized);

                contextMenuStrip.Items.Add("-");
            }

            if (Program.acpi.IsXGConnected() || XGM.IsConnected())
            {
                var menuXGM = new ToolStripMenuItem("ROG XG Mobile");
                menuXGM.Margin = padding;
                menuXGM.DropDown.BackColor = this.BackColor;
                menuXGM.DropDown.ForeColor = this.ForeColor;

                var menuXgmLight = new ToolStripMenuItem("LED Light");
                menuXgmLight.Margin = padding;
                menuXgmLight.Checked = !(AppConfig.Get("xmg_light") == 0);
                menuXgmLight.Click += (sender, args) =>
                {
                    menuXgmLight.Checked = !menuXgmLight.Checked;
                    AppConfig.Set("xmg_light", menuXgmLight.Checked ? 1 : 0);
                    int val = menuXgmLight.Checked ? AppConfig.Get("xgm_brightness", 80) : 0;
                    if (val <= 0 && menuXgmLight.Checked) val = 80;
                    XGM.Light(val);
                };
                menuXGM.DropDownItems.Add(menuXgmLight);

                var menuXgmTest = new ToolStripMenuItem("Test Fan (8s)");
                menuXgmTest.Margin = padding;
                menuXgmTest.Click += async (sender, args) =>
                {
                    await XGM.TestFan(8);
                };
                menuXGM.DropDownItems.Add(menuXgmTest);

                menuXGM.DropDownItems.Add("-");

                var menuXgmDisconnect = new ToolStripMenuItem("Safe Disconnect");
                menuXgmDisconnect.Margin = padding;
                menuXgmDisconnect.Click += (sender, args) =>
                {
                    gpuControl.ToggleXGM();
                };
                menuXGM.DropDownItems.Add(menuXgmDisconnect);

                contextMenuStrip.Items.Add(menuXGM);
                contextMenuStrip.Items.Add("-");
            }

            // --- Power & Sleep Submenu ---
            var menuPower = new ToolStripMenuItem("Power & Sleep");
            menuPower.Margin = padding;
            menuPower.DropDown.BackColor = this.BackColor;
            menuPower.DropDown.ForeColor = this.ForeColor;

            var menuSleepNow = new ToolStripMenuItem("Sleep Now") { Margin = padding };
            menuSleepNow.Click += (s, e) =>
            {
                Application.SetSuspendState(PowerState.Suspend, false, false);
            };
            menuPower.DropDownItems.Add(menuSleepNow);

            var menuHibernateNow = new ToolStripMenuItem("Hibernate Now") { Margin = padding };
            menuHibernateNow.Click += (s, e) =>
            {
                Application.SetSuspendState(PowerState.Hibernate, false, false);
            };
            menuPower.DropDownItems.Add(menuHibernateNow);

            menuPower.DropDownItems.Add("-");

            string awakeSuffix = PowerNative.IsKeepAwake ? $" ({PowerNative.GetKeepAwakeShortLabel()})" : "";
            var menuKeepAwake = new ToolStripMenuItem("Keep Awake (Caffeine)" + awakeSuffix)
            {
                Margin = padding,
                Checked = PowerNative.IsKeepAwake
            };
            menuKeepAwake.DropDown.BackColor = this.BackColor;
            menuKeepAwake.DropDown.ForeColor = this.ForeColor;

            var awakePresets = new (int min, string label)[]
            {
                (0, "Off (Allow Normal Sleep)"),
                (30, "30 Minutes"),
                (60, "1 Hour"),
                (120, "2 Hours"),
                (-1, "Always On (∞)")
            };
            foreach (var preset in awakePresets)
            {
                int m = preset.min;
                var item = new ToolStripMenuItem(preset.label)
                {
                    Margin = padding,
                    Checked = (PowerNative.KeepAwakePresetMinutes == m)
                };
                item.Click += (s, e) =>
                {
                    PowerNative.SetKeepAwake(m);
                    VisualiseMicAndPowerSaver();
                    SetContextMenu();
                    string toastText = m == 0 ? "Keep Awake: Off"
                                     : m < 0 ? "Keep Awake: Always On (∞)"
                                     : $"Keep Awake: {m}m Timer";
                    Program.toast?.RunToast(toastText, ToastIcon.Battery);
                };
                menuKeepAwake.DropDownItems.Add(item);
            }
            menuPower.DropDownItems.Add(menuKeepAwake);

            menuPower.DropDownItems.Add("-");

            var lidActions = new (int id, string name)[]
            {
                (1, "Sleep"),
                (0, "Do nothing"),
                (2, "Hibernate"),
                (3, "Shut down")
            };

            var menuLidAC = new ToolStripMenuItem("When Lid Closed (Plugged In)") { Margin = padding };
            menuLidAC.DropDown.BackColor = this.BackColor;
            menuLidAC.DropDown.ForeColor = this.ForeColor;
            int curLidAC = PowerNative.GetLidAction(true);
            foreach (var opt in lidActions)
            {
                var item = new ToolStripMenuItem(opt.name) { Checked = (curLidAC == opt.id), Margin = padding };
                int val = opt.id;
                string titleName = opt.name;
                item.Click += (s, e) =>
                {
                    PowerNative.SetLidAction(val, true);
                    if (val > 0) AppConfig.Set("clamshell_default_lid_action", val);
                    Program.toast?.RunToast($"Lid Closed (Plugged In): {titleName}");
                    SetContextMenu();
                };
                menuLidAC.DropDownItems.Add(item);
            }
            menuPower.DropDownItems.Add(menuLidAC);

            var menuLidDC = new ToolStripMenuItem("When Lid Closed (On Battery)") { Margin = padding };
            menuLidDC.DropDown.BackColor = this.BackColor;
            menuLidDC.DropDown.ForeColor = this.ForeColor;
            int curLidDC = PowerNative.GetLidAction(false);
            foreach (var opt in lidActions)
            {
                var item = new ToolStripMenuItem(opt.name) { Checked = (curLidDC == opt.id), Margin = padding };
                int val = opt.id;
                string titleName = opt.name;
                item.Click += (s, e) =>
                {
                    PowerNative.SetLidAction(val, false);
                    Program.toast?.RunToast($"Lid Closed (On Battery): {titleName}");
                    SetContextMenu();
                };
                menuLidDC.DropDownItems.Add(item);
            }
            menuPower.DropDownItems.Add(menuLidDC);

            menuPower.DropDownItems.Add("-");

            // Adaptive Brightness
            var menuAdaptive = new ToolStripMenuItem("Adaptive Screen Brightness") { Margin = padding };
            menuAdaptive.DropDown.BackColor = this.BackColor;
            menuAdaptive.DropDown.ForeColor = this.ForeColor;

            bool adaptAC = PowerNative.GetAdaptiveBrightness(true);
            var menuAdaptAC = new ToolStripMenuItem("Enable on AC (Plugged In)") { Checked = adaptAC, Margin = padding };
            menuAdaptAC.Click += (s, e) =>
            {
                bool next = !PowerNative.GetAdaptiveBrightness(true);
                PowerNative.SetAdaptiveBrightness(next, true);
                Program.toast?.RunToast(next ? "Adaptive Brightness (AC): On" : "Adaptive Brightness (AC): Off");
                SetContextMenu();
            };
            menuAdaptive.DropDownItems.Add(menuAdaptAC);

            bool adaptDC = PowerNative.GetAdaptiveBrightness(false);
            var menuAdaptDC = new ToolStripMenuItem("Enable on Battery") { Checked = adaptDC, Margin = padding };
            menuAdaptDC.Click += (s, e) =>
            {
                bool next = !PowerNative.GetAdaptiveBrightness(false);
                PowerNative.SetAdaptiveBrightness(next, false);
                Program.toast?.RunToast(next ? "Adaptive Brightness (Battery): On" : "Adaptive Brightness (Battery): Off");
                SetContextMenu();
            };
            menuAdaptive.DropDownItems.Add(menuAdaptDC);

            menuPower.DropDownItems.Add(menuAdaptive);

            // Auto Keyboard Backlight
            bool autoKbd = AppConfig.Is("auto_keyboard_backlight");
            var menuAutoKbd = new ToolStripMenuItem("Auto Keyboard Backlight (Dim on Battery)")
            {
                Checked = autoKbd,
                Margin = padding
            };
            menuAutoKbd.Click += (s, e) =>
            {
                bool next = !AppConfig.Is("auto_keyboard_backlight");
                AppConfig.Set("auto_keyboard_backlight", next ? 1 : 0);
                InputDispatcher.SetBacklightAuto();
                VisualiseBacklight(InputDispatcher.GetBacklight());
                Program.toast?.RunToast(next ? "Auto Keyboard Backlight: On" : "Auto Keyboard Backlight: Off");
                SetContextMenu();
            };
            menuPower.DropDownItems.Add(menuAutoKbd);

            // Dim screen inactivity timeouts
            var menuDimScreen = new ToolStripMenuItem("Dim Screen After (Inactivity)") { Margin = padding };
            menuDimScreen.DropDown.BackColor = this.BackColor;
            menuDimScreen.DropDown.ForeColor = this.ForeColor;

            var dimTimeouts = new (int min, string name)[]
            {
                (1, "1 minute"),
                (2, "2 minutes"),
                (3, "3 minutes"),
                (5, "5 minutes"),
                (10, "10 minutes"),
                (15, "15 minutes"),
                (0, "Never")
            };

            var menuDimAC = new ToolStripMenuItem("Plugged In") { Margin = padding };
            menuDimAC.DropDown.BackColor = this.BackColor;
            menuDimAC.DropDown.ForeColor = this.ForeColor;
            int curDimAC = PowerNative.GetDisplayDimTimeout(true);
            foreach (var opt in dimTimeouts)
            {
                var item = new ToolStripMenuItem(opt.name) { Checked = (curDimAC == opt.min), Margin = padding };
                int m = opt.min;
                string label = opt.name;
                item.Click += (s, e) =>
                {
                    PowerNative.SetDisplayDimTimeout(m, true);
                    Program.toast?.RunToast($"Dim Screen (Plugged In): {label}");
                    SetContextMenu();
                };
                menuDimAC.DropDownItems.Add(item);
            }
            menuDimScreen.DropDownItems.Add(menuDimAC);

            var menuDimDC = new ToolStripMenuItem("On Battery") { Margin = padding };
            menuDimDC.DropDown.BackColor = this.BackColor;
            menuDimDC.DropDown.ForeColor = this.ForeColor;
            int curDimDC = PowerNative.GetDisplayDimTimeout(false);
            foreach (var opt in dimTimeouts)
            {
                var item = new ToolStripMenuItem(opt.name) { Checked = (curDimDC == opt.min), Margin = padding };
                int m = opt.min;
                string label = opt.name;
                item.Click += (s, e) =>
                {
                    PowerNative.SetDisplayDimTimeout(m, false);
                    Program.toast?.RunToast($"Dim Screen (On Battery): {label}");
                    SetContextMenu();
                };
                menuDimDC.DropDownItems.Add(item);
            }
            menuDimScreen.DropDownItems.Add(menuDimDC);

            menuPower.DropDownItems.Add(menuDimScreen);

            menuPower.DropDownItems.Add("-");

            var menuPowerExtra = new ToolStripMenuItem("Power Settings (Extra)...") { Margin = padding };
            menuPowerExtra.Click += (s, e) =>
            {
                if (extraForm == null || extraForm.Text == "")
                {
                    extraForm = new Extra();
                    AddOwnedForm(extraForm);
                }
                extraForm.Show();
                extraForm.BringToFront();
            };
            menuPower.DropDownItems.Add(menuPowerExtra);

            contextMenuStrip.Items.Add(menuPower);

            // --- Battery & Eco ---
            var menuBatteryLimit = new ToolStripMenuItem("Battery Charge Limit");
            menuBatteryLimit.Margin = padding;
            menuBatteryLimit.DropDown.BackColor = this.BackColor;
            menuBatteryLimit.DropDown.ForeColor = this.ForeColor;

            int currentLimit = AppConfig.Get("charge_limit", 85);
            bool isFullCharge = BatteryControl.chargeFull;

            Action<int> updateBatteryLimit = null!;
            updateBatteryLimit = (limit) =>
            {
                if (InvokeRequired)
                {
                    BeginInvoke(() => updateBatteryLimit(limit));
                    return;
                }
                if (limit == 100)
                {
                    sliderBattery.Value = 100;
                    BatteryControl.SetBatteryLimitFull();
                }
                else
                {
                    BatteryControl.chargeFull = false;
                    sliderBattery.Value = limit;
                    BatteryControl.SetBatteryChargeLimit(limit);
                }
                VisualiseBatteryFull();
                SetContextMenu();
                Program.toast?.RunToast($"Battery Limit: {limit}%", ToastIcon.Battery);
            };

            var limitFullOnce = new ToolStripMenuItem("One-Time Full Charge (until unplugged)") { Checked = isFullCharge, Margin = padding };
            limitFullOnce.Click += (s, e) =>
            {
                if (InvokeRequired) { BeginInvoke(() => limitFullOnce.PerformClick()); return; }
                BatteryControl.ToggleBatteryLimitFull();
                VisualiseBatteryFull();
                SetContextMenu();
                Program.toast?.RunToast(BatteryControl.chargeFull ? "One-Time Full Charge: 100%" : $"Battery Limit: {currentLimit}%", ToastIcon.Battery);
            };
            menuBatteryLimit.DropDownItems.Add(limitFullOnce);
            menuBatteryLimit.DropDownItems.Add("-");

            var menuPauseCharge = new ToolStripMenuItem("Pause Charge (AC Bypass)") { Checked = BatteryControl.chargePaused, Margin = padding };
            menuPauseCharge.Click += (s, e) =>
            {
                BatteryControl.ToggleChargePause();
                VisualiseBatteryOptions();
                SetContextMenu();
            };
            menuBatteryLimit.DropDownItems.Add(menuPauseCharge);

            var menuSmartCharge = new ToolStripMenuItem($"Smart Charge (<{BatteryControl.smartChargeMin}%)") { Checked = BatteryControl.smartCharge, Margin = padding };
            menuSmartCharge.Click += (s, e) =>
            {
                BatteryControl.ToggleSmartCharge();
                VisualiseBatteryOptions();
                SetContextMenu();
            };
            menuBatteryLimit.DropDownItems.Add(menuSmartCharge);
            menuBatteryLimit.DropDownItems.Add("-");

            var limit80 = new ToolStripMenuItem("80% (Maximum Longevity)") { Checked = !isFullCharge && currentLimit == 80, Margin = padding };
            limit80.Click += (s, e) => updateBatteryLimit(80);
            menuBatteryLimit.DropDownItems.Add(limit80);

            var limit85 = new ToolStripMenuItem("85% (Balanced Daily)") { Checked = !isFullCharge && currentLimit == 85, Margin = padding };
            limit85.Click += (s, e) => updateBatteryLimit(85);
            menuBatteryLimit.DropDownItems.Add(limit85);

            var limit100 = new ToolStripMenuItem("100% (Full Capacity)") { Checked = isFullCharge || currentLimit == 100, Margin = padding };
            limit100.Click += (s, e) => updateBatteryLimit(100);
            menuBatteryLimit.DropDownItems.Add(limit100);

            contextMenuStrip.Items.Add(menuBatteryLimit);

            var menuTravelMode = new ToolStripMenuItem("Ultra-Eco Travel Mode") { Checked = TravelMode.IsActive, Margin = padding };
            menuTravelMode.Click += (s, e) =>
            {
                TravelMode.Toggle();
            };
            contextMenuStrip.Items.Add(menuTravelMode);

            contextMenuStrip.Items.Add("-");

            // --- Hardware & System Tuning ---
            if (Program.acpi.IsSupported(AsusACPI.ScreenMiniled1) || Program.acpi.IsSupported(AsusACPI.ScreenMiniled2))
            {
                var menuMiniled = new ToolStripMenuItem("Mini-LED Backlight");
                menuMiniled.Margin = padding;
                menuMiniled.DropDown.BackColor = this.BackColor;
                menuMiniled.DropDown.ForeColor = this.ForeColor;

                int currentMiniled = AppConfig.Get("miniled", 1);
                var menuMultiZone = new ToolStripMenuItem("Multi-Zone (HDR / Gaming)") { Checked = currentMiniled == 1, Margin = padding };
                menuMultiZone.Click += (s, e) =>
                {
                    AppConfig.Set("miniled", 1);
                    ScreenControl.SetMiniled(1);
                    ScreenControl.InitScreen();
                    Program.toast?.RunToast("Mini-LED: Multi-Zone", ToastIcon.BrightnessUp);
                };
                menuMiniled.DropDownItems.Add(menuMultiZone);

                var menuSingleZone = new ToolStripMenuItem("Single-Zone (Clarity / Battery)") { Checked = currentMiniled == 0, Margin = padding };
                menuSingleZone.Click += (s, e) =>
                {
                    AppConfig.Set("miniled", 0);
                    ScreenControl.SetMiniled(0);
                    ScreenControl.InitScreen();
                    Program.toast?.RunToast("Mini-LED: Single-Zone", ToastIcon.BrightnessDown);
                };
                menuMiniled.DropDownItems.Add(menuSingleZone);

                var menuAutoMiniled = new ToolStripMenuItem("Auto Single-Zone on Battery") { Checked = AppConfig.Is("auto_miniled"), Margin = padding };
                menuAutoMiniled.Click += (s, e) =>
                {
                    menuAutoMiniled.Checked = !menuAutoMiniled.Checked;
                    AppConfig.Set("auto_miniled", menuAutoMiniled.Checked ? 1 : 0);
                    ScreenControl.AutoScreen(true);
                };
                menuMiniled.DropDownItems.Add(menuAutoMiniled);

                contextMenuStrip.Items.Add(menuMiniled);
            }

            if (PawnIO.CpuInfo.IsAMD)
            {
                bool pawnInstalled = PawnIO.RyzenSmuService.IsPawnInstalled();
                int currentUV = AppConfig.GetMode("cpu_uv", 0);
                string uvLabel = !pawnInstalled 
                    ? "CPU Undervolt: Driver Required" 
                    : (currentUV < 0 ? $"CPU Undervolt: {currentUV} (Active)" : "CPU Undervolt: Disabled (0)");
                var menuUV = new ToolStripMenuItem(uvLabel) { Margin = padding };
                menuUV.DropDown.BackColor = this.BackColor;
                menuUV.DropDown.ForeColor = this.ForeColor;

                if (!pawnInstalled)
                {
                    var itemInstall = new ToolStripMenuItem("Install PawnIO Driver (Unlocks Undervolting)") { Margin = padding };
                    itemInstall.Click += async (s, e) =>
                    {
                        itemInstall.Text = "Installing PawnIO...";
                        bool ok = await PawnIO.PawnInstaller.InstallAsync();
                        if (ok)
                        {
                            Program.toast?.RunToast("PawnIO Installed: CPU Undervolting Unlocked!");
                            SetContextMenu();
                        }
                        else
                        {
                            itemInstall.Text = "Install Failed (Click to retry)";
                        }
                    };
                    menuUV.DropDownItems.Add(itemInstall);
                }
                else
                {
                    Action<int> applyUVPreset = (uv) =>
                    {
                        AppConfig.SetMode("cpu_uv", uv);
                        AppConfig.SetMode("auto_uv", uv < 0 ? 1 : 0);
                        Program.modeControl.SetUV(uv);
                        Program.toast?.RunToast(uv < 0 ? $"CPU Undervolt: {uv} Applied" : "CPU Undervolt: Disabled (Stock)");
                        SetContextMenu();
                    };

                    var uv0 = new ToolStripMenuItem("Stock / Off (0)") { Checked = currentUV == 0, Margin = padding };
                    uv0.Click += (s, e) => applyUVPreset(0);
                    menuUV.DropDownItems.Add(uv0);

                    var uv10 = new ToolStripMenuItem("Light (-10)") { Checked = currentUV == -10, Margin = padding };
                    uv10.Click += (s, e) => applyUVPreset(-10);
                    menuUV.DropDownItems.Add(uv10);

                    var uv15 = new ToolStripMenuItem("Balanced (-15)") { Checked = currentUV == -15, Margin = padding };
                    uv15.Click += (s, e) => applyUVPreset(-15);
                    menuUV.DropDownItems.Add(uv15);

                    var uv20 = new ToolStripMenuItem("Aggressive (-20)") { Checked = currentUV == -20, Margin = padding };
                    uv20.Click += (s, e) => applyUVPreset(-20);
                    menuUV.DropDownItems.Add(uv20);

                    var uvCustom = new ToolStripMenuItem("Custom Sliders (Fans + Power)...") { Margin = padding };
                    uvCustom.Click += (s, e) => { FansToggle(1); };
                    menuUV.DropDownItems.Add(uvCustom);
                }

                contextMenuStrip.Items.Add(menuUV);
            }

            var curTouchMode = _cachedTouchPenMode ?? TouchPenMode.TouchAndPen;
            var menuTouchPen = new ToolStripMenuItem("Touch / Pen Mode") { Margin = padding };
            menuTouchPen.DropDown.BackColor = this.BackColor;
            menuTouchPen.DropDown.ForeColor = this.ForeColor;

            var itemTouchAndPen = new ToolStripMenuItem("Touch + Pen (Default)") { Checked = curTouchMode == TouchPenMode.TouchAndPen, Margin = padding };
            itemTouchAndPen.Click += (s, e) => { ApplyTouchPenModeUI(TouchPenMode.TouchAndPen); SetContextMenu(); };
            menuTouchPen.DropDownItems.Add(itemTouchAndPen);

            var itemPenOnly = new ToolStripMenuItem("Pen Only (Palm Rejection)") { Checked = curTouchMode == TouchPenMode.PenOnly, Margin = padding };
            itemPenOnly.Click += (s, e) => { ApplyTouchPenModeUI(TouchPenMode.PenOnly); SetContextMenu(); };
            menuTouchPen.DropDownItems.Add(itemPenOnly);

            var itemTouchOff = new ToolStripMenuItem("Touchscreen Off") { Checked = curTouchMode == TouchPenMode.Off, Margin = padding };
            itemTouchOff.Click += (s, e) => { ApplyTouchPenModeUI(TouchPenMode.Off); SetContextMenu(); };
            menuTouchPen.DropDownItems.Add(itemTouchOff);

            contextMenuStrip.Items.Add(menuTouchPen);

            if (AutoRotationControl.IsSupported())
            {
                bool isRotationLocked = !AutoRotationControl.IsEnabled();
                var menuRotationLock = new ToolStripMenuItem("Convertible Auto-Rotation Lock") { Checked = isRotationLocked, Margin = padding };
                menuRotationLock.Click += (s, e) =>
                {
                    bool newAutoRotate = AutoRotationControl.Toggle();
                    menuRotationLock.Checked = !newAutoRotate;
                    VisualiseRotation();
                    Program.toast?.RunToast(newAutoRotate ? "Auto-Rotation Unlocked" : "Auto-Rotation Locked");
                };
                contextMenuStrip.Items.Add(menuRotationLock);
            }

            var menuGameMode = new ToolStripMenuItem("Auto Game Mode (Turbo in Games)");
            menuGameMode.Margin = padding;
            menuGameMode.Checked = GameWatcher.IsEnabled();
            menuGameMode.Click += (s, e) =>
            {
                menuGameMode.Checked = !menuGameMode.Checked;
                GameWatcher.Toggle(menuGameMode.Checked);
                Program.toast?.RunToast(menuGameMode.Checked ? "Auto Game Mode Enabled" : "Auto Game Mode Disabled");
            };
            contextMenuStrip.Items.Add(menuGameMode);

            var menuTrimRam = new ToolStripMenuItem("Trim Standby Memory (Free RAM)");
            menuTrimRam.Margin = padding;
            menuTrimRam.Click += (s, e) =>
            {
                Task.Run(() =>
                {
                    long freedMB = MemoryCleaner.TrimWorkingSet();
                    Program.toast?.RunToast($"RAM Trimmed: Freed ~{freedMB} MB");
                });
            };
            contextMenuStrip.Items.Add(menuTrimRam);

            contextMenuStrip.Items.Add("-");

            // --- Display OSD & Tools ---
            var menuOverlay = new ToolStripMenuItem(Properties.Strings.Overlay);
            menuOverlay.Click += (sender, args) => ToggleOverlay();
            menuOverlay.Margin = padding;
            menuOverlay.Checked = AppConfig.IsOverlay();
            contextMenuStrip.Items.Add(menuOverlay);

            var menuOverlayGameOnly = new ToolStripMenuItem(Properties.Strings.OverlayOnlyInGames);
            menuOverlayGameOnly.Click += (sender, args) => ToggleOverlayGameOnly();
            menuOverlayGameOnly.Margin = padding;
            menuOverlayGameOnly.Checked = AppConfig.IsOverlayGameOnly();
            menuOverlayGameOnly.Enabled = AppConfig.IsOverlay();
            contextMenuStrip.Items.Add(menuOverlayGameOnly);

            var bwIcon = new ToolStripMenuItem(Properties.Strings.BWTrayIcon);
            bwIcon.Margin = padding;
            bwIcon.Checked = AppConfig.IsBWIcon();
            bwIcon.Click += (sender, args) =>
            {
                bwIcon.Checked = !bwIcon.Checked;
                AppConfig.Set("bw_icon", bwIcon.Checked ? 1 : 0);
                VisualiseIcon();
            };
            contextMenuStrip.Items.Add(bwIcon);

            var menuExtraSettings = new ToolStripMenuItem(Properties.Strings.ExtraSettings + "...") { Margin = padding };
            menuExtraSettings.Click += (s, e) =>
            {
                if (extraForm == null || extraForm.Text == "")
                {
                    extraForm = new Extra();
                    AddOwnedForm(extraForm);
                }
                extraForm.Show();
                extraForm.BringToFront();
            };
            contextMenuStrip.Items.Add(menuExtraSettings);

            contextMenuStrip.Items.Add("-");

            var quit = new ToolStripMenuItem(Properties.Strings.Quit);
            quit.Click += ButtonQuit_Click;
            quit.Margin = padding;
            contextMenuStrip.Items.Add(quit);

            //contextMenuStrip.ShowCheckMargin = true;
            contextMenuStrip.Renderer = new CustomMenuRenderer();

            InitContextMenuTheme();

            if (Program.trayIcon is not null) Program.trayIcon.ContextMenuStrip = contextMenuStrip;


        }

        public void InitContextMenuTheme()
        {
            if (contextMenuStrip is not null)
            {
                contextMenuStrip.BackColor = this.BackColor;
                contextMenuStrip.ForeColor = this.ForeColor;
                ApplyThemeToMenuItems(contextMenuStrip.Items);
            }

            donateControl?.ApplyTheme();
        }

        private void ApplyThemeToMenuItems(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                if (item is ToolStripMenuItem menuItem && menuItem.HasDropDownItems)
                {
                    menuItem.DropDown.BackColor = this.BackColor;
                    menuItem.DropDown.ForeColor = this.ForeColor;
                    ApplyThemeToMenuItems(menuItem.DropDownItems);
                }
            }
        }

        private void ButtonXGM_Click(object? sender, EventArgs e)
        {
            if (!Program.acpi.IsXGConnected() && !XGM.IsConnected()) return;
            gpuControl.ToggleXGM();
        }


        public void SetVersionLabel(string label, bool update = false)
        {
            if (InvokeRequired)
                Invoke(delegate
                {
                    labelVersion.Text = label;
                    if (update) labelVersion.ForeColor = colorTurbo;
                });
            else
            {
                labelVersion.Text = label;
                if (update) labelVersion.ForeColor = colorTurbo;
            }
        }


        private void LabelVersion_Click(object? sender, EventArgs e)
        {
            updateControl.Update();
        }


        private static void OnTimedEvent(Object? source, ElapsedEventArgs? e)
        {
            Program.settingsForm.RefreshSensors();
        }

        private void ButtonFHD_MouseHover(object? sender, EventArgs e)
        {
            labelTipScreen.Text = "Switch to " + ((buttonFHD.Text == "FHD") ? "UHD" : "FHD") + " Mode";
        }

        private void Button120Hz_MouseHover(object? sender, EventArgs e)
        {
            labelTipScreen.Text = Properties.Strings.MaxRefreshTooltip;
        }

        private void Button60Hz_MouseHover(object? sender, EventArgs e)
        {
            labelTipScreen.Text = Properties.Strings.MinRefreshTooltip.Replace("60", ScreenControl.MIN_RATE.ToString());
        }

        private void ButtonScreen_MouseLeave(object? sender, EventArgs e)
        {
            labelTipScreen.Text = "";
        }

        private void ButtonScreenAuto_MouseHover(object? sender, EventArgs e)
        {
            labelTipScreen.Text = Properties.Strings.AutoRefreshTooltip.Replace("60", ScreenControl.MIN_RATE.ToString());
        }

        private void ButtonUltimate_MouseHover(object? sender, EventArgs e)
        {
            labelTipGPU.Text = Properties.Strings.UltimateGPUTooltip;
        }

        private void ButtonStandard_MouseHover(object? sender, EventArgs e)
        {
            labelTipGPU.Text = Properties.Strings.StandardGPUTooltip;
        }

        private void ButtonEco_MouseHover(object? sender, EventArgs e)
        {
            labelTipGPU.Text = Properties.Strings.EcoGPUTooltip;
        }

        private void ButtonOptimized_MouseHover(object? sender, EventArgs e)
        {
            labelTipGPU.Text = Properties.Strings.OptimizedGPUTooltip;
        }

        private void ButtonGPU_MouseLeave(object? sender, EventArgs e)
        {
            labelTipGPU.Text = "";
        }

        private void ButtonXGM_MouseMove(object? sender, MouseEventArgs e)
        {
            if (sender is null) return;
            TableLayoutPanel table = (TableLayoutPanel)sender;

            if (!buttonXGM.Visible) return;

            labelTipGPU.Text = buttonXGM.Bounds.Contains(table.PointToClient(Cursor.Position)) ?
                "XGMobile toggle works only in Standard mode" : "";

        }


        private void ButtonScreenAuto_Click(object? sender, EventArgs e)
        {
            buttonScreenAuto.Activated = true;
            button60Hz.Activated = false;
            button120Hz.Activated = false;
            ScreenControl.SetAutoRefresh(1);
            Task.Run(() => ScreenControl.AutoScreen());
        }


        private void CheckStartup_CheckedChanged(object? sender, EventArgs e)
        {
            if (sender is null) return;
            CheckBox chk = (CheckBox)sender;

            if (chk.Checked)
                Startup.Schedule();
            else
                Startup.UnSchedule();
        }

        private void ButtonMatrix_Click(object? sender, EventArgs e)
        {

            if (matrixControl.IsSlash)
            {
                if (slashForm == null || slashForm.Text == "")
                {
                    slashForm = new Slash();
                    AddOwnedForm(slashForm);
                }

                if (slashForm.Visible)
                {
                    slashForm.Close();
                }
                else
                {
                    slashForm.FormPosition();
                    slashForm.Show();
                }

                return;
            }

            if (matrixForm == null || matrixForm.Text == "")
            {
                matrixForm = new Matrix();
                AddOwnedForm(matrixForm);
            }

            if (matrixForm.Visible)
            {
                matrixForm.Close();
            }
            else
            {
                matrixForm.FormPosition();
                matrixForm.Show();
            }

        }

        public void VisualiseMatrixRunning(int mode)
        {
            if (InvokeRequired) { Invoke(() => VisualiseMatrixRunning(mode)); return; }
            comboMatrixRunning.SelectedIndex = mode;
            if (comboMatrix.SelectedIndex == 0) comboMatrix.SelectedIndex = 3;
        }

        public void SetMatrixRunning(int mode)
        {
            VisualiseMatrixRunning(mode);
            AppConfig.Set("matrix_running", mode);
            matrixControl.SetDevice();
            if (!matrixControl.IsSlash && matrixForm != null && matrixForm.Text != "") matrixForm.VisualiseMode();
        }

        private void ComboMatrixRunning_SelectedValueChanged(object? sender, EventArgs e)
        {
            SetMatrixRunning(comboMatrixRunning.SelectedIndex);
            if (!matrixControl.IsSlash && comboMatrixRunning.SelectedIndex == (int)MatrixMode.Text && (matrixForm == null || !matrixForm.Visible)) ButtonMatrix_Click(sender, e);
        }


        private void ComboMatrix_SelectedValueChanged(object? sender, EventArgs e)
        {
            AppConfig.Set("matrix_brightness", comboMatrix.SelectedIndex);
            matrixControl.SetDevice();
        }


        private void LabelCPUFan_Click(object? sender, EventArgs e)
        {
            FanSensorControl.fanRpm = !FanSensorControl.fanRpm;
            RefreshSensors(true);
        }

        private void ButtonKeyboardColor2_Click(object? sender, EventArgs e)
        {
            SetColorPicker("aura_color2", Aura.Color2);
        }

        private void ButtonKeyboard_Click(object? sender, EventArgs e)
        {
            if (extraForm == null || extraForm.Text == "")
            {
                extraForm = new Extra();
                AddOwnedForm(extraForm);
            }

            if (extraForm.Visible)
            {
                extraForm.Close();
            }
            else
            {
                extraForm.Show();
            }
        }

        public void FansInit()
        {
            if (fansForm == null || fansForm.Text == "") return;
            Invoke(fansForm.InitAll);
        }

        public void GPUInit()
        {
            if (fansForm == null || fansForm.Text == "") return;
            Invoke(fansForm.InitGPU);
        }

        public void FansToggle(int index = 0)
        {
            if (fansForm == null || fansForm.Text == "")
            {
                fansForm = new Fans();
                AddOwnedForm(fansForm);
            }

            if (fansForm.Visible)
            {
                fansForm.Close();
            }
            else
            {
                fansForm.FormPosition();
                fansForm.Show();
                fansForm.ToggleNavigation(index);
            }

        }

        private void ButtonFans_Click(object? sender, EventArgs e)
        {
            FansToggle();
        }

        private void SetColorPicker(string colorField, Color initial)
        {
            RColorPicker colorDlg = new RColorPicker(initial, colorField == "aura_color" && Aura.HasRandomColor());
            colorDlg.ColorChanged += c =>
            {
                AppConfig.Set(colorField, c.ToArgb());
                SetAura();
            };
            colorDlg.ShowDialog(this);
        }

        private void ButtonKeyboardColor_Click(object? sender, EventArgs e)
        {
            SetColorPicker("aura_color", Aura.Color1);
        }

        private void ButtonRearColor_Click(object? sender, EventArgs e)
        {
            SetColorPicker("rear_color", Aura.RearColor);
        }

        private void ComboRearLight_SelectedValueChanged(object? sender, EventArgs e)
        {
            AppConfig.Set("rear_mode", (int)comboRearLight.SelectedValue);
            SetAura();
        }

        public void InitRearLight()
        {
            if (!AppConfig.HasRearLight())
                return;

            Aura.RearMode = (AuraMode)AppConfig.Get("rear_mode");
            Aura.SetRearColor(AppConfig.Get("rear_color"));

            comboRearLight.DropDownStyle = ComboBoxStyle.DropDownList;
            comboRearLight.DataSource = new BindingSource(Aura.GetRearModes(), null);
            comboRearLight.DisplayMember = "Value";
            comboRearLight.ValueMember = "Key";
            comboRearLight.SelectedValue = Aura.RearMode;
            comboRearLight.SelectedValueChanged += ComboRearLight_SelectedValueChanged;

            buttonRearColor.Click += ButtonRearColor_Click;

            buttonRearColor.SwatchColor = Aura.RearColor;
            panelRearLight.Visible = true;
        }

        public void InitAura()
        {
            comboKeyboard.DropDownStyle = ComboBoxStyle.DropDownList;
            if (!Aura.IsBacklightDetected)
                Aura.Init();

            Aura.Mode = (AuraMode)AppConfig.Get("aura_mode");
            Aura.Speed = (AuraSpeed)AppConfig.Get("aura_speed");
            Aura.SetColor(AppConfig.Get("aura_color"));
            Aura.SetColor2(AppConfig.Get("aura_color2"));

            comboKeyboard.DataSource = new BindingSource(Aura.GetModes(), null);
            comboKeyboard.DisplayMember = "Value";
            comboKeyboard.ValueMember = "Key";
            comboKeyboard.SelectedValue = Aura.Mode;
            comboKeyboard.SelectedValueChanged += ComboKeyboard_SelectedValueChanged;


            if (Aura.isWhite)
            {
                buttonKeyboardColor.Visible = false;
            }

            if (AppConfig.NoAura())
            {
                comboKeyboard.Visible = false;
            }

            VisualiseAura();

            InitRearLight();
        }

        public void SetAura()
        {
            Task.Run(() =>
            {
                Aura.ApplyAura();
                VisualiseAura();
            });
        }

        private void _VisualiseAura()
        {
            buttonKeyboardColor.SwatchColor = Aura.Color1;
            buttonKeyboardColor.SwatchColor2 = Aura.HasSecondColor() ? Aura.Color2 : (Color?)null;

            if (panelRearLight.Visible) buttonRearColor.SwatchColor = Aura.RearColor;

            bool dynamic = AppConfig.IsDynamicLighting() && DynamicLightingHelper.IsEnabled() && !AppConfig.IsDynamicLightingOnly();

            if (dynamic)
            {
                labelBacklight.Cursor = Cursors.Hand;
                labelBacklight.Text = Strings.DisableDynamicLighting;
            } else if (Aura.Mode == AuraMode.AMBIENT)
            {
                labelBacklight.Cursor = Cursors.Default;
                labelBacklight.Text = Strings.AmbientModeResources;
            } else
            {
                labelBacklight.Cursor = Cursors.Default;
                labelBacklight.Text = "";
            }
            labelBacklight.Visible = !string.IsNullOrEmpty(labelBacklight.Text);
        }

        public void VisualiseAura()
        {
            if (InvokeRequired)
                Invoke(_VisualiseAura);
            else
                _VisualiseAura();
        }

        public void InitMatrix()
        {

            if (!matrixControl.IsValid)
            {
                panelMatrix.Visible = false;
                return;
            }

            if (matrixControl.IsSlash)
            {
                labelMatrix.Text = "Slash Lighting";
                pictureMatrix.BackgroundImage = ControlHelper.TintImage(Properties.Resources.slash_32, foreMain);
                comboMatrixRunning.Items.Clear();

                foreach (var item in SlashDevice.Modes)
                {
                    comboMatrixRunning.Items.Add(item.Value);
                }

                buttonMatrix.Text = "Slash";
            }

            comboMatrix.SelectedIndex = Math.Max(0, Math.Min(AppConfig.Get("matrix_brightness", 0), comboMatrix.Items.Count - 1));
            comboMatrixRunning.SelectedIndex = Math.Min(AppConfig.Get("matrix_running", 0), comboMatrixRunning.Items.Count - 1);
        }


        public void CycleMatrix(int delta)
        {
            comboMatrix.SelectedIndex = Math.Min(Math.Max(0, comboMatrix.SelectedIndex + delta), comboMatrix.Items.Count - 1);
            AppConfig.Set("matrix_brightness", comboMatrix.SelectedIndex);
            matrixControl.SetDevice();
            Program.toast.RunToast(comboMatrix.GetItemText(comboMatrix.SelectedItem), delta > 0 ? ToastIcon.BacklightUp : ToastIcon.BacklightDown);
        }


        public void CycleAuraMode(int delta)
        {
            if (delta > 0)
            {
                if (comboKeyboard.SelectedIndex < comboKeyboard.Items.Count - 1)
                    comboKeyboard.SelectedIndex += 1;
                else
                    comboKeyboard.SelectedIndex = 0;
            }
            else
            {
                if (comboKeyboard.SelectedIndex > 0)
                    comboKeyboard.SelectedIndex -= 1;
                else
                    comboKeyboard.SelectedIndex = comboKeyboard.Items.Count - 1;
            }

            Program.toast.RunToast(comboKeyboard.GetItemText(comboKeyboard.SelectedItem), ToastIcon.BacklightUp);
        }

        private void ComboKeyboard_SelectedValueChanged(object? sender, EventArgs e)
        {
            AppConfig.Set("aura_mode", (int)comboKeyboard.SelectedValue);
            SetAura();
        }


        private void Button120Hz_Click(object? sender, EventArgs e)
        {
            buttonScreenAuto.Activated = false;
            button60Hz.Activated = false;
            button120Hz.Activated = true;
            ScreenControl.SetAutoRefresh(0);
            Task.Run(() => ScreenControl.SetScreen(ScreenControl.MAX_REFRESH));
        }

        private void Button60Hz_Click(object? sender, EventArgs e)
        {
            buttonScreenAuto.Activated = false;
            button60Hz.Activated = true;
            button120Hz.Activated = false;
            ScreenControl.SetAutoRefresh(0);
            Task.Run(() => ScreenControl.SetScreen(ScreenControl.MIN_RATE));
        }

        private void ButtonOverdrive_Click(object? sender, EventArgs e)
        {
            bool nextOd = !buttonOverdrive.Activated;
            buttonOverdrive.Activated = nextOd;
            Program.toast.RunToast($"{Properties.Strings.Overdrive}: {(nextOd ? Properties.Strings.On : Properties.Strings.Off)}", nextOd ? ToastIcon.BrightnessUp : ToastIcon.BrightnessDown);
            Task.Run(() => ScreenControl.ToggleOverdrive());
        }

        private void ButtonMiniled_Click(object? sender, EventArgs e)
        {
            bool wasMultizone = buttonMiniled.Activated;
            if (wasMultizone && buttonHDR != null && buttonHDR.Activated)
            {
                buttonHDR.Activated = false;
                buttonHDR.Text = "HDR: Off";
            }
            Task.Run(() =>
            {
                string modeName = ScreenControl.ToogleMiniled();
                if (IsHandleCreated)
                {
                    BeginInvoke(() =>
                    {
                        SetContextMenu();
                        Program.toast.RunToast(modeName, ToastIcon.BrightnessUp);
                    });
                }
            });
        }



        public void VisualiseScreen(bool screenEnabled, bool screenAuto, int frequency, int maxFrequency, int overdrive, bool overdriveSetting, int miniled1, int miniled2, bool hdr, bool acm, int fhd, int hdrControl)
        {
            bool advancedColor = hdr || acm;

            ButtonEnabled(button60Hz, screenEnabled);
            ButtonEnabled(button120Hz, screenEnabled);
            ButtonEnabled(buttonScreenAuto, screenEnabled);
            ButtonEnabled(buttonOverdrive, screenEnabled);
            ButtonEnabled(buttonMiniled, screenEnabled);

            _baseScreenTitle = screenEnabled
                ? Properties.Strings.LaptopScreen + ": " + frequency + "Hz" + ((overdrive == 1) ? " + " + Properties.Strings.Overdrive : "")
                : Properties.Strings.LaptopScreen + ": " + Properties.Strings.TurnedOff;

            UpdateScreenHeaderLabel();
            panelScreen.AccessibleName = _baseScreenTitle;

            button60Hz.Activated = false;
            button120Hz.Activated = false;
            buttonScreenAuto.Activated = false;

            if (screenAuto)
            {
                buttonScreenAuto.Activated = true;
            }
            else if (frequency == ScreenControl.MIN_RATE)
            {
                button60Hz.Activated = true;
            }
            else if (frequency > ScreenControl.MIN_RATE)
            {
                button120Hz.Activated = true;
            }

            button60Hz.Text = ScreenControl.MIN_RATE + "Hz";

            if (overdrive >= 0)
            {
                buttonOverdrive.Visible = true;
                buttonOverdrive.Activated = overdrive == 1;
                buttonOverdrive.Text = Properties.Strings.Overdrive;
            }
            else
            {
                buttonOverdrive.Visible = false;
            }

            if (maxFrequency > ScreenControl.MIN_RATE)
            {
                button120Hz.Text = maxFrequency.ToString() + "Hz";
                panelScreen.Visible = true;
                tableScreen.Visible = true;
            }
            else if (maxFrequency > 0)
            {
                tableScreen.Visible = false;
                panelScreen.Visible = AppConfig.NoGpu();
            }

            if (fhd >= 0)
            {
                buttonFHD.Visible = true;
                buttonFHD.Text = fhd > 0 ? "FHD" : "UHD";
            }

            bool hdrControlVisible = (hdr && hdrControl >= 0);

            if (miniled1 >= 0)
            {
                buttonMiniled.Visible = !hdrControlVisible;
                ButtonEnabled(buttonMiniled, screenEnabled);
                buttonMiniled.Activated = miniled1 == 1;
                buttonMiniled.Text = (miniled1 == 1) ? Properties.Strings.Multizone : Properties.Strings.OneZone;
                buttonMiniled.BorderColor = (miniled1 == 1) ? colorTurbo : colorStandard;
            }
            else if (miniled2 >= 0)
            {
                buttonMiniled.Visible = !hdrControlVisible;
                ButtonEnabled(buttonMiniled, screenEnabled);

                switch (miniled2)
                {
                    // Multizone On
                    case 0:
                        buttonMiniled.Text = Properties.Strings.Multizone;
                        buttonMiniled.BorderColor = colorStandard;
                        buttonMiniled.Activated = true;
                        break;
                    // Multizone Strong
                    case 1:
                        buttonMiniled.Text = Properties.Strings.MultizoneStrong;
                        buttonMiniled.BorderColor = colorTurbo;
                        buttonMiniled.Activated = true;
                        break;
                    // Multizone Off
                    case 2:
                        buttonMiniled.Text = Properties.Strings.OneZone;
                        buttonMiniled.BorderColor = colorStandard;
                        buttonMiniled.Activated = false;
                        break;
                }
            }
            else
            {
                buttonMiniled.Visible = false;
            }

            if (hdrControlVisible)
            {
                buttonHDRControl.Visible = true;
                buttonHDRControl.Activated = hdrControl > 0;
                buttonHDRControl.BorderColor = colorTurbo;
            }
            else
            {
                buttonHDRControl.Visible = false;
            }

            bool hasCol4 = (miniled1 >= 0) || (miniled2 >= 0) || hdrControlVisible || (fhd >= 0);
            if (hasCol4)
                tableScreen.ColumnCount = 5;
            else if (overdrive >= 0)
                tableScreen.ColumnCount = 4;
            else
                tableScreen.ColumnCount = 3;

            if (advancedColor) labelVisual.Text = Properties.Strings.VisualModesHDR;
            if (!screenEnabled) labelVisual.Text = Properties.Strings.VisualModesScreen;

            VisualiseScreenSwitches(hdr);

            Task.Run(() =>
            {
                try
                {
                    int currentBrightness = ScreenBrightness.Get();
                    if (currentBrightness >= 0 && currentBrightness <= 100)
                    {
                        UpdateBrightnessSlider(currentBrightness);
                        AppConfig.Set("screen_brightness", currentBrightness);
                    }
                }
                catch { }
            });

            if ((!screenEnabled || advancedColor) && !VisualControl.forceVisual)
            {
                tableVisual.Visible = false;
                labelVisual.Visible = true;
            }
            else
            {
                labelVisual.Visible = false;
                if (comboVisual.Items.Count > 0 || buttonInstallColor.Visible)
                    tableVisual.Visible = true;
            }


        }

        private void ButtonQuit_Click(object? sender, EventArgs e)
        {
            AsusLampArray.Release();
            matrixControl.Dispose();
            Close();
            Program.trayIcon.Visible = false;
            Application.Exit();
        }

        /// <summary>
        /// Closes all forms except the settings. Hides the settings
        /// </summary>
        public void HideAll()
        {
            this.Hide();
            if (fansForm != null && fansForm.Text != "") fansForm.Close();
            if (extraForm != null && extraForm.Text != "") extraForm.Close();
            if (updatesForm != null && updatesForm.Text != "") updatesForm.Close();
            if (matrixForm != null && matrixForm.Text != "") matrixForm.Close();
            if (slashForm != null && slashForm.Text != "") slashForm.Close();
            if (handheldForm != null && handheldForm.Text != "") handheldForm.Close();
            if (overlayForm != null && overlayForm.Text != "") overlayForm.Close();
            if (mouseSettings != null && mouseSettings.Text != "") mouseSettings.Close();
            if (keyboardSettings != null && keyboardSettings.Text != "") keyboardSettings.Close();
            MemoryHelper.TrimAfter();
        }

        /// <summary>
        /// Brings all visible windows to the top, with settings being the focus
        /// </summary>
        public void ShowAll()
        {
            this.Activate();
            this.TopMost = true;
            this.TopMost = AppConfig.Is("topmost");
        }

        public DialogResult ShowMessage(string text, string title = "", MessageBoxButtons buttons = MessageBoxButtons.OK)
        {
            DialogResult result = DialogResult.None;
            Invoke((MethodInvoker)delegate
            {
                result = MessageBox.Show(this, text, title, buttons);
            });
            return result;
        }

        /// <summary>
        /// Check if any of fans, keyboard, update, or itself has focus
        /// </summary>
        /// <returns>Focus state</returns>
        public bool HasAnyFocus(bool lostFocusCheck = false)
        {
            return (fansForm != null && fansForm.ContainsFocus) ||
                   (extraForm != null && extraForm.ContainsFocus) ||
                   (updatesForm != null && updatesForm.ContainsFocus) ||
                   (matrixForm != null && matrixForm.ContainsFocus) ||
                   (slashForm != null && slashForm.ContainsFocus) ||
                   (handheldForm != null && handheldForm.ContainsFocus) ||
                   (overlayForm != null && overlayForm.ContainsFocus) ||
                   this.ContainsFocus ||
                   (lostFocusCheck && Math.Abs(DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastLostFocus) < 300);
        }

        private void SettingsForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideAll();
            }
        }

        private void ButtonUltimate_Click(object? sender, EventArgs e)
        {
            gpuControl.SetGPUMode(AsusACPI.GPUModeUltimate);
        }

        private void ButtonStandard_Click(object? sender, EventArgs e)
        {
            gpuControl.SetGPUMode(AsusACPI.GPUModeStandard);
        }

        private void ButtonEco_Click(object? sender, EventArgs e)
        {
            gpuControl.SetGPUMode(AsusACPI.GPUModeEco);
        }


        private void ButtonOptimized_Click(object? sender, EventArgs e)
        {
            AppConfig.Set("gpu_auto", (AppConfig.Get("gpu_auto") == 1) ? 0 : 1);
            VisualiseGPUMode();
            Task.Run(() => gpuControl.AutoGPUMode(true));
        }

        private void ButtonStopGPU_Click(object? sender, EventArgs e)
        {
            gpuControl.KillGPUApps();
        }

        public async void RefreshSensors(bool force = false)
        {
            int throttle = (!Visible && sensorsAlways) ? 6000 : (Visible ? 400 : 2000);
            if (!force && Math.Abs(DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastRefresh) < throttle) return;
            lastRefresh = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            string battery = "";
            string charge = "";

            await Task.Run(() => HardwareControl.ReadSensors());
            if (Visible)
            {
                if (DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastPeripheralCheck > 30000)
                {
                    lastPeripheralCheck = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    _ = Task.Run((Action)PeripheralsProvider.RefreshBatteryForAllDevices);
                }
                bool xgmConnected = HardwareControl.IsXGConnectedCached();
                if (buttonXGM.Visible != xgmConnected)
                {
                    VisualizeXGM();
                }
            }

            string pctPrefix = HardwareControl.batteryCapacity > 0
                ? Math.Round((decimal)HardwareControl.batteryCapacity, 0) + "% | "
                : "";
            string timeSuffix = !string.IsNullOrEmpty(HardwareControl.batteryTimeStr)
                ? " (" + HardwareControl.batteryTimeStr + ")"
                : "";

            if (HardwareControl.batteryCapacity > 0)
            {
                charge = Properties.Strings.BatteryCharge + ": " + HardwareControl.batteryCharge;
            }

            string trayBattery = "";
            if (HardwareControl.batteryRate < 0)
            {
                string watts = Math.Round(-(decimal)HardwareControl.batteryRate, 1).ToString() + "W";
                battery = pctPrefix + Properties.Strings.Discharging + ": " + watts + timeSuffix;
                trayBattery = (HardwareControl.batteryCapacity > 0 ? Math.Round((decimal)HardwareControl.batteryCapacity, 0) + "% " : "") + "-" + watts + timeSuffix;
            }
            else if (HardwareControl.batteryRate > 0)
            {
                string watts = "+" + Math.Round((decimal)HardwareControl.batteryRate, 1).ToString() + "W";
                battery = pctPrefix + Properties.Strings.Charging + ": " + watts + timeSuffix;
                trayBattery = (HardwareControl.batteryCapacity > 0 ? Math.Round((decimal)HardwareControl.batteryCapacity, 0) + "% " : "") + watts + timeSuffix;
            }
            else if (HardwareControl.batteryCapacity > 0)
            {
                if (BatteryControl.chargePaused)
                    battery = pctPrefix + "AC Bypass (Paused)";
                else if (BatteryControl.isSmartHoldActive)
                    battery = pctPrefix + $"Smart Hold (<{BatteryControl.smartChargeMin}%)";
                else
                    battery = pctPrefix + "AC Bypass (0.0W)";
                trayBattery = battery;
            }


            bool isXgm = HardwareControl.IsXGConnectedCached();

            string cpuTempStr = HardwareControl.cpuTemp > 0 ? TempHelper.FormatTemp((double)HardwareControl.cpuTemp) : "";
            string cpuFanStr = HardwareControl.cpuFan is not null ? HardwareControl.cpuFan : "";

            string gpuTempStr = HardwareControl.gpuTemp > 0 ? TempHelper.FormatTemp((double)HardwareControl.gpuTemp) : "";
            string gpuFanStr = HardwareControl.gpuFan is not null ? HardwareControl.gpuFan : "";

            string xgmTempStr = HardwareControl.xgmTemp > 0 ? TempHelper.FormatTemp((double)HardwareControl.xgmTemp) : "";
            if (HardwareControl.xgmHotspotTemp is not null && HardwareControl.xgmHotspotTemp > 0)
                xgmTempStr += "/" + Math.Round((double)HardwareControl.xgmHotspotTemp) + "°C";
            string xgmFanStr = HardwareControl.xgmFan is not null ? HardwareControl.xgmFan : "";

            string trayTip = "CPU: " + (cpuTempStr.Length > 0 ? cpuTempStr : "--") + (cpuFanStr.Length > 0 ? " " + cpuFanStr : "");
            if (gpuTempStr.Length > 0 || gpuFanStr.Length > 0)
                trayTip += "\nGPU: " + (gpuTempStr.Length > 0 ? gpuTempStr : "--") + (gpuFanStr.Length > 0 ? " " + gpuFanStr : "");
            
            string midTempStr = HardwareControl.midTemp > 0 ? TempHelper.FormatTemp((double)HardwareControl.midTemp) : "";
            if (HardwareControl.midFan is not null)
                trayTip += "\nMid: " + (midTempStr.Length > 0 ? midTempStr : "--") + " " + HardwareControl.midFan;
                
            if (isXgm && (xgmTempStr.Length > 0 || xgmFanStr.Length > 0))
                trayTip += "\nXGM: " + (xgmTempStr.Length > 0 ? xgmTempStr : "--") + (xgmFanStr.Length > 0 ? " " + xgmFanStr : "");
            if (trayBattery.Length > 0)
                trayTip += "\nBat: " + trayBattery;
            if (trayTip.Length > 127)
                trayTip = trayTip.Substring(0, 127);
            
            if (Program.settingsForm.IsHandleCreated)
                Program.settingsForm.BeginInvoke(delegate
                {
                    string cpuHeader = "CPU: " + (cpuTempStr.Length > 0 ? cpuTempStr : "--") + (cpuFanStr.Length > 0 ? " " + cpuFanStr : "");
                    SetLabelText(labelCPUFan, cpuHeader);
                    Color cpuColor = HardwareControl.cpuTemp >= 85 ? colorTurbo
                                   : HardwareControl.cpuTemp >= 75 ? colorCustom
                                   : foreMain;
                    if (labelCPUFan.ForeColor != cpuColor) labelCPUFan.ForeColor = cpuColor;

                    string gpuHeader;
                    if (HardwareControl.gpuTemp > 0 || HardwareControl.gpuFan is not null)
                    {
                        gpuHeader = "GPU: " + (gpuTempStr.Length > 0 ? gpuTempStr : "--") + (gpuFanStr.Length > 0 ? " " + gpuFanStr : "");
                    }
                    else
                    {
                        gpuHeader = AppConfig.Get("gpu_mode") == AsusACPI.GPUModeEco ? "GPU: Off (Eco)" : "GPU: --";
                    }
                    SetLabelText(labelGPUFan, gpuHeader);
                    Color gpuColor = HardwareControl.gpuTemp >= 85 ? colorTurbo
                                   : HardwareControl.gpuTemp >= 75 ? colorCustom
                                   : foreMain;
                    if (labelGPUFan.ForeColor != gpuColor) labelGPUFan.ForeColor = gpuColor;

                    if (HardwareControl.ramUsedMb > 0)
                    {
                        string fanTip = $"System RAM: {HardwareControl.ramUsedMb / 1024.0:F1} GB used ({HardwareControl.ramUsage}%)  |  Left-click: Toggle Fan RPM / %  |  Right-click: Trim Standby RAM";
                        if (toolTip.GetToolTip(labelCPUFan) != fanTip)
                        {
                            toolTip.SetToolTip(labelCPUFan, fanTip);
                            toolTip.SetToolTip(labelGPUFan, fanTip);
                            toolTip.SetToolTip(labelMidFan, fanTip);
                        }
                    }

                    string midHeader = "";
                    if (AppConfig.IsMidFan() || HardwareControl.midFan is not null)
                    {
                        string midFanStr = HardwareControl.midFan ?? (FanSensorControl.fanRpm ? "0RPM" : "0%");
                        midHeader = "Mid: " + (midTempStr.Length > 0 ? midTempStr + " " : "") + midFanStr;
                        if (!labelMidFan.Visible) labelMidFan.Visible = true;
                    }
                    SetLabelText(labelMidFan, midHeader);

                    string xgmHeader = "";
                    if (isXgm)
                    {
                        xgmHeader = "XGM: " + (xgmTempStr.Length > 0 ? xgmTempStr : "--") + (xgmFanStr.Length > 0 ? " " + xgmFanStr : "");
                        if (!labelXGMFan.Visible) labelXGMFan.Visible = true;
                    }
                    SetLabelText(labelXGMFan, xgmHeader);

                    SetLabelText(labelBattery, battery);
                    if (HardwareControl.batteryCapacity > 0)
                    {
                        Color batColor;
                        if (HardwareControl.batteryRate < 0)
                        {
                            decimal absRate = Math.Abs((decimal)HardwareControl.batteryRate);
                            batColor = absRate < 12m ? colorEco : (absRate <= 25m ? colorStandard : colorTurbo);
                        }
                        else if (HardwareControl.batteryRate > 0)
                        {
                            batColor = colorStandard;
                        }
                        else
                        {
                            batColor = colorEco;
                        }
                        if (labelBattery.ForeColor != batColor) labelBattery.ForeColor = batColor;
                    }
                    UpdateScreenHeaderLabel();
                    if (Visible && PowerNative.KeepAwakeExpiresAt.HasValue) VisualiseMicAndPowerSaver();
                    if (!batteryMouseOver && !batteryFullMouseOver) SetLabelText(labelCharge, charge);
                    if (!string.IsNullOrEmpty(HardwareControl.batteryHealthStr))
                    {
                        if (toolTip.GetToolTip(labelBattery) != HardwareControl.batteryHealthStr)
                        {
                            toolTip.SetToolTip(labelBattery, HardwareControl.batteryHealthStr);
                            toolTip.SetToolTip(labelBatteryTitle, HardwareControl.batteryHealthStr);
                        }
                    }

                    if (fansForm != null && !fansForm.IsDisposed && fansForm.Visible)
                        fansForm.UpdateSensors();
                });

            if (Program.trayIcon is not null && Program.trayIcon.Text != trayTip) Program.trayIcon.Text = trayTip;
        }

        public void LabelFansResult(string text)
        {
            if (fansForm != null && !fansForm.IsDisposed && fansForm.Text != "")
                fansForm.LabelFansResult(text);
        }

        public void ToggleOverlay(bool fromHotkey = false)
        {
            bool enable = !AppConfig.IsOverlay();
            AppConfig.Set("overlay", enable ? 1 : 0);
            Logger.WriteLine("Overlay " + (enable ? "On" : "Off") + (AppConfig.IsOverlayGameOnly() ? " (game only)" : ""));
            if (enable)
                Program.hardwareOverlay?.StartOverlay();
            else
                Program.hardwareOverlay?.StopOverlay();

            VisualiseOverlay();

            if (fromHotkey && AppConfig.IsOverlayGameOnly())
                Program.toast.RunToast(Properties.Strings.Overlay + " " + (enable ? Properties.Strings.On : Properties.Strings.Off));

            SetContextMenu();
        }

        public void ToggleOverlayGameOnly()
        {
            AppConfig.Set("overlay_game_only", AppConfig.IsOverlayGameOnly() ? 0 : 1);
            if (AppConfig.IsOverlay())
            {
                Program.hardwareOverlay?.StopOverlay();
                Program.hardwareOverlay?.StartOverlay();
            }
            SetContextMenu();
        }

        public void ShowMode(int mode)
        {
            if (InvokeRequired)
                Invoke(delegate
                {
                    VisualiseMode(mode);
                });
            else
                VisualiseMode(mode);
        }

        protected void VisualiseMode(int mode)
        {
            buttonSilent.Activated = false;
            buttonBalanced.Activated = false;
            buttonTurbo.Activated = false;
            buttonFans.Activated = false;

            switch (mode)
            {
                case AsusACPI.PerformanceSilent:
                    buttonSilent.Activated = true;
                    break;
                case AsusACPI.PerformanceTurbo:
                    buttonTurbo.Activated = true;
                    break;
                case AsusACPI.PerformanceBalanced:
                    buttonBalanced.Activated = true;
                    break;
                default:
                    buttonFans.Activated = true;
                    buttonFans.BorderColor = Modes.GetBase(mode) switch
                    {
                        AsusACPI.PerformanceSilent => colorEco,
                        AsusACPI.PerformanceTurbo => colorTurbo,
                        AsusACPI.PerformanceFullSpeed => Color.Orange,
                        _ => colorStandard,
                    };
                    break;
            }

            foreach (var item in contextMenuStrip.Items)
            {
                if (item is ToolStripMenuItem menuItem && menuItem.Tag is not null)
                {
                    menuItem.Checked = ((int)menuItem.Tag == mode);
                }
            }
        }


        public void SetModeLabel(string modeText)
        {
            if (InvokeRequired)
            {
                Invoke(delegate
                {
                    labelPerf.Text = modeText;
                    panelPerformance.AccessibleName = labelPerf.Text;
                });
            }
            else
            {
                labelPerf.Text = modeText;
                panelPerformance.AccessibleName = labelPerf.Text;
            }

        }



        public void VisualizeXGM(int GPUMode = -1)
        {
            Task.Run(() =>
            {
                bool connected = Program.acpi.IsXGConnected() || XGM.IsConnected();
                int activated = connected ? Program.acpi.DeviceGet(AsusACPI.GPUXG) : -1;
                if (IsHandleCreated)
                    BeginInvoke(() => VisualizeXGM(connected, activated, GPUMode));
            });
        }

        void VisualizeXGM(bool connected, int activated, int GPUMode)
        {
            buttonXGM.Visible = connected;
            buttonXGM.Enabled = connected;

            if (!AppConfig.IsAlly() && isGpuSection)
            {
                if (connected || !isEcoGpu)
                    tableGPU.ColumnCount = 5;
                else
                    tableGPU.ColumnCount = (!isEcoGpu || !isMuxGpu) ? 3 : 4;
            }

            if (!connected)
            {
                ButtonEnabled(buttonXGM, false);
                return;
            }

            if (GPUMode != -1)
                ButtonEnabled(buttonXGM, true);


            Logger.WriteLine("XGM Activated flag: " + activated);

            buttonXGM.Activated = activated == 1;

            if (activated == 1)
            {
                ButtonEnabled(buttonOptimized, false);
                ButtonEnabled(buttonEco, false);
                ButtonEnabled(buttonStandard, false);
                ButtonEnabled(buttonUltimate, false);
            }
            else
            {
                ButtonEnabled(buttonOptimized, true);
                ButtonEnabled(buttonEco, true);
                ButtonEnabled(buttonStandard, true);
                ButtonEnabled(buttonUltimate, true);
            }

        }

        public void VisualiseGPUButtons(bool eco = true, bool ultimate = true)
        {
            if (InvokeRequired) { Invoke(() => VisualiseGPUButtons(eco, ultimate)); return; }
            isMuxGpu = ultimate;
            isEcoGpu = eco;

            if (!eco)
            {
                menuEco.Visible = buttonEco.Visible = false;
                menuOptimized.Visible = buttonOptimized.Visible = false;
                buttonStopGPU.Visible = true;
                tableGPU.ColumnCount = 5;
            }
            else
            {
                buttonStopGPU.Visible = false;
            }

            if (!ultimate)
            {
                menuUltimate.Visible = buttonUltimate.Visible = false;
                if (!buttonXGM.Visible && isEcoGpu)
                    tableGPU.ColumnCount = 3;
            }
        }

        public void HideGPUModes(bool gpuExists)
        {
            isGpuSection = false;

            buttonEco.Visible = false;
            buttonStandard.Visible = false;
            buttonUltimate.Visible = false;
            buttonOptimized.Visible = false;
            buttonStopGPU.Visible = true;

            tableGPU.ColumnCount = 0;

            SetContextMenu();

            panelGPU.Visible = gpuExists;

        }


        public void LockGPUModes(string text = null)
        {
            if (InvokeRequired) { Invoke(() => LockGPUModes(text)); return; }
            if (text is null) text = Properties.Strings.GPUMode + ": " + Properties.Strings.GPUChanging + " ...";

            ButtonEnabled(buttonOptimized, false);
            ButtonEnabled(buttonEco, false);
            ButtonEnabled(buttonStandard, false);
            ButtonEnabled(buttonUltimate, false);
            ButtonEnabled(buttonXGM, false);

            labelGPU.Text = text;
        }

        public void VisualiseGPUMode(int GPUMode = -1)
        {
            if (InvokeRequired) { Invoke(() => VisualiseGPUMode(GPUMode)); return; }

            if (toolTip.GetToolTip(pictureGPU) != (GPUModeControl.gpuError ?? ""))
            {
                pictureGPU.BackgroundImage = GPUModeControl.gpuError is null ? Properties.Resources.icons8_video_card_32 : SystemIcons.Warning.ToBitmap();
                pictureGPU.Cursor = GPUModeControl.gpuError is null ? Cursors.Default : Cursors.Hand;
                toolTip.SetToolTip(pictureGPU, GPUModeControl.gpuError);
            }

            if (AppConfig.IsAlly())
            {
                tableGPU.Visible = false;
                labelGPU.Text = "GPU";
                if (Program.acpi.IsXGConnected())
                {
                    tableAMD.Controls.Add(buttonXGM, 1, 0);
                    VisualizeXGM();
                }
                VisualiseIcon();
                return;
            }

            ButtonEnabled(buttonOptimized, true);
            ButtonEnabled(buttonEco, true);
            ButtonEnabled(buttonStandard, true);
            ButtonEnabled(buttonUltimate, true);

            if (GPUMode == -1)
                GPUMode = AppConfig.Get("gpu_mode");

            bool GPUAuto = AppConfig.Is("gpu_auto");

            buttonEco.Activated = false;
            buttonStandard.Activated = false;
            buttonUltimate.Activated = false;
            buttonOptimized.Activated = false;

            switch (GPUMode)
            {
                case AsusACPI.GPUModeEco:
                    buttonOptimized.BorderColor = colorEco;
                    buttonEco.Activated = !GPUAuto;
                    buttonOptimized.Activated = GPUAuto;
                    labelGPU.Text = Properties.Strings.GPUMode + ": " + Properties.Strings.GPUModeEco;
                    panelGPU.AccessibleName = Properties.Strings.GPUMode + " - " + (GPUAuto ? Properties.Strings.Optimized : Properties.Strings.EcoMode);
                    break;
                case AsusACPI.GPUModeUltimate:
                    buttonUltimate.Activated = true;
                    labelGPU.Text = Properties.Strings.GPUMode + ": " + Properties.Strings.GPUModeUltimate;
                    panelGPU.AccessibleName = Properties.Strings.GPUMode + " - " + Properties.Strings.UltimateMode;
                    break;
                default:
                    buttonOptimized.BorderColor = colorStandard;
                    buttonStandard.Activated = !GPUAuto;
                    buttonOptimized.Activated = GPUAuto;
                    labelGPU.Text = Properties.Strings.GPUMode + ": " + (AppConfig.IsAlwaysUltimate() ? Properties.Strings.GPUModeUltimate : Properties.Strings.GPUModeStandard);
                    panelGPU.AccessibleName = Properties.Strings.GPUMode + " - " + (GPUAuto ? Properties.Strings.Optimized : Properties.Strings.StandardMode);
                    break;
            }

            VisualiseIcon();
            VisualizeXGM(GPUMode);

            if (isGpuSection)
            {
                menuEco.Checked = buttonEco.Activated;
                menuStandard.Checked = buttonStandard.Activated;
                menuUltimate.Checked = buttonUltimate.Activated;
                menuOptimized.Checked = buttonOptimized.Activated;
            }

            // UI Fix for small screeens
            if (Top < 0)
            {
                labelTipGPU.Visible = false;
                labelTipScreen.Visible = false;
                Top = 5;
            }

        }


        private (int, bool, bool)? lastIcon;
        private bool isDark = CheckSystemDarkModeStatus();

        public void VisualiseIcon(bool themeChange = false)
        {
            if (Program.trayIcon is null) return;
            if (themeChange) isDark = CheckSystemDarkModeStatus();

            int GPUMode = AppConfig.Get("gpu_mode");
            bool bw = AppConfig.IsBWIcon();

            if (lastIcon == (GPUMode, isDark, bw)) return;
            lastIcon = (GPUMode, isDark, bw);

            Icon newIcon = GPUMode switch
            {
                AsusACPI.GPUModeEco => bw ? (isDark ? Properties.Resources.light_eco : Properties.Resources.dark_eco) : Properties.Resources.eco,
                AsusACPI.GPUModeUltimate => bw ? (isDark ? Properties.Resources.light_standard : Properties.Resources.dark_standard) : Properties.Resources.ultimate,
                _ => bw ? (isDark ? Properties.Resources.light_standard : Properties.Resources.dark_standard) : Properties.Resources.standard,
            };

            Icon? oldIcon = Program.trayIcon.Icon;
            Program.trayIcon.Icon = newIcon;
            oldIcon?.Dispose();
        }

        private void PictureGPU_Click(object? sender, EventArgs e)
        {
            if (GPUModeControl.gpuError is null) return;
            GPUModeControl.CheckGpuError();
            Process.Start(new ProcessStartInfo("devmgmt.msc") { UseShellExecute = true });
        }

        private void ButtonSilent_Click(object? sender, EventArgs e)
        {
            Program.modeControl.SetPerformanceMode(AsusACPI.PerformanceSilent);
        }

        private void ButtonBalanced_Click(object? sender, EventArgs e)
        {
            Program.modeControl.SetPerformanceMode(AsusACPI.PerformanceBalanced);
        }

        private void ButtonTurbo_Click(object? sender, EventArgs e)
        {
            Program.modeControl.SetPerformanceMode(AsusACPI.PerformanceTurbo);
        }


        public void ButtonEnabled(RButton but, bool enabled)
        {
            but.Enabled = enabled;
            but.BackColor = but.Enabled ? Color.FromArgb(255, but.BackColor) : Color.FromArgb(100, but.BackColor);
        }

        public void VisualiseBatteryTitle(int limit)
        {
            string suffix = "";
            if (BatteryControl.chargePaused)
                suffix = " (Paused / AC Bypass)";
            else if (BatteryControl.smartCharge && BatteryControl.isSmartHoldActive)
                suffix = $" (Smart Hold <{BatteryControl.smartChargeMin}%)";
            else if (BatteryControl.smartCharge)
                suffix = $" (Smart <{BatteryControl.smartChargeMin}%)";

            labelBatteryTitle.Text = Properties.Strings.BatteryChargeLimit + ": " + limit.ToString() + "%" + suffix;

            if (sliderBattery != null)
            {
                Color sliderColor = (limit <= 80 || BatteryControl.chargePaused || BatteryControl.isSmartHoldActive)
                    ? colorEco
                    : (limit <= 95 ? colorStandard : colorCustom);
                if (sliderBattery.accentColor != sliderColor)
                {
                    sliderBattery.accentColor = sliderColor;
                    sliderBattery.Invalidate();
                }
            }
        }

        public void VisualiseBattery(int limit)
        {
            if (InvokeRequired) { Invoke(() => VisualiseBattery(limit)); return; }
            VisualiseBatteryTitle(limit);
            sliderBattery.Value = limit;

            sliderBattery.AccessibleName = Properties.Strings.BatteryChargeLimit + ": " + limit.ToString() + "%";

            VisualiseBatteryFull();
            VisualiseBatteryOptions();
        }

        public void VisualiseBatteryOptions()
        {
            if (InvokeRequired) { Invoke(VisualiseBatteryOptions); return; }
            if (buttonPauseCharge == null) return;

            buttonPauseCharge.Activated = BatteryControl.chargePaused;
            if (buttonSmartCharge != null)
            {
                buttonSmartCharge.Activated = BatteryControl.smartCharge;
                buttonSmartCharge.Text = $"Smart <{BatteryControl.smartChargeMin}%";
                buttonSmartCharge.BorderColor = BatteryControl.isSmartHoldActive ? colorEco : colorStandard;
            }
            if (TravelMode.IsActive)
            {
                buttonBatteryBoost.Activated = true;
                buttonBatteryBoost.BorderColor = colorStandard;
                buttonBatteryBoost.Text = "Travel Mode: On";
            }
            else
            {
                buttonBatteryBoost.Activated = AppConfig.Is("limit_cpu_boost_battery");
                buttonBatteryBoost.BorderColor = colorEco;
                buttonBatteryBoost.Text = "No Boost on Bat";
            }
            buttonBatteryMiniled.Activated = AppConfig.Is("auto_miniled");
            buttonBatteryHDR.Activated = AppConfig.Is("hdr_battery_off");
            VisualiseBatteryTitle(sliderBattery.Value);
        }

        private TouchPenMode? _cachedTouchPenMode;
        private bool _touchStatusChecked;

        public void SyncTouchPenMode(TouchPenMode mode)
        {
            _cachedTouchPenMode = mode;
            if (IsHandleCreated)
            {
                BeginInvoke(() => UpdateTouchButtonDisplay(mode));
            }
        }

        private void ApplyTouchPenModeUI(TouchPenMode nextMode)
        {
            _cachedTouchPenMode = nextMode;
            UpdateTouchButtonDisplay(nextMode);
            string toastMsg = nextMode switch
            {
                TouchPenMode.TouchAndPen => "Touchscreen + Pen: On",
                TouchPenMode.PenOnly => "Pen Only Mode (Touch Off for Palm Rejection)",
                _ => "Touchscreen + Pen: Off"
            };
            Program.toast.RunToast(toastMsg, ToastIcon.Touchpad);
            Task.Run(() =>
            {
                TouchscreenHelper.SetMode(nextMode);
                _cachedTouchPenMode = TouchscreenHelper.GetMode();
                if (IsHandleCreated)
                    BeginInvoke(() => VisualiseScreenSwitches(ScreenCCD.GetHDRStatus()));
            });
        }

        private void UpdateTouchButtonDisplay(TouchPenMode mode)
        {
            buttonTouchscreen.Visible = true;
            switch (mode)
            {
                case TouchPenMode.TouchAndPen:
                    buttonTouchscreen.Text = "Touch: On";
                    buttonTouchscreen.BorderColor = colorEco;
                    buttonTouchscreen.Activated = true;
                    break;
                case TouchPenMode.PenOnly:
                    buttonTouchscreen.Text = "Pen Only";
                    buttonTouchscreen.BorderColor = colorStandard;
                    buttonTouchscreen.Activated = true;
                    break;
                default:
                    buttonTouchscreen.Text = "Touch: Off";
                    buttonTouchscreen.BorderColor = colorEco;
                    buttonTouchscreen.Activated = false;
                    break;
            }
        }

        public void VisualiseRotation()
        {
            if (InvokeRequired) { Invoke(VisualiseRotation); return; }
            if (buttonRotation == null) return;

            bool unlocked = AutoRotationControl.IsEnabled();
            bool autoTablet = AppConfig.Get("auto_tablet_rotation", 1) == 1;
            buttonRotation.Activated = unlocked;
            buttonRotation.BorderColor = autoTablet ? colorStandard : colorGray;
            buttonRotation.Text = unlocked ? "Rotate: On" : "Rotate: Lock";
        }

        public void VisualiseScreenSwitches(bool hdr)
        {
            if (InvokeRequired) { Invoke(() => VisualiseScreenSwitches(hdr)); return; }
            if (buttonHDR == null) return;

            buttonHDR.Text = hdr ? "HDR: On" : "HDR: Off";
            buttonHDR.Activated = hdr;

            bool autoHdr = ScreenCCD.GetAutoHDR();
            buttonAutoHDR.Text = autoHdr ? "Auto HDR: On" : "Auto HDR: Off";
            buttonAutoHDR.Activated = autoHdr;

            VisualiseRotation();

            if (!_touchStatusChecked)
            {
                _touchStatusChecked = true;
                Task.Run(() =>
                {
                    _cachedTouchPenMode = TouchscreenHelper.GetMode();
                    if (IsHandleCreated)
                        BeginInvoke(() => VisualiseScreenSwitches(ScreenCCD.GetHDRStatus()));
                });
            }

            if (_cachedTouchPenMode.HasValue)
            {
                UpdateTouchButtonDisplay(_cachedTouchPenMode.Value);
            }
            else
            {
                buttonTouchscreen.Text = "Touchscreen";
                buttonTouchscreen.Activated = false;
            }
        }

        public void VisualiseBatteryFull()
        {
            if (InvokeRequired) { Invoke(VisualiseBatteryFull); return; }
            if (BatteryControl.chargeFull)
            {
                buttonBatteryFull.BackColor = colorStandard;
                buttonBatteryFull.ForeColor = SystemColors.ControlLightLight;
                buttonBatteryFull.AccessibleName = Properties.Strings.BatteryChargeLimit + "100% on";
            }
            else
            {
                buttonBatteryFull.BackColor = buttonSecond;
                buttonBatteryFull.ForeColor = SystemColors.ControlDark;
                buttonBatteryFull.AccessibleName = Properties.Strings.BatteryChargeLimit + "100% off";
            }

        }


        public void UpdateKeyboardLabel()
        {
            labelKeyboard.Text = Properties.Strings.LaptopKeyboard + (PeripheralsProvider.IsAuraSync ? " +" : "");
        }

        public void VisualizePeripherals()
        {
            if (!PeripheralsProvider.IsAnyPeripheralConnect())
            {
                panelPeripherals.Visible = false;
                return;
            }

            Button[] buttons = new Button[] { buttonPeripheral1, buttonPeripheral2, buttonPeripheral3 };

            //we only support 4 devces for now. Who has more than 4 mice connected to the same PC anyways....
            List<IPeripheral> lp = PeripheralsProvider.AllPeripherals();

            for (int i = 0; i < lp.Count && i < buttons.Length; ++i)
            {
                IPeripheral m = lp.ElementAt(i);
                Button b = buttons[i];

                string id = m.GetDisplayName();
                bool ready = m.IsDeviceReady;
                bool hasBat = m.HasBattery();
                bool charging = ready && hasBat && m.Charging;
                int level = (ready && hasBat) ? Math.Min(5, (m.Battery + 10) / 20) : -1;
                bool showPercent = AppConfig.Is("mouse_battery") && ready && hasBat;
                int cacheBattery = showPercent ? m.Battery : -1;
                var state = (id, ready, charging, level, cacheBattery, b.ForeColor.ToArgb());

                if (b.Tag is ValueTuple<string, bool, bool, int, int, int> prev && prev.Equals(state) && b.Visible)
                    continue;

                b.Text = showPercent ? id + "\n" + m.Battery + "%" : id;

                Image? baseIcon = m.DeviceType() switch
                {
                    PeripheralType.Mouse => Properties.Resources.icons8_maus_48,
                    PeripheralType.Keyboard => Properties.Resources.icons8_keyboard_48,
                    _ => null,
                };

                if (baseIcon is not null)
                {
                    int ih = baseIcon.Height;
                    // icon PNG may be wider than tall (baked-in right text padding); badge/bars anchor to the glyph square
                    int iw = Math.Min(baseIcon.Width, ih);
                    Image composed = ControlHelper.TintImage(baseIcon, b.ForeColor);
                    if (!ready)
                    {
                        composed = ControlHelper.OverlayBadge(composed, Properties.Resources.icons8_cancel_48, RForm.colorTurbo, iconWidth: iw, iconHeight: ih);
                    }
                    else if (hasBat)
                    {
                        if (charging)
                            composed = ControlHelper.OverlayBadge(composed, Properties.Resources.icons8_flash_48, RForm.colorEco, iconWidth: iw, iconHeight: ih);

                        Color barColor = level <= 1 ? colorTurbo
                                       : level <= 3 ? colorStandard
                                       : colorEco;
                        composed = ControlHelper.OverlayChargeBars(composed, level, 5, barColor, iconWidth: iw, iconHeight: ih);
                    }

                    b.Image = ControlHelper.ResizeImage(composed, ControlHelper.Scale);
                }

                b.Tag = state;
                b.Visible = true;
            }

            for (int i = lp.Count; i < buttons.Length; ++i)
            {
                buttons[i].Visible = false;
            }

            panelPeripherals.Visible = true;
        }

        private void ButtonPeripheral_MouseEnter(object? sender, EventArgs e)
        {
            int index = 0;
            if (sender == buttonPeripheral2) index = 1;
            if (sender == buttonPeripheral3) index = 2;
            IPeripheral iph = PeripheralsProvider.AllPeripherals().ElementAt(index);


            if (iph is null)
            {
                return;
            }

            if (!iph.IsDeviceReady)
            {
                //Refresh battery on hover if the device is marked as "Not Ready"
                iph.ReadBattery();
            }
        }

        private void ButtonPeripheral_Click(object? sender, EventArgs e)
        {
            if (mouseSettings is not null)
            {
                mouseSettings.Close();
                return;
            }

            if (keyboardSettings is not null)
            {
                keyboardSettings.Close();
                return;
            }

            int index = 0;
            if (sender == buttonPeripheral2) index = 1;
            if (sender == buttonPeripheral3) index = 2;

            IPeripheral iph = PeripheralsProvider.AllPeripherals().ElementAt(index);

            if (iph is null)
            {
                //Can only happen when the user hits the button in the exact moment a device is disconnected.
                return;
            }

            if (iph.DeviceType() == PeripheralType.Mouse)
            {
                AsusMouse? am = iph as AsusMouse;
                if (am is null || !am.IsDeviceReady)
                {
                    //Should not happen if all device classes are implemented correctly. But better safe than sorry.
                    return;
                }
                mouseSettings = new AsusMouseSettings(am);
                mouseSettings.TopMost = AppConfig.Is("topmost");
                mouseSettings.FormClosed += MouseSettings_FormClosed;
                mouseSettings.Disposed += MouseSettings_Disposed;
                if (!mouseSettings.IsDisposed)
                {
                    mouseSettings.Show();
                }
                else
                {
                    mouseSettings = null;
                }

            }

            if (iph.DeviceType() == PeripheralType.Keyboard)
            {
                AsusKeyboard? kb = iph as AsusKeyboard;
                if (kb is null || !kb.IsDeviceReady)
                {
                    return;
                }
                ShowKeyboardSettings(kb);
            }
        }

        private void ShowKeyboardSettings(AsusKeyboard kb)
        {
            AsusKeyboardSettings.RequestReopen = ShowKeyboardSettings;
            keyboardSettings = new AsusKeyboardSettings(kb);
            keyboardSettings.TopMost = AppConfig.Is("topmost");
            keyboardSettings.FormClosed += KeyboardSettings_FormClosed;
            keyboardSettings.Disposed += KeyboardSettings_Disposed;
            if (!keyboardSettings.IsDisposed)
            {
                keyboardSettings.Show();
            }
            else
            {
                keyboardSettings = null;
            }
        }

        private void KeyboardSettings_Disposed(object? sender, EventArgs e)
        {
            keyboardSettings = null;
        }

        private void KeyboardSettings_FormClosed(object? sender, FormClosedEventArgs e)
        {
            keyboardSettings = null;
        }

        private void MouseSettings_Disposed(object? sender, EventArgs e)
        {
            mouseSettings = null;
        }

        private void MouseSettings_FormClosed(object? sender, FormClosedEventArgs e)
        {
            mouseSettings = null;
        }

        public void VisualiseAudio(double level)
        {
            if (InvokeRequired) { Invoke(() => VisualiseAudio(level)); return; }
            int filledSquares = (int)Math.Round(level/2);
            string squares = new string('|', filledSquares);
            labelMatrix.Text = $"Slash Lighting: {squares}";
        }

        public void VisualiseOverlay()
        {
            if (InvokeRequired) { Invoke(VisualiseOverlay); return; }
            bool enabled = AppConfig.IsOverlay();
            buttonOverlay.Activated = enabled;
            buttonOverlay.Text = enabled ? $"{Properties.Strings.Overlay}: {Properties.Strings.On}" : Properties.Strings.Overlay;
        }

        public void VisualiseFnLock()
        {
            if (InvokeRequired) { Invoke(VisualiseFnLock); return; }
            bool fnLock = AppConfig.Is("fn_lock");
            buttonFnLock.Activated = fnLock;
            buttonFnLock.AccessibleName = fnLock ? "Fn-Lock on" : "Fn-Lock off";
        }


        private void ButtonFnLock_Click(object? sender, EventArgs e)
        {
            buttonFnLock.Activated = !buttonFnLock.Activated;
            Task.Run(() => InputDispatcher.ToggleFnLock());
        }

        public void VisualiseMicAndPowerSaver()
        {
            if (InvokeRequired) { Invoke(VisualiseMicAndPowerSaver); return; }
            if (buttonMicMute != null)
            {
                bool muted = Audio.IsMicMuted();
                buttonMicMute.Activated = !muted;
                buttonMicMute.BorderColor = muted ? colorTurbo : colorEco;
                buttonMicMute.Text = muted ? "Mic: Muted" : "Mic: On";
            }
            if (buttonPowerSaver != null)
            {
                bool saver = PowerNative.IsPowerSaverActive();
                bool awake = PowerNative.IsKeepAwake;
                string awakeShort = PowerNative.GetKeepAwakeShortLabel();
                buttonPowerSaver.Activated = saver || awake;
                buttonPowerSaver.BorderColor = awake ? colorStandard : colorEco;
                buttonPowerSaver.Text = (saver && awake) ? $"Saver + {awakeShort}"
                                      : awake ? $"Awake: {awakeShort}"
                                      : saver ? "Power Saver: On"
                                      : "Power Saver";
            }
        }

        private void ButtonMicMute_Click(object? sender, EventArgs e)
        {
            Task.Run(() =>
            {
                InputDispatcher.ToggleMic();
            });
        }

        private void ButtonPowerSaver_Click(object? sender, EventArgs e)
        {
            Task.Run(() =>
            {
                bool active = PowerNative.TogglePowerSaver();
                Program.toast.RunToast(active ? "Power Saver: On" : "Power Saver: Off", ToastIcon.Battery);
                if (IsHandleCreated) BeginInvoke(VisualiseMicAndPowerSaver);
            });
        }

    }


}






























