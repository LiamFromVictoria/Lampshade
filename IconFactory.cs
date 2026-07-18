using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;

namespace Lampshade;

/// <summary>The visual state a tray icon should reflect.</summary>
internal enum TrayIconState
{
    Normal,
    Dimmed,
    LowBlueLight,
}

/// <summary>
/// Builds the tray-icon states from the embedded lamp silhouette (a white-on-
/// transparent alpha mask baked from Assets/lamp-mask.png at build time), tinted
/// per state so the icon always reflects whether dimming and/or the low-blue-light
/// filter are active.
/// </summary>
internal static class IconFactory
{
    private const string MaskResourceName = "Lampshade.lamp-mask.png";

    private static readonly Color NormalColor = Color.FromArgb(255, 196, 61);
    private static readonly Color DimmedColor = Color.FromArgb(120, 120, 132);
    private static readonly Color LowBlueLightColor = Color.FromArgb(255, 140, 40);

    private static Bitmap? _mask;

    public static Icon CreateTrayIcon(TrayIconState state)
    {
        var color = state switch
        {
            TrayIconState.Dimmed => DimmedColor,
            TrayIconState.LowBlueLight => LowBlueLightColor,
            TrayIconState.Normal or _ => NormalColor,
        };

        using var tinted = TintMask(GetMask(), color);
        using var icon32 = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(icon32))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(tinted, 0, 0, 32, 32);
        }

        return Icon.FromHandle(icon32.GetHicon());
    }

    private static Bitmap GetMask()
    {
        if (_mask is not null)
        {
            return _mask;
        }

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(MaskResourceName)
                            ?? throw new InvalidOperationException($"Embedded resource '{MaskResourceName}' not found.");
        _mask = new Bitmap(stream);
        return _mask;
    }

    /// <summary>Replaces the mask's RGB with <paramref name="color"/> while preserving its alpha channel.</summary>
    private static Bitmap TintMask(Bitmap mask, Color color)
    {
        var result = new Bitmap(mask.Width, mask.Height, PixelFormat.Format32bppArgb);
        var srcData = mask.LockBits(new Rectangle(0, 0, mask.Width, mask.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var dstData = result.LockBits(new Rectangle(0, 0, result.Width, result.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        try
        {
            var stride = srcData.Stride;
            var bytes = stride * mask.Height;
            var buffer = new byte[bytes];
            System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, buffer, 0, bytes);

            for (var i = 0; i < bytes; i += 4)
            {
                var alpha = buffer[i + 3];
                buffer[i + 0] = color.B;
                buffer[i + 1] = color.G;
                buffer[i + 2] = color.R;
                buffer[i + 3] = alpha;
            }

            System.Runtime.InteropServices.Marshal.Copy(buffer, 0, dstData.Scan0, bytes);
        }
        finally
        {
            mask.UnlockBits(srcData);
            result.UnlockBits(dstData);
        }

        return result;
    }
}
