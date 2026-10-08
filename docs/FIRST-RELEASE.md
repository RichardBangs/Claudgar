# Claudgar first release plan

The first release should let a Windows user download Claudgar, connect ChatGPT or Claude Desktop, ask about their saved WoW Forever character, and receive future app updates automatically. Enable launching Claudgar at Windows sign-in by default, with an easy way to turn it off. Keep the portable Windows x64 app and the existing five data sections.

The implementation is now available in `dist\win-x64\Claudgar.exe` at version `0.1.0`. Automated checks use synthetic data and temporary installations. Real client, phone, in-game, clean-account, and published-release upgrade acceptance remain open below. Interpret "next boot" as the next Claudgar launch; installing immediately means confirming a Claudgar restart.

## Implemented foundation

- The app already browses saved characters, quests, talents, inventory, and equipment, and runs in the system tray.
- ChatGPT's local-tools/Codex integration uses the local configuration and a read-only loopback MCP service. Claude Desktop uses a separate bundled native stdio helper with the same tools. Setup preserves user-owned changes.
- Reader tests already cover account isolation, invalid exports, stale cached data, and recovery. App presentation tests and simulated addon tests also exist.
- `Version.props` supplies version `0.1.0` to the executable, assemblies, manifest, MCP metadata, displayed version, and packaged addon TOC. Tagged Windows builds prepare draft GitHub releases with the app, checksum, license, and generated notes.
- Startup registration, staged GitHub updates with rollback, per-installation addon version markers, assistant launch actions, phone help, and the minimap portrait are implemented. Documentation describes the supported modes and remaining manual checks.

## Delivery order

| Step | Work | Completion condition |
| --- | --- | --- |
| 1 | Define release version and assets | App, MCP metadata, Windows manifest, Git tag, and download assets use matching version information. |
| 2 | Add assistant choices, startup option, phone instructions, and minimap portrait | Users can open either assistant, reach downloads and startup preferences from settings, find phone setup instructions, and recognize Claudgar's face beside the minimap. |
| 3 | Add Claude Desktop | Safe configuration setup and the bundled connection helper work; the user completes a separate Claude test. |
| 4 | Add staged updates and versioned addon installation | Startup download, next-launch replacement, install-now restart, addon copying when the app version changes, and recovery work with both chat apps. |
| 5 | Verify the packaged app and prepare the release | Regression checks and manual acceptance pass, and the release download and instructions are ready. |

Steps 2 and 3 can run in parallel after step 1. Resolve Claude's connection process before finishing the updater so it cannot prevent executable replacement.

## Main window guidance

- [x] Make **Open ChatGPT** and **Open Claude** the main actions after selecting a saved character. Keep the old section tabs behind **View character data**.
- [x] Keep **Download ChatGPT** and **Download Claude** in settings, opening the official pages in the user's browser; the Claude link is [claude.com/download](https://claude.com/download).
- [x] Keep background-running and connection instructions available in **Setup & status**.
- [x] Offer three copyable questions about quest areas, Deadmines quests, and gear, with the selected name and realm included in the copied prompt.
- [x] Keep **Ask from your phone** and **About Claudgar** in settings before the first export and during normal use.
- [x] Keep the main view minimal, with concise save status and existing corrective actions when setup needs attention.
- [x] Keep setup status specific to each client. A running local service or a request received is not proof that a chatbot completed a character query.

Use the official [ChatGPT desktop setup page](https://learn.chatgpt.com/docs/quickstart) for the ChatGPT download action. The release acceptance check must confirm that it offers the Windows download and explains the mode used by Claudgar.

## Launch on startup

- [x] Keep **Launch on startup** in the settings menu, reachable during initial setup and normal use.
- [x] Enable it by default on first use, including for existing settings that have no saved startup preference. Register Claudgar to launch when the current user signs into Windows.
- [x] Save checkbox changes immediately. Once the user disables startup, preserve that choice across app restarts, setup repair, and updates.
- [x] Start in the system tray when launched by Windows startup, keeping the local connection available. A normal manual launch should show the main window.
- [x] Manage only Claudgar's startup entry for the current Windows user, without administrator privileges. If registration fails, show a clear status message and keep the checkbox consistent with the effective setting.
- [x] Register the actual portable executable path, including correct handling of spaces. Refresh that path when an enabled app is manually reopened from a new location; keep it valid after an in-place app update.
- [x] Preserve single-instance behavior when startup and a manual launch overlap. Apply any staged update through the same next-launch flow and retain startup-in-tray behavior after replacement.
- [x] Verify default enablement, persistence after disabling, removal of Claudgar's entry, and paths with spaces using an isolated startup-registration target. Actual updater fixtures verify startup argument preservation.
- [ ] Confirm Windows sign-in opens the packaged app in the tray, including after an update with startup enabled or disabled.

## Addon minimap portrait

- [x] Replace the minimap button's book icon with Claudgar's existing pixelated face from `src/Claudgar.App/Resources/Artwork/claudgar-portrait.png`, matching the desktop and tray branding.
- [x] Package a game-compatible texture inside the addon. Preserve crisp pixel edges, transparency, and a readable face at the button's small displayed size.
- [x] Reference the bundled portrait in `MinimapButton.lua`. Include it in the app's embedded addon payload and owned-file installation so fresh installs and app updates deliver the texture automatically.
- [x] Preserve the existing border, hover highlight, tooltip, drag positioning, saved position, player-click refresh/reload, and combat guards. This change affects the button's artwork only.
- [x] Update README and the manual test guide to describe the Claudgar face button instead of the book icon.
- [x] Verify the packaged asset and inspect a small-size preview without running the game. Existing simulated minimap interaction checks pass.
- [ ] User acceptance: confirm the portrait, hover, click, and drag behavior in Forever.

## Addon updates with the app

- [x] Record the last app version that successfully installed the bundled addon, separately for each game installation. Store the marker with Claudgar's local addon installation metadata, rather than the game's character exports.
- [x] On app launch, compare that marker with the running app version. If it is missing or different, run the owned-file installer to copy the bundled addon, including Lua, TOC, and portrait assets, into that installation's Claudgar addon folder.
- [x] Record the new app version only after the entire addon installation succeeds, including when the new version bundles identical addon bytes. Track addon success independently of chatbot/skill setup. If copying or saving the marker fails, show the issue and retry on a later launch or setup repair; do not mark an incomplete update as current.
- [x] Skip routine addon copying when the recorded version matches and no interrupted installation is pending. Keep setup health checks and explicit repair able to restore missing files at the same app version while preserving user-modified files and explaining conflicts.
- [x] Preserve saved character data and unrelated addons. Continue using the existing ownership checks and interrupted-install recovery when updating Claudgar's files. A missing version marker must not authorize overwriting an unowned folder or user-edited files.
- [x] Apply this rule after both automatic and manually downloaded app upgrades. A rollback also changes the app version and must reconcile the bundled addon with that version.
- [x] Explain after an addon update that the user should reload the game UI or log out and back in to load the updated addon. Claudgar must not initiate an in-game reload.
- [x] Verify first install, migration without a version marker, repeated launch at the same version, upgrade, rollback, a new game folder, and failed/interrupted copies. Confirm that each installation's marker advances independently and only on success.

## Claude Desktop support

Claude's [local MCP setup guide](https://modelcontextprotocol.io/docs/2026-07-28/develop/connect-local-servers) documents command/argument registration in `%APPDATA%\Claude\claude_desktop_config.json`. Target Claude Desktop chats that support local MCP, and identify that mode in setup instructions. Claude's [connector guide](https://support.claude.com/en/articles/11175166-get-started-with-custom-connectors-using-remote-mcp) explains which modes support local connections.

- [x] Add a Claude configuration installer with responsibility limited to Claudgar's entry. Preserve unrelated servers and settings; back up changes, leave malformed or conflicting configuration untouched, and make repeat setup harmless.
- [x] Provide a self-contained native stdio connection helper that forwards to the existing loopback MCP service. Embed it in the main app and install it into a dedicated app-owned folder under `%LOCALAPPDATA%\Claudgar`. Users should not need Node.js, Python, or another runtime.
- [x] Keep the same six tools, opaque character IDs, freshness information, coverage, and read-only behavior for both clients. Forward tool metadata, structured results, errors, and server instructions so Claude gets character-selection and stale-data guidance. Do not assume it loads the existing `.agents` skill.
- [x] Show Claude setup, restart, and connection errors independently. A missing Claude installation must not make a working ChatGPT setup appear broken.
- [x] Keep the main executable out of Claude's long-running connection process. The helper must reconnect after Claudgar restarts and report a failed in-flight request clearly.
- [x] If a future helper update encounters a file lock, preserve the working helper and explain that Claude must be quit and reopened to finish that connection update. Keep the pending helper compatible with the updated app.
- [x] Add meaningful checks for configuration preservation, repeated setup, all tools, missing service, and reconnecting after an app update.
- [x] Detect the Microsoft Store (MSIX) Claude Desktop, whose configuration lives under `%LOCALAPPDATA%\Packages\Claude_*\LocalCache\Roaming\Claude`, in addition to `%APPDATA%\Claude`. Register and check every detected file with its own ownership journal, and report each installation in one combined Desktop result.
- [x] Make `skill/claudgar/SKILL.md` client-neutral and the single guidance source: the MCP server instructions (forwarded by the bridge) are its body without frontmatter, and tool descriptions repeat the essential rules.
- [ ] User acceptance: install/restart Claude, list characters, query each section, verify fresh data in the same conversation, and query again after a Claudgar update.

## Claude Code support

- [x] Add a Claude Code installer that owns only `mcpServers.claudgar` (`{"type":"http","url":"http://127.0.0.1:<port>/mcp"}`) in `~/.claude.json`, with the same ownership journal, backup, conflict, malformed-file, and duplicate-key rules as Claude Desktop, a re-read immediately before replacement, and a larger size limit for project history.
- [x] Install the shared skill to `~/.claude/skills/claudgar/SKILL.md`. Skip cleanly with an informative status when neither `~/.claude.json` nor `~/.claude` exists.
- [x] Include Claude Code in setup, health checks, and the per-client status in **Setup & status**. Verify the loopback service accepts Claude Code's HTTP requests (no Origin, Host `127.0.0.1:<port>`) and still rejects browser origins.
- [ ] User acceptance: `claude mcp list` shows `claudgar`; a new session lists characters and queries each section; phone access through `claude remote-control`.

Addon/skill installation retains its small-file limits. Native helper and updater installation use bounded streaming hashes, independently of saved-export limits.

## Phone instructions

Add these instructions to README and an accessible help area in the app:

1. Set up Claudgar and confirm a character query works in ChatGPT on the PC.
2. In the desktop app, open **Settings > Connections > Control this Mac or PC**, then **Set up** or **Add**.
3. Scan its QR code with the phone and finish pairing in ChatGPT using the same account and workspace.
4. On the phone, choose the connected PC in **Codex**, or **Remote** where that label is still used, and ask a Claudgar question.
5. Keep the PC awake, online, and running ChatGPT and Claudgar. Feature availability depends on the account/workspace and rollout.

The phone uses the PC's configured local tools; it does not connect directly to Claudgar. Keep Claudgar's service bound to loopback. These steps follow OpenAI's [remote connections guide](https://learn.chatgpt.com/docs/remote-connections).

For Claude, the phone continues a Claude Code session on the PC through [Remote Control](https://code.claude.com/docs/en/remote-control) (`claude remote-control`, or Remote Control in Claude Desktop's settings). It needs Claude Code and a paid Claude plan; Claude Desktop's local MCP connection itself works on the free plan.

- [x] Add Claude Remote Control steps to README and the in-app phone help.
- [ ] User acceptance: ask from a paired phone and verify the answer contains the selected character's saved data.

## GitHub updater

Use GitHub's [release API](https://docs.github.com/en/rest/releases/releases#get-the-latest-release) and release asset digest metadata for the release lookup and download verification.

- [x] Check `RichardBangs/Claudgar` releases in the background on startup. Use published regular releases for the initial channel; ignore drafts, prereleases, equal versions, and older versions. Release assets must be readable without requiring users to supply GitHub credentials.
- [x] Download the Windows x64 release asset `Claudgar.exe` to an app-owned staging folder under `%LOCALAPPDATA%\Claudgar\updates`. Validate its version and SHA-256 before marking it ready, and validate the staged bytes again before applying them. Interrupted downloads must never become executable updates.
- [x] Show **Update available** in the header and **Install update** in settings once an update is ready. During download, show a brief preparation state. Clicking the action asks: "Claudgar <version> is ready. Would you like to install it now? Claudgar will restart." Provide **Install now** and **Later**.
- [x] Choosing **Later** leaves the update staged for the next app launch. Normal window close/minimise still keeps Claudgar in the tray; that is not a restart.
- [x] Before normal startup, apply any verified pending update with a narrowly scoped helper. Release the app mutex, wait for the old process to exit, replace the executable at its original path, and relaunch that path. The staged executable can supply the helper mode, preserving one downloadable app.
- [x] Use the same replacement path for **Install now**, with a real app exit and orderly service shutdown.
- [x] Keep a rollback copy and recover from interrupted replacement, file locks, or failed startup. Clear or quarantine a failed pending update so it cannot cause an endless restart loop.
- [x] Preserve settings, game exports, unrelated files, and chatbot configuration. After relaunch, update the addon according to its last successfully installed app version, as described above; retain owned-component handling for embedded skill assets.
- [x] Offline checks, GitHub failures/rate limits, missing assets, and download failures must leave the current app usable. Make any update problem discoverable in status without blocking startup.
- [x] Test version comparison, wrong/corrupt assets, interrupted downloads, parent exit, mutex handover, executable file locks, health confirmation, and rollback using disposable app fixtures under `.tmp`.
- [ ] Confirm the full packaged app's next-launch/install-now flow, tray shutdown, real GitHub download, and updates while Claude is open.

## Release packaging and acceptance

- [x] Establish one version source for the assembly, manifest, MCP metadata, displayed version, release tag, and updater comparison. `v0.1.0` is the proposed initial tag; confirm the final number when preparing the release.
- [x] Build the Windows x64 `Claudgar.exe` and checksum, embed the MIT license with an About link, and include the license with the distribution.
- [ ] Publish accepted GitHub release assets and review the generated release notes.
- [x] Add a Windows build/release workflow that runs the checks and calls `build.ps1`. Always publish the runnable app into `dist\win-x64`; keep temporary packaging and upgrade tests under `.tmp`. Prepare a draft release from a version tag before making it public.
- [x] Surface returned export issue messages in the app. An invalid first export must explain the failure and recovery, rather than showing only a generic "No export yet" message. Check no export, malformed export, stale cache, partial accounts, and successful recovery.
- [x] Update README, DESIGN, FIRST-TEST, setup text, and package description for ChatGPT and Claude. Remove obsolete first-test restrictions now that the first manual test has occurred. Add download, privacy, and GitHub Issues links.
- [x] Run the core and app regression programs, plus the simulated Lua addon suite. Add the targeted updater, Claude configuration, and transport checks described above.
- [x] Inspect fictional first-run, minimum-size, large-window, and doubled-layout previews. Validate the actual bundle includes its required runtimes.
- [ ] Check the packaged app on a clean Windows account without a separately installed .NET runtime, including folder setup, tray exit/reopen, repair, links, and real display scaling.
- [ ] Complete real ChatGPT, separate user-tested Claude, phone, and upgrade acceptance. The existing [manual test guide](FIRST-TEST.md) covers the five sections and saved-data refresh. The user performs every in-game check; development and automated tests must not launch or interact with World of Warcraft.

The release is ready when all required checks above pass against the same packaged executable, both desktop clients can retrieve the expected saved data, and a staged upgrade succeeds or recovers without losing the working installation.

## Validation recorded during implementation

- Release size optimization reduced the executable from 106,925,722 to 75,530,921 bytes (29.4%). The trimmed native Claude helper is 12,563,255 bytes, down from 38,393,908. The app remains compressed, self-contained, and built in Release mode; package inspection now checks its Release configuration explicitly. Main Windows Forms trimming stays disabled.
- After adding Claude Code and Microsoft Store Claude Desktop support: 44 Core regression groups, all App test groups (including a live loopback HTTP check of Claude Code-style requests), both native bridge groups, three updater process scenarios, and 50 simulated addon checks passed.
- 38 Core regression groups passed, including startup registration, addon version migration/upgrade/rollback, safe Claude configuration/helper installation, verified update staging, recovery, and file locks.
- App presentation tests and read-only inspection of the published single-file bundle passed. Package inspection verifies versions, included runtimes, embedded helper, addon portrait and stamped TOC, skill, MIT license, and checksum.
- The published native Claude helper passed both stdio protocol and reconnect groups against a disposable loopback service.
- The updater passed three actual Windows process scenarios using inert fixture executables under `.tmp`, including successful next-launch/install-now replacement and rollback after failed startup.
- The simulated addon suite passed 50 checks, including the minimap portrait and existing interaction/combat behavior.
- Offscreen UI previews use fictional data and never initialize production setup or the service. They check first-run guidance, minimum-size layout, and a 2x layout simulation; real display scaling remains manual.

The local build is self-contained. This machine could not retrieve NuGet vulnerability metadata, so local build logs include `NU1900` warnings; the online CI run must complete its restore/audit before release. No public release or game interaction occurred during implementation.

## Remaining release gates

1. Complete the checks in [FIRST-TEST.md](FIRST-TEST.md), including separate Claude and phone testing, the in-game face button, Windows sign-in, clean-account setup, and an app/addon upgrade while Claude is open.
2. Run the GitHub workflow and confirm it passes on the accepted commit. Merge that commit to the default branch before creating a matching version tag.
3. Review the draft download, checksum, license, and release notes. Test the real published-release download/update path between two versions before calling automatic updates accepted.
4. Publish the accepted draft release. This checklist does not claim manual acceptance or public publication.
