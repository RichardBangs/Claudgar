# Isolated desktop preview

This small Windows-only harness renders the current application with a fictional character and embedded layout fixtures. It moves the populated application shell into a plain, nonactivating offscreen preview host and uses `DrawToBitmap`. The production main window is never shown, and the startup, installer, game discovery, and MCP service paths are never called. The coordinator constructor only loads settings and embedded resources. A constructor-created tray icon is immediately hidden and explicitly disposed. Native WinForms rendering may require running outside a restricted desktop sandbox.

From the repository root:

```powershell
dotnet run --project tools/Claudgar.Preview -- .tmp/preview-home-1440.png --width 1440 --height 960
dotnet run --project tools/Claudgar.Preview -- .tmp/preview-home-1120.png --width 1120 --height 780
dotnet run --project tools/Claudgar.Preview -- .tmp/preview-home-scale2.png --width 1120 --height 750 --scale 2
dotnet run --project tools/Claudgar.Preview -- .tmp/preview-no-game.png --width 1120 --height 780 --no-game
dotnet run --project tools/Claudgar.Preview -- .tmp/preview-character-data.png --section character
dotnet run --project tools/Claudgar.Preview -- .tmp/preview-inventory.png --section inventory
dotnet run --project tools/Claudgar.Preview -- .tmp/preview-talents.png --section talents
dotnet run --project tools/Claudgar.Preview -- .tmp/preview-talents-wide.png --width 2386 --height 1080 --section talents
dotnet run --project tools/Claudgar.Preview -- .tmp/preview-equipment.png --section equipment
dotnet run --project tools/Claudgar.Preview -- .tmp/preview-equipment-wide.png --width 2386 --height 1080 --section equipment
```

The requested dimensions are the client area; output includes the native window frame. Visual inspection of the resulting PNGs is still required.

`--scale 2` exercises explicit `Control.Scale` geometry at a fixed native 96-DPI baseline. The custom controls track that explicit scale independently of native monitor DPI, and the same strict action-bounds and data-view checks run before capture. This does not reproduce a 200% DPI monitor: native font rendering, fixed native row heights, and Windows DPI-change messages remain manual acceptance checks. No private DPI overrides are used. The native offscreen window may clamp the requested doubled height to the desktop's maximum window size; the printed output dimensions are authoritative.

Talent screenshots also save a sibling `-tree.png` artifact rendered directly from the native canvas, without the application shell. Equipment screenshots save an equivalent `-sheet.png` artifact.

The default capture shows the assistant home. Before capturing, the harness verifies that the old instruction banner, startup checkbox, detailed export footer, and data tabs stay out of the main view. It checks the three requested question suggestions, both assistant choices, the character-details action, and the settings menu containing startup and help destinations. It never activates an assistant launch or external help link. Home/details navigation must retain the selected character and last data section.

The harness also checks major control bounds, character navigation, app and tray icon presence, and conditional setup actions appearing and disappearing as health changes. Data checks reveal the hidden views first, then verify completed quest title search, the inventory-to-JSON switch, bag slot preservation and search, and partial inventory uncertainty. Talent checks verify a full canvas without selectors, toolbar buttons or a details sidebar, viewport utilization and automatic resize fitting, multiple native trees, a single native tree containing three groups, preserved native coordinates, unknown/shared/unassigned membership, the active option on a choice node, unapplied-change warnings in tooltips, hidden nodes, and complete-list fallback for missing or overlapping visible positions. It uses reflection to keep preview-only fixtures out of the production app and deliberately fails if relevant internal controls change. `--section` chooses the final screenshot and accepts `home` (the default), `character`, `quests`, `talents`, `inventory`, or `equipment`.

The displayed talent fixture contains a sanitized native Priest tree with three groups and 54 recorded nodes. The character identity is fictional; the embedded talent fixture retains its recorded coordinates and ranks. It includes a duplicate Holy Specialization far outside the ordinary rows, so screenshots exercise that real layout pathology while keeping all 54 nodes available. A separate illustrative 47-node fixture exercises general layout regressions. Talent and item tiles use locally drawn text placeholders; empty equipment slots show slot labels. The fixtures require no game artwork, network connection, or access to the game.

Equipment previews show a fictional level-60 Priest with 20 physical slots, 18 named items and two explicitly empty slots. Item identities and qualities use public game metadata; durability, item bonuses and character stats are synthetic. Checks cover the full paper-doll viewport and resizing, familiar slot positions, recorded hover details, quality borders, named-slot authority, partial-export uncertainty, conflicting and unfamiliar records shown as additional items, older text slot names, numeric stats, and keyboard selection.

`--no-game` renders the initial folder-selection screen and checks that it offers a single highlighted action without a redundant issue bar.
