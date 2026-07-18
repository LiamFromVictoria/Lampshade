using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Lampshade;

/// <summary>
/// Dark, flat rendering for the tray context menu: rounded item highlight, a
/// minimal accent checkmark for the Dim Mode / Low Blue Light toggles, and a
/// hairline separator instead of the default 3D-engraved look.
/// </summary>
internal sealed class ModernMenuRenderer : ToolStripProfessionalRenderer
{
    public ModernMenuRenderer() : base(new ModernColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled)
        {
            return;
        }

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
        using var path = RoundedRect(rect, 5);
        using var brush = new SolidBrush(Theme.MenuHover);
        g.FillPath(brush, path);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.TextPrimary : Theme.TextSecondary;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var box = e.ImageRectangle;
        var dot = new Rectangle(box.X + box.Width / 2 - 4, box.Y + box.Height / 2 - 4, 8, 8);
        using var brush = new SolidBrush(Theme.Accent);
        g.FillEllipse(brush, dot);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var g = e.Graphics;
        var y = e.Item.Height / 2;
        using var pen = new Pen(Theme.MenuSeparator);
        g.DrawLine(pen, 10, y, e.Item.Width - 10, y);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Theme.MenuBorder);
        var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        using var path = RoundedRect(rect, 8);
        g.DrawPath(pen, path);
    }

    internal static GraphicsPath RoundedRect(Rectangle rect, int radius)
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

    private sealed class ModernColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.MenuBackground;
        public override Color ImageMarginGradientBegin => Theme.MenuBackground;
        public override Color ImageMarginGradientMiddle => Theme.MenuBackground;
        public override Color ImageMarginGradientEnd => Theme.MenuBackground;
        public override Color MenuBorder => Theme.MenuBorder;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Theme.MenuHover;
        public override Color MenuItemSelectedGradientBegin => Theme.MenuHover;
        public override Color MenuItemSelectedGradientEnd => Theme.MenuHover;
        public override Color MenuItemPressedGradientBegin => Theme.MenuHover;
        public override Color MenuItemPressedGradientEnd => Theme.MenuHover;
        public override Color SeparatorDark => Theme.MenuSeparator;
        public override Color SeparatorLight => Theme.MenuSeparator;
    }
}
