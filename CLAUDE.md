# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Khorshid is a Windows tray app showing the Persian (Solar Hijri) date, with a month calendar popup and an optional desktop widget. It is a single WinForms file (`Khorshid.cs`) compiled into a ~50 KB exe with no dependencies beyond .NET Framework 4.x. UI text is Persian and RTL.

## Commands

```powershell
.\build.ps1                      # compile Khorshid.exe (repo root)
.\tools\make-assets.ps1          # regenerate docs\*.png, assets\logo.png, assets\Khorshid.ico, packaging\Assets\*.png
.\packaging\build-msix.ps1 -Sign # build dist\Khorshid_<ver>.msix, self-signed for local testing
```

There is no test suite, linter or project file. Verify changes by building and running `Khorshid.exe`. Only one instance runs at a time (named mutex `Khorshid.SingleInstance`), so stop any running copy (`Get-Process Khorshid | Stop-Process`) before launching a rebuilt exe, or the new one exits immediately; the build also fails if the exe is locked.

## Compiler constraint: C# 5 only

Builds use the in-box `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`, which supports **C# 5**. Do not use string interpolation (`$"..."`), `?.`, `nameof`, expression-bodied members (`=>`), auto-property initializers, `out var`, tuples, or other C# 6+ features. Only `System.Windows.Forms.dll` and `System.Drawing.dll` are referenced; adding any other assembly means editing both `build.ps1` and `tools/make-assets.ps1`. Source is compiled with `/codepage:65001`, so keep files UTF-8 (they contain Persian literals).

## Architecture (`Khorshid.cs`)

- `Program` → single-instance mutex → `Application.Run(new TrayApp())`.
- `Persian`: all calendar logic on top of `System.Globalization.PersianCalendar` — month/weekday names, Persian digit conversion (`Digits`), Saturday-first column (`Col`), `IsOff` (Friday or holiday), and Iran's lunar calendar (`Lunar`). `LunarStarts` holds the official first day of each lunar month (Iran uses moon sighting); outside it, `HijriCalendar` is the fallback and may be a day off. **Each year, append the next year's month starts** from the official calendar (derive them from the published lunar holidays) and check the new year's holidays against the official list.
- `Occasions`: string tables of Solar, Lunar and Gregorian occasions (`"month/day text"`, `!` = official holiday, lunar day `0` = last day of the month), plus computed ones (last Friday of Ramadan, last Wednesday of the year). `For(date)` returns a day's occasions, holidays first.
- `Settings`: hand-rolled `key=value` file at `%APPDATA%\Khorshid\settings.ini` (widget on/off, topmost, position).
- `Startup`: start-with-Windows via the HKCU `Run` key — except when running as an MSIX package (`Native.IsPackaged()` via `GetCurrentPackageFullName`), where the tray menu instead opens Settings → Apps → Startup and the package's `StartupTask` (in `packaging/AppxManifest.xml`) handles it. Keep both paths working.
- `Native`: P/Invoke (DPI awareness, DWM rounded corners/dark title, window dragging, package detection).
- `Theme`: light/dark palette read from the `AppsUseLightTheme` registry value; `Theme.ForceDark` overrides it for asset generation.
- `Ui`: shared drawing helpers, including `DayIcon` which renders the tray icon with the day number.
- `TrayApp` (ApplicationContext) owns the `NotifyIcon`, context menu, and creates `CalendarPopup` and `DesktopWidget` (both owner-drawn `Form`s).

## Asset generation

`tools/AssetGen.cs` is compiled **together with** `Khorshid.cs` (entry point `Khorshid.Tools.AssetGen`) and renders the real `CalendarPopup`/`DesktopWidget` to produce README screenshots, the icon and Store logos. Any rename or signature change to those classes, `Theme`, `Ui`, or `Native` must keep `AssetGen.cs` compiling. Rerun `make-assets.ps1` after visual changes so `docs/` screenshots stay current.

## Release / distribution

- `install.ps1` is the per-user installer used by the one-liner in the README; it downloads `Khorshid.exe` from the latest GitHub release of `fr2ad/Khorshid` into `%LOCALAPPDATA%\Programs\Khorshid`.
- Store packaging details (Partner Center identity, version rules — last version part must be `0`) are in `packaging/README.md`; listing text is in `packaging/store-listing.md`. `AppxManifest.xml` uses `$IdentityName$`-style placeholders filled in by `build-msix.ps1`.
- The assembly version lives in `[assembly: AssemblyVersion]` at the top of `Khorshid.cs`.
- `*.exe`, `dist/`, `.tools/` (Windows SDK build tools for MSIX) are git-ignored.
