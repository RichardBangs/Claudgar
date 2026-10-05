using System.Text;

namespace Claudgar.Core.Setup;

/// <summary>Coordinates explicit first-run setup. Constructing this service does not alter the machine.</summary>
public sealed class SetupService
{
    private readonly IReadOnlyDictionary<string, byte[]> addonFiles;
    private readonly string skillMarkdown;
    private readonly OwnedFilesInstaller fileInstaller = new();
    private readonly GameDiscovery discovery = new();
    private readonly CodexConfigurationInstaller codexInstaller = new();

    public SetupService(IReadOnlyDictionary<string, byte[]> addonFiles, string skillMarkdown)
    {
        ArgumentNullException.ThrowIfNull(addonFiles);
        ArgumentException.ThrowIfNullOrWhiteSpace(skillMarkdown);
        this.addonFiles = addonFiles;
        this.skillMarkdown = skillMarkdown;
    }

    public SetupReport Install(IEnumerable<GameInstallation> installations, int port,
        string? codexConfigPath = null, string? skillDirectory = null)
    {
        var addons = installations.GroupBy(installation => installation.GameDirectory, StringComparer.OrdinalIgnoreCase)
            .Select(group => InstallAddon(group.First())).ToArray();
        var skillPath = skillDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".agents", "skills", "claudgar");
        var skill = fileInstaller.Install(skillPath, new Dictionary<string, byte[]>
        {
            ["SKILL.md"] = new UTF8Encoding(false).GetBytes(skillMarkdown)
        });
        var codex = codexInstaller.Register(port, codexConfigPath);
        return new(addons, codex, skill);
    }

    public InstallationResult InstallAddon(GameInstallation installation)
    {
        // Revalidate immediately before writing, including installations loaded from old settings.
        var validation = discovery.Validate(installation.GameDirectory);
        if (validation.Installation is not { } validated)
            return new(false, false, validation.Error ?? "This game installation is not supported.", installation.GameDirectory);
        return fileInstaller.Install(Path.Combine(validated.AddonsDirectory, "Claudgar"), addonFiles);
    }
}
