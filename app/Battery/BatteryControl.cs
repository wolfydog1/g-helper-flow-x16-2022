using GHelper.Helpers;
using Microsoft.Win32;
using System.Diagnostics;

namespace GHelper.Battery
{
    public static class BatteryControl
    {

        static bool _chargeFull = AppConfig.Is("charge_full");
        public static bool chargeFull
        {
            get
            {
                return _chargeFull;
            }
            set
            {
                AppConfig.Set("charge_full", value ? 1 : 0);
                _chargeFull = value;
            }
        }

        static bool _chargePaused = AppConfig.Is("charge_pause");
        public static bool chargePaused
        {
            get => _chargePaused;
            set
            {
                AppConfig.Set("charge_pause", value ? 1 : 0);
                _chargePaused = value;
            }
        }

        static bool _smartCharge = AppConfig.Is("smart_charge");
        public static bool smartCharge
        {
            get => _smartCharge;
            set
            {
                AppConfig.Set("smart_charge", value ? 1 : 0);
                _smartCharge = value;
            }
        }

        public static int smartChargeMin
        {
            get
            {
                int val = AppConfig.Get("smart_charge_min", 60);
                return (val >= 40 && val <= 80) ? val : 60;
            }
            set
            {
                AppConfig.Set("smart_charge_min", value);
            }
        }

        public static bool isSmartChargingActive = false;
        public static bool isSmartHoldActive = false;
        private static int _lastAppliedEcLimit = -1;

        public static void ToggleSmartCharge()
        {
            smartCharge = !smartCharge;
            if (smartCharge)
            {
                chargePaused = false;
                decimal pct = HardwareControl.batteryCapacity > 0
                    ? HardwareControl.batteryCapacity
                    : (decimal)(SystemInformation.PowerStatus.BatteryLifePercent * 100f);
                isSmartChargingActive = pct < smartChargeMin;
            }
            else
            {
                isSmartChargingActive = false;
                isSmartHoldActive = false;
            }
            SetBatteryChargeLimit();
        }

        public static int CycleSmartChargeMin()
        {
            int next = smartChargeMin switch
            {
                50 => 60,
                60 => 70,
                _ => 50
            };
            smartChargeMin = next;
            smartCharge = true;
            chargePaused = false;
            decimal pct = HardwareControl.batteryCapacity > 0
                ? HardwareControl.batteryCapacity
                : (decimal)(SystemInformation.PowerStatus.BatteryLifePercent * 100f);
            isSmartChargingActive = pct < next;
            SetBatteryChargeLimit();
            return next;
        }

        public static void EvaluateSmartCharge(decimal currentPct, bool onAc)
        {
            if (!smartCharge || chargePaused || chargeFull || currentPct <= 0)
            {
                isSmartHoldActive = false;
                return;
            }

            int maxLimit = AppConfig.Get("charge_limit", 80);
            if (maxLimit < 40 || maxLimit > 100) maxLimit = 80;
            int minLimit = Math.Min(smartChargeMin, maxLimit - 5);

            if (!onAc)
            {
                // When on battery, arm charging only if battery drops below minLimit
                isSmartChargingActive = currentPct < minLimit;
                isSmartHoldActive = false;
                return;
            }

            // When plugged in on AC:
            if (currentPct < minLimit)
            {
                isSmartChargingActive = true;
            }
            else if (currentPct >= maxLimit)
            {
                isSmartChargingActive = false;
            }

            bool shouldHold = !isSmartChargingActive && currentPct >= minLimit && currentPct < maxLimit;
            isSmartHoldActive = shouldHold;

            int desiredEcLimit = isSmartChargingActive ? maxLimit : 40;
            if (_lastAppliedEcLimit != desiredEcLimit)
            {
                _lastAppliedEcLimit = desiredEcLimit;
                Program.acpi?.DeviceSet(AsusACPI.BatteryLimit, desiredEcLimit, "SmartBatteryLimit");
                Logger.WriteLine($"Smart Charge ({minLimit}%..{maxLimit}%): SoC={Math.Round(currentPct, 1)}% -> EC Limit={desiredEcLimit}% (Charging={isSmartChargingActive})");
                if (Program.settingsForm is { IsHandleCreated: true, IsDisposed: false })
                    Program.settingsForm.BeginInvoke(() => Program.settingsForm.VisualiseBatteryOptions());
            }
        }

        public static void ToggleChargePause()
        {
            chargePaused = !chargePaused;
            if (chargePaused)
            {
                chargeFull = false;
                isSmartHoldActive = false;
                _lastAppliedEcLimit = 40;
                Program.acpi.DeviceSet(AsusACPI.BatteryLimit, 40, "BatteryLimitPaused");
                Logger.WriteLine("Battery charging paused (EC limit set to 40% for AC bypass)");
                Program.toast?.RunToast("Battery Paused: Direct AC Bypass (0W Battery Flow)", ToastIcon.Battery);
            }
            else
            {
                SetBatteryChargeLimit();
                Program.toast?.RunToast($"Battery Limit Active: {AppConfig.Get("charge_limit", 80)}%", ToastIcon.Battery);
            }
            Program.settingsForm.VisualiseBattery(AppConfig.Get("charge_limit", 80));
            Program.settingsForm.RefreshSensors(true);
        }

        public static void ToggleBatteryLimitFull()
        {
            if (chargeFull) SetBatteryChargeLimit();
            else SetBatteryLimitFull();
        }

        public static void SetBatteryLimitFull()
        {
            chargeFull = true;
            chargePaused = false;
            isSmartHoldActive = false;
            _lastAppliedEcLimit = 100;
            Program.acpi.DeviceSet(AsusACPI.BatteryLimit, 100, "BatteryLimit");
            Program.settingsForm.VisualiseBattery(AppConfig.Get("charge_limit", 80));
        }

        public static void UnSetBatteryLimitFull()
        {
            chargeFull = false;
            Logger.WriteLine("Battery fully charged");
            if (Program.settingsForm is { IsHandleCreated: true, IsDisposed: false })
                Program.settingsForm.BeginInvoke(Program.settingsForm.VisualiseBatteryFull);
        }

        public static void AutoBattery(bool init = false)
        {
            if (init && AppConfig.Is("xgm_battery_limit") && Program.acpi is not null)
            {
                bool isXgm = Program.acpi.IsXGConnected();
                int prevLimit = AppConfig.Get("pre_xgm_charge_limit", -1);
                
                if (isXgm && prevLimit < 0)
                {
                    OnXgmConnected();
                }
                else if (!isXgm && prevLimit > 0)
                {
                    OnXgmDisconnected();
                }
            }

            if (chargePaused)
            {
                _lastAppliedEcLimit = 40;
                Program.acpi.DeviceSet(AsusACPI.BatteryLimit, 40, "BatteryLimitPaused");
                Program.settingsForm.VisualiseBattery(AppConfig.Get("charge_limit", 80));
            }
            else if (chargeFull && !init)
            {
                SetBatteryLimitFull();
            }
            else
            {
                SetBatteryChargeLimit();
            }
        }

        public static void SetAsusChargeLimit(int value)
        {
            if (!ProcessHelper.IsUserAdministrator()) return;
            const string keyPath = @"SOFTWARE\ASUS\ASUS System Control Interface\AsusOptimization\ASUS Keyboard Hotkeys";
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(keyPath, writable: true);
                key?.SetValue("ChargingRate", value, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Failed to set ChargingRate: {ex.Message}");
            }
        }

        public static void SetBatteryChargeLimit(int setLimit = -1)
        {
            int limit = setLimit;
            if (limit < 0) limit = AppConfig.Get("charge_limit");
            if (limit < 40 || limit > 100) return;

            if (AppConfig.IsChargeLimit6080())
            {
                if (limit > 85) limit = 100;
                else if (limit >= 80) limit = 80;
                else if (limit < 60) limit = 60;
            }

            if (setLimit > 0)
            {
                chargePaused = false;
                SetAsusChargeLimit(limit);
            }

            AppConfig.Set("charge_limit", limit);
            chargeFull = false;

            int ecLimit = chargePaused ? 40 : limit;
            if (!chargePaused && smartCharge)
            {
                decimal pct = HardwareControl.batteryCapacity > 0
                    ? HardwareControl.batteryCapacity
                    : (decimal)(SystemInformation.PowerStatus.BatteryLifePercent * 100f);
                int minLimit = Math.Min(smartChargeMin, limit - 5);
                if (pct < minLimit) isSmartChargingActive = true;
                else if (pct >= limit) isSmartChargingActive = false;

                bool onAc = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online;
                isSmartHoldActive = onAc && !isSmartChargingActive && pct >= minLimit && pct < limit;
                ecLimit = isSmartChargingActive ? limit : 40;
            }
            else
            {
                isSmartHoldActive = false;
            }

            _lastAppliedEcLimit = ecLimit;
            Program.acpi.DeviceSet(AsusACPI.BatteryLimit, ecLimit, "BatteryLimit");

            Program.settingsForm.VisualiseBattery(limit);
        }

        public static void BatteryReport()
        {
            var reportDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            try
            {
                var cmd = new Process();
                cmd.StartInfo.WorkingDirectory = reportDir;
                cmd.StartInfo.UseShellExecute = false;
                cmd.StartInfo.CreateNoWindow = true;
                cmd.StartInfo.FileName = Path.Combine(Environment.SystemDirectory, "powercfg.exe");
                cmd.StartInfo.Arguments = "/batteryreport /output battery-report.html";
                cmd.Start();
                cmd.WaitForExit(5000);

                string reportPath = Path.Combine(reportDir, "battery-report.html");
                if (File.Exists(reportPath))
                {
                    Process.Start(new ProcessStartInfo(reportPath) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("BatteryReport error: " + ex.Message);
            }
        }

        public static void OnXgmConnected()
        {
            if (AppConfig.Is("xgm_battery_limit"))
            {
                int currentLimit = AppConfig.Get("charge_limit", 80);
                int dockedLimit = AppConfig.Get("xgm_docked_charge_limit", 80);
                if (currentLimit != dockedLimit)
                {
                    AppConfig.Set("pre_xgm_charge_limit", currentLimit);
                    SetBatteryChargeLimit(dockedLimit);
                    Logger.WriteLine($"XGM Docked: Switched battery limit from {currentLimit}% to {dockedLimit}%");
                }
            }
        }

        public static void OnXgmDisconnected()
        {
            if (AppConfig.Is("xgm_battery_limit"))
            {
                int prevLimit = AppConfig.Get("pre_xgm_charge_limit", -1);
                if (prevLimit >= 40 && prevLimit <= 100)
                {
                    SetBatteryChargeLimit(prevLimit);
                    AppConfig.Remove("pre_xgm_charge_limit");
                    Logger.WriteLine($"XGM Undocked: Restored battery limit to {prevLimit}%");
                }
            }
        }
    }
}
