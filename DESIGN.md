# Claudgar Design

Claudgar gives local Codex character context from WoW Forever Beta only. Adapt to changing game APIs without pinning an exact patch/build. The first version includes all five data sections and a complete local data browser.

## User flow

1. Install Codex and sign in.
2. Run Claudgar; it installs the addon and skill and registers the local MCP connection automatically.

Detect game/export paths; prompt only when missing or ambiguous. Bundle dependencies, preserve configuration, and update only Claudgar-owned files.

Keep Claudgar running in the background; reopen it after closing or restarting the computer. Reload or log out of the game to save updated data. Show connection/export status, including “no export yet”; suggest restarting Codex if setup is not detected.

Ship a portable self-contained C# Windows executable with dependencies and addon/skill assets embedded. The user installs no separate runtime. Browse characters, every section and exported field; search and page collections, view details, and save character JSON. Show observation times and coverage alongside data.

## Architecture

| Component | Responsibility |
| --- | --- |
| Addon | Collect full character snapshots into SavedVariables |
| Claudgar app | Install components, safely read exports, browse data, and serve MCP tools |
| Codex skill | Guide tool use, character selection, and explanations of data age/gaps |

**Queries:** Codex → localhost MCP → app reader → latest validated saved snapshot → Codex answer.

Serve read-only Streamable HTTP MCP at `http://127.0.0.1:<port>/mcp`, bound only to loopback with a stable port. Tools: `list_characters`, `get_character`, `get_quests`, `get_talents`, `get_inventory`, `get_equipment`.

Install `SKILL.md` under `%USERPROFILE%\.agents\skills\claudgar`; register MCP separately in user Codex configuration. The skill supplies instructions; MCP supplies data access. No project setup is required. Official [skill](https://learn.chatgpt.com/docs/build-skills) and [MCP](https://learn.chatgpt.com/docs/extend/mcp) guides.

## Data contract

The shared versioned prototype lives in [DATA-CONTRACT.md](docs/DATA-CONTRACT.md), with a [JSON schema](schemas/export.schema.json), representative [Lua fixture](tests/fixtures/Claudgar.lua), central Lua `Schema.lua`, and C# `ExportContract`/`SnapshotValidator`. Both components follow this boundary.

| Section | Contents |
| --- | --- |
| Character | Identity, realm, level, class, race, faction, locale, zone/map |
| Quests | Active objectives/progress; completed quest IDs where available |
| Talents | Selections, ranks, available points |
| Inventory | Carried items, variants, quantities, bags, slots |
| Equipment | Equipped slots and item details |

- Store IDs/names, schema version, and section observation times.
- Return freshness and coverage: complete, partial, unavailable, or failed. Missing data differs from empty results.
- Separate characters across realms, accounts, and installations.
- Parse and validate without executing Lua. Retry incomplete reads; report failures and label older cached data.
- Exclude credentials/local account paths from results. Returned character data is processed by the model provider.

## Build and verify

1. Define the shared data prototype and safe reader; implement identity, quests, talents, inventory, and equipment collectors using Forever client sources.
2. Add MCP, skill, portable automatic setup, and the full exported-data browser.
3. Compile and package the self-contained Windows executable. Do not run the app, addon, or tests before the user's first test.
4. The user performs the [first manual test](docs/FIRST-TEST.md): exports after reload/logout, all sections, changed data within the same Codex conversation, account isolation, invalid exports, and repeat setup. Automated regression tests are supplied for later execution.

Later: ChatGPT/Claude connections, plugin distribution, bank/mail/auction data. Gameplay automation is outside scope.
