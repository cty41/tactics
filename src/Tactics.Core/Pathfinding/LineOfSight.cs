using Tactics.Core.Board;

namespace Tactics.Core.Pathfinding;

public interface ILineOfSightService
{
    bool HasLineOfSight(BoardSnapshot board, GridPoint origin, GridPoint target, IReadOnlySet<GridPoint>? dynamicBlockers = null);
}

/// <summary>
/// Supercover LOS. When the ray crosses a grid corner, both orthogonal cells must be clear.
/// </summary>
public sealed class SupercoverLineOfSight : ILineOfSightService
{
    public bool HasLineOfSight(BoardSnapshot board, GridPoint origin, GridPoint target, IReadOnlySet<GridPoint>? dynamicBlockers = null)
    {
        ArgumentNullException.ThrowIfNull(board);

        if (!board.Contains(origin) || !board.Contains(target))
            return false;
        if (origin == target)
            return true;

        int x = origin.X;
        int y = origin.Y;
        int dx = target.X - x;
        int dy = target.Y - y;
        int nx = Math.Abs(dx);
        int ny = Math.Abs(dy);
        int signX = Math.Sign(dx);
        int signY = Math.Sign(dy);
        int ix = 0;
        int iy = 0;

        while (ix < nx || iy < ny)
        {
            long horizontal = (1L + 2L * ix) * ny;
            long vertical = (1L + 2L * iy) * nx;
            if (horizontal == vertical)
            {
                if (IsBlockingIntermediate(board, new GridPoint(x + signX, y), target, dynamicBlockers) ||
                    IsBlockingIntermediate(board, new GridPoint(x, y + signY), target, dynamicBlockers))
                    return false;

                x += signX;
                y += signY;
                ix++;
                iy++;
            }
            else if (horizontal < vertical)
            {
                x += signX;
                ix++;
            }
            else
            {
                y += signY;
                iy++;
            }

            if (x == target.X && y == target.Y)
                break;

            GridPoint current = new(x, y);
            if (IsBlockingIntermediate(board, current, target, dynamicBlockers))
                return false;
        }

        return true;
    }

    private static bool IsBlockingIntermediate(BoardSnapshot board, GridPoint point, GridPoint target, IReadOnlySet<GridPoint>? dynamicBlockers)
    {
        if (point == target)
            return false;

        return board.GetCell(point).IsLineBlocked || dynamicBlockers?.Contains(point) == true;
    }
}
