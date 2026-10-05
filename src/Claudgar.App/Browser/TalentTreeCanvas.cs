using System.Drawing.Drawing2D;
using static Claudgar.App.Browser.TalentTreeLayout;

namespace Claudgar.App.Browser;

/// <summary>Fits the saved talent tree to the whole viewport and handles read-only selection.</summary>
internal sealed class TalentTreeCanvas : Control
{
    private readonly ToolTip tooltip = new() { InitialDelay = 250, ReshowDelay = 100, AutoPopDelay = 12000 };
    private IReadOnlyList<PlacedPanel> panels = [];
    private IReadOnlyList<PlacedNode> nodes = [];
    private TalentPresentation.Node? selected;
    private TalentPresentation.Node? hovered;
    private SizeF treeSize;
    private float fittedScale = 1;
    [System.ComponentModel.DefaultValue(false)]
    public bool HasStagedChanges { get; set; }
    public event Action<TalentPresentation.Node>? NodeSelected;

    public TalentTreeCanvas()
    {
        Dock = DockStyle.Fill; BackColor = BrowserTheme.Background; ForeColor = BrowserTheme.Ink;
        TabStop = true;
        AccessibleName = "Saved talent trees";
        AccessibleDescription = "Saved talent trees automatically fill the view. Hover talent tiles for names and ranks, or use arrow keys to select talents.";
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    public bool SetTree(IReadOnlyList<TalentPresentation.Node> allNodes, out string? explanation) =>
        SetPanels([new("Talents", allNodes, new(null, "Talents", null))], out explanation);

    public bool SetPanels(IReadOnlyList<TalentPresentation.Panel> source, out string? explanation)
    {
        selected = null; hovered = null; tooltip.SetToolTip(this, null);
        var layout = Create(source, out explanation);
        panels = layout?.Panels ?? [];
        nodes = panels.SelectMany(panel => panel.Nodes).ToArray();
        treeSize = layout?.Size ?? SizeF.Empty;
        FitToViewport();
        return layout is not null;
    }

    public void FitToViewport() { UpdateFit(); Invalidate(); }
    private float DrawScale => fittedScale;
    private PointF Origin => new((ClientSize.Width - treeSize.Width * DrawScale) / 2,
        (ClientSize.Height - treeSize.Height * DrawScale) / 2);
    private void UpdateFit()
    {
        if (treeSize.IsEmpty || ClientSize.Width <= 24 || ClientSize.Height <= 24) { fittedScale = 1; return; }
        // ClientSize is already in display pixels. Fit once, including all ranks and frames,
        // and allow enlargement instead of capping large windows at the original icon size.
        fittedScale = Math.Min((ClientSize.Width - 16) / treeSize.Width, (ClientSize.Height - 16) / treeSize.Height);
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); FitToViewport(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); FitToViewport(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias; graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        var origin = Origin;
        graphics.TranslateTransform(origin.X, origin.Y);
        graphics.ScaleTransform(DrawScale, DrawScale);
        for (var index = 0; index < panels.Count; index++) DrawPanel(graphics, panels[index], index);
        // IDs are scoped to their saved tree, so cross-group connections retain their real targets.
        foreach (var tree in panels.GroupBy(panel => panel.Source.TreeId))
        {
            var treeNodes = tree.SelectMany(panel => panel.Nodes).ToArray();
            var byId = treeNodes.Where(node => node.Node.Id.HasValue).GroupBy(node => node.Node.Id!.Value)
                .ToDictionary(group => group.Key, group => group.First());
            foreach (var node in treeNodes)
                foreach (var edge in node.Node.Edges)
                    if (byId.TryGetValue(edge.Target, out var target))
                    {
                        using var arrow = new AdjustableArrowCap(3, 4, true);
                        using var pen = new Pen(edge.Active == true ? BrowserTheme.Accent : Color.FromArgb(84, 87, 92), edge.Active == true ? 2 : 1.5f) { CustomEndCap = arrow };
                        var start = Center(node); var end = Center(target);
                        var dx = end.X - start.X; var dy = end.Y - start.Y;
                        var length = Math.Sqrt(dx * dx + dy * dy); var inset = IconSize / 2f + 3;
                        if (length > inset * 2)
                            graphics.DrawLine(pen, new PointF(start.X + (float)(dx / length * inset), start.Y + (float)(dy / length * inset)),
                                new PointF(end.X - (float)(dx / length * inset), end.Y - (float)(dy / length * inset)));
                    }
        }
        foreach (var node in nodes) DrawNode(graphics, node);
    }

    private void DrawPanel(Graphics graphics, PlacedPanel panel, int index)
    {
        Color[] tints = [Color.FromArgb(44, 36, 49), Color.FromArgb(49, 43, 35), Color.FromArgb(31, 36, 58)];
        using var background = new LinearGradientBrush(panel.Bounds, tints[index % tints.Length], BrowserTheme.Background, LinearGradientMode.Vertical);
        using var border = new Pen(BrowserTheme.Border);
        graphics.FillRectangle(background, panel.Bounds);
        graphics.DrawRectangle(border, panel.Bounds.X, panel.Bounds.Y, panel.Bounds.Width, panel.Bounds.Height);
        var header = new RectangleF(panel.Bounds.X + 6, 4, panel.Bounds.Width - 12, 21);
        using var ink = new SolidBrush(BrowserTheme.Ink);
        using var titleFont = new Font("Cambria", 16, FontStyle.Bold, GraphicsUnit.Pixel);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
        graphics.DrawString(panel.Source.Name, titleFont, ink, header, format);
        using var muted = new SolidBrush(BrowserTheme.Accent);
        using var pointFont = new Font(Font.FontFamily, 11, GraphicsUnit.Pixel);
        graphics.DrawString(TalentPresentation.PanelPointSummary(panel.Source.Pool), pointFont, muted,
            new RectangleF(panel.Bounds.X + 6, 26, panel.Bounds.Width - 12, 16), format);
        graphics.DrawLine(border, panel.Bounds.Left + 6, 44, panel.Bounds.Right - 6, 44);
        if (panel.OutlierCaptionY is float captionY)
        {
            graphics.DrawLine(border, panel.Bounds.Left + 6, captionY, panel.Bounds.Right - 6, captionY);
            graphics.DrawString("Outside saved layout", pointFont, muted,
                new RectangleF(panel.Bounds.Left + 6, captionY + 2, panel.Bounds.Width - 12, 18), format);
        }
    }
    private static PointF Center(PlacedNode node) => new(node.Position.X + IconSize / 2f, node.Position.Y + IconSize / 2f);
    private void DrawNode(Graphics graphics, PlacedNode placed)
    {
        var node = placed.Node;
        var frame = new RectangleF(placed.Position.X, placed.Position.Y, IconSize, IconSize);
        var learned = node.Maximum.HasValue && node.ActiveRank == node.Maximum ? BrowserTheme.Accent : BrowserTheme.Success;
        var color = node.Active ? learned : node.Available == true ? BrowserTheme.Ink : BrowserTheme.Muted;
        using var surface = new SolidBrush(BrowserTheme.Raised); using var border = new Pen(color, node.Active ? 2 : 1);
        graphics.FillRectangle(surface, frame);
        using var placeholderFont = new Font(Font.FontFamily, 11, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        using var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString(node.Initials, placeholderFont, brush, frame, centered);
        graphics.DrawRectangle(border, frame.X, frame.Y, frame.Width, frame.Height);
        if (placed.OutsideSavedLayout)
        {
            using var marker = new SolidBrush(BrowserTheme.Warning);
            using var markerFont = new Font(Font.FontFamily, 12, FontStyle.Bold, GraphicsUnit.Pixel);
            graphics.DrawString("!", markerFont, marker, frame.Right + 2, frame.Top);
        }
        if (node.Entries.Count > 1)
        {
            using var choice = new Pen(color, 1); var center = Center(placed);
            PointF[] diamond = [new(center.X, frame.Top - 5), new(frame.Right + 5, center.Y), new(center.X, frame.Bottom + 5), new(frame.Left - 5, center.Y)];
            graphics.DrawPolygon(choice, diamond);
        }
        var rank = new RectangleF(frame.Right - 17, frame.Bottom - 7, 27, 16);
        using var badge = new SolidBrush(Color.FromArgb(12, 16, 20)); using var rankInk = new SolidBrush(color);
        using var rankFont = new Font(Font.FontFamily, 10, FontStyle.Bold, GraphicsUnit.Pixel);
        using var rankFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.FillRectangle(badge, rank); graphics.DrawRectangle(border, rank.X, rank.Y, rank.Width, rank.Height);
        graphics.DrawString(node.Rank, rankFont, rankInk, rank, rankFormat);
        if (ReferenceEquals(node, selected))
        {
            using var selection = new Pen(BrowserTheme.Ink, 2);
            graphics.DrawRectangle(selection, frame.X - 4, frame.Y - 4, frame.Width + 8, frame.Height + 8);
        }
    }

    private PlacedNode? Hit(Point location)
    {
        var origin = Origin;
        var point = new PointF((location.X - origin.X) / DrawScale, (location.Y - origin.Y) / DrawScale);
        return nodes.FirstOrDefault(node => new RectangleF(node.Position.X - 4, node.Position.Y - 4, NodeSize + 4, NodeSize + 4).Contains(point));
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); Focus();
        if (e.Button == MouseButtons.Left && Hit(e.Location) is { } hit) { SelectNode(hit.Node); return; }
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hit = Hit(e.Location)?.Node;
        if (ReferenceEquals(hit, hovered)) return;
        hovered = hit; Cursor = hit is null ? Cursors.Default : Cursors.Hand;
        tooltip.SetToolTip(this, hit is null ? null : NodeDescription(hit));
    }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = null; Cursor = Cursors.Default; tooltip.SetToolTip(this, null); }
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (nodes.Count == 0 || e.KeyCode is not (Keys.Left or Keys.Right or Keys.Up or Keys.Down)) return;
        var current = nodes.FirstOrDefault(node => ReferenceEquals(node.Node, selected));
        var next = current is null ? nodes.First() : nodes.Where(node => !ReferenceEquals(node, current)).Where(node => e.KeyCode switch
        {
            Keys.Left => node.Position.X < current.Position.X, Keys.Right => node.Position.X > current.Position.X,
            Keys.Up => node.Position.Y < current.Position.Y, _ => node.Position.Y > current.Position.Y
        }).OrderBy(node => Math.Pow(node.Position.X - current.Position.X, 2) + Math.Pow(node.Position.Y - current.Position.Y, 2)).FirstOrDefault();
        if (next is null) return;
        SelectNode(next.Node);
        e.Handled = true;
    }
    private string NodeDescription(TalentPresentation.Node node)
    {
        var text = TalentPresentation.Describe(node);
        if (nodes.Any(placed => ReferenceEquals(placed.Node, node) && placed.OutsideSavedLayout))
            text += Environment.NewLine + "A duplicate talent was saved far outside the main tree. It is shown separately; its original position is unchanged in the export." +
                Environment.NewLine + $"Saved position: {node.X}, {node.Y}";
        if (HasStagedChanges) text += Environment.NewLine + "Unapplied changes were present. Active ranks are shown.";
        return text;
    }
    private void SelectNode(TalentPresentation.Node node)
    {
        selected = node; AccessibleDescription = NodeDescription(node); Invalidate(); NodeSelected?.Invoke(node);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) tooltip.Dispose();
        base.Dispose(disposing);
    }
}
