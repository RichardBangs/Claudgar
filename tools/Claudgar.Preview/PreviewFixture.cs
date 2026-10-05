using System.Text.Json.Nodes;

namespace Claudgar.Preview;

/// <summary>Fictional character data and sanitized layout fixtures; no game files or user exports are read.</summary>
internal static class PreviewFixture
{
    public static JsonObject CreateSnapshot()
    {
        var identity = new JsonObject
        {
            ["name"] = "Aeloria", ["realm"] = "Classic Beta PvE", ["level"] = 22,
            ["raceName"] = "Human", ["className"] = "Priest", ["classToken"] = "PRIEST",
            ["faction"] = "Alliance", ["zone"] = "Stormwind City", ["subZone"] = "The Canals"
        };
        var data = (JsonObject)identity.DeepClone();
        data["guid"] = "Player-PREVIEW-0001";
        data["xp"] = 12480;
        data["maxXp"] = 28000;
        data["money"] = 21437;
        return new JsonObject
        {
            ["characterId"] = "preview-aeloria", ["accountId"] = "DEMO", ["character"] = identity,
            ["exportState"] = "ready", ["freshness"] = new JsonObject { ["isStale"] = false, ["cached"] = false },
            ["sections"] = new JsonObject
            {
                ["character"] = Section(data),
                ["quests"] = Section(new JsonObject
                {
                    ["active"] = new JsonArray(
                        new JsonObject { ["questId"] = 155, ["title"] = "The Defias Brotherhood", ["level"] = 22, ["isComplete"] = false },
                        new JsonObject { ["questId"] = 94, ["title"] = "A Watchful Eye", ["level"] = 20, ["isComplete"] = true }),
                    ["completed"] = new JsonArray(62, 76, 83, 112),
                    ["completedDetails"] = new JsonArray(
                        new JsonObject { ["questId"] = 62, ["title"] = "The Fargodeep Mine" },
                        new JsonObject { ["questId"] = 76, ["title"] = "The Jasperlode Mine" },
                        new JsonObject { ["questId"] = 83, ["title"] = "Red Linen Goods" })
                }),
                ["talents"] = Section(new JsonObject
                {
                    ["mode"] = "traits", ["activeConfigId"] = 1, ["configName"] = "Aeloria's talents", ["hasStagedChanges"] = false,
                    ["trees"] = new JsonArray(
                        TalentTree(1, "Discipline", DisciplineTalents),
                        TalentTree(2, "Holy", HolyTalents),
                        TalentTree(3, "Shadow", ShadowTalents))
                }),
                ["inventory"] = Section(new JsonObject
                {
                    ["bags"] = new JsonArray(
                        new JsonObject
                        {
                            ["bagId"] = 0, ["name"] = "Backpack", ["slotCount"] = 16, ["freeSlots"] = 11,
                            ["items"] = new JsonArray(
                                Item(1, 6948, "Hearthstone", 1, 1), Item(2, 2589, "Linen Cloth", 20, 1),
                                Item(5, 929, "Healing Potion", 5, 1), Item(8, 1205, "Melon Juice", 12, 1),
                                Item(11, 2996, "Bolt of Linen Cloth", 8, 1))
                        },
                        new JsonObject
                        {
                            ["bagId"] = 1, ["name"] = "Mageweave Bag", ["slotCount"] = 12, ["freeSlots"] = 9,
                            ["items"] = new JsonArray(
                                Item(1, 2042, "Staff of Westfall", 1, 3), Item(3, 14113, "Aboriginal Sash", 1, 2),
                                Item(7, 7073, "Broken Fang", 3, 0))
                        })
                }),
                ["equipment"] = CreateEquipmentSection()
            }
        };
    }

    private static JsonObject Item(int slot, int id, string name, int count, int quality) => new()
    {
        ["slot"] = slot, ["itemId"] = id, ["name"] = name, ["count"] = count, ["quality"] = quality,
        ["link"] = $"item:{id}:0:0:0:0:0:0:0", ["itemLevel"] = 22, ["requiredLevel"] = 18
    };

    public static JsonObject CreateEquipmentSection() => Section(new JsonObject
    {
        ["items"] = new JsonArray(
            Equipped(1, "HeadSlot", 16693, "Devout Crown", 3, 132767, durability: 56, maximum: 60),
            Equipped(2, "NeckSlot", 5180, "Necklace of Harmony", 2, 133279),
            Equipped(3, "ShoulderSlot", 16695, "Devout Mantle", 3, 135033, durability: 39, maximum: 50),
            Equipped(15, "BackSlot", 6449, "Glowing Lizardscale Cloak", 3, 132656, durability: 25, maximum: 35),
            Equipped(5, "ChestSlot", 16690, "Devout Robe", 3, 132652, durability: 32, maximum: 80),
            Equipped(4, "ShirtSlot", 2576, "White Linen Shirt", 1, 135030),
            Equipped(19, "TabardSlot", 5976, "Guild Tabard", 1, 135026),
            Equipped(9, "WristSlot", 16697, "Devout Bracers", 3, 132520, durability: 23, maximum: 35),
            Equipped(10, "HandsSlot", 16692, "Devout Gloves", 3, 132948, durability: 8, maximum: 40),
            Equipped(6, "WaistSlot", 16696, "Devout Belt", 3, 132499, durability: 35, maximum: 35),
            Equipped(7, "LegsSlot", 16694, "Devout Skirt", 3, 134588, durability: 55, maximum: 65),
            Equipped(8, "FeetSlot", 16691, "Devout Sandals", 3, 132539, durability: 40, maximum: 50),
            Equipped(11, "Finger0Slot", 6414, "Seal of Sylvanas", 3, 133357),
            Equipped(12, "Finger1Slot", 2933, "Seal of Wrynn", 3, 133347),
            Equipped(13, "Trinket0Slot", 11832, "Burst of Knowledge", 3, 133282),
            EmptyEquipment(14, "Trinket1Slot"),
            Equipped(16, "MainHandSlot", 13964, "Witchblade", 3, 135661, durability: 47, maximum: 65),
            Equipped(17, "SecondaryHandSlot", 15912, "Buccaneer's Orb", 2, 135467),
            Equipped(18, "RangedSlot", 5207, "Opaque Wand", 2, 135469, durability: 37, maximum: 40),
            EmptyEquipment(0, "AmmoSlot"))
    });

    public static JsonObject CreateCharacterStats() => new()
    {
        ["maxHealth"] = 2860d, ["maxMana"] = 4275d, ["maxPower"] = 4275d, ["powerType"] = 0d,
        ["armor"] = 980d, ["strength"] = 43d, ["agility"] = 50d, ["stamina"] = 142d,
        ["intellect"] = 205d, ["spirit"] = 187d,
        ["meleeDamageMin"] = 38d, ["meleeDamageMax"] = 72d, ["meleeAttackSpeed"] = 1.8d,
        ["meleeAttackPower"] = 60d, ["meleeCritChance"] = 3.47d,
        ["rangedDamageMin"] = 56d, ["rangedDamageMax"] = 104d, ["rangedAttackSpeed"] = 1.5d,
        ["rangedAttackPower"] = 12d, ["rangedCritChance"] = 2.53d,
        ["spellPower"] = 110d, ["spellHealing"] = 125d, ["spellCritChance"] = 7.12d
    };

    private static JsonObject Equipped(int slot, string slotName, int id, string name, int quality, long icon,
        int? durability = null, int? maximum = null)
    {
        var item = new JsonObject
        {
            ["slot"] = slot, ["slotName"] = slotName, ["itemId"] = id, ["name"] = name,
            ["quality"] = quality, ["iconFileId"] = icon, ["empty"] = false, ["count"] = 1,
            ["link"] = $"item:{id}:0:0:0:0:0:0:0", ["bound"] = true
        };
        if (quality > 1) item["stats"] = new JsonObject { ["ITEM_MOD_INTELLECT_SHORT"] = 4, ["ITEM_MOD_SPIRIT_SHORT"] = 3 };
        if (durability.HasValue) { item["durability"] = durability.Value; item["maxDurability"] = maximum; }
        return item;
    }

    private static JsonObject EmptyEquipment(int slot, string slotName) => new()
    {
        ["slot"] = slot, ["slotName"] = slotName, ["empty"] = true
    };

    // The sparse layout is illustrative, and is never substituted for an actual saved export.
    private sealed record Talent(string Name, int Column, int Row, int Maximum, long IconFileId,
        int Rank = 0, int? Requires = null);

    private static readonly Talent[] DisciplineTalents =
    [
        new("Unbreakable Will", 1, 0, 5, 135995),
        new("Wand Specialization", 2, 0, 5, 135463, Rank: 5),
        new("Silent Resolve", 0, 1, 5, 136053),
        new("Improved Power Word: Shield", 1, 1, 3, 135940),
        new("Improved Power Word: Fortitude", 2, 1, 2, 135987),
        new("Martyrdom", 3, 1, 2, 136107),
        new("Inner Focus", 1, 2, 1, 135863),
        new("Meditation", 2, 2, 3, 136090),
        new("Improved Inner Fire", 0, 3, 3, 135926),
        new("Mental Agility", 1, 3, 5, 132156),
        new("Improved Mana Burn", 3, 3, 2, 136170),
        new("Mental Strength", 1, 4, 5, 136031),
        new("Divine Spirit", 2, 4, 1, 135898, Requires: 7),
        new("Force of Will", 2, 5, 5, 136092),
        new("Power Infusion", 1, 6, 1, 135939, Requires: 11)
    ];

    private static readonly Talent[] HolyTalents =
    [
        new("Healing Focus", 0, 0, 2, 135918),
        new("Improved Renew", 1, 0, 3, 135953, Rank: 3),
        new("Holy Specialization", 2, 0, 5, 135967, Rank: 5),
        new("Spell Warding", 1, 1, 5, 135976),
        new("Divine Fury", 2, 1, 5, 135971),
        new("Holy Nova", 0, 2, 1, 135922),
        new("Blessed Recovery", 1, 2, 3, 135877),
        new("Inspiration", 3, 2, 3, 135928),
        new("Holy Reach", 0, 3, 2, 135949),
        new("Improved Healing", 1, 3, 3, 135916),
        new("Searing Light", 2, 3, 2, 135973, Requires: 4),
        new("Improved Prayer of Healing", 0, 4, 2, 135943),
        new("Spirit of Redemption", 1, 4, 1, 132864),
        new("Spiritual Guidance", 2, 4, 5, 135977),
        new("Spiritual Healing", 2, 5, 5, 136057),
        new("Lightwell", 1, 6, 1, 135980, Requires: 12)
    ];

    private static readonly Talent[] ShadowTalents =
    [
        new("Spirit Tap", 1, 0, 5, 136188),
        new("Blackout", 2, 0, 5, 136160),
        new("Shadow Affinity", 0, 1, 3, 136205),
        new("Improved Shadow Word: Pain", 1, 1, 2, 136207),
        new("Shadow Focus", 2, 1, 5, 136126),
        new("Improved Psychic Scream", 0, 2, 2, 136184),
        new("Improved Mind Blast", 1, 2, 5, 136224),
        new("Mind Flay", 2, 2, 1, 136208),
        new("Improved Fade", 1, 3, 2, 135994),
        new("Shadow Reach", 2, 3, 3, 136130),
        new("Shadow Weaving", 3, 3, 5, 136123),
        new("Silence", 0, 4, 1, 136164, Requires: 5),
        new("Vampiric Embrace", 1, 4, 1, 136230),
        new("Improved Vampiric Embrace", 2, 4, 2, 136165, Requires: 12),
        new("Darkness", 2, 5, 5, 136223),
        new("Shadowform", 1, 6, 1, 136200, Requires: 12)
    ];

    private static JsonObject TalentTree(int id, string name, Talent[] talents)
    {
        var nodes = new JsonArray();
        var spent = talents.Sum(talent => talent.Rank);
        for (var i = 0; i < talents.Length; i++)
        {
            var nodeId = id * 100 + i;
            var talent = talents[i];
            var edges = new JsonArray();
            for (var dependent = 0; dependent < talents.Length; dependent++)
                if (talents[dependent].Requires == i)
                    edges.Add(new JsonObject { ["targetNodeId"] = id * 100 + dependent, ["isActive"] = false });
            nodes.Add(new JsonObject
            {
                ["nodeId"] = nodeId, ["posX"] = 1800 + talent.Column * 600, ["posY"] = 1200 + talent.Row * 600,
                ["activeRank"] = talent.Rank, ["currentRank"] = talent.Rank, ["maxRanks"] = talent.Maximum,
                ["activeEntryId"] = nodeId * 10, ["isVisible"] = true, ["isAvailable"] = talent.Row <= spent / 5,
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["entryId"] = nodeId * 10, ["spellId"] = nodeId * 20, ["name"] = talent.Name, ["rank"] = talent.Rank,
                    ["maxRanks"] = talent.Maximum, ["isActive"] = talent.Rank > 0, ["iconFileId"] = talent.IconFileId
                }),
                ["visibleEdges"] = edges
            });
        }
        var currencies = new JsonArray(new JsonObject { ["currencyId"] = id, ["quantity"] = 0, ["spent"] = spent });
        return new JsonObject
        {
            ["treeId"] = id, ["nodes"] = nodes, ["currencies"] = currencies,
            ["groups"] = new JsonArray(new JsonObject
            {
                ["groupId"] = id, ["name"] = name, ["iconFileId"] = talents[0].IconFileId,
                ["currencies"] = currencies.DeepClone()
            })
        };
    }

    public static JsonObject CreateGroupedTalentSection()
    {
        var section = CreateSnapshot()["sections"]!["talents"]!.DeepClone().AsObject();
        var sourceTrees = section["data"]!["trees"]!.AsArray();
        var nodes = new JsonArray();
        var groups = new JsonArray();
        var currencies = new JsonArray();
        for (var index = 0; index < sourceTrees.Count; index++)
        {
            var source = sourceTrees[index]!;
            groups.Add(source["groups"]![0]!.DeepClone());
            currencies.Add(source["currencies"]![0]!.DeepClone());
            foreach (var sourceNode in source["nodes"]!.AsArray())
            {
                var node = sourceNode!.DeepClone().AsObject();
                node["groupIds"] = new JsonArray(index + 1);
                node["posX"] = node["posX"]!.GetValue<int>() + index * 2400;
                nodes.Add(node);
            }
        }
        sourceTrees.Clear();
        sourceTrees.Add(new JsonObject
        {
            ["treeId"] = 700, ["nodes"] = nodes, ["groups"] = groups, ["currencies"] = currencies
        });
        return section;
    }

    public static JsonObject CreateNativeTalentSection()
    {
        using var stream = typeof(PreviewFixture).Assembly.GetManifestResourceStream("Fixtures/priest-native-layout.json")
            ?? throw new InvalidOperationException("The sanitized native talent coordinate fixture is missing.");
        return Section(JsonNode.Parse(stream)!.AsObject());
    }

    private static JsonObject Section(JsonObject data) => new()
    {
        ["status"] = "complete", ["observedAt"] = 1791226800L,
        ["warnings"] = new JsonArray(), ["data"] = data
    };
}
