using System.Drawing;

namespace Lampshade;

/// <summary>Shared color palette and fonts for the app's dark, flat UI.</summary>
internal static class Theme
{
    public static readonly Color WindowBackground = Color.FromArgb(30, 30, 32);
    public static readonly Color PanelBackground = Color.FromArgb(37, 37, 40);
    public static readonly Color NavBackground = Color.FromArgb(24, 24, 26);

    public static readonly Color Accent = Color.FromArgb(255, 196, 61);
    public static readonly Color AccentHover = Color.FromArgb(255, 212, 110);
    public static readonly Color AccentPressed = Color.FromArgb(224, 168, 45);

    public static readonly Color TextPrimary = Color.FromArgb(240, 240, 242);
    public static readonly Color TextSecondary = Color.FromArgb(150, 150, 156);
    public static readonly Color TextOnAccent = Color.FromArgb(30, 24, 8);

    public static readonly Color Border = Color.FromArgb(55, 55, 59);
    public static readonly Color TrackOff = Color.FromArgb(72, 72, 78);

    public static readonly Color MenuBackground = Color.FromArgb(28, 28, 31);
    public static readonly Color MenuHover = Color.FromArgb(54, 47, 27);
    public static readonly Color MenuBorder = Color.FromArgb(50, 50, 54);
    public static readonly Color MenuSeparator = Color.FromArgb(52, 52, 56);

    public static readonly Font FontRegular = new("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font FontSemibold = new("Segoe UI Semibold", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font FontHeading = new("Segoe UI Semibold", 12f, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font FontSmall = new("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
}
