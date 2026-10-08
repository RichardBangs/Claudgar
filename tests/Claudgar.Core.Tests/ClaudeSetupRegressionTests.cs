using System.Text;
using System.Text.Json.Nodes;
using Claudgar.Core.Setup;

internal static class ClaudeSetupRegressionTests
{
    public static void ClaudeConfigurationPreservesSettings() => InTemporaryFolder(folder =>
    {
        var path = Path.Combine(folder, "Claude", "claude_desktop_config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        const string original = "{\"preferences\":{\"theme\":\"dark\"},\"mcpServers\":{\"other\":{\"command\":\"other-helper\",\"args\":[\"keep\"]}}}";
        File.WriteAllText(path, original);
        var helper = Path.Combine(folder, "folder with spaces", "Claudgar.McpBridge.exe");
        var installer = new ClaudeConfigurationInstaller();
        var first = installer.Register(helper, 43827, path);
        Assert(first.Succeeded && first.Changed, "Claude registration failed.");
        var installed = File.ReadAllText(path);
        var json = JsonNode.Parse(installed)!;
        Assert(json["preferences"]!["theme"]!.GetValue<string>() == "dark", "Unrelated Claude settings were lost.");
        Assert(json["mcpServers"]!["other"]!["command"]!.GetValue<string>() == "other-helper", "Unrelated server was changed.");
        Assert(json["mcpServers"]!["claudgar"]!["command"]!.GetValue<string>() == helper, "Helper path with spaces was corrupted.");
        Assert(Directory.GetFiles(Path.GetDirectoryName(path)!, "claude_desktop_config.json.claudgar-backup-*").Any(), "Original configuration was not backed up.");
        var repeat = installer.Register(helper, 43827, path);
        Assert(repeat.Succeeded && !repeat.Changed && File.ReadAllText(path) == installed, "Repeated registration rewrote configuration.");
        var update = installer.Register(helper, 43828, path);
        Assert(update.Succeeded && update.Changed, "Owned connection port could not be updated.");
        Assert(installer.Check(helper, 43828, path).Succeeded, "Updated registration failed its read-only check.");
        var edited = JsonNode.Parse(File.ReadAllText(path))!;
        edited["mcpServers"]!["claudgar"]!["env"] = new JsonObject { ["USER_CHANGE"] = "keep" };
        File.WriteAllText(path, edited.ToJsonString());
        var userContent = File.ReadAllText(path);
        Assert(!installer.Register(helper, 43829, path).Succeeded && File.ReadAllText(path) == userContent, "A user edit to the managed server was overwritten.");
    });

    public static void ClaudeMalformedAndConflictingConfigurationIsPreserved()
    {
        var installer = new ClaudeConfigurationInstaller();
        var helper = Path.GetFullPath(".tmp/test bridge/Claudgar.McpBridge.exe");
        foreach (var original in new[]
        {
            "{unfinished", "[]", "{\"mcpServers\":false}", "{\"mcpServers\":null}",
            "{\"mcpServers\":{},\"mcpServers\":{}}",
            "{\"mcpServers\":{\"claudgar\":{\"command\":\"user-helper\"}}}",
            "{\"mcpServers\":{\"claudgar\":null}}"
        })
        {
            var result = installer.Merge(original, helper, 43827);
            Assert(!result.Succeeded && !result.Changed && result.Content == original, "Malformed or conflicting JSON was changed.");
        }
    }

    public static void NativeBridgeInstallationAndLockedUpdate() => InTemporaryFolder(folder =>
    {
        var directory = Path.Combine(folder, "Claude helper");
        var installer = new ClaudeBridgeInstaller();
        // More than 16 MiB verifies that executable installation does not use the export reader limit.
        var firstBinary = new byte[17 * 1024 * 1024];
        firstBinary[0] = 1;
        var first = installer.Install(firstBinary, directory);
        Assert(first.Succeeded && first.Changed, "Large native helper installation failed.");
        Assert(installer.Install(firstBinary, directory) is { Succeeded: true, Changed: false }, "Identical helper changed during repeat setup.");
        var updated = (byte[])firstBinary.Clone();
        updated[0] = 2;
        using (var locked = new FileStream(first.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var deferred = installer.Install(updated, directory);
            Assert(!deferred.Succeeded && deferred.Message.Contains("Quit Claude", StringComparison.Ordinal), "Locked helper update did not explain recovery.");
            Assert(ClaudeBridgeInstaller.HashFile(first.Path) == Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(firstBinary)), "The working locked helper was changed.");
            Assert(File.Exists(first.Path + ".pending"), "Locked update did not retain the verified pending payload.");
        }
        Assert(installer.Install(updated, directory).Succeeded, "Pending helper update did not resume after unlocking.");
        Assert(installer.Check(updated, directory).Succeeded, "Updated helper failed its read-only health check.");
        File.WriteAllText(first.Path, "user-owned edit");
        Assert(!installer.Install(firstBinary, directory).Succeeded && File.ReadAllText(first.Path) == "user-owned edit", "A changed native helper was overwritten.");
        var unowned = Path.Combine(folder, "unowned");
        Directory.CreateDirectory(unowned);
        File.WriteAllText(Path.Combine(unowned, "notes.txt"), "keep");
        Assert(!installer.Install(firstBinary, unowned).Succeeded, "An unowned helper folder was adopted.");
        var untrustedTarget = Path.Combine(unowned, ClaudeBridgeInstaller.ExecutableName);
        File.WriteAllText(untrustedTarget, "user-owned helper");
        var configPath = Path.Combine(folder, "untrusted-claude.json");
        var unsafeSetup = new ClaudeSetupService(firstBinary).Install(43827, configPath, unowned);
        Assert(!unsafeSetup.Succeeded && !File.Exists(configPath), "An unowned executable was registered with Claude.");
    });

    public static void ClaudeInterruptedSetupResumes() => InTemporaryFolder(folder =>
    {
        var path = Path.Combine(folder, "claude_desktop_config.json");
        var helper = Path.Combine(folder, "Claudgar.McpBridge.exe");
        var installer = new ClaudeConfigurationInstaller();
        File.WriteAllText(path, "");
        Assert(!installer.Register(helper, 43827, path).Succeeded && File.ReadAllText(path) == "", "An empty existing Claude config was overwritten.");
        File.Delete(path);
        var merged = installer.Merge("", helper, 43827);
        File.WriteAllText(path, merged.Content);
        // Simulate interruption after JSON replacement but before the ownership commit.
        File.WriteAllText(path + ".claudgar-owned-entry.json", new JsonObject
        {
            ["Owner"] = "Claudgar", ["SchemaVersion"] = 1, ["EntryHash"] = null, ["PendingEntryHash"] = merged.EntryHash
        }.ToJsonString());
        Assert(installer.Register(helper, 43828, path).Succeeded, "Interrupted Claude registration could not resume.");

        var directory = Path.Combine(folder, "helper");
        var bridge = new ClaudeBridgeInstaller();
        var first = Encoding.UTF8.GetBytes("first helper");
        var second = Encoding.UTF8.GetBytes("second helper");
        var third = Encoding.UTF8.GetBytes("third helper");
        Assert(bridge.Install(first, directory).Succeeded, "Initial interrupted-helper fixture failed.");
        File.WriteAllBytes(Path.Combine(directory, "Claudgar.McpBridge.exe.pending"), second);
        // Simulate interruption between journaling an updated stage and replacing the previous stage.
        File.WriteAllText(Path.Combine(directory, ".claudgar-bridge.json"), new JsonObject
        {
            ["Owner"] = "Claudgar", ["SchemaVersion"] = 1,
            ["InstalledHash"] = Hash(first), ["PendingHash"] = Hash(third), ["PreviousPendingHash"] = Hash(second)
        }.ToJsonString());
        Assert(bridge.Install(third, directory).Succeeded && bridge.Check(third, directory).Succeeded, "Interrupted helper stage did not resume safely.");
        using (var locked = new FileStream(Path.Combine(directory, ClaudeBridgeInstaller.ExecutableName), FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert(!bridge.Install(second, directory).Succeeded, "Locked rollback-stage fixture failed.");
        Assert(bridge.Install(third, directory).Succeeded, "Rolling back to the working helper could not cancel the owned pending update.");
        Assert(bridge.Install(second, directory).Succeeded, "Cancelled pending metadata prevented a later helper update.");
    });

    public static void ClaudeStoreAndClassicConfigurationsAreDetected() => InTemporaryFolder(folder =>
    {
        var roaming = Path.Combine(folder, "Roaming");
        var local = Path.Combine(folder, "Local");
        var locator = new ClaudeDesktopConfigLocator(roaming, local);
        Assert(locator.Find().Count == 0, "Claude Desktop was detected without any configuration folder.");
        var helperDirectory = Path.Combine(folder, "helper");
        var binary = Encoding.UTF8.GetBytes("helper");
        var absent = new ClaudeSetupService(binary, locator).Install(43827, helperDirectory: helperDirectory);
        Assert(!absent.Detected && absent.Succeeded && !Directory.Exists(helperDirectory), "Missing Claude Desktop installed files or reported an error.");

        // A Store package that has never started has no virtualised Claude folder yet, and other packages never match.
        Directory.CreateDirectory(Path.Combine(local, "Packages", "Claude_neverstarted"));
        Directory.CreateDirectory(Path.Combine(local, "Packages", "ClaudeHelper_other", "LocalCache", "Roaming", "Claude"));
        Assert(locator.Find().Count == 0, "An unstarted or unrelated package was treated as Claude Desktop.");

        var storeDirectory = Path.Combine(local, "Packages", "Claude_pzs8sxrjxfjjc", "LocalCache", "Roaming", "Claude");
        Directory.CreateDirectory(storeDirectory);
        var storeConfig = Path.Combine(storeDirectory, ClaudeDesktopConfigLocator.ConfigFileName);
        File.WriteAllText(storeConfig, "{\"preferences\":{\"keep\":true}}");
        var storeOnly = locator.Find();
        Assert(storeOnly.Count == 1 && storeOnly[0].IsStorePackage && storeOnly[0].Path == storeConfig, "The Microsoft Store configuration was not found.");

        Directory.CreateDirectory(Path.Combine(roaming, "Claude"));
        var both = locator.Find();
        Assert(both.Count == 2 && !both[0].IsStorePackage && both[1].IsStorePackage, "Classic and Store configurations must both be found.");

        var service = new ClaudeSetupService(binary, locator);
        var installed = service.Install(43827, helperDirectory: helperDirectory);
        Assert(installed.Detected && installed.Succeeded && installed.Configurations.Count == 2, "Both Claude Desktop configurations were not registered: " + installed.Configuration.Message);
        Assert(JsonNode.Parse(File.ReadAllText(storeConfig))!["preferences"]!["keep"]!.GetValue<bool>(), "Store Claude settings were lost.");
        foreach (var location in both)
            Assert(JsonNode.Parse(File.ReadAllText(location.Path))!["mcpServers"]!["claudgar"]!["command"]!.GetValue<string>() == installed.Helper.Path,
                "A Claude Desktop configuration does not point at the shared helper: " + location.Path);
        Assert(service.Check(43827, helperDirectory: helperDirectory).Succeeded, "Registered Claude Desktop configurations failed their check.");
        Assert(service.Install(43827, helperDirectory: helperDirectory) is { Succeeded: true, Changed: false }, "Repeated multi-location setup changed files.");

        // One conflicting file must not block or hide the other installation's result.
        var classicConfig = both[0].Path;
        File.WriteAllText(classicConfig, "{\"mcpServers\":{\"claudgar\":{\"command\":\"user-helper\"}}}");
        var conflict = service.Install(43828, helperDirectory: helperDirectory);
        Assert(!conflict.Succeeded && !conflict.Configurations[0].Result.Succeeded && conflict.Configurations[1].Result.Succeeded,
            "Each Claude Desktop configuration must be reported independently.");
        Assert(File.ReadAllText(classicConfig).Contains("user-helper", StringComparison.Ordinal), "A user-managed classic entry was overwritten.");
        Assert(conflict.Configuration.Message.Contains("Microsoft Store", StringComparison.Ordinal), "The combined Desktop result does not identify each installation.");
    });

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));

    private static void InTemporaryFolder(Action<string> test)
    {
        var folder = Path.GetFullPath(Path.Combine(".tmp", "claude-setup-tests", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(folder);
        try { test(folder); }
        finally { Directory.Delete(folder, recursive: true); }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
