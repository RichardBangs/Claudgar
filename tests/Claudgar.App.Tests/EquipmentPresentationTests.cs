using System.Text.Json.Nodes;
using Claudgar.App.Browser;
using static Claudgar.App.Browser.EquipmentPresentation;

internal static class EquipmentPresentationTests
{
    public static void Run()
    {
        NamesRemainAuthoritative();
        EmptyAndUnknownRemainDistinct();
        UnrecognizedAndConflictingRecordsRemainAvailable();
        ItemVariantsAndNumericStatsArePreserved();
        Console.WriteLine("Equipment presentation regression tests passed.");
    }

    private static void NamesRemainAuthoritative()
    {
        var item = Item("HeadSlot"); item["slot"] = 987;
        var sheet = Create(Section(item));
        Assert(sheet.Slots.Single(slot => slot.Definition.Name == "HeadSlot").Item == item,
            "The saved canonical slot must take precedence over numeric IDs.");
        var unknown = Item("FutureSlot"); unknown["slot"] = "Chest";
        Assert(Create(Section(unknown)).Unplaced.Count == 1, "An unknown named slot must not fall back to a different legacy slot.");
        var legacy = new JsonObject { ["slot"] = "Main hand", ["name"] = "Legacy weapon" };
        Assert(Create(Section(legacy)).Slots.Single(slot => slot.Definition.Name == "MainHandSlot").Item == legacy,
            "Readable legacy slot labels should still work.");
        var numeric = new JsonObject { ["slot"] = 0, ["name"] = "Unknown numeric slot" };
        Assert(Create(Section(numeric)).Unplaced.Count == 1, "Numeric inventory IDs alone must not invent a slot mapping.");
    }

    private static void EmptyAndUnknownRemainDistinct()
    {
        var empty = new JsonObject { ["slotName"] = "AmmoSlot", ["slot"] = 0, ["empty"] = true };
        var sheet = Create(Section(empty));
        Assert(sheet.Slots.Single(slot => slot.Definition.Name == "AmmoSlot").State == SlotState.Empty,
            "Explicit observed empty slots must remain empty, including ammo ID zero.");
        Assert(sheet.Slots.Where(slot => slot.Definition.Name != "AmmoSlot").All(slot => slot.State == SlotState.Unknown),
            "Absent records must never be inferred empty.");
        Assert(Create(null).Slots.Count == 20 && Create(null).Slots.All(slot => slot.State == SlotState.Unknown),
            "A missing export should display the physical slots as unknown.");
    }

    private static void UnrecognizedAndConflictingRecordsRemainAvailable()
    {
        var first = Item("HeadSlot"); var second = Item("HeadSlot"); var future = Item("FutureSlot");
        var sheet = Create(Section(first, second, future));
        Assert(sheet.Slots.Single(slot => slot.Definition.Name == "HeadSlot").State == SlotState.Conflict,
            "Multiple records for a slot must be marked conflicting.");
        Assert(sheet.Unplaced.Count == 3 && sheet.Unplaced.Any(value => ReferenceEquals(value.Item, first)) &&
            sheet.Unplaced.Any(value => ReferenceEquals(value.Item, second)) && sheet.Unplaced.Any(value => ReferenceEquals(value.Item, future)),
            "All original conflicting and unknown records must remain inspectable.");
        var contradictory = Item("ChestSlot"); contradictory["empty"] = true;
        Assert(Create(Section(contradictory)).Unplaced.Single().Item == contradictory,
            "A contradictory empty record must retain its item information.");
    }

    private static void ItemVariantsAndNumericStatsArePreserved()
    {
        var item = Item("ChestSlot");
        const string link = "|cff1eff00|Hitem:100:99:-34:7:60:0|h[Variant robe]|h|r";
        item["link"] = link; item["durability"] = 1; item["maxDurability"] = 33;
        item["stats"] = new JsonObject { ["ITEM_MOD_INTELLECT_SHORT"] = 7 };
        var original = item.ToJsonString();
        var details = Details(item, "Chest");
        Assert(details.Contains(link, StringComparison.Ordinal) && details.Contains("Durability: 1 / 33", StringComparison.Ordinal) &&
            details.Contains("Intellect: 7", StringComparison.Ordinal), "Details must retain exact variants, condition and item bonuses.");
        Create(Section(item));
        Assert(item.ToJsonString() == original, "Presentation must not alter exported equipment.");
        Assert(Stat(new JsonObject { ["n"] = 10 }, "n") == 10 && Stat(new JsonObject { ["n"] = 10.5 }, "n") == 10.5,
            "Native and parsed finite numeric stats must both render.");
        Assert(Stat(new JsonObject { ["n"] = double.NaN }, "n") is null, "Non-finite stats must remain unknown.");
        item["iconFileId"] = (long)uint.MaxValue;
        Assert(IconId(item) == uint.MaxValue, "File IDs must support the complete unsigned 32-bit range.");
    }

    private static JsonObject Item(string slot) => new() { ["slotName"] = slot, ["itemId"] = 100, ["name"] = "Recorded item", ["empty"] = false };
    private static JsonObject Section(params JsonObject[] items) => new() { ["status"] = "partial", ["data"] = new JsonObject { ["items"] = new JsonArray(items.Cast<JsonNode?>().ToArray()) } };
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
