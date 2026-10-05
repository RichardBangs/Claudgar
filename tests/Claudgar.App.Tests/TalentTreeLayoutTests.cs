using System.Text.Json;
using Claudgar.App.Browser;

internal static class TalentTreeLayoutTests
{
    public static void Run()
    {
        DistantDuplicateUsesMarkedFooterWithoutChangingTheTree();
        UniqueDistantTalentKeepsItsSavedPosition();
        UncertainOrIncompatibleDuplicatesKeepTheirSavedPositions();
        SparseSnapshotsKeepTheirSavedPositions();
        Console.WriteLine("Talent tree layout regression tests passed.");
    }

    private static void DistantDuplicateUsesMarkedFooterWithoutChangingTheTree()
    {
        var regular = Grid();
        var duplicate = DistantDuplicate(regular[0]);
        var nodes = regular.Append(duplicate).ToArray();
        var savedValues = nodes.Select(node => JsonSerializer.Serialize(node)).ToArray();
        var outliers = TalentLayoutOutliers.Find(nodes);
        Assert(outliers.Count == 1 && outliers.Contains(duplicate), "Only the distant duplicate should be classified.");

        var baseline = Layout(regular);
        var layout = Layout(nodes);
        var placed = layout.Panels.Single().Nodes;
        Assert(placed.Count == nodes.Length && nodes.All(node => placed.Any(value => ReferenceEquals(value.Node, node))),
            "Every saved node must remain in the layout as its original record.");
        Assert(placed.Count(node => node.OutsideSavedLayout) == 1, "The footer node must be explicitly marked.");
        var extra = placed.Single(node => node.OutsideSavedLayout);
        Assert(ReferenceEquals(extra.Node, duplicate) && layout.Panels.Single().OutlierCaptionY.HasValue,
            "The distant duplicate must have its own footer caption.");
        Assert(extra.Position.Y > placed.Where(node => !node.OutsideSavedLayout).Max(node => node.Position.Y) + TalentTreeLayout.NodeSize,
            "The footer must sit below the ordinary tree.");
        Assert(layout.Size.Height < baseline.Size.Height * 2, "A distant duplicate must not dominate the tree's fitted bounds.");
        foreach (var expected in baseline.Panels.Single().Nodes)
        {
            var actual = placed.Single(value => ReferenceEquals(value.Node, expected.Node));
            Assert(actual.Position == expected.Position && !actual.OutsideSavedLayout,
                "Adding a duplicate footer must preserve every ordinary saved position.");
        }
        Assert(nodes.Select(node => JsonSerializer.Serialize(node)).SequenceEqual(savedValues),
            "Classification and layout must not change any saved node values, ranks, entries, groups, or edges.");
    }

    private static void UniqueDistantTalentKeepsItsSavedPosition()
    {
        var regular = Grid();
        var unique = DistantDuplicate(regular[0]) with
        {
            Entries = [regular[0].Entries[0] with { Id = 900, SpellId = 900, Name = "Unique distant talent" }]
        };
        AssertNative(regular.Append(unique).ToArray(), unique, "A unique distant talent must retain its native position.");
    }

    private static void UncertainOrIncompatibleDuplicatesKeepTheirSavedPositions()
    {
        var regular = Grid();
        var duplicate = DistantDuplicate(regular[0]);
        var unknown = duplicate with { GroupMembershipSaved = false };
        AssertNative(regular.Append(unknown).ToArray(), unknown, "Unknown membership must not justify relocating a duplicate.");
        var differentMaximum = duplicate with { Maximum = 6 };
        AssertNative(regular.Append(differentMaximum).ToArray(), differentMaximum, "Different node rank limits must retain native positions.");
        var differentEntryMaximum = duplicate with { Entries = [duplicate.Entries[0] with { Maximum = 6 }] };
        AssertNative(regular.Append(differentEntryMaximum).ToArray(), differentEntryMaximum,
            "Different entry rank limits must retain native positions.");
        var differentGroup = duplicate with { GroupIds = [2] };
        AssertNative(regular.Append(differentGroup).ToArray(), differentGroup, "Duplicates without a shared saved group must retain native positions.");
    }

    private static void SparseSnapshotsKeepTheirSavedPositions()
    {
        var regular = Grid();
        var duplicate = DistantDuplicate(regular[0]);
        AssertNative(regular.Take(6).Append(duplicate).ToArray(), duplicate,
            "Fewer than eight visible positioned nodes must not justify duplicate relocation.");
    }

    private static void AssertNative(TalentPresentation.Node[] nodes, TalentPresentation.Node distant, string message)
    {
        Assert(TalentLayoutOutliers.Find(nodes).Count == 0, message);
        var layout = Layout(nodes);
        var placed = layout.Panels.Single().Nodes;
        Assert(placed.All(node => !node.OutsideSavedLayout) && layout.Panels.Single().OutlierCaptionY is null, message);
        var extra = placed.Single(node => ReferenceEquals(node.Node, distant));
        Assert(extra.Position.Y > placed.Where(node => !ReferenceEquals(node.Node, distant)).Max(node => node.Position.Y) + 500,
            "An unclassified distant node must retain its large saved separation.");
    }

    private static TalentTreeLayout.Layout Layout(IReadOnlyList<TalentPresentation.Node> nodes)
    {
        var pool = new TalentPresentation.PointPool(1, "Example talents", null);
        var layout = TalentTreeLayout.Create([new("Example talents", nodes, pool, 1)], out var explanation);
        Assert(layout is not null && explanation is null, "The synthetic saved tree should produce a complete layout.");
        return layout!;
    }

    private static TalentPresentation.Node[] Grid() => Enumerable.Range(0, 9).Select(index => new TalentPresentation.Node(
        100 + index, 0, 0, 5, true, true, 1000 + index % 3 * 600, 2000 + index / 3 * 600,
        [new(200 + index, $"Example talent {index + 1}", 300 + index, 0, 5, false, 135967)],
        index == 0 ? [new(101, false)] : [], [1], true)).ToArray();

    private static TalentPresentation.Node DistantDuplicate(TalentPresentation.Node original) => original with
    {
        Id = 999, X = 1000, Y = 20000, Entries = [original.Entries[0] with { Id = 999 }], Edges = []
    };

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
