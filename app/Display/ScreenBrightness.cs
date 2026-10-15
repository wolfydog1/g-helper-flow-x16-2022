namespace GHelper.Display
{
    using System;
    using System.Diagnostics;
    using System.Management;

    public static class ScreenBrightness
    {
        public static int Get()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightness");
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
                                if (val >= 0 && val <= 100) AppConfig.Set("screen_brightness", val);
                                return val;
                            }
                        }
                        catch { }
                    }
                }

                if (fallbackVal >= 0)
                {
                    AppConfig.Set("screen_brightness", fallbackVal);
                    return fallbackVal;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("ScreenBrightness.Get error: " + ex.Message);
            }

            return -1;
        }

        public static void Set(int brightness)
        {
            brightness = Math.Clamp(brightness, 0, 100);
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightnessMethods");
                using var instances = searcher.Get();
                var args = new object[] { 1, brightness };
                foreach (ManagementObject instance in instances)
                {
                    using (instance)
                    {
                        try
                        {
                            instance.InvokeMethod("WmiSetBrightness", args);
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
            int brightness = Get();
            if (brightness < 0) brightness = AppConfig.Get("screen_brightness", 100);
            Debug.WriteLine(brightness);
            brightness = Math.Min(100, Math.Max(0, brightness + delta));
            Set(brightness);
            return brightness;
        }

    }
}
