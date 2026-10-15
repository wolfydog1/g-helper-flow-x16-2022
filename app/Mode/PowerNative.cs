using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace GHelper.Mode
{
    internal class PowerNative
    {
        [DllImport("PowrProf.dll", CharSet = CharSet.Unicode)]
        static extern UInt32 PowerWriteDCValueIndex(IntPtr RootPowerKey,
            [MarshalAs(UnmanagedType.LPStruct)] Guid SchemeGuid,
            [MarshalAs(UnmanagedType.LPStruct)] Guid SubGroupOfPowerSettingsGuid,
            [MarshalAs(UnmanagedType.LPStruct)] Guid PowerSettingGuid,
            int AcValueIndex);

        [DllImport("PowrProf.dll", CharSet = CharSet.Unicode)]
        static extern UInt32 PowerWriteACValueIndex(IntPtr RootPowerKey,
            [MarshalAs(UnmanagedType.LPStruct)] Guid SchemeGuid,
            [MarshalAs(UnmanagedType.LPStruct)] Guid SubGroupOfPowerSettingsGuid,
            [MarshalAs(UnmanagedType.LPStruct)] Guid PowerSettingGuid,
            int AcValueIndex);

        [DllImport("PowrProf.dll", CharSet = CharSet.Unicode)]
        static extern UInt32 PowerReadACValueIndex(IntPtr RootPowerKey,
            [MarshalAs(UnmanagedType.LPStruct)] Guid SchemeGuid,
            [MarshalAs(UnmanagedType.LPStruct)] Guid SubGroupOfPowerSettingsGuid,
            [MarshalAs(UnmanagedType.LPStruct)] Guid PowerSettingGuid,
            out IntPtr AcValueIndex
            );

        [DllImport("PowrProf.dll", CharSet = CharSet.Unicode)]
        static extern UInt32 PowerReadDCValueIndex(IntPtr RootPowerKey,
            [MarshalAs(UnmanagedType.LPStruct)] Guid SchemeGuid,
            [MarshalAs(UnmanagedType.LPStruct)] Guid SubGroupOfPowerSettingsGuid,
            [MarshalAs(UnmanagedType.LPStruct)] Guid PowerSettingGuid,
            out IntPtr AcValueIndex
            );


        [DllImport("powrprof.dll")]
        static extern uint PowerReadACValue(
            IntPtr RootPowerKey,
            Guid SchemeGuid,
            Guid SubGroupOfPowerSettingGuid,
            Guid PowerSettingGuid,
            ref int Type,
            ref IntPtr Buffer,
            ref uint BufferSize
            );


        [DllImport("PowrProf.dll", CharSet = CharSet.Unicode)]
        static extern UInt32 PowerSetActiveScheme(IntPtr RootPowerKey,
            [MarshalAs(UnmanagedType.LPStruct)] Guid SchemeGuid);

        [DllImport("PowrProf.dll", CharSet = CharSet.Unicode)]
        static extern UInt32 PowerGetActiveScheme(IntPtr UserPowerKey, out IntPtr ActivePolicyGuid);

        static readonly Guid GUID_CPU = new Guid("54533251-82be-4824-96c1-47b60b740d00");
        static readonly Guid GUID_BOOST = new Guid("be337238-0d82-4146-a960-4f3749d470c7");

        private static Guid GUID_SLEEP_SUBGROUP = new Guid("238c9fa8-0aad-41ed-83f4-97be242c8f20");
        private static Guid GUID_STANDBYIDLE = new Guid("29f6c1db-86da-48c5-9fdb-f2b67b1f44da");
        private static Guid GUID_HIBERNATEIDLE = new Guid("9d7815a6-7ee4-497e-8888-515a05f02364");

        private static Guid GUID_VIDEO_SUBGROUP = new Guid("7516b95f-f776-4464-8c53-06167f40cc99");
        private static Guid GUID_VIDEOIDLE = new Guid("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");
        private static Guid GUID_VIDEODIM = new Guid("17aaa29b-8b43-4b94-aafe-35f64daaf1ee");
        private static Guid GUID_ADAPTBRIGHT = new Guid("fbd9aa66-9553-4097-ba44-ed6e9d65eab8");

        private static Guid GUID_SYSTEM_BUTTON_SUBGROUP = new Guid("4f971e89-eebd-4455-a8de-9e59040e7347");
        private static Guid GUID_LIDACTION = new Guid("5CA83367-6E45-459F-A27B-476B1D01C936");
        private static Guid GUID_PBUTTONACTION = new Guid("7648eac1-f84a-4788-ac07-10819773c1ac");

        private static Guid GUID_SUB_PCIEXPRESS = new Guid("501a4d13-42af-4429-9fd1-a8218c268e20");
        private static Guid GUID_PCI_EXPRESS_ASPM = new Guid("ee12f906-d277-404b-b6da-e5fa1a576df5");

        private static Guid GUID_SUB_NONE = new Guid("fea3413e-7e05-4911-9a71-700331f1c294");
        private static Guid GUID_CONNECTIVITY_IN_STANDBY = new Guid("f15576e8-98b7-4186-b944-eafa664402d9");

        [DllImportAttribute("powrprof.dll", EntryPoint = "PowerGetActualOverlayScheme")]
        public static extern uint PowerGetActualOverlayScheme(out Guid ActualOverlayGuid);

        [DllImportAttribute("powrprof.dll", EntryPoint = "PowerGetEffectiveOverlayScheme")]
        public static extern uint PowerGetEffectiveOverlayScheme(out Guid EffectiveOverlayGuid);

        [DllImportAttribute("powrprof.dll", EntryPoint = "PowerSetActiveOverlayScheme")]
        public static extern uint PowerSetActiveOverlayScheme(Guid OverlaySchemeGuid);

        const string POWER_SILENT = "961cc777-2547-4f9d-8174-7d86181b8a7a";
        const string POWER_BALANCED = "00000000-0000-0000-0000-000000000000";
        const string POWER_TURBO = "ded574b5-45a0-4f42-8737-46345c09c238";

        const string PLAN_BALANCED = "381b4222-f694-41f0-9685-ff5bb260df2e";
        const string PLAN_HIGH_PERFORMANCE = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

        static List<string> overlays = new() {
                POWER_BALANCED,
                POWER_TURBO,
                POWER_SILENT,
            };

        public static Dictionary<string, string> powerModes = new Dictionary<string, string>
            {
                { POWER_SILENT, "Best Power Efficiency" },
                { POWER_BALANCED, "Balanced" },
                { POWER_TURBO, "Best Performance" },
                { PLAN_HIGH_PERFORMANCE, "High Performance Plan"},
            };
        static Guid GetActiveScheme()
        {
            IntPtr pActiveSchemeGuid;
            var hr = PowerGetActiveScheme(IntPtr.Zero, out pActiveSchemeGuid);
            Guid activeSchemeGuid = (Guid)Marshal.PtrToStructure(pActiveSchemeGuid, typeof(Guid));
            return activeSchemeGuid;
        }

        public static int GetCPUBoost()
        {
            IntPtr AcValueIndex;
            Guid activeSchemeGuid = GetActiveScheme();

            UInt32 value = PowerReadACValueIndex(IntPtr.Zero,
                 activeSchemeGuid,
                 GUID_CPU,
                 GUID_BOOST, out AcValueIndex);

            return AcValueIndex.ToInt32();

        }

        public static void SetCPUBoost(int boost = 0)
        {
            Guid activeSchemeGuid = GetActiveScheme();

            if (boost == GetCPUBoost()) return;

            var hrAC = PowerWriteACValueIndex(
                 IntPtr.Zero,
                 activeSchemeGuid,
                 GUID_CPU,
                 GUID_BOOST,
                 boost);

            PowerSetActiveScheme(IntPtr.Zero, activeSchemeGuid);

            var hrDC = PowerWriteDCValueIndex(
                 IntPtr.Zero,
                 activeSchemeGuid,
                 GUID_CPU,
                 GUID_BOOST,
                 boost);

            PowerSetActiveScheme(IntPtr.Zero, activeSchemeGuid);

            Logger.WriteLine("Boost " + boost);
        }

        public static string GetPowerMode()
        {
            if (GetActiveScheme().ToString() == PLAN_HIGH_PERFORMANCE) return PLAN_HIGH_PERFORMANCE;
            PowerGetEffectiveOverlayScheme(out Guid activeScheme);
            return activeScheme.ToString();
        }

        public static void SetPowerMode(string scheme)
        {

            if (scheme == PLAN_HIGH_PERFORMANCE)
            {
                SetPowerPlan(scheme);
                return;
            }
            else
            {
                // Power plan from config or defaulting to balanced
                string plan = AppConfig.GetModeString("scheme");
                if (Program.currentSource == Program.PowerSource.USBC && AppConfig.GetModeString("scheme_usbc") is string usbc) plan = usbc;
                SetPowerPlan(plan);            
            }

            if (!overlays.Contains(scheme)) return;

            Guid guidScheme = new Guid(scheme);

            uint status = PowerGetEffectiveOverlayScheme(out Guid activeScheme);

            if (GetBatterySaverStatus())
            {
                Logger.WriteLine("Battery Saver detected");
                return;
            }

            if (status != 0 || activeScheme != guidScheme)
            {
                status = PowerSetActiveOverlayScheme(guidScheme);
                Logger.WriteLine("Power Mode " + activeScheme + " -> " + scheme + ":" + (status == 0 ? "OK" : status));
            }

        }

        public static void SetPowerPlan(string scheme)
        {
            // Skipping power modes
            if (overlays.Contains(scheme)) return;

            if (scheme is null) scheme = PLAN_BALANCED;
            var activeScheme = GetActiveScheme().ToString();
            if (activeScheme == scheme) return;

            uint status = PowerSetActiveScheme(IntPtr.Zero, new Guid(scheme));
            Logger.WriteLine($"Power Plan {activeScheme} -> {scheme} :" + (status == 0 ? "OK" : status));
        }

        public static string GetDefaultPowerMode(int mode)
        {
            switch (mode)
            {
                case 1: // turbo
                    return POWER_TURBO;
                case 2: //silent
                    return POWER_SILENT;
                case 3:
                    return PLAN_HIGH_PERFORMANCE;
                default: // balanced
                    return POWER_BALANCED;
            }
        }

        public static void SetPowerMode(int mode)
        {
            SetPowerMode(GetDefaultPowerMode(mode));
        }

        public static int GetASPM()
        {
            Guid activeSchemeGuid = GetActiveScheme();
            IntPtr activeIndex;

            PowerReadACValueIndex(IntPtr.Zero,
                    activeSchemeGuid,
                    GUID_SUB_PCIEXPRESS,
                    GUID_PCI_EXPRESS_ASPM, out activeIndex);

            return activeIndex.ToInt32();
        }

        public static void SetASPM(int status = 0)
        {
            Guid activeSchemeGuid = GetActiveScheme();
            var currentASPM = GetASPM();
            if (currentASPM == status) return;

            var hrAC = PowerWriteACValueIndex(
                IntPtr.Zero,
                activeSchemeGuid,
                GUID_SUB_PCIEXPRESS,
                GUID_PCI_EXPRESS_ASPM,
                status);

            PowerSetActiveScheme(IntPtr.Zero, activeSchemeGuid);
            Logger.WriteLine($"Changed AC ASPM {currentASPM} -> {status}");
        }

        public static void SetBalancedASPM(int status = 0)
        {
            if (GetActiveScheme().ToString() != PLAN_BALANCED) return;
            SetASPM(status);
        }

        public static void SetConnectivityInStandby(int ac = 0, int dc = 0)
        {
            Guid activeSchemeGuid = GetActiveScheme();

            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\{activeSchemeGuid}\{GUID_CONNECTIVITY_IN_STANDBY}");
            if (key != null && (int?)key.GetValue("ACSettingIndex") == ac && (int?)key.GetValue("DCSettingIndex") == dc) return;

            var hrAC = PowerWriteACValueIndex(
                IntPtr.Zero,
                activeSchemeGuid,
                GUID_SUB_NONE,
                GUID_CONNECTIVITY_IN_STANDBY,
                ac);

            var hrDC = PowerWriteDCValueIndex(
                IntPtr.Zero,
                activeSchemeGuid,
                GUID_SUB_NONE,
                GUID_CONNECTIVITY_IN_STANDBY,
                dc);

            PowerSetActiveScheme(IntPtr.Zero, activeSchemeGuid);
            Logger.WriteLine($"Connectivity in Standby {ac}/{dc}: " + (hrAC == 0 && hrDC == 0 ? "OK" : $"{hrAC}/{hrDC}"));
        }

        public static int GetLidAction(bool ac)
        {
            Guid activeSchemeGuid = GetActiveScheme();

            IntPtr activeIndex;
            if (ac)
                PowerReadACValueIndex(IntPtr.Zero,
                     activeSchemeGuid,
                     GUID_SYSTEM_BUTTON_SUBGROUP,
                     GUID_LIDACTION, out activeIndex);

            else
                PowerReadDCValueIndex(IntPtr.Zero,
                    activeSchemeGuid,
                    GUID_SYSTEM_BUTTON_SUBGROUP,
                    GUID_LIDACTION, out activeIndex);


            return activeIndex.ToInt32();
        }


        public static void SetLidAction(int action, bool ac)
        {
            /**
             * 0: Do nothing
             * 1: Sleep
             * 2: Hibernate
             * 3: Shutdown
             */
            Guid activeSchemeGuid = GetActiveScheme();

            var hr = ac
                ? PowerWriteACValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_SYSTEM_BUTTON_SUBGROUP, GUID_LIDACTION, action)
                : PowerWriteDCValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_SYSTEM_BUTTON_SUBGROUP, GUID_LIDACTION, action);

            PowerSetActiveScheme(IntPtr.Zero, activeSchemeGuid);
            Logger.WriteLine($"Changed Lid Action ({(ac ? "AC" : "DC")}) to {action}: " + (hr == 0 ? "OK" : hr));
        }

        public static int GetSleepTimeout(bool ac)
        {
            Guid activeSchemeGuid = GetActiveScheme();
            IntPtr seconds;
            uint hr = ac
                ? PowerReadACValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_SLEEP_SUBGROUP, GUID_STANDBYIDLE, out seconds)
                : PowerReadDCValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_SLEEP_SUBGROUP, GUID_STANDBYIDLE, out seconds);

            if (hr != 0) return 0;
            int sec = seconds.ToInt32();
            if (sec <= 0 || sec > 1000000) return 0;
            return sec / 60;
        }

        public static void SetSleepTimeout(int minutes, bool ac)
        {
            int seconds = minutes * 60;
            Guid activeSchemeGuid = GetActiveScheme();
            var hr = ac
                ? PowerWriteACValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_SLEEP_SUBGROUP, GUID_STANDBYIDLE, seconds)
                : PowerWriteDCValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_SLEEP_SUBGROUP, GUID_STANDBYIDLE, seconds);

            PowerSetActiveScheme(IntPtr.Zero, activeSchemeGuid);
            Logger.WriteLine($"Changed Sleep Timeout ({(ac ? "AC" : "DC")}) to {minutes}m: " + (hr == 0 ? "OK" : hr));
        }

        public static int GetDisplayTimeout(bool ac)
        {
            Guid activeSchemeGuid = GetActiveScheme();
            IntPtr seconds;
            uint hr = ac
                ? PowerReadACValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_VIDEO_SUBGROUP, GUID_VIDEOIDLE, out seconds)
                : PowerReadDCValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_VIDEO_SUBGROUP, GUID_VIDEOIDLE, out seconds);

            if (hr != 0) return 0;
            int sec = seconds.ToInt32();
            if (sec <= 0 || sec > 1000000) return 0;
            return sec / 60;
        }

        public static void SetDisplayTimeout(int minutes, bool ac)
        {
            int seconds = minutes * 60;
            Guid activeSchemeGuid = GetActiveScheme();
            var hr = ac
                ? PowerWriteACValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_VIDEO_SUBGROUP, GUID_VIDEOIDLE, seconds)
                : PowerWriteDCValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_VIDEO_SUBGROUP, GUID_VIDEOIDLE, seconds);

            PowerSetActiveScheme(IntPtr.Zero, activeSchemeGuid);
            Logger.WriteLine($"Changed Display Timeout ({(ac ? "AC" : "DC")}) to {minutes}m: " + (hr == 0 ? "OK" : hr));
        }

        public static int GetDisplayDimTimeout(bool ac)
        {
            Guid activeSchemeGuid = GetActiveScheme();
            IntPtr seconds;
            uint hr = ac
                ? PowerReadACValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_VIDEO_SUBGROUP, GUID_VIDEODIM, out seconds)
                : PowerReadDCValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_VIDEO_SUBGROUP, GUID_VIDEODIM, out seconds);

            if (hr != 0) return 0;
            int sec = seconds.ToInt32();
            if (sec <= 0 || sec > 1000000) return 0;
            return sec / 60;
        }

        public static void SetDisplayDimTimeout(int minutes, bool ac)
        {
            int seconds = minutes * 60;
            Guid activeSchemeGuid = GetActiveScheme();
            var hr = ac
                ? PowerWriteACValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_VIDEO_SUBGROUP, GUID_VIDEODIM, seconds)
                : PowerWriteDCValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_VIDEO_SUBGROUP, GUID_VIDEODIM, seconds);

            PowerSetActiveScheme(IntPtr.Zero, activeSchemeGuid);
            Logger.WriteLine($"Changed Display Dim Timeout ({(ac ? "AC" : "DC")}) to {minutes}m: " + (hr == 0 ? "OK" : hr));
        }

        public static bool GetAdaptiveBrightness(bool ac)
        {
            Guid activeSchemeGuid = GetActiveScheme();
            IntPtr val;
            uint hr = ac
                ? PowerReadACValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_VIDEO_SUBGROUP, GUID_ADAPTBRIGHT, out val)
                : PowerReadDCValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_VIDEO_SUBGROUP, GUID_ADAPTBRIGHT, out val);

            if (hr != 0) return false;
            return val.ToInt32() == 1;
        }

        public static void SetAdaptiveBrightness(bool enabled, bool ac)
        {
            int val = enabled ? 1 : 0;
            Guid activeSchemeGuid = GetActiveScheme();
            var hr = ac
                ? PowerWriteACValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_VIDEO_SUBGROUP, GUID_ADAPTBRIGHT, val)
                : PowerWriteDCValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_VIDEO_SUBGROUP, GUID_ADAPTBRIGHT, val);

            PowerSetActiveScheme(IntPtr.Zero, activeSchemeGuid);
            Logger.WriteLine($"Changed Adaptive Brightness ({(ac ? "AC" : "DC")}) to {val}: " + (hr == 0 ? "OK" : hr));
        }

        public static int GetHibernateTimeout(bool ac)
        {
            Guid activeSchemeGuid = GetActiveScheme();
            IntPtr seconds;
            uint hr = ac
                ? PowerReadACValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_SLEEP_SUBGROUP, GUID_HIBERNATEIDLE, out seconds)
                : PowerReadDCValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_SLEEP_SUBGROUP, GUID_HIBERNATEIDLE, out seconds);

            if (hr != 0) return 0;
            int sec = seconds.ToInt32();
            if (sec <= 0 || sec > 1000000) return 0;
            return sec / 60;
        }

        public static void SetHibernateTimeout(int minutes, bool ac)
        {
            int seconds = minutes * 60;
            Guid activeSchemeGuid = GetActiveScheme();
            var hr = ac
                ? PowerWriteACValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_SLEEP_SUBGROUP, GUID_HIBERNATEIDLE, seconds)
                : PowerWriteDCValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_SLEEP_SUBGROUP, GUID_HIBERNATEIDLE, seconds);

            PowerSetActiveScheme(IntPtr.Zero, activeSchemeGuid);
            Logger.WriteLine($"Changed Hibernate Timeout ({(ac ? "AC" : "DC")}) to {minutes}m: " + (hr == 0 ? "OK" : hr));
        }

        public static int GetPowerButtonAction(bool ac)
        {
            Guid activeSchemeGuid = GetActiveScheme();
            IntPtr activeIndex;
            uint hr = ac
                ? PowerReadACValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_SYSTEM_BUTTON_SUBGROUP, GUID_PBUTTONACTION, out activeIndex)
                : PowerReadDCValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_SYSTEM_BUTTON_SUBGROUP, GUID_PBUTTONACTION, out activeIndex);

            if (hr != 0) return 1;
            return activeIndex.ToInt32();
        }

        public static void SetPowerButtonAction(int action, bool ac)
        {
            Guid activeSchemeGuid = GetActiveScheme();
            var hr = ac
                ? PowerWriteACValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_SYSTEM_BUTTON_SUBGROUP, GUID_PBUTTONACTION, action)
                : PowerWriteDCValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_SYSTEM_BUTTON_SUBGROUP, GUID_PBUTTONACTION, action);

            PowerSetActiveScheme(IntPtr.Zero, activeSchemeGuid);
            Logger.WriteLine($"Changed Power Button Action ({(ac ? "AC" : "DC")}) to {action}: " + (hr == 0 ? "OK" : hr));
        }

        public static int GetHibernateAfter() => GetHibernateTimeout(false);
        public static void SetHibernateAfter(int minutes) => SetHibernateTimeout(minutes, false);

        [DllImport("Kernel32")]
        private static extern bool GetSystemPowerStatus(SystemPowerStatus sps);
        public enum ACLineStatus : byte
        {
            Offline = 0, Online = 1, Unknown = 255
        }

        public enum BatteryFlag : byte
        {
            High = 1,
            Low = 2,
            Critical = 4,
            Charging = 8,
            NoSystemBattery = 128,
            Unknown = 255
        }

        // Fields must mirror their unmanaged counterparts, in order
        [StructLayout(LayoutKind.Sequential)]
        public class SystemPowerStatus
        {
            public ACLineStatus ACLineStatus;
            public BatteryFlag BatteryFlag;
            public Byte BatteryLifePercent;
            public Byte SystemStatusFlag;
            public Int32 BatteryLifeTime;
            public Int32 BatteryFullLifeTime;
        }

        public static bool GetBatterySaverStatus()
        {
            try
            {
                var status = Registry.GetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\Power", "EnergySaverState", null);
                if (status == null)
                {
                    SystemPowerStatus sps = new SystemPowerStatus();
                    GetSystemPowerStatus(sps);
                    return (sps.SystemStatusFlag > 0);
                }
                return (int)status == 1;
            }
            catch (Exception e)
            {
                Logger.WriteLine("Can't check EnergySaverState" + e.Message);
                return false;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern uint SetThreadExecutionState(uint esFlags);

        const uint ES_SYSTEM_REQUIRED = 0x00000001;
        const uint ES_DISPLAY_REQUIRED = 0x00000002;
        const uint ES_CONTINUOUS = 0x80000000;

        public static bool IsKeepAwake { get; private set; } = false;

        public static bool ToggleKeepAwake()
        {
            IsKeepAwake = !IsKeepAwake;
            if (IsKeepAwake)
            {
                SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
                Logger.WriteLine("KeepAwake: Enabled (System + Display)");
            }
            else
            {
                SetThreadExecutionState(ES_CONTINUOUS);
                Logger.WriteLine("KeepAwake: Disabled");
            }
            return IsKeepAwake;
        }

        private static readonly Guid GUID_SUB_ENERGYSAVER = new("de830923-a562-41af-a086-e3a2c6bad2da");
        private static readonly Guid GUID_ESBATTTHRESHOLD = new("e69653ca-cf7f-4f05-aa73-cb833fa90ad4");

        public static bool IsPowerSaverActive()
        {
            if (AppConfig.Is("power_saver_override")) return true;
            if (GetBatterySaverStatus()) return true;
            PowerGetEffectiveOverlayScheme(out Guid activeOverlay);
            return activeOverlay.ToString().Equals(POWER_SILENT, StringComparison.OrdinalIgnoreCase);
        }

        public static bool TogglePowerSaver()
        {
            bool next = !IsPowerSaverActive();
            AppConfig.Set("power_saver_override", next ? 1 : 0);

            try
            {
                Guid activeSchemeGuid = GetActiveScheme();
                if (next)
                {
                    PowerSetActiveOverlayScheme(new Guid(POWER_SILENT));
                    PowerWriteDCValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_SUB_ENERGYSAVER, GUID_ESBATTTHRESHOLD, 100);
                    PowerSetActiveScheme(IntPtr.Zero, activeSchemeGuid);
                    Program.acpi?.DeviceSet(AsusACPI.PowerSavingMode, 1, "PowerSavingMode");
                }
                else
                {
                    PowerWriteDCValueIndex(IntPtr.Zero, activeSchemeGuid, GUID_SUB_ENERGYSAVER, GUID_ESBATTTHRESHOLD, 20);
                    PowerSetActiveScheme(IntPtr.Zero, activeSchemeGuid);
                    string defaultOverlay = GetDefaultPowerMode(Modes.GetCurrentBase());
                    if (overlays.Contains(defaultOverlay))
                    {
                        PowerSetActiveOverlayScheme(new Guid(defaultOverlay));
                    }
                    Program.acpi?.DeviceSet(AsusACPI.PowerSavingMode, 0, "PowerSavingMode");
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("TogglePowerSaver error: " + ex.Message);
            }

            return next;
        }

    }
}
