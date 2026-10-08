using System.Reflection;

namespace Claudgar.Core;

/// <summary>Release identity shared by the app, its connection, and addon installer.</summary>
public static class BuildInfo
{
    public static string Version { get; } = typeof(BuildInfo).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? throw new InvalidOperationException("The release version is missing.");
}
