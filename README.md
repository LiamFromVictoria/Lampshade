# Lampshade

A lightweight Windows tray app that dims your screens and/or applies a warm low-blue-light tint — no main window, no fuss.

## Features

- **Dim Mode** — darkens all monitors by an adjustable percentage (10–90%).
- **Low Blue Light** — applies an adjustable warm/amber tint (10–100%) to ease eye strain, especially at night.
- Both modes run independently and can be combined; each renders as a per-monitor click-through overlay.
- **Tray-only UI** — lives entirely in the system tray via a right-click menu (`Dim Mode`, `Low Blue Light`, `Settings…`, `Exit`); no taskbar window.
- **Settings window** — adjust dim/tint strength and toggle "Start with Windows".
- **Start with Windows** — optional auto-launch at sign-in via the per-user `Run` registry key (no installer needed).
- **Single instance + hotkey-friendly toggle** — relaunching the app (e.g. from a pinned shortcut with a keyboard shortcut) toggles the already-running instance's dim state instead of spawning a second tray icon.
- Settings persist as JSON under `%AppData%\Lampshade\settings.json`.

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download) (to build; the published app is self-contained enough to just run the `.exe`)

## Build & Run

```bash
./build.sh
```

This restores, validates formatting (`dotnet format --verify-no-changes`), builds with warnings as errors, and publishes to `publish/Lampshade.exe`. The same script runs in CI (`.github/workflows/ci.yml`) on `windows-latest`.

Or manually:

```bash
dotnet build --configuration Release
dotnet publish --configuration Release --output publish
```

Run `publish/Lampshade.exe` — it starts minimized to the system tray.

## Usage

Right-click the tray icon to:
- Toggle **Dim Mode** or **Low Blue Light** independently.
- Open **Settings…** to adjust dim strength, tint strength, and startup behavior.
- **Exit** the app.

## Project Layout

| File | Purpose |
|---|---|
| `Program.cs` | Entry point; single-instance enforcement and toggle signaling |
| `TrayApplicationContext.cs` | Tray icon, menu wiring, overlay lifecycle |
| `TintOverlayForm.cs` | Per-monitor click-through dim/tint overlay window |
| `SettingsForm.cs` | Settings window UI |
| `AppSettings.cs` | JSON-persisted user preferences |
| `StartupManager.cs` | Registers/unregisters launch-at-sign-in |
| `IconFactory.cs` | Generates tray icon states |
| `Theme.cs`, `Modern*.cs` | Custom dark-mode UI controls (toggle, slider, context menu) |

## License

No license file present — all rights reserved by default unless the repository owner adds one.
