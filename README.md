# Claudgar
WoW Forever Addon for syncing your game state to a chatbot, for more accurate and detailed answers.

Claudgar exports WoW Forever Beta character data into a portable Windows app. Browse character details, quests, talents, carried inventory, and equipment, or ask local Codex about the saved data.

## Run the first version

Run `dist/win-x64/Claudgar.exe`. The published executable includes its dependencies; no .NET installation, web server, administrator access, or separate Claudgar installer is required. Windows 10/11 x64 is the initial target.

On first launch, Claudgar finds your Forever Beta installation, installs its addon and user Codex skill, and registers the local Codex connection. Choose the game folder when detection is missing or ambiguous. Existing unrelated or locally modified files are preserved and setup conflicts are shown in **Setup & status**.

Enter the game with the addon enabled, then `/reload` or log out to save an export. In Claudgar, select a character and browse each section. Expand the navigation tree or double-click a collection/record; search collections, page through results, and view or copy every field as JSON. **Save character JSON** saves the selected character's full validated data. Coverage and collection times appear with each section.

Keep Claudgar running when asking Codex about your character. Restart Codex after setup if the connection or skill is not visible. Closing the window hides it in the tray; **Exit Claudgar** stops the app. Open it again after a computer restart.

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

The first version was compiled and packaged, but the application, addon, and automated tests were deliberately not run at the user's request. Game API availability and end-to-end Codex behavior remain to be verified in the first manual test.
