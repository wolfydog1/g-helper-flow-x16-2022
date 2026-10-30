using GHelper.UI;
namespace GHelper
{
    partial class OverlayConfig
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            panelToggles = new TableLayoutPanel();
            checkEnable = new GHelper.UI.RCheckBox();
            checkGameOnly = new GHelper.UI.RCheckBox();
            tableModes = new TableLayoutPanel();
            buttonLight = new GHelper.UI.RButton();
            buttonDefault = new GHelper.UI.RButton();
            buttonFull = new GHelper.UI.RButton();
            buttonComplete = new GHelper.UI.RButton();
            buttonBar = new GHelper.UI.RButton();
            tableBlocks = new TableLayoutPanel();
            checkFps = new GHelper.UI.RCheckBox();
            checkTemp = new GHelper.UI.RCheckBox();
            checkFans = new GHelper.UI.RCheckBox();
            checkChart = new GHelper.UI.RCheckBox();
            checkPower = new GHelper.UI.RCheckBox();
            checkUsage = new GHelper.UI.RCheckBox();
            checkRam = new GHelper.UI.RCheckBox();
            checkBattery = new GHelper.UI.RCheckBox();
            checkLabels = new GHelper.UI.RCheckBox();
            check1PercentLow = new GHelper.UI.RCheckBox();
            checkJitter = new GHelper.UI.RCheckBox();
            checkShadow = new GHelper.UI.RCheckBox();
            checkHdrBadge = new GHelper.UI.RCheckBox();
            checkFrametime = new GHelper.UI.RCheckBox();
            checkClock = new GHelper.UI.RCheckBox();
            checkVram = new GHelper.UI.RCheckBox();
            checkMidFan = new GHelper.UI.RCheckBox();
            panelSliders = new TableLayoutPanel();
            panelSizeHeader = new Panel();
            panelAlphaHeader = new Panel();
            labelSizeTitle = new Label();
            labelSize = new Label();
            trackScale = new RTrackBar();
            labelAlphaTitle = new Label();
            labelAlpha = new Label();
            trackAlpha = new RTrackBar();
            tableAppearance = new TableLayoutPanel();
            labelFontTitle = new Label();
            comboFont = new GHelper.UI.RComboBox();
            labelStyleTitle = new Label();
            comboStyle = new GHelper.UI.RComboBox();
            tableColors = new TableLayoutPanel();
            buttonCpuColor = new GHelper.UI.RColorButton();
            buttonGpuColor = new GHelper.UI.RColorButton();
            buttonReset = new GHelper.UI.RButton();
            tableCorners = new TableLayoutPanel();
            buttonTopLeft = new GHelper.UI.RButton();
            buttonTopRight = new GHelper.UI.RButton();
            buttonBottomLeft = new GHelper.UI.RButton();
            buttonBottomRight = new GHelper.UI.RButton();
            panelHotkeys = new Panel();
            labelHotkeys = new Label();
            panelToggles.SuspendLayout();
            tableModes.SuspendLayout();
            tableBlocks.SuspendLayout();
            panelSliders.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)trackScale).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trackAlpha).BeginInit();
            tableColors.SuspendLayout();
            tableCorners.SuspendLayout();
            panelHotkeys.SuspendLayout();
            SuspendLayout();
            //
            // panelToggles
            //
            panelToggles.AutoSize = true;
            panelToggles.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelToggles.ColumnCount = 2;
            panelToggles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            panelToggles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            panelToggles.Controls.Add(checkEnable, 0, 0);
            panelToggles.Controls.Add(checkGameOnly, 1, 0);
            panelToggles.Dock = DockStyle.Top;
            panelToggles.Location = new Point(10, 10);
            panelToggles.Margin = new Padding(0, 0, 0, 8);
            panelToggles.Name = "panelToggles";
            panelToggles.RowCount = 1;
            panelToggles.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panelToggles.Size = new Size(880, 60);
            panelToggles.TabIndex = 0;
            //
            // checkEnable
            //
            checkEnable.AutoSize = true;
            checkEnable.Dock = DockStyle.Fill;
            checkEnable.Location = new Point(2, 2);
            checkEnable.Margin = new Padding(2);
            checkEnable.Name = "checkEnable";
            checkEnable.Padding = new Padding(6, 6, 6, 6);
            checkEnable.Size = new Size(436, 48);
            checkEnable.TabIndex = 0;
            checkEnable.Text = "Enable In-Game Overlay";
            checkEnable.UseVisualStyleBackColor = false;
            //
            // checkGameOnly
            //
            checkGameOnly.AutoSize = true;
            checkGameOnly.Dock = DockStyle.Fill;
            checkGameOnly.Location = new Point(442, 2);
            checkGameOnly.Margin = new Padding(2);
            checkGameOnly.Name = "checkGameOnly";
            checkGameOnly.Padding = new Padding(6, 6, 6, 6);
            checkGameOnly.Size = new Size(436, 48);
            checkGameOnly.TabIndex = 1;
            checkGameOnly.Text = "Show Only in Games";
            checkGameOnly.UseVisualStyleBackColor = false;
            //
            // tableModes
            //
            tableModes.AutoSize = true;
            tableModes.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tableModes.ColumnCount = 5;
            tableModes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tableModes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tableModes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tableModes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tableModes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tableModes.Controls.Add(buttonLight, 0, 0);
            tableModes.Controls.Add(buttonDefault, 1, 0);
            tableModes.Controls.Add(buttonFull, 2, 0);
            tableModes.Controls.Add(buttonComplete, 3, 0);
            tableModes.Controls.Add(buttonBar, 4, 0);
            tableModes.Dock = DockStyle.Top;
            tableModes.Location = new Point(10, 80);
            tableModes.Name = "tableModes";
            tableModes.RowCount = 1;
            tableModes.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            tableModes.Size = new Size(880, 64);
            tableModes.TabIndex = 1;
            //
            // buttonLight
            //
            buttonLight.Activated = false;
            buttonLight.BackColor = SystemColors.ControlLight;
            buttonLight.BorderColor = Color.Transparent;
            buttonLight.BorderRadius = 2;
            buttonLight.Dock = DockStyle.Fill;
            buttonLight.FlatAppearance.BorderSize = 0;
            buttonLight.FlatStyle = FlatStyle.Flat;
            buttonLight.ForeColor = SystemColors.ControlText;
            buttonLight.Location = new Point(4, 4);
            buttonLight.Margin = new Padding(4, 2, 4, 2);
            buttonLight.Name = "buttonLight";
            buttonLight.Secondary = true;
            buttonLight.Size = new Size(168, 58);
            buttonLight.TabIndex = 0;
            buttonLight.Text = "Light";
            buttonLight.UseVisualStyleBackColor = false;
            //
            // buttonDefault
            //
            buttonDefault.Activated = false;
            buttonDefault.BackColor = SystemColors.ControlLight;
            buttonDefault.BorderColor = Color.Transparent;
            buttonDefault.BorderRadius = 2;
            buttonDefault.Dock = DockStyle.Fill;
            buttonDefault.FlatAppearance.BorderSize = 0;
            buttonDefault.FlatStyle = FlatStyle.Flat;
            buttonDefault.ForeColor = SystemColors.ControlText;
            buttonDefault.Location = new Point(224, 4);
            buttonDefault.Margin = new Padding(4, 2, 4, 2);
            buttonDefault.Name = "buttonDefault";
            buttonDefault.Secondary = true;
            buttonDefault.Size = new Size(212, 58);
            buttonDefault.TabIndex = 1;
            buttonDefault.Text = "Default";
            buttonDefault.UseVisualStyleBackColor = false;
            //
            // buttonFull
            //
            buttonFull.Activated = false;
            buttonFull.BackColor = SystemColors.ControlLight;
            buttonFull.BorderColor = Color.Transparent;
            buttonFull.BorderRadius = 2;
            buttonFull.Dock = DockStyle.Fill;
            buttonFull.FlatAppearance.BorderSize = 0;
            buttonFull.FlatStyle = FlatStyle.Flat;
            buttonFull.ForeColor = SystemColors.ControlText;
            buttonFull.Location = new Point(444, 4);
            buttonFull.Margin = new Padding(4, 2, 4, 2);
            buttonFull.Name = "buttonFull";
            buttonFull.Secondary = true;
            buttonFull.Size = new Size(212, 58);
            buttonFull.TabIndex = 2;
            buttonFull.Text = "Full";
            buttonFull.UseVisualStyleBackColor = false;
            //
            // buttonComplete
            //
            buttonComplete.Activated = false;
            buttonComplete.BackColor = SystemColors.ControlLight;
            buttonComplete.BorderColor = Color.Transparent;
            buttonComplete.BorderRadius = 2;
            buttonComplete.Dock = DockStyle.Fill;
            buttonComplete.FlatAppearance.BorderSize = 0;
            buttonComplete.FlatStyle = FlatStyle.Flat;
            buttonComplete.ForeColor = SystemColors.ControlText;
            buttonComplete.Location = new Point(664, 4);
            buttonComplete.Margin = new Padding(4, 2, 4, 2);
            buttonComplete.Name = "buttonComplete";
            buttonComplete.Secondary = true;
            buttonComplete.Size = new Size(168, 58);
            buttonComplete.TabIndex = 3;
            buttonComplete.Text = "Complete";
            buttonComplete.UseVisualStyleBackColor = false;
            //
            // buttonBar
            //
            buttonBar.Activated = false;
            buttonBar.BackColor = SystemColors.ControlLight;
            buttonBar.BorderColor = Color.Transparent;
            buttonBar.BorderRadius = 2;
            buttonBar.Dock = DockStyle.Fill;
            buttonBar.FlatAppearance.BorderSize = 0;
            buttonBar.FlatStyle = FlatStyle.Flat;
            buttonBar.ForeColor = SystemColors.ControlText;
            buttonBar.Location = new Point(704, 4);
            buttonBar.Margin = new Padding(4, 2, 4, 2);
            buttonBar.Name = "buttonBar";
            buttonBar.Secondary = true;
            buttonBar.Size = new Size(168, 58);
            buttonBar.TabIndex = 4;
            buttonBar.Text = "Strip";
            buttonBar.UseVisualStyleBackColor = false;
            //
            // tableBlocks
            //
            tableBlocks.AutoSize = true;
            tableBlocks.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tableBlocks.ColumnCount = 3;
            tableBlocks.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            tableBlocks.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            tableBlocks.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
            tableBlocks.Controls.Add(checkFps, 0, 0);
            tableBlocks.Controls.Add(checkTemp, 1, 0);
            tableBlocks.Controls.Add(checkFans, 2, 0);
            tableBlocks.Controls.Add(checkChart, 0, 1);
            tableBlocks.Controls.Add(checkPower, 1, 1);
            tableBlocks.Controls.Add(checkUsage, 2, 1);
            tableBlocks.Controls.Add(checkRam, 0, 2);
            tableBlocks.Controls.Add(checkBattery, 1, 2);
            tableBlocks.Controls.Add(checkLabels, 2, 2);
            tableBlocks.Controls.Add(check1PercentLow, 0, 3);
            tableBlocks.Controls.Add(checkJitter, 1, 3);
            tableBlocks.Controls.Add(checkShadow, 2, 3);
            tableBlocks.Controls.Add(checkHdrBadge, 0, 4);
            tableBlocks.Controls.Add(checkFrametime, 1, 4);
            tableBlocks.Controls.Add(checkClock, 2, 4);
            tableBlocks.Controls.Add(checkVram, 0, 5);
            tableBlocks.Controls.Add(checkMidFan, 1, 5);
            tableBlocks.Dock = DockStyle.Top;
            tableBlocks.Location = new Point(10, 160);
            tableBlocks.Name = "tableBlocks";
            tableBlocks.RowCount = 6;
            tableBlocks.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            tableBlocks.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            tableBlocks.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            tableBlocks.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            tableBlocks.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            tableBlocks.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            tableBlocks.Size = new Size(880, 276);
            tableBlocks.TabIndex = 2;
            //
            // checkFps
            //
            checkFps.Anchor = AnchorStyles.Left;
            checkFps.AutoSize = true;
            checkFps.Location = new Point(10, 6);
            checkFps.Margin = new Padding(10, 2, 4, 2);
            checkFps.Name = "checkFps";
            checkFps.Size = new Size(120, 48);
            checkFps.TabIndex = 0;
            checkFps.Text = "FPS";
            //
            // checkTemp
            //
            checkTemp.Anchor = AnchorStyles.Left;
            checkTemp.AutoSize = true;
            checkTemp.Location = new Point(303, 6);
            checkTemp.Margin = new Padding(10, 2, 4, 2);
            checkTemp.Name = "checkTemp";
            checkTemp.Size = new Size(200, 48);
            checkTemp.TabIndex = 1;
            checkTemp.Text = "Temperatures";
            //
            // checkFans
            //
            checkFans.Anchor = AnchorStyles.Left;
            checkFans.AutoSize = true;
            checkFans.Location = new Point(596, 6);
            checkFans.Margin = new Padding(10, 2, 4, 2);
            checkFans.Name = "checkFans";
            checkFans.Size = new Size(120, 48);
            checkFans.TabIndex = 2;
            checkFans.Text = "Fan";
            //
            // checkChart
            //
            checkChart.Anchor = AnchorStyles.Left;
            checkChart.AutoSize = true;
            checkChart.Location = new Point(10, 66);
            checkChart.Margin = new Padding(10, 2, 4, 2);
            checkChart.Name = "checkChart";
            checkChart.Size = new Size(130, 48);
            checkChart.TabIndex = 3;
            checkChart.Text = "Chart";
            //
            // checkPower
            //
            checkPower.Anchor = AnchorStyles.Left;
            checkPower.AutoSize = true;
            checkPower.Location = new Point(303, 66);
            checkPower.Margin = new Padding(10, 2, 4, 2);
            checkPower.Name = "checkPower";
            checkPower.Size = new Size(130, 48);
            checkPower.TabIndex = 4;
            checkPower.Text = "Power";
            //
            // checkUsage
            //
            checkUsage.Anchor = AnchorStyles.Left;
            checkUsage.AutoSize = true;
            checkUsage.Location = new Point(596, 66);
            checkUsage.Margin = new Padding(10, 2, 4, 2);
            checkUsage.Name = "checkUsage";
            checkUsage.Size = new Size(120, 48);
            checkUsage.TabIndex = 5;
            checkUsage.Text = "Load";
            //
            // checkRam
            //
            checkRam.Anchor = AnchorStyles.Left;
            checkRam.AutoSize = true;
            checkRam.Location = new Point(10, 126);
            checkRam.Margin = new Padding(10, 2, 4, 2);
            checkRam.Name = "checkRam";
            checkRam.Size = new Size(120, 48);
            checkRam.TabIndex = 6;
            checkRam.Text = "RAM";
            //
            // checkBattery
            //
            checkBattery.Anchor = AnchorStyles.Left;
            checkBattery.AutoSize = true;
            checkBattery.Location = new Point(303, 126);
            checkBattery.Margin = new Padding(10, 2, 4, 2);
            checkBattery.Name = "checkBattery";
            checkBattery.Size = new Size(140, 48);
            checkBattery.TabIndex = 7;
            checkBattery.Text = "Battery";
            checkBattery.ThreeState = true;
            //
            // checkLabels
            //
            checkLabels.Anchor = AnchorStyles.Left;
            checkLabels.AutoSize = true;
            checkLabels.Location = new Point(596, 126);
            checkLabels.Margin = new Padding(10, 2, 4, 2);
            checkLabels.Name = "checkLabels";
            checkLabels.Size = new Size(130, 48);
            checkLabels.TabIndex = 8;
            checkLabels.Text = "Labels";
            //
            // check1PercentLow
            //
            check1PercentLow.Anchor = AnchorStyles.Left;
            check1PercentLow.AutoSize = true;
            check1PercentLow.Location = new Point(10, 142);
            check1PercentLow.Margin = new Padding(10, 2, 4, 2);
            check1PercentLow.Name = "check1PercentLow";
            check1PercentLow.Size = new Size(160, 44);
            check1PercentLow.TabIndex = 9;
            check1PercentLow.Text = "1% Low FPS";
            //
            // checkJitter
            //
            checkJitter.Anchor = AnchorStyles.Left;
            checkJitter.AutoSize = true;
            checkJitter.Location = new Point(303, 142);
            checkJitter.Margin = new Padding(10, 2, 4, 2);
            checkJitter.Name = "checkJitter";
            checkJitter.Size = new Size(160, 44);
            checkJitter.TabIndex = 10;
            checkJitter.Text = "Frame Pacing";
            //
            // checkShadow
            //
            checkShadow.Anchor = AnchorStyles.Left;
            checkShadow.AutoSize = true;
            checkShadow.Location = new Point(596, 142);
            checkShadow.Margin = new Padding(10, 2, 4, 2);
            checkShadow.Name = "checkShadow";
            checkShadow.Size = new Size(160, 44);
            checkShadow.TabIndex = 11;
            checkShadow.Text = "Text Shadow";
            //
            // checkHdrBadge
            //
            checkHdrBadge.Anchor = AnchorStyles.Left;
            checkHdrBadge.AutoSize = true;
            checkHdrBadge.Location = new Point(10, 188);
            checkHdrBadge.Margin = new Padding(10, 2, 4, 2);
            checkHdrBadge.Name = "checkHdrBadge";
            checkHdrBadge.Size = new Size(200, 44);
            checkHdrBadge.TabIndex = 12;
            checkHdrBadge.Text = "HDR / Mini-LED Badge";
            // 
            // checkFrametime
            // 
            checkFrametime.Anchor = AnchorStyles.Left;
            checkFrametime.AutoSize = true;
            checkFrametime.Location = new Point(303, 188);
            checkFrametime.Margin = new Padding(10, 2, 4, 2);
            checkFrametime.Name = "checkFrametime";
            checkFrametime.Size = new Size(200, 44);
            checkFrametime.TabIndex = 13;
            checkFrametime.Text = "Frametime (ms)";
            // 
            // checkClock
            // 
            checkClock.Anchor = AnchorStyles.Left;
            checkClock.AutoSize = true;
            checkClock.Location = new Point(596, 188);
            checkClock.Margin = new Padding(10, 2, 4, 2);
            checkClock.Name = "checkClock";
            checkClock.Size = new Size(200, 44);
            checkClock.TabIndex = 14;
            checkClock.Text = "System Clock";
            // 
            // checkVram
            // 
            checkVram.Anchor = AnchorStyles.Left;
            checkVram.AutoSize = true;
            checkVram.Location = new Point(10, 234);
            checkVram.Margin = new Padding(10, 2, 4, 2);
            checkVram.Name = "checkVram";
            checkVram.Size = new Size(200, 44);
            checkVram.TabIndex = 15;
            checkVram.Text = "VRAM Usage";
            // 
            // checkMidFan
            // 
            checkMidFan.Anchor = AnchorStyles.Left;
            checkMidFan.AutoSize = true;
            checkMidFan.Location = new Point(303, 234);
            checkMidFan.Margin = new Padding(10, 2, 4, 2);
            checkMidFan.Name = "checkMidFan";
            checkMidFan.Size = new Size(200, 44);
            checkMidFan.TabIndex = 16;
            checkMidFan.Text = "Mid Fan RPM";
            //
            // panelSliders (tableSliders)
            //
            panelSliders.AutoSize = true;
            panelSliders.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelSliders.ColumnCount = 2;
            panelSliders.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            panelSliders.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            panelSliders.Controls.Add(panelSizeHeader, 0, 0);
            panelSliders.Controls.Add(panelAlphaHeader, 1, 0);
            panelSliders.Controls.Add(trackScale, 0, 1);
            panelSliders.Controls.Add(trackAlpha, 1, 1);
            panelSliders.Dock = DockStyle.Top;
            panelSliders.Location = new Point(10, 340);
            panelSliders.Margin = new Padding(0, 0, 0, 8);
            panelSliders.Name = "panelSliders";
            panelSliders.RowCount = 2;
            panelSliders.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            panelSliders.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            panelSliders.Size = new Size(880, 86);
            panelSliders.TabIndex = 3;
            //
            // panelSizeHeader
            //
            panelSizeHeader.Controls.Add(labelSizeTitle);
            panelSizeHeader.Controls.Add(labelSize);
            panelSizeHeader.Dock = DockStyle.Fill;
            panelSizeHeader.Location = new Point(2, 2);
            panelSizeHeader.Margin = new Padding(2);
            panelSizeHeader.Name = "panelSizeHeader";
            panelSizeHeader.Size = new Size(436, 30);
            //
            // labelSizeTitle
            //
            labelSizeTitle.AutoSize = true;
            labelSizeTitle.Dock = DockStyle.Left;
            labelSizeTitle.Location = new Point(0, 0);
            labelSizeTitle.Name = "labelSizeTitle";
            labelSizeTitle.Size = new Size(110, 28);
            labelSizeTitle.TabIndex = 0;
            labelSizeTitle.Text = "HUD Scale Size:";
            //
            // labelSize
            //
            labelSize.AutoSize = true;
            labelSize.Dock = DockStyle.Right;
            labelSize.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            labelSize.Location = new Point(376, 0);
            labelSize.Name = "labelSize";
            labelSize.Size = new Size(60, 28);
            labelSize.TabIndex = 1;
            labelSize.Text = "100%";
            //
            // panelAlphaHeader
            //
            panelAlphaHeader.Controls.Add(labelAlphaTitle);
            panelAlphaHeader.Controls.Add(labelAlpha);
            panelAlphaHeader.Dock = DockStyle.Fill;
            panelAlphaHeader.Location = new Point(442, 2);
            panelAlphaHeader.Margin = new Padding(2);
            panelAlphaHeader.Name = "panelAlphaHeader";
            panelAlphaHeader.Size = new Size(436, 30);
            //
            // labelAlphaTitle
            //
            labelAlphaTitle.AutoSize = true;
            labelAlphaTitle.Dock = DockStyle.Left;
            labelAlphaTitle.Location = new Point(0, 0);
            labelAlphaTitle.Name = "labelAlphaTitle";
            labelAlphaTitle.Size = new Size(140, 28);
            labelAlphaTitle.TabIndex = 2;
            labelAlphaTitle.Text = "HUD Transparency:";
            //
            // labelAlpha
            //
            labelAlpha.AutoSize = true;
            labelAlpha.Dock = DockStyle.Right;
            labelAlpha.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            labelAlpha.Location = new Point(386, 0);
            labelAlpha.Name = "labelAlpha";
            labelAlpha.Size = new Size(50, 28);
            labelAlpha.TabIndex = 3;
            labelAlpha.Text = "50%";
            //
            // trackScale
            //
            trackScale.Dock = DockStyle.Fill;
            trackScale.LargeChange = 25;
            trackScale.Location = new Point(2, 36);
            trackScale.Margin = new Padding(2);
            trackScale.Maximum = 300;
            trackScale.Minimum = 35;
            trackScale.Name = "trackScale";
            trackScale.Size = new Size(436, 48);
            trackScale.SmallChange = 5;
            trackScale.TabIndex = 4;
            trackScale.TickFrequency = 25;
            trackScale.TickStyle = TickStyle.None;
            trackScale.Value = 100;
            //
            // trackAlpha
            //
            trackAlpha.Dock = DockStyle.Fill;
            trackAlpha.LargeChange = 32;
            trackAlpha.Location = new Point(442, 36);
            trackAlpha.Margin = new Padding(2);
            trackAlpha.Maximum = 255;
            trackAlpha.Name = "trackAlpha";
            trackAlpha.Size = new Size(436, 48);
            trackAlpha.SmallChange = 8;
            trackAlpha.TabIndex = 5;
            trackAlpha.TickFrequency = 16;
            trackAlpha.TickStyle = TickStyle.None;
            trackAlpha.Value = 128;
            //
            // tableAppearance
            //
            tableAppearance.AutoSize = true;
            tableAppearance.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tableAppearance.ColumnCount = 2;
            tableAppearance.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableAppearance.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableAppearance.Controls.Add(labelFontTitle, 0, 0);
            tableAppearance.Controls.Add(labelStyleTitle, 1, 0);
            tableAppearance.Controls.Add(comboFont, 0, 1);
            tableAppearance.Controls.Add(comboStyle, 1, 1);
            tableAppearance.Dock = DockStyle.Top;
            tableAppearance.Location = new Point(10, 440);
            tableAppearance.Margin = new Padding(0, 0, 0, 8);
            tableAppearance.Name = "tableAppearance";
            tableAppearance.RowCount = 2;
            tableAppearance.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            tableAppearance.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            tableAppearance.Size = new Size(880, 78);
            tableAppearance.TabIndex = 4;
            // 
            // labelFontTitle
            // 
            labelFontTitle.AutoSize = true;
            labelFontTitle.Dock = DockStyle.Fill;
            labelFontTitle.Location = new Point(2, 2);
            labelFontTitle.Margin = new Padding(2);
            labelFontTitle.Name = "labelFontTitle";
            labelFontTitle.Size = new Size(436, 28);
            labelFontTitle.TabIndex = 0;
            labelFontTitle.Text = "HUD Typography Font:";
            // 
            // labelStyleTitle
            // 
            labelStyleTitle.AutoSize = true;
            labelStyleTitle.Dock = DockStyle.Fill;
            labelStyleTitle.Location = new Point(442, 2);
            labelStyleTitle.Margin = new Padding(2);
            labelStyleTitle.Name = "labelStyleTitle";
            labelStyleTitle.Size = new Size(436, 28);
            labelStyleTitle.TabIndex = 1;
            labelStyleTitle.Text = "HUD Theme && Backdrop Style:";
            // 
            // comboFont
            // 
            comboFont.Dock = DockStyle.Fill;
            comboFont.DropDownStyle = ComboBoxStyle.DropDownList;
            comboFont.FlatStyle = FlatStyle.Flat;
            comboFont.Location = new Point(2, 34);
            comboFont.Margin = new Padding(2);
            comboFont.Name = "comboFont";
            comboFont.Size = new Size(436, 38);
            comboFont.TabIndex = 2;
            // 
            // comboStyle
            // 
            comboStyle.Dock = DockStyle.Fill;
            comboStyle.DropDownStyle = ComboBoxStyle.DropDownList;
            comboStyle.FlatStyle = FlatStyle.Flat;
            comboStyle.Location = new Point(442, 34);
            comboStyle.Margin = new Padding(2);
            comboStyle.Name = "comboStyle";
            comboStyle.Size = new Size(436, 38);
            comboStyle.TabIndex = 3;
            //
            // tableColors
            //
            tableColors.AutoSize = true;
            tableColors.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tableColors.ColumnCount = 3;
            tableColors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            tableColors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            tableColors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
            tableColors.Controls.Add(buttonCpuColor, 0, 0);
            tableColors.Controls.Add(buttonGpuColor, 1, 0);
            tableColors.Controls.Add(buttonReset, 2, 0);
            tableColors.Dock = DockStyle.Top;
            tableColors.Location = new Point(10, 460);
            tableColors.Name = "tableColors";
            tableColors.RowCount = 1;
            tableColors.RowStyles.Add(new RowStyle(SizeType.Absolute, 63F));
            tableColors.Size = new Size(880, 63);
            tableColors.TabIndex = 4;
            //
            // buttonCpuColor
            //
            buttonCpuColor.Activated = false;
            buttonCpuColor.BackColor = SystemColors.ControlLight;
            buttonCpuColor.BorderColor = Color.Transparent;
            buttonCpuColor.BorderRadius = 2;
            buttonCpuColor.Dock = DockStyle.Fill;
            buttonCpuColor.FlatAppearance.BorderSize = 0;
            buttonCpuColor.FlatStyle = FlatStyle.Flat;
            buttonCpuColor.ForeColor = SystemColors.ControlText;
            buttonCpuColor.Location = new Point(4, 4);
            buttonCpuColor.Margin = new Padding(4);
            buttonCpuColor.Name = "buttonCpuColor";
            buttonCpuColor.Secondary = true;
            buttonCpuColor.Size = new Size(285, 55);
            buttonCpuColor.TabIndex = 0;
            buttonCpuColor.Text = "CPU Color";
            buttonCpuColor.UseVisualStyleBackColor = false;
            //
            // buttonGpuColor
            //
            buttonGpuColor.Activated = false;
            buttonGpuColor.BackColor = SystemColors.ControlLight;
            buttonGpuColor.BorderColor = Color.Transparent;
            buttonGpuColor.BorderRadius = 2;
            buttonGpuColor.Dock = DockStyle.Fill;
            buttonGpuColor.FlatAppearance.BorderSize = 0;
            buttonGpuColor.FlatStyle = FlatStyle.Flat;
            buttonGpuColor.ForeColor = SystemColors.ControlText;
            buttonGpuColor.Location = new Point(297, 4);
            buttonGpuColor.Margin = new Padding(4);
            buttonGpuColor.Name = "buttonGpuColor";
            buttonGpuColor.Secondary = true;
            buttonGpuColor.Size = new Size(285, 55);
            buttonGpuColor.TabIndex = 1;
            buttonGpuColor.Text = "GPU Color";
            buttonGpuColor.UseVisualStyleBackColor = false;
            //
            // buttonReset
            //
            buttonReset.Activated = false;
            buttonReset.BackColor = SystemColors.ControlLight;
            buttonReset.BorderColor = Color.Transparent;
            buttonReset.BorderRadius = 2;
            buttonReset.Dock = DockStyle.Fill;
            buttonReset.FlatAppearance.BorderSize = 0;
            buttonReset.FlatStyle = FlatStyle.Flat;
            buttonReset.ForeColor = SystemColors.ControlText;
            buttonReset.Location = new Point(590, 4);
            buttonReset.Margin = new Padding(4);
            buttonReset.Name = "buttonReset";
            buttonReset.Secondary = true;
            buttonReset.Size = new Size(286, 55);
            buttonReset.TabIndex = 2;
            buttonReset.Text = "Reset";
            buttonReset.UseVisualStyleBackColor = false;
            //
            // tableCorners
            //
            tableCorners.AutoSize = true;
            tableCorners.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tableCorners.ColumnCount = 4;
            tableCorners.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tableCorners.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tableCorners.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tableCorners.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tableCorners.Controls.Add(buttonTopLeft, 0, 0);
            tableCorners.Controls.Add(buttonTopRight, 1, 0);
            tableCorners.Controls.Add(buttonBottomLeft, 2, 0);
            tableCorners.Controls.Add(buttonBottomRight, 3, 0);
            tableCorners.Dock = DockStyle.Top;
            tableCorners.Location = new Point(10, 525);
            tableCorners.Name = "tableCorners";
            tableCorners.RowCount = 1;
            tableCorners.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            tableCorners.Size = new Size(880, 58);
            tableCorners.TabIndex = 5;
            //
            // buttonTopLeft
            //
            buttonTopLeft.Activated = false;
            buttonTopLeft.BackColor = SystemColors.ControlLight;
            buttonTopLeft.BorderColor = Color.Transparent;
            buttonTopLeft.BorderRadius = 2;
            buttonTopLeft.Dock = DockStyle.Fill;
            buttonTopLeft.FlatAppearance.BorderSize = 0;
            buttonTopLeft.FlatStyle = FlatStyle.Flat;
            buttonTopLeft.ForeColor = SystemColors.ControlText;
            buttonTopLeft.Location = new Point(4, 4);
            buttonTopLeft.Margin = new Padding(4);
            buttonTopLeft.Name = "buttonTopLeft";
            buttonTopLeft.Secondary = true;
            buttonTopLeft.Size = new Size(212, 50);
            buttonTopLeft.TabIndex = 0;
            buttonTopLeft.Text = "↖ Top-Left";
            buttonTopLeft.UseVisualStyleBackColor = false;
            //
            // buttonTopRight
            //
            buttonTopRight.Activated = false;
            buttonTopRight.BackColor = SystemColors.ControlLight;
            buttonTopRight.BorderColor = Color.Transparent;
            buttonTopRight.BorderRadius = 2;
            buttonTopRight.Dock = DockStyle.Fill;
            buttonTopRight.FlatAppearance.BorderSize = 0;
            buttonTopRight.FlatStyle = FlatStyle.Flat;
            buttonTopRight.ForeColor = SystemColors.ControlText;
            buttonTopRight.Location = new Point(224, 4);
            buttonTopRight.Margin = new Padding(4);
            buttonTopRight.Name = "buttonTopRight";
            buttonTopRight.Secondary = true;
            buttonTopRight.Size = new Size(212, 50);
            buttonTopRight.TabIndex = 1;
            buttonTopRight.Text = "↗ Top-Right";
            buttonTopRight.UseVisualStyleBackColor = false;
            //
            // buttonBottomLeft
            //
            buttonBottomLeft.Activated = false;
            buttonBottomLeft.BackColor = SystemColors.ControlLight;
            buttonBottomLeft.BorderColor = Color.Transparent;
            buttonBottomLeft.BorderRadius = 2;
            buttonBottomLeft.Dock = DockStyle.Fill;
            buttonBottomLeft.FlatAppearance.BorderSize = 0;
            buttonBottomLeft.FlatStyle = FlatStyle.Flat;
            buttonBottomLeft.ForeColor = SystemColors.ControlText;
            buttonBottomLeft.Location = new Point(444, 4);
            buttonBottomLeft.Margin = new Padding(4);
            buttonBottomLeft.Name = "buttonBottomLeft";
            buttonBottomLeft.Secondary = true;
            buttonBottomLeft.Size = new Size(212, 50);
            buttonBottomLeft.TabIndex = 2;
            buttonBottomLeft.Text = "↙ Bottom-Left";
            buttonBottomLeft.UseVisualStyleBackColor = false;
            //
            // buttonBottomRight
            //
            buttonBottomRight.Activated = false;
            buttonBottomRight.BackColor = SystemColors.ControlLight;
            buttonBottomRight.BorderColor = Color.Transparent;
            buttonBottomRight.BorderRadius = 2;
            buttonBottomRight.Dock = DockStyle.Fill;
            buttonBottomRight.FlatAppearance.BorderSize = 0;
            buttonBottomRight.FlatStyle = FlatStyle.Flat;
            buttonBottomRight.ForeColor = SystemColors.ControlText;
            buttonBottomRight.Location = new Point(664, 4);
            buttonBottomRight.Margin = new Padding(4);
            buttonBottomRight.Name = "buttonBottomRight";
            buttonBottomRight.Secondary = true;
            buttonBottomRight.Size = new Size(212, 50);
            buttonBottomRight.TabIndex = 3;
            buttonBottomRight.Text = "↘ Bottom-Right";
            buttonBottomRight.UseVisualStyleBackColor = false;
            //
            // panelHotkeys
            //
            panelHotkeys.Controls.Add(labelHotkeys);
            panelHotkeys.Dock = DockStyle.Top;
            panelHotkeys.Location = new Point(10, 425);
            panelHotkeys.Name = "panelHotkeys";
            panelHotkeys.Size = new Size(880, 180);
            panelHotkeys.TabIndex = 6;
            //
            // labelHotkeys
            //
            labelHotkeys.AutoSize = true;
            labelHotkeys.Font = new Font("Consolas", 9F);
            labelHotkeys.ForeColor = SystemColors.ControlDarkDark;
            labelHotkeys.Location = new Point(10, 8);
            labelHotkeys.Margin = new Padding(4, 0, 4, 0);
            labelHotkeys.Name = "labelHotkeys";
            labelHotkeys.Size = new Size(400, 160);
            labelHotkeys.TabIndex = 0;
            //
            // OverlayConfig
            //
            AutoScaleDimensions = new SizeF(192F, 192F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(900, 840);
            AutoScroll = true;
            Controls.Add(panelHotkeys);
            Controls.Add(tableColors);
            Controls.Add(tableCorners);
            Controls.Add(tableAppearance);
            Controls.Add(panelSliders);
            Controls.Add(tableBlocks);
            Controls.Add(tableModes);
            Controls.Add(panelToggles);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "OverlayConfig";
            Padding = new Padding(10);
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = "Overlay";
            panelToggles.ResumeLayout(false);
            panelToggles.PerformLayout();
            tableModes.ResumeLayout(false);
            tableBlocks.ResumeLayout(false);
            tableBlocks.PerformLayout();
            panelSliders.ResumeLayout(false);
            panelSliders.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)trackScale).EndInit();
            ((System.ComponentModel.ISupportInitialize)trackAlpha).EndInit();
            tableColors.ResumeLayout(false);
            tableCorners.ResumeLayout(false);
            panelHotkeys.ResumeLayout(false);
            panelHotkeys.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TableLayoutPanel panelToggles;
        private UI.RCheckBox checkEnable;
        private UI.RCheckBox checkGameOnly;
        private TableLayoutPanel tableModes;
        private UI.RButton buttonLight;
        private UI.RButton buttonDefault;
        private UI.RButton buttonFull;
        private UI.RButton buttonComplete;
        private UI.RButton buttonBar;
        private TableLayoutPanel tableBlocks;
        private UI.RCheckBox checkFps;
        private UI.RCheckBox checkTemp;
        private UI.RCheckBox checkFans;
        private UI.RCheckBox checkChart;
        private UI.RCheckBox checkPower;
        private UI.RCheckBox checkUsage;
        private UI.RCheckBox checkRam;
        private UI.RCheckBox checkBattery;
        private UI.RCheckBox checkLabels;
        private UI.RCheckBox check1PercentLow;
        private UI.RCheckBox checkJitter;
        private UI.RCheckBox checkShadow;
        private UI.RCheckBox checkHdrBadge;
        private UI.RCheckBox checkFrametime;
        private UI.RCheckBox checkClock;
        private UI.RCheckBox checkVram;
        private UI.RCheckBox checkMidFan;
        private TableLayoutPanel panelSliders;
        private Label labelSizeTitle;
        private Label labelSize;
        private RTrackBar trackScale;
        private Label labelAlphaTitle;
        private Label labelAlpha;
        private RTrackBar trackAlpha;
        private Panel panelSizeHeader;
        private Panel panelAlphaHeader;
        private TableLayoutPanel tableAppearance;
        private Label labelFontTitle;
        private UI.RComboBox comboFont;
        private Label labelStyleTitle;
        private UI.RComboBox comboStyle;
        private TableLayoutPanel tableColors;
        private UI.RColorButton buttonCpuColor;
        private UI.RColorButton buttonGpuColor;
        private UI.RButton buttonReset;
        private TableLayoutPanel tableCorners;
        private UI.RButton buttonTopLeft;
        private UI.RButton buttonTopRight;
        private UI.RButton buttonBottomLeft;
        private UI.RButton buttonBottomRight;
        private Panel panelHotkeys;
        private Label labelHotkeys;
    }
}
