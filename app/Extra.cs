using GHelper.Display;
using GHelper.Gpu.AMD;
using GHelper.Helpers;
using GHelper.Input;
using GHelper.Mode;
using GHelper.UI;
using GHelper.USB;
using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace GHelper
{
    public partial class Extra : RForm
    {

        ClamshellModeControl clamshellControl = new ClamshellModeControl();

        int[] vramOptions = [];

        int coresMinP = AsusACPI.PCoreMin;
        int coresMinE = AsusACPI.ECoreMin;

        const string EMPTY = "--------------";

        private bool _hasPower = true;
        private bool _hasAPU = false;
        private bool _hasCores = false;
        private bool _hasACPI = false;
        private bool _hasOptimalBrightness = false;


        private void SetKeyCombo(ComboBox combo, TextBox txbox, string name)
        {
            if (combo is RComboBox rcombo) rcombo.NativeHeight = true;

            Dictionary<string, string> customActions = new Dictionary<string, string>
            {
              {"", EMPTY},
              {"volume_down", Properties.Strings.VolumeDown},
              {"volume_up", Properties.Strings.VolumeUp},
              {"backlight_down", Properties.Strings.BacklightDown},
              {"backlight_up", Properties.Strings.BacklightUp},
              {"mute", Properties.Strings.VolumeMute},
              {"screenshot", Properties.Strings.PrintScreen},
              {"play", Properties.Strings.PlayPause},
              {"aura", Properties.Strings.ToggleAura},
              {"performance", Properties.Strings.PerformanceMode},
              {"screen", Properties.Strings.ToggleScreen},
              {"lock", Properties.Strings.LockScreen},
              {"miniled", Properties.Strings.ToggleMiniled},
              {"overdrive", "Toggle Overdrive"},
              {"hdr", "Toggle HDR"},
              {"autohdr", "Toggle Auto HDR"},
              {"fnlock", Properties.Strings.ToggleFnLock},
              {"brightness_down", Properties.Strings.BrightnessDown},
              {"brightness_up", Properties.Strings.BrightnessUp},
              {"visual", Properties.Strings.VisualMode},
              {"touchscreen", Properties.Strings.ToggleTouchscreen },
              {"penonly", "Toggle Pen Only Mode"},
              {"rotation", "Toggle Rotation Lock"},
              {"travelmode", "Toggle Ultra-Eco Travel Mode"},
              {"keepawake", "Toggle Keep Awake (Caffeine)"},
              {"smartcharge", "Toggle Smart Charge"},
              {"audiooutput", "Cycle Audio Output Device"},
              {"trimram", "Trim Standby RAM"},
              {"micmute", Properties.Strings.MuteMic},
              {"ghelper", Properties.Strings.OpenGHelper},
              {"overlay", Properties.Strings.Overlay},
              {"custom", Properties.Strings.Custom}
            };

            if (AppConfig.IsDUO())
            {
                customActions.Add("screenpad_down", Properties.Strings.ScreenPadDown);
                customActions.Add("screenpad_up", Properties.Strings.ScreenPadUp);
            }

            if (AppConfig.IsAlly())
            {
                customActions.Add("controller", "Controller Mode");
            }

            switch (name)
            {
                case "m1":
                    customActions[""] = Properties.Strings.VolumeDown;
                    customActions.Remove("volume_down");
                    break;
                case "m2":
                    customActions[""] = Properties.Strings.VolumeUp;
                    customActions.Remove("volume_up");
                    break;
                case "m3":
                    customActions[""] = Properties.Strings.MuteMic;
                    customActions.Remove("micmute");
                    break;
                case "m4":
                    customActions[""] = Properties.Strings.OpenGHelper;
                    customActions.Remove("ghelper");
                    break;
                case "m5":
                    customActions[""] = Properties.Strings.PerformanceMode;
                    customActions.Remove("performance");
                    break;
                case "fnf4":
                    customActions[""] = Properties.Strings.ToggleAura;
                    customActions.Remove("aura");
                    break;
                case "fnc":
                    customActions[""] = Properties.Strings.ToggleFnLock;
                    customActions.Remove("fnlock");
                    break;
                case "fnv":
                    customActions[""] = Properties.Strings.VisualMode;
                    customActions.Remove("visual");
                    break;
                case "fne":
                    customActions[""] = "Calculator";
                    break;
                case "paddle":
                    customActions[""] = EMPTY;
                    break;
                case "cc":
                    customActions[""] = EMPTY;
                    break;
            }

            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Items.Clear();
            foreach (var kv in customActions)
            {
                combo.Items.Add(new ComboOption(kv.Key, kv.Value));
            }

            string action = AppConfig.GetString(name) ?? "";
            SelectComboKey(combo, action);

            void SyncCustomBoxVisibility()
            {
                if (!combo.Visible)
                {
                    txbox.Visible = false;
                    return;
                }
                string selectedKey = (combo.SelectedItem as ComboOption)?.Key ?? "";
                bool isCustom = string.Equals(selectedKey, "custom", StringComparison.OrdinalIgnoreCase);
                txbox.Visible = isCustom;
                if (combo.Parent is TableLayoutPanel tlp)
                {
                    tlp.SetColumnSpan(combo, isCustom ? 1 : 2);
                }
            }

            combo.SelectionChangeCommitted += delegate
            {
                if (combo.SelectedItem is ComboOption opt)
                    AppConfig.Set(name, opt.Key);

                SyncCustomBoxVisibility();

                if (name == "m1" || name == "m2" || name == "m3" || name == "m4" || name == "m5")
                {
                    MKeyControl.ApplyAll();
                    Program.inputDispatcher.RegisterKeys();
                }

            };

            txbox.PlaceholderText = "Command / Path (Double-click to browse)";
            txbox.Text = AppConfig.GetString(name + "_custom");
            txbox.TextChanged += delegate
            {
                AppConfig.Set(name + "_custom", txbox.Text);
            };
            txbox.DoubleClick += delegate
            {
                using var dlg = new OpenFileDialog
                {
                    Title = "Select Application or Script for Custom Hotkey",
                    Filter = "Applications & Scripts (*.exe;*.bat;*.cmd;*.ps1)|*.exe;*.bat;*.cmd;*.ps1|All Files (*.*)|*.*"
                };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    txbox.Text = dlg.FileName.Contains(' ') ? $"\"{dlg.FileName}\"" : dlg.FileName;
                }
            };
            toolTip.SetToolTip(txbox, "Enter a custom command, keystroke (e.g. ^!t), or double-click to browse for an application/script.");

            SyncCustomBoxVisibility();
        }

        public Extra()
        {
            InitializeComponent();

            labelBindings.Text = Properties.Strings.KeyBindings;
            buttonResetBindings.Text = Properties.Strings.Reset;
            labelBacklightTitle.Text = Properties.Strings.LaptopBacklight;
            labelSettings.Text = Properties.Strings.Other;

            checkAwake.Text = Properties.Strings.Awake;
            checkSleep.Text = Properties.Strings.Sleep;
            checkBoot.Text = checkBootLogo.Text = checkBootBar.Text = checkBootLid.Text = Properties.Strings.Boot + "/" + Properties.Strings.Shutdown;
            checkBattery.Text = checkBatteryLogo.Text = checkBatteryBar.Text = checkBatteryLid.Text = Properties.Strings.Battery;
            checkBootSound.Text = Properties.Strings.BootSound;
            checkKeystoneSound.Text = Properties.Strings.KeystoneSound;
            checkStatusLed.Text = Properties.Strings.LEDStatusIndicators;

            labelSpeed.Text = Properties.Strings.AnimationSpeed;
            //labelBrightness.Text = Properties.Strings.Brightness;

            labelBacklightTimeout.Text = Properties.Strings.BacklightTimeout;
            //labelBacklightTimeoutPlugged.Text = Properties.Strings.BacklightTimeoutPlugged;

            checkNoOverdrive.Text = Properties.Strings.DisableOverdrive;
            checkELMB.Text = Properties.Strings.ExtremeLowMotionBlur;
            checkTopmost.Text = Properties.Strings.WindowTop;
            checkUSBC.Text = Properties.Strings.OptimizedUSBC;
            checkUSBCPowerLimit.Text = "Auto-limit power on USB-C charger (prevents battery discharge under load)";
            checkAutoToggleClamshellMode.Text = Properties.Strings.ToggleClamshellMode;

            labelBacklightKeyboard.Text = Properties.Strings.Keyboard;
            labelBacklightBar.Text = Properties.Strings.Lightbar;
            labelBacklightLid.Text = Properties.Strings.Lid;
            labelBacklightLogo.Text = Properties.Strings.Logo;

            checkGpuApps.Text = Properties.Strings.KillGpuApps;
            checkAspm.Text = Properties.Strings.DisablePCIeASPM;
            checkStandbyNetworking.Text = Properties.Strings.DisableStandbyNetworking;
            checkNVPlatform.Text = Properties.Strings.StopStartNVServices;
            numericBacklightTime.OffText = Properties.Strings.Off;
            numericBacklightPluggedTime.OffText = Properties.Strings.Off;

            labelAPUMem.Text = Properties.Strings.APUMemory;
            labelCores.Text = Properties.Strings.CPUCoresConfiguration;

            labelOptimalBrightness.Text = Properties.Strings.OptimalDisplayBrightness;
            comboOptimalBrightness.Items[0] = Properties.Strings.Off;
            comboOptimalBrightness.Items[1] = Properties.Strings.OnAlways;
            comboOptimalBrightness.Items[2] = Properties.Strings.OnBattery;

            Text = Properties.Strings.ExtraSettings;

            // Accessible Labels

            panelServices.AccessibleName = Properties.Strings.AsusServicesRunning;
            panelBindings.AccessibleName = Properties.Strings.KeyBindings;
            tableBindings.AccessibleName = Properties.Strings.KeyBindings;

            comboM1.AccessibleName = "M1 Action";
            comboM2.AccessibleName = "M2 Action";
            comboM3.AccessibleName = "M3 Action";
            comboM4.AccessibleName = "M4 Action";
            comboM5.AccessibleName = "M5 Action";

            labelM5.Visible = comboM5.Visible = textM5.Visible = false;
            comboFNF4.AccessibleName = "Fn+F4 Action";
            comboFNC.AccessibleName = "Fn+C Action";
            comboFNV.AccessibleName = "Fn+V Action";
            comboFNE.AccessibleName = "Fn+Numpad Action";

            numericBacklightPluggedTime.AccessibleName = Properties.Strings.BacklightTimeoutPlugged;
            numericBacklightTime.AccessibleName = Properties.Strings.BacklightTimeoutBattery;

            comboKeyboardSpeed.AccessibleName = Properties.Strings.LaptopBacklight + " " + Properties.Strings.AnimationSpeed;
            comboAPU.AccessibleName = Properties.Strings.LaptopBacklight + " " + Properties.Strings.AnimationSpeed;

            checkBoot.AccessibleName = Properties.Strings.Boot + "/" + Properties.Strings.Shutdown + " " + Properties.Strings.LaptopBacklight;
            checkAwake.AccessibleName = Properties.Strings.Awake + " " + Properties.Strings.LaptopBacklight;
            checkSleep.AccessibleName = Properties.Strings.Sleep + " " + Properties.Strings.LaptopBacklight;

            panelSettings.AccessibleName = Properties.Strings.ExtraSettings;

            if (AppConfig.IsARCNM())
            {
                labelM3.Text = "FN+F6";
                labelM1.Visible = comboM1.Visible = textM1.Visible = false;
                labelM2.Visible = comboM2.Visible = textM2.Visible = false;
                labelM4.Visible = comboM4.Visible = textM4.Visible = false;
                labelFNF4.Visible = comboFNF4.Visible = textFNF4.Visible = false;
            }

            if (AppConfig.NoMKeys())
            {
                labelM1.Text = "FN+F2";
                labelM2.Text = "FN+F3";
                labelM3.Text = "FN+F4";
                labelM4.Visible = comboM4.Visible = textM4.Visible = AppConfig.IsM4Button();
                labelFNF4.Visible = comboFNF4.Visible = textFNF4.Visible = false;
            }

            if (AppConfig.IsVivoZenPro())
            {
                labelM1.Visible = comboM1.Visible = textM1.Visible = false;
                labelM2.Visible = comboM2.Visible = textM2.Visible = false;
                labelM3.Visible = comboM3.Visible = textM3.Visible = false;
                labelFNF4.Visible = comboFNF4.Visible = textFNF4.Visible = false;
                labelM4.Text = "FN+F12";
            }

            if (AppConfig.MediaKeys())
            {
                labelFNF4.Visible = comboFNF4.Visible = textFNF4.Visible = false;
            }

            if (!AppConfig.IsTUF())
            {
                labelFNE.Visible = comboFNE.Visible = textFNE.Visible = false;
            }

            if (AppConfig.IsNoFNV())
            {
                labelFNV.Visible = comboFNV.Visible = textFNV.Visible = false;
            }

            if (!Program.acpi.IsSupported(AsusACPI.GPUEco))
            {
                checkGpuApps.Visible = false;
                checkUSBC.Visible = false;
            }

            checkNoOverdrive.Visible = Program.acpi.IsOverdriveSupported();

            // Change text and hide irrelevant options on the ROG Ally,
            // which is a bit of a special case piece of hardware.
            if (AppConfig.IsAlly())
            {
                labelM1.Visible = comboM1.Visible = textM1.Visible = false;
                labelM2.Visible = comboM2.Visible = textM2.Visible = false;

                // Re-label M3 and M4 and FNF4 to match the front labels.
                labelM3.Text = "Cmd Center";
                labelM4.Text = "ROG";
                labelFNF4.Text = "Back Paddles";

                // Hide all of the FN options, as the Ally has no special keyboard FN key.
                labelFNC.Visible = false;
                comboFNC.Visible = false;
                textFNC.Visible = false;

                labelFNV.Visible = false;
                comboFNV.Visible = false;
                textFNV.Visible = false;

                SetKeyCombo(comboM3, textM3, "cc");
                SetKeyCombo(comboM4, textM4, "m4");
                SetKeyCombo(comboFNF4, textFNF4, "paddle");

                checkGpuApps.Visible = false;
                checkUSBC.Visible = false;
                checkAutoToggleClamshellMode.Visible = false;
            }
            else
            {
                SetKeyCombo(comboM1, textM1, "m1");
                SetKeyCombo(comboM2, textM2, "m2");

                SetKeyCombo(comboM3, textM3, "m3");
                SetKeyCombo(comboM4, textM4, "m4");
                SetKeyCombo(comboFNF4, textFNF4, "fnf4");

                SetKeyCombo(comboFNC, textFNC, "fnc");
                SetKeyCombo(comboFNV, textFNV, "fnv");
                SetKeyCombo(comboFNE, textFNE, "fne");
            }

            if (AppConfig.IsStrix())
            {
                labelM4.Text = "M5/ROG";
                labelM5.Visible = comboM5.Visible = textM5.Visible = true;
                SetKeyCombo(comboM5, textM5, "m5");
            }


            InitTheme();
            InitAppearanceAndBackupCard();

            Shown += Keyboard_Shown;

            comboKeyboardSpeed.DropDownStyle = ComboBoxStyle.DropDownList;
            comboKeyboardSpeed.Items.Clear();
            foreach (var kv in Aura.GetSpeeds())
            {
                comboKeyboardSpeed.Items.Add(new ComboOption(kv.Key.ToString(), kv.Value));
            }
            SelectComboKey(comboKeyboardSpeed, Aura.Speed.ToString());
            comboKeyboardSpeed.SelectionChangeCommitted += ComboKeyboardSpeed_SelectedValueChanged;

            // Keyboard
            checkAwake.Checked = AppConfig.IsNotFalse("keyboard_awake");
            checkBattery.Checked = AppConfig.IsOnBattery("keyboard_awake");
            checkBoot.Checked = AppConfig.IsNotFalse("keyboard_boot");
            checkSleep.Checked = AppConfig.IsNotFalse("keyboard_sleep");

            // Lightbar
            checkAwakeBar.Checked = AppConfig.IsNotFalse("keyboard_awake_bar");
            checkBatteryBar.Checked = AppConfig.IsOnBattery("keyboard_awake_bar");
            checkBootBar.Checked = AppConfig.IsNotFalse("keyboard_boot_bar");
            checkSleepBar.Checked = AppConfig.IsNotFalse("keyboard_sleep_bar");

            // Lid
            checkAwakeLid.Checked = AppConfig.IsNotFalse("keyboard_awake_lid");
            checkBatteryLid.Checked = AppConfig.IsOnBattery("keyboard_awake_lid");
            checkBootLid.Checked = AppConfig.IsNotFalse("keyboard_boot_lid");
            checkSleepLid.Checked = AppConfig.IsNotFalse("keyboard_sleep_lid");

            // Logo
            checkAwakeLogo.Checked = AppConfig.IsNotFalse("keyboard_awake_logo");
            checkBatteryLogo.Checked = AppConfig.IsOnBattery("keyboard_awake_logo");
            checkBootLogo.Checked = AppConfig.IsNotFalse("keyboard_boot_logo");
            checkSleepLogo.Checked = AppConfig.IsNotFalse("keyboard_sleep_logo");

            checkAwake.CheckedChanged += CheckPower_CheckedChanged;
            checkBattery.CheckedChanged += CheckPower_CheckedChanged;
            checkBoot.CheckedChanged += CheckPower_CheckedChanged;
            checkSleep.CheckedChanged += CheckPower_CheckedChanged;

            checkAwakeBar.CheckedChanged += CheckPower_CheckedChanged;
            checkBatteryBar.CheckedChanged += CheckPower_CheckedChanged;
            checkBootBar.CheckedChanged += CheckPower_CheckedChanged;
            checkSleepBar.CheckedChanged += CheckPower_CheckedChanged;

            checkAwakeLid.CheckedChanged += CheckPower_CheckedChanged;
            checkBatteryLid.CheckedChanged += CheckPower_CheckedChanged;
            checkBootLid.CheckedChanged += CheckPower_CheckedChanged;
            checkSleepLid.CheckedChanged += CheckPower_CheckedChanged;

            checkAwakeLogo.CheckedChanged += CheckPower_CheckedChanged;
            checkBatteryLogo.CheckedChanged += CheckPower_CheckedChanged;
            checkBootLogo.CheckedChanged += CheckPower_CheckedChanged;
            checkSleepLogo.CheckedChanged += CheckPower_CheckedChanged;

            if (!AppConfig.IsBacklightZones() || AppConfig.IsARCNM())
            {
                labelBacklightKeyboard.Visible = false;
                checkBattery.Visible = false;
            }

            if (!Aura.HasLightbar)
            {
                labelBacklightBar.Visible = false;
                checkAwakeBar.Visible = false;
                checkBatteryBar.Visible = false;
                checkBootBar.Visible = false;
                checkSleepBar.Visible = false;
            }

            if (!Aura.HasLogo)
            {
                labelBacklightLogo.Visible = false;
                checkAwakeLogo.Visible = false;
                checkBatteryLogo.Visible = false;
                checkBootLogo.Visible = false;
                checkSleepLogo.Visible = false;
            }

            if (!Aura.HasRearglow)
            {
                labelBacklightLid.Visible = false;
                checkAwakeLid.Visible = false;
                checkBatteryLid.Visible = false;
                checkBootLid.Visible = false;
                checkSleepLid.Visible = false;
            }

            //checkAutoToggleClamshellMode.Visible = clamshellControl.IsExternalDisplayConnected();
            checkAutoToggleClamshellMode.Checked = AppConfig.Is("toggle_clamshell_mode");
            checkAutoToggleClamshellMode.CheckedChanged += checkAutoToggleClamshellMode_CheckedChanged;

            checkTopmost.Checked = AppConfig.Is("topmost");
            checkTopmost.CheckedChanged += CheckTopmost_CheckedChanged; ;

            checkNoOverdrive.Checked = AppConfig.IsNoOverdrive();
            checkNoOverdrive.CheckedChanged += CheckNoOverdrive_CheckedChanged;

            int elmb = ScreenELMB.Get();
            checkELMB.Visible = elmb >= 0;
            checkELMB.Checked = elmb == 1;
            checkELMB.CheckedChanged += CheckELMB_CheckedChanged;

            checkUSBC.Checked = AppConfig.Is("optimized_usbc");
            checkUSBC.CheckedChanged += CheckUSBC_CheckedChanged;

            checkUSBCPowerLimit.Checked = AppConfig.Get("limit_power_usbc", 1) == 1;
            checkUSBCPowerLimit.CheckedChanged += (s, e) =>
            {
                AppConfig.Set("limit_power_usbc", checkUSBCPowerLimit.Checked ? 1 : 0);
                Program.modeControl?.SetPower(false);
                Program.modeControl?.SetGPUClocks(false);
            };

            sliderBrightness.Value = InputDispatcher.GetBacklight();
            sliderBrightness.AccessibleName = Properties.Strings.LaptopBacklight + ": " + sliderBrightness.Value;
            sliderBrightness.ValueChanged += SliderBrightness_ValueChanged;

            panelXGM.Visible = XGM.IsConnected() || Program.acpi.IsXGConnected() || AppConfig.IsXGM() || AppConfig.ContainsModel("GV601") || AppConfig.ContainsModel("GV301") || AppConfig.ContainsModel("GZ301") || XGM.AlwaysShowFanCurve();
            checkXGM.Checked = !(AppConfig.Get("xmg_light") == 0);
            checkXGM.CheckedChanged += CheckXGM_CheckedChanged;

            int xgmBrightness = AppConfig.Get("xgm_brightness", 80);
            if (xgmBrightness <= 0 && checkXGM.Checked) xgmBrightness = 80;
            sliderXGMBrightness.Value = checkXGM.Checked ? xgmBrightness : 0;
            sliderXGMBrightness.AccessibleName = "XG Mobile Brightness: " + sliderXGMBrightness.Value + "%";
            sliderXGMBrightness.ValueChanged += SliderXGMBrightness_ValueChanged;

            checkXGMBattery.Checked = AppConfig.Is("xgm_battery_limit");
            checkXGMBattery.CheckedChanged += CheckXGMBattery_CheckedChanged;

            checkXGMMidBoost.Checked = AppConfig.Is("xgm_mid_boost");
            checkXGMMidBoost.CheckedChanged += CheckXGMMidBoost_CheckedChanged;

            // Add XG Mobile Fan Curve Controls to panelXGM
            var checkXGMIndepFan = new RCheckBox
            {
                AutoSize = true,
                Location = new Point(checkXGMBattery.Left, checkXGMBattery.Bottom + 4),
                Margin = checkXGMBattery.Margin,
                Padding = checkXGMBattery.Padding,
                Font = checkXGMBattery.Font,
                ForeColor = checkXGMBattery.ForeColor,
                BackColor = checkXGMBattery.BackColor,
                Text = "Independent XGM Fan Curve",
                Checked = XGM.IsIndependentFanEnabled()
            };
            checkXGMIndepFan.CheckedChanged += (s, e) =>
            {
                AppConfig.Set("xgm_independent_fan", checkXGMIndepFan.Checked ? 1 : 0);
                Program.modeControl?.AutoFans();
                if (Program.settingsForm?.fansForm is { Text.Length: > 0 } ff) ff.InitFans();
            };

            var checkXGMShowFan = new RCheckBox
            {
                AutoSize = true,
                Location = new Point(checkXGMMidBoost.Left, checkXGMMidBoost.Bottom + 4),
                Margin = checkXGMMidBoost.Margin,
                Padding = checkXGMMidBoost.Padding,
                Font = checkXGMMidBoost.Font,
                ForeColor = checkXGMMidBoost.ForeColor,
                BackColor = checkXGMMidBoost.BackColor,
                Text = "Always show XGM Fan Curve",
                Checked = XGM.AlwaysShowFanCurve()
            };
            checkXGMShowFan.CheckedChanged += (s, e) =>
            {
                AppConfig.Set("show_xgm_fan", checkXGMShowFan.Checked ? 1 : 0);
                if (Program.settingsForm?.fansForm is { Text.Length: > 0 } ff) ff.InitFans();
            };

            panelXGM.Controls.Add(checkXGMIndepFan);
            panelXGM.Controls.Add(checkXGMShowFan);
            panelXGM.Height = checkXGMIndepFan.Bottom + 8;

            numericBacklightTime.Value = AppConfig.Get("keyboard_timeout", 60);
            numericBacklightPluggedTime.Value = AppConfig.Get("keyboard_ac_timeout", 0);

            numericBacklightTime.ValueChanged += NumericBacklightTime_ValueChanged;
            numericBacklightPluggedTime.ValueChanged += NumericBacklightTime_ValueChanged;

            checkAutoBacklight.Text = "Auto-dim keyboard backlight on battery";
            checkAutoBacklight.Checked = AppConfig.Is("auto_keyboard_backlight");
            checkAutoBacklight.CheckedChanged += (s, e) =>
            {
                AppConfig.Set("auto_keyboard_backlight", checkAutoBacklight.Checked ? 1 : 0);
                InputDispatcher.SetBacklightAuto();
                Program.settingsForm?.VisualiseBacklight(InputDispatcher.GetBacklight());
            };

            checkGpuApps.Checked = AppConfig.Is("kill_gpu_apps");
            checkGpuApps.CheckedChanged += CheckGpuApps_CheckedChanged;

            int bootSound = Program.acpi.DeviceGet(AsusACPI.BootSound);
            checkBootSound.Visible = bootSound >= 0;
            if (bootSound < 0 || bootSound > UInt16.MaxValue) bootSound = AppConfig.Get("boot_sound", 0);

            checkBootSound.Checked = (bootSound == 1);
            checkBootSound.CheckedChanged += CheckBootSound_CheckedChanged;

            var statusLed = Program.acpi.DeviceGet(AsusACPI.StatusLed);
            checkStatusLed.Visible = statusLed >= 0;
            checkStatusLed.Checked = (statusLed > 0);
            checkStatusLed.CheckedChanged += CheckLEDStatus_CheckedChanged;

            int numberPad = AppConfig.IsNumberPad() ? NumberPad.Get() : -1;
            checkNumberPad.Visible = numberPad >= 0;
            checkNumberPad.Checked = numberPad == 1;
            checkNumberPad.CheckedChanged += CheckNumberPad_CheckedChanged;

            var optimalBrightness = ScreenControl.GetOptimalBrightness();
            if (optimalBrightness >= 0)
            {
                _hasOptimalBrightness = true;
                panelOptimalBrightness.Visible = true;
                comboOptimalBrightness.DropDownStyle = ComboBoxStyle.DropDownList;
                comboOptimalBrightness.SelectedIndex = AppConfig.Get("optimal_brightness", optimalBrightness);
                comboOptimalBrightness.SelectedIndexChanged += OptimalBrightness_Changed;
            }

            pictureHelp.Click += PictureHelp_Click;
            buttonResetBindings.Click += ButtonResetBindings_Click;
            buttonServices.Click += ButtonServices_Click;

            pictureLog.Click += PictureLog_Click;

            checkNVPlatform.Visible = Program.acpi.IsNVidiaGPU();
            checkNVPlatform.Checked = AppConfig.IsNVPlatform();
            checkNVPlatform.CheckedChanged += CheckNVPlatform_CheckedChanged;

            checkAspm.Checked = AppConfig.IsAutoASPM();
            checkAspm.CheckedChanged += CheckAspm_CheckedChanged;

            checkStandbyNetworking.Checked = AppConfig.IsAutoStandbyNetworking();
            checkStandbyNetworking.CheckedChanged += CheckStandbyNetworking_CheckedChanged;

            checkKeystoneSound.Visible = AppConfig.IsKeystone();
            checkKeystoneSound.Checked = Keystone.IsEnabled();
            checkKeystoneSound.CheckedChanged += CheckKeystoneSoundCheckedChanged;

            // Add Convertible, Stylus & Smart Battery Automation checkboxes to panelSettings
            RCheckBox CreateExtraCheckBox(string text, bool isChecked, EventHandler onChanged, string tip)
            {
                var cb = new RCheckBox
                {
                    AutoSize = true,
                    Dock = DockStyle.Top,
                    Margin = checkTopmost.Margin,
                    Padding = checkTopmost.Padding,
                    Font = checkTopmost.Font,
                    ForeColor = checkTopmost.ForeColor,
                    BackColor = checkTopmost.BackColor,
                    Text = text,
                    Checked = isChecked
                };
                cb.CheckedChanged += onChanged;
                toolTip.SetToolTip(cb, tip);
                panelSettings.Controls.Add(cb);
                return cb;
            }

            CreateExtraCheckBox(
                "Auto Windows Power Saver at ≤ 20% battery",
                AppConfig.Get("auto_low_battery_saver", 1) == 1,
                (s, e) => AppConfig.Set("auto_low_battery_saver", ((RCheckBox)s!).Checked ? 1 : 0),
                "Automatically enables Windows Power Saver & Energy Saver when battery drops to 20% or below while discharging."
            );

            CreateExtraCheckBox(
                "Auto Pen-Only (palm rejection) in Drawing & Note apps",
                AppConfig.Is("auto_pen_drawing_apps"),
                (s, e) =>
                {
                    bool en = ((RCheckBox)s!).Checked;
                    GameWatcher.TogglePenDrawingApps(en);
                },
                "Automatically switches touchscreen to Pen-Only Mode (disables finger touch, keeps stylus active) when OneNote, Drawboard PDF, Krita, Photoshop, or Journal is in the foreground."
            );

            if (AppConfig.HasTabletMode() || AutoRotationControl.IsSupported())
            {
                CreateExtraCheckBox(
                    "Auto-unlock Screen Rotation in Tablet / Tent posture",
                    AppConfig.Get("auto_tablet_rotation", 1) == 1,
                    (s, e) => AppConfig.Set("auto_tablet_rotation", ((RCheckBox)s!).Checked ? 1 : 0),
                    "Automatically unlocks screen auto-rotation when folded into Tablet or Tent mode, and locks rotation in Laptop mode."
                );

                CreateExtraCheckBox(
                    "Auto-Silent performance mode in Tablet posture",
                    AppConfig.Get("auto_tablet_silent", 1) == 1,
                    (s, e) => AppConfig.Set("auto_tablet_silent", ((RCheckBox)s!).Checked ? 1 : 0),
                    "Automatically switches to Silent mode when folded into Tablet mode to keep the chassis cool and quiet in your hands, and restores your previous mode in Laptop posture."
                );
            }

            CreateExtraCheckBox(
                "Auto Creator Studio Profile (512-Zone Mini-LED + 165Hz in Photoshop / Premiere / Resolve / Blender)",
                GameWatcher.IsCreatorAppsEnabled(),
                (s, e) => GameWatcher.ToggleCreatorApps(((RCheckBox)s!).Checked),
                "Automatically enables 512-Zone Mini-LED local dimming and maximum refresh rate when creative applications are in the foreground on AC power."
            );

            CreateExtraCheckBox(
                "Smart Thermal Alert toast when CPU ≥ 92°C or GPU ≥ 88°C sustained",
                AppConfig.Is("thermal_alert_enabled"),
                (s, e) =>
                {
                    bool en = ((RCheckBox)s!).Checked;
                    AppConfig.Set("thermal_alert_enabled", en ? 1 : 0);
                    if (en && SettingsForm.sensorTimer != null) SettingsForm.sensorTimer.Enabled = true;
                    Program.settingsForm?.SetContextMenu();
                },
                "Displays a non-intrusive toast alert with a 5-minute cooldown if CPU or GPU temperature remains critically high."
            );

            CreateExtraCheckBox(
                "Smart Battery Alert toast when battery reaches Charge Limit target",
                AppConfig.Is("battery_alert_enabled"),
                (s, e) =>
                {
                    bool en = ((RCheckBox)s!).Checked;
                    AppConfig.Set("battery_alert_enabled", en ? 1 : 0);
                    if (en && SettingsForm.sensorTimer != null) SettingsForm.sensorTimer.Enabled = true;
                    Program.settingsForm?.SetContextMenu();
                },
                "Notifies you with a toast alert once per charge cycle when your battery reaches its configured Charge Limit (e.g. 80%, 85%, or 100%)."
            );

            CreateExtraCheckBox(
                "Wi-Fi Low-Latency Gaming Mode (eliminates MediaTek/Intel Wi-Fi power-saving ping spikes)",
                WifiLatencyHelper.IsLowLatencyActive(),
                (s, e) =>
                {
                    bool en = ((RCheckBox)s!).Checked;
                    WifiLatencyHelper.SetLowLatency(en, true);
                    Program.settingsForm?.SetContextMenu();
                },
                "Forces Wireless Adapter Power Saving Mode to Maximum Performance (0) and enables WLAN Media Streaming Mode to prevent packet latency spikes."
            );

            CreateExtraCheckBox(
                "Auto Low-Latency Wi-Fi when a game is running",
                AppConfig.Is("auto_wifi_gaming"),
                (s, e) =>
                {
                    bool en = ((RCheckBox)s!).Checked;
                    AppConfig.Set("auto_wifi_gaming", en ? 1 : 0);
                    Program.settingsForm?.SetContextMenu();
                },
                "Automatically switches Wi-Fi to Low-Latency Maximum Performance mode while gaming and restores Eco power saving afterwards."
            );

            CreateExtraCheckBox(
                "Show Convertible Posture Touch Bar when folded into Tablet or Tent mode",
                AppConfig.IsNotFalse("posture_osd_enabled"),
                (s, e) =>
                {
                    bool en = ((RCheckBox)s!).Checked;
                    AppConfig.Set("posture_osd_enabled", en ? 1 : 0);
                    Program.settingsForm?.SetContextMenu();
                },
                "Displays a sleek, touch-friendly floating mini bar for 6 seconds when folding into Tablet or Tent mode with 1-tap Touch/Pen, Rotate, Silent, 1Z/512Z, and Brightness controls."
            );

            CreateExtraCheckBox(
                "Show Live 60s Sensor Sparkline Graph in Main Window",
                AppConfig.Is("show_main_sparkline"),
                (s, e) =>
                {
                    bool en = ((RCheckBox)s!).Checked;
                    AppConfig.Set("show_main_sparkline", en ? 1 : 0);
                    Program.settingsForm?.SetMainSparklineVisible(en);
                    Program.settingsForm?.SetContextMenu();
                },
                "Displays a live 60-second rolling sparkline graph of CPU/GPU temperatures or Battery Wattage in the main G-Helper window."
            );

            // Keep Appearance & Backup card at the very top of the System tab
            panelSettings.Controls.SetChildIndex(panelTheme, panelSettings.Controls.Count - 1);

            toolTip.SetToolTip(checkAutoToggleClamshellMode, Properties.Strings.ClamshellModeTooltip);
            toolTip.SetToolTip(checkNVPlatform, Properties.Strings.NVPlatformTooltip);
            toolTip.SetToolTip(checkAspm, Properties.Strings.DisablePCIeASPMTooltip);
            toolTip.SetToolTip(checkStandbyNetworking, Properties.Strings.DisableStandbyNetworkingTooltip);
            toolTip.SetToolTip(checkTopmost, "Keeps the G-Helper main window pinned above other desktop windows.");
            toolTip.SetToolTip(checkNoOverdrive, "Keeps panel Overdrive disabled even at high refresh rates on AC power.");
            toolTip.SetToolTip(checkUSBC, "Prevents the discrete NVIDIA/AMD GPU from automatically waking up in Optimized mode when powered via a lower-wattage USB-C PD charger.");
            toolTip.SetToolTip(checkUSBCPowerLimit, "When connected to a 65W or 100W USB-C charger, automatically limits CPU power to 35W and GPU boost to 45W so total power draw does not exceed charger capacity, preventing the battery from discharging while plugged in.");
            toolTip.SetToolTip(checkGpuApps, "Automatically terminates non-essential background apps holding onto the discrete GPU before switching to Eco mode.");
            toolTip.SetToolTip(checkBootSound, "Enables or disables the ASUS ROG startup sound effect in BIOS.");
            toolTip.SetToolTip(checkXGMBattery, "Automatically caps laptop battery charge at 80% while docked to the ROG XG Mobile to prevent heat/voltage wear.");
            toolTip.SetToolTip(checkXGMMidBoost, "Syncs the laptop's 3rd (Middle/System) fan curve to match the CPU fan curve while the XG Mobile is docked.");
            toolTip.SetToolTip(checkXGMIndepFan, "Applies your custom XG Mobile fan curve whenever the XG Mobile is connected, even if laptop BIOS custom fan curves are unchecked.");
            toolTip.SetToolTip(checkXGMShowFan, "Always displays the XG Mobile fan curve chart in Fans & Power so you can pre-configure curves while unplugged.");
            toolTip.SetToolTip(checkAutoBacklight, "Automatically dims the keyboard backlight on battery power to save energy.");

            InitCores();
            InitPowerSettings();
            InitVramMem();

            InitACPITesting();

            buttonTabKeyboard.Click += (s, e) => ShowTab(0);
            buttonTabLighting.Click += (s, e) => ShowTab(1);
            buttonTabPower.Click += (s, e) => ShowTab(2);
            buttonTabSystem.Click += (s, e) => ShowTab(3);

            int savedTab = Math.Clamp(AppConfig.Get("extra_tab", 0), 0, 3);
            tableTabs.BringToFront();
            ShowTab(savedTab);

            ControlHelper.Adjust(this);
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

        private Action? _syncAppearanceCombos;
        private int _baseFormClientWidth = 0;

        public void ShowTab(int index)
        {
            AppConfig.Set("extra_tab", index);

            SuspendLayout();
            try
            {
                tableTabs.BringToFront();
                AutoScrollPosition = new Point(0, 0);

                buttonTabKeyboard.Activated = (index == 0);
                buttonTabLighting.Activated = (index == 1);
                buttonTabPower.Activated = (index == 2);
                buttonTabSystem.Activated = (index == 3);

                buttonTabKeyboard.BorderColor = RForm.colorStandard;
                buttonTabLighting.BorderColor = RForm.colorStandard;
                buttonTabPower.BorderColor = RForm.colorEco;
                buttonTabSystem.BorderColor = RForm.colorTurbo;

                // Tab 0: Keyboard
                panelBindingsHeader.Visible = (index == 0);
                panelBindings.Visible = (index == 0);

                // Tab 1: Lighting
                panelBacklightHeader.Visible = (index == 1);
                panelBacklight.Visible = (index == 1);

                // Tab 2: Power
                panelPowerHeader.Visible = (index == 2);
                panelPower.Visible = (index == 2 && _hasPower);
                panelAPU.Visible = (index == 2 && _hasAPU);
                panelCores.Visible = (index == 2 && _hasCores);
                panelACPI.Visible = (index == 2 && _hasACPI);

                // Tab 3: System
                panelSettingsHeader.Visible = (index == 3);
                panelSettings.Visible = (index == 3);
                panelOptimalBrightness.Visible = (index == 3 && _hasOptimalBrightness);
                panelServices.Visible = (index == 3);

                if (index == 3)
                {
                    _syncAppearanceCombos?.Invoke();
                }
            }
            finally
            {
                ResumeLayout(true);
            }

            if (IsHandleCreated && Visible)
            {
                UpdateTabWindowSize();
                Invalidate(true);
            }
        }

        private void UpdateTabWindowSize()
        {
            if (Program.settingsForm == null || Program.settingsForm.IsDisposed) return;

            Rectangle workArea = Screen.FromControl(Program.settingsForm).WorkingArea;
            int maxAllowedHeight = workArea.Height - 20;

            int borderChromeHeight = Height - ClientSize.Height;
            int borderChromeWidth = Width - ClientSize.Width;

            if (_baseFormClientWidth <= 0)
            {
                _baseFormClientWidth = Math.Max(tableTabs.Width + Padding.Horizontal, ClientSize.Width);
            }

            AutoSize = false;
            MaximumSize = Size.Empty;
            MinimumSize = new Size(300, 180);

            // Reset scroll state and client width first so DockStyle.Top AutoSize panels measure accurately
            AutoScrollPosition = new Point(0, 0);
            AutoScroll = false;
            ClientSize = new Size(_baseFormClientWidth, ClientSize.Height);
            PerformLayout();

            int contentHeight = Padding.Top + Padding.Bottom;
            int maxBottom = 0;
            foreach (Control ctrl in Controls)
            {
                if (!ctrl.Visible) continue;
                ctrl.PerformLayout();
                contentHeight += ctrl.Height + ctrl.Margin.Vertical;
                if (ctrl.Bottom + ctrl.Margin.Bottom > maxBottom)
                    maxBottom = ctrl.Bottom + ctrl.Margin.Bottom;
            }

            int measuredClientHeight = Math.Max(contentHeight, maxBottom + Padding.Bottom) + 6;
            int desiredTotalHeight = measuredClientHeight + borderChromeHeight;

            if (desiredTotalHeight > maxAllowedHeight)
            {
                int scrollPad = SystemInformation.VerticalScrollBarWidth + 6;
                AutoScroll = true;
                Size = new Size(_baseFormClientWidth + borderChromeWidth + scrollPad, maxAllowedHeight);
                Top = workArea.Top + 10;
            }
            else
            {
                AutoScroll = false;
                Size = new Size(_baseFormClientWidth + borderChromeWidth, desiredTotalHeight);
                if (Height > Program.settingsForm.Height)
                {
                    int top = Program.settingsForm.Top + Program.settingsForm.Height - Height;
                    Top = Math.Max(workArea.Top + 10, top);
                }
                else
                {
                    Top = Math.Clamp(Program.settingsForm.Top, workArea.Top + 10, Math.Max(workArea.Top + 10, workArea.Bottom - Height - 10));
                }
            }

            Left = Math.Max(workArea.Left + 10, Program.settingsForm.Left - Width - 5);
        }

        private void InitAppearanceAndBackupCard()
        {
            panelTheme.SuspendLayout();
            panelTheme.Controls.Clear();
            panelTheme.AutoSize = true;
            panelTheme.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelTheme.Padding = new Padding(4, 6, 4, 10);

            var table = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 6,
                Margin = new Padding(0)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));

            Control MakeRowHeader(string title, string tip, Action<Graphics, RectangleF, Color> drawIcon)
            {
                var rowPanel = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    AutoSize = true,
                    ColumnCount = 2,
                    RowCount = 1,
                    Margin = new Padding(0),
                    Padding = new Padding(0),
                    BackColor = Color.Transparent
                };
                int iconPx = Math.Max(18, (int)Math.Round(18 * (DeviceDpi / 96f)));
                rowPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, iconPx + 8));
                rowPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

                var pic = new PictureBox
                {
                    Size = new Size(iconPx, iconPx),
                    Margin = new Padding(2, 4, 4, 4),
                    Anchor = AnchorStyles.Left | AnchorStyles.None,
                    BackColor = Color.Transparent
                };
                pic.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    drawIcon(e.Graphics, new RectangleF(1, 1, pic.Width - 2, pic.Height - 2), RForm.foreMain);
                };

                var lbl = new Label
                {
                    AutoSize = true,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Text = title,
                    Margin = new Padding(0, 4, 3, 4),
                    ForeColor = RForm.foreMain,
                    BackColor = Color.Transparent
                };

                toolTip.SetToolTip(rowPanel, tip);
                toolTip.SetToolTip(pic, tip);
                toolTip.SetToolTip(lbl, tip);

                rowPanel.Controls.Add(pic, 0, 0);
                rowPanel.Controls.Add(lbl, 1, 0);

                return rowPanel;
            }

            RComboBox MakeCombo(Dictionary<string, string> items, string selectedKey, Action<string> onSelect, string tip)
            {
                var cb = new RComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(3, 4, 3, 4),
                    NativeHeight = true
                };
                foreach (var kv in items)
                {
                    cb.Items.Add(new ComboOption(kv.Key, kv.Value));
                }
                SelectComboKey(cb, selectedKey);
                cb.SelectionChangeCommitted += (s, e) =>
                {
                    if (cb.SelectedItem is ComboOption opt)
                        onSelect(opt.Key);
                };
                toolTip.SetToolTip(cb, tip);
                return cb;
            }

            // 1. Theme & Color Style
            var styleMap = new Dictionary<string, string>
            {
                { "default", "Default Dark" },
                { "flat", "Flat Minimal Dark" },
                { "rog_cyber", "ROG Cyber Red" },
                { "nebula_blue", "Nebula Midnight Blue" },
                { "amoled", "AMOLED Pitch Black" },
                { "emerald_matrix", "Emerald Stealth Matrix" },
                { "solar_amber", "Solaris Cyberpunk Gold" },
                { "vapor_violet", "Vaporwave Twilight Violet" },
                { "frost_titanium", "Frost Titanium Silver" },
                { "light", "Clean Daylight Light" }
            };
            string curStyle = AppConfig.GetString("theme")?.ToLower() ?? "default";
            var cbStyle = MakeCombo(styleMap, curStyle, val =>
            {
                AppConfig.Set("theme", val);
                AppConfig.Set("ui_mode", val == "light" ? "light" : "dark");
                RForm.RefreshAllThemes();
            }, "Instantly switch G-Helper's color theme and surface style without restarting.");

            // 2. Font Family
            var fontMap = new Dictionary<string, string>
            {
                { "Segoe UI", "Segoe UI (Default)" },
                { "Segoe UI Semibold", "Segoe UI Semibold" },
                { "Segoe UI Variable Display", "Segoe UI Variable (Win11)" },
                { "Bahnschrift", "Bahnschrift (Geometric Sans)" },
                { "Cascadia Code", "Cascadia Code (Modern Terminal)" },
                { "Cascadia Mono", "Cascadia Mono" },
                { "Consolas", "Consolas (Technical Mono)" },
                { "Verdana", "Verdana (High Legibility)" },
                { "Calibri", "Calibri (Clean Sans)" },
                { "Arial", "Arial (Clean Universal)" },
                { "Century Gothic", "Century Gothic (Wide Geometric)" },
                { "Trebuchet MS", "Trebuchet MS" }
            };
            string curFont = AppConfig.GetString("ui_font_family") ?? "Segoe UI";
            var cbFont = MakeCombo(fontMap, curFont, val =>
            {
                AppConfig.Set("ui_font_family", val);
                RForm.RefreshAllThemes();
            }, "Customize the typography across all G-Helper windows.");

            // 3. Font Scale / Size
            var scaleMap = new Dictionary<string, string>
            {
                { "85", "Extra Compact (85%)" },
                { "90", "Compact (90%)" },
                { "95", "Dense (95%)" },
                { "100", "Default (100%)" },
                { "105", "Balanced (105%)" },
                { "108", "Comfortable (108%)" },
                { "112", "Medium-Large (112%)" },
                { "115", "Large (115%)" },
                { "120", "Extra Large (120%)" }
            };
            string curScale = AppConfig.Get("ui_font_scale", 100).ToString();
            var cbScale = MakeCombo(scaleMap, curScale, val =>
            {
                if (int.TryParse(val, out int sc))
                {
                    AppConfig.Set("ui_font_scale", sc);
                    RForm.RefreshAllThemes();
                }
            }, "Adjust text size scaling for easier readability.");

            // 4. Button Corner Shape
            var radiusMap = new Dictionary<string, string>
            {
                { "1", "Sharp Rectangular (1px)" },
                { "4", "Subtle (4px)" },
                { "6", "Medium (6px)" },
                { "8", "Rounded (8px Default)" },
                { "10", "Smooth (10px)" },
                { "12", "Curved (12px)" },
                { "14", "Soft Pill (14px)" },
                { "18", "Full Capsule (18px)" }
            };
            string curRadius = AppConfig.Get("ui_button_radius", 8).ToString();
            var cbRadius = MakeCombo(radiusMap, curRadius, val =>
            {
                if (int.TryParse(val, out int r))
                {
                    AppConfig.Set("ui_button_radius", r);
                    RForm.RefreshAllThemes();
                }
            }, "Customize button corner roundness across the interface.");

            // 5. Live Tray Icon Telemetry
            var trayMap = new Dictionary<string, string>
            {
                { "0", "Default ROG GPU Logo" },
                { "1", "Live CPU Temperature (°C)" },
                { "2", "Live Battery Power Draw (W)" },
                { "3", "Live GPU Temperature (°C)" },
                { "4", "Live Max Fan Speed (kRPM)" },
                { "5", "Live RAM Usage (%)" },
                { "6", "Live Battery Capacity (%)" },
                { "7", "Live CPU Package Power (W)" },
                { "8", "Live Mid Fan Speed (RPM)" }
            };
            string curTray = ((int)FlowFeaturesHelper.GetTrayMode()).ToString();
            var cbTray = MakeCombo(trayMap, curTray, val =>
            {
                if (int.TryParse(val, out int m))
                {
                    FlowFeaturesHelper.SetTrayMode((TrayTelemetryMode)m);
                    Program.settingsForm?.SetContextMenu();
                }
            }, "Choose whether the system tray icon displays the ROG GPU mode icon or live hardware telemetry.");

            // 6. Settings Backup & Restore Row
            var panelBackupBtns = new TableLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            panelBackupBtns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            panelBackupBtns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            var btnExport = new RButton
            {
                Dock = DockStyle.Fill,
                Height = 36,
                Margin = new Padding(2, 3, 2, 3),
                Secondary = true,
                Text = "Export Backup..."
            };
            btnExport.Click += (s, e) => FlowFeaturesHelper.ExportSettingsBackup(this);
            toolTip.SetToolTip(btnExport, "Save all fan curves, power limits, keybindings, and UI settings to a .json backup file.");

            var btnImport = new RButton
            {
                Dock = DockStyle.Fill,
                Height = 36,
                Margin = new Padding(2, 3, 2, 3),
                Secondary = true,
                Text = "Restore Backup..."
            };
            btnImport.Click += (s, e) => FlowFeaturesHelper.ImportSettingsBackup(this);
            toolTip.SetToolTip(btnImport, "Restore all settings and profiles from a saved .json backup file.");

            panelBackupBtns.Controls.Add(btnExport, 0, 0);
            panelBackupBtns.Controls.Add(btnImport, 1, 0);

            table.Controls.Add(MakeRowHeader("UI Theme & Color Style:", "Choose a dark, cyber, midnight blue, emerald, amber, violet, titanium, AMOLED, or daylight theme.",
                (g, r, fg) =>
                {
                    using var pen = new Pen(fg, 1.6f);
                    float w = r.Width;
                    float h = r.Height;
                    using var path = new GraphicsPath();
                    path.AddBezier(r.Left + w * 0.15f, r.Top + h * 0.85f,
                                   r.Left, r.Top + h * 0.35f,
                                   r.Left + w * 0.25f, r.Top,
                                   r.Left + w * 0.75f, r.Top + h * 0.1f);
                    path.AddBezier(r.Left + w * 0.75f, r.Top + h * 0.1f,
                                   r.Right, r.Top + h * 0.4f,
                                   r.Right - w * 0.15f, r.Bottom,
                                   r.Left + w * 0.5f, r.Bottom - h * 0.15f);
                    path.AddBezier(r.Left + w * 0.5f, r.Bottom - h * 0.15f,
                                   r.Left + w * 0.4f, r.Bottom,
                                   r.Left + w * 0.25f, r.Bottom,
                                   r.Left + w * 0.15f, r.Top + h * 0.85f);
                    g.DrawPath(pen, path);

                    float dotR = Math.Max(2f, w * 0.13f);
                    using var bRed = new SolidBrush(Color.FromArgb(235, 75, 75));
                    using var bBlue = new SolidBrush(Color.FromArgb(60, 160, 245));
                    using var bGold = new SolidBrush(Color.FromArgb(245, 190, 50));
                    using var bGreen = new SolidBrush(Color.FromArgb(50, 205, 130));
                    g.FillEllipse(bRed, r.Left + w * 0.28f, r.Top + h * 0.22f, dotR, dotR);
                    g.FillEllipse(bBlue, r.Left + w * 0.52f, r.Top + h * 0.18f, dotR, dotR);
                    g.FillEllipse(bGold, r.Left + w * 0.72f, r.Top + h * 0.35f, dotR, dotR);
                    g.FillEllipse(bGreen, r.Left + w * 0.65f, r.Top + h * 0.60f, dotR, dotR);
                }), 0, 0);
            table.Controls.Add(cbStyle, 1, 0);

            table.Controls.Add(MakeRowHeader("Interface Font Family:", "Select your preferred font family across all G-Helper windows.",
                (g, r, fg) =>
                {
                    using var fLarge = new Font("Segoe UI", r.Height * 0.62f, FontStyle.Bold, GraphicsUnit.Pixel);
                    using var fSmall = new Font("Segoe UI", r.Height * 0.46f, FontStyle.Regular, GraphicsUnit.Pixel);
                    using var brush = new SolidBrush(fg);
                    using var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                    g.DrawString("A", fLarge, brush, new PointF(r.Left, r.Top + r.Height * 0.45f), sf);
                    g.DrawString("a", fSmall, brush, new PointF(r.Left + r.Width * 0.50f, r.Top + r.Height * 0.55f), sf);
                }), 0, 1);
            table.Controls.Add(cbFont, 1, 1);

            table.Controls.Add(MakeRowHeader("Text Scale & Readability:", "Scale UI text size from Extra Compact (85%) to Extra Large (120%).",
                (g, r, fg) =>
                {
                    using var pen = new Pen(fg, 1.6f);
                    float w = r.Width;
                    float h = r.Height;
                    float lensR = w * 0.65f;
                    var lensRect = new RectangleF(r.Left, r.Top, lensR, lensR);
                    g.DrawEllipse(pen, lensRect);

                    using var handlePen = new Pen(fg, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                    g.DrawLine(handlePen, r.Left + lensR * 0.85f, r.Top + lensR * 0.85f, r.Right - 1, r.Bottom - 1);

                    using var f = new Font("Segoe UI", lensR * 0.54f, FontStyle.Bold, GraphicsUnit.Pixel);
                    using var b = new SolidBrush(fg);
                    using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("A", f, b, new RectangleF(lensRect.X, lensRect.Y + 0.5f, lensRect.Width, lensRect.Height), sf);
                }), 0, 2);
            table.Controls.Add(cbScale, 1, 2);

            table.Controls.Add(MakeRowHeader("Button Corner Style:", "Adjust the corner radius of buttons and cards from Sharp (1px) to Capsule (18px).",
                (g, r, fg) =>
                {
                    using var pen = new Pen(fg, 1.6f);
                    using var accentPen = new Pen(Color.FromArgb(58, 174, 239), 2f);
                    float w = r.Width;
                    float h = r.Height;
                    var cardRect = new RectangleF(r.Left + 1.5f, r.Top + 1.5f, w - 3, h - 3);
                    float rad = cardRect.Width * 0.35f;
                    using var path = new GraphicsPath();
                    path.AddArc(cardRect.Right - rad * 2, cardRect.Top, rad * 2, rad * 2, 270, 90);
                    path.AddLine(cardRect.Right, cardRect.Top + rad, cardRect.Right, cardRect.Bottom - rad);
                    path.AddArc(cardRect.Right - rad * 2, cardRect.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
                    path.AddLine(cardRect.Right - rad, cardRect.Bottom, cardRect.Left + rad, cardRect.Bottom);
                    path.AddArc(cardRect.Left, cardRect.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
                    path.AddLine(cardRect.Left, cardRect.Bottom - rad, cardRect.Left, cardRect.Top + rad);
                    path.AddArc(cardRect.Left, cardRect.Top, rad * 2, rad * 2, 180, 90);
                    path.CloseFigure();
                    g.DrawPath(pen, path);

                    g.DrawArc(accentPen, cardRect.Left, cardRect.Top, rad * 2, rad * 2, 180, 90);
                }), 0, 3);
            table.Controls.Add(cbRadius, 1, 3);

            table.Controls.Add(MakeRowHeader("System Tray Icon Display:", "Show live CPU temp, GPU temp, Battery Wattage, Fan RPM, RAM, or CPU Power in the Windows system tray.",
                (g, r, fg) =>
                {
                    using var pen = new Pen(fg, 1.5f);
                    using var accentPen = new Pen(Color.FromArgb(6, 180, 138), 1.8f);
                    float w = r.Width;
                    float h = r.Height;
                    var box = new RectangleF(r.Left + 1, r.Top + 1, w - 2, h - 3);
                    g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);

                    PointF[] pulse = [
                        new PointF(box.Left + 2, box.Top + box.Height * 0.6f),
                        new PointF(box.Left + box.Width * 0.28f, box.Top + box.Height * 0.6f),
                        new PointF(box.Left + box.Width * 0.42f, box.Top + box.Height * 0.2f),
                        new PointF(box.Left + box.Width * 0.58f, box.Top + box.Height * 0.85f),
                        new PointF(box.Left + box.Width * 0.72f, box.Top + box.Height * 0.45f),
                        new PointF(box.Right - 2, box.Top + box.Height * 0.45f)
                    ];
                    g.DrawLines(accentPen, pulse);
                }), 0, 4);
            table.Controls.Add(cbTray, 1, 4);

            table.Controls.Add(MakeRowHeader("Settings & Profiles Backup:", "Export or restore your complete G-Helper configuration.",
                (g, r, fg) =>
                {
                    using var pen = new Pen(fg, 1.5f);
                    using var accentBrush = new SolidBrush(Color.FromArgb(58, 174, 239));
                    float w = r.Width;
                    float h = r.Height;
                    var dRect = new RectangleF(r.Left + 1, r.Top + 1, w - 2, h - 2);
                    using var path = new GraphicsPath();
                    float clip = w * 0.22f;
                    path.AddLine(dRect.Left, dRect.Top, dRect.Right - clip, dRect.Top);
                    path.AddLine(dRect.Right - clip, dRect.Top, dRect.Right, dRect.Top + clip);
                    path.AddLine(dRect.Right, dRect.Top + clip, dRect.Right, dRect.Bottom);
                    path.AddLine(dRect.Right, dRect.Bottom, dRect.Left, dRect.Bottom);
                    path.CloseFigure();
                    g.DrawPath(pen, path);

                    g.FillRectangle(accentBrush, dRect.Left + w * 0.25f, dRect.Top + 2, w * 0.42f, h * 0.32f);
                    g.DrawRectangle(pen, dRect.Left + w * 0.18f, dRect.Top + h * 0.52f, w * 0.64f, h * 0.38f);
                }), 0, 5);
            table.Controls.Add(panelBackupBtns, 1, 5);

            _syncAppearanceCombos = () =>
            {
                SelectComboKey(cbStyle, AppConfig.GetString("theme")?.ToLower() ?? "default");
                SelectComboKey(cbFont, AppConfig.GetString("ui_font_family") ?? "Segoe UI");
                SelectComboKey(cbScale, AppConfig.Get("ui_font_scale", 100).ToString());
                SelectComboKey(cbRadius, AppConfig.Get("ui_button_radius", 8).ToString());
                SelectComboKey(cbTray, ((int)FlowFeaturesHelper.GetTrayMode()).ToString());
            };

            panelTheme.Controls.Add(table);
            panelTheme.ResumeLayout(true);
        }

        private void CheckKeystoneSoundCheckedChanged(object? sender, EventArgs e)
        {
            Keystone.SetEnabled(checkKeystoneSound.Checked);
        }

        private void CheckAspm_CheckedChanged(object? sender, EventArgs e)
        {
            AppConfig.Set("aspm", (checkAspm.Checked ? 1 : 0));
            PowerNative.SetBalancedASPM(checkAspm.Checked ? 0 : 2);
        }

        private void CheckStandbyNetworking_CheckedChanged(object? sender, EventArgs e)
        {
            AppConfig.Set("standby_networking", (checkStandbyNetworking.Checked ? 1 : 0));
            if (checkStandbyNetworking.Checked) PowerNative.SetConnectivityInStandby(0, 0);
            else PowerNative.SetConnectivityInStandby(1, 2);
        }

        private void CheckNVPlatform_CheckedChanged(object? sender, EventArgs e)
        {
            AppConfig.Set("nv_platform", (checkNVPlatform.Checked ? 1 : 0));
        }

        private void OptimalBrightness_Changed(object? sender, EventArgs e)
        {
            ScreenControl.SetOptimalBrightness(comboOptimalBrightness.SelectedIndex);
        }

        private void CheckLEDStatus_CheckedChanged(object? sender, EventArgs e)
        {
            InputDispatcher.SetStatusLED(checkStatusLed.Checked);
        }

        private void CheckNumberPad_CheckedChanged(object? sender, EventArgs e)
        {
            NumberPad.Set(checkNumberPad.Checked);
        }

        private void InitACPITesting()
        {
            pictureScan.Visible = true;
            pictureScan.Click += PictureScan_Click;

            if (!AppConfig.Is("debug")) return;

            _hasACPI = true;
            panelACPI.Visible = true;
            textACPICommand.Text = "110034";
            textACPIParam.Text = "0x0303";
            buttonACPISend.Click += ButtonACPISend_Click;
        }

        private void ButtonACPISend_Click(object? sender, EventArgs e)
        {
            try
            {
                int deviceID = Convert.ToInt32(textACPICommand.Text, 16);
                int status = Convert.ToInt32(textACPIParam.Text, textACPIParam.Text.Contains("x") ? 16 : 10);
                int result = Program.acpi.DeviceSet((uint)deviceID, status, "TestACPI " + deviceID.ToString("X8") + " " + status.ToString("X4"));
                labelACPITitle.Text = "ACPI DEVS Test : " + result.ToString();
            }
            catch (Exception ex)
            {
                Logger.WriteLine(ex.Message);
            }
        }

        private void InitCores()
        {
            (int eCores, int pCores) = Program.acpi.GetCores();
            (int eCoresMax, int pCoresMax) = Program.acpi.GetCores(AsusACPI.CORES_MAX);

            if (eCores < 0 || pCores < 0 || eCoresMax < 0 || pCoresMax < 0)
            {
                _hasCores = false;
                panelCores.Visible = false;
                return;
            }

            if (eCoresMax == 0) eCoresMax = 8;
            if (pCoresMax == 0) pCoresMax = 6;

            if (AppConfig.Is8Ecores()) eCoresMax = Math.Max(8, eCoresMax);

            eCoresMax = Math.Max(4, eCoresMax);

            (int eMin, int pMin) = Program.acpi.GetCores(AsusACPI.CORES_MIN);
            if (pMin >= 1)
            {
                coresMinP = Math.Min(pMin, pCoresMax);
                coresMinE = Math.Min(eMin, eCoresMax);
            }

            _hasCores = true;
            panelCores.Visible = true;

            comboCoresE.DropDownStyle = ComboBoxStyle.DropDownList;
            comboCoresP.DropDownStyle = ComboBoxStyle.DropDownList;

            for (int i = coresMinP; i <= pCoresMax; i++) comboCoresP.Items.Add(i.ToString() + " Pcores");
            for (int i = coresMinE; i <= eCoresMax; i++) comboCoresE.Items.Add(i.ToString() + " Ecores");

            comboCoresP.SelectedIndex = Math.Max(Math.Min(pCores - coresMinP, comboCoresP.Items.Count - 1), 0);
            comboCoresE.SelectedIndex = Math.Max(Math.Min(eCores - coresMinE, comboCoresE.Items.Count - 1), 0);

            buttonCores.Click += ButtonCores_Click;

        }

        private void ButtonCores_Click(object? sender, EventArgs e)
        {
            DialogResult dialogResult = MessageBox.Show(Properties.Strings.AlertAPUMemoryRestart, Properties.Strings.AlertAPUMemoryRestartTitle, MessageBoxButtons.YesNo);

            if (dialogResult == DialogResult.Yes)
            {
                Program.acpi.SetCores(coresMinE + comboCoresE.SelectedIndex, coresMinP + comboCoresP.SelectedIndex);
                Process.Start("shutdown", "/r /t 1");
            }
        }


        private void PictureScan_Click(object? sender, EventArgs e)
        {
            string logFile = Program.acpi.ScanRange();
            new Process
            {
                StartInfo = new ProcessStartInfo(logFile)
                {
                    UseShellExecute = true
                }
            }.Start();
        }

        private void InitVramMem()
        {
            int unitMb = 0;
            if (PawnIO.CpuInfo.IsAMD) vramOptions = Program.acpi.GetVramOptions(out unitMb);

            if (vramOptions.Length > 0)
            {
                comboAPU.Items.Clear();
                foreach (int option in vramOptions)
                    comboAPU.Items.Add(option == 0 ? Properties.Strings.AutoMode : ((double)option * unitMb / 1024).ToString("0.#") + "G");

                int current = Program.acpi.GetVramMem();
                if (current == 0) current = AppConfig.Get("vram_mem", 0);

                comboAPU.SelectedIndex = Math.Max(0, Array.IndexOf(vramOptions, current));
            }
            else
            {
                if (!AppConfig.IsAlly()) return;

                int apuMem = Program.acpi.GetAPUMem();
                if (apuMem < 0) return;

                comboAPU.SelectedIndex = apuMem;
            }

            _hasAPU = true;
            panelAPU.Visible = true;
            comboAPU.DropDownStyle = ComboBoxStyle.DropDownList;
            comboAPU.SelectedIndexChanged += ComboAPU_SelectedIndexChanged;
        }

        private void ComboAPU_SelectedIndexChanged(object? sender, EventArgs e)
        {
            int mem = comboAPU.SelectedIndex;

            if (vramOptions.Length == 0) Program.acpi.SetAPUMem(mem);
            else
            {
                Program.acpi.SetVramMem(vramOptions[mem]);
                AppConfig.Set("vram_mem", vramOptions[mem]);
            }

            DialogResult dialogResult = MessageBox.Show(Properties.Strings.AlertAPUMemoryRestart, Properties.Strings.AlertAPUMemoryRestartTitle, MessageBoxButtons.YesNo);
            if (dialogResult == DialogResult.Yes)
            {
                Process.Start("shutdown", "/r /t 1");
            }

        }

        private void CheckBootSound_CheckedChanged(object? sender, EventArgs e)
        {
            int bootSound = checkBootSound.Checked ? 1 : 0;
            Program.acpi.DeviceSet(AsusACPI.BootSound, bootSound, "BootSound");
            AppConfig.Set("boot_sound", bootSound);
        }

        private static string FormatTimeout(int minutes)
        {
            if (minutes <= 0) return Properties.Strings.Never ?? "Never";
            if (minutes == 1) return "1 minute";
            if (minutes < 60) return $"{minutes} minutes";
            if (minutes == 60) return "1 hour";
            int hours = minutes / 60;
            int rem = minutes % 60;
            if (rem == 0) return $"{hours} hours";
            return $"{hours} hr {rem} min";
        }

        private static readonly Dictionary<int, string> LidActionOptions = new()
        {
            { 0, "Do nothing" },
            { 1, "Sleep" },
            { 2, "Hibernate" },
            { 3, "Shut down" }
        };

        private static readonly Dictionary<int, string> PowerButtonOptions = new()
        {
            { 0, "Do nothing" },
            { 1, "Sleep" },
            { 2, "Hibernate" },
            { 3, "Shut down" },
            { 4, "Turn off display" }
        };

        private void PopulateTimeoutCombo(RComboBox combo, int currentValue, Action<int> onSelect)
        {
            var list = new List<int> { 0, 1, 2, 3, 5, 10, 15, 20, 25, 30, 45, 60, 120, 180, 240, 300 };
            if (!list.Contains(currentValue) && currentValue > 0)
            {
                list.Add(currentValue);
                list.Sort();
            }

            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Items.Clear();
            foreach (var m in list)
            {
                combo.Items.Add(new ComboOption(m.ToString(), FormatTimeout(m)));
            }
            SelectComboKey(combo, currentValue.ToString());

            combo.SelectionChangeCommitted += (s, e) =>
            {
                if (combo.SelectedItem is ComboOption opt && int.TryParse(opt.Key, out int val))
                {
                    onSelect(val);
                }
            };
        }

        private void PopulateLidCombo(RComboBox combo, int currentValue, Action<int> onSelect)
        {
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Items.Clear();
            foreach (var kv in LidActionOptions)
            {
                combo.Items.Add(new ComboOption(kv.Key.ToString(), kv.Value));
            }
            if (!LidActionOptions.ContainsKey(currentValue)) currentValue = 1;
            SelectComboKey(combo, currentValue.ToString());

            combo.SelectionChangeCommitted += (s, e) =>
            {
                if (combo.SelectedItem is ComboOption opt && int.TryParse(opt.Key, out int val))
                {
                    onSelect(val);
                }
            };
        }

        private void PopulatePowerButtonCombo(RComboBox combo, int currentValue, Action<int> onSelect)
        {
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Items.Clear();
            foreach (var kv in PowerButtonOptions)
            {
                combo.Items.Add(new ComboOption(kv.Key.ToString(), kv.Value));
            }
            if (!PowerButtonOptions.ContainsKey(currentValue)) currentValue = 1;
            SelectComboKey(combo, currentValue.ToString());

            combo.SelectionChangeCommitted += (s, e) =>
            {
                if (combo.SelectedItem is ComboOption opt && int.TryParse(opt.Key, out int val))
                {
                    onSelect(val);
                }
            };
        }

        private static readonly Dictionary<bool, string> AdaptiveBrightnessOptions = new()
        {
            { false, "Off" },
            { true, "On" }
        };

        private void PopulateAdaptiveBrightnessCombo(RComboBox combo, bool currentValue, Action<bool> onSelect)
        {
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Items.Clear();
            foreach (var kv in AdaptiveBrightnessOptions)
            {
                combo.Items.Add(new ComboOption(kv.Key.ToString(), kv.Value));
            }
            SelectComboKey(combo, currentValue.ToString());

            combo.SelectionChangeCommitted += (s, e) =>
            {
                if (combo.SelectedItem is ComboOption opt && bool.TryParse(opt.Key, out bool val))
                {
                    onSelect(val);
                }
            };
        }

        private void InitPowerSettings()
        {
            try
            {
                labelPowerTitle.Text = "Power & Sleep";
                labelHeaderAC.Text = "Plugged in";
                labelHeaderDC.Text = "On battery";
                labelScreenTimeout.Text = "Turn off screen after";
                labelScreenDimTimeout.Text = "Dim screen after";
                labelAdaptiveBrightness.Text = "Adaptive screen brightness";
                labelSleepTimeout.Text = "Put device to sleep after";
                labelHibernateTimeout.Text = "Hibernate after";
                labelLidAction.Text = "When I close the lid";
                labelPowerBtnAction.Text = "When I press power button";

                // Screen timeouts
                PopulateTimeoutCombo(comboScreenAC, PowerNative.GetDisplayTimeout(true), val => PowerNative.SetDisplayTimeout(val, true));
                PopulateTimeoutCombo(comboScreenDC, PowerNative.GetDisplayTimeout(false), val => PowerNative.SetDisplayTimeout(val, false));

                // Screen dim timeouts
                PopulateTimeoutCombo(comboScreenDimAC, PowerNative.GetDisplayDimTimeout(true), val => PowerNative.SetDisplayDimTimeout(val, true));
                PopulateTimeoutCombo(comboScreenDimDC, PowerNative.GetDisplayDimTimeout(false), val => PowerNative.SetDisplayDimTimeout(val, false));

                // Adaptive brightness
                PopulateAdaptiveBrightnessCombo(comboAdaptiveBrightnessAC, PowerNative.GetAdaptiveBrightness(true), val => PowerNative.SetAdaptiveBrightness(val, true));
                PopulateAdaptiveBrightnessCombo(comboAdaptiveBrightnessDC, PowerNative.GetAdaptiveBrightness(false), val => PowerNative.SetAdaptiveBrightness(val, false));

                // Sleep timeouts
                PopulateTimeoutCombo(comboSleepAC, PowerNative.GetSleepTimeout(true), val => PowerNative.SetSleepTimeout(val, true));
                PopulateTimeoutCombo(comboSleepDC, PowerNative.GetSleepTimeout(false), val => PowerNative.SetSleepTimeout(val, false));

                // Hibernate timeouts
                PopulateTimeoutCombo(comboHibernateAC, PowerNative.GetHibernateTimeout(true), val => PowerNative.SetHibernateTimeout(val, true));
                PopulateTimeoutCombo(comboHibernateDC, PowerNative.GetHibernateTimeout(false), val => PowerNative.SetHibernateTimeout(val, false));

                // Lid actions
                PopulateLidCombo(comboLidAC, PowerNative.GetLidAction(true), val =>
                {
                    PowerNative.SetLidAction(val, true);
                    if (val > 0) AppConfig.Set("clamshell_default_lid_action", val);
                });
                PopulateLidCombo(comboLidDC, PowerNative.GetLidAction(false), val => PowerNative.SetLidAction(val, false));

                // Power button actions
                PopulatePowerButtonCombo(comboPowerBtnAC, PowerNative.GetPowerButtonAction(true), val => PowerNative.SetPowerButtonAction(val, true));
                PopulatePowerButtonCombo(comboPowerBtnDC, PowerNative.GetPowerButtonAction(false), val => PowerNative.SetPowerButtonAction(val, false));
            }
            catch (Exception ex)
            {
                _hasPower = false;
                panelPower.Visible = false;
                Logger.WriteLine("InitPowerSettings error: " + ex);
            }
        }

        private void PictureLog_Click(object? sender, EventArgs e)
        {
            new Process
            {
                StartInfo = new ProcessStartInfo(Logger.logFile)
                {
                    UseShellExecute = true
                }
            }.Start();
        }

        private void SliderBrightness_ValueChanged(object? sender, EventArgs e)
        {
            bool onBattery = SystemInformation.PowerStatus.PowerLineStatus != PowerLineStatus.Online;

            if (onBattery)
                AppConfig.Set("keyboard_brightness_ac", sliderBrightness.Value);
            else
                AppConfig.Set("keyboard_brightness", sliderBrightness.Value);

            Aura.ApplyBrightness(sliderBrightness.Value, "Slider");
            sliderBrightness.AccessibleName = Properties.Strings.LaptopBacklight + ": " + sliderBrightness.Value;
        }

        public void VisualiseBacklight(int backlight)
        {
            if (InvokeRequired) { Invoke(() => VisualiseBacklight(backlight)); return; }
            sliderBrightness.ValueChanged -= SliderBrightness_ValueChanged;
            sliderBrightness.Value = backlight;
            sliderBrightness.AccessibleName = Properties.Strings.LaptopBacklight + ": " + sliderBrightness.Value;
            sliderBrightness.ValueChanged += SliderBrightness_ValueChanged;
        }

        private void InitServices()
        {
            buttonServices.Enabled = false;
            Task.Run(() =>
            {
                int servicesCount = AsusService.GetRunningCount();
                if (!IsDisposed) Invoke(() => VisualiseServices(servicesCount));
            });
        }

        private void VisualiseServices(int servicesCount)
        {
            if (servicesCount > 0)
            {
                buttonServices.Text = Properties.Strings.Stop;
                labelServices.ForeColor = colorTurbo;
            }
            else
            {
                buttonServices.Text = Properties.Strings.Start;
                labelServices.ForeColor = colorStandard;
            }

            labelServices.Text = Properties.Strings.AsusServicesRunning + ":  " + servicesCount;
            buttonServices.Enabled = true;

        }

        public void ServiesToggle()
        {
            buttonServices.Enabled = false;

            if (AsusService.GetRunningCount() > 0)
            {
                labelServices.Text = Properties.Strings.StoppingServices + " ...";
                Task.Run(() =>
                {
                    AsusService.StopAsusServices();
                    Program.inputDispatcher.Init();
                    BeginInvoke(delegate
                    {
                        InitServices();
                    });
                });
            }
            else
            {
                labelServices.Text = Properties.Strings.StartingServices + " ...";
                Task.Run(() =>
                {
                    AsusService.StartAsusServices();
                    BeginInvoke(delegate
                    {
                        InitServices();
                    });
                });
            }
        }

        private void ButtonServices_Click(object? sender, EventArgs e)
        {
            if (ProcessHelper.IsUserAdministrator())
                ServiesToggle();
            else
                ProcessHelper.RunAsAdmin("services");
        }

        private void CheckGpuApps_CheckedChanged(object? sender, EventArgs e)
        {
            AppConfig.Set("kill_gpu_apps", (checkGpuApps.Checked ? 1 : 0));
        }

        private void NumericBacklightTime_ValueChanged(object? sender, EventArgs e)
        {
            AppConfig.Set("keyboard_timeout", (int)numericBacklightTime.Value);
            AppConfig.Set("keyboard_ac_timeout", (int)numericBacklightPluggedTime.Value);
            Program.inputDispatcher.InitBacklightTimer();
        }

        private void SliderXGMBrightness_ValueChanged(object? sender, EventArgs e)
        {
            if (sliderXGMBrightness.Value > 0)
                AppConfig.Set("xgm_brightness", sliderXGMBrightness.Value);

            checkXGM.CheckedChanged -= CheckXGM_CheckedChanged;
            checkXGM.Checked = sliderXGMBrightness.Value > 0;
            checkXGM.CheckedChanged += CheckXGM_CheckedChanged;
            AppConfig.Set("xmg_light", checkXGM.Checked ? 1 : 0);

            XGM.Light(sliderXGMBrightness.Value);
            sliderXGMBrightness.AccessibleName = "XG Mobile Brightness: " + sliderXGMBrightness.Value + "%";
        }

        private void CheckXGM_CheckedChanged(object? sender, EventArgs e)
        {
            AppConfig.Set("xmg_light", (checkXGM.Checked ? 1 : 0));
            int val = 0;
            if (checkXGM.Checked)
            {
                val = AppConfig.Get("xgm_brightness", 80);
                if (val <= 0) val = 80;
            }
            sliderXGMBrightness.ValueChanged -= SliderXGMBrightness_ValueChanged;
            sliderXGMBrightness.Value = val;
            sliderXGMBrightness.AccessibleName = "XG Mobile Brightness: " + sliderXGMBrightness.Value + "%";
            sliderXGMBrightness.ValueChanged += SliderXGMBrightness_ValueChanged;
            XGM.Light(val);
        }

        private void CheckXGMBattery_CheckedChanged(object? sender, EventArgs e)
        {
            AppConfig.Set("xgm_battery_limit", checkXGMBattery.Checked ? 1 : 0);
            if (checkXGMBattery.Checked)
            {
                if (Program.acpi.IsXGConnected() || XGM.IsConnected())
                {
                    Battery.BatteryControl.OnXgmConnected();
                }
            }
            else
            {
                Battery.BatteryControl.OnXgmDisconnected();
            }
        }

        private void CheckXGMMidBoost_CheckedChanged(object? sender, EventArgs e)
        {
            AppConfig.Set("xgm_mid_boost", checkXGMMidBoost.Checked ? 1 : 0);
            if (Program.modeControl is not null && AppConfig.IsApplyFans())
            {
                Program.modeControl.AutoFans();
            }
        }

        private void CheckELMB_CheckedChanged(object? sender, EventArgs e)
        {
            AppConfig.Set("elmb", checkELMB.Checked ? 1 : 0);
            ScreenELMB.Set(checkELMB.Checked ? 1 : 0);
        }

        private void CheckUSBC_CheckedChanged(object? sender, EventArgs e)
        {
            AppConfig.Set("optimized_usbc", (checkUSBC.Checked ? 1 : 0));
        }

        private void PictureHelp_Click(object? sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/seerge/g-helper/wiki/Power-user-settings#custom-hotkey-actions") { UseShellExecute = true });
        }

        private void ButtonResetBindings_Click(object? sender, EventArgs e)
        {
            SelectComboKey(comboM1, "");
            SelectComboKey(comboM2, "");
            SelectComboKey(comboM3, "");
            SelectComboKey(comboM4, "");
            SelectComboKey(comboM5, "");

            textM1.Text = textM2.Text = textM3.Text = textM4.Text = textM5.Text = "";

            MKeyControl.Reset();
            Program.inputDispatcher.RegisterKeys();
        }

        private void CheckNoOverdrive_CheckedChanged(object? sender, EventArgs e)
        {
            AppConfig.Set("no_overdrive", (checkNoOverdrive.Checked ? 1 : 0));
            ScreenControl.AutoScreen(true);
        }


        private void CheckTopmost_CheckedChanged(object? sender, EventArgs e)
        {
            AppConfig.Set("topmost", (checkTopmost.Checked ? 1 : 0));
            Program.settingsForm.TopMost = checkTopmost.Checked;
        }

        private void CheckPower_CheckedChanged(object? sender, EventArgs e)
        {
            AppConfig.Set("keyboard_awake", (checkAwake.Checked ? 1 : 0));
            AppConfig.Set("keyboard_boot", (checkBoot.Checked ? 1 : 0));
            AppConfig.Set("keyboard_sleep", (checkSleep.Checked ? 1 : 0));
            AppConfig.Set("keyboard_shutdown", (checkBoot.Checked ? 1 : 0));

            AppConfig.Set("keyboard_awake_bar", (checkAwakeBar.Checked ? 1 : 0));
            AppConfig.Set("keyboard_boot_bar", (checkBootBar.Checked ? 1 : 0));
            AppConfig.Set("keyboard_sleep_bar", (checkSleepBar.Checked ? 1 : 0));
            AppConfig.Set("keyboard_shutdown_bar", (checkBootBar.Checked ? 1 : 0));

            AppConfig.Set("keyboard_awake_lid", (checkAwakeLid.Checked ? 1 : 0));
            AppConfig.Set("keyboard_boot_lid", (checkBootLid.Checked ? 1 : 0));
            AppConfig.Set("keyboard_sleep_lid", (checkSleepLid.Checked ? 1 : 0));
            AppConfig.Set("keyboard_shutdown_lid", (checkBootLid.Checked ? 1 : 0));

            AppConfig.Set("keyboard_awake_logo", (checkAwakeLogo.Checked ? 1 : 0));
            AppConfig.Set("keyboard_boot_logo", (checkBootLogo.Checked ? 1 : 0));
            AppConfig.Set("keyboard_sleep_logo", (checkSleepLogo.Checked ? 1 : 0));
            AppConfig.Set("keyboard_shutdown_logo", (checkBootLogo.Checked ? 1 : 0));

            if (AppConfig.IsBacklightZones())
            {
                AppConfig.Set("keyboard_awake_bat", (checkBattery.Checked ? 1 : 0));
                AppConfig.Set("keyboard_awake_bar_bat", (checkBatteryBar.Checked ? 1 : 0));
                AppConfig.Set("keyboard_awake_lid_bat", (checkBatteryLid.Checked ? 1 : 0));
                AppConfig.Set("keyboard_awake_logo_bat", (checkBatteryLogo.Checked ? 1 : 0));
            }

            Aura.ApplyPower();
            if (Aura.IsOldStrix) Aura.ApplyAura();

        }

        private void ComboKeyboardSpeed_SelectedValueChanged(object? sender, EventArgs e)
        {
            if (comboKeyboardSpeed.SelectedItem is ComboOption opt && int.TryParse(opt.Key, out int speed))
            {
                AppConfig.Set("aura_speed", speed);
                Aura.ApplyAura();
            }
        }


        private void Keyboard_Shown(object? sender, EventArgs e)
        {
            UpdateTabWindowSize();
            InitServices();
        }


        private void checkAutoToggleClamshellMode_CheckedChanged(object? sender, EventArgs e)
        {
            AppConfig.Set("toggle_clamshell_mode", checkAutoToggleClamshellMode.Checked ? 1 : 0);

            if (checkAutoToggleClamshellMode.Checked)
            {
                clamshellControl.ToggleLidAction();
            }
            else
            {
                ClamshellModeControl.DisableClamshellMode();
            }

        }

        private void panelAPU_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}
