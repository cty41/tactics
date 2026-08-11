using NUnit.Framework;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

public sealed class PureRunRuntimeTests
{
    [Test]
    public void Settlement_VictoryRecoversLivingAndRecordsProgressionWithoutLeveling()
    {
        PureRunDefinition definition = TestDefinition();
        PureRunState pending = PendingState(definition, seed: 19);
        PureRunBattleResult result = Result(pending, victory: true, rounds: 3,
            health: new[] { 10, 0, 8 }, mana: new[] { 2, 0, 1 });

        PureRunSettlementResult settled = new PureRunSettlementService().Apply(
            definition, pending, result, Array.Empty<ContentId>());

        Assert.Multiple(() =>
        {
            Assert.That(settled.Succeeded, Is.True);
            Assert.That(settled.ActiveRun!.EncounterContentId.Value, Is.EqualTo("encounter.pure-run.n2"));
            Assert.That(settled.ActiveRun.Gold, Is.EqualTo(8));
            Assert.That(settled.ActiveRun.Party[0].CurrentHealth, Is.EqualTo(20));
            Assert.That(settled.ActiveRun.Party[1].IsDead, Is.False);
            Assert.That(settled.ActiveRun.Party[1].CurrentHealth, Is.EqualTo(10));
            Assert.That(settled.ActiveRun.Party[2].CurrentMana, Is.EqualTo(6));
            Assert.That(settled.ActiveRun.PendingProgression.Single().CharacterId, Is.EqualTo("mage"));
            Assert.That(settled.ActiveRun.Party.Select(member => member.Level), Is.All.EqualTo(1));
        });
    }

    [Test]
    public void Settlement_N3ProducesSliceCompletedInsteadOfBossVictory()
    {
        PureRunDefinition definition = TestDefinition();
        PureRunState pending = PendingState(definition, 7, encounterIndex: 2);
        PureRunSettlementResult result = new PureRunSettlementService().Apply(
            definition, pending, Result(pending, true, 6, new[] { 20, 20, 20 }, new[] { 5, 5, 5 }),
            Array.Empty<ContentId>());

        Assert.Multiple(() =>
        {
            Assert.That(result.ActiveRun, Is.Null);
            Assert.That(result.TerminalSummary!.Outcome, Is.EqualTo(PureRunOutcome.SliceCompleted));
            Assert.That(result.TerminalSummary.BattlesCompleted, Is.EqualTo(1));
        });
    }

    [Test]
    public void Settlement_DefeatDoesNotRecoverOrReviveParty()
    {
        PureRunDefinition definition=TestDefinition();PureRunState pending=PendingState(definition,13);
        PureRunSettlementResult result=new PureRunSettlementService().Apply(definition,pending,
            Result(pending,false,4,new[]{0,7,8},new[]{0,1,2}),Array.Empty<ContentId>());
        Assert.Multiple(() =>
        {
            Assert.That(result.ActiveRun,Is.Null);
            Assert.That(result.TerminalSummary!.Outcome,Is.EqualTo(PureRunOutcome.Defeated));
            Assert.That(result.TerminalSummary.DeadCharacters,Does.Contain("mage"));
        });
    }

    [Test]
    public void Settlement_RejectsStaleRevisionWithoutMutation()
    {
        PureRunDefinition definition = TestDefinition();
        PureRunState pending = PendingState(definition, 7);
        PureRunBattleResult stale = Result(pending, true, 2, new[] { 20, 20, 20 }, new[] { 5, 5, 5 })
            with { CheckpointRevision = pending.Checkpoint!.Revision - 1 };

        PureRunSettlementResult result = new PureRunSettlementService().Apply(
            definition, pending, stale, Array.Empty<ContentId>());

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.RejectionCode, Is.EqualTo("run.result_revision_mismatch"));
            Assert.That(result.ActiveRun, Is.SameAs(pending));
        });
    }

    [Test]
    public void DeriveSeed_MatchesFrozenUnityFnvContract()
    {
        Assert.That(PureRunSettlementService.DeriveSeed(1203, "battle-drop:encounter.pure-run.n1"),
            Is.EqualTo(unchecked((int)0x759961a2)));
    }

    internal static PureRunDefinition TestDefinition() => new(
        new ContentId("run.pure-run.three-encounter-v1"),
        new[] { "encounter.pure-run.n1", "encounter.pure-run.n2", "encounter.pure-run.n3" }.Select(value => new ContentId(value)),
        new[]
        {
            new PureRunPartyTemplate("mage", new ContentId("unit.pure-run.mage"), new ContentId("skill.mage.fireball.lv1"), new UnitAttributes(5,5,5,6,5,5)),
            new PureRunPartyTemplate("necro", new ContentId("unit.pure-run.necromancer"), new ContentId("skill.necromancer.summon-skeleton.lv1"), new UnitAttributes(5,5,5,5,6,5)),
            new PureRunPartyTemplate("amazon", new ContentId("unit.pure-run.amazon"), new ContentId("skill.amazon.thrust.lv1"), new UnitAttributes(5,6,5,5,5,5))
        });

    internal static PureRunState PendingState(PureRunDefinition definition, int seed, int encounterIndex = 0)
    {
        RunCharacterState[] party = definition.Party.Select(template => new RunCharacterState(
            template.CharacterId, template.UnitContentId, 1, template.Attributes,
            20, 20, 5, 18, false, new[] { template.StartingSkillContentId })).ToArray();
        ContentId encounter = definition.Encounters[encounterIndex];
        var checkpoint = new RunEncounterCheckpoint(encounter, encounterIndex, 2, party,
            Array.Empty<Tactics.Core.Items.BattleConsumableState>(), Array.Empty<RunEquipmentState>());
        return new PureRunState("run-test", seed, 2, PureRunPhase.PendingBattle, encounterIndex, encounter, party,
            checkpoint: checkpoint);
    }

    internal static PureRunBattleResult Result(
        PureRunState state, bool victory, int rounds, int[] health, int[] mana) => new(
            state.RunId, state.Checkpoint!.Revision, state.EncounterContentId, victory, rounds, 3,
            state.Party.Select((member, index) => new BattlePartyResult(
                member.CharacterId, health[index], mana[index], health[index] == 0,
                Array.Empty<Tactics.Core.Items.BattleConsumableState>())).ToArray());
}
