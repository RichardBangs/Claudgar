using System.Text.Json.Nodes;
using Claudgar.App.Browser;

internal static class ReleasePresentationTests
{
    public static void Run()
    {
        var failed = new JsonObject { ["exportState"] = "failed", ["characters"] = new JsonArray(),
            ["issues"] = new JsonArray(new JsonObject { ["message"] = "The saved export could not be validated." }) };
        var message = ExportStatusPresentation.Describe(failed);
        Check(message.Contains("could not be validated") && message.Contains("Reload") && !message.Contains("No export yet"),
            "An invalid first export needs the actual failure and a recovery action.");
        var empty = new JsonObject { ["exportState"] = "no_export", ["characters"] = new JsonArray(), ["issues"] = new JsonArray() };
        Check(ExportStatusPresentation.Describe(empty).Contains("save your first export"), "Initial setup needs instructions for saving an export.");
        var recovered = new JsonObject { ["exportState"] = "ready", ["characters"] = new JsonArray(new JsonObject()), ["issues"] = new JsonArray() };
        Check(!ExportStatusPresentation.Describe(recovered).Contains("Repair"), "A recovered export must clear the failure advice.");
        Console.WriteLine("PASS Release export errors, first export, and recovery guidance.");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
