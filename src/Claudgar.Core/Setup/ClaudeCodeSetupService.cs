using System.Text;

namespace Claudgar.Core.Setup;

public sealed record ClaudeCodeSetupResult(InstallationResult Configuration, InstallationResult Skill, bool Detected)
{
    public bool Succeeded => Configuration.Succeeded && Skill.Succeeded;
    public bool Changed => Configuration.Changed || Skill.Changed;
}

/// <summary>Where Claude Code keeps its user configuration and personal skills.</summary>
public sealed record ClaudeCodePaths(string ConfigFile, string ConfigDirectory)
{
    public static ClaudeCodePaths ForUserProfile(string userProfile) =>
        new(Path.Combine(userProfile, ".claude.json"), Path.Combine(userProfile, ".claude"));

    public static ClaudeCodePaths Default => ForUserProfile(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    public string SkillDirectory => Path.Combine(ConfigDirectory, "skills", "claudgar");

    /// <summary>Claude Code has run on this account when either its config file or folder exists.</summary>
    public bool IsInstalled => File.Exists(ConfigFile) || Directory.Exists(ConfigDirectory);
}

/// <summary>
/// Connects Claude Code (CLI, IDE, and Remote Control sessions) through the loopback HTTP service and installs
/// the shared Claudgar skill. Skips cleanly when Claude Code has never been used on this account.
/// </summary>
public sealed class ClaudeCodeSetupService(string skillMarkdown, ClaudeCodePaths? paths = null)
{
    private const string AbsentMessage = "Claude Code was not found. Install Claude Code and run setup repair to connect it.";
    private readonly ClaudeCodeConfigurationInstaller configuration = new();
    private readonly OwnedFilesInstaller files = new();
    private readonly ClaudeCodePaths paths = paths ?? ClaudeCodePaths.Default;

    public ClaudeCodeSetupResult Install(int port)
    {
        if (!paths.IsInstalled) return Absent();
        var skill = files.Install(paths.SkillDirectory, new Dictionary<string, byte[]>
        {
            ["SKILL.md"] = new UTF8Encoding(false).GetBytes(skillMarkdown)
        });
        return new(configuration.Register(port, paths.ConfigFile), skill, Detected: true);
    }

    public ClaudeCodeSetupResult Check(int port)
    {
        if (!paths.IsInstalled) return Absent();
        var skillPath = Path.Combine(paths.SkillDirectory, "SKILL.md");
        var skill = File.Exists(skillPath)
            ? new InstallationResult(true, false, "Claude Code skill is installed.", paths.SkillDirectory)
            : new InstallationResult(false, false, "Claude Code skill is missing. Run setup repair to restore it.", paths.SkillDirectory);
        return new(configuration.Check(port, paths.ConfigFile), skill, Detected: true);
    }

    private ClaudeCodeSetupResult Absent() =>
        new(new(true, false, AbsentMessage, paths.ConfigFile), new(true, false, AbsentMessage, paths.SkillDirectory), Detected: false);
}
