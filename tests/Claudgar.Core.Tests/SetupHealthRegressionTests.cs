using System.Text;
using Claudgar.Core.Setup;

internal static class SetupHealthRegressionTests
{
    public static void SetupHealthChecksAreReadOnlyAndDetectIssues() => SetupRegressionTests.InTemporaryFolder(folder =>
    {
        var game = Path.Combine(folder, GameDiscovery.ClientDirectoryName);
        Directory.CreateDirectory(game);
        File.WriteAllBytes(Path.Combine(game, "WowB.exe"), []); // Presence-only fixture; never executed.
        File.WriteAllText(Path.Combine(folder, ".build.info"),
            "Version!STRING:0|Product!STRING:0|Active!DEC:1\n1.60.1.69913|wow_classic_beta|1\n");
        var addon = Path.Combine(game, "Interface", "AddOns", "Claudgar");
        var content = Encoding.UTF8.GetBytes("## Title: Claudgar\n");
        var checker = new SetupHealthChecker(new Dictionary<string, byte[]> { ["Claudgar.toc"] = content });
        var ready = new InstallationResult(true, false, "Ready", folder);
        var report = new SetupReport([ready], ready, ready);

        var unconfigured = checker.Check([], report);
        Assert(unconfigured.NeedsGameFolder && !unconfigured.NeedsRepair, "An unconfigured app should request a game folder.");
        var missing = checker.Check([game], report);
        Assert(!missing.NeedsGameFolder && missing.NeedsRepair, "A missing addon should request repair.");
        Assert(!Directory.Exists(addon), "The health check installed a missing addon.");

        Directory.CreateDirectory(addon);
        var toc = Path.Combine(addon, "Claudgar.toc");
        File.WriteAllBytes(toc, content);
        var healthy = checker.Check([game], report);
        Assert(!healthy.NeedsGameFolder && !healthy.NeedsRepair && healthy.Message is null,
            "Healthy setup should not show recovery actions.");
        Assert(Directory.GetFiles(addon).Length == 1, "The health check wrote setup metadata.");

        const string customization = "user customization";
        File.WriteAllText(toc, customization);
        var modified = checker.Check([game], report);
        Assert(modified.NeedsRepair && File.ReadAllText(toc) == customization,
            "A changed addon must be reported and left untouched.");
        File.WriteAllBytes(toc, content);
        var failedReport = report with { Codex = new(false, false, "Configuration needs attention", folder) };
        var failed = checker.Check([game], failedReport);
        Assert(failed.NeedsRepair && failed.Message == "Configuration needs attention", "A failed setup report was hidden.");
        var offline = checker.Check([game], report, "The local connection is unavailable.");
        Assert(offline.NeedsRepair && offline.Message == "The local connection is unavailable.", "An offline local service was hidden.");

        File.Delete(Path.Combine(game, "WowB.exe"));
        var invalid = checker.Check([game], report);
        Assert(invalid.NeedsGameFolder && !invalid.NeedsRepair, "An invalid game folder should request a new folder.");
        var deleted = checker.Check([Path.Combine(folder, "deleted-install")], report);
        Assert(deleted.NeedsGameFolder && !deleted.NeedsRepair, "A deleted game folder should request a new folder.");
        Assert(File.ReadAllBytes(toc).AsSpan().SequenceEqual(content), "Checking invalid game folders changed addon files.");
    });

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
