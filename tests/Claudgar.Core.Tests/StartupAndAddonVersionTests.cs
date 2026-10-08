using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Claudgar.Core.Setup;

internal static class StartupAndAddonVersionTests
{
    public static void StartupDefaultPersistenceAndRegistration() => InTemporaryFolder(folder =>
    {
        var settingsPath = Path.Combine(folder, "settings.json");
        var store = new SettingsStore(settingsPath);
        Assert(store.Load().Settings.LaunchOnStartup, "New settings must enable startup by default.");
        File.WriteAllText(settingsPath, "{\"SchemaVersion\":1,\"Port\":43827,\"GameDirectories\":[]}");
        Assert(store.Load().Settings.LaunchOnStartup, "Settings without a preference must enable startup.");
        store.Save(store.Load().Settings with { LaunchOnStartup = false });
        Assert(!store.Load().Settings.LaunchOnStartup, "An explicit disabled choice was lost.");

        var backend = new FakeStartupBackend();
        var service = new StartupRegistrationService(backend);
        var executable = Path.Combine(folder, "App With Spaces", "Claudgar.exe");
        var registered = service.Apply(true, executable);
        Assert(registered.Succeeded && registered.Enabled && registered.Changed, "Startup was not enabled.");
        Assert(backend.Command == "\"" + executable + "\" --startup", "The startup path was not correctly quoted.");
        Assert(!service.Apply(true, executable).Changed && backend.Writes == 1, "An unchanged startup entry was written again.");
        var relocated = Path.Combine(folder, "Moved App", "Claudgar.exe");
        Assert(service.Apply(true, relocated).Succeeded && backend.Command == StartupRegistrationService.BuildCommand(relocated),
            "An enabled startup entry did not follow a moved portable app.");
        Assert(service.Apply(false, relocated) is { Succeeded: true, Enabled: false, Changed: true } && backend.Command is null,
            "Disabling startup did not remove the Claudgar entry.");
        Assert(!service.Apply(false, relocated).Changed, "Repeated disabling wrote to startup registration.");
    });

    public static void StartupFailurePreservesEffectiveState() => InTemporaryFolder(folder =>
    {
        var executable = Path.Combine(folder, "Claudgar.exe");
        var backend = new FakeStartupBackend { RejectWrites = true };
        var service = new StartupRegistrationService(backend);
        Assert(service.Apply(true, executable) is { Succeeded: false, Enabled: false, Warning: not null },
            "A failed registration reported startup enabled.");
        backend.RejectWrites = false;
        Assert(service.Apply(true, executable).Succeeded, "A failed registration could not be retried.");
        backend.RejectWrites = true;
        Assert(service.Apply(false, executable) is { Succeeded: false, Enabled: true, Warning: not null },
            "Failed removal lost the effective enabled state.");
        backend.RejectWrites = false;
        backend.Command = "user-owned-command --keep";
        Assert(!service.Apply(true, executable).Succeeded && backend.Command == "user-owned-command --keep",
            "An unknown startup command was overwritten.");
        Assert(!service.Apply(false, executable).Succeeded && backend.Command == "user-owned-command --keep",
            "An unknown startup command was removed.");
        foreach (var invalid in new[] { "relative/Claudgar.exe", executable + "\" --other", executable + "\n" })
            Assert(!service.Apply(true, invalid).Succeeded, "An unsafe startup path was accepted.");
    });

    public static void AddonVersionsInstallMigrateUpgradeAndRollback() => InTemporaryFolder(folder =>
    {
        var installation = GameFixture(folder, "Game One");
        var directory = Path.Combine(installation.AddonsDirectory, "Claudgar");
        var original = Payload("version one");
        // Existing owned installations without a version marker are migrated even when bytes match.
        Assert(new OwnedFilesInstaller().Install(directory, original).Succeeded, "The migration fixture was not installed.");
        var addonFile = Path.Combine(directory, "Claudgar.toc");
        var lastWrite = File.GetLastWriteTimeUtc(addonFile);
        var v1 = new SetupService(original, "skill", "0.1.0");
        Assert(v1.InstallAddon(installation) is { Succeeded: true, Changed: true } && Marker(directory) == "0.1.0",
            "An old owned installation did not record the current app version.");
        Assert(File.GetLastWriteTimeUtc(addonFile) == lastWrite, "Migrating metadata recopied identical addon bytes.");
        var backupCount = Directory.GetFiles(directory, "*.claudgar-backup-*").Length;
        var repeat = v1.InstallAddon(installation);
        Assert(repeat is { Succeeded: true, Changed: false }, "The same app version copied the addon again.");
        Assert(Directory.GetFiles(directory, "*.claudgar-backup-*").Length == backupCount,
            "A repeated same-version launch wrote ownership metadata.");
        File.Delete(addonFile);
        Assert(!v1.InstallAddon(installation).Changed && !File.Exists(addonFile), "Routine version checks did not skip same-version copying.");
        Assert(v1.InstallAddon(installation, forceRepair: true).Succeeded && File.Exists(addonFile),
            "Explicit repair did not restore a missing file at the same version.");

        var savedVariables = Path.Combine(installation.AccountDirectory, "TEST", "SavedVariables", "Claudgar.lua");
        Directory.CreateDirectory(Path.GetDirectoryName(savedVariables)!);
        File.WriteAllText(savedVariables, "saved character data");
        var unrelatedAddon = Path.Combine(installation.AddonsDirectory, "OtherAddon", "Other.lua");
        Directory.CreateDirectory(Path.GetDirectoryName(unrelatedAddon)!);
        File.WriteAllText(unrelatedAddon, "other addon");
        var v2 = new SetupService(Payload("version two"), "skill", "0.2.0");
        Assert(v2.InstallAddon(installation).Succeeded && Marker(directory) == "0.2.0" && File.ReadAllText(addonFile) == "version two",
            "Upgrading did not update both addon bytes and its success marker.");
        Assert(v1.InstallAddon(installation).Succeeded && Marker(directory) == "0.1.0" && File.ReadAllText(addonFile) == "version one",
            "Rolling back did not reconcile the addon with the older app.");
        Assert(File.ReadAllText(savedVariables) == "saved character data" && File.ReadAllText(unrelatedAddon) == "other addon",
            "An addon update altered saved character data or an unrelated addon.");

        var second = GameFixture(folder, "Game Two");
        Assert(v2.InstallAddon(second).Succeeded, "A new game installation was not installed.");
        Assert(Marker(Path.Combine(second.AddonsDirectory, "Claudgar")) == "0.2.0" && Marker(directory) == "0.1.0",
            "Separate game installations shared their version marker.");
        var v3 = new SetupService(Payload("version two"), "skill", "0.3.0");
        Assert(v3.InstallAddon(second).Changed && Marker(Path.Combine(second.AddonsDirectory, "Claudgar")) == "0.3.0",
            "An app update with identical addon bytes did not advance the marker.");
    });

    public static void AddonFailuresRetryAndPendingSameVersionResumes() => InTemporaryFolder(folder =>
    {
        var installation = GameFixture(folder, "Game");
        var directory = Path.Combine(installation.AddonsDirectory, "Claudgar");
        var v1 = new SetupService(Payload("original"), "skill", "0.1.0");
        Assert(v1.InstallAddon(installation).Succeeded, "The first addon install failed.");
        var addonFile = Path.Combine(directory, "Claudgar.toc");
        File.WriteAllText(addonFile, "user customization");
        var v2 = new SetupService(Payload("updated"), "skill", "0.2.0");
        Assert(!v2.InstallAddon(installation).Succeeded && Marker(directory) == "0.1.0" &&
            File.ReadAllText(addonFile) == "user customization", "A failed install advanced its marker or overwrote user changes.");
        File.WriteAllText(addonFile, "original");
        using (var locked = new FileStream(addonFile, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert(!v2.InstallAddon(installation).Succeeded && Marker(directory) == "0.1.0", "A locked copy advanced its marker.");
        Assert(v2.InstallAddon(installation).Succeeded && Marker(directory) == "0.2.0", "A failed install did not retry successfully.");

        // An interrupted explicit repair must resume even if the successful marker equals this app version.
        var manifestPath = Path.Combine(directory, ".claudgar-owned-files.json");
        var pending = new
        {
            Owner = "Claudgar", SchemaVersion = 1, successfulAppVersion = "0.2.0",
            Files = new Dictionary<string, string> { ["Claudgar.toc"] = Hash("original") },
            PendingFiles = new Dictionary<string, string> { ["Claudgar.toc"] = Hash("updated") }
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(pending));
        Assert(v2.InstallAddon(installation) is { Succeeded: true, Changed: true }, "A pending same-version installation was skipped.");
        using (var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath)))
            Assert(!manifest.RootElement.GetProperty("PendingFiles").EnumerateObject().Any(), "An interrupted journal was not cleared.");

        var unowned = GameFixture(folder, "Unowned Game");
        var unownedDirectory = Path.Combine(unowned.AddonsDirectory, "Claudgar");
        Directory.CreateDirectory(unownedDirectory);
        File.WriteAllText(Path.Combine(unownedDirectory, "Claudgar.toc"), "user addon");
        Assert(!v2.InstallAddon(unowned).Succeeded && !File.Exists(Path.Combine(unownedDirectory, ".claudgar-owned-files.json")),
            "A missing version marker authorized adopting an unowned addon.");
        // Addon success is recorded independently even when another setup component fails.
        var malformedCodex = Path.Combine(folder, "config.toml");
        File.WriteAllText(malformedCodex, "[mcp_servers.claudgar]\nurl = 'user-custom-server'\n");
        var third = GameFixture(folder, "Independent Game");
        var report = v2.Install([third], 43827, malformedCodex, Path.Combine(folder, "skill"));
        Assert(!report.Codex.Succeeded && report.Addons.Single().Succeeded &&
            Marker(Path.Combine(third.AddonsDirectory, "Claudgar")) == "0.2.0", "Chatbot setup failure prevented an addon success marker.");
    });

    private static IReadOnlyDictionary<string, byte[]> Payload(string content) =>
        new Dictionary<string, byte[]> { ["Claudgar.toc"] = Encoding.UTF8.GetBytes(content) };

    private static string Hash(string content) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    private static string? Marker(string directory)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, ".claudgar-owned-files.json")));
        return manifest.RootElement.TryGetProperty("successfulAppVersion", out var value) ? value.GetString() : null;
    }

    private static GameInstallation GameFixture(string folder, string name)
    {
        var root = Path.Combine(folder, name);
        var game = Path.Combine(root, GameDiscovery.ClientDirectoryName);
        Directory.CreateDirectory(game);
        File.WriteAllBytes(Path.Combine(game, "WowB.exe"), []); // Presence-only fixture, never executed.
        File.WriteAllText(Path.Combine(root, ".build.info"),
            "Version!STRING:0|Product!STRING:0|Active!DEC:1\n1.60.1.69913|wow_classic_beta|1\n");
        return new GameDiscovery().Validate(game).Installation ?? throw new InvalidOperationException("Invalid game fixture.");
    }

    private static void InTemporaryFolder(Action<string> run)
    {
        var root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".tmp", "startup-addon-tests"));
        var folder = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try { run(folder); }
        finally
        {
            var target = Path.GetFullPath(folder);
            if (Path.GetDirectoryName(target) != root || (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("The test cleanup path changed.");
            Directory.Delete(target, recursive: true);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class FakeStartupBackend : IStartupRegistrationBackend
    {
        public string? Command { get; set; }
        public int Writes { get; private set; }
        public bool RejectWrites { get; set; }
        public string? ReadCommand() => Command;
        public void WriteCommand(string command)
        {
            if (RejectWrites) throw new UnauthorizedAccessException("Simulated startup registration failure.");
            Command = command;
            Writes++;
        }
        public void RemoveCommand()
        {
            if (RejectWrites) throw new UnauthorizedAccessException("Simulated startup registration failure.");
            Command = null;
            Writes++;
        }
    }
}
