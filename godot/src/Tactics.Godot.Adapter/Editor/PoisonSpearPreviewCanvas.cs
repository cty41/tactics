#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class PoisonSpearPreviewCanvas : Node2D
{
    private const float CellSize = 10f;
    private static readonly Color BoardColor = new(0.18f, 0.22f, 0.28f);
    private static readonly Color ProjectileColor = new(0.25f, 0.9f, 0.7f);
    private static readonly Color ImpactColor = new(0.95f, 0.55f, 0.2f);

    public override void _Ready() => QueueRedraw();

    public override void _Draw()
    {
        Vector2 origin = new(12f, 8f);
        for (int x = 0; x <= 10; x++)
        {
            Vector2 start = origin + new Vector2(x * CellSize, 0f);
            Vector2 end = origin + new Vector2(x * CellSize, 10f * CellSize);
            DrawLine(start, end, BoardColor, 1f);
        }
        for (int y = 0; y <= 10; y++)
        {
            Vector2 start = origin + new Vector2(0f, y * CellSize);
            Vector2 end = origin + new Vector2(10f * CellSize, y * CellSize);
            DrawLine(start, end, BoardColor, 1f);
        }

        Vector2 caster = origin + new Vector2(1.5f * CellSize, 1.5f * CellSize);
        Vector2 target = origin + new Vector2(3.5f * CellSize, 2.5f * CellSize);
        DrawLine(caster, target, ProjectileColor, 2f);
        DrawCircle(caster, 3f, ProjectileColor);
        DrawCircle(target, 5f, ImpactColor);
    }
}
#endif
