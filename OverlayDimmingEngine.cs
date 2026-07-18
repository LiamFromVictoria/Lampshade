namespace Lampshade;

/// <summary>
/// The original dimming backend: a borderless, click-through, always-on-top
/// <see cref="TintOverlayForm"/> per monitor for each active effect. Works
/// identically on every GPU vendor since it's plain window compositing, but sits
/// above the desktop compositor — a fullscreen-exclusive game or Vulkan/DirectX
/// swapchain that bypasses DWM won't show it.
/// </summary>
internal sealed class OverlayDimmingEngine : IDimmingEngine
{
    private readonly List<TintOverlayForm> _dimOverlays = new();
    private readonly List<TintOverlayForm> _lowBlueLightOverlays = new();

    public bool SetDimActive(bool active, int percent)
    {
        if (active)
        {
            RebuildOverlaySet(_dimOverlays, Color.Black, percent);
        }
        else
        {
            ClearOverlaySet(_dimOverlays);
        }
        return true; // window opacity has no OS-level rejection path, unlike SetDeviceGammaRamp
    }

    public bool SetDimPercent(int percent)
    {
        foreach (var overlay in _dimOverlays)
        {
            overlay.SetIntensityPercent(percent);
        }
        return true;
    }

    public void SetLowBlueLightActive(bool active, int percent)
    {
        if (active)
        {
            RebuildOverlaySet(_lowBlueLightOverlays, DimmingMath.LowBlueLightColor, DimmingMath.MapLowBlueLightOpacityPercent(percent));
        }
        else
        {
            ClearOverlaySet(_lowBlueLightOverlays);
        }
    }

    public void SetLowBlueLightPercent(int percent)
    {
        var opacityPercent = DimmingMath.MapLowBlueLightOpacityPercent(percent);
        foreach (var overlay in _lowBlueLightOverlays)
        {
            overlay.SetIntensityPercent(opacityPercent);
        }
    }

    public void RefreshForDisplayChange(bool dimActive, int dimPercent, bool lowBlueLightActive, int lowBlueLightPercent)
    {
        if (dimActive)
        {
            RebuildOverlaySet(_dimOverlays, Color.Black, dimPercent);
        }
        if (lowBlueLightActive)
        {
            RebuildOverlaySet(_lowBlueLightOverlays, DimmingMath.LowBlueLightColor, DimmingMath.MapLowBlueLightOpacityPercent(lowBlueLightPercent));
        }
    }

    public void Dispose()
    {
        ClearOverlaySet(_dimOverlays);
        ClearOverlaySet(_lowBlueLightOverlays);
    }

    private static void RebuildOverlaySet(List<TintOverlayForm> overlays, Color color, int intensityPercent)
    {
        ClearOverlaySet(overlays);
        foreach (var screen in Screen.AllScreens)
        {
            var overlay = new TintOverlayForm(screen.Bounds, color, intensityPercent);
            overlays.Add(overlay);
            overlay.Show();
        }
    }

    private static void ClearOverlaySet(List<TintOverlayForm> overlays)
    {
        foreach (var overlay in overlays)
        {
            overlay.Close();
            overlay.Dispose();
        }
        overlays.Clear();
    }
}
