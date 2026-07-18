using System.Runtime.InteropServices;

namespace Lampshade;

/// <summary>
/// A vendor-neutral dimming backend that scales each display's GDI gamma ramp
/// (<c>SetDeviceGammaRamp</c>) instead of drawing a window overlay. This is applied
/// by the display driver itself, below any graphics API, so it works identically on
/// NVIDIA, AMD, and Intel and — unlike <see cref="OverlayDimmingEngine"/> — still
/// dims fullscreen-exclusive and Vulkan/DirectX content that bypasses the desktop
/// compositor. Trade-off: Windows itself rejects a gamma ramp that darkens the
/// display too aggressively (a long-standing anti-abuse check in the GDI gamma
/// ramp API, independent of GPU vendor), so very strong settings can't always be
/// reached through this method. Rather than let a rejected <c>SetDeviceGammaRamp</c>
/// call silently leave the display at whatever ramp happened to be active before,
/// <see cref="ApplyToDisplay"/> binary-searches between the display's original ramp
/// (guaranteed acceptable) and the requested one to find and apply the strongest
/// ramp the display will actually accept, and reports whether the full request was
/// honored so the UI can tell the user when a display is capped.
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

    /// <summary>Bisection steps used to home in on the strongest ramp a display accepts.
    /// 10 steps resolves the interpolation factor to roughly 1/1024 — far finer than the
    /// 8-bit-per-channel ramp values can distinguish — while staying cheap (only runs when
    /// the full-strength ramp was rejected).</summary>
    private const int AcceptanceSearchSteps = 10;

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
    // resetting to a flat linear ramp. It also doubles as the known-acceptable floor
    // the acceptance search bisects from.
    private readonly Dictionary<string, GammaRamp> _originalRamps = new();

    private bool _dimActive;
    private int _dimPercent = 50;
    private bool _lowBlueLightActive;
    private int _lowBlueLightPercent = 40;

    public bool SetDimActive(bool active, int percent)
    {
        _dimActive = active;
        _dimPercent = percent;
        return ApplyToAllDisplays();
    }

    public bool SetDimPercent(int percent)
    {
        _dimPercent = percent;
        return ApplyToAllDisplays();
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

    /// <returns><c>true</c> if every connected display accepted the exact requested ramp.</returns>
    private bool ApplyToAllDisplays()
    {
        var fullyApplied = true;
        foreach (var screen in Screen.AllScreens)
        {
            fullyApplied &= ApplyToDisplay(screen.DeviceName);
        }
        return fullyApplied;
    }

    /// <returns><c>true</c> if this display accepted the exact requested ramp; <c>false</c>
    /// if it had to be clamped to the strongest ramp the display would accept instead.</returns>
    private bool ApplyToDisplay(string deviceName)
    {
        var hdc = CreateDC("DISPLAY", deviceName, null, IntPtr.Zero);
        if (hdc == IntPtr.Zero)
        {
            return true; // display unavailable: nothing to clamp, don't report a false failure
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

            var requested = ComputeEffectiveRamp(baseline);
            return ApplyStrongestAcceptedRamp(hdc, baseline, requested);
        }
        finally
        {
            DeleteDC(hdc);
        }
    }

    /// <summary>
    /// Applies <paramref name="requested"/> if the display accepts it outright. Otherwise
    /// bisects between <paramref name="baseline"/> (the display's original ramp, which by
    /// construction it already accepts) and <paramref name="requested"/>, applying the
    /// strongest interpolated ramp found acceptable, so a rejection never leaves the
    /// display showing a stale or arbitrary intensity from an earlier call.
    /// </summary>
    private static bool ApplyStrongestAcceptedRamp(IntPtr hdc, GammaRamp baseline, GammaRamp requested)
    {
        if (SetDeviceGammaRamp(hdc, ref requested))
        {
            return true;
        }

        var strongestAccepted = baseline;
        double acceptedT = 0.0, rejectedT = 1.0;
        for (var step = 0; step < AcceptanceSearchSteps; step++)
        {
            var midT = (acceptedT + rejectedT) / 2.0;
            var candidate = InterpolateRamp(baseline, requested, midT);
            if (SetDeviceGammaRamp(hdc, ref candidate))
            {
                strongestAccepted = candidate;
                acceptedT = midT;
            }
            else
            {
                rejectedT = midT;
            }
        }

        // The search's last successful call already left the display showing
        // strongestAccepted; re-apply defensively in case the final step rejected.
        SetDeviceGammaRamp(hdc, ref strongestAccepted);
        return false;
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

    private static GammaRamp InterpolateRamp(GammaRamp from, GammaRamp to, double t)
    {
        var result = AllocateRamp();
        for (var i = 0; i < 256; i++)
        {
            result.Red[i] = LerpChannel(from.Red[i], to.Red[i], t);
            result.Green[i] = LerpChannel(from.Green[i], to.Green[i], t);
            result.Blue[i] = LerpChannel(from.Blue[i], to.Blue[i], t);
        }
        return result;
    }

    private static ushort LerpChannel(ushort from, ushort to, double t) =>
        (ushort)Math.Clamp(Math.Round(from + (to - from) * t), 0, 65535);

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
