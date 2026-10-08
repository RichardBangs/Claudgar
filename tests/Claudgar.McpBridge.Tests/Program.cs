using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using Claudgar.Core;
using Claudgar.McpBridge;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

var bridgeBinary = args.Length == 2 && args[0] == "--bridge" ? Path.GetFullPath(args[1]) : null;
var failed = 0;
foreach (var (name, test) in new (string, Func<Task>)[]
{
    ("Native stdio forwards instructions, all tools, metadata, results, and errors", ProxyPreservesData),
    ("A live stdio connection recovers after a missing service and app restart", Reconnects)
})
{
    try { await test(); Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
}
return failed == 0 ? 0 : 1;

async Task ProxyPreservesData()
{
    var port = FreePort();
    await using var host = await StartHost(port, "first");
    await using var client = await ConnectBridge(port);
    Assert(client.ServerInstructions == McpInstructions.Text + " Test upstream instructions.", "Upstream instructions were not forwarded: " + client.ServerInstructions);
    Assert(client.ServerInfo.Version == "1.2.3", "Upstream server version was not forwarded.");
    var tools = await client.ListToolsAsync(new ListToolsRequestParams());
    Assert(tools.Tools.Count == 6, "The six tools were not forwarded.");
    foreach (var tool in tools.Tools)
    {
        Assert(tool.Annotations?.ReadOnlyHint == true && tool.Title == "Saved " + tool.Name, "Tool annotations/title were lost.");
        Assert(tool.InputSchema.GetProperty("properties").TryGetProperty("characterId", out _), "Input schema was lost.");
        Assert(tool.OutputSchema?.GetProperty("type").GetString() == "object", "Output schema was lost.");
        Assert(tool.Meta?["coverage"]?.GetValue<string>() == "saved only", "Tool metadata was lost.");
        var result = await client.CallToolAsync(new CallToolRequestParams
        {
            Name = tool.Name,
            Arguments = new Dictionary<string, JsonElement> { ["characterId"] = JsonSerializer.SerializeToElement("opaque-123") }
        });
        Assert(result.StructuredContent?.GetProperty("characterId").GetString() == "opaque-123", "Opaque character ID or structured result was lost.");
        Assert(result.StructuredContent?.GetProperty("generation").GetString() == "first", "Structured generation was lost.");
        Assert(result.Meta?["freshness"]?.GetValue<string>() == "stale", "Result metadata was lost.");
        Assert(result.Content.OfType<TextContentBlock>().Single().Text == tool.Name, "Tool text content was lost.");
    }
    var toolError = await client.CallToolAsync(new CallToolRequestParams { Name = "get_equipment", Arguments = new Dictionary<string, JsonElement> { ["characterId"] = JsonSerializer.SerializeToElement("missing") } });
    Assert(toolError.IsError == true && toolError.StructuredContent?.GetProperty("missing").GetBoolean() == true, "Structured tool errors were lost.");
    try { await client.CallToolAsync(new CallToolRequestParams { Name = "unknown_tool" }); throw new InvalidOperationException("Protocol error was swallowed."); }
    catch (McpProtocolException error) { Assert(error.ErrorCode == McpErrorCode.InvalidParams && error.Message.Contains("Unknown tool", StringComparison.Ordinal), "Protocol error code/message was lost."); }
}

async Task Reconnects()
{
    var port = FreePort();
    // Claude can start before the desktop app. Its stdio server remains running and initialize works.
    await using var client = await ConnectBridge(port);
    Assert(client.ServerInstructions == McpInstructions.Text, "Missing-service initialization lost character guidance.");
    await ExpectUnavailable(client);
    var host = await StartHost(port, "before restart");
    try
    {
        var first = await client.CallToolAsync(new CallToolRequestParams { Name = "list_characters" });
        Assert(first.StructuredContent?.GetProperty("generation").GetString() == "before restart", "Bridge did not connect after starting Claudgar: " + JsonSerializer.Serialize(first));
    }
    finally { await host.StopAsync(); await host.DisposeAsync(); }
    await ExpectUnavailable(client);
    await using var restarted = await StartHost(port, "after restart");
    var second = await client.CallToolAsync(new CallToolRequestParams { Name = "list_characters" });
    Assert(second.StructuredContent?.GetProperty("generation").GetString() == "after restart", "Existing Claude stdio connection did not recover after app replacement.");
}

async Task<McpClient> ConnectBridge(int port)
{
    var command = bridgeBinary ?? "dotnet";
    string[] arguments = bridgeBinary is null
        ? ["exec", "--runtimeconfig", Path.Combine(AppContext.BaseDirectory, "Claudgar.McpBridge.Tests.runtimeconfig.json"),
            "--depsfile", Path.Combine(AppContext.BaseDirectory, "Claudgar.McpBridge.Tests.deps.json"),
            typeof(BridgeServer).Assembly.Location, "--port", port.ToString()]
        : ["--port", port.ToString()];
    return await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
    {
        Command = command, Arguments = arguments,
        StandardErrorLines = _ => { }, ShutdownTimeout = TimeSpan.FromSeconds(3)
    }), new McpClientOptions { InitializationTimeout = TimeSpan.FromSeconds(10) });
}

static async Task ExpectUnavailable(McpClient client)
{
    try { await client.ListToolsAsync(new ListToolsRequestParams()); throw new InvalidOperationException("Missing service unexpectedly responded."); }
    catch (McpException error) { Assert(error.Message.Contains("Keep Claudgar running", StringComparison.Ordinal), "Missing-service failure did not give recovery instructions."); }
}

static async Task<WebApplication> StartHost(int port, string generation)
{
    var builder = WebApplication.CreateSlimBuilder();
    builder.Logging.ClearProviders();
    builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, port));
    builder.Services.AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation { Name = "Claudgar", Version = "1.2.3" };
        options.ServerInstructions = McpInstructions.Text + " Test upstream instructions.";
    }).WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
      .WithListToolsHandler((request, token) => new ValueTask<ListToolsResult>(new ListToolsResult
      {
          Tools = new[] { "list_characters", "get_quests", "get_talents", "get_inventory", "get_equipment", "get_character" }.Select(name => new Tool
          {
              Name = name, Title = "Saved " + name, Description = "Read saved section",
              InputSchema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { characterId = new { type = "string" } } }),
              OutputSchema = JsonSerializer.SerializeToElement(new { type = "object" }),
              Annotations = new ToolAnnotations { ReadOnlyHint = true, DestructiveHint = false },
              Meta = new JsonObject { ["coverage"] = "saved only" }
          }).ToList()
      }))
      .WithCallToolHandler((request, token) =>
      {
          var name = request.Params!.Name;
          if (name == "unknown_tool") throw new McpProtocolException("Unknown tool", McpErrorCode.InvalidParams);
          var id = request.Params.Arguments?.TryGetValue("characterId", out var argument) == true ? argument.GetString() : "opaque-123";
          return new ValueTask<CallToolResult>(new CallToolResult
          {
              Content = [new TextContentBlock { Text = name }],
              StructuredContent = JsonSerializer.SerializeToElement(new { characterId = id, generation, missing = id == "missing" }),
              Meta = new JsonObject { ["freshness"] = "stale" }, IsError = id == "missing"
          });
      });
    var app = builder.Build();
    app.MapMcp("/mcp");
    await app.StartAsync();
    return app;
}

static int FreePort()
{
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    return ((IPEndPoint)listener.LocalEndpoint).Port;
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
