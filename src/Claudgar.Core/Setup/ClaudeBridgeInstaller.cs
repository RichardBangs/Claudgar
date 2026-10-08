using System.Security.Cryptography;
using System.Text.Json;

namespace Claudgar.Core.Setup;

/// <summary>Installs the large native helper into its own owned folder without saved-export size limits.</summary>
public sealed class ClaudeBridgeInstaller
{
    public const string ExecutableName = "Claudgar.McpBridge.exe";
    public const long MaximumBinaryBytes = 256L * 1024 * 1024;
    private const string ManifestName = ".claudgar-bridge.json";
    private const string PendingName = "Claudgar.McpBridge.exe.pending";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Claudgar", "claude-bridge");

    public InstallationResult Install(byte[] binary, string? helperDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(binary);
        var directory = Path.GetFullPath(helperDirectory ?? DefaultDirectory);
        var target = Path.Combine(directory, ExecutableName);
        var manifestPath = Path.Combine(directory, ManifestName);
        var pendingPath = Path.Combine(directory, PendingName);
        bool changed = false;
        try
        {
            if (binary.Length == 0 || binary.LongLength > MaximumBinaryBytes)
                throw new IOException("The packaged Claude helper is missing or exceeds its size limit.");
            SafeFiles.RejectReparsePoints(directory);
            SafeFiles.RejectReparsePoints(target);
            SafeFiles.RejectReparsePoints(manifestPath);
            SafeFiles.RejectReparsePoints(pendingPath);
            var manifest = ReadManifest(manifestPath);
            if (manifest is null && Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
                return new(false, false, "The Claude helper folder is not owned by Claudgar. Its files were preserved; select setup repair after moving the existing folder.", target);
            manifest ??= new();
            var expected = SafeFiles.Hash(binary);
            var previous = File.Exists(target) ? HashFile(target) : null;
            if (previous is not null && previous != manifest.InstalledHash && previous != manifest.PendingHash)
                return new(false, false, "The Claude helper was changed outside Claudgar. It was preserved; restore the original helper before setup repair.", target);
            var pendingHash = File.Exists(pendingPath) ? HashFile(pendingPath) : null;
            if (pendingHash is not null && pendingHash != manifest.PendingHash && pendingHash != manifest.PreviousPendingHash)
                return new(false, false, "The staged Claude helper was changed outside Claudgar. It was preserved; remove that changed pending file before setup repair.", target);
            if (previous == expected)
            {
                if (pendingHash is not null)
                {
                    SafeFiles.RejectReparsePoints(pendingPath);
                    if (HashFile(pendingPath) != pendingHash) throw new IOException("The staged helper changed during setup. It was preserved.");
                    File.Delete(pendingPath);
                    changed = true;
                }
                if (manifest.InstalledHash != expected || manifest.PendingHash is not null)
                {
                    WriteManifest(manifestPath, new BridgeOwnership { InstalledHash = expected });
                    changed = true;
                }
                return new(true, changed, "Claude's native connection helper is up to date.", target);
            }
            // Keep a journal and verified pending payload when Claude has the stable executable open.
            // New app versions remain compatible with the working helper's six read-only tools.
            WriteManifest(manifestPath, manifest with { PendingHash = expected, PreviousPendingHash = pendingHash });
            changed = true;
            SafeFiles.WriteAtomic(pendingPath, binary, backup: false);
            if (HashFile(pendingPath) != expected) throw new IOException("The staged Claude helper did not pass verification.");
            var current = File.Exists(target) ? HashFile(target) : null;
            if (current != previous) throw new IOException("The Claude helper changed during setup. Its current bytes were preserved.");
            SafeFiles.RejectReparsePoints(target);
            SafeFiles.RejectReparsePoints(pendingPath);
            try { File.Move(pendingPath, target, overwrite: true); }
            catch (Exception error) when (previous is not null && error is IOException or UnauthorizedAccessException)
            {
                return new(false, changed, "Claude's working connection helper was preserved and its update is staged. Quit Claude Desktop completely, run Claudgar setup repair, then reopen Claude. " + error.Message, target);
            }
            WriteManifest(manifestPath, new BridgeOwnership { InstalledHash = expected });
            return new(true, true, "Claude's native connection helper is installed.", target);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or JsonException or NotSupportedException)
        {
            return new(false, changed, "Claude's connection helper could not finish setup: " + error.Message, target);
        }
    }

    public InstallationResult Check(byte[] binary, string? helperDirectory = null)
    {
        var directory = Path.GetFullPath(helperDirectory ?? DefaultDirectory);
        var target = Path.Combine(directory, ExecutableName);
        try
        {
            SafeFiles.RejectReparsePoints(target);
            SafeFiles.RejectReparsePoints(Path.Combine(directory, ManifestName));
            var manifest = ReadManifest(Path.Combine(directory, ManifestName));
            if (manifest is null || !File.Exists(target))
                return new(false, false, "Claude's connection helper is missing. Run setup repair, then quit and reopen Claude.", target);
            var actual = HashFile(target);
            var matches = actual == SafeFiles.Hash(binary) && actual == manifest.InstalledHash && manifest.PendingHash is null;
            return new(matches, false, matches ? "Claude's native connection helper is up to date."
                : "Claude's connection helper needs repair or an update. Quit Claude completely, run setup repair, then reopen Claude.", target);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or JsonException)
        {
            return new(false, false, "Claude's connection helper could not be checked: " + error.Message, target);
        }
    }

    public bool IsInstalledHelperOwned(string? helperDirectory = null)
    {
        var directory = Path.GetFullPath(helperDirectory ?? DefaultDirectory);
        var target = Path.Combine(directory, ExecutableName);
        var manifestPath = Path.Combine(directory, ManifestName);
        try
        {
            SafeFiles.RejectReparsePoints(target);
            SafeFiles.RejectReparsePoints(manifestPath);
            var manifest = ReadManifest(manifestPath);
            if (manifest is null || !File.Exists(target)) return false;
            var actual = HashFile(target);
            return actual == manifest.InstalledHash || actual == manifest.PendingHash;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or JsonException)
        {
            return false;
        }
    }

    public static string HashFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length <= 0 || stream.Length > MaximumBinaryBytes) throw new IOException("The Claude helper size is invalid.");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        long total = 0;
        int count;
        while ((count = stream.Read(buffer)) != 0)
        {
            total += count;
            if (total > MaximumBinaryBytes) throw new IOException("The Claude helper grew beyond its size limit.");
            hash.AppendData(buffer, 0, count);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static BridgeOwnership? ReadManifest(string path)
    {
        if (!File.Exists(path)) return null;
        var manifest = JsonSerializer.Deserialize<BridgeOwnership>(SafeFiles.ReadLimited(path, 64 * 1024))
            ?? throw new JsonException("Claude helper ownership metadata is invalid.");
        if (manifest.Owner != "Claudgar" || manifest.SchemaVersion != 1 || !ValidHash(manifest.InstalledHash) || !ValidHash(manifest.PendingHash) || !ValidHash(manifest.PreviousPendingHash))
            throw new JsonException("Claude helper ownership metadata is unsupported.");
        return manifest;
    }

    private static bool ValidHash(string? value) => value is null || (value.Length == 64 && value.All(Uri.IsHexDigit));
    private static void WriteManifest(string path, BridgeOwnership manifest) =>
        SafeFiles.WriteAtomic(path, JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions), backup: false);

    private sealed record BridgeOwnership
    {
        public string Owner { get; init; } = "Claudgar";
        public int SchemaVersion { get; init; } = 1;
        public string? InstalledHash { get; init; }
        public string? PendingHash { get; init; }
        public string? PreviousPendingHash { get; init; }
    }
}
