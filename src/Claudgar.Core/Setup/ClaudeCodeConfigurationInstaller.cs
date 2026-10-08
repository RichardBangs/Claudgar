using System.Globalization;
using System.Text.Json.Nodes;

namespace Claudgar.Core.Setup;

/// <summary>
/// Owns only the user-scope claudgar HTTP server in Claude Code's ~/.claude.json. Claude Code rewrites that
/// file often, so the shared entry owner re-reads it immediately before replacement and never merges over changes.
/// </summary>
public sealed class ClaudeCodeConfigurationInstaller
{
    // ~/.claude.json also stores per-project history and can grow far beyond Desktop's small settings file.
    private const int MaximumConfigBytes = 64 * 1024 * 1024;
    private readonly OwnedJsonServerEntry entry = new(new JsonEntryClient("Claude Code",
        "Start a new Claude Code session (or run /mcp) to load Claudgar.", MaximumConfigBytes));

    public static string DefaultConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude.json");

    public InstallationResult Register(int port, string? configFilePath = null) =>
        WithEntry(port, configFilePath, entry.Register);

    public ClaudeConfigMergeResult Merge(string text, int port, string? ownedEntryHash = null, string? pendingEntryHash = null) =>
        entry.Merge(text, Entry(port), ownedEntryHash, pendingEntryHash);

    public InstallationResult Check(int port, string? configFilePath = null) =>
        WithEntry(port, configFilePath, entry.Check);

    private static InstallationResult WithEntry(int port, string? configFilePath, Func<string, JsonObject, InstallationResult> action)
    {
        var path = configFilePath ?? DefaultConfigPath;
        try { return action(path, Entry(port)); }
        catch (ArgumentException error)
        {
            return new(false, false, "Claude Code connection could not be prepared: " + error.Message, path);
        }
    }

    private static JsonObject Entry(int port)
    {
        if (port is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        return new JsonObject
        {
            ["type"] = "http",
            ["url"] = $"http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}/mcp"
        };
    }
}
