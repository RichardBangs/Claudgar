# Claudgar
WoW Forever Addon for syncing your game state to a chatbot, for more accurate and detailed answers.

Claudgar exports WoW Forever Beta character data into a portable Windows app. Browse character details, quests, talents, carried inventory, and equipment, or ask local Codex about the saved data.

## Run the first version

Run `dist/win-x64/Claudgar.exe`. The published executable includes its dependencies; no .NET installation, web server, administrator access, or separate Claudgar installer is required. Windows 10/11 x64 is the initial target.

On first launch, Claudgar finds your Forever Beta installation, installs its addon and user Codex skill, and registers the local Codex connection. It checks the default `C:\Program Files (x86)\World of Warcraft\_classic_beta_` installation and Battle.net locations. When detection is missing or ambiguous, the welcome screen highlights **CHOOSE GAME FOLDER**. Existing unrelated or locally modified files are preserved and setup conflicts are shown in **Setup & status**.

Enter the game with the addon enabled, then `/reload` or log out to save an export. Claudgar checks saved-file timestamps every three seconds and when its window gains focus, and reloads changed data automatically. Select a character and browse each section. Expand the navigation tree or double-click a collection/record; search collections, page through results, and view or copy fields as JSON. Coverage and collection times appear with each section. Folder selection and repair actions appear only when setup needs attention.

The addon collects while your character is in the world and retains that snapshot during loading screens and logout. This avoids overwriting your items, completed quests, talents, money, and XP with the empty/default values game APIs can return during shutdown. After updating the addon, enter the world, wait a few seconds, and `/reload` or log out to replace an earlier incomplete export.

Keep Claudgar running when asking Codex about your character. Restart Codex after setup if the connection or skill is not visible. Minimising or closing the window hides it in the tray; double-click the pixel portrait to reopen it, or use **Exit Claudgar** to stop the app. Open it again after a computer restart.

The addon collects all five sections in the first version. Bank, mail, auctions, other WoW clients, and gameplay automation are outside its scope. ChatGPT/Claude connections will follow later. Character data requested through Codex is processed by the model provider.

## Shared data and architecture

- [Shared data prototype and contract](docs/DATA-CONTRACT.md), [JSON schema](schemas/export.schema.json), and [synthetic Lua export](tests/fixtures/Claudgar.lua).
- [Design](DESIGN.md), [API sources](docs/API-SOURCES.md), and [Forever API decisions](docs/FOREVER-API.md).
- [First manual test guide](docs/FIRST-TEST.md).

The addon saves account-wide snapshots; the app safely parses Lua tables without executing them. Account/installation/character identities remain separate, incomplete files retain labeled older cached data, and each query rereads saved exports. The loopback-only MCP service exposes `list_characters`, `get_character`, `get_quests`, `get_talents`, `get_inventory`, and `get_equipment`.

## Build from source

Building requires the .NET 10 SDK on the developer's computer. End users only need the published executable.

```powershell
./build.ps1
# Optional native Windows ARM64 package:
./build.ps1 -Runtime win-arm64
```

The bundled addon and skill are embedded at build time. Build output and downloaded build dependencies are ignored by Git.

A package-free regression suite is supplied for the Lua parser, schema, safe reading, freshness, and account isolation:

```powershell
dotnet run --project tests/Claudgar.Core.Tests
```

Addon regression checks load the actual Lua sources against simulated game APIs, including loading screens, logout teardown, late-loading data, and genuine empty/zero values. Run them with Lua 5.1 or with Python and Lupa:

```powershell
lua tests/addon/run.lua
python tests/addon/run.py
```

The first in-game export exposed a logout capture bug, now covered by regression tests. Simulated APIs and successful builds do not replace checking a fresh export in the Forever client.
