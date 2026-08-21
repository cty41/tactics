namespace Tactics.Core.Board;

public enum TerrainKind { Ground, ShallowWater }
public enum MovementObstacleKind { None, Flyover, Absolute }

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
        float movementCost = 1f,
        TerrainKind terrain = TerrainKind.Ground,
        MovementObstacleKind obstacle = MovementObstacleKind.None)
    {
        if (movementCost <= 0 || movementCost != MathF.Truncate(movementCost))
            throw new ArgumentOutOfRangeException(nameof(movementCost));

        IsOccupied = isOccupied;
        BlocksMovement = blocksMovement || obstacle != MovementObstacleKind.None;
        BlocksLineOfSight = blocksLineOfSight;
        MovementCost = movementCost;
        Terrain = terrain;
        Obstacle = blocksMovement && obstacle == MovementObstacleKind.None
            ? MovementObstacleKind.Flyover
            : obstacle;
    }

    public bool IsOccupied { get; }
    public bool BlocksMovement { get; }
    public bool BlocksLineOfSight { get; }
    public float MovementCost { get; }
    public TerrainKind Terrain { get; }
    public MovementObstacleKind Obstacle { get; }

    public bool IsWalkable => !BlocksMovement && !IsOccupied;

    public bool IsLineBlocked => BlocksLineOfSight || IsOccupied;

    public bool CanTraverse(Units.UnitMovementKind movementKind) =>
        Obstacle != MovementObstacleKind.Absolute &&
        (movementKind == Units.UnitMovementKind.Air || (Obstacle == MovementObstacleKind.None && !IsOccupied));

    public bool CanStop(Units.UnitMovementKind movementKind) =>
        Obstacle == MovementObstacleKind.None && !IsOccupied;

    public int MovementPointCost(Units.UnitMovementKind movementKind) =>
        Terrain == TerrainKind.ShallowWater && movementKind == Units.UnitMovementKind.Land ? 2 : (int)MovementCost;
}
