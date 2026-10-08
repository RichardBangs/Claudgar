using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Claudgar.Core.Setup;

internal static class SetupRegressionTests
{
    public static void ManagedTomlPreservesUnrelatedConfiguration()
    {
        // Marker-like text inside a multiline value must never be treated as app ownership.
        var unrelated = """"
            model = 'user-selected-model'
            note = """
            # BEGIN CLAUDGAR MANAGED MCP
            [mcp_servers.claudgar]
            url = "http://not-a-real-setting.invalid"
            # END CLAUDGAR MANAGED MCP
            """
            [mcp_servers.other]
            url = 'https://example.test/mcp'
            [projects.'C:\Games']
            trust_level = 'trusted'
            """".Replace("\n", "\r\n", StringComparison.Ordinal);
        var installer = new CodexConfigurationInstaller();
        var initial = installer.Merge(unrelated, 43827);
        Assert(initial.Succeeded && initial.Changed, "Initial registration failed.");
        Assert(initial.Content.StartsWith(unrelated, StringComparison.Ordinal), "Unrelated config bytes changed.");
        var repeat = installer.Merge(initial.Content, 43827);
        Assert(repeat.Succeeded && !repeat.Changed && repeat.Content == initial.Content, "Repeated setup changed configuration.");
        var update = installer.Merge(repeat.Content, 43828);
        Assert(update.Succeeded && update.Changed, "Owned connection port could not be updated.");
        Assert(update.Content.StartsWith(unrelated, StringComparison.Ordinal), "Updating the managed block changed unrelated config.");
        Assert(update.Content.Contains("url = \"http://127.0.0.1:43828/mcp\"", StringComparison.Ordinal), "Updated port is missing.");
    }

    public static void ConflictingTomlIsPreserved()
    {
        var installer = new CodexConfigurationInstaller();
        foreach (var original in new[]
        {
            "[mcp_servers.\"claudgar\"]\nurl = 'https://user.example/mcp'\nenabled = false\n",
            "mcp_servers = { claudgar = { url = 'https://user.example/mcp' } }\n",
            "[mcp_servers]\nclaudgar.url = 'https://user.example/mcp'\n",
            "[mcp_servers.claudgar]\nurl = 'http://127.0.0.1:43827/mcp'\nenabled = false\n",
            "[mcp_servers.claudgar]\nurl = 'https://user.example/mcp'\n[mcp_servers.claudgar.env]\nKEEP = 'yes'\n"
        })
        {
            var result = installer.Merge(original, 43827);
            Assert(!result.Succeeded && !result.Changed && result.Content == original, "A conflicting user server was modified.");
        }
        var compatible = "[mcp_servers.'claudgar']\nurl = 'http://127.0.0.1:43827/mcp'\n";
        var existing = installer.Merge(compatible, 43827);
        Assert(existing.Succeeded && !existing.Changed && existing.Content == compatible, "A compatible user server was not preserved.");
        var managed = installer.Merge("", 43827).Content;
        var edited = managed.Replace("enabled = true", "enabled = false", StringComparison.Ordinal);
        var protectedResult = installer.Merge(edited, 43828);
        Assert(!protectedResult.Succeeded && protectedResult.Content == edited, "A user edit to the managed block was overwritten.");
        var unfinished = "note = \"\"\"unfinished\n";
        Assert(installer.Merge(unfinished, 43827).Content == unfinished, "An unfinished multiline value was changed.");
    }

    public static void OwnedInstallIsRepeatableAndPreservesChanges() => InTemporaryFolder(folder =>
    {
        var destination = Path.Combine(folder, "Claudgar");
        var resources = new Dictionary<string, byte[]>
        {
            ["Claudgar.toc"] = Bytes("## Title: Claudgar\nCollectors/Character.lua\n"),
            ["Collectors/Character.lua"] = Bytes("local version = 1\n")
        };
        var installer = new OwnedFilesInstaller();
        var first = installer.Install(destination, resources);
        Assert(first.Succeeded && first.Changed, "Initial owned-file install failed.");
        var repeat = installer.Install(destination, resources);
        Assert(repeat.Succeeded && !repeat.Changed, "Repeated install should leave identical files alone.");
        var unrelated = Path.Combine(destination, "user-notes.txt");
        File.WriteAllText(unrelated, "keep this");
        resources["Collectors/Character.lua"] = Bytes("local version = 2\n");
        var update = installer.Install(destination, resources);
        Assert(update.Succeeded && update.Changed, "An owned file could not be updated.");
        Assert(File.ReadAllText(unrelated) == "keep this", "An unrelated file changed.");
        Assert(Directory.EnumerateFiles(Path.Combine(destination, "Collectors"), "*.claudgar-backup-*").Any(), "An overwritten owned file was not backed up.");
        var customized = Path.Combine(destination, "Collectors", "Character.lua");
        File.WriteAllText(customized, "user customization");
        var conflict = installer.Install(destination, resources);
        Assert(!conflict.Succeeded && File.ReadAllText(customized) == "user customization", "A user customization was overwritten.");
        var unknown = Path.Combine(folder, "ExistingSkill");
        Directory.CreateDirectory(unknown);
        File.WriteAllText(Path.Combine(unknown, "SKILL.md"), "user-owned skill");
        Assert(!installer.Install(unknown, new Dictionary<string, byte[]> { ["SKILL.md"] = Bytes("packaged skill") }).Succeeded,
            "An unowned skill folder was adopted.");
        Assert(File.ReadAllText(Path.Combine(unknown, "SKILL.md")) == "user-owned skill", "An existing user skill was overwritten.");
    });

    public static void OwnedInstallRejectsEscapingPaths() => InTemporaryFolder(folder =>
    {
        var destination = Path.Combine(folder, "Claudgar");
        var escaped = Path.Combine(folder, "outside.lua");
        File.WriteAllText(escaped, "keep outside");
        var installer = new OwnedFilesInstaller();
        foreach (var relative in new[] { "../outside.lua", "nested/../../outside.lua", escaped })
        {
            var result = installer.Install(destination, new Dictionary<string, byte[]> { [relative] = Bytes("overwrite") });
            Assert(!result.Succeeded, "An escaping resource path was accepted.");
            Assert(File.ReadAllText(escaped) == "keep outside", "A resource escaped the installation directory.");
        }
        var duplicate = installer.Install(destination, new Dictionary<string, byte[]>
        {
            ["Claudgar.toc"] = Bytes("first"), ["nested/../Claudgar.toc"] = Bytes("second")
        });
        Assert(!duplicate.Succeeded && !File.Exists(Path.Combine(destination, "Claudgar.toc")), "Duplicate resolved paths passed preflight.");
    });

    public static void InterruptedOwnedInstallCanResume() => InTemporaryFolder(folder =>
    {
        var destination = Path.Combine(folder, "Claudgar");
        Directory.CreateDirectory(destination);
        var content = Bytes("known packaged bytes");
        File.WriteAllBytes(Path.Combine(destination, "Claudgar.toc"), content);
        var manifest = new
        {
            Owner = "Claudgar", SchemaVersion = 1,
            Files = new Dictionary<string, string>(),
            PendingFiles = new Dictionary<string, string>
            {
                ["Claudgar.toc"] = Convert.ToHexStringLower(SHA256.HashData(content))
            }
        };
        File.WriteAllText(Path.Combine(destination, ".claudgar-owned-files.json"), JsonSerializer.Serialize(manifest));
        var result = new OwnedFilesInstaller().Install(destination, new Dictionary<string, byte[]> { ["Claudgar.toc"] = content });
        Assert(result.Succeeded, "An interrupted first install could not resume from its ownership journal.");
        Assert(!new OwnedFilesInstaller().Install(destination, new Dictionary<string, byte[]> { ["Claudgar.toc"] = content }).Changed,
            "A resumed installation is not stable.");
    });

    public static void MalformedSettingsArePreserved() => InTemporaryFolder(folder =>
    {
        var path = Path.Combine(folder, "settings.json");
        const string original = "{ unfinished user settings";
        File.WriteAllText(path, original);
        var store = new SettingsStore(path);
        Assert(store.Load().Warning is not null, "Invalid settings were not reported.");
        bool rejected = false;
        try { store.Save(new AppSettings()); }
        catch (IOException) { rejected = true; }
        Assert(rejected && File.ReadAllText(path) == original, "Malformed settings were reset.");
        File.WriteAllText(path, "{\"SchemaVersion\":1,\"Port\":43827,\"GameDirectories\":[],\"UserPreference\":\"keep\"}");
        store.Save(store.Load().Settings with { Port = 43828 });
        var updated = JsonDocument.Parse(File.ReadAllText(path));
        using (updated)
            Assert(updated.RootElement.GetProperty("Port").GetInt32() == 43828 &&
                   updated.RootElement.GetProperty("UserPreference").GetString() == "keep", "An unrelated setting was discarded.");
    });

    public static void ForeverValidationDiscriminatesSharedBetaSlot() => InTemporaryFolder(folder =>
    {
        var game = Path.Combine(folder, GameDiscovery.ClientDirectoryName);
        Directory.CreateDirectory(game);
        File.WriteAllBytes(Path.Combine(game, "WowB.exe"), []); // Presence-only fixture, never executed.
        var discovery = new GameDiscovery();
        void Manifest(string version, string product = "wow_classic_beta", int active = 1) =>
            File.WriteAllText(Path.Combine(folder, ".build.info"),
                "Branch!STRING:0|Version!STRING:0|Product!STRING:0|Active!DEC:1\n" +
                "eu|12.1.0.69875|wow|1\n" + $"eu|{version}|{product}|{active}\n");
        foreach (var version in new[] { "1.60.1.69913", "1.60.9.76543" })
        {
            Manifest(version);
            Assert(discovery.Validate(folder).IsValid && discovery.Validate(game).Installation?.Version == version,
                "A Forever patch/build in its supported client family was rejected.");
        }
        foreach (var version in new[] { "1.15.8.69913", "5.5.2.69913", "12.1.0.69913" })
        {
            Manifest(version);
            Assert(!discovery.Validate(game).IsValid, "A different beta sharing the product slot was accepted.");
        }
        Manifest("1.60.1.69913", "wow_classic_era");
        Assert(!discovery.Validate(game).IsValid, "A different product was accepted.");
        Manifest("1.60.1.69913", active: 0);
        Assert(!discovery.Validate(game).IsValid, "An inactive install row was accepted.");
        Manifest("1.60.1.69913");
        var retail = Path.Combine(folder, "_retail_");
        Directory.CreateDirectory(retail);
        Assert(!discovery.Validate(retail).IsValid, "Explicit Retail selection was accepted as Forever.");
    });

    public static void GameDiscoveryFindsRootOrBetaDirectory() => InTemporaryFolder(folder =>
    {
        var root = Path.Combine(folder, "World of Warcraft");
        var beta = Path.Combine(root, GameDiscovery.ClientDirectoryName);
        Directory.CreateDirectory(beta);
        File.WriteAllBytes(Path.Combine(beta, "WowB.exe"), []);
        File.WriteAllText(Path.Combine(root, ".build.info"),
            "Version!STRING:0|Product!STRING:0|Active!DEC:1\n1.60.1.69913|wow_classic_beta|1\n");
        var discovery = new GameDiscovery();
        var found = discovery.Discover([root, beta, root + Path.DirectorySeparatorChar]);
        Assert(found.Count(game => game.GameDirectory == beta) == 1,
            "Root and beta candidates did not resolve to one installation.");
        Assert(discovery.SuggestDirectory([root]) == beta && discovery.SuggestDirectory([beta]) == beta,
            "Manual selection did not start in the existing beta directory.");
        File.Delete(Path.Combine(beta, "WowB.exe"));
        Assert(!discovery.Discover([root]).Any(game => game.GameDirectory == beta),
            "An incomplete install was accepted during discovery.");
        Assert(discovery.SuggestDirectory([root]) == beta,
            "An incomplete install should still be offered as a manual-selection starting point.");
    });

    public static void InTemporaryFolder(Action<string> test)
    {
        var tempRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".tmp", "core-tests"))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = "Claudgar.Setup.Tests." + Guid.NewGuid().ToString("N");
        var createdFolder = Path.GetFullPath(Path.Combine(tempRoot, name));
        Directory.CreateDirectory(createdFolder);
        try { test(createdFolder); }
        finally
        {
            // Resolve and verify the exact created workspace before any recursive cleanup.
            var target = Path.GetFullPath(createdFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (target != createdFolder || !target.StartsWith(tempRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                Path.GetDirectoryName(target) != tempRoot || Path.GetFileName(target) != name)
                throw new InvalidOperationException("Temporary cleanup target escaped the created test folder.");
            if (Directory.Exists(target))
            {
                if ((File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Temporary cleanup target became a directory link.");
                Directory.Delete(target, recursive: true);
            }
        }
    }

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
