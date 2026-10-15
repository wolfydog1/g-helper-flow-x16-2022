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
        private DataPoint curPoint = null;
        private Chart curChart = null;
        
        public BrightnessCurve()
        {
            this.Text = "Auto Brightness Curves";
            this.Size = new Size(600, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowIcon = false;
            this.TopMost = true;
            
            Color bg = Color.FromArgb(255, 32, 32, 32);
            Color fg = Color.White;
            this.BackColor = bg;
            this.ForeColor = fg;

            chartScreen = CreateChart("Screen Brightness (%) based on Room Light", 100, "screen_curve");
            chartKeyboard = CreateChart("Keyboard Brightness based on Room Light", 3, "keyboard_curve");
            
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            layout.Controls.Add(chartScreen, 0, 0);
            layout.Controls.Add(chartKeyboard, 0, 1);
            
            this.Controls.Add(layout);
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
            ca.AxisX.MajorGrid.LineColor = Color.Gray;
            
            ca.AxisY.Minimum = 0;
            ca.AxisY.Maximum = maxY;
            ca.AxisY.Title = title;
            ca.AxisY.TitleForeColor = this.ForeColor;
            ca.AxisY.LineColor = this.ForeColor;
            ca.AxisY.LabelStyle.ForeColor = this.ForeColor;
            ca.AxisY.MajorGrid.LineColor = Color.Gray;
            
            chart.ChartAreas.Add(ca);
            
            Series series = new Series { ChartType = SeriesChartType.Line, BorderWidth = 3, Color = Color.DodgerBlue };
            series.MarkerStyle = MarkerStyle.Circle;
            series.MarkerSize = 10;
            
            LoadCurve(series, configKey, maxY);
            
            chart.Series.Add(series);

            chart.MouseDown += Chart_MouseDown;
            chart.MouseMove += Chart_MouseMove;
            chart.MouseUp += Chart_MouseUp;

            return chart;
        }

        private void LoadCurve(Series series, string key, int maxY)
        {
            string saved = AppConfig.GetString(key);
            if (!string.IsNullOrEmpty(saved))
            {
                try
                {
                    var points = JsonSerializer.Deserialize<int[][]>(saved);
                    foreach (var p in points) series.Points.AddXY(p[0], p[1]);
                    return;
                }
                catch { }
            }
            
            // Defaults
            if (maxY == 100) {
                series.Points.AddXY(0, 20);
                series.Points.AddXY(100, 50);
                series.Points.AddXY(500, 100);
            } else {
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
            AppConfig.Set(chart.Tag.ToString(), JsonSerializer.Serialize(points));
        }

        private void Chart_MouseDown(object sender, MouseEventArgs e)
        {
            Chart chart = (Chart)sender;
            HitTestResult hit = chart.HitTest(e.X, e.Y);
            if (hit.ChartElementType == ChartElementType.DataPoint)
            {
                curPoint = hit.Series.Points[hit.PointIndex];
                curChart = chart;
            }
        }

        private void Chart_MouseMove(object sender, MouseEventArgs e)
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
                    curChart.Invalidate();
                }
                catch { }
            }
        }

        private void Chart_MouseUp(object sender, MouseEventArgs e)
        {
            if (curChart != null) SaveCurve(curChart);
            curPoint = null;
            curChart = null;
        }
    }
}



