using System.Text.Json;
using System.Text.Json.Serialization;

namespace Claudgar.Core.Setup;

/// <summary>Updates only files whose current bytes match Claudgar's last ownership manifest.</summary>
public sealed class OwnedFilesInstaller
{
    private const string ManifestName = ".claudgar-owned-files.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public InstallationResult Install(string directory, IReadOnlyDictionary<string, byte[]> files,
        string? successfulAppVersion = null, bool skipIfVersionCurrent = false)
    {
        bool changed = false;
        try
        {
            if (successfulAppVersion is not null) ArgumentException.ThrowIfNullOrWhiteSpace(successfulAppVersion);
            directory = Path.GetFullPath(directory);
            SafeFiles.RejectReparsePoints(directory);
            var manifestPath = Path.Combine(directory, ManifestName);
            var owned = new OwnershipManifest();
            if (File.Exists(manifestPath))
            {
                owned = JsonSerializer.Deserialize<OwnershipManifest>(SafeFiles.ReadLimited(manifestPath, 1024 * 1024))
                    ?? throw new IOException("The existing Claudgar ownership manifest is invalid.");
                if (owned.Owner != "Claudgar" || owned.SchemaVersion != 1 || owned.Files is null || owned.Files.Count > 1024 ||
                    owned.PendingFiles is null || owned.PendingFiles.Count > 1024)
                    throw new IOException("The existing Claudgar ownership manifest is unsupported.");
            }
            else if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
            {
                return new(false, false, "An existing folder is not owned by Claudgar. Its files were preserved; rename it before setting up again.", directory);
            }

            // The marker belongs to this installation and only advances with a completed install.
            // Health inspection and explicit repair still check the actual files at the same version.
            if (skipIfVersionCurrent && successfulAppVersion is not null &&
                owned.SuccessfulAppVersion == successfulAppVersion && owned.PendingFiles.Count == 0)
                return new(true, false, "The addon is already installed for Claudgar " + successfulAppVersion + ".", directory);

            var targets = new List<(string Relative, string Path, byte[] Content, string Hash, string? PreviousHash)>();
            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (relative, content) in files)
            {
                var target = SafeFiles.UnderDirectory(directory, relative);
                if (!uniquePaths.Add(target) || string.Equals(Path.GetFileName(target), ManifestName, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Packaged resources contain duplicate or reserved filenames.", nameof(files));
                SafeFiles.RejectReparsePoints(target);
                var hash = SafeFiles.Hash(content);
                string? previousHash = null;
                if (Directory.Exists(target)) return new(false, false, "A folder conflicts with a packaged Claudgar file. It was preserved: " + relative, directory);
                if (File.Exists(target))
                {
                    previousHash = SafeFiles.HashFile(target);
                    var matchesPrevious = owned.Files.TryGetValue(relative, out var expectedHash) && previousHash == expectedHash;
                    var matchesInterruptedUpdate = owned.PendingFiles.TryGetValue(relative, out var pendingHash) && previousHash == pendingHash;
                    if (!matchesPrevious && !matchesInterruptedUpdate)
                        return new(false, false, "A Claudgar file was changed outside the app. It was preserved: " + relative, directory);
                    if (matchesInterruptedUpdate) owned.Files[relative] = previousHash;
                }
                targets.Add((relative, target, content, hash, previousHash));
            }

            // Journal intended hashes before replacements. An interrupted first install or update can resume
            // only when existing bytes match either the previous owned version or our journaled version.
            if (targets.Any(target => target.PreviousHash != target.Hash))
            {
                foreach (var target in targets) owned.PendingFiles[target.Relative] = target.Hash;
                SafeFiles.WriteAtomic(manifestPath, JsonSerializer.SerializeToUtf8Bytes(owned, JsonOptions), backup: true);
                changed = true;
            }
            foreach (var target in targets)
            {
                if (target.PreviousHash != target.Hash)
                {
                    // Detect changes between preflight and replacement; setup never adopts unknown bytes.
                    var current = File.Exists(target.Path) ? SafeFiles.HashFile(target.Path) : null;
                    if (current != target.PreviousHash) throw new IOException("A file changed during setup: " + target.Relative);
                    SafeFiles.WriteAtomic(target.Path, target.Content, backup: true);
                    changed = true;
                }
                owned.Files[target.Relative] = target.Hash;
                owned.PendingFiles.Remove(target.Relative);
            }
            if (successfulAppVersion is not null) owned.SuccessfulAppVersion = successfulAppVersion;
            var manifestContent = JsonSerializer.SerializeToUtf8Bytes(owned, JsonOptions);
            if (!File.Exists(manifestPath) || !SafeFiles.ReadLimited(manifestPath, 1024 * 1024).AsSpan().SequenceEqual(manifestContent))
            {
                SafeFiles.WriteAtomic(manifestPath, manifestContent, backup: true);
                changed = true;
            }
            return new(true, changed, changed ? "Installed successfully." : "Already installed and up to date.", directory);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or JsonException or NotSupportedException)
        {
            return new(false, changed, "Setup could not finish: " + error.Message, directory);
        }
    }

    private sealed record OwnershipManifest
    {
        public OwnershipManifest() { }
        public string Owner { get; init; } = "Claudgar";
        public int SchemaVersion { get; init; } = 1;
        public Dictionary<string, string> Files { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> PendingFiles { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        [JsonPropertyName("successfulAppVersion")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SuccessfulAppVersion { get; set; }
    }
}
