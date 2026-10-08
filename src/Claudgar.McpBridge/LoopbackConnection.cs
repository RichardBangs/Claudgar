using Claudgar.Core;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Claudgar.McpBridge;

/// <summary>Each read-only request gets a fresh upstream session, so app restarts cannot strand Claude.</summary>
public sealed class LoopbackConnection(int port)
{
    private Uri Endpoint => new($"http://127.0.0.1:{port}/mcp");

    public async Task<McpClient> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (port is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        // Never use a system proxy for character data, redirects, or an open-ended HTTP endpoint.
        var http = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = Endpoint,
            Name = "Claudgar loopback",
            TransportMode = HttpTransportMode.StreamableHttp,
            ConnectionTimeout = TimeSpan.FromSeconds(3),
            EnableStandaloneGetStream = false
        }, http, ownsHttpClient: true);
        try
        {
            return await McpClient.CreateAsync(transport, new McpClientOptions
            {
                ClientInfo = new Implementation { Name = "Claudgar Claude bridge", Version = BuildInfo.Version },
                InitializationTimeout = TimeSpan.FromSeconds(3)
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transport.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public Task<ListToolsResult> ListToolsAsync(ListToolsRequestParams? request, CancellationToken cancellationToken = default) =>
        ForwardAsync(client => client.ListToolsAsync(request ?? new ListToolsRequestParams(), cancellationToken), cancellationToken);

    public Task<CallToolResult> CallToolAsync(CallToolRequestParams request, CancellationToken cancellationToken = default) =>
        ForwardAsync(client => client.CallToolAsync(request, cancellationToken), cancellationToken);

    private async Task<T> ForwardAsync<T>(Func<McpClient, ValueTask<T>> request, CancellationToken cancellationToken)
    {
        try
        {
            await using var client = await ConnectAsync(cancellationToken).ConfigureAwait(false);
            return await request(client).ConfigureAwait(false);
        }
        // Preserve protocol errors from the actual server. Transport failures explain that this
        // in-flight request failed; the next request creates a connection to the restarted app.
        catch (McpProtocolException) { throw; }
        catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException or
            InvalidOperationException or McpException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            throw new McpException("This character request failed because Claudgar's local service is unavailable or restarted. Keep Claudgar running, then retry; the connection reconnects for your next request.", error);
        }
    }
}
