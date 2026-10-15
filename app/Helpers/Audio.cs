using NAudio.CoreAudioApi;
using System.Runtime.InteropServices;

namespace GHelper.Helpers
{
    [ComImport]
    [Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicyConfig
    {
        int GetMixFormat(string pszDeviceName, IntPtr ppFormat);
        int GetDeviceFormat(string pszDeviceName, bool bDefault, IntPtr ppFormat);
        int ResetDeviceFormat(string pszDeviceName);
        int SetDeviceFormat(string pszDeviceName, IntPtr pEndpointFormat, IntPtr MixFormat);
        int GetProcessingPeriod(string pszDeviceName, bool bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);
        int SetProcessingPeriod(string pszDeviceName, IntPtr pmftPeriod);
        int GetShareMode(string pszDeviceName, IntPtr pMode);
        int SetShareMode(string pszDeviceName, IntPtr mode);
        int GetPropertyValue(string pszDeviceName, bool bFxStore, IntPtr key, IntPtr pv);
        int SetPropertyValue(string pszDeviceName, bool bFxStore, IntPtr key, IntPtr pv);
        int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, Role eRole);
        int SetEndpointVisibility(string pszDeviceName, bool bVisible);
    }

    [ComImport]
    [Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    internal class PolicyConfigClient
    {
    }

    internal class Audio
    {
        static AudioEndpointVolume? endpointVolume; // keeps the COM callback alive

        public static void SubscribeMute(Action<bool> onMuteChange)
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                endpointVolume = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).AudioEndpointVolume;
                bool muted = endpointVolume.Mute;
                onMuteChange(muted);
                endpointVolume.OnVolumeNotification += (data) => { if (data.Muted != muted) onMuteChange(muted = data.Muted); };
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Error subscribing to volume notifications: " + ex.Message);
            }
        }

        public static bool ToggleMicMute()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var commDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                var consoleDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
                var mmDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);

                bool status = !commDevice.AudioEndpointVolume.Mute;

                commDevice.AudioEndpointVolume.Mute = status;
                consoleDevice.AudioEndpointVolume.Mute = status;
                mmDevice.AudioEndpointVolume.Mute = status;

                if (Program.acpi != null && Program.acpi.IsSupported(AsusACPI.MicMuteLed))
                {
                    Program.acpi.DeviceSet(AsusACPI.MicMuteLed, status ? 1 : 0, "MicmuteLed");
                }

                Logger.WriteLine(commDevice.ToString() + ":" + status);
                return status;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Error toggling mic mute: " + ex.Message);
                return false;
            }
        }

        public static bool IsMicMuted()
        {
            try
            {
                using var deviceEnumerator = new MMDeviceEnumerator();
                var commDevice = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                return commDevice.AudioEndpointVolume.Mute;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Error checking mic mute status: " + ex.Message);
                return false;
            }
        }

        public static bool IsMuted()
        {
            try
            {
                using var deviceEnumerator = new MMDeviceEnumerator();
                var defaultDevice = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                return defaultDevice.AudioEndpointVolume.Mute;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Error checking mute status: " + ex.Message);
                return false;
            }
        }

        public static string? GetDefaultOutputName()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var defaultDev = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                return defaultDev?.FriendlyName;
            }
            catch
            {
                return null;
            }
        }

        public static string CycleAudioOutput()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
                if (devices.Count == 0) return "No Audio Output";
                if (devices.Count == 1) return devices[0].FriendlyName;

                string currentId = "";
                try
                {
                    var currentDev = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    currentId = currentDev.ID;
                }
                catch { }

                int currentIndex = devices.FindIndex(d => d.ID == currentId);
                int nextIndex = (currentIndex + 1) % devices.Count;
                var nextDevice = devices[nextIndex];

                var policyConfig = (IPolicyConfig)new PolicyConfigClient();
                policyConfig.SetDefaultEndpoint(nextDevice.ID, Role.Console);
                policyConfig.SetDefaultEndpoint(nextDevice.ID, Role.Multimedia);
                policyConfig.SetDefaultEndpoint(nextDevice.ID, Role.Communications);

                Logger.WriteLine($"Switched Default Audio Output to: {nextDevice.FriendlyName}");
                return nextDevice.FriendlyName;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Error cycling audio output: " + ex.Message);
                return "Audio Output Error";
            }
        }
    }
}
