using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Claudgar.App.Browser;

/// <summary>Projects saved equipment into physical slots without changing or completing its data.</summary>
internal static class EquipmentPresentation
{
    internal enum SlotState { Unknown, Empty, Equipped, Conflict }
    internal enum SlotRegion { Left, Right, Weapon, Ammo }
    internal sealed record SlotDefinition(string Name, string Label, SlotRegion Region, int Position);
    internal sealed record Slot(SlotDefinition Definition, SlotState State, JsonObject? Item);
    internal sealed record UnplacedItem(JsonObject Item, string Reason);
    internal sealed record Sheet(IReadOnlyList<Slot> Slots, IReadOnlyList<UnplacedItem> Unplaced);

    public static readonly IReadOnlyList<SlotDefinition> Definitions =
    [
        new("HeadSlot", "Head", SlotRegion.Left, 0), new("NeckSlot", "Neck", SlotRegion.Left, 1),
        new("ShoulderSlot", "Shoulders", SlotRegion.Left, 2), new("BackSlot", "Back", SlotRegion.Left, 3),
        new("ChestSlot", "Chest", SlotRegion.Left, 4), new("ShirtSlot", "Shirt", SlotRegion.Left, 5),
        new("TabardSlot", "Tabard", SlotRegion.Left, 6), new("WristSlot", "Wrists", SlotRegion.Left, 7),
        new("HandsSlot", "Hands", SlotRegion.Right, 0), new("WaistSlot", "Waist", SlotRegion.Right, 1),
        new("LegsSlot", "Legs", SlotRegion.Right, 2), new("FeetSlot", "Feet", SlotRegion.Right, 3),
        new("Finger0Slot", "Finger 1", SlotRegion.Right, 4), new("Finger1Slot", "Finger 2", SlotRegion.Right, 5),
        new("Trinket0Slot", "Trinket 1", SlotRegion.Right, 6), new("Trinket1Slot", "Trinket 2", SlotRegion.Right, 7),
        new("MainHandSlot", "Main hand", SlotRegion.Weapon, 0), new("SecondaryHandSlot", "Off hand", SlotRegion.Weapon, 1),
        new("RangedSlot", "Ranged", SlotRegion.Weapon, 2), new("AmmoSlot", "Ammo", SlotRegion.Ammo, 0)
    ];

    public static Sheet Create(JsonObject? section)
    {
        var grouped = new Dictionary<string, List<JsonObject>>(StringComparer.Ordinal);
        var unplaced = new List<UnplacedItem>();
        if (section?["data"]?["items"] is JsonArray items)
            foreach (var item in items.OfType<JsonObject>())
            {
                var definition = ResolveSlot(item);
                if (definition is null) { unplaced.Add(new(item, "Slot not recognized")); continue; }
                if (!grouped.TryGetValue(definition.Name, out var records)) grouped.Add(definition.Name, records = []);
                records.Add(item);
            }
        var slots = new List<Slot>();
        foreach (var definition in Definitions)
        {
            if (!grouped.TryGetValue(definition.Name, out var records)) { slots.Add(new(definition, SlotState.Unknown, null)); continue; }
            if (records.Count > 1)
            {
                slots.Add(new(definition, SlotState.Conflict, null));
                unplaced.AddRange(records.Select(item => new UnplacedItem(item, $"Conflicting records for {definition.Label}")));
                continue;
            }
            var record = records[0];
            var empty = Boolean(record, "empty");
            var hasItem = InventoryPresentation.Number(record, "itemId") is > 0 ||
                !string.IsNullOrWhiteSpace(InventoryPresentation.Text(record, "link")) ||
                !string.IsNullOrWhiteSpace(InventoryPresentation.Text(record, "name"));
            if (empty == true && hasItem)
            {
                slots.Add(new(definition, SlotState.Conflict, null));
                unplaced.Add(new(record, $"Contradictory empty record for {definition.Label}"));
                continue;
            }
            var state = empty == true && !hasItem ? SlotState.Empty : hasItem && empty != true ? SlotState.Equipped : SlotState.Unknown;
            slots.Add(new(definition, state, record));
        }
        return new(slots, unplaced);
    }

    public static SlotDefinition? ResolveSlot(JsonObject item)
    {
        // The collector resolves the game's inventory IDs. Its named slot is authoritative,
        // including clients whose numeric IDs differ from a conventional inventory table.
        var name = InventoryPresentation.Text(item, "slotName");
        if (!string.IsNullOrWhiteSpace(name)) return Definitions.FirstOrDefault(slot => slot.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var legacy = InventoryPresentation.Text(item, "slot");
        if (legacy is null) return null;
        var normalized = legacy.Replace(" ", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
        return Definitions.FirstOrDefault(slot => slot.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
            slot.Label.Replace(" ", "", StringComparison.Ordinal).Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static bool? Boolean(JsonObject value, string name) => value[name] is JsonValue field && field.TryGetValue<bool>(out var result) ? result : null;
    public static long? IconId(JsonObject item) => Stat(item, "iconFileId") is > 0 and <= uint.MaxValue and var id && id == Math.Truncate(id) ? (long)id : null;
    public static int? Quality(JsonObject item)
    {
        if (InventoryPresentation.Number(item, "quality") is int value) return value;
        return InventoryPresentation.Text(item, "quality")?.ToLowerInvariant() switch
        { "poor" => 0, "common" => 1, "uncommon" => 2, "rare" => 3, "epic" => 4, "legendary" => 5, "artifact" => 6, "heirloom" => 7, _ => null };
    }

    public static string Details(JsonObject item, string slotLabel)
    {
        var details = new StringBuilder();
        details.AppendLine(InventoryPresentation.ItemName(item));
        details.AppendLine(slotLabel + " · " + InventoryPresentation.QualityName(Quality(item)));
        foreach (var (field, label) in new (string, string)[]
        {
            ("itemLevel", "Item level"), ("requiredLevel", "Requires level"), ("className", "Type"),
            ("subclassName", "Subtype"), ("count", "Count"), ("itemId", "Item ID"), ("slot", "Saved slot")
        })
            if (item[field] is JsonValue value) details.AppendLine($"{label}: {value}");
        if (InventoryPresentation.Number(item, "durability") is int durability)
            details.AppendLine($"Durability: {durability}" + (InventoryPresentation.Number(item, "maxDurability") is int maximum ? $" / {maximum}" : ""));
        if (item["stats"] is JsonObject { Count: > 0 } stats)
        {
            details.AppendLine();
            foreach (var (key, value) in stats) details.AppendLine($"{StatLabel(key)}: {value}");
        }
        if (InventoryPresentation.Text(item, "link") is { Length: > 0 } link)
        { details.AppendLine(); details.AppendLine("Saved item link"); details.AppendLine(link); }
        return details.ToString().TrimEnd();
    }

    public static string StatLabel(string key) => key switch
    {
        "ITEM_MOD_STRENGTH_SHORT" => "Strength", "ITEM_MOD_AGILITY_SHORT" => "Agility",
        "ITEM_MOD_STAMINA_SHORT" => "Stamina", "ITEM_MOD_INTELLECT_SHORT" => "Intellect",
        "ITEM_MOD_SPIRIT_SHORT" => "Spirit", "ITEM_MOD_HEALTH_SHORT" => "Health",
        "ITEM_MOD_MANA_SHORT" => "Mana", "ITEM_MOD_ARMOR_SHORT" or "RESISTANCE0_NAME" => "Armor",
        "ITEM_MOD_SPELL_POWER_SHORT" => "Spell power", "ITEM_MOD_SPELL_HEALING_DONE_SHORT" => "Healing",
        "ITEM_MOD_SPELL_DAMAGE_DONE_SHORT" => "Spell damage", "ITEM_MOD_ATTACK_POWER_SHORT" => "Attack power",
        _ => JsonPresentation.Humanize(key)
    };

    public static double? Stat(JsonObject? stats, string key)
    {
        if (stats?[key] is not JsonValue value) return null;
        if (value.TryGetValue<double>(out var real) && double.IsFinite(real)) return real;
        if (value.TryGetValue<int>(out var integer)) return integer;
        if (value.TryGetValue<long>(out var longInteger)) return longInteger;
        if (value.TryGetValue<float>(out var single) && float.IsFinite(single)) return single;
        if (value.TryGetValue<decimal>(out var decimalNumber)) return (double)decimalNumber;
        return null;
    }
    public static string Format(double? number, bool percent = false) => number.HasValue
        ? number.Value.ToString("0.##", CultureInfo.InvariantCulture) + (percent ? "%" : "") : "—";
}
