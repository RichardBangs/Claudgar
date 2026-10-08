using Claudgar.App.Mcp;
using Claudgar.App.Resources;
using Claudgar.Core.Exports;
using Claudgar.Core.Setup;
using Claudgar.Core;
using Claudgar.Core.Updates;
using Claudgar.App.Updates;

namespace Claudgar.App;

internal sealed class ApplicationCoordinator : IAsyncDisposable
{
    private readonly SettingsStore store = new();
    private readonly GameDiscovery discovery = new();
    private readonly SetupService setup;
    private readonly SetupHealthChecker healthChecker;
    private readonly ClaudeSetupService claudeSetup;
    private readonly ClaudeCodeSetupService claudeCodeSetup;
    private readonly StartupRegistrationService startup = new();
    private readonly AppUpdater updater;
    private readonly CancellationTokenSource lifetime = new();
    private readonly string[] startupArguments;
    private Task? updateCheck;
    private readonly object settingsLock = new();
    private readonly HashSet<string> exportDirectories = new(StringComparer.OrdinalIgnoreCase);
    public AppSettings Settings { get; private set; }
    public ExportRepository Exports { get; }
    public LocalMcpHost Server { get; }
    public IReadOnlyList<GameInstallation> Installations { get; private set; } = [];
    public SetupReport? SetupReport { get; private set; }
    public string? Warning { get; private set; }
    public string? ServerError { get; private set; }
    public ClaudeSetupResult? ClaudeReport { get; private set; }
    public ClaudeCodeSetupResult? ClaudeCodeReport { get; private set; }
    public bool StartInTray { get; }
    public bool StartupEnabled { get; private set; }
    public string? StartupWarning { get; private set; }
    public event Action? UpdateChanged;
    public bool AppUpdateReady => updater.Status.Ready;
    public bool AppUpdateFailed => updater.Status.State == UpdateState.Failed;
    public bool AppUpdatePreparing => updater.Status.State is UpdateState.Checking or UpdateState.Downloading;
    public string? AppUpdateVersion => updater.Status.Version;
    public string AppUpdateMessage => updater.Status.Message ?? "Checks automatically on startup.";

    public ApplicationCoordinator() : this(new AppUpdater(BuildInfo.Version), []) { }

    public ApplicationCoordinator(AppUpdater updater, string[] startupArguments)
    {
        this.updater = updater;
        this.startupArguments = startupArguments;
        StartInTray = startupArguments.Contains("--startup", StringComparer.Ordinal);
        updater.StatusChanged += HandleUpdateStatus;
        var loaded = store.Load();
        Settings = loaded.Settings;
        Warning = loaded.Warning;
        var payload = EmbeddedPayload.Load();
        setup = new(payload.Addon, payload.Skill, BuildInfo.Version);
        claudeSetup = new(EmbeddedPayload.LoadBridgeBinary());
        claudeCodeSetup = new(payload.Skill);
        healthChecker = new(payload.Addon);
        Exports = new ExportRepository(() => { lock (settingsLock) return exportDirectories.ToArray(); });
        Server = new(Exports);
    }

    public async Task InitializeAsync(bool forceAddonRepair = false)
    {
        await Task.Run(() =>
        {
            ConfigureStartup();
            InstallComponents(forceAddonRepair);
        });
        try { await Server.StartAsync(Settings.Port); ServerError = null; }
        catch (Exception error) { ServerError = "The local connection could not start: " + error.Message; }
    }

    public Task<SetupHealth> CheckSetupHealthAsync()
    {
        string[] directories;
        lock (settingsLock) directories = Settings.GameDirectories.ToArray();
        var report = SetupReport;
        var serviceIssue = Server.IsRunning ? null : ServerError ?? "The local connection is unavailable. Repair setup to reconnect.";
        return Task.Run(() =>
        {
            ClaudeReport = claudeSetup.Check(Settings.Port);
            ClaudeCodeReport = claudeCodeSetup.Check(Settings.Port);
            var health = healthChecker.Check(directories, report, serviceIssue);
            // Each chat client is optional and independent: an absent client is reported as success.
            var clientIssue = ClaudeReport is { Succeeded: false } claude
                ? !claude.Helper.Succeeded ? claude.Helper.Message : claude.Configuration.Message
                : ClaudeCodeReport is { Succeeded: false } code
                    ? !code.Configuration.Succeeded ? code.Configuration.Message : code.Skill.Message
                    : null;
            if (!health.NeedsRepair && clientIssue is not null)
                health = health with { NeedsRepair = true, Message = clientIssue };
            return health;
        });
    }

    public void InstallComponents(bool forceAddonRepair = false)
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
        SetupReport = setup.Install(found, Settings.Port, forceAddonRepair: forceAddonRepair);
        ClaudeReport = claudeSetup.Install(Settings.Port);
        ClaudeCodeReport = claudeCodeSetup.Install(Settings.Port);
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

    public Task SetLaunchOnStartupAsync(bool enabled) => Task.Run(() =>
    {
        lock (settingsLock)
        {
            try
            {
                var updated = Settings with { LaunchOnStartup = enabled };
                store.Save(updated);
                Settings = updated;
                var result = startup.Apply(enabled, Environment.ProcessPath ?? "");
                StartupEnabled = result.Enabled;
                StartupWarning = result.Warning;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            { StartupWarning = "The startup preference could not be saved. Existing settings were preserved: " + error.Message; }
        }
    });

    public Task CheckForAppUpdateAsync() => updateCheck ??= Task.Run(RunUpdateCheckAsync);

    private async Task RunUpdateCheckAsync()
    {
        try { await updater.CheckAsync(lifetime.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }

    public bool TryInstallAppUpdate(out string? issue) => updater.TryStartPendingUpdate(StartInTray, out issue);

    public void ConfirmHealthyLaunch()
    {
        if (Server.IsRunning) updater.AcknowledgeHealthyLaunch(startupArguments);
    }

    private void HandleUpdateStatus(UpdateStatus status) => UpdateChanged?.Invoke();

    private void ConfigureStartup()
    {
        lock (settingsLock)
        {
            if (store.Load().Warning is { } issue)
            {
                StartupWarning = "Launch on startup was left unchanged because the settings could not be read. " + issue;
                return;
            }
            var result = startup.Apply(Settings.LaunchOnStartup, Environment.ProcessPath ?? "");
            StartupEnabled = result.Enabled;
            StartupWarning = result.Warning;
        }
    }

    public async ValueTask DisposeAsync()
    {
        updater.StatusChanged -= HandleUpdateStatus;
        lifetime.Cancel();
        if (updateCheck is not null) await updateCheck.ConfigureAwait(false);
        await Server.DisposeAsync().ConfigureAwait(false);
        updater.Dispose();
        lifetime.Dispose();
    }

    private void PersistSettings()
    {
        try { store.Save(Settings); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Warning = "The existing settings file was preserved. Current settings are available for this run: " + error.Message;
        }
    }
}
