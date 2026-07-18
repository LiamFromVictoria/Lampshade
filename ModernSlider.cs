using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Lampshade;

/// <summary>
/// A flat, rounded-track slider replacing the native <see cref="TrackBar"/>, whose
/// Win32 chrome (tick marks, beveled thumb) doesn't fit a dark themed window.
/// Supports mouse drag/click-to-jump and keyboard arrow/Home/End navigation.
/// </summary>
internal sealed class ModernSlider : Control
{
    private const int ThumbRadius = 8;
    private const int TrackHeight = 4;

    private int _minimum;
    private int _maximum = 100;
    private int _value;
    private bool _dragging;

    public ModernSlider()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.Selectable,
            true);
        Height = 28;
        TabStop = true;
        Cursor = Cursors.Hand;
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    public event EventHandler? ValueChanged;

    public int Minimum
    {
        get => _minimum;
        set { _minimum = value; Value = Math.Clamp(_value, _minimum, _maximum); Invalidate(); }
    }

    public int Maximum
    {
        get => _maximum;
        set { _maximum = value; Value = Math.Clamp(_value, _minimum, _maximum); Invalidate(); }
    }

    public int Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, _minimum, _maximum);
            if (clamped == _value)
            {
                return;
            }
            _value = clamped;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override bool IsInputKey(Keys keyData) => keyData switch
    {
        Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.Up or Keys.Down => true,
        _ => base.IsInputKey(keyData),
    };

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Left or Keys.Down:
                Value -= 1;
                break;
            case Keys.Right or Keys.Up:
                Value += 1;
                break;
            case Keys.Home:
                Value = _minimum;
                break;
            case Keys.End:
                Value = _maximum;
                break;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }
        Focus();
        _dragging = true;
        SetValueFromX(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            SetValueFromX(e.X);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
    }

    private void SetValueFromX(int x)
    {
        var usableWidth = Math.Max(1, Width - ThumbRadius * 2);
        var ratio = Math.Clamp((x - ThumbRadius) / (double)usableWidth, 0.0, 1.0);
        Value = _minimum + (int)Math.Round(ratio * (_maximum - _minimum));
    }

    private float ThumbCenterX()
    {
        var usableWidth = Math.Max(1, Width - ThumbRadius * 2);
        var ratio = _maximum > _minimum ? (_value - _minimum) / (double)(_maximum - _minimum) : 0;
        return (float)(ThumbRadius + ratio * usableWidth);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.PanelBackground);

        var midY = Height / 2f;
        var thumbX = ThumbCenterX();
        var trackRect = new RectangleF(ThumbRadius, midY - TrackHeight / 2f, Width - ThumbRadius * 2, TrackHeight);

        using (var offBrush = new SolidBrush(Theme.TrackOff))
        {
            FillRoundedRect(g, offBrush, trackRect, TrackHeight / 2f);
        }

        var filledRect = new RectangleF(trackRect.X, trackRect.Y, Math.Max(0, thumbX - trackRect.X), TrackHeight);
        using (var accentBrush = new SolidBrush(Theme.Accent))
        {
            FillRoundedRect(g, accentBrush, filledRect, TrackHeight / 2f);
        }

        var thumbColor = Focused ? Theme.AccentHover : Color.White;
        using var thumbBrush = new SolidBrush(thumbColor);
        using var thumbPen = new Pen(Theme.Accent, 2f);
        var thumbRect = new RectangleF(thumbX - ThumbRadius, midY - ThumbRadius, ThumbRadius * 2, ThumbRadius * 2);
        g.FillEllipse(thumbBrush, thumbRect);
        g.DrawEllipse(thumbPen, thumbRect);
    }

    private static void FillRoundedRect(Graphics g, Brush brush, RectangleF rect, float radius)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }
        using var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }
}
