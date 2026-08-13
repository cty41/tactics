using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class PureRunSessionServiceTests
{
    [Test]
    public void NewRunSetup_RequiresThreeCanonicalChoicesBeforeReplacingActiveRun()
    {
        var store = new MemoryRunStore();
        PureRunDefinition definition = DefinitionWithChoices();
        var service = new PureRunSessionService(definition, store);
        Assert.That(service.StartNewRun(3).Succeeded, Is.True);
        string oldRunId = store.Snapshot!.ActiveRun!.RunId;

        RunSessionResult begun = service.BeginNewRunSetup(9);
        Assert.Multiple(() =>
        {
            Assert.That(begun.Succeeded, Is.True);
            Assert.That(begun.Snapshot!.ActiveRun!.RunId, Is.EqualTo(oldRunId));
            Assert.That(begun.Snapshot.PendingRunSetup!.CurrentCharacterId, Is.EqualTo("mage"));
        });

        Assert.That(service.ChooseStartingSkill("mage", new ContentId("skill.mage.fireball.lv1")).Succeeded, Is.True);
        Assert.That(service.ChooseStartingSkill("necromancer", new ContentId("skill.necromancer.bone-spear.lv1")).Succeeded, Is.True);
        RunSessionResult completed = service.ChooseStartingSkill("amazon", new ContentId("skill.amazon.poison-spear.lv1"));

        Assert.Multiple(() =>
        {
            Assert.That(completed.Succeeded, Is.True);
            Assert.That(completed.Snapshot!.PendingRunSetup, Is.Null);
            Assert.That(completed.Snapshot.ActiveRun!.RunId, Is.Not.EqualTo(oldRunId));
            Assert.That(completed.Snapshot.ActiveRun.Party.SelectMany(value => value.LearnedSkills),
                Is.EquivalentTo(new[]
                {
                    new ContentId("skill.mage.fireball.lv1"),
                    new ContentId("skill.necromancer.bone-spear.lv1"),
                    new ContentId("skill.amazon.poison-spear.lv1")
                }));
        });
    }

    [Test]
    public void NewRunSetup_RejectsCrossClassChoiceAndCancelPreservesOldRun()
    {
        var store = new MemoryRunStore();
        var service = new PureRunSessionService(DefinitionWithChoices(), store);
        service.StartNewRun(4);
        string oldRunId = store.Snapshot!.ActiveRun!.RunId;
        service.BeginNewRunSetup(10);

        RunSessionResult rejected = service.ChooseStartingSkill("mage", new ContentId("skill.amazon.thrust.lv1"));
        RunSessionResult canceled = service.CancelNewRunSetup();

        Assert.Multiple(() =>
        {
            Assert.That(rejected.ErrorCode, Is.EqualTo("run_setup.skill_not_offered"));
            Assert.That(canceled.Succeeded, Is.True);
            Assert.That(canceled.Snapshot!.PendingRunSetup, Is.Null);
            Assert.That(canceled.Snapshot.ActiveRun!.RunId, Is.EqualTo(oldRunId));
        });
    }
    [Test]
    public void BeginEncounter_PersistsCheckpointBeforeReturningRequest()
    {
        var store = new MemoryRunStore();
        var service = new PureRunSessionService(Definition(), store);
        Assert.That(service.StartNewRun(42).Succeeded, Is.True);

        RunSessionResult result = service.BeginEncounter();

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(store.Snapshot!.ActiveRun!.Phase, Is.EqualTo(PureRunPhase.PendingBattle));
            Assert.That(result.EncounterRequest!.CheckpointRevision,
                Is.EqualTo(store.Snapshot.ActiveRun.Checkpoint!.Revision));
        });
    }

    [Test]
    public void ResumePendingBattle_ReissuesSameCheckpoint()
    {
        var store = new MemoryRunStore();
        var service = new PureRunSessionService(Definition(), store);
        service.StartNewRun(9);
        EncounterRequest first = service.BeginEncounter().EncounterRequest!;

        EncounterRequest resumed = service.ResumeRun().EncounterRequest!;

        Assert.That(resumed, Is.EqualTo(first));
    }

    [Test]
    public void ThreeVictories_ProduceTerminalSummaryAndNoActiveRun()
    {
        var store = new MemoryRunStore();
        var service = new PureRunSessionService(Definition(), store);
        service.StartNewRun(11);
        for (int index = 0; index < 3; index++)
        {
            EncounterRequest request = service.BeginEncounter().EncounterRequest!;
            PureRunState run = store.Snapshot!.ActiveRun!;
            RunSessionResult settled = service.ApplyBattleResult(Victory(run));
            Assert.That(settled.Succeeded, Is.True);
        }

        Assert.Multiple(() =>
        {
            Assert.That(store.Snapshot!.ActiveRun, Is.Null);
            Assert.That(store.Snapshot.TerminalSummary!.Outcome, Is.EqualTo(PureRunOutcome.SliceCompleted));
            Assert.That(store.Snapshot.TerminalSummary.BattlesCompleted, Is.EqualTo(3));
        });
    }

    [Test]
    public void StoreRejectsStaleRevision()
    {
        var store = new MemoryRunStore();
        var service = new PureRunSessionService(Definition(), store);
        service.StartNewRun(1);
        RunStoreResult stale = store.Save(store.Snapshot!, expectedRevision: 0);
        Assert.That(stale.ErrorCode, Is.EqualTo("save.revision_conflict"));
    }

    [Test]
    public void DuplicateSettlementAfterAdvance_DoesNotApplyRewardTwice()
    {
        var store = new MemoryRunStore();
        var service = new PureRunSessionService(Definition(), store);
        service.StartNewRun(17);
        service.BeginEncounter();
        PureRunBattleResult result = Victory(store.Snapshot!.ActiveRun!);
        RunSessionResult first = service.ApplyBattleResult(result);
        int gold = first.Snapshot!.ActiveRun!.Gold;

        RunSessionResult duplicate = service.ApplyBattleResult(result);

        Assert.Multiple(() =>
        {
            Assert.That(duplicate.Succeeded, Is.True);
            Assert.That(duplicate.WasDuplicate, Is.True);
            Assert.That(duplicate.Snapshot!.ActiveRun!.Gold, Is.EqualTo(gold));
        });
    }

    [Test]
    public void DuplicateTerminalSettlement_ReturnsExistingSummaryWithoutRewrite()
    {
        var store = new MemoryRunStore();
        var service = new PureRunSessionService(Definition(), store);
        service.StartNewRun(23);
        PureRunBattleResult? finalResult = null;
        for (int index = 0; index < 3; index++)
        {
            service.BeginEncounter();
            finalResult = Victory(store.Snapshot!.ActiveRun!);
            Assert.That(service.ApplyBattleResult(finalResult).Succeeded, Is.True);
        }
        long terminalRevision = store.Snapshot!.Revision;

        RunSessionResult duplicate = service.ApplyBattleResult(finalResult!);

        Assert.Multiple(() =>
        {
            Assert.That(duplicate.Succeeded, Is.True);
            Assert.That(duplicate.WasDuplicate, Is.True);
            Assert.That(duplicate.Snapshot!.Revision, Is.EqualTo(terminalRevision));
            Assert.That(duplicate.Snapshot.TerminalSummary!.Outcome, Is.EqualTo(PureRunOutcome.SliceCompleted));
        });
    }

    [Test]
    public void ResumeRun_RepairsLegacyAllZeroAttributesAndPersistsOnNextTransaction()
    {
        PureRunDefinition definition = Definition();
        var store = new MemoryRunStore();
        var zero = new UnitAttributes(0,0,0,0,0,0);
        RunCharacterState[] party = definition.Party.Select(template => new RunCharacterState(template.CharacterId,
            template.UnitContentId,1,zero,20,20,5,18,false,new[]{template.StartingSkillContentId})).ToArray();
        store.Snapshot = new PureRunSaveSnapshot(1,new PureRunState("run-legacy",7,1,PureRunPhase.Ready,0,
            definition.Encounters[0],party),null);
        var service = new PureRunSessionService(definition,store);

        RunSessionResult resumed = service.ResumeRun();
        RunSessionResult begun = service.BeginEncounter();

        Assert.Multiple(() =>
        {
            Assert.That(resumed.Succeeded,Is.True);
            Assert.That(resumed.Diagnostics,Does.Contain("save.attributes_repaired_from_run_definition"));
            Assert.That(resumed.Snapshot!.ActiveRun!.Party[0].Attributes.Strength,Is.EqualTo(5));
            Assert.That(begun.Succeeded,Is.True);
            Assert.That(store.Snapshot!.ActiveRun!.Party.All(member=>member.Attributes.Strength>0),Is.True);
        });
    }

    private static PureRunBattleResult Victory(PureRunState run) => new(
        run.RunId, run.Checkpoint!.Revision, run.EncounterContentId, true, 3, 3,
        run.Party.Select(member => new BattlePartyResult(
            member.CharacterId, member.MaxHealth, member.MaxMana, false,
            Array.Empty<Tactics.Core.Items.BattleConsumableState>())).ToArray());

    private static PureRunDefinition Definition() => new(
        new ContentId("run.pure-run.three-encounter-v1"),
        new[] { "encounter.pure-run.n1", "encounter.pure-run.n2", "encounter.pure-run.n3" }.Select(value => new ContentId(value)),
        new[]
        {
            new PureRunPartyTemplate("mage", new ContentId("unit.pure-run.mage"), new ContentId("skill.mage.fireball.lv1"), new UnitAttributes(5,5,5,6,5,5)),
            new PureRunPartyTemplate("necro", new ContentId("unit.pure-run.necromancer"), new ContentId("skill.necromancer.summon-skeleton.lv1"), new UnitAttributes(5,5,5,5,6,5)),
            new PureRunPartyTemplate("amazon", new ContentId("unit.pure-run.amazon"), new ContentId("skill.amazon.thrust.lv1"), new UnitAttributes(5,6,5,5,5,5))
        });

    private static PureRunDefinition DefinitionWithChoices() => new(
        new ContentId("run.pure-run.three-encounter-v1"),
        new[] { "encounter.pure-run.n1", "encounter.pure-run.n2", "encounter.pure-run.n3" }.Select(value => new ContentId(value)),
        new[]
        {
            new PureRunPartyTemplate("mage", new ContentId("unit.pure-run.mage"), new ContentId("skill.mage.fireball.lv1"), new UnitAttributes(5,5,5,6,5,5), 1,
                [new ContentId("skill.mage.fireball.lv1"), new ContentId("skill.mage.ice-bolt.lv1"), new ContentId("skill.mage.lightning.lv1")]),
            new PureRunPartyTemplate("necromancer", new ContentId("unit.pure-run.necromancer"), new ContentId("skill.necromancer.summon-skeleton.lv1"), new UnitAttributes(5,5,5,5,6,5), 1,
                [new ContentId("skill.necromancer.summon-skeleton.lv1"), new ContentId("skill.necromancer.amplify-damage.lv1"), new ContentId("skill.necromancer.bone-spear.lv1")]),
            new PureRunPartyTemplate("amazon", new ContentId("unit.pure-run.amazon"), new ContentId("skill.amazon.thrust.lv1"), new UnitAttributes(5,6,5,5,5,5), 1,
                [new ContentId("skill.amazon.thrust.lv1"), new ContentId("skill.amazon.poison-spear.lv1"), new ContentId("skill.amazon.combat-techniques.lv1")])
        });

    private sealed class MemoryRunStore : IRunSaveStore
    {
        public PureRunSaveSnapshot? Snapshot { get; set; }

        public RunStoreResult Load() => new(true, null, Snapshot ?? new PureRunSaveSnapshot(0, null, null));

        public RunStoreResult Save(PureRunSaveSnapshot snapshot, long expectedRevision)
        {
            long current = Snapshot?.Revision ?? 0;
            if (current != expectedRevision)
                return new RunStoreResult(false, "save.revision_conflict", Snapshot);
            Snapshot = snapshot;
            return new RunStoreResult(true, null, Snapshot);
        }
    }
}
