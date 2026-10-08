using Claudgar.App.Browser;
using Claudgar.App.Updates;
using Claudgar.Core;

namespace Claudgar.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (AppUpdater.IsHelperInvocation(args)) return AppUpdater.RunHelper(args);
        using var instance = new Mutex(true, @"Local\Claudgar.App", out var firstInstance);
        ApplicationConfiguration.Initialize();
        if (!firstInstance)
        {
            if (!args.Contains("--startup", StringComparer.Ordinal))
                MessageBox.Show("Claudgar is already running. Open it from the system tray.", "Claudgar");
            return 0;
        }
        try
        {
            using var updater = new AppUpdater(BuildInfo.Version);
            if (!AppUpdater.IsConfirmationInvocation(args) && updater.TryStartPendingUpdate(args.Contains("--startup", StringComparer.Ordinal), out _))
                return 0;
            var coordinator = new ApplicationCoordinator(updater, args);
            using var form = new MainForm(coordinator);
            try { Application.Run(form); }
            finally { coordinator.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            return 0;
        }
        catch (Exception error)
        {
            MessageBox.Show("Claudgar could not start.\n\n" + error.Message, "Claudgar", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        finally { instance.ReleaseMutex(); }
    }
}
