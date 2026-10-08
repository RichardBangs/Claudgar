using Claudgar.Core.Setup;

namespace Claudgar.App.Browser;

/// <summary>
/// Describes each chat client's setup on its own. A client that is not installed is reported as such and
/// never turns another client's status into an error.
/// </summary>
internal static class ChatClientStatusPresentation
{
    public static IReadOnlyList<string> Lines(SetupReport? chatGpt, ClaudeSetupResult? claudeDesktop, ClaudeCodeSetupResult? claudeCode)
    {
        var lines = new List<string>();
        if (chatGpt is not null)
        {
            lines.Add($"ChatGPT: {State(chatGpt.Codex.Succeeded && chatGpt.Skill.Succeeded)}");
            lines.Add("  Connection: " + chatGpt.Codex.Message);
            lines.Add("  Skill: " + chatGpt.Skill.Message);
            lines.Add("");
        }
        if (claudeDesktop is not null)
        {
            if (!claudeDesktop.Detected) lines.Add("Claude Desktop: not installed. " + claudeDesktop.Configuration.Message);
            else
            {
                lines.Add($"Claude Desktop: {State(claudeDesktop.Succeeded)}");
                lines.Add("  Helper: " + claudeDesktop.Helper.Message);
                foreach (var config in claudeDesktop.Configurations)
                    lines.Add($"  {config.Location.Label}: {config.Result.Message}");
            }
            lines.Add("");
        }
        if (claudeCode is not null)
        {
            if (!claudeCode.Detected) lines.Add("Claude Code: not installed. " + claudeCode.Configuration.Message);
            else
            {
                lines.Add($"Claude Code: {State(claudeCode.Succeeded)}");
                lines.Add("  Connection: " + claudeCode.Configuration.Message);
                lines.Add("  Skill: " + claudeCode.Skill.Message);
            }
            lines.Add("");
        }
        return lines;
    }

    private static string State(bool succeeded) => succeeded ? "ready" : "needs repair";
}
