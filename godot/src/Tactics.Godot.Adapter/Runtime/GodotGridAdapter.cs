using Godot;
using Tactics.Core.Board;

namespace Tactics.Godot.Adapter.Runtime;

public sealed class GodotGridAdapter
{
    public GridPoint ToCore(Vector2I cell)
    {
        var point = new GridPoint(cell.X, cell.Y);
        if (!BoardSpec.Contains(point))
            throw new ArgumentOutOfRangeException(nameof(cell), $"Cell {cell} is outside the board.");
        return point;
    }

    public Vector2I ToGodot(GridPoint point)
    {
        if (!BoardSpec.Contains(point))
            throw new ArgumentOutOfRangeException(nameof(point));
        return new Vector2I(point.X, point.Y);
    }
}
