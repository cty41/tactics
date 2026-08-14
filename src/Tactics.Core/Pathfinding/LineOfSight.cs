using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Units;

namespace Tactics.Core.Pathfinding;

public enum LineOfSightBlockingKind { Terrain, LivingUnit, OccupyingInteraction, CornerSupercover }
public sealed record LineOfSightBlocker(LineOfSightBlockingKind Kind, UnitInstanceId? UnitId = null);
public sealed record LineOfSightResult(bool IsClear, IReadOnlyList<GridPoint> RayCells,
    GridPoint? BlockingCell = null, LineOfSightBlockingKind? BlockingKind = null,
    UnitInstanceId? BlockingUnitId = null);

public interface ILineOfSightService
{
    bool HasLineOfSight(BoardSnapshot board, GridPoint origin, GridPoint target,
        IReadOnlySet<GridPoint>? dynamicBlockers = null);
    LineOfSightResult Trace(BoardSnapshot board, GridPoint origin, GridPoint target,
        IReadOnlyDictionary<GridPoint, LineOfSightBlocker>? dynamicBlockers = null);
}

/// <summary>Supercover LOS. When a ray crosses a grid corner, either occupied orthogonal cell blocks it.</summary>
public sealed class SupercoverLineOfSight : ILineOfSightService
{
    public bool HasLineOfSight(BoardSnapshot board, GridPoint origin, GridPoint target,
        IReadOnlySet<GridPoint>? dynamicBlockers = null)
    {
        IReadOnlyDictionary<GridPoint, LineOfSightBlocker>? mapped = dynamicBlockers?.ToDictionary(
            point => point, _ => new LineOfSightBlocker(LineOfSightBlockingKind.LivingUnit));
        return Trace(board, origin, target, mapped).IsClear;
    }

    public LineOfSightResult Trace(BoardSnapshot board, GridPoint origin, GridPoint target,
        IReadOnlyDictionary<GridPoint, LineOfSightBlocker>? dynamicBlockers = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        if (!board.Contains(origin) || !board.Contains(target))
            return new LineOfSightResult(false, Array.Empty<GridPoint>(), target, LineOfSightBlockingKind.Terrain);
        if (origin == target)
            return new LineOfSightResult(true, Array.Empty<GridPoint>());

        var ray = new List<GridPoint>();
        GridPoint? firstBlockingCell = null;
        LineOfSightBlockingKind? firstBlockingKind = null;
        UnitInstanceId? firstBlockingUnit = null;
        int x = origin.X, y = origin.Y;
        int dx = target.X - x, dy = target.Y - y;
        int nx = Math.Abs(dx), ny = Math.Abs(dy);
        int signX = Math.Sign(dx), signY = Math.Sign(dy);
        int ix = 0, iy = 0;

        void Observe(GridPoint point, bool corner)
        {
            if (!ray.Contains(point)) ray.Add(point);
            if (firstBlockingCell is not null || point == target) return;
            LineOfSightBlocker? blocker = Blocker(board, point, dynamicBlockers);
            if (blocker is null) return;
            firstBlockingCell = point;
            firstBlockingKind = corner ? LineOfSightBlockingKind.CornerSupercover : blocker.Kind;
            firstBlockingUnit = blocker.UnitId;
        }

        while (ix < nx || iy < ny)
        {
            long horizontal = (1L + 2L * ix) * ny;
            long vertical = (1L + 2L * iy) * nx;
            if (horizontal == vertical)
            {
                Observe(new GridPoint(x + signX, y), true);
                Observe(new GridPoint(x, y + signY), true);
                x += signX; y += signY; ix++; iy++;
            }
            else if (horizontal < vertical) { x += signX; ix++; }
            else { y += signY; iy++; }
            Observe(new GridPoint(x, y), false);
        }

        return new LineOfSightResult(firstBlockingCell is null, ray, firstBlockingCell,
            firstBlockingKind, firstBlockingUnit);
    }

    private static LineOfSightBlocker? Blocker(BoardSnapshot board, GridPoint point,
        IReadOnlyDictionary<GridPoint, LineOfSightBlocker>? dynamicBlockers)
    {
        if (board.GetCell(point).IsLineBlocked)
            return new LineOfSightBlocker(LineOfSightBlockingKind.Terrain);
        return dynamicBlockers?.GetValueOrDefault(point);
    }
}
