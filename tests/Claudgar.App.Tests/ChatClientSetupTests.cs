using System.Net;
using System.Net.Sockets;
using System.Text;
using Claudgar.App.Browser;
using Claudgar.App.Mcp;
using Claudgar.Core.Exports;
using Claudgar.Core.Setup;

internal static class ChatClientSetupTests
{
    public static void Run()
    {
        ClientStatusesAreIndependent();
        // Earlier WinForms tests install a UI synchronization context on this thread; keep awaits off it.
        Task.Run(LocalServiceAcceptsChatClientRequests).GetAwaiter().GetResult();
        Console.WriteLine("PASS Chat client statuses are independent and the local service accepts Codex and Claude Code HTTP requests.");
    }

    private static void ClientStatusesAreIndependent()
    {
        var ok = new InstallationResult(true, false, "configured", "path");
        var chatGpt = new SetupReport([ok], ok, ok);
        var absentDesktop = new ClaudeSetupResult(ok with { Message = "Claude Desktop was not found." }, [], Detected: false, "Claude Desktop was not found.");
        var absentCode = new ClaudeCodeSetupResult(ok with { Message = "Claude Code was not found." }, ok, Detected: false);
        var lines = string.Join("\n", ChatClientStatusPresentation.Lines(chatGpt, absentDesktop, absentCode));
        Check(lines.Contains("ChatGPT: configured") && !lines.Contains(": ready") && lines.Contains("Claude Desktop: not installed") && lines.Contains("Claude Code: not installed") &&
            !lines.Contains("needs repair"), "A missing Claude client must not make any client look broken.");

        var failed = new InstallationResult(false, false, "conflict", "path");
        var store = new ClaudeDesktopConfigLocation("store.json", true);
        var classic = new ClaudeDesktopConfigLocation("classic.json", false);
        var desktop = new ClaudeSetupResult(ok, [new(classic, failed), new(store, ok)], Detected: true);
        var code = new ClaudeCodeSetupResult(ok, ok, Detected: true);
        lines = string.Join("\n", ChatClientStatusPresentation.Lines(chatGpt, desktop, code));
        Check(lines.Contains("Claude Desktop: needs repair") && lines.Contains("Claude Desktop (Microsoft Store): configured") &&
            lines.Contains("Claude Code: configured") && lines.Contains("ChatGPT: configured"),
            "Each client and each Desktop installation must show its own status.");
    }

    private static async Task LocalServiceAcceptsChatClientRequests()
    {
        var port = FreePort();
        await using var host = new LocalMcpHost(new ExportRepository(() => []));
        await host.StartAsync(port);
        using var client = new HttpClient();
        // Both clients connect to the same HTTP endpoint without a browser Origin header.
        foreach (var clientName in new[] { "codex", "claude-code" })
        {
            var initializeJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = 1, method = "initialize",
                @params = new { protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = clientName, version = "1" } }
            });
            var initialize = await Post(client, port, initializeJson, null);
            Check(initialize.Status == HttpStatusCode.OK && initialize.Body.Contains("list_characters", StringComparison.Ordinal),
                $"{clientName} initialize was rejected or lost the shared guidance: {initialize.Status} {initialize.Body}");
            var characters = await Post(client, port, "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"list_characters\",\"arguments\":{}}}", null);
            Check(characters.Status == HttpStatusCode.OK && characters.Body.Contains("structuredContent", StringComparison.Ordinal) &&
                !characters.Body.Contains("\"isError\":true", StringComparison.Ordinal),
                $"{clientName} could not call list_characters: {characters.Status} {characters.Body}");
        }
        var tools = await Post(client, port, "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\",\"params\":{}}", null);
        Check(tools.Status == HttpStatusCode.OK && tools.Body.Contains("Call this first", StringComparison.Ordinal) &&
            tools.Body.Contains("characterId returned by list_characters", StringComparison.Ordinal),
            "Tool descriptions must carry the essential character rules: " + tools.Body);
        var browser = await Post(client, port, "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/list\",\"params\":{}}", "https://attacker.example");
        Check(browser.Status == HttpStatusCode.Forbidden, "A browser origin must still be rejected.");
    }

    private static async Task<(HttpStatusCode Status, string Body)> Post(HttpClient client, int port, string json, string? origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{port}/mcp")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (origin is not null) request.Headers.Add("Origin", origin);
        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
