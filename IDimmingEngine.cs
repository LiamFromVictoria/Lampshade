namespace Lampshade;

/// <summary>
/// Strategy for applying the dim / low-blue-light effects to the desktop. Each
/// backend trades reach against compatibility: <see cref="OverlayDimmingEngine"/>
/// works on every GPU but sits above the desktop compositor as a window, so it
/// can't affect fullscreen-exclusive content; <see cref="GammaRampDimmingEngine"/>
/// adjusts the display driver's gamma table directly instead — vendor-neutral
/// (NVIDIA/AMD/Intel alike, no Vulkan/DirectX-specific hook needed) — so it also
/// reaches fullscreen-exclusive games, at the cost of some drivers rejecting very
/// extreme ramps.
/// </summary>
internal interface IDimmingEngine : IDisposable
{
    /// <summary>
    /// Turns Dim Mode on/off and sets its strength in one call. Returns <c>true</c>
    /// if <paramref name="percent"/> was fully applied on every connected display,
    /// or <c>false</c> if it had to be clamped to a weaker level a display/driver
    /// would actually accept (see <see cref="GammaRampDimmingEngine"/>).
    /// </summary>
    bool SetDimActive(bool active, int percent);

    /// <summary>
    /// Updates Dim Mode's strength while it's already active (e.g. live slider
    /// drag). Same clamped-vs-fully-applied return semantics as <see cref="SetDimActive"/>.
    /// </summary>
    bool SetDimPercent(int percent);

    /// <summary>Turns Low Blue Light on/off and sets its strength in one call.</summary>
    void SetLowBlueLightActive(bool active, int percent);

    /// <summary>Updates Low Blue Light's strength while it's already active (e.g. live slider drag).</summary>
    void SetLowBlueLightPercent(int percent);

    /// <summary>
    /// Re-syncs the effect to the current monitor layout after a display was
    /// added/removed/resized, given the full current on/off + strength state.
    /// </summary>
    void RefreshForDisplayChange(bool dimActive, int dimPercent, bool lowBlueLightActive, int lowBlueLightPercent);
}
