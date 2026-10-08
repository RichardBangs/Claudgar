namespace Claudgar.Core.Setup;

/// <summary>The outcome for one Claude Desktop configuration file.</summary>
public sealed record ClaudeDesktopConfigResult(ClaudeDesktopConfigLocation Location, InstallationResult Result);

public sealed record ClaudeSetupResult(InstallationResult Helper, IReadOnlyList<ClaudeDesktopConfigResult> Configurations,
    bool Detected, string? AbsentMessage = null)
{
    public bool Succeeded => Helper.Succeeded && Configurations.All(config => config.Result.Succeeded);
    public bool Changed => Helper.Changed || Configurations.Any(config => config.Result.Changed);

    /// <summary>All configuration files summarised as one result; a single file keeps its own wording.</summary>
    public InstallationResult Configuration => Configurations.Count switch
    {
        0 => new(true, false, AbsentMessage ?? "Claude Desktop was not found.", ClaudeConfigurationInstaller.DefaultConfigPath),
        1 => Configurations[0].Result,
        _ => new(Configurations.All(config => config.Result.Succeeded), Configurations.Any(config => config.Result.Changed),
            string.Join(Environment.NewLine, Configurations.Select(config => $"[{config.Location.Label}] {config.Result.Message}")),
            string.Join("; ", Configurations.Select(config => config.Result.Path)))
    };
}

/// <summary>
/// Keeps optional Claude Desktop setup independent of the other clients. One native helper serves every
/// Desktop installation; each detected configuration file (classic and Microsoft Store) is registered separately.
/// </summary>
public sealed class ClaudeSetupService(byte[] helperBinary, ClaudeDesktopConfigLocator? locator = null)
{
    private const string AbsentMessage = "Claude Desktop was not found. Install Claude Desktop and run setup repair to connect its chats with local MCP support.";
    private readonly ClaudeBridgeInstaller bridge = new();
    private readonly ClaudeConfigurationInstaller configuration = new();
    private readonly ClaudeDesktopConfigLocator locator = locator ?? new();

    public ClaudeSetupResult Install(int port, string? configFilePath = null, string? helperDirectory = null)
    {
        var locations = Locations(configFilePath);
        if (locations.Count == 0) return Absent(helperDirectory);
        var helper = bridge.Install(helperBinary, helperDirectory);
        // A locked update may retain the working helper; registration remains safe and the separate
        // helper result carries the repair requirement. Missing helpers must not be registered.
        var registrable = helper.Succeeded || bridge.IsInstalledHelperOwned(helperDirectory);
        return new(helper, locations.Select(location => new ClaudeDesktopConfigResult(location, registrable
            ? configuration.Register(helper.Path, port, location.Path)
            : new InstallationResult(false, false, "Claude connection was not registered because the native helper could not be installed.", location.Path)))
            .ToArray(), Detected: true);
    }

    public ClaudeSetupResult Check(int port, string? configFilePath = null, string? helperDirectory = null)
    {
        var locations = Locations(configFilePath);
        if (locations.Count == 0) return Absent(helperDirectory);
        var helper = bridge.Check(helperBinary, helperDirectory);
        return new(helper, locations.Select(location =>
            new ClaudeDesktopConfigResult(location, configuration.Check(helper.Path, port, location.Path))).ToArray(), Detected: true);
    }

    private IReadOnlyList<ClaudeDesktopConfigLocation> Locations(string? configFilePath) =>
        configFilePath is null ? locator.Find() : [new ClaudeDesktopConfigLocation(Path.GetFullPath(configFilePath), false)];

    private static ClaudeSetupResult Absent(string? helperDirectory) =>
        new(new(true, false, AbsentMessage, Path.Combine(helperDirectory ?? ClaudeBridgeInstaller.DefaultDirectory, ClaudeBridgeInstaller.ExecutableName)),
            [], Detected: false, AbsentMessage);
}
