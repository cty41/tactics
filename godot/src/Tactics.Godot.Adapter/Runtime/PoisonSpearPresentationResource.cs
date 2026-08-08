using Godot;
using Tactics.Core.Presentation;

namespace Tactics.Godot.Adapter.Runtime;

[GlobalClass]
public partial class PoisonSpearPresentationResource : Resource
{
    [Export] public string ContentIdValue { get; set; } = "presentation.poison-spear.lv1";
    [Export] public int SchemaVersion { get; set; } = 1;
    [Export] public string[] NodeIds { get; set; } = { "poison-spear.sequence", "poison-spear.projectile", "poison-spear.impact" };
    [Export] public string[] NodeTypes { get; set; } = { "sequence", "projectile.flight", "projectile.impact" };
    [Export] public string[] NodeChildren { get; set; } = { "poison-spear.projectile,poison-spear.impact", "", "" };
    [Export] public string ProjectileScenePath { get; set; } = "res://content/poison_spear/PoisonSpearProjectile.tscn";
    [Export] public string ImpactScenePath { get; set; } = "res://content/poison_spear/PoisonSpearImpact.tscn";
    [Export] public string PlanRootNodeId { get; set; } = "poison-spear.sequence";

    public PresentationExecutionPlan BuildExecutionPlan()
    {
        if (NodeIds.Length != NodeTypes.Length || NodeIds.Length != NodeChildren.Length)
            throw new InvalidOperationException("Poison Spear presentation arrays must have equal lengths.");

        var nodes = new List<PresentationNode>(NodeIds.Length);
        for (int index = 0; index < NodeIds.Length; index++)
        {
            string nodeType = NodeTypes[index];
            PresentationNodeKind kind = nodeType switch
            {
                "sequence" => PresentationNodeKind.Sequence,
                "parallel" => PresentationNodeKind.Parallel,
                "projectile.flight" or "projectile.impact" => PresentationNodeKind.Leaf,
                _ => throw new InvalidOperationException($"Unsupported Poison Spear presentation node type '{nodeType}'.")
            };
            IReadOnlyList<string> children = string.IsNullOrWhiteSpace(NodeChildren[index])
                ? Array.Empty<string>()
                : NodeChildren[index].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            nodes.Add(new PresentationNode(NodeIds[index], nodeType, kind, children));
        }

        var plan = new PresentationExecutionPlan(SchemaVersion, PlanRootNodeId, nodes);
        plan.Validate();
        return plan;
    }
}
