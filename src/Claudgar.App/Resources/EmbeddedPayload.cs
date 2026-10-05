using System.Reflection;
using System.Text;

namespace Claudgar.App.Resources;

internal static class EmbeddedPayload
{
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
        using var skill = assembly.GetManifestResourceStream("Skill/SKILL.md")
            ?? throw new InvalidOperationException("The packaged Codex skill is missing.");
        using var reader = new StreamReader(skill, Encoding.UTF8);
        if (files.Count == 0) throw new InvalidOperationException("The packaged addon is missing.");
        return (files, reader.ReadToEnd());
    }
}
