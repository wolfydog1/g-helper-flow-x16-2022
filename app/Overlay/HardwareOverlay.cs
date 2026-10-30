using GHelper.Display;
using GHelper.Helpers;
using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace GHelper.Overlay
{
    public enum OverlayCorner
    {
        TopLeft = 0,
        TopRight = 1,
        BottomLeft = 2,
        BottomRight = 3
    }

    public class HardwareOverlay : OSDNativeForm
    {
        // Foreground window — used to pin FPS measurement to the active process
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        // Foreground-change subscription (alt-tab detection).
        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);
        private delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

        // Drag support
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        private static extern bool SetCapture(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
        private const uint GW_HWNDPREV = 3;

        private const int GWL_EXSTYLE        = -20;
        private const int WS_EX_TRANSPARENT_FLAG = 0x00000020;
        private const int WM_LBUTTONDOWN     = 0x0201;
        private const int WM_MOUSEMOVE       = 0x0200;
        private const int WM_LBUTTONUP       = 0x0202;
        private const int WM_MOUSEWHEEL      = 0x020A;
        private const int WM_MBUTTONDOWN     = 0x0207;
        private const int VK_CONTROL         = 0x11;
        private const int VK_SHIFT           = 0x10;
        private const int VK_MENU            = 0x12; // ALT

        private const int MinScalePercent  = 35;
        private const int MaxScalePercent  = 300;
        private const int ScaleStepPercent = 10;
        private int _scalePercent = 100;

        // Fixed base scale, deliberately independent of Windows DPI. 2.0 reproduces
        // the size the overlay had at 200% display scaling — the desired default.
        private const float BaseScale = 2.0f;

        private bool _dragging;
        private Point _dragCursorStart;
        private Point _dragWindowStart;
        private bool _dragModeActive;
        private volatile bool _dragKey;
        public enum OverlayMode { Default = 0, Light = 1, Full = 2, Complete = 3, Bar = 4 }
        private OverlayMode _mode;

        // ── Layout constants (base = 96 dpi) ─────────────────────────────────
        //
        // Light:    fps | temp                        | power
        // Default:  fps | temp + fan RPM              | chart | power | bat% + rate
        // Full:     fps | temp + fan RPM              | chart | power | usage% | bar | bat% + rate
        // Complete: fps | name | temp + fan RPM       | chart | power | usage% | bar | mem GB | bar | bat% + rate
        //
        // The battery column only appears while the device runs on battery (always on Ally).
        //
        // Click on the overlay cycles Light → Default → Full → Complete → Light.
        //
        // Bar height is fixed per DPI (~BaseUsageBarHeight * sc) and cell pitch is
        // integer, so the number of cells varies with available pixels while every
        // cell stays a clean integer height.
        //
        private const float BaseFontSize = 13f;
        private const float BaseRpmFontSize = 8.5f;
        private const int BaseLineHeight = 18;
        private const int BaseLineSpacing = 1;
        private const int BasePadX = 8;
        private const int BasePadY = 4;
        private const int BaseFpsColWidth = 52;
        private const int BaseLeftColWidth = 128;
        private const int BaseChartColWidth = 120;
        private const int BasePowerGap = 4;
        private const int BasePowerColWidth = 46; // fits "120.9W" (6 chars, F1 + "W") right-aligned, no extra slack
        private const int BaseColGap = 8;
        private const int CornerRadius = 3;
        private const int MarginFromEdge = 10;
        private const int BaseLightLeftColWidth = 64; // fits "GPU: 82° " (9 Consolas chars); trailing space is the gap to the power column
        private const int BaseUsageBarGap = 11;       // gap between the power W and the usage % column (full mode)
        private const int BaseUsageBarWidth = 5;
        private const int BaseUsageNumGap = 4;        // gap between the usage % text and its bar
        private const int BaseUsageNumColWidth = 30;  // right-aligned column fitting "100%"
        private const int BaseFullPadRight = 4;       // tighter right margin in full mode (vs BasePadX)
        // Target bar height at base DPI; tuned so at 2x DPI numCells = 10.
        private const int BaseUsageBarHeight = 15;
        private const int BaseUsageBarYNudge = 1; // raise bars to align with the text baseline
        private const int BaseNameColWidth = 90;     // fits "Core Ultra 9" / "Ryzen AI 9"
        private const int BaseMemBarGap = 8;
        private const int BaseMemNumColWidth = 54;   // fits "127.9GB"
        private const int BaseBatColGap = 8;
        private const int BaseBatColWidth = 38;      // fits "-22.4"
        private const int BaseBatIconGap = 2;
        private const int BaseBatIconWidth = 8;
        private const int BaseBatIconHeight = 15;
        private const int BaseBatNubWidth = 4;
        private const int BaseBatNubHeight = 2;
        private const int BaseLightWidth = BasePadX + BaseFpsColWidth + BaseColGap + BaseLightLeftColWidth + BasePowerGap + BasePowerColWidth + BasePadX;
        private const int BaseWidth = BasePadX + BaseFpsColWidth + BaseColGap + BaseLeftColWidth + BaseColGap + BaseChartColWidth + BasePowerGap + BasePowerColWidth + BasePadX;
        private const int BaseFullWidth = BaseWidth - BasePadX + BaseUsageBarGap + BaseUsageBarWidth + BaseUsageNumGap + BaseUsageNumColWidth + BaseFullPadRight;
        private const int BaseCompleteWidth = BaseFullWidth + BaseMemBarGap + BaseMemNumColWidth + BaseUsageNumGap + BaseUsageBarWidth;
        private const int BaseBarWidth = 320;

        private static readonly Color DefaultGpuColor = Color.FromArgb(255, 0, 255, 80);
        private static readonly Color DefaultCpuColor = Color.FromArgb(255, 60, 220, 255);
        private static readonly Color DefaultMidColor = Color.FromArgb(255, 195, 145, 255);
        private static readonly Color DefaultXgmColor = Color.FromArgb(255, 255, 120, 90);
        private static readonly SolidBrush _batBrush = new(Color.FromArgb(255, 235, 235, 235));
        private static readonly SolidBrush _batDimBrush = new(Color.FromArgb(128, 78, 78, 78));
        private static readonly SolidBrush _shadowBrush = new(Color.FromArgb(190, 0, 0, 0));
        private static readonly SolidBrush _jitterGreenBrush = new(Color.FromArgb(255, 0, 255, 120));
        private static readonly SolidBrush _jitterYellowBrush = new(Color.FromArgb(255, 255, 200, 50));
        private static readonly SolidBrush _jitterRedBrush = new(Color.FromArgb(255, 255, 70, 70));
        private static readonly SolidBrush _badgeBrush = new(Color.FromArgb(255, 255, 215, 0));

        // Minimum background alpha while dragging, so a near-transparent box stays grabbable
        // (a layered window ignores mouse hits on fully transparent pixels).
        private const int DragMinAlpha = 110;
        private static readonly SolidBrush _dragBgBrush = new(Color.FromArgb(DragMinAlpha, 0, 0, 0));
        private int _bgAlpha = 128;

        private SolidBrush _bgBrush = new(Color.FromArgb(128, 0, 0, 0));
        private SolidBrush _gpuBrush = new(DefaultGpuColor);
        private SolidBrush _cpuBrush = new(DefaultCpuColor);
        private SolidBrush _midBrush = new(DefaultMidColor);
        private SolidBrush _xgmBrush = new(DefaultXgmColor);
        private SolidBrush _xgmFillBrush = new(Color.FromArgb(128, 85, 35, 20));
        private Pen _gpuLinePen = new(DefaultGpuColor, 1.5f);
        private Pen _cpuLinePen = new(DefaultCpuColor, 1.5f);
        private SolidBrush _gpuFillBrush = new(Color.FromArgb(128, 0, 85, 27));
        private SolidBrush _cpuFillBrush = new(Color.FromArgb(128, 20, 73, 85));

        // Cached drawing resources  recreated only when the scale changes
        private float _lastScale = 0f;
        private string? _lastFontName = null;
        private float _charW;
        private Font? _font;
        private Font? _rpmFont;
        private Font? _fpsBold;
        private Pen? _totalPen;
        private Pen? _axPen;
        private Pen? _borderPen;

        // Pre-allocated chart point arrays — reused every repaint to avoid GC pressure
        private readonly PointF[] _basePts = new PointF[HistoryLength];
        private readonly PointF[] _cpuPts  = new PointF[HistoryLength];
        private readonly PointF[] _gpuPts  = new PointF[HistoryLength];
        private readonly PointF[] _polyPts = new PointF[HistoryLength * 2];

        // _gpuTempStr includes a trailing space so fan number has breathing room
        private string _gpuTempStr = "";
        private string _cpuTempStr = "";
        private string _gpuFanNum = "";
        private string _cpuFanNum = "";
        private string _midTempStr = "";
        private string _midFanNum = "";
        private string _xgmTempStr = "";
        private string _xgmFanNum = "";
        private string _gpuPow = "";
        private string _cpuPow = "";
        private string _xgmPow = "";
        private int? _gpuUsage;
        private int? _cpuUsage;
        private int? _xgmUsage;
        private int? _vramUsage;
        private int? _ramUsage;
        private int? _vramUsedMb;
        private int? _ramUsedMb;
        private string _gpuShortName = "";
        private string _cpuShortName = "";
        private string _xgmShortName = "";
        private string _batLevel = "";
        private string _batRate = "";
        private int _batPercent;
        private bool _batCharging;

        private static readonly PointF[] _boltShape = { new(0.6f, 0f), new(0.15f, 0.55f), new(0.45f, 0.55f), new(0.35f, 1f), new(0.85f, 0.45f), new(0.5f, 0.45f) };
        private static readonly PointF[] _boltPts = new PointF[6];
        private static readonly SolidBrush _batBoltBrush = new(Color.FromArgb(255, 25, 25, 25));

        private const int HistoryLength = 60;
        private readonly float[] _cpuHistory = new float[HistoryLength];
        private readonly float[] _gpuHistory = new float[HistoryLength];
        private int _historyHead = 0;

        private readonly System.Timers.Timer _timer = new(1000) { AutoReset = true };
        private EtwFpsMonitor? _fps;
        private Task? _fpsTask;
        private volatile int _currentFps;
        private volatile int _current1PercentLowFps;
        private volatile float _currentJitterMs;
        private int _lastFgPid;
        private bool _active;
        private bool _gameOnly;
        private bool _showNames;
        private bool _showFps, _showTemp, _showFans, _showChart, _showPower, _showUsage, _showRam, _showBattery, _show1PercentLow, _showShadow, _showJitter, _showHdrBadge, _showFrametime, _showClock, _showVram, _showMidFan;
        private bool _onBattery;
        private int _overlayBattery = -1;
        private static readonly bool _isAlly = AppConfig.IsAlly();
        private bool _hidden;
        private int _shownPid;
        private bool _fgDesktop;
        private IntPtr _fgHook;
        private WinEventProc? _fgHookProc; // keep delegate alive
        private const int MinGameFps = 6;
        private int _gameTicks;

        private static readonly HashSet<string> DesktopApps = new(StringComparer.OrdinalIgnoreCase)
        {
            "chrome", "msedge", "firefox", "opera", "brave", "vivaldi", "iexplore", "chromium", "librewolf", "arc", "waterfox", "thorium",
            "WindowsTerminal", "conhost", "cmd", "powershell", "pwsh", "alacritty", "wezterm-gui", "mintty",
            "discord", "slack", "Teams", "ms-teams", "Spotify", "WhatsApp", "Signal", "Telegram", "Code", "Notion", "obsidian", "zoom", "Skype", "Element", "Viber", "LINE", "WeChat",
            "notepad", "notepad++", "sublime_text", "devenv", "rider64", "idea64", "pycharm64", "webstorm64",
            "steam", "steamwebhelper", "EpicGamesLauncher", "Battle.net", "GalaxyClient", "EADesktop", "UbisoftConnect",
            "vlc", "mpv", "mpc-hc64", "mpc-be64", "PotPlayerMini64", "wmplayer", "smplayer", "foobar2000", "aimp",
            "WINWORD", "EXCEL", "POWERPNT", "OUTLOOK", "Acrobat", "AcroRd32", "SumatraPDF", "thunderbird", "Mailspring", "OneNote", "GitHubDesktop", "7zFM", "WinRAR", "SnippingTool",
            "explorer", "ShellExperienceHost", "ShellHost", "SearchHost", "StartMenuExperienceHost", "ApplicationFrameHost", "SystemSettings", "Taskmgr",
            "PowerToys.PowerLauncher", "PowerToys.Settings", "PowerToys.ColorPickerUI", "PowerToys.FancyZonesEditor", "PowerToys.Peek.UI", "PowerToys.PowerOCR", "PowerToys.MeasureToolUI", "PowerToys.ImageResizer", "PowerToys.PowerRename", "PowerToys.AdvancedPaste", "Microsoft.CmdPal.UI",
        };

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int w, int h, uint flags);
        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;

        public HardwareOverlay()
        {
            Alpha = 255;
            _timer.Elapsed += (_, _) => Tick();
        }

        private const int WM_NCDESTROY = 0x0082;
        private const int WM_SETCURSOR  = 0x0020;
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCDESTROY)
            {
                base.WndProc(ref m);
                if (_active)
                    Program.settingsForm?.BeginInvoke(() => { RestorePosition(); base.Show(); });
                return;
            }

            if (m.Msg == WM_SETCURSOR)
            {
                Cursor.Current = _dragging ? Cursors.SizeAll : Cursors.Hand;
                m.Result = (IntPtr)1; // prevent DefWindowProc from overriding the cursor
                return;
            }

            if (m.Msg == WM_MOUSEWHEEL && AreDragKeysDown())
            {
                int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                ApplyScale(_scalePercent + (delta > 0 ? ScaleStepPercent : -ScaleStepPercent));
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == WM_MBUTTONDOWN && AreDragKeysDown())
            {
                ApplyScale(100);
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == WM_LBUTTONDOWN && _dragModeActive)
            {
                _dragging = true;
                _dragCursorStart = Cursor.Position;
                _dragWindowStart = Location;
                SetCapture(Handle);
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == WM_MOUSEMOVE && _dragging)
            {
                Point cursor = Cursor.Position;
                int newX = _dragWindowStart.X + cursor.X - _dragCursorStart.X;
                int newY = _dragWindowStart.Y + cursor.Y - _dragCursorStart.Y;
                Screen screen = Screen.FromPoint(cursor);
                const int margin = 5;
                newX = Math.Clamp(newX, screen.Bounds.Left + margin, screen.Bounds.Right  - Width  - margin);
                newY = Math.Clamp(newY, screen.Bounds.Top  + margin, screen.Bounds.Bottom - Height - margin);
                Location = new Point(newX, newY);
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == WM_LBUTTONUP && _dragging)
            {
                _dragging = false;
                ReleaseCapture();

                Point upCursor = Cursor.Position;
                bool wasClick = Math.Abs(upCursor.X - _dragCursorStart.X) <= 5 &&
                                Math.Abs(upCursor.Y - _dragCursorStart.Y) <= 5;
                if (wasClick)
                {
                    _mode = _mode switch
                    {
                        OverlayMode.Light    => OverlayMode.Default,
                        OverlayMode.Default  => OverlayMode.Full,
                        OverlayMode.Full     => OverlayMode.Complete,
                        OverlayMode.Complete => OverlayMode.Bar,
                        _                    => OverlayMode.Light,
                    };
                    AppConfig.Set("overlay_mode", (int)_mode);
                    RefreshSettings();
                }
                else
                {
                    SavePosition();
                }

                if (!_dragKey && !AreDragKeysDown())
                {
                    _dragModeActive = false;
                    SetTransparentStyle(true);
                }
                m.Result = IntPtr.Zero;
                return;
            }

            base.WndProc(ref m);
        }

        private float GetScale() => BaseScale * (_scalePercent / 100f);

        private int BaseModeWidth()
        {
            int w = _mode switch
            {
                OverlayMode.Light    => BaseLightWidth,
                OverlayMode.Full     => BaseFullWidth,
                OverlayMode.Complete => BaseCompleteWidth,
                OverlayMode.Bar      => BaseBarWidth,
                _                    => BaseWidth,
            };
            if (_showBattery && _onBattery) w += BaseBatColGap + BaseBatColWidth + BaseBatIconGap + BaseBatIconWidth;
            return _showNames ? w + BaseNameColWidth + BaseColGap : w;
        }

        private static int S(float sc, int v) => (int)(v * sc);
        private static double D(object? v) { try { return v is null ? 0.0 : Convert.ToDouble(v); } catch { return 0.0; } }

        private static string FmtTemp(double t) =>
        t > 0 ? ((int)Math.Round(t) + "°").PadLeft(4) : " -- ";

        private static string FmtPow(double p) =>
        p > 0 ? Math.Round(p, 1).ToString("F1") + "W" : "";

        // "NVIDIA GeForce RTX 4070 Laptop GPU" -> "RTX 4070", "AMD Radeon RX 6850M XT" -> "RX 6850M"
        private static string ShortGpuName(string? full)
        {
            if (string.IsNullOrEmpty(full)) return "";
            foreach (string tag in new[] { "RTX", "GTX", "RX", "Arc" })
            {
                int i = full.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
                if (i < 0) continue;
                string[] p = full[i..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string s = p.Length >= 2 ? p[0] + " " + p[1] : p[0];
                if (p.Length >= 3 && p[2] == "Ti") s += " Ti";
                return s;
            }
            return full;
        }

        // "...Core(TM) i9-13980HX" -> "i9-13980HX", "AMD Ryzen 9 7945HX..." -> "Ryzen 7945HX", "...Core(TM) Ultra 9 185H" -> "Ultra 185H"
        private static string ShortCpuName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            var m = Regex.Match(name, @"i[3579]-\w+");
            if (m.Success) return m.Value;

            m = Regex.Match(name, @"Ultra\s+\d+\s+(\w*\d\w*)");
            if (m.Success) return "Ultra " + m.Groups[1].Value;

            if (name.Contains("Ryzen", StringComparison.OrdinalIgnoreCase))
            {
                m = Regex.Match(name, @"(?:[A-Z]{2,}\s+)?\d{3,}\w*");
                return m.Success ? "Ryzen " + m.Value : "Ryzen";
            }

            return name.Split(' ', StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } t ? t[0] : "";
        }

        private static string FormatFan(int? fan)
        {
            if (fan is null || fan < 0) return "";
            return fan.ToString();
        }

        private void Tick()
        {
            // Drag-mode detection: only activate when the cursor is already over the
            // overlay, preventing CTRL+SHIFT+ALT used in games from toggling drag mode.
            bool mouseOver = new Rectangle(Location, Size).Contains(Cursor.Position);
            bool keysDown = _dragKey || (mouseOver && AreDragKeysDown());
            if (keysDown != _dragModeActive && !_dragging)
                ApplyDragMode(keysDown);

            if (Handle != nint.Zero && GetWindow(Handle, GW_HWNDPREV) != IntPtr.Zero)
                SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

            // Pin FPS counter to foreground process — queried every second so
            // switching games is handled automatically without manual configuration.
            GetWindowThreadProcessId(GetForegroundWindow(), out uint fgPidRaw);
            int fgPid = (int)fgPidRaw;
            bool ownWindow = fgPid == 0 || fgPid == Environment.ProcessId;

            if (_fps != null)
            {
                if (!ownWindow && fgPid != _lastFgPid)
                {
                    _lastFgPid = fgPid;
                    _currentFps = 0;
                    _current1PercentLowFps = 0;
                    _currentJitterMs = 0;
                    _fgDesktop = IsDesktopApp(fgPid);
                    _fps.TargetPid = _fgDesktop ? 0 : fgPid;
                }
                else
                {
                    _currentFps = (int)Math.Round(_fps.SampleFps());
                    if (_currentFps > 0)
                    {
                        if (_show1PercentLow)
                            _current1PercentLowFps = (int)Math.Round(_fps.Sample1PercentLowFps());
                        if (_showJitter)
                            _currentJitterMs = (float)_fps.SampleJitterMs();
                    }
                    else
                    {
                        _current1PercentLowFps = 0;
                        _currentJitterMs = 0;
                    }
                }
            }

            if (_gameOnly)
            {
                if (!ownWindow) UpdateGameVisibility(fgPid);
                if (_hidden) return;
            }

            HardwareControl.ReadSensorsOverlay();

            // gpuActive gates power, fan and chart history — when the GPU is disabled
            // its sensors return stale non-zero values, so we zero them out explicitly.
            double gpuTemp = D(HardwareControl.gpuTemp);
            double cpuTemp = D(HardwareControl.cpuTemp);
            bool gpuActive = gpuTemp > 0;

            _gpuTempStr = "GPU:" + FmtTemp(gpuTemp) + " ";
            _cpuTempStr = "CPU:" + FmtTemp(cpuTemp) + " ";
            _gpuFanNum = FormatFan(HardwareControl.gpuFanRPM);
            _cpuFanNum = FormatFan(HardwareControl.cpuFanRPM);

            bool hasMid = AppConfig.IsMidFan();
            if (hasMid)
            {
                _midTempStr = "MID:" + FmtTemp(HardwareControl.midTemp ?? 0f) + " ";
                _midFanNum = FormatFan(HardwareControl.midFanRPM);
            }
            else
            {
                _midTempStr = "";
                _midFanNum = "";
            }

            bool hasXgm = HardwareControl.IsXGConnectedCached() && (HardwareControl.xgmTemp is not null || HardwareControl.xgmFanRPM is not null);
            if (hasXgm)
            {
                double xgmTemp = D(HardwareControl.xgmTemp > 0 ? HardwareControl.xgmTemp : HardwareControl.xgmHotspotTemp);
                _xgmTempStr = "XGM:" + FmtTemp(xgmTemp) + " ";
                _xgmFanNum = FormatFan(HardwareControl.xgmFanRPM);
                _xgmShortName = "XG Mobile";

                float? xgmPowVal = HardwareControl.GetXGMPower();
                _xgmPow = xgmPowVal is null ? "" : Math.Round(xgmPowVal.Value, 1).ToString("F1") + "W";
                _xgmUsage = HardwareControl.GetXGMUsage();
            }
            else
            {
                _xgmTempStr = "";
                _xgmFanNum = "";
                _xgmShortName = "";
                _xgmPow = "";
                _xgmUsage = null;
            }

            _gpuPow = HardwareControl.gpuPower is null ? "" : Math.Round(HardwareControl.gpuPower.Value, 1).ToString("F1") + "W";
            _cpuPow = FmtPow(D(HardwareControl.cpuPower));

            _cpuHistory[_historyHead] = (float)Math.Max(0, D(HardwareControl.cpuPower));
            _gpuHistory[_historyHead] = gpuActive ? (float)Math.Max(0, D(HardwareControl.gpuPower)) : 0f;
            _historyHead = (_historyHead + 1) % HistoryLength;

            _cpuUsage = HardwareControl.cpuUsage;
            _gpuUsage = gpuActive ? (HardwareControl.gpuUsage ?? 0) : null;
            _vramUsage = gpuActive ? (HardwareControl.vramUsage ?? 0) : null;
            _vramUsedMb = gpuActive ? (HardwareControl.vramUsedMb ?? 0) : null;
            _ramUsage = HardwareControl.ramUsage;
            _ramUsedMb = HardwareControl.ramUsedMb;

            if (_showBattery)
            {
                bool onBattery = _isAlly || _overlayBattery == 1 || SystemInformation.PowerStatus.PowerLineStatus != PowerLineStatus.Online;
                if (onBattery != _onBattery)
                {
                    _onBattery = onBattery;
                    ApplySensorFlags();

                    Point center = new Point(Location.X + Width / 2, Location.Y + Height / 2);
                    Screen screen = Screen.FromPoint(center);
                    if (center.X > screen.Bounds.X + screen.Bounds.Width / 2)
                    {
                        int rightEdge = Location.X + Width;
                        Invalidate();
                        Location = new Point(rightEdge - Width, Location.Y);
                    }
                }

                if (_onBattery)
                {
                    double level = HardwareControl.GetBatteryChargePercentage();
                    _batPercent = (int)Math.Round(level);
                    _batLevel = level > 0 ? _batPercent + "%" : "--";
                    decimal rate = HardwareControl.batteryRate ?? 0;
                    _batRate = rate == 0 ? "" : (rate > 0 ? "+" : "-") + Math.Abs(rate).ToString("F1");
                    _batCharging = rate > 0;
                }
            }

            if (_showNames)
            {
                if (_cpuShortName.Length == 0)
                    _cpuShortName = ShortCpuName(PawnIO.CpuInfo.Name);
                if (_gpuShortName.Length == 0)
                    _gpuShortName = ShortGpuName(HardwareControl.GpuControl?.FullName);
            }

            Invalidate();
        }

        private void UpdateGameVisibility(int fgPid)
        {
            _gameTicks = _currentFps >= MinGameFps && !_fgDesktop ? _gameTicks + 1 : 0;
            if (_gameTicks >= 2) _shownPid = fgPid;
            bool show = fgPid == _shownPid;
            if (show != _hidden) return;
            _hidden = !show;
            if (Handle != nint.Zero)
                User32.ShowWindow(Handle, (short)(_hidden ? User32.SW_HIDE : User32.SW_SHOWNOACTIVATE));
        }

        // Instant show/hide vs the latched game. Doesn't latch _shownPid (timer's job —
        // _currentFps still reflects the old foreground here). UI thread, so no lock needed.
        private void OnForegroundChanged()
        {
            if (!_active || !_gameOnly) return;
            GetWindowThreadProcessId(GetForegroundWindow(), out uint fgPidRaw);
            int fgPid = (int)fgPidRaw;
            if (fgPid == 0 || fgPid == Environment.ProcessId) return;
            bool show = fgPid == _shownPid;
            if (show != _hidden) return;
            _hidden = !show;
            if (Handle != nint.Zero)
                User32.ShowWindow(Handle, (short)(_hidden ? User32.SW_HIDE : User32.SW_SHOWNOACTIVATE));
        }

        private static bool IsDesktopApp(int pid)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                return DesktopApps.Contains(p.ProcessName);
            }
            catch { return false; }
        }

        protected override void PerformPaint(PaintEventArgs e)
        {
            float sc = GetScale();

            int padX = S(sc, BasePadX);
            int padY = S(sc, BasePadY);
            int lineH = S(sc, BaseLineHeight);
            int lineGap = S(sc, BaseLineSpacing);
            int radius = S(sc, CornerRadius);
            int fpsColW = S(sc, BaseFpsColWidth);
            int chartColW = S(sc, BaseChartColWidth);
            int powColW = S(sc, BasePowerColWidth);
            int colGap = S(sc, BaseColGap);
            int powGap = S(sc, BasePowerGap);

            bool showFps = _showFps, showTemp = _showTemp, showFans = _showFans, showChart = _showChart;
            bool showPower = _showPower, showUsageMetric = _showUsage, showMem = _showRam, showNames = _showNames;
            bool showBat = _showBattery && _onBattery;
            bool showUsage = _showUsage || _showRam; // bar sizing is shared by the usage and ram bars

            bool showMidRow = _showMidFan && (_midTempStr.Length > 0 || _midFanNum.Length > 0) && (showTemp || showFans);
            bool showXgmRow = (_xgmTempStr.Length > 0 || _xgmFanNum.Length > 0 || _xgmPow.Length > 0 || _xgmUsage.HasValue) && (showTemp || showFans || showPower || showUsageMetric);

            int rowCount = 2;
            int gpuRow = 0;
            int cpuRow = 1;
            int midRow = -1;
            int xgmRow = -1;

            if (showMidRow) midRow = rowCount++;
            if (showXgmRow) xgmRow = rowCount++;

            int innerH = lineH * rowCount + lineGap * (rowCount - 1);
            int totalH = padY * 2 + innerH;

            int nameColW = showNames ? S(sc, BaseNameColWidth) : 0;
            int usageNumColW = S(sc, BaseUsageNumColWidth);
            int barW = S(sc, BaseUsageBarWidth);
            int memNumColW = S(sc, BaseMemNumColWidth);
            int batColW = S(sc, BaseBatColWidth);

            int cursor = padX;
            if (showFps) cursor += fpsColW + colGap;

            int nameX = cursor;
            if (showNames) cursor += nameColW + colGap;

            int leftX = cursor;
            cursor += S(sc, showTemp && showFans ? BaseLeftColWidth : showTemp || showFans ? BaseLightLeftColWidth : 0);

            int chartX = cursor;
            if (showChart) cursor += chartColW;

            int powX = cursor;
            if (showPower) { powX = cursor + powGap; cursor = powX + powColW; }

            int usageNumX = cursor, barX = cursor;
            if (showUsageMetric)
            {
                usageNumX = cursor + S(sc, BaseUsageBarGap);
                barX = usageNumX + usageNumColW + S(sc, BaseUsageNumGap);
                cursor = barX + barW;
            }

            int memNumX = cursor, memBarX = cursor;
            if (showMem)
            {
                memNumX = cursor + S(sc, BaseMemBarGap);
                memBarX = memNumX + memNumColW + S(sc, BaseUsageNumGap);
                cursor = memBarX + barW;
            }

            int batX = cursor, batIconX = cursor;
            if (showBat)
            {
                batX = cursor + S(sc, BaseBatColGap);
                batIconX = batX + batColW + S(sc, BaseBatIconGap);
                cursor = batIconX + S(sc, BaseBatIconWidth);
            }

            int width = cursor + padX;

            if (Size.Width != width || Size.Height != totalH)
                Size = new Size(width, totalH);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = _scalePercent <= 75 ? TextRenderingHint.ClearTypeGridFit : TextRenderingHint.AntiAliasGridFit;

            g.FillRoundedRectangle(_dragModeActive && _bgAlpha < DragMinAlpha ? _dragBgBrush : _bgBrush, Bound, radius);
            if (_borderPen != null && !_dragModeActive)
            {
                g.DrawRoundedRectangle(_borderPen, Bound, radius);
            }

            string fontName = AppConfig.GetString("overlay_font", "Consolas");
            if (sc != _lastScale || _lastFontName != fontName)
            {
                _lastScale = sc;
                _lastFontName = fontName;
                _font?.Dispose();    _font    = new Font(fontName, BaseFontSize * sc, FontStyle.Regular, GraphicsUnit.Pixel);
                _rpmFont?.Dispose(); _rpmFont = new Font(fontName, BaseRpmFontSize * sc, FontStyle.Regular, GraphicsUnit.Pixel);
                _fpsBold?.Dispose(); _fpsBold = new Font(fontName, (lineH * 2 + lineGap) / 1.15f, FontStyle.Bold, GraphicsUnit.Pixel);
                _totalPen?.Dispose(); _totalPen = new Pen(Color.FromArgb(255, 200, 200, 200), sc * 0.75f) { DashStyle = DashStyle.Dot };
                _axPen?.Dispose();    _axPen    = new Pen(Color.FromArgb(255, 80, 80, 80), sc * 0.5f);
                _charW = g.MeasureString("XX", _font).Width - g.MeasureString("X", _font).Width;
            }

            var font    = _font!;
            var rpmFont = _rpmFont!;
            var fpsBold = _fpsBold!;

            float charW = _charW;

            int topY = padY;
            // Nudge per-row text down so it lines up with the vertically centered usage bars.
            int textY = topY + (int)Math.Round(sc);
            int GetRowY(int r) => textY + r * (lineH + lineGap);

            if (_mode == OverlayMode.Bar)
            {
                int barRowH = lineH;
                int barTotalH = padY * 2 + barRowH;
                int barCursor = padX;

                // FPS
                if (showFps)
                {
                    string fpsStr = _currentFps > 0 ? _currentFps.ToString() : "--";
                    DrawStringWithShadow(g, fpsStr, font, _gpuBrush, new PointF(barCursor, textY));
                    barCursor += (int)g.MeasureString(fpsStr, font).Width + S(sc, 4);

                    if (_show1PercentLow && _currentFps > 0)
                    {
                        string lowStr = _current1PercentLowFps > 0 ? $"1% {_current1PercentLowFps}" : "1% --";
                        DrawStringWithShadow(g, lowStr, rpmFont, _gpuBrush, new PointF(barCursor, textY + 2f * sc));
                        barCursor += (int)g.MeasureString(lowStr, rpmFont).Width + S(sc, 4);
                    }

                    if (_showJitter && _currentFps > 0)
                    {
                        var jitterBrush = _currentJitterMs < 2.0 ? _jitterGreenBrush : (_currentJitterMs < 5.0 ? _jitterYellowBrush : _jitterRedBrush);
                        float dotSize = Math.Max(3f, 4f * sc);
                        g.FillEllipse(jitterBrush, barCursor, textY + (barRowH - dotSize) / 2f, dotSize, dotSize);
                        barCursor += (int)dotSize + S(sc, 6);
                    }

                    if (_showFrametime && _currentFps > 0)
                    {
                        string ftStr = $"{1000.0 / _currentFps:F1}ms";
                        DrawStringWithShadow(g, ftStr, rpmFont, _gpuBrush, new PointF(barCursor, textY + 2f * sc));
                        barCursor += (int)g.MeasureString(ftStr, rpmFont).Width + S(sc, 4);
                    }

                    barCursor += S(sc, 4);
                }

                // CPU
                if (showTemp || showPower || showUsageMetric)
                {
                    string cpuText = "CPU: " + (showTemp ? _cpuTempStr.Replace("CPU:", "").Trim() : "") + (showPower && _cpuPow.Length > 0 ? " " + _cpuPow : "") + (showUsageMetric && _cpuUsage.HasValue ? " " + _cpuUsage.Value + "%" : "");
                    DrawStringWithShadow(g, cpuText, font, _cpuBrush, new PointF(barCursor, textY));
                    barCursor += (int)g.MeasureString(cpuText, font).Width + colGap;
                }

                // GPU
                if (showTemp || showPower || showUsageMetric)
                {
                    string gpuText = "GPU: " + (showTemp ? _gpuTempStr.Replace("GPU:", "").Trim() : "") + (showPower && _gpuPow.Length > 0 ? " " + _gpuPow : "") + (showUsageMetric && _gpuUsage.HasValue ? " " + _gpuUsage.Value + "%" : "");
                    DrawStringWithShadow(g, gpuText, font, _gpuBrush, new PointF(barCursor, textY));
                    barCursor += (int)g.MeasureString(gpuText, font).Width + colGap;
                }

                // HDR badge
                if (_showHdrBadge)
                {
                    bool isHdr = ScreenCCD.IsHDR();
                    string badge = isHdr ? "HDR" : (AppConfig.Get("miniled", 1) == 1 ? "512Z" : "1Z");
                    DrawStringWithShadow(g, badge, rpmFont, _badgeBrush, new PointF(barCursor, textY + 2f * sc));
                    barCursor += (int)g.MeasureString(badge, rpmFont).Width + colGap;
                }

                // Battery
                if (showBat && _batLevel.Length > 0)
                {
                    string batText = _batLevel + (_batRate.Length > 0 ? " " + _batRate + "W" : "");
                    DrawStringWithShadow(g, batText, font, _batBrush, new PointF(barCursor, textY));
                    barCursor += (int)g.MeasureString(batText, font).Width;
                }

                int finalBarW = barCursor + padX;
                if (Size.Width != finalBarW || Size.Height != barTotalH)
                    Size = new Size(finalBarW, barTotalH);

                return;
            }

            // FPS
            if (showFps)
            {
                string fpsStr = _currentFps > 0 ? _currentFps.ToString() : "--";
                if (_show1PercentLow && _currentFps > 0)
                {
                    string lowStr = _current1PercentLowFps > 0 ? $"1% {_current1PercentLowFps}" : "1% --";
                    float fpsW = g.MeasureString(fpsStr, font).Width;
                    float lowW = g.MeasureString(lowStr, rpmFont).Width;
                    float fpsX = padX + (fpsColW - fpsW) / 2f;
                    float lowX = padX + (fpsColW - lowW) / 2f;
                    DrawStringWithShadow(g, fpsStr, font, _gpuBrush, new PointF(fpsX, GetRowY(gpuRow)));
                    DrawStringWithShadow(g, lowStr, rpmFont, _gpuBrush, new PointF(lowX, GetRowY(cpuRow) + 2f * sc));

                    // Small frametime stability meter / dot
                    if (_showJitter)
                    {
                        var jitterBrush = _currentJitterMs < 2.0 ? _jitterGreenBrush : (_currentJitterMs < 5.0 ? _jitterYellowBrush : _jitterRedBrush);
                        float dotSize = Math.Max(3f, 4f * sc);
                        float dotX = padX + fpsColW - dotSize - 2f * sc;
                        float dotY = GetRowY(cpuRow) + 4f * sc;
                        g.FillEllipse(jitterBrush, dotX, dotY, dotSize, dotSize);
                    }
                }
                else
                {
                    float fpsW = g.MeasureString(fpsStr, fpsBold).Width;
                    float fpsH = g.MeasureString(fpsStr, fpsBold).Height;
                    DrawStringWithShadow(g, fpsStr, fpsBold, _gpuBrush,
                        new PointF(padX + (fpsColW - fpsW) / 2f, topY + (innerH - fpsH) / 2f));
                }

                // Optional small HDR & Mini-LED badge
                if (_showHdrBadge)
                {
                    bool isHdr = ScreenCCD.IsHDR();
                    string badge = isHdr ? "HDR" : (AppConfig.Get("miniled", 1) == 1 ? "512Z" : "1Z");
                    float bW = g.MeasureString(badge, rpmFont).Width;
                    float bX = padX + (fpsColW - bW) / 2f;
                    float bY = topY - 1f * sc;
                    DrawStringWithShadow(g, badge, rpmFont, _badgeBrush, new PointF(bX, bY));
                }

                if (_showFrametime && _currentFps > 0 && !_showHdrBadge)
                {
                    string ftStr = $"{1000.0 / _currentFps:F1}ms";
                    float ftW = g.MeasureString(ftStr, rpmFont).Width;
                    float ftX = padX + (fpsColW - ftW) / 2f;
                    float ftY = topY - 1f * sc;
                    DrawStringWithShadow(g, ftStr, rpmFont, _gpuBrush, new PointF(ftX, ftY));
                }
            }

            if (showNames)
            {
                var savedClip = g.Save();
                g.SetClip(new RectangleF(nameX, topY, nameColW, innerH));
                DrawStringWithShadow(g, _gpuShortName, font, _gpuBrush, new PointF(nameX, GetRowY(gpuRow)));
                DrawStringWithShadow(g, _cpuShortName, font, _cpuBrush, new PointF(nameX, GetRowY(cpuRow)));
                if (showMidRow)
                    DrawStringWithShadow(g, "Mid Fan", font, _midBrush, new PointF(nameX, GetRowY(midRow)));
                if (showXgmRow)
                    DrawStringWithShadow(g, _xgmShortName, font, _xgmBrush, new PointF(nameX, GetRowY(xgmRow)));
                g.Restore(savedClip);
            }

            // Left column: temp and fan RPM
            string gpuTempLabel = showTemp ? _gpuTempStr : (showFans ? "GPU: " : "");
            string cpuTempLabel = showTemp ? _cpuTempStr : (showFans ? "CPU: " : "");
            string midTempLabel = showTemp ? _midTempStr : (showFans ? "MID: " : "");
            string xgmTempLabel = showTemp ? _xgmTempStr : (showFans ? "XGM: " : "");

            string gpuFanStr = showFans ? _gpuFanNum : "";
            string cpuFanStr = showFans ? _cpuFanNum : "";
            string midFanStr = showFans ? _midFanNum : "";
            string xgmFanStr = showFans ? _xgmFanNum : "";

            DrawTempFan(g, font, rpmFont, charW, sc, leftX, GetRowY(gpuRow), gpuTempLabel, gpuFanStr, _gpuBrush);
            DrawTempFan(g, font, rpmFont, charW, sc, leftX, GetRowY(cpuRow), cpuTempLabel, cpuFanStr, _cpuBrush);
            if (showMidRow)
                DrawTempFan(g, font, rpmFont, charW, sc, leftX, GetRowY(midRow), midTempLabel, midFanStr, _midBrush);
            if (showXgmRow)
                DrawTempFan(g, font, rpmFont, charW, sc, leftX, GetRowY(xgmRow), xgmTempLabel, xgmFanStr, _xgmBrush);

            // Chart — hidden in Light mode
            if (showChart)
                DrawStackedChart(g, chartX, topY, chartColW, innerH, sc);

            // Power — right-aligned, drawn in all modes
            if (showPower)
            {
                if (_gpuPow.Length > 0)
                    DrawStringWithShadow(g, _gpuPow, font, _gpuBrush,
                        new PointF(powX + powColW - g.MeasureString(_gpuPow, font).Width, GetRowY(gpuRow)));
                if (_cpuPow.Length > 0)
                    DrawStringWithShadow(g, _cpuPow, font, _cpuBrush,
                        new PointF(powX + powColW - g.MeasureString(_cpuPow, font).Width, GetRowY(cpuRow)));
                if (showXgmRow && _xgmPow.Length > 0)
                    DrawStringWithShadow(g, _xgmPow, font, _xgmBrush,
                        new PointF(powX + powColW - g.MeasureString(_xgmPow, font).Width, GetRowY(xgmRow)));
            }

            if (showUsage)
            {
                // Bar sizing: fixed bar height per DPI; cellH grows with DPI but sepH stays 1.
                int cellH = Math.Max(1, (int)Math.Floor(sc));
                int sepH = 1;
                int targetBarH = S(sc, BaseUsageBarHeight);
                int pitch = cellH + sepH;
                int numCells = Math.Max(2, (targetBarH + sepH) / pitch);
                int barH = numCells * cellH + (numCells - 1) * sepH;
                int barYOff = (lineH - barH) / 2 - S(sc, BaseUsageBarYNudge);

                int GetBarY(int r) => topY + r * (lineH + lineGap) + barYOff;

                if (showUsageMetric)
                {
                    DrawUsageBar(g, barX, GetBarY(gpuRow), barW, cellH, sepH, numCells, _gpuUsage ?? 0, _gpuBrush, _gpuFillBrush);
                    DrawUsageBar(g, barX, GetBarY(cpuRow), barW, cellH, sepH, numCells, _cpuUsage ?? 0, _cpuBrush, _cpuFillBrush);
                    if (showXgmRow && _xgmUsage.HasValue)
                        DrawUsageBar(g, barX, GetBarY(xgmRow), barW, cellH, sepH, numCells, _xgmUsage.Value, _xgmBrush, _xgmFillBrush);

                    DrawUsagePercent(g, font, usageNumX, usageNumColW, GetRowY(gpuRow), _gpuUsage, _gpuBrush);
                    DrawUsagePercent(g, font, usageNumX, usageNumColW, GetRowY(cpuRow), _cpuUsage, _cpuBrush);
                    if (showXgmRow && _xgmUsage.HasValue)
                        DrawUsagePercent(g, font, usageNumX, usageNumColW, GetRowY(xgmRow), _xgmUsage, _xgmBrush);
                }

                // VRAM (GPU row) / RAM (CPU row) — complete mode only
                if (showMem)
                {
                    DrawMemGb(g, font, memNumX, memNumColW, GetRowY(gpuRow), _vramUsedMb, _gpuBrush);
                    DrawMemGb(g, font, memNumX, memNumColW, GetRowY(cpuRow), _ramUsedMb, _cpuBrush);

                    DrawUsageBar(g, memBarX, GetBarY(gpuRow), barW, cellH, sepH, numCells, _vramUsage ?? 0, _gpuBrush, _gpuFillBrush);
                    DrawUsageBar(g, memBarX, GetBarY(cpuRow), barW, cellH, sepH, numCells, _ramUsage ?? 0, _cpuBrush, _cpuFillBrush);
                }
            }

            if (showBat)
            {
                if (_batLevel.Length > 0)
                    DrawStringWithShadow(g, _batLevel, font, _batBrush,
                        new PointF(batX + batColW - g.MeasureString(_batLevel, font).Width, GetRowY(0)));
                if (_batRate.Length > 0)
                {
                    DrawStringWithShadow(g, _batRate, font, _batBrush,
                        new PointF(batX + batColW - g.MeasureString(_batRate, font).Width, GetRowY(1)));
                    DrawStringWithShadow(g, "W", font, _batBrush,
                        new PointF(batIconX + (S(sc, BaseBatIconWidth) - g.MeasureString("W", font).Width) / 2f, GetRowY(1)));
                }

                int batIconH = S(sc, BaseBatIconHeight);
                DrawBatteryIcon(g, batIconX, topY + (lineH - batIconH) / 2 - S(sc, BaseUsageBarYNudge), batIconH, sc, _batPercent, _batCharging);
            }
        }

        private void DrawStringWithShadow(Graphics g, string text, Font font, Brush brush, PointF pt)
        {
            if (_showShadow)
            {
                g.DrawString(text, font, _shadowBrush, new PointF(pt.X + 1f, pt.Y + 1f));
            }
            g.DrawString(text, font, brush, pt);
        }

        private void DrawUsagePercent(Graphics g, Font font, int x, int colW, int y, int? usage, SolidBrush brush)
        {
            if (!usage.HasValue) return; // mirror the power column — empty when unavailable
            string s = usage.Value + "%";
            DrawStringWithShadow(g, s, font, brush, new PointF(x + colW - g.MeasureString(s, font).Width, y));
        }

        private void DrawMemGb(Graphics g, Font font, int x, int colW, int y, int? usedMb, SolidBrush brush)
        {
            if (!usedMb.HasValue) return;
            string s = (usedMb.Value / 1024.0).ToString("F1") + "GB";
            DrawStringWithShadow(g, s, font, brush, new PointF(x + colW - g.MeasureString(s, font).Width, y));
        }

        private static void DrawBatteryIcon(Graphics g, int x, int y, int h, float sc, int level, bool charging)
        {
            var prevSmoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None;

            int w = S(sc, BaseBatIconWidth);
            int nubW = Math.Max(2, S(sc, BaseBatNubWidth));
            int nubH = Math.Max(1, S(sc, BaseBatNubHeight));
            int b = Math.Max(1, (int)sc);
            int bodyY = y + nubH;
            int bodyH = h - nubH;

            g.FillRectangle(_batBrush, x + (w - nubW) / 2, y, nubW, nubH);
            g.FillRectangle(_batBrush, x, bodyY, w, b);
            g.FillRectangle(_batBrush, x, bodyY + bodyH - b, w, b);
            g.FillRectangle(_batBrush, x, bodyY, b, bodyH);
            g.FillRectangle(_batBrush, x + w - b, bodyY, b, bodyH);

            int innerX = x + b, innerW = w - 2 * b;
            int innerY = bodyY + b, innerH = bodyH - 2 * b;
            int fillH = (int)Math.Round(innerH * Math.Clamp(level, 0, 100) / 100.0);
            if (fillH > 0) g.FillRectangle(_batBrush, innerX, innerY + innerH - fillH, innerW, fillH);
            if (fillH < innerH) g.FillRectangle(_batDimBrush, innerX, innerY, innerW, innerH - fillH);

            if (charging)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float bw = w * 0.9f, bh = bodyH * 0.64f;
                float bx = x + (w - bw) / 2f, by = bodyY + (bodyH - bh) / 2f;
                for (int i = 0; i < _boltShape.Length; i++)
                    _boltPts[i] = new PointF(bx + _boltShape[i].X * bw, by + _boltShape[i].Y * bh);
                g.FillPolygon(_batBoltBrush, _boltPts);
            }

            g.SmoothingMode = prevSmoothing;
        }

        private static void DrawUsageBar(Graphics g, int x, int y, int w, int cellH, int sepH, int numCells, int usage, SolidBrush litBrush, SolidBrush dimBrush)
        {
            var prevSmoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None;

            int lit = Math.Clamp((int)Math.Ceiling(usage * numCells / 100f), 0, numCells);
            int pitch = cellH + sepH;

            for (int i = 0; i < numCells; i++)
            {
                bool isLit = i >= (numCells - lit);
                g.FillRectangle(isLit ? litBrush : dimBrush, x, y + i * pitch, w, cellH);
            }

            g.SmoothingMode = prevSmoothing;
        }

        // tempStr already has a trailing space — natural separator from fan number.
        // 2px gap added before "RPM" superscript so it doesn't glue to the digits.
        private void DrawTempFan(Graphics g, Font font, Font rpmFont, float charW, float sc,
        float x, float y, string tempStr, string fanNum, SolidBrush brush)
        {
            DrawStringWithShadow(g, tempStr, font, brush, new PointF(x, y));

            if (fanNum.Length == 0) return;

            float numX = x + charW * tempStr.Length;
            DrawStringWithShadow(g, fanNum, font, brush, new PointF(numX, y));

            // 2px gap between digits and "RPM" label
            float rpmX = numX + charW * fanNum.Length + 2f * sc;
            DrawStringWithShadow(g, "RPM", rpmFont, brush, new PointF(rpmX, y));
        }

        private void DrawStackedChart(Graphics g, int x, int y, int w, int h, float sc)
        {
            float peak = 10f;
            for (int i = 0; i < HistoryLength; i++)
            {
                float t = _cpuHistory[i] + _gpuHistory[i];
                if (t > peak) peak = t;
            }

            float stepX = (float)w / (HistoryLength - 1);
            int Idx(int i) => (_historyHead + i) % HistoryLength;

            for (int i = 0; i < HistoryLength; i++)
            {
                int idx = Idx(i);
                float px = x + i * stepX;
                float cpuH = (_cpuHistory[idx] / peak) * h;
                float gpuH = (_gpuHistory[idx] / peak) * h;

                _basePts[i] = new PointF(px, y + h);
                _cpuPts[i]  = new PointF(px, y + h - cpuH);
                _gpuPts[i]  = new PointF(px, y + h - cpuH - gpuH);
            }

            var saved = g.Save();
            g.SetClip(new RectangleF(x, y, w, h));

            FillArea(g, _cpuPts, _basePts, _cpuFillBrush);
            g.DrawLines(_cpuLinePen, _cpuPts);
            FillArea(g, _gpuPts, _cpuPts, _gpuFillBrush);
            g.DrawLines(_gpuLinePen, _gpuPts);

            g.DrawLines(_totalPen!, _gpuPts);

            g.Restore(saved);
            g.DrawLine(_axPen!, x, y + h, x + w, y + h);
        }

        private void FillArea(Graphics g, PointF[] topLine, PointF[] bottomLine, SolidBrush brush)
        {
            int n = topLine.Length;
            topLine.CopyTo(_polyPts, 0);
            for (int i = 0; i < n; i++)
                _polyPts[n + i] = bottomLine[n - 1 - i];
            g.FillPolygon(brush, _polyPts);
        }

        private void PositionAtTopLeft()
        {
            Screen screen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
            Location = new Point(screen.Bounds.X + MarginFromEdge, screen.Bounds.Y + MarginFromEdge);
        }

        public void SnapToCorner(OverlayCorner corner)
        {
            Screen screen = TargetScreen();
            int margin = MarginFromEdge;
            int x = 0;
            int y = 0;
            int anchor = 0;
            switch (corner)
            {
                case OverlayCorner.TopLeft:
                    x = screen.Bounds.X + margin;
                    y = screen.Bounds.Y + margin;
                    anchor = 0;
                    break;
                case OverlayCorner.TopRight:
                    x = screen.Bounds.Right - Width - margin;
                    y = screen.Bounds.Y + margin;
                    anchor = 1;
                    break;
                case OverlayCorner.BottomLeft:
                    x = screen.Bounds.X + margin;
                    y = screen.Bounds.Bottom - Height - margin;
                    anchor = 2;
                    break;
                case OverlayCorner.BottomRight:
                    x = screen.Bounds.Right - Width - margin;
                    y = screen.Bounds.Bottom - Height - margin;
                    anchor = 3;
                    break;
            }
            Location = new Point(x, y);
            AppConfig.Set("overlay_anchor", anchor);
            AppConfig.Set("overlay_offset_x", margin);
            AppConfig.Set("overlay_offset_y", margin);
            AppConfig.Set("overlay_screen", screen.Primary ? "" : screen.DeviceName);
        }

        public static OverlayCorner CurrentCorner => (OverlayCorner)Math.Clamp(AppConfig.Get("overlay_anchor", 0), 0, 3);

        private static Screen TargetScreen()
        {
            string name = AppConfig.GetString("overlay_screen");
            if (!string.IsNullOrEmpty(name))
                foreach (Screen s in Screen.AllScreens)
                    if (s.DeviceName == name) return s;
            return Screen.PrimaryScreen ?? Screen.AllScreens[0];
        }

        private bool AreDragKeysDown() =>
            (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0 &&
            (GetAsyncKeyState(VK_SHIFT)   & 0x8000) != 0 &&
            (GetAsyncKeyState(VK_MENU)    & 0x8000) != 0;

        public void SetDragKey(bool down)
        {
            if (down && (!_active || _hidden)) return;
            _dragKey = down;
            if (down != _dragModeActive && !_dragging) ApplyDragMode(down);
        }

        private void ApplyDragMode(bool on)
        {
            _dragModeActive = on;
            SetTransparentStyle(!on);
            Cursor.Current = on ? Cursors.Hand : Cursors.Default;
            if (_bgAlpha < DragMinAlpha) Invalidate();
        }

        private void SetTransparentStyle(bool transparent)
        {
            if (Handle == nint.Zero) return;
            int style = GetWindowLong(Handle, GWL_EXSTYLE);
            style = transparent ? (style | WS_EX_TRANSPARENT_FLAG) : (style & ~WS_EX_TRANSPARENT_FLAG);
            SetWindowLong(Handle, GWL_EXSTYLE, style);
        }

        private void ApplyScale(int next)
        {
            next = Math.Clamp(next, MinScalePercent, MaxScalePercent);
            if (next == _scalePercent) return;

            Point center = new Point(Location.X + Width / 2, Location.Y + Height / 2);
            Screen screen = Screen.FromPoint(center);
            bool isRight  = center.X > screen.Bounds.X + screen.Bounds.Width  / 2;
            bool isBottom = center.Y > screen.Bounds.Y + screen.Bounds.Height / 2;
            int rightEdge  = Location.X + Width;
            int bottomEdge = Location.Y + Height;

            _scalePercent = next;
            AppConfig.Set("overlay_scale_percent", _scalePercent);
            Invalidate(); // resizes synchronously via PerformPaint → Size.set

            int newX = isRight  ? rightEdge  - Width  : Location.X;
            int newY = isBottom ? bottomEdge - Height : Location.Y;
            if (newX != Location.X || newY != Location.Y)
                Location = new Point(newX, newY);
            SavePosition();
        }

        public void RefreshSettings()
        {
            if (!_active) return;
            _mode = (OverlayMode)Math.Clamp(AppConfig.Get("overlay_mode", 0), 0, 4);
            _scalePercent = Math.Clamp(AppConfig.Get("overlay_scale_percent", 100), MinScalePercent, MaxScalePercent);
            _lastScale = 0f;
            _lastFontName = null;
            ApplyColors();
            ApplyPreset(_mode);
            ApplySensorFlags();
            EnsureFpsMonitor();
            Invalidate();
            RestorePosition();
        }

        private void SavePosition()
        {
            Point center = new Point(Location.X + Width / 2, Location.Y + Height / 2);
            Screen screen = Screen.FromPoint(center);
            bool isRight  = center.X > screen.Bounds.X + screen.Bounds.Width  / 2;
            bool isBottom = center.Y > screen.Bounds.Y + screen.Bounds.Height / 2;
            int anchor  = (isBottom ? 2 : 0) | (isRight ? 1 : 0);
            AppConfig.Set("overlay_screen", screen.Primary ? "" : screen.DeviceName);
            int offsetX = isRight  ? screen.Bounds.Right  - Location.X - Width  : Location.X - screen.Bounds.X;
            int offsetY = isBottom ? screen.Bounds.Bottom - Location.Y - Height : Location.Y - screen.Bounds.Y;
            AppConfig.Set("overlay_anchor",   anchor);
            AppConfig.Set("overlay_offset_x", offsetX);
            AppConfig.Set("overlay_offset_y", offsetY);
        }

        public static Color CpuColor => ParseColor("overlay_color_cpu", DefaultCpuColor);
        public static Color GpuColor => ParseColor("overlay_color_gpu", DefaultGpuColor);

        private static Color ParseColor(string key, Color fallback)
        {
            string hex = AppConfig.GetString(key);
            if (string.IsNullOrEmpty(hex)) return fallback;
            try { return ColorTranslator.FromHtml(hex.StartsWith("#") ? hex : "#" + hex); }
            catch { return fallback; }
        }

        private void ApplyColors()
        {
            Color gpu = ParseColor("overlay_color_gpu", DefaultGpuColor);
            Color cpu = ParseColor("overlay_color_cpu", DefaultCpuColor);
            _bgAlpha = Math.Clamp(AppConfig.Get("overlay_alpha", 128), 0, 255);

            _gpuBrush.Dispose();     _gpuBrush = new SolidBrush(gpu);
            _cpuBrush.Dispose();     _cpuBrush = new SolidBrush(cpu);
            Color mid = ParseColor("overlay_color_mid", DefaultMidColor);
            Color xgm = ParseColor("overlay_color_xgm", DefaultXgmColor);
            _midBrush.Dispose();     _midBrush = new SolidBrush(mid);
            _xgmBrush.Dispose();     _xgmBrush = new SolidBrush(xgm);
            _xgmFillBrush.Dispose(); _xgmFillBrush = new SolidBrush(Color.FromArgb(128, xgm.R / 3, xgm.G / 3, xgm.B / 3));
            _gpuLinePen.Dispose();   _gpuLinePen = new Pen(gpu, 1.5f);
            _cpuLinePen.Dispose();   _cpuLinePen = new Pen(cpu, 1.5f);
            // Chart fill = base color at 1/3 brightness, alpha 128
            _gpuFillBrush.Dispose(); _gpuFillBrush = new SolidBrush(Color.FromArgb(128, gpu.R / 3, gpu.G / 3, gpu.B / 3));
            _cpuFillBrush.Dispose(); _cpuFillBrush = new SolidBrush(Color.FromArgb(128, cpu.R / 3, cpu.G / 3, cpu.B / 3));

            string overlayStyle = AppConfig.GetString("overlay_style", "translucent_dark");
            _bgBrush.Dispose();
            _borderPen?.Dispose();
            switch (overlayStyle)
            {
                case "frosted_glass":
                    _bgBrush = new SolidBrush(Color.FromArgb(Math.Min(235, Math.Max(40, _bgAlpha)), 16, 24, 38));
                    _borderPen = new Pen(Color.FromArgb(Math.Min(255, _bgAlpha + 60), 0, 190, 240), 1.2f);
                    break;
                case "neon_cyber":
                    _bgBrush = new SolidBrush(Color.FromArgb(_bgAlpha, 20, 10, 14));
                    _borderPen = new Pen(Color.FromArgb(Math.Min(255, _bgAlpha + 70), 240, 40, 60), 1.2f);
                    break;
                case "minimal_frameless":
                    _bgBrush = new SolidBrush(Color.FromArgb(Math.Min(25, _bgAlpha / 4), 0, 0, 0));
                    _borderPen = null;
                    break;
                case "solid_black":
                    _bgBrush = new SolidBrush(Color.FromArgb(255, 0, 0, 0));
                    _borderPen = new Pen(Color.FromArgb(200, 60, 60, 60), 1f);
                    break;
                case "nebula_tint":
                    _bgBrush = new SolidBrush(Color.FromArgb(_bgAlpha, 14, 20, 36));
                    _borderPen = new Pen(Color.FromArgb(Math.Min(255, _bgAlpha + 50), 55, 110, 200), 1.2f);
                    break;
                default: // "translucent_dark"
                    _bgBrush = new SolidBrush(Color.FromArgb(_bgAlpha, 12, 14, 18));
                    _borderPen = new Pen(Color.FromArgb(Math.Min(255, _bgAlpha / 2 + 30), 70, 80, 100), 1f);
                    break;
            }
        }

        public static string ModeKey(string key, int mode) => key + "_" + mode;

        public static bool BlockShown(string key, int mode)
        {
            OverlayMode m = (OverlayMode)mode;
            bool def = key switch
            {
                "overlay_show_fans" or "overlay_show_chart" => m != OverlayMode.Light && m != OverlayMode.Bar,
                "overlay_show_usage" => m == OverlayMode.Full || m == OverlayMode.Complete,
                "overlay_show_ram" => m == OverlayMode.Complete,
                "overlay_show_1pct_low" => m == OverlayMode.Full || m == OverlayMode.Complete || m == OverlayMode.Bar,
                "overlay_show_jitter" => m == OverlayMode.Full || m == OverlayMode.Complete || m == OverlayMode.Bar,
                "overlay_show_hdr_badge" => false,
                "overlay_show_frametime" => m == OverlayMode.Full || m == OverlayMode.Complete,
                "overlay_show_clock" => false,
                "overlay_show_vram" => m == OverlayMode.Complete,
                "overlay_show_mid_fan" => true,
                "overlay_text_shadow" => true,
                "overlay_names" => false,
                _ => true,
            };
            return AppConfig.Get(ModeKey(key, mode), def ? 1 : 0) != 0;
        }

        // -1 = only on battery, 0 = off, 1 = always
        public static int BatteryState(int mode) =>
            AppConfig.Get(ModeKey("overlay_show_battery", mode), (OverlayMode)mode == OverlayMode.Light ? 0 : -1);

        private void ApplyPreset(OverlayMode mode)
        {
            int m = (int)mode;
            _showFps   = BlockShown("overlay_show_fps", m);
            _showTemp  = BlockShown("overlay_show_temp", m);
            _showFans  = BlockShown("overlay_show_fans", m);
            _showChart = BlockShown("overlay_show_chart", m);
            _showPower = BlockShown("overlay_show_power", m);
            _showUsage = BlockShown("overlay_show_usage", m);
            _showRam   = BlockShown("overlay_show_ram", m);
            _showNames = BlockShown("overlay_names", m);
            _show1PercentLow = BlockShown("overlay_show_1pct_low", m);
            _showJitter = BlockShown("overlay_show_jitter", m);
            _showHdrBadge = BlockShown("overlay_show_hdr_badge", m);
            _showFrametime = BlockShown("overlay_show_frametime", m);
            _showClock = BlockShown("overlay_show_clock", m);
            _showVram = BlockShown("overlay_show_vram", m);
            _showMidFan = BlockShown("overlay_show_mid_fan", m);
            _showShadow = BlockShown("overlay_text_shadow", m);
            _overlayBattery = BatteryState(m);
            _showBattery = _overlayBattery != 0;
        }

        // Don't pull sensors for blocks that aren't drawn (power feeds both the power and chart blocks).
        private void ApplySensorFlags()
        {
            HardwareControl.readFans   = _showFans || _showMidFan;
            HardwareControl.readUsage  = _showUsage;
            HardwareControl.readMemory = _showRam || _showVram;
            HardwareControl.readPower  = _showPower || _showChart;
            HardwareControl.readBattery = _showBattery && _onBattery;
        }

        // Started for the FPS block, or for Auto Show to detect a game even with FPS hidden. Only
        // torn down in StopOverlay — disposing here would race the timer thread's SampleFps (the
        // reference write is atomic, so that thread cleanly sees null-or-monitor).
        private void EnsureFpsMonitor()
        {
            if (_fps != null || !(_showFps || _gameOnly)) return;
            _currentFps = 0;
            _fps = new EtwFpsMonitor();
            _fpsTask = Task.Run(() => _fps.Start());
        }

        // Re-anchor the overlay after the user changes resolution or swaps the primary
        // display — without this the absolute Location can end up off-screen or far
        // from the corner the user originally pinned it to.
        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            if (!_active) return;
            Program.settingsForm?.BeginInvoke(() => { if (_active) RestorePosition(); });
        }

        private void RestorePosition()
        {
            int anchor = AppConfig.Get("overlay_anchor", -1);
            if (anchor < 0) { PositionAtTopLeft(); return; }
            int offsetX = AppConfig.Get("overlay_offset_x", MarginFromEdge);
            int offsetY = AppConfig.Get("overlay_offset_y", MarginFromEdge);
            Screen screen = TargetScreen();
            bool isRight  = (anchor & 1) != 0;
            bool isBottom = (anchor & 2) != 0;
            int x = isRight  ? screen.Bounds.Right  - Width  - offsetX : screen.Bounds.X + offsetX;
            int y = isBottom ? screen.Bounds.Bottom - Height - offsetY : screen.Bounds.Y + offsetY;
            const int margin = 5;
            x = Math.Max(screen.Bounds.Left + margin, Math.Min(x, screen.Bounds.Right  - Width  - margin));
            y = Math.Max(screen.Bounds.Top  + margin, Math.Min(y, screen.Bounds.Bottom - Height - margin));
            Location = new Point(x, y);
        }

        public void StartOverlay()
        {
            _active = true;
            _lastFgPid = 0;
            _gameOnly = AppConfig.IsOverlayGameOnly();
            _hidden = false;
            _shownPid = 0;
            _fgDesktop = false;
            _mode = (OverlayMode)Math.Clamp(AppConfig.Get("overlay_mode", 0), 0, 3);
            _scalePercent = Math.Clamp(AppConfig.Get("overlay_scale_percent", 100), MinScalePercent, MaxScalePercent);
            ApplyColors();
            ApplyPreset(_mode);
            _onBattery = _isAlly || _overlayBattery == 1 || SystemInformation.PowerStatus.PowerLineStatus != PowerLineStatus.Online;
            ApplySensorFlags();
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            HardwareControl.ResetCPUPowerCounter();

            _fps?.Dispose();
            _fps = null;
            _currentFps = 0;
            EnsureFpsMonitor();

            float sc = GetScale();
            int innerH = S(sc, BaseLineHeight) * 2 + S(sc, BaseLineSpacing);
            Size = new Size(S(sc, BaseModeWidth()), S(sc, BasePadY) * 2 + innerH);

            RestorePosition();
            base.Show();
            if (_gameOnly) { _hidden = true; User32.ShowWindow(Handle, User32.SW_HIDE); }
            Tick();
            RestorePosition(); // re-anchor once the first paint has settled the collapsed width
            _timer.Start();

            if (_gameOnly && _fgHook == IntPtr.Zero)
            {
                _fgHookProc = (_, _, _, _, _, _, _) => OnForegroundChanged();
                _fgHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                    IntPtr.Zero, _fgHookProc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
            }
        }

        public void StopOverlay()
        {
            _active = false;
            if (_fgHook != IntPtr.Zero) { UnhookWinEvent(_fgHook); _fgHook = IntPtr.Zero; }
            _fgHookProc = null;
            HardwareControl.readUsage = false;
            HardwareControl.readFans = false;
            HardwareControl.readMemory = false;
            HardwareControl.readPower = false;
            HardwareControl.readBattery = false;
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _timer.Stop();
            _dragModeActive = false;
            _dragging = false;
            _dragKey = false;

            _fps?.Dispose();
            _fps = null;
            _currentFps = 0;

            // Wait for the ETW pump thread to actually finish before we trim memory,
            // otherwise its kernel ring buffers are still mapped into our working set.
            Task? task = _fpsTask;
            _fpsTask = null;
            MemoryHelper.TrimAfter(task);

            _font?.Dispose();     _font     = null;
            _rpmFont?.Dispose();  _rpmFont  = null;
            _fpsBold?.Dispose();  _fpsBold  = null;
            _totalPen?.Dispose(); _totalPen = null;
            _axPen?.Dispose();    _axPen    = null;
            _borderPen?.Dispose(); _borderPen = null;
            _lastFontName = null;
            _lastScale = 0f;
            _lastScale = 0f;
            base.Hide();
        }

        public void SuspendForDisplayOff()
        {
            if (_active) StopOverlay();
        }

        public void ResumeForDisplayOn()
        {
            if (!_active && AppConfig.IsOverlay()) StartOverlay();
        }
    }
}
