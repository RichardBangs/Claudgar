using System.Text.Json;
using System.Text.Json.Serialization;

namespace Claudgar.Core.Setup;

public sealed record AppSettings
{
    public const int DefaultPort = 43827;
    public int SchemaVersion { get; init; } = 1;
    public int Port { get; init; } = DefaultPort;
    public bool LaunchOnStartup { get; init; } = true;
    public List<string> GameDirectories { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalSettings { get; init; }
}

public sealed record SettingsLoadResult(AppSettings Settings, string? Warning = null);
