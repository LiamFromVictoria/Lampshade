using System;
using System.IO;
using System.Text.Json;

namespace ScreenDimmer;

/// <summary>
/// Persisted user preferences, stored as JSON under %AppData%\ScreenDimmer.
/// </summary>
internal sealed class AppSettings
{
    private static readonly string SettingsDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenDimmer");

    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    /// <summary>How dark the overlay is, from 10 (barely dimmed) to 90 (nearly black).</summary>
    public int DimPercent { get; set; } = 50;

    /// <summary>Whether ScreenDimmer should launch automatically at Windows sign-in.</summary>
    public bool StartWithWindows { get; set; }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded is not null)
                {
                    loaded.DimPercent = Math.Clamp(loaded.DimPercent, 10, 90);
                    return loaded;
                }
            }
        }
        catch
        {
            // Corrupt or unreadable settings file — fall back to defaults rather than crash.
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Best-effort persistence; a failed save should never crash the tray app.
        }
    }
}
