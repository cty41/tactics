namespace Tactics.Application.Presentation;

/// <summary>
/// Produces a stable left-to-right authoring layout without depending on an editor engine.
/// </summary>
public sealed class PresentationGraphLayoutService
{
    private const float HorizontalSpacing = 280f;
    private const float VerticalSpacing = 200f;

    public IReadOnlyDictionary<string, PresentationNodePosition> Arrange(PresentationGraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var outgoing = document.Nodes.ToDictionary(
            node => node.NodeId,
            _ => new List<string>(),
            StringComparer.Ordinal);
        var indegree = document.Nodes.ToDictionary(
            node => node.NodeId,
            _ => 0,
            StringComparer.Ordinal);
        foreach (PresentationEdgeDocument edge in document.Edges)
        {
            outgoing[edge.SourceNodeId].Add(edge.TargetNodeId);
            indegree[edge.TargetNodeId]++;
        }
        foreach (List<string> targets in outgoing.Values)
            targets.Sort(StringComparer.Ordinal);

        var depth = document.Nodes.ToDictionary(
            node => node.NodeId,
            _ => 0,
            StringComparer.Ordinal);
        string[] roots = document.Nodes
            .Where(node => indegree[node.NodeId] == 0)
            .OrderBy(node => CueRank(node))
            .ThenBy(node => node.NodeId, StringComparer.Ordinal)
            .Select(node => node.NodeId)
            .ToArray();
        var lane = roots
            .Select((nodeId, index) => (nodeId, index))
            .ToDictionary(item => item.nodeId, item => item.index, StringComparer.Ordinal);
        var ready = new List<string>(roots);
        var ordered = new List<string>(document.Nodes.Count);
        while (ready.Count > 0)
        {
            ready.Sort((left, right) =>
            {
                int laneOrder = lane[left].CompareTo(lane[right]);
                return laneOrder != 0 ? laneOrder : StringComparer.Ordinal.Compare(left, right);
            });
            string nodeId = ready[0];
            ready.RemoveAt(0);
            ordered.Add(nodeId);
            foreach (string targetId in outgoing[nodeId])
            {
                depth[targetId] = Math.Max(depth[targetId], depth[nodeId] + 1);
                lane[targetId] = lane.TryGetValue(targetId, out int targetLane)
                    ? Math.Min(targetLane, lane[nodeId])
                    : lane[nodeId];
                indegree[targetId]--;
                if (indegree[targetId] == 0)
                    ready.Add(targetId);
            }
        }

        if (ordered.Count != document.Nodes.Count)
            throw new InvalidOperationException("Presentation graph layout requires an acyclic graph.");

        var result = new Dictionary<string, PresentationNodePosition>(StringComparer.Ordinal);
        foreach (IGrouping<int, string> layer in ordered.GroupBy(nodeId => depth[nodeId]))
        {
            string[] nodeIds = layer
                .OrderBy(nodeId => lane[nodeId])
                .ThenBy(nodeId => nodeId, StringComparer.Ordinal)
                .ToArray();
            for (int row = 0; row < nodeIds.Length; row++)
            {
                result[nodeIds[row]] = new PresentationNodePosition(
                    layer.Key * HorizontalSpacing,
                    row * VerticalSpacing);
            }
        }

        return result;
    }

    public PresentationGraphChangeSet CreateChangeSet(
        PresentationGraphDocument document,
        string changeId = "presentation.auto-layout")
    {
        IReadOnlyDictionary<string, PresentationNodePosition> positions = Arrange(document);
        return new PresentationGraphChangeSet(
            changeId,
            document.Revision,
            document.Nodes.Select(node =>
                (PresentationGraphOperation)new SetPresentationNodePositionOperation(
                    node.NodeId,
                    positions[node.NodeId])));
    }

    private static string CueRank(PresentationNodeDocument node) =>
        string.IsNullOrWhiteSpace(node.Cue) ? "~" + node.NodeId : node.Cue + "\0" + node.NodeId;
}
