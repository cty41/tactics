using Tactics.Core.Board;

namespace Tactics.Core.Pathfinding;

public interface IPathfinder
{
    IReadOnlyList<GridPoint> FindPath(
        BoardSnapshot board,
        GridPoint origin,
        GridPoint destination,
        bool allowOccupiedDestination = false);
}

/// <summary>
/// Deterministic four-neighbour A* matching the square-grid adapter contract.
/// The returned path excludes the origin and includes the destination.
/// </summary>
public sealed class AStarPathfinder : IPathfinder
{
    public IReadOnlyList<GridPoint> FindPath(
        BoardSnapshot board,
        GridPoint origin,
        GridPoint destination,
        bool allowOccupiedDestination = false)
    {
        ArgumentNullException.ThrowIfNull(board);

        if (!board.Contains(origin) || !board.Contains(destination))
            return Array.Empty<GridPoint>();
        if (origin == destination)
            return Array.Empty<GridPoint>();

        var frontier = new PriorityQueue<GridPoint, PathPriority>();
        var cameFrom = new Dictionary<GridPoint, GridPoint>();
        var costSoFar = new Dictionary<GridPoint, float> { [origin] = 0f };
        frontier.Enqueue(origin, new PathPriority(0f, origin));

        while (frontier.Count > 0)
        {
            GridPoint current = frontier.Dequeue();
            if (current == destination)
                break;

            foreach (GridPoint neighbour in board.GetNeighbours(current))
            {
                CellState cell = board.GetCell(neighbour);
                bool isDestination = neighbour == destination;
                if ((!cell.IsWalkable && !(allowOccupiedDestination && isDestination)) ||
                    cell.MovementCost <= 0)
                    continue;

                float nextCost = costSoFar[current] + cell.MovementCost;
                if (costSoFar.TryGetValue(neighbour, out float previousCost) && nextCost >= previousCost)
                    continue;

                costSoFar[neighbour] = nextCost;
                cameFrom[neighbour] = current;
                float priority = nextCost + ManhattanDistance(neighbour, destination);
                frontier.Enqueue(neighbour, new PathPriority(priority, neighbour));
            }
        }

        if (!cameFrom.ContainsKey(destination))
            return Array.Empty<GridPoint>();

        var path = new List<GridPoint>();
        GridPoint currentPoint = destination;
        while (currentPoint != origin)
        {
            path.Add(currentPoint);
            currentPoint = cameFrom[currentPoint];
        }

        path.Reverse();
        return path;
    }

    private static int ManhattanDistance(GridPoint left, GridPoint right) =>
        Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);

    private readonly record struct PathPriority(float Cost, GridPoint Point) : IComparable<PathPriority>
    {
        public int CompareTo(PathPriority other)
        {
            int costComparison = Cost.CompareTo(other.Cost);
            return costComparison != 0 ? costComparison : Point.CompareTo(other.Point);
        }
    }
}
