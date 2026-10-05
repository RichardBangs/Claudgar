using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Claudgar.Core.Data;

namespace Claudgar.Core.Exports;

/// <summary>
/// Discovers account SavedVariables beneath configured Forever installations and rereads them
/// for every query. The last valid snapshot survives missing, locked, or incomplete saves.
/// </summary>
public sealed class ExportRepository
{
    private const long MaximumFileBytes = 32 * 1024 * 1024;
    private readonly Func<IReadOnlyList<string>> gameDirectoryProvider;
    private readonly object gate = new();
    private readonly Dictionary<string, SourceState> sources = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ExportIssue> discoveryIssues = [];
    private long lastReadAt;

    public ExportRepository(Func<IReadOnlyList<string>> gameDirectoryProvider)
    {
        this.gameDirectoryProvider = gameDirectoryProvider ?? throw new ArgumentNullException(nameof(gameDirectoryProvider));
    }

    public JsonObject ListCharacters()
    {
        lock (gate)
        {
            Refresh();
            var list = new JsonArray();
            foreach (var character in EnumerateCharacters().OrderBy(record =>
                         SnapshotValidator.ReadString(record.Character["sections"]?["character"]?["data"]?["name"]), StringComparer.OrdinalIgnoreCase))
                list.Add(CreateIdentity(character));
            return new JsonObject
            {
                ["schemaVersion"] = ExportContract.SchemaVersion, ["exportState"] = CurrentState(),
                ["readAt"] = lastReadAt, ["characters"] = list, ["issues"] = CreateIssues()
            };
        }
    }

    public JsonObject GetCharacter(string characterId)
    {
        lock (gate)
        {
            Refresh();
            var character = FindCharacter(characterId);
            if (character is null) return NotFound();
            var result = CreateIdentity(character);
            var sections = character.Character["sections"]!.DeepClone().AsObject();
            foreach (var (_, rawSection) in sections)
                if (rawSection is JsonObject section) section["freshness"] = CreateFreshness(character.Source, section["observedAt"]);
            result["sections"] = sections;
            return result;
        }
    }

    public JsonObject GetSection(string characterId, string sectionName)
    {
        lock (gate)
        {
            Refresh();
            if (!ExportContract.SectionNames.Contains(sectionName, StringComparer.Ordinal))
                return new JsonObject { ["error"] = new JsonObject { ["code"] = "unknown_section", ["message"] = "Choose a documented character section." } };
            var character = FindCharacter(characterId);
            if (character is null) return NotFound();
            var section = character.Character["sections"]![sectionName]!.AsObject();
            var result = CreateIdentity(character);
            result["section"] = sectionName;
            result["coverage"] = section["status"]!.DeepClone();
            result["observedAt"] = section["observedAt"]?.DeepClone();
            result["data"] = section["data"]?.DeepClone();
            result["warnings"] = section["warnings"]!.DeepClone();
            if (section["error"] is { } error) result["error"] = error.DeepClone();
            result["freshness"] = CreateFreshness(character.Source, section["observedAt"]);
            return result;
        }
    }

    public ExportHealth GetHealth()
    {
        lock (gate)
        {
            Refresh();
            return new ExportHealth(CurrentState(), EnumerateCharacters().Count(),
                sources.Values.Count(source => source.Snapshot is not null), AllIssues().Count(), lastReadAt);
        }
    }

    private void Refresh()
    {
        lastReadAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        discoveryIssues.Clear();
        var activeInstallations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<string> configuredDirectories;
        try { configuredDirectories = gameDirectoryProvider(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            discoveryIssues.Add(new ExportIssue(null, null, "configuration_unavailable", "The configured game folders could not be read."));
            foreach (var source in sources.Values) source.Issue = new ExportIssue(source.InstallationId, source.AccountId,
                "configuration_unavailable", "The game folder configuration could not be read; showing the last valid export.");
            return;
        }
        foreach (var configuredDirectory in configuredDirectories)
        {
            string gameDirectory;
            try { gameDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredDirectory)); }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                discoveryIssues.Add(new ExportIssue(null, null, "invalid_game_directory", "A configured game folder is invalid."));
                continue;
            }
            if (!activeInstallations.Add(gameDirectory)) continue;
            var installationId = OpaqueId("installation", gameDirectory.ToUpperInvariant());
            var accountRoot = Path.Combine(gameDirectory, "WTF", "Account");
            if (!Directory.Exists(accountRoot)) continue;
            try
            {
                var accountOptions = new EnumerationOptions
                {
                    RecurseSubdirectories = false, IgnoreInaccessible = false,
                    AttributesToSkip = FileAttributes.ReparsePoint
                };
                foreach (var accountDirectory in Directory.EnumerateDirectories(accountRoot, "*", accountOptions))
                {
                    var accountId = OpaqueId("account", accountDirectory.ToUpperInvariant());
                    try
                    {
                        var options = new EnumerationOptions
                        {
                            RecurseSubdirectories = true, IgnoreInaccessible = false,
                            AttributesToSkip = FileAttributes.ReparsePoint, MaxRecursionDepth = 8,
                            MatchCasing = MatchCasing.CaseInsensitive
                        };
                        foreach (var exportPath in Directory.EnumerateFiles(accountDirectory, ExportContract.ExportFileName, options))
                        {
                            if (!string.Equals(Path.GetFileName(Path.GetDirectoryName(exportPath)), "SavedVariables", StringComparison.OrdinalIgnoreCase)) continue;
                            var fullPath = Path.GetFullPath(exportPath);
                            discovered.Add(fullPath);
                            if (!sources.TryGetValue(fullPath, out var source))
                            {
                                source = new SourceState(fullPath, gameDirectory, installationId, accountId);
                                sources.Add(fullPath, source);
                            }
                            ReadSource(source);
                        }
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        discoveryIssues.Add(new ExportIssue(installationId, accountId, "account_directory_unreadable", "An account's SavedVariables folders could not be fully read."));
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                discoveryIssues.Add(new ExportIssue(installationId, null, "game_directory_unreadable", "An installation's SavedVariables folders could not be fully read."));
            }
        }
        // Explicitly removing an installation from configuration removes its data. A missing
        // file in a still-configured installation instead remains visible as a stale snapshot.
        foreach (var source in sources.Values.ToArray())
        {
            if (!activeInstallations.Contains(source.GameDirectory)) { sources.Remove(source.Path); continue; }
            if (!discovered.Contains(source.Path))
                source.Issue = new ExportIssue(source.InstallationId, source.AccountId, "export_missing",
                    source.Snapshot is null ? "An export file is missing." : "The export file is missing; showing the last valid snapshot.");
        }
    }

    private void ReadSource(SourceState source)
    {
        string code = "invalid_export";
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var before = new FileInfo(source.Path);
                before.Refresh();
                if (!before.Exists) throw new FileNotFoundException();
                var length = before.Length;
                var modified = before.LastWriteTimeUtc;
                if (length > MaximumFileBytes) throw new ExportFormatException("The export exceeds the supported size.");
                string contents;
                using (var stream = new FileStream(source.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true))
                {
                    var buffer = new char[8192];
                    var text = new StringBuilder();
                    int count;
                    while ((count = reader.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        if (text.Length + count > LuaSavedVariablesParser.MaximumCharacters)
                            throw new ExportFormatException("The export exceeds the supported size.");
                        text.Append(buffer, 0, count);
                    }
                    contents = text.ToString();
                }
                var after = new FileInfo(source.Path);
                after.Refresh();
                if (!after.Exists || after.Length != length || after.LastWriteTimeUtc != modified)
                    throw new IOException("The export changed while being read.");
                var snapshot = new SnapshotValidator().Validate(new LuaSavedVariablesParser().Parse(contents));
                source.Snapshot = snapshot;
                source.LastSuccessfulReadAt = lastReadAt;
                source.FileModifiedAt = new DateTimeOffset(modified, TimeSpan.Zero).ToUnixTimeSeconds();
                source.Issue = null;
                return;
            }
            catch (ExportFormatException) { code = "invalid_export"; }
            catch (DecoderFallbackException) { code = "invalid_export_encoding"; }
            catch (FileNotFoundException) { code = "export_missing"; }
            catch (DirectoryNotFoundException) { code = "export_missing"; }
            catch (UnauthorizedAccessException) { code = "export_unreadable"; }
            catch (IOException) { code = "export_unreadable"; }
            if (attempt < 2) Thread.Sleep(35);
        }
        source.Issue = new ExportIssue(source.InstallationId, source.AccountId, code,
            source.Snapshot is null
                ? "The export could not be safely read. Reload or log out of WoW Forever to save a new snapshot."
                : "The latest export could not be safely read; showing the last valid snapshot. Reload or log out to save updated data.");
    }

    private IEnumerable<CharacterRecord> EnumerateCharacters()
    {
        foreach (var source in sources.Values)
            if (source.Snapshot is not null)
                foreach (var (key, character) in source.Snapshot.Characters)
                    yield return new CharacterRecord(OpaqueId("character", source.Path.ToUpperInvariant() + "\n" + key), source, character);
    }

    private CharacterRecord? FindCharacter(string characterId) =>
        EnumerateCharacters().FirstOrDefault(character => string.Equals(character.Id, characterId, StringComparison.Ordinal));

    private JsonObject CreateIdentity(CharacterRecord record)
    {
        var sections = record.Character["sections"]!.AsObject();
        var coverage = new JsonObject();
        foreach (var (name, section) in sections) coverage[name] = section!["status"]!.DeepClone();
        return new JsonObject
        {
            ["characterId"] = record.Id, ["installationId"] = record.Source.InstallationId,
            ["accountId"] = record.Source.AccountId, ["client"] = record.Source.Snapshot!.Client.DeepClone(),
            ["character"] = sections["character"]!["data"]?.DeepClone(), ["coverage"] = coverage,
            ["freshness"] = CreateFreshness(record.Source, sections["character"]!["observedAt"])
        };
    }

    private JsonObject CreateFreshness(SourceState source, JsonNode? observedAt)
    {
        var freshness = new JsonObject
        {
            ["source"] = "saved_variables", ["cached"] = source.Issue is not null,
            ["readAt"] = lastReadAt, ["lastSuccessfulReadAt"] = source.LastSuccessfulReadAt,
            ["exportFileModifiedAt"] = source.FileModifiedAt, ["observedAt"] = observedAt?.DeepClone(),
            ["saveInstruction"] = "Reload or log out of WoW Forever to write updated data."
        };
        if (SnapshotValidator.TryInteger(observedAt, out var observed))
        {
            freshness["ageSeconds"] = Math.Max(0, lastReadAt - observed);
            if (observed > lastReadAt + 300) freshness["clockWarning"] = "The export observation time is ahead of the app clock.";
        }
        else freshness["ageSeconds"] = null;
        if (source.Issue is not null)
        {
            freshness["issueCode"] = source.Issue.Code;
            freshness["message"] = source.Issue.Message;
        }
        return freshness;
    }

    private string CurrentState()
    {
        var usable = sources.Values.Where(source => source.Snapshot is not null).ToArray();
        if (usable.Length == 0 || usable.All(source => source.Snapshot!.Characters.Count == 0))
            return AllIssues().Any() ? "failed" : "no_export";
        if (usable.All(source => source.Issue is not null)) return "stale";
        return AllIssues().Any() ? "partial" : "ready";
    }

    private IEnumerable<ExportIssue> AllIssues() => discoveryIssues.Concat(sources.Values.Select(source => source.Issue).OfType<ExportIssue>());
    private JsonArray CreateIssues()
    {
        var issues = new JsonArray();
        foreach (var issue in AllIssues())
            issues.Add(new JsonObject
            {
                ["installationId"] = issue.InstallationId, ["accountId"] = issue.AccountId,
                ["code"] = issue.Code, ["message"] = issue.Message
            });
        return issues;
    }

    private JsonObject NotFound() => new()
    {
        ["error"] = new JsonObject
        {
            ["code"] = "character_not_found", ["message"] = "Call list_characters and choose an exact characterId.",
            ["exportState"] = CurrentState(), ["issues"] = CreateIssues()
        }
    };

    private static string OpaqueId(string kind, string value) => kind + "_" +
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..32];

    private sealed class SourceState(string path, string gameDirectory, string installationId, string accountId)
    {
        public string Path { get; } = path;
        public string GameDirectory { get; } = gameDirectory;
        public string InstallationId { get; } = installationId;
        public string AccountId { get; } = accountId;
        public ValidatedExport? Snapshot { get; set; }
        public long LastSuccessfulReadAt { get; set; }
        public long FileModifiedAt { get; set; }
        public ExportIssue? Issue { get; set; }
    }
    private sealed record CharacterRecord(string Id, SourceState Source, JsonObject Character);
    private sealed record ExportIssue(string? InstallationId, string? AccountId, string Code, string Message);
}
