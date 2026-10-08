namespace Claudgar.App.Browser;

internal static class BrowserTheme
{
    public static readonly Color Background = Color.FromArgb(16, 22, 28);
    public static readonly Color Surface = Color.FromArgb(24, 33, 42);
    public static readonly Color Raised = Color.FromArgb(32, 44, 54);
    public static readonly Color Border = Color.FromArgb(52, 65, 74);
    public static readonly Color Ink = Color.FromArgb(239, 240, 242);
    public static readonly Color Muted = Color.FromArgb(165, 177, 184);
    public static readonly Color Accent = Color.FromArgb(237, 188, 106);
    public static readonly Color Warning = Color.FromArgb(229, 183, 109);
    public static readonly Color Success = Color.FromArgb(130, 191, 162);

    public static Button Button(string text, EventHandler handler)
    {
        var button = new ThemeButton
        {
            Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, 36), Padding = new Padding(12, 6, 12, 6),
            Margin = new Padding(0, 0, 8, 4), FlatStyle = FlatStyle.Flat,
            BackColor = Raised, ForeColor = Ink, UseVisualStyleBackColor = false, UseMnemonic = false, Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(47, 61, 71);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(63, 70, 72);
        button.Click += handler;
        return button;
    }

    public static void StyleInput(TextBox input)
    {
        input.BackColor = Raised;
        input.ForeColor = Ink;
        input.BorderStyle = BorderStyle.FixedSingle;
    }

    public static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AllowUserToOrderColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
        BackgroundColor = Surface, BorderStyle = BorderStyle.None, RowHeadersVisible = false,
        GridColor = Border, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
        ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText,
        AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(27, 37, 46) },
        DefaultCellStyle = new DataGridViewCellStyle
        {
            Padding = new Padding(10, 7, 10, 7), BackColor = Surface, ForeColor = Ink,
            SelectionBackColor = Color.FromArgb(61, 66, 64), SelectionForeColor = Ink
        },
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Raised, ForeColor = Muted, Padding = new Padding(10, 8, 10, 8),
            SelectionBackColor = Raised, SelectionForeColor = Ink
        },
        EnableHeadersVisualStyles = false, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells,
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
    };
}
