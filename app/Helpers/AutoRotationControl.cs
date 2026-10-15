using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace GHelper.Helpers
{
    public static class AutoRotationControl
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetAutoRotationState(out int pAutoRotationState);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetAutoRotationState(bool bEnable);

        private const string AutoRotationKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\AutoRotation";

        public static bool IsSupported()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(AutoRotationKey, false);
                if (key != null)
                {
                    var sensor = key.GetValue("SensorPresent");
                    if (sensor is int s && s == 1) return true;
                }
            }
            catch { }

            return AppConfig.ContainsModel("GV601") || AppConfig.ContainsModel("GV301") || AppConfig.ContainsModel("GV302") || AppConfig.HasTabletMode();
        }

        public static bool IsEnabled()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(AutoRotationKey, false);
                if (key != null)
                {
                    var val = key.GetValue("Enable");
                    if (val is int e) return e == 1;
                }
            }
            catch { }

            if (GetAutoRotationState(out int state))
            {
                return (state & 1) == 0; // 0 = Enabled, 1 = Disabled
            }

            return false;
        }

        public static bool SetAutoRotation(bool enable)
        {
            bool success = false;
            try
            {
                success = SetAutoRotationState(enable);
            }
            catch { }

            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(AutoRotationKey, true);
                if (key != null)
                {
                    key.SetValue("Enable", enable ? 1 : 0, RegistryValueKind.DWord);
                    success = true;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("AutoRotation registry error: " + ex.Message);
            }

            Logger.WriteLine($"AutoRotation set: {enable} (success: {success})");
            return success;
        }

        public static bool Toggle()
        {
            bool current = IsEnabled();
            bool target = !current;
            SetAutoRotation(target);
            return target;
        }
    }
}
