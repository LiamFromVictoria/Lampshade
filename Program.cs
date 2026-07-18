using System;
using System.Threading;
using System.Windows.Forms;

namespace Lampshade;

internal static class Program
{
    private const string SingleInstanceMutexName = "Lampshade.SingleInstance";
    private const string ToggleSignalName = "Lampshade.ToggleSignal";

    /// <summary>
    /// Application entry point. This app has no main window — it lives entirely
    /// in the system tray, driven by <see cref="TrayApplicationContext"/>.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        using var singleInstanceMutex = new Mutex(initiallyOwned: true, name: SingleInstanceMutexName, out var createdNew);
        using var toggleSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ToggleSignalName);

        if (!createdNew)
        {
            // Already running — e.g. the user relaunched via a pinned shortcut with a
            // keyboard hotkey. Toggle the running instance's dim state instead of
            // spawning a second tray icon, then exit immediately.
            toggleSignal.Set();
            return;
        }

        ApplicationConfiguration.Initialize();

        using var context = new TrayApplicationContext(toggleSignal);
        Application.Run(context);
    }
}
