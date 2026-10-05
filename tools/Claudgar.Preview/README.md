# Isolated desktop preview

This small Windows-only harness renders the current application with a fictional character and embedded layout fixtures. It moves the populated application shell into a plain, nonactivating offscreen preview host and uses `DrawToBitmap`. The production main window is never shown, and the startup, installer, game discovery, and MCP service paths are never called. The coordinator constructor only loads settings and embedded resources. A constructor-created tray icon is immediately hidden and explicitly disposed. Native WinForms rendering may require running outside a restricted desktop sandbox.

From the repository root:

```powershell
dotnet run --project tools/Claudgar.Preview -- dist/preview-1440.png --width 1440 --height 960
dotnet run --project tools/Claudgar.Preview -- dist/preview-1120.png --width 1120 --height 780
dotnet run --project tools/Claudgar.Preview -- dist/preview-scale2.png --width 1440 --height 960 --scale 2
dotnet run --project tools/Claudgar.Preview -- dist/preview-no-game.png --width 1120 --height 780 --no-game
dotnet run --project tools/Claudgar.Preview -- dist/preview-inventory.png --section inventory
dotnet run --project tools/Claudgar.Preview -- dist/preview-talents.png --section talents
dotnet run --project tools/Claudgar.Preview -- dist/preview-talents-wide.png --width 2386 --height 1080 --section talents
dotnet run --project tools/Claudgar.Preview -- dist/preview-equipment.png --section equipment
dotnet run --project tools/Claudgar.Preview -- dist/preview-equipment-wide.png --width 2386 --height 1080 --section equipment
```

The requested dimensions are the client area; output includes the native window frame. `--scale 2` exercises doubled control bounds at a 96-DPI baseline. It is a layout simulation, not verification on a real 200% DPI monitor. Visual inspection of the resulting PNGs is still required.

Talent screenshots also save a sibling `-tree.png` artifact rendered directly from the native canvas, without the application shell. Equipment screenshots save an equivalent `-sheet.png` artifact.

The harness checks major control bounds, character filtering and navigation, app and tray icon presence, and conditional setup actions appearing and disappearing as health changes. It also checks completed quest title search, the inventory-to-JSON switch, bag slot preservation and search, and partial inventory uncertainty. Talent checks verify a full canvas without selectors, toolbar buttons or a details sidebar, viewport utilization and automatic resize fitting, multiple native trees, a single native tree containing three groups, preserved native coordinates, unknown/shared/unassigned membership, the active option on a choice node, unapplied-change warnings in tooltips, hidden nodes, and complete-list fallback for missing or overlapping visible positions. It uses reflection to keep preview-only fixtures out of the production app and deliberately fails if relevant internal controls change. `--section` chooses the final screenshot tab and accepts `character`, `quests`, `talents`, `inventory`, or `equipment`.

The displayed talent fixture contains a sanitized native Priest tree with three groups and 54 recorded nodes. The character identity is fictional; the embedded talent fixture retains its recorded coordinates and ranks. It includes a duplicate Holy Specialization far outside the ordinary rows, so screenshots exercise that real layout pathology while keeping all 54 nodes available. A separate illustrative 47-node fixture exercises general layout regressions. Talent and item tiles use locally drawn text placeholders; empty equipment slots show slot labels. The fixtures require no game artwork, network connection, or access to the game.

Equipment previews show a fictional level-60 Priest with 20 physical slots, 18 named items and two explicitly empty slots. Item identities and qualities use public game metadata; durability, item bonuses and character stats are synthetic. Checks cover the full paper-doll viewport and resizing, familiar slot positions, recorded hover details, quality borders, named-slot authority, partial-export uncertainty, conflicting and unfamiliar records shown as additional items, older text slot names, numeric stats, and keyboard selection.

`--no-game` renders the initial folder-selection screen and checks that it offers a single highlighted action without a redundant issue bar.
