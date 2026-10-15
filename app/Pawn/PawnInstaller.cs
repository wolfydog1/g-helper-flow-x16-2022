using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace PawnIO
{
    public static class PawnInstaller
    {
        private const string InstallerUrl = "https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe";

        public static async Task<bool> InstallAsync(Action<string>? statusCallback = null)
        {
            if (RyzenSmuService.IsPawnInstalled())
            {
                statusCallback?.Invoke("PawnIO already installed.");
                return true;
            }

            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "GHelperPawnIO");
                Directory.CreateDirectory(tempDir);
                string setupPath = Path.Combine(tempDir, "PawnIO_setup.exe");

                string wingetCached = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Temp\WinGet\namazso.PawnIO.2.2.0\PawnIO_setup.exe"
                );

                if (File.Exists(wingetCached))
                {
                    statusCallback?.Invoke("Using cached PawnIO installer...");
                    File.Copy(wingetCached, setupPath, true);
                }
                else if (!File.Exists(setupPath) || new FileInfo(setupPath).Length < 1000000)
                {
                    statusCallback?.Invoke("Downloading PawnIO driver installer...");
                    Logger.WriteLine("Downloading PawnIO driver from " + InstallerUrl);
                    using var client = new HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(30);
                    using var response = await client.GetAsync(InstallerUrl);
                    response.EnsureSuccessStatusCode();
                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(setupPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await stream.CopyToAsync(fileStream);
                }

                statusCallback?.Invoke("Installing PawnIO driver silently...");
                Logger.WriteLine("Running PawnIO installer: " + setupPath);

                var psi = new ProcessStartInfo(setupPath, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    await proc.WaitForExitAsync();
                    Logger.WriteLine($"PawnIO installer finished with exit code {proc.ExitCode}");
                }

                // Give the Windows driver service manager a brief moment to finish registration
                await Task.Delay(1500);

                bool installed = RyzenSmuService.IsPawnInstalled();
                Logger.WriteLine($"PawnIO driver installation verified: {installed}");
                statusCallback?.Invoke(installed ? "PawnIO driver installed successfully." : "Installation failed.");
                return installed;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("PawnIO installer exception: " + ex);
                statusCallback?.Invoke("Error: " + ex.Message);
                return false;
            }
        }
    }
}
