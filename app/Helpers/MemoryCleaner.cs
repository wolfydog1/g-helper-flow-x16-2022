using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GHelper.Helpers
{
    public static class MemoryCleaner
    {
        [DllImport("psapi.dll", SetLastError = true)]
        private static extern int EmptyWorkingSet(IntPtr hwProc);

        private static int _isCleaning = 0;

        /// <summary>
        /// Trims working sets of inactive background applications to free physical RAM and reduce memory paging stutter.
        /// </summary>
        /// <param name="foregroundPid">Optional PID of foreground game/app to preserve.</param>
        /// <returns>Estimated MB of physical RAM freed.</returns>
        public static long TrimWorkingSet(int foregroundPid = 0)
        {
            if (Interlocked.CompareExchange(ref _isCleaning, 1, 0) != 0) return 0;

            long freedBytes = 0;
            int currentPid = Environment.ProcessId;

            try
            {
                Process[] processes = Process.GetProcesses();
                foreach (var p in processes)
                {
                    try
                    {
                        if (p.Id <= 4 || p.Id == currentPid || (foregroundPid > 0 && p.Id == foregroundPid))
                        {
                            p.Dispose();
                            continue;
                        }

                        string name = p.ProcessName;
                        if (name.Equals("csrss", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("dwm", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("system", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("Registry", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("smss", StringComparison.OrdinalIgnoreCase))
                        {
                            p.Dispose();
                            continue;
                        }

                        long before = p.WorkingSet64;
                        if (before < 10 * 1024 * 1024) // Skip tiny processes (< 10MB) to minimize context switching overhead
                        {
                            p.Dispose();
                            continue;
                        }

                        if (EmptyWorkingSet(p.Handle) != 0)
                        {
                            p.Refresh();
                            long after = p.WorkingSet64;
                            if (before > after) freedBytes += (before - after);
                        }
                    }
                    catch
                    {
                        // Ignore access denied on protected services
                    }
                    finally
                    {
                        p.Dispose();
                    }
                }

                GC.Collect(2, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, true, true);

                long freedMB = freedBytes / (1024 * 1024);
                Logger.WriteLine($"MemoryCleaner: Trimmed working sets. Freed ~{freedMB} MB RAM.");
                return freedMB;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("MemoryCleaner error: " + ex.Message);
                return 0;
            }
            finally
            {
                Interlocked.Exchange(ref _isCleaning, 0);
            }
        }
    }
}
