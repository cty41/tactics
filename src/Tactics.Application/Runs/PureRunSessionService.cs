using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Runs;

public sealed record PureRunSaveSnapshot(PureRunState? ActiveRun, PureRunSummary? TerminalSummary);

public sealed record RunStoreResult(bool Succeeded, string? ErrorCode, PureRunSaveSnapshot? Snapshot);

public interface IRunSaveStore
{
    RunStoreResult Load();
    RunStoreResult Save(PureRunSaveSnapshot snapshot, long expectedRevision);
}

public sealed record EncounterRequest(
    string RunId,
    long CheckpointRevision,
    ContentId EncounterContentId,
    IReadOnlyList<RunCharacterState> Party);

public sealed record RunSessionResult(
    bool Succeeded,
    string? ErrorCode,
    PureRunSaveSnapshot? Snapshot,
    EncounterRequest? EncounterRequest,
    bool WasDuplicate = false);

public sealed class PureRunSessionService
{
    private readonly PureRunDefinition _definition;
    private readonly IRunSaveStore _store;
    private readonly PureRunSettlementService _settlement;
    private readonly IReadOnlyList<ContentId> _dropPool;

    public PureRunSessionService(
        PureRunDefinition definition,
        IRunSaveStore store,
        IEnumerable<ContentId>? dropPool = null,
        PureRunSettlementService? settlement = null)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _settlement = settlement ?? new PureRunSettlementService();
        _dropPool = dropPool?.OrderBy(value => value.Value, StringComparer.Ordinal).ToArray() ?? Array.Empty<ContentId>();
    }

    public RunSessionResult StartNewRun(int seed)
    {
        RunStoreResult loaded = _store.Load();
        if (!loaded.Succeeded)
            return Fail(loaded.ErrorCode);
        long expected = loaded.Snapshot?.ActiveRun?.Revision ?? 0;
        string runId = $"run-{unchecked((uint)PureRunSettlementService.DeriveSeed(seed, "run-id")):x8}";
        RunCharacterState[] party = _definition.Party.Select(template => CreateCharacter(template)).ToArray();
        var run = new PureRunState(runId, seed, expected + 1, PureRunPhase.Ready, 0,
            _definition.Encounters[0], party);
        return Save(new PureRunSaveSnapshot(run, null), expected);
    }

    public RunSessionResult BeginEncounter()
    {
        RunStoreResult loaded = _store.Load();
        if (!loaded.Succeeded || loaded.Snapshot?.ActiveRun is not PureRunState run)
            return Fail(loaded.ErrorCode ?? "run.no_active_run");
        if (run.Phase != PureRunPhase.Ready)
            return Fail("run.not_ready", loaded.Snapshot);
        var checkpoint = new RunEncounterCheckpoint(
            run.EncounterContentId, run.EncounterIndex, run.Revision + 1,
            run.Party.ToArray(), run.BackpackConsumables.ToArray(), run.BackpackEquipment.ToArray());
        var pending = new PureRunState(
            run.RunId, run.Seed, run.Revision + 1, PureRunPhase.PendingBattle,
            run.EncounterIndex, run.EncounterContentId, run.Party, run.BackpackConsumables,
            run.BackpackEquipment, run.PendingProgression, run.AppliedTransactionKeys,
            run.Gold, run.BattlesCompleted, run.EnemiesDefeated, run.AcquiredItems, checkpoint);
        RunSessionResult saved = Save(new PureRunSaveSnapshot(pending, loaded.Snapshot.TerminalSummary), run.Revision);
        return saved.Succeeded ? saved with { EncounterRequest = CreateRequest(pending) } : saved;
    }

    public RunSessionResult ResumeRun()
    {
        RunStoreResult loaded = _store.Load();
        if (!loaded.Succeeded || loaded.Snapshot?.ActiveRun is not PureRunState run)
            return Fail(loaded.ErrorCode ?? "run.no_active_run", loaded.Snapshot);
        return run.Phase switch
        {
            PureRunPhase.Ready => new RunSessionResult(true, null, loaded.Snapshot, null),
            PureRunPhase.PendingBattle when run.Checkpoint is not null =>
                new RunSessionResult(true, null, loaded.Snapshot, CreateRequest(run)),
            _ => Fail("run.not_resumable", loaded.Snapshot)
        };
    }

    public RunSessionResult ApplyBattleResult(PureRunBattleResult battleResult)
    {
        RunStoreResult loaded = _store.Load();
        if (!loaded.Succeeded || loaded.Snapshot?.ActiveRun is not PureRunState run)
            return Fail(loaded.ErrorCode ?? "run.no_active_run", loaded.Snapshot);
        PureRunSettlementResult settlement = _settlement.Apply(_definition, run, battleResult, _dropPool);
        if (!settlement.Succeeded)
            return Fail(settlement.RejectionCode, loaded.Snapshot);
        if (settlement.WasDuplicate)
            return new RunSessionResult(true, null, loaded.Snapshot, null, true);
        var snapshot = new PureRunSaveSnapshot(settlement.ActiveRun, settlement.TerminalSummary);
        RunSessionResult saved = Save(snapshot, run.Revision);
        return saved with { WasDuplicate = false };
    }

    public RunSessionResult AbandonRun()
    {
        RunStoreResult loaded = _store.Load();
        if (!loaded.Succeeded || loaded.Snapshot?.ActiveRun is not PureRunState run)
            return Fail(loaded.ErrorCode ?? "run.no_active_run", loaded.Snapshot);
        return Save(new PureRunSaveSnapshot(null, _settlement.Abandon(run)), run.Revision);
    }

    public RunSessionResult ConsumeCompletedSummary()
    {
        RunStoreResult loaded = _store.Load();
        if (!loaded.Succeeded || loaded.Snapshot is null)
            return Fail(loaded.ErrorCode ?? "run.no_save");
        long revision = loaded.Snapshot.ActiveRun?.Revision ?? 0;
        return Save(new PureRunSaveSnapshot(loaded.Snapshot.ActiveRun, null), revision);
    }

    private RunSessionResult Save(PureRunSaveSnapshot snapshot, long expectedRevision)
    {
        RunStoreResult stored = _store.Save(snapshot, expectedRevision);
        return stored.Succeeded
            ? new RunSessionResult(true, null, stored.Snapshot, null)
            : Fail(stored.ErrorCode, stored.Snapshot);
    }

    private static EncounterRequest CreateRequest(PureRunState run) => new(
        run.RunId, run.Checkpoint!.Revision, run.EncounterContentId, run.Checkpoint.Party);

    private static RunCharacterState CreateCharacter(PureRunPartyTemplate template)
    {
        UnitDerivedStats stats = UnitDerivedStatRules.Calculate(template.Attributes, speed: 3f);
        return new RunCharacterState(
            template.CharacterId, template.UnitContentId, template.Level, template.Attributes,
            stats.MaxHealth, stats.MaxHealth, stats.StartingMana, stats.MaxMana, false,
            new[] { template.StartingSkillContentId });
    }

    private static RunSessionResult Fail(string? code, PureRunSaveSnapshot? snapshot = null) =>
        new(false, code ?? "run.store_failure", snapshot, null);
}
