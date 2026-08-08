namespace Tactics.Core.Turns;

/// <summary>
/// One movement use per turn. No remaining movement points are carried or deducted.
/// </summary>
public sealed class MovementActionState
{
    public MovementActionState(int moveRange)
    {
        if (moveRange < 0)
            throw new ArgumentOutOfRangeException(nameof(moveRange));

        MoveRange = moveRange;
    }

    public int MoveRange { get; private set; }
    public bool HasMovedThisTurn { get; private set; }

    public bool CanUseMove(int pathLength) =>
        !HasMovedThisTurn && pathLength >= 0 && pathLength <= MoveRange;

    public bool TryUseMove(int pathLength)
    {
        if (!CanUseMove(pathLength))
            return false;

        HasMovedThisTurn = true;
        return true;
    }

    public void PrepareForTurn() => HasMovedThisTurn = false;

    public void ResetMoveUse() => HasMovedThisTurn = false;

    public void SetMoveRange(int moveRange)
    {
        if (moveRange < 0)
            throw new ArgumentOutOfRangeException(nameof(moveRange));

        MoveRange = moveRange;
    }
}
