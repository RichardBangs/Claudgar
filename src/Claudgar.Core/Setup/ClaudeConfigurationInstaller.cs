using System.Globalization;
using System.Text.Json.Nodes;

namespace Claudgar.Core.Setup;

/// <summary>Owns only the claudgar stdio server in one Claude Desktop JSON file; unrelated settings are retained.</summary>
public sealed class ClaudeConfigurationInstaller
{
    private readonly OwnedJsonServerEntry entry = new(new JsonEntryClient("Claude Desktop",
        "Fully quit Claude Desktop from the tray and reopen it to load Claudgar.", 4 * 1024 * 1024));

    public static string DefaultConfigPath => ClaudeDesktopConfigLocator.ClassicConfigPath(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

    public InstallationResult Register(string helperExecutable, int port, string? configFilePath = null) =>
        WithEntry(helperExecutable, port, configFilePath, entry.Register);

    public ClaudeConfigMergeResult Merge(string text, string helperExecutable, int port,
        string? ownedEntryHash = null, string? pendingEntryHash = null) =>
        entry.Merge(text, Entry(helperExecutable, port), ownedEntryHash, pendingEntryHash);

    public InstallationResult Check(string helperExecutable, int port, string? configFilePath = null) =>
        WithEntry(helperExecutable, port, configFilePath, entry.Check);

    private static InstallationResult WithEntry(string helperExecutable, int port, string? configFilePath,
        Func<string, JsonObject, InstallationResult> action)
    {
        var path = configFilePath ?? DefaultConfigPath;
        try { return action(path, Entry(helperExecutable, port)); }
        catch (ArgumentException error)
        {
            return new(false, false, "Claude Desktop connection could not be prepared: " + error.Message, path);
        }
    }

    private static JsonObject Entry(string executable, int port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        if (!Path.IsPathFullyQualified(executable)) throw new ArgumentException("The Claude helper path must be absolute.", nameof(executable));
        if (port is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        return new JsonObject
        {
            ["command"] = Path.GetFullPath(executable), ["args"] = new JsonArray("--port", port.ToString(CultureInfo.InvariantCulture))
        };
    }
}
