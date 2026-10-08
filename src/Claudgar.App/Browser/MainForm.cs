using System.Text.Json.Nodes;
using Claudgar.Core.Data;
using Claudgar.Core.Setup;
using Claudgar.Core.Exports;
using Claudgar.App.Resources;
using Claudgar.Core;

namespace Claudgar.App.Browser;

internal sealed class MainForm : Form
{
    private readonly ApplicationCoordinator coordinator;
    private readonly NotifyIcon tray;
    private readonly BrandArtwork brandArtwork = new();
    private readonly PixelPortrait headerPortrait;
    private readonly ExportChangeTracker exportChanges = new();
    private readonly ToolTip rosterTooltip = new();
    private readonly System.Windows.Forms.Timer refreshTimer = new() { Interval = 3000 };
    private readonly Label connection = new() { AutoSize = true, ForeColor = BrowserTheme.Muted, Padding = new Padding(8, 8, 16, 8), Anchor = AnchorStyles.Right };
    private readonly Label exportStatus = new() { Dock = DockStyle.Fill, AutoSize = true, ForeColor = BrowserTheme.Muted, Padding = new Padding(20, 12, 20, 12), Margin = Padding.Empty };
    private readonly TextBox characterSearch = new() { Dock = DockStyle.Fill, PlaceholderText = "Find a character or realm", Margin = new Padding(0, 6, 0, 18) };
    private readonly ListBox characters = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false, ItemHeight = 76, DrawMode = DrawMode.OwnerDrawFixed, BackColor = BrowserTheme.Surface, ForeColor = BrowserTheme.Ink };
    private readonly CharacterBanner characterHeading = new() { Dock = DockStyle.Top };
    private readonly ThemeTabs tabs = new() { Dock = DockStyle.Fill };
    private readonly RichTextBox setupDetails = new() { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = BrowserTheme.Surface, ForeColor = BrowserTheme.Ink, Font = new Font("Segoe UI", 10) };
    private readonly Label rosterCount = new() { Text = "Characters", Dock = DockStyle.Fill, AutoSize = true, ForeColor = BrowserTheme.Muted, Font = new Font("Segoe UI", 10, FontStyle.Bold), Margin = new Padding(0, 6, 0, 16) };
    private readonly AssistantHome assistantHome = new() { Dock = DockStyle.Fill };
    private readonly Panel dataView = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Button settingsButton;
    private readonly AppSettingsMenu settingsMenu;
    private readonly Button updateAction;
    private readonly Button dataBackButton;
    private readonly Dictionary<string, SectionBrowser> browsers = [];
    private readonly List<Button> actions = [];
    private readonly SetupIssueBar setupIssueBar;
    private readonly SplitContainer characterBrowser;
    private readonly GameFolderPrompt gameFolderPrompt;
    private readonly ToolStripMenuItem exitTrayMenu = new("Exit Claudgar");
    private readonly ReleaseControls releaseControls;
    private bool changingStartup;
    private bool initialVisibilityHandled;
    private bool initializing;
    private JsonArray characterData = [];
    private JsonObject? currentSnapshot;
    private string? exportAttention;
    private string? selectedId;
    private bool busy;
    private bool exiting;
    private bool populating;
    private bool initialized;
    private bool checkingForChanges;
    private bool retryRequested;
    private int selectionLoadVersion;
    private SetupHealth? setupHealth;
    private DateTimeOffset nextSetupCheck;
    private FormWindowState restoreWindowState = FormWindowState.Normal;

    private sealed record CharacterEntry(string Id, string Name, string Detail, JsonObject Data)
    {
        public override string ToString() => Name;
    }

    public MainForm(ApplicationCoordinator coordinator)
    {
        this.coordinator = coordinator;
        Text = "Claudgar · WoW Forever Beta";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        Width = 1440; Height = 960; MinimumSize = new Size(1120, 750);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10); ForeColor = BrowserTheme.Ink; BackColor = BrowserTheme.Background;
        Icon = brandArtwork.WindowIcon;

        BrowserTheme.StyleInput(characterSearch);
        var title = new Label { Text = "CLAUDGAR", Font = new Font("Cambria", 22, FontStyle.Bold), AutoSize = true, ForeColor = BrowserTheme.Accent, Margin = Padding.Empty };
        var wordmark = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        wordmark.Controls.Add(title);
        wordmark.Controls.Add(new Label { Text = "WoW Forever Beta", AutoSize = true, ForeColor = BrowserTheme.Muted,
            Font = new Font("Segoe UI", 9), Margin = new Padding(2, 0, 0, 0) });
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4, RowCount = 1, Padding = new Padding(20, 16, 20, 16), Margin = Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerPortrait = new PixelPortrait(brandArtwork.Portrait) { Size = new Size(72, 72), Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 10, 0), AccessibleName = "Claudgar, your archmage companion" };
        header.Controls.Add(headerPortrait, 0, 0);
        header.Controls.Add(wordmark, 1, 0);
        releaseControls = new ReleaseControls(async (_, _) => await ChangeStartupAsync(), (_, _) => InstallUpdate());
        SetStartupCheck(coordinator.Settings.LaunchOnStartup);
        releaseControls.StartupCheckBox.Enabled = false;
        settingsMenu = new AppSettingsMenu(releaseControls, async (_, _) => await ChooseGameAsync(), async (_, _) => await RepairAsync(), (_, _) => ShowSetupView());
        settingsButton = new MinimalButton("⚙", (_, _) => settingsMenu.Show(settingsButton!, new Point(0, settingsButton!.Height)))
        {
            AutoSize = false, Size = new Size(44, 44), MinimumSize = new Size(44, 44), Padding = Padding.Empty,
            Font = new Font("Segoe UI Symbol", 18), ForeColor = BrowserTheme.Muted, BackColor = BrowserTheme.Background,
            AccessibleName = "Settings and help", Margin = Padding.Empty
        };
        updateAction = new MinimalButton("Update available", (_, _) => { if (coordinator.AppUpdateReady) InstallUpdate(); else settingsMenu.Show(settingsButton, new Point(0, settingsButton.Height)); })
        {
            MinimumSize = new Size(0, 36), Padding = new Padding(10, 4, 10, 4), Visible = false,
            ForeColor = BrowserTheme.Accent, AccessibleName = "Install available Claudgar update"
        };
        var headerActions = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false, Anchor = AnchorStyles.Right, Margin = Padding.Empty };
        headerActions.Controls.Add(updateAction); headerActions.Controls.Add(connection); headerActions.Controls.Add(settingsButton);
        header.Controls.Add(headerActions, 3, 0);
        setupIssueBar = new SetupIssueBar(async (_, _) => await ChooseGameAsync(), async (_, _) => await RepairAsync());
        actions.AddRange([setupIssueBar.ChooseFolderButton, setupIssueBar.RepairButton]);

        var sidebar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(14), BackColor = BrowserTheme.Surface, Margin = Padding.Empty };
        sidebar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.Controls.Add(rosterCount, 0, 0);
        sidebar.Controls.Add(characterSearch, 0, 1);
        sidebar.Controls.Add(characters, 0, 2);
        var content = new Panel { Dock = DockStyle.Fill, BackColor = BrowserTheme.Background, Padding = new Padding(16, 0, 0, 0) };
        var dataToolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0, 10, 0, 10), WrapContents = false, Margin = Padding.Empty };
        dataBackButton = new MinimalButton("‹ Back", (_, _) => ShowAssistantHome()) { AccessibleName = "Back to assistants", MinimumSize = new Size(0, 38) };
        dataToolbar.Controls.Add(dataBackButton);
        dataToolbar.Controls.Add(new Label { Text = "Character data", AutoSize = true, ForeColor = BrowserTheme.Muted, Margin = new Padding(12, 10, 0, 0) });
        dataView.Controls.Add(tabs); dataView.Controls.Add(exportStatus); dataView.Controls.Add(dataToolbar);
        exportStatus.Dock = DockStyle.Bottom;
        content.Controls.Add(dataView); content.Controls.Add(assistantHome); content.Controls.Add(characterHeading);
        characterHeading.DetailsRequested += (_, _) => ShowDataView();
        assistantHome.DetailsRequested += (_, _) => ShowDataView();
        foreach (var name in ExportContract.SectionNames)
        {
            var browser = new SectionBrowser(name);
            browsers[name] = browser;
            tabs.AddPage(JsonPresentation.Humanize(name), browser);
        }
        var setupPage = new Panel { BackColor = BrowserTheme.Surface, Padding = new Padding(22) };
        setupPage.Controls.Add(setupDetails); tabs.AddPage("Setup & status", setupPage);
        characterBrowser = new SplitContainer { Size = new Size(1360, 700), Dock = DockStyle.Fill, SplitterDistance = 210, Panel1MinSize = 180, Panel2MinSize = 790, FixedPanel = FixedPanel.Panel1, IsSplitterFixed = true, SplitterWidth = 1, BackColor = BrowserTheme.Background, Margin = Padding.Empty };
        characterBrowser.Panel1.Controls.Add(sidebar); characterBrowser.Panel2.Controls.Add(content);
        gameFolderPrompt = new GameFolderPrompt(async (_, _) => await ChooseGameAsync());
        actions.Add(gameFolderPrompt.ChooseFolderButton);
        var body = new Panel { Dock = DockStyle.Fill, Margin = new Padding(16, 0, 16, 16) };
        body.Controls.Add(characterBrowser);
        body.Controls.Add(gameFolderPrompt);
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.Controls.Add(header, 0, 0); shell.Controls.Add(setupIssueBar, 0, 1); shell.Controls.Add(body, 0, 2);
        Controls.Add(shell);
        UpdateGameFolderView();

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Open Claudgar", null, (_, _) => ShowBrowser());
        exitTrayMenu.Click += (_, _) => Exit();
        trayMenu.Items.Add(exitTrayMenu);
        tray = new NotifyIcon { Icon = brandArtwork.TrayIcon, Text = "Claudgar · Saved Forever data", Visible = true, ContextMenuStrip = trayMenu };
        tray.DoubleClick += (_, _) => ShowBrowser();
        characters.DrawItem += DrawCharacter;
        characters.MouseMove += (_, e) =>
        {
            var index = characters.IndexFromPoint(e.Location);
            var detail = index >= 0 && characters.Items[index] is CharacterEntry entry ? entry.Detail : "";
            if (rosterTooltip.GetToolTip(characters) != detail) rosterTooltip.SetToolTip(characters, detail);
        };
        characters.HandleCreated += (_, _) => ScaleCharacterRows();
        characters.DpiChangedAfterParent += (_, _) => ScaleCharacterRows();
        characters.SelectedIndexChanged += async (_, _) =>
        {
            if (populating || characters.SelectedItem is not CharacterEntry entry) return;
            selectedId = entry.Id;
            try { await LoadSelectedAsync(); }
            catch (Exception error) { ShowError(error); }
        };
        characterSearch.TextChanged += async (_, _) =>
        {
            PopulateCharacters();
            try { await LoadSelectedAsync(); }
            catch (Exception error) { ShowError(error); }
        };
        refreshTimer.Tick += async (_, _) => await CheckSavedDataAsync();
        Activated += async (_, _) => await CheckSavedDataAsync(checkSetup: true);
        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized) Hide();
            else { restoreWindowState = WindowState; AdjustBannerHeight(); }
        };
        Shown += async (_, _) => await StartAsync();
        FormClosing += (_, e) =>
        {
            if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
        };
        FormClosed += (_, _) => { refreshTimer.Stop(); tray.Visible = false; };
        coordinator.UpdateChanged += OnUpdateChanged;
        AdjustBannerHeight();
    }

    private async Task StartAsync()
    {
        if (initialized || initializing) return;
        initializing = true;
        gameFolderPrompt.SetSearching(true);
        SetBusy(true);
        try { await coordinator.InitializeAsync(); }
        catch (Exception error) { ShowError(error); }
        finally { SetBusy(false); initializing = false; }
        initialized = true;
        SetStartupCheck(coordinator.StartupEnabled);
        releaseControls.StartupCheckBox.Enabled = true;
        coordinator.ConfirmHealthyLaunch();
        await CheckSavedDataAsync(checkSetup: true);
        if (!IsDisposed) refreshTimer.Start();
        _ = coordinator.CheckForAppUpdateAsync();
    }

    private async Task CheckSavedDataAsync(bool checkSetup = false)
    {
        if (!initialized || busy || checkingForChanges || IsDisposed) return;
        checkingForChanges = true;
        SetBusy(true, showWaitCursor: false);
        try
        {
            if (retryRequested) { exportChanges.Invalidate(); retryRequested = false; }
            if (checkSetup || DateTimeOffset.UtcNow >= nextSetupCheck)
            {
                setupHealth = await coordinator.CheckSetupHealthAsync();
                if (IsDisposed) return;
                nextSetupCheck = DateTimeOffset.UtcNow.AddSeconds(30);
                UpdateSetupStatus();
            }
            var directories = coordinator.Settings.GameDirectories.ToArray();
            var changed = await Task.Run(() => exportChanges.HasChanges(directories));
            if (IsDisposed) return;
            if (changed && await RefreshDataAsync()) exportChanges.AcceptChanges();
        }
        catch (Exception error)
        {
            if (!IsDisposed) SetExportStatus("Waiting to read saved data: " + error.Message);
        }
        finally { checkingForChanges = false; if (!IsDisposed) SetBusy(false); }
    }

    private async Task<bool> RefreshDataAsync()
    {
        try
        {
            var result = await Task.Run(coordinator.Exports.ListCharacters);
            if (IsDisposed) return false;
            characterData = result["characters"] as JsonArray ?? [];
            PopulateCharacters();
            exportStatus.Text = ExportStatusPresentation.Describe(result);
            exportAttention = characterData.Count == 0 || result["issues"] is JsonArray { Count: > 0 } ? exportStatus.Text : null;
            var selectedReadSucceeded = await LoadSelectedAsync();
            if (IsDisposed) return false;
            UpdateSetupStatus();
            // A save may be temporarily locked or incomplete. Retry until its valid snapshot is available.
            return selectedReadSucceeded && result["issues"] is not JsonArray { Count: > 0 };
        }
        catch (Exception error) { if (!IsDisposed) SetExportStatus("Could not read saved data: " + error.Message); return false; }
    }

    private void PopulateCharacters()
    {
        populating = true; characters.BeginUpdate(); characters.Items.Clear();
        var filter = characterSearch.Text.Trim();
        foreach (var item in characterData.OfType<JsonObject>())
        {
            var identity = item["character"] as JsonObject;
            var id = item["characterId"]?.ToString();
            if (id is null) continue;
            var name = identity?["name"]?.ToString() ?? "Unnamed character";
            var realm = identity?["realm"]?.ToString() ?? "Unknown realm";
            var detail = $"{realm} · Level {identity?["level"]} {identity?["className"]}";
            var accountId = item["accountId"]?.ToString() ?? "";
            detail = "Account " + (accountId.Length > 12 ? accountId[^6..] : accountId) + " · " + detail;
            if (filter.Length > 0 && !(name + " " + detail).Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            characters.Items.Add(new CharacterEntry(id, name, detail, item));
        }
        var index = characters.Items.Cast<CharacterEntry>().ToList().FindIndex(c => c.Id == selectedId);
        if (index < 0 && characters.Items.Count > 0) index = 0;
        characters.SelectedIndex = index;
        rosterCount.Text = "Characters";
        rosterCount.AccessibleDescription = $"{characterData.Count} saved characters";
        characterSearch.Visible = characterData.Count > 1;
        if (index >= 0) selectedId = ((CharacterEntry)characters.Items[index]).Id;
        else selectedId = null;
        characters.EndUpdate(); populating = false;
    }

    private async Task<bool> LoadSelectedAsync()
    {
        var requestVersion = ++selectionLoadVersion;
        if (characters.SelectedItem is not CharacterEntry entry)
        {
            currentSnapshot = null; characterHeading.SetCharacter(null);
            assistantHome.SetCharacter(null);
            if (exportAttention is not null) assistantHome.SetExportStatus(exportAttention);
            foreach (var browser in browsers.Values) { browser.SetCharacter(null); browser.SetSection(null, false); }
            return true;
        }
        var id = entry.Id;
        var snapshot = await Task.Run(() => coordinator.Exports.GetCharacter(id));
        if (IsDisposed || requestVersion != selectionLoadVersion || selectedId != id ||
            characters.SelectedItem is not CharacterEntry selected || selected.Id != id) return false;
        currentSnapshot = snapshot;
        characterHeading.SetCharacter(snapshot);
        assistantHome.SetCharacter(snapshot);
        if (exportAttention is not null) assistantHome.SetExportStatus(exportAttention);
        var freshness = snapshot["freshness"] as JsonObject;
        var stale = snapshot["exportState"]?.ToString() == "stale" || freshness?["isStale"]?.GetValue<bool>() == true || freshness?["cached"]?.GetValue<bool>() == true;
        foreach (var (name, browser) in browsers)
        {
            browser.SetCharacter(snapshot["sections"]?["character"] as JsonObject);
            browser.SetSection(snapshot["sections"]?[name] as JsonObject, stale);
        }
        var succeeded = !stale && snapshot["error"] is null;
        if (!succeeded) retryRequested = true;
        return succeeded;
    }

    private async Task ChooseGameAsync()
    {
        if (busy) return;
        SetBusy(true);
        try
        {
            using var picker = new FolderBrowserDialog
            {
                Description = "Choose the World of Warcraft folder or its _classic_beta_ folder.",
                UseDescriptionForTitle = true,
                SelectedPath = new GameDiscovery().SuggestDirectory(coordinator.Settings.GameDirectories),
                ShowNewFolderButton = false
            };
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            var error = await Task.Run(() => coordinator.AddGameDirectory(picker.SelectedPath));
            if (error is not null) MessageBox.Show(this, error, "Game folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception error) { ShowError(error); }
        finally { SetBusy(false); }
        SetStartupCheck(coordinator.StartupEnabled);
        exportChanges.Invalidate();
        await CheckSavedDataAsync(checkSetup: true);
    }

    private async Task RepairAsync()
    {
        if (busy) return;
        SetBusy(true);
        try
        {
            await coordinator.InitializeAsync(forceAddonRepair: true);
        }
        catch (Exception error) { ShowError(error); }
        finally { SetBusy(false); }
        SetStartupCheck(coordinator.StartupEnabled);
        exportChanges.Invalidate();
        await CheckSavedDataAsync(checkSetup: true);
    }

    private void UpdateSetupStatus()
    {
        UpdateGameFolderView();
        releaseControls.SetStartupWarning(coordinator.StartupWarning);
        connection.Text = coordinator.Server.IsRunning ? "●  Ready" : "●  Needs attention";
        connection.ForeColor = coordinator.Server.IsRunning ? BrowserTheme.Success : BrowserTheme.Warning;
        connection.AccessibleDescription = coordinator.Server.IsRunning ? "Local service ready" : "Local service is not running. Open settings for connection details.";
        var lines = new List<string>
        {
            $"CLAUDGAR {BuildInfo.Version}", "", "SETUP & CONNECTION", "",
            $"Local service: {(coordinator.Server.IsRunning ? "ready" : "not running")}",
            $"ChatGPT local address: http://127.0.0.1:{coordinator.Settings.Port}/mcp",
            $"Latest client request: {coordinator.Server.LastClientRequest?.ToLocalTime().ToString("g") ?? "none yet"}",
            "A client request indicates contact with the service; it does not prove that a chat app completed a tool call.", "",
            $"Launch on startup: {(coordinator.StartupEnabled ? "enabled" : "disabled")}",
            "App updates: " + coordinator.AppUpdateMessage, ""
        };
        if (coordinator.Warning is not null) lines.Add(coordinator.Warning);
        if (coordinator.ServerError is not null) lines.Add(coordinator.ServerError);
        if (coordinator.StartupWarning is not null) lines.Add(coordinator.StartupWarning);
        if (coordinator.Installations.Count == 0)
            lines.Add("No Forever Beta installation was selected. Choose your game folder to install the addon.");
        foreach (var game in coordinator.Installations) lines.Add($"Forever Beta {game.Version}\n{game.GameDirectory}");
        if (coordinator.SetupReport is { } report)
        {
            lines.Add(""); lines.Add("Addon setup:");
            foreach (var result in report.Addons) lines.Add(result.Message);
        }
        lines.AddRange(["", "CHAT CLIENTS", ""]);
        lines.AddRange(ChatClientStatusPresentation.Lines(coordinator.SetupReport, coordinator.ClaudeReport, coordinator.ClaudeCodeReport));
        lines.AddRange(["FIRST USE", "1. Keep Claudgar running and start Forever Beta. Enable Claudgar in the AddOns menu.",
            "2. Enter the world, then /reload or log out so SavedVariables are written.",
            "3. Your saved data appears automatically. Select your character, then open ChatGPT or Claude to ask a question. Suggested questions copy to your clipboard.",
            "4. After setup, restart ChatGPT, fully quit and reopen Claude Desktop, or start a new Claude Code session. Ask it about your Forever character.",
            "5. On your phone: run claude remote-control on this PC (Claude Code, paid plan) and continue the session from the Claude app.", "",
            "DATA", "All five sections are shown, including coverage, observation time, and unavailable fields.",
            "Only saved exports are read. A running game does not continuously write them.",
            "Character data requested by a chat app is processed by the model provider. Local account and installation paths are excluded from tool results.", "",
            "Minimise to keep the app in the tray. Open it from the tray, or use Exit Claudgar to stop it."]);
        setupDetails.Text = string.Join(Environment.NewLine, lines);
    }

    private void UpdateGameFolderView()
    {
        var hasGame = coordinator.Installations.Count > 0;
        settingsMenu.SetHasGame(hasGame);
        setupIssueBar.SetHealth(setupHealth, showFolderPrompt: !hasGame);
        characterBrowser.Visible = hasGame;
        connection.Visible = hasGame;
        gameFolderPrompt.Visible = !hasGame;
        gameFolderPrompt.SetSearching(false, setupHealth?.NeedsGameFolder == true ? setupHealth.Message : coordinator.Warning);
        AcceptButton = hasGame ? null : gameFolderPrompt.ChooseFolderButton;
    }

    private void DrawCharacter(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || characters.Items[e.Index] is not CharacterEntry item) return;
        var selected = (e.State & DrawItemState.Selected) != 0;
        var scale = e.Bounds.Height / 76f;
        var fontScale = scale / (characters.DeviceDpi / 96f);
        int D(int value) => (int)Math.Round(value * scale);
        using var background = new SolidBrush(selected ? BrowserTheme.Raised : BrowserTheme.Surface);
        e.Graphics.FillRectangle(background, e.Bounds);
        using var accent = new SolidBrush(BrowserTheme.Accent);
        if (selected) e.Graphics.FillRectangle(accent, e.Bounds.X, e.Bounds.Y + D(5), D(3), e.Bounds.Height - D(10));
        using var bold = new Font("Segoe UI", 11 * fontScale, FontStyle.Bold);
        using var small = new Font("Segoe UI", 9 * fontScale);
        var identity = item.Data["character"];
        var classLine = $"Level {identity?["level"]} · {identity?["className"]}";
        var flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        var duplicateName = characterData.OfType<JsonObject>().Count(c => c["character"]?["name"]?.ToString() == item.Name) > 1;
        var displayName = duplicateName ? item.Name + " · " + identity?["realm"] : item.Name;
        if (duplicateName && characterData.OfType<JsonObject>().Count(c =>
            c["character"]?["name"]?.ToString() == item.Name && c["character"]?["realm"]?.ToString() == identity?["realm"]?.ToString()) > 1)
        {
            var account = item.Data["accountId"]?.ToString() ?? "Unknown";
            displayName += " · " + (account.Length > 6 ? account[^6..] : account);
        }
        TextRenderer.DrawText(e.Graphics, displayName, bold, new Rectangle(e.Bounds.X + D(12), e.Bounds.Y + D(13), e.Bounds.Width - D(20), D(25)), selected ? BrowserTheme.Accent : BrowserTheme.Ink, flags);
        TextRenderer.DrawText(e.Graphics, classLine, small, new Rectangle(e.Bounds.X + D(12), e.Bounds.Y + D(41), e.Bounds.Width - D(20), D(22)), BrowserTheme.Muted, flags);
        e.DrawFocusRectangle();
    }

    private void ScaleCharacterRows()
    {
        // WinForms does not scale fixed ListBox row heights; native rows are capped at 255 pixels.
        characters.ItemHeight = Math.Min(255, (int)Math.Round(76 * characters.DeviceDpi / 96f));
        characters.Invalidate();
    }

    private void AdjustBannerHeight()
    {
        var scale = DeviceDpi / 96f;
        var logicalHeight = ClientSize.Height / scale;
        var condensed = logicalHeight < 800;
        characterHeading.Height = (int)Math.Round((condensed ? 108 : 140) * scale);
        var portraitSize = (int)Math.Round((condensed ? 64 : 72) * scale);
        headerPortrait.Size = new Size(portraitSize, portraitSize);
        if (headerPortrait.Parent is TableLayoutPanel header)
        {
            var horizontal = (int)Math.Round(16 * scale);
            var vertical = (int)Math.Round((condensed ? 10 : 12) * scale);
            header.Padding = new Padding(horizontal, vertical, horizontal, vertical);
        }
    }

    private void ShowDataView()
    {
        assistantHome.Visible = false;
        dataView.Visible = true;
        characterHeading.DetailsButton.Visible = false;
        dataBackButton.Focus();
    }

    private void ShowAssistantHome()
    {
        dataView.Visible = false;
        assistantHome.Visible = true;
        characterHeading.DetailsButton.Visible = true;
        characterHeading.DetailsButton.Focus();
    }

    private void ShowSetupView()
    {
        ShowDataView();
        tabs.SelectedIndex = ExportContract.SectionNames.Count;
    }

    private void SetExportStatus(string message)
    {
        exportAttention = message;
        exportStatus.Text = message;
        assistantHome.SetExportStatus(message);
    }

    private void SetBusy(bool value, bool showWaitCursor = true)
    {
        busy = value;
        foreach (var action in actions) action.Enabled = !value;
        releaseControls.StartupCheckBox.Enabled = initialized && !value;
        exitTrayMenu.Enabled = !value; UseWaitCursor = value && showWaitCursor;
    }
    private void ShowError(Exception error) => MessageBox.Show(this, error.Message, "Claudgar", MessageBoxButtons.OK, MessageBoxIcon.Error);
    private void ShowBrowser() { WindowState = restoreWindowState; Show(); Activate(); }
    private void Exit() { exiting = true; Close(); }

    protected override void SetVisibleCore(bool value)
    {
        if (value && !initialVisibilityHandled)
        {
            initialVisibilityHandled = true;
            if (coordinator.StartInTray)
            {
                if (!IsHandleCreated) CreateHandle();
                BeginInvoke(new Action(async () => await StartAsync()));
                value = false;
            }
        }
        base.SetVisibleCore(value);
    }

    private void SetStartupCheck(bool enabled)
    {
        changingStartup = true;
        releaseControls.StartupCheckBox.Checked = enabled;
        changingStartup = false;
    }

    private async Task ChangeStartupAsync()
    {
        if (changingStartup || !initialized || busy) return;
        SetBusy(true, showWaitCursor: false);
        try
        {
            await coordinator.SetLaunchOnStartupAsync(releaseControls.StartupCheckBox.Checked);
            if (IsDisposed) return;
            SetStartupCheck(coordinator.StartupEnabled);
            UpdateSetupStatus();
            if (coordinator.StartupWarning is not null)
                MessageBox.Show(this, coordinator.StartupWarning, "Launch on startup", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        finally { if (!IsDisposed) SetBusy(false); }
    }

    private void InstallUpdate()
    {
        if (!coordinator.AppUpdateReady || busy) return;
        var installNow = new TaskDialogButton("Install now");
        var later = new TaskDialogButton("Later");
        var confirmation = new TaskDialogPage
        {
            Caption = "New version", Heading = $"Claudgar {coordinator.AppUpdateVersion} is ready.",
            Text = "Would you like to install it now? Claudgar will restart.\n\nChoose Later to install it the next time Claudgar starts.",
            AllowCancel = true, DefaultButton = later, Buttons = { installNow, later }
        };
        if (TaskDialog.ShowDialog(this, confirmation) != installNow || busy) return;
        if (coordinator.TryInstallAppUpdate(out var issue)) Exit();
        else MessageBox.Show(this, issue ?? "The update could not be started. The current app is still available.", "Claudgar update", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnUpdateChanged()
    {
        if (IsDisposed || Disposing || !IsHandleCreated) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(OnUpdateChanged)); }
            catch (InvalidOperationException) when (IsDisposed || Disposing || !IsHandleCreated) { }
            return;
        }
        releaseControls.SetUpdate(coordinator.AppUpdateReady, coordinator.AppUpdatePreparing,
            coordinator.AppUpdateFailed ? coordinator.AppUpdateMessage : null);
        updateAction.Visible = coordinator.AppUpdateReady || coordinator.AppUpdatePreparing || coordinator.AppUpdateFailed;
        updateAction.Text = coordinator.AppUpdatePreparing ? "Preparing update…" : coordinator.AppUpdateFailed ? "Update needs attention" : "Update available";
        updateAction.Enabled = !coordinator.AppUpdatePreparing;
        UpdateSetupStatus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            coordinator.UpdateChanged -= OnUpdateChanged;
            refreshTimer.Dispose();
            rosterTooltip.Dispose();
            settingsMenu.Dispose();
            tray?.Dispose();
            base.Dispose(true);
            brandArtwork.Dispose();
            return;
        }
        base.Dispose(false);
    }
}
