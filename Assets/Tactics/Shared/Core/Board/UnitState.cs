using Tactics.Core.Content;

namespace Tactics.Core.Board;

/// <summary>
/// Immutable unit facts consumed by Core rules. Runtime counters stay outside content definitions.
/// </summary>
public readonly record struct UnitState
{
    public UnitState(ContentId unitId, GridPoint position, int moveRange, int initiative, bool isAlive = true)
    {
        UnitId = unitId;
        Position = position;
        MoveRange = ValidateMoveRange(moveRange);
        Initiative = initiative;
        IsAlive = isAlive;
    }

    public ContentId UnitId { get; init; }
    public GridPoint Position { get; init; }
    public int MoveRange { get; init; }
    public int Initiative { get; init; }
    public bool IsAlive { get; init; }

    private static int ValidateMoveRange(int moveRange) =>
        moveRange < 0 ? throw new ArgumentOutOfRangeException(nameof(moveRange)) : moveRange;
}
