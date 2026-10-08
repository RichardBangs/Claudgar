# Release manual acceptance

Use this guide to check a fresh export in Forever Beta. The first user test found
that collecting during logout replaced live data with default API values; the
addon now saves its latest in-world snapshot. Automated reader and simulated Lua
regression tests cover this bug, while game compatibility requires this manual check.

Run the checks against the same packaged executable that is intended for the
release. Record the app version, beta build, client versions, and each outcome.
Claude, phone, clean-account, upgrade, and in-game acceptance remain manual
release gates until their results are recorded. This guide does not imply they
have passed. Every real World of Warcraft launch, interaction, and reload below
is performed by the user; development checks use simulated data only.

## Setup

1. Start `dist/win-x64/Claudgar.exe` on Windows 10/11 x64. No separate .NET
   runtime installation is required.
2. If no Forever Beta installation is detected, the welcome screen highlights
   **CHOOSE GAME FOLDER**. Select the World of Warcraft install root or its
   current `_classic_beta_` folder. That shared folder name does not establish
   Forever support; Claudgar additionally checks the active product/version.
3. Check that addon, skill, and ChatGPT connection setup succeeded. Existing
   conflicting user-owned files/configuration are preserved and explained here.
   The app does not request administrator privileges. Open the settings gear
   and test both download links, **Ask from your phone**, and **About Claudgar**,
   including the license and issue link, before the first export and after
   selecting a saved character. The normal character screen opens with the
   assistant choices; verify **View character data** and **Back** preserve the
   selected character and that suggested questions copy without sending.
4. Start Forever Beta and verify Claudgar appears and is enabled in the AddOns
   list. For a changed beta interface number, the game's out-of-date addon toggle
   may be needed until the TOC is updated; the runtime guard still requires Forever.
5. Enter the world. `/claudgar status` reports collector state. `/claudgar snapshot`
   requests another snapshot. `/reload` or logout is needed to write it to disk.
   After updating, wait a few seconds in the world before saving. Compare both
   a `/reload` export and a logout export: completed quests, equipment, inventory,
   talents, money, and XP should survive both, with their in-world collection times.
6. Check that Claudgar's pixelated face appears beside the minimap, matching the
   desktop and tray portrait. Left-click it while out of combat. The UI
   should reload and the desktop should automatically receive a fresh export.
   Left-drag or right-drag the button around the minimap. Releasing a drag should
   not reload the UI; a separate left-click should still refresh and reload.
   Check the hover text and that the position persists after reload.
   Combat clicks should ask you to click again afterward and never reload later
   automatically.

## Browse every section

1. Focus the app, wait for its automatic update, and select your character. Verify its name,
   realm, level, class, faction, and location in **Character**.
2. In **Quests**, double-click **Active** and a quest. Compare its objective text
   and progress to the quest log. Browse **Completed** IDs and any saved names;
   verify that title search works and unknown names remain labeled as missing.
3. In **Talents**, compare the tree layout, active ranks, groups, and available/spent
   points. Check talent hover details and automatic fit when resizing the
   window. Inspect any stale/incomplete notice or saved-layout fallback; a
   missing position should not hide a saved talent. Unapplied changes should be
   identified from the exported metadata.
4. In **Inventory**, compare the bag layout and select item stacks. Try item search
   and **JSON tree**. Compare slot locations, names,
   quantities, and full item links; browse the carried keyring if it is exposed.
5. In **Equipment**, compare slots, empty slots, item variants, stats and
   durability. Forever ranged and ammo slots are included when exposed.
6. Expand the navigation tree, double-click rows to drill into details, use
   **Back**, search a selected collection, and page large collections with
   **Previous/Next**. **JSON details** and **Copy JSON** show all fields of the
   selected value.
7. Check coverage and collection time in each tab. Empty placeholders in an
   unavailable/failed section do not mean the character has no data.

## ChatGPT and saved freshness

1. Keep Claudgar running. Restart the
   [ChatGPT desktop app](https://learn.chatgpt.com/docs/quickstart) after setup.
   Use its Codex mode with local MCP tools.
2. Ask: “Use Claudgar to list my WoW Forever characters.” If several are saved,
   select one by its name and realm; same-name characters also have different
   opaque account/character IDs.
3. Ask about active quests, talent points, bag contents, and equipment. Check
   that the answer describes snapshot age and any partial/unavailable coverage.
4. Change a bag item or quest objective in-game, `/reload`, then ask for fresh
   data in the same conversation. It should call the tools again and report the
   changed saved data. App updates cannot force a running game to write SavedVariables.
5. Minimise the app window; it should remain in the tray and answer tool requests.
   Double-click its portrait icon to restore the window and check saved-file changes.
   Use the tray's **Exit Claudgar** to stop it, then reopen the executable.
6. Confirm a healthy setup shows no folder or repair actions. If a real setup issue
   is reported, use its **Repair setup** action and check that it disappears after
   success. Repair must preserve unrelated addon files, skills, and client
   configuration. Local service readiness or the latest client-request timestamp
   alone does not count as a successful character query.

## Claude Desktop

1. Install and open [Claude Desktop](https://claude.com/download) once, then
   reopen Claudgar or use **Repair setup**. Verify **Setup & status** reports
   Claude Desktop (helper and each configuration file) separately from ChatGPT
   and Claude Code. With the Microsoft Store version, the line
   **Claude Desktop (Microsoft Store)** must appear.
2. Fully quit Claude (right-click its tray icon > Quit), then reopen it.
   **Settings > Developer** must list `claudgar` as running. Use a chat supporting local MCP tools,
   following the [local MCP guide](https://modelcontextprotocol.io/docs/develop/connect-local-servers)
   if the tools are not visible. Confirm setup requires no separate Node.js,
   Python, or .NET installation.
3. Ask Claude to list characters, choose the intended character, and query all
   five sections. Confirm character IDs, save age, coverage, tool errors, and
   character isolation agree with ChatGPT and the desktop browser.
4. The user changes saved game state and reloads, then asks Claude again in the
   same conversation. Confirm fresh data is fetched. Keep this Claude acceptance
   separate from the ChatGPT result.
5. With Claude still open, exit and reopen Claudgar. An in-flight failed request
   should be explained, and a later character request should reconnect. Repeat
   after an app upgrade. If a helper update is staged because Claude holds it
   open, quit Claude, run **Repair setup**, reopen Claude, and query again.
6. On a clean account without Claude, verify ChatGPT still works and the status
   explains how to add Claude later. Check unrelated Claude servers/settings
   remain present after setup and repeat repair. Malformed/conflicting config
   checks use disposable fixture configuration, preserving the user's files.

## Claude Code

1. With Claude Code installed and used at least once, reopen Claudgar or use
   **Repair setup**. **Setup & status** must show **Claude Code: ready** with its
   connection and skill. Without Claude Code it must say "not installed" and
   leave the other clients' status unchanged.
2. In a terminal run `claude mcp list`; `claudgar` must appear as an HTTP server
   at `http://127.0.0.1:<port>/mcp` and report connected while Claudgar runs.
3. Start a new `claude` session and ask: “Use Claudgar to list my WoW Forever
   characters.” Then query each section for one character and confirm save age
   and coverage are reported.
4. Confirm unrelated `~/.claude.json` settings, other MCP servers, and project
   history remain after setup and repeat repair, and that
   `~/.claude/skills/claudgar/SKILL.md` exists.

## Phone access

**ChatGPT:** complete the [README phone setup](../README.md#ask-from-your-phone), based on the
[official remote connections guide](https://learn.chatgpt.com/docs/remote-connections).

**Claude:** after the Claude Code check passes, run `claude remote-control` on
the PC (or enable Remote Control in Claude Desktop's settings), open the Claude
app on the phone, select that session, and ask a Claudgar question. This needs
a paid Claude plan.

Ask from the paired phone and verify the selected character's saved data and
freshness. Confirm the PC stays awake, online, and running both apps. Record
unavailable account/workspace/plan features as an unmet acceptance check.

## Launch on startup and tray behavior

1. On a clean Windows account, check **Launch on startup** is enabled by default
   in the settings menu. Its state and any registration warning must remain reachable
   before choosing a game folder. A settings migration with no preference also
   defaults to enabled.
2. Sign out and back into Windows. Claudgar should run quietly in the tray, with
   its local service available and no second window. Open it from the tray and
   make a character query. Starting the executable while it is already running
   must keep one app/service instance.
3. Uncheck startup, exit/reopen Claudgar, run repair, then sign out and back in.
   Confirm it remains disabled and does not start automatically. Re-enable it
   and repeat the sign-in check. Only Claudgar's current-user entry should change.
4. On a disposable portable copy in a path containing spaces, enable startup,
   move the copy, and open it once from the new location. Confirm the quoted
   startup entry points to that executable. Restore the desired permanent path
   and preference afterward.
5. Verify normal manual launch shows the window, minimise/close keeps the
   service in the tray, **Exit Claudgar** stops it, and reopening restores normal
   operation. Check the assistant choices and questions at the minimum window size and with enlarged
   Windows display scaling.

## App and addon updates

Use disposable app copies under `.tmp` and a prepared update candidate for
replacement/failure checks. The published-release check requires an available
stable GitHub release; record it as pending until that release exists.

1. Check that startup prepares a newer valid release without blocking normal
   use. **Update available** appears only after verification. Equal/older versions,
   prereleases, offline checks, missing assets, and failed downloads leave the
   current app usable and explain relevant errors in status.
2. Click **Update available** and decline the restart. Minimise/close to the tray;
   the version must stay unchanged. Exit Claudgar and launch again; the staged
   update should install at the same executable path and start one service.
3. Repeat with **Update available** and confirm installation now. Verify a real exit
   and restart, matching displayed version, preserved settings and client
   configuration, and successful queries with Claude left open.
4. Repeat upgrades with startup enabled and disabled. Both preferences must
   survive. A sign-in launch remains quiet in the tray after replacement; a
   manual launch shows the window.
5. Confirm each selected game installation's addon ownership metadata records
   `successfulAppVersion` equal to the app version only after the complete addon
   copy succeeds. Check first install, migration without a marker, identical
   addon bytes in a newer app, a second game installation, and a manual upgrade.
   Reopening the same app version should not routinely rewrite addon files.
6. At the same version, remove a file only from a disposable addon fixture and
   run repair; it should be restored. Customized files and unowned addon folders
   must remain preserved with an explanation. Failed/interrupted copies must
   retain the previous marker and retry successfully after the conflict clears.
7. In disposable app copies, verify a corrupt candidate, file lock, failed
   startup, and interrupted replacement either leave the old executable usable
   or restore it, with no restart loop. An app rollback must also reconcile the
   addon against the restored version's payload. SavedVariables and unrelated
   addons must remain unchanged. The isolated process suite covers these cases
   without touching a live installation.
8. After a real addon update, the user reloads the game UI or logs out/back in
   and checks the portrait and all five exports again. Claudgar must never
   initiate that reload itself.

## Export recovery and clean Windows account

Verify no-export, malformed first export, stale cached data, partial account
failures, and recovery using disposable saved-export fixtures. The app should
show the returned issue and the save/retry action, retain clearly labeled older
data, and keep healthy accounts available. A new valid save should recover
without restarting the app. Automated core/presentation checks cover these
states; do not damage live SavedVariables for the test.

Run the packaged app on a clean Windows account without a separate .NET runtime
and repeat folder setup, chat setup, tray exit/reopen, repair, startup preference,
download/help links, and update acceptance. Record this outcome separately from
the development-machine result.

## Information to report if something fails

Send the app and client versions, exact beta version/build, the section coverage/warnings visible in the
app, `/claudgar status` output, any game Lua error, and the app's setup message.
The relevant export is the account-wide
`_classic_beta_/WTF/Account/<account>/SavedVariables/Claudgar.lua`.
If sharing an export or JSON, it contains your character data; remove anything
you do not want to share. Credentials are not collected.

The source includes a synthetic fixture and package-free regression checks for
reader/contract isolation, malformed files, setup, startup, addon versions,
native transport, and app-update recovery. See the
[development check commands](../README.md#development-checks). They do not
exercise the real game API or establish in-game/client compatibility. Report
issues through [GitHub Issues](https://github.com/RichardBangs/Claudgar/issues).
