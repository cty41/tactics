namespace Tactics.Core.Board;

public readonly record struct ObstacleState(
    GridPoint Position,
    bool BlocksMovement,
    bool BlocksLineOfSight);
