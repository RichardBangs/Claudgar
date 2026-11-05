using Claudgar.Core.Setup;

internal static class GameDiscoveryRegressionTests
{
    private const string ReleaseVersion = "1.60.1.70790";
    private const string Header = "Branch!STRING:0|Version!STRING:0|Product!STRING:0|Active!DEC:1\n";

    public static void ReleaseValidationChecksProductAndVersion() => SetupRegressionTests.InTemporaryFolder(root =>
    {
        var game = CreateRelease(root);
        var discovery = new GameDiscovery();
        foreach (var version in new[] { ReleaseVersion, "1.60.9.76543" })
        {
            Manifest(root, version);
            Assert(discovery.Validate(root).IsValid && discovery.Validate(game).Installation?.Version == version,
                "A Forever release patch/build in the supported client family was rejected.");
        }
        foreach (var version in new[] { "1.15.9.70790", "2.5.6.70577", "5.5.2.69913", "12.1.0.69913", "1.61.0.1", "invalid", "" })
        {
            Manifest(root, version);
            Assert(!discovery.Validate(game).IsValid, "An unsupported or invalid client version was accepted.");
        }
        foreach (var product in new[] { "wow_classic_beta", "wow_classic_era", "wow_anniversary", "wow", "wow_forever" })
        {
            Manifest(root, ReleaseVersion, product);
            Assert(!discovery.Validate(game).IsValid, "An unverified product was accepted as the Forever release.");
        }
        Manifest(root, ReleaseVersion, active: 0);
        Assert(!discovery.Validate(game).IsValid, "An inactive release install row was accepted.");

        // A real shared install includes other products and may include historical/inactive rows.
        File.WriteAllText(Path.Combine(root, ".build.info"), Header +
            "eu|1.60.1.69913|wow_classic_beta|1\n" +
            "eu|1.60.1.70450|wowf|0\n" +
            "eu|2.5.6.70577|wow_anniversary|1\n" +
            $"eu|{ReleaseVersion}|wowf|1\n");
        Assert(discovery.Validate(game).Installation?.Version == ReleaseVersion, "Mixed products hid the active release.");
        foreach (var flavor in new[] { "_classic_beta_", "_retail_", "_anniversary_", "_classic_" })
        {
            var other = Path.Combine(root, flavor);
            Directory.CreateDirectory(other);
            File.WriteAllBytes(Path.Combine(other, "Wow.exe"), []);
            Assert(!discovery.Validate(other).IsValid, "Explicit selection of another client was redirected to Forever.");
        }
    });

    public static void DiscoveryFindsRootOrReleaseDirectory() => SetupRegressionTests.InTemporaryFolder(root =>
    {
        var game = CreateRelease(root);
        var discovery = new GameDiscovery();
        var found = discovery.Discover([root, game, root + Path.DirectorySeparatorChar]);
        Assert(found.Count(installation => installation.GameDirectory == game) == 1,
            "Root and release candidates did not resolve to one installation.");
        Assert(discovery.SuggestDirectory([root]) == game && discovery.SuggestDirectory([game]) == game,
            "Manual selection did not start in the existing release directory.");
        Assert(!Directory.Exists(Path.Combine(game, "Interface")) && !Directory.Exists(Path.Combine(game, "WTF")),
            "Read-only discovery created addon or character data folders.");
        File.Delete(Path.Combine(game, "Wow.exe"));
        Assert(!discovery.Discover([root]).Any(installation => installation.GameDirectory == game),
            "An incomplete install was accepted during discovery.");
        Assert(discovery.SuggestDirectory([root]) == game,
            "An incomplete install should still be offered as a manual-selection starting point.");
    });

    public static void LegacyBetaPathsFindSiblingRelease() => SetupRegressionTests.InTemporaryFolder(root =>
    {
        var game = CreateRelease(root);
        var beta = Path.Combine(root, "_classic_beta_");
        var discovery = new GameDiscovery();
        // A removed beta directory must still work as a hint for an install outside standard locations.
        Assert(discovery.Discover([beta]).Any(installation => installation.GameDirectory == game),
            "A removed beta setting did not lead to its sibling release.");
        Assert(discovery.SuggestDirectory([beta]) == game, "A removed beta setting did not suggest the release.");
        Directory.CreateDirectory(beta);
        File.WriteAllBytes(Path.Combine(beta, "WowB.exe"), []);
        File.WriteAllText(Path.Combine(beta, "existing-beta-data.txt"), "keep beta data");
        var found = discovery.Discover([beta + Path.DirectorySeparatorChar]);
        Assert(found.Any(installation => installation.GameDirectory == game) &&
               found.All(installation => installation.GameDirectory != beta),
            "A remaining beta install prevented release discovery or was accepted itself.");
        Assert(discovery.SuggestDirectory([beta]) == game && !discovery.Validate(beta).IsValid,
            "Beta selection was accepted or suggested ahead of its release sibling.");
        Assert(File.ReadAllText(Path.Combine(beta, "existing-beta-data.txt")) == "keep beta data",
            "Discovery changed existing beta data.");
        Manifest(root, ReleaseVersion, "wow_classic_beta");
        Assert(!discovery.Discover([beta]).Any(installation => installation.RootDirectory == root),
            "A beta manifest authorized its sibling release folder.");
    });

    public static void MalformedAndIncompleteInstallsAreRejected() => SetupRegressionTests.InTemporaryFolder(root =>
    {
        var game = CreateRelease(root);
        var discovery = new GameDiscovery();
        foreach (var manifest in new[]
        {
            "", Header, Header + "eu|1.60.1.70790\n",
            "Version!STRING:0|Product!STRING:0\n1.60.1.70790|wowf\n",
            "Version!STRING:0|Product!STRING:0|Product!STRING:0|Active!DEC:1\n1.60.1.70790|wowf|wowf|1\n"
        })
        {
            File.WriteAllText(Path.Combine(root, ".build.info"), manifest);
            Assert(!discovery.Validate(game).IsValid, "An incomplete or malformed manifest was accepted.");
        }
        File.Delete(Path.Combine(root, ".build.info"));
        Assert(!discovery.Validate(game).IsValid, "A missing manifest was accepted.");
        Manifest(root, ReleaseVersion);
        File.Delete(Path.Combine(game, "Wow.exe"));
        File.WriteAllBytes(Path.Combine(game, "WowB.exe"), []);
        Assert(!discovery.Validate(game).IsValid, "The beta executable substituted for the release executable.");
    });

    private static string CreateRelease(string root)
    {
        var game = Path.Combine(root, GameDiscovery.ClientDirectoryName);
        Directory.CreateDirectory(game);
        File.WriteAllBytes(Path.Combine(game, "Wow.exe"), []); // Presence-only fixture, never executed.
        Manifest(root, ReleaseVersion);
        return game;
    }

    private static void Manifest(string root, string version, string product = "wowf", int active = 1) =>
        File.WriteAllText(Path.Combine(root, ".build.info"), Header +
            "eu|12.1.0.69875|wow|1\n" + $"eu|{version}|{product}|{active}\n");

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
