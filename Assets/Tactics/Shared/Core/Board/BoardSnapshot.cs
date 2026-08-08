using System.Collections.Generic;

namespace Tactics.Core.Board;

/// <summary>
/// Read-only local board snapshot. Adapters build this from Tilemap or Godot data.
/// </summary>
public sealed class BoardSnapshot
{
    private static readonly GridPoint[] Directions =
    {
        new(0, -1),
        new(-1, 0),
        new(1, 0),
        new(0, 1)
    };

    private readonly IReadOnlyDictionary<GridPoint, CellState> _cells;

    public BoardSnapshot(IReadOnlyDictionary<GridPoint, CellState> cells)
    {
        _cells = cells ?? throw new ArgumentNullException(nameof(cells));
    }

    public CellState GetCell(GridPoint point) =>
        _cells.TryGetValue(point, out CellState state) ? state : new CellState(blocksMovement: true, blocksLineOfSight: true);

    public bool Contains(GridPoint point) => BoardSpec.Contains(point) && _cells.ContainsKey(point);

    public IEnumerable<GridPoint> GetNeighbours(GridPoint point)
    {
        foreach (GridPoint direction in Directions)
        {
            GridPoint neighbour = new(point.X + direction.X, point.Y + direction.Y);
            if (Contains(neighbour))
                yield return neighbour;
        }
    }
}
