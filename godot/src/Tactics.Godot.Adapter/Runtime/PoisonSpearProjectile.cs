using Godot;

namespace Tactics.Godot.Adapter.Runtime;

public partial class PoisonSpearProjectile : Node2D
{
    [Export] public float FlightSeconds { get; set; } = 0.25f;

    public override void _Ready() => QueueRedraw();

    public override void _Draw()
    {
        DrawLine(new Vector2(-7f, 0f), new Vector2(7f, 0f), new Color(0.25f, 0.9f, 0.7f), 3f);
        DrawLine(new Vector2(7f, 0f), new Vector2(3f, -3f), new Color(0.7f, 1f, 0.85f), 2f);
        DrawLine(new Vector2(7f, 0f), new Vector2(3f, 3f), new Color(0.7f, 1f, 0.85f), 2f);
    }
}
