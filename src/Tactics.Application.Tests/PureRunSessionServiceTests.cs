using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class PureRunSessionServiceTests
{
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

    private sealed class MemoryRunStore : IRunSaveStore
    {
        public PureRunSaveSnapshot? Snapshot { get; private set; }

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
