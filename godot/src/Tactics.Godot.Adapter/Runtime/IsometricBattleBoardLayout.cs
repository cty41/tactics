using Godot;
using Tactics.Core.Board;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Canonical 1600x900 projection for the playable 10x10 battle board.</summary>
public static class IsometricBattleBoardLayout
{
    public const int GridSize = IsometricGridProjection.GridSize;
    public const float TileWidth = IsometricGridProjection.TileWidth;
    public const float TileHeight = IsometricGridProjection.TileHeight;
    public static readonly Vector2 TopCenter = new(550f, 145f);
    public static readonly Vector2 FirstCellCenter = IsometricGridProjection.FirstCellCenter;

    public static Vector2 GridToScreen(GridPoint cell) => IsometricGridProjection.GridToScreen(cell);

    public static bool TryScreenToGrid(Vector2 screen, out GridPoint cell)
    {
        return IsometricGridProjection.TryScreenToGrid(screen, out cell);
    }

    public static Vector2[] Diamond(GridPoint cell)
    {
        return IsometricGridProjection.Diamond(cell);
    }
}
