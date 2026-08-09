namespace Tactics.Core.Board;

/// <summary>
/// Immutable board facts consumed by Core pathing and LOS services.
/// </summary>
public readonly struct CellState
{
    public CellState() : this(
        isOccupied: false,
        blocksMovement: false,
        blocksLineOfSight: false,
        movementCost: 1f)
    {
    }

    public CellState(
        bool isOccupied = false,
        bool blocksMovement = false,
        bool blocksLineOfSight = false,
        float movementCost = 1f)
    {
        if (movementCost <= 0)
            throw new ArgumentOutOfRangeException(nameof(movementCost));

        IsOccupied = isOccupied;
        BlocksMovement = blocksMovement;
        BlocksLineOfSight = blocksLineOfSight;
        MovementCost = movementCost;
    }

    public bool IsOccupied { get; }
    public bool BlocksMovement { get; }
    public bool BlocksLineOfSight { get; }
    public float MovementCost { get; }

    public bool IsWalkable => !BlocksMovement && !IsOccupied;

    public bool IsLineBlocked => BlocksLineOfSight || IsOccupied;
}
