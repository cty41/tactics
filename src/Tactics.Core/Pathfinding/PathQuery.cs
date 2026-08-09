namespace Tactics.Core.Pathfinding;

public readonly record struct PathQuery(
    Board.GridPoint Origin,
    Board.GridPoint Destination,
    bool AllowOccupiedDestination = false);

public readonly record struct LineOfSightQuery(
    Board.GridPoint Origin,
    Board.GridPoint Destination);
