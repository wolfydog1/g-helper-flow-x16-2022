// Reference : thanks to https://github.com/RomanYazvinsky/ for initial discovery of XGM payloads

using GHelper.Helpers;
using HidSharp;
using HidSharp.Reports;
using System.Text;

namespace GHelper.USB
{
    public static class XGM
    {
        const byte XGM_REPORT_ID = 0x5e;
        const int ASUS_ID = 0x0b05;
        static readonly int[] deviceIds = { 0x1970, 0x1a9a, 0x1C28, 0x1C29, 0x1BC1, 0x19FC, 0x1A27 };

        public static HidDevice? GetDevice()
        {
            try
            {
                var asusDevices = DeviceList.Local.GetHidDevices(ASUS_ID);

                // Look for known XG Mobile PIDs first, verifying the device supports the 0x5e Feature report
                foreach (var device in asusDevices)
                {
                    if (!deviceIds.Contains(device.ProductID) ||
                        AsusHid.ALL_PIDS.Contains(device.ProductID) ||
                        device.ProductID == 0x19B6)
                        continue;

                    try
                    {
                        if (device.CanOpen && device.GetReportDescriptor().TryGetReport(ReportType.Feature, XGM_REPORT_ID, out _))
                            return device;
                    }
                    catch { }
                }

                // Fallback: detect any ASUS HID device with feature report ID 0x5e ONLY if:
                // 1) ACPI reports physical XGM hardware connection
                // 2) Device is NOT an internal laptop PID (keyboard, touchpad, etc.)
                bool acpiConnected = Program.acpi is not null &&
                    (Program.acpi.DeviceGet(AsusACPI.GPUXGConnected) > 0 || Program.acpi.DeviceGet(AsusACPI.GPUXG) == 1);

                if (acpiConnected)
                {
                    foreach (var device in asusDevices)
                    {
                        if (AsusHid.ALL_PIDS.Contains(device.ProductID) || device.ProductID == 0x19B6)
                            continue;

                        try
                        {
                            if (device.CanOpen && device.GetReportDescriptor().TryGetReport(ReportType.Feature, XGM_REPORT_ID, out _))
                            {
                                Logger.WriteLine($"Dynamically discovered XGM Device: PID=0x{device.ProductID:X4}, MaxFeatureReportLength={device.GetMaxFeatureReportLength()}");
                                return device;
                            }
                        }
                        catch { }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error getting XGM device: {ex}");
                return null;
            }
        }

        public static bool IsConnected()
        {
            return GetDevice() is not null;
        }

        public static void Write(byte[] data)
        {
            try
            {
                HidDevice? device = GetDevice();
                if (device is null)
                {
                    Logger.WriteLine("XGM USB device not found");
                    return;
                }

                using (HidStream hidStream = device.Open())
                {
                    int reportLen = device.GetMaxFeatureReportLength();
                    int payloadLen = Math.Max(reportLen > 0 ? reportLen : 300, data.Length);
                    byte[] payload = new byte[payloadLen];
                    data.CopyTo(payload, 0);
                    hidStream.SetFeature(payload);
                    Logger.WriteLine($"XGM-{device.ProductID}|{reportLen}:{BitConverter.ToString(data)}");
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error accessing XGM device: {ex}");
            }
        }

        public static int? LastFanRPM { get; private set; } = null;
        public static int? LastTargetRPM { get; private set; } = null;
        public static int? LastHotspotTemp { get; private set; } = null;
        public static int? LastCoreTemp { get; private set; } = null;

        public static bool IsIndependentFanEnabled() => AppConfig.Is("xgm_independent_fan");
        public static bool AlwaysShowFanCurve() => AppConfig.Is("show_xgm_fan");

        private static bool _isProbing = false;

        public static void Probe()
        {
            if (_isProbing) return;
            _isProbing = true;

            Task.Run(() =>
            {
                try
                {
                    // Logger.WriteLine("XGM Probe: Getting device...");
                    HidDevice? device = GetDevice();
                    if (device is null) { /* Logger.WriteLine("XGM Probe: Device is null"); */ return; }
                    
                    // Logger.WriteLine("XGM Probe: Opening stream...");
                    using (HidStream hidStream = device.Open())
                    {
                        try {
                            byte[] req = new byte[device.GetMaxFeatureReportLength()];
                            req[0] = XGM_REPORT_ID;
                            req[1] = 0xC0;
                            req[2] = 0x01;
                            req[3] = 0x01;
                            hidStream.SetFeature(req);
                            System.Threading.Thread.Sleep(50);
                        } catch (Exception ex) {
                            Logger.WriteLine($"XGM Probe SetFeature Error: {ex.Message}");
                        }

                        byte[] response = new byte[device.GetMaxFeatureReportLength()];
                        response[0] = XGM_REPORT_ID;
                        
                        try {
                            hidStream.GetFeature(response);
                            if (response[1] == 0xC0)
                            {
                                LastFanRPM = response[12] + (response[13] << 8);
                                LastTargetRPM = response[14] + (response[15] << 8);
                                int b6 = response[6];
                                int b7 = response[7];
                                int b8 = response[8];
                                int b9 = response[9];
                                int b18 = response[18];
                                int b19 = response[19];
                                LastHotspotTemp = (b6 > 0 && b6 < 135) ? b6 : null; // B6 is hotspot/VRAM temp
                                LastCoreTemp = (b9 > 0 && b9 < 125) ? b9 : null; // B9 is core temp
                                int targetRpm = LastTargetRPM.Value;
                                
                                Logger.WriteLine($"XGM Telemetry -> RPM: {LastFanRPM}, TargetRPM: {targetRpm}, B6(Hotspot): {b6}, B7: {b7}, B8: {b8}, B9(Core): {b9}, B18: {b18}, B19: {b19}");
                            }
                        } catch (Exception ex) {
                            Logger.WriteLine($"XGM Probe Read Error: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex) { 
                    Logger.WriteLine($"XGM Probe Outer Error: {ex.Message}");
                }
                finally
                {
                    _isProbing = false;
                }
            });
        }

        public static void Init()
        {
            Task.Run(async () =>
            {
                for (int i = 0; i < 5; i++)
                {
                    if (IsConnected())
                    {
                        // Exact Armoury Crate XGM Handshake Sequence
                        Write(Encoding.ASCII.GetBytes("^ASUS Tech.Inc.\0"));
                        await Task.Delay(25);
                        Write(Encoding.ASCII.GetBytes("^ASUS Tech.Inc.\0"));
                        await Task.Delay(25);
                        Write([XGM_REPORT_ID, 0xc5, 0x50]);
                        await Task.Delay(25);
                        Write([XGM_REPORT_ID, 0xce, 0x01]);
                        await Task.Delay(25);
                        Write([XGM_REPORT_ID, 0xce, 0x03]);
                        Light(AppConfig.Is("xmg_light"));
                        return;
                    }
                    await Task.Delay(500);
                }
            });
        }

        public static void Light(int brightness)
        {
            byte bVal = (byte)Math.Clamp(brightness, 0, 100);
            byte bScale = (byte)Math.Clamp(brightness * 255 / 100, 0, 255);
            Write([XGM_REPORT_ID, 0xc5, bVal]);
            Write([XGM_REPORT_ID, 0xbd, 0x00, bVal > 0 ? (byte)0x01 : (byte)0x00]);
            Write([XGM_REPORT_ID, 0xba, 0xc5, 0xc4, bScale]);
        }

        public static void Light(bool status)
        {
            if (status)
            {
                int b = AppConfig.Get("xgm_brightness", 80);
                if (b <= 0) b = 80;
                Light(b);
            }
            else
            {
                Light(0);
            }
        }

        public static void LightBrightness(int brightness)
        {
            Task.Run(() =>
            {
                if (IsConnected())
                {
                    Light(brightness);
                }
            });
        }

        public static void LightMode(AuraMode mode, Color color, Color color2, int speed)
        {
            Task.Run(() =>
            {
                if (IsConnected())
                {
                    var msg = Aura.AuraMessage(mode, color, color2, speed);
                    msg[0] = XGM_REPORT_ID;
                    Write(msg);
                    Write([XGM_REPORT_ID, 0xb4]);
                    Write([XGM_REPORT_ID, 0xb5]);
                }
            });
        }

        public static void InitLight()
        {
            Task.Run(() =>
            {
                if (IsConnected()) Light(AppConfig.Is("xmg_light"));
            });
        }

        public static void NotifyShutdown()
        {
            if (IsConnected()) Write([XGM_REPORT_ID, 0xE4, 0x01]);
        }

        public static void Reset()
        {
            Task.Run(() =>
            {
                if (IsConnected()) Write([XGM_REPORT_ID, 0xd1, 0x02]);
            });
        }

        public static void SetFan(byte[] curve)
        {
            Task.Run(async () =>
            {
                if (AsusACPI.IsInvalidCurve(curve)) return;
                byte[] xgmPayload = new byte[16];
                for (int i = 0; i < 8; i++)
                {
                    xgmPayload[i] = curve[i]; // Temperatures first
                    xgmPayload[i + 8] = Math.Min((byte)100, curve[i + 8]); // Fan speeds second (0-100%)
                }
                if (IsConnected())
                {
                    // Exact Armoury Crate Mode Unlock Sequence:
                    // D0-01, D0-02, D0-03, D0-04 unlocks the firmware for custom fan curve control
                    Write([XGM_REPORT_ID, 0xd0, 0x01]);
                    await Task.Delay(10);
                    Write([XGM_REPORT_ID, 0xd0, 0x02]);
                    await Task.Delay(10);
                    Write([XGM_REPORT_ID, 0xd0, 0x03]);
                    await Task.Delay(10);
                    Write([XGM_REPORT_ID, 0xd0, 0x04]);
                    await Task.Delay(50);

                    // Send the 16-byte custom fan curve (8 temps + 8 speeds)
                    Write([XGM_REPORT_ID, 0xd1, 0x01, .. xgmPayload]);
                }
            });
        }

        public static async Task TestFan(int seconds = 8)
        {
            if (!IsConnected()) return;

            byte[] maxCurve = [20, 30, 40, 50, 60, 70, 80, 90, 95, 95, 95, 95, 95, 95, 95, 95];
            SetFan(maxCurve);

            await Task.Delay(TimeSpan.FromSeconds(seconds));

            if (AppConfig.IsApplyFans() || IsIndependentFanEnabled())
            {
                byte[] xgmCurve = AppConfig.GetFanConfig(AsusFan.XGM);
                if (AsusACPI.IsInvalidCurve(xgmCurve)) xgmCurve = AppConfig.GetDefaultCurve(AsusFan.XGM);
                SetFan(xgmCurve);
            }
            else
            {
                Reset();
            }
        }
    }
}
