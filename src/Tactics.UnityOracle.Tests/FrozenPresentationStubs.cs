namespace Tactics.Common.Skills.Graph;

/// <summary>
/// Minimal graph surface consumed by the frozen presentation compiler source.
/// </summary>
public enum PresentationCueKind
{
    Action
}

public abstract class PresentationNodeRecord
{
    public string NodeId { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}

public sealed class PresentationEntryNodeRecord : PresentationNodeRecord
{
    public PresentationCueKind Cue { get; set; }
}

public sealed class PresentationFinishNodeRecord : PresentationNodeRecord
{
}

public sealed class PresentationJoinNodeRecord : PresentationNodeRecord
{
}

public sealed class PresentationForkNodeRecord : PresentationNodeRecord
{
    public string JoinNodeId { get; set; } = string.Empty;
}

public sealed class PresentationLeafNodeRecord : PresentationNodeRecord
{
    public string NodeTypeId { get; set; } = "leaf";
}

public sealed record PresentationEdgeRecord(string SourceNodeId, string TargetNodeId);

public sealed class BattlePresentationGraph
{
    private readonly IReadOnlyList<PresentationNodeRecord> _nodes;
    private readonly IReadOnlyList<PresentationEdgeRecord> _edges;

    public BattlePresentationGraph(
        IEnumerable<PresentationNodeRecord> nodes,
        IEnumerable<PresentationEdgeRecord> edges)
    {
        _nodes = nodes.ToArray();
        _edges = edges.ToArray();
    }

    public PresentationEntryNodeRecord FindEntry(PresentationCueKind cue) =>
        _nodes.OfType<PresentationEntryNodeRecord>().FirstOrDefault(node => node.Cue == cue);

    public PresentationNodeRecord FindNode(string nodeId) =>
        _nodes.FirstOrDefault(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal));

    public List<PresentationEdgeRecord> GetEdgesFrom(string sourceNodeId) => _edges
        .Where(edge => string.Equals(edge.SourceNodeId, sourceNodeId, StringComparison.Ordinal))
        .ToList();
}
