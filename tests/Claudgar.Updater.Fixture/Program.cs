using System.Reflection;
using Claudgar.App.Updates;

// An inert console fixture for disposable updater tests. It has no UI, game discovery,
// registry, addon installer, or network request. Never copy it into the runnable app build.
if (AppUpdater.IsHelperInvocation(args)) return AppUpdater.RunHelper(args);
var testRoot = Environment.GetEnvironmentVariable("CLAUDGAR_UPDATER_TEST_ROOT");
var mutexName = Environment.GetEnvironmentVariable("CLAUDGAR_UPDATER_TEST_MUTEX");
if (string.IsNullOrWhiteSpace(testRoot) || string.IsNullOrWhiteSpace(mutexName) || !Directory.Exists(testRoot)) return 2;
var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
using var mutex = new Mutex(true, mutexName, out var firstInstance);
if (!firstInstance) return 3;
try
{
    if (args.Contains("--test-parent", StringComparer.Ordinal))
    {
        File.WriteAllText(Path.Combine(testRoot, "parent-running.txt"), Environment.ProcessId.ToString());
        Thread.Sleep(700);
    }
    using var updater = new AppUpdater(version, mutexName, Path.Combine(testRoot, "updates"));
    if (!AppUpdater.IsConfirmationInvocation(args) && updater.TryStartPendingUpdate(args.Contains("--startup", StringComparer.Ordinal), out _)) return 0;
    if (AppUpdater.IsConfirmationInvocation(args) && Environment.GetEnvironmentVariable("CLAUDGAR_UPDATER_TEST_FAIL") == "yes") return 1;
    updater.AcknowledgeHealthyLaunch(args);
    File.WriteAllText(Path.Combine(testRoot, "started.txt"), version + "|" + args.Contains("--startup", StringComparer.Ordinal));
    return 0;
}
finally { mutex.ReleaseMutex(); }
