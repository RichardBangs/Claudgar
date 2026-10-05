using Claudgar.App.Browser;

namespace Claudgar.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var instance = new Mutex(true, @"Local\Claudgar.App", out var firstInstance);
        ApplicationConfiguration.Initialize();
        if (!firstInstance)
        {
            MessageBox.Show("Claudgar is already running. Open it from the system tray.", "Claudgar");
            return;
        }
        try
        {
            var coordinator = new ApplicationCoordinator();
            using var form = new MainForm(coordinator);
            Application.Run(form);
            coordinator.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (Exception error)
        {
            MessageBox.Show("Claudgar could not start.\n\n" + error.Message, "Claudgar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
