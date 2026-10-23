using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using GHelper.Helpers;
using GHelper.UI;

namespace GHelper.Battery
{
    public class BatteryHealthForm : RForm
    {
        private Label labelHealthPercent;
        private Label labelHealthStatus;
        private Label labelCapacities;
        private Panel panelHealthBar;

        private Label labelSourceVal;
        private Label labelChargeVal;
        private Label labelRateVal;
        private Label labelAvgRateVal;
        private Label labelTimeVal;
        private Label labelLimitVal;
        private Label labelProtectionVal;
        private Label labelHardwareVal;

        private System.Windows.Forms.Timer liveTimer;
        private (string Manufacturer, string Chemistry, int VoltageMv) _hwInfo;

        public BatteryHealthForm()
        {
            this.Text = "Battery Health & Power Telemetry";
            this.Size = new Size(560, 690);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.ShowIcon = false;
            this.TopMost = true;

            InitColors(darkTheme);
            this.BackColor = formBack;
            this.ForeColor = foreMain;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            _hwInfo = HardwareControl.GetBatteryHardwareInfo();

            BuildUI();

            liveTimer = new System.Windows.Forms.Timer { Interval = 1500 };
            liveTimer.Tick += (s, e) => UpdateTelemetry();
            liveTimer.Start();

            this.FormClosing += (s, e) =>
            {
                liveTimer.Stop();
                liveTimer.Dispose();
            };

            UpdateTelemetry();
        }

        private void BuildUI()
        {
            this.Padding = new Padding(20, 16, 20, 16);

            // 1. Top Header
            Panel headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Margin = new Padding(0)
            };

            Label titleLabel = new Label
            {
                Text = "Battery Health & Telemetry",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Dock = DockStyle.Left,
                AutoSize = true,
                ForeColor = foreMain
            };

            Label subTitleLabel = new Label
            {
                Text = "ROG Flow X16 (GV601)",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Dock = DockStyle.Right,
                AutoSize = true,
                ForeColor = colorGray,
                TextAlign = ContentAlignment.MiddleRight
            };

            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(subTitleLabel);
            this.Controls.Add(headerPanel);

            // 2. Health Card (Top Container)
            Panel healthCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = 145,
                BackColor = buttonSecond,
                Margin = new Padding(0, 8, 0, 12),
                Padding = new Padding(16, 12, 16, 12)
            };
            healthCard.Paint += (s, e) =>
            {
                using var pen = new Pen(borderMain, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, healthCard.Width - 1, healthCard.Height - 1);
            };

            labelHealthPercent = new Label
            {
                Font = new Font("Segoe UI", 26F, FontStyle.Bold),
                ForeColor = colorEco,
                AutoSize = true,
                Location = new Point(14, 10),
                Text = "--%"
            };

            labelHealthStatus = new Label
            {
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = colorEco,
                AutoSize = true,
                Location = new Point(160, 20),
                Text = "EXCELLENT HEALTH"
            };

            labelCapacities = new Label
            {
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = colorGray,
                AutoSize = true,
                Location = new Point(160, 42),
                Text = "Design Capacity: -- Wh | Full Charge: -- Wh"
            };

            panelHealthBar = new Panel
            {
                Height = 24,
                Dock = DockStyle.Bottom,
                Margin = new Padding(0),
                BackColor = Color.FromArgb(40, 40, 40)
            };
            panelHealthBar.Paint += PanelHealthBar_Paint;

            healthCard.Controls.Add(labelHealthPercent);
            healthCard.Controls.Add(labelHealthStatus);
            healthCard.Controls.Add(labelCapacities);
            healthCard.Controls.Add(panelHealthBar);

            // Spacer
            Panel spacer1 = new Panel { Dock = DockStyle.Top, Height = 10 };

            // 3. Telemetry Grid (TableLayoutPanel)
            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 8,
                Height = 310,
                BackColor = buttonSecond,
                Padding = new Padding(14, 10, 14, 10),
                Margin = new Padding(0, 0, 0, 12)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
            for (int i = 0; i < 8; i++)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5F));
            }
            grid.Paint += (s, e) =>
            {
                using var pen = new Pen(borderMain, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, grid.Width - 1, grid.Height - 1);
            };

            void AddRow(int row, string title, out Label valLabel)
            {
                Label lblTitle = new Label
                {
                    Text = title,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    ForeColor = colorGray
                };
                valLabel = new Label
                {
                    Text = "--",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                    ForeColor = foreMain
                };
                grid.Controls.Add(lblTitle, 0, row);
                grid.Controls.Add(valLabel, 1, row);
            }

            AddRow(0, "Power Source:", out labelSourceVal);
            AddRow(1, "Current Charge:", out labelChargeVal);
            AddRow(2, "Live Flow Rate:", out labelRateVal);
            AddRow(3, "Session Avg Drain:", out labelAvgRateVal);
            AddRow(4, "Estimated Runtime:", out labelTimeVal);
            AddRow(5, "Hardware Charge Limit:", out labelLimitVal);
            AddRow(6, "Charging Protection:", out labelProtectionVal);
            AddRow(7, "Battery Hardware:", out labelHardwareVal);

            // Spacer
            Panel spacer2 = new Panel { Dock = DockStyle.Top, Height = 14 };

            // 4. Bottom Action Buttons
            Panel buttonBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 42,
                Margin = new Padding(0)
            };

            RButton btnReport = new RButton
            {
                Text = "Full Battery Report",
                Width = 170,
                Dock = DockStyle.Left,
                Secondary = true,
                BorderColor = colorStandard,
                Cursor = Cursors.Hand
            };
            btnReport.Click += (s, e) =>
            {
                HardwareControl.GenerateAndOpenBatteryReport();
            };

            RButton btnRefresh = new RButton
            {
                Text = "Refresh",
                Width = 110,
                Dock = DockStyle.Right,
                Secondary = true,
                BorderColor = colorGray,
                Cursor = Cursors.Hand
            };
            btnRefresh.Click += (s, e) =>
            {
                HardwareControl.ReadBatteryState();
                HardwareControl.RefreshBatteryHealth();
                UpdateTelemetry();
            };

            RButton btnClose = new RButton
            {
                Text = "Close",
                Width = 110,
                Dock = DockStyle.Right,
                Secondary = true,
                Margin = new Padding(8, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            btnClose.Click += (s, e) => this.Close();

            buttonBar.Controls.Add(btnReport);
            buttonBar.Controls.Add(btnRefresh);
            buttonBar.Controls.Add(btnClose);

            // Add in reverse dock order:
            this.Controls.Add(buttonBar);
            this.Controls.Add(grid);
            this.Controls.Add(spacer2);
            this.Controls.Add(spacer1);
            this.Controls.Add(healthCard);
        }

        private void PanelHealthBar_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            decimal health = HardwareControl.batteryHealth;
            if (health <= 0 && HardwareControl.fullCapacity > 0 && HardwareControl.designCapacity > 0)
            {
                health = (HardwareControl.fullCapacity.Value / HardwareControl.designCapacity.Value) * 100m;
            }

            int w = panelHealthBar.Width;
            int h = panelHealthBar.Height;

            // Background
            using (var brushBg = new SolidBrush(Color.FromArgb(45, 45, 48)))
            {
                g.FillRectangle(brushBg, 0, 0, w, h);
            }

            if (health > 0)
            {
                float pct = (float)Math.Clamp(health / 100m, 0m, 1m);
                int fillW = Math.Max(4, (int)(w * pct));

                Color barColor = health >= 85 ? colorEco : (health >= 70 ? colorStandard : colorTurbo);
                using (var brushFill = new LinearGradientBrush(new Rectangle(0, 0, fillW, h), barColor, Color.FromArgb(Math.Min(255, barColor.R + 30), Math.Min(255, barColor.G + 30), Math.Min(255, barColor.B + 30)), LinearGradientMode.Horizontal))
                {
                    g.FillRectangle(brushFill, 0, 0, fillW, h);
                }

                // Draw wear section marker if health < 100
                if (pct < 0.99f)
                {
                    using var pen = new Pen(Color.FromArgb(180, 255, 60, 60), 1.5f);
                    pen.DashStyle = DashStyle.Dot;
                    g.DrawLine(pen, fillW, 0, fillW, h);
                }
            }

            // Outline
            using (var pen = new Pen(borderMain, 1))
            {
                g.DrawRectangle(pen, 0, 0, w - 1, h - 1);
            }
        }

        private void UpdateTelemetry()
        {
            if (IsDisposed || Disposing) return;

            // 1. Health & Capacities
            decimal health = HardwareControl.batteryHealth;
            decimal? fullMwh = HardwareControl.fullCapacity;
            decimal? designMwh = HardwareControl.designCapacity;

            if (health <= 0 && fullMwh > 0 && designMwh > 0)
            {
                health = Math.Round((fullMwh.Value / designMwh.Value) * 100m, 1);
            }

            if (health > 0)
            {
                labelHealthPercent.Text = $"{Math.Round(health, 1)}%";
                if (health >= 85)
                {
                    labelHealthPercent.ForeColor = colorEco;
                    labelHealthStatus.Text = "EXCELLENT HEALTH";
                    labelHealthStatus.ForeColor = colorEco;
                }
                else if (health >= 70)
                {
                    labelHealthPercent.ForeColor = colorStandard;
                    labelHealthStatus.Text = "GOOD HEALTH";
                    labelHealthStatus.ForeColor = colorStandard;
                }
                else
                {
                    labelHealthPercent.ForeColor = colorTurbo;
                    labelHealthStatus.Text = "SERVICE RECOMMENDED";
                    labelHealthStatus.ForeColor = colorTurbo;
                }

                decimal fullWh = fullMwh.HasValue ? Math.Round(fullMwh.Value / 1000m, 1) : 0;
                decimal designWh = designMwh.HasValue ? Math.Round(designMwh.Value / 1000m, 1) : 0;
                decimal wearPct = Math.Max(0m, Math.Round(100m - health, 1));
                labelCapacities.Text = $"Full Charge: {fullWh:0.0} Wh / Design: {designWh:0.0} Wh  •  Wear: {wearPct:0.0}%";
            }
            else
            {
                labelHealthPercent.Text = "N/A";
                labelHealthPercent.ForeColor = colorGray;
                labelHealthStatus.Text = "READING...";
                labelCapacities.Text = "Querying battery telemetry...";
            }
            panelHealthBar.Invalidate();

            // 2. Power Source
            bool onAc = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online;
            Program.PowerSource src = Program.currentSource;
            if (!onAc)
            {
                labelSourceVal.Text = "Battery Power (Discharging)";
                labelSourceVal.ForeColor = foreMain;
            }
            else if (src == Program.PowerSource.USBC)
            {
                bool limited = AppConfig.Get("limit_power_usbc", 1) == 1;
                labelSourceVal.Text = limited
                    ? "USB-C PD Adapter (Power Limited to 35W/45W)"
                    : "USB-C PD Adapter (Full Power)";
                labelSourceVal.ForeColor = colorEco;
            }
            else
            {
                labelSourceVal.Text = "AC Adapter (Barrel 240W • Full Power)";
                labelSourceVal.ForeColor = colorStandard;
            }

            // 3. Current Charge
            decimal pctRemaining = HardwareControl.batteryCapacity > 0
                ? HardwareControl.batteryCapacity
                : (decimal)(SystemInformation.PowerStatus.BatteryLifePercent * 100f);

            decimal? chargeMwh = HardwareControl.chargeCapacity;
            string whPart = chargeMwh.HasValue && chargeMwh.Value > 0
                ? $" ({Math.Round(chargeMwh.Value / 1000m, 1):0.0} Wh remaining)"
                : "";
            labelChargeVal.Text = $"{Math.Round(pctRemaining, 0)}%{whPart}";

            // 4. Live Flow Rate
            decimal? rate = HardwareControl.batteryRate;
            if (rate.HasValue && Math.Abs(rate.Value) >= 0.5m)
            {
                if (rate.Value < 0)
                {
                    labelRateVal.Text = $"{Math.Abs(rate.Value):0.0} W  (Discharging)";
                    labelRateVal.ForeColor = Math.Abs(rate.Value) > 25m ? colorTurbo : (Math.Abs(rate.Value) > 15m ? colorStandard : colorEco);
                }
                else
                {
                    labelRateVal.Text = $"+{rate.Value:0.0} W  (Charging)";
                    labelRateVal.ForeColor = colorEco;
                }
            }
            else
            {
                labelRateVal.Text = "0.0 W (Idle / Full / Bypass)";
                labelRateVal.ForeColor = colorGray;
            }

            // 5. Session Average Drain
            decimal? avgDrain = HardwareControl.sessionAvgDischargeW;
            if (avgDrain.HasValue && avgDrain.Value > 0.5m)
            {
                labelAvgRateVal.Text = $"{avgDrain.Value:0.0} W";
                if (fullMwh.HasValue && fullMwh.Value > 0)
                {
                    decimal fullWh = fullMwh.Value / 1000m;
                    int fullMins = (int)Math.Round((double)(fullWh / avgDrain.Value * 60m));
                    if (fullMins > 0 && fullMins < 36 * 60)
                    {
                        labelAvgRateVal.Text += $"  (Est. 100%→0%: ~{fullMins / 60}h {fullMins % 60}m)";
                    }
                }
            }
            else
            {
                labelAvgRateVal.Text = "--";
            }

            // 6. Estimated Runtime
            if (!string.IsNullOrEmpty(HardwareControl.batteryTimeStr))
            {
                labelTimeVal.Text = HardwareControl.batteryTimeStr;
                labelTimeVal.ForeColor = onAc ? colorEco : foreMain;
            }
            else
            {
                labelTimeVal.Text = onAc ? "Plugged in" : "Calculating...";
                labelTimeVal.ForeColor = colorGray;
            }

            // 7. Hardware Charge Limit
            int ecLimit = BatteryControl.chargeFull ? 100 : AppConfig.Get("charge_limit", 80);
            labelLimitVal.Text = $"{ecLimit}% (ASUS Hardware EC Cap)";

            // 8. Protection Status
            var protections = new System.Collections.Generic.List<string>();
            if (BatteryControl.chargePaused)
                protections.Add("Pause Charge / AC Bypass");
            if (BatteryControl.smartCharge)
                protections.Add($"Smart Charge (<{BatteryControl.smartChargeMin}%)");
            if (TravelMode.IsActive)
                protections.Add("Travel Mode");
            if (AppConfig.Get("auto_low_battery_saver", 1) == 1)
                protections.Add("Auto Low-Battery Guard (<20%)");

            labelProtectionVal.Text = protections.Count > 0 ? string.Join(" • ", protections) : "Standard Protection";
            labelProtectionVal.ForeColor = BatteryControl.chargePaused || BatteryControl.smartCharge ? colorEco : foreMain;

            // 9. Hardware Specs
            string voltStr = _hwInfo.VoltageMv > 0 ? $" • {Math.Round(_hwInfo.VoltageMv / 1000f, 2):0.00}V" : "";
            labelHardwareVal.Text = $"{_hwInfo.Manufacturer} ({_hwInfo.Chemistry}{voltStr})";
        }
    }
}
