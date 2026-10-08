# Claudgar Design

Claudgar gives ChatGPT, Claude Desktop, and Claude Code saved character context from WoW Forever Beta only. Adapt to changing game APIs without pinning an exact patch/build. The first version includes all five data sections, a complete local data browser, Windows startup registration, and staged app/addon updates.

## User flow

1. Install the [ChatGPT desktop app](https://learn.chatgpt.com/docs/quickstart), [Claude Desktop](https://claude.com/download), or Claude Code and sign in.
2. Run Claudgar; it installs the addon and ChatGPT skill, registers the ChatGPT local MCP connection, configures each detected Claude Desktop installation (classic and Microsoft Store), and configures Claude Code (connection and skill) when it has been used on the account.
3. Enable the addon in Forever, enter the world, and use the Claudgar face beside the minimap or `/reload`/logout to save an export. Ask in ChatGPT's Codex mode with local tools, a Claude Desktop chat with local MCP support, or a Claude Code session.

Detect game/export paths; prompt only when missing or ambiguous. Bundle dependencies, preserve configuration, and update only Claudgar-owned files.

Keep Claudgar running in the background. The main window focuses on opening ChatGPT or Claude and copying suggested questions for the selected character. **View character data** reveals the existing section tabs and full export details; **Back** returns to the assistant choices. The settings gear holds startup preferences, separate client connection details, downloads, phone help, and About. Launch on startup defaults to enabled and starts quietly in the tray; its checkbox persists an explicit disabled choice. Normal launches show the window, and the tray provides reopen/exit actions. Keep save age and incomplete/older-data status concise in the main view, with actionable export failures in Details. Service readiness or a client request does not establish a successful character query. Restart the relevant chat app after configuration changes.

Phone access uses the connected PC's existing local tools, through ChatGPT's remote connection or Claude Code Remote Control (`claude remote-control`; paid Claude plan). Pair from the desktop, use the same account/workspace, and keep the host awake and online. Follow the [official remote connections guide](https://learn.chatgpt.com/docs/remote-connections); availability depends on account/workspace and rollout.

Ship a portable self-contained C# Windows executable with dependencies and addon/skill assets embedded. The user installs no separate runtime. Browse characters, every section and exported field; search and page collections, view details, and save character JSON. Show observation times and coverage alongside data.

## Architecture

| Component | Responsibility |
| --- | --- |
| Addon | Collect full character snapshots into SavedVariables |
| Claudgar app | Install components, safely read exports, browse data, and serve MCP tools |
| Claudgar skill | One client-neutral guide for tool use, character selection, and data age/gaps; installed for ChatGPT and Claude Code and served as MCP instructions |
| Native Claude bridge | Forward Claude Desktop's stdio MCP connection to the loopback service and reconnect after app restarts |
| Client setup | Own only Claudgar's entry in each client's configuration (Codex TOML, every Claude Desktop JSON, Claude Code's `~/.claude.json`) and report each client independently |
| Updater | Verify and stage GitHub release assets, replace the app after exit, and recover a failed replacement/startup |
| Startup registration | Reconcile only Claudgar's current-user Windows startup entry with the saved preference and current portable path |

**Queries:** ChatGPT local tools → loopback MCP → app reader → latest validated saved snapshot → answer. Claude Desktop → native stdio bridge → the same service and reader → answer. Claude Code → loopback HTTP MCP (no Origin, Host `127.0.0.1:<port>`) → the same service → answer.

Serve read-only Streamable HTTP MCP at `http://127.0.0.1:<port>/mcp`, bound only to loopback with a stable port. Tools: `list_characters`, `get_character`, `get_quests`, `get_talents`, `get_inventory`, `get_equipment`.

Install `SKILL.md` under `%USERPROFILE%\.agents\skills\claudgar`; register MCP separately in the local Codex configuration used by ChatGPT. The skill supplies instructions; MCP supplies data access. No project setup is required. Official [skill](https://learn.chatgpt.com/docs/build-skills) and [MCP](https://learn.chatgpt.com/docs/extend/mcp) guides.

Claude Desktop setup owns only the `claudgar` server entry in every detected `claude_desktop_config.json`: the classic `%APPDATA%\Claude` folder and each Microsoft Store package folder `%LOCALAPPDATA%\Packages\Claude_*\LocalCache\Roaming\Claude`. Each file keeps its own ownership journal and result, retains unrelated settings, and is backed up before changes. Malformed, conflicting, and user-edited entries are preserved. Install the embedded self-contained helper under `%LOCALAPPDATA%\Claudgar\claude-bridge`; Claude's long-running connection does not lock the main app. A locked helper replacement is staged and explained separately. Claude Code setup owns only `mcpServers.claudgar` (`{"type":"http","url":"http://127.0.0.1:<port>/mcp"}`) in `~/.claude.json` with the same journal, backup, conflict, malformed-file, and duplicate-key rules; it re-reads the file immediately before replacement because Claude Code rewrites it often, and allows a larger file. It installs the shared skill to `~/.claude/skills/claudgar` and skips cleanly when neither `~/.claude.json` nor `~/.claude` exists. `skill/claudgar/SKILL.md` is the single guidance source: it is installed as the ChatGPT and Claude Code skill, and its body (without frontmatter) is served as MCP server instructions, which the Desktop bridge forwards. Tool descriptions repeat the essential rules (call `list_characters` first, use the opaque `characterId`, report snapshot age and coverage). See the [local MCP setup guide](https://modelcontextprotocol.io/docs/develop/connect-local-servers).

Addon files retain ownership hashes and an interrupted-install journal. Each game installation's `.claudgar-owned-files.json` also records `successfulAppVersion`, which advances only after its complete addon installation succeeds. Different versions, including rollbacks, reconcile the bundled files; a matching version skips routine copies unless installation is pending. Health checks remain read-only, and explicit repair checks/restores same-version files without overwriting customizations. The app tells the user to reload/logout after updates and never initiates a game reload.

The minimap portrait is a transparent power-of-two TGA derived from the existing desktop portrait with nearest-neighbor sampling. Only its texture changed; secure player-click reload, combat guards, drag position, border, highlight, and hover behavior remain.

Check public stable `RichardBangs/Claudgar` releases on startup. Verify SHA-256, file size, and Windows version metadata before recording a ready update, and verify again before replacement. Store downloads and the update journal under `%LOCALAPPDATA%\Claudgar\updates`. **Update available** in the header or **Install update** in settings confirms install-now restart; declining keeps it ready for the next launch. A helper waits for the current process and mutex, replaces only the original app executable, preserves a rollback copy, and restores it if replacement/startup fails. Settings, exports, client configuration, and startup-in-tray behavior survive this flow. Offline and download failures do not block normal use.

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
3. Compile and package with `build.ps1`, updating only `dist/win-x64`. Embed the native connector, addon, skill, portrait, and license. Generate a checksum and use `Version.props` for app, MCP, staged addon metadata, release tag, and comparison versions. Keep temporary checks/packaging under `.tmp` and compiler caches separate.
4. Run core, presentation, simulated Lua, native connector, and isolated updater checks. The Windows GitHub workflow verifies a matching version tag and prepares a draft release with executable, checksum, and license; publishing remains a release step.
5. The user completes [manual acceptance](docs/FIRST-TEST.md): real exports, both desktop clients, same-conversation refresh, phone pairing, startup choices, and upgrades/recovery. Automated checks use fixtures and never launch or interact with World of Warcraft. Real Claude, phone, clean-account, and game acceptance must be recorded separately; implementation is not evidence that those checks passed.

Later: plugin distribution and bank/mail/auction data. Gameplay automation is outside scope. Track release readiness in [FIRST-RELEASE.md](docs/FIRST-RELEASE.md).
