using Claudgar.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Claudgar.McpBridge;

/// <summary>Native stdio server: forwards the loopback tool schemas and complete typed results.</summary>
public static class BridgeServer
{
    public static async Task RunAsync(int port, CancellationToken cancellationToken = default)
    {
        var upstream = new LoopbackConnection(port);
        var info = new Implementation { Name = "Claudgar", Version = BuildInfo.Version };
        var instructions = McpInstructions.Text;
        try
        {
            await using var client = await upstream.ConnectAsync(cancellationToken).ConfigureAwait(false);
            info = client.ServerInfo;
            instructions = client.ServerInstructions ?? instructions;
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested)
        {
            // Starting Claude before Claudgar is recoverable: initialize the stdio server and
            // report the service issue on the actual tool request instead of closing stdin.
            await Console.Error.WriteLineAsync("Start Claudgar to use its saved-character tools. " + error.Message).ConfigureAwait(false);
        }
        var options = new McpServerOptions
        {
            ServerInfo = info,
            ServerInstructions = instructions,
            Capabilities = new ServerCapabilities { Tools = new ToolsCapability() },
            Handlers = new McpServerHandlers
            {
                ListToolsHandler = async (request, token) => await upstream.ListToolsAsync(request.Params, token).ConfigureAwait(false),
                CallToolHandler = async (request, token) => await upstream.CallToolAsync(
                    request.Params ?? throw new ArgumentException("A tool request is required."), token).ConfigureAwait(false)
            }
        };
        await using var server = McpServer.Create(new StdioServerTransport("Claudgar Claude bridge"), options);
        await server.RunAsync(cancellationToken).ConfigureAwait(false);
    }
}
