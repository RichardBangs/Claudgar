using Claudgar.Core.Setup;

namespace Claudgar.App.Browser;

/// <summary>Shows corrective actions only while a read-only setup check finds an issue.</summary>
internal sealed class SetupIssueBar : TableLayoutPanel
{
    private readonly Label message = new()
    {
        Dock = DockStyle.Fill, AutoSize = true, ForeColor = BrowserTheme.Warning,
        TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 8, 16, 8), UseMnemonic = false
    };
    public Button ChooseFolderButton { get; }
    public Button RepairButton { get; }

    public SetupIssueBar(EventHandler chooseFolder, EventHandler repair)
    {
        Dock = DockStyle.Fill; AutoSize = true; ColumnCount = 3; RowCount = 1;
        Margin = new Padding(20, 0, 20, 12); Padding = new Padding(14, 6, 6, 6);
        BackColor = BrowserTheme.Surface;
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        ChooseFolderButton = BrowserTheme.Button("Choose game folder", chooseFolder);
        RepairButton = BrowserTheme.Button("Repair setup", repair);
        Controls.Add(message, 0, 0); Controls.Add(ChooseFolderButton, 1, 0); Controls.Add(RepairButton, 2, 0);
        Visible = false;
    }

    public void SetHealth(SetupHealth? health, bool showFolderPrompt)
    {
        message.Text = health?.Message ?? "";
        ChooseFolderButton.Visible = health?.NeedsGameFolder == true;
        RepairButton.Visible = health?.NeedsRepair == true;
        Visible = !showFolderPrompt && (health?.NeedsGameFolder == true || health?.NeedsRepair == true);
    }
}
