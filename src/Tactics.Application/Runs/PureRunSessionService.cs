using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Runs;

public sealed record PureRunSaveSnapshot(
    long Revision,
    PureRunState? ActiveRun,
    PureRunSummary? TerminalSummary,
    PendingRunSetup? PendingRunSetup = null);

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
    bool WasDuplicate = false,
    IReadOnlyList<string>? Diagnostics = null);

public sealed class PureRunSessionService
{
    private readonly PureRunDefinition _definition;
    private readonly IRunSaveStore _store;
    private readonly PureRunSettlementService _settlement;
    private readonly IReadOnlyList<ContentId> _dropPool;
    private readonly PureRunMapDefinition? _mapDefinition;

    public PureRunSessionService(
        PureRunDefinition definition,
        IRunSaveStore store,
        IEnumerable<ContentId>? dropPool = null,
        PureRunSettlementService? settlement = null,
        PureRunMapDefinition? mapDefinition = null)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _settlement = settlement ?? new PureRunSettlementService();
        _dropPool = dropPool?.OrderBy(value => value.Value, StringComparer.Ordinal).ToArray() ?? Array.Empty<ContentId>();
        if (mapDefinition is not null && definition.LayerFourMapContentId != mapDefinition.ContentId)
            throw new ArgumentException("Layer Four map does not match the Run Definition.", nameof(mapDefinition));
        _mapDefinition = mapDefinition;
    }

    public RunSessionResult StartNewRun(int seed)
    {
        RunStoreResult loaded = _store.Load();
        if (!loaded.Succeeded)
            return Fail(loaded.ErrorCode);
        if (HasPendingSetup(loaded)) return Fail("run_setup.pending", loaded.Snapshot);
        long expected = loaded.Snapshot?.Revision ?? 0;
        string runId = $"run-{unchecked((uint)PureRunSettlementService.DeriveSeed(seed, "run-id")):x8}";
        RunCharacterState[] party = _definition.Party.Select(template => CreateCharacter(template)).ToArray();
        var run = new PureRunState(runId, seed, expected + 1, PureRunPhase.Ready, 0,
            _definition.Encounters[0], party);
        return Save(new PureRunSaveSnapshot(run.Revision, run, null), expected);
    }

    public RunSessionResult BeginNewRunSetup(int seed)
    {
        RunStoreResult loaded = _store.Load();
        if (!loaded.Succeeded) return Fail(loaded.ErrorCode, loaded.Snapshot);
        if (HasPendingSetup(loaded)) return Fail("run_setup.pending", loaded.Snapshot);
        long expected = loaded.Snapshot?.Revision ?? 0;
        long revision = expected + 1;
        PureRunState? preserved = loaded.Snapshot?.ActiveRun is null
            ? null
            : CopyRevision(loaded.Snapshot.ActiveRun, revision);
        var setup = new PendingRunSetup(seed, 0, Array.Empty<PendingRunStartingSkillChoice>())
        {
            CurrentCharacterId = _definition.Party[0].CharacterId
        };
        return Save(new PureRunSaveSnapshot(revision, preserved, loaded.Snapshot?.TerminalSummary, setup), expected);
    }

    public RunSessionResult ChooseStartingSkill(string characterId, ContentId skillContentId)
    {
        RunStoreResult loaded = _store.Load();
        if (!loaded.Succeeded || loaded.Snapshot?.PendingRunSetup is not PendingRunSetup setup)
            return Fail(loaded.ErrorCode ?? "run_setup.not_pending", loaded.Snapshot);
        if (setup.CurrentCharacterIndex >= _definition.Party.Count)
            return Fail("run_setup.complete", loaded.Snapshot);
        PureRunPartyTemplate template = _definition.Party[setup.CurrentCharacterIndex];
        if (!string.Equals(template.CharacterId, characterId, StringComparison.Ordinal))
            return Fail("run_setup.character_out_of_order", loaded.Snapshot);
        if (!template.EffectiveStartingSkillChoices.Contains(skillContentId))
            return Fail("run_setup.skill_not_offered", loaded.Snapshot);

        PendingRunStartingSkillChoice[] choices = setup.Choices
            .Append(new PendingRunStartingSkillChoice(characterId, skillContentId)).ToArray();
        long revision = loaded.Snapshot.Revision + 1;
        if (choices.Length == _definition.Party.Count)
        {
            string runId = $"run-{unchecked((uint)PureRunSettlementService.DeriveSeed(setup.Seed, "run-id")):x8}";
            RunCharacterState[] party = _definition.Party.Select((value, index) =>
                CreateCharacter(value, choices[index].SkillContentId)).ToArray();
            var run = new PureRunState(runId, setup.Seed, revision, PureRunPhase.Ready, 0,
                _definition.Encounters[0], party);
            return Save(new PureRunSaveSnapshot(revision, run, null), loaded.Snapshot.Revision);
        }

        PureRunState? preserved = loaded.Snapshot.ActiveRun is null
            ? null
            : CopyRevision(loaded.Snapshot.ActiveRun, revision);
        var pending = new PendingRunSetup(setup.Seed, choices.Length, choices)
        {
            CurrentCharacterId = _definition.Party[choices.Length].CharacterId
        };
        return Save(new PureRunSaveSnapshot(revision, preserved, loaded.Snapshot.TerminalSummary, pending),
            loaded.Snapshot.Revision);
    }

    public RunSessionResult CancelNewRunSetup()
    {
        RunStoreResult loaded = _store.Load();
        if (!loaded.Succeeded || loaded.Snapshot?.PendingRunSetup is null)
            return Fail(loaded.ErrorCode ?? "run_setup.not_pending", loaded.Snapshot);
        long revision = loaded.Snapshot.Revision + 1;
        PureRunState? restored = loaded.Snapshot.ActiveRun is null
            ? null
            : CopyRevision(loaded.Snapshot.ActiveRun, revision);
        return Save(new PureRunSaveSnapshot(revision, restored, loaded.Snapshot.TerminalSummary), loaded.Snapshot.Revision);
    }

    public RunSessionResult BeginEncounter()
    {
        RunStoreResult loaded = LoadWithAttributeRepair(out string[] diagnostics);
        if (HasPendingSetup(loaded)) return Fail("run_setup.pending", loaded.Snapshot);
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
            run.Gold, run.BattlesCompleted, run.EnemiesDefeated, run.AcquiredItems, checkpoint,
            run.MapState, run.NodeTransaction);
        RunSessionResult saved = Save(new PureRunSaveSnapshot(pending.Revision, pending, loaded.Snapshot.TerminalSummary), run.Revision);
        return saved.Succeeded ? saved with { EncounterRequest = CreateRequest(pending), Diagnostics = diagnostics } : saved;
    }

    public RunSessionResult ResumeRun()
    {
        RunStoreResult loaded = LoadWithAttributeRepair(out string[] diagnostics);
        if (HasPendingSetup(loaded)) return Fail("run_setup.pending", loaded.Snapshot);
        if (!loaded.Succeeded || loaded.Snapshot?.ActiveRun is not PureRunState run)
            return Fail(loaded.ErrorCode ?? "run.no_active_run", loaded.Snapshot);
        return run.Phase switch
        {
            PureRunPhase.Ready => new RunSessionResult(true, null, loaded.Snapshot, null, Diagnostics: diagnostics),
            PureRunPhase.PendingBattle when run.Checkpoint is not null =>
                new RunSessionResult(true, null, loaded.Snapshot, CreateRequest(run), Diagnostics: diagnostics),
            PureRunPhase.AwaitingLayerFourChoice or PureRunPhase.ResolvingLayerFourNode or PureRunPhase.ReadyForLayerFive or
            PureRunPhase.ReadyForLayerSix or PureRunPhase.AwaitingLayerSixChoice or PureRunPhase.ResolvingLayerSixNode or PureRunPhase.ReadyForBoss =>
                new RunSessionResult(true, null, loaded.Snapshot, null, Diagnostics: diagnostics),
            _ => Fail("run.not_resumable", loaded.Snapshot)
        };
    }

    public RunSessionResult ApplyBattleResult(PureRunBattleResult battleResult)
    {
        RunStoreResult loaded = LoadWithAttributeRepair(out string[] diagnostics);
        if (HasPendingSetup(loaded)) return Fail("run_setup.pending", loaded.Snapshot);
        if (!loaded.Succeeded || loaded.Snapshot is null)
            return Fail(loaded.ErrorCode ?? "run.no_save", loaded.Snapshot);
        if (loaded.Snapshot.ActiveRun is not PureRunState run)
        {
            string transactionKey = $"battle:{battleResult.EncounterContentId.Value}:settlement";
            PureRunSummary? summary = loaded.Snapshot.TerminalSummary;
            if (summary is not null &&
                string.Equals(summary.RunId, battleResult.RunId, StringComparison.Ordinal) &&
                summary.AppliedTransactionKeys.Contains(transactionKey, StringComparer.Ordinal))
            {
                return new RunSessionResult(true, null, loaded.Snapshot, null, true);
            }
            return Fail("run.no_active_run", loaded.Snapshot);
        }
        PureRunSettlementResult settlement = _settlement.Apply(_definition, run, battleResult, _dropPool, _mapDefinition);
        if (!settlement.Succeeded)
            return Fail(settlement.RejectionCode, loaded.Snapshot);
        if (settlement.WasDuplicate)
            return new RunSessionResult(true, null, loaded.Snapshot, null, true);
        long nextRevision = settlement.ActiveRun?.Revision ?? run.Revision + 1;
        var snapshot = new PureRunSaveSnapshot(nextRevision, settlement.ActiveRun, settlement.TerminalSummary);
        RunSessionResult saved = Save(snapshot, run.Revision);
        return saved with { WasDuplicate = false, Diagnostics = diagnostics };
    }

    public RunSessionResult ApplyLayerFourBattleResult(PureRunBattleResult battleResult)
    {
        RunStoreResult loaded = LoadWithAttributeRepair(out string[] diagnostics);
        if (HasPendingSetup(loaded)) return Fail("run_setup.pending", loaded.Snapshot);
        if (!loaded.Succeeded || loaded.Snapshot?.ActiveRun is not PureRunState run)
            return Fail(loaded.ErrorCode ?? "run.no_active_run", loaded.Snapshot);
        PureRunSettlementResult settlement = _settlement.ApplyLayerFour(run, battleResult, _dropPool);
        if (!settlement.Succeeded) return Fail(settlement.RejectionCode, loaded.Snapshot);
        if (settlement.WasDuplicate) return new(true, null, loaded.Snapshot, null, true, diagnostics);
        long revision = settlement.ActiveRun?.Revision ?? run.Revision + 1;
        return Save(new PureRunSaveSnapshot(revision, settlement.ActiveRun, settlement.TerminalSummary), run.Revision)
            with { Diagnostics = diagnostics };
    }

    public RunSessionResult AbandonRun()
    {
        RunStoreResult loaded = LoadWithAttributeRepair(out string[] diagnostics);
        if (HasPendingSetup(loaded)) return Fail("run_setup.pending", loaded.Snapshot);
        if (!loaded.Succeeded || loaded.Snapshot?.ActiveRun is not PureRunState run)
            return Fail(loaded.ErrorCode ?? "run.no_active_run", loaded.Snapshot);
        return Save(new PureRunSaveSnapshot(run.Revision + 1, null, _settlement.Abandon(run)), run.Revision) with { Diagnostics = diagnostics };
    }

    public RunSessionResult ConsumeCompletedSummary()
    {
        RunStoreResult loaded = _store.Load();
        if (HasPendingSetup(loaded)) return Fail("run_setup.pending", loaded.Snapshot);
        if (!loaded.Succeeded || loaded.Snapshot is null)
            return Fail(loaded.ErrorCode ?? "run.no_save");
        long revision = loaded.Snapshot.Revision;
        return Save(new PureRunSaveSnapshot(revision + 1, loaded.Snapshot.ActiveRun, null), revision);
    }

    public RunSessionResult ApplyMutation(Func<PureRunState, RunMutationResult> mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        RunStoreResult loaded = LoadWithAttributeRepair(out string[] diagnostics);
        if (HasPendingSetup(loaded)) return Fail("run_setup.pending", loaded.Snapshot);
        if (!loaded.Succeeded || loaded.Snapshot?.ActiveRun is not PureRunState run)
            return Fail(loaded.ErrorCode ?? "run.no_active_run", loaded.Snapshot);
        RunMutationResult result = mutation(run);
        if (!result.Succeeded) return Fail(result.RejectionCode, loaded.Snapshot);
        return Save(new PureRunSaveSnapshot(result.State.Revision, result.State, loaded.Snapshot.TerminalSummary), run.Revision) with { Diagnostics = diagnostics };
    }

    public RunSessionResult ApplyLayerFourMutation(Func<PureRunState, RunMutationResult> mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        RunStoreResult loaded = LoadWithAttributeRepair(out string[] diagnostics);
        if (HasPendingSetup(loaded)) return Fail("run_setup.pending", loaded.Snapshot);
        if (!loaded.Succeeded || loaded.Snapshot?.ActiveRun is not PureRunState run)
            return Fail(loaded.ErrorCode ?? "run.no_active_run", loaded.Snapshot);
        RunMutationResult result = mutation(run);
        if (!result.Succeeded) return Fail(result.RejectionCode, loaded.Snapshot);
        PureRunSaveSnapshot snapshot = result.State.Phase == PureRunPhase.Defeated
            ? new PureRunSaveSnapshot(result.State.Revision, null, _settlement.DefeatOutsideBattle(result.State))
            : new PureRunSaveSnapshot(result.State.Revision, result.State, loaded.Snapshot.TerminalSummary);
        return Save(snapshot, run.Revision) with { Diagnostics = diagnostics };
    }

    public RunSessionResult ApplyFullRunTransition(Func<PureRunState, FullRunTransitionResult> transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        RunStoreResult loaded = LoadWithAttributeRepair(out string[] diagnostics);
        if (HasPendingSetup(loaded)) return Fail("run_setup.pending", loaded.Snapshot);
        if (!loaded.Succeeded || loaded.Snapshot?.ActiveRun is not PureRunState run)
            return Fail(loaded.ErrorCode ?? "run.no_active_run", loaded.Snapshot);
        FullRunTransitionResult result = transition(run);
        if (!result.Succeeded) return Fail(result.RejectionCode, loaded.Snapshot);
        PureRunSaveSnapshot snapshot = result.TerminalSummary is null
            ? new PureRunSaveSnapshot(result.State.Revision, result.State, loaded.Snapshot.TerminalSummary)
            : new PureRunSaveSnapshot(result.State.Revision, null, result.TerminalSummary);
        RunSessionResult saved = Save(snapshot, run.Revision);
        return saved with { EncounterRequest = result.EncounterRequest, WasDuplicate = result.WasDuplicate, Diagnostics = diagnostics };
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

    private static RunCharacterState CreateCharacter(PureRunPartyTemplate template) =>
        CreateCharacter(template, template.StartingSkillContentId);

    private static RunCharacterState CreateCharacter(PureRunPartyTemplate template, ContentId startingSkill)
    {
        UnitDerivedStats stats = UnitDerivedStatRules.Calculate(template.Attributes, speed: 3f);
        return new RunCharacterState(
            template.CharacterId, template.UnitContentId, template.Level, template.Attributes,
            stats.MaxHealth, stats.MaxHealth, stats.StartingMana, stats.MaxMana, false,
            new[] { startingSkill }, startingSkillContentId: startingSkill);
    }

    private static PureRunState CopyRevision(PureRunState run, long revision) => new(
        run.RunId, run.Seed, revision, run.Phase, run.EncounterIndex, run.EncounterContentId,
        run.Party, run.BackpackConsumables, run.BackpackEquipment, run.PendingProgression,
        run.AppliedTransactionKeys, run.Gold, run.BattlesCompleted, run.EnemiesDefeated,
        run.AcquiredItems, run.Checkpoint, run.MapState, run.NodeTransaction);

    private static RunSessionResult Fail(string? code, PureRunSaveSnapshot? snapshot = null) =>
        new(false, code ?? "run.store_failure", snapshot, null);

    private static bool HasPendingSetup(RunStoreResult loaded) =>
        loaded.Succeeded && loaded.Snapshot?.PendingRunSetup is not null;

    private RunStoreResult LoadWithAttributeRepair(out string[] diagnostics)
    {
        diagnostics = Array.Empty<string>();
        RunStoreResult loaded = _store.Load();
        if (!loaded.Succeeded || loaded.Snapshot?.ActiveRun is not PureRunState run) return loaded;
        try
        {
            bool repaired = false;
            RunCharacterState[] party = run.Party.Select(character => RepairCharacter(character, ref repaired)).ToArray();
            RunEncounterCheckpoint? checkpoint = run.Checkpoint;
            if (checkpoint is not null)
            {
                RunCharacterState[] checkpointParty = checkpoint.Party.Select(character => RepairCharacter(character, ref repaired)).ToArray();
                checkpoint = checkpoint with { Party = checkpointParty };
            }
            PureRunMapState? mapState = run.MapState;
            bool repairedLayerFourMap = false;
            if (run.Phase == PureRunPhase.AwaitingLayerFourChoice && mapState is null)
            {
                if (run.BattlesCompleted != _definition.Encounters.Count || _mapDefinition is null ||
                    _definition.LayerFourMapContentId != _mapDefinition.ContentId)
                    throw new InvalidDataException("save.layer_four_map_invalid");
                mapState = new PureRunMapService(_mapDefinition).UnlockLayerFour(run.Seed);
                repaired = true;
                repairedLayerFourMap = true;
            }
            if (!repaired) return loaded;
            var repairedRun = new PureRunState(run.RunId, run.Seed, run.Revision, run.Phase, run.EncounterIndex,
                run.EncounterContentId, party, run.BackpackConsumables, run.BackpackEquipment, run.PendingProgression,
                run.AppliedTransactionKeys, run.Gold, run.BattlesCompleted, run.EnemiesDefeated, run.AcquiredItems, checkpoint,
                mapState, run.NodeTransaction);
            diagnostics = new[]
            {
                repairedLayerFourMap ? "save.layer_four_map_repaired" : "save.attributes_repaired_from_run_definition"
            };
            return loaded with { Snapshot = loaded.Snapshot with { ActiveRun = repairedRun } };
        }
        catch (InvalidDataException error)
        {
            return new RunStoreResult(false, error.Message, loaded.Snapshot);
        }
    }

    private RunCharacterState RepairCharacter(RunCharacterState character, ref bool repaired)
    {
        int[] values = { character.Attributes.Strength, character.Attributes.Agility, character.Attributes.Constitution,
            character.Attributes.Intelligence, character.Attributes.Charisma, character.Attributes.Luck };
        PureRunPartyTemplate? template = _definition.Party.FirstOrDefault(item =>
            item.CharacterId == character.CharacterId && item.UnitContentId == character.UnitContentId);
        if (template is null) throw new InvalidDataException("save.zero_attributes_identity_mismatch");
        ContentId? selectedStartingSkill = character.StartingSkillContentId;
        if (selectedStartingSkill is ContentId explicitSelection &&
            (!template.EffectiveStartingSkillChoices.Contains(explicitSelection) ||
             !character.LearnedSkillStates.Any(skill => IsLearnedStartingBranch(skill, explicitSelection))))
            throw new InvalidDataException("save.starting_skill_invalid");
        if (selectedStartingSkill is null)
        {
            ContentId[] matches = template.EffectiveStartingSkillChoices
                .Where(choice => character.LearnedSkillStates.Any(skill => IsLearnedStartingBranch(skill, choice)))
                .ToArray();
            if (matches.Length > 1)
                throw new InvalidDataException("save.starting_skill_ambiguous");
            if (matches.Length == 1)
            {
                selectedStartingSkill = matches[0];
                repaired = true;
            }
        }
        if (values.All(value => value != 0))
        {
            if (selectedStartingSkill == character.StartingSkillContentId) return character;
            return new RunCharacterState(character.CharacterId, character.UnitContentId, character.Level,
                character.Attributes, character.CurrentHealth, character.MaxHealth, character.CurrentMana,
                character.MaxMana, character.IsDead, character.LearnedSkills, character.Equipment,
                character.CarriedConsumables, character.LearnedSkillStates, selectedStartingSkill);
        }
        if (values.Any(value => value != 0)) throw new InvalidDataException("save.partial_attributes_invalid");
        repaired = true;
        return new RunCharacterState(character.CharacterId, character.UnitContentId, character.Level, template.Attributes,
            character.CurrentHealth, character.MaxHealth, character.CurrentMana, character.MaxMana, character.IsDead,
            character.LearnedSkills, character.Equipment, character.CarriedConsumables, character.LearnedSkillStates,
            selectedStartingSkill);
    }

    private static bool IsLearnedStartingBranch(RunLearnedSkillState learned, ContentId startingSkill)
    {
        if (!TrySkillIdentity(learned.DefinitionId, out string branchId, out int level) ||
            !TrySkillIdentity(startingSkill, out string startingBranchId, out _)) return false;
        return string.Equals(learned.BranchId, branchId, StringComparison.Ordinal) &&
               learned.Level == level &&
               string.Equals(branchId, startingBranchId, StringComparison.Ordinal);
    }

    private static bool TrySkillIdentity(ContentId contentId, out string branchId, out int level)
    {
        string value = contentId.Value;
        if (value == "skill.poison-spear.lv1")
        {
            branchId = "amazon.poison-spear";
            level = 1;
            return true;
        }
        if (!value.StartsWith("skill.", StringComparison.Ordinal))
        {
            branchId = string.Empty;
            level = 0;
            return false;
        }
        int marker = value.LastIndexOf(".lv", StringComparison.Ordinal);
        int prefixLength = "skill.".Length;
        string levelText = marker >= prefixLength ? value[(marker + 3)..] : string.Empty;
        if (marker >= prefixLength && int.TryParse(levelText, out level) && level > 0 &&
            string.Equals(levelText, level.ToString(System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal))
        {
            branchId = value[prefixLength..marker];
            return branchId.Length > 0;
        }
        branchId = string.Empty;
        level = 0;
        return false;
    }
}
