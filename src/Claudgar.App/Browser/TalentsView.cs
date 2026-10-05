using System.Text.Json.Nodes;

namespace Claudgar.App.Browser;

/// <summary>Shows all saved talents directly, with a complete list when the saved layout is unusable.</summary>
internal sealed class TalentsView : UserControl, ISectionView
{
    private readonly TalentTreeCanvas canvas = new();
    private readonly DataGridView list = BrowserTheme.Grid();
    private readonly Label fallbackMessage = new()
    {
        AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(12),
        ForeColor = BrowserTheme.Muted, BackColor = BrowserTheme.Surface, UseMnemonic = false
    };
    private string? fingerprint;
    private bool nativeLayout;

    public TalentsView()
    {
        Dock = DockStyle.Fill; AutoScaleMode = AutoScaleMode.None;
        BackColor = BrowserTheme.Background; ForeColor = BrowserTheme.Ink;
        var content = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = BrowserTheme.Background };
        content.Controls.Add(list); content.Controls.Add(canvas);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(fallbackMessage, 0, 0); layout.Controls.Add(content, 0, 1);
        Controls.Add(layout);
        list.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        list.Columns.Add("name", "Talent"); list.Columns.Add("rank", "Active rank");
        list.Columns.Add("state", "State"); list.Columns.Add("visibility", "In tree");
        list.Columns["name"]!.FillWeight = 55; list.Columns["rank"]!.FillWeight = 13;
        list.Columns["state"]!.FillWeight = 22; list.Columns["visibility"]!.FillWeight = 10;
        SetSection(null);
    }

    public void SetSection(JsonObject? section)
    {
        var data = section?["data"] as JsonObject;
        var hash = data?.ToJsonString() ?? "none";
        if (hash == fingerprint) return;
        fingerprint = hash;
        canvas.HasStagedChanges = data?["hasStagedChanges"] is JsonValue staged && staged.TryGetValue<bool>(out var hasChanges) && hasChanges;
        var trees = TalentPresentation.ReadTrees(data);
        list.Rows.Clear();
        foreach (var node in trees.SelectMany(tree => tree.Nodes))
        {
            var row = list.Rows[list.Rows.Add(node.Name, node.Rank, node.State,
                node.Visible == false ? "Hidden" : node.Visible == true ? "Visible" : "Unknown")];
            row.Tag = node;
            if (node.Active) row.DefaultCellStyle.ForeColor = BrowserTheme.Accent;
        }
        list.ClearSelection();
        nativeLayout = canvas.SetPanels(TalentPresentation.OverviewPanels(trees), out var explanation);
        fallbackMessage.Text = trees.Count == 0 ? "No saved talents are available yet." : explanation;
        fallbackMessage.Visible = !nativeLayout;
        canvas.Visible = nativeLayout; list.Visible = !nativeLayout;
        if (nativeLayout) canvas.BringToFront(); else list.BringToFront();
    }
}
