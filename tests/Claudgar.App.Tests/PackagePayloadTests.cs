using System.Diagnostics;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Claudgar.Core;
using Claudgar.Core.Updates;

internal static class PackagePayloadTests
{
    public static void Run(string packagePath)
    {
        Check(Path.IsPathFullyQualified(packagePath), "The package validation path must be absolute.");
        packagePath = Path.GetFullPath(packagePath);
        var repository = FindRepository();
        Check(ReleaseVersion.TryParse(BuildInfo.Version, out var releaseVersion), "The release version is invalid.");
        new WindowsUpdateExecutableValidator().Validate(packagePath, releaseVersion);
        var appBytes = SingleFilePackageReader.ReadAssembly(packagePath, "Claudgar.dll");
        var context = new AssemblyLoadContext("Read-only package inspection " + Guid.NewGuid(), isCollectible: true);
        try
        {
            // Loading metadata/resources does not run Program or create setup/coordinator objects.
            using var assemblyStream = new MemoryStream(appBytes, writable: false);
            var assembly = context.LoadFromStream(assemblyStream);
            Check(assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration == "Release",
                "The published app must be a Release build.");
            Check(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion == BuildInfo.Version,
                "The bundled app assembly version differs from Version.props.");
            using var coreStream = new MemoryStream(SingleFilePackageReader.ReadAssembly(packagePath, "Claudgar.Core.dll"), writable: false);
            var core = context.LoadFromStream(coreStream);
            Check(core.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion == BuildInfo.Version,
                "The bundled connection/data library differs from the app release version.");
            CheckSelfContained(packagePath, "Claudgar.runtimeconfig.json", ["Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App", "Microsoft.AspNetCore.App"]);
            var fileInfo = FileVersionInfo.GetVersionInfo(packagePath);
            Check(fileInfo.FileVersion == BuildInfo.Version + ".0" && fileInfo.ProductVersion == BuildInfo.Version,
                "The executable's Windows version differs from the release version.");

            var bridge = Resource(assembly, "Bridge/Claudgar.McpBridge.exe");
            var builtBridge = Path.Combine(repository, ".tmp", "claude-bridge", "Claudgar.McpBridge.exe");
            using (var bridgeStream = new MemoryStream(bridge, writable: false))
            using (var bridgeHeaders = new PEReader(bridgeStream))
                Check(bridgeHeaders.PEHeaders.CoffHeader.Machine == Machine.Amd64 &&
                    bridgeHeaders.PEHeaders.PEHeader?.Magic == PEMagic.PE32Plus,
                    "The native Windows x64 helper is missing from the package.");
            Check(Hash(bridge) == HashFile(builtBridge), "The embedded Claude helper differs from the current native build.");
            Check(FileVersionInfo.GetVersionInfo(builtBridge).ProductVersion == BuildInfo.Version,
                "The native Claude helper version differs from the release version.");
            CheckSelfContained(builtBridge, "Claudgar.McpBridge.runtimeconfig.json", ["Microsoft.NETCore.App"]);

            var license = Resource(assembly, "License/MIT.txt");
            Check(Hash(license) == HashFile(Path.Combine(repository, "LICENSE")) &&
                Hash(license) == HashFile(Path.Combine(Path.GetDirectoryName(packagePath)!, "LICENSE")),
                "The packaged and distributed MIT licenses differ from the repository license.");

            var portrait = Resource(assembly, "Addon/Textures/ClaudgarPortrait.tga");
            Check(Hash(portrait) == HashFile(Path.Combine(repository, "addon", "Claudgar", "Textures", "ClaudgarPortrait.tga")),
                "The addon portrait differs from the verified game texture.");
            Check(portrait.Length == 18 + 64 * 64 * 4 && portrait[2] == 2 && portrait[16] == 32 && (portrait[17] & 15) == 8,
                "The bundled portrait must be an uncompressed 64x64 RGBA game texture.");
            var minimap = Encoding.UTF8.GetString(Resource(assembly, "Addon/MinimapButton.lua"));
            Check(minimap.Contains("Interface\\\\AddOns\\\\Claudgar\\\\Textures\\\\ClaudgarPortrait", StringComparison.Ordinal),
                "The bundled minimap code does not reference the bundled portrait.");
            var tocNames = assembly.GetManifestResourceNames().Where(name => name.StartsWith("Addon/", StringComparison.Ordinal) && name.EndsWith(".toc", StringComparison.OrdinalIgnoreCase)).ToArray();
            Check(tocNames.Length > 0, "The bundled addon TOC is missing.");
            foreach (var name in tocNames)
            {
                var toc = Encoding.UTF8.GetString(Resource(assembly, name));
                Check(Regex.IsMatch(toc, "(?m)^## Version: " + Regex.Escape(BuildInfo.Version) + "\\r?$"),
                    "The bundled addon version was not stamped to match the app: " + name);
                Check(toc.Contains("## SavedVariables: ClaudgarDB", StringComparison.Ordinal), "Addon packaging changed the saved-data variable.");
            }
            Check(Hash(Resource(core, "Skill/SKILL.md")) == HashFile(Path.Combine(repository, "skill", "claudgar", "SKILL.md")),
                "The bundled character guidance skill differs from the repository skill.");
            var checksum = File.ReadAllText(packagePath + ".sha256").Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            Check(checksum == HashFile(packagePath), "The distributed app checksum does not match the executable.");
            Console.WriteLine("PASS Published package versions, native helper, license, portrait, addon TOC, skill, and checksum.");
        }
        finally { context.Unload(); }
    }

    private static byte[] Resource(Assembly assembly, string name)
    {
        var actualName = assembly.GetManifestResourceNames().SingleOrDefault(resource => resource.Replace('\\', '/') == name.Replace('\\', '/'))
            ?? throw new InvalidOperationException("The package resource is missing: " + name);
        using var stream = assembly.GetManifestResourceStream(actualName)
            ?? throw new InvalidOperationException("The package resource is missing: " + name);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static void CheckSelfContained(string packagePath, string configurationName, string[] requiredFrameworks)
    {
        using var configuration = JsonDocument.Parse(SingleFilePackageReader.ReadFile(packagePath, configurationName));
        var runtime = configuration.RootElement.GetProperty("runtimeOptions");
        Check(!runtime.TryGetProperty("framework", out _) && !runtime.TryGetProperty("frameworks", out _),
            "The package unexpectedly requires a separately installed .NET runtime: " + packagePath);
        var included = runtime.GetProperty("includedFrameworks").EnumerateArray().Select(item => item.GetProperty("name").GetString()).ToArray();
        Check(requiredFrameworks.All(included.Contains), "A required runtime was not included in the native package: " + packagePath);
    }

    private static string FindRepository()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Version.props")) && File.Exists(Path.Combine(directory.FullName, "build.ps1")))
                return directory.FullName;
        throw new InvalidOperationException("Run package validation from this repository's App.Tests build.");
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
