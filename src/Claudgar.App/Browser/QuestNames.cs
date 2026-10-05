using System.Text.Json.Nodes;

namespace Claudgar.App.Browser;

/// <summary>Names from the saved export only; the desktop never requests quest data from a service.</summary>
internal sealed class QuestNames
{
    private readonly Dictionary<string, string> titles = new(StringComparer.Ordinal);

    public void SetData(JsonObject? data)
    {
        titles.Clear();
        foreach (var field in new[] { "active", "completedDetails" })
            if (data?[field] is JsonArray records)
                foreach (var record in records.OfType<JsonObject>())
                    if (record["questId"] is { } id && record["title"] is JsonValue title &&
                        title.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
                        titles[id.ToString()] = text;
    }

    public string? Title(JsonNode? id) => id is not null && titles.TryGetValue(id.ToString(), out var title) ? title : null;
    public string Label(JsonNode? id) => Title(id) is { } title ? $"{title} · Quest {id}" : $"Quest {id} · Name not saved yet";
}
