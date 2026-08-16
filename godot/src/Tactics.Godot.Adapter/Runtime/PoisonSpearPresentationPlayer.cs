using Godot;
using Tactics.Core.Presentation;
using Tactics.Core.Runtime;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>
/// Minimal runtime presentation bridge. It owns only transient nodes; Core owns the action result.
/// </summary>
public partial class PoisonSpearPresentationPlayer : Node2D
{
    public Task Start(Vector2 from, Vector2 to, PoisonSpearPresentationResource presentation, BattleRuntimeScope scope)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(scope);
        Task task = PlayAsync(from, to, presentation, scope.Token);
        scope.Track(task);
        return task;
    }

    private async Task PlayAsync(
        Vector2 from,
        Vector2 to,
        PoisonSpearPresentationResource presentation,
        CancellationToken cancellationToken)
    {
        PresentationExecutionPlan plan = presentation.BuildExecutionPlan();
        foreach (PresentationNode leaf in EnumerateLeaves(plan, plan.RootNodeId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (leaf.NodeTypeId)
            {
                case "unit.tween.ranged":
                    // Actor pose assets are a later content batch. The plan node is still consumed in order.
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    break;
                case "projectile.flight-impact":
                    await PlayProjectileAndImpactAsync(from, to, presentation, cancellationToken);
                    break;
                default:
                    if (leaf.Required)
                        throw new InvalidOperationException($"Unsupported required presentation leaf '{leaf.NodeTypeId}'.");
                    break;
            }
        }
    }

    private async Task PlayProjectileAndImpactAsync(
        Vector2 from,
        Vector2 to,
        PoisonSpearPresentationResource presentation,
        CancellationToken cancellationToken)
    {
        PackedScene projectileScene = ResourceLoader.Load<PackedScene>(presentation.ProjectileScenePath)
            ?? throw new InvalidOperationException($"Missing projectile scene '{presentation.ProjectileScenePath}'.");
        PackedScene impactScene = ResourceLoader.Load<PackedScene>(presentation.ImpactScenePath)
            ?? throw new InvalidOperationException($"Missing impact scene '{presentation.ImpactScenePath}'.");

        var projectile = projectileScene.Instantiate<PoisonSpearProjectile>();
        AddChild(projectile);
        projectile.Position = from;

        Tween flight = CreateTween();
        flight.TweenProperty(projectile, "position", to, projectile.FlightSeconds);
        await ToSignal(flight, Tween.SignalName.Finished);
        if (cancellationToken.IsCancellationRequested || !IsAliveInTree())
        {
            FreeIfValid(projectile);
            return;
        }

        FreeIfValid(projectile);
        var impact = impactScene.Instantiate<PoisonSpearImpact>();
        AddChild(impact);
        impact.Position = to;

        Tween tail = CreateTween();
        tail.TweenInterval(impact.TailSeconds);
        await ToSignal(tail, Tween.SignalName.Finished);
        if (!cancellationToken.IsCancellationRequested)
            FreeIfValid(impact);
    }

    private static IEnumerable<PresentationNode> EnumerateLeaves(
        PresentationExecutionPlan plan,
        string nodeId)
    {
        PresentationNode node = plan.Nodes[nodeId];
        if (node.Kind == PresentationNodeKind.Leaf)
        {
            yield return node;
            yield break;
        }

        foreach (string childId in node.Children)
        {
            foreach (PresentationNode leaf in EnumerateLeaves(plan, childId))
                yield return leaf;
        }
    }

    private bool IsAliveInTree() => GodotObject.IsInstanceValid(this) && IsInsideTree();

    private static void FreeIfValid(Node node)
    {
        if (GodotObject.IsInstanceValid(node))
            node.QueueFree();
    }
}
