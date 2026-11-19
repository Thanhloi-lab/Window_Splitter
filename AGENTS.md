# AGENTS.md - Project Context for Antigravity

This file provides a fast architectural index for AI agents working on **Windows Splitter** across any machine. For full behavioral details, read `TECHNICAL_SPEC.md`.

## 1. Project Identity & Stack
- **App:** Windows Splitter (Ultrawide Zone & Smart Borderless Manager)
- **Runtime:** `.NET 8.0` (`net8.0-windows`, WPF, `WPF-UI 4.3.0`, `Hardcodet.NotifyIcon.Wpf 2.0.1`)
- **Project File:** `WindowsSplitter.csproj`
- **Privileges & DPI:** `app.manifest` enforces `requireAdministrator` (required for `WH_KEYBOARD_LL` and cross-process window manipulation across UIPI) and `PerMonitorV2` DPI awareness.
- **Settings File:** `%AppData%\WindowsSplitter\appsettings.json`

## 2. Build & Package Commands
```powershell
dotnet build WindowsSplitter.csproj -c Release
dotnet run --project WindowsSplitter.csproj
installer\build-installer.bat   # Self-contained win-x64 publish + Inno Setup 6
```

## 3. Architecture Map
- **Entry & Lifecycle:** `App.xaml(.cs)` -> Single-instance `Mutex` (`WindowsSplitter_SingleInstance_Mutex`) + `WM_SHOW_SPLITTER` broadcast, `LocalizationManager.Initialize`, `ApplicationTheme.Dark`.
- **Main Host & Tray:** `UI/MainWindow.xaml(.cs)` -> Owns `AppSettings`, `ZoneEngine`, `BorderlessEngine`, `WinEventHookService`, `LowLevelKeyboardHook`, `WM_DISPLAYCHANGE` hook, and `Hardcodet.Wpf.TaskbarNotification.TaskbarIcon`.
- **Win32 Interop:** `Core/NativeMethods.cs` -> P/Invoke declarations (`user32`, `dwmapi`, `shcore`, `kernel32`).
- **Monitors & DPI:** `Core/MonitorManager.cs` -> Singleton `Instance`, left-to-right monitor ordering, `GetDpiForMonitor`, `IsDuplicateMode`, `GetNextMonitor` wrap-around.
- **Global Hotkeys & Top-Edge Guard:** `Core/LowLevelKeyboardHook.cs` (`WH_KEYBOARD_LL` intercepting `Win+Arrow`, `Win+Shift+Arrow`, `Win+Ctrl+B`) + `Core/TopEdgeSnapGuard.cs` (`WH_MOUSE_LL` preventing accidental Windows Snap maximize when clicking `HTTOP` at `WorkArea.Top` on snapped windows).
- **Zone Snapping & 2D Navigation:** `Core/ZoneEngine.cs` -> `ResolveZonesForMonitor` (WorkArea-scaled), `SnapWindow` (2D spatial `FindNeighborZone`, edge Maximize/Minimize, Cross-Monitor Wrap), `MoveWindowToMonitor`, `ApplyWindowToZone` (2-pass DWM alignment via `DWMWA_EXTENDED_FRAME_BOUNDS` / fallback `BorderCompensationPixels = 7`, Win11 `DWMWCP_DONOTROUND`).
- **Borderless Mode:** `Core/BorderlessEngine.cs` + `Core/WinEventHookService.cs` + `Core/WindowEnumerator.cs` -> Strips frame styles (`WS_CAPTION`, `WS_THICKFRAME`, etc.), applies `WS_POPUP`, preserves snapped zone or fills monitor above taskbar (`HWND_TOPMOST` + `EVENT_SYSTEM_FOREGROUND`), attaches `FloatingCaptionBar`, restores all managed windows on `CleanUp()`.
- **Models & Presets:** `Models/AppSettings.cs`, `Models/ZoneLayout.cs` (`Zone` with Pixel & `Percent*`, `ZoneSplitNode` recursive binary split tree guaranteeing non-overlapping partitions, `ZonePreset` with `ZoneGap` & `IsDefault = true` locked against overwrite/delete, `MonitorLayoutConfig`), `Models/RunningWindowItem.cs`.
- **Auxiliary Windows:** `UI/ZoneOverlayWindow.xaml(.cs)`, `UI/FloatingCaptionBar.xaml(.cs)`, `UI/BorderlessNoticeWindow.xaml(.cs)`, `UI/SaveProfileDialog.xaml(.cs)`.
- **Localization & Startup:** `Core/LocalizationManager.cs` (`Resources/Languages/Strings.en.xaml` default, `Strings.vi.xaml`), `Core/StartupManager.cs` (Task Scheduler `WindowsSplitter_Elevated` `/RL HIGHEST /IT` with Registry `Run` fallback).

## 4. Non-Negotiable Engineering & Design Invariants
1. **Design / No AI-Cliché Icons:** Do NOT use decorative AI-style emojis/icons (such as rockets, UFOs, sparkles, stars, gears, wrenches) in documentation, UI copy, or new XAML components. Prefer clean text, vector geometry, or `Segoe Fluent Icons` where iconography is needed, following `WPF_UI_DESIGN_SYSTEM.md`.
2. **Physical Pixels vs WPF DIPs:** Overlay/floating Win32-aligned windows (`ZoneOverlayWindow`, `FloatingCaptionBar`, `BorderlessNoticeWindow`) must position using `SetWindowPos` in physical pixels (or explicit `DpiScaleX`/`DpiScaleY` scaling) so multi-monitor mixed-DPI setups stay aligned.
3. **Default Preset Immutability:** Never mutate `ZonePreset` entries where `IsDefault == true`; edits to a default preset in the UI must preview only and prompt/save as a new custom profile (`IsDefault = false`).
4. **DWM Compensation Guard:** Only apply DWM invisible border compensation when `hasDwmFrame` (`WS_CAPTION | WS_THICKFRAME | WS_BORDER`) is true; applying fallback pixel offsets to borderless popup windows creates unwanted gaps.
5. **Cleanup on Exit:** Always ensure `BorderlessEngine.CleanUp()` and hook `Dispose()` run on real exit (`MainWindow.OnClosing`) so user windows do not remain trapped without title bars.

## 5. Optional Local CodeGraph Indexing
- If `@colbymchenry/codegraph` is installed and configured in the machine's global Antigravity MCP config (`~/.gemini/config/mcp_config.json`), initialize once per clone with `codegraph init -i` to enable symbol/call-graph queries via MCP. `.codegraph/` is gitignored and local-only.
