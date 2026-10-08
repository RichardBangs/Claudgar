using System.Security;

namespace Claudgar.Core.Setup;

public interface IStartupRegistrationBackend
{
    string? ReadCommand();
    void WriteCommand(string command);
    void RemoveCommand();
}

public sealed record StartupRegistrationResult(bool Succeeded, bool Enabled, bool Changed, string? Warning = null);

/// <summary>Reconciles only Claudgar's current-user startup entry; registration is injectable for tests.</summary>
public sealed class StartupRegistrationService(IStartupRegistrationBackend? backend = null)
{
    private readonly IStartupRegistrationBackend backend = backend ?? new WindowsStartupRegistrationBackend();

    public StartupRegistrationResult Apply(bool enabled, string executablePath)
    {
        bool changed = false;
        string? previous = null;
        try
        {
            var command = BuildCommand(executablePath);
            previous = backend.ReadCommand();
            if (previous is not null && previous != command && !IsManagedCommand(previous))
                return new(false, false, false,
                    "Claudgar's startup entry contains a different command. It was preserved; review Windows startup settings.");
            if (enabled && previous != command)
            {
                backend.WriteCommand(command);
                changed = true;
            }
            else if (!enabled && previous is not null)
            {
                backend.RemoveCommand();
                changed = true;
            }

            var effective = backend.ReadCommand();
            var actualEnabled = effective == command || effective is not null && IsManagedCommand(effective);
            if (actualEnabled != enabled || enabled && effective != command)
                return new(false, actualEnabled, changed, "Windows did not save Claudgar's startup setting. Try again.");
            return new(true, actualEnabled, changed);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException or
            ArgumentException or NotSupportedException)
        {
            bool effectiveEnabled = previous is not null && IsManagedCommand(previous);
            try
            {
                var current = backend.ReadCommand();
                effectiveEnabled = current is not null && IsManagedCommand(current);
            }
            catch (Exception retryError) when (retryError is IOException or UnauthorizedAccessException or
                SecurityException or NotSupportedException) { }
            return new(false, effectiveEnabled, changed, "Launch on startup could not be changed: " + error.Message);
        }
    }

    public static string BuildCommand(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (executablePath.Any(character => character == '"' || char.IsControl(character)) || !Path.IsPathFullyQualified(executablePath))
            throw new ArgumentException("The app executable must have an absolute path without quotes or control characters.", nameof(executablePath));
        var absolute = Path.GetFullPath(executablePath);
        if (!string.Equals(Path.GetFileName(absolute), "Claudgar.exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Launch on startup requires the portable Claudgar.exe app.", nameof(executablePath));
        return "\"" + absolute + "\" --startup";
    }

    private static bool IsManagedCommand(string command)
    {
        const string suffix = "\" --startup";
        if (!command.StartsWith('"') || !command.EndsWith(suffix, StringComparison.Ordinal)) return false;
        try { return BuildCommand(command[1..^suffix.Length]) == command; }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }
    }
}
