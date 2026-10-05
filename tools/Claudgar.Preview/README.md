# Isolated desktop preview

This small Windows-only harness renders the current application with fictional data. It moves the populated application shell into a plain, nonactivating offscreen preview host and uses `DrawToBitmap`. The production main window is never shown, and the startup, installer, game discovery, and MCP service paths are never called. The coordinator constructor only loads settings and embedded resources. A constructor-created tray icon is immediately hidden and explicitly disposed. Native WinForms rendering may require running outside a restricted desktop sandbox.

From the repository root:

```powershell
dotnet run --project tools/Claudgar.Preview -- dist/preview-1440.png --width 1440 --height 960
dotnet run --project tools/Claudgar.Preview -- dist/preview-1120.png --width 1120 --height 780
dotnet run --project tools/Claudgar.Preview -- dist/preview-scale2.png --width 1440 --height 960 --scale 2
dotnet run --project tools/Claudgar.Preview -- dist/preview-no-game.png --width 1120 --height 780 --no-game
```

The requested dimensions are the client area; output includes the native window frame. `--scale 2` exercises doubled control bounds at a 96-DPI baseline. It is a layout simulation, not verification on a real 200% DPI monitor. Visual inspection of the resulting PNGs is still required.

The harness checks major control bounds, character filtering and navigation, icon presence, and conditional setup actions appearing and disappearing as health changes. It uses reflection to keep preview-only fixtures out of the production app and deliberately fails if relevant internal controls change.

`--no-game` renders the initial folder-selection screen and checks that it offers a single highlighted action without a redundant issue bar.
