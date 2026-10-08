namespace Claudgar.App.Browser;

/// <summary>Preferences, connection tools and help, kept out of the normal assistant view.</summary>
internal sealed class AppSettingsMenu : ContextMenuStrip
{
    private readonly ToolStripMenuItem connectionDetails;
    public AppSettingsMenu(ReleaseControls releaseControls, EventHandler chooseFolder, EventHandler repair, EventHandler showConnection)
    {
        BackColor = BrowserTheme.Surface;
        ForeColor = BrowserTheme.Ink;
        Font = new Font("Segoe UI", 10);
        ShowImageMargin = false;
        Padding = new Padding(6);
        Renderer = new ToolStripProfessionalRenderer(new MenuColors());

        Items.Add(new ToolStripControlHost(releaseControls)
        {
            AutoSize = true, Margin = new Padding(10, 8, 10, 8), Padding = Padding.Empty
        });
        Items.Add(new ToolStripSeparator());
        Add("Choose game folder…", chooseFolder);
        Add("Repair setup", repair);
        connectionDetails = Add("Connection details", showConnection);
        Items.Add(new ToolStripSeparator());
        Add("Download ChatGPT", (_, _) => AppGuidance.OpenUrl(OwnerWindow, AppGuidance.ChatGptDownload));
        Add("Download Claude", (_, _) => AppGuidance.OpenUrl(OwnerWindow, AppGuidance.ClaudeDownload));
        Add("Ask from your phone", (_, _) => AppGuidance.ShowPhoneHelp(OwnerWindow));
        Add("About Claudgar", (_, _) => AppGuidance.ShowAbout(OwnerWindow));
    }

    private IWin32Window OwnerWindow => (IWin32Window?)SourceControl?.FindForm() ?? this;

    public void SetHasGame(bool hasGame) => connectionDetails.Enabled = hasGame;

    private ToolStripMenuItem Add(string text, EventHandler clicked)
    {
        var item = new ToolStripMenuItem(text, null, clicked)
        {
            ForeColor = BrowserTheme.Ink, Padding = new Padding(10, 6, 10, 6)
        };
        Items.Add(item);
        return item;
    }

    private sealed class MenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => BrowserTheme.Surface;
        public override Color MenuBorder => BrowserTheme.Border;
        public override Color MenuItemSelected => BrowserTheme.Raised;
        public override Color MenuItemBorder => BrowserTheme.Border;
        public override Color SeparatorDark => BrowserTheme.Border;
        public override Color SeparatorLight => BrowserTheme.Surface;
    }
}
