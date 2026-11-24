# QuickUp (Dynamic Web Panel)

A fast, lightweight Windows system tray web panel written in **C# (.NET 10 WinForms)** and **Microsoft Edge WebView2**.

QuickUp pins any web app (such as webmail, chat, AI tools, dashboards, or notes) to your Windows notification area as a borderless popup window—without bundling a separate Chromium/Electron runtime.

## Why .NET 10 + WebView2?

| Metric | Legacy Electron Build | QuickUp (.NET 10 + WebView2) |
| --- | --- | --- |
| Unpacked distribution (`dist\win-unpacked`) | 258.0 MB | ~1.22 MB |
| Single-file portable (`dist\DynamicWebPanel.exe`) | Bundled Chromium | ~1.39 MB |
| Idle footprint (`--hidden`) | Electron main + utility processes | ~11 MB private memory, **0** `msedgewebview2.exe` processes |

## Features

- **System Tray Popup**: Opens anchored next to the Windows taskbar on any edge (bottom, top, left, right) across multi-monitor and mixed-DPI (`PerMonitorV2`) displays.
- **Tabler v3 Icons**: Zero-dependency GDI+ SVG path renderer (`TablerIcons.cs`) powering the popup toolbar, onboarding view, Antigravity-style Settings view, and built-in icon presets.
- **Native Header Toolbar**: Back, Forward, Home, Reload, Open in Browser, and Pin to Foreground controls, plus an animated loading bar and themed right-click context menu.
- **Two-Stage WebView2 Lifecycle**:
  - **On Hide**: Hides the control and calls `CoreWebView2.TrySuspendAsync()` to pause timers, media, and network activity while keeping warm reopen fast.
  - **On Idle**: Disposes the `WebView2` control after a configurable timeout (`Instant`, `15s`, `30s`, or `60s`) so background WebView2 child processes exit completely.
- **Antigravity-Style Settings Window**:
  - **General**: Link URL, custom Panel Title / tray tooltip, and **Start with Windows** (`HKCU\...\Run`).
  - **Appearance**: Separate **Light Taskbar** and **Dark Taskbar** tray icons (upload PNG/JPEG/ICO/BMP/GIF or pick from 8 Tabler presets) and popup width (`400px`, `500px`, `600px`).
  - **Behavior**: Toggle between opening links **Inside Panel** or in your **Default Browser** (`Ctrl+Click` / middle-click always opens externally) and configure **Idle WebView Teardown**.
- **Single-Instance IPC**: Launching interactively while already running brings up the existing tray popup over a named pipe instead of starting a duplicate process.

## Requirements

- Windows 10 or Windows 11 (x64)
- [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (pre-installed on Windows 11)
- *(Optional)* [Inno Setup 6](https://jrsoftware.org/isdl.php) to compile the Windows installer (`dist\DynamicWebPanelSetup.exe`)

## Usage

Run the portable executable directly:

```powershell
.\dist\DynamicWebPanel.exe
```

### Command-Line Flags

- `--hidden`, `--background`, `--startup` (or `/hidden`, `/background`, `/startup`): Starts silently in the system tray without creating the popup or WebView2 until opened.

## Building from Source

```powershell
# Compile Release build
dotnet build .\QuickUp.csproj -c Release

# Publish dist\win-unpacked, dist\DynamicWebPanel.exe, and (if Inno Setup is installed) dist\DynamicWebPanelSetup.exe
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

`build.ps1` automatically stops any running `DynamicWebPanel.exe` instance launched from `dist\` before refreshing output binaries.

## Configuration & Data Paths

Settings remain backward-compatible with the legacy JSON config schema:

- **Config file**: `%APPDATA%\dynamic-web-panel\config.json`
- **WebView2 profile & cache**: `%LOCALAPPDATA%\DynamicWebPanel\WebView2` (disk cache capped at 50 MB)

Example `config.json`:

```json
{
  "customUrl": "https://notion.so",
  "customTitle": "Notion",
  "customIcon": "",
  "isPresetIcon": false,
  "customIconDark": "",
  "isPresetIconDark": false,
  "popupWidth": 400,
  "linkBehavior": "popup",
  "openAtLogin": false,
  "idleTeardownSeconds": 30
}
```

## Project Structure

- `Program.cs` — Single-instance mutex, CLI flag handling, named-pipe `SHOW` signaling, DPI/color mode initialization.
- `TrayAppContext.cs` — `NotifyIcon`, tray menu, left-click debounce, theme/display listeners, and pipe server.
- `PopupForm.cs` — Frameless DPI-aware popup, Tabler toolbar, onboarding screen, lazy WebView2 lifecycle, and context menu.
- `SettingsForm.cs` — DPI-scaled two-pane Antigravity-style settings dialog (`General`, `Appearance`, `Behavior`).
- `TablerIcons.cs` — Official Tabler v3 outline SVG path definitions and GDI+ path renderer.
- `AppSettings.cs` — Atomic JSON config persistence and URL normalization.
- `IconHelper.cs` — Embedded asset loader, image resizing/encoding (`<= 128x128` PNG), `.ico` conversion, and preset icon rendering.
- `ThemeHelper.cs` — Windows light/dark theme detection (`Personalize` registry keys) and color palettes.
- `NativeMethods.cs` — Win32 DWM attributes, taskbar edge detection, per-monitor DPI queries, and `WebView2Loader.dll` discovery.
- `StartupHelper.cs` — Windows startup registry management (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`).
- `build.ps1` & `setup.iss` — Distribution build script and Inno Setup installer definition.
