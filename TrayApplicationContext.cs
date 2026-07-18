using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ScreenDimmer;

/// <summary>
/// Owns the tray icon, the per-monitor dim overlays, and all menu wiring. This is
/// the whole application — there is no main window.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private static readonly int[] DimLevels = { 25, 50, 65, 80, 90 };

    private readonly AppSettings _settings;
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _toggleMenuItem;
    private readonly ToolStripMenuItem _startWithWindowsMenuItem;
    private readonly Dictionary<int, ToolStripMenuItem> _dimLevelMenuItems = new();
    private readonly List<DimOverlayForm> _overlays = new();

    private readonly Icon _brightIcon;
    private readonly Icon _dimIcon;

    private readonly EventWaitHandle _toggleSignal;
    private readonly Thread _toggleSignalListener;
    private bool _isDimmed;
    private bool _disposed;

    public TrayApplicationContext(EventWaitHandle toggleSignal)
    {
        _settings = AppSettings.Load();
        _toggleSignal = toggleSignal;

        _brightIcon = IconFactory.CreateTrayIcon(dimmed: false);
        _dimIcon = IconFactory.CreateTrayIcon(dimmed: true);

        _toggleMenuItem = new ToolStripMenuItem("Dim Mode", null, (_, _) => ToggleDim())
        {
            CheckOnClick = false,
        };

        var dimLevelMenu = new ToolStripMenuItem("Dim Level");
        foreach (var level in DimLevels)
        {
            var item = new ToolStripMenuItem($"{level}%", null, (_, _) => SetDimLevel(level))
            {
                Checked = level == _settings.DimPercent,
            };
            _dimLevelMenuItems[level] = item;
            dimLevelMenu.DropDownItems.Add(item);
        }

        _startWithWindowsMenuItem = new ToolStripMenuItem("Start with Windows", null, (_, _) => ToggleStartWithWindows())
        {
            Checked = StartupManager.IsEnabled(),
        };
        // Keep the persisted flag honest in case the registry value was removed out-of-band.
        _settings.StartWithWindows = _startWithWindowsMenuItem.Checked;

        var exitMenuItem = new ToolStripMenuItem("Exit", null, (_, _) => Exit());

        var menu = new ContextMenuStrip();
        menu.Items.Add(_toggleMenuItem);
        menu.Items.Add(dimLevelMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startWithWindowsMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitMenuItem);

        _trayIcon = new NotifyIcon
        {
            Icon = _brightIcon,
            Text = "ScreenDimmer — click to dim",
            ContextMenuStrip = menu,
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
            Name = "ScreenDimmer.ToggleSignalListener",
        };
        _toggleSignalListener.Start();

        UpdateMenuAndIconState();
    }

    private void OnTrayIconMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ToggleDim();
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Monitor added/removed/resolution changed while dimmed: rebuild overlays
        // to match the new monitor layout.
        if (_isDimmed)
        {
            RemoveOverlays();
            CreateOverlays();
        }
    }

    private void ToggleDim()
    {
        _isDimmed = !_isDimmed;

        if (_isDimmed)
        {
            CreateOverlays();
        }
        else
        {
            RemoveOverlays();
        }

        UpdateMenuAndIconState();
    }

    private void SetDimLevel(int percent)
    {
        _settings.DimPercent = percent;
        _settings.Save();

        foreach (var item in _dimLevelMenuItems.Values)
        {
            item.Checked = false;
        }
        _dimLevelMenuItems[percent].Checked = true;

        foreach (var overlay in _overlays)
        {
            overlay.SetDimPercent(percent);
        }
    }

    private void ToggleStartWithWindows()
    {
        var enable = !_startWithWindowsMenuItem.Checked;
        StartupManager.SetEnabled(enable);
        _startWithWindowsMenuItem.Checked = enable;
        _settings.StartWithWindows = enable;
        _settings.Save();
    }

    private void CreateOverlays()
    {
        RemoveOverlays();

        foreach (var screen in Screen.AllScreens)
        {
            var overlay = new DimOverlayForm(screen.Bounds, _settings.DimPercent);
            _overlays.Add(overlay);
            overlay.Show();
        }
    }

    private void RemoveOverlays()
    {
        foreach (var overlay in _overlays)
        {
            overlay.Close();
            overlay.Dispose();
        }
        _overlays.Clear();
    }

    private void UpdateMenuAndIconState()
    {
        _toggleMenuItem.Text = _isDimmed ? "Dim Mode (On)" : "Dim Mode (Off)";
        _toggleMenuItem.Checked = _isDimmed;
        _trayIcon.Icon = _isDimmed ? _dimIcon : _brightIcon;
        _trayIcon.Text = _isDimmed
            ? $"ScreenDimmer — dimmed to {_settings.DimPercent}% (click to restore)"
            : "ScreenDimmer — click to dim";
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
        RemoveOverlays();

        _trayIcon.Visible = false;
        _trayIcon.Dispose();

        var brightHandle = _brightIcon.Handle;
        var dimHandle = _dimIcon.Handle;
        _brightIcon.Dispose();
        _dimIcon.Dispose();
        DestroyIcon(brightHandle);
        DestroyIcon(dimHandle);

        ExitThread();
    }
}
