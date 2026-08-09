using Godot;

namespace Tactics.Godot.Adapter.Runtime;

public partial class PoisonSpearImpact : Node2D
{
    [Export] public float TailSeconds { get; set; }
    [Export] public float SourceScale { get; set; }
    [Export] public Color Tint { get; set; }

    public override void _Ready() => QueueRedraw();

    public override void _Draw()
    {
        float radius = 18f * SourceScale;
        DrawCircle(Vector2.Zero, radius, new Color(Tint.R, Tint.G, Tint.B, 0.35f));
        DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 24, Tint.Lightened(0.25f), 2f);
        DrawCircle(Vector2.Zero, Math.Max(2f, radius * 0.3f), Tint);
    }
}
