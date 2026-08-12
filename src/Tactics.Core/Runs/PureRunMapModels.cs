using Tactics.Core.Content;

namespace Tactics.Core.Runs;

public enum PureRunNodeKind { Battle, Rest, Store, Mystery }
public enum PureRunMapPhase { Locked, ChoosingLayerFour, ResolvingNode, ReadyForLayerFive }

public sealed record PureRunMapNodeDefinition(string NodeId, int Layer, PureRunNodeKind Kind, ContentId ContentId);

public sealed record PureRunMapDefinition
{
    public PureRunMapDefinition(ContentId contentId, int layoutVersion, IEnumerable<PureRunMapNodeDefinition> nodes)
    {
        ContentId = contentId;
        LayoutVersion = layoutVersion;
        Nodes = nodes?.OrderBy(node => node.NodeId, StringComparer.Ordinal).ToArray()
            ?? throw new ArgumentNullException(nameof(nodes));
        if (LayoutVersion != 2 || Nodes.Count(node => node.Layer == 4) != 4 ||
            Nodes.Where(node => node.Layer == 4).Select(node => node.Kind).Distinct().Count() != 4)
            throw new ArgumentException("Layer four must expose exactly battle, rest, store, and mystery.", nameof(nodes));
    }

    public ContentId ContentId { get; }
    public int LayoutVersion { get; }
    public IReadOnlyList<PureRunMapNodeDefinition> Nodes { get; }
}

public sealed record PureRunMapState(
    PureRunMapPhase Phase,
    string CurrentNodeId,
    IReadOnlyList<string> ReachableNodeIds,
    IReadOnlyList<string> VisitedNodeIds,
    IReadOnlyDictionary<string, string> MysteryEventAssignments,
    string? PendingNodeId = null,
    string? PendingTransactionKey = null);

public sealed record RunNodeTransaction(
    string TransactionKey,
    string NodeId,
    PureRunNodeKind Kind,
    bool Committed = false);

public sealed record PureRunMapResult(bool Succeeded, string? RejectionCode, PureRunMapState State, RunNodeTransaction? Transaction = null, bool WasDuplicate = false);
