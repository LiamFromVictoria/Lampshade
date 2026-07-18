using System.Drawing;
using System.Drawing.Drawing2D;

namespace ScreenDimmer;

/// <summary>
/// Draws the two tray-icon states (bright / dimmed) at runtime with GDI+ instead of
/// shipping separate .ico assets, so the icon always matches the current dim state.
/// </summary>
internal static class IconFactory
{
    public static Icon CreateTrayIcon(bool dimmed)
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var sunColor = dimmed ? Color.FromArgb(255, 120, 120, 130) : Color.FromArgb(255, 255, 196, 61);
            var coreColor = dimmed ? Color.FromArgb(255, 90, 90, 100) : Color.FromArgb(255, 255, 209, 84);

            float cx = size / 2f, cy = size / 2f, r = size * 0.22f;

            if (!dimmed)
            {
                using var rayPen = new Pen(sunColor, size * 0.09f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                for (var i = 0; i < 8; i++)
                {
                    var angle = Math.PI / 4 * i;
                    var inner = r * 1.4f;
                    var outer = r * 2.05f;
                    var x1 = cx + inner * (float)Math.Cos(angle);
                    var y1 = cy + inner * (float)Math.Sin(angle);
                    var x2 = cx + outer * (float)Math.Cos(angle);
                    var y2 = cy + outer * (float)Math.Sin(angle);
                    g.DrawLine(rayPen, x1, y1, x2, y2);
                }
            }
            else
            {
                // A crescent instead of rays reads clearly as "dimmed" at 16x16.
                using var crescentBrush = new SolidBrush(Color.FromArgb(255, 70, 70, 82));
                g.FillEllipse(crescentBrush, cx - r * 1.9f, cy - r * 1.9f, r * 3.8f, r * 3.8f);
            }

            using var coreBrush = new SolidBrush(coreColor);
            g.FillEllipse(coreBrush, cx - r, cy - r, r * 2, r * 2);
        }

        var hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }
}
