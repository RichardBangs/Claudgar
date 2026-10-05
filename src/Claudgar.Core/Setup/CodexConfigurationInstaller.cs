using System.Text;
using System.Text.Json;

namespace Claudgar.Core.Setup;

public sealed record CodexConfigMergeResult(bool Succeeded, bool Changed, string Content, string Message);

/// <summary>Appends a dedicated server or updates its exact managed block; preserves all other TOML bytes.</summary>
public sealed class CodexConfigurationInstaller
{
    private const string BeginMarker = "# BEGIN CLAUDGAR MANAGED MCP";
    private const string EndMarker = "# END CLAUDGAR MANAGED MCP";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static string DefaultConfigPath
    {
        get
        {
            var configuredHome = Environment.GetEnvironmentVariable("CODEX_HOME");
            var home = string.IsNullOrWhiteSpace(configuredHome)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex")
                : configuredHome;
            return Path.Combine(Path.GetFullPath(home), "config.toml");
        }
    }

    public InstallationResult Register(int port, string? configFilePath = null)
    {
        var path = configFilePath ?? DefaultConfigPath;
        try
        {
            SafeFiles.RejectReparsePoints(path);
            var original = File.Exists(path) ? SafeFiles.ReadLimited(path, 4 * 1024 * 1024) : null;
            var text = original is null ? "" : Utf8.GetString(original);
            var merged = Merge(text, port);
            if (!merged.Succeeded || !merged.Changed) return new(merged.Succeeded, false, merged.Message, path);
            var current = File.Exists(path) ? SafeFiles.ReadLimited(path, 4 * 1024 * 1024) : null;
            if ((original is null) != (current is null) || (original is not null && !original.AsSpan().SequenceEqual(current)))
                return new(false, false, "Codex configuration changed during setup. It was preserved; try setup again.", path);
            SafeFiles.WriteAtomic(path, Utf8.GetBytes(merged.Content), backup: true);
            return new(true, true, merged.Message, path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or DecoderFallbackException)
        {
            return new(false, false, "Codex configuration was preserved. Setup could not finish: " + error.Message, path);
        }
    }

    public CodexConfigMergeResult Merge(string text, int port)
    {
        if (port is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        try
        {
            var statements = TomlStatements.Scan(text);
            var currentTable = Array.Empty<string>();
            var serverHeaders = new List<TomlStatements.Statement>();
            var serverEntries = new Dictionary<string, TomlStatements.Statement>(StringComparer.Ordinal);
            bool conflict = false;
            foreach (var statement in statements)
            {
                var value = statement.Text.TrimStart('\uFEFF');
                if (value.Length == 0) continue;
                if (value.StartsWith('['))
                {
                    bool array = value.StartsWith("[[", StringComparison.Ordinal);
                    var suffix = array ? "]]" : "]";
                    if (!value.EndsWith(suffix, StringComparison.Ordinal)) throw new FormatException("A TOML table header is invalid.");
                    currentTable = TomlStatements.KeyPath(value[(array ? 2 : 1)..^(array ? 2 : 1)]).ToArray();
                    if (IsServer(currentTable))
                    {
                        if (array || currentTable.Length != 2) conflict = true;
                        else serverHeaders.Add(statement);
                    }
                    continue;
                }
                var equals = TomlStatements.AssignmentSeparator(value);
                if (equals < 1) throw new FormatException("A TOML setting is invalid.");
                var key = TomlStatements.KeyPath(value[..equals]);
                var absolute = currentTable.Concat(key).ToArray();
                if (absolute.Length == 1 && absolute[0] == "mcp_servers") conflict = true; // An inline parent cannot be extended.
                if (IsServer(absolute))
                {
                    if (!IsServer(currentTable) || currentTable.Length != 2 || key.Count != 1 ||
                        !serverEntries.TryAdd(key[0], statement)) conflict = true;
                }
            }
            if (conflict || serverHeaders.Count > 1)
                return Failure(text, "An existing Claudgar or inline MCP configuration cannot be safely updated. It was preserved.");

            string newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var body = Body(port, newline);
            var newBlock = BeginMarker + newline + body + EndMarker + newline;
            var begin = FindMarker(text, statements, BeginMarker);
            var end = FindMarker(text, statements, EndMarker);
            if (begin is not null || end is not null)
            {
                if (begin is null || end is null || begin.End > end.Start || serverHeaders.Count != 1 ||
                    serverHeaders[0].Start < begin.End || serverHeaders[0].End > end.Start)
                    return Failure(text, "The Claudgar managed MCP block was changed or is incomplete. It was preserved.");
                var previousBody = text[begin.End..end.Start];
                if (!TryManagedPort(previousBody, newline, out var previousPort))
                    return Failure(text, "The Claudgar managed MCP block contains user changes. They were preserved.");
                if (previousPort == port) return new(true, false, text, "Claudgar MCP is already registered.");
                var updated = text[..begin.Start] + newBlock + text[end.End..];
                return new(true, true, updated, "Claudgar MCP connection updated. Restart Codex to load the change.");
            }

            if (serverHeaders.Count == 1)
            {
                if (serverEntries.TryGetValue("url", out var urlStatement) &&
                    ParseValue(urlStatement) == "http://127.0.0.1:" + port + "/mcp" &&
                    !serverEntries.ContainsKey("command") &&
                    (!serverEntries.TryGetValue("enabled", out var enabled) || enabled.Text[(TomlStatements.AssignmentSeparator(enabled.Text) + 1)..].Trim() == "true"))
                    return new(true, false, text, "An existing Claudgar MCP connection is already configured; it was preserved.");
                return Failure(text, "A user-managed MCP server named claudgar already exists. It was preserved; use the app's local URL in that connection.");
            }
            var separator = text.Length == 0 ? "" : (text.EndsWith('\n') ? newline : newline + newline);
            return new(true, true, text + separator + newBlock, "Claudgar MCP registered. Restart Codex to load the connection.");
        }
        catch (Exception error) when (error is FormatException or JsonException or ArgumentException)
        {
            return Failure(text, "Codex configuration could not be safely interpreted and was preserved: " + error.Message);
        }
    }

    private static bool IsServer(IReadOnlyList<string> path) => path.Count >= 2 && path[0] == "mcp_servers" && path[1] == "claudgar";
    private static string Body(int port, string newline) => "[mcp_servers.claudgar]" + newline +
        "url = \"http://127.0.0.1:" + port + "/mcp\"" + newline + "enabled = true" + newline;
    private static CodexConfigMergeResult Failure(string text, string message) => new(false, false, text, message);

    private static TomlStatements.Statement? FindMarker(string text, IReadOnlyList<TomlStatements.Statement> statements, string marker)
    {
        var matches = statements.Where(statement => statement.Text.Length == 0 && text[statement.Start..statement.End].Trim() == marker).ToArray();
        if (matches.Length > 1) throw new FormatException("The managed MCP marker occurs more than once.");
        return matches.SingleOrDefault();
    }

    private static bool TryManagedPort(string body, string newline, out int port)
    {
        port = 0;
        var statements = TomlStatements.Scan(body).Where(statement => statement.Text.Length != 0).ToArray();
        if (statements.Length != 3 || statements[0].Text != "[mcp_servers.claudgar]") return false;
        var value = ParseValue(statements[1]);
        if (value is null || !Uri.TryCreate(value, UriKind.Absolute, out var url) || url.Scheme != "http" ||
            url.Host != "127.0.0.1" || url.AbsolutePath != "/mcp" || url.Query.Length != 0 || url.Fragment.Length != 0 || url.UserInfo.Length != 0 ||
            url.Port is < 1024 or > 65535) return false;
        port = url.Port;
        return body == Body(port, newline);
    }

    private static string? ParseValue(TomlStatements.Statement statement)
    {
        var equals = TomlStatements.AssignmentSeparator(statement.Text);
        if (equals < 0) return null;
        var value = statement.Text[(equals + 1)..].Trim();
        if (value.Length < 2) return null;
        if (value[0] == '\'' && value[^1] == '\'') return value[1..^1];
        return value[0] == '"' && value[^1] == '"' ? JsonSerializer.Deserialize<string>(value) : null;
    }
}
