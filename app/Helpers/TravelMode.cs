using GHelper.Display;
using GHelper.Mode;

namespace GHelper.Helpers
{
    public static class TravelMode
    {
        public static bool IsActive { get; private set; } = false;

        private static int _prevPerfMode = AsusACPI.PerformanceBalanced;
        private static int _prevGpuMode = AsusACPI.GPUModeStandard;
        private static int _prevMiniled = 1;
        private static int _prevLimitBoost = 0;
        private static bool _prevPowerSaver = false;

        public static bool Toggle()
        {
            if (IsActive)
                Deactivate();
            else
                Activate();
            return IsActive;
        }

        public static void Activate()
        {
            Logger.WriteLine("Activating Ultra-Eco Travel Mode");

            if (!IsActive)
            {
                _prevPerfMode = Modes.GetCurrent();
                _prevGpuMode = AppConfig.Get("gpu_mode", AsusACPI.GPUModeStandard);
                _prevMiniled = AppConfig.Get("miniled", 1);
                _prevLimitBoost = AppConfig.Get("limit_cpu_boost_battery", 0);
                _prevPowerSaver = PowerNative.IsPowerSaverActive();
            }

            IsActive = true;

            // 1. Silent performance mode
            Program.modeControl?.SetPerformanceMode(AsusACPI.PerformanceSilent);

            // 2. Eco GPU mode (dGPU completely powered down)
            Program.gpuControl?.SetGPUMode(AsusACPI.GPUModeEco);

            // 3. Screen 60Hz
            var laptopScreen = ScreenNative.FindLaptopScreen(true);
            if (laptopScreen != null)
            {
                ScreenNative.SetRefreshRate(laptopScreen, 60);
            }

            // 4. Mini-LED Single-Zone (saving significant backlight wattage)
            ScreenControl.SetMiniled(0);
            ScreenControl.InitScreen();

            // 5. Disable CPU Boost & enable Windows Power Saver for cool/quiet efficiency
            AppConfig.Set("limit_cpu_boost_battery", 1);
            PowerNative.SetCPUBoost(0);
            if (!PowerNative.IsPowerSaverActive())
            {
                PowerNative.TogglePowerSaver();
            }

            Program.settingsForm?.VisualiseBatteryOptions();
            Program.settingsForm?.VisualiseMicAndPowerSaver();
            Program.toast?.RunToast("Ultra-Eco Travel Mode: Active", ToastIcon.Battery);
        }

        public static void Deactivate()
        {
            if (!IsActive) return;
            Logger.WriteLine("Deactivating Ultra-Eco Travel Mode and restoring previous settings");
            IsActive = false;

            AppConfig.Set("limit_cpu_boost_battery", _prevLimitBoost);
            Program.modeControl?.SetPerformanceMode(_prevPerfMode);

            if (_prevGpuMode != AppConfig.Get("gpu_mode"))
            {
                Program.gpuControl?.SetGPUMode(_prevGpuMode);
            }

            ScreenControl.SetMiniled(_prevMiniled);
            ScreenControl.AutoScreen(true);

            if (PowerNative.IsPowerSaverActive() != _prevPowerSaver)
            {
                PowerNative.TogglePowerSaver();
            }

            Program.settingsForm?.VisualiseBatteryOptions();
            Program.settingsForm?.VisualiseMicAndPowerSaver();
            Program.toast?.RunToast("Travel Mode: Off (Settings Restored)", ToastIcon.Battery);
        }
    }
}
