using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Claudgar.App.Browser;

internal static partial class JsonPresentation
{
    public static string Humanize(string key)
    {
        var text = Words().Replace(key.Replace('_', ' '), "$1 $2");
        return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
    }

    public static string Summary(JsonNode? value) => value switch
    {
        null => "Not provided",
        JsonArray array => $"{array.Count:N0} entries — double-click to browse",
        JsonObject obj => $"{obj.Count} fields — double-click to browse",
        JsonValue scalar when scalar.TryGetValue<bool>(out var boolean) => boolean ? "Yes" : "No",
        _ => value.ToString()
    };

    public static string RecordName(JsonNode? value, int index)
    {
        if (value is not JsonObject record) return value is JsonValue ? value.ToString() : $"Entry {index + 1}";
        foreach (var name in new[] { "title", "name", "slotName", "configName" })
            if (record[name] is JsonValue scalar && !string.IsNullOrWhiteSpace(scalar.ToString())) return scalar.ToString();
        foreach (var id in new[] { "questId", "itemId", "spellId", "nodeId", "treeId", "bagId", "slot", "groupId", "entryId", "currencyId" })
            if (record[id] is JsonValue scalar) return $"{Humanize(id)} {scalar}";
        return $"Entry {index + 1}";
    }

    public static string Pretty(JsonNode? value) => value?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "null";

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex Words();
}
