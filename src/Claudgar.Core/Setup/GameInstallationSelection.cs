namespace Claudgar.Core.Setup;

/// <summary>Preserves the selected install roots when moving from the beta to the released client.</summary>
public sealed record GameInstallationSelection(
    IReadOnlyList<GameInstallation> Installations,
    IReadOnlyList<string> GameDirectories,
    bool RequiresChoice)
{
    public static GameInstallationSelection Resolve(
        IEnumerable<GameInstallation> discovered, IEnumerable<string> savedDirectories)
    {
        var found = discovered.DistinctBy(game => game.GameDirectory, StringComparer.OrdinalIgnoreCase).ToArray();
        var saved = savedDirectories.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (saved.Length == 0)
            return found.Length == 1
                ? new(found, [found[0].GameDirectory], false)
                : new([], [], found.Length > 1);

        var selected = new List<GameInstallation>();
        var updatedDirectories = new List<string>();
        foreach (var directory in saved)
        {
            var candidate = ReleaseDirectory(directory);
            var installation = found.FirstOrDefault(game =>
                string.Equals(game.GameDirectory, candidate, StringComparison.OrdinalIgnoreCase));
            if (installation is null)
            {
                // Keep unavailable choices so health checks request a folder instead of switching installs.
                updatedDirectories.Add(directory);
                continue;
            }
            selected.Add(installation);
            updatedDirectories.Add(installation.GameDirectory);
        }
        return new(selected.DistinctBy(game => game.GameDirectory, StringComparer.OrdinalIgnoreCase).ToArray(),
            updatedDirectories.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), false);
    }

    private static string? ReleaseDirectory(string directory)
    {
        try
        {
            var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
            var name = Path.GetFileName(path);
            if (string.Equals(name, GameDiscovery.ClientDirectoryName, StringComparison.OrdinalIgnoreCase)) return path;
            if (string.Equals(name, "_classic_beta_", StringComparison.OrdinalIgnoreCase))
                path = Path.GetDirectoryName(path)!;
            return Path.Combine(path, GameDiscovery.ClientDirectoryName);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or IOException)
        {
            return null;
        }
    }
}
