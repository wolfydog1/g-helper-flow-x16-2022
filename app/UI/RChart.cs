using System.Reflection;
using System.Windows.Forms.DataVisualization.Charting;

namespace GHelper.UI
{
    public class RChart : Chart
    {
        // Rare GDI+ ObjectBusy / state races in Chart.OnPaint propagate out of
        // base.OnPaint and leave `disableInvalidates` stuck true or `paintBufferBitmapGraphics`
        // in a corrupted transform/state, causing the chart to render as a solid black box
        // until app restart. Reset internal buffer + flags so the chart immediately self-heals.
        private static readonly FieldInfo? _disableInvalidates =
            typeof(Chart).GetField("disableInvalidates", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? _dirtyFlag =
            typeof(Chart).GetField("dirtyFlag", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? _paintTopLevelElementOnly =
            typeof(Chart).GetField("paintTopLevelElementOnly", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? _paintBufferBitmap =
            typeof(Chart).GetField("paintBufferBitmap", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? _paintBufferBitmapGraphics =
            typeof(Chart).GetField("paintBufferBitmapGraphics", BindingFlags.Instance | BindingFlags.NonPublic);

        private long _lastLogTicks;
        private bool _retryQueued;

        private void ResetPaintBuffer()
        {
            try
            {
                _disableInvalidates?.SetValue(this, false);
                _paintTopLevelElementOnly?.SetValue(this, false);
                _dirtyFlag?.SetValue(this, true);

                if (_paintBufferBitmapGraphics?.GetValue(this) is Graphics g)
                {
                    _paintBufferBitmapGraphics.SetValue(this, null);
                    g.Dispose();
                }
                if (_paintBufferBitmap?.GetValue(this) is Bitmap bmp)
                {
                    _paintBufferBitmap.SetValue(this, null);
                    bmp.Dispose();
                }
            }
            catch
            {
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 4 || Height <= 4 || ClientRectangle.Width <= 4 || ClientRectangle.Height <= 4)
                return;

            try
            {
                base.OnPaint(e);
                _retryQueued = false;
            }
            catch (Exception ex)
            {
                long now = Environment.TickCount64;
                if (now - _lastLogTicks > 5000)
                {
                    _lastLogTicks = now;
                    Logger.WriteLine($"RChart paint ({Name}): {ex.GetType().Name}: {ex.Message}");
                }

                ResetPaintBuffer();

                try
                {
                    Color fill = BackColor.A > 0 ? BackColor : RForm.chartMain;
                    e.Graphics.Clear(fill);
                }
                catch
                {
                }

                if (!_retryQueued && IsHandleCreated && !IsDisposed)
                {
                    _retryQueued = true;
                    BeginInvoke(new Action(() =>
                    {
                        if (!IsDisposed) Invalidate();
                    }));
                }
            }
        }
    }
}
