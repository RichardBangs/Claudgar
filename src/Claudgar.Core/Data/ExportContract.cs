namespace Claudgar.Core.Data;

/// <summary>The versioned boundary shared by the addon and the desktop reader.</summary>
public static class ExportContract
{
    public const int SchemaVersion = 1;
    public const string SavedVariableName = "ClaudgarDB";
    public const string ClientFlavor = "forever";
    public const string ExportFileName = "Claudgar.lua";

    public static IReadOnlyList<string> SectionNames { get; } =
        Array.AsReadOnly(new[] { "character", "quests", "talents", "inventory", "equipment" });

    public static IReadOnlySet<string> CoverageStatuses { get; } =
        new HashSet<string>(["complete", "partial", "unavailable", "failed"], StringComparer.Ordinal);
}
