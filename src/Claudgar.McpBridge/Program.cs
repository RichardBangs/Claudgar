using Claudgar.McpBridge;

// Stdout is exclusively the MCP transport. Human diagnostics belong on stderr.
if (args.Length != 2 || args[0] != "--port" || !int.TryParse(args[1], out var port) || port is < 1024 or > 65535)
{
    await Console.Error.WriteLineAsync("Use Claudgar setup repair to register the Claude connection helper (--port <1024-65535>).");
    return 2;
}
try
{
    await BridgeServer.RunAsync(port);
    return 0;
}
catch (Exception error)
{
    await Console.Error.WriteLineAsync("Claudgar's Claude connection ended: " + error.Message);
    return 1;
}
