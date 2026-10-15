using GHelper.Display;
using GHelper.Mode;

namespace GHelper.Helpers
{
    public static class TravelMode
    {
        public static void Activate()
        {
            Logger.WriteLine("Activating Ultra-Eco Travel Mode");

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

            // 5. Disable CPU Boost for cool/quiet efficiency
            PowerNative.SetCPUBoost(0);

            Program.toast?.RunToast("Ultra-Eco Travel Mode Active", ToastIcon.Battery);
        }
    }
}
