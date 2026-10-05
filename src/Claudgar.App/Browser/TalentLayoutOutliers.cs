namespace Claudgar.App.Browser;

/// <summary>Identifies isolated duplicates without guessing their intended talent positions.</summary>
internal static class TalentLayoutOutliers
{
    public static IReadOnlySet<TalentPresentation.Node> Find(IReadOnlyList<TalentPresentation.Node> source)
    {
        var result = new HashSet<TalentPresentation.Node>(ReferenceEqualityComparer.Instance);
        var nodes = source.Where(node => node.Visible != false && node.X.HasValue && node.Y.HasValue).ToArray();
        if (nodes.Length < 8) return result;

        var nearest = nodes.Select(node => nodes.Where(other => !ReferenceEquals(node, other))
            .Select(other => Distance(node, other)).DefaultIfEmpty(0).Min()).ToArray();
        var distances = nearest.Order().ToArray();
        var median = distances[distances.Length / 2];
        if (!double.IsFinite(median) || median <= 0) return result;
        var threshold = median * 6;

        for (var index = 0; index < nodes.Length; index++)
        {
            var node = nodes[index];
            if (!double.IsFinite(nearest[index]) || nearest[index] <= threshold ||
                !node.GroupMembershipSaved || node.GroupIds.Count == 0 || !node.Maximum.HasValue ||
                node.Entries.Count == 0 || node.Entries.Any(entry => !entry.SpellId.HasValue || !entry.Maximum.HasValue)) continue;

            // Require an equivalent saved talent in the ordinary cluster and a shared saved group.
            // A distant unique capstone or a separate subtree must retain its own native position.
            for (var otherIndex = 0; otherIndex < nodes.Length; otherIndex++)
            {
                var other = nodes[otherIndex];
                if (otherIndex == index || nearest[otherIndex] > threshold || !other.GroupMembershipSaved ||
                    node.Maximum != other.Maximum || !node.GroupIds.Intersect(other.GroupIds).Any() ||
                    other.Entries.Count != node.Entries.Count) continue;
                var spells = node.Entries.Select(entry => (entry.SpellId, entry.Maximum)).OrderBy(entry => entry.SpellId).ThenBy(entry => entry.Maximum);
                var otherSpells = other.Entries.Select(entry => (entry.SpellId, entry.Maximum)).OrderBy(entry => entry.SpellId).ThenBy(entry => entry.Maximum);
                if (!spells.SequenceEqual(otherSpells)) continue;
                result.Add(node);
                break;
            }
        }
        return result;
    }

    private static double Distance(TalentPresentation.Node first, TalentPresentation.Node second)
    {
        var x = first.X!.Value - second.X!.Value;
        var y = first.Y!.Value - second.Y!.Value;
        return Math.Sqrt(x * x + y * y);
    }
}
