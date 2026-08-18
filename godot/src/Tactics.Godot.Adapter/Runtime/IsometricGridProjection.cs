using Godot;
using Tactics.Core.Board;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Shared 10x10 isometric projection for battle and adventure boards.</summary>
public static class IsometricGridProjection
{
    public const int GridSize = 10;
    public const float TileWidth = 96f;
    public const float TileHeight = 48f;
    public static readonly Vector2 FirstCellCenter = new(550f, 601f);

    public static Vector2 GridToScreen(GridPoint cell) => FirstCellCenter + new Vector2(
        (cell.X - cell.Y) * TileWidth * 0.5f,
        -(cell.X + cell.Y) * TileHeight * 0.5f);

    public static bool TryScreenToGrid(Vector2 screen, out GridPoint cell)
    {
        var candidates = new List<(GridPoint Cell, float Distance)>();
        for (int y = 0; y < GridSize; y++)
        for (int x = 0; x < GridSize; x++)
        {
            GridPoint candidate = new(x, y);
            Vector2 delta = screen - GridToScreen(candidate);
            float diamondDistance = MathF.Abs(delta.X) / (TileWidth * .5f) + MathF.Abs(delta.Y) / (TileHeight * .5f);
            if (diamondDistance <= 1.0001f) candidates.Add((candidate, delta.LengthSquared()));
        }
        if (candidates.Count == 0) { cell = default; return false; }
        cell = candidates.OrderBy(value => value.Distance).ThenBy(value => value.Cell.Y).ThenBy(value => value.Cell.X).First().Cell;
        return true;
    }

    public static Vector2[] Diamond(GridPoint cell)
    {
        Vector2 center = GridToScreen(cell);
        return
        [
            center + new Vector2(0, -TileHeight * .5f), center + new Vector2(TileWidth * .5f, 0),
            center + new Vector2(0, TileHeight * .5f), center + new Vector2(-TileWidth * .5f, 0)
        ];
    }
}
