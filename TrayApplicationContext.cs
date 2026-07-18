using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Lampshade;

/// <summary>
/// Owns the tray icon, the per-monitor dim/low-blue-light overlays, the Settings
/// window, and all menu wiring. This is the whole application — there is no main
/// window.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private static readonly Color LowBlueLightColor = Color.FromArgb(255, 130, 30);

    private readonly AppSettings _settings;
    private readonly NotifyIcon _trayIcon;
    private readonly ModernContextMenuStrip _menu;
    private readonly ToolStripMenuItem _dimMenuItem;
    private readonly ToolStripMenuItem _lowBlueLightMenuItem;

    private readonly List<TintOverlayForm> _dimOverlays = new();
    private readonly List<TintOverlayForm> _lowBlueLightOverlays = new();

    private readonly Icon _normalIcon;
    private readonly Icon _dimmedIcon;
    private readonly Icon _lowBlueLightIcon;

    private readonly EventWaitHandle _toggleSignal;
    private readonly Thread _toggleSignalListener;

    private SettingsForm? _settingsForm;
    private bool _isDimmed;
    private bool _isLowBlueLight;
    private bool _disposed;

    public TrayApplicationContext(EventWaitHandle toggleSignal)
    {
        _settings = AppSettings.Load();
        _toggleSignal = toggleSignal;

        _normalIcon = IconFactory.CreateTrayIcon(TrayIconState.Normal);
        _dimmedIcon = IconFactory.CreateTrayIcon(TrayIconState.Dimmed);
        _lowBlueLightIcon = IconFactory.CreateTrayIcon(TrayIconState.LowBlueLight);

        _dimMenuItem = new ToolStripMenuItem("Dim Mode", null, (_, _) => ToggleDim());
        _lowBlueLightMenuItem = new ToolStripMenuItem("Low Blue Light", null, (_, _) => ToggleLowBlueLight());
        var settingsMenuItem = new ToolStripMenuItem("Settings…", null, (_, _) => ShowSettings());
        var exitMenuItem = new ToolStripMenuItem("Exit", null, (_, _) => Exit());

        _menu = new ModernContextMenuStrip();
        _menu.Items.Add(_dimMenuItem);
        _menu.Items.Add(_lowBlueLightMenuItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(settingsMenuItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(exitMenuItem);

        _trayIcon = new NotifyIcon
        {
            Icon = _normalIcon,
            Text = "Lampshade",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _trayIcon.MouseUp += OnTrayIconMouseUp;

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        // A relaunched second instance signals this handle (see Program.cs) to ask
        // the running instance to toggle dim mode. Marshal back onto the UI thread —
        // NotifyIcon's native window is created above, so the WinForms sync context
        // is installed by the time this thread starts waiting.
        var uiContext = SynchronizationContext.Current;
        _toggleSignalListener = new Thread(() =>
        {
            while (true)
            {
                _toggleSignal.WaitOne();
                if (_disposed)
                {
                    return;
                }
                uiContext?.Post(_ => ToggleDim(), null);
            }
        })
        {
            IsBackground = true,
            Name = "Lampshade.ToggleSignalListener",
        };
        _toggleSignalListener.Start();

        UpdateMenuAndIconState();
    }

    private void OnTrayIconMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button is MouseButtons.Left or MouseButtons.Right)
        {
            ShowTrayMenu();
        }
    }

    /// <summary>
    /// NotifyIcon only auto-shows its ContextMenuStrip on a right-click. We want the
    /// same menu on a left-click too, so we invoke the framework's own (non-public)
    /// display routine — it gets positioning, dismiss-on-click-away, and keyboard
    /// nav exactly right. Fall back to a manual Show if that private method is ever
    /// unavailable on a future runtime.
    /// </summary>
    private void ShowTrayMenu()
    {
        var showContextMenu = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
        if (showContextMenu is not null)
        {
            showContextMenu.Invoke(_trayIcon, null);
        }
        else
        {
            _menu.Show(Cursor.Position);
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Monitor added/removed/resolution changed: rebuild whichever overlay sets
        // are currently active to match the new monitor layout.
        if (_isDimmed)
        {
            RebuildOverlaySet(_dimOverlays, Color.Black, _settings.DimPercent);
        }
        if (_isLowBlueLight)
        {
            RebuildOverlaySet(_lowBlueLightOverlays, LowBlueLightColor, _settings.LowBlueLightPercent);
        }
    }

    private void ToggleDim()
    {
        _isDimmed = !_isDimmed;
        if (_isDimmed)
        {
            RebuildOverlaySet(_dimOverlays, Color.Black, _settings.DimPercent);
        }
        else
        {
            ClearOverlaySet(_dimOverlays);
        }
        UpdateMenuAndIconState();
    }

    private void ToggleLowBlueLight()
    {
        _isLowBlueLight = !_isLowBlueLight;
        if (_isLowBlueLight)
        {
            RebuildOverlaySet(_lowBlueLightOverlays, LowBlueLightColor, _settings.LowBlueLightPercent);
        }
        else
        {
            ClearOverlaySet(_lowBlueLightOverlays);
        }
        UpdateMenuAndIconState();
    }

    private void ApplyDimPercent(int percent)
    {
        _settings.DimPercent = percent;
        _settings.Save();
        foreach (var overlay in _dimOverlays)
        {
            overlay.SetIntensityPercent(percent);
        }
    }

    private void ApplyLowBlueLightPercent(int percent)
    {
        _settings.LowBlueLightPercent = percent;
        _settings.Save();
        foreach (var overlay in _lowBlueLightOverlays)
        {
            overlay.SetIntensityPercent(percent);
        }
    }

    private void ApplyStartWithWindows(bool enabled)
    {
        StartupManager.SetEnabled(enabled);
        _settings.StartWithWindows = enabled;
        _settings.Save();
    }

    private void ShowSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(
            _settings,
            StartupManager.IsEnabled(),
            _normalIcon,
            ApplyDimPercent,
            ApplyLowBlueLightPercent,
            ApplyStartWithWindows);
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.Show();
        _settingsForm.Activate();
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

    private void UpdateMenuAndIconState()
    {
        _dimMenuItem.Checked = _isDimmed;
        _lowBlueLightMenuItem.Checked = _isLowBlueLight;

        _trayIcon.Icon = _isDimmed ? _dimmedIcon : _isLowBlueLight ? _lowBlueLightIcon : _normalIcon;

        _trayIcon.Text = (_isDimmed, _isLowBlueLight) switch
        {
            (true, true) => $"Lampshade — dimmed {_settings.DimPercent}% + low blue light",
            (true, false) => $"Lampshade — dimmed {_settings.DimPercent}%",
            (false, true) => $"Lampshade — low blue light {_settings.LowBlueLightPercent}%",
            (false, false) => "Lampshade",
        };
    }

    private void Exit()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _toggleSignal.Set(); // wake the listener thread so it observes _disposed and exits

        ClearOverlaySet(_dimOverlays);
        ClearOverlaySet(_lowBlueLightOverlays);
        _settingsForm?.Close();

        _trayIcon.Visible = false;
        _trayIcon.Dispose();

        var normalHandle = _normalIcon.Handle;
        var dimmedHandle = _dimmedIcon.Handle;
        var lowBlueLightHandle = _lowBlueLightIcon.Handle;
        _normalIcon.Dispose();
        _dimmedIcon.Dispose();
        _lowBlueLightIcon.Dispose();
        DestroyIcon(normalHandle);
        DestroyIcon(dimmedHandle);
        DestroyIcon(lowBlueLightHandle);

        ExitThread();
    }
}
