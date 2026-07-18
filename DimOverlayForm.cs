using System.Drawing;
using System.Windows.Forms;

namespace ScreenDimmer;

/// <summary>
/// A borderless, click-through, always-on-top black window stretched over a single
/// monitor. Its opacity simulates a brightness reduction — this works uniformly on
/// every monitor, unlike DDC/CI hardware brightness control, which many external
/// displays (and some laptop panels) don't expose reliably to Windows.
/// </summary>
internal sealed class DimOverlayForm : Form
{
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    public DimOverlayForm(Rectangle bounds, int dimPercent)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        BackColor = Color.Black;
        TopMost = true;
        Cursor = Cursors.Default;

        SetDimPercent(dimPercent);
    }

    /// <summary>Never steals focus when shown — the whole point is to sit inertly on top.</summary>
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    public void SetDimPercent(int dimPercent)
    {
        Opacity = Math.Clamp(dimPercent, 0, 100) / 100.0;
    }

    public void UpdateBounds(Rectangle bounds)
    {
        Bounds = bounds;
    }
}
