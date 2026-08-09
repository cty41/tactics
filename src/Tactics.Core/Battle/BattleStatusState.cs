using Tactics.Core.Content;
using Tactics.Core.Units;

namespace Tactics.Core.Battle;

/// <summary>
/// Stores immutable runtime state for one status instance.
/// </summary>
/// <remarks>
/// The source and tick value are captured when the status is applied so later content reloads cannot
/// retroactively change an in-progress battle or replay.
/// </remarks>
public sealed record BattleStatusState
{
    public BattleStatusState(
        ContentId contentId,
        UnitInstanceId sourceId,
        int remainingTurns,
        int damagePerTurn)
    {
        if (remainingTurns <= 0)
            throw new ArgumentOutOfRangeException(nameof(remainingTurns));
        if (damagePerTurn < 0)
            throw new ArgumentOutOfRangeException(nameof(damagePerTurn));

        ContentId = contentId;
        SourceId = sourceId;
        RemainingTurns = remainingTurns;
        DamagePerTurn = damagePerTurn;
    }

    public ContentId ContentId { get; }
    public UnitInstanceId SourceId { get; }
    public int RemainingTurns { get; }
    public int DamagePerTurn { get; }

    public BattleStatusState WithRemainingTurns(int remainingTurns) =>
        new(ContentId, SourceId, remainingTurns, DamagePerTurn);
}
