using System.Security.Cryptography;

namespace Claudgar.Core.Updates;

public sealed class UpdateStagingService(UpdateStore store, IReleaseSource source, IUpdateExecutableValidator validator)
{
    public event Action<UpdateStatus>? StatusChanged;
    public UpdateStatus Status { get; private set; } = new(UpdateState.Idle);

    public async Task<UpdateStatus> CheckAsync(ReleaseVersion currentVersion, CancellationToken cancellationToken = default)
    {
        PendingUpdate? downloading = null;
        try
        {
            Set(new(UpdateState.Checking, Message: "Checking for a Claudgar update…"));
            var pending = store.Load();
            store.CleanUnusedStaging(pending);
            if (pending is not null && pending.Phase == UpdatePhase.Ready &&
                ReleaseVersion.TryParse(pending.Version, out var pendingVersion) && pendingVersion.CompareTo(currentVersion) > 0)
            {
                ValidatePending(pending);
                return Set(new(UpdateState.Ready, pending.Version, "A new Claudgar version is ready."));
            }
            // A helper owns an in-progress journal; startup recovery handles it before normal UI startup.
            if (pending is not null) return Set(new(UpdateState.Idle));
            var release = await source.GetLatestAsync(cancellationToken);
            if (release is null || release.Version.CompareTo(currentVersion) <= 0)
            {
                var failure = store.LastFailure();
                return Set(failure is null ? new(UpdateState.Idle) :
                    new(UpdateState.Failed, failure.Version, failure.Message));
            }
            if (store.IsRejected(release)) return Set(new(UpdateState.Failed, release.Version.ToString(),
                "The previous attempt to install this update failed. Download a release manually or wait for the next version."));
            downloading = new()
            {
                Token = Guid.NewGuid().ToString("N"), TargetPath = store.TargetPath,
                PreviousVersion = currentVersion.ToString(), Version = release.Version.ToString(),
                Sha256 = release.Sha256, Size = release.Size, Phase = UpdatePhase.Ready
            };
            store.Validate(downloading);
            Directory.CreateDirectory(store.StageDirectory(downloading));
            var partial = store.StagedPath(downloading) + ".download";
            Set(new(UpdateState.Downloading, downloading.Version, "Preparing a new Claudgar version…"));
            await using (var input = await source.OpenDownloadAsync(release, cancellationToken))
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             81920, FileOptions.Asynchronous))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) != 0)
                {
                    total += read;
                    if (total > release.Size) throw new IOException("The update download exceeded its expected size.");
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                if (total != release.Size || !Convert.ToHexStringLower(hash.GetHashAndReset()).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("The update download was incomplete or failed SHA-256 verification.");
                await output.FlushAsync(cancellationToken);
                output.Flush(flushToDisk: true);
            }
            validator.Validate(partial, release.Version);
            File.Move(partial, store.StagedPath(downloading));
            // Ready is committed last, after the bytes, length, and executable metadata have all passed.
            store.Save(downloading);
            return Set(new(UpdateState.Ready, downloading.Version, "A new Claudgar version is ready."));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            DeletePartial(downloading);
            return Set(new(UpdateState.Idle));
        }
        catch (Exception error) when (error is not OutOfMemoryException && error is not StackOverflowException)
        {
            DeletePartial(downloading);
            return Set(new(UpdateState.Failed, Message: "Claudgar could not prepare an update: " + error.Message));
        }
    }

    public void ValidatePending(PendingUpdate pending)
    {
        store.Validate(pending);
        UpdateFiles.Verify(store.StagedPath(pending), pending.Sha256, pending.Size);
        validator.Validate(store.StagedPath(pending), ReleaseVersion.TryParse(pending.Version, out var version) ? version : throw new IOException("Invalid update version."));
    }

    public void ReportFailure(string message, string? version = null) => Set(new(UpdateState.Failed, version, message));

    private UpdateStatus Set(UpdateStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(status);
        return status;
    }

    private void DeletePartial(PendingUpdate? update)
    {
        if (update is null) return;
        try { File.Delete(store.StagedPath(update) + ".download"); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
}
