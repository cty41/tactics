using Tactics.Application.Runs;
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
            amazonStartingSkill: new ContentId("skill.amazon.combat-techniques.lv1"), battleRandomState: 2),
        "reload-pending-battle-v1" => PendingBattle(id, character => character),
        _ => throw new InvalidDataException("Unknown validated Godot checkpoint: " + id)
    };

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
        value.MapState, value.NodeTransaction);

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
