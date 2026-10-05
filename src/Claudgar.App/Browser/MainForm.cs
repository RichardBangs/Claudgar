using System.Text.Json.Nodes;
using Claudgar.Core.Data;
using Claudgar.Core.Setup;
using Claudgar.Core.Exports;
using Claudgar.App.Resources;

namespace Claudgar.App.Browser;

internal sealed class MainForm : Form
{
    private readonly ApplicationCoordinator coordinator;
    private readonly NotifyIcon tray;
    private readonly BrandArtwork brandArtwork = new();
    private readonly ExportChangeTracker exportChanges = new();
    private readonly System.Windows.Forms.Timer refreshTimer = new() { Interval = 3000 };
    private readonly Label connection = new() { AutoSize = true, ForeColor = BrowserTheme.Muted, Padding = new Padding(12, 9, 12, 9), BackColor = BrowserTheme.Surface, Anchor = AnchorStyles.Right };
    private readonly Label exportStatus = new() { Dock = DockStyle.Fill, AutoSize = true, ForeColor = BrowserTheme.Muted, Padding = new Padding(20, 12, 20, 12), Margin = Padding.Empty };
    private readonly TextBox characterSearch = new() { Dock = DockStyle.Fill, PlaceholderText = "Find a character or realm", Margin = new Padding(0, 6, 0, 18) };
    private readonly ListBox characters = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false, ItemHeight = 116, DrawMode = DrawMode.OwnerDrawFixed, BackColor = BrowserTheme.Surface, ForeColor = BrowserTheme.Ink };
    private readonly CharacterBanner characterHeading = new() { Dock = DockStyle.Top };
    private readonly ThemeTabs tabs = new() { Dock = DockStyle.Fill };
    private readonly RichTextBox setupDetails = new() { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = BrowserTheme.Surface, ForeColor = BrowserTheme.Ink, Font = new Font("Segoe UI", 10) };
    private readonly Label rosterCount = new() { Text = "YOUR CHARACTERS", Dock = DockStyle.Fill, AutoSize = true, ForeColor = BrowserTheme.Accent, Font = new Font("Segoe UI", 9, FontStyle.Bold), Margin = new Padding(0, 4, 0, 10) };
    private readonly Dictionary<string, SectionBrowser> browsers = [];
    private readonly List<Button> actions = [];
    private readonly SetupIssueBar setupIssueBar;
    private readonly SplitContainer characterBrowser;
    private readonly GameFolderPrompt gameFolderPrompt;
    private readonly ToolStripMenuItem exitTrayMenu = new("Exit Claudgar");
    private JsonArray characterData = [];
    private JsonObject? currentSnapshot;
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
        Width = 1440; Height = 960; MinimumSize = new Size(1120, 780);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10); ForeColor = BrowserTheme.Ink; BackColor = BrowserTheme.Background;
        Icon = brandArtwork.WindowIcon;

        BrowserTheme.StyleInput(characterSearch);
        var title = new Label { Text = "CLAUDGAR", Font = new Font("Cambria", 24, FontStyle.Bold), AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = BrowserTheme.Accent, Margin = new Padding(0, 0, 20, 0) };
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 4, RowCount = 1, Padding = new Padding(20, 16, 20, 16), Margin = Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(new PixelPortrait(brandArtwork.Portrait) { Size = new Size(94, 94), Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 3, 0), AccessibleName = "Claudgar, your archmage companion" }, 0, 0);
        header.Controls.Add(title, 1, 0);
        header.Controls.Add(new Label { Text = "YOUR AZEROTH COMPANION\nWoW Forever Beta", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = BrowserTheme.Muted, Margin = Padding.Empty }, 2, 0);
        header.Controls.Add(connection, 3, 0);
        setupIssueBar = new SetupIssueBar(async (_, _) => await ChooseGameAsync(), async (_, _) => await RepairAsync());
        actions.AddRange([setupIssueBar.ChooseFolderButton, setupIssueBar.RepairButton]);

        var sidebar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(16), BackColor = BrowserTheme.Surface, Margin = Padding.Empty };
        sidebar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidebar.Controls.Add(rosterCount, 0, 0);
        sidebar.Controls.Add(characterSearch, 0, 1);
        sidebar.Controls.Add(characters, 0, 2);
        sidebar.Controls.Add(new Label { Text = "SAVED IN AZEROTH\nReload or log out in-game to save your latest progress.", AutoSize = true, Dock = DockStyle.Fill, ForeColor = BrowserTheme.Muted, Padding = new Padding(0, 14, 0, 0), Margin = Padding.Empty }, 0, 3);
        var content = new Panel { Dock = DockStyle.Fill, BackColor = BrowserTheme.Background, Padding = new Padding(16, 0, 0, 0) };
        content.Controls.Add(tabs); content.Controls.Add(characterHeading);
        foreach (var name in ExportContract.SectionNames)
        {
            var browser = new SectionBrowser(name);
            browsers[name] = browser;
            tabs.AddPage(JsonPresentation.Humanize(name), browser);
        }
        var setupPage = new Panel { BackColor = BrowserTheme.Surface, Padding = new Padding(22) };
        setupPage.Controls.Add(setupDetails); tabs.AddPage("Setup & status", setupPage);
        characterBrowser = new SplitContainer { Size = new Size(1360, 700), Dock = DockStyle.Fill, SplitterDistance = 260, Panel1MinSize = 230, Panel2MinSize = 710, FixedPanel = FixedPanel.Panel1, SplitterWidth = 1, BackColor = BrowserTheme.Border, Margin = new Padding(20, 0, 20, 0) };
        characterBrowser.Panel1.Controls.Add(sidebar); characterBrowser.Panel2.Controls.Add(content);
        gameFolderPrompt = new GameFolderPrompt(async (_, _) => await ChooseGameAsync());
        actions.Add(gameFolderPrompt.ChooseFolderButton);
        var body = new Panel { Dock = DockStyle.Fill, Margin = new Padding(20, 0, 20, 0) };
        body.Controls.Add(characterBrowser);
        body.Controls.Add(gameFolderPrompt);
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.Controls.Add(header, 0, 0); shell.Controls.Add(setupIssueBar, 0, 1); shell.Controls.Add(body, 0, 2); shell.Controls.Add(exportStatus, 0, 3);
        Controls.Add(shell);
        UpdateGameFolderView();

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Open Claudgar", null, (_, _) => ShowBrowser());
        exitTrayMenu.Click += (_, _) => Exit();
        trayMenu.Items.Add(exitTrayMenu);
        tray = new NotifyIcon { Icon = brandArtwork.TrayIcon, Text = "Claudgar · Saved Forever data", Visible = true, ContextMenuStrip = trayMenu };
        tray.DoubleClick += (_, _) => ShowBrowser();
        characters.DrawItem += DrawCharacter;
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
        refreshTimer.Tick += async (_, _) => await CheckForUpdatesAsync();
        Activated += async (_, _) => await CheckForUpdatesAsync(checkSetup: true);
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
    }

    private async Task StartAsync()
    {
        gameFolderPrompt.SetSearching(true);
        SetBusy(true);
        try { await coordinator.InitializeAsync(); }
        catch (Exception error) { ShowError(error); }
        finally { SetBusy(false); }
        initialized = true;
        await CheckForUpdatesAsync(checkSetup: true);
        if (!IsDisposed) refreshTimer.Start();
    }

    private async Task CheckForUpdatesAsync(bool checkSetup = false)
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
            if (!IsDisposed) exportStatus.Text = "Waiting to read saved data: " + error.Message;
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
            var state = result["exportState"]?.ToString() ?? "no_export";
            exportStatus.Text = $"{characterData.Count} saved character(s) · Updates automatically · Export: {state.Replace('_', ' ')}";
            var selectedReadSucceeded = await LoadSelectedAsync();
            if (IsDisposed) return false;
            UpdateSetupStatus();
            // A save may be temporarily locked or incomplete. Retry until its valid snapshot is available.
            return selectedReadSucceeded && result["issues"] is not JsonArray { Count: > 0 };
        }
        catch (Exception error) { if (!IsDisposed) exportStatus.Text = "Could not read saved data: " + error.Message; return false; }
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
        rosterCount.Text = $"YOUR CHARACTERS  ·  {characters.Items.Count}";
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
            foreach (var browser in browsers.Values) { browser.SetCharacter(null); browser.SetSection(null, false); }
            return true;
        }
        var id = entry.Id;
        var snapshot = await Task.Run(() => coordinator.Exports.GetCharacter(id));
        if (IsDisposed || requestVersion != selectionLoadVersion || selectedId != id ||
            characters.SelectedItem is not CharacterEntry selected || selected.Id != id) return false;
        currentSnapshot = snapshot;
        characterHeading.SetCharacter(snapshot);
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
        exportChanges.Invalidate();
        await CheckForUpdatesAsync(checkSetup: true);
    }

    private async Task RepairAsync()
    {
        if (busy) return;
        SetBusy(true);
        try
        {
            await coordinator.InitializeAsync();
        }
        catch (Exception error) { ShowError(error); }
        finally { SetBusy(false); }
        exportChanges.Invalidate();
        await CheckForUpdatesAsync(checkSetup: true);
    }

    private void UpdateSetupStatus()
    {
        UpdateGameFolderView();
        connection.Text = coordinator.Server.IsRunning ? "●  Local service ready" : "●  Connection unavailable";
        connection.ForeColor = coordinator.Server.IsRunning ? BrowserTheme.Success : BrowserTheme.Warning;
        var lines = new List<string>
        {
            "SETUP & CONNECTION", "",
            $"Local service: {(coordinator.Server.IsRunning ? "ready" : "not running")}",
            $"Codex address: http://127.0.0.1:{coordinator.Settings.Port}/mcp",
            $"Latest client request: {coordinator.Server.LastClientRequest?.ToLocalTime().ToString("g") ?? "none yet"}",
            "A client request indicates contact with the service; it does not prove that Codex completed a tool call.", ""
        };
        if (coordinator.Warning is not null) lines.Add(coordinator.Warning);
        if (coordinator.ServerError is not null) lines.Add(coordinator.ServerError);
        if (coordinator.Installations.Count == 0)
            lines.Add("No Forever Beta installation was selected. Choose your game folder to install the addon.");
        foreach (var game in coordinator.Installations) lines.Add($"Forever Beta {game.Version}\n{game.GameDirectory}");
        if (coordinator.SetupReport is { } report)
        {
            lines.Add(""); lines.Add("Addon setup:");
            foreach (var result in report.Addons) lines.Add(result.Message);
            lines.Add(""); lines.Add("Codex connection: " + report.Codex.Message);
            lines.Add("Codex skill: " + report.Skill.Message);
        }
        lines.AddRange(["", "FIRST USE", "1. Keep Claudgar running and start Forever Beta. Enable Claudgar in the AddOns menu.",
            "2. Enter the world, then /reload or log out so SavedVariables are written.",
            "3. Your saved data appears automatically. Select your character and double-click entries for details.",
            "4. Restart Codex after setup if it cannot see Claudgar. Ask it about your Forever character.", "",
            "DATA", "All five sections are shown, including coverage, observation time, and unavailable fields.",
            "Only saved exports are read. A running game does not continuously write them.",
            "Character data requested by Codex is processed by the model provider. Local account and installation paths are excluded from tool results.", "",
            "Minimise to keep the app in the tray. Open it from the tray, or use Exit Claudgar to stop it."]);
        setupDetails.Text = string.Join(Environment.NewLine, lines);
    }

    private void UpdateGameFolderView()
    {
        var hasGame = coordinator.Installations.Count > 0;
        setupIssueBar.SetHealth(setupHealth, showFolderPrompt: !hasGame);
        characterBrowser.Visible = hasGame;
        connection.Visible = hasGame;
        exportStatus.Visible = hasGame;
        gameFolderPrompt.Visible = !hasGame;
        gameFolderPrompt.SetSearching(false, setupHealth?.NeedsGameFolder == true ? setupHealth.Message : coordinator.Warning);
        AcceptButton = hasGame ? null : gameFolderPrompt.ChooseFolderButton;
    }

    private void DrawCharacter(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || characters.Items[e.Index] is not CharacterEntry item) return;
        var selected = (e.State & DrawItemState.Selected) != 0;
        var scale = e.Bounds.Height / 116f;
        var fontScale = scale / (characters.DeviceDpi / 96f);
        int D(int value) => (int)Math.Round(value * scale);
        using var background = new SolidBrush(selected ? BrowserTheme.Raised : BrowserTheme.Surface);
        e.Graphics.FillRectangle(background, e.Bounds);
        using var accent = new SolidBrush(BrowserTheme.Accent);
        if (selected) e.Graphics.FillRectangle(accent, e.Bounds.X, e.Bounds.Y + D(5), D(3), e.Bounds.Height - D(10));
        using var bold = new Font("Cambria", 14 * fontScale, FontStyle.Bold);
        using var small = new Font("Segoe UI", 9 * fontScale);
        var identity = item.Data["character"];
        var classLine = $"Level {identity?["level"]} · {identity?["className"]}";
        var flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        TextRenderer.DrawText(e.Graphics, item.Name, bold, new Rectangle(e.Bounds.X + D(14), e.Bounds.Y + D(10), e.Bounds.Width - D(24), D(27)), selected ? BrowserTheme.Accent : BrowserTheme.Ink, flags);
        TextRenderer.DrawText(e.Graphics, classLine, small, new Rectangle(e.Bounds.X + D(14), e.Bounds.Y + D(41), e.Bounds.Width - D(24), D(22)), BrowserTheme.Ink, flags);
        TextRenderer.DrawText(e.Graphics, identity?["realm"]?.ToString(), small, new Rectangle(e.Bounds.X + D(14), e.Bounds.Y + D(65), e.Bounds.Width - D(24), D(22)), BrowserTheme.Muted, flags);
        var account = item.Data["accountId"]?.ToString() ?? "Unknown";
        if (account.Length > 12) account = account[^6..];
        TextRenderer.DrawText(e.Graphics, "Account " + account, small, new Rectangle(e.Bounds.X + D(14), e.Bounds.Y + D(87), e.Bounds.Width - D(24), D(22)), BrowserTheme.Muted, flags);
        e.DrawFocusRectangle();
    }

    private void ScaleCharacterRows()
    {
        // WinForms does not scale fixed ListBox row heights; native rows are capped at 255 pixels.
        characters.ItemHeight = Math.Min(255, (int)Math.Round(116 * characters.DeviceDpi / 96f));
        characters.Invalidate();
    }

    private void AdjustBannerHeight()
    {
        var scale = DeviceDpi / 96f;
        characterHeading.Height = (int)Math.Round((ClientSize.Height / scale < 850 ? 174 : 246) * scale);
    }

    private void SetBusy(bool value, bool showWaitCursor = true) { busy = value; foreach (var action in actions) action.Enabled = !value; exitTrayMenu.Enabled = !value; UseWaitCursor = value && showWaitCursor; }
    private void ShowError(Exception error) => MessageBox.Show(this, error.Message, "Claudgar", MessageBoxButtons.OK, MessageBoxIcon.Error);
    private void ShowBrowser() { WindowState = restoreWindowState; Show(); Activate(); }
    private void Exit() { exiting = true; Close(); }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            refreshTimer.Dispose();
            tray?.Dispose();
            base.Dispose(true);
            brandArtwork.Dispose();
            return;
        }
        base.Dispose(false);
    }
}
