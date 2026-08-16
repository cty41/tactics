using Tactics.Core.Content;
using Tactics.Core.Units;

namespace Tactics.Core.Board;

/// <summary>
/// Immutable unit facts consumed by Core rules. Runtime counters stay outside content definitions.
/// </summary>
public readonly record struct UnitState
{
    public UnitState(
        UnitInstanceId instanceId,
        ContentId definitionId,
        GridPoint position,
        int moveRange,
        float initiative,
        int playerNumber,
        int spawnOrdinal,
        bool isAlive = true)
    {
        if (!float.IsFinite(initiative))
            throw new ArgumentOutOfRangeException(nameof(initiative));
        if (playerNumber < 0)
            throw new ArgumentOutOfRangeException(nameof(playerNumber));
        if (spawnOrdinal < 0)
            throw new ArgumentOutOfRangeException(nameof(spawnOrdinal));

        InstanceId = instanceId;
        DefinitionId = definitionId;
        Position = position;
        MoveRange = ValidateMoveRange(moveRange);
        Initiative = initiative;
        PlayerNumber = playerNumber;
        SpawnOrdinal = spawnOrdinal;
        IsAlive = isAlive;
    }

    public UnitInstanceId InstanceId { get; init; }
    public ContentId DefinitionId { get; init; }
    public GridPoint Position { get; init; }
    public int MoveRange { get; init; }
    public float Initiative { get; init; }
    public int PlayerNumber { get; init; }
    public int SpawnOrdinal { get; init; }
    public bool IsAlive { get; init; }

    private static int ValidateMoveRange(int moveRange) =>
        moveRange < 0 ? throw new ArgumentOutOfRangeException(nameof(moveRange)) : moveRange;
}
