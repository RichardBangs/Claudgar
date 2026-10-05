# First manual test

The application and addon have not been run during development at the user's
request. The C# build and portable publish verify compilation and packaging only.
This guide is for the user's first runtime test; it has not already been carried out.

## Setup

1. Start `dist/win-x64/Claudgar.exe` on Windows 10/11 x64. No separate .NET
   runtime installation is required.
2. Open **Setup & status**. If no Forever Beta installation is detected, use
   **Choose game folder** and select the World of Warcraft install root or its
   current `_classic_beta_` folder. That shared folder name does not establish
   Forever support; Claudgar additionally checks the active product/version.
3. Check that addon, skill, and Codex connection setup succeeded. Existing
   conflicting user-owned files/configuration are preserved and explained here.
   The app does not request administrator privileges.
4. Start Forever Beta and verify Claudgar appears and is enabled in the AddOns
   list. For a changed beta interface number, the game's out-of-date addon toggle
   may be needed until the TOC is updated; the runtime guard still requires Forever.
5. Enter the world. `/claudgar status` reports collector state. `/claudgar snapshot`
   requests another snapshot. `/reload` or logout is needed to write it to disk.

## Browse every section

1. In the app, click **Refresh data** and select your character. Verify its name,
   realm, level, class, faction, and location in **Character**.
2. In **Quests**, double-click **Active** and a quest. Compare its objective text
   and progress to the quest log. Browse **Completed** IDs separately.
3. In **Talents**, browse trees, nodes, entries, and currencies. Compare active
   ranks and available/spent points. Unapplied UI changes should be flagged.
4. In **Inventory**, browse bags and their items. Compare slot locations, names,
   quantities, and full item links; browse the carried keyring if it is exposed.
5. In **Equipment**, compare slots, empty slots, item variants, stats and
   durability. Forever ranged and ammo slots are included when exposed.
6. Expand the navigation tree, double-click rows to drill into details, use
   **Back**, search a selected collection, and page large collections with
   **Previous/Next**. **JSON details** and **Copy JSON** show all fields of the
   selected value. **Save character JSON** exports the full selected snapshot.
7. Check coverage and collection time in each tab. Empty placeholders in an
   unavailable/failed section do not mean the character has no data.

## Codex and saved freshness

1. Keep Claudgar running. Restart Codex once after setup if necessary.
2. Ask: “Use Claudgar to list my WoW Forever characters.” If several are saved,
   select one by its name and realm; same-name characters also have different
   opaque account/character IDs.
3. Ask about active quests, talent points, bag contents, and equipment. Check
   that the answer describes snapshot age and any partial/unavailable coverage.
4. Change a bag item or quest objective in-game, `/reload`, then ask for fresh
   data in the same conversation. It should call the tools again and report the
   changed saved data. Clicking Refresh in the app cannot force a running game
   to write SavedVariables.
5. Close the app window; it should remain in the tray and answer tool requests.
   Use the tray's **Exit Claudgar** to stop it, then reopen the executable.
6. Click **Install / repair setup** twice. Setup should be repeatable and should
   preserve unrelated addon files, skills, and Codex configuration.

## Information to report if something fails

Send the exact beta version/build, the section coverage/warnings visible in the
app, `/claudgar status` output, any game Lua error, and the app's setup message.
The relevant export is the account-wide
`_classic_beta_/WTF/Account/<account>/SavedVariables/Claudgar.lua`.
If sharing an export or JSON, it contains your character data; remove anything
you do not want to share. Credentials are not collected.

The source includes a synthetic fixture and package-free regression checks for
reader/contract isolation and malformed files. They can be run separately with
`dotnet run --project tests/Claudgar.Core.Tests` when testing is desired. They do
not exercise the game API or establish in-game compatibility.
