# NotSoBright

A lightweight, always-on-top overlay for dimming your screen without changing system brightness.

## Features
- Adjustable opacity with quick +/- controls
- Edit mode for drag/resize and control interaction
- Passive mode for click-through overlay
- Tray icon with quick actions

## Limitations
- Exclusive fullscreen apps may block overlays. Switch to borderless or windowed mode.

## Build
- Requires .NET SDK for Windows (net10.0-windows)

```powershell
# From repo root
 dotnet build NotSoBright.sln
```

## Run
```powershell
# From repo root
 dotnet run --project NotSoBright\NotSoBright.csproj
```

## Notes
- Settings are stored in %APPDATA%\NotSoBright\config.json
