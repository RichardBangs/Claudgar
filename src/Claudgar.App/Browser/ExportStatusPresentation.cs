using System.Text.Json.Nodes;

namespace Claudgar.App.Browser;

/// <summary>Turns saved-export outcomes into useful recovery guidance.</summary>
internal static class ExportStatusPresentation
{
    public static string Describe(JsonObject result)
    {
        var count = (result["characters"] as JsonArray)?.Count ?? 0;
        var state = result["exportState"]?.ToString() ?? "no_export";
        var summary = $"{count} saved character(s) · Updates automatically · Export: {state.Replace('_', ' ')}";
        var issues = (result["issues"] as JsonArray)?.OfType<JsonObject>().Select(issue => issue["message"]?.ToString())
            .Where(message => !string.IsNullOrWhiteSpace(message)).Distinct().Take(3).ToArray() ?? [];
        if (issues.Length > 0)
            return summary + "\n" + string.Join(" ", issues) + " Reload or log out in-game to save a fresh export; use Repair setup if the problem persists.";
        if (count == 0)
            return summary + "\nEnable the Claudgar addon, enter the world, then click its face beside the minimap or reload/log out to save your first export.";
        return summary;
    }
}
