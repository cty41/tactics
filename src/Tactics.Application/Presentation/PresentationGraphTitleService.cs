namespace Tactics.Application.Presentation;

/// <summary>
/// Derives concise editor labels while stable node IDs remain the reference identity.
/// </summary>
public sealed class PresentationGraphTitleService
{
    public IReadOnlyDictionary<string, string> CreateTitles(PresentationGraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Nodes.ToDictionary(
            node => node.NodeId,
            node => Title(document, node),
            StringComparer.Ordinal);
    }

    private static string Title(PresentationGraphDocument document, PresentationNodeDocument node) =>
        node.NodeTypeId switch
        {
            "PresentationEntryNodeRecord" => string.IsNullOrWhiteSpace(node.Cue)
                ? "Entry"
                : $"{node.Cue} Entry",
            "PresentationUnitTweenNodeRecord" => "Ranged Tween",
            "PresentationProjectileNodeRecord" => "Projectile",
            "PresentationFinishNodeRecord" => FinishTitle(document, node.NodeId),
            _ => FriendlyType(node.NodeTypeId)
        };

    private static string FinishTitle(PresentationGraphDocument document, string nodeId)
    {
        string cue = FindUpstreamCue(document, nodeId);
        return string.IsNullOrWhiteSpace(cue) ? "Finish" : $"{cue} Finish";
    }

    private static string FindUpstreamCue(PresentationGraphDocument document, string nodeId)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>();
        pending.Enqueue(nodeId);
        while (pending.Count > 0)
        {
            string current = pending.Dequeue();
            if (!visited.Add(current))
                continue;
            PresentationNodeDocument node = document.Nodes.Single(item => item.NodeId == current);
            if (!string.IsNullOrWhiteSpace(node.Cue))
                return node.Cue;
            foreach (PresentationEdgeDocument edge in document.Edges
                         .Where(edge => edge.TargetNodeId == current)
                         .OrderBy(edge => edge.EdgeId, StringComparer.Ordinal))
            {
                pending.Enqueue(edge.SourceNodeId);
            }
        }
        return string.Empty;
    }

    private static string FriendlyType(string nodeType) => nodeType
        .Replace("Presentation", string.Empty, StringComparison.Ordinal)
        .Replace("NodeRecord", string.Empty, StringComparison.Ordinal);
}
