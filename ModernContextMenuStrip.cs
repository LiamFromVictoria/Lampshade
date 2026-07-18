using System.Windows.Forms;

namespace Lampshade;

/// <summary>
/// A <see cref="ContextMenuStrip"/> themed dark/flat via <see cref="ModernMenuRenderer"/>
/// with rounded outer corners, matching the Windows 11 context-menu look instead of
/// the default square WinForms popup.
/// </summary>
internal sealed class ModernContextMenuStrip : ContextMenuStrip
{
    public ModernContextMenuStrip()
    {
        Renderer = new ModernMenuRenderer();
        BackColor = Theme.MenuBackground;
        ForeColor = Theme.TextPrimary;
        Font = Theme.FontRegular;
        Padding = new Padding(4);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        ApplyRoundedRegion();
    }

    private void ApplyRoundedRegion()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }
        using var path = ModernMenuRenderer.RoundedRect(new Rectangle(0, 0, Width, Height), 8);
        Region = new Region(path);
    }
}
