using System.Text.Json.Nodes;

namespace Claudgar.Preview;

/// <summary>Fictional data only; no game files or saved character exports are read.</summary>
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
                        new JsonObject { ["title"] = "The Defias Brotherhood", ["level"] = 22, ["zone"] = "Westfall", ["isComplete"] = false },
                        new JsonObject { ["title"] = "A Watchful Eye", ["level"] = 20, ["zone"] = "Redridge Mountains", ["isComplete"] = true }),
                    ["completed"] = new JsonArray(62, 76, 83, 112)
                }),
                ["talents"] = Section(new JsonObject
                {
                    ["unspentPoints"] = 0,
                    ["trees"] = new JsonArray(
                        new JsonObject { ["name"] = "Discipline", ["pointsSpent"] = 5 },
                        new JsonObject { ["name"] = "Holy", ["pointsSpent"] = 8 },
                        new JsonObject { ["name"] = "Shadow", ["pointsSpent"] = 0 })
                }),
                ["inventory"] = Section(new JsonObject
                {
                    ["bags"] = new JsonArray(new JsonObject
                    {
                        ["name"] = "Backpack", ["slots"] = 16, ["freeSlots"] = 9,
                        ["items"] = new JsonArray(new JsonObject { ["name"] = "Linen Cloth", ["count"] = 20 }, new JsonObject { ["name"] = "Healing Potion", ["count"] = 5 })
                    })
                }),
                ["equipment"] = Section(new JsonObject
                {
                    ["items"] = new JsonArray(
                        new JsonObject { ["slot"] = "Chest", ["name"] = "Robes of the Light", ["quality"] = "Uncommon" },
                        new JsonObject { ["slot"] = "Main hand", ["name"] = "Staff of the Westfall Adept", ["quality"] = "Rare" })
                })
            }
        };
    }

    private static JsonObject Section(JsonObject data) => new()
    {
        ["status"] = "complete", ["observedAt"] = 1791226800L,
        ["warnings"] = new JsonArray(), ["data"] = data
    };
}
