using Claudgar.App.Mcp;
using Claudgar.App.Resources;
using Claudgar.Core.Exports;
using Claudgar.Core.Setup;

namespace Claudgar.App;

internal sealed class ApplicationCoordinator : IAsyncDisposable
{
    private readonly SettingsStore store = new();
    private readonly GameDiscovery discovery = new();
    private readonly SetupService setup;
    private readonly SetupHealthChecker healthChecker;
    private readonly object settingsLock = new();
    private readonly HashSet<string> exportDirectories = new(StringComparer.OrdinalIgnoreCase);
    public AppSettings Settings { get; private set; }
    public ExportRepository Exports { get; }
    public LocalMcpHost Server { get; }
    public IReadOnlyList<GameInstallation> Installations { get; private set; } = [];
    public SetupReport? SetupReport { get; private set; }
    public string? Warning { get; private set; }
    public string? ServerError { get; private set; }

    public ApplicationCoordinator()
    {
        var loaded = store.Load();
        Settings = loaded.Settings;
        Warning = loaded.Warning;
        var payload = EmbeddedPayload.Load();
        setup = new(payload.Addon, payload.Skill);
        healthChecker = new(payload.Addon);
        Exports = new ExportRepository(() => { lock (settingsLock) return exportDirectories.ToArray(); });
        Server = new(Exports);
    }

    public async Task InitializeAsync()
    {
        await Task.Run(InstallComponents);
        try { await Server.StartAsync(Settings.Port); ServerError = null; }
        catch (Exception error) { ServerError = "The local connection could not start: " + error.Message; }
    }

    public Task<SetupHealth> CheckSetupHealthAsync()
    {
        string[] directories;
        lock (settingsLock) directories = Settings.GameDirectories.ToArray();
        var report = SetupReport;
        var serviceIssue = Server.IsRunning ? null : ServerError ?? "The local connection is unavailable. Repair setup to reconnect.";
        return Task.Run(() => healthChecker.Check(directories, report, serviceIssue));
    }

    public void InstallComponents()
    {
        var found = discovery.Discover(Settings.GameDirectories);
        lock (settingsLock)
        {
            if (Settings.GameDirectories.Count > 0)
                found = found.Where(i => Settings.GameDirectories.Contains(i.GameDirectory, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (Settings.GameDirectories.Count == 0 && found.Count > 1)
            {
                Warning = "Several Forever Beta installations were found. Choose the game folder you want Claudgar to use.";
                found = [];
            }
            Installations = found;
            foreach (var installation in found) exportDirectories.Add(installation.GameDirectory);
            Settings = Settings with
            {
                GameDirectories = Settings.GameDirectories.Concat(found.Select(i => i.GameDirectory))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };
            PersistSettings();
        }
        SetupReport = setup.Install(found, Settings.Port);
    }

    public string? AddGameDirectory(string path)
    {
        var validation = discovery.Validate(path);
        if (!validation.IsValid) return validation.Error;
        lock (settingsLock)
        {
            Settings = Settings with { GameDirectories = [validation.Installation!.GameDirectory] };
            exportDirectories.Clear();
            Warning = null;
            PersistSettings();
        }
        InstallComponents();
        return null;
    }

    public ValueTask DisposeAsync() => Server.DisposeAsync();

    private void PersistSettings()
    {
        try { store.Save(Settings); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Warning = "The existing settings file was preserved. Current settings are available for this run: " + error.Message;
        }
    }
}
