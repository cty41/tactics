namespace Tactics.Core.Presentation;

public enum PresentationNodeKind
{
    Sequence,
    Parallel,
    Leaf
}

public sealed record PresentationNode
{
    public PresentationNode(
        string nodeId,
        string nodeTypeId,
        PresentationNodeKind kind,
        IReadOnlyList<string>? children = null,
        bool required = true,
        string? forkNodeId = null,
        string? joinNodeId = null)
    {
        NodeId = ValidateText(nodeId, nameof(nodeId));
        NodeTypeId = ValidateText(nodeTypeId, nameof(nodeTypeId));
        Kind = kind;
        Children = children?.ToArray() ?? Array.Empty<string>();
        Required = required;
        ForkNodeId = NormalizeOptionalText(forkNodeId);
        JoinNodeId = NormalizeOptionalText(joinNodeId);
    }

    public string NodeId { get; }
    public string NodeTypeId { get; }
    public PresentationNodeKind Kind { get; }
    public IReadOnlyList<string> Children { get; }
    public bool Required { get; }
    public string? ForkNodeId { get; }
    public string? JoinNodeId { get; }

    private static string ValidateText(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value cannot be empty.", name) : value.Trim();

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class PresentationExecutionPlan
{
    private readonly IReadOnlyDictionary<string, PresentationNode> _nodes;

    public PresentationExecutionPlan(int schemaVersion, string rootNodeId, IEnumerable<PresentationNode> nodes)
    {
        if (schemaVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        if (string.IsNullOrWhiteSpace(rootNodeId))
            throw new ArgumentException("Root node is required.", nameof(rootNodeId));

        var materialized = nodes?.ToArray() ?? throw new ArgumentNullException(nameof(nodes));
        if (materialized.Length == 0)
            throw new ArgumentException("A presentation plan must contain nodes.", nameof(nodes));
        if (materialized.Select(node => node.NodeId).Distinct(StringComparer.Ordinal).Count() != materialized.Length)
            throw new ArgumentException("Presentation node IDs must be unique.", nameof(nodes));

        _nodes = materialized.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        if (!_nodes.ContainsKey(rootNodeId))
            throw new ArgumentException("Root node does not exist.", nameof(rootNodeId));

        SchemaVersion = schemaVersion;
        RootNodeId = rootNodeId;
    }

    public int SchemaVersion { get; }
    public string RootNodeId { get; }
    public IReadOnlyDictionary<string, PresentationNode> Nodes => _nodes;

    public void Validate()
    {
        foreach (PresentationNode node in _nodes.Values)
        {
            if (node.Kind == PresentationNodeKind.Leaf && node.Children.Count > 0)
                throw new InvalidOperationException($"Leaf node '{node.NodeId}' cannot have children.");
            if (node.Kind == PresentationNodeKind.Parallel &&
                (node.ForkNodeId is null || node.JoinNodeId is null))
            {
                throw new InvalidOperationException(
                    $"Parallel node '{node.NodeId}' must preserve its Fork/Join boundary.");
            }

            foreach (string child in node.Children)
            {
                if (!_nodes.ContainsKey(child))
                    throw new InvalidOperationException($"Node '{node.NodeId}' references missing child '{child}'.");
            }
        }

        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        Visit(RootNodeId, visiting, visited);
    }

    private void Visit(string nodeId, HashSet<string> visiting, HashSet<string> visited)
    {
        if (!visiting.Add(nodeId))
            throw new InvalidOperationException($"Presentation graph contains a cycle at '{nodeId}'.");
        if (visited.Contains(nodeId))
            return;

        foreach (string child in _nodes[nodeId].Children)
            Visit(child, visiting, visited);

        visiting.Remove(nodeId);
        visited.Add(nodeId);
    }
}
