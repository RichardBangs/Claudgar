# Forever Beta API notes

This implementation targets World of Warcraft: Forever Beta only. Sources were
reviewed on 5 October 2026. No addon was installed in a game client and no game,
mock, or automated tests were run; the user's first game run remains the first
runtime test.

The authoritative material used for collector signatures is Blizzard-authored
interface source and generated API documentation, publicly mirrored in the
[`forever` branch of Gethe/wow-ui-source](https://github.com/Gethe/wow-ui-source/tree/forever).
This is a community mirror of primary client code, rather than an official
Blizzard hosting service. Branch contents can change with beta updates; source
review does not establish runtime availability or unrestricted access.

## Client boundary

Forever's internal game type is `camelot`: Blizzard's
[`Blizzard_PlayerSpells.toc`](https://raw.githubusercontent.com/Gethe/wow-ui-source/forever/Interface/AddOns/Blizzard_PlayerSpells/Blizzard_PlayerSpells.toc)
routes its game-specific implementations with `AllowLoadGameType camelot`.
Claudgar supplies only `Claudgar_Camelot.toc`, declares this game type and interface
`16001`, and additionally requires a `1.x` client version with interface number
`16000 <= interface < 20000` at runtime. It has no Retail/Classic collectors or
fallback TOCs. The interface family guard permits subsequent Forever beta builds
without pinning a build number. `WOW_PROJECT_ID` is deliberately not used to
distinguish Forever from the shared Mainline API family.

The `16001` packaging value is corroborated by the Forever-specific manifest in
the [Questie author's source audit](https://github.com/Questie/Questie/blob/master/FOREVER_WORK_LEFT_TO_DO.md),
whose documented source baseline is Blizzard client `1.60.1 / 69913`, commit
`70ef1b2fd78061a73f886c4a1e79dc5b5cff6d5e`. The implementation checks the actual
client's capabilities; this baseline is evidence for the interface family, not a
claim that build 69913 is the newest beta build on the review date.

## Collectors

| Section | Primary Forever source | Implementation |
| --- | --- | --- |
| Character | [UnitDocumentation.lua](https://raw.githubusercontent.com/Gethe/wow-ui-source/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/UnitDocumentation.lua) | Player identity, race/class, level/XP, faction, location and money; restricted values omitted. |
| Quests | [QuestLogDocumentation.lua](https://raw.githubusercontent.com/Gethe/wow-ui-source/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/QuestLogDocumentation.lua) | `C_QuestLog` entry records, objective counts/progress, completion/failure, completed quest ID list. A missing list produces partial coverage. |
| Talents | [SharedTraitsDocumentation.lua](https://raw.githubusercontent.com/Gethe/wow-ui-source/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/SharedTraitsDocumentation.lua) | `C_Traits` configuration, trees, nodes, entries, definitions, group names and point currencies. Active and current ranks remain separate. |
| Talent selection | [Camelot/Blizzard_ClassTalentsFrame.lua](https://raw.githubusercontent.com/Gethe/wow-ui-source/forever/Interface/AddOns/Blizzard_PlayerSpells/Camelot/ClassTalents/Blizzard_ClassTalentsFrame.lua), [SpecializationInfoDocumentation.lua](https://raw.githubusercontent.com/Gethe/wow-ui-source/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/SpecializationInfoDocumentation.lua), [ClassTalentsDocumentation.lua](https://raw.githubusercontent.com/Gethe/wow-ui-source/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/ClassTalentsDocumentation.lua) | Active configuration with the Forever spec-group mapping when necessary. The Camelot UI confirms trait groups and dual-spec tabs, and explicitly omits PvP talents. |
| Inventory | [ContainerDocumentation.lua](https://raw.githubusercontent.com/Gethe/wow-ui-source/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/ContainerDocumentation.lua), [Forever branch ContainerFrame.lua](https://raw.githubusercontent.com/Gethe/wow-ui-source/forever/Interface/AddOns/Blizzard_UIPanels_Game/Mainline/ContainerFrame.lua) | `C_Container` tables, carried bags, slot location, quantities and full variant hyperlinks. Backpack, equipped bags and carried keyring when exposed; no bank, mail or auctions. |
| Item details | [ItemDocumentation.lua](https://raw.githubusercontent.com/Gethe/wow-ui-source/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/ItemDocumentation.lua) | `C_Item.GetItemInfo`, actual item level and stat maps. Uncached item details are requested and refreshed on data-load events. Full links preserve enchants, suffixes and other variants. |
| Equipment slots | [Camelot/PaperDollFrame.lua](https://raw.githubusercontent.com/Gethe/wow-ui-source/forever/Interface/AddOns/Blizzard_UIPanels_Game/Camelot/PaperDollFrame.lua), [PaperDollInfoDocumentation.lua](https://raw.githubusercontent.com/Gethe/wow-ui-source/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/PaperDollInfoDocumentation.lua) | `C_PaperDollInfo.GetInventorySlotInfo` resolves equipment, ranged and ammo slot tokens; the loaded Camelot code verifies the inventory link/count/durability globals that remain available. |

The talent collector follows the loaded Camelot implementation, not old Classic
talent-tab APIs that happen to appear in other clients. Unapplied talent edits
are flagged with `hasStagedChanges`; tree currency requests exclude staged
changes while group currencies use the active configuration API. `activeRank` records active ranks, while `currentRank` and
`ranksPurchased` can reflect pending edits.

## Restrictions and persistence

All collection is deferred during combat. API calls are isolated and every
returned primitive/table is checked with `issecretvalue` before comparisons,
iteration or persistence. If that check is absent, collectors fail closed with
unavailable coverage. No secret values, arbitrary API objects or raw exception
strings are written. Missing APIs and unregistered beta events become concise
coverage warnings; one failed collector cannot prevent the others from saving.

Snapshots refresh after login and relevant character, quest, trait, bag,
equipment and item-cache events. Events are debounced with a three-second upper
bound during continuous activity. Logout captures synchronously before
SavedVariables serialization. Reload/logout is still required for the separate
companion to see an updated on-disk export. Earlier snapshots retain their actual
observation times when collection is restricted.

The shared export prototype is defined in [DATA-CONTRACT.md](DATA-CONTRACT.md),
[export.schema.json](../schemas/export.schema.json) and the addon
[Schema.lua](../addon/Claudgar/Schema.lua). An unavailable section's empty arrays
are placeholders, not evidence that the character has no quests or items.
