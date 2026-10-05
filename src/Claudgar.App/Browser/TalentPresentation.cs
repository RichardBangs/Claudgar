using System.Text.Json.Nodes;

namespace Claudgar.App.Browser;

/// <summary>Reads the saved talent snapshot without requiring game APIs or downloaded metadata.</summary>
internal static class TalentPresentation
{
    internal sealed record Entry(long? Id, string Name, long? SpellId, int? Rank, int? Maximum, bool Active, long? IconFileId);
    internal sealed record Edge(long Target, bool? Active);
    internal sealed record Node(long? Id, int? ActiveRank, int? CurrentRank, int? Maximum, bool? Available,
        bool? Visible, double? X, double? Y, IReadOnlyList<Entry> Entries, IReadOnlyList<Edge> Edges,
        IReadOnlyList<long> GroupIds, bool GroupMembershipSaved)
    {
        public string Name => Entries.Count > 0 ? string.Join(" / ", Entries.Select(entry => entry.Name)) : $"Talent {Id?.ToString() ?? "unknown"}";
        public string Rank => $"{ActiveRank?.ToString() ?? "?"}/{Maximum?.ToString() ?? "?"}";
        public bool Active => ActiveRank > 0;
        public long? IconFileId => (Entries.FirstOrDefault(entry => entry.Active) ?? Entries.FirstOrDefault())?.IconFileId;
        public string State => Active ? "Learned" : Available == true ? "Available" : Available == false ? "Unavailable" : "Availability unknown";
        public string Initials
        {
            get
            {
                var chosen = Entries.FirstOrDefault(entry => entry.Active) ?? Entries.FirstOrDefault();
                var words = (chosen?.Name ?? "Talent").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return string.Concat(words.Take(2).Select(word => char.ToUpperInvariant(word[0])));
            }
        }
    }
    internal sealed record PointPool(long? GroupId, string Name, JsonArray? Currencies, long? IconFileId = null)
    {
        public override string ToString() => Name;
    }
    internal sealed record Tree(long? Id, string Name, IReadOnlyList<Node> Nodes, IReadOnlyList<PointPool> Pools)
    {
        public override string ToString() => Name;
    }
    internal sealed record Panel(string Name, IReadOnlyList<Node> Nodes, PointPool Pool, long? TreeId = null);

    public static IReadOnlyList<Panel> OverviewPanels(IReadOnlyList<Tree> trees)
    {
        var panels = new List<Panel>();
        foreach (var tree in trees)
        {
            var groups = tree.Pools.Where(pool => pool.GroupId.HasValue).ToArray();
            var grouped = groups.Select(group => tree.Nodes.Where(node => node.GroupIds.Contains(group.GroupId!.Value)).ToArray()).ToArray();
            var outliers = TalentLayoutOutliers.Find(tree.Nodes);
            var mainNodes = tree.Nodes.Where(node => node.Visible != false && !outliers.Contains(node)).ToArray();
            var mainGroups = grouped.Select(nodes => nodes.Where(node => node.Visible != false && !outliers.Contains(node)).ToArray()).ToArray();
            var canSeparate = groups.Length > 1 && mainNodes.All(node => node.GroupMembershipSaved &&
                groups.Count(group => node.GroupIds.Contains(group.GroupId!.Value)) == 1) &&
                mainGroups.All(nodes => nodes.Length > 0 && nodes.All(node => node.X.HasValue));
            for (var index = 1; canSeparate && index < mainGroups.Length; index++)
                canSeparate = mainGroups[index - 1].Max(node => node.X!.Value) < mainGroups[index].Min(node => node.X!.Value);
            if (!canSeparate)
            {
                panels.Add(new(tree.Name, tree.Nodes, groups.Length == 1 ? groups[0] : tree.Pools[0], tree.Id));
                continue;
            }
            for (var index = 0; index < groups.Length; index++)
                panels.Add(new(groups[index].Name, grouped[index], groups[index], tree.Id));
        }
        return panels;
    }

    public static IReadOnlyList<Tree> ReadTrees(JsonObject? data)
    {
        if (data?["trees"] is not JsonArray trees) return [];
        return trees.OfType<JsonObject>().Select((tree, index) =>
        {
            var groups = tree["groups"] as JsonArray;
            var names = groups?.OfType<JsonObject>().Select(group => Text(group["name"]))
                .Where(name => !string.IsNullOrWhiteSpace(name)).Distinct().ToArray() ?? [];
            var pools = new List<PointPool> { new(null, "All talent groups", tree["currencies"] as JsonArray) };
            if (groups is not null)
                pools.AddRange(groups.OfType<JsonObject>().Select(group => new PointPool(
                    Number(group["groupId"]), Text(group["name"]) ?? $"Group {Number(group["groupId"])?.ToString() ?? "unknown"}", group["currencies"] as JsonArray, Number(group["iconFileId"]))));
            var nodes = tree["nodes"] is JsonArray array ? array.OfType<JsonObject>().Select(ReadNode).ToArray() : [];
            var id = Number(tree["treeId"]);
            var name = names.Length > 0 ? string.Join(" / ", names) : $"Talent tree {id?.ToString() ?? (index + 1).ToString()}";
            return new Tree(id, name, nodes, pools);
        }).ToArray();
    }

    private static Node ReadNode(JsonObject node)
    {
        var entries = node["entries"] is JsonArray array ? array.OfType<JsonObject>().Select(entry => new Entry(
            Number(entry["entryId"]), Text(entry["name"]) ?? $"Talent {Number(entry["spellId"])?.ToString() ?? Number(entry["entryId"])?.ToString() ?? "unknown"}",
            Number(entry["spellId"]), Integer(entry["rank"]), Integer(entry["maxRanks"]), Boolean(entry["isActive"]) == true, Number(entry["iconFileId"]))).ToArray() : [];
        var edges = node["visibleEdges"] is JsonArray visibleEdges ? visibleEdges.OfType<JsonObject>()
            .Where(edge => Number(edge["targetNodeId"]) is not null)
            .Select(edge => new Edge(Number(edge["targetNodeId"])!.Value, Boolean(edge["isActive"]))).ToArray() : [];
        return new(Number(node["nodeId"]), Integer(node["activeRank"]), Integer(node["currentRank"]), Integer(node["maxRanks"]),
            Boolean(node["isAvailable"]), Boolean(node["isVisible"]), Coordinate(node["posX"]), Coordinate(node["posY"]), entries, edges,
            node["groupIds"] is JsonArray groupIds ? groupIds.Select(Number).OfType<long>().ToArray() : [], node["groupIds"] is JsonArray);
    }

    public static string PointSummary(PointPool pool)
    {
        if (pool.Currencies is not { Count: > 0 }) return "Point information was not saved for this pool.";
        return string.Join("   ·   ", pool.Currencies.OfType<JsonObject>().Select(currency =>
        {
            var spent = Number(currency["spent"]);
            var quantity = Number(currency["quantity"]);
            var maximum = Number(currency["maxQuantity"]);
            var id = Number(currency["currencyId"]);
            return $"Pool {id?.ToString() ?? "?"}: {spent?.ToString() ?? "?"} spent, {quantity?.ToString() ?? "?"} remaining" +
                (maximum.HasValue ? $", {maximum} maximum" : "");
        }));
    }

    public static string PanelPointSummary(PointPool pool)
    {
        if (pool.Currencies is not { Count: > 0 }) return "Points not saved";
        return string.Join(" · ", pool.Currencies.OfType<JsonObject>().Select(currency =>
            $"{Number(currency["spent"])?.ToString() ?? "?"} spent · {Number(currency["quantity"])?.ToString() ?? "?"} remaining"));
    }

    public static string Describe(Node node)
    {
        var lines = new List<string> { node.Name, $"{node.State}  ·  Active rank {node.Rank}  ·  Node {node.Id?.ToString() ?? "unknown"}" };
        if (node.CurrentRank is int current && current != node.ActiveRank) lines.Add($"Current preview rank: {current}. This may include unapplied talent changes.");
        if (node.Visible == false) lines.Add("This node was hidden in the saved game tree.");
        foreach (var entry in node.Entries)
            lines.Add($"{(entry.Active ? "Selected: " : "Option: ")}{entry.Name}  ·  Rank {entry.Rank?.ToString() ?? "?"}/{entry.Maximum?.ToString() ?? node.Maximum?.ToString() ?? "?"}" +
                (entry.SpellId.HasValue ? $"  ·  Spell {entry.SpellId}" : ""));
        return string.Join(Environment.NewLine, lines);
    }

    public static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var result) ? result : null;
    private static long? Number(JsonNode? node)
    {
        if (node is not JsonValue value) return null;
        if (value.TryGetValue<long>(out var result)) return result;
        if (value.TryGetValue<int>(out var integer)) return integer;
        if (value.TryGetValue<double>(out var numeric) && double.IsFinite(numeric) && numeric == Math.Truncate(numeric) &&
            numeric >= long.MinValue && numeric < (double)long.MaxValue) return (long)numeric;
        return null;
    }
    private static int? Integer(JsonNode? node) => Number(node) is long number && number is >= int.MinValue and <= int.MaxValue ? (int)number : null;
    private static bool? Boolean(JsonNode? node) => node is JsonValue value && value.TryGetValue<bool>(out var result) ? result : null;
    private static double? Coordinate(JsonNode? node)
    {
        if (node is not JsonValue value) return null;
        if (value.TryGetValue<double>(out var result) && double.IsFinite(result)) return result;
        if (value.TryGetValue<decimal>(out var decimalValue)) return (double)decimalValue;
        return Number(node);
    }
}
