namespace Lampshade;

/// <summary>
/// How the dim / low-blue-light effects are rendered onto the screen. See
/// <see cref="IDimmingEngine"/> for the trade-offs between methods.
/// </summary>
internal enum DimMethod
{
    /// <summary>Per-monitor click-through window overlay. Works on any GPU.</summary>
    Overlay,

    /// <summary>
    /// Scales each display's GDI gamma ramp directly via the display driver.
    /// Vendor-neutral (NVIDIA/AMD/Intel) and reaches fullscreen-exclusive content
    /// the overlay can't.
    /// </summary>
    GammaRamp,
}
