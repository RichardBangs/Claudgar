namespace Claudgar.Core.Setup;

public sealed record SetupHealth(bool NeedsGameFolder, bool NeedsRepair, string? Message);

/// <summary>Inspects setup without changing game, addon, or chatbot files.</summary>
public sealed class SetupHealthChecker
{
    private readonly IReadOnlyDictionary<string, string> addonHashes;
    private readonly GameDiscovery discovery = new();

    public SetupHealthChecker(IReadOnlyDictionary<string, byte[]> addonFiles)
    {
        ArgumentNullException.ThrowIfNull(addonFiles);
        addonHashes = addonFiles.ToDictionary(file => file.Key, file => SafeFiles.Hash(file.Value),
            StringComparer.OrdinalIgnoreCase);
    }

    public SetupHealth Check(IEnumerable<string> gameDirectories, SetupReport? report, string? serviceError = null)
    {
        var directories = gameDirectories.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var needsGameFolder = directories.Length == 0;
        string? folderIssue = needsGameFolder ? "Choose your Forever game folder to finish setup." : null;
        string? addonIssue = null;
        foreach (var directory in directories)
        {
            var validation = discovery.Validate(directory);
            if (validation.Installation is not { } installation)
            {
                needsGameFolder = true;
                folderIssue ??= validation.Error ?? "The saved game folder is no longer available.";
                continue;
            }
            addonIssue ??= CheckAddon(installation);
        }

        var failedSetup = report?.Addons.Append(report.Codex).Append(report.Skill)
            .FirstOrDefault(result => !result.Succeeded)?.Message;
        var repairIssue = addonIssue ?? failedSetup ?? serviceError;
        return new(needsGameFolder, repairIssue is not null, folderIssue ?? repairIssue);
    }

    private string? CheckAddon(GameInstallation installation)
    {
        try
        {
            var directory = Path.Combine(installation.AddonsDirectory, "Claudgar");
            SafeFiles.RejectReparsePoints(directory);
            foreach (var (relative, expectedHash) in addonHashes)
            {
                var path = SafeFiles.UnderDirectory(directory, relative);
                SafeFiles.RejectReparsePoints(path);
                if (!File.Exists(path)) return "The Claudgar addon is missing a file. Repair setup to restore it.";
                if (SafeFiles.HashFile(path) != expectedHash)
                    return "A Claudgar addon file differs from this version. Review setup; any custom changes will be preserved.";
            }
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return "The Claudgar addon could not be checked: " + error.Message;
        }
    }
}
