namespace Claudgar.App.Browser;

/// <summary>Preserves native positions and keeps isolated duplicate nodes in an explicit footer.</summary>
internal static class TalentTreeLayout
{
    internal sealed record PlacedNode(TalentPresentation.Node Node, PointF Position, bool OutsideSavedLayout = false);
    internal sealed record PlacedPanel(TalentPresentation.Panel Source, RectangleF Bounds, IReadOnlyList<PlacedNode> Nodes,
        float? OutlierCaptionY = null);
    internal sealed record Layout(IReadOnlyList<PlacedPanel> Panels, SizeF Size);
    public const int IconSize = 40;
    public const int NodeSize = 48;
    private const int Padding = 10;
    private const int HeaderHeight = 44;
    private const int PanelGap = 8;
    private const int FooterCaptionHeight = 24;

    public static Layout? Create(IReadOnlyList<TalentPresentation.Panel> source, out string? explanation)
    {
        var visible = source.Select(panel => panel.Nodes.Where(node => node.Visible != false).ToArray()).ToArray();
        var all = visible.SelectMany(nodes => nodes).ToArray();
        if (all.Length == 0) return Fail("No visible talent nodes were saved.", out explanation);
        if (all.Any(node => node.X is null || node.Y is null))
            return Fail("Some talent positions were not saved, so the saved tree cannot be drawn.", out explanation);
        var scale = 0d;
        var nativeTrees = source.Select((panel, index) => (panel, index)).GroupBy(value =>
            value.panel.TreeId.HasValue ? (value.panel.TreeId.Value, -1) : (0L, value.index)).ToArray();
        var outliers = new HashSet<TalentPresentation.Node>(ReferenceEqualityComparer.Instance);
        foreach (var nativeTree in nativeTrees)
            outliers.UnionWith(TalentLayoutOutliers.Find(nativeTree.SelectMany(value => visible[value.index]).ToArray()));
        var regular = visible.Select(nodes => nodes.Where(node => !outliers.Contains(node)).ToArray()).ToArray();
        var ordinaryNodes = regular.SelectMany(nodes => nodes).ToArray();
        foreach (var nativeTree in nativeTrees)
        {
            var nodes = nativeTree.SelectMany(value => regular[value.index]).ToArray();
            for (var first = 0; first < nodes.Length; first++)
                for (var second = first + 1; second < nodes.Length; second++)
                {
                    var dx = Math.Abs(nodes[first].X!.Value - nodes[second].X!.Value);
                    var dy = Math.Abs(nodes[first].Y!.Value - nodes[second].Y!.Value);
                    if (dx == 0 && dy == 0)
                        return Fail("Saved talent positions overlap, so the saved tree cannot be drawn.", out explanation);
                    scale = Math.Max(scale, Math.Min(dx > 0 ? (NodeSize + 10) / dx : double.PositiveInfinity,
                        dy > 0 ? (NodeSize + 10) / dy : double.PositiveInfinity));
                }
        }
        if (scale == 0) scale = 1;
        // Use the same vertical origin and scale across columns so saved row alignment survives.
        var minimumY = ordinaryNodes.Min(node => node.Y!.Value);
        var ordinaryHeight = (ordinaryNodes.Max(node => node.Y!.Value) - minimumY) * scale + NodeSize + Padding * 2 + HeaderHeight;
        var height = ordinaryHeight;
        if (!double.IsFinite(height) || height > 24000) return Fail("Saved talent coordinates cannot produce a readable tree.", out explanation);
        var panels = new List<PlacedPanel>();
        var offset = 0d;
        for (var index = 0; index < source.Count;)
        {
            var end = index + 1;
            if (source[index].TreeId.HasValue)
                while (end < source.Count && source[end].TreeId == source[index].TreeId) end++;
            var treeNodes = regular.Skip(index).Take(end - index).SelectMany(nodes => nodes).ToArray();
            var minimumX = treeNodes.Length > 0 ? treeNodes.Min(node => node.X!.Value) : 0;
            var span = treeNodes.Length > 0 ? treeNodes.Max(node => node.X!.Value) - minimumX : 0;
            var width = span * scale + NodeSize + Padding * 2;
            if (!double.IsFinite(width) || offset + width > 24000)
                return Fail("Saved talent coordinates cannot produce a readable tree.", out explanation);
            var inset = (width - span * scale - NodeSize) / 2;
            var left = offset;
            for (var panelIndex = index; panelIndex < end; panelIndex++)
            {
                var right = offset + width;
                if (panelIndex + 1 < end)
                {
                    // Background cuts are decorative; every node keeps its global saved X coordinate.
                    var previous = regular[panelIndex].Select(node => node.X!.Value).DefaultIfEmpty(minimumX).Max();
                    var next = regular[panelIndex + 1].Select(node => node.X!.Value).DefaultIfEmpty(previous).Min();
                    right = Math.Clamp(offset + inset + ((previous + next) / 2 - minimumX) * scale + NodeSize / 2d, left + 1, offset + width);
                }
                var placed = regular[panelIndex].Select(node => new PlacedNode(node, new PointF(
                    (float)(offset + inset + (node.X!.Value - minimumX) * scale),
                    (float)(HeaderHeight + Padding + (node.Y!.Value - minimumY) * scale)))).ToList();
                var gap = panelIndex + 1 < end ? PanelGap : 0;
                var panelWidth = Math.Max(1, right - left - gap);
                var extra = visible[panelIndex].Where(outliers.Contains).ToArray();
                float? captionY = null;
                if (extra.Length > 0)
                {
                    captionY = (float)ordinaryHeight;
                    var columns = Math.Max(1, (int)((panelWidth - Padding * 2 + 10) / (NodeSize + 10)));
                    var rows = (extra.Length + columns - 1) / columns;
                    for (var extraIndex = 0; extraIndex < extra.Length; extraIndex++)
                    {
                        var row = extraIndex / columns;
                        var rowCount = Math.Min(columns, extra.Length - row * columns);
                        var rowWidth = rowCount * (NodeSize + 10) - 10;
                        placed.Add(new(extra[extraIndex], new PointF(
                            (float)(left + (panelWidth - rowWidth) / 2 + extraIndex % columns * (NodeSize + 10)),
                            (float)(ordinaryHeight + FooterCaptionHeight + row * (NodeSize + 10))), true));
                    }
                    height = Math.Max(height, ordinaryHeight + FooterCaptionHeight + rows * (NodeSize + 10) - 10 + Padding);
                }
                panels.Add(new(source[panelIndex], new RectangleF((float)left, 0, (float)panelWidth, (float)ordinaryHeight), placed, captionY));
                left = right;
            }
            offset += width + PanelGap;
            index = end;
        }
        if (!double.IsFinite(height) || height > 24000) return Fail("Saved talent coordinates cannot produce a readable tree.", out explanation);
        panels = panels.Select(panel => panel with { Bounds = new RectangleF(panel.Bounds.X, panel.Bounds.Y, panel.Bounds.Width, (float)height) }).ToList();
        explanation = null;
        return new(panels, new((float)(offset - PanelGap), (float)height));
    }

    private static Layout? Fail(string reason, out string? explanation) { explanation = reason; return null; }
}
