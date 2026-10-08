using System.Text.Json.Nodes;
using System.Runtime.InteropServices;
using Claudgar.App.Browser;

internal static class AssistantHomeTests
{
    public static void Run()
    {
        QuestionsUseOnlySelectedIdentity();
        SuggestionActionsCopyWithoutLaunching();
        SnapshotSummaryRetainsUncertainty();
        LaunchPlansSelectOnlyTheRequestedAssistant();
        Console.WriteLine("PASS Assistant question identity, saved-data uncertainty, and installed/browser launch plans.");
    }

    private static void SuggestionActionsCopyWithoutLaunching()
    {
        var launcher = new RecordingLauncher();
        string? copied = null;
        using var home = new AssistantHome(launcher, text => copied = text) { Size = new Size(900, 600) };
        Check(home.SuggestionButtons.All(button => !button.Enabled),
            "Suggestions must wait for a saved character rather than imply a live or invented selection.");
        home.SetCharacter(Snapshot());
        home.SuggestionButtons[1].PerformClick();
        Check(copied is not null && copied.Contains("Aeloria") && copied.EndsWith("What quests do I need for Deadmines?"),
            "Selecting a suggestion must copy that question with the currently selected character.");
        Check(launcher.Opened.Count == 0 && home.FeedbackLabel.Text.Contains("Copied"),
            "Question copy must confirm the result and leave the choice of assistant to the user.");
        home.ChatGptButton.PerformClick();
        home.ClaudeButton.PerformClick();
        Check(launcher.Opened.SequenceEqual(new[] { AssistantKind.ChatGpt, AssistantKind.Claude }),
            "Each assistant button must request only its corresponding launcher.");

        using var busyClipboard = new AssistantHome(launcher, _ => throw new ExternalException()) { Size = new Size(900, 600) };
        busyClipboard.SetCharacter(Snapshot());
        busyClipboard.SuggestionButtons[0].PerformClick();
        Check(busyClipboard.FeedbackLabel.Text.Contains("clipboard is busy"),
            "A clipboard conflict must leave the home usable with a clear retry message.");
    }

    private static void QuestionsUseOnlySelectedIdentity()
    {
        var snapshot = Snapshot();
        snapshot["character"]!["name"] = "Aéloria";
        snapshot["accountId"] = "PRIVATE-ACCOUNT";
        snapshot["characterId"] = "OPAQUE-CHARACTER-ID";
        snapshot["sourcePath"] = @"C:\Private\Account\SavedVariables\Claudgar.lua";
        var original = snapshot.ToJsonString();
        var question = AssistantHomePresentation.BuildQuestion(snapshot, "Which areas should I quest next?");
        Check(question.Contains("Aéloria", StringComparison.Ordinal) && question.Contains("Classic Beta PvE", StringComparison.Ordinal) &&
            question.EndsWith("Which areas should I quest next?", StringComparison.Ordinal),
            "Question copy must identify the selected saved character and preserve the chosen question.");
        Check(!question.Contains("PRIVATE-ACCOUNT") && !question.Contains("OPAQUE-CHARACTER-ID") && !question.Contains(@"C:\Private"),
            "Question copy must exclude local account, opaque ID, and installation metadata.");
        Check(snapshot.ToJsonString() == original, "Preparing a question must not mutate the saved snapshot.");
        Check(AssistantHomePresentation.BuildQuestion(null, "How can I improve my gear?").Contains("my saved character"),
            "A missing selection needs a useful generic question without invented identity.");

        snapshot["character"]!["name"] = "Aéloria\r\n\"Priest\"";
        var oneLine = AssistantHomePresentation.BuildQuestion(snapshot, "How can I improve my gear?");
        Check(!oneLine.Contains('\r') && !oneLine.Contains('\n') && oneLine.Contains("Aéloria"),
            "Recorded character names must remain one line in question copy.");
        Check(AssistantHomePresentation.Heading(null) == "Ask about your character" &&
            AssistantHomePresentation.Heading(Snapshot()).Contains("Aeloria"),
            "The home heading must follow the selected saved character and handle an empty selection.");
    }

    private static void SnapshotSummaryRetainsUncertainty()
    {
        var missing = AssistantHomePresentation.Status(null);
        Check(!missing.HasCharacter && missing.Text.Contains("Select"), "A missing export must not imply an available character.");
        var snapshot = Snapshot();
        var complete = AssistantHomePresentation.Status(snapshot);
        Check(complete.HasCharacter && !complete.Warning && complete.Text.StartsWith("Saved "),
            "A complete saved snapshot needs a concise save-time summary.");

        snapshot["coverage"]!["inventory"] = "partial";
        var partial = AssistantHomePresentation.Status(snapshot);
        Check(partial.HasCharacter && partial.Warning && partial.Text.Contains("Partial"),
            "The simplified home must retain incomplete-export uncertainty.");
        snapshot["coverage"]!["inventory"] = "unavailable";
        Check(AssistantHomePresentation.Status(snapshot).Warning, "Unavailable sections must not be presented as complete.");
        snapshot["coverage"]!["inventory"] = "complete";
        snapshot["freshness"]!["cached"] = true;
        var cached = AssistantHomePresentation.Status(snapshot);
        Check(cached.HasCharacter && cached.Warning && cached.Text.Contains("Older") && !cached.Text.Contains("ready", StringComparison.OrdinalIgnoreCase),
            "A cached snapshot must remain usable while clearly identified as older saved data.");
        snapshot["freshness"]!["cached"] = false;
        snapshot["exportState"] = "stale";
        Check(AssistantHomePresentation.Status(snapshot).Warning, "An explicitly stale export must remain visible as a warning.");

        snapshot["freshness"]!["observedAt"] = long.MaxValue;
        Check(AssistantHomePresentation.Status(snapshot).Text.Contains("Save time unavailable"),
            "Out-of-range recorded save times must not crash the home or produce a fabricated date.");
    }

    private static void LaunchPlansSelectOnlyTheRequestedAssistant()
    {
        var chatGpt = new InstalledAssistant("ChatGPT", "known-chatgpt-target", true);
        var claude = new InstalledAssistant("Claude Desktop", "known-claude-target", false);
        var impostor = new InstalledAssistant("ChatGPT helper", "unrelated-target", false);
        var installed = new[] { impostor, claude, chatGpt };
        Check(AssistantLaunchPlan.For(AssistantKind.ChatGpt, installed).InstalledTarget == chatGpt,
            "The ChatGPT action must select a discovered ChatGPT app, not a similarly named helper or Claude.");
        Check(AssistantLaunchPlan.For(AssistantKind.Claude, installed).InstalledTarget == claude,
            "The Claude action must select the discovered Claude Desktop app independently of discovery order.");
        foreach (var kind in new[] { AssistantKind.ChatGpt, AssistantKind.Claude })
        {
            var fallback = AssistantLaunchPlan.For(kind, [impostor]);
            var expectedHost = kind == AssistantKind.ChatGpt ? "chatgpt.com" : "claude.ai";
            Check(fallback.InstalledTarget is null && new Uri(fallback.BrowserUrl).Host == expectedHost &&
                fallback.FallbackMessage.Contains("local", StringComparison.OrdinalIgnoreCase) &&
                fallback.FallbackMessage.Contains("desktop", StringComparison.OrdinalIgnoreCase),
                "Browser fallback must use the requested provider and explain the local-connection limitation.");
        }
    }

    private static JsonObject Snapshot() => new()
    {
        ["character"] = new JsonObject { ["name"] = "Aeloria", ["realm"] = "Classic Beta PvE", ["level"] = 22, ["className"] = "Priest" },
        ["exportState"] = "ready",
        ["freshness"] = new JsonObject { ["cached"] = false, ["isStale"] = false, ["observedAt"] = 1791226800L },
        ["coverage"] = new JsonObject
        {
            ["character"] = "complete", ["quests"] = "complete", ["talents"] = "complete", ["inventory"] = "complete", ["equipment"] = "complete"
        }
    };

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private sealed class RecordingLauncher : IAssistantLauncher
    {
        public List<AssistantKind> Opened { get; } = [];
        public AssistantLaunchResult Open(AssistantKind kind)
        {
            Opened.Add(kind);
            return new(true, false, "");
        }
    }
}
