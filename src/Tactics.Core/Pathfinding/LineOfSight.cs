using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Units;

namespace Tactics.Core.Pathfinding;

/// <summary>Identifies the gameplay object that blocks a line-of-sight query.</summary>
public enum LineOfSightBlockingKind { Terrain, LivingUnit, OccupyingInteraction }

/// <summary>Describes one dynamic line-of-sight blocker.</summary>
/// <param name="Kind">The gameplay category of the blocker.</param>
/// <param name="UnitId">The blocking unit identity when <paramref name="Kind"/> is a living unit.</param>
public sealed record LineOfSightBlocker(LineOfSightBlockingKind Kind, UnitInstanceId? UnitId = null);

/// <summary>Contains the deterministic cells and first blocker observed by a line-of-sight query.</summary>
/// <param name="IsClear">Whether the target is visible from the origin.</param>
/// <param name="RayCells">Intermediate cells whose open interiors the center ray crosses, in traversal order.</param>
/// <param name="BlockingCell">The nearest blocking cell, if any.</param>
/// <param name="BlockingKind">The category of the nearest blocker, if any.</param>
/// <param name="BlockingUnitId">The nearest blocking unit identity, if any.</param>
public sealed record LineOfSightResult(bool IsClear, IReadOnlyList<GridPoint> RayCells,
    GridPoint? BlockingCell = null, LineOfSightBlockingKind? BlockingKind = null,
    UnitInstanceId? BlockingUnitId = null);

/// <summary>Provides the engine-neutral battle line-of-sight contract.</summary>
public interface ILineOfSightService
{
    /// <summary>Checks whether a target cell is visible.</summary>
    /// <param name="board">The immutable battle board.</param>
    /// <param name="origin">The observing cell.</param>
    /// <param name="target">The target cell.</param>
    /// <param name="dynamicBlockers">Optional occupied cells that cast sight shadows.</param>
    /// <returns><see langword="true"/> when no blocker shadows the target.</returns>
    bool HasLineOfSight(BoardSnapshot board, GridPoint origin, GridPoint target,
        IReadOnlySet<GridPoint>? dynamicBlockers = null);

    /// <summary>Traces the center ray and reports the nearest blocker.</summary>
    /// <param name="board">The immutable battle board.</param>
    /// <param name="origin">The observing cell.</param>
    /// <param name="target">The target cell.</param>
    /// <param name="dynamicBlockers">Optional occupied cells with structured blocker identities.</param>
    /// <returns>The ordered ray cells and nearest blocking diagnostic.</returns>
    LineOfSightResult Trace(BoardSnapshot board, GridPoint origin, GridPoint target,
        IReadOnlyDictionary<GridPoint, LineOfSightBlocker>? dynamicBlockers = null);
}

/// <summary>Projects each blocking cell's open interior into a deterministic shadow cone.</summary>
/// <remarks>
/// The ray is measured between cell centers. Crossing a blocker's open interior blocks sight, while
/// touching only an edge or corner remains clear. Rational comparisons keep this boundary stable.
/// </remarks>
public sealed class ShadowConeLineOfSight : ILineOfSightService
{
    /// <summary>The stable identifier for the current Godot battle sight contract.</summary>
    public const string ContractId = "godot-los-shadow-cone-v1";

    /// <inheritdoc />
    public bool HasLineOfSight(BoardSnapshot board, GridPoint origin, GridPoint target,
        IReadOnlySet<GridPoint>? dynamicBlockers = null)
    {
        IReadOnlyDictionary<GridPoint, LineOfSightBlocker>? mapped = dynamicBlockers?.ToDictionary(
            point => point, _ => new LineOfSightBlocker(LineOfSightBlockingKind.LivingUnit));
        return Trace(board, origin, target, mapped).IsClear;
    }

    /// <inheritdoc />
    public LineOfSightResult Trace(BoardSnapshot board, GridPoint origin, GridPoint target,
        IReadOnlyDictionary<GridPoint, LineOfSightBlocker>? dynamicBlockers = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        if (!board.Contains(origin) || !board.Contains(target))
            return new LineOfSightResult(false, Array.Empty<GridPoint>(), target, LineOfSightBlockingKind.Terrain);
        if (origin == target)
            return new LineOfSightResult(true, Array.Empty<GridPoint>());

        var crossed = board.Cells.Keys
            .Where(point => point != origin && point != target)
            .Select(point => (Point: point, Entry: OpenInteriorEntry(origin, target, point)))
            .Where(value => value.Entry is not null)
            .OrderBy(value => value.Entry!.Value)
            .ThenBy(value => value.Point.X)
            .ThenBy(value => value.Point.Y)
            .ToArray();

        foreach ((GridPoint point, _) in crossed)
        {
            LineOfSightBlocker? blocker = Blocker(board, point, dynamicBlockers);
            if (blocker is not null)
                return new LineOfSightResult(false, crossed.Select(value => value.Point).ToArray(),
                    point, blocker.Kind, blocker.UnitId);
        }

        return new LineOfSightResult(true, crossed.Select(value => value.Point).ToArray());
    }

    private static Fraction? OpenInteriorEntry(GridPoint origin, GridPoint target, GridPoint cell)
    {
        // Coordinates are doubled so every cell edge is integral. The open interval deliberately
        // rejects a ray that merely touches an edge or corner of the projected blocking square.
        long originX = 2L * origin.X;
        long originY = 2L * origin.Y;
        long deltaX = 2L * (target.X - origin.X);
        long deltaY = 2L * (target.Y - origin.Y);
        Fraction lower = Fraction.Zero;
        Fraction upper = Fraction.One;

        if (!IntersectOpenAxis(originX, deltaX, 2L * cell.X - 1L, 2L * cell.X + 1L, ref lower, ref upper) ||
            !IntersectOpenAxis(originY, deltaY, 2L * cell.Y - 1L, 2L * cell.Y + 1L, ref lower, ref upper))
            return null;

        return lower < upper ? lower : null;
    }

    private static bool IntersectOpenAxis(long origin, long delta, long minimum, long maximum,
        ref Fraction lower, ref Fraction upper)
    {
        if (delta == 0)
            return minimum < origin && origin < maximum;

        Fraction first = Fraction.Create(minimum - origin, delta);
        Fraction second = Fraction.Create(maximum - origin, delta);
        if (second < first) (first, second) = (second, first);
        if (lower < first) lower = first;
        if (second < upper) upper = second;
        return lower < upper;
    }

    private static LineOfSightBlocker? Blocker(BoardSnapshot board, GridPoint point,
        IReadOnlyDictionary<GridPoint, LineOfSightBlocker>? dynamicBlockers)
    {
        if (board.GetCell(point).IsLineBlocked)
            return new LineOfSightBlocker(LineOfSightBlockingKind.Terrain);
        return dynamicBlockers?.GetValueOrDefault(point);
    }

    private readonly record struct Fraction(long Numerator, long Denominator) : IComparable<Fraction>
    {
        public static Fraction Zero { get; } = new(0, 1);
        public static Fraction One { get; } = new(1, 1);

        public static Fraction Create(long numerator, long denominator) => denominator < 0
            ? new Fraction(-numerator, -denominator)
            : new Fraction(numerator, denominator);

        public int CompareTo(Fraction other) => (Numerator * other.Denominator)
            .CompareTo(other.Numerator * Denominator);

        public static bool operator <(Fraction left, Fraction right) => left.CompareTo(right) < 0;
        public static bool operator >(Fraction left, Fraction right) => left.CompareTo(right) > 0;
    }
}
