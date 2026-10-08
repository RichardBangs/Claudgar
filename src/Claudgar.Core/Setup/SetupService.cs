using System.Text;

namespace Claudgar.Core.Setup;

/// <summary>Coordinates explicit first-run setup. Constructing this service does not alter the machine.</summary>
public sealed class SetupService
{
    private readonly IReadOnlyDictionary<string, byte[]> addonFiles;
    private readonly string skillMarkdown;
    private readonly string? appVersion;
    private readonly OwnedFilesInstaller fileInstaller = new();
    private readonly GameDiscovery discovery = new();
    private readonly CodexConfigurationInstaller codexInstaller = new();

    public SetupService(IReadOnlyDictionary<string, byte[]> addonFiles, string skillMarkdown, string? appVersion = null)
    {
        ArgumentNullException.ThrowIfNull(addonFiles);
        ArgumentException.ThrowIfNullOrWhiteSpace(skillMarkdown);
        this.addonFiles = addonFiles;
        this.skillMarkdown = skillMarkdown;
        if (appVersion is not null) ArgumentException.ThrowIfNullOrWhiteSpace(appVersion);
        this.appVersion = appVersion;
    }

    public SetupReport Install(IEnumerable<GameInstallation> installations, int port,
        string? codexConfigPath = null, string? skillDirectory = null, bool forceAddonRepair = false)
    {
        var addons = installations.GroupBy(installation => installation.GameDirectory, StringComparer.OrdinalIgnoreCase)
            .Select(group => InstallAddon(group.First(), forceAddonRepair)).ToArray();
        var skillPath = skillDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".agents", "skills", "claudgar");
        var skill = fileInstaller.Install(skillPath, new Dictionary<string, byte[]>
        {
            ["SKILL.md"] = new UTF8Encoding(false).GetBytes(skillMarkdown)
        });
        var codex = codexInstaller.Register(port, codexConfigPath);
        return new(addons, codex, skill);
    }

    public InstallationResult InstallAddon(GameInstallation installation, bool forceRepair = false)
    {
        // Revalidate immediately before writing, including installations loaded from old settings.
        var validation = discovery.Validate(installation.GameDirectory);
        if (validation.Installation is not { } validated)
            return new(false, false, validation.Error ?? "This game installation is not supported.", installation.GameDirectory);
        var result = fileInstaller.Install(Path.Combine(validated.AddonsDirectory, "Claudgar"), addonFiles,
            successfulAppVersion: appVersion, skipIfVersionCurrent: !forceRepair && appVersion is not null);
        if (result.Succeeded && result.Changed)
            result = result with { Message = "Addon updated. Reload the game UI or log out and back in to load it." };
        return result;
    }
}
