using Tactics.Core.Runs;

namespace Tactics.Application.Runs;

internal static class RunSaveNormalizer
{
    public static PureRunSaveSnapshot Normalize(PureRunSaveSnapshot snapshot)
    {
        if (snapshot.Revision < 0)
            throw new ArgumentOutOfRangeException(nameof(snapshot));
        PureRunState? active = snapshot.ActiveRun;
        if (active is not null && active.Revision != snapshot.Revision)
            throw new ArgumentException("Active run revision must equal envelope revision.", nameof(snapshot));
        return snapshot with
        {
            ActiveRun = active is null ? null : Normalize(active),
            TerminalSummary = snapshot.TerminalSummary is null ? null : Normalize(snapshot.TerminalSummary)
        };
    }

    private static PureRunState Normalize(PureRunState state) => new(
        state.RunId, state.Seed, state.Revision, state.Phase, state.EncounterIndex,
        state.EncounterContentId, state.Party, state.BackpackConsumables, state.BackpackEquipment,
        state.PendingProgression.OrderBy(value => value.TransactionKey, StringComparer.Ordinal).ToArray(),
        state.AppliedTransactionKeys.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        state.Gold, state.BattlesCompleted, state.EnemiesDefeated,
        state.AcquiredItems.OrderBy(value => value.Value, StringComparer.Ordinal).ToArray(), state.Checkpoint);

    private static PureRunSummary Normalize(PureRunSummary summary) => summary with
    {
        AcquiredItems = summary.AcquiredItems.OrderBy(value => value.Value, StringComparer.Ordinal).ToArray(),
        DeadCharacters = summary.DeadCharacters.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        AppliedTransactionKeys = summary.AppliedTransactionKeys.OrderBy(value => value, StringComparer.Ordinal).ToArray()
    };
}
