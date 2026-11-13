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

        private static HidDevice? _cachedDevice;
        private static long _lastDeviceCheck = 0;

        public static HidDevice? GetDevice()
        {
            try
            {
                if (_cachedDevice != null) return _cachedDevice;

                if (Environment.TickCount64 - _lastDeviceCheck < 1500)
                    return null;

                _lastDeviceCheck = Environment.TickCount64;

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
                        if (device.GetReportDescriptor().TryGetReport(ReportType.Feature, XGM_REPORT_ID, out _))
                        {
                            _cachedDevice = device;
                            return device;
                        }
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
                            if (device.GetReportDescriptor().TryGetReport(ReportType.Feature, XGM_REPORT_ID, out _))
                            {
                                _cachedDevice = device;
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

        public static void InvalidateCache()
        {
            _cachedDevice = null;
            _lastDeviceCheck = 0;
        }

        public static bool IsConnected()
        {
            return GetDevice() is not null;
        }

        private static readonly object _writeLock = new();

        public static void Write(byte[] data)
        {
            lock (_writeLock)
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
                    _cachedDevice = null;
                }
            }
        }

        public static int? LastFanRPM { get; private set; } = null;
        public static int? LastTargetRPM { get; private set; } = null;
        public static int? LastHotspotTemp { get; private set; } = null;
        public static int? LastCoreTemp { get; private set; } = null;
        public static bool? LastCurveActive { get; private set; } = null;
        public static bool IsManualSpeedActive { get; private set; } = false;
        public static int? ManualSpeedPercent { get; private set; } = null;

        public static void ClearManualSpeed()
        {
            IsManualSpeedActive = false;
            ManualSpeedPercent = null;
        }

        public static bool IsIndependentFanEnabled() => AppConfig.Get("xgm_independent_fan", 1) == 1;
        public static bool AlwaysShowFanCurve() => AppConfig.Is("show_xgm_fan");

        private static bool _isProbing = false;

        public static void ProbeSync()
        {
            try
            {
                HidDevice? device = GetDevice();
                if (device is null) return;

                lock (_writeLock)
                {
                    using (HidStream hidStream = device.Open())
                    {
                        int reportLen = device.GetMaxFeatureReportLength();
                        byte[] req = new byte[reportLen];
                        req[0] = XGM_REPORT_ID;
                        req[1] = 0xC0;
                        req[2] = 0x01;
                        req[3] = 0x01;
                        hidStream.SetFeature(req);
                        Thread.Sleep(50);

                        byte[] response = new byte[reportLen];
                        response[0] = XGM_REPORT_ID;
                        hidStream.GetFeature(response);

                        if (response[1] == 0xC0)
                        {
                            int rpm = response[12] + (response[13] << 8);
                            int targetRpm = response[14] + (response[15] << 8);
                            if (rpm >= 0 && rpm < 15000) LastFanRPM = rpm;
                            if (targetRpm >= 0 && targetRpm < 15000) LastTargetRPM = targetRpm;
                            int b6 = response[6];
                            if (b6 > 0 && b6 < 125)
                            {
                                LastCoreTemp = b6;
                            }
                            else if (response[18] == 0x80 && response[9] >= 20 && response[9] <= 115)
                            {
                                LastCoreTemp = response[9];
                            }
                            else
                            {
                                LastCoreTemp = null;
                            }

                            // Bytes 8 and 9 form a 16-bit big-endian temperature in tenths of a degree when byte 8 > 0 (e.g. 0x01AC = 428 => 42.8°C)
                            // When byte 8 == 0, byte 9 is either a 1-byte °C value (20..115) or a sentinel (e.g. 0xAC)
                            int rawT9 = (response[8] << 8) | response[9];
                            if (response[8] > 0 && rawT9 >= 200 && rawT9 <= 1150)
                            {
                                LastHotspotTemp = (int)Math.Round(rawT9 / 10.0);
                            }
                            else
                            {
                                int b9 = response[9];
                                LastHotspotTemp = (response[8] == 0 && b9 >= 20 && b9 <= 115 && LastCoreTemp != b9) ? b9 : null;
                            }

                            LastCurveActive = (response[18] == 0x80);
                            Logger.WriteLine($"XGM Telemetry: RPM={rpm}, Target={targetRpm}, Core={LastCoreTemp}C, Hot={LastHotspotTemp}C, active=0x{response[18]:X2} | {BitConverter.ToString(response, 0, Math.Min(24, response.Length))}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"XGM Probe Error: {ex.Message}");
                _cachedDevice = null;
            }
        }

        public static void Probe()
        {
            if (_isProbing) return;
            _isProbing = true;

            Task.Run(() =>
            {
                try
                {
                    ProbeSync();
                }
                finally
                {
                    _isProbing = false;
                }
            });
        }

        private static void HandshakeSync()
        {
            byte[] init = Encoding.ASCII.GetBytes("^ASUS Tech.Inc.\0");
            init[0] = XGM_REPORT_ID;
            Write(init);
            Write(init);
            Write([XGM_REPORT_ID, 0xCE, 0x01]);
            Write([XGM_REPORT_ID, 0xCE, 0x03]);
            Write([XGM_REPORT_ID, 0xCE, 0x03]);
            Write([XGM_REPORT_ID, 0xCE, 0x02]);
            Write([XGM_REPORT_ID, 0xD0, 0x01]);
            Write([XGM_REPORT_ID, 0xD0, 0x02]);
            Write([XGM_REPORT_ID, 0xD0, 0x03]);
            Write([XGM_REPORT_ID, 0xD0, 0x04]);
        }

        public static void Init()
        {
            Task.Run(async () =>
            {
                InvalidateCache();
                for (int i = 0; i < 5; i++)
                {
                    if (IsConnected())
                    {
                        HandshakeSync();
                        Light(AppConfig.Is("xmg_light"));

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

        private static readonly object _fanLock = new();

        private static void ApplyCurvePayloadSync(byte[] temps, byte[] speeds)
        {
            byte[] xgmPayload = new byte[16];
            temps.CopyTo(xgmPayload, 0);
            speeds.CopyTo(xgmPayload, 8);

            Write([XGM_REPORT_ID, 0xd1, 0x01, .. xgmPayload]);
        }

        public static void Reset()
        {
            Task.Run(() =>
            {
                lock (_fanLock)
                {
                    if (IsConnected())
                    {
                        Write([XGM_REPORT_ID, 0xd1, 0x02]);
                        IsManualSpeedActive = false;
                        ManualSpeedPercent = null;
                        Thread.Sleep(50);
                        ProbeSync();
                    }
                }
            });
        }

        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        public static void SetFan(byte[] curve)
        {
            if (curve == null || curve.Length < 16) return;

            Task.Run(async () =>
            {
                await _semaphore.WaitAsync();
                try
                {
                    if (!IsConnected()) return;

                    // G-Helper curve format: [0..7: Speeds, 8..15: Temperatures]
                    // XG Mobile EC format:   [0..7: Temperatures, 8..15: Speeds]
                    byte[] payload = new byte[16];
                    Array.Copy(curve, 8, payload, 0, 8); // Temps -> payload[0..7]
                    Array.Copy(curve, 0, payload, 8, 8); // Speeds -> payload[8..15]

                    // Ensure strictly increasing temperatures
                    for (int i = 1; i < 8; i++)
                    {
                        if (payload[i] <= payload[i - 1])
                            payload[i] = (byte)(payload[i - 1] + 1);
                    }

                    List<byte> packet = new List<byte> { 0x5E, 0xD1, 0x01 };
                    packet.AddRange(payload);

                    Write(packet.ToArray());
                    Logger.WriteLine($"XGM: Fan curve applied: {BitConverter.ToString(packet.ToArray())}");
                    IsManualSpeedActive = false;
                    ManualSpeedPercent = null;
                    Thread.Sleep(50);
                    ProbeSync();
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"XGM SetFan error: {ex.Message}");
                }
                finally
                {
                    _semaphore.Release();
                }
            });
        }

        public static void SetManualSpeed(int percent)
        {
            byte speed = (byte)Math.Clamp(percent, 0, 100);
            byte[] temps = [30, 40, 50, 60, 70, 80, 90, 100];
            byte[] speeds = Enumerable.Repeat(speed, 8).ToArray();
            Logger.WriteLine($"XGM SetManualSpeed: {speed}%");
            Task.Run(() =>
            {
                lock (_fanLock)
                {
                    if (IsConnected())
                    {
                        ApplyCurvePayloadSync(temps, speeds);
                        IsManualSpeedActive = true;
                        ManualSpeedPercent = speed;
                        Thread.Sleep(50);
                        ProbeSync();
                    }
                }
            });
        }

        public static async Task TestFan(int seconds = 10, Action<int, int>? onProgress = null)
        {
            if (!IsConnected()) return;

            byte[] temps = [30, 40, 50, 60, 70, 80, 90, 100];
            byte[] speeds = Enumerable.Repeat((byte)85, 8).ToArray();
            lock (_fanLock)
            {
                ApplyCurvePayloadSync(temps, speeds);
                IsManualSpeedActive = true;
                ManualSpeedPercent = 85;
            }

            for (int i = 0; i < seconds; i++)
            {
                await Task.Delay(1000);
                ProbeSync();
                onProgress?.Invoke(LastFanRPM ?? 0, LastTargetRPM ?? 0);
            }

            IsManualSpeedActive = false;
            ManualSpeedPercent = null;

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
