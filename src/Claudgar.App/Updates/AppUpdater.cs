using System.Diagnostics;
using System.Globalization;
using Claudgar.Core.Updates;

namespace Claudgar.App.Updates;

internal sealed class AppUpdater : IDisposable
{
    public const string DefaultMutexName = @"Local\Claudgar.App";
    private const string HelperFlag = "--apply-update";
    private const string ConfirmFlag = "--update-confirm";
    private readonly HttpClient httpClient;
    private readonly UpdateStore store;
    private readonly UpdateStagingService staging;
    private readonly ReleaseVersion appVersion;
    private readonly SemaphoreSlim checkGate = new(1, 1);
    private readonly string mutexName;
    private readonly IUpdateExecutableValidator validator = new WindowsUpdateExecutableValidator();

    public event Action<UpdateStatus>? StatusChanged;
    public UpdateStatus Status => staging.Status;

    public AppUpdater(string appVersion, string mutexName = DefaultMutexName, string? updatesDirectory = null)
    {
        if (!ReleaseVersion.TryParse(appVersion, out this.appVersion)) throw new ArgumentException("The app version is invalid.", nameof(appVersion));
        this.mutexName = mutexName;
        store = new(Environment.ProcessPath ?? throw new IOException("The current executable path is unavailable."), updatesDirectory);
        httpClient = new() { Timeout = TimeSpan.FromMinutes(10) };
        staging = new(store, new GitHubReleaseSource(httpClient), validator);
        staging.StatusChanged += status => StatusChanged?.Invoke(status);
    }

    // Recognize the flag anywhere, including malformed arguments, so helper mode never opens the UI.
    public static bool IsHelperInvocation(string[] arguments) => arguments.Contains(HelperFlag, StringComparer.Ordinal);
    public static bool IsConfirmationInvocation(string[] arguments) => arguments.Contains(ConfirmFlag, StringComparer.Ordinal);

    public async Task<UpdateStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await checkGate.WaitAsync(cancellationToken);
            try { return await staging.CheckAsync(appVersion, cancellationToken); }
            finally { checkGate.Release(); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return Status; }
    }

    // The caller still owns the app mutex here. On success it must perform a real shutdown and
    // release that mutex. The helper waits for that exact process to exit before changing any file.
    public bool TryStartPendingUpdate(bool startInTray, out string? issue)
    {
        issue = null;
        PendingUpdate? pending = null;
        try
        {
            pending = store.Load();
            if (pending is null)
            {
                issue = store.LastFailure()?.Message;
                if (issue is not null) staging.ReportFailure(issue);
                return false;
            }
            var processPath = Environment.ProcessPath!;
            var executable = store.StagedPath(pending);
            if (pending.Phase == UpdatePhase.Ready)
            {
                if (!ReleaseVersion.TryParse(pending.Version, out var version) || version.CompareTo(appVersion) <= 0 ||
                    pending.PreviousVersion != appVersion.ToString())
                {
                    store.Fail(pending, "The staged update no longer matches this app version.");
                    staging.ReportFailure("The staged update no longer matches this app version.");
                    return false;
                }
                staging.ValidatePending(pending);
            }
            else
            {
                // Recovery needs a disposable helper even if the staged candidate was removed or damaged.
                // Copy the running executable instead of locking the installed path during restoration.
                Directory.CreateDirectory(store.StageDirectory(pending));
                executable = Path.Combine(store.StageDirectory(pending), "Recovery-" + Guid.NewGuid().ToString("N") + ".exe");
                File.Copy(processPath, executable, overwrite: false);
                UpdateFiles.Verify(executable, UpdateFiles.Hash(processPath).Sha256, new FileInfo(processPath).Length);
                validator.Validate(executable, appVersion);
            }
            pending = pending with { StartInTray = startInTray };
            store.Save(pending);
            using var current = Process.GetCurrentProcess();
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = store.StageDirectory(pending)
            };
            foreach (var argument in new[] { HelperFlag, store.ManifestPath, pending.Token,
                         current.Id.ToString(CultureInfo.InvariantCulture), current.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture), mutexName })
                start.ArgumentList.Add(argument);
            using var helper = Process.Start(start) ?? throw new IOException("The update helper could not be started.");
            return true;
        }
        catch (Exception error) when (error is not OutOfMemoryException && error is not StackOverflowException)
        {
            issue = "Claudgar could not install its staged update: " + error.Message;
            try
            {
                if (pending is { Phase: UpdatePhase.Ready }) store.Fail(pending, issue);
                else if (pending is null) store.QuarantineInvalidJournal();
            }
            catch (Exception cleanupError) when (cleanupError is IOException or UnauthorizedAccessException) { }
            staging.ReportFailure(issue, pending?.Version);
            return false;
        }
    }

    public void AcknowledgeHealthyLaunch(string[] arguments)
    {
        var index = Array.IndexOf(arguments, ConfirmFlag);
        if (index < 0 || index + 1 >= arguments.Length) return;
        try
        {
            var pending = store.Load();
            if (pending is not null && pending.Token == arguments[index + 1] && pending.Version == appVersion.ToString())
                store.Acknowledge(pending);
        }
        catch (Exception error) when (error is not OutOfMemoryException && error is not StackOverflowException)
        {
            staging.ReportFailure("The updated app could not confirm startup: " + error.Message);
        }
    }

    public static int RunHelper(string[] arguments, string mutexName = DefaultMutexName)
    {
        try
        {
            if (arguments.Length is not (5 or 6) || arguments[0] != HelperFlag || !Guid.TryParseExact(arguments[2], "N", out _) ||
                !int.TryParse(arguments[3], NumberStyles.None, CultureInfo.InvariantCulture, out var parentPid) || parentPid <= 0 ||
                !long.TryParse(arguments[4], NumberStyles.None, CultureInfo.InvariantCulture, out var parentTicks) || parentTicks <= 0)
                return 2;
            if (arguments.Length == 6)
            {
                if (!arguments[5].StartsWith(@"Local\Claudgar", StringComparison.Ordinal) || arguments[5].Length > 200) return 2;
                mutexName = arguments[5];
            }
            // Derive the owned store from its target and require its exact expected manifest path.
            var manifestPath = Path.GetFullPath(arguments[1]);
            UpdateFiles.RejectLinks(manifestPath);
            if (!File.Exists(manifestPath) || new FileInfo(manifestPath).Length > 64 * 1024) return 2;
            var document = System.Text.Json.JsonSerializer.Deserialize<PendingUpdate>(File.ReadAllBytes(manifestPath))
                ?? throw new IOException("The update journal is empty.");
            var updatesDirectory = Path.GetDirectoryName(Path.GetDirectoryName(manifestPath))
                ?? throw new IOException("The update folder is invalid.");
            var store = new UpdateStore(document.TargetPath, updatesDirectory);
            if (!store.ManifestPath.Equals(manifestPath, StringComparison.OrdinalIgnoreCase)) return 2;
            var pending = store.Load() ?? throw new IOException("The update journal is missing.");
            if (pending.Token != arguments[2]) return 2;
            var helperPath = Path.GetFullPath(Environment.ProcessPath!);
            if (!Path.GetDirectoryName(helperPath)!.Equals(store.StageDirectory(pending), StringComparison.OrdinalIgnoreCase)) return 2;
            // Serialize helpers for this portable installation, independently of the app's lifetime mutex.
            using var operation = new Mutex(true, store.OperationMutexName, out var firstHelper);
            if (!firstHelper) return 3;
            try
            {
                var result = new UpdateApplyEngine(store, new WindowsUpdateExecutableValidator(), new WindowsUpdateProcessRuntime(mutexName))
                    .ApplyAsync(pending.Token, parentPid, parentTicks).GetAwaiter().GetResult();
                return result.Succeeded ? 0 : 1;
            }
            finally { operation.ReleaseMutex(); }
        }
        catch (Exception error) when (error is not OutOfMemoryException && error is not StackOverflowException) { return 1; }
    }

    public void Dispose() { httpClient.Dispose(); checkGate.Dispose(); }
}
