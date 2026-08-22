using Tactics.Application.Runs;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Items;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Godot.Tests.GameplaySpec;

/// <summary>Builds versioned, deterministic checkpoints consumed by gameplay-spec scenarios.</summary>
public static class GodotGameplayCheckpointCatalog
{
    public static ValidatedGodotRunCheckpoint Create(string id) => id switch
    {
        "inventory-store-ready-v1" => InventoryReady(id),
        "defeat-no-summon-v1" => PendingBattle(id, character => Copy(character, health: 1)),
        "numbers-mana-v1" => PendingBattle(id, character => character.CharacterId == "pure_run_mage"
            ? Copy(character, mana: 0)
            : character),
        "numbers-miss-v1" => PendingBattle(id,
            character => character.CharacterId == "pure_run_amazon" ? character : Copy(character, health: 0),
            amazonStartingSkill: new ContentId("skill.amazon.combat-techniques.lv1"), battleRandomState: 6),
        "reload-pending-battle-v1" => PendingBattle(id, character => character),
        "demonbound-ready-v1" => DemonboundPendingBattle(id),
        "layer4-choice-ready-v1" => LayerFourChoice(id, damaged: true),
        "layer4-event-ready-v1" => LayerFourChoice(id, damaged: false),
        "layer6-event-ready-v1" => LayerSixChoice(id),
        "layer6-escort-ready-v1" => LayerSixEscort(id),
        _ => throw new InvalidDataException("Unknown validated Godot checkpoint: " + id)
    };

    private static ValidatedGodotRunCheckpoint LayerSixEscort(string id)
    {
        PureRunState source = LayerSixChoice("layer6-escort-source").Snapshot.ActiveRun!;
        var service = new PureRunEscortService();
        RunEscortTransition accepted = service.Accept(source, "escort.lost-villager.v1",
            "layer_04_event", "layer_06_event");
        Require(accepted.Succeeded, accepted.RejectionCode);
        RunEscortTransition traveling = service.BeginTravel(accepted.State);
        Require(traveling.Succeeded, traveling.RejectionCode);
        return ValidatedGodotRunCheckpoint.Create(id, "validated://" + id,
            new PureRunSaveSnapshot(traveling.State.Revision, traveling.State, null));
    }

    private static ValidatedGodotRunCheckpoint LayerSixChoice(string id)
    {
        (_, MemoryRunStore store, _) = ReadyRun();
        PureRunState source = store.Snapshot!.ActiveRun!;
        RunCharacterState[] party = source.Party.Select(character => new RunCharacterState(
            character.CharacterId, character.UnitContentId, 10, new UnitAttributes(50, 50, 50, 50, 50, 50),
            200, 200, 100, 100, false, character.LearnedSkills, character.Equipment,
            character.CarriedConsumables, character.LearnedSkillStates, character.StartingSkillContentId)).ToArray();
        PureRunMapDefinition map = AdventureMap();
        PureRunMapState mapState = new PureRunMapService(map)
            .UnlockLayerSix(new PureRunMapService(map).UnlockLayerFour(source.Seed), source.Seed);
        var run = new PureRunState(source.RunId, source.Seed, source.Revision + 1,
            PureRunPhase.AwaitingLayerSixChoice, 5, new ContentId("encounter.pure-run.n5"), party,
            source.BackpackConsumables, source.BackpackEquipment, source.PendingProgression,
            source.AppliedTransactionKeys, 50, 5, source.EnemiesDefeated, source.AcquiredItems,
            mapState: mapState, adventureState: MapAdventure(source.AdventureState!, party, map.ContentId));
        return ValidatedGodotRunCheckpoint.Create(id, "validated://" + id,
            new PureRunSaveSnapshot(run.Revision, run, null));
    }

    private static PureRunMapDefinition AdventureMap() => new(new ContentId("run-map.pure-run.layer4-v1"), 3,
    [
        new("layer_04_battle", 4, PureRunNodeKind.Battle, new ContentId("encounter.pure-run.n4")),
        new("layer_04_rest", 4, PureRunNodeKind.Rest, new ContentId("rest.pure-run.standard-v1")),
        new("layer_04_store", 4, PureRunNodeKind.Store, new ContentId("store.pure-run.standard-v1")),
        new("layer_04_event", 4, PureRunNodeKind.Mystery, new ContentId("event.pure-run.cursed-chest")),
        new("layer_04_treasure", 4, PureRunNodeKind.Treasure, new ContentId("treasure.pure-run.standard-v1")),
        new("layer_06_battle", 6, PureRunNodeKind.Battle, new ContentId("encounter.pure-run.e1")),
        new("layer_06_rest", 6, PureRunNodeKind.Rest, new ContentId("rest.pure-run.standard-v1")),
        new("layer_06_store", 6, PureRunNodeKind.Store, new ContentId("store.pure-run.standard-v1")),
        new("layer_06_event", 6, PureRunNodeKind.Mystery, new ContentId("event.pure-run.fallen-altar")),
        new("layer_06_treasure", 6, PureRunNodeKind.Treasure, new ContentId("treasure.pure-run.standard-v1"))
    ]);

    private static ValidatedGodotRunCheckpoint LayerFourChoice(string id, bool damaged)
    {
        (_, MemoryRunStore store, _) = ReadyRun();
        PureRunState source = store.Snapshot!.ActiveRun!;
        RunCharacterState[] party = source.Party.Select(character => damaged
            ? Copy(character, health: Math.Max(1, character.MaxHealth / 3), mana: 0)
            : new RunCharacterState(character.CharacterId, character.UnitContentId, 10,
                new UnitAttributes(50, 50, 50, 50, 50, 50), 200, 200, 100, 100, false,
                character.LearnedSkills, character.Equipment, character.CarriedConsumables,
                character.LearnedSkillStates, character.StartingSkillContentId)).ToArray();
        var run = new PureRunState(source.RunId, source.Seed, source.Revision + 1,
            PureRunPhase.AwaitingLayerFourChoice, 3, new ContentId("encounter.pure-run.n3"), party,
            source.BackpackConsumables, source.BackpackEquipment, source.PendingProgression,
            source.AppliedTransactionKeys, 50, 3, source.EnemiesDefeated, source.AcquiredItems,
            adventureState: MapAdventure(source.AdventureState!, party, AdventureMap().ContentId));
        return ValidatedGodotRunCheckpoint.Create(id, "validated://" + id,
            new PureRunSaveSnapshot(run.Revision, run, null));
    }

    private static RunAdventureState MapAdventure(RunAdventureState state, IReadOnlyList<RunCharacterState> party, ContentId boardId)
    {
        GridPoint[] cells = [new(2, 5), new(1, 4), new(1, 6)];
        return state with
        {
            Lifecycle = RunAdventureLifecycle.MapActive,
            BoardContentId = boardId,
            LeaderId = party[0].CharacterId,
            ActorCells = party.Select((member, index) => new RunAdventureActorCell(member.CharacterId, cells[index])).ToArray(),
            SceneRevision = state.SceneRevision + 1,
            Revision = state.Revision + 1
        };
    }

    private static ValidatedGodotRunCheckpoint DemonboundPendingBattle(string id)
    {
        var definition = new PureRunDefinition(new ContentId("run.demonbound.qa"),
            [new ContentId("encounter.pure-run.n1"), new ContentId("encounter.pure-run.n2"), new ContentId("encounter.pure-run.n3")],
            [
                new PureRunPartyTemplate("pure_run_mage", new ContentId("unit.pure-run.mage"),
                    new ContentId("skill.mage.fireball.lv1"), new UnitAttributes(5,5,5,6,5,5)),
                new PureRunPartyTemplate("pure_run_necromancer", new ContentId("unit.pure-run.necromancer"),
                    new ContentId("skill.necromancer.summon-skeleton.lv1"), new UnitAttributes(5,5,5,5,6,5)),
                new PureRunPartyTemplate("pure_run_demonbound", new ContentId("unit.pure-run.demonbound"),
                    new ContentId("skill.demonbound.bane.lv1"), new UnitAttributes(5,5,5,5,6,5),
                    InherentSkills: [new ContentId("skill.demonbound.meditation")])
            ]);
        var store = new MemoryRunStore();
        var service = new PureRunSessionService(definition, store);
        Require(service.BeginNewRunSetup(7).Succeeded, "demonbound_setup_failed");
        foreach (PureRunPartyTemplate member in definition.Party)
            Require(service.ChooseStartingSkill(member.CharacterId, member.StartingSkillContentId).Succeeded,
                "demonbound_choice_failed:" + member.CharacterId);
        Require(service.BeginEncounter().Succeeded, "demonbound_encounter_failed");
        return ValidatedGodotRunCheckpoint.Create(id, "validated://" + id, store.Snapshot!);
    }

    private static ValidatedGodotRunCheckpoint InventoryReady(string id)
    {
        (PureRunDefinition definition, MemoryRunStore store, PureRunSessionService service) = ReadyRun();
        var equipment = new RunEquipmentState(new ItemInstanceId("qa-armor"),
            new ContentId("item.equipment.leather-armor-01"), EquipmentSlot.Armor);
        var consumable = new BattleConsumableState(new ItemInstanceId("qa-life-potion"),
            new ContentId("item.consumable.life-potion"), 1, 1);
        RunSessionResult mutation = service.ApplyMutation(run => new RunMutationResult(true, null,
            Copy(run, revision: run.Revision + 1, backpackEquipment: [equipment],
                backpackConsumables: [consumable])));
        Require(mutation.Succeeded, mutation.ErrorCode);
        return ValidatedGodotRunCheckpoint.Create(id, "validated://" + id, store.Snapshot!);
    }

    private static ValidatedGodotRunCheckpoint PendingBattle(string id,
        Func<RunCharacterState, RunCharacterState> characterMutation,
        ContentId? amazonStartingSkill = null, long? battleRandomState = null)
    {
        (_, MemoryRunStore store, PureRunSessionService service) = ReadyRun(amazonStartingSkill);
        RunSessionResult mutation = service.ApplyMutation(run => new RunMutationResult(true, null,
            Copy(run, revision: run.Revision + 1, party: run.Party.Select(characterMutation).ToArray())));
        Require(mutation.Succeeded, mutation.ErrorCode);
        RunSessionResult begun = service.BeginEncounter();
        Require(begun.Succeeded, begun.ErrorCode);
        PureRunSaveSnapshot snapshot = store.Snapshot!;
        if (battleRandomState is long revision)
        {
            PureRunState run = snapshot.ActiveRun!;
            RunEncounterCheckpoint checkpoint = run.Checkpoint!;
            var deterministicCheckpoint = new RunEncounterCheckpoint(checkpoint.EncounterContentId,
                checkpoint.EncounterIndex, revision, checkpoint.Party, checkpoint.BackpackConsumables,
                checkpoint.BackpackEquipment);
            PureRunState deterministicRun = Copy(run, revision, checkpoint: deterministicCheckpoint);
            snapshot = new PureRunSaveSnapshot(revision, deterministicRun, snapshot.TerminalSummary);
        }
        return ValidatedGodotRunCheckpoint.Create(id, "validated://" + id, snapshot);
    }

    private static (PureRunDefinition Definition, MemoryRunStore Store, PureRunSessionService Service) ReadyRun(
        ContentId? amazonStartingSkill = null)
    {
        var definition = new PureRunDefinition(new ContentId("run.pure-run.three-encounter-v1"),
            [new ContentId("encounter.pure-run.n1"), new ContentId("encounter.pure-run.n2"), new ContentId("encounter.pure-run.n3")],
            [
                new PureRunPartyTemplate("pure_run_mage", new ContentId("unit.pure-run.mage"),
                    new ContentId("skill.mage.fireball.lv1"), new UnitAttributes(5, 5, 5, 6, 5, 5), 1,
                    [new ContentId("skill.mage.fireball.lv1"), new ContentId("skill.mage.ice-bolt.lv1"), new ContentId("skill.mage.lightning.lv1")]),
                new PureRunPartyTemplate("pure_run_necromancer", new ContentId("unit.pure-run.necromancer"),
                    new ContentId("skill.necromancer.summon-skeleton.lv1"), new UnitAttributes(5, 5, 5, 5, 6, 5), 1,
                    [new ContentId("skill.necromancer.summon-skeleton.lv1"), new ContentId("skill.necromancer.amplify-damage.lv1"), new ContentId("skill.necromancer.bone-spear.lv1")]),
                new PureRunPartyTemplate("pure_run_amazon", new ContentId("unit.pure-run.amazon"),
                    new ContentId("skill.amazon.thrust.lv1"), new UnitAttributes(5, 6, 5, 5, 5, 5), 1,
                    [new ContentId("skill.amazon.thrust.lv1"), new ContentId("skill.poison-spear.lv1"), new ContentId("skill.amazon.combat-techniques.lv1")])
            ], new ContentId("run-map.pure-run.layer4-v1"));
        var store = new MemoryRunStore();
        var service = new PureRunSessionService(definition, store);
        Require(service.BeginNewRunSetup(7).Succeeded, "run_setup.begin_failed");
        foreach (PureRunPartyTemplate member in definition.Party)
        {
            ContentId selected = member.CharacterId == "pure_run_amazon" && amazonStartingSkill is ContentId amazon
                ? amazon
                : member.StartingSkillContentId;
            Require(service.ChooseStartingSkill(member.CharacterId, selected).Succeeded,
                "run_setup.choice_failed:" + member.CharacterId);
        }
        return (definition, store, service);
    }

    private static void Require(bool condition, string? error)
    {
        if (!condition) throw new InvalidOperationException(error ?? "checkpoint_build_failed");
    }

    private static RunCharacterState Copy(RunCharacterState value, int? health = null, int? mana = null) => new(
        value.CharacterId, value.UnitContentId, value.Level, value.Attributes,
        health ?? value.CurrentHealth, value.MaxHealth, mana ?? value.CurrentMana, value.MaxMana,
        (health ?? value.CurrentHealth) == 0, value.LearnedSkills, value.Equipment, value.CarriedConsumables,
        value.LearnedSkillStates, value.StartingSkillContentId);

    private static PureRunState Copy(PureRunState value, long revision,
        IReadOnlyList<RunCharacterState>? party = null,
        IReadOnlyList<BattleConsumableState>? backpackConsumables = null,
        IReadOnlyList<RunEquipmentState>? backpackEquipment = null,
        RunEncounterCheckpoint? checkpoint = null) => new(
        value.RunId, value.Seed, revision, value.Phase, value.EncounterIndex, value.EncounterContentId,
        party ?? value.Party, backpackConsumables ?? value.BackpackConsumables,
        backpackEquipment ?? value.BackpackEquipment, value.PendingProgression, value.AppliedTransactionKeys,
        value.Gold, value.BattlesCompleted, value.EnemiesDefeated, value.AcquiredItems, checkpoint ?? value.Checkpoint,
        value.MapState, value.NodeTransaction, value.EscortState, value.AdventureState);

    private sealed class MemoryRunStore : IRunSaveStore
    {
        public PureRunSaveSnapshot? Snapshot { get; private set; } = new(0, null, null);
        public RunStoreResult Load() => new(true, null, Snapshot);
        public RunStoreResult Save(PureRunSaveSnapshot snapshot, long expectedRevision)
        {
            if (Snapshot?.Revision != expectedRevision) return new RunStoreResult(false, "save.stale_revision", Snapshot);
            Snapshot = snapshot;
            return new RunStoreResult(true, null, Snapshot);
        }
    }
}
