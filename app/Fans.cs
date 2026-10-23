using GHelper.Fan;
using GHelper.Gpu.NVidia;
using GHelper.Helpers;
using GHelper.Mode;
using GHelper.UI;
using GHelper.USB;
using PawnIO;
using System.Diagnostics;
using System.Windows.Forms.DataVisualization.Charting;

namespace GHelper
{
    public partial class Fans : RForm
    {

        int curIndex = -1;
        DataPoint? curPoint = null;
        int _kbIndex = 0;
        int _chartTabDirection = 0;

        Series seriesCPU;
        Series seriesGPU;
        Series seriesMid;
        Series seriesXGM;

        static bool gpuVisible = true;
        static bool fanRpm = true;

        static readonly Font _axisFont = new Font("Arial", 7F);

        const int tempMin = 0;
        const int tempMax = 110;

        const int fansMax = 100;

        NvidiaGpuControl? nvControl = null;
        ModeControl modeControl = Program.modeControl;

        FanSensorControl fanSensorControl;

        static int gpuPowerBase = 0;
        static bool isGPUPower => gpuPowerBase > 0;
        static bool clampFanDots = AppConfig.IsClampFanDots();

        public Fans()
        {

            InitializeComponent();

            panelFans.AutoScroll = true;
            panelSliders.AutoScroll = true;
            AutoScroll = true;

            fanSensorControl = new FanSensorControl(this);

            //float dpi = ControlHelper.GetDpiScale(this).Value;
            //comboModes.Size = new Size(comboModes.Width, (int)dpi * 18);
            comboModes.ClientSize = new Size(comboModes.Width, comboModes.Height - 4);

            Text = Properties.Strings.FansAndPower;
            labelPowerLimits.Text = Properties.Strings.PowerLimits;
            checkApplyPower.Text = Properties.Strings.ApplyPowerLimits;

            labelFans.Text = "BIOS " + Properties.Strings.FanCurves;
            labelBoost.Text = Properties.Strings.CPUBoost;
            buttonReset.Text = Properties.Strings.FactoryDefaults;
            checkApplyFans.Text = Properties.Strings.ApplyFanCurve;

            labelGPU.Text = Properties.Strings.GPUSettings;

            labelGPUCoreTitle.Text = Properties.Strings.GPUCoreClockOffset;
            labelGPUMemoryTitle.Text = Properties.Strings.GPUMemoryClockOffset;
            labelGPUBoostTitle.Text = Properties.Strings.GPUBoost;
            labelGPUTempTitle.Text = Properties.Strings.GPUTempTarget;
            labelGPUPowerTitle.Text = Properties.Strings.GPUPower;

            labelRisky.Text = Properties.Strings.UndervoltingRisky;
            buttonApplyAdvanced.Text = Properties.Strings.Apply;
            checkApplyUV.Text = Properties.Strings.AutoApply;

            buttonCalibrate.Text = Properties.Strings.Calibrate;

            checkFanClamp.Text = Properties.Strings.ClampToGrid;
            labelHysteresisUp.Text = Properties.Strings.HysteresisUp;
            labelHysteresisDown.Text = Properties.Strings.HysteresisDown;
            buttonReadLimits.Text = Properties.Strings.ReadLimits;
            buttonDownload.Text = Properties.Strings.InstallPawnIODriver;

            InitTheme(true);

            labelTip.Visible = false;
            labelTip.BackColor = Color.Transparent;

            seriesCPU = chartCPU.Series.Add("CPU");
            seriesGPU = chartGPU.Series.Add("GPU");
            seriesMid = chartMid.Series.Add("Mid");
            seriesXGM = chartXGM.Series.Add("XGM");

            seriesCPU.Color = colorStandard;
            seriesGPU.Color = colorTurbo;
            seriesMid.Color = colorEco;
            seriesXGM.Color = Color.Orange;

            chartCPU.MouseMove += (sender, e) => ChartCPU_MouseMove(sender, e, AsusFan.CPU);
            chartCPU.MouseUp += ChartCPU_MouseUp;
            chartCPU.MouseLeave += ChartCPU_MouseLeave;

            chartGPU.MouseMove += (sender, e) => ChartCPU_MouseMove(sender, e, AsusFan.GPU);
            chartGPU.MouseUp += ChartCPU_MouseUp;
            chartGPU.MouseLeave += ChartCPU_MouseLeave;

            chartMid.MouseMove += (sender, e) => ChartCPU_MouseMove(sender, e, AsusFan.Mid);
            chartMid.MouseUp += ChartCPU_MouseUp;
            chartMid.MouseLeave += ChartCPU_MouseLeave;

            chartXGM.MouseMove += (sender, e) => ChartCPU_MouseMove(sender, e, AsusFan.XGM);
            chartXGM.MouseUp += ChartCPU_MouseUp;
            chartXGM.MouseLeave += ChartCPU_MouseLeave;

            chartCPU.MouseClick += ChartCPU_MouseClick;
            chartGPU.MouseClick += ChartCPU_MouseClick;
            chartMid.MouseClick += ChartCPU_MouseClick;
            chartXGM.MouseClick += ChartCPU_MouseClick;

            chartCPU.TabStop = true;
            chartGPU.TabStop = true;
            chartMid.TabStop = true;
            chartXGM.TabStop = true;

            chartCPU.PreviewKeyDown += Chart_PreviewKeyDown;
            chartGPU.PreviewKeyDown += Chart_PreviewKeyDown;
            chartMid.PreviewKeyDown += Chart_PreviewKeyDown;
            chartXGM.PreviewKeyDown += Chart_PreviewKeyDown;

            chartCPU.KeyDown += (s, e) => Chart_KeyDown(s, e, AsusFan.CPU);
            chartGPU.KeyDown += (s, e) => Chart_KeyDown(s, e, AsusFan.GPU);
            chartMid.KeyDown += (s, e) => Chart_KeyDown(s, e, AsusFan.Mid);
            chartXGM.KeyDown += (s, e) => Chart_KeyDown(s, e, AsusFan.XGM);

            chartCPU.GotFocus += (s, e) => Chart_GotFocus(s, AsusFan.CPU);
            chartGPU.GotFocus += (s, e) => Chart_GotFocus(s, AsusFan.GPU);
            chartMid.GotFocus += (s, e) => Chart_GotFocus(s, AsusFan.Mid);
            chartXGM.GotFocus += (s, e) => Chart_GotFocus(s, AsusFan.XGM);

            chartCPU.LostFocus += Chart_LostFocus;
            chartGPU.LostFocus += Chart_LostFocus;
            chartMid.LostFocus += Chart_LostFocus;
            chartXGM.LostFocus += Chart_LostFocus;

            buttonReset.Click += ButtonReset_Click;

            trackTotal.Maximum = AsusACPI.MaxTotal;
            trackTotal.Minimum = AsusACPI.MinTotal;

            trackSlow.Maximum = AsusACPI.MaxTotal;
            trackSlow.Minimum = AsusACPI.MinTotal;

            trackCPU.Maximum = AsusACPI.MaxCPU;
            trackCPU.Minimum = AsusACPI.MinCPU;

            trackFast.Maximum = AsusACPI.MaxTotal;
            trackFast.Minimum = AsusACPI.MinTotal;

            trackCrossLoad.Maximum = AsusACPI.MaxCrossLoad;
            trackCrossLoad.Minimum = AsusACPI.MinCrossLoad;

            trackGPUtoCPU.Maximum = AsusACPI.MaxGPUtoCPU;
            trackGPUtoCPU.Minimum = AsusACPI.MinGPUtoCPU;

            trackCPUTemp.Maximum = AsusACPI.MaxCPUTemp;
            trackCPUTemp.Minimum = AsusACPI.MinCPUTemp;

            trackTotal.Scroll += TrackTotal_Scroll;
            trackSlow.Scroll += TrackSlow_Scroll;
            trackFast.Scroll += TrackFast_Scroll;
            trackCPU.Scroll += TrackCPU_Scroll;
            trackCrossLoad.Scroll += TrackCross_Scroll;
            trackGPUtoCPU.Scroll += TrackCross_Scroll;
            trackCPUTemp.Scroll += TrackCross_Scroll;

            trackFast.MouseUp += TrackPower_MouseUp;
            trackCPU.MouseUp += TrackPower_MouseUp;
            trackTotal.MouseUp += TrackPower_MouseUp;
            trackSlow.MouseUp += TrackPower_MouseUp;
            trackCrossLoad.MouseUp += TrackPower_MouseUp;
            trackGPUtoCPU.MouseUp += TrackPower_MouseUp;
            trackCPUTemp.MouseUp += TrackPower_MouseUp;

            trackFast.KeyUp += TrackPower_KeyUp;
            trackCPU.KeyUp += TrackPower_KeyUp;
            trackTotal.KeyUp += TrackPower_KeyUp;
            trackSlow.KeyUp += TrackPower_KeyUp;
            trackCrossLoad.KeyUp += TrackPower_KeyUp;
            trackGPUtoCPU.KeyUp += TrackPower_KeyUp;
            trackCPUTemp.KeyUp += TrackPower_KeyUp;

            checkApplyFans.Click += CheckApplyFans_Click;
            checkApplyPower.Click += CheckApplyPower_Click;

            trackGPUClockLimit.Minimum = NvidiaGpuControl.MinClockLimit;
            trackGPUClockLimit.Maximum = NvidiaGpuControl.MaxClockLimit;

            trackGPUCore.Minimum = NvidiaGpuControl.MinCoreOffset;
            trackGPUCore.Maximum = NvidiaGpuControl.MaxCoreOffset;

            trackGPUMemory.Minimum = NvidiaGpuControl.MinMemoryOffset;
            trackGPUMemory.Maximum = NvidiaGpuControl.MaxMemoryOffset;

            trackGPUBoost.Minimum = AsusACPI.MinGPUBoost;
            trackGPUBoost.Maximum = AsusACPI.MaxGPUBoost;

            trackGPUTemp.Minimum = AsusACPI.MinGPUTemp;
            trackGPUTemp.Maximum = AsusACPI.MaxGPUTemp;

            trackGPUPower.Minimum = AsusACPI.MinGPUPower;
            trackGPUPower.Maximum = AsusACPI.MaxGPUPower;

            trackGPUClockLimit.Scroll += trackGPUClockLimit_Scroll;
            trackGPUCore.Scroll += trackGPU_Scroll;
            trackGPUMemory.Scroll += trackGPU_Scroll;

            trackGPUBoost.Scroll += trackGPUPower_Scroll;
            trackGPUTemp.Scroll += trackGPUPower_Scroll;
            trackGPUPower.Scroll += trackGPUPower_Scroll;

            trackGPUCore.MouseUp += TrackGPUClocks_MouseUp;
            trackGPUMemory.MouseUp += TrackGPUClocks_MouseUp;
            trackGPUClockLimit.MouseUp += TrackGPUClocks_MouseUp;

            trackGPUBoost.MouseUp += TrackGPU_MouseUp;
            trackGPUTemp.MouseUp += TrackGPU_MouseUp;
            trackGPUPower.MouseUp += TrackGPU_MouseUp;

            trackHysteresisUp.Scroll += TrackHysteresis_Scroll;
            trackHysteresisDown.Scroll += TrackHysteresis_Scroll;

            trackHysteresisUp.MouseUp += TrackHysteresis_MouseUp;
            trackHysteresisDown.MouseUp += TrackHysteresis_MouseUp;


            //labelInfo.MaximumSize = new Size(280, 0);
            labelFansResult.Visible = false;


            trackUV.Minimum = CpuInfo.MinCPUUV;
            trackUV.Maximum = CpuInfo.MaxCPUUV;

            trackUViGPU.Minimum = CpuInfo.MinIGPUUV;
            trackUViGPU.Maximum = CpuInfo.MaxIGPUUV;

            trackTemp.Minimum = CpuInfo.MinTemp;
            trackTemp.Maximum = CpuInfo.DefaultTemp;

            comboPowerMode.DropDownStyle = ComboBoxStyle.DropDownList;
            comboPowerMode.DataSource = new BindingSource(PowerNative.powerModes, null);
            comboPowerMode.DisplayMember = "Value";
            comboPowerMode.ValueMember = "Key";

            FillModes(false);
            InitAll();
            InitCPU();

            comboBoost.SelectedValueChanged += ComboBoost_Changed;
            comboPowerMode.SelectedValueChanged += ComboPowerMode_Changed;


            comboModes.SelectionChangeCommitted += ComboModes_SelectedValueChanged;
            comboModes.TextChanged += ComboModes_TextChanged;
            comboModes.KeyPress += ComboModes_KeyPress;

            Shown += Fans_Shown;

            buttonAdd.Click += ButtonAdd_Click;
            buttonRemove.Click += ButtonRemove_Click;
            buttonRename.Click += ButtonRename_Click;


            trackUV.Scroll += TrackUV_Scroll;
            trackUViGPU.Scroll += TrackUV_Scroll;
            trackTemp.Scroll += TrackUV_Scroll;

            buttonApplyAdvanced.Click += ButtonApplyAdvanced_Click;
            buttonReadLimits.Click += ButtonReadLimits_Click;

            buttonCPU.BorderColor = colorStandard;
            buttonGPU.BorderColor = colorTurbo;
            buttonAdvanced.BorderColor = Color.Gray;

            buttonCPU.Click += ButtonCPU_Click;
            buttonGPU.Click += ButtonGPU_Click;
            buttonAdvanced.Click += ButtonAdvanced_Click;

            checkApplyUV.Click += CheckApplyUV_Click;

            buttonCalibrate.Click += ButtonCalibrate_Click;
            buttonTestXGM.Click += ButtonTestXGM_Click;

            buttonDownload.Click += ButtonDownload_Click;

            checkFanClamp.Checked = clampFanDots;
            checkFanClamp.Click += CheckFanClamp_Click;

            ToggleNavigation(0);

            if (!Program.acpi.IsSupported(AsusACPI.DevsCPUFanCurve)) buttonCalibrate.Visible = false;

            gpuPowerBase = Program.acpi.DeviceGet(AsusACPI.GPU_BASE);
            panelGPUPower.Visible = isGPUPower;

            FormClosed += Fans_FormClosed;
            Activated  += (_, _) => VisualiseAdvanced();

            trackUV.AccessibleName = labelLeftUV.Text;
            trackUViGPU.AccessibleName = labelLeftUViGPU.Text;
            trackTemp.AccessibleName = labelLeftTemp.Text;
            trackGPUCore.AccessibleName = labelGPUCoreTitle.Text;
            trackGPUMemory.AccessibleName = labelGPUMemoryTitle.Text;
            trackGPUBoost.AccessibleName = labelGPUBoostTitle.Text;
            trackGPUTemp.AccessibleName = labelGPUTempTitle.Text;
            trackGPUPower.AccessibleName = labelGPUPowerTitle.Text;
            trackGPUClockLimit.AccessibleName = labelGPUClockLimitTitle.Text;
            trackHysteresisUp.AccessibleName = labelHysteresisUp.Text;
            trackHysteresisDown.AccessibleName = labelHysteresisDown.Text;

            chartCPU.AccessibleName = "CPU fan curve";
            chartGPU.AccessibleName = "GPU fan curve";
            chartMid.AccessibleName = "Mid fan curve";
            chartXGM.AccessibleName = "XG Mobile fan curve";

            ConfigureSliderErgonomics();
            ConfigureChartAndModeMenus();
        }

        private void CheckFanClamp_Click(object? sender, EventArgs e)
        {
            clampFanDots = checkFanClamp.Checked;
            AppConfig.Set("fan_clamp", clampFanDots ? 1 : 0);
        }

        private async void ButtonDownload_Click(object? sender, EventArgs e)
        {
            buttonDownload.Enabled = false;
            string originalText = buttonDownload.Text;
            buttonDownload.Text = "Installing PawnIO Driver...";

            bool success = await PawnInstaller.InstallAsync(msg =>
            {
                if (IsHandleCreated && !IsDisposed)
                    BeginInvoke(() => { buttonDownload.Text = msg; });
            });

            if (success)
            {
                Program.toast?.RunToast("PawnIO Installed: CPU Undervolting Unlocked!");
                VisualiseAdvanced();
                Program.settingsForm?.SetContextMenu();
            }
            else
            {
                buttonDownload.Text = originalText;
                buttonDownload.Enabled = true;
                Process.Start(new ProcessStartInfo("https://pawnio.eu/") { UseShellExecute = true });
            }
        }

        private void ButtonCalibrate_Click(object? sender, EventArgs e)
        {
            buttonCalibrate.Enabled = false;
            fanSensorControl.StartCalibration();
        }

        private async void ButtonTestXGM_Click(object? sender, EventArgs e)
        {
            buttonTestXGM.Enabled = false;
            buttonTestXGM.Text = "Testing...";
            try
            {
                await XGM.TestFan(8);
            }
            catch (Exception ex)
            {
                Logger.WriteLine(ex.ToString());
            }
            finally
            {
                buttonTestXGM.Text = "Test XGM";
                buttonTestXGM.Enabled = true;
            }
        }

        private void ChartCPU_MouseClick(object? sender, MouseEventArgs e)
        {
            if (sender is null) return;
            Chart chart = (Chart)sender;

            if (e.Button == MouseButtons.Right)
            {
                AsusFan device = chart == chartGPU ? AsusFan.GPU
                               : chart == chartMid ? AsusFan.Mid
                               : chart == chartXGM ? AsusFan.XGM
                               : AsusFan.CPU;
                ShowChartContextMenu(chart, device, e.Location);
                return;
            }

            HitTestResult result = chart.HitTest(e.X, e.Y);

            if ((result.ChartElementType == ChartElementType.AxisLabels || result.ChartElementType == ChartElementType.Axis) && result.Axis == chart.ChartAreas[0].AxisY)
            {
                fanRpm = !fanRpm;
                SetAxis(chartCPU, AsusFan.CPU);
                SetAxis(chartGPU, AsusFan.GPU);
                if (chartMid.Visible) SetAxis(chartMid, AsusFan.Mid);
                if (chartXGM.Visible) SetAxis(chartXGM, AsusFan.XGM);
            }

        }

        private void Fans_FormClosed(object? sender, FormClosedEventArgs e)
        {
            fanSensorControl.StopCalibration();
            //Because windows charts seem to eat a lot of memory :(
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
        }

        private void CheckApplyUV_Click(object? sender, EventArgs e)
        {
            AppConfig.SetMode("auto_uv", checkApplyUV.Checked ? 1 : 0);
            modeControl.AutoRyzen();
        }

        public void InitAll()
        {
            InitMode();
            InitFans();
            InitPower();
            InitPowerPlan();
            InitUV();
            InitGPU();
        }

        public void InitCPU()
        {
            string name = CpuInfo.Name;
            if (name.Length > 0) Text = Properties.Strings.FansAndPower + " - " + name;
        }

        public void ToggleNavigation(int index = 0)
        {

            SuspendLayout();

            buttonCPU.Activated = false;
            buttonGPU.Activated = false;
            buttonAdvanced.Activated = false;

            panelPower.Visible = false;
            panelGPU.Visible = false;
            panelAdvanced.Visible = false;

            switch (index)
            {
                case 1:
                    buttonGPU.Activated = true;
                    panelGPU.Visible = true;
                    break;
                case 2:
                    buttonAdvanced.Activated = true;
                    panelAdvanced.Visible = true;
                    break;
                default:
                    buttonCPU.Activated = true;
                    panelPower.Visible = true;
                    break;
            }

            ResumeLayout(false);
            PerformLayout();
        }

        private void ButtonAdvanced_Click(object? sender, EventArgs e)
        {
            ToggleNavigation(2);
        }

        private void ButtonGPU_Click(object? sender, EventArgs e)
        {
            ToggleNavigation(1);
        }

        private void ButtonCPU_Click(object? sender, EventArgs e)
        {
            ToggleNavigation(0);
        }

        private void ButtonApplyAdvanced_Click(object? sender, EventArgs e)
        {
            string result = modeControl.SetRyzen(true);
            checkApplyUV.Enabled = true;
            ShowLabelRisky(result);
        }

        private void ButtonReadLimits_Click(object? sender, EventArgs e)
        {
            ShowLabelRisky(modeControl.ReadRyzenLimits());
        }

        private void ShowLabelRisky(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            labelRisky.Text = text;
            labelRisky.Visible = true;
        }

        public void InitUV()
        {

            //if (!ProcessHelper.IsUserAdministrator()) return;

            int cpuUV = Math.Max(trackUV.Minimum, Math.Min(trackUV.Maximum, AppConfig.GetMode("cpu_uv", 0)));
            int igpuUV = Math.Max(trackUViGPU.Minimum, Math.Min(trackUViGPU.Maximum, AppConfig.GetMode("igpu_uv", 0)));

            int temp = AppConfig.GetMode("cpu_temp");
            if (temp < CpuInfo.MinTemp || temp > CpuInfo.DefaultTemp) temp = CpuInfo.DefaultTemp;

            checkApplyUV.Enabled = checkApplyUV.Checked = AppConfig.IsApplyUV();

            trackUV.Value = cpuUV;
            trackUViGPU.Value = igpuUV;
            trackTemp.Value = temp;

            VisualiseAdvanced();

            buttonAdvanced.Visible = CpuInfo.IsAMD;

        }

        private void VisualiseAdvanced()
        {
            bool available = ModeControl.IsPawnAvailable();
            bool installed = available || ModeControl.IsPawnInstalled();

            panelPawnIO.Visible   = installed;
            panelDownload.Visible = !installed;

            if (installed)
            {
                panelTitleAdvanced.Visible = CpuInfo.IsSupportedUV();
                labelRisky.Visible         = CpuInfo.IsSupportedUV();
                panelUV.Visible            = CpuInfo.IsSupportedUV();
                panelUViGPU.Visible        = CpuInfo.IsSupportedUViGPU();
            }

            labelUV.Text     = trackUV.Value.ToString();
            labelUViGPU.Text = trackUViGPU.Value.ToString();

            labelTemp.Text = (trackTemp.Value < CpuInfo.DefaultTemp) ? TempHelper.FormatTemp(trackTemp.Value) : "Default";
        }

        private void AdvancedScroll()
        {
            AppConfig.SetMode("auto_uv", 0);
            checkApplyUV.Enabled = checkApplyUV.Checked = false;

            VisualiseAdvanced();

            AppConfig.SetMode("cpu_temp", trackTemp.Value);
            AppConfig.SetMode("cpu_uv", trackUV.Value);
            AppConfig.SetMode("igpu_uv", trackUViGPU.Value);
        }


        private void TrackUV_Scroll(object? sender, EventArgs e)
        {
            AdvancedScroll();
        }

        private void ComboModes_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13) RenameToggle();
        }

        private void ComboModes_TextChanged(object? sender, EventArgs e)
        {
            if (comboModes.DropDownStyle == ComboBoxStyle.DropDownList) return;
            if (!Modes.IsCurrentCustom()) return;
            AppConfig.SetMode("mode_name", comboModes.Text);
        }

        private void RenameToggle()
        {
            if (comboModes.DropDownStyle == ComboBoxStyle.DropDownList)
                comboModes.DropDownStyle = ComboBoxStyle.DropDown;
            else
            {
                var mode = Modes.GetCurrent();
                FillModes();
                comboModes.SelectedValue = mode;
            }
        }

        private void ButtonRename_Click(object? sender, EventArgs e)
        {
            RenameToggle();
        }

        private void ButtonRemove_Click(object? sender, EventArgs e)
        {
            int mode = Modes.GetCurrent();
            if (!Modes.IsCurrentCustom()) return;

            Modes.Remove(mode);
            FillModes();
            modeControl.SetPerformanceMode(AsusACPI.PerformanceBalanced);

        }

        private void FillModes(bool contextMenu = true)
        {
            comboModes.DropDownStyle = ComboBoxStyle.DropDownList;
            comboModes.DataSource = new BindingSource(Modes.GetDictonary(), null);
            comboModes.DisplayMember = "Value";
            comboModes.ValueMember = "Key";
            if (contextMenu) Program.settingsForm.SetContextMenu();
        }

        private void ButtonAdd_Click(object? sender, EventArgs e)
        {
            int mode = Modes.Add();
            FillModes();
            modeControl.SetPerformanceMode(mode);
        }

        public void InitMode()
        {
            int mode = Modes.GetCurrent();
            comboModes.SelectedValue = mode;
            buttonRename.Visible = buttonRemove.Visible = Modes.IsCurrentCustom();
        }

        private void ComboModes_SelectedValueChanged(object? sender, EventArgs e)
        {
            var selectedMode = comboModes.SelectedValue;

            if (selectedMode == null) return;
            if ((int)selectedMode == Modes.GetCurrent()) return;

            Debug.WriteLine(selectedMode);

            modeControl.SetPerformanceMode((int)selectedMode);
        }

        private void TrackGPU_MouseUp(object? sender, MouseEventArgs e)
        {
            modeControl.SetGPUPower();
        }

        private void TrackGPUClocks_MouseUp(object? sender, MouseEventArgs e)
        {
            modeControl.SetGPUClocks(true);
        }

        private void InitGPUPower()
        {
            if (!isGPUPower) return;

            int maxGPUPower = NvidiaSmi.GetMaxGPUPower();
            if (maxGPUPower > 0)
            {
                AsusACPI.MaxGPUPower = maxGPUPower - gpuPowerBase - AsusACPI.MaxGPUBoost;
                trackGPUPower.Minimum = AsusACPI.MinGPUPower;
                trackGPUPower.Maximum = AsusACPI.MaxGPUPower;
            }

            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200));
                int gpuPowerVar = Program.acpi.DeviceGet(AsusACPI.GPU_POWER);
                Logger.WriteLine($"ReadGPUPower ({Modes.GetCurrentBase()}): {gpuPowerBase} + {gpuPowerVar}");

                int gpu_power = AppConfig.GetMode("gpu_power");
                if (gpu_power < 0) gpu_power = (gpuPowerVar >= 0) ? gpuPowerVar : AsusACPI.MaxGPUPower;

                Invoke(delegate
                {
                    trackGPUPower.Value = Math.Max(Math.Min(gpu_power, AsusACPI.MaxGPUPower), AsusACPI.MinGPUPower);
                    VisualiseGPUSettings();
                });
            });
        }

        public void InitGPU()
        {
            Task.Run(() =>
            {
                if (Program.acpi.DeviceGet(AsusACPI.GPUEco) == 1)
                {
                    Invoke(delegate { gpuVisible = buttonGPU.Visible = false; });
                    return;
                }

                if (HardwareControl.GpuControl is null || !HardwareControl.GpuControl.IsValid) HardwareControl.RecreateGpuControl();

                if (HardwareControl.GpuControl is not NvidiaGpuControl nv)
                {
                    Invoke(delegate { gpuVisible = buttonGPU.Visible = false; });
                    return;
                }

                nvControl = nv;

                try
                {
                    int gpu_boost = AppConfig.GetMode("gpu_boost");
                    int gpu_temp = AppConfig.GetMode("gpu_temp");

                    int core = AppConfig.GetMode("gpu_core");
                    int memory = AppConfig.GetMode("gpu_memory");
                    int clock_limit = AppConfig.GetMode("gpu_clock_limit");

                    if (gpu_boost < 0) gpu_boost = AsusACPI.MaxGPUBoost;
                    if (gpu_temp < 0) gpu_temp = AsusACPI.MaxGPUTemp;

                    if (core == -1) core = 0;
                    if (memory == -1) memory = 0;
                    if (clock_limit == -1) clock_limit = NvidiaGpuControl.MaxClockLimit;

                    string? gpuName = null;

                    if (nvControl is not null)
                    {
                        if (nvControl.GetClocks(out int current_core, out int current_memory))
                        {
                            core = current_core;
                            memory = current_memory;
                        }

                        int _clockLimit = nvControl.GetMaxGPUCLock();

                        if (_clockLimit == 0) clock_limit = NvidiaGpuControl.MaxClockLimit;
                        else if (_clockLimit > 0) clock_limit = _clockLimit;

                        try { gpuName = nvControl.FullName; } catch { }
                    }

                    bool boostVisible = Program.acpi.IsSupported(AsusACPI.PPT_GPUC0);
                    bool tempVisible = Program.acpi.IsSupported(AsusACPI.PPT_GPUC2);

                    Invoke(delegate
                    {
                        gpuVisible = buttonGPU.Visible = true;
                        if (gpuName is not null) labelGPU.Text = gpuName;

                        trackGPUClockLimit.Value = Math.Max(Math.Min(clock_limit, NvidiaGpuControl.MaxClockLimit), NvidiaGpuControl.MinClockLimit);

                        trackGPUCore.Value = Math.Max(Math.Min(core, NvidiaGpuControl.MaxCoreOffset), NvidiaGpuControl.MinCoreOffset);
                        trackGPUMemory.Value = Math.Max(Math.Min(memory, NvidiaGpuControl.MaxMemoryOffset), NvidiaGpuControl.MinMemoryOffset);

                        trackGPUBoost.Value = Math.Max(Math.Min(gpu_boost, AsusACPI.MaxGPUBoost), AsusACPI.MinGPUBoost);
                        trackGPUTemp.Value = Math.Max(Math.Min(gpu_temp, AsusACPI.MaxGPUTemp), AsusACPI.MinGPUTemp);

                        panelGPUBoost.Visible = boostVisible;
                        panelGPUTemp.Visible = tempVisible;

                        VisualiseGPUSettings();

                        InitGPUPower();
                    });
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(ex.ToString());
                    try { Invoke(delegate { gpuVisible = buttonGPU.Visible = false; }); } catch { }
                }
            });
        }

        private void VisualiseGPUSettings()
        {
            labelGPUCore.Text = $"{trackGPUCore.Value} MHz";
            labelGPUMemory.Text = $"{trackGPUMemory.Value} MHz";

            labelGPUBoost.Text = $"{trackGPUBoost.Value}W";
            labelGPUTemp.Text = TempHelper.FormatTemp(trackGPUTemp.Value);

            if (trackGPUClockLimit.Value >= NvidiaGpuControl.MaxClockLimit)
                labelGPUClockLimit.Text = "Default";
            else
                labelGPUClockLimit.Text = $"{trackGPUClockLimit.Value} MHz";

            labelGPUPower.Text = (gpuPowerBase + trackGPUPower.Value) + "W";

        }

        private static readonly string[] HysteresisLabels = { Properties.Strings.VeryLow, Properties.Strings.Low, Properties.Strings.Medium, Properties.Strings.High, Properties.Strings.VeryHigh };

        private void VisualiseHysteresis()
        {
            labelHysteresisUpValue.Text = HysteresisLabels[trackHysteresisUp.Value - 1];
            labelHysteresisDownValue.Text = HysteresisLabels[trackHysteresisDown.Value - 1];
        }

        private void InitHysteresis()
        {
            var defaults = Program.acpi.GetFanHysteresis();
            if (defaults.up < 0 || defaults.down < 0)
            {
                panelHysteresis.Visible = false;
                return;
            }

            panelHysteresis.Visible = true;

            int up = AppConfig.GetMode("hysteresis_up");
            int down = AppConfig.GetMode("hysteresis_down");

            if (up < 0) up = defaults.up > 0 ? defaults.up : 3;
            if (down < 0) down = defaults.down > 0 ? defaults.down : 3;

            trackHysteresisUp.Value = Math.Clamp(up, trackHysteresisUp.Minimum, trackHysteresisUp.Maximum);
            trackHysteresisDown.Value = Math.Clamp(down, trackHysteresisDown.Minimum, trackHysteresisDown.Maximum);
            VisualiseHysteresis();
        }

        private void TrackHysteresis_Scroll(object? sender, EventArgs e)
        {
            AppConfig.SetMode("hysteresis_up", trackHysteresisUp.Value);
            AppConfig.SetMode("hysteresis_down", trackHysteresisDown.Value);
            VisualiseHysteresis();
        }

        private void TrackHysteresis_MouseUp(object? sender, MouseEventArgs e)
        {
            Program.acpi.SetFanHysteresis(trackHysteresisUp.Value, trackHysteresisDown.Value);
        }


        private void trackGPUClockLimit_Scroll(object? sender, EventArgs e)
        {

            int maxClock = (int)Math.Round((float)trackGPUClockLimit.Value / 5) * 5;

            trackGPUClockLimit.Value = maxClock;
            AppConfig.SetMode("gpu_clock_limit", maxClock);
            VisualiseGPUSettings();
        }

        private void trackGPU_Scroll(object? sender, EventArgs e)
        {
            if (sender is null) return;
            TrackBar track = (TrackBar)sender;
            track.Value = (int)Math.Round((float)track.Value / 5) * 5;

            AppConfig.SetMode("gpu_core", trackGPUCore.Value);
            AppConfig.SetMode("gpu_memory", trackGPUMemory.Value);


            VisualiseGPUSettings();

        }

        private void trackGPUPower_Scroll(object? sender, EventArgs e)
        {
            AppConfig.SetMode("gpu_boost", trackGPUBoost.Value);
            AppConfig.SetMode("gpu_temp", trackGPUTemp.Value);

            if (isGPUPower) AppConfig.SetMode("gpu_power", trackGPUPower.Value);

            VisualiseGPUSettings();
        }

        static string ChartYLabel(int percentage, AsusFan device, string unit = "")
        {
            if (percentage == 0) return "OFF";

            int Min = FanSensorControl.GetFanMin(device);
            int Max = FanSensorControl.GetFanMax(device);

            if (fanRpm)
                return (200 * Math.Floor((float)(Min * 100 + (Max - Min) * percentage) / 200)).ToString() + unit;
            else
                return percentage + "%";
        }

        void SetAxis(Chart chart, AsusFan device)
        {

            chart.ChartAreas[0].AxisY.CustomLabels.Clear();

            for (int i = 0; i <= fansMax; i += 10)
            {
                chart.ChartAreas[0].AxisY.CustomLabels.Add(i - 2, i + 2, ChartYLabel(i, device));
            }

            //chart.ChartAreas[0].AxisY.CustomLabels.Add(fansMax -2, fansMax + 2, Properties.Strings.RPM);
            chart.ChartAreas[0].AxisY.Interval = 10;

            chart.ChartAreas[0].AxisX.CustomLabels.Clear();
            for (int i = tempMin; i <= tempMax; i += 10)
            {
                string xLabel = TempHelper.IsFahrenheit
                    ? Math.Round(TempHelper.CelsiusToFahrenheit(i)).ToString()
                    : i.ToString();
                chart.ChartAreas[0].AxisX.CustomLabels.Add(i - 4.5, i + 4.5, xLabel);
            }
        }

        void SetChart(Chart chart, AsusFan device)
        {

            string title = "";
            string scale = TempHelper.IsFahrenheit ? ", RPM/°F" : ", RPM/°C";

            switch (device)
            {
                case AsusFan.CPU:
                    title = Properties.Strings.FanProfileCPU + scale;
                    break;
                case AsusFan.GPU:
                    title = Properties.Strings.FanProfileGPU + scale;
                    break;
                case AsusFan.Mid:
                    title = Properties.Strings.FanProfileMid + scale;
                    break;
                case AsusFan.XGM:
                    string xgmInfo = "";
                    if (HardwareControl.xgmTemp > 0)
                    {
                        string tempStr = TempHelper.FormatTemp((double)HardwareControl.xgmTemp);


                        string fanStr = HardwareControl.xgmFan != null ? $", {HardwareControl.xgmFan}" : "";
                        xgmInfo = $" ({tempStr}{fanStr})";
                    }
                    title = "XG Mobile" + xgmInfo + scale;
                    break;
            }

            chart.Titles[0].Text = title;

            chart.ChartAreas[0].AxisX.Minimum = tempMin;
            chart.ChartAreas[0].AxisX.Maximum = tempMax;
            chart.ChartAreas[0].AxisX.Interval = 10;

            chart.ChartAreas[0].AxisY.Minimum = 0;
            chart.ChartAreas[0].AxisY.Maximum = fansMax;

            chart.ChartAreas[0].AxisY.LabelStyle.Font = _axisFont;
            chart.ChartAreas[0].AxisX.LabelStyle.Font = _axisFont;
            chart.ChartAreas[0].AxisX.LabelStyle.Angle = 0;

            chart.ChartAreas[0].AxisX.MajorGrid.LineColor = chartGrid;
            chart.ChartAreas[0].AxisY.MajorGrid.LineColor = chartGrid;
            chart.ChartAreas[0].AxisX.LineColor = chartGrid;
            chart.ChartAreas[0].AxisY.LineColor = chartGrid;

            SetAxis(chart, device);

            if (chart.Legends.Count > 0)
                chart.Legends[0].Enabled = false;

        }

        public void FormPosition()
        {
            int maxWorkingHeight = (Screen.FromControl(this)?.WorkingArea.Height ?? 1000) - 50;
            if (Height > maxWorkingHeight)
            {
                Height = maxWorkingHeight;
            }

            if (Height > Program.settingsForm.Height)
            {
                Top = Math.Max(0, Program.settingsForm.Top + Program.settingsForm.Height - Height);
            }
            else
            {
                Size = MinimumSize = new Size(0, Program.settingsForm.Height);
                Height = Program.settingsForm.Height;
                Top = Program.settingsForm.Top;
            }

            Left = Math.Max(0, Program.settingsForm.Left - Width - 5);
        }

        private void Fans_Shown(object? sender, EventArgs e)
        {
            FormPosition();
            UpdateSensors();
        }


        private void TrackPower_MouseUp(object? sender, MouseEventArgs e)
        {
            Task.Run(() =>
            {
                modeControl.AutoPower(true);
            });
        }


        private void TrackPower_KeyUp(object? sender, KeyEventArgs e)
        {
            Task.Run(() =>
            {
                modeControl.AutoPower(true);
            });
        }

        public void InitPowerPlan()
        {
            int boost = PowerNative.GetCPUBoost();
            if (boost >= 0)
                comboBoost.SelectedIndex = Math.Min(boost, comboBoost.Items.Count - 1);

            string powerMode = PowerNative.GetPowerMode();
            bool batterySaver = PowerNative.GetBatterySaverStatus();

            comboPowerMode.Enabled = !batterySaver;

            if (batterySaver)
                comboPowerMode.SelectedIndex = 0;
            else
                comboPowerMode.SelectedValue = powerMode;

        }

        private void ComboPowerMode_Changed(object? sender, EventArgs e)
        {
            string powerMode = (string)comboPowerMode.SelectedValue;
            PowerNative.SetPowerMode(powerMode);

            if (PowerNative.GetDefaultPowerMode(Modes.GetCurrentBase()) != powerMode)
                AppConfig.SetMode("powermode", powerMode);
            else
                AppConfig.RemoveMode("powermode");
        }

        private void ComboBoost_Changed(object? sender, EventArgs e)
        {
            if (AppConfig.GetMode("auto_boost") != comboBoost.SelectedIndex)
            {
                PowerNative.SetCPUBoost(comboBoost.SelectedIndex);
            }
            AppConfig.SetMode("auto_boost", comboBoost.SelectedIndex);
        }

        private void CheckApplyPower_Click(object? sender, EventArgs e)
        {
            if (sender is null) return;
            CheckBox chk = (CheckBox)sender;

            AppConfig.SetMode("auto_apply_power", chk.Checked ? 1 : 0);
            modeControl.SetPerformanceMode();

        }

        private void CheckApplyFans_Click(object? sender, EventArgs e)
        {
            if (sender is null) return;
            CheckBox chk = (CheckBox)sender;

            AppConfig.SetMode("auto_apply", chk.Checked ? 1 : 0);
            modeControl.SetPerformanceMode();

        }

        public void InitAxis()
        {
            if (this.IsDisposed || !this.IsHandleCreated || this.Text == "") return;

            try
            {
                BeginInvoke(delegate
                {
                    buttonCalibrate.Enabled = true;
                    SetAxis(chartCPU, AsusFan.CPU);
                    SetAxis(chartGPU, AsusFan.GPU);
                    if (chartMid.Visible) SetAxis(chartMid, AsusFan.Mid);
                    if (chartXGM.Visible) SetAxis(chartXGM, AsusFan.XGM);
                });
            }
            catch (ObjectDisposedException) { }
        }

        public void LabelFansResult(string text)
        {
            if (text.Length > 0) Logger.WriteLine(text);

            if (this.IsDisposed || !this.IsHandleCreated || this.Text == "") return;

            try { 
                BeginInvoke(delegate
                {
                    labelFansResult.Text = text;
                    labelFansResult.Visible = (text.Length > 0);
                });
            }
            catch (ObjectDisposedException) { }
        }

        private static void UpdateChartTempLine(Chart chart, double? temp, Color lineColor)
        {
            if (chart == null || chart.ChartAreas.Count == 0) return;
            var stripLines = chart.ChartAreas[0].AxisX.StripLines;
            if (temp == null || temp <= 0)
            {
                if (stripLines.Count > 0) stripLines.Clear();
                return;
            }
            double clampedTemp = Math.Clamp(temp.Value, tempMin, tempMax);
            Color targetColor = Color.FromArgb(180, lineColor);
            if (stripLines.Count == 0)
            {
                StripLine line = new StripLine
                {
                    Interval = 0,
                    IntervalOffset = clampedTemp,
                    StripWidth = 0,
                    BorderColor = targetColor,
                    BorderDashStyle = ChartDashStyle.Dash,
                    BorderWidth = 2
                };
                stripLines.Add(line);
                chart.Invalidate();
            }
            else if (Math.Abs(stripLines[0].IntervalOffset - clampedTemp) >= 0.5 || stripLines[0].BorderColor.ToArgb() != targetColor.ToArgb())
            {
                stripLines[0].IntervalOffset = clampedTemp;
                stripLines[0].BorderColor = targetColor;
                chart.Invalidate();
            }
        }

        private static int? GetInterpolatedFanPercent(Series series, double? temp)
        {
            if (series == null || series.Points.Count == 0 || temp == null || temp <= 0) return null;
            double t = temp.Value;
            if (t <= series.Points[0].XValue) return (int)Math.Round(series.Points[0].YValues[0]);
            for (int i = 0; i < series.Points.Count - 1; i++)
            {
                double x0 = series.Points[i].XValue;
                double x1 = series.Points[i + 1].XValue;
                if (t >= x0 && t <= x1)
                {
                    double y0 = series.Points[i].YValues[0];
                    double y1 = series.Points[i + 1].YValues[0];
                    if (Math.Abs(x1 - x0) < 0.01) return (int)Math.Round(y1);
                    double ratio = (t - x0) / (x1 - x0);
                    return (int)Math.Round(y0 + ratio * (y1 - y0));
                }
            }
            return (int)Math.Round(series.Points[^1].YValues[0]);
        }

        public void UpdateSensors()
        {
            if (this.IsDisposed || !this.IsHandleCreated || this.Text == "" || !this.Visible) return;

            try
            {
                BeginInvoke(delegate
                {
                    string scale = TempHelper.IsFahrenheit ? ", RPM/°F" : ", RPM/°C";

                    if (chartCPU.Titles.Count > 0 && HardwareControl.cpuTemp > 0)
                    {
                        string cpuFanStr = HardwareControl.cpuFan != null ? $", {HardwareControl.cpuFan}" : "";
                        int? targetPct = GetInterpolatedFanPercent(seriesCPU, HardwareControl.cpuTemp);
                        string targetStr = targetPct.HasValue ? $" → {targetPct.Value}%" : "";
                        chartCPU.Titles[0].Text = $"{Properties.Strings.FanProfileCPU} ({TempHelper.FormatTemp((double)HardwareControl.cpuTemp)}{cpuFanStr}{targetStr}){scale}";
                    }
                    UpdateChartTempLine(chartCPU, HardwareControl.cpuTemp, colorStandard);

                    if (chartGPU.Titles.Count > 0 && HardwareControl.gpuTemp > 0)
                    {
                        string gpuFanStr = HardwareControl.gpuFan != null ? $", {HardwareControl.gpuFan}" : "";
                        int? targetPct = GetInterpolatedFanPercent(seriesGPU, HardwareControl.gpuTemp);
                        string targetStr = targetPct.HasValue ? $" → {targetPct.Value}%" : "";
                        chartGPU.Titles[0].Text = $"{Properties.Strings.FanProfileGPU} ({TempHelper.FormatTemp((double)HardwareControl.gpuTemp)}{gpuFanStr}{targetStr}){scale}";
                    }
                    UpdateChartTempLine(chartGPU, HardwareControl.gpuTemp, colorTurbo);

                    if (chartMid.Visible && chartMid.Titles.Count > 0)
                    {
                        double? midActiveTemp = HardwareControl.midTemp ?? HardwareControl.cpuTemp;
                        string midTempStr = midActiveTemp > 0 ? TempHelper.FormatTemp(midActiveTemp.Value) + ", " : "";
                        string midFanStr = HardwareControl.midFan ?? (FanSensorControl.fanRpm ? "0RPM" : "0%");
                        int? targetPct = GetInterpolatedFanPercent(seriesMid, midActiveTemp);
                        string targetStr = targetPct.HasValue ? $" → {targetPct.Value}%" : "";
                        chartMid.Titles[0].Text = $"{Properties.Strings.FanProfileMid} ({midTempStr}{midFanStr}{targetStr}){scale}";
                        UpdateChartTempLine(chartMid, midActiveTemp, colorEco);
                    }

                    if (chartXGM.Visible && chartXGM.Titles.Count > 0)
                    {
                        double? xgmActiveTemp = HardwareControl.xgmTemp ?? HardwareControl.xgmHotspotTemp;
                        string xgmInfo = "";
                        if (xgmActiveTemp > 0 || HardwareControl.xgmFan != null)
                        {
                            string tempStr = HardwareControl.xgmTemp > 0 ? TempHelper.FormatTemp((double)HardwareControl.xgmTemp) : "";
                            if (HardwareControl.xgmHotspotTemp > 0 && HardwareControl.xgmHotspotTemp != (int?)HardwareControl.xgmTemp)
                            {
                                string hotStr = TempHelper.FormatTemp((double)HardwareControl.xgmHotspotTemp.Value);
                                tempStr = tempStr.Length > 0 ? $"{tempStr} / Hot {hotStr}" : $"Hot {hotStr}";
                            }
                            string fanStr = HardwareControl.xgmFan != null ? (tempStr.Length > 0 ? $", {HardwareControl.xgmFan}" : HardwareControl.xgmFan) : "";
                            int? targetPct = GetInterpolatedFanPercent(seriesXGM, xgmActiveTemp);
                            string targetStr = targetPct.HasValue ? $" → {targetPct.Value}%" : "";
                            xgmInfo = $" ({tempStr}{fanStr}{targetStr})";
                        }
                        else if (!Program.acpi.IsXGConnected() && !XGM.IsConnected())
                        {
                            xgmInfo = " (Unplugged - Pre-configuring)";
                        }
                        chartXGM.Titles[0].Text = $"XG Mobile{xgmInfo}{scale}";
                        UpdateChartTempLine(chartXGM, xgmActiveTemp, Color.Orange);
                    }
                });
            }
            catch (ObjectDisposedException) { }
        }


        public void InitPower()
        {

            bool modeA = Program.acpi.IsSupported(AsusACPI.PPT_APUA0) || CpuInfo.IsAMD;
            bool modeB0 = Program.acpi.IsAllAmdPPT();
            bool modeC1 = Program.acpi.IsSupported(AsusACPI.PPT_APUC1);

            bool modeCross = Program.acpi.IsSupported(AsusACPI.PPT_CROSS9F);
            bool modeGPUtoCPU = Program.acpi.IsSupported(AsusACPI.PPT_GPUCPU9C);
            bool modeCPUTemp = Program.acpi.IsSupported(AsusACPI.PPT_TEMP9E);

            panelTotal.Visible = modeA;
            panelCPU.Visible = modeB0;

            panelCrossLoad.Visible = modeCross;
            panelGPUtoCPU.Visible = modeGPUtoCPU;
            panelCPUTemp.Visible = modeCPUTemp;

            panelApplyPower.Visible = panelTitleCPU.Visible = modeA || modeB0 || modeC1;


            // All AMD version has B0 but doesn't have C0 (Nvidia GPU) settings
            if (modeB0)
            {
                labelLeftTotal.Text = "Platform (CPU + GPU)";
                labelLeftCPU.Text = "CPU";
                panelFast.Visible = false;
                panelSlow.Visible = false;
            }
            else
            {
                panelSlow.Visible = modeA;

                if (CpuInfo.IsAMD)
                {
                    labelLeftTotal.Text = "SPL (CPU sustained)";
                    labelLeftSlow.Text = "sPPT (CPU long boost)";
                    labelLeftFast.Text = "fPPT (CPU short boost)";
                    panelFast.Visible = modeC1;

                }
                else
                {
                    labelLeftTotal.Text = "PL1 (CPU sustained)";
                    labelLeftSlow.Text = "PL2 (CPU long boost)";
                    panelFast.Visible = false;
                }

            }

            checkApplyPower.Checked = AppConfig.IsApplyPower();

            int limit_total = AppConfig.GetMode("limit_total", AsusACPI.DefaultTotal);
            int limit_slow = AppConfig.GetMode("limit_slow", limit_total);
            int limit_fast = AppConfig.GetMode("limit_fast", limit_total);
            int limit_cpu = AppConfig.GetMode("limit_cpu", AsusACPI.DefaultCPU);

            if (limit_total > AsusACPI.MaxTotal) limit_total = AsusACPI.MaxTotal;
            if (limit_total < AsusACPI.MinTotal) limit_total = AsusACPI.MinTotal;

            if (limit_cpu > AsusACPI.MaxCPU) limit_cpu = AsusACPI.MaxCPU;
            if (limit_cpu < AsusACPI.MinCPU) limit_cpu = AsusACPI.MinCPU;

            if (limit_slow > AsusACPI.MaxTotal) limit_slow = AsusACPI.MaxTotal;
            if (limit_slow < AsusACPI.MinTotal) limit_slow = AsusACPI.MinTotal;

            if (limit_fast > AsusACPI.MaxTotal) limit_fast = AsusACPI.MaxTotal;
            if (limit_fast < AsusACPI.MinTotal) limit_fast = AsusACPI.MinTotal;

            int limit_crossload = AppConfig.GetMode("limit_crossload", AsusACPI.MaxCrossLoad);
            int limit_gpucpu = AppConfig.GetMode("limit_gpucpu", AsusACPI.MaxGPUtoCPU);
            int limit_cputemp = AppConfig.GetMode("limit_cputemp", AsusACPI.MaxCPUTemp);

            limit_crossload = Math.Clamp(limit_crossload, AsusACPI.MinCrossLoad, AsusACPI.MaxCrossLoad);
            limit_gpucpu = Math.Clamp(limit_gpucpu, AsusACPI.MinGPUtoCPU, AsusACPI.MaxGPUtoCPU);
            limit_cputemp = Math.Clamp(limit_cputemp, AsusACPI.MinCPUTemp, AsusACPI.MaxCPUTemp);

            trackTotal.Value = limit_total;
            trackSlow.Value = limit_slow;
            trackCPU.Value = limit_cpu;
            trackFast.Value = limit_fast;

            trackCrossLoad.Value = limit_crossload;
            trackGPUtoCPU.Value = limit_gpucpu;
            trackCPUTemp.Value = limit_cputemp;

            trackTotal.AccessibleName = labelLeftTotal.Text;
            trackSlow.AccessibleName = labelLeftSlow.Text;
            trackFast.AccessibleName = labelLeftFast.Text;
            trackCPU.AccessibleName = labelLeftCPU.Text;
            trackCrossLoad.AccessibleName = labelLeftCrossLoad.Text;
            trackGPUtoCPU.AccessibleName = labelLeftGPUtoCPU.Text;
            trackCPUTemp.AccessibleName = labelLeftCPUTemp.Text;

            SavePower();

        }

        private void SavePower()
        {
            labelTotal.Text = trackTotal.Value.ToString() + "W";
            labelSlow.Text = trackSlow.Value.ToString() + "W";
            labelCPU.Text = trackCPU.Value.ToString() + "W";
            labelFast.Text = trackFast.Value.ToString() + "W";

            labelCrossLoad.Text = trackCrossLoad.Value.ToString() + "W";
            labelGPUtoCPU.Text = trackGPUtoCPU.Value.ToString() + "W";
            labelCPUTemp.Text = TempHelper.FormatTemp(trackCPUTemp.Value);

            AppConfig.SetMode("limit_total", trackTotal.Value);
            AppConfig.SetMode("limit_slow", trackSlow.Value);
            AppConfig.SetMode("limit_cpu", trackCPU.Value);
            AppConfig.SetMode("limit_fast", trackFast.Value);

            if (Program.acpi.IsSupported(AsusACPI.PPT_CROSS9F)) AppConfig.SetMode("limit_crossload", trackCrossLoad.Value);
            if (Program.acpi.IsSupported(AsusACPI.PPT_GPUCPU9C)) AppConfig.SetMode("limit_gpucpu", trackGPUtoCPU.Value);
            if (Program.acpi.IsSupported(AsusACPI.PPT_TEMP9E)) AppConfig.SetMode("limit_cputemp", trackCPUTemp.Value);
        }

        private void TrackTotal_Scroll(object? sender, EventArgs e)
        {
            if (trackTotal.Value > trackSlow.Value) trackSlow.Value = trackTotal.Value;
            if (trackTotal.Value > trackFast.Value) trackFast.Value = trackTotal.Value;
            if (trackTotal.Value < trackCPU.Value) trackCPU.Value = trackTotal.Value;
            SavePower();
        }

        private void TrackSlow_Scroll(object? sender, EventArgs e)
        {
            if (trackSlow.Value < trackTotal.Value) trackTotal.Value = trackSlow.Value;
            if (trackSlow.Value > trackFast.Value) trackFast.Value = trackSlow.Value;
            SavePower();
        }

        private void TrackFast_Scroll(object? sender, EventArgs e)
        {
            if (trackFast.Value < trackSlow.Value) trackSlow.Value = trackFast.Value;
            if (trackFast.Value < trackTotal.Value) trackTotal.Value = trackFast.Value;
            SavePower();
        }

        private void TrackCPU_Scroll(object? sender, EventArgs e)
        {
            if (trackCPU.Value > trackTotal.Value) trackTotal.Value = trackCPU.Value;
            SavePower();
        }

        private void TrackCross_Scroll(object? sender, EventArgs e)
        {
            trackGPUtoCPU.Value -= trackGPUtoCPU.Value % AsusACPI.StepGPUtoCPU;
            SavePower();
        }

        public void InitFans()
        {

            int chartCount = 2;

            // Middle / system fan check
            if (AppConfig.IsMidFan() || !AsusACPI.IsEmptyCurve(Program.acpi.GetFanCurve(AsusFan.Mid)) || Program.acpi.IsMidFanSupported())
            {
                AppConfig.Set("mid_fan", 1);
                chartCount++;
                chartMid.Visible = true;
                SetChart(chartMid, AsusFan.Mid);
                LoadProfile(seriesMid, AsusFan.Mid);
            }
            else
            {
                AppConfig.Set("mid_fan", 0);
            }

            // XG Mobile Fan check (show when connected OR when Always Show XGM Fan Curve is enabled)
            bool xgmConnected = Program.acpi.IsXGConnected() || XGM.IsConnected();
            if (xgmConnected || XGM.AlwaysShowFanCurve())
            {
                AppConfig.Set("xgm_fan", 1);
                chartCount++;
                chartXGM.Visible = true;
                buttonTestXGM.Visible = xgmConnected;
                SetChart(chartXGM, AsusFan.XGM);
                LoadProfile(seriesXGM, AsusFan.XGM);
            }
            else
            {
                AppConfig.Set("xgm_fan", 0);
                chartXGM.Visible = false;
                buttonTestXGM.Visible = false;
            }

            try
            {
                if (chartCount > 2)
                {
                    int maxWorkingHeight = (Screen.FromControl(this)?.WorkingArea.Height ?? 1000) - 50;
                    int calculatedHeight = (int)(ControlHelper.GetDpiScale(this).Value * (chartCount * 200 + 100));
                    int finalHeight = Math.Min(maxWorkingHeight, Math.Max(MinimumSize.Height, calculatedHeight));
                    Size = MinimumSize = new Size(Size.Width, finalHeight);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }


            SetChart(chartCPU, AsusFan.CPU);
            SetChart(chartGPU, AsusFan.GPU);

            LoadProfile(seriesCPU, AsusFan.CPU);
            LoadProfile(seriesGPU, AsusFan.GPU);

            bool autoFans = AppConfig.IsApplyPower() && AppConfig.IsFanRequired();
            bool applyFans = AppConfig.IsApplyFans();

            checkApplyFans.Checked = applyFans;

            if (autoFans || applyFans)
            {
                seriesCPU.Color = colorStandard;
                seriesGPU.Color = colorTurbo;
                seriesMid.Color = colorEco;
                seriesXGM.Color = Color.Orange;
            }
            else
            {
                seriesCPU.Color = Color.Gray;
                seriesGPU.Color = Color.Gray;
                seriesMid.Color = Color.Gray;
                seriesXGM.Color = XGM.IsIndependentFanEnabled() ? Color.Orange : Color.Gray;
            }

            InitHysteresis();

        }


        void LoadProfile(Series series, AsusFan device, bool reset = false)
        {

            series.ChartType = SeriesChartType.Line;
            series.MarkerSize = 10;
            series.MarkerStyle = MarkerStyle.Circle;

            series.Points.Clear();

            byte[] curve = AppConfig.GetFanConfig(device);

            if (reset || AsusACPI.IsInvalidCurve(curve))
            {
                curve = Program.acpi.GetFanCurve(device, Modes.GetCurrentBase());
                Logger.WriteLine($"Default Curve: {device} - {BitConverter.ToString(curve)}");
                if (AsusACPI.IsInvalidCurve(curve))
                    curve = AppConfig.GetDefaultCurve(device);

                curve = AsusACPI.FixFanCurve(curve);

            }

            //Debug.WriteLine(BitConverter.ToString(curve));

            int old = -1;
            for (int i = 0; i < 8; i++)
            {
                if (curve[i] <= old) curve[i] = (byte)Math.Min(100, old + 1); // preventing 2 points in same spot from default asus profiles
                series.Points.AddXY(curve[i], curve[i + 8]);
                old = curve[i];
            }

            SaveProfile(series, device);

        }

        void SaveProfile(Series series, AsusFan device)
        {
            byte[] curve = new byte[16];
            int i = 0;
            foreach (DataPoint point in series.Points)
            {
                curve[i] = (byte)point.XValue;
                curve[i + 8] = (byte)point.YValues.First();
                i++;
            }

            AppConfig.SetFanConfig(device, curve);
            //Program.wmi.SetFanCurve(device, curve);

        }

        private void ButtonReset_Click(object? sender, EventArgs e)
        {

            LoadProfile(seriesCPU, AsusFan.CPU, true);
            LoadProfile(seriesGPU, AsusFan.GPU, true);

            if (AppConfig.IsMidFan())
                LoadProfile(seriesMid, AsusFan.Mid, true);

            if (Program.acpi.IsXGConnected() || XGM.IsConnected())
                LoadProfile(seriesXGM, AsusFan.XGM, true);

            checkApplyFans.Checked = false;
            checkApplyPower.Checked = false;
            seriesCPU.Color = Color.Gray;
            seriesGPU.Color = Color.Gray;
            seriesMid.Color = Color.Gray;
            seriesXGM.Color = Color.Gray;

            AppConfig.SetMode("auto_apply", 0);
            AppConfig.SetMode("auto_apply_power", 0);

            trackUV.Value = CpuInfo.MaxCPUUV;
            trackUViGPU.Value = CpuInfo.MaxIGPUUV;
            trackTemp.Value = CpuInfo.DefaultTemp;

            trackCrossLoad.Value = AsusACPI.MaxCrossLoad;
            trackGPUtoCPU.Value = AsusACPI.MaxGPUtoCPU;
            trackCPUTemp.Value = AsusACPI.MaxCPUTemp;
            SavePower();

            AdvancedScroll();
            AppConfig.RemoveMode("cpu_temp");

            modeControl.ResetPerformanceMode();

            InitPowerPlan();

            XGM.Reset();


            if (gpuVisible)
            {
                trackGPUClockLimit.Value = NvidiaGpuControl.MaxClockLimit;
                trackGPUCore.Value = 0;
                trackGPUMemory.Value = 0;

                trackGPUBoost.Value = AsusACPI.MaxGPUBoost;
                trackGPUTemp.Value = AsusACPI.MaxGPUTemp;

                //AppConfig.SetMode("gpu_boost", trackGPUBoost.Value);
                //AppConfig.SetMode("gpu_temp", trackGPUTemp.Value);

                AppConfig.RemoveMode("gpu_boost");
                AppConfig.RemoveMode("gpu_temp");

                AppConfig.RemoveMode("gpu_power");
                AppConfig.RemoveMode("gpu_clock_limit");
                AppConfig.RemoveMode("gpu_core");
                AppConfig.RemoveMode("gpu_memory");

                InitGPUPower();

                VisualiseGPUSettings();
                modeControl.SetGPUClocks(true, true);
                modeControl.SetGPUPower();
            }

            if (panelHysteresis.Visible)
            {
                AppConfig.RemoveMode("hysteresis_up");
                AppConfig.RemoveMode("hysteresis_down");
                InitHysteresis();
            }

        }

        private void Chart_Save()
        {
            curPoint = null;
            curIndex = -1;
            labelTip.Visible = false;

            SaveProfile(seriesCPU, AsusFan.CPU);
            SaveProfile(seriesGPU, AsusFan.GPU);

            if (AppConfig.IsMidFan())
                SaveProfile(seriesMid, AsusFan.Mid);

            if (Program.acpi.IsXGConnected() || XGM.IsConnected() || AppConfig.Is("xgm_fan"))
                SaveProfile(seriesXGM, AsusFan.XGM);

            modeControl.AutoFans();
        }

        private void ChartCPU_MouseUp(object? sender, MouseEventArgs e)
        {
            Chart_Save();
        }


        private void ChartCPU_MouseLeave(object? sender, EventArgs e)
        {
            curPoint = null;
            curIndex = -1;
            labelTip.Visible = false;
            FanDragHint(false);
        }

        private void Chart_PreviewKeyDown(object? sender, PreviewKeyDownEventArgs e)
        {
            Chart chart = (Chart)sender!;
            int count = chart.Series[0].Points.Count;

            switch (e.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                case Keys.Home:
                case Keys.End:
                    e.IsInputKey = true;
                    break;
                case Keys.Tab:
                    bool back = e.Modifiers.HasFlag(Keys.Shift);
                    if (back ? _kbIndex > 0 : _kbIndex < count - 1)
                        e.IsInputKey = true;
                    else
                        _chartTabDirection = back ? -1 : 1;
                    break;
            }
        }

        private void Chart_GotFocus(object? sender, AsusFan device)
        {
            Chart chart = (Chart)sender!;
            int count = chart.Series[0].Points.Count;
            if (count == 0) return;

            if (_chartTabDirection > 0) _kbIndex = 0;
            else if (_chartTabDirection < 0) _kbIndex = count - 1;
            else if (_kbIndex < 0 || _kbIndex >= count) _kbIndex = 0;
            _chartTabDirection = 0;

            UpdateChartTip(chart, device);
        }

        private void Chart_LostFocus(object? sender, EventArgs e)
        {
            labelTip.Visible = false;
        }

        private void Chart_KeyDown(object? sender, KeyEventArgs e, AsusFan device)
        {
            Chart chart = (Chart)sender!;
            Series series = chart.Series[0];
            int count = series.Points.Count;
            if (count == 0) return;

            if (_kbIndex < 0 || _kbIndex >= count) _kbIndex = 0;

            int step = e.Shift ? 5 : 1;

            switch (e.KeyCode)
            {
                case Keys.Tab:
                    _kbIndex = Math.Max(0, Math.Min(count - 1, e.Shift ? _kbIndex - 1 : _kbIndex + 1));
                    break;
                case Keys.Home:
                    _kbIndex = 0;
                    break;
                case Keys.End:
                    _kbIndex = count - 1;
                    break;
                case Keys.Left:
                    Chart_AdjustPoint(-step, 0, series, device);
                    break;
                case Keys.Right:
                    Chart_AdjustPoint(step, 0, series, device);
                    break;
                case Keys.Down:
                    Chart_AdjustPoint(0, -step, series, device);
                    break;
                case Keys.Up:
                    Chart_AdjustPoint(0, step, series, device);
                    break;
                default:
                    return;
            }

            UpdateChartTip(chart, device);
            e.Handled = true;
        }

        private void Chart_AdjustPoint(int dx, int dy, Series series, AsusFan device)
        {
            DataPoint point = series.Points[_kbIndex];

            double newX = point.XValue + dx;
            double newY = point.YValues[0] + dy;

            newX = Math.Max(tempMin, Math.Min(tempMax, newX));
            if (clampFanDots)
            {
                double minX = _kbIndex == 0 ? tempMin : (tempMin + (_kbIndex * 10));
                double maxX = minX + 9;
                newX = Math.Max(minX, Math.Min(maxX, newX));
            }

            newY = Math.Max(0, Math.Min(fansMax, newY));
            double dymin = (newX - 70) * 1.2;
            if (newY < dymin) newY = dymin;

            point.XValue = newX;
            point.YValues[0] = newY;

            AdjustAllLevels(_kbIndex, newX, newY, series);
            SaveProfile(series, device);
        }

        private void UpdateChartTip(Chart chart, AsusFan device)
        {
            DataPoint point = chart.Series[0].Points[_kbIndex];
            labelTip.Text = TempHelper.FormatTemp(point.XValue) + ", " + ChartYLabel((int)point.YValues[0], device, " " + Properties.Strings.RPM);

            try
            {
                ChartArea ca = chart.ChartAreas[0];
                double pixX = ca.AxisX.ValueToPixelPosition(point.XValue);
                double pixY = ca.AxisY.ValueToPixelPosition(point.YValues[0]);
                labelTip.Top = (int)pixY + chart.Top;
                labelTip.Left = Math.Min(chart.Width - labelTip.Width - 20, (int)pixX - 50);
                labelTip.Visible = true;
            }
            catch { }

            if (chart.Focused)
            {
                try
                {
                    chart.AccessibilityObject.RaiseAutomationNotification(
                        System.Windows.Forms.Automation.AutomationNotificationKind.Other,
                        System.Windows.Forms.Automation.AutomationNotificationProcessing.MostRecent,
                        $"{device} point {_kbIndex + 1}, {labelTip.Text}");
                }
                catch { }
            }
        }

        private void ChartCPU_MouseMove(object? sender, MouseEventArgs e, AsusFan device)
        {

            if (sender is null) return;
            Chart chart = (Chart)sender;

            ChartArea ca = chart.ChartAreas[0];
            Axis ax = ca.AxisX;
            Axis ay = ca.AxisY;

            bool tip = false;

            Series series = chart.Series[0];

            if (!e.Button.HasFlag(MouseButtons.Left) || curPoint == null)
            {
                try
                {
                    HitTestResult hit = chart.HitTest(e.X, e.Y);
                    if (hit.Series is not null && hit.PointIndex >= 0 && hit.PointIndex < hit.Series.Points.Count)
                    {
                        curIndex = hit.PointIndex;
                        curPoint = hit.Series.Points[curIndex];
                        tip = true;
                    }
                    else if (!e.Button.HasFlag(MouseButtons.Left))
                    {
                        curPoint = null;
                        curIndex = -1;
                    }
                }
                catch
                {
                    curPoint = null;
                    curIndex = -1;
                }
            }
            else
            {
                tip = true;
            }


            if (curPoint != null)
            {

                double dx, dy, dymin;

                try
                {
                    dx = ax.PixelPositionToValue(e.X);
                    dy = ay.PixelPositionToValue(e.Y);

                    if (dx < tempMin) dx = tempMin;
                    if (dx > tempMax) dx = tempMax;

                    if (dy < 0) dy = 0;
                    if (dy > fansMax) dy = fansMax;

                    dymin = (dx - 70) * 1.2;

                    if (dy < dymin) dy = dymin;

                    if (e.Button.HasFlag(MouseButtons.Left))
                    {
                        double deltaY = dy - curPoint.YValues[0];
                        double deltaX = dx - curPoint.XValue;

                        if (clampFanDots)
                        {
                            double minX = curIndex == 0 ? tempMin : (tempMin + (curIndex * 10));
                            double maxX = minX + 9;
                            dx = Math.Max(minX, Math.Min(maxX, dx));
                        }

                        curPoint.XValue = dx;

                        if (Control.ModifierKeys == Keys.Shift)
                            AdjustAll(0, deltaY, series);
                        else
                        {
                            curPoint.YValues[0] = dy;
                            AdjustAllLevels(curIndex, dx, dy, series);
                        }

                        tip = true;
                    }

                    labelTip.Text = TempHelper.FormatTemp(curPoint.XValue) + ", " + ChartYLabel((int)curPoint.YValues[0], device, " " + Properties.Strings.RPM);
                    labelTip.Top = e.Y + ((Control)sender).Top;
                    labelTip.Left = Math.Min(chart.Width - labelTip.Width - 20, e.X - 50);

                }
                catch
                {
                    Debug.WriteLine(e.Y);
                    tip = false;
                }

            }

            labelTip.Visible = tip;

            FanDragHint(curPoint != null);

        }

        private void FanDragHint(bool show)
        {
            labelFansResult.Text = show ? (Properties.Strings.FanDragAll + "  |  Right-click chart for Presets & Copy") : "";
            labelFansResult.ForeColor = show ? colorGray : colorTurbo;
            labelFansResult.Visible = show;
        }

        private void ConfigureSliderErgonomics()
        {
            void AttachWheel(TrackBar tb, int step, EventHandler onScroll, Action? onCommit = null)
            {
                tb.MouseWheel += (s, e) =>
                {
                    if (e is HandledMouseEventArgs hme) hme.Handled = true;
                    int delta = e.Delta > 0 ? step : -step;
                    int next = Math.Clamp(tb.Value + delta, tb.Minimum, tb.Maximum);
                    if (next != tb.Value)
                    {
                        tb.Value = next;
                        onScroll(tb, EventArgs.Empty);
                        onCommit?.Invoke();
                    }
                };
            }

            void AttachReset(Control labelLeft, Control labelRight, Action resetAction)
            {
                EventHandler handler = (s, e) => resetAction();
                labelLeft.DoubleClick += handler;
                labelRight.DoubleClick += handler;
                labelLeft.Cursor = Cursors.Hand;
                labelRight.Cursor = Cursors.Hand;
            }

            AttachWheel(trackTotal, 1, TrackTotal_Scroll, () => Task.Run(() => modeControl.AutoPower(true)));
            AttachWheel(trackSlow, 1, TrackSlow_Scroll, () => Task.Run(() => modeControl.AutoPower(true)));
            AttachWheel(trackFast, 1, TrackFast_Scroll, () => Task.Run(() => modeControl.AutoPower(true)));
            AttachWheel(trackCPU, 1, TrackCPU_Scroll, () => Task.Run(() => modeControl.AutoPower(true)));
            AttachWheel(trackCrossLoad, 1, TrackCross_Scroll, () => Task.Run(() => modeControl.AutoPower(true)));
            AttachWheel(trackGPUtoCPU, AsusACPI.StepGPUtoCPU, TrackCross_Scroll, () => Task.Run(() => modeControl.AutoPower(true)));
            AttachWheel(trackCPUTemp, 1, TrackCross_Scroll, () => Task.Run(() => modeControl.AutoPower(true)));

            AttachWheel(trackGPUClockLimit, 5, trackGPUClockLimit_Scroll, () => modeControl.SetGPUClocks(true));
            AttachWheel(trackGPUCore, 5, trackGPU_Scroll, () => modeControl.SetGPUClocks(true));
            AttachWheel(trackGPUMemory, 5, trackGPU_Scroll, () => modeControl.SetGPUClocks(true));
            AttachWheel(trackGPUBoost, 1, trackGPUPower_Scroll, () => modeControl.SetGPUPower());
            AttachWheel(trackGPUTemp, 1, trackGPUPower_Scroll, () => modeControl.SetGPUPower());
            AttachWheel(trackGPUPower, 1, trackGPUPower_Scroll, () => modeControl.SetGPUPower());

            AttachWheel(trackUV, 1, TrackUV_Scroll);
            AttachWheel(trackUViGPU, 1, TrackUV_Scroll);
            AttachWheel(trackTemp, 1, TrackUV_Scroll);

            AttachReset(labelLeftTotal, labelTotal, () =>
            {
                trackTotal.Value = Math.Clamp(AsusACPI.DefaultTotal, trackTotal.Minimum, trackTotal.Maximum);
                TrackTotal_Scroll(trackTotal, EventArgs.Empty);
                Task.Run(() => modeControl.AutoPower(true));
            });
            AttachReset(labelLeftSlow, labelSlow, () =>
            {
                trackSlow.Value = trackTotal.Value;
                TrackSlow_Scroll(trackSlow, EventArgs.Empty);
                Task.Run(() => modeControl.AutoPower(true));
            });
            AttachReset(labelLeftFast, labelFast, () =>
            {
                trackFast.Value = trackSlow.Value;
                TrackFast_Scroll(trackFast, EventArgs.Empty);
                Task.Run(() => modeControl.AutoPower(true));
            });
            AttachReset(labelLeftCPU, labelCPU, () =>
            {
                trackCPU.Value = Math.Clamp(AsusACPI.DefaultCPU, trackCPU.Minimum, trackCPU.Maximum);
                TrackCPU_Scroll(trackCPU, EventArgs.Empty);
                Task.Run(() => modeControl.AutoPower(true));
            });
            AttachReset(labelLeftCrossLoad, labelCrossLoad, () =>
            {
                trackCrossLoad.Value = AsusACPI.MaxCrossLoad;
                TrackCross_Scroll(trackCrossLoad, EventArgs.Empty);
                Task.Run(() => modeControl.AutoPower(true));
            });
            AttachReset(labelLeftGPUtoCPU, labelGPUtoCPU, () =>
            {
                trackGPUtoCPU.Value = AsusACPI.MaxGPUtoCPU;
                TrackCross_Scroll(trackGPUtoCPU, EventArgs.Empty);
                Task.Run(() => modeControl.AutoPower(true));
            });
            AttachReset(labelLeftCPUTemp, labelCPUTemp, () =>
            {
                trackCPUTemp.Value = AsusACPI.MaxCPUTemp;
                TrackCross_Scroll(trackCPUTemp, EventArgs.Empty);
                Task.Run(() => modeControl.AutoPower(true));
            });

            AttachReset(labelGPUClockLimitTitle, labelGPUClockLimit, () =>
            {
                trackGPUClockLimit.Value = NvidiaGpuControl.MaxClockLimit;
                AppConfig.RemoveMode("gpu_clock_limit");
                VisualiseGPUSettings();
                modeControl.SetGPUClocks(true);
            });
            AttachReset(labelGPUCoreTitle, labelGPUCore, () =>
            {
                trackGPUCore.Value = 0;
                AppConfig.SetMode("gpu_core", 0);
                VisualiseGPUSettings();
                modeControl.SetGPUClocks(true);
            });
            AttachReset(labelGPUMemoryTitle, labelGPUMemory, () =>
            {
                trackGPUMemory.Value = 0;
                AppConfig.SetMode("gpu_memory", 0);
                VisualiseGPUSettings();
                modeControl.SetGPUClocks(true);
            });
            AttachReset(labelGPUBoostTitle, labelGPUBoost, () =>
            {
                trackGPUBoost.Value = AsusACPI.MaxGPUBoost;
                AppConfig.RemoveMode("gpu_boost");
                VisualiseGPUSettings();
                modeControl.SetGPUPower();
            });
            AttachReset(labelGPUTempTitle, labelGPUTemp, () =>
            {
                trackGPUTemp.Value = AsusACPI.MaxGPUTemp;
                AppConfig.RemoveMode("gpu_temp");
                VisualiseGPUSettings();
                modeControl.SetGPUPower();
            });
            AttachReset(labelGPUPowerTitle, labelGPUPower, () =>
            {
                trackGPUPower.Value = AsusACPI.MaxGPUPower;
                AppConfig.RemoveMode("gpu_power");
                VisualiseGPUSettings();
                modeControl.SetGPUPower();
            });

            AttachReset(labelLeftUV, labelUV, () =>
            {
                trackUV.Value = 0;
                AdvancedScroll();
            });
            AttachReset(labelLeftUViGPU, labelUViGPU, () =>
            {
                trackUViGPU.Value = 0;
                AdvancedScroll();
            });
            AttachReset(labelLeftTemp, labelTemp, () =>
            {
                trackTemp.Value = CpuInfo.DefaultTemp;
                AdvancedScroll();
            });
        }

        private void ConfigureChartAndModeMenus()
        {
            MouseEventHandler modeMenuHandler = (s, e) =>
            {
                if (e.Button == MouseButtons.Right && s is Control ctrl)
                {
                    ShowModeContextMenu(ctrl, e.Location);
                }
            };
            buttonAdd.MouseUp += modeMenuHandler;
            comboModes.MouseUp += modeMenuHandler;
            labelPowerLimits.MouseUp += modeMenuHandler;
        }

        private Series GetSeriesForDevice(AsusFan device) => device switch
        {
            AsusFan.GPU => seriesGPU,
            AsusFan.Mid => seriesMid,
            AsusFan.XGM => seriesXGM,
            _ => seriesCPU
        };

        private void ApplyCustomCurveBytes(Series series, AsusFan device, byte[] curve)
        {
            byte[] fixedCurve = AsusACPI.FixFanCurve(curve);
            series.Points.Clear();
            int old = -1;
            for (int i = 0; i < 8; i++)
            {
                if (fixedCurve[i] <= old) fixedCurve[i] = (byte)Math.Min(100, old + 1);
                series.Points.AddXY(fixedCurve[i], fixedCurve[i + 8]);
                old = fixedCurve[i];
            }
            SaveProfile(series, device);
            if (device == AsusFan.XGM && XGM.IsIndependentFanEnabled() && !AppConfig.IsApplyFans())
            {
                if (HardwareControl.IsXGConnectedCached()) XGM.SetFan(fixedCurve);
                seriesXGM.Color = Color.Orange;
            }
            else
            {
                if (!checkApplyFans.Checked)
                {
                    checkApplyFans.Checked = true;
                    AppConfig.SetMode("auto_apply", 1);
                    seriesCPU.Color = colorStandard;
                    seriesGPU.Color = colorTurbo;
                    seriesMid.Color = colorEco;
                    seriesXGM.Color = Color.Orange;
                }
                modeControl.AutoFans();
            }
            UpdateSensors();
        }

        private void ShowChartContextMenu(Chart chart, AsusFan device, Point location)
        {
            Series series = GetSeriesForDevice(device);
            var menu = new ContextMenuStrip
            {
                Renderer = new CustomMenuRenderer(),
                BackColor = this.BackColor,
                ForeColor = this.ForeColor,
                ShowImageMargin = false,
                ShowCheckMargin = true
            };
            Padding pad = new Padding(5, 4, 5, 4);

            string fanName = device switch
            {
                AsusFan.CPU => "CPU Fan",
                AsusFan.GPU => "GPU Fan",
                AsusFan.Mid => "Mid / System Fan",
                AsusFan.XGM => "XG Mobile Fan",
                _ => "Fan"
            };

            var header = new ToolStripMenuItem($"{fanName} Curve Actions") { Enabled = false, Margin = pad };
            menu.Items.Add(header);
            menu.Items.Add("-");

            // Presets
            var presets = new (string name, byte[] curve)[]
            {
                ("Preset: Silent / Zero-RPM Idle (< 55°C)", [30, 40, 50, 55, 65, 75, 85, 95, 0, 0, 0, 14, 28, 48, 68, 88]),
                ("Preset: Quiet Balanced",                  [30, 40, 50, 60, 70, 80, 90, 100, 0, 8, 18, 28, 42, 58, 74, 90]),
                ("Preset: Linear Standard",                 [30, 40, 50, 60, 70, 80, 90, 100, 10, 20, 32, 45, 60, 74, 88, 100]),
                ("Preset: Aggressive Cooling",              [30, 40, 50, 60, 70, 80, 90, 100, 20, 32, 46, 62, 76, 88, 96, 100]),
                ("Preset: Max Cooling (95% Full Blast)",    [30, 40, 50, 60, 70, 80, 90, 100, 50, 65, 80, 90, 95, 95, 95, 100])
            };

            foreach (var p in presets)
            {
                var item = new ToolStripMenuItem(p.name) { Margin = pad };
                byte[] curveCopy = (byte[])p.curve.Clone();
                item.Click += (s, e) => ApplyCustomCurveBytes(series, device, curveCopy);
                menu.Items.Add(item);
            }

            menu.Items.Add("-");

            var itemShiftUp = new ToolStripMenuItem("Shift Entire Curve Up (+5%)") { Margin = pad };
            itemShiftUp.Click += (s, e) =>
            {
                AdjustAll(0, 5, series);
                Chart_Save();
                UpdateSensors();
            };
            menu.Items.Add(itemShiftUp);

            var itemShiftDown = new ToolStripMenuItem("Shift Entire Curve Down (-5%)") { Margin = pad };
            itemShiftDown.Click += (s, e) =>
            {
                AdjustAll(0, -5, series);
                Chart_Save();
                UpdateSensors();
            };
            menu.Items.Add(itemShiftDown);

            menu.Items.Add("-");

            // Copy Curve From...
            var copySources = new List<(AsusFan fan, string label)>
            {
                (AsusFan.CPU, "Copy Curve from CPU Fan"),
                (AsusFan.GPU, "Copy Curve from GPU Fan")
            };
            if (chartMid.Visible) copySources.Add((AsusFan.Mid, "Copy Curve from Mid Fan"));
            if (chartXGM.Visible) copySources.Add((AsusFan.XGM, "Copy Curve from XG Mobile Fan"));

            foreach (var src in copySources)
            {
                if (src.fan == device) continue;
                var itemCopy = new ToolStripMenuItem(src.label) { Margin = pad };
                AsusFan sourceFan = src.fan;
                itemCopy.Click += (s, e) =>
                {
                    Series srcSeries = GetSeriesForDevice(sourceFan);
                    byte[] curve = new byte[16];
                    for (int i = 0; i < Math.Min(8, srcSeries.Points.Count); i++)
                    {
                        curve[i] = (byte)srcSeries.Points[i].XValue;
                        curve[i + 8] = (byte)srcSeries.Points[i].YValues[0];
                    }
                    ApplyCustomCurveBytes(series, device, curve);
                };
                menu.Items.Add(itemCopy);
            }

            menu.Items.Add("-");

            // XG Mobile Fan Settings Submenu / Direct Items
            bool showXgmOptions = (device == AsusFan.XGM) || Program.acpi.IsXGConnected() || XGM.IsConnected() || XGM.AlwaysShowFanCurve();
            if (showXgmOptions)
            {
                var itemXgmIndep = new ToolStripMenuItem("XG Mobile: Apply Fan Curve Independently of Laptop BIOS")
                {
                    Checked = XGM.IsIndependentFanEnabled(),
                    Margin = pad
                };
                itemXgmIndep.Click += (s, e) =>
                {
                    bool next = !XGM.IsIndependentFanEnabled();
                    AppConfig.Set("xgm_independent_fan", next ? 1 : 0);
                    InitFans();
                    modeControl.AutoFans();
                    Program.toast?.RunToast(next ? "XGM Independent Fan Curve: On" : "XGM Independent Fan Curve: Off");
                };
                menu.Items.Add(itemXgmIndep);

                var itemXgmMidBoost = new ToolStripMenuItem("XG Mobile: Boost Laptop Mid Fan to Match CPU when Docked")
                {
                    Checked = AppConfig.Is("xgm_mid_boost"),
                    Margin = pad
                };
                itemXgmMidBoost.Click += (s, e) =>
                {
                    bool next = !AppConfig.Is("xgm_mid_boost");
                    AppConfig.Set("xgm_mid_boost", next ? 1 : 0);
                    if (AppConfig.IsApplyFans()) modeControl.AutoFans();
                };
                menu.Items.Add(itemXgmMidBoost);

                var itemAlwaysShowXgm = new ToolStripMenuItem("XG Mobile: Always Show XGM Fan Chart (Even Unplugged)")
                {
                    Checked = XGM.AlwaysShowFanCurve(),
                    Margin = pad
                };
                itemAlwaysShowXgm.Click += (s, e) =>
                {
                    bool next = !XGM.AlwaysShowFanCurve();
                    AppConfig.Set("show_xgm_fan", next ? 1 : 0);
                    InitFans();
                };
                menu.Items.Add(itemAlwaysShowXgm);

                var itemXgmScale = new ToolStripMenuItem($"XG Mobile: Max RPM Scale ({FanSensorControl.GetFanMax(AsusFan.XGM) * 100} RPM)") { Margin = pad };
                int currentXgmMax = FanSensorControl.GetFanMax(AsusFan.XGM);
                int[] scaleOptions = new int[] { 76, 72, 65, 60, 55, 50, 45, 36 };

                if (!scaleOptions.Contains(currentXgmMax))
                {
                    var customOpt = new ToolStripMenuItem($"Current: {currentXgmMax * 100} RPM") { Checked = true, Margin = pad };
                    itemXgmScale.DropDownItems.Add(customOpt);
                }

                foreach (int scaleVal in scaleOptions)
                {
                    string optionName = scaleVal switch
                    {
                        76 => "7600 RPM (Full Physical Max)",
                        72 => "7200 RPM (Rated Spec)",
                        36 => "3600 RPM (Quiet Limit)",
                        _ => $"{scaleVal * 100} RPM"
                    };

                    var subOption = new ToolStripMenuItem(optionName)
                    {
                        Checked = (currentXgmMax == scaleVal),
                        Margin = pad
                    };

                    int targetVal = scaleVal;
                    subOption.Click += (s, e) =>
                    {
                        FanSensorControl.SetFanMax(AsusFan.XGM, targetVal);
                        InitFans();
                        Program.toast?.RunToast($"XG Mobile Max RPM Scale: {targetVal * 100} RPM");
                    };

                    itemXgmScale.DropDownItems.Add(subOption);
                }
                menu.Items.Add(itemXgmScale);

                if (Program.acpi.IsXGConnected() || XGM.IsConnected())
                {
                    var itemTestXgm = new ToolStripMenuItem("XG Mobile: Test Fan Full Speed (8s)") { Margin = pad };
                    itemTestXgm.Click += ButtonTestXGM_Click;
                    menu.Items.Add(itemTestXgm);
                }

                menu.Items.Add("-");
            }
            else
            {
                var itemShowXgm = new ToolStripMenuItem("Show XG Mobile Fan Curve Chart")
                {
                    Checked = XGM.AlwaysShowFanCurve(),
                    Margin = pad
                };
                itemShowXgm.Click += (s, e) =>
                {
                    AppConfig.Set("show_xgm_fan", 1);
                    InitFans();
                };
                menu.Items.Add(itemShowXgm);
                menu.Items.Add("-");
            }

            var itemResetThis = new ToolStripMenuItem($"Reset {fanName} Curve to Default") { Margin = pad };
            itemResetThis.Click += (s, e) =>
            {
                LoadProfile(series, device, true);
                modeControl.AutoFans();
                UpdateSensors();
            };
            menu.Items.Add(itemResetThis);

            menu.Show(chart, location);
        }

        private void ShowModeContextMenu(Control anchor, Point location)
        {
            var menu = new ContextMenuStrip
            {
                Renderer = new CustomMenuRenderer(),
                BackColor = this.BackColor,
                ForeColor = this.ForeColor,
                ShowImageMargin = false,
                ShowCheckMargin = true
            };
            Padding pad = new Padding(5, 4, 5, 4);

            int curMode = Modes.GetCurrent();
            string curName = Modes.GetCurrentName();

            var header = new ToolStripMenuItem($"Mode Profile: {curName}") { Enabled = false, Margin = pad };
            menu.Items.Add(header);
            menu.Items.Add("-");

            var itemClone = new ToolStripMenuItem($"Clone '{curName}' into New Custom Mode") { Margin = pad };
            itemClone.Click += (s, e) =>
            {
                int srcMode = Modes.GetCurrent();
                int newMode = Modes.Add();
                CopyModeSettings(srcMode, newMode);
                FillModes();
                modeControl.SetPerformanceMode(newMode);
                InitAll();
                Program.toast?.RunToast($"Cloned '{curName}' → '{Modes.GetCurrentName()}'");
            };
            menu.Items.Add(itemClone);

            var menuCopyFrom = new ToolStripMenuItem("Copy Fan Curves & Power Limits From...") { Margin = pad };
            menuCopyFrom.DropDown.BackColor = this.BackColor;
            menuCopyFrom.DropDown.ForeColor = this.ForeColor;
            foreach (var kv in Modes.GetDictonary())
            {
                if (kv.Key == curMode) continue;
                int srcKey = kv.Key;
                string srcLabel = kv.Value;
                var sub = new ToolStripMenuItem(srcLabel) { Margin = pad };
                sub.Click += (s, e) =>
                {
                    CopyModeSettings(srcKey, Modes.GetCurrent());
                    InitAll();
                    modeControl.SetPerformanceMode();
                    Program.toast?.RunToast($"Copied settings from '{srcLabel}'");
                };
                menuCopyFrom.DropDownItems.Add(sub);
            }
            menu.Items.Add(menuCopyFrom);

            menu.Items.Add("-");

            var itemExport = new ToolStripMenuItem("Export Mode Profile to Clipboard (JSON)") { Margin = pad };
            itemExport.Click += (s, e) =>
            {
                try
                {
                    var data = new Dictionary<string, string?>
                    {
                        ["mode_base"] = Modes.GetCurrentBase().ToString(),
                        ["fan_cpu"] = AppConfig.GetFanConfig(AsusFan.CPU) is byte[] c ? BitConverter.ToString(c) : null,
                        ["fan_gpu"] = AppConfig.GetFanConfig(AsusFan.GPU) is byte[] g ? BitConverter.ToString(g) : null,
                        ["fan_mid"] = AppConfig.GetFanConfig(AsusFan.Mid) is byte[] m ? BitConverter.ToString(m) : null,
                        ["fan_xgm"] = AppConfig.GetFanConfig(AsusFan.XGM) is byte[] x ? BitConverter.ToString(x) : null,
                        ["auto_apply"] = AppConfig.GetMode("auto_apply", 0).ToString(),
                        ["auto_apply_power"] = AppConfig.GetMode("auto_apply_power", 0).ToString(),
                        ["limit_total"] = trackTotal.Value.ToString(),
                        ["limit_slow"] = trackSlow.Value.ToString(),
                        ["limit_fast"] = trackFast.Value.ToString(),
                        ["limit_cpu"] = trackCPU.Value.ToString(),
                        ["cpu_uv"] = trackUV.Value.ToString(),
                        ["igpu_uv"] = trackUViGPU.Value.ToString(),
                        ["cpu_temp"] = trackTemp.Value.ToString()
                    };
                    string json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    Clipboard.SetText(json);
                    Program.toast?.RunToast("Profile exported to Clipboard!");
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Export profile error: " + ex.Message);
                }
            };
            menu.Items.Add(itemExport);

            var itemImport = new ToolStripMenuItem("Import Mode Profile from Clipboard (JSON)") { Margin = pad };
            itemImport.Click += (s, e) =>
            {
                try
                {
                    string clip = Clipboard.GetText();
                    if (string.IsNullOrWhiteSpace(clip) || !clip.TrimStart().StartsWith("{")) return;
                    var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(clip);
                    if (data == null) return;

                    byte[]? ParseCurve(string? str)
                    {
                        if (string.IsNullOrWhiteSpace(str)) return null;
                        string[] parts = str.Split('-');
                        if (parts.Length != 16) return null;
                        byte[] b = new byte[16];
                        for (int i = 0; i < 16; i++) b[i] = Convert.ToByte(parts[i], 16);
                        return b;
                    }

                    if (data.TryGetValue("fan_cpu", out var fc) && ParseCurve(fc) is byte[] bc) AppConfig.SetFanConfig(AsusFan.CPU, bc);
                    if (data.TryGetValue("fan_gpu", out var fg) && ParseCurve(fg) is byte[] bg) AppConfig.SetFanConfig(AsusFan.GPU, bg);
                    if (data.TryGetValue("fan_mid", out var fm) && ParseCurve(fm) is byte[] bm) AppConfig.SetFanConfig(AsusFan.Mid, bm);
                    if (data.TryGetValue("fan_xgm", out var fx) && ParseCurve(fx) is byte[] bx) AppConfig.SetFanConfig(AsusFan.XGM, bx);

                    string[] intKeys = ["auto_apply", "auto_apply_power", "limit_total", "limit_slow", "limit_fast", "limit_cpu", "cpu_uv", "igpu_uv", "cpu_temp"];
                    foreach (string k in intKeys)
                    {
                        if (data.TryGetValue(k, out var valStr) && int.TryParse(valStr, out int v))
                            AppConfig.SetMode(k, v);
                    }

                    InitAll();
                    modeControl.SetPerformanceMode();
                    Program.toast?.RunToast("Profile imported from Clipboard!");
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Import profile error: " + ex.Message);
                    Program.toast?.RunToast("Invalid profile JSON in Clipboard");
                }
            };
            menu.Items.Add(itemImport);

            menu.Show(anchor, location);
        }

        private static void CopyModeSettings(int srcMode, int dstMode)
        {
            foreach (AsusFan fan in new[] { AsusFan.CPU, AsusFan.GPU, AsusFan.Mid, AsusFan.XGM })
            {
                string srcKey = $"fan_profile_{fan}_{srcMode}";
                string dstKey = $"fan_profile_{fan}_{dstMode}";
                string? curveStr = AppConfig.GetString(srcKey);
                if (!string.IsNullOrEmpty(curveStr))
                    AppConfig.Set(dstKey, curveStr);
            }

            string[] intKeys = [
                "auto_apply", "auto_apply_power", "limit_total", "limit_slow", "limit_fast", "limit_cpu",
                "limit_crossload", "limit_gpucpu", "limit_cputemp", "gpu_boost", "gpu_temp", "gpu_power",
                "gpu_core", "gpu_memory", "gpu_clock_limit", "auto_boost", "cpu_uv", "igpu_uv", "cpu_temp", "auto_uv"
            ];
            foreach (string k in intKeys)
            {
                int val = AppConfig.Get($"{k}_{srcMode}", int.MinValue);
                if (val != int.MinValue)
                    AppConfig.Set($"{k}_{dstMode}", val);
            }
        }

        private void AdjustAll(double deltaX, double deltaY, Series series)
        {
            for (int i = 0; i < series.Points.Count; i++)
            {
                series.Points[i].XValue = Math.Max(tempMin, Math.Min(tempMax, series.Points[i].XValue + deltaX));
                series.Points[i].YValues[0] = Math.Max(0, Math.Min(100, series.Points[i].YValues[0] + deltaY));
            }
        }

        private void AdjustAllLevels(int index, double curXVal, double curYVal, Series series)
        {

            // Get the neighboring DataPoints of the hit point
            DataPoint? upperPoint = null;
            DataPoint? lowerPoint = null;

            if (index > 0)
            {
                lowerPoint = series.Points[index - 1];
            }

            if (index < series.Points.Count - 1)
            {
                upperPoint = series.Points[index + 1];
            }

            // Adjust the values according to the comparison between the value and its neighbors
            if (upperPoint != null)
            {
                if (curYVal > upperPoint.YValues[0])
                {

                    for (int i = index + 1; i < series.Points.Count; i++)
                    {
                        DataPoint curUpper = series.Points[i];
                        if (curUpper.YValues[0] >= curYVal) break;

                        curUpper.YValues[0] = curYVal;
                    }
                }
                if (curXVal > upperPoint.XValue)
                {

                    for (int i = index + 1; i < series.Points.Count; i++)
                    {
                        DataPoint curUpper = series.Points[i];
                        if (curUpper.XValue >= curXVal) break;

                        curUpper.XValue = curXVal;
                    }
                }
            }

            if (lowerPoint != null)
            {
                //Debug.WriteLine(curYVal + " <? " + Math.Floor(lowerPoint.YValues[0]));
                if (curYVal <= Math.Floor(lowerPoint.YValues[0]))
                {
                    for (int i = index - 1; i >= 0; i--)
                    {
                        DataPoint curLower = series.Points[i];
                        if (curLower.YValues[0] < curYVal) break;
                        curLower.YValues[0] = Math.Floor(curYVal);
                    }
                }
                if (curXVal < lowerPoint.XValue)
                {

                    for (int i = index - 1; i >= 0; i--)
                    {
                        DataPoint curLower = series.Points[i];
                        if (curLower.XValue <= curXVal) break;

                        curLower.XValue = curXVal;
                    }
                }
            }
        }

    }

}
