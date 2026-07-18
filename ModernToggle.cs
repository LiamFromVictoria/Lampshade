using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Lampshade;

/// <summary>
/// A flat pill-shaped toggle switch replacing <see cref="CheckBox"/>'s dated
/// square check glyph. The knob glides to its new side over a short animation.
/// </summary>
internal sealed class ModernToggle : Control
{
    private const int AnimationIntervalMs = 15;
    private const double AnimationStep = 0.18;

    private readonly System.Windows.Forms.Timer _animationTimer;
    private double _knobPosition; // 0 = off, 1 = on
    private bool _checked;

    public ModernToggle()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.Selectable,
            true);
        Size = new Size(40, 20);
        TabStop = true;
        Cursor = Cursors.Hand;

        _animationTimer = new System.Windows.Forms.Timer { Interval = AnimationIntervalMs };
        _animationTimer.Tick += (_, _) => StepAnimation();
    }

    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }
            _checked = value;
            _animationTimer.Start();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Sets the initial state without animating or raising <see cref="CheckedChanged"/>.</summary>
    public void SetInitialChecked(bool value)
    {
        _checked = value;
        _knobPosition = value ? 1 : 0;
        Invalidate();
    }

    private void StepAnimation()
    {
        var target = _checked ? 1.0 : 0.0;
        _knobPosition += (target - _knobPosition) * AnimationStep;
        if (Math.Abs(target - _knobPosition) < 0.01)
        {
            _knobPosition = target;
            _animationTimer.Stop();
        }
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Space ? true : base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            Checked = !Checked;
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Focus();
        Checked = !Checked;
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

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.PanelBackground);

        var pillRect = new RectangleF(0, 0, Width, Height);
        var trackColor = LerpColor(Theme.TrackOff, Theme.Accent, _knobPosition);
        using (var trackBrush = new SolidBrush(trackColor))
        {
            FillPill(g, trackBrush, pillRect);
        }

        if (Focused)
        {
            using var focusPen = new Pen(Theme.AccentHover, 1.5f);
            FillPillOutline(g, focusPen, Rectangle.Inflate(Rectangle.Round(pillRect), -1, -1));
        }

        var knobDiameter = Height - 4;
        var travel = Width - knobDiameter - 4;
        var knobX = 2 + (float)(_knobPosition * travel);
        using var knobBrush = new SolidBrush(Color.White);
        g.FillEllipse(knobBrush, knobX, 2, knobDiameter, knobDiameter);
    }

    private static Color LerpColor(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    private static void FillPill(Graphics g, Brush brush, RectangleF rect)
    {
        using var path = PillPath(rect);
        g.FillPath(brush, path);
    }

    private static void FillPillOutline(Graphics g, Pen pen, Rectangle rect)
    {
        using var path = PillPath(rect);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath PillPath(RectangleF rect)
    {
        var radius = rect.Height / 2f;
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 90, 180);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 180);
        path.CloseFigure();
        return path;
    }
}
