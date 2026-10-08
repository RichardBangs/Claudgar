using System.Globalization;
using System.Text.Json.Nodes;

namespace Claudgar.App.Browser;

/// <summary>Small, human-readable summaries of the selected saved character.</summary>
internal static class AssistantHomePresentation
{
    public static IReadOnlyList<string> Suggestions { get; } = Array.AsReadOnly(new[]
    {
        "Which areas should I quest next?",
        "What quests do I need for Deadmines?",
        "How can I improve my gear?"
    });

    public static string Heading(JsonObject? snapshot)
    {
        var name = Identity(snapshot)?["name"]?.ToString();
        return string.IsNullOrWhiteSpace(name) ? "Ask about your character" : $"Ask about {name}";
    }

    public static string BuildQuestion(JsonObject? snapshot, string question)
    {
        var identity = Identity(snapshot);
        var name = identity?["name"]?.ToString();
        var realm = identity?["realm"]?.ToString();
        if (string.IsNullOrWhiteSpace(name))
            return $"Use Claudgar to check my saved character. {question}";
        var selected = $"Use Claudgar to check my saved character \"{OneLine(name)}\"";
        if (!string.IsNullOrWhiteSpace(realm)) selected += $" on realm \"{OneLine(realm)}\"";
        return selected + ". " + question;
    }

    public static AssistantSnapshotStatus Status(JsonObject? snapshot)
    {
        if (Identity(snapshot) is null)
            return new("Select a saved character to get started.", false, false);

        var freshness = snapshot?["freshness"] as JsonObject;
        var stale = snapshot?["exportState"]?.ToString() == "stale" ||
            Boolean(freshness?["cached"]) || Boolean(freshness?["isStale"]);
        var coverage = (snapshot?["coverage"] as JsonObject)?.Select(pair => pair.Value?.ToString()).ToArray() ??
            (snapshot?["sections"] as JsonObject)?.Select(pair => pair.Value?["status"]?.ToString()).ToArray() ?? [];
        var partial = coverage.Length == 0 || coverage.Any(value => value != "complete");
        var observed = Integer(freshness?["observedAt"]) ??
            Integer(snapshot?["sections"]?["character"]?["observedAt"]);
        var saved = "Save time unavailable";
        if (observed is > 0)
        {
            try
            {
                var date = DateTimeOffset.FromUnixTimeSeconds(observed.Value).ToLocalTime();
                saved = "Saved " + date.ToString("d MMM, HH:mm", CultureInfo.CurrentCulture);
            }
            catch (ArgumentOutOfRangeException) { }
        }
        var text = saved + (partial ? " · Partial data" : "") + (stale ? " · Older saved data" : "");
        return new(text, stale || partial, true);
    }

    private static JsonObject? Identity(JsonObject? snapshot) =>
        snapshot?["sections"]?["character"]?["data"] as JsonObject ?? snapshot?["character"] as JsonObject;

    private static long? Integer(JsonNode? value) =>
        value is JsonValue scalar && scalar.TryGetValue<long>(out var number) ? number : null;

    private static bool Boolean(JsonNode? value) =>
        value is JsonValue scalar && scalar.TryGetValue<bool>(out var flag) && flag;

    private static string OneLine(string value) => value.Replace('\r', ' ').Replace('\n', ' ').Replace('"', '\u2019');
}

internal sealed record AssistantSnapshotStatus(string Text, bool Warning, bool HasCharacter);
