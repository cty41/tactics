using Tactics.Core.Board;

namespace Tactics.Core.Pathfinding;

public interface IPathfinder
{
    IReadOnlyList<GridPoint> FindPath(
        BoardSnapshot board,
        GridPoint origin,
        GridPoint destination,
        bool allowOccupiedDestination = false,
        Units.UnitMovementKind movementKind = Units.UnitMovementKind.Land);
}

/// <summary>
/// Deterministic four-neighbour Dijkstra pathfinding matching the frozen Unity movement contract.
/// </summary>
/// <remarks>
/// Neighbours are discovered right, left, up, then down by <see cref="BoardSnapshot"/>. Equal-cost
/// frontier entries use the same binary-heap comparisons as the final Unity runtime. The returned path
/// excludes the origin and includes the destination.
/// </remarks>
public sealed class DeterministicDijkstraPathfinder : IPathfinder
{
    public IReadOnlyList<GridPoint> FindPath(
        BoardSnapshot board,
        GridPoint origin,
        GridPoint destination,
        bool allowOccupiedDestination = false,
        Units.UnitMovementKind movementKind = Units.UnitMovementKind.Land)
    {
        ArgumentNullException.ThrowIfNull(board);

        if (!board.Contains(origin) || !board.Contains(destination))
            return Array.Empty<GridPoint>();
        if (origin == destination)
            return Array.Empty<GridPoint>();

        var frontier = new FrozenPriorityQueue(board.Cells.Count);
        var cameFrom = new Dictionary<GridPoint, GridPoint>();
        var costSoFar = new Dictionary<GridPoint, float> { [origin] = 0f };
        frontier.Enqueue(origin, 0f);

        while (frontier.Count != 0)
        {
            GridPoint current = frontier.Dequeue();
            if (current == destination)
                break;

            foreach (GridPoint neighbour in board.GetNeighbours(current))
            {
                CellState cell = board.GetCell(neighbour);
                bool isDestination = neighbour == destination;
                if (isDestination && (cell.Obstacle != MovementObstacleKind.None ||
                    (cell.IsOccupied && !allowOccupiedDestination)))
                    continue;
                if (!isDestination && !cell.CanTraverse(movementKind))
                    continue;

                float nextCost = costSoFar[current] + cell.MovementPointCost(movementKind);
                if (costSoFar.TryGetValue(neighbour, out float previousCost) && nextCost >= previousCost)
                    continue;

                costSoFar[neighbour] = nextCost;
                cameFrom[neighbour] = current;
                frontier.Enqueue(neighbour, nextCost);
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

    public static int MovementPointCost(BoardSnapshot board, IReadOnlyList<GridPoint> path,
        Units.UnitMovementKind movementKind) => path.Sum(point => board.GetCell(point).MovementPointCost(movementKind));

    /// <summary>
    /// Preserves the final Unity heap's equal-priority behavior instead of relying on an unspecified
    /// framework priority-queue tie order.
    /// </summary>
    private sealed class FrozenPriorityQueue
    {
        private readonly List<QueueItem> _queue;

        public FrozenPriorityQueue(int capacity) => _queue = new List<QueueItem>(capacity);

        public int Count => _queue.Count;

        public void Enqueue(GridPoint point, float priority)
        {
            _queue.Add(new QueueItem(point, priority));
            int childIndex = _queue.Count - 1;
            while (childIndex > 0)
            {
                int parentIndex = (childIndex - 1) / 2;
                if (_queue[childIndex].Priority >= _queue[parentIndex].Priority)
                    break;

                (_queue[parentIndex], _queue[childIndex]) = (_queue[childIndex], _queue[parentIndex]);
                childIndex = parentIndex;
            }
        }

        public GridPoint Dequeue()
        {
            int lastIndex = _queue.Count - 1;
            QueueItem front = _queue[0];
            _queue[0] = _queue[lastIndex];
            _queue.RemoveAt(lastIndex);

            lastIndex--;
            int parentIndex = 0;
            while (true)
            {
                int childIndex = (parentIndex * 2) + 1;
                if (childIndex > lastIndex)
                    break;

                int rightChildIndex = childIndex + 1;
                if (rightChildIndex <= lastIndex &&
                    _queue[rightChildIndex].Priority < _queue[childIndex].Priority)
                {
                    childIndex = rightChildIndex;
                }

                if (_queue[parentIndex].Priority <= _queue[childIndex].Priority)
                    break;

                (_queue[parentIndex], _queue[childIndex]) = (_queue[childIndex], _queue[parentIndex]);
                parentIndex = childIndex;
            }

            return front.Point;
        }

        private readonly record struct QueueItem(GridPoint Point, float Priority);
    }
}
