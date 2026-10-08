using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Claudgar.Core.Setup;

public sealed record ClaudeConfigMergeResult(bool Succeeded, bool Changed, string Content, string Message, string? EntryHash);

/// <summary>Client-specific wording for an owned JSON server entry.</summary>
internal sealed record JsonEntryClient(string Name, string ReloadAdvice, int MaximumBytes);

/// <summary>
/// Owns only <c>mcpServers.claudgar</c> in a chat client's JSON configuration. Unrelated settings are kept,
/// user-managed or changed entries are never adopted, and a hash journal beside the file records ownership.
/// </summary>
internal sealed class OwnedJsonServerEntry(JsonEntryClient client)
{
    public const string ServerName = "claudgar";
    private const string OwnershipSuffix = ".claudgar-owned-entry.json";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    // Configuration files are not HTML; keep non-ASCII text and punctuation readable when rewriting.
    private static readonly JsonSerializerOptions ConfigOptions = new()
    {
        WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public InstallationResult Register(string configFilePath, JsonObject desired)
    {
        var path = Path.GetFullPath(configFilePath);
        var ownershipPath = path + OwnershipSuffix;
        bool changed = false;
        try
        {
            SafeFiles.RejectReparsePoints(path);
            SafeFiles.RejectReparsePoints(ownershipPath);
            var original = ReadOptional(path);
            var originalOwnership = ReadOptional(ownershipPath);
            var ownership = ReadOwnership(originalOwnership);
            var text = original is null ? "" : Utf8.GetString(original).TrimStart('﻿');
            if (original is not null && string.IsNullOrWhiteSpace(text))
                return new(false, false, $"{client.Name}'s existing configuration is empty or malformed. It was preserved; repair it before running setup again.", path);
            var merged = Merge(text, desired, ownership?.EntryHash, ownership?.PendingEntryHash);
            if (!merged.Succeeded || !merged.Changed) return new(merged.Succeeded, false, merged.Message, path);

            // Journal the exact intended entry before writing configuration. If setup stops between
            // these writes, a later run can recognize our own pending entry without adopting edits.
            var pending = new EntryOwnership { EntryHash = ownership?.EntryHash, PendingEntryHash = merged.EntryHash };
            EnsureUnchanged(path, original);
            EnsureUnchanged(ownershipPath, originalOwnership);
            SafeFiles.WriteAtomic(ownershipPath, JsonSerializer.SerializeToUtf8Bytes(pending, JsonOptions), backup: true);
            changed = true;
            // Re-read immediately before replacement: chat clients may rewrite their own file at any time.
            EnsureUnchanged(path, original);
            SafeFiles.WriteAtomic(path, Utf8.GetBytes(merged.Content), backup: true);
            SafeFiles.WriteAtomic(ownershipPath, JsonSerializer.SerializeToUtf8Bytes(
                new EntryOwnership { EntryHash = merged.EntryHash }, JsonOptions), backup: true);
            return new(true, true, merged.Message, path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
            DecoderFallbackException or JsonException or NotSupportedException)
        {
            return new(false, changed, $"{client.Name} configuration was preserved where possible. Setup could not finish: " + error.Message, path);
        }
    }

    public ClaudeConfigMergeResult Merge(string text, JsonObject desired, string? ownedEntryHash, string? pendingEntryHash)
    {
        try
        {
            var root = ParseObject(text);
            if (root.TryGetPropertyValue("mcpServers", out var existingServers) && existingServers is not JsonObject)
                return Failure(text, $"{client.Name}'s mcpServers setting is not an object. It was preserved.");
            var servers = root["mcpServers"] as JsonObject;
            var hash = HashEntry(desired);
            if (servers?.TryGetPropertyValue(ServerName, out var existing) == true)
            {
                var existingHash = existing is null ? null : HashEntry(existing);
                if (JsonNode.DeepEquals(existing, desired))
                    return new(true, false, text, $"{client.Name} connection is configured. {client.ReloadAdvice}", hash);
                if (existingHash is null || (existingHash != ownedEntryHash && existingHash != pendingEntryHash))
                    return Failure(text, $"A user-managed or changed {client.Name} server named claudgar exists. It was preserved; rename that entry or configure the bundled connection manually.");
            }
            if (servers is null) root["mcpServers"] = servers = new JsonObject();
            servers[ServerName] = desired.DeepClone();
            return new(true, true, root.ToJsonString(ConfigOptions) + Environment.NewLine,
                $"{client.Name} connection configured. {client.ReloadAdvice}", hash);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException)
        {
            return Failure(text, $"{client.Name} configuration could not be safely interpreted and was preserved: " + error.Message);
        }
    }

    public InstallationResult Check(string configFilePath, JsonObject desired)
    {
        var path = Path.GetFullPath(configFilePath);
        try
        {
            SafeFiles.RejectReparsePoints(path);
            if (!File.Exists(path))
                return new(false, false, $"{client.Name} connection has not been configured. Run setup repair. {client.ReloadAdvice}", path);
            var root = ParseObject(Utf8.GetString(SafeFiles.ReadLimited(path, client.MaximumBytes)).TrimStart('﻿'));
            var matches = root["mcpServers"] is JsonObject servers && JsonNode.DeepEquals(servers[ServerName], desired);
            return new(matches, false, matches
                ? $"{client.Name} connection is configured. Keep Claudgar running. {client.ReloadAdvice}"
                : $"{client.Name} connection differs from this app's setup. Run setup repair; user changes will be preserved.", path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or DecoderFallbackException or JsonException)
        {
            return new(false, false, $"{client.Name} connection could not be checked: " + error.Message, path);
        }
    }

    private static JsonObject ParseObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new JsonObject();
        using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 64 });
        RejectDuplicateKeys(document.RootElement);
        return JsonNode.Parse(text) as JsonObject ?? throw new JsonException("The root must be a JSON object.");
    }

    private static void RejectDuplicateKeys(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in node.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException("A JSON property occurs more than once: " + property.Name);
                RejectDuplicateKeys(property.Value);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
            foreach (var child in node.EnumerateArray()) RejectDuplicateKeys(child);
    }

    private static string HashEntry(JsonNode entry) => SafeFiles.Hash(Utf8.GetBytes(entry.ToJsonString()));
    private static ClaudeConfigMergeResult Failure(string text, string message) => new(false, false, text, message, null);
    private byte[]? ReadOptional(string path) => File.Exists(path) ? SafeFiles.ReadLimited(path, client.MaximumBytes) : null;

    private static EntryOwnership? ReadOwnership(byte[]? content)
    {
        if (content is null) return null;
        var manifest = JsonSerializer.Deserialize<EntryOwnership>(content) ?? throw new JsonException("Connection ownership metadata is invalid.");
        if (manifest.Owner != "Claudgar" || manifest.SchemaVersion != 1 || !ValidHash(manifest.EntryHash) || !ValidHash(manifest.PendingEntryHash))
            throw new JsonException("Connection ownership metadata is unsupported.");
        return manifest;
    }

    private static bool ValidHash(string? value) => value is null || (value.Length == 64 && value.All(Uri.IsHexDigit));

    private void EnsureUnchanged(string path, byte[]? original)
    {
        SafeFiles.RejectReparsePoints(path);
        var current = ReadOptional(path);
        if ((original is null) != (current is null) || (original is not null && !original.AsSpan().SequenceEqual(current)))
            throw new IOException($"{client.Name} configuration changed during setup. Try setup again.");
    }

    private sealed record EntryOwnership
    {
        public string Owner { get; init; } = "Claudgar";
        public int SchemaVersion { get; init; } = 1;
        public string? EntryHash { get; init; }
        public string? PendingEntryHash { get; init; }
    }
}
