using Godot;

namespace Tactics.Godot.Adapter.Runtime;

public partial class PoisonSpearImpact : Node2D
{
    [Export] public float TailSeconds { get; set; } = 0.15f;

    public override void _Ready() => QueueRedraw();

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, 10f, new Color(0.95f, 0.55f, 0.2f, 0.35f));
        DrawArc(Vector2.Zero, 10f, 0f, Mathf.Tau, 24, new Color(1f, 0.8f, 0.35f), 2f);
        DrawCircle(Vector2.Zero, 3f, new Color(0.8f, 1f, 0.35f));
    }
}
