using GHelper.Display;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace GHelper.Helpers
{
    public static class AutoRotationControl
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetAutoRotationState(out int pAutoRotationState);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetDisplayAutoRotationPreferences(int orientation);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettingsEx(string? lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode, int dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ChangeDisplaySettingsEx(string? lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

        private const int ENUM_CURRENT_SETTINGS = -1;
        private const uint CDS_UPDATEREGISTRY = 0x00000001;
        private const int DM_DISPLAYORIENTATION = 0x00000080;
        private const int DM_PELSWIDTH = 0x00080000;
        private const int DM_PELSHEIGHT = 0x00100000;

        public const int DMDO_DEFAULT = 0; // 0° Landscape
        public const int DMDO_90 = 1;      // 90° Portrait
        public const int DMDO_180 = 2;     // 180° Landscape (Flipped)
        public const int DMDO_270 = 3;     // 270° Portrait (Flipped)

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public ushort dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

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
                SetDisplayAutoRotationPreferences(0);
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

        private static string? GetTargetDisplayName()
        {
            try
            {
                string? internalScreen = ScreenNative.FindLaptopScreen(true);
                if (!string.IsNullOrEmpty(internalScreen))
                    return internalScreen;
            }
            catch { }
            return Screen.PrimaryScreen?.DeviceName;
        }

        public static int GetOrientation()
        {
            try
            {
                string? devName = GetTargetDisplayName();
                DEVMODE dm = new DEVMODE();
                dm.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
                if (EnumDisplaySettingsEx(devName, ENUM_CURRENT_SETTINGS, ref dm, 0))
                {
                    return dm.dmDisplayOrientation;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("GetOrientation error: " + ex.Message);
            }
            return DMDO_DEFAULT;
        }

        public static bool SetOrientation(int newOrientation)
        {
            newOrientation = ((newOrientation % 4) + 4) % 4;
            try
            {
                string? devName = GetTargetDisplayName();
                DEVMODE dm = new DEVMODE();
                dm.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
                if (!EnumDisplaySettingsEx(devName, ENUM_CURRENT_SETTINGS, ref dm, 0))
                    return false;

                int oldOrientation = dm.dmDisplayOrientation;
                if (oldOrientation == newOrientation)
                    return true;

                // Swap width & height when transitioning between landscape (0, 2) and portrait (1, 3)
                if ((oldOrientation % 2) != (newOrientation % 2))
                {
                    (dm.dmPelsWidth, dm.dmPelsHeight) = (dm.dmPelsHeight, dm.dmPelsWidth);
                }

                dm.dmDisplayOrientation = newOrientation;
                dm.dmFields = DM_DISPLAYORIENTATION | DM_PELSWIDTH | DM_PELSHEIGHT;

                int res = ChangeDisplaySettingsEx(devName, ref dm, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
                Logger.WriteLine($"SetOrientation({newOrientation}) on {devName} result: {res}");
                return res == 0;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("SetOrientation error: " + ex.Message);
                return false;
            }
        }

        public static int CycleOrientation()
        {
            int cur = GetOrientation();
            int next = (cur + 1) % 4;
            SetOrientation(next);
            if (next == DMDO_DEFAULT)
            {
                SetAutoRotation(true);
            }
            return GetOrientation();
        }

        public static string GetOrientationLabel(int orientation)
        {
            return orientation switch
            {
                DMDO_90 => "Portrait (90°)",
                DMDO_180 => "Landscape Flipped (180°)",
                DMDO_270 => "Portrait Flipped (270°)",
                _ => "Landscape (0°)"
            };
        }

        public static string GetShortButtonText()
        {
            int o = GetOrientation();
            return o switch
            {
                DMDO_90 => "Rot: 90°",
                DMDO_180 => "Rot: 180°",
                DMDO_270 => "Rot: 270°",
                _ => "Rotate"
            };
        }
    }
}
