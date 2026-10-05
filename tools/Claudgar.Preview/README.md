# Isolated desktop preview

This small Windows-only harness renders the current application with fictional data. It creates control handles and uses `DrawToBitmap`; it never shows the main window or calls the startup, installer, game discovery, or MCP service paths. The coordinator constructor only loads settings and embedded resources. A constructor-created tray icon is immediately hidden and explicitly disposed.

From the repository root:

```powershell
dotnet run --project tools/Claudgar.Preview -- dist/preview-1440.png --width 1440 --height 960
dotnet run --project tools/Claudgar.Preview -- dist/preview-1120.png --width 1120 --height 780
dotnet run --project tools/Claudgar.Preview -- dist/preview-scale2.png --width 1440 --height 960 --scale 2
```

The requested dimensions are the client area; output includes the native window frame. `--scale 2` exercises doubled control bounds at a 96-DPI baseline. It is a layout simulation, not verification on a real 200% DPI monitor. Visual inspection of the resulting PNGs is still required.

The harness checks major control bounds, a populated character grid, collection filtering, navigation into a quest collection, and a nonempty image. It uses reflection to keep preview-only fixtures out of the production app and deliberately fails if relevant internal controls change.
