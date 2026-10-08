using System.Reflection;
using System.Text;

namespace Claudgar.App.Resources;

internal static class EmbeddedPayload
{
    public static byte[] LoadBridgeBinary()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Bridge/Claudgar.McpBridge.exe");
        if (stream is null) return [];
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    public static string LoadLicense()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("License/MIT.txt")
            ?? throw new InvalidOperationException("The packaged license is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static (IReadOnlyDictionary<string, byte[]> Addon, string Skill) Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith("Addon/", StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            files.Add(name[6..].Replace('\\', '/'), buffer.ToArray());
        }
        if (files.Count == 0) throw new InvalidOperationException("The packaged addon is missing.");
        return (files, Core.McpInstructions.SkillMarkdown);
    }
}
