namespace Claudgar.Core.Setup;

/// <summary>One Claude Desktop configuration file and the kind of installation that reads it.</summary>
public sealed record ClaudeDesktopConfigLocation(string Path, bool IsStorePackage)
{
    public string Label => IsStorePackage ? "Claude Desktop (Microsoft Store)" : "Claude Desktop";
}

/// <summary>
/// Finds Claude Desktop configuration folders without changing anything. The classic installer reads
/// %APPDATA%\Claude; the Microsoft Store (MSIX) app reads a virtualised copy under its package LocalCache.
/// </summary>
public sealed class ClaudeDesktopConfigLocator(string? roamingAppData = null, string? localAppData = null)
{
    public const string ConfigFileName = "claude_desktop_config.json";
    private const string StorePackagePattern = "Claude_*";

    private string RoamingAppData => roamingAppData ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private string LocalAppData => localAppData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string ClassicConfigPath(string roamingAppData) => Path.Combine(roamingAppData, "Claude", ConfigFileName);

    /// <summary>Every existing Claude Desktop configuration folder, classic first, then Store packages by name.</summary>
    public IReadOnlyList<ClaudeDesktopConfigLocation> Find()
    {
        var locations = new List<ClaudeDesktopConfigLocation>();
        var classic = ClassicConfigPath(RoamingAppData);
        if (Directory.Exists(Path.GetDirectoryName(classic))) locations.Add(new(classic, false));
        foreach (var configDirectory in StoreConfigDirectories())
            locations.Add(new(Path.Combine(configDirectory, ConfigFileName), true));
        return locations;
    }

    private IEnumerable<string> StoreConfigDirectories()
    {
        var packages = Path.Combine(LocalAppData, "Packages");
        string[] candidates;
        try { candidates = Directory.Exists(packages) ? Directory.GetDirectories(packages, StorePackagePattern) : []; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { candidates = []; }
        return candidates.Order(StringComparer.OrdinalIgnoreCase)
            .Select(package => Path.Combine(package, "LocalCache", "Roaming", "Claude"))
            .Where(Directory.Exists);
    }
}
