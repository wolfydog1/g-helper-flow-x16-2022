using System.Runtime.InteropServices;

public static class NvmlHelper
{
    const string NvmlDll = "nvml.dll";

    [DllImport(NvmlDll)] static extern int nvmlInit_v2();
    [DllImport(NvmlDll)] static extern int nvmlShutdown();
    [DllImport(NvmlDll)] static extern int nvmlDeviceGetCount_v2(out uint count);
    [DllImport(NvmlDll)] static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);
    [DllImport(NvmlDll)] static extern int nvmlDeviceGetTemperature(IntPtr device, uint sensorType, out uint temp);
    [DllImport(NvmlDll)] static extern int nvmlDeviceGetPowerUsage(IntPtr device, out uint powerMilliWatts);
    [DllImport(NvmlDll)] static extern int nvmlDeviceGetMemoryInfo(IntPtr device, out nvmlMemory_t memory);
    [DllImport(NvmlDll)] static extern int nvmlDeviceGetClockInfo(IntPtr device, uint type, out uint clockMhz);

    [StructLayout(LayoutKind.Sequential)]
    struct nvmlMemory_t { public ulong total; public ulong free; public ulong used; }

    const uint NVML_TEMPERATURE_GPU = 0;
    const uint NVML_CLOCK_GRAPHICS = 0;
    const uint NVML_CLOCK_MEM = 2;
    const int NVML_SUCCESS = 0;
    const int NVML_ERROR_UNINITIALIZED = 1;
    const int NVML_ERROR_GPU_IS_LOST = 15;

    private static readonly object _lock = new();
    private static bool _init = false;

    public static void Init()
    {
        if (_init) return;
        try
        {
            int rc = nvmlInit_v2();
            _init = rc == NVML_SUCCESS;
            Logger.WriteLine($"NVML Init: {rc}");
        }
        catch (Exception e)
        {
            Logger.WriteLine($"NVML Init exception: {e.Message}");
            _init = false;
        }
    }

    public static int? GetTemperature(uint gpuIndex = 0)
    {
        lock (_lock)
        {
            Init();
            if (!_init) return null;
            try
            {
                if (nvmlDeviceGetCount_v2(out uint count) != NVML_SUCCESS || count == 0 || gpuIndex >= count) return null;
                if (nvmlDeviceGetHandleByIndex_v2(gpuIndex, out IntPtr device) != NVML_SUCCESS || device == IntPtr.Zero) return null;
                if (nvmlDeviceGetTemperature(device, NVML_TEMPERATURE_GPU, out uint temp) != NVML_SUCCESS) return null;
                return (int)temp;
            }
            catch { return null; }
        }
    }

    public static float? GetGpuPower(uint gpuIndex = 0)
    {
        lock (_lock)
        {
            Init();
            if (!_init) return null;
            try
            {
                if (nvmlDeviceGetCount_v2(out uint count) != NVML_SUCCESS || count == 0 || gpuIndex >= count) return null;
                if (nvmlDeviceGetHandleByIndex_v2(gpuIndex, out IntPtr device) != NVML_SUCCESS || device == IntPtr.Zero) return null;
                int rc = nvmlDeviceGetPowerUsage(device, out uint mW);
                if (rc == NVML_ERROR_UNINITIALIZED || rc == NVML_ERROR_GPU_IS_LOST)
                {
                    _init = false;
                    return null;
                }
                if (rc != NVML_SUCCESS || mW > 250_000f) return null;
                return mW / 1000f;
            }
            catch { return null; }
        }
    }

    public static (long usedMb, long totalMb)? GetMemoryInfo(uint gpuIndex = 0)
    {
        lock (_lock)
        {
            Init();
            if (!_init) return null;
            try
            {
                if (nvmlDeviceGetCount_v2(out uint count) != NVML_SUCCESS || count == 0 || gpuIndex >= count) return null;
                if (nvmlDeviceGetHandleByIndex_v2(gpuIndex, out IntPtr device) != NVML_SUCCESS || device == IntPtr.Zero) return null;
                if (nvmlDeviceGetMemoryInfo(device, out nvmlMemory_t mem) != NVML_SUCCESS) return null;
                if (mem.total == 0) return null;
                const ulong MB = 1024 * 1024;
                return ((long)(mem.used / MB), (long)(mem.total / MB));
            }
            catch { return null; }
        }
    }

    public static (int coreMhz, int memMhz)? GetClocks(uint gpuIndex = 0)
    {
        lock (_lock)
        {
            Init();
            if (!_init) return null;
            try
            {
                if (nvmlDeviceGetCount_v2(out uint count) != NVML_SUCCESS || count == 0 || gpuIndex >= count) return null;
                if (nvmlDeviceGetHandleByIndex_v2(gpuIndex, out IntPtr device) != NVML_SUCCESS || device == IntPtr.Zero) return null;
                if (nvmlDeviceGetClockInfo(device, NVML_CLOCK_GRAPHICS, out uint core) != NVML_SUCCESS) return null;
                nvmlDeviceGetClockInfo(device, NVML_CLOCK_MEM, out uint mem);
                return ((int)core, (int)mem);
            }
            catch { return null; }
        }
    }

    public static void Shutdown()
    {
        lock (_lock)
        {
            if (!_init) return;
            try
            {
                int rc = nvmlShutdown();
                Logger.WriteLine($"NVML Shutdown: {rc}");
            }
            catch { }
            _init = false;
        }
    }
}
