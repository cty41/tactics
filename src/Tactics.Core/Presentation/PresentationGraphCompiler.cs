namespace Tactics.Core.Presentation;

/// <summary>
/// Authoring-graph node kinds required to compile deterministic presentation execution structure.
/// </summary>
public enum PresentationGraphNodeKind
{
    Entry,
    Finish,
    Fork,
    Join,
    Leaf
}

/// <summary>
/// Engine-neutral presentation authoring node.
/// </summary>
public sealed record PresentationGraphNode
{
    public PresentationGraphNode(
        string nodeId,
        string nodeTypeId,
        PresentationGraphNodeKind kind,
        bool enabled = true,
        string? cueId = null,
        string? joinNodeId = null,
        bool required = true)
    {
        NodeId = ValidateText(nodeId, nameof(nodeId));
        NodeTypeId = ValidateText(nodeTypeId, nameof(nodeTypeId));
        Kind = kind;
        Enabled = enabled;
        CueId = NormalizeOptionalText(cueId);
        JoinNodeId = NormalizeOptionalText(joinNodeId);
        Required = required;
    }

    public string NodeId { get; }
    public string NodeTypeId { get; }
    public PresentationGraphNodeKind Kind { get; }
    public bool Enabled { get; }
    public string? CueId { get; }
    public string? JoinNodeId { get; }
    public bool Required { get; }

    private static string ValidateText(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value cannot be empty.", name) : value.Trim();

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Ordered authoring edge. Equal-order edges preserve declaration order.
/// </summary>
public sealed record PresentationGraphEdge(string SourceNodeId, string TargetNodeId, int Order = 0);

/// <summary>
/// Immutable engine-neutral presentation graph snapshot.
/// </summary>
public sealed class PresentationGraphDefinition
{
    private readonly IReadOnlyDictionary<string, PresentationGraphNode> _nodes;
    private readonly IReadOnlyList<PresentationGraphEdge> _edges;

    public PresentationGraphDefinition(
        int schemaVersion,
        IEnumerable<PresentationGraphNode> nodes,
        IEnumerable<PresentationGraphEdge> edges)
    {
        if (schemaVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        PresentationGraphNode[] materializedNodes = nodes.ToArray();
        if (materializedNodes.Length == 0)
            throw new ArgumentException("A presentation graph must contain nodes.", nameof(nodes));
        if (materializedNodes.Select(node => node.NodeId).Distinct(StringComparer.Ordinal).Count() != materializedNodes.Length)
            throw new ArgumentException("Presentation graph node IDs must be unique.", nameof(nodes));

        _nodes = materializedNodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        _edges = edges.ToArray();
        foreach (PresentationGraphEdge edge in _edges)
        {
            if (!_nodes.ContainsKey(edge.SourceNodeId) || !_nodes.ContainsKey(edge.TargetNodeId))
                throw new ArgumentException($"Presentation edge '{edge.SourceNodeId}->{edge.TargetNodeId}' references a missing node.", nameof(edges));
        }

        SchemaVersion = schemaVersion;
    }

    public int SchemaVersion { get; }
    public IReadOnlyDictionary<string, PresentationGraphNode> Nodes => _nodes;

    public PresentationGraphNode? FindEntry(string cueId) => _nodes.Values.FirstOrDefault(node =>
        node.Kind == PresentationGraphNodeKind.Entry &&
        string.Equals(node.CueId, cueId, StringComparison.Ordinal));

    public PresentationGraphNode? FindNode(string nodeId) => _nodes.GetValueOrDefault(nodeId);

    public IReadOnlyList<PresentationGraphEdge> GetEdgesFrom(string nodeId) => _edges
        .Select((edge, index) => (edge, index))
        .Where(item => string.Equals(item.edge.SourceNodeId, nodeId, StringComparison.Ordinal))
        .OrderBy(item => item.edge.Order)
        .ThenBy(item => item.index)
        .Select(item => item.edge)
        .ToArray();
}

/// <summary>
/// Compiles an authoring graph into explicit sequence/parallel runtime structure.
/// </summary>
/// <remarks>
/// Fork branches stop before their Join, and the continuation after that Join is appended exactly
/// once. This mirrors the frozen Unity compiler while keeping graph assets and engine objects out of Core.
/// </remarks>
public static class PresentationGraphCompiler
{
    public static PresentationExecutionPlan Compile(PresentationGraphDefinition graph, string cueId)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (string.IsNullOrWhiteSpace(cueId))
            throw new ArgumentException("Cue ID is required.", nameof(cueId));

        PresentationGraphNode? entry = graph.FindEntry(cueId);
        CompiledStep root = entry is null || !entry.Enabled
            ? new SequenceStep(Array.Empty<CompiledStep>())
            : CompileNext(graph, entry.NodeId, null, new HashSet<string>(StringComparer.Ordinal));
        return Materialize(graph.SchemaVersion, root);
    }

    private static CompiledStep CompileNext(
        PresentationGraphDefinition graph,
        string sourceNodeId,
        string? stopBeforeNodeId,
        HashSet<string> path)
    {
        PresentationGraphEdge? edge = graph.GetEdgesFrom(sourceNodeId).FirstOrDefault();
        if (edge is null)
            return new SequenceStep(Array.Empty<CompiledStep>());

        return CompileNode(graph, graph.FindNode(edge.TargetNodeId), stopBeforeNodeId, path);
    }

    private static CompiledStep CompileNode(
        PresentationGraphDefinition graph,
        PresentationGraphNode? node,
        string? stopBeforeNodeId,
        HashSet<string> path)
    {
        if (node is null || string.Equals(node.NodeId, stopBeforeNodeId, StringComparison.Ordinal) ||
            node.Kind == PresentationGraphNodeKind.Finish)
        {
            return new SequenceStep(Array.Empty<CompiledStep>());
        }

        if (!path.Add(node.NodeId))
            throw new InvalidOperationException($"Presentation plan encountered cycle at '{node.NodeId}'.");

        try
        {
            if (!node.Enabled)
                return CompileNext(graph, node.NodeId, stopBeforeNodeId, path);

            if (node.Kind == PresentationGraphNodeKind.Fork)
            {
                if (node.JoinNodeId is null || graph.FindNode(node.JoinNodeId)?.Kind != PresentationGraphNodeKind.Join)
                    throw new InvalidOperationException($"Fork '{node.NodeId}' must reference a Join node.");

                CompiledStep[] branches = graph.GetEdgesFrom(node.NodeId)
                    .Select(edge => CompileNode(
                        graph,
                        graph.FindNode(edge.TargetNodeId),
                        node.JoinNodeId,
                        new HashSet<string>(path, StringComparer.Ordinal)))
                    .ToArray();
                CompiledStep continuation = CompileNext(graph, node.JoinNodeId, stopBeforeNodeId, path);
                return Sequence(
                    new LeafStep(node),
                    new ParallelStep(node.NodeId, node.JoinNodeId, branches),
                    continuation);
            }

            return Sequence(
                new LeafStep(node),
                CompileNext(graph, node.NodeId, stopBeforeNodeId, path));
        }
        finally
        {
            path.Remove(node.NodeId);
        }
    }

    private static CompiledStep Sequence(params CompiledStep[] steps)
    {
        var children = new List<CompiledStep>();
        foreach (CompiledStep step in steps)
        {
            if (step is SequenceStep sequence)
                children.AddRange(sequence.Children);
            else
                children.Add(step);
        }

        return new SequenceStep(children);
    }

    private static PresentationExecutionPlan Materialize(int schemaVersion, CompiledStep root)
    {
        var nodes = new List<PresentationNode>();
        int sequenceIndex = 0;
        int parallelIndex = 0;

        string MaterializeStep(CompiledStep step)
        {
            switch (step)
            {
                case LeafStep leaf:
                    nodes.Add(new PresentationNode(
                        leaf.Node.NodeId,
                        leaf.Node.NodeTypeId,
                        PresentationNodeKind.Leaf,
                        required: leaf.Node.Required));
                    return leaf.Node.NodeId;
                case SequenceStep sequence:
                    {
                        string nodeId = $"__plan.sequence.{sequenceIndex++}";
                        string[] children = sequence.Children.Select(MaterializeStep).ToArray();
                        nodes.Add(new PresentationNode(nodeId, "sequence", PresentationNodeKind.Sequence, children));
                        return nodeId;
                    }
                case ParallelStep parallel:
                    {
                        string nodeId = $"__plan.parallel.{parallelIndex++}";
                        string[] branches = parallel.Branches.Select(MaterializeStep).ToArray();
                        nodes.Add(new PresentationNode(
                            nodeId,
                            "parallel",
                            PresentationNodeKind.Parallel,
                            branches,
                            forkNodeId: parallel.ForkNodeId,
                            joinNodeId: parallel.JoinNodeId));
                        return nodeId;
                    }
                default:
                    throw new InvalidOperationException($"Unsupported compiled presentation step '{step.GetType().Name}'.");
            }
        }

        string rootNodeId = MaterializeStep(root);
        var plan = new PresentationExecutionPlan(schemaVersion, rootNodeId, nodes);
        plan.Validate();
        return plan;
    }

    private abstract record CompiledStep;
    private sealed record SequenceStep(IReadOnlyList<CompiledStep> Children) : CompiledStep;
    private sealed record ParallelStep(
        string ForkNodeId,
        string JoinNodeId,
        IReadOnlyList<CompiledStep> Branches) : CompiledStep;
    private sealed record LeafStep(PresentationGraphNode Node) : CompiledStep;
}
