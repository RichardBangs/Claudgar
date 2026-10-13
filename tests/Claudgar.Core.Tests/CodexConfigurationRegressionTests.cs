using System.Text;
using Claudgar.Core.Setup;

internal static class CodexConfigurationRegressionTests
{
    public static void RegistrationPreservesBytesAndBackups() => SetupRegressionTests.InTemporaryFolder(folder =>
    {
        var path = Path.Combine(folder, "config.toml");
        var installer = new CodexConfigurationInstaller();
        var original = Encoding.UTF8.GetBytes("\uFEFF# café and 中文\r\nmodel = 'user-model'\r\n[features]\r\nkeep = true\r\n");
        File.WriteAllBytes(path, original);
        var installed = installer.Register(43827, path);
        Check(installed.Succeeded && installed.Changed, installed.Message);
        var configured = File.ReadAllBytes(path);
        Check(configured.AsSpan().StartsWith(original), "Registration changed existing config bytes or encoding.");
        var backup = Directory.GetFiles(folder, "*.claudgar-backup-*").Single();
        Check(File.ReadAllBytes(backup).SequenceEqual(original), "Backup is not the exact original config.");
        var repeated = installer.Register(43827, path);
        Check(repeated.Succeeded && !repeated.Changed && File.ReadAllBytes(path).SequenceEqual(configured), "Repeated setup rewrote the config.");
        Check(Directory.GetFiles(folder, "*.claudgar-backup-*").Length == 1, "No-op setup created a backup.");
        var updated = installer.Register(43828, path);
        Check(updated.Succeeded && updated.Changed, updated.Message);
        Check(Directory.GetFiles(folder, "*.claudgar-backup-*").Any(file => File.ReadAllBytes(file).SequenceEqual(configured)),
            "A port update did not back up the previous connection.");
        Check(File.ReadAllBytes(path).AsSpan().StartsWith(original), "Port update changed unrelated bytes.");
        Check(!Directory.GetFiles(folder, ".claudgar-*.tmp").Any(), "Staging files remain after setup.");

        // Empty TOML is valid, including a freshly created config with no preferences yet.
        var empty = Path.Combine(folder, "empty.toml");
        File.WriteAllBytes(empty, []);
        Check(installer.Register(43827, empty).Succeeded, "A valid empty config was rejected.");
    });

    public static void CorruptionIsPreserved() => SetupRegressionTests.InTemporaryFolder(folder =>
    {
        var path = Path.Combine(folder, "config.toml");
        var installer = new CodexConfigurationInstaller();
        foreach (var original in new byte[][]
        {
            new byte[4606], // The failure observed on the user's machine.
            Encoding.UTF8.GetBytes("model = 'keep'\n# partially corrupted\0\n"),
            Encoding.UTF8.GetBytes("note = 'corrupted\0value'\n"),
            [0xff, 0xfe, 0x61],
            Encoding.UTF8.GetBytes("note = \"\"\"unfinished\n")
        })
        {
            File.WriteAllBytes(path, original);
            var result = installer.Register(43827, path);
            Check(!result.Succeeded && !result.Changed, "Corrupted config was accepted.");
            Check(File.ReadAllBytes(path).SequenceEqual(original), "Corrupted config was overwritten.");
            Check(!installer.Check(43827, path).Succeeded, "Health check accepted corrupt config.");
            Check(Directory.GetFiles(folder).Length == 1, "Failed setup created a backup or left a staging file.");
        }
    });

    public static void HealthChecksReadCurrentConfiguration() => SetupRegressionTests.InTemporaryFolder(folder =>
    {
        var path = Path.Combine(folder, "config.toml");
        var installer = new CodexConfigurationInstaller();
        Check(!installer.Check(43827, path).Succeeded && !File.Exists(path), "Health check created a missing config.");
        var registration = installer.Register(43827, path);
        Check(registration.Succeeded, registration.Message);
        var configured = File.ReadAllText(path);
        Check(installer.Check(43827, path).Succeeded, "Valid connection was not recognized.");
        foreach (var changed in new[]
        {
            "model = 'restored-backup'\n", "", new string('\0', 4606),
            configured.Replace("43827", "43828", StringComparison.Ordinal),
            configured.Replace("enabled = true", "enabled = false", StringComparison.Ordinal)
        })
        {
            File.WriteAllText(path, changed);
            var before = File.ReadAllBytes(path);
            var result = installer.Check(43827, path);
            Check(!result.Succeeded && !result.Changed, "Health check reused stale registration success.");
            Check(File.ReadAllBytes(path).SequenceEqual(before), "Health check modified the config.");
            Check(Directory.GetFiles(folder).Length == 1, "Health check wrote extra files.");
        }
        File.Delete(path);
        Check(!installer.Check(43827, path).Succeeded, "A deleted config still appeared configured.");
    });

    public static void ConcurrentChangesAndLockedFilesArePreserved() => SetupRegressionTests.InTemporaryFolder(folder =>
    {
        var path = Path.Combine(folder, "config.toml");
        var original = Encoding.UTF8.GetBytes("model = 'old'\n");
        var concurrent = Encoding.UTF8.GetBytes("model = 'new'\n");
        var candidate = Encoding.UTF8.GetBytes("model = 'old'\n# appended\n");
        File.WriteAllBytes(path, concurrent);
        Reject(() => GuardedConfigurationWriter.Write(path, candidate, original, 1024));
        Check(File.ReadAllBytes(path).SequenceEqual(concurrent), "A concurrent same-length edit was overwritten.");
        Reject(() => GuardedConfigurationWriter.Write(path, candidate, null, 1024));
        Check(File.ReadAllBytes(path).SequenceEqual(concurrent), "A concurrently created config was overwritten.");

        File.WriteAllBytes(path, original);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            Reject(() => GuardedConfigurationWriter.Write(path, candidate, original, 1024));
        Check(File.ReadAllBytes(path).SequenceEqual(original), "An actively written config was overwritten.");
        Reject(() => GuardedConfigurationWriter.Write(path, candidate, original, 1));
        Check(File.ReadAllBytes(path).SequenceEqual(original), "An oversized staged file was installed.");
        Check(Directory.GetFiles(folder).Length == 1, "Failed updates left a backup or staging file.");

        File.Delete(path);
        Reject(() => GuardedConfigurationWriter.Write(path, candidate, original, 1024));
        Check(Directory.GetFiles(folder).Length == 0, "A concurrently deleted file was recreated.");
    });

    private static void Reject(Action action)
    {
        try { action(); }
        catch (IOException) { return; }
        throw new InvalidOperationException("An unsafe config replacement was accepted.");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
