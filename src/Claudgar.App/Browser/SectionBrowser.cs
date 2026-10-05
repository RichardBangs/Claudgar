using System.Text.Json.Nodes;

namespace Claudgar.App.Browser;

/// <summary>Browse every field without relying on a particular collector's UI shape.</summary>
internal sealed class SectionBrowser : UserControl
{
    private sealed record DataLocation(JsonNode? Value, string Path, int Start = 0, int? Count = null, bool QuestIds = false);
    private readonly string? sectionName;
    private readonly QuestNames questNames = new();
    private readonly ISectionView? customView;
    private readonly ThemeTabs? viewModes;
    private readonly Label status = new() { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(14, 12, 14, 12), Margin = Padding.Empty, ForeColor = BrowserTheme.Muted, BackColor = BrowserTheme.Raised };
    private readonly TreeView tree = new()
    {
        Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, HideSelection = false, ShowNodeToolTips = true,
        BackColor = BrowserTheme.Surface, ForeColor = BrowserTheme.Ink, LineColor = BrowserTheme.Border,
        FullRowSelect = true, ShowLines = false, Indent = 18, ItemHeight = 28, DrawMode = TreeViewDrawMode.OwnerDrawText
    };
    private readonly DataGridView grid = BrowserTheme.Grid();
    private readonly TextBox search = new()
    {
        Width = 160, PlaceholderText = "Search this collection", BorderStyle = BorderStyle.FixedSingle,
        BackColor = BrowserTheme.Background, ForeColor = BrowserTheme.Ink, Margin = new Padding(6, 10, 10, 6)
    };
    private readonly Label breadcrumb = new()
    {
        AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(12, 5, 12, 12), Margin = Padding.Empty,
        ForeColor = BrowserTheme.Muted, BackColor = BrowserTheme.Surface
    };
    private readonly RichTextBox raw = new() { Dock = DockStyle.Fill, ReadOnly = true, Font = new Font("Consolas", 10), BackColor = BrowserTheme.Surface, ForeColor = BrowserTheme.Ink, BorderStyle = BorderStyle.None, WordWrap = false };
    private readonly RichTextBox collectionDetails = new() { Dock = DockStyle.Fill, ReadOnly = true, BackColor = BrowserTheme.Surface, ForeColor = BrowserTheme.Ink, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10) };
    private readonly Stack<DataLocation> history = new();
    private readonly Button back;
    private readonly Button previousPage;
    private readonly Button nextPage;
    private readonly SplitContainer dataBrowser;
    private DataLocation? current;
    private string? fingerprint;
    private int page;
    private const int PageSize = 250;

    public SectionBrowser(string? sectionName = null)
    {
        this.sectionName = sectionName;
        Dock = DockStyle.Fill; AutoScaleMode = AutoScaleMode.None;
        BackColor = BrowserTheme.Background; ForeColor = BrowserTheme.Ink;
        back = BrowserTheme.Button("Back", (_, _) => { if (history.TryPop(out var location)) ShowLocation(location, false); });
        previousPage = BrowserTheme.Button("Previous", (_, _) => { if (page > 0) { page--; PopulateGrid(); } });
        nextPage = BrowserTheme.Button("Next", (_, _) => { page++; PopulateGrid(); });
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(6), Margin = Padding.Empty, WrapContents = true, BackColor = BrowserTheme.Surface
        };
        toolbar.Controls.AddRange([back, search, previousPage, nextPage, BrowserTheme.Button("Copy JSON", (_, _) => CopyJson())]);
        var views = new ThemeTabs { Dock = DockStyle.Fill, Margin = Padding.Empty };
        views.AddPage("Browse", grid);
        views.AddPage("JSON details", PaddedContent(raw));
        views.AddPage("Collection details", PaddedContent(collectionDetails));
        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty,
            BackColor = BrowserTheme.Surface
        };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.Controls.Add(toolbar, 0, 0); right.Controls.Add(breadcrumb, 0, 1); right.Controls.Add(views, 0, 2);
        var navigation = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = BrowserTheme.Surface };
        navigation.Controls.Add(tree);
        var split = new SplitContainer
        {
            Size = new Size(950, 550), Dock = DockStyle.Fill, SplitterDistance = 235, Panel1MinSize = 150,
            Panel2MinSize = 260, SplitterWidth = 5, BackColor = BrowserTheme.Border, Margin = Padding.Empty
        };
        dataBrowser = split;
        split.Panel1.Controls.Add(navigation); split.Panel2.Controls.Add(right);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(status, 0, 0);
        Control? customControl = sectionName switch
        {
            "inventory" => new InventoryView(),
            "talents" => new TalentsView(),
            "equipment" => new EquipmentView(),
            _ => null
        };
        if (customControl is ISectionView savedView)
        {
            customView = savedView;
            if (sectionName is "talents" or "equipment") layout.Controls.Add(customControl, 0, 1);
            else
            {
                viewModes = new ThemeTabs { Dock = DockStyle.Fill, Margin = Padding.Empty };
                viewModes.AddPage("Bags", customControl);
                viewModes.AddPage("JSON tree", split);
                layout.Controls.Add(viewModes, 0, 1);
            }
        }
        else layout.Controls.Add(split, 0, 1);
        Controls.Add(layout);
        tree.DrawNode += DrawDataNode;
        tree.HandleCreated += (_, _) => ScaleTreeRows();
        tree.DpiChangedAfterParent += (_, _) => ScaleTreeRows();
        tree.BeforeExpand += (_, e) => { if (e.Node is not null) Expand(e.Node); };
        tree.AfterSelect += (_, e) => { if (e.Node?.Tag is DataLocation location) { history.Clear(); ShowLocation(location, false); } };
        search.TextChanged += (_, _) => { page = 0; PopulateGrid(); };
        grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].Tag is DataLocation location && location.Value is JsonObject or JsonArray)
                ShowLocation(location, true);
        };
        views.SelectedIndexChanged += (_, _) => { if (views.SelectedIndex == 1) raw.Text = JsonPresentation.Pretty(current?.Value); };
        SetSection(null, false);
    }

    private static Panel PaddedContent(Control content)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), BackColor = BrowserTheme.Surface };
        panel.Controls.Add(content);
        return panel;
    }

    private void ScaleTreeRows()
    {
        tree.ItemHeight = Math.Max(tree.Font.Height + 6, (int)Math.Round(28 * tree.DeviceDpi / 96f));
        tree.Indent = (int)Math.Round(18 * tree.DeviceDpi / 96f);
    }

    private void DrawDataNode(object? sender, DrawTreeNodeEventArgs e)
    {
        if (e.Node is null) return;
        var selected = (e.State & TreeNodeStates.Selected) != 0;
        var bounds = new Rectangle(e.Bounds.X, e.Bounds.Y, Math.Max(0, tree.ClientSize.Width - e.Bounds.X), e.Bounds.Height);
        using var background = new SolidBrush(selected ? BrowserTheme.Raised : BrowserTheme.Surface);
        e.Graphics.FillRectangle(background, bounds);
        TextRenderer.DrawText(e.Graphics, e.Node.Text, tree.Font, bounds, selected ? BrowserTheme.Accent : BrowserTheme.Ink,
            TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        if (selected && tree.Focused) ControlPaint.DrawFocusRectangle(e.Graphics, bounds, BrowserTheme.Accent, BrowserTheme.Raised);
    }

    public void SetCharacter(JsonObject? section)
    {
        if (customView is EquipmentView equipment) equipment.SetCharacter(section);
    }

    public void SetSection(JsonObject? section, bool stale)
    {
        var hash = section is null ? "none" : string.Join("|", new[] { "data", "status", "observedAt", "warnings", "error" }.Select(key => section[key]?.ToJsonString()));
        UpdateStatus(section, stale);
        collectionDetails.Text = section is null ? "No saved export is available yet." :
            "Collection information\n\n" + JsonPresentation.Pretty(new JsonObject
            {
                ["coverage"] = section["status"]?.DeepClone(), ["observedAt"] = section["observedAt"]?.DeepClone(),
                ["warnings"] = section["warnings"]?.DeepClone(), ["error"] = section["error"]?.DeepClone(),
                ["freshness"] = section["freshness"]?.DeepClone()
            });
        if (hash == fingerprint) return;
        fingerprint = hash;
        customView?.SetSection(section);
        if (sectionName is "talents" or "equipment") return;
        questNames.SetData(sectionName == "quests" ? section?["data"] as JsonObject : null);
        tree.BeginUpdate(); tree.Nodes.Clear(); history.Clear();
        if (section?["data"] is JsonNode data)
        {
            var root = MakeNode("Saved data", new(data, "Saved data"));
            tree.Nodes.Add(root); Expand(root); root.Expand(); tree.SelectedNode = root;
        }
        else { current = null; PopulateGrid(); raw.Clear(); breadcrumb.Text = "No saved data"; back.Enabled = false; }
        tree.EndUpdate();
    }

    private void UpdateStatus(JsonObject? section, bool stale)
    {
        if (section is null)
        {
            status.Visible = true;
            status.Text = "No export yet. Enter the game with Claudgar enabled, then reload or log out.";
            status.ForeColor = BrowserTheme.Muted;
            return;
        }
        var coverage = section["status"]?.ToString() ?? "unavailable";
        var observed = section["observedAt"]?.GetValue<long>() ?? 0;
        var date = observed > 0 ? DateTimeOffset.FromUnixTimeSeconds(observed).ToLocalTime().ToString("g") : "unknown";
        var warnings = section["warnings"] is JsonArray array ? string.Join(" · ", array.Select(n => n?.ToString())) : "";
        var explanation = section["error"]?.ToString() ?? warnings;
        status.Visible = sectionName is not ("talents" or "equipment") || coverage != "complete" || stale || !string.IsNullOrWhiteSpace(explanation);
        status.Text = $"Coverage: {coverage}  ·  Collected: {date}" + (stale ? "  ·  Older cached export" : "") +
            (string.IsNullOrWhiteSpace(explanation) ? "" : "\n" + explanation);
        status.ForeColor = coverage == "complete" && !stale ? BrowserTheme.Muted : BrowserTheme.Warning;
    }

    private static TreeNode MakeNode(string name, DataLocation location)
    {
        var node = new TreeNode(name) { Tag = location, ToolTipText = location.Path };
        if (location.Value is JsonObject { Count: > 0 } or JsonArray { Count: > 0 }) node.Nodes.Add(new TreeNode("Loading…"));
        return node;
    }

    private void Expand(TreeNode node)
    {
        if (node.Tag is not DataLocation location || node.Nodes.Count != 1 || node.Nodes[0].Tag is not null) return;
        node.Nodes.Clear();
        if (location.Value is JsonObject obj)
            foreach (var (key, value) in obj)
                node.Nodes.Add(MakeNode(JsonPresentation.Humanize(key) + (value is JsonValue ? ": " + JsonPresentation.Summary(value) : ""), new(value, location.Path + " / " + JsonPresentation.Humanize(key), QuestIds: sectionName == "quests" && key == "completed")));
        else if (location.Value is JsonArray array)
        {
            var count = location.Count ?? array.Count;
            if (count > 250)
                for (var start = location.Start; start < location.Start + count; start += 250)
                {
                    var length = Math.Min(250, location.Start + count - start);
                    node.Nodes.Add(MakeNode($"Entries {start + 1:N0}–{start + length:N0}", new(array, location.Path, start, length, location.QuestIds)));
                }
            else
                for (var i = location.Start; i < location.Start + count; i++)
                {
                    var name = location.QuestIds ? questNames.Label(array[i]) : JsonPresentation.RecordName(array[i], i);
                    node.Nodes.Add(MakeNode(name, new(array[i], location.Path + " / " + name)));
                }
        }
    }

    private void ShowLocation(DataLocation location, bool remember)
    {
        if (remember && current is not null) history.Push(current);
        current = location; page = 0; search.Clear(); breadcrumb.Text = location.Path;
        back.Enabled = history.Count > 0; PopulateGrid();
        raw.Clear();
        if (raw.Visible) raw.Text = JsonPresentation.Pretty(location.Value);
    }

    private void PopulateGrid()
    {
        grid.SuspendLayout(); grid.Rows.Clear(); grid.Columns.Clear();
        previousPage.Enabled = false; nextPage.Enabled = false;
        if (current is null) { grid.ResumeLayout(); return; }
        var filter = search.Text.Trim();
        bool Matches(JsonNode? node) => filter.Length == 0 || (node?.ToJsonString().Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
        if (current.Value is JsonArray array)
        {
            var entries = Enumerable.Range(current.Start, current.Count ?? array.Count).Select(i => (Index: i, Value: array[i]))
                .Where(x => Matches(x.Value) || current.QuestIds && questNames.Label(x.Value).Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();
            page = Math.Clamp(page, 0, Math.Max(0, (entries.Length - 1) / PageSize));
            previousPage.Enabled = page > 0; nextPage.Enabled = (page + 1) * PageSize < entries.Length;
            var visibleEntries = entries.Skip(page * PageSize).Take(PageSize).ToArray();
            breadcrumb.Text = current.Path + $" · {entries.Length:N0} matches · Page {page + 1}";
            if (current.QuestIds)
            {
                grid.Columns.Add("questId", "Quest ID");
                grid.Columns.Add("title", "Quest name");
                grid.Columns["title"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                foreach (var entry in visibleEntries)
                {
                    var row = grid.Rows[grid.Rows.Add(entry.Value?.ToString() ?? "", questNames.Title(entry.Value) ?? "Name not saved yet")];
                    row.Tag = new DataLocation(entry.Value, current.Path + " / " + questNames.Label(entry.Value));
                }
                grid.ResumeLayout();
                return;
            }
            var columns = visibleEntries.SelectMany(e => e.Value is JsonObject o ? o.Select(p => p.Key) : ["value"]).Distinct().ToArray();
            if (columns.Length == 0) columns = ["value"];
            grid.Columns.Add("index", "#");
            foreach (var column in columns) grid.Columns.Add(column, JsonPresentation.Humanize(column));
            foreach (var entry in visibleEntries)
            {
                var cells = new List<object> { entry.Index + 1 };
                cells.AddRange(columns.Select(key => (object)JsonPresentation.Summary(entry.Value is JsonObject obj ? obj[key] : entry.Value)));
                var row = grid.Rows[grid.Rows.Add(cells.ToArray())];
                row.Tag = new DataLocation(entry.Value, current.Path + " / " + JsonPresentation.RecordName(entry.Value, entry.Index));
            }
        }
        else
        {
            grid.Columns.Add("field", "Field"); grid.Columns.Add("value", "Value");
            grid.Columns["value"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns["value"]!.MinimumWidth = 140;
            if (current.Value is JsonObject obj)
                foreach (var (key, value) in obj)
                {
                    if (filter.Length > 0 && !key.Contains(filter, StringComparison.OrdinalIgnoreCase) && !Matches(value)) continue;
                    var row = grid.Rows[grid.Rows.Add(JsonPresentation.Humanize(key), JsonPresentation.Summary(value))];
                    row.Tag = new DataLocation(value, current.Path + " / " + JsonPresentation.Humanize(key), QuestIds: sectionName == "quests" && key == "completed");
                }
            else grid.Rows.Add("Value", JsonPresentation.Summary(current.Value));
        }
        grid.ResumeLayout();
    }

    private void CopyJson()
    {
        if (current?.Value is null) return;
        try { Clipboard.SetText(JsonPresentation.Pretty(current.Value)); }
        catch (System.Runtime.InteropServices.ExternalException) { MessageBox.Show("The clipboard is busy. Try again.", "Claudgar"); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && dataBrowser.Parent is null) dataBrowser.Dispose();
        base.Dispose(disposing);
    }
}
