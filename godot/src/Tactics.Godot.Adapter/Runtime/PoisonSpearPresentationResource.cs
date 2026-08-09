using Godot;
using Tactics.Core.Presentation;

namespace Tactics.Godot.Adapter.Runtime;

[GlobalClass]
public partial class PoisonSpearPresentationResource : Resource
{
    [Export] public string ContentIdValue { get; set; } = string.Empty;
    [Export] public int SchemaVersion { get; set; }
    [Export] public string Revision { get; set; } = string.Empty;
    [Export] public string[] NodeIds { get; set; } = Array.Empty<string>();
    [Export] public string[] NodeTypes { get; set; } = Array.Empty<string>();
    [Export] public string[] NodeChildren { get; set; } = Array.Empty<string>();
    [Export] public string[] AuthoringNodeIds { get; set; } = Array.Empty<string>();
    [Export] public string[] AuthoringNodeTypes { get; set; } = Array.Empty<string>();
    [Export] public string[] AuthoringNodeKinds { get; set; } = Array.Empty<string>();
    [Export] public string[] AuthoringNodeCues { get; set; } = Array.Empty<string>();
    [Export] public int[] AuthoringNodeEnabled { get; set; } = Array.Empty<int>();
    [Export] public Vector2[] AuthoringNodePositions { get; set; } = Array.Empty<Vector2>();
    [Export] public string[] EdgeIds { get; set; } = Array.Empty<string>();
    [Export] public string[] EdgeSources { get; set; } = Array.Empty<string>();
    [Export] public string[] EdgeTargets { get; set; } = Array.Empty<string>();
    [Export] public string ProjectileScenePath { get; set; } = string.Empty;
    [Export] public string ImpactScenePath { get; set; } = string.Empty;
    [Export] public string PlanRootNodeId { get; set; } = string.Empty;
    [Export] public float ProjectileSpeed { get; set; }
    [Export] public float FallbackTravelTime { get; set; }

    public PresentationExecutionPlan BuildExecutionPlan()
        => BuildExecutionPlan(SchemaVersion, PlanRootNodeId, NodeIds, NodeTypes, NodeChildren);

    public static PresentationExecutionPlan BuildExecutionPlan(
        int schemaVersion,
        string rootNodeId,
        IReadOnlyList<string> nodeIds,
        IReadOnlyList<string> nodeTypes,
        IReadOnlyList<string> nodeChildren)
    {
        if (nodeIds.Count != nodeTypes.Count || nodeIds.Count != nodeChildren.Count)
            throw new InvalidOperationException("Poison Spear presentation arrays must have equal lengths.");

        var nodes = new List<PresentationNode>(nodeIds.Count);
        for (int index = 0; index < nodeIds.Count; index++)
        {
            string nodeType = nodeTypes[index];
            PresentationNodeKind kind = nodeType switch
            {
                "sequence" => PresentationNodeKind.Sequence,
                "parallel" => PresentationNodeKind.Parallel,
                _ => PresentationNodeKind.Leaf
            };
            IReadOnlyList<string> children = string.IsNullOrWhiteSpace(nodeChildren[index])
                ? Array.Empty<string>()
                : nodeChildren[index].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            nodes.Add(new PresentationNode(nodeIds[index], nodeType, kind, children));
        }

        var plan = new PresentationExecutionPlan(schemaVersion, rootNodeId, nodes);
        plan.Validate();
        return plan;
    }

    public void ValidateAuthoringGraph()
    {
        if (AuthoringNodeIds.Length != AuthoringNodeTypes.Length ||
            AuthoringNodeIds.Length != AuthoringNodeKinds.Length ||
            AuthoringNodeIds.Length != AuthoringNodeCues.Length ||
            AuthoringNodeIds.Length != AuthoringNodeEnabled.Length ||
            AuthoringNodeIds.Length != AuthoringNodePositions.Length)
        {
            throw new InvalidOperationException("Poison Spear authoring node arrays must have equal lengths.");
        }
        if (EdgeIds.Length != EdgeSources.Length || EdgeIds.Length != EdgeTargets.Length)
            throw new InvalidOperationException("Poison Spear authoring edge arrays must have equal lengths.");
        var nodeIds = AuthoringNodeIds.ToHashSet(StringComparer.Ordinal);
        if (nodeIds.Count != AuthoringNodeIds.Length)
            throw new InvalidOperationException("Poison Spear authoring node IDs must be unique.");
        if (AuthoringNodePositions.Any(position => !float.IsFinite(position.X) || !float.IsFinite(position.Y)))
            throw new InvalidOperationException("Poison Spear authoring node positions must be finite.");
        if (AuthoringNodePositions.Distinct().Count() != AuthoringNodePositions.Length)
            throw new InvalidOperationException("Poison Spear authoring node positions must not overlap.");
        for (int index = 0; index < EdgeIds.Length; index++)
        {
            if (!nodeIds.Contains(EdgeSources[index]) || !nodeIds.Contains(EdgeTargets[index]))
                throw new InvalidOperationException($"Poison Spear edge '{EdgeIds[index]}' references a missing node.");
        }
    }
}
