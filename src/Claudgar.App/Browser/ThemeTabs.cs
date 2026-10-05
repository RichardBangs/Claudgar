namespace Claudgar.App.Browser;

/// <summary>Keyboard-accessible tab buttons and panels with consistent dark surfaces.</summary>
internal sealed class ThemeTabs : UserControl
{
    private readonly FlowLayoutPanel strip = new()
    {
        Dock = DockStyle.Fill, AutoSize = true, WrapContents = true,
        Margin = Padding.Empty, Padding = new Padding(0, 6, 0, 6), BackColor = BrowserTheme.Background
    };
    private readonly Panel body = new() { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = BrowserTheme.Surface };
    private readonly List<Control> pages = [];
    private readonly List<Button> buttons = [];
    private int selectedIndex = -1;

    public int Count => pages.Count;
    public event EventHandler? SelectedIndexChanged;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (value == selectedIndex || value < 0 || value >= pages.Count) return;
            body.SuspendLayout();
            if (selectedIndex >= 0) pages[selectedIndex].Visible = false;
            selectedIndex = value;
            pages[value].Visible = true;
            pages[value].BringToFront();
            for (var i = 0; i < buttons.Count; i++)
            {
                buttons[i].ForeColor = i == value ? BrowserTheme.Accent : BrowserTheme.Muted;
                buttons[i].BackColor = i == value ? BrowserTheme.Raised : BrowserTheme.Background;
                buttons[i].FlatAppearance.BorderColor = i == value ? BrowserTheme.Accent : BrowserTheme.Background;
            }
            body.ResumeLayout();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public ThemeTabs()
    {
        AutoScaleMode = AutoScaleMode.None;
        BackColor = BrowserTheme.Background;
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(strip, 0, 0);
        layout.Controls.Add(body, 0, 1);
        Controls.Add(layout);
    }

    public void AddPage(string title, Control content)
    {
        var index = pages.Count;
        var button = BrowserTheme.Button(title, (_, _) => SelectedIndex = index);
        button.Padding = new Padding(14, 7, 14, 7);
        button.Margin = new Padding(0, 0, 6, 0);
        button.AccessibleDescription = "Show " + title;
        buttons.Add(button);
        strip.Controls.Add(button);
        pages.Add(content);
        content.Dock = DockStyle.Fill;
        content.Visible = false;
        body.Controls.Add(content);
        if (selectedIndex < 0) SelectedIndex = 0;
        else
        {
            button.BackColor = BrowserTheme.Background;
            button.ForeColor = BrowserTheme.Muted;
            button.FlatAppearance.BorderColor = BrowserTheme.Background;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (Count > 0 && keyData is (Keys.Control | Keys.Tab) or (Keys.Control | Keys.Shift | Keys.Tab))
        {
            SelectedIndex = (SelectedIndex + (keyData.HasFlag(Keys.Shift) ? Count - 1 : 1)) % Count;
            buttons[SelectedIndex].Focus();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
