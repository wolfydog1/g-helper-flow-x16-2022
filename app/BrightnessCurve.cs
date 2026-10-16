using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Text.Json;

namespace GHelper
{
    public class BrightnessCurve : Form
    {
        private Chart chartScreen;
        private Chart chartKeyboard;
        private DataPoint? curPoint = null;
        private Chart? curChart = null;
        private Label labelCurrentLux;
        private System.Windows.Forms.Timer luxTimer;

        public BrightnessCurve()
        {
            this.Text = "Auto Brightness Curves (Ambient Light Sensor)";
            this.Size = new Size(640, 650);
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowIcon = false;
            this.TopMost = true;

            Color bg = Color.FromArgb(255, 32, 32, 32);
            Color fg = Color.White;
            this.BackColor = bg;
            this.ForeColor = fg;

            Panel headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(12, 6, 12, 6),
                BackColor = Color.FromArgb(255, 42, 42, 42)
            };

            labelCurrentLux = new Label
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(this.Font, FontStyle.Bold),
                ForeColor = Color.MediumSpringGreen,
                Padding = new Padding(0, 5, 0, 0)
            };
            UpdateLuxLabel();

            Button btnReset = new Button
            {
                Dock = DockStyle.Right,
                Text = "Reset Defaults",
                Width = 120,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 58, 58, 58),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnReset.FlatAppearance.BorderColor = Color.Gray;
            btnReset.Click += (s, e) =>
            {
                AppConfig.Set("screen_curve", "");
                AppConfig.Set("keyboard_curve", "");
                SettingsForm.InvalidateCurveCache();
                chartScreen.Series[0].Points.Clear();
                chartKeyboard.Series[0].Points.Clear();
                LoadCurve(chartScreen.Series[0], "screen_curve", 100);
                LoadCurve(chartKeyboard.Series[0], "keyboard_curve", 3);
                RefreshPointLabels(chartScreen);
                RefreshPointLabels(chartKeyboard);
                chartScreen.Invalidate();
                chartKeyboard.Invalidate();
                Program.settingsForm?.TriggerAlsUpdate();
            };

            headerPanel.Controls.Add(labelCurrentLux);
            headerPanel.Controls.Add(btnReset);

            chartScreen = CreateChart("Screen Brightness (%) based on Room Light", 100, "screen_curve");
            chartKeyboard = CreateChart("Keyboard Backlight (0–3) based on Room Light", 3, "keyboard_curve");

            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            layout.Controls.Add(chartScreen, 0, 0);
            layout.Controls.Add(chartKeyboard, 0, 1);

            this.Controls.Add(layout);
            this.Controls.Add(headerPanel);

            luxTimer = new System.Windows.Forms.Timer { Interval = 500 };
            luxTimer.Tick += (s, e) => UpdateLuxLabel();
            luxTimer.Start();
            this.FormClosed += (s, e) => luxTimer.Dispose();
        }

        private void UpdateLuxLabel()
        {
            float lux = SettingsForm.CurrentLux;
            labelCurrentLux.Text = lux >= 0
                ? $"Current Room Light: {lux:F0} Lux  (Drag points to customize)"
                : "Ambient Light Sensor: Active (Drag points to customize)";
        }

        private Chart CreateChart(string title, int maxY, string configKey)
        {
            Chart chart = new Chart { Dock = DockStyle.Fill, Tag = configKey };
            chart.BackColor = this.BackColor;
            chart.ForeColor = this.ForeColor;

            ChartArea ca = new ChartArea();
            ca.BackColor = this.BackColor;
            ca.AxisX.Minimum = 0;
            ca.AxisX.Maximum = 500;
            ca.AxisX.Title = "Room Brightness (Lux - Higher is Brighter)";
            ca.AxisX.TitleForeColor = this.ForeColor;
            ca.AxisX.LineColor = this.ForeColor;
            ca.AxisX.LabelStyle.ForeColor = this.ForeColor;
            ca.AxisX.MajorGrid.LineColor = Color.FromArgb(80, Color.Gray);

            ca.AxisY.Minimum = 0;
            ca.AxisY.Maximum = maxY;
            if (maxY == 3) ca.AxisY.Interval = 1;
            ca.AxisY.Title = title;
            ca.AxisY.TitleForeColor = this.ForeColor;
            ca.AxisY.LineColor = this.ForeColor;
            ca.AxisY.LabelStyle.ForeColor = this.ForeColor;
            ca.AxisY.MajorGrid.LineColor = Color.FromArgb(80, Color.Gray);

            chart.ChartAreas.Add(ca);

            Series series = new Series
            {
                ChartType = SeriesChartType.Line,
                BorderWidth = 3,
                Color = Color.DodgerBlue,
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = 11,
                Font = new Font(this.Font.FontFamily, 8f, FontStyle.Regular),
                LabelForeColor = Color.LightSkyBlue
            };

            LoadCurve(series, configKey, maxY);
            chart.Series.Add(series);
            RefreshPointLabels(chart);

            chart.MouseDown += Chart_MouseDown;
            chart.MouseMove += Chart_MouseMove;
            chart.MouseUp += Chart_MouseUp;

            return chart;
        }

        private static void RefreshPointLabels(Chart chart)
        {
            bool isScreen = chart.Tag?.ToString() == "screen_curve";
            foreach (var pt in chart.Series[0].Points)
            {
                pt.Label = isScreen
                    ? $"{pt.XValue:F0}lx → {pt.YValues[0]:F0}%"
                    : $"{pt.XValue:F0}lx → Lv.{pt.YValues[0]:F0}";
            }
        }

        private void LoadCurve(Series series, string key, int maxY)
        {
            string saved = AppConfig.GetString(key);
            if (!string.IsNullOrEmpty(saved))
            {
                try
                {
                    var points = JsonSerializer.Deserialize<int[][]>(saved);
                    if (points != null && points.Length > 0)
                    {
                        foreach (var p in points) series.Points.AddXY(p[0], p[1]);
                        return;
                    }
                }
                catch { }
            }

            // Defaults
            if (maxY == 100)
            {
                series.Points.AddXY(0, 20);
                series.Points.AddXY(100, 50);
                series.Points.AddXY(500, 100);
            }
            else
            {
                series.Points.AddXY(0, 3);
                series.Points.AddXY(50, 2);
                series.Points.AddXY(200, 1);
                series.Points.AddXY(500, 0);
            }
        }

        private void SaveCurve(Chart chart)
        {
            var series = chart.Series[0];
            int[][] points = new int[series.Points.Count][];
            for (int i = 0; i < series.Points.Count; i++)
            {
                points[i] = new int[] { (int)series.Points[i].XValue, (int)series.Points[i].YValues[0] };
            }
            AppConfig.Set(chart.Tag!.ToString()!, JsonSerializer.Serialize(points));
            SettingsForm.InvalidateCurveCache();
            Program.settingsForm?.TriggerAlsUpdate();
        }

        private void Chart_MouseDown(object? sender, MouseEventArgs e)
        {
            if (sender is not Chart chart) return;
            HitTestResult hit = chart.HitTest(e.X, e.Y);
            if (hit.ChartElementType == ChartElementType.DataPoint)
            {
                curPoint = hit.Series.Points[hit.PointIndex];
                curChart = chart;
            }
        }

        private void Chart_MouseMove(object? sender, MouseEventArgs e)
        {
            if (curPoint != null && curChart != null && e.Button == MouseButtons.Left)
            {
                ChartArea ca = curChart.ChartAreas[0];
                try
                {
                    double xVal = ca.AxisX.PixelPositionToValue(e.X);
                    double yVal = ca.AxisY.PixelPositionToValue(e.Y);

                    xVal = Math.Max(ca.AxisX.Minimum, Math.Min(ca.AxisX.Maximum, xVal));
                    yVal = Math.Max(ca.AxisY.Minimum, Math.Min(ca.AxisY.Maximum, yVal));

                    curPoint.XValue = Math.Round(xVal / 10.0) * 10;
                    curPoint.YValues[0] = Math.Round(yVal);

                    // Sort points
                    curChart.Series[0].Sort(PointSortOrder.Ascending, "X");
                    RefreshPointLabels(curChart);
                    curChart.Invalidate();
                }
                catch { }
            }
        }

        private void Chart_MouseUp(object? sender, MouseEventArgs e)
        {
            if (curChart != null) SaveCurve(curChart);
            curPoint = null;
            curChart = null;
        }
    }
}



