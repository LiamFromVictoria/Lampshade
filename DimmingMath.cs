namespace Lampshade;

/// <summary>
/// Shared math for translating the low-blue-light percentage into an effect
/// strength, used by every <see cref="IDimmingEngine"/> backend so the slider
/// feels consistent regardless of which method is active.
/// </summary>
internal static class DimmingMath
{
    /// <summary>The warm/amber tint color the low-blue-light effect blends toward.</summary>
    public static readonly Color LowBlueLightColor = Color.FromArgb(255, 172, 110);

    /// <summary>
    /// The settings slider is a 10–100 user-facing percentage, but mapping it
    /// directly to effect strength (the old behavior) made even a 10% setting look
    /// intense: a saturated warm color is far more perceptually strong than its
    /// strength percentage suggests. This remaps the slider onto a gentler, capped
    /// curve — quiet at the low end, and never more than a mild warm cast at the
    /// top of the range.
    /// </summary>
    public static int MapLowBlueLightOpacityPercent(int percent)
    {
        const double maxOpacityPercent = 55.0;
        const double curve = 1.6; // >1 keeps low settings subtle
        var t = Math.Clamp(percent, 0, 100) / 100.0;
        return (int)Math.Round(Math.Pow(t, curve) * maxOpacityPercent);
    }
}
