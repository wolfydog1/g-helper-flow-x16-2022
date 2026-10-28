using GHelper.Helpers;
using Microsoft.Win32;
using System.Diagnostics;

namespace GHelper.Display
{
    public static class ScreenControl
    {

        public const int MAX_REFRESH = 1000;
        public static int MIN_RATE = AppConfig.Get("min_rate", 60);
        public static int MAX_RATE = AppConfig.Get("max_rate");

        public static int GetMaxRate(string? laptopScreen)
        {
            if (MAX_RATE > 0) return MAX_RATE;
            else return ScreenNative.GetMaxRefreshRate(laptopScreen);
        }

        public static void AutoScreen(bool force = false)
        {
            bool online = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online;
            int autoMiniled = AppConfig.Is("auto_miniled") ? (online ? 1 : 0) : -1;

            if (AppConfig.Is("auto_hdr_miniled"))
            {
                bool isHdr = ScreenCCD.GetHDRStatus();
                if (isHdr) autoMiniled = 1;
                else if (AppConfig.IsNotFalse("auto_miniled_sdr_single")) autoMiniled = 0;
            }

            if (AppConfig.Is("hdr_battery_off"))
            {
                if (!online && ScreenCCD.GetHDRStatus())
                {
                    AppConfig.Set("hdr_was_on_ac", 1);
                    ScreenCCD.SetHDRStatus(false);
                }
                else if (online && AppConfig.Is("hdr_was_on_ac"))
                {
                    AppConfig.Set("hdr_was_on_ac", 0);
                    ScreenCCD.SetHDRStatus(true);
                }
            }

            int targetOd = AppConfig.IsNoOverdrive() ? 0 : 1;

            if (force || AppConfig.Is("screen_auto"))
            {
                if (online)
                    SetScreen(MAX_REFRESH, targetOd, miniled: autoMiniled);
                else
                    SetScreen(MIN_RATE, 0, miniled: autoMiniled);
            }
            else
            {
                if (autoMiniled >= 0) AppConfig.Set("miniled", autoMiniled);
                SetScreen(overdrive: targetOd, miniled: autoMiniled);
            }
        }

        public static bool ToggleOverdrive()
        {
            int rawOd = Program.acpi.DeviceGet(AsusACPI.ScreenOverdrive);
            bool isCurrentlyOn = !AppConfig.IsNoOverdrive() && rawOd == 1;
            int nextOd = isCurrentlyOn ? 0 : 1;

            AppConfig.Set("no_overdrive", nextOd == 1 ? 0 : 1);
            AppConfig.Set("overdrive", nextOd);

            if (Program.acpi.IsOverdriveSupported())
            {
                Program.acpi.DeviceSet(AsusACPI.ScreenOverdrive, nextOd, "ScreenOverdrive");
            }

            InitScreen();
            return nextOd == 1;
        }

        public static void SetAutoRefresh(int auto)
        {
            AppConfig.Set("screen_auto", auto);
            if (auto == 0) SetAsusRefreshFlag(0);
        }

        public static void SetAsusRefreshFlag(int value)
        {
            if (!ProcessHelper.IsUserAdministrator()) return;
            const string keyPath = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{8714A8D1-0F08-4681-9DF6-A8C4607A58B4}";
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(keyPath, writable: true);
                key?.SetValue("RefreshFlag", value, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Failed to set RefreshFlag: {ex.Message}");
            }
        }

        public static void ToggleScreenRate()
        {
            var laptopScreen = ScreenNative.FindLaptopScreen(true);
            var refreshRate = ScreenNative.GetRefreshRate(laptopScreen);
            if (refreshRate < 0) return;

            ScreenNative.SetRefreshRate(laptopScreen, refreshRate > MIN_RATE ? MIN_RATE : GetMaxRate(laptopScreen));
            InitScreen();
        }


        public static void SetScreen(int frequency = -1, int overdrive = -1, int miniled = -1)
        {
            var laptopScreen = ScreenNative.FindLaptopScreen(true);
            var refreshRate = ScreenNative.GetRefreshRate(laptopScreen);

            if (refreshRate < 0) return;

            if (frequency >= MAX_REFRESH)
            {
                frequency = GetMaxRate(laptopScreen);
            }

            if (frequency > 0 && frequency != refreshRate)
            {
                ScreenNative.SetRefreshRate(laptopScreen, frequency);
            }

            if (Program.acpi.IsOverdriveSupported() && overdrive >= 0)
            {
                if (AppConfig.IsNoOverdrive()) overdrive = 0;
                if (overdrive != Program.acpi.DeviceGet(AsusACPI.ScreenOverdrive))
                {
                    Program.acpi.DeviceSet(AsusACPI.ScreenOverdrive, overdrive, "ScreenOverdrive");
                }
            }

            SetMiniled(miniled);

            InitScreen();
        }

        public static void SetMiniled(int miniled = -1)
        {
            if (miniled >= 0)
            {
                bool isMiniled1 = Program.acpi.IsSupported(AsusACPI.ScreenMiniled1);
                bool isSingleZone = isMiniled1 ? (miniled == 0) : (miniled == 2);

                if (isSingleZone && ScreenCCD.GetHDRStatus())
                {
                    Logger.WriteLine("Turning off HDR because Multi-Zone was turned off (Single-Zone selected)");
                    ScreenCCD.SetHDRStatus(false);
                    Thread.Sleep(150);
                }

                if (isMiniled1)
                    Program.acpi.DeviceSet(AsusACPI.ScreenMiniled1, miniled, "Miniled1");
                else
                {
                    Program.acpi.DeviceSet(AsusACPI.ScreenMiniled2, miniled, "Miniled2");
                    Thread.Sleep(100);
                }
            }
        }

        public static void InitMiniled()
        {
            if (AppConfig.IsAMDiGPU() || AppConfig.ContainsModel("GV601"))
            {
                DisableAmdVariBright();
            }

            if (AppConfig.IsForceMiniled())
            {
                if (ScreenCCD.IsHDR() && Program.acpi.DeviceGet(AsusACPI.ScreenHDRControl) >= 0)
                    SetHDRControl(AppConfig.Get("hdr_control"));
                else
                    SetMiniled(AppConfig.Get("miniled"));
            }

            InitELMB();
        }

        public static void DisableAmdVariBright()
        {
            try
            {
                string baseKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
                using var classKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(baseKey, true);
                if (classKey != null)
                {
                    foreach (var subKeyName in classKey.GetSubKeyNames())
                    {
                        if (!subKeyName.StartsWith("000")) continue;
                        using var driverKey = classKey.OpenSubKey(subKeyName, true);
                        if (driverKey != null)
                        {
                            var desc = driverKey.GetValue("DriverDesc")?.ToString();
                            if (desc != null && desc.Contains("AMD", StringComparison.OrdinalIgnoreCase))
                            {
                                int currentVal = (int)(driverKey.GetValue("PP_UserVariBrightLevel") ?? -1);
                                if (currentVal != 0)
                                {
                                    driverKey.SetValue("PP_UserVariBrightLevel", 0, Microsoft.Win32.RegistryValueKind.DWord);
                                    driverKey.SetValue("Dal_UserVariBrightLevel", 0, Microsoft.Win32.RegistryValueKind.DWord);
                                    driverKey.SetValue("KMD_EnableVariBright", 0, Microsoft.Win32.RegistryValueKind.DWord);
                                    Logger.WriteLine($"Disabled AMD Vari-Bright on {subKeyName}: {desc}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"DisableAmdVariBright: {ex.Message}");
            }
        }

        public static void InitELMB()
        {
            if (!AppConfig.IsELMB()) return;

            int elmb = AppConfig.Get("elmb");
            if (elmb >= 0 && !ScreenCCD.IsHDR()) ScreenELMB.Set(elmb);
        }

        public static void InitOptimalBrightness()
        {
            int optimalBrightness = AppConfig.Get("optimal_brightness");
            if (optimalBrightness >= 0) SetOptimalBrightness(optimalBrightness);
        }

        public static void SetOptimalBrightness(int status)
        {
            AppConfig.Set("optimal_brightness", status);
            if (status == 2) status = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline ? 1 : 0;
            Program.acpi.DeviceSet(AsusACPI.ScreenOptimalBrightness, status, "Optimal Brightness");
        }

        public static int GetOptimalBrightness()
        {
            return Program.acpi.DeviceGet(AsusACPI.ScreenOptimalBrightness);
        }

        public static void ToogleFHD()
        {
            int fhd = Program.acpi.DeviceGet(AsusACPI.ScreenFHD);
            Logger.WriteLine($"FHD Toggle: {fhd}");

            DialogResult dialogResult = MessageBox.Show("Changing display mode requires reboot", Properties.Strings.AlertUltimateTitle, MessageBoxButtons.YesNo);
            if (dialogResult == DialogResult.Yes)
            {
                Program.acpi.DeviceSet(AsusACPI.ScreenFHD, (fhd == 1) ? 0 : 1, "FHD");
                Process.Start(new ProcessStartInfo("shutdown", "/r /t 1") { CreateNoWindow = true, UseShellExecute = false });
            }
        }

        public static void SetHDRControl(int status = -1)
        {
            if (status >= 0)
            {
                AppConfig.Set("hdr_control", status);
                Program.acpi.DeviceSet(AsusACPI.ScreenHDRControl, status, "HDR Control");
            }
        }

        public static void ToogleHDRControl()
        {
            int hdrControl = Program.acpi.DeviceGet(AsusACPI.ScreenHDRControl);
            Logger.WriteLine($"HDR Control Toggle: {hdrControl}");
            SetHDRControl((hdrControl == 1) ? 1 : 0);
            Thread.Sleep(200);
            InitScreen();
        }

        public static string ToogleMiniled()
        {
            int miniled1 = Program.acpi.DeviceGet(AsusACPI.ScreenMiniled1);
            int miniled2 = Program.acpi.DeviceGet(AsusACPI.ScreenMiniled2);

            Logger.WriteLine($"MiniledToggle: {miniled1} {miniled2}");

            int miniled;
            string name;

            if (miniled1 >= 0)
            {
                int current = AppConfig.Get("miniled", miniled1);
                if (current != 0 && current != 1) current = miniled1;

                switch (current)
                {
                    case 1: 
                        miniled = 0;
                        name = Properties.Strings.OneZone;
                        break;
                    default:
                        miniled = 1;
                        name = Properties.Strings.Multizone;
                        break;
                }
            }
            else
            {
                int current2 = AppConfig.Get("miniled", miniled2);
                if (current2 < 0 || current2 > 2) current2 = miniled2;

                switch (current2)
                {
                    case 1: 
                        miniled = 2;
                        name = Properties.Strings.OneZone;
                        break;
                    case 2: 
                        miniled = 0;
                        name = Properties.Strings.Multizone;
                        break;
                    default: 
                        miniled = 1;
                        name = Properties.Strings.MultizoneStrong;
                        break;
                }
            }

            AppConfig.Set("miniled", miniled);
            SetScreen(miniled: miniled);
            
            return name;
        }

        public static void InitScreen()
        {
            var laptopScreen = ScreenNative.FindLaptopScreen();
            int frequency = ScreenNative.GetRefreshRate(laptopScreen);
            int maxFrequency = GetMaxRate(laptopScreen);

            if (maxFrequency > 0) AppConfig.Set("max_frequency", maxFrequency);
            else maxFrequency = AppConfig.Get("max_frequency");

            bool screenAuto = AppConfig.Is("screen_auto");
            bool overdriveSupported = Program.acpi.IsOverdriveSupported();
            bool overdriveSetting = overdriveSupported && !AppConfig.IsNoOverdrive();

            int overdrive = overdriveSupported ? (overdriveSetting ? Program.acpi.DeviceGet(AsusACPI.ScreenOverdrive) : 0) : -1;

            int miniled1 = Program.acpi.DeviceGet(AsusACPI.ScreenMiniled1);
            int miniled2 = Program.acpi.DeviceGet(AsusACPI.ScreenMiniled2);

            int miniled = (miniled1 >= 0) ? miniled1 : miniled2;
            bool hdr = false;
            bool acm = false;

            try
            {
                hdr = ScreenCCD.GetHDRStatus(out acm);
            } catch (Exception ex)
            {
                Logger.WriteLine(ex.Message);
            }

            if (miniled >= 0)
            {
                Logger.WriteLine($"Miniled: {miniled1} {miniled2}");
                if (AppConfig.Is("auto_hdr_miniled"))
                {
                    if (hdr && miniled != 1)
                    {
                        Logger.WriteLine("Auto Multi-Zone on HDR: Engaging Multi-Zone");
                        SetMiniled(1);
                        miniled = 1;
                        if (miniled1 >= 0) miniled1 = 1;
                        else if (miniled2 >= 0) miniled2 = 1;
                        AppConfig.Set("miniled", 1);
                    }
                    else if (!hdr && miniled == 1 && AppConfig.IsNotFalse("auto_miniled_sdr_single"))
                    {
                        Logger.WriteLine("Auto Multi-Zone on HDR: Reverting to Single-Zone for SDR");
                        SetMiniled(0);
                        miniled = 0;
                        if (miniled1 >= 0) miniled1 = 0;
                        else if (miniled2 >= 0) miniled2 = 0;
                        AppConfig.Set("miniled", 0);
                    }
                }

                int savedMiniled = AppConfig.Get("miniled", -1);
                if (hdr && savedMiniled >= 0 && savedMiniled != miniled)
                {
                    if (miniled1 >= 0) miniled1 = savedMiniled;
                    else if (miniled2 >= 0) miniled2 = savedMiniled;
                }
                else
                {
                    AppConfig.Set("miniled", miniled);
                }
            }

            bool screenEnabled = (frequency >= 0);

            int fhd = -1;
            if (AppConfig.IsDUO())
            {
                fhd = Program.acpi.DeviceGet(AsusACPI.ScreenFHD);
            }

            int hdrControl = Program.acpi.DeviceGet(AsusACPI.ScreenHDRControl);
            if (hdrControl >= 0) Logger.WriteLine($"HDR Control Status: {hdrControl}");

            AppConfig.Set("frequency", frequency);
            AppConfig.Set("overdrive", overdrive);

            Program.RunOnUI(delegate
            {
                Program.settingsForm.VisualiseScreen(
                    screenEnabled: screenEnabled,
                    screenAuto: screenAuto,
                    frequency: frequency,
                    maxFrequency: maxFrequency,
                    overdrive: overdrive,
                    overdriveSetting: overdriveSetting,
                    miniled1: miniled1,
                    miniled2: miniled2,
                    hdr: hdr,
                    acm: acm,
                    fhd: fhd,
                    hdrControl: hdrControl
                );
            });

        }
    }
}
