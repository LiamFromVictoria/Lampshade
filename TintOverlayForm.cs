using System.Drawing;
using System.Windows.Forms;

namespace Lampshade;

/// <summary>
/// A borderless, click-through, always-on-top solid-color window stretched over a
/// single monitor. Its color and opacity simulate either a brightness reduction
/// (black tint) or a blue-light filter (warm amber tint) — this works uniformly on
/// every monitor, unlike DDC/CI hardware brightness control, which many external
/// displays (and some laptop panels) don't expose reliably to Windows. Layered
/// windows compose naturally with each other, so a dim overlay and a low-blue-light
/// overlay can both sit on the same monitor and blend correctly without any manual
/// alpha math.
/// </summary>
internal sealed class TintOverlayForm : Form
{
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    public TintOverlayForm(Rectangle bounds, Color tintColor, int intensityPercent)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        BackColor = tintColor;
        TopMost = true;
        Cursor = Cursors.Default;

        SetIntensityPercent(intensityPercent);
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

    public void SetIntensityPercent(int intensityPercent)
    {
        Opacity = Math.Clamp(intensityPercent, 0, 100) / 100.0;
    }

    public void UpdateBounds(Rectangle bounds)
    {
        Bounds = bounds;
    }
}
