using GHelper.Mode;
using GHelper.USB;

namespace GHelper.Fan
{
    public class FanSensorControl
    {
        public const int DEFAULT_FAN_MIN = 18;
        public const int DEFAULT_FAN_MAX = 58;

        public const int XGM_FAN_MAX = 76;

        public const int INADEQUATE_MAX = 104;

        const int FAN_COUNT = 3;

        Fans fansForm;
        ModeControl modeControl = Program.modeControl;

        static int[] measuredMax;
        static int sameCount = 0;

        static System.Timers.Timer timer = default!;

        static int[] _fanMax = InitFanMax();
        static int[] _fanMin = GetDefaultMin();
        static bool _fanRpm = AppConfig.IsNotFalse("fan_rpm");

        public FanSensorControl(Fans fansForm)
        {
            this.fansForm = fansForm;
            bool calibrating = timer is not null && timer.Enabled;
            timer?.Dispose();
            timer = new System.Timers.Timer(1000);
            timer.Elapsed += Timer_Elapsed;
            timer.Enabled = calibrating;
        }

        static int[] InitFanMax()
        {
            int[] defaultMax = GetDefaultMax();

            return new int[4] {
                AppConfig.Get("fan_max_" + (int)AsusFan.CPU, defaultMax[(int)AsusFan.CPU]),
                AppConfig.Get("fan_max_" + (int)AsusFan.GPU, defaultMax[(int)AsusFan.GPU]),
                AppConfig.Get("fan_max_" + (int)AsusFan.Mid, defaultMax[(int)AsusFan.Mid]),
                AppConfig.Get("fan_max_" + (int)AsusFan.XGM, XGM_FAN_MAX)
            };
        }


        static int[] GetDefaultMax()
        {
            if (AppConfig.ContainsModel("GA401I")) return new int[3] { 78, 76, DEFAULT_FAN_MAX };
            if (AppConfig.ContainsModel("GA401")) return new int[3] { 71, 73, DEFAULT_FAN_MAX };
            if (AppConfig.ContainsModel("GA402")) return new int[3] { 55, 56, DEFAULT_FAN_MAX };

            if (AppConfig.ContainsModel("G513R")) return new int[3] { 58, 60, DEFAULT_FAN_MAX };
            if (AppConfig.ContainsModel("G513Q")) return new int[3] { 69, 69, DEFAULT_FAN_MAX };
            if (AppConfig.ContainsModel("GA503")) return new int[3] { 64, 64, DEFAULT_FAN_MAX };

            if (AppConfig.ContainsModel("GU603")) return new int[3] { 62, 64, DEFAULT_FAN_MAX };

            if (AppConfig.ContainsModel("FA507R")) return new int[3] { 63, 57, DEFAULT_FAN_MAX };
            if (AppConfig.ContainsModel("FA507X")) return new int[3] { 63, 68, DEFAULT_FAN_MAX };

            if (AppConfig.ContainsModel("FX607J")) return new int[3] { 74, 72, DEFAULT_FAN_MAX };

            if (AppConfig.ContainsModel("GX650")) return new int[3] { 62, 62, DEFAULT_FAN_MAX };

            if (AppConfig.ContainsModel("G732")) return new int[3] { 61, 60, DEFAULT_FAN_MAX };
            if (AppConfig.ContainsModel("G713")) return new int[3] { 56, 60, DEFAULT_FAN_MAX };

            if (AppConfig.ContainsModel("Z301")) return new int[3] { 72, 64, DEFAULT_FAN_MAX };

            if (AppConfig.ContainsModel("GV601")) return new int[3] { 78, 59, 85 };

            if (AppConfig.ContainsModel("GA403")) return new int[3] { 68, 68, 80 };
            if (AppConfig.ContainsModel("GU605")) return new int[3] { 62, 62, 92 };

            return new int[3] { DEFAULT_FAN_MAX, DEFAULT_FAN_MAX, DEFAULT_FAN_MAX };
        }

        static int[] GetDefaultMin()
        {
            if (AppConfig.ContainsModel("GA403")) return new int[3] { 22, 22, 22 };
            if (AppConfig.ContainsModel("GU605")) return new int[3] { 22, 22, 22 };
            if (AppConfig.ContainsModel("HN7306")) return new int[3] { 22, 22, 22 };
            return new int[3] { DEFAULT_FAN_MIN, DEFAULT_FAN_MIN, DEFAULT_FAN_MIN };
        }

        public static int GetFanMax(AsusFan device)
        {
            int devIndex = (int)device;
            if (devIndex >= _fanMax.Length) return device == AsusFan.XGM ? XGM_FAN_MAX : DEFAULT_FAN_MAX;

            if (_fanMax[devIndex] < 0 || _fanMax[devIndex] > INADEQUATE_MAX)
                SetFanMax(device, device == AsusFan.XGM ? XGM_FAN_MAX : DEFAULT_FAN_MAX);

            return _fanMax[devIndex];
        }

        public static int GetFanMin(AsusFan device)
        {
            if (device == AsusFan.XGM) return DEFAULT_FAN_MIN;
            int devIndex = (int)device;
            if (devIndex >= _fanMin.Length) return DEFAULT_FAN_MIN;
            return _fanMin[devIndex];
        }

        public static void SetFanMax(AsusFan device, int value)
        {
            int devIndex = (int)device;
            if (devIndex < _fanMax.Length) _fanMax[devIndex] = value;
            AppConfig.Set("fan_max_" + (int)device, value);
        }

        public static bool fanRpm
        {
            get
            {
                return _fanRpm;
            }
            set
            {
                AppConfig.Set("fan_rpm", value ? 1 : 0);
                _fanRpm = value;
            }
        }

        public static string FormatFan(AsusFan device, int value)
        {
            if (value < 0) return null;

            if (value > GetFanMax(device) && value <= INADEQUATE_MAX) SetFanMax(device, value);

            if (fanRpm)
                return (value * 100).ToString() + "RPM";
            else
                return Math.Min(Math.Round((float)value / GetFanMax(device) * 100), 100).ToString() + "%"; // relatively to max RPM
        }

        public static (string? formatted, int? rpm) GetXgmFan(float? temp)
        {
            if (temp is null || temp <= 0) return (null, null);

            byte[] curve = AppConfig.GetFanConfig(AsusFan.XGM);
            if (AsusACPI.IsInvalidCurve(curve))
                curve = AppConfig.GetDefaultCurve(AsusFan.XGM);

            if (curve.Length != 16) return (null, null);

            float t = (float)temp;
            float percent;

            if (t <= curve[0])
            {
                percent = curve[8];
            }
            else if (t >= curve[7])
            {
                percent = curve[15];
            }
            else
            {
                percent = curve[15];
                for (int i = 0; i < 7; i++)
                {
                    if (t >= curve[i] && t <= curve[i + 1])
                    {
                        float span = curve[i + 1] - curve[i];
                        if (span <= 0)
                            percent = curve[8 + i];
                        else
                        {
                            float factor = (t - curve[i]) / span;
                            percent = curve[8 + i] + factor * (curve[8 + i + 1] - curve[8 + i]);
                        }
                        break;
                    }
                }
            }

            int roundedPercent = Math.Clamp((int)Math.Round(percent), 0, 100);
            int min = GetFanMin(AsusFan.XGM);
            int max = GetFanMax(AsusFan.XGM);
            int calculatedRpm = (int)(200 * Math.Floor((float)(min * 100 + (max - min) * roundedPercent) / 200));

            if (roundedPercent == 0)
                return (fanRpm ? "0RPM" : "0%", 0);

            if (t < 50 && roundedPercent <= 20)
                return (fanRpm ? "0RPM (0dB)" : "0% (0dB)", 0);

            string formatted = fanRpm ? $"{calculatedRpm}RPM" : $"{roundedPercent}%";
            return (formatted, calculatedRpm);
        }

        static int previousXgmMax = XGM_FAN_MAX;

        public void StartCalibration()
        {

            measuredMax = new int[] { 0, 0, 0, 0 };
            timer.Enabled = true;

            previousXgmMax = AppConfig.Get("fan_max_" + (int)AsusFan.XGM, XGM_FAN_MAX);
            if (previousXgmMax <= 36) previousXgmMax = XGM_FAN_MAX;

            for (int i = 0; i < FAN_COUNT; i++)
                AppConfig.Remove("fan_max_" + i);

            Program.acpi.DeviceSet(AsusACPI.PerformanceMode, AppConfig.IsManualModeRequired() ? AsusACPI.PerformanceManual : AsusACPI.PerformanceTurbo, "ModeCalibration");

            for (int i = 0; i < FAN_COUNT; i++)
                Program.acpi.SetFanCurve((AsusFan)i, new byte[] { 20, 30, 40, 50, 60, 70, 80, 90, 100, 100, 100, 100, 100, 100, 100, 100 });

            if (Program.acpi.IsXGConnected() || XGM.IsConnected())
            {
                XGM.SetFan(new byte[] { 20, 25, 30, 35, 40, 45, 50, 55, 100, 100, 100, 100, 100, 100, 100, 100 });
            }

        }

        private void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            int fan;
            bool same = true;

            for (int i = 0; i < FAN_COUNT; i++)
            {
                fan = Program.acpi.GetFan((AsusFan)i);
                if (fan > measuredMax[i])
                {
                    measuredMax[i] = fan;
                    same = false;
                }
            }

            if (Program.acpi.IsXGConnected() || XGM.IsConnected())
            {
                XGM.ProbeSync();
                int xgmRPM = XGM.LastFanRPM ?? HardwareControl.xgmFanRPM ?? 0;
                if (xgmRPM > 0)
                {
                    HardwareControl.xgmFanRPM = xgmRPM;
                    HardwareControl.xgmFan = $"{xgmRPM}RPM";
                }
                int xgmFan = xgmRPM / 100;
                if (xgmFan > measuredMax[3])
                {
                    measuredMax[3] = xgmFan;
                    same = false;
                }
            }

            if (same) sameCount++;
            else sameCount = 0;

            string label = "Measuring Max Speed - CPU: " + measuredMax[(int)AsusFan.CPU] * 100 + ", GPU: " + measuredMax[(int)AsusFan.GPU] * 100;
            if (measuredMax[(int)AsusFan.Mid] > 10) label = label + ", Mid: " + measuredMax[(int)AsusFan.Mid] * 100;
            if (Program.acpi.IsXGConnected() || XGM.IsConnected()) label = label + ", XGM: " + measuredMax[3] * 100;
            label = label + " (" + sameCount + "s)";

            fansForm.LabelFansResult(label);

            if (sameCount >= 15)
            {
                for (int i = 0; i < FAN_COUNT; i++)
                {
                    if (measuredMax[i] > 30 && measuredMax[i] < INADEQUATE_MAX) SetFanMax((AsusFan)i, measuredMax[i]);
                }

                if (Program.acpi.IsXGConnected() || XGM.IsConnected())
                {
                    // The XG Mobile fan can physically reach 5500-7200 RPM under thermal load, but the onboard
                    // MCU firmware limits idle target speed to ~3400-3600 RPM. Only store measuredMax if it
                    // reached true high RPM (>= 5000 RPM). Otherwise, preserve the full XGM_FAN_MAX rating (7200 RPM).
                    if (measuredMax[3] >= 50 && measuredMax[3] < INADEQUATE_MAX)
                        SetFanMax(AsusFan.XGM, measuredMax[3]);
                    else
                        SetFanMax(AsusFan.XGM, Math.Max(previousXgmMax, XGM_FAN_MAX));
                }

                sameCount = 0;
                FinishCalibration();
            }

        }

        public void StopCalibration()
        {
            if (timer is not null && timer.Enabled)
            {
                timer.Enabled = false;
                sameCount = 0;
                modeControl.SetPerformanceMode();
                if (Program.acpi.IsXGConnected() || XGM.IsConnected())
                {
                    if (AppConfig.IsApplyFans())
                    {
                        byte[] xgmCurve = AppConfig.GetFanConfig(AsusFan.XGM);
                        if (AsusACPI.IsInvalidCurve(xgmCurve)) xgmCurve = AppConfig.GetDefaultCurve(AsusFan.XGM);
                        XGM.SetFan(xgmCurve);
                    }
                    else
                    {
                        XGM.Reset();
                    }
                }
            }
        }

        private void FinishCalibration()
        {

            timer.Enabled = false;
            modeControl.SetPerformanceMode();

            string label = "Measured - CPU: " + AppConfig.Get("fan_max_" + (int)AsusFan.CPU) * 100;

            if (AppConfig.Get("fan_max_" + (int)AsusFan.GPU) > 0)
                label = label + ", GPU: " + AppConfig.Get("fan_max_" + (int)AsusFan.GPU) * 100;

            if (AppConfig.Get("fan_max_" + (int)AsusFan.Mid) > 0)
                label = label + ", Mid: " + AppConfig.Get("fan_max_" + (int)AsusFan.Mid) * 100;

            if (Program.acpi.IsXGConnected() || XGM.IsConnected())
            {
                int currentXgmMax = GetFanMax(AsusFan.XGM) * 100;
                if (measuredMax[3] > 0 && measuredMax[3] < 50)
                    label = label + $", XGM: {currentXgmMax} (idle: {measuredMax[3] * 100})";
                else
                    label = label + ", XGM: " + currentXgmMax;

                if (AppConfig.IsApplyFans())
                {
                    byte[] xgmCurve = AppConfig.GetFanConfig(AsusFan.XGM);
                    if (AsusACPI.IsInvalidCurve(xgmCurve)) xgmCurve = AppConfig.GetDefaultCurve(AsusFan.XGM);
                    XGM.SetFan(xgmCurve);
                }
                else
                {
                    XGM.Reset();
                }
            }

            fansForm.LabelFansResult(label);
            fansForm.InitAxis();
        }
    }
}
