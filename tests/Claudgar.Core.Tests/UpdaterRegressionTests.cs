using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Claudgar.Core.Updates;

internal static class UpdaterRegressionTests
{
    public static void VersionsAndReleaseSelection()
    {
        Assert(ReleaseVersion.TryParse("v1.10.2", out var newer) && ReleaseVersion.TryParse("1.2.9", out var older) && newer.CompareTo(older) > 0,
            "Versions were compared lexically rather than numerically.");
        foreach (var invalid in new[] { "1.0", "1.0.0.0", "1.0.0-preview", "v1.0.0+build", "01.0.0", "1.0.-1", "1. 0.0", "999999999999.0.0" })
            Assert(!ReleaseVersion.TryParse(invalid, out _), "A non-stable release version was accepted: " + invalid);
        using var normal = JsonDocument.Parse(ReleaseJson());
        var release = GitHubReleaseSource.ParseRelease(normal.RootElement);
        Assert(release?.Version.ToString() == "0.2.0" && release.Sha256.Length == 64, "The stable release was not selected.");
        foreach (var skipped in new[] { ReleaseJson(draft: true), ReleaseJson(prerelease: true), ReleaseJson(version: "v0.2.0-beta"), ReleaseJson(published: false) })
        {
            using var document = JsonDocument.Parse(skipped);
            Assert(GitHubReleaseSource.ParseRelease(document.RootElement) is null, "An unpublished or prerelease update was selected.");
        }
        foreach (var wrong in new[] { ReleaseJson(assetName: "wrong.exe"), ReleaseJson(digest: ""), ReleaseJson(uri: "https://example.com/Claudgar.exe"), ReleaseJson(size: 0) })
        {
            using var document = JsonDocument.Parse(wrong);
            ExpectFailure(() => GitHubReleaseSource.ParseRelease(document.RootElement));
        }
    }

    public static void AbsentOfflineAndOlderReleases()
    {
        using var temporary = new TestDirectory();
        using var http = new HttpClient(new StubHandler(_ => new(HttpStatusCode.NotFound)));
        Assert(new GitHubReleaseSource(http).GetLatestAsync(default).GetAwaiter().GetResult() is null, "No published releases should be a harmless no-update result.");
        foreach (var asset in new ReleaseAsset?[] { null, Asset("0.1.0"), Asset("0.0.9") })
        {
            var source = new FakeSource(asset, NewBytes);
            var staging = new UpdateStagingService(temporary.Store, source, new FakeValidator());
            Assert(staging.CheckAsync(Current).GetAwaiter().GetResult().State == UpdateState.Idle && source.DownloadCount == 0,
                "Equal, older, or absent releases attempted a download.");
        }
        var offline = new UpdateStagingService(temporary.Store, new FakeSource(Asset(), NewBytes) { Offline = true }, new FakeValidator());
        Assert(offline.CheckAsync(Current).GetAwaiter().GetResult().State == UpdateState.Failed && !File.Exists(temporary.Store.ManifestPath),
            "Network failure created a pending update.");
        using var forbiddenHttp = new HttpClient(new StubHandler(_ => new(HttpStatusCode.Forbidden)));
        var unavailable = new UpdateStagingService(temporary.Store, new GitHubReleaseSource(forbiddenHttp), new FakeValidator());
        Assert(unavailable.CheckAsync(Current).GetAwaiter().GetResult().State == UpdateState.Failed, "A GitHub rate limit was not surfaced safely.");
        Assert(File.ReadAllBytes(temporary.Store.TargetPath).SequenceEqual(OldBytes), "A failed check modified the installed app.");
    }

    public static void CorruptInterruptedAndWrongDownloads()
    {
        foreach (var bytes in new[] { NewBytes[..^1], Encoding.ASCII.GetBytes("wrong bytes"), NewBytes.Concat([ (byte)'x' ]).ToArray() })
        {
            using var temporary = new TestDirectory();
            var staging = new UpdateStagingService(temporary.Store, new FakeSource(Asset(), bytes), new FakeValidator());
            Assert(staging.CheckAsync(Current).GetAwaiter().GetResult().State == UpdateState.Failed, "A corrupt or truncated download was accepted.");
            Assert(!File.Exists(temporary.Store.ManifestPath), "An unverified download was marked ready.");
            Assert(!Directory.EnumerateFiles(temporary.Root, "*.download", SearchOption.AllDirectories).Any(), "An interrupted download was retained as an update.");
        }
        using (var temporary = new TestDirectory())
        {
            var staging = new UpdateStagingService(temporary.Store, new FakeSource(Asset(), NewBytes) { Interrupt = true }, new FakeValidator());
            Assert(staging.CheckAsync(Current).GetAwaiter().GetResult().State == UpdateState.Failed && !File.Exists(temporary.Store.ManifestPath),
                "An interrupted stream was committed.");
        }
        using (var temporary = new TestDirectory())
        {
            var staging = new UpdateStagingService(temporary.Store, new FakeSource(Asset(), NewBytes), new FakeValidator { RejectNewVersion = true });
            Assert(staging.CheckAsync(Current).GetAwaiter().GetResult().State == UpdateState.Failed && !File.Exists(temporary.Store.ManifestPath),
                "The wrong executable version was committed.");
        }
        using (var temporary = new TestDirectory())
        {
            // A file at the journal directory represents an unwritable/unusable staging destination.
            Directory.CreateDirectory(Path.GetDirectoryName(temporary.Store.DirectoryPath)!);
            File.WriteAllText(temporary.Store.DirectoryPath, "occupied");
            var staging = new UpdateStagingService(temporary.Store, new FakeSource(Asset(), NewBytes), new FakeValidator());
            Assert(staging.CheckAsync(Current).GetAwaiter().GetResult().State == UpdateState.Failed, "An unavailable staging folder did not fail safely.");
        }
    }

    public static void ReadyUpdatesAreRevalidated()
    {
        using var temporary = new TestDirectory();
        var source = new FakeSource(Asset(), NewBytes);
        var staging = new UpdateStagingService(temporary.Store, source, new FakeValidator());
        Assert(staging.CheckAsync(Current).GetAwaiter().GetResult().Ready, "A verified update was not staged.");
        var pending = temporary.Store.Load()!;
        var staleToken = Guid.NewGuid().ToString("N");
        var staleFolder = Path.Combine(temporary.Store.DirectoryPath, staleToken);
        Directory.CreateDirectory(staleFolder);
        File.WriteAllText(Path.Combine(staleFolder, "Claudgar.exe.download"), "incomplete previous download");
        var restarted = new UpdateStagingService(temporary.Store, new FakeSource(null, []), new FakeValidator());
        Assert(restarted.CheckAsync(Current).GetAwaiter().GetResult().Ready, "A staged update was not restored after restart.");
        Assert(!Directory.Exists(staleFolder) && File.Exists(temporary.Store.StagedPath(pending)), "Staging cleanup did not preserve the active update.");
        var invalidTokenResult = new UpdateApplyEngine(temporary.Store, new FakeValidator(), new FakeProcesses(temporary.Store))
            .ApplyAsync(Guid.NewGuid().ToString("N"), 1, 1).GetAwaiter().GetResult();
        Assert(!invalidTokenResult.Succeeded && temporary.Store.Load()?.Token == pending.Token, "A stale helper discarded another pending update.");
        File.WriteAllBytes(temporary.Store.StagedPath(pending), Encoding.ASCII.GetBytes("changed"));
        Assert(restarted.CheckAsync(Current).GetAwaiter().GetResult().State == UpdateState.Failed, "Changed staged bytes were trusted after restart.");
        var runtime = new FakeProcesses(temporary.Store);
        var result = new UpdateApplyEngine(temporary.Store, new FakeValidator(), runtime).ApplyAsync(pending.Token, 1, 1).GetAwaiter().GetResult();
        Assert(!result.Succeeded && File.ReadAllBytes(temporary.Store.TargetPath).SequenceEqual(OldBytes), "Corrupt staged bytes were installed.");
        Assert(!File.Exists(temporary.Store.ManifestPath) && temporary.Store.IsRejected(Asset()), "A failed attempt could cause repeated restart loops.");
    }

    public static void SuccessfulApplyWaitsAndPreservesStartup()
    {
        foreach (var startup in new[] { true, false })
        {
            using var temporary = new TestDirectory();
            var pending = Stage(temporary) with { StartInTray = startup };
            temporary.Store.Save(pending);
            var unrelated = Path.Combine(Path.GetDirectoryName(temporary.Store.TargetPath)!, "settings.json");
            File.WriteAllText(unrelated, "user settings");
            var runtime = new FakeProcesses(temporary.Store) { Acknowledge = true };
            var result = new UpdateApplyEngine(temporary.Store, new FakeValidator(), runtime).ApplyAsync(pending.Token, 1, 1).GetAwaiter().GetResult();
            Assert(result.Succeeded && !result.RolledBack, "A healthy staged replacement failed.");
            Assert(runtime.ParentWaited && runtime.Starts.Count == 1 && runtime.Starts[0].Contains("--update-confirm"), "Apply did not wait for shutdown or request healthy acknowledgement.");
            Assert(runtime.Starts[0].Contains("--startup") == startup, "Startup tray mode changed during the update.");
            Assert(File.ReadAllBytes(temporary.Store.TargetPath).SequenceEqual(NewBytes), "The installed bytes do not match the verified update.");
            Assert(!File.Exists(temporary.Store.ManifestPath) && !File.Exists(temporary.Store.BackupPath(pending)), "A healthy update left an active recovery journal.");
            Assert(File.ReadAllText(unrelated) == "user settings", "Updater changed unrelated user settings.");
        }
    }

    public static void FailedLaunchRollsBackAndQuarantines()
    {
        foreach (var earlyExit in new[] { true, false })
        {
            using var temporary = new TestDirectory();
            var pending = Stage(temporary);
            var runtime = new FakeProcesses(temporary.Store) { NewAppExits = earlyExit };
            var result = new UpdateApplyEngine(temporary.Store, new FakeValidator(), runtime, TimeSpan.Zero)
                .ApplyAsync(pending.Token, 1, 1).GetAwaiter().GetResult();
            Assert(!result.Succeeded && result.RolledBack, "An unacknowledged app was not rolled back.");
            Assert(File.ReadAllBytes(temporary.Store.TargetPath).SequenceEqual(OldBytes), "Rollback did not restore the working executable.");
            Assert(runtime.Starts.Count == 2 && !runtime.Starts[1].Contains("--update-confirm"), "Rollback did not relaunch the previous version.");
            Assert(earlyExit || runtime.StoppedChild, "The hung updated app was not stopped before rollback.");
            Assert(!File.Exists(temporary.Store.ManifestPath) && temporary.Store.IsRejected(Asset()), "Failed update was not quarantined.");
            var check = new UpdateStagingService(temporary.Store, new FakeSource(Asset(), NewBytes), new FakeValidator());
            Assert(check.CheckAsync(Current).GetAwaiter().GetResult().State == UpdateState.Failed, "The same failed release immediately downloaded again.");
        }
    }

    public static void InterruptedReplacementRecovers()
    {
        foreach (var replacementCompleted in new[] { true, false })
        {
            using var temporary = new TestDirectory();
            var pending = Stage(temporary);
            var previous = UpdateFiles.Hash(temporary.Store.TargetPath);
            pending = pending with { Phase = UpdatePhase.Applying, PreviousSha256 = previous.Sha256, PreviousSize = previous.Size };
            temporary.Store.Save(pending);
            if (replacementCompleted)
            {
                File.Copy(temporary.Store.TargetPath, temporary.Store.BackupPath(pending));
                File.WriteAllBytes(temporary.Store.TargetPath, NewBytes);
            }
            var runtime = new FakeProcesses(temporary.Store);
            var result = new UpdateApplyEngine(temporary.Store, new FakeValidator(), runtime).ApplyAsync(pending.Token, 1, 1).GetAwaiter().GetResult();
            Assert(result.RolledBack && File.ReadAllBytes(temporary.Store.TargetPath).SequenceEqual(OldBytes), "An interrupted replacement did not recover the old app.");
            Assert(runtime.Starts.Count == 1 && !File.Exists(temporary.Store.ManifestPath), "Interrupted recovery did not relaunch or clear the loop.");
        }
        using (var temporary = new TestDirectory())
        {
            var pending = Stage(temporary);
            var previous = UpdateFiles.Hash(temporary.Store.TargetPath);
            File.Copy(temporary.Store.TargetPath, temporary.Store.BackupPath(pending));
            File.WriteAllBytes(temporary.Store.TargetPath, NewBytes);
            pending = pending with { Phase = UpdatePhase.AwaitingHealth, PreviousSha256 = previous.Sha256, PreviousSize = previous.Size };
            temporary.Store.Save(pending);
            temporary.Store.Acknowledge(pending);
            var result = new UpdateApplyEngine(temporary.Store, new FakeValidator(), new FakeProcesses(temporary.Store))
                .ApplyAsync(pending.Token, 1, 1).GetAwaiter().GetResult();
            Assert(result.Succeeded && File.ReadAllBytes(temporary.Store.TargetPath).SequenceEqual(NewBytes), "An acknowledged update was rolled back after helper interruption.");
        }
        using (var temporary = new TestDirectory())
        {
            var pending = Stage(temporary);
            var runtime = new FakeProcesses(temporary.Store) { ParentWaitFails = true };
            var result = new UpdateApplyEngine(temporary.Store, new FakeValidator(), runtime).ApplyAsync(pending.Token, 1, 1).GetAwaiter().GetResult();
            Assert(!result.Succeeded && runtime.Starts.Count == 0 && File.ReadAllBytes(temporary.Store.TargetPath).SequenceEqual(OldBytes),
                "A still-running parent was replaced or duplicated.");
        }
    }

    public static void LockedAndMalformedJournalsRecoverSafely()
    {
        using (var temporary = new TestDirectory())
        {
            var pending = Stage(temporary);
            using var fileLock = new FileStream(temporary.Store.TargetPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var result = new UpdateApplyEngine(temporary.Store, new FakeValidator(), new FakeProcesses(temporary.Store))
                .ApplyAsync(pending.Token, 1, 1).GetAwaiter().GetResult();
            Assert(!result.Succeeded && result.RolledBack && File.ReadAllBytes(temporary.Store.TargetPath).SequenceEqual(OldBytes),
                "A locked destination lost the working app.");
            Assert(!File.Exists(temporary.Store.ManifestPath), "A locked destination left an endless staged update loop.");
        }
        using (var temporary = new TestDirectory())
        {
            var pending = Stage(temporary);
            ExpectFailure(() => temporary.Store.Save(pending with { Token = "../outside" }));
            ExpectFailure(() => temporary.Store.Save(pending with { TargetPath = Path.Combine(temporary.Root, "unrelated.exe") }));
            File.WriteAllText(temporary.Store.ManifestPath, "{broken");
            var check = new UpdateStagingService(temporary.Store, new FakeSource(Asset(), NewBytes), new FakeValidator());
            Assert(check.CheckAsync(Current).GetAwaiter().GetResult().State == UpdateState.Failed, "A malformed journal was accepted.");
            temporary.Store.QuarantineInvalidJournal();
            Assert(!File.Exists(temporary.Store.ManifestPath) && Directory.EnumerateFiles(temporary.Store.DirectoryPath, "pending.json.invalid-*").Count() == 1,
                "A malformed journal was discarded or repeatedly retried instead of preserved for inspection.");
            Assert(File.ReadAllBytes(temporary.Store.TargetPath).SequenceEqual(OldBytes), "An invalid journal changed the working app.");
        }
        using (var temporary = new TestDirectory())
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            var check = new UpdateStagingService(temporary.Store, new FakeSource(Asset(), NewBytes), new FakeValidator());
            Assert(check.CheckAsync(Current, cancellation.Token).GetAwaiter().GetResult().State == UpdateState.Idle &&
                !File.Exists(temporary.Store.ManifestPath), "Shutdown cancellation committed a partial download.");
        }
    }

    private static readonly byte[] OldBytes = Encoding.ASCII.GetBytes("Claudgar 0.1.0 executable");
    private static readonly byte[] NewBytes = Encoding.ASCII.GetBytes("Claudgar 0.2.0 executable");
    private static readonly ReleaseVersion Current = new(0, 1, 0);
    private static ReleaseAsset Asset(string version = "0.2.0") => new(
        ReleaseVersion.TryParse(version, out var parsed) ? parsed : throw new ArgumentException(version),
        new($"https://github.com/RichardBangs/Claudgar/releases/download/v{version}/Claudgar.exe"),
        Convert.ToHexStringLower(SHA256.HashData(NewBytes)), NewBytes.Length);

    private static PendingUpdate Stage(TestDirectory directory)
    {
        Assert(new UpdateStagingService(directory.Store, new FakeSource(Asset(), NewBytes), new FakeValidator()).CheckAsync(Current).GetAwaiter().GetResult().Ready,
            "Could not prepare test update.");
        return directory.Store.Load()!;
    }

    private static string ReleaseJson(bool draft = false, bool prerelease = false, string version = "v0.2.0", bool published = true,
        string assetName = "Claudgar.exe", string? digest = null, string? uri = null, long size = 24) => JsonSerializer.Serialize(new
        {
            draft, prerelease, tag_name = version, published_at = published ? "2026-10-08T12:00:00Z" : null,
            assets = new[] { new { name = assetName, state = "uploaded", size, digest = digest ?? "sha256:" + new string('a', 64),
                browser_download_url = uri ?? $"https://github.com/RichardBangs/Claudgar/releases/download/{version}/Claudgar.exe" } }
        });

    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void ExpectFailure(Action action)
    {
        try { action(); }
        catch (IOException) { return; }
        throw new InvalidOperationException("Invalid update metadata was accepted.");
    }

    private sealed class TestDirectory : IDisposable
    {
        public string Root { get; } = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, ".tmp", "updater-tests", Guid.NewGuid().ToString("N")));
        public UpdateStore Store { get; }
        public TestDirectory()
        {
            var target = Path.Combine(Root, "portable folder with spaces", "Claudgar.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, OldBytes);
            Store = new(target, Path.Combine(Root, "updates"));
        }
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed class FakeValidator : IUpdateExecutableValidator
    {
        public bool RejectNewVersion { get; init; }
        public void Validate(string path, ReleaseVersion expectedVersion)
        {
            if (RejectNewVersion && expectedVersion.ToString() == "0.2.0") throw new IOException("Wrong executable identity/version.");
            var content = Encoding.ASCII.GetString(File.ReadAllBytes(path));
            if (!content.Contains(expectedVersion.ToString(), StringComparison.Ordinal)) throw new IOException("Wrong executable version.");
        }
    }

    private sealed class FakeSource(ReleaseAsset? asset, byte[] bytes) : IReleaseSource
    {
        public int DownloadCount { get; private set; }
        public bool Offline { get; init; }
        public bool Interrupt { get; init; }
        public Task<ReleaseAsset?> GetLatestAsync(CancellationToken cancellationToken) => Offline
            ? Task.FromException<ReleaseAsset?>(new HttpRequestException("Offline")) : Task.FromResult(asset);
        public Task<Stream> OpenDownloadAsync(ReleaseAsset release, CancellationToken cancellationToken)
        {
            DownloadCount++;
            return Task.FromResult<Stream>(Interrupt ? new BrokenStream(bytes) : new MemoryStream(bytes));
        }
    }

    private sealed class BrokenStream(byte[] bytes) : MemoryStream(bytes)
    {
        private bool read;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (read) throw new IOException("Connection interrupted.");
            read = true;
            return base.ReadAsync(buffer[..Math.Min(buffer.Length, 8)], cancellationToken);
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }

    private sealed class FakeProcesses(UpdateStore store) : IUpdateProcessRuntime
    {
        public bool ParentWaited { get; private set; }
        public bool StoppedChild { get; private set; }
        public bool Acknowledge { get; init; }
        public bool NewAppExits { get; init; }
        public bool ParentWaitFails { get; init; }
        public List<IReadOnlyList<string>> Starts { get; } = [];
        private bool mutexHeld;
        public Task WaitForParentExitAsync(int processId, long startedUtcTicks, CancellationToken cancellationToken)
        {
            ParentWaited = true;
            Assert(File.ReadAllBytes(store.TargetPath).SequenceEqual(OldBytes) || store.Load()?.Phase != UpdatePhase.Ready,
                "The target was changed before the parent exited.");
            return ParentWaitFails ? Task.FromException(new IOException("Parent still running")) : Task.CompletedTask;
        }
        public Task<IDisposable> AcquireAppMutexAsync(CancellationToken cancellationToken)
        {
            Assert(ParentWaited && !mutexHeld, "The app mutex was acquired in the wrong order.");
            mutexHeld = true;
            return Task.FromResult<IDisposable>(new Lease(() => mutexHeld = false));
        }
        public IUpdateChildProcess Start(string executable, IReadOnlyList<string> arguments)
        {
            Assert(!mutexHeld && executable == store.TargetPath, "The app was relaunched while the helper owned its mutex or at a different path.");
            Starts.Add(arguments);
            var confirm = arguments.Contains("--update-confirm");
            if (confirm && Acknowledge) store.Acknowledge(store.Load()!);
            return new Child(confirm && NewAppExits, () => StoppedChild = true);
        }
        private sealed class Lease(Action release) : IDisposable { public void Dispose() => release(); }
        private sealed class Child(bool exited, Action stopped) : IUpdateChildProcess
        {
            public bool HasExited { get; private set; } = exited;
            public Task StopAsync(CancellationToken cancellationToken) { stopped(); HasExited = true; return Task.CompletedTask; }
            public void Dispose() { }
        }
    }
}
