using System.Diagnostics;
using GHelper.Helpers;
using System.Runtime.InteropServices;
using System.Text;

public enum TouchPenMode
{
    TouchAndPen = 0,
    PenOnly = 1,
    Off = 2
}

public static class TouchscreenHelper
{
    private static readonly Guid GUID_DEVCLASS_HIDCLASS = new("745a17a0-74d3-11d0-b6fe-00a0c90f57da");
    private const uint DIGCF_PRESENT = 0x00000002;
    private const uint SPDRP_FRIENDLYNAME = 0x0000000C;
    private const uint DIF_PROPERTYCHANGE = 0x00000012;
    private const uint DICS_ENABLE = 0x00000001;
    private const uint DICS_DISABLE = 0x00000002;
    private const uint DICS_FLAG_GLOBAL = 0x00000001;
    private const uint DN_STARTED = 0x00000008;

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_CLASSINSTALL_HEADER
    {
        public uint cbSize;
        public uint InstallFunction;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_PROPCHANGE_PARAMS
    {
        public SP_CLASSINSTALL_HEADER ClassInstallHeader;
        public uint StateChange;
        public uint Scope;
        public uint HwProfile;
    }

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, uint Flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceRegistryProperty(
        IntPtr DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData,
        uint Property,
        out uint PropertyRegDataType,
        StringBuilder PropertyBuffer,
        uint PropertyBufferSize,
        out uint RequiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiSetClassInstallParams(
        IntPtr DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData,
        ref SP_PROPCHANGE_PARAMS ClassInstallParams,
        int ClassInstallParamsSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiCallClassInstaller(
        uint InstallFunction,
        IntPtr DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

    [DllImport("cfgmgr32.dll", SetLastError = true)]
    private static extern int CM_Get_DevNode_Status(out uint ulStatus, out uint ulProblemNumber, uint dnDevInst, uint ulFlags);

    private static bool IsTouchDeviceName(string name)
    {
        return name.Contains("touch screen", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("touchscreen", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPenDeviceName(string name)
    {
        return name.Contains("compliant pen", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("stylus", StringComparison.OrdinalIgnoreCase);
    }

    public static TouchPenMode GetMode()
    {
        bool? touchOn = null;
        bool? penOn = null;

        Guid hidGuid = GUID_DEVCLASS_HIDCLASS;
        IntPtr hDevInfo = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT);
        if (hDevInfo != new IntPtr(-1))
        {
            try
            {
                SP_DEVINFO_DATA devInfo = new() { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
                StringBuilder sb = new(256);
                for (uint i = 0; SetupDiEnumDeviceInfo(hDevInfo, i, ref devInfo); i++)
                {
                    sb.Clear();
                    if (SetupDiGetDeviceRegistryProperty(hDevInfo, ref devInfo, SPDRP_FRIENDLYNAME, out _, sb, (uint)sb.Capacity, out _))
                    {
                        string name = sb.ToString();
                        if (IsTouchDeviceName(name))
                        {
                            if (CM_Get_DevNode_Status(out uint status, out uint problem, devInfo.DevInst, 0) == 0)
                                touchOn = (status & DN_STARTED) != 0 && problem == 0;
                        }
                        else if (IsPenDeviceName(name))
                        {
                            if (CM_Get_DevNode_Status(out uint status, out uint problem, devInfo.DevInst, 0) == 0)
                                penOn = (status & DN_STARTED) != 0 && problem == 0;
                        }
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(hDevInfo);
            }
        }

        if (touchOn == true)
            return TouchPenMode.TouchAndPen;
        if (touchOn == false && penOn == true)
            return TouchPenMode.PenOnly;
        if (touchOn == false)
            return TouchPenMode.Off;

        return TouchPenMode.TouchAndPen;
    }

    public static bool? GetStatus()
    {
        return GetMode() == TouchPenMode.TouchAndPen;
    }

    private static void RunPnpCommand(string script)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                CreateNoWindow = true,
                UseShellExecute = !ProcessHelper.IsUserAdministrator(),
                Verb = ProcessHelper.IsUserAdministrator() ? "" : "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(3000);
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"Fallback PnP execution error: {ex.Message}");
        }
    }

    public static void SetMode(TouchPenMode mode)
    {
        bool enableTouch = mode == TouchPenMode.TouchAndPen;
        bool enablePen = mode == TouchPenMode.TouchAndPen || mode == TouchPenMode.PenOnly;

        bool nativeTouchSuccess = SetDeviceClassState(IsTouchDeviceName, enableTouch);
        bool nativePenSuccess = SetDeviceClassState(IsPenDeviceName, enablePen);

        if (!nativeTouchSuccess)
        {
            RunPnpCommand((enableTouch ? "Enable-PnpDevice" : "Disable-PnpDevice") + " -InstanceId (Get-PnpDevice -FriendlyName '*touch*screen*').InstanceId -Confirm:$false");
        }

        if (!nativePenSuccess && mode != TouchPenMode.TouchAndPen)
        {
            RunPnpCommand((enablePen ? "Enable-PnpDevice" : "Disable-PnpDevice") + " -InstanceId (Get-PnpDevice -FriendlyName '*compliant*pen*').InstanceId -Confirm:$false");
        }

        AppConfig.Set("touch_pen_mode", (int)mode);
        if (mode == TouchPenMode.PenOnly)
        {
            SetTouchKeyboardAutoInvoke(true);
        }
        Logger.WriteLine($"TouchPenMode set to {mode} (Touch={enableTouch}, Pen={enablePen})");
    }

    public static void SetTouchKeyboardAutoInvoke(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\TabletTip\1.7", true);
            key?.SetValue("EnableDesktopModeAutoInvoke", enable ? 1 : 0, Microsoft.Win32.RegistryValueKind.DWord);
            key?.SetValue("DisableNewKeyboardExperience", 0, Microsoft.Win32.RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"SetTouchKeyboardAutoInvoke error: {ex.Message}");
        }
    }

    private static bool SetDeviceClassState(Func<string, bool> predicate, bool enable)
    {
        bool anyMatched = false;
        bool allSucceeded = true;

        Guid hidGuid = GUID_DEVCLASS_HIDCLASS;
        IntPtr hDevInfo = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT);
        if (hDevInfo == new IntPtr(-1)) return false;

        try
        {
            SP_DEVINFO_DATA devInfo = new() { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
            StringBuilder sb = new(256);

            for (uint i = 0; SetupDiEnumDeviceInfo(hDevInfo, i, ref devInfo); i++)
            {
                sb.Clear();
                if (!SetupDiGetDeviceRegistryProperty(hDevInfo, ref devInfo, SPDRP_FRIENDLYNAME, out _, sb, (uint)sb.Capacity, out _))
                    continue;

                string name = sb.ToString();
                if (!predicate(name)) continue;

                anyMatched = true;
                SP_PROPCHANGE_PARAMS propParams = new()
                {
                    ClassInstallHeader = new SP_CLASSINSTALL_HEADER
                    {
                        cbSize = (uint)Marshal.SizeOf<SP_CLASSINSTALL_HEADER>(),
                        InstallFunction = DIF_PROPERTYCHANGE
                    },
                    StateChange = enable ? DICS_ENABLE : DICS_DISABLE,
                    Scope = DICS_FLAG_GLOBAL,
                    HwProfile = 0
                };

                if (!SetupDiSetClassInstallParams(hDevInfo, ref devInfo, ref propParams, Marshal.SizeOf<SP_PROPCHANGE_PARAMS>()) ||
                    !SetupDiCallClassInstaller(DIF_PROPERTYCHANGE, hDevInfo, ref devInfo))
                {
                    allSucceeded = false;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"SetupAPI SetDeviceClassState error: {ex.Message}");
            return false;
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(hDevInfo);
        }

        return anyMatched && allSucceeded;
    }

    public static void ToggleTouchscreen(bool status)
    {
        SetMode(status ? TouchPenMode.TouchAndPen : TouchPenMode.Off);
    }
}
