# Shared export contract: version 1

The addon and desktop app share one versioned boundary. The addon constructs it in
`addon/Claudgar/Schema.lua`; the app validates it in
`src/Claudgar.Core/Exports/SnapshotValidator.cs`. The machine-readable prototype is
[`schemas/export.schema.json`](../schemas/export.schema.json). The representative
SavedVariables file is [`tests/fixtures/Claudgar.lua`](../tests/fixtures/Claudgar.lua).
The fixture is synthetic data, not evidence of a successful game export.

## Storage and wire representation

WoW writes the account-wide global `ClaudgarDB` into
`WTF/Account/<account>/SavedVariables/Claudgar.lua` on UI reload or logout. Each
file holds snapshots for multiple characters on that account. Claudgar parses a
restricted Lua table syntax; it never runs the file as a Lua program.

The schema describes the equivalent normalized JSON. Lua keyed tables become
objects. Dense tables indexed from 1 become arrays. Known list fields such as
`active`, `completed`, `objectives`, `trees`, `nodes`, `entries`, `currencies`,
`groups`, `bags`, `items`, and `warnings` convert an empty Lua `{}` to JSON `[]`.
An empty keyed table such as `stats` remains `{}`. Sparse lists and mixed numeric
and string keys are rejected. `nil` fields are omitted by WoW serialization.

```lua
ClaudgarDB = {
    schemaVersion = 1,
    client = {
        flavor = "forever", version = "1.60.1", build = "example-build",
        interface = 16001, locale = "enGB",
    },
    characters = {
        ["Example Realm:Player-0000-EXAMPLE"] = {
            characterKey = "Example Realm:Player-0000-EXAMPLE",
            sections = {
                character = {
                    status = "partial", observedAt = 1791201600,
                    data = { guid = "Player-0000-EXAMPLE", name = "Example",
                             realm = "Example Realm", level = 20 },
                    warnings = {},
                },
                -- quests, talents, inventory, and equipment use the same envelope.
            },
        },
    },
}
```

This abbreviated shape illustrates the boundary; use the complete fixture for a
full record. Each emitted character has all five sections. The table key must
equal its `characterKey`. The currently targeted Forever family is 1.60, with no
exact patch/build pin. Capability checks handle unavailable or changed APIs.

## Section envelope

| Field | Type | Meaning |
| --- | --- | --- |
| `status` | string enum | `complete`, `partial`, `unavailable`, or `failed` |
| `observedAt` | integer | UTC Unix seconds when collection was attempted |
| `data` | object or null | The section payload; required for complete/partial |
| `warnings` | string array | Short coverage explanations without account paths |
| `error` | optional string | A collector failure explanation |

`complete` means the supported collector finished with no known gaps; it does
not imply live game state. `partial` means some requested values are missing or
not yet cached. `unavailable` means the section's API is unavailable. `failed`
means an attempted collector failed. Empty lists are actual observed empty
collections only when their coverage supports that conclusion. Failed and
unavailable payloads may contain empty prototype lists, which are not evidence
of an empty quest log or inventory.

IDs, quantities, ranks, and positions are JSON numbers; flags are booleans;
names/tokens/links are strings. Optional unavailable values are omitted, never
invented as zero. Secret/restricted beta values are not persisted. The addon
normalizes each field to its declared type before saving.

## Character data

`guid`, `name`, `realm`, `className`, `classToken`, `raceName`, `raceToken`,
`faction`, `locale`, `zone`, and `subZone` are strings. `level`, `classId`,
`raceId`, `mapId`, `money`, `xp`, `maxXp`, and `restedXp` are numbers. Money is
in copper. `zone` and `subZone` use the client locale. Some optional map or XP
fields may be absent while zoning or at a level cap.

## Quest data

`active` is an array of quest records. `completed` is an array of positive quest
IDs returned by the completed-quest API; completed titles/objectives are not
invented when only IDs are available.

| Active quest fields | Type |
| --- | --- |
| `questId`, `level`, `suggestedGroup`, `frequency`, `requiredMoney` | number |
| `title` | string |
| `isComplete`, `isFailed`, `isTask`, `isHidden` | boolean |
| `objectives` | array of objective objects |

Objective fields are `text` and `type` (strings), `finished` (boolean), and
`numFulfilled`, `numRequired`, and `objectiveType` (numbers). `objectiveType` is
the client's numeric objective enum. An objective's localized
text is retained alongside numeric progress. Quest headers are excluded.

## Talent data

The Forever collector uses the client trait APIs. `mode` is `"traits"`.
`activeConfigId` and `activeSpecGroup` are numbers; `configName` is a string;
`hasStagedChanges` is a boolean. `trees` contains:

- `treeId`; `nodes`; `currencies`; and `groups`.
- Nodes: `nodeId`, `activeEntryId`, `activeRank`, `currentRank`, `ranksPurchased`,
  `maxRanks`, `posX`, `posY`; booleans `isAvailable`, `isVisible`; and `entries`.
- Entries: `entryId`, `definitionId`, `spellId`, `rank`, `maxRanks`, `name`,
  and boolean `isActive`.
- Currencies: `currencyId`, `quantity`, `maxQuantity`, `spent`.
- Groups: `groupId`, `name`, `iconFileId`, and their `currencies`.

`activeRank` describes the applied selection. The UI's `currentRank` and
`ranksPurchased` may include staged edits. When edits are pending, coverage is
partial with an explanation. Tree currency requests exclude staged changes;
group currencies are returned by the active configuration API.
The app and model must keep those concepts distinct.

## Inventory and equipment data

Inventory has `bags`: each bag carries numeric `bagId`, `slotCount`, `freeSlots`,
`bagFamily`, optional string `name`, and `items`. Bag item `slot` is the slot
inside its enclosing bag. Only carried bags are collected; bank, mail, auction,
and account storage are outside the first version's scope.

Equipment has `items`: each record has numeric `slot`, string `slotName`,
boolean `empty`, and available item details. Equipment slot 0 is valid for the
Forever ammo slot; don't assume all equipment IDs start at 1. Empty equipment
slots are explicit. Empty bag slots are derived from capacity and occupied slots.

Shared item fields:

| Fields | Type / units |
| --- | --- |
| `itemId`, `slot`, `count`, `quality`, `itemLevel`, `requiredLevel` | number |
| `classId`, `subclassId`, `iconFileId`, `maxStackCount` | number |
| `sellPrice`, `durability`, `maxDurability` | number; sell price in copper |
| `name`, `link`, `className`, `subclassName`, `equipLocation`, `slotName` | string |
| `locked`, `bound`, `isCraftingReagent`, `empty` | boolean |
| `stats` | object mapping stat tokens to numeric values |

Keep the full `link` alongside `itemId`: its variant/enchantment/bonus fields
identify the actual item. Missing asynchronously cached details mark the section
partial and trigger a later collection when the game reports that item data is
available. No external item database is required.

## Reader and MCP response envelope

Saved `characterKey` is scoped to an export file. The main app derives opaque
`installationId`, `accountId`, and `characterId` hashes that incorporate the
installation, account export file, and character key. Those IDs prevent
characters with identical names/realms in different accounts from merging.
Changing an installation's path changes its derived IDs.

`list_characters` returns `characters`, `exportState`, `readAt`, and `issues`.
Character summaries include `characterId`, `installationId`, `accountId`,
`client`, `character`, `coverage`, and `freshness`. `get_character` adds
`sections`. Section tools add `section`, `coverage`, `observedAt`, `data`,
`warnings`, optional `error`, and `freshness`. Use only a `characterId` from `list_characters`.

`exportState` distinguishes ready, partial, stale, failed, and no export.
Freshness includes per-section age, the saved export's timestamp, and whether
the reader used its older in-memory cache after an incomplete/missing file.
Every request checks exports again. File save time is not observation time;
both are distinct from the request's read time. A successfully read old snapshot
may be old without being a read-failure cache fallback.

Neither raw installation paths nor account directory names are returned by MCP.
Diagnostic issues use opaque IDs and generic messages. The local desktop UI may
show a selected game path to help the user configure it.

## Versioning and ownership

Change both `Schema.lua` and `ExportContract.cs` alongside this schema when the
contract changes. Increment `schemaVersion` for incompatible changes. Version 1
rejects unsupported versions and unexpected payload fields rather than guessing.
Update the addon and app together. Future optional fields need matching
prototype/validator/schema changes even when the version remains compatible.
The JSON schema defines the normalized storage boundary; account isolation and
matching `characterKey` are additionally enforced by the reader.
