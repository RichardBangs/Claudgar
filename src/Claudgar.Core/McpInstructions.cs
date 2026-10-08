using System.Reflection;
using System.Text;

namespace Claudgar.Core;

/// <summary>
/// Character-data guidance shared by every chat client. The packaged skill file is the single source:
/// skill-aware clients install it as-is, and MCP clients receive its body as server instructions.
/// </summary>
public static class McpInstructions
{
    private const string SkillResourceName = "Skill/SKILL.md";
    private static readonly Lazy<string> Skill = new(LoadSkill);
    private static readonly Lazy<string> Body = new(() => StripFrontmatter(Skill.Value));

    /// <summary>The complete packaged SKILL.md, including its frontmatter.</summary>
    public static string SkillMarkdown => Skill.Value;

    /// <summary>The skill guidance without frontmatter, sent as MCP server instructions.</summary>
    public static string Text => Body.Value;

    internal static string StripFrontmatter(string markdown)
    {
        var text = markdown.Replace("\r\n", "\n").TrimStart('﻿');
        if (!text.StartsWith("---\n", StringComparison.Ordinal)) return text.Trim();
        var end = text.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (end < 0) throw new InvalidOperationException("The packaged skill frontmatter is not closed.");
        return text[(end + 5)..].Trim();
    }

    private static string LoadSkill()
    {
        using var stream = typeof(McpInstructions).Assembly.GetManifestResourceStream(SkillResourceName)
            ?? throw new InvalidOperationException("The packaged character guidance skill is missing.");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
        return reader.ReadToEnd();
    }
}
