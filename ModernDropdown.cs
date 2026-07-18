using System.Drawing.Drawing2D;

namespace Lampshade;

/// <summary>
/// A flat, borderless dropdown field replacing <see cref="ComboBox"/>'s default
/// Win32 chrome (white dropdown list, square edges), which doesn't fit a dark
/// themed window. Clicking shows a <see cref="ModernContextMenuStrip"/> populated
/// with <see cref="Items"/>, reusing the same themed popup as the tray menu.
/// </summary>
internal sealed class ModernDropdown : Control
{
    private string[] _items = Array.Empty<string>();
    private int _selectedIndex = -1;
    private bool _hovered;

    public ModernDropdown()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.Selectable,
            true);
        Height = 30;
        TabStop = true;
        Cursor = Cursors.Hand;
    }

    public event EventHandler? SelectedIndexChanged;

    public IReadOnlyList<string> Items
    {
        get => _items;
        set
        {
            _items = value.ToArray();
            SelectedIndex = _items.Length == 0 ? -1 : Math.Clamp(_selectedIndex, 0, _items.Length - 1);
            Invalidate();
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value == _selectedIndex)
            {
                return;
            }
            _selectedIndex = value;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        Invalidate();
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

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Down or Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Up:
                if (_items.Length > 0)
                {
                    SelectedIndex = Math.Max(0, _selectedIndex - 1);
                }
                break;
            case Keys.Down:
                if (_items.Length > 0)
                {
                    SelectedIndex = Math.Min(_items.Length - 1, _selectedIndex + 1);
                }
                break;
            case Keys.Space:
            case Keys.Enter:
                ShowPopup();
                break;
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Focus();
        ShowPopup();
    }

    private void ShowPopup()
    {
        if (_items.Length == 0)
        {
            return;
        }

        var menu = new ModernContextMenuStrip { MinimumSize = new Size(Width, 0) };
        for (var i = 0; i < _items.Length; i++)
        {
            var index = i;
            var item = new ToolStripMenuItem(_items[i]) { Checked = index == _selectedIndex };
            item.Click += (_, _) => SelectedIndex = index;
            menu.Items.Add(item);
        }
        menu.Show(this, new Point(0, Height));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.WindowBackground);

        var rect = new RectangleF(0, 0, Width - 1, Height - 1);
        using (var backBrush = new SolidBrush(_hovered ? Theme.Border : Theme.PanelBackground))
        {
            FillRoundedRect(g, backBrush, rect, 6);
        }
        using (var borderPen = new Pen(Focused ? Theme.Accent : Theme.Border))
        {
            DrawRoundedRect(g, borderPen, rect, 6);
        }

        var text = _selectedIndex >= 0 && _selectedIndex < _items.Length ? _items[_selectedIndex] : string.Empty;
        var textRect = new RectangleF(10, 0, Width - 30, Height);
        using (var textBrush = new SolidBrush(Theme.TextPrimary))
        using (var format = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
        {
            g.DrawString(text, Theme.FontRegular, textBrush, textRect, format);
        }

        using var chevronBrush = new SolidBrush(Theme.TextSecondary);
        var chevron = new[]
        {
            new PointF(Width - 20, Height / 2f - 3),
            new PointF(Width - 12, Height / 2f - 3),
            new PointF(Width - 16, Height / 2f + 3),
        };
        g.FillPolygon(chevronBrush, chevron);
    }

    private static void FillRoundedRect(Graphics g, Brush brush, RectangleF rect, float radius)
    {
        using var path = RoundedPath(rect, radius);
        g.FillPath(brush, path);
    }

    private static void DrawRoundedRect(Graphics g, Pen pen, RectangleF rect, float radius)
    {
        using var path = RoundedPath(rect, radius);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath RoundedPath(RectangleF rect, float radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
