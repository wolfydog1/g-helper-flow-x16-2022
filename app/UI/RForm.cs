using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace GHelper.UI
{
    public class RForm : Form
    {

        public static Color colorEco = Color.FromArgb(255, 6, 180, 138);
        public static Color colorStandard = Color.FromArgb(255, 58, 174, 239);
        public static Color colorTurbo = Color.FromArgb(255, 255, 32, 32);
        public static Color colorCustom = Color.FromArgb(255, 255, 128, 0);
        public static Color colorGray = Color.FromArgb(255, 168, 168, 168);


        public static Color buttonMain;
        public static Color buttonSecond;

        public static Color formBack;
        public static Color foreMain;
        public static Color borderMain;
        public static Color borderSecond;
        public static Color chartMain;
        public static Color chartGrid;

        public static bool flatTheme = false;
        public static string CurrentStyle = "default";
        public static string CurrentFontFamily = "Segoe UI";
        public static float CurrentFontScale = 1.0f;
        public static int CurrentButtonRadius = 8;

        [DllImport("UXTheme.dll", SetLastError = true, EntryPoint = "#138")]
        public static extern bool CheckSystemDarkModeStatus();

        [DllImport("UXTheme.dll", SetLastError = true, EntryPoint = "#135")]
        private static extern int SetPreferredAppMode(int preferredAppMode);

        [DllImport("UXTheme.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(nint hWnd, string pszSubAppName, string? pszSubIdList);

        [DllImport("DwmApi")] //System.Runtime.InteropServices
        private static extern int DwmSetWindowAttribute(nint hwnd, int attr, int[] attrValue, int attrSize);

        public bool darkTheme = false;
        private bool themeInitialized = false;
        protected override CreateParams CreateParams
        {
            get
            {
                var parms = base.CreateParams;
                if (!AutoScroll)
                {
                    parms.Style &= ~0x02000000;  // Turn off WS_CLIPCHILDREN only for non-scrolling forms
                }
                parms.ClassStyle &= ~0x00020000;
                return parms;
            }
        }

        public static void LoadStyleTokens()
        {
            string? style = AppConfig.GetString("theme")?.ToLower();
            CurrentStyle = string.IsNullOrWhiteSpace(style) ? "default" : style;
            flatTheme = CurrentStyle == "flat" || CurrentStyle == "amoled";

            string? fam = AppConfig.GetString("ui_font_family");
            CurrentFontFamily = string.IsNullOrWhiteSpace(fam) ? "Segoe UI" : fam;

            int scalePct = AppConfig.Get("ui_font_scale", 100);
            if (scalePct < 80 || scalePct > 130) scalePct = 100;
            CurrentFontScale = scalePct / 100f;

            int rad = AppConfig.Get("ui_button_radius", 8);
            if (rad < 1 || rad > 20) rad = 8;
            CurrentButtonRadius = rad;
        }

        public static void InitColors(bool darkTheme)
        {
            LoadStyleTokens();

            if (!darkTheme || CurrentStyle == "light")
            {
                buttonMain = Color.FromArgb(255, 250, 251, 253);
                buttonSecond = Color.FromArgb(255, 237, 240, 245);

                formBack = Color.FromArgb(255, 244, 246, 250);
                foreMain = Color.FromArgb(255, 24, 28, 34);
                borderMain = Color.FromArgb(255, 208, 214, 224);
                borderSecond = Color.FromArgb(255, 220, 225, 233);

                chartMain = Color.FromArgb(255, 250, 251, 253);
                chartGrid = Color.FromArgb(255, 205, 210, 218);
                return;
            }

            switch (CurrentStyle)
            {
                case "rog_cyber":
                    buttonMain = Color.FromArgb(255, 44, 24, 28);
                    buttonSecond = Color.FromArgb(255, 32, 18, 22);
                    formBack = Color.FromArgb(255, 19, 13, 16);
                    foreMain = Color.FromArgb(255, 248, 240, 242);
                    borderMain = Color.FromArgb(255, 110, 32, 42);
                    borderSecond = Color.FromArgb(255, 72, 24, 30);
                    chartMain = Color.FromArgb(255, 28, 16, 20);
                    chartGrid = Color.FromArgb(255, 92, 36, 44);
                    break;

                case "nebula_blue":
                    buttonMain = Color.FromArgb(255, 28, 40, 58);
                    buttonSecond = Color.FromArgb(255, 20, 30, 44);
                    formBack = Color.FromArgb(255, 14, 20, 30);
                    foreMain = Color.FromArgb(255, 236, 244, 255);
                    borderMain = Color.FromArgb(255, 46, 82, 122);
                    borderSecond = Color.FromArgb(255, 34, 58, 88);
                    chartMain = Color.FromArgb(255, 18, 26, 38);
                    chartGrid = Color.FromArgb(255, 52, 78, 110);
                    break;

                case "amoled":
                    buttonMain = Color.FromArgb(255, 24, 24, 28);
                    buttonSecond = Color.FromArgb(255, 14, 14, 17);
                    formBack = Color.FromArgb(255, 6, 6, 8);
                    foreMain = Color.FromArgb(255, 245, 245, 248);
                    borderMain = Color.FromArgb(255, 48, 48, 54);
                    borderSecond = Color.FromArgb(255, 32, 32, 38);
                    chartMain = Color.FromArgb(255, 12, 12, 15);
                    chartGrid = Color.FromArgb(255, 52, 52, 60);
                    break;

                case "emerald_matrix":
                    buttonMain = Color.FromArgb(255, 20, 36, 28);
                    buttonSecond = Color.FromArgb(255, 14, 26, 20);
                    formBack = Color.FromArgb(255, 10, 18, 14);
                    foreMain = Color.FromArgb(255, 235, 252, 243);
                    borderMain = Color.FromArgb(255, 34, 110, 72);
                    borderSecond = Color.FromArgb(255, 24, 76, 50);
                    chartMain = Color.FromArgb(255, 14, 24, 18);
                    chartGrid = Color.FromArgb(255, 30, 70, 48);
                    break;

                case "solar_amber":
                    buttonMain = Color.FromArgb(255, 42, 32, 18);
                    buttonSecond = Color.FromArgb(255, 30, 22, 12);
                    formBack = Color.FromArgb(255, 20, 16, 10);
                    foreMain = Color.FromArgb(255, 254, 243, 225);
                    borderMain = Color.FromArgb(255, 140, 95, 25);
                    borderSecond = Color.FromArgb(255, 95, 62, 16);
                    chartMain = Color.FromArgb(255, 26, 20, 14);
                    chartGrid = Color.FromArgb(255, 85, 60, 25);
                    break;

                case "vapor_violet":
                    buttonMain = Color.FromArgb(255, 38, 24, 48);
                    buttonSecond = Color.FromArgb(255, 28, 16, 36);
                    formBack = Color.FromArgb(255, 18, 12, 24);
                    foreMain = Color.FromArgb(255, 246, 238, 255);
                    borderMain = Color.FromArgb(255, 112, 54, 146);
                    borderSecond = Color.FromArgb(255, 78, 36, 102);
                    chartMain = Color.FromArgb(255, 24, 16, 32);
                    chartGrid = Color.FromArgb(255, 75, 40, 95);
                    break;

                case "frost_titanium":
                    buttonMain = Color.FromArgb(255, 26, 34, 44);
                    buttonSecond = Color.FromArgb(255, 18, 24, 32);
                    formBack = Color.FromArgb(255, 13, 17, 23);
                    foreMain = Color.FromArgb(255, 238, 244, 250);
                    borderMain = Color.FromArgb(255, 42, 98, 134);
                    borderSecond = Color.FromArgb(255, 30, 68, 94);
                    chartMain = Color.FromArgb(255, 17, 22, 30);
                    chartGrid = Color.FromArgb(255, 45, 80, 105);
                    break;

                default: // "default" and "flat"
                    buttonMain = Color.FromArgb(255, 46, 46, 46);
                    buttonSecond = Color.FromArgb(255, 36, 36, 36);
                    formBack = Color.FromArgb(255, 28, 28, 28);
                    foreMain = Color.FromArgb(255, 240, 240, 240);
                    borderMain = Color.FromArgb(255, 55, 55, 55);
                    borderSecond = Color.FromArgb(255, 42, 42, 42);
                    chartMain = Color.FromArgb(255, 35, 35, 35);
                    chartGrid = Color.FromArgb(255, 70, 70, 70);
                    break;
            }
        }

        private static bool IsDarkTheme()
        {
            string? style = AppConfig.GetString("theme")?.ToLower();
            if (style == "light") return false;
            if (style is "rog_cyber" or "nebula_blue" or "amoled" or "flat" or "default" or "emerald_matrix" or "solar_amber" or "vapor_violet" or "frost_titanium")
            {
                string? modeOverride = AppConfig.GetString("ui_mode")?.ToLower();
                if (modeOverride == "light") return false;
                return true;
            }

            string? uiMode = AppConfig.GetString("ui_mode");

            if (uiMode is not null && uiMode.ToLower() == "dark")
            {
                return true;
            }

            if (uiMode is not null && uiMode.ToLower() == "light")
            {
                return false;
            }

            if (uiMode is not null && uiMode.ToLower() == "windows")
            {
                return CheckSystemDarkModeStatus();
            }

            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var registryValueObject = key?.GetValue("AppsUseLightTheme");

            if (registryValueObject == null) return false;
            return (int)registryValueObject <= 0;
        }

        public bool InitTheme(bool setDPI = false, bool forceAdjust = false)
        {
            bool newDarkTheme = IsDarkTheme();
            bool changed = darkTheme != newDarkTheme;
            bool firstInit = !themeInitialized;
            darkTheme = newDarkTheme;
            themeInitialized = true;

            InitColors(darkTheme);

            if (setDPI)
                ControlHelper.Resize(this);

            if (changed || firstInit || forceAdjust)
            {
                DwmSetWindowAttribute(Handle, 20, new[] { darkTheme ? 1 : 0 }, 4);
                SetPreferredAppMode(darkTheme ? 1 : 0);
                SetWindowTheme(Handle, darkTheme ? "DarkMode_Explorer" : "Explorer", null);
                ControlHelper.Adjust(this, changed);
                this.Invalidate(true);
            }

            return changed;
        }

        public static void RefreshAllThemes()
        {
            LoadStyleTokens();
            foreach (Form frm in Application.OpenForms.Cast<Form>().ToList())
            {
                if (frm is RForm rform && !rform.IsDisposed)
                {
                    rform.InitTheme(setDPI: false, forceAdjust: true);
                    rform.Refresh();
                }
            }
            Program.settingsForm?.InitContextMenuTheme();
        }

    }
}
