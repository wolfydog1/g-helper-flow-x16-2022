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
        private static int _savedGameMiniled = -1;
        private static bool _savedWifiLowLatency = false;
        private static int _lastGamePid = 0;
        private static int _nonGameStreak = 0;

        private static bool _penDrawingActive = false;
        private static TouchPenMode _savedTouchPenMode = TouchPenMode.TouchAndPen;
        private static int _nonDrawingStreak = 0;

        private static bool _creatorModeActive = false;
        private static int _savedCreatorMiniled = -1;
        private static int _nonCreatorStreak = 0;

        private static readonly HashSet<string> DrawingAndNoteApps = new(StringComparer.OrdinalIgnoreCase)
        {
            "ONENOTE", "ONENOTEIM", "Drawboard.PDF", "DrawboardPDF", "Drawboard",
            "Journal", "Microsoft.Journal", "xournalpp", "krita", "Photoshop",
            "Illustrator", "CLIPStudioPaint", "Concepts", "Rebelle", "sai2",
            "MediBangPaintPro", "Goodnotes", "Whiteboard", "Microsoft.Whiteboard", "ZBrush"
        };

        private static readonly HashSet<string> CreatorStudioApps = new(StringComparer.OrdinalIgnoreCase)
        {
            "Photoshop", "Illustrator", "AfterFX", "Premiere", "Adobe Premiere Pro",
            "Lightroom", "LightroomClassic", "Resolve", "DaVinciResolve", "blender",
            "krita", "CLIPStudioPaint", "ZBrush", "Cinema 4D", "UnrealEditor", "SubstancePainter"
        };

        // Indie & 2D games that run effortlessly at 60-165 FPS in Silent mode with a 35W CPU cap and near-0 RPM fans
        public static readonly HashSet<string> IndieAnd2DGames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Hades", "Hades2", "Balatro", "Hollow Knight", "Hollow_Knight", "Silksong",
            "Celeste", "Stardew Valley", "StardewValley", "Terraria", "SlayTheSpire",
            "DeadCells", "Factorio", "Cuphead", "isaac-ng", "BindingOfIsaac",
            "VampireSurvivors", "Undertale", "Deltarune", "Ori", "OriAndTheWillOfTheWisps",
            "Brotato", "DaveTheDiver", "IntoTheBreach", "FTLGame", "ShovelKnight",
            "Spelunky2", "AnimalWell", "Blasphemous", "Katana ZERO", "NineSols"
        };

        private static Dictionary<string, string>? _cachedCustomRules;
        private static string _cachedCustomRulesRaw = "\0";
        private static int _lastCheckedPid = 0;
        private static string _lastCheckedProcName = "";

        private static IReadOnlyDictionary<string, string> GetCustomAppRulesInternal()
        {
            string raw = AppConfig.GetString("custom_app_rules", "") ?? "";
            if (_cachedCustomRules != null && string.Equals(raw, _cachedCustomRulesRaw, StringComparison.Ordinal))
                return _cachedCustomRules;

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string entry in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    int eq = entry.IndexOf('=');
                    if (eq > 0 && eq < entry.Length - 1)
                    {
                        string key = Path.GetFileNameWithoutExtension(entry.Substring(0, eq).Trim());
                        string val = entry.Substring(eq + 1).Trim();
                        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(val))
                            dict[key] = val;
                    }
                }
            }
            _cachedCustomRulesRaw = raw;
            _cachedCustomRules = dict;
            return dict;
        }

        public static Dictionary<string, string> GetCustomAppRules() =>
            new Dictionary<string, string>(GetCustomAppRulesInternal(), StringComparer.OrdinalIgnoreCase);

        public static void SaveCustomAppRules(Dictionary<string, string> rules)
        {
            var sb = new StringBuilder();
            foreach (var kv in rules)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(kv.Key).Append('=').Append(kv.Value);
            }
            string raw = sb.ToString();
            _cachedCustomRulesRaw = raw;
            _cachedCustomRules = new Dictionary<string, string>(rules, StringComparer.OrdinalIgnoreCase);
            AppConfig.Set("custom_app_rules", raw);
        }

        public static void SetCustomAppRule(string procName, string rule)
        {
            string clean = Path.GetFileNameWithoutExtension(procName.Trim());
            if (string.IsNullOrEmpty(clean)) return;
            var rules = GetCustomAppRules();
            rules[clean] = rule;
            SaveCustomAppRules(rules);
            if (_timer != null) _timer.Enabled = ShouldTimerRun();
        }

        public static void RemoveCustomAppRule(string procName)
        {
            string clean = Path.GetFileNameWithoutExtension(procName.Trim());
            var rules = GetCustomAppRules();
            if (rules.Remove(clean))
                SaveCustomAppRules(rules);
        }

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
            "obs64", "obs32", "blender", "Photoshop", "Illustrator", "AfterFX", "Premiere", "Adobe Premiere Pro",
            "Lightroom", "LightroomClassic", "Resolve", "DaVinciResolve", "audacity",
            "WINWORD", "EXCEL", "POWERPNT", "Acrobat", "AcroRd32",
            "ONENOTE", "ONENOTEIM", "Drawboard.PDF", "DrawboardPDF", "Drawboard",
            "Journal", "Microsoft.Journal", "xournalpp", "krita", "CLIPStudioPaint",
            "Concepts", "Rebelle", "sai2", "MediBangPaintPro", "Goodnotes", "Whiteboard", "Microsoft.Whiteboard", "ZBrush"
        };

        private static bool ShouldTimerRun() =>
            AppConfig.Is("auto_game_mode") ||
            AppConfig.Is("auto_pen_drawing_apps") ||
            AppConfig.Is("auto_creator_apps") ||
            !string.IsNullOrWhiteSpace(AppConfig.GetString("custom_app_rules", ""));

        public static void Init()
        {
            if (_timer != null)
            {
                _timer.Enabled = ShouldTimerRun();
                return;
            }

            _timer = new System.Timers.Timer(2500);
            _timer.Elapsed += (s, e) => CheckForeground();
            _timer.AutoReset = true;
            _timer.Enabled = ShouldTimerRun();
        }

        public static void Toggle(bool enable)
        {
            AppConfig.Set("auto_game_mode", enable ? 1 : 0);
            if (_timer != null) _timer.Enabled = ShouldTimerRun();
            else if (ShouldTimerRun()) Init();

            if (!enable && _gameModeActive && _savedMode >= 0)
            {
                RevertMode();
            }
        }

        public static void TogglePenDrawingApps(bool enable)
        {
            AppConfig.Set("auto_pen_drawing_apps", enable ? 1 : 0);
            if (_timer != null) _timer.Enabled = ShouldTimerRun();
            else if (ShouldTimerRun()) Init();

            if (!enable && _penDrawingActive)
            {
                RevertPenDrawingMode();
            }
        }

        public static void ToggleCreatorApps(bool enable)
        {
            AppConfig.Set("auto_creator_apps", enable ? 1 : 0);
            if (_timer != null) _timer.Enabled = ShouldTimerRun();
            else if (ShouldTimerRun()) Init();

            if (!enable && _creatorModeActive)
            {
                RevertCreatorMode();
            }
        }

        public static bool IsEnabled() => AppConfig.Is("auto_game_mode");
        public static bool IsPenDrawingAppsEnabled() => AppConfig.Is("auto_pen_drawing_apps");
        public static bool IsCreatorAppsEnabled() => AppConfig.Is("auto_creator_apps");

        private static int _isChecking = 0;

        private static void RevertMode()
        {
            if (!_gameModeActive) return;

            int revertMode = _savedMode >= 0 ? _savedMode : AsusACPI.PerformanceBalanced;
            int revertMiniled = _savedGameMiniled;
            bool restoreWifi = _savedWifiLowLatency;
            Logger.WriteLine($"AutoGameMode: Reverting to mode {revertMode}.");
            _gameModeActive = false;
            _savedMode = -1;
            _savedGameMiniled = -1;
            _lastGamePid = 0;

            if (AppConfig.Is("auto_wifi_gaming") && !restoreWifi)
            {
                WifiLatencyHelper.SetLowLatency(false, false);
            }

            if (Program.settingsForm != null && !Program.settingsForm.IsDisposed && Program.settingsForm.IsHandleCreated)
            {
                Program.settingsForm.BeginInvoke(delegate
                {
                    Program.modeControl.SetPerformanceMode(revertMode);
                    if (revertMiniled >= 0)
                    {
                        AppConfig.Set("miniled", revertMiniled);
                        ScreenControl.SetMiniled(revertMiniled);
                    }
                    ScreenControl.AutoScreen();
                    Program.toast?.RunToast("Game Mode: Reverted to " + Modes.GetName(revertMode));
                });
            }
        }

        private static void RevertPenDrawingMode()
        {
            if (!_penDrawingActive) return;
            _penDrawingActive = false;
            var restoreMode = _savedTouchPenMode;
            Logger.WriteLine($"AutoPenDrawing: Restoring touch mode to {restoreMode}.");

            TouchscreenHelper.SetMode(restoreMode);
            if (Program.settingsForm != null && !Program.settingsForm.IsDisposed && Program.settingsForm.IsHandleCreated)
            {
                Program.settingsForm.BeginInvoke(delegate
                {
                    Program.settingsForm.SyncTouchPenMode(restoreMode);
                    Program.toast?.RunToast("Touch + Pen Restored", ToastIcon.Touchpad);
                });
            }
        }

        private static void RevertCreatorMode()
        {
            if (!_creatorModeActive) return;
            _creatorModeActive = false;
            int restoreMiniled = _savedCreatorMiniled;
            _savedCreatorMiniled = -1;
            Logger.WriteLine($"AutoCreatorApps: Restoring Mini-LED mode {restoreMiniled}.");

            if (Program.settingsForm != null && !Program.settingsForm.IsDisposed && Program.settingsForm.IsHandleCreated)
            {
                Program.settingsForm.BeginInvoke(delegate
                {
                    if (restoreMiniled >= 0)
                    {
                        AppConfig.Set("miniled", restoreMiniled);
                        ScreenControl.SetMiniled(restoreMiniled);
                    }
                    ScreenControl.InitScreen();
                });
            }
        }

        private static void CheckForeground()
        {
            if (!ShouldTimerRun()) return;
            if (Interlocked.CompareExchange(ref _isChecking, 1, 0) != 0) return;

            try
            {
                // Never activate or switch performance modes when workstation is locked
                if (Aura.sessionLock)
                {
                    if (_gameModeActive) RevertMode();
                    if (_penDrawingActive) RevertPenDrawingMode();
                    if (_creatorModeActive) RevertCreatorMode();
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

                string procName;
                if (pid == _lastCheckedPid && !string.IsNullOrEmpty(_lastCheckedProcName))
                {
                    procName = _lastCheckedProcName;
                }
                else
                {
                    try
                    {
                        using var p = Process.GetProcessById(pid);
                        procName = p.ProcessName;
                        _lastCheckedPid = pid;
                        _lastCheckedProcName = procName;
                    }
                    catch { return; }
                }

                if (string.IsNullOrEmpty(procName)) return;

                // 1. Check Auto Pen-Only Mode for Drawing & Note-Taking Apps
                if (AppConfig.Is("auto_pen_drawing_apps"))
                {
                    bool isDrawingApp = DrawingAndNoteApps.Contains(procName) ||
                                        windowTitle.Contains("OneNote", StringComparison.OrdinalIgnoreCase) ||
                                        windowTitle.Contains("Drawboard", StringComparison.OrdinalIgnoreCase);

                    if (isDrawingApp)
                    {
                        _nonDrawingStreak = 0;
                        if (!_penDrawingActive)
                        {
                            var currentTouch = TouchscreenHelper.GetMode();
                            if (currentTouch == TouchPenMode.TouchAndPen)
                            {
                                _savedTouchPenMode = currentTouch;
                                _penDrawingActive = true;
                                Logger.WriteLine($"AutoPenDrawing: Detected '{procName}' ({windowTitle}). Switching to PenOnly mode.");
                                TouchscreenHelper.SetMode(TouchPenMode.PenOnly);
                                if (Program.settingsForm != null && !Program.settingsForm.IsDisposed && Program.settingsForm.IsHandleCreated)
                                {
                                    Program.settingsForm.BeginInvoke(delegate
                                    {
                                        Program.settingsForm.SyncTouchPenMode(TouchPenMode.PenOnly);
                                        Program.toast?.RunToast($"Pen-Only Mode: {procName} (Palm Rejection)", ToastIcon.Touchpad);
                                    });
                                }
                            }
                        }
                    }
                    else if (_penDrawingActive)
                    {
                        if (++_nonDrawingStreak >= 3)
                        {
                            RevertPenDrawingMode();
                        }
                    }
                }

                // 2. Check Auto Creator Studio Profile (Multi-Zone Mini-LED + Max Hz on AC)
                if (AppConfig.Is("auto_creator_apps"))
                {
                    bool isCreatorApp = CreatorStudioApps.Contains(procName);
                    if (isCreatorApp)
                    {
                        _nonCreatorStreak = 0;
                        if (!_creatorModeActive && SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online)
                        {
                            _savedCreatorMiniled = AppConfig.Get("miniled", 1);
                            _creatorModeActive = true;
                            Logger.WriteLine($"AutoCreatorApps: Detected '{procName}'. Activating 512-Zone Mini-LED + Max Refresh.");
                            if (Program.settingsForm != null && !Program.settingsForm.IsDisposed && Program.settingsForm.IsHandleCreated)
                            {
                                Program.settingsForm.BeginInvoke(delegate
                                {
                                    if (_savedCreatorMiniled == 0)
                                    {
                                        AppConfig.Set("miniled", 1);
                                        ScreenControl.SetMiniled(1);
                                        ScreenControl.InitScreen();
                                    }
                                    ScreenControl.SetScreen(frequency: ScreenControl.MAX_REFRESH, overdrive: 1);
                                    Program.toast?.RunToast($"Creator Profile: {procName} (512Z Mini-LED)", ToastIcon.BrightnessUp);
                                });
                            }
                        }
                    }
                    else if (_creatorModeActive)
                    {
                        if (++_nonCreatorStreak >= 4)
                        {
                            RevertCreatorMode();
                        }
                    }
                }

                // 3. Check Auto Game Mode & Per-App Custom Rules Engine
                var customRules = GetCustomAppRulesInternal();
                bool hasCustomRule = customRules.TryGetValue(procName, out string? customRule);
                if (hasCustomRule && string.Equals(customRule, "ignore", StringComparison.OrdinalIgnoreCase))
                {
                    if (_gameModeActive && ++_nonGameStreak >= 3) RevertMode();
                    return;
                }

                if (AppConfig.Is("auto_game_mode") || hasCustomRule)
                {
                    bool isIndie = (hasCustomRule && customRule == "silent_35w") ||
                                   (!hasCustomRule && AppConfig.IsNotFalse("auto_indie_silent") && IndieAnd2DGames.Contains(procName));
                    bool isCustomBalanced = hasCustomRule && customRule == "balanced_512z";
                    bool isGame = hasCustomRule || !ExcludedProcesses.Contains(procName);

                    if (isGame)
                    {
                        _nonGameStreak = 0;
                        if (!_gameModeActive)
                        {
                            _savedMode = Modes.GetCurrent();
                            _savedGameMiniled = AppConfig.Get("miniled", 1);
                            _savedWifiLowLatency = WifiLatencyHelper.IsLowLatencyActive();
                            _lastGamePid = pid;
                            _gameModeActive = true;

                            int targetMode = isIndie ? AsusACPI.PerformanceSilent
                                           : isCustomBalanced ? AsusACPI.PerformanceBalanced
                                           : AsusACPI.PerformanceTurbo;
                            string profileLabel = isIndie ? "Silent • 35W Indie Cap"
                                                : isCustomBalanced ? "Balanced • 512Z"
                                                : "Turbo • 512Z HDR";

                            Logger.WriteLine($"AutoGameMode: Detected '{procName}' (PID {pid}, Title: '{windowTitle}'). Activating {profileLabel}.");

                            if (AppConfig.Is("auto_wifi_gaming") && !_savedWifiLowLatency)
                            {
                                WifiLatencyHelper.SetLowLatency(true, false);
                            }

                            // Automatically trim standby memory to optimize physical RAM headroom for the incoming game
                            Task.Run(() => MemoryCleaner.TrimWorkingSet(pid));

                            if (Program.settingsForm != null && !Program.settingsForm.IsDisposed && Program.settingsForm.IsHandleCreated)
                            {
                                Program.settingsForm.BeginInvoke(delegate
                                {
                                    Program.modeControl.SetPerformanceMode(targetMode);
                                    if (isIndie)
                                    {
                                        // Cap APU SPL/SPPT to 35W for cool, near-0 RPM silent indie gaming
                                        Program.acpi.DeviceSet(AsusACPI.PPT_APUA0, 35, "IndieCapA0");
                                        Program.acpi.DeviceSet(AsusACPI.PPT_APUA3, 35, "IndieCapA3");
                                    }
                                    else if (SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online)
                                    {
                                        if (AppConfig.IsNotFalse("auto_game_miniled") && _savedGameMiniled == 0)
                                        {
                                            AppConfig.Set("miniled", 1);
                                            ScreenControl.SetMiniled(1);
                                        }
                                        ScreenControl.SetScreen(frequency: ScreenControl.MAX_REFRESH, overdrive: 1);
                                    }
                                    Program.toast?.RunToast($"Game Profile: {procName} ({profileLabel})");
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
