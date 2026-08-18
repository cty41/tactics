namespace Tactics.Core.Runs;

public enum AdventureRouteLifecycle { Draft, ReadyToCommit, Committed }

public sealed record AdventureRouteCandidate(string NodeId, int Group, AdventureObjectKind Kind);

/// <summary>Fixed two-stage route choice: exactly one of three nodes must be selected in each group.</summary>
public sealed record AdventureRoutePlan(
    IReadOnlyList<AdventureRouteCandidate> Candidates,
    IReadOnlyDictionary<int, string> Selections,
    AdventureRouteLifecycle Lifecycle)
{
    public static AdventureRoutePlan Create(IReadOnlyList<AdventureRouteCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ValidateCandidates(candidates);
        return new AdventureRoutePlan(candidates.ToArray(), new Dictionary<int, string>(), AdventureRouteLifecycle.Draft);
    }

    public IReadOnlyList<string> CurrentCandidateNodeIds => Lifecycle == AdventureRouteLifecycle.Committed
        ? Selections.OrderBy(value => value.Key).Select(value => value.Value).ToArray()
        : Candidates.Where(value => value.Group == (Selections.ContainsKey(1) ? 2 : 1)).Select(value => value.NodeId).ToArray();

    public AdventureRoutePlan Select(string nodeId)
    {
        if (Lifecycle == AdventureRouteLifecycle.Committed) throw new InvalidOperationException("Committed route plans are immutable.");
        AdventureRouteCandidate candidate = Candidates.SingleOrDefault(value => value.NodeId == nodeId)
            ?? throw new ArgumentException("Unknown route node.", nameof(nodeId));
        int expectedGroup = Selections.ContainsKey(1) ? 2 : 1;
        if (candidate.Group != expectedGroup) throw new InvalidOperationException("Route groups must be selected in order.");
        var next = new Dictionary<int, string>(Selections) { [candidate.Group] = candidate.NodeId };
        return this with { Selections = next, Lifecycle = next.Count == 2 ? AdventureRouteLifecycle.ReadyToCommit : AdventureRouteLifecycle.Draft };
    }

    public AdventureRoutePlan Commit()
    {
        if (Lifecycle != AdventureRouteLifecycle.ReadyToCommit) throw new InvalidOperationException("Both route groups must be selected before commit.");
        return this with { Lifecycle = AdventureRouteLifecycle.Committed };
    }

    private static void ValidateCandidates(IReadOnlyList<AdventureRouteCandidate> candidates)
    {
        if (candidates.Select(value => value.NodeId).Distinct(StringComparer.Ordinal).Count() != candidates.Count)
            throw new ArgumentException("Route node ids must be unique.");
        if (!candidates.GroupBy(value => value.Group).OrderBy(value => value.Key)
                .Select(value => (value.Key, value.Count())).SequenceEqual(new[] { (1, 3), (2, 3) }))
            throw new ArgumentException("A route plan requires exactly two ordered groups of three candidates.");
    }
}
