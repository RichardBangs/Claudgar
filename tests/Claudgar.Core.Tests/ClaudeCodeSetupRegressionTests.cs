using System.Text;
using System.Text.Json.Nodes;
using Claudgar.Core;
using Claudgar.Core.Setup;

internal static class ClaudeCodeSetupRegressionTests
{
    public static void ClaudeCodeConfigurationPreservesSettings() => InTemporaryFolder(folder =>
    {
        var path = Path.Combine(folder, ".claude.json");
        const string original = "{\"numStartups\":12345678901234,\"theme\":\"dark\",\"projects\":{\"C:/repos/élan\":{\"allowedTools\":[\"Read\"],\"history\":[{\"display\":\"<b>&</b>\"}]}},\"mcpServers\":{\"other\":{\"type\":\"stdio\",\"command\":\"other\"}}}";
        File.WriteAllText(path, original);
        var installer = new ClaudeCodeConfigurationInstaller();
        var first = installer.Register(43827, path);
        Assert(first.Succeeded && first.Changed, "Claude Code registration failed: " + first.Message);
        var installed = File.ReadAllText(path);
        var json = JsonNode.Parse(installed)!;
        Assert(JsonNode.DeepEquals(json["projects"], JsonNode.Parse(original)!["projects"]), "Claude Code project history was changed.");
        Assert(json["numStartups"]!.ToJsonString() == "12345678901234" && json["theme"]!.GetValue<string>() == "dark", "Unrelated Claude Code settings were lost.");
        Assert(json["mcpServers"]!["other"]!["command"]!.GetValue<string>() == "other", "An unrelated Claude Code server was changed.");
        Assert(json["mcpServers"]!["claudgar"]!["type"]!.GetValue<string>() == "http" &&
            json["mcpServers"]!["claudgar"]!["url"]!.GetValue<string>() == "http://127.0.0.1:43827/mcp", "The Claude Code HTTP entry is wrong.");
        Assert(installed.Contains("élan", StringComparison.Ordinal), "Non-ASCII project paths were needlessly escaped.");
        Assert(Directory.GetFiles(folder, ".claude.json.claudgar-backup-*").Any(), "The original Claude Code configuration was not backed up.");
        Assert(installer.Check(43827, path).Succeeded, "Registered Claude Code entry failed its read-only check.");

        var repeat = installer.Register(43827, path);
        Assert(repeat.Succeeded && !repeat.Changed && File.ReadAllText(path) == installed, "Repeated Claude Code registration rewrote configuration.");
        Assert(installer.Register(43828, path) is { Succeeded: true, Changed: true } && installer.Check(43828, path).Succeeded,
            "The owned Claude Code entry could not move to a new port.");

        // Claude Code rewrites its file; an owned entry must survive unrelated rewrites by the client.
        var rewritten = JsonNode.Parse(File.ReadAllText(path))!;
        rewritten["numStartups"] = 99;
        File.WriteAllText(path, rewritten.ToJsonString());
        Assert(installer.Register(43828, path) is { Succeeded: true, Changed: false }, "A client rewrite of unrelated settings broke ownership.");

        var edited = JsonNode.Parse(File.ReadAllText(path))!;
        edited["mcpServers"]!["claudgar"]!["headers"] = new JsonObject { ["X-User"] = "keep" };
        File.WriteAllText(path, edited.ToJsonString());
        var userContent = File.ReadAllText(path);
        Assert(!installer.Register(43829, path).Succeeded && File.ReadAllText(path) == userContent, "A user edit to the Claude Code entry was overwritten.");
        Assert(!installer.Check(43829, path).Succeeded, "A changed Claude Code entry passed its health check.");
    });

    public static void ClaudeCodeMalformedAndConflictingConfigurationIsPreserved() => InTemporaryFolder(folder =>
    {
        var installer = new ClaudeCodeConfigurationInstaller();
        foreach (var original in new[]
        {
            "{unfinished", "[]", "{\"mcpServers\":[]}", "{\"mcpServers\":null}",
            "{\"projects\":{},\"projects\":{}}",
            "{\"mcpServers\":{\"claudgar\":{\"type\":\"http\",\"url\":\"http://example.invalid/mcp\"}}}",
            "{\"mcpServers\":{\"claudgar\":null}}"
        })
        {
            var merged = installer.Merge(original, 43827);
            Assert(!merged.Succeeded && !merged.Changed && merged.Content == original, "Malformed or conflicting Claude Code JSON was changed: " + original);
            var path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, original);
            Assert(!installer.Register(43827, path).Succeeded && File.ReadAllText(path) == original, "Malformed Claude Code file was written: " + original);
        }
        var empty = Path.Combine(folder, "empty.json");
        File.WriteAllText(empty, "  ");
        Assert(!installer.Register(43827, empty).Succeeded && File.ReadAllText(empty) == "  ", "An empty Claude Code config was overwritten.");
    });

    public static void ClaudeCodeLargeConfigurationIsSupported() => InTemporaryFolder(folder =>
    {
        // Real ~/.claude.json files carry project history beyond Desktop's 4 MiB settings limit.
        var path = Path.Combine(folder, ".claude.json");
        var history = new JsonArray();
        for (var index = 0; index < 60_000; index++) history.Add(new JsonObject { ["display"] = "message " + index + new string('x', 64) });
        File.WriteAllText(path, new JsonObject { ["projects"] = new JsonObject { ["C:/big"] = new JsonObject { ["history"] = history } } }.ToJsonString());
        Assert(new FileInfo(path).Length > 4 * 1024 * 1024, "Large Claude Code fixture is too small.");
        var installer = new ClaudeCodeConfigurationInstaller();
        Assert(installer.Register(43827, path).Succeeded && installer.Check(43827, path).Succeeded, "A large Claude Code configuration was rejected.");
    });

    public static void ClaudeCodeSetupSkipsWhenAbsentAndInstallsSkill() => InTemporaryFolder(folder =>
    {
        var paths = ClaudeCodePaths.ForUserProfile(folder);
        var service = new ClaudeCodeSetupService("---\nname: claudgar\n---\nskill", paths);
        var absent = service.Install(43827);
        Assert(!absent.Detected && absent.Succeeded && !absent.Changed && absent.Configuration.Message.Contains("not found", StringComparison.Ordinal),
            "Missing Claude Code must be an informative non-error.");
        Assert(!File.Exists(paths.ConfigFile) && !Directory.Exists(paths.ConfigDirectory), "Setup created Claude Code files although it is not installed.");
        Assert(service.Check(43827) is { Detected: false, Succeeded: true }, "Missing Claude Code health check reported an issue.");

        Directory.CreateDirectory(paths.ConfigDirectory);
        var installed = service.Install(43827);
        Assert(installed.Detected && installed.Succeeded && installed.Changed, "Claude Code setup failed: " + installed.Configuration.Message + " " + installed.Skill.Message);
        Assert(File.ReadAllText(Path.Combine(paths.SkillDirectory, "SKILL.md")).EndsWith("skill", StringComparison.Ordinal), "The Claude Code skill was not installed.");
        Assert(service.Check(43827).Succeeded && service.Install(43827) is { Succeeded: true, Changed: false }, "Repeated Claude Code setup was not idempotent.");

        File.WriteAllText(Path.Combine(paths.SkillDirectory, "SKILL.md"), "user edit");
        var preserved = service.Install(43827);
        Assert(!preserved.Succeeded && preserved.Configuration.Succeeded && File.ReadAllText(Path.Combine(paths.SkillDirectory, "SKILL.md")) == "user edit",
            "A user-edited Claude Code skill was overwritten or blocked the independent connection.");
    });

    public static void SharedGuidanceComesFromTheSkill()
    {
        Assert(McpInstructions.SkillMarkdown.StartsWith("---", StringComparison.Ordinal), "The packaged skill lost its frontmatter.");
        var text = McpInstructions.Text;
        Assert(!text.StartsWith("---", StringComparison.Ordinal) && !text.Contains("name: claudgar", StringComparison.Ordinal),
            "MCP instructions must not include skill frontmatter.");
        Assert(text.Contains("list_characters", StringComparison.Ordinal) && text.Contains("untrusted", StringComparison.Ordinal),
            "MCP instructions lost the essential character guidance.");
        Assert(!text.Contains("restart Codex", StringComparison.OrdinalIgnoreCase) && text.Contains("Claude Code", StringComparison.Ordinal),
            "Shared guidance must be client-neutral.");
        Assert(McpInstructions.StripFrontmatter("---\r\nname: x\r\n---\r\n\r\nBody\r\n") == "Body" && McpInstructions.StripFrontmatter("Plain") == "Plain",
            "Frontmatter stripping is wrong.");
    }

    private static void InTemporaryFolder(Action<string> test)
    {
        var folder = Path.GetFullPath(Path.Combine(".tmp", "claude-code-setup-tests", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(folder);
        try { test(folder); }
        finally { Directory.Delete(folder, recursive: true); }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
