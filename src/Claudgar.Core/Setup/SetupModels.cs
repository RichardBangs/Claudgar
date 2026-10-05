namespace Claudgar.Core.Setup;

public sealed record GameInstallation(string GameDirectory, string RootDirectory, string Version)
{
    public string AddonsDirectory => Path.Combine(GameDirectory, "Interface", "AddOns");
    public string AccountDirectory => Path.Combine(GameDirectory, "WTF", "Account");
}

public sealed record GameValidationResult(GameInstallation? Installation, string? Error)
{
    public bool IsValid => Installation is not null;
}

public sealed record InstallationResult(bool Succeeded, bool Changed, string Message, string Path);

public sealed record SetupReport(IReadOnlyList<InstallationResult> Addons, InstallationResult Codex, InstallationResult Skill)
{
    public bool IsSuccessful => Addons.Count > 0 && Addons.All(result => result.Succeeded) && Codex.Succeeded && Skill.Succeeded;
}
