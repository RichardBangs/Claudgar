namespace Claudgar.App.Browser;

internal enum AssistantKind { ChatGpt, Claude }

internal sealed record InstalledAssistant(string Name, string Target, bool IsPackagedApp);

/// <summary>Chooses a known installed assistant and explains the limitations of a browser fallback.</summary>
internal sealed record AssistantLaunchPlan(InstalledAssistant? InstalledTarget, string BrowserUrl, string FallbackMessage)
{
    public static AssistantLaunchPlan For(AssistantKind kind, IReadOnlyList<InstalledAssistant> installed)
    {
        var target = Targets(kind, installed).FirstOrDefault();
        return kind == AssistantKind.ChatGpt
            ? new(target, "https://chatgpt.com/", "ChatGPT opened in your browser. To ask about Claudgar’s local character data, use the ChatGPT desktop app in Work mode.")
            : new(target, "https://claude.ai/", "Claude opened in your browser. Claudgar’s local character connection is available in Claude Desktop and Claude Code.");
    }

    internal static bool Matches(AssistantKind kind, string name) => kind == AssistantKind.ChatGpt
        ? name.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase) || name.Equals("Codex", StringComparison.OrdinalIgnoreCase)
        : name.Equals("Claude", StringComparison.OrdinalIgnoreCase) || name.Equals("Claude Desktop", StringComparison.OrdinalIgnoreCase);

    internal static IEnumerable<InstalledAssistant> Targets(AssistantKind kind, IReadOnlyList<InstalledAssistant> installed) =>
        installed.Where(app => Matches(kind, app.Name)).OrderBy(app =>
            app.Name.Equals(kind == AssistantKind.ChatGpt ? "ChatGPT" : "Claude", StringComparison.OrdinalIgnoreCase) ? 0 : 1);
}

internal sealed record AssistantLaunchResult(bool Opened, bool BrowserFallback, string Message);

internal interface IAssistantLauncher
{
    AssistantLaunchResult Open(AssistantKind kind);
}
