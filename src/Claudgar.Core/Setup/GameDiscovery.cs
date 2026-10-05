using Microsoft.Win32;
using System.Runtime.Versioning;
using System.Text.Json;

namespace Claudgar.Core.Setup;

/// <summary>Discovers only the Forever beta, which currently occupies the shared Classic beta product slot.</summary>
public sealed class GameDiscovery
{
    public const string ClientDirectoryName = "_classic_beta_";
    public const string ProductCode = "wow_classic_beta";

    public IReadOnlyList<GameInstallation> Discover(IEnumerable<string>? savedDirectories = null)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var saved in savedDirectories ?? []) if (!string.IsNullOrWhiteSpace(saved)) candidates.Add(saved);
        foreach (var root in CommonRoots()) candidates.Add(root);
        foreach (var root in BattleNetRoots()) candidates.Add(root);
        if (OperatingSystem.IsWindows())
            foreach (var root in RegistryRoots()) candidates.Add(root);

        var found = new Dictionary<string, GameInstallation>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates.Take(128))
        {
            var validation = Validate(candidate);
            if (validation.Installation is { } installation) found[installation.GameDirectory] = installation;
        }
        return found.Values.OrderBy(installation => installation.GameDirectory, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public GameValidationResult Validate(string selectedDirectory)
    {
        try
        {
            var selection = Path.GetFullPath(selectedDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var game = string.Equals(Path.GetFileName(selection), ClientDirectoryName, StringComparison.OrdinalIgnoreCase)
                ? selection : Path.Combine(selection, ClientDirectoryName);
            var root = Path.GetDirectoryName(game)!;
            if (!Directory.Exists(game)) return new(null, "Select the World of Warcraft folder or its _classic_beta_ folder.");
            if (!File.Exists(Path.Combine(game, "WowB.exe")))
                return new(null, "The selected beta folder does not contain WowB.exe. Complete its Battle.net installation first.");
            var manifestPath = Path.Combine(root, ".build.info");
            if (!File.Exists(manifestPath)) return new(null, "The install's .build.info file is missing; this folder cannot be identified as Forever beta.");
            var text = System.Text.Encoding.UTF8.GetString(SafeFiles.ReadLimited(manifestPath, 1024 * 1024));
            var rows = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (rows.Length < 2) return new(null, "The install's .build.info file is incomplete.");
            var headings = rows[0].Trim('\uFEFF', '\r').Split('|')
                .Select((column, index) => (Name: column.Split('!')[0].Trim(), Index: index))
                .ToDictionary(column => column.Name, column => column.Index, StringComparer.OrdinalIgnoreCase);
            if (!headings.TryGetValue("Product", out var productIndex) || !headings.TryGetValue("Version", out var versionIndex) ||
                !headings.TryGetValue("Active", out var activeIndex))
                return new(null, "The install's .build.info file lacks product, version, or active-install information.");
            foreach (var row in rows.Skip(1))
            {
                var fields = row.TrimEnd('\r').Split('|');
                if (fields.Length <= Math.Max(activeIndex, Math.Max(productIndex, versionIndex))) continue;
                if (!string.Equals(fields[productIndex].Trim(), ProductCode, StringComparison.OrdinalIgnoreCase) || fields[activeIndex].Trim() != "1") continue;
                var version = fields[versionIndex].Trim();
                // The product slot previously hosted other Classic betas. Product and folder alone are insufficient.
                // 1.60 identifies Forever; no exact patch or build is pinned. See docs/API-SOURCES.md.
                if (!Version.TryParse(version, out var parsed) || parsed.Major != 1 || parsed.Minor != 60)
                    return new(null, "The beta product is installed, but its version is not the Forever 1.60 client family.");
                return new(new(game, root, version), null);
            }
            return new(null, "The selected install has no active World of Warcraft: Forever beta product. Retail and other Classic clients are unsupported.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new(null, "This folder could not be validated: " + error.Message);
        }
    }

    private static IEnumerable<string> CommonRoots()
    {
        foreach (var programFiles in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }.Where(path => !string.IsNullOrEmpty(path)))
        {
            yield return Path.Combine(programFiles, "World of Warcraft");
            yield return Path.Combine(programFiles, "Battle.net", "World of Warcraft");
        }
        foreach (var drive in DriveInfo.GetDrives().Where(drive => drive.DriveType == DriveType.Fixed).Take(16))
        {
            yield return Path.Combine(drive.Name, "World of Warcraft");
            yield return Path.Combine(drive.Name, "Games", "World of Warcraft");
            yield return Path.Combine(drive.Name, "Battle.net", "World of Warcraft");
            yield return Path.Combine(drive.Name, "Program Files (x86)", "World of Warcraft");
        }
    }

    private static IEnumerable<string> BattleNetRoots()
    {
        // Read only the install preference, never login or account fields from Battle.net configuration.
        var paths = new List<string>();
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Battle.net", "Battle.net.config");
        if (!File.Exists(path)) return paths;
        try
        {
            using var document = JsonDocument.Parse(SafeFiles.ReadLimited(path, 2 * 1024 * 1024), new JsonDocumentOptions { MaxDepth = 32 });
            if (document.RootElement.TryGetProperty("Client", out var client) && client.TryGetProperty("Install", out var install) &&
                install.TryGetProperty("DefaultInstallPath", out var preference) && preference.ValueKind == JsonValueKind.String &&
                preference.GetString() is { Length: > 0 } root)
                paths.Add(Path.Combine(root, "World of Warcraft"));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException) { }
        return paths;
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> RegistryRoots()
    {
        var paths = new List<string>();
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                foreach (var subKey in new[] { @"SOFTWARE\Blizzard Entertainment\World of Warcraft",
                             @"SOFTWARE\Blizzard Entertainment\World of Warcraft Beta",
                             @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\World of Warcraft" })
                {
                    using var key = baseKey.OpenSubKey(subKey);
                    foreach (var name in new[] { "InstallPath", "InstallLocation" })
                        if (key?.GetValue(name) is string path && !string.IsNullOrWhiteSpace(path))
                        {
                            paths.Add(path);
                            // Other installed flavors share the same manifest root. Never validate those
                            // flavors as Forever; use their parent only as a discovery candidate.
                            var directoryName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                            if (directoryName.StartsWith('_') && directoryName.EndsWith('_') && Path.GetDirectoryName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) is { } root)
                                paths.Add(root);
                        }
                }
            }
            catch (Exception error) when (error is UnauthorizedAccessException or System.Security.SecurityException or IOException) { }
        }
        return paths;
    }
}
