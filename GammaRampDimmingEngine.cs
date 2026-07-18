using System.Runtime.InteropServices;

namespace Lampshade;

/// <summary>
/// A vendor-neutral dimming backend that scales each display's GDI gamma ramp
/// (<c>SetDeviceGammaRamp</c>) instead of drawing a window overlay. This is applied
/// by the display driver itself, below any graphics API, so it works identically on
/// NVIDIA, AMD, and Intel and — unlike <see cref="OverlayDimmingEngine"/> — still
/// dims fullscreen-exclusive and Vulkan/DirectX content that bypasses the desktop
/// compositor. Trade-off: some drivers reject a ramp that deviates too far from the
/// default, so very strong settings may silently have no effect on a given display;
/// per-display failures are swallowed rather than surfaced, matching how the rest
/// of this app treats OS-level operations as best-effort.
/// </summary>
internal sealed class GammaRampDimmingEngine : IDimmingEngine
{
    [StructLayout(LayoutKind.Sequential)]
    private struct GammaRamp
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Red;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Green;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Blue;
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string? lpszOutput, IntPtr lpInitData);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool GetDeviceGammaRamp(IntPtr hdc, ref GammaRamp lpRamp);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool SetDeviceGammaRamp(IntPtr hdc, ref GammaRamp lpRamp);

    // Keyed by Screen.DeviceName (e.g. "\\.\DISPLAY1"): the ramp that was in effect
    // before this engine first touched that display, so it can be restored exactly
    // — respecting any existing ICC profile / Night Light calibration — rather than
    // resetting to a flat linear ramp.
    private readonly Dictionary<string, GammaRamp> _originalRamps = new();

    private bool _dimActive;
    private int _dimPercent = 50;
    private bool _lowBlueLightActive;
    private int _lowBlueLightPercent = 40;

    public void SetDimActive(bool active, int percent)
    {
        _dimActive = active;
        _dimPercent = percent;
        ApplyToAllDisplays();
    }

    public void SetDimPercent(int percent)
    {
        _dimPercent = percent;
        ApplyToAllDisplays();
    }

    public void SetLowBlueLightActive(bool active, int percent)
    {
        _lowBlueLightActive = active;
        _lowBlueLightPercent = percent;
        ApplyToAllDisplays();
    }

    public void SetLowBlueLightPercent(int percent)
    {
        _lowBlueLightPercent = percent;
        ApplyToAllDisplays();
    }

    public void RefreshForDisplayChange(bool dimActive, int dimPercent, bool lowBlueLightActive, int lowBlueLightPercent)
    {
        _dimActive = dimActive;
        _dimPercent = dimPercent;
        _lowBlueLightActive = lowBlueLightActive;
        _lowBlueLightPercent = lowBlueLightPercent;
        PruneDisconnectedDisplays();
        ApplyToAllDisplays();
    }

    public void Dispose()
    {
        foreach (var (deviceName, original) in _originalRamps)
        {
            RestoreDisplay(deviceName, original);
        }
        _originalRamps.Clear();
    }

    private void ApplyToAllDisplays()
    {
        foreach (var screen in Screen.AllScreens)
        {
            ApplyToDisplay(screen.DeviceName);
        }
    }

    private void ApplyToDisplay(string deviceName)
    {
        var hdc = CreateDC("DISPLAY", deviceName, null, IntPtr.Zero);
        if (hdc == IntPtr.Zero)
        {
            return;
        }

        try
        {
            if (!_originalRamps.TryGetValue(deviceName, out var baseline))
            {
                baseline = AllocateRamp();
                if (!GetDeviceGammaRamp(hdc, ref baseline))
                {
                    baseline = IdentityRamp();
                }
                _originalRamps[deviceName] = baseline;
            }

            var effective = ComputeEffectiveRamp(baseline);
            SetDeviceGammaRamp(hdc, ref effective); // best-effort: some drivers reject extreme ramps
        }
        finally
        {
            DeleteDC(hdc);
        }
    }

    private GammaRamp ComputeEffectiveRamp(GammaRamp baseline)
    {
        var brightnessFactor = _dimActive ? 1.0 - Math.Clamp(_dimPercent, 0, 100) / 100.0 : 1.0;
        var tintStrength = _lowBlueLightActive ? DimmingMath.MapLowBlueLightOpacityPercent(_lowBlueLightPercent) / 100.0 : 0.0;
        var tint = DimmingMath.LowBlueLightColor;

        var redScale = brightnessFactor * Lerp(1.0, tint.R / 255.0, tintStrength);
        var greenScale = brightnessFactor * Lerp(1.0, tint.G / 255.0, tintStrength);
        var blueScale = brightnessFactor * Lerp(1.0, tint.B / 255.0, tintStrength);

        var result = AllocateRamp();
        for (var i = 0; i < 256; i++)
        {
            result.Red[i] = ScaleChannel(baseline.Red[i], redScale);
            result.Green[i] = ScaleChannel(baseline.Green[i], greenScale);
            result.Blue[i] = ScaleChannel(baseline.Blue[i], blueScale);
        }
        return result;
    }

    private void PruneDisconnectedDisplays()
    {
        var connected = new HashSet<string>(Screen.AllScreens.Select(screen => screen.DeviceName));
        foreach (var deviceName in _originalRamps.Keys.Where(deviceName => !connected.Contains(deviceName)).ToList())
        {
            _originalRamps.Remove(deviceName);
        }
    }

    private static void RestoreDisplay(string deviceName, GammaRamp original)
    {
        var hdc = CreateDC("DISPLAY", deviceName, null, IntPtr.Zero);
        if (hdc == IntPtr.Zero)
        {
            return;
        }
        var ramp = original;
        SetDeviceGammaRamp(hdc, ref ramp);
        DeleteDC(hdc);
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static ushort ScaleChannel(ushort value, double scale) =>
        (ushort)Math.Clamp(Math.Round(value * scale), 0, 65535);

    private static GammaRamp AllocateRamp() => new()
    {
        Red = new ushort[256],
        Green = new ushort[256],
        Blue = new ushort[256],
    };

    private static GammaRamp IdentityRamp()
    {
        var ramp = AllocateRamp();
        for (var i = 0; i < 256; i++)
        {
            var v = (ushort)(i * 257);
            ramp.Red[i] = v;
            ramp.Green[i] = v;
            ramp.Blue[i] = v;
        }
        return ramp;
    }
}
