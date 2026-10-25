namespace GHelper.Display
{
    using Microsoft.Win32.SafeHandles;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Management;
    using System.Runtime.InteropServices;

    public static class ScreenBrightness
    {
        private static ManagementScope? _wmiScope;
        private static readonly object _wmiLock = new();
        private static int _lastKnownBrightness = -1;

        // Native Win32 \\.\LCD Kernel IOCTL constants for internal laptop panels
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;

        private const uint IOCTL_VIDEO_QUERY_DISPLAY_BRIGHTNESS = 0x00230498;
        private const uint IOCTL_VIDEO_SET_DISPLAY_BRIGHTNESS = 0x0023049C;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct DISPLAY_BRIGHTNESS
        {
            public byte ucDisplayPolicy;
            public byte ucACBrightness;
            public byte ucDCBrightness;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            ref DISPLAY_BRIGHTNESS lpInBuffer,
            int nInBufferSize,
            out DISPLAY_BRIGHTNESS lpOutBuffer,
            int nOutBufferSize,
            out int lpBytesReturned,
            IntPtr lpOverlapped);

        private static bool TryGetIoctl(out int brightness)
        {
            brightness = -1;
            try
            {
                using SafeFileHandle hLcd = CreateFile(
                    @"\\.\LCD",
                    GENERIC_READ | GENERIC_WRITE,
                    FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    0,
                    IntPtr.Zero);

                if (hLcd.IsInvalid) return false;

                DISPLAY_BRIGHTNESS dummy = default;
                if (DeviceIoControl(
                    hLcd,
                    IOCTL_VIDEO_QUERY_DISPLAY_BRIGHTNESS,
                    ref dummy,
                    0,
                    out DISPLAY_BRIGHTNESS db,
                    Marshal.SizeOf<DISPLAY_BRIGHTNESS>(),
                    out int bytesReturned,
                    IntPtr.Zero) && bytesReturned >= 3)
                {
                    // ucDisplayPolicy: 1 = AC, 2 = DC
                    int val = db.ucDisplayPolicy == 1 ? db.ucACBrightness
                            : db.ucDisplayPolicy == 2 ? db.ucDCBrightness
                            : (System.Windows.Forms.SystemInformation.PowerStatus.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online ? db.ucACBrightness : db.ucDCBrightness);

                    if (val >= 0 && val <= 100)
                    {
                        brightness = val;
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool TrySetIoctl(int brightness)
        {
            try
            {
                using SafeFileHandle hLcd = CreateFile(
                    @"\\.\LCD",
                    GENERIC_READ | GENERIC_WRITE,
                    FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    0,
                    IntPtr.Zero);

                if (hLcd.IsInvalid) return false;

                byte b = (byte)Math.Clamp(brightness, 0, 100);
                var db = new DISPLAY_BRIGHTNESS
                {
                    ucDisplayPolicy = 3, // DISPLAYPOLICY_BOTH (AC & DC)
                    ucACBrightness = b,
                    ucDCBrightness = b
                };

                return DeviceIoControl(
                    hLcd,
                    IOCTL_VIDEO_SET_DISPLAY_BRIGHTNESS,
                    ref db,
                    Marshal.SizeOf<DISPLAY_BRIGHTNESS>(),
                    out _,
                    0,
                    out _,
                    IntPtr.Zero);
            }
            catch { }
            return false;
        }

        private static ManagementScope GetScope()
        {
            lock (_wmiLock)
            {
                if (_wmiScope == null)
                {
                    _wmiScope = new ManagementScope(@"root\wmi");
                    _wmiScope.Connect();
                }
                else if (!_wmiScope.IsConnected)
                {
                    _wmiScope.Connect();
                }
                return _wmiScope;
            }
        }

        public static int Get()
        {
            // Fast path: Direct kernel driver IOCTL (\\.\LCD)
            if (TryGetIoctl(out int ioctlVal))
            {
                _lastKnownBrightness = ioctlVal;
                AppConfig.Set("screen_brightness", ioctlVal);
                return ioctlVal;
            }

            // Fallback path: WMI WmiMonitorBrightness
            try
            {
                var scope = GetScope();
                using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT CurrentBrightness, Active FROM WmiMonitorBrightness"));
                using var instances = searcher.Get();

                int fallbackVal = -1;
                foreach (ManagementObject instance in instances)
                {
                    using (instance)
                    {
                        try
                        {
                            int val = Convert.ToInt32(instance.GetPropertyValue("CurrentBrightness"));
                            if (fallbackVal < 0 && val >= 0 && val <= 100) fallbackVal = val;

                            var active = instance.GetPropertyValue("Active");
                            if (active is bool b && b)
                            {
                                if (val >= 0 && val <= 100)
                                {
                                    _lastKnownBrightness = val;
                                    AppConfig.Set("screen_brightness", val);
                                }
                                return val;
                            }
                        }
                        catch { }
                    }
                }

                if (fallbackVal >= 0)
                {
                    _lastKnownBrightness = fallbackVal;
                    AppConfig.Set("screen_brightness", fallbackVal);
                    return fallbackVal;
                }
            }
            catch (Exception ex)
            {
                lock (_wmiLock) _wmiScope = null;
                Logger.WriteLine("ScreenBrightness.Get error: " + ex.Message);
            }

            return -1;
        }

        public static void Set(int brightness)
        {
            brightness = Math.Clamp(brightness, 0, 100);
            if (_lastKnownBrightness == brightness)
            {
                AppConfig.Set("screen_brightness", brightness);
                return;
            }

            // Fast path: Direct kernel driver IOCTL (\\.\LCD)
            if (TrySetIoctl(brightness))
            {
                _lastKnownBrightness = brightness;
                AppConfig.Set("screen_brightness", brightness);
                return;
            }

            // Fallback path: WMI WmiMonitorBrightnessMethods
            try
            {
                var scope = GetScope();
                using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM WmiMonitorBrightnessMethods"));
                using var instances = searcher.Get();
                var args = new object[] { 1, brightness };
                foreach (ManagementObject instance in instances)
                {
                    using (instance)
                    {
                        try
                        {
                            instance.InvokeMethod("WmiSetBrightness", args);
                            _lastKnownBrightness = brightness;
                            Logger.WriteLine($"ScreenBrightness.Set: {brightness}%");
                        }
                        catch (Exception ex)
                        {
                            Logger.WriteLine("WmiSetBrightness instance error: " + ex.Message);
                        }
                    }
                }

                AppConfig.Set("screen_brightness", brightness);
            }
            catch (Exception ex)
            {
                lock (_wmiLock) _wmiScope = null;
                Logger.WriteLine("ScreenBrightness.Set error: " + ex.Message);
            }
        }

        private static int _targetBrightness = -1;
        private static int _isSetting = 0;

        public static void SetAsync(int brightness)
        {
            brightness = Math.Clamp(brightness, 0, 100);
            AppConfig.Set("screen_brightness", brightness);
            System.Threading.Interlocked.Exchange(ref _targetBrightness, brightness);

            if (System.Threading.Interlocked.CompareExchange(ref _isSetting, 1, 0) == 0)
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        while (true)
                        {
                            int target = System.Threading.Interlocked.Exchange(ref _targetBrightness, -1);
                            if (target < 0) break;
                            Set(target);
                        }
                    }
                    finally
                    {
                        System.Threading.Interlocked.Exchange(ref _isSetting, 0);
                        int pending = System.Threading.Interlocked.Exchange(ref _targetBrightness, -1);
                        if (pending >= 0) Set(pending);
                    }
                });
            }
        }

        public static int Adjust(int delta)
        {
            int brightness = _lastKnownBrightness >= 0 ? _lastKnownBrightness : Get();
            if (brightness < 0) brightness = AppConfig.Get("screen_brightness", 100);
            Debug.WriteLine(brightness);
            brightness = Math.Min(100, Math.Max(0, brightness + delta));
            Set(brightness);
            return brightness;
        }

    }
}
