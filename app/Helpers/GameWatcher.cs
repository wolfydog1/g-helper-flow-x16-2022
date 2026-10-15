using GHelper.Display;
using GHelper.Mode;
using GHelper.USB;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GHelper.Helpers
{
    public static class GameWatcher
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private static System.Timers.Timer? _timer;
        private static bool _gameModeActive = false;
        private static int _savedMode = -1;
        private static int _lastGamePid = 0;
        private static int _nonGameStreak = 0;

        // Common desktop applications, Windows system hosts, media players, and store launchers that should never trigger game mode
        private static readonly HashSet<string> ExcludedProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "explorer", "ShellExperienceHost", "SearchApp", "StartMenuExperienceHost", "ApplicationFrameHost",
            "LockApp", "ScreenClippingHost", "SearchHost", "SystemSettings", "TextInputHost", "CredentialUIBroker", "OpenWith",
            "SecHealthUI", "SecurityHealthSystray", "smartscreen", "dwm", "csrss", "sihost", "fontdrvhost", "ctfmon",
            "SearchIndexer", "audiodg", "svchost", "WmiPrvSE", "dllhost", "sppsvc", "Taskmgr", "SnippingTool", "CalculatorApp",
            "chrome", "msedge", "firefox", "opera", "brave", "vivaldi", "iexplore", "chromium", "librewolf", "arc", "waterfox",
            "WindowsTerminal", "conhost", "cmd", "powershell", "pwsh", "alacritty", "wezterm-gui",
            "discord", "slack", "Teams", "ms-teams", "Spotify", "WhatsApp", "Signal", "Telegram", "Code", "Notion", "obsidian", "zoom",
            "notepad", "notepad++", "sublime_text", "devenv", "rider64", "idea64", "pycharm64",
            "GHelper", "Antigravity", "steam", "steamwebhelper", "EpicGamesLauncher", "Origin", "EADesktop", "GalaxyClient", "Battle.net",
            "RiotClientServices", "UbisoftConnect", "upc",
            "vlc", "mpc-hc", "mpc-hc64", "mpc-be", "mpc-be64", "mpv", "wmplayer", "Video.UI", "foobar2000",
            "obs64", "obs32", "blender", "Photoshop", "Illustrator", "AfterFX", "Premiere", "audacity",
            "WINWORD", "EXCEL", "POWERPNT", "Acrobat", "AcroRd32"
        };

        public static void Init()
        {
            if (_timer != null) return;

            _timer = new System.Timers.Timer(2500);
            _timer.Elapsed += (s, e) => CheckForeground();
            _timer.AutoReset = true;
            _timer.Enabled = AppConfig.Is("auto_game_mode");
        }

        public static void Toggle(bool enable)
        {
            AppConfig.Set("auto_game_mode", enable ? 1 : 0);
            if (_timer != null) _timer.Enabled = enable;
            else if (enable) Init();

            if (!enable && _gameModeActive && _savedMode >= 0)
            {
                RevertMode();
            }
        }

        public static bool IsEnabled() => AppConfig.Is("auto_game_mode");

        private static int _isChecking = 0;

        private static void RevertMode()
        {
            if (!_gameModeActive) return;

            int revertMode = _savedMode >= 0 ? _savedMode : AsusACPI.PerformanceBalanced;
            Logger.WriteLine($"AutoGameMode: Reverting to mode {revertMode}.");
            _gameModeActive = false;
            _savedMode = -1;
            _lastGamePid = 0;

            if (Program.settingsForm != null && !Program.settingsForm.IsDisposed && Program.settingsForm.IsHandleCreated)
            {
                Program.settingsForm.BeginInvoke(delegate
                {
                    Program.modeControl.SetPerformanceMode(revertMode);
                    ScreenControl.AutoScreen();
                    Program.toast?.RunToast("Game Mode: Reverted to " + Modes.GetName(revertMode));
                });
            }
        }

        private static void CheckForeground()
        {
            if (!AppConfig.Is("auto_game_mode")) return;
            if (Interlocked.CompareExchange(ref _isChecking, 1, 0) != 0) return;

            try
            {
                // Never activate or switch performance modes when workstation is locked
                if (Aura.sessionLock)
                {
                    if (_gameModeActive)
                    {
                        RevertMode();
                    }
                    return;
                }

                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return;

                int titleLen = GetWindowTextLength(hwnd);
                if (titleLen == 0) return; // Ignore invisible background helper and headless windows

                StringBuilder title = new StringBuilder(titleLen + 1);
                GetWindowText(hwnd, title, title.Capacity);
                string windowTitle = title.ToString().Trim();
                if (string.Equals(windowTitle, "Program Manager", StringComparison.OrdinalIgnoreCase)) return;

                GetWindowThreadProcessId(hwnd, out uint pidRaw);
                int pid = (int)pidRaw;
                if (pid == 0 || pid == Environment.ProcessId) return;

                string procName = "";
                try
                {
                    using var p = Process.GetProcessById(pid);
                    procName = p.ProcessName;
                }
                catch { return; }

                if (string.IsNullOrEmpty(procName)) return;

                bool isGame = !ExcludedProcesses.Contains(procName);

                if (isGame)
                {
                    _nonGameStreak = 0;
                    if (!_gameModeActive)
                    {
                        _savedMode = Modes.GetCurrent();
                        _lastGamePid = pid;
                        _gameModeActive = true;
                        Logger.WriteLine($"AutoGameMode: Detected game '{procName}' (PID {pid}, Title: '{windowTitle}'). Activating Turbo.");

                        // Automatically trim standby memory to optimize physical RAM headroom for the incoming game
                        Task.Run(() => MemoryCleaner.TrimWorkingSet(pid));

                        if (Program.settingsForm != null && !Program.settingsForm.IsDisposed && Program.settingsForm.IsHandleCreated)
                        {
                            Program.settingsForm.BeginInvoke(delegate
                            {
                                Program.modeControl.SetPerformanceMode(AsusACPI.PerformanceTurbo);
                                if (SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online)
                                {
                                    ScreenControl.SetScreen(frequency: ScreenControl.MAX_REFRESH, overdrive: 1);
                                }
                                Program.toast?.RunToast($"Game Mode: {procName} (Turbo)");
                            });
                        }
                    }
                }
                else if (_gameModeActive)
                {
                    // Require 3 consecutive non-game checks (~7.5s) before reverting to allow brief alt-tabbing
                    if (++_nonGameStreak >= 3)
                    {
                        RevertMode();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("GameWatcher error: " + ex.Message);
            }
            finally
            {
                Interlocked.Exchange(ref _isChecking, 0);
            }
        }
    }
}
