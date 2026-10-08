using System.Diagnostics;
using System.Text.Json;
using Claudgar.Core.Updates;

if (args.Length != 2 || !OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("Provide the old and new Windows x64 updater fixture executable paths.");
    return 2;
}
var repository = Environment.CurrentDirectory;
var failures = 0;
foreach (var test in new[] { (Startup: false, FailNew: false), (Startup: true, FailNew: false), (Startup: true, FailNew: true) })
{
    var root = Path.GetFullPath(Path.Combine(repository, ".tmp", "updater-process-tests", Guid.NewGuid().ToString("N")));
    try
    {
        Run(root, args[0], args[1], test.Startup, test.FailNew);
        Console.WriteLine($"PASS Real updater helper, parent exit, executable lock, mutex, {(test.FailNew ? "rollback" : "healthy acknowledgement")}, startup={test.Startup}");
    }
    catch (Exception error) { failures++; Console.Error.WriteLine("FAIL Real updater process: " + error); }
    finally
    {
        // Wait for the short-lived fixture/helper process to release its staging executable.
        for (var attempt = 0; attempt < 50; attempt++)
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); break; }
            catch (IOException) when (attempt < 49) { Thread.Sleep(100); }
            catch (UnauthorizedAccessException) when (attempt < 49) { Thread.Sleep(100); }
        }
    }
}
return failures == 0 ? 0 : 1;

static void Run(string root, string oldFixture, string newFixture, bool startup, bool failNew)
{
    var target = Path.Combine(root, "portable folder with spaces", "Claudgar.exe");
    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    File.Copy(oldFixture, target);
    var store = new UpdateStore(target, Path.Combine(root, "updates"));
    var hash = UpdateFiles.Hash(newFixture);
    var pending = new PendingUpdate
    {
        Token = Guid.NewGuid().ToString("N"), TargetPath = target, PreviousVersion = "0.1.0", Version = "0.2.0",
        Sha256 = hash.Sha256, Size = hash.Size, Phase = UpdatePhase.Ready
    };
    Directory.CreateDirectory(store.StageDirectory(pending));
    File.Copy(newFixture, store.StagedPath(pending));
    var validator = new WindowsUpdateExecutableValidator();
    validator.Validate(target, new(0, 1, 0));
    validator.Validate(store.StagedPath(pending), new(0, 2, 0));
    store.Save(pending);
    var originalHash = UpdateFiles.Hash(target);
    var start = new ProcessStartInfo(target) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
    start.Environment["CLAUDGAR_UPDATER_TEST_ROOT"] = root;
    start.Environment["CLAUDGAR_UPDATER_TEST_MUTEX"] = @"Local\Claudgar.UpdateTest." + pending.Token;
    start.Environment["CLAUDGAR_UPDATER_TEST_FAIL"] = failNew ? "yes" : "no";
    start.ArgumentList.Add("--test-parent");
    if (startup) start.ArgumentList.Add("--startup");
    using var parent = Process.Start(start) ?? throw new IOException("Could not start updater fixture.");
    var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
    var startedPath = Path.Combine(root, "started.txt");
    WaitUntil(() => File.Exists(Path.Combine(root, "parent-running.txt")), deadline, "The inert parent fixture did not start.");
    Assert(UpdateFiles.Hash(target) == originalHash, "The updater changed a still-running parent.");
    Assert(parent.WaitForExit(15000), "The old fixture did not shut down.");
    Assert(parent.ExitCode == 0, "The staged helper could not be scheduled.");
    WaitUntil(() => !File.Exists(store.ManifestPath) && File.Exists(startedPath), deadline, "The helper did not finish replacement or rollback.");
    var expected = (failNew ? "0.1.0" : "0.2.0") + "|" + startup;
    Assert(File.ReadAllText(startedPath) == expected, "The restarted app version or startup mode was incorrect.");
    var installedHash = UpdateFiles.Hash(target);
    Assert(installedHash == (failNew ? originalHash : hash), "The installed executable did not match the expected committed bytes.");
    Assert(failNew == File.Exists(store.FailurePath), "Rollback was not recorded independently of successful updates.");
    if (failNew) Assert(store.IsRejected(new(new(0, 2, 0), new("https://github.com/RichardBangs/Claudgar/releases/download/v0.2.0/Claudgar.exe"), hash.Sha256, hash.Size)),
        "A failed version could trigger another restart loop.");
}

static void WaitUntil(Func<bool> condition, DateTimeOffset deadline, string message)
{
    while (DateTimeOffset.UtcNow < deadline)
    {
        if (condition()) return;
        Thread.Sleep(50);
    }
    throw new InvalidOperationException(message);
}
static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
