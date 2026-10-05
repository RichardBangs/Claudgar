namespace Claudgar.App.Browser;

/// <summary>The single next step shown until a Forever installation is available.</summary>
internal sealed class GameFolderPrompt : UserControl
{
    private readonly Label message = new()
    {
        AutoSize = true, ForeColor = BrowserTheme.Muted, TextAlign = ContentAlignment.MiddleCenter,
        Anchor = AnchorStyles.None, Margin = new Padding(0, 0, 0, 24), MaximumSize = new Size(600, 0)
    };

    public Button ChooseFolderButton { get; }

    public GameFolderPrompt(EventHandler chooseFolder)
    {
        Dock = DockStyle.Fill;
        BackColor = BrowserTheme.Background;
        ChooseFolderButton = BrowserTheme.Button("CHOOSE GAME FOLDER", chooseFolder);
        ChooseFolderButton.Font = new Font("Segoe UI", 12, FontStyle.Bold);
        ChooseFolderButton.Padding = new Padding(28, 14, 28, 14);
        ChooseFolderButton.Anchor = AnchorStyles.None;
        ChooseFolderButton.Margin = Padding.Empty;
        ChooseFolderButton.BackColor = BrowserTheme.Accent;
        ChooseFolderButton.ForeColor = BrowserTheme.Background;
        ChooseFolderButton.FlatAppearance.BorderColor = BrowserTheme.Accent;
        ChooseFolderButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(222, 187, 125);
        ChooseFolderButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(184, 146, 84);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(32) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        layout.Controls.Add(new Label
        {
            Text = "Connect your game", Font = new Font("Cambria", 28, FontStyle.Bold),
            ForeColor = BrowserTheme.Ink, AutoSize = true, Anchor = AnchorStyles.None, Margin = new Padding(0, 0, 0, 12)
        }, 0, 1);
        layout.Controls.Add(message, 0, 2);
        layout.Controls.Add(ChooseFolderButton, 0, 3);
        Controls.Add(layout);
        SetSearching(false);
    }

    public void SetSearching(bool searching, string? explanation = null)
    {
        message.Text = searching
            ? "Looking for your WoW Forever Beta installation…"
            : explanation ?? "Choose your WoW Forever Beta folder to get started.";
    }
}
