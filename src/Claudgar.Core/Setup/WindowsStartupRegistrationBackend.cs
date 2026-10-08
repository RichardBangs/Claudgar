using Microsoft.Win32;

namespace Claudgar.Core.Setup;

/// <summary>Uses Windows' per-user Run key without elevation or touching other startup entries.</summary>
public sealed class WindowsStartupRegistrationBackend : IStartupRegistrationBackend
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Claudgar";

    public string? ReadCommand()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows startup registration is available only on Windows.");
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
        var value = key?.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value is null) return null;
        if (value is not string command) throw new IOException("Claudgar's startup entry has an unsupported value type; it was preserved.");
        return command;
    }

    public void WriteCommand(string command)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows startup registration is available only on Windows.");
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        key.SetValue(ValueName, command, RegistryValueKind.String);
        key.Flush();
    }

    public void RemoveCommand()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows startup registration is available only on Windows.");
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
        key?.Flush();
    }
}
