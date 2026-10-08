using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Claudgar.Core.Updates;

public sealed class UpdateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string TargetPath { get; }
    public string DirectoryPath { get; }
    public string ManifestPath => Path.Combine(DirectoryPath, "pending.json");
    public string FailurePath => Path.Combine(DirectoryPath, "failed.json");
    public string OperationMutexName => @"Local\Claudgar.Update." + Path.GetFileName(DirectoryPath);

    public UpdateStore(string targetPath, string? updatesDirectory = null)
    {
        TargetPath = Path.GetFullPath(targetPath);
        var baseDirectory = updatesDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Claudgar", "updates");
        var key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(TargetPath.ToUpperInvariant())))[..24];
        DirectoryPath = Path.Combine(Path.GetFullPath(baseDirectory), key);
    }

    public string StageDirectory(PendingUpdate update) => Path.Combine(DirectoryPath, update.Token);
    public string StagedPath(PendingUpdate update) => Path.Combine(StageDirectory(update), GitHubReleaseSource.AssetName);
    public string HealthPath(PendingUpdate update) => Path.Combine(StageDirectory(update), "healthy.txt");
    public string BackupPath(PendingUpdate update) => TargetPath + ".claudgar-rollback-" + update.Token;
    public string ReplacementPath(PendingUpdate update) => TargetPath + ".claudgar-replacement-" + update.Token;

    public PendingUpdate? Load()
    {
        UpdateFiles.RejectLinks(ManifestPath);
        if (!File.Exists(ManifestPath)) return null;
        var update = JsonSerializer.Deserialize<PendingUpdate>(ReadSmall(ManifestPath), JsonOptions)
            ?? throw new IOException("The staged update journal is empty.");
        Validate(update);
        return update;
    }

    public void Save(PendingUpdate update)
    {
        Validate(update);
        UpdateFiles.WriteAtomic(ManifestPath, JsonSerializer.SerializeToUtf8Bytes(update, JsonOptions));
    }

    public void Validate(PendingUpdate update)
    {
        if (update.SchemaVersion != 1 || !Guid.TryParseExact(update.Token, "N", out _) ||
            !string.Equals(Path.GetFullPath(update.TargetPath), TargetPath, StringComparison.OrdinalIgnoreCase) ||
            !ReleaseVersion.TryParse(update.Version, out var version) ||
            !ReleaseVersion.TryParse(update.PreviousVersion, out var previous) || version.CompareTo(previous) <= 0 ||
            !UpdateFiles.IsSha256(update.Sha256) || update.Size is <= 0 or > GitHubReleaseSource.MaximumAssetBytes ||
            !Enum.IsDefined(update.Phase) || (update.Phase != UpdatePhase.Ready &&
                (!UpdateFiles.IsSha256(update.PreviousSha256) || update.PreviousSize is <= 0 or > GitHubReleaseSource.MaximumAssetBytes)))
            throw new IOException("The staged update journal is invalid. The current app was preserved.");
        UpdateFiles.RejectLinks(DirectoryPath);
        UpdateFiles.RejectLinks(TargetPath);
        UpdateFiles.RejectLinks(StageDirectory(update));
    }

    public void Acknowledge(PendingUpdate update)
    {
        Validate(update);
        if (update.Phase != UpdatePhase.AwaitingHealth) throw new IOException("No updated launch is awaiting confirmation.");
        UpdateFiles.Verify(TargetPath, update.Sha256, update.Size);
        UpdateFiles.WriteAtomic(HealthPath(update), Encoding.ASCII.GetBytes(update.Token));
    }

    public bool IsHealthy(PendingUpdate update) => File.Exists(HealthPath(update)) &&
        Encoding.ASCII.GetString(ReadSmall(HealthPath(update))) == update.Token;

    public void Complete(PendingUpdate update)
    {
        Validate(update);
        // Keep the old executable until the new one has acknowledged a healthy launch.
        UpdateFiles.Verify(TargetPath, update.Sha256, update.Size);
        if (!IsHealthy(update)) throw new IOException("The updated app has not confirmed a healthy launch.");
        File.Delete(ManifestPath);
        TryDelete(BackupPath(update));
        TryDelete(ReplacementPath(update));
    }

    public void Fail(PendingUpdate update, string message)
    {
        Validate(update);
        UpdateFiles.WriteAtomic(FailurePath, JsonSerializer.SerializeToUtf8Bytes(
            new FailedUpdate(update.Version, update.Sha256, message, DateTimeOffset.UtcNow), JsonOptions));
        if (File.Exists(ManifestPath)) File.Delete(ManifestPath);
        TryDelete(ReplacementPath(update));
    }

    public FailedUpdate? LastFailure()
    {
        if (!File.Exists(FailurePath)) return null;
        UpdateFiles.RejectLinks(FailurePath);
        return JsonSerializer.Deserialize<FailedUpdate>(ReadSmall(FailurePath), JsonOptions);
    }

    public bool IsRejected(ReleaseAsset asset)
    {
        var failure = LastFailure();
        return failure?.Version == asset.Version.ToString() && string.Equals(failure.Sha256, asset.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    public void CleanUnusedStaging(PendingUpdate? active)
    {
        UpdateFiles.RejectLinks(DirectoryPath);
        if (!Directory.Exists(DirectoryPath)) return;
        foreach (var directory in Directory.EnumerateDirectories(DirectoryPath))
        {
            var token = Path.GetFileName(directory);
            if (token == active?.Token || !Guid.TryParseExact(token, "N", out _)) continue;
            try
            {
                UpdateFiles.RejectLinks(directory);
                // Only flat updater-owned payloads are cleaned. Unknown contents remain untouched.
                foreach (var path in Directory.EnumerateFiles(directory))
                {
                    var name = Path.GetFileName(path);
                    if (name != GitHubReleaseSource.AssetName && name != GitHubReleaseSource.AssetName + ".download" &&
                        name != "healthy.txt" && !(name.StartsWith("Recovery-", StringComparison.Ordinal) && name.EndsWith(".exe", StringComparison.Ordinal)))
                        continue;
                    UpdateFiles.RejectLinks(path);
                    File.Delete(path);
                }
                if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // A finishing helper may still lock its own executable. Retry this cleanup next launch.
            }
        }
    }

    // Malformed journals must not create a repeat-launch failure loop. Preserve the evidence for support.
    public void QuarantineInvalidJournal()
    {
        UpdateFiles.RejectLinks(ManifestPath);
        if (File.Exists(ManifestPath)) File.Move(ManifestPath, ManifestPath + ".invalid-" + Guid.NewGuid().ToString("N"));
    }

    private static byte[] ReadSmall(string path)
    {
        UpdateFiles.RejectLinks(path);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (file.Length > 64 * 1024) throw new IOException("The update journal is too large.");
        var data = new byte[file.Length];
        file.ReadExactly(data);
        return data;
    }

    private static void TryDelete(string path)
    {
        try { UpdateFiles.RejectLinks(path); File.Delete(path); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
}

public sealed record FailedUpdate(string Version, string Sha256, string Message, DateTimeOffset FailedAt);
