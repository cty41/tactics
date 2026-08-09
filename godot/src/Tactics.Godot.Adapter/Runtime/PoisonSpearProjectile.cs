using Godot;

namespace Tactics.Godot.Adapter.Runtime;

public partial class PoisonSpearProjectile : Node2D
{
    [Export] public float FlightSeconds { get; set; }
    [Export] public float SourceScale { get; set; }
    [Export] public float ArcHeight { get; set; }
    [Export] public Color Tint { get; set; }
    [Export] public bool RotateAlongTangent { get; set; }

    public override void _Ready() => QueueRedraw();

    public override void _Draw()
    {
        float length = 14f * SourceScale;
        DrawLine(new Vector2(-length / 2f, 0f), new Vector2(length / 2f, 0f), Tint, 3f);
        DrawLine(new Vector2(length / 2f, 0f), new Vector2(length / 2f - 4f, -3f), Tint.Lightened(0.35f), 2f);
        DrawLine(new Vector2(length / 2f, 0f), new Vector2(length / 2f - 4f, 3f), Tint.Lightened(0.35f), 2f);
    }
}
