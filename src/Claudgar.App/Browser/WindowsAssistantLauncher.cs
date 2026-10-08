using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;

namespace Claudgar.App.Browser;

/// <summary>Launches Windows-registered desktop assistants without guessing protocols or user paths.</summary>
internal sealed class WindowsAssistantLauncher : IAssistantLauncher
{
    public AssistantLaunchResult Open(AssistantKind kind)
    {
        var installed = Discover();
        var plan = AssistantLaunchPlan.For(kind, installed);
        foreach (var app in AssistantLaunchPlan.Targets(kind, installed))
        {
            try
            {
                if (app.IsPackagedApp) Activate(app.Target);
                else Process.Start(new ProcessStartInfo(app.Target) { UseShellExecute = true });
                return new(true, false, "");
            }
            catch (Exception error) when (error is COMException or Win32Exception or InvalidOperationException or UnauthorizedAccessException) { }
        }

        try
        {
            Process.Start(new ProcessStartInfo(plan.BrowserUrl) { UseShellExecute = true });
            return new(true, true, plan.FallbackMessage);
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            return new(false, false, "The assistant could not open. Please open its desktop app from the Windows Start menu.");
        }
    }

    private static IReadOnlyList<InstalledAssistant> Discover()
    {
        var apps = new List<InstalledAssistant>();
        DiscoverPackagedApps(apps);
        DiscoverStartMenuApps(apps);
        foreach (var (name, executable) in new[] { ("ChatGPT", "ChatGPT.exe"), ("Codex", "Codex.exe"), ("Claude", "Claude.exe") })
        {
            foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    using var key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + executable);
                    var target = (key?.GetValue(null) as string)?.Trim().Trim('"');
                    if (!string.IsNullOrEmpty(target) && Path.IsPathFullyQualified(target) &&
                        Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(target))
                        apps.Add(new(name, target, false));
                }
                catch (Exception error) when (error is SecurityException or UnauthorizedAccessException or IOException) { }
            }
        }
        return apps.DistinctBy(app => app.Target, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void DiscoverPackagedApps(List<InstalledAssistant> apps)
    {
        object? shell = null;
        object? folder = null;
        object? items = null;
        try
        {
            var type = Type.GetTypeFromProgID("Shell.Application");
            if (type is null) return;
            shell = Activator.CreateInstance(type);
            if (shell is null) return;
            folder = ((dynamic)shell).Namespace("shell:AppsFolder");
            if (folder is null) return;
            items = ((dynamic)folder).Items();
            var count = (int)((dynamic)items).Count;
            for (var index = 0; index < count; index++)
            {
                object? item = null;
                try
                {
                    item = ((dynamic)items).Item(index);
                    if (item is null) continue;
                    var name = (string)((dynamic)item).Name;
                    if (!AssistantLaunchPlan.Matches(AssistantKind.ChatGpt, name) && !AssistantLaunchPlan.Matches(AssistantKind.Claude, name)) continue;
                    var target = (string)((dynamic)item).Path;
                    if (IsAppUserModelId(target)) apps.Add(new(name, target, true));
                    else if (IsInstalledFile(target)) apps.Add(new(name, target, false));
                }
                finally { Release(item); }
            }
        }
        catch (Exception error) when (error is COMException or InvalidCastException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }
        finally { Release(items); Release(folder); Release(shell); }
    }

    private static bool IsAppUserModelId(string value) => value.Count(character => character == '!') == 1 &&
        value.Length is > 3 and < 256 && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-' or '!');

    private static void DiscoverStartMenuApps(List<InstalledAssistant> apps)
    {
        foreach (var directory in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
        })
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) continue;
            try
            {
                foreach (var link in Directory.EnumerateFiles(directory, "*.lnk", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileNameWithoutExtension(link);
                    if (AssistantLaunchPlan.Matches(AssistantKind.ChatGpt, name) || AssistantLaunchPlan.Matches(AssistantKind.Claude, name))
                        apps.Add(new(name, link, false));
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException) { }
        }
    }

    private static bool IsInstalledFile(string target) => Path.IsPathFullyQualified(target) &&
        (Path.GetExtension(target).Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
         Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase)) && File.Exists(target);

    private static void Activate(string appUserModelId)
    {
        var manager = (IApplicationActivationManager)new ApplicationActivationManager();
        try
        {
            const uint noErrorUi = 0x00000002;
            var result = manager.ActivateApplication(appUserModelId, "", noErrorUi, out _);
            Marshal.ThrowExceptionForHR(result);
        }
        finally { Release(manager); }
    }

    private static void Release(object? instance)
    {
        if (instance is not null && Marshal.IsComObject(instance)) Marshal.ReleaseComObject(instance);
    }

    [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    private class ApplicationActivationManager { }

    [ComImport, Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        [PreserveSig]
        int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [MarshalAs(UnmanagedType.LPWStr)] string arguments, uint options, out uint processId);
        [PreserveSig]
        int ActivateForFile([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId, nint itemArray,
            [MarshalAs(UnmanagedType.LPWStr)] string verb, out uint processId);
        [PreserveSig]
        int ActivateForProtocol([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId, nint itemArray, out uint processId);
    }
}
