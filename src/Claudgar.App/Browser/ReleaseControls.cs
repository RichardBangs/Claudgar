namespace Claudgar.App.Browser;

/// <summary>Startup preference and staged update controls inside the settings menu.</summary>
internal sealed class ReleaseControls : UserControl
{
    public CheckBox StartupCheckBox { get; } = new() { Text = "Launch on startup", AutoSize = true,
        ForeColor = BrowserTheme.Ink, Margin = new Padding(0, 0, 0, 6) };
    private readonly Button updateButton;
    private readonly Label progress = new() { AutoSize = true, Visible = false, ForeColor = BrowserTheme.Muted, Margin = Padding.Empty };
    private readonly LinkLabel startupWarning = new() { AutoSize = true, Visible = false, LinkColor = BrowserTheme.Warning,
        ActiveLinkColor = BrowserTheme.Ink,
        MaximumSize = new Size(235, 0), Margin = new Padding(0, 0, 0, 4) };
    private string? startupIssue;
    private readonly LinkLabel updateWarning = new() { AutoSize = true, Visible = false, LinkColor = BrowserTheme.Warning,
        ActiveLinkColor = BrowserTheme.Ink, MaximumSize = new Size(235, 0), Margin = Padding.Empty };
    private string? updateIssue;

    public ReleaseControls(EventHandler startupChanged, EventHandler installUpdate)
    {
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Anchor = AnchorStyles.Right; Margin = Padding.Empty; BackColor = BrowserTheme.Surface;
        updateButton = BrowserTheme.Button("Install update", installUpdate); updateButton.Visible = false;
        updateButton.Margin = Padding.Empty; updateButton.ForeColor = BrowserTheme.Accent;
        var layout = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, Margin = Padding.Empty };
        layout.Controls.Add(StartupCheckBox); layout.Controls.Add(startupWarning); layout.Controls.Add(updateButton); layout.Controls.Add(progress);
        layout.Controls.Add(updateWarning);
        Controls.Add(layout); StartupCheckBox.CheckedChanged += startupChanged;
        startupWarning.LinkClicked += (_, _) => MessageBox.Show(this, startupIssue, "Launch on startup",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        updateWarning.LinkClicked += (_, _) => MessageBox.Show(this, updateIssue, "Claudgar update",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public void SetUpdate(bool ready, bool preparing, string? issue = null)
    {
        updateButton.Visible = ready;
        progress.Text = preparing ? "Preparing update…" : "";
        progress.Visible = preparing;
        updateIssue = issue;
        updateWarning.Text = issue is null ? "" : "Update needs attention";
        updateWarning.Visible = issue is not null;
        updateWarning.AccessibleDescription = issue;
    }

    public void SetStartupWarning(string? warning)
    {
        startupIssue = warning;
        startupWarning.Text = warning is null ? "" : "Startup setting needs attention";
        startupWarning.Visible = warning is not null;
        startupWarning.AccessibleDescription = warning;
    }
}
