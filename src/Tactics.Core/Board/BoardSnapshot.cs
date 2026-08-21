using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Tactics.Core.Board;

/// <summary>
/// Read-only local board snapshot. Adapters build this from Tilemap or Godot data.
/// </summary>
public sealed class BoardSnapshot
{
    private static readonly GridPoint[] Directions =
    {
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1)
    };

    private readonly IReadOnlyDictionary<GridPoint, CellState> _cells;

    public BoardSnapshot(IReadOnlyDictionary<GridPoint, CellState> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        _cells = new ReadOnlyDictionary<GridPoint, CellState>(new Dictionary<GridPoint, CellState>(cells));
    }

    /// <summary>
    /// Gets the immutable cell map used by path, line-of-sight, and transition rules.
    /// </summary>
    public IReadOnlyDictionary<GridPoint, CellState> Cells => _cells;

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

    /// <summary>
    /// Creates a new board with the supplied cells marked as occupied.
    /// </summary>
    /// <param name="occupiedCells">Cells occupied by live battle units.</param>
    /// <returns>A defensive board copy containing the requested occupancy overlay.</returns>
    public BoardSnapshot WithOccupancy(IEnumerable<GridPoint> occupiedCells)
    {
        ArgumentNullException.ThrowIfNull(occupiedCells);

        var cells = new Dictionary<GridPoint, CellState>(_cells);
        foreach (GridPoint point in occupiedCells)
        {
            if (!cells.TryGetValue(point, out CellState cell))
                continue;

            cells[point] = new CellState(
                isOccupied: true,
                blocksMovement: cell.BlocksMovement,
                blocksLineOfSight: cell.BlocksLineOfSight,
                movementCost: cell.MovementCost,
                terrain: cell.Terrain,
                obstacle: cell.Obstacle);
        }

        return new BoardSnapshot(cells);
    }
}
