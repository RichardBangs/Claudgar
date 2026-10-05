using System.Text;
using System.Text.Json.Nodes;

namespace Claudgar.App.Browser;

/// <summary>Formats only the recorded inventory fields; it never resolves data online.</summary>
internal static class InventoryPresentation
{
    public static string? Text(JsonObject? value, string field) =>
        value?[field] is JsonValue node && node.TryGetValue<string>(out var text) ? text : null;

    public static int? Number(JsonObject? value, string field)
    {
        if (value?[field] is not JsonValue node) return null;
        if (node.TryGetValue<int>(out var number)) return number;
        if (node.TryGetValue<long>(out var integer) && integer is >= int.MinValue and <= int.MaxValue) return (int)integer;
        if (node.TryGetValue<double>(out var real) && double.IsFinite(real) && real is >= int.MinValue and <= int.MaxValue && real == Math.Truncate(real)) return (int)real;
        return null;
    }

    public static string ItemName(JsonObject item) => Text(item, "name") is { Length: > 0 } name
        ? name : Number(item, "itemId") is int id ? $"Item #{id}" : "Unnamed item";

    public static string BagName(JsonObject bag, int index) => Text(bag, "name") is { Length: > 0 } name
        ? name : Number(bag, "bagId") switch { 0 => "Backpack", < 0 => "Keyring", int id => $"Bag {id}", _ => $"Bag {index + 1}" };

    public static string QualityName(int? quality) => quality switch
    {
        0 => "Poor", 1 => "Common", 2 => "Uncommon", 3 => "Rare", 4 => "Epic", 5 => "Legendary",
        6 => "Artifact", 7 => "Heirloom", int number => $"Quality {number}", _ => "Quality unknown"
    };

    public static Color QualityColor(int? quality) => quality switch
    {
        0 => Color.FromArgb(157, 157, 157), 1 => Color.FromArgb(232, 229, 220),
        2 => Color.FromArgb(95, 193, 91), 3 => Color.FromArgb(105, 164, 236),
        4 => Color.FromArgb(187, 127, 225), 5 => Color.FromArgb(238, 172, 79),
        6 or 7 => Color.FromArgb(222, 195, 116), _ => BrowserTheme.Muted
    };

    public static string Monogram(JsonObject item)
    {
        var name = Text(item, "name");
        if (string.IsNullOrWhiteSpace(name)) return "#";
        var words = name.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 1 ? $"{words[0][0]}{words[^1][0]}".ToUpperInvariant() : name[..Math.Min(2, name.Length)].ToUpperInvariant();
    }

    public static string Tooltip(JsonObject item, string bag, int? slot)
    {
        var lines = new List<string> { ItemName(item), $"{bag} · {(slot is int position ? $"Slot {position}" : "Slot not recorded")}", QualityName(Number(item, "quality")) };
        if (Number(item, "count") is int count) lines.Add($"Stack: {count}");
        if (Number(item, "itemLevel") is int level) lines.Add($"Item level: {level}");
        return string.Join(Environment.NewLine, lines);
    }

    public static string Details(JsonObject item, string bag, int? slot)
    {
        var details = new StringBuilder();
        details.AppendLine(ItemName(item));
        details.AppendLine($"{bag} · {(slot is int position ? $"Slot {position}" : "Slot not recorded")}");
        details.AppendLine(QualityName(Number(item, "quality")));
        details.AppendLine();
        foreach (var (field, label) in new (string, string)[]
        {
            ("itemId", "Item ID"), ("count", "Stack"), ("itemLevel", "Item level"), ("requiredLevel", "Required level"),
            ("className", "Type"), ("subclassName", "Subtype"), ("equipLocation", "Equip location"), ("maxStackCount", "Maximum stack")
        })
            if (item[field] is JsonValue value) details.AppendLine($"{label}: {value}");
        if (Number(item, "durability") is int durability)
            details.AppendLine($"Durability: {durability}" + (Number(item, "maxDurability") is int maximum ? $" / {maximum}" : ""));
        if (Number(item, "sellPrice") is int copper)
            details.AppendLine($"Vendor value: {copper / 10000}g {copper / 100 % 100}s {copper % 100}c");
        foreach (var (field, label) in new (string, string)[] { ("bound", "Bound"), ("locked", "Locked"), ("isCraftingReagent", "Crafting reagent") })
            if (item[field] is JsonValue value && value.TryGetValue<bool>(out var state)) details.AppendLine($"{label}: {(state ? "Yes" : "No")}");
        if (item["stats"] is JsonObject { Count: > 0 } stats)
        {
            details.AppendLine(); details.AppendLine("Recorded stats");
            foreach (var (stat, value) in stats) details.AppendLine($"{JsonPresentation.Humanize(stat)}: {value}");
        }
        if (Text(item, "link") is { Length: > 0 } link)
        {
            details.AppendLine(); details.AppendLine("Saved item link"); details.AppendLine(link);
        }
        details.AppendLine();
        details.Append("Select JSON tree to inspect every exported field.");
        return details.ToString();
    }
}
