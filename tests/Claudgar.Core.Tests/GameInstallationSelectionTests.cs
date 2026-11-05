using Claudgar.Core.Exports;
using Claudgar.Core.Setup;

internal static class GameInstallationSelectionTests
{
    public static void Run()
    {
        var root = Path.GetFullPath(Path.Combine(".tmp", "selection-fixture"));
        var release = Release(root);
        var other = Release(root + "-other");
        var beta = Path.Combine(root, "_classic_beta_");

        var migrated = GameInstallationSelection.Resolve([other, release], [beta]);
        Check(migrated.Installations.SequenceEqual([release]) &&
              migrated.GameDirectories.SequenceEqual([release.GameDirectory]),
            "A saved beta choice must migrate only to its verified sibling release.");
        var missing = GameInstallationSelection.Resolve([other], [beta]);
        Check(missing.Installations.Count == 0 && missing.GameDirectories.SequenceEqual([beta]),
            "An unavailable selected root must be preserved without choosing another install.");
        var duplicate = GameInstallationSelection.Resolve([release],
            [beta.ToUpperInvariant() + Path.DirectorySeparatorChar, release.GameDirectory, root]);
        Check(duplicate.Installations.Count == 1 && duplicate.GameDirectories.SequenceEqual([release.GameDirectory]),
            "Equivalent beta, root, and release selections should resolve to one release folder.");
        var explicitRelease = GameInstallationSelection.Resolve([release, other], [other.GameDirectory]);
        Check(explicitRelease.Installations.SequenceEqual([other]), "An explicit release choice was lost.");
        var mixed = GameInstallationSelection.Resolve([release], [beta, other.GameDirectory]);
        Check(mixed.Installations.SequenceEqual([release]) && mixed.GameDirectories.SequenceEqual([release.GameDirectory, other.GameDirectory]),
            "Migration must preserve unavailable configured installations for setup health checks.");
        foreach (var unsupported in new[] { "_retail_", "_classic_", "_anniversary_" })
            Check(GameInstallationSelection.Resolve([release], [Path.Combine(root, unsupported)]).Installations.Count == 0,
                "An unrelated flavor must not silently select the release.");
        var ambiguous = GameInstallationSelection.Resolve([release, other], []);
        Check(ambiguous.RequiresChoice && ambiguous.Installations.Count == 0 && ambiguous.GameDirectories.Count == 0,
            "Multiple new installations require the user's selection.");
        Check(GameInstallationSelection.Resolve([release], []).Installations.SequenceEqual([release]),
            "A single new release should be selected automatically.");
        Check(!GameInstallationSelection.Resolve([], []).RequiresChoice, "No install is not an ambiguous choice.");

        BetaExportsDoNotSurviveMigration();
    }

    private static void BetaExportsDoNotSurviveMigration() => SetupRegressionTests.InTemporaryFolder(folder =>
    {
        var beta = Path.Combine(folder, "_classic_beta_");
        var release = Release(folder);
        var fixture = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "Claudgar.lua"));
        WriteExport(beta, fixture);
        IReadOnlyList<string> directories = [beta];
        var repository = new ExportRepository(() => directories);
        var betaId = repository.ListCharacters()["characters"]![0]!["characterId"]!.GetValue<string>();

        var migrated = GameInstallationSelection.Resolve([release], directories);
        directories = migrated.Installations.Select(game => game.GameDirectory).ToArray();
        var empty = repository.ListCharacters();
        Check(empty["characters"]!.AsArray().Count == 0 && empty["exportState"]!.GetValue<string>() == "no_export",
            "Release migration must wait for its first export instead of showing cached beta characters.");
        Check(repository.GetCharacter(betaId)["error"]?["code"]?.GetValue<string>() == "character_not_found",
            "Beta character IDs must stop resolving after migration.");

        WriteExport(release.GameDirectory, fixture.Replace("69913", "70790", StringComparison.Ordinal));
        var current = repository.ListCharacters();
        Check(current["characters"]!.AsArray().Count == 1 && current["exportState"]!.GetValue<string>() == "ready" &&
              current["characters"]![0]!["characterId"]!.GetValue<string>() != betaId,
            "The first release export must have an identity separate from the old beta export.");
    });

    private static GameInstallation Release(string root) =>
        new(Path.Combine(root, GameDiscovery.ClientDirectoryName), root, "1.60.1.70790");

    private static void WriteExport(string directory, string fixture)
    {
        var savedVariables = Path.Combine(directory, "WTF", "Account", "SYNTHETIC", "SavedVariables");
        Directory.CreateDirectory(savedVariables);
        File.WriteAllText(Path.Combine(savedVariables, "Claudgar.lua"), fixture);
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
