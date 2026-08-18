namespace Tactics.Core.Runs;

public sealed record RunEscortTransition(bool Succeeded, string? RejectionCode, PureRunState State);

/// <summary>Owns the persistent, resolve-once lifecycle of a cross-node escort contract.</summary>
public sealed class PureRunEscortService
{
    public RunEscortTransition Accept(PureRunState state, string questId, string acceptedNodeId,
        string destinationNodeId)
    {
        if (state.EscortState is not null) return Reject(state, "escort.already_active");
        if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(acceptedNodeId) ||
            string.IsNullOrWhiteSpace(destinationNodeId) || acceptedNodeId == destinationNodeId)
            return Reject(state, "escort.contract_invalid");
        return Success(Copy(state, new RunEscortState(questId.Trim(), RunEscortLifecycle.Accepted, true,
            acceptedNodeId.Trim(), destinationNodeId.Trim(), state.Revision + 1)));
    }

    public RunEscortTransition BeginTravel(PureRunState state)
    {
        if (state.EscortState is not { Lifecycle: RunEscortLifecycle.Accepted } escort)
            return Reject(state, "escort.not_accepted");
        return Success(Copy(state, escort with
        {
            Lifecycle = RunEscortLifecycle.Traveling,
            Revision = state.Revision + 1
        }));
    }

    public RunEscortTransition BeginBattle(PureRunState state)
    {
        if (state.EscortState is not { Lifecycle: RunEscortLifecycle.Traveling, ProtectedNpcAlive: true } escort)
            return Reject(state, "escort.battle_unavailable");
        return Success(Copy(state, escort with
        {
            Lifecycle = RunEscortLifecycle.BattlePending,
            Revision = state.Revision + 1
        }));
    }

    public RunEscortTransition ResolveBattle(PureRunState state, bool enemiesDefeated, bool protectedNpcAlive)
    {
        if (state.EscortState is not { Lifecycle: RunEscortLifecycle.BattlePending } escort)
            return Reject(state, "escort.battle_not_pending");
        RunEscortLifecycle lifecycle = enemiesDefeated && protectedNpcAlive
            ? RunEscortLifecycle.Completed
            : RunEscortLifecycle.Failed;
        return Success(Copy(state, escort with
        {
            Lifecycle = lifecycle,
            ProtectedNpcAlive = protectedNpcAlive,
            Revision = state.Revision + 1
        }));
    }

    private static PureRunState Copy(PureRunState state, RunEscortState escort) => new(
        state.RunId, state.Seed, state.Revision + 1, state.Phase, state.EncounterIndex,
        state.EncounterContentId, state.Party, state.BackpackConsumables, state.BackpackEquipment,
        state.PendingProgression, state.AppliedTransactionKeys, state.Gold, state.BattlesCompleted,
        state.EnemiesDefeated, state.AcquiredItems, state.Checkpoint, state.MapState,
        state.NodeTransaction, escort, state.AdventureState);

    private static RunEscortTransition Success(PureRunState state) => new(true, null, state);
    private static RunEscortTransition Reject(PureRunState state, string code) => new(false, code, state);
}
