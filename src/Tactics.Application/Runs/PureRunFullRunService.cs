using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Application.Runs;

public sealed record FullRunTransitionResult(bool Succeeded, string? RejectionCode, PureRunState State,
    EncounterRequest? EncounterRequest = null, PureRunSummary? TerminalSummary = null, bool WasDuplicate = false);

/// <summary>Coordinates the fixed elite and boss layers without bypassing battle settlement.</summary>
public sealed class PureRunFullRunService
{
    private readonly PureRunSettlementService _settlement;
    private readonly IReadOnlyList<ContentId> _dropPool;

    public PureRunFullRunService(IEnumerable<ContentId>? dropPool = null, PureRunSettlementService? settlement = null)
    {
        _settlement = settlement ?? new PureRunSettlementService();
        _dropPool = dropPool?.OrderBy(value => value.Value, StringComparer.Ordinal).ToArray() ?? Array.Empty<ContentId>();
    }

    public FullRunTransitionResult BeginLayerFive(PureRunState state, PureRunMapDefinition map)
    {
        if (state.Phase != PureRunPhase.ReadyForLayerFive || state.PendingProgression.Count != 0)
            return Fail("full_run.layer_five_unavailable", state);
        ContentId encounter = new PureRunMapService(map).SelectLateEncounter(state.Seed, "layer_05_battle");
        return BeginBattle(state, encounter, 4, PureRunPhase.PendingBattle);
    }

    public FullRunTransitionResult CompleteLayerFive(PureRunState state, PureRunBattleResult result)
    {
        PureRunSettlementResult settled = _settlement.ApplyFixedLateBattle(state, result, _dropPool,
            PureRunPhase.ReadyForLayerSix, boss: false);
        return FromSettlement(state, settled);
    }

    public FullRunTransitionResult UnlockLayerSix(PureRunState state, PureRunMapDefinition map)
    {
        if (state.Phase != PureRunPhase.ReadyForLayerSix || state.PendingProgression.Count != 0)
            return Fail("full_run.layer_six_unavailable", state);
        PureRunMapState prior = state.MapState ?? new PureRunMapService(map).UnlockLayerFour(state.Seed);
        PureRunMapState nextMap = new PureRunMapService(map).UnlockLayerSix(prior, state.Seed);
        return Success(Copy(state, PureRunPhase.AwaitingLayerSixChoice, map: nextMap));
    }

    public FullRunTransitionResult BeginBoss(PureRunState state, PureRunMapDefinition map)
    {
        if (state.Phase != PureRunPhase.ReadyForBoss || state.PendingProgression.Count != 0)
            return Fail("full_run.boss_unavailable", state);
        ContentId encounter = new PureRunMapService(map).SelectLateEncounter(state.Seed, "layer_07_battle", boss: true);
        return BeginBattle(state, encounter, 6, PureRunPhase.PendingBattle);
    }

    public FullRunTransitionResult CompleteBoss(PureRunState state, PureRunBattleResult result)
    {
        PureRunSettlementResult settled = _settlement.ApplyFixedLateBattle(state, result, _dropPool,
            PureRunPhase.SliceCompleted, boss: true);
        return FromSettlement(state, settled);
    }

    private static FullRunTransitionResult BeginBattle(PureRunState state, ContentId encounter, int index, PureRunPhase phase)
    {
        long revision = state.Revision + 1;
        var checkpoint = new RunEncounterCheckpoint(encounter, index, revision, state.Party.ToArray(),
            state.BackpackConsumables.ToArray(), state.BackpackEquipment.ToArray());
        PureRunState pending = Copy(state, phase, encounter, index, checkpoint: checkpoint);
        return new(true, null, pending, new EncounterRequest(state.RunId, revision, encounter, checkpoint.Party,
            pending.AdventureState?.Revision ?? 0));
    }

    private static FullRunTransitionResult FromSettlement(PureRunState prior, PureRunSettlementResult result)
    {
        if (!result.Succeeded) return Fail(result.RejectionCode ?? "full_run.settlement_failed", prior);
        if (result.WasDuplicate) return new(true, null, prior, WasDuplicate: true);
        if (result.TerminalSummary is PureRunSummary terminal)
        {
            // Terminal settlement has no ActiveRun, but the save envelope still
            // requires a strictly increasing revision. Preserve that committed
            // revision in the transition result instead of falling back to the
            // pre-settlement state and producing save.non_increasing_revision.
            PureRunPhase phase = terminal.Outcome == PureRunOutcome.BossVictory
                ? PureRunPhase.SliceCompleted
                : PureRunPhase.Defeated;
            return new(true, null, Copy(prior, phase), TerminalSummary: terminal);
        }
        return new(true, null, result.ActiveRun ?? prior);
    }

    private static FullRunTransitionResult Success(PureRunState state) => new(true, null, state);
    private static FullRunTransitionResult Fail(string code, PureRunState state) => new(false, code, state);

    private static PureRunState Copy(PureRunState value, PureRunPhase phase, ContentId? encounter = null,
        int? encounterIndex = null, RunEncounterCheckpoint? checkpoint = null, PureRunMapState? map = null) =>
        new(value.RunId, value.Seed, value.Revision + 1, phase, encounterIndex ?? value.EncounterIndex,
            encounter ?? value.EncounterContentId, value.Party, value.BackpackConsumables, value.BackpackEquipment,
            value.PendingProgression, value.AppliedTransactionKeys, value.Gold, value.BattlesCompleted,
            value.EnemiesDefeated, value.AcquiredItems, checkpoint, map ?? value.MapState, value.NodeTransaction,
            value.EscortState, value.AdventureState);
}
