namespace Claudgar.Core.Updates;

public interface IUpdateProcessRuntime
{
    Task WaitForParentExitAsync(int processId, long startedUtcTicks, CancellationToken cancellationToken);
    Task<IDisposable> AcquireAppMutexAsync(CancellationToken cancellationToken);
    IUpdateChildProcess Start(string executable, IReadOnlyList<string> arguments);
}

public interface IUpdateChildProcess : IDisposable
{
    bool HasExited { get; }
    Task StopAsync(CancellationToken cancellationToken);
}

public sealed record UpdateApplyResult(bool Succeeded, bool RolledBack, string? Message = null);

// Runs only in the staged helper process. The app mutex is held during filesystem replacement,
// then released before starting the app that must acquire it and acknowledge a healthy launch.
public sealed class UpdateApplyEngine(UpdateStore store, IUpdateExecutableValidator validator,
    IUpdateProcessRuntime processes, TimeSpan? healthTimeout = null)
{
    public async Task<UpdateApplyResult> ApplyAsync(string token, int parentPid, long parentStartedUtcTicks,
        CancellationToken cancellationToken = default)
    {
        PendingUpdate? update = null;
        IUpdateChildProcess? launched = null;
        var parentExited = false;
        var appMutexAcquired = false;
        var recoveredInterrupted = false;
        try
        {
            update = store.Load() ?? throw new IOException("The pending update no longer exists.");
            if (update.Token != token) return new(false, false, "The update helper token does not match the pending update.");
            await processes.WaitForParentExitAsync(parentPid, parentStartedUtcTicks, cancellationToken);
            parentExited = true;
            using (await processes.AcquireAppMutexAsync(cancellationToken))
            {
                appMutexAcquired = true;
                // Re-read after waiting: never apply a journal replaced by another check or helper.
                update = store.Load() ?? throw new IOException("The pending update no longer exists.");
                if (update.Token != token) return new(false, false, "The update changed while waiting for shutdown.");
                if (update.Phase != UpdatePhase.Ready)
                {
                    if (store.IsHealthy(update))
                    {
                        store.Complete(update);
                        // The previous helper died after the healthy acknowledgement; finish its cleanup.
                    }
                    else
                    {
                        RestorePrevious(update);
                        store.Fail(update, "An interrupted update was rolled back to the previous working version.");
                        recoveredInterrupted = true;
                    }
                }
                else
                {
                    ValidateCandidate(update);
                    var previous = UpdateFiles.Hash(store.TargetPath);
                    validator.Validate(store.TargetPath, ParseVersion(update.PreviousVersion));
                    update = update with { Phase = UpdatePhase.Applying, PreviousSha256 = previous.Sha256, PreviousSize = previous.Size };
                    store.Save(update);
                    // Copy to the original volume before File.Replace. The running helper's source remains untouched.
                    var replacement = store.ReplacementPath(update);
                    UpdateFiles.RejectLinks(replacement);
                    File.Copy(store.StagedPath(update), replacement, overwrite: false);
                    UpdateFiles.Verify(replacement, update.Sha256, update.Size);
                    validator.Validate(replacement, ParseVersion(update.Version));
                    await ReplaceWithRetryAsync(replacement, store.TargetPath, store.BackupPath(update), cancellationToken);
                    update = update with { Phase = UpdatePhase.AwaitingHealth };
                    store.Save(update);
                }
            }

            if (recoveredInterrupted) return StartRecovered(update, "An interrupted update was rolled back.");

            if (!File.Exists(store.ManifestPath))
            {
                using var recovered = processes.Start(store.TargetPath, StartupArguments(update));
                return new(true, false);
            }
            launched = processes.Start(store.TargetPath, StartupArguments(update, confirm: true));
            var deadline = DateTimeOffset.UtcNow + (healthTimeout ?? TimeSpan.FromSeconds(60));
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (store.IsHealthy(update))
                {
                    store.Complete(update);
                    return new(true, false);
                }
                if (launched.HasExited) throw new IOException("The updated app exited before it confirmed a healthy launch.");
                await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
            }
            throw new IOException("The updated app did not confirm a healthy launch within one minute.");
        }
        catch (Exception error) when (error is not OutOfMemoryException && error is not StackOverflowException)
        {
            var message = "The update could not be installed: " + error.Message;
            if (update is null) return new(false, false, message);
            try
            {
                if (launched is not null && !launched.HasExited) await launched.StopAsync(CancellationToken.None);
                if (parentExited && appMutexAcquired)
                {
                    using (await processes.AcquireAppMutexAsync(CancellationToken.None))
                    {
                        if (update.Phase != UpdatePhase.Ready) RestorePrevious(update);
                        store.Fail(update, message + " The previous working app was restored.");
                    }
                    return StartRecovered(update, message);
                }
                // No replacement occurred. Keep the still-running app intact and quarantine the attempt.
                if (update.Phase == UpdatePhase.Ready) store.Fail(update, message);
                return new(false, false, message);
            }
            catch (Exception recoveryError) when (recoveryError is not OutOfMemoryException && recoveryError is not StackOverflowException)
            {
                // The journal and rollback copy remain so the next launch can retry recovery safely.
                return new(false, false, message + " Recovery will be retried on the next launch: " + recoveryError.Message);
            }
        }
        finally { launched?.Dispose(); }
    }

    private UpdateApplyResult StartRecovered(PendingUpdate update, string message)
    {
        using var process = processes.Start(store.TargetPath, StartupArguments(update));
        return new(false, true, message);
    }

    private void ValidateCandidate(PendingUpdate update)
    {
        UpdateFiles.Verify(store.StagedPath(update), update.Sha256, update.Size);
        validator.Validate(store.StagedPath(update), ParseVersion(update.Version));
    }

    private void RestorePrevious(PendingUpdate update)
    {
        var backup = store.BackupPath(update);
        if (File.Exists(backup))
        {
            UpdateFiles.Verify(backup, update.PreviousSha256!, update.PreviousSize);
            validator.Validate(backup, ParseVersion(update.PreviousVersion));
            UpdateFiles.RejectLinks(store.TargetPath);
            // Atomic same-volume restoration; no point leaves the installation without an executable.
            if (File.Exists(store.TargetPath)) File.Replace(backup, store.TargetPath, null);
            else File.Move(backup, store.TargetPath);
        }
        UpdateFiles.Verify(store.TargetPath, update.PreviousSha256!, update.PreviousSize);
        validator.Validate(store.TargetPath, ParseVersion(update.PreviousVersion));
    }

    private static async Task ReplaceWithRetryAsync(string replacement, string target, string backup, CancellationToken cancellationToken)
    {
        UpdateFiles.RejectLinks(target);
        UpdateFiles.RejectLinks(backup);
        if (File.Exists(backup)) throw new IOException("An existing rollback copy needs recovery before another replacement.");
        for (var attempt = 0; ; attempt++)
        {
            try { File.Replace(replacement, target, backup); return; }
            catch (IOException) when (attempt < 19) { await Task.Delay(250, cancellationToken); }
        }
    }

    private static ReleaseVersion ParseVersion(string text) => ReleaseVersion.TryParse(text, out var version)
        ? version : throw new IOException("The update version is invalid.");

    private static string[] StartupArguments(PendingUpdate update, bool confirm = false)
    {
        var arguments = new List<string>();
        if (update.StartInTray) arguments.Add("--startup");
        if (confirm) { arguments.Add("--update-confirm"); arguments.Add(update.Token); }
        return arguments.ToArray();
    }
}
