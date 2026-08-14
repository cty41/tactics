using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Encounters;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class PureRunFullRunServiceTests
{
    [Test]
    public void LayerFiveSelectionIsStableAndProgressionGuardsLayerSix()
    {
        PureRunMapDefinition map = Map();
        PureRunFullRunService service = new();
        PureRunState ready = Run(PureRunPhase.ReadyForLayerFive);
        FullRunTransitionResult first = service.BeginLayerFive(ready, map);
        FullRunTransitionResult replay = service.BeginLayerFive(ready, map);
        Assert.Multiple(() =>
        {
            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.State.EncounterContentId, Is.EqualTo(replay.State.EncounterContentId));
            Assert.That(first.State.EncounterContentId.Value, Is.AnyOf("encounter.pure-run.e1", "encounter.pure-run.e2"));
            Assert.That(first.State.EncounterIndex, Is.EqualTo(4));
        });
        PureRunState withProgression = Run(PureRunPhase.ReadyForLayerSix,
            [new PendingProgression("p", "e1", "mage")]);
        Assert.That(service.UnlockLayerSix(withProgression, map).RejectionCode,
            Is.EqualTo("full_run.layer_six_unavailable"));
        FullRunTransitionResult unlocked = service.UnlockLayerSix(Run(PureRunPhase.ReadyForLayerSix), map);
        Assert.That(unlocked.State.MapState!.ReachableNodeIds, Has.Count.EqualTo(4));
        Assert.That(unlocked.State.Phase, Is.EqualTo(PureRunPhase.AwaitingLayerSixChoice));
    }

    [Test]
    public void BossVictoryProducesTerminalBossSummaryWithoutProgression()
    {
        PureRunFullRunService service = new();
        FullRunTransitionResult pending = service.BeginBoss(Run(PureRunPhase.ReadyForBoss), Map());
        PureRunBattleResult result = Result(pending.State, victory: true);
        FullRunTransitionResult completed = service.CompleteBoss(pending.State, result);
        Assert.Multiple(() =>
        {
            Assert.That(completed.Succeeded, Is.True);
            Assert.That(completed.TerminalSummary!.Outcome, Is.EqualTo(PureRunOutcome.BossVictory));
            Assert.That(completed.TerminalSummary.BossDefeated, Is.True);
            Assert.That(completed.TerminalSummary.TerminalEncounterId!.Value.Value,
                Is.EqualTo("encounter.pure-run.special"));
            Assert.That(completed.TerminalSummary.BattlesCompleted, Is.EqualTo(5));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LateBattleDefeatProducesDefeatedTerminalSummary(bool boss)
    {
        PureRunFullRunService service = new();
        FullRunTransitionResult pending = boss
            ? service.BeginBoss(Run(PureRunPhase.ReadyForBoss), Map())
            : service.BeginLayerFive(Run(PureRunPhase.ReadyForLayerFive), Map());
        FullRunTransitionResult completed = boss
            ? service.CompleteBoss(pending.State, Result(pending.State, victory: false))
            : service.CompleteLayerFive(pending.State, Result(pending.State, victory: false));

        Assert.Multiple(() =>
        {
            Assert.That(completed.Succeeded, Is.True);
            Assert.That(completed.TerminalSummary, Is.Not.Null);
            Assert.That(completed.TerminalSummary!.Outcome, Is.EqualTo(PureRunOutcome.Defeated));
            Assert.That(completed.TerminalSummary.BossDefeated, Is.False);
        });
    }

    [Test]
    public void EliteEncounterMultipliersAreExplicitData()
    {
        var elite = new EncounterDefinition(new ContentId("encounter.pure-run.e1"),
            new ContentId("battle-layout.pure-run.center-blocker"), Array.Empty<EncounterMonsterDefinition>(),
            HealthMultiplier: 1.3f, OutputMultiplier: 1.15f, MinimumStartingMana: 8,
            Class: EncounterClass.Elite);
        Assert.Multiple(() =>
        {
            Assert.That(elite.HealthMultiplier, Is.EqualTo(1.3f));
            Assert.That(elite.OutputMultiplier, Is.EqualTo(1.15f));
            Assert.That(elite.MinimumStartingMana, Is.EqualTo(8));
            Assert.That(elite.Class, Is.EqualTo(EncounterClass.Elite));
        });
    }

    private static PureRunBattleResult Result(PureRunState pending, bool victory) => new(pending.RunId,
        pending.Checkpoint!.Revision, pending.EncounterContentId, victory, 3, 2,
        pending.Party.Select(value => new BattlePartyResult(value.CharacterId, value.CurrentHealth,
            value.CurrentMana, value.IsDead, value.CarriedConsumables)).ToArray());

    private static PureRunState Run(PureRunPhase phase, IReadOnlyList<PendingProgression>? progression = null)
    {
        UnitAttributes attributes = new(5, 5, 5, 5, 5, 5);
        string[] characterIds = ["mage", "necro", "amazon"];
        RunCharacterState[] party = characterIds.Select(id => new RunCharacterState(id,
            new ContentId($"unit.{id}"), 2, attributes, 20, 24, 10, 15, false,
            [new ContentId($"skill.{id}.lv2")])).ToArray();
        return new PureRunState("run", 42, 10, phase, phase == PureRunPhase.ReadyForBoss ? 5 : 3,
            new ContentId("encounter.pure-run.n4"), party, pendingProgression: progression,
            battlesCompleted: 4, mapState: new PureRunMapService(Map()).UnlockLayerFour(42));
    }

    private static PureRunMapDefinition Map() => new(new ContentId("run-map.pure-run.layer4-v1"), 2,
    [
        new("layer_04_battle", 4, PureRunNodeKind.Battle, new ContentId("encounter.pure-run.n4")),
        new("layer_04_rest", 4, PureRunNodeKind.Rest, new ContentId("rest.pure-run.standard-v1")),
        new("layer_04_store", 4, PureRunNodeKind.Store, new ContentId("store.pure-run.standard-v1")),
        new("layer_04_event", 4, PureRunNodeKind.Mystery, new ContentId("event.pure-run.cursed-chest")),
        new("layer_06_battle", 6, PureRunNodeKind.Battle, new ContentId("encounter.pure-run.e1")),
        new("layer_06_rest", 6, PureRunNodeKind.Rest, new ContentId("rest.pure-run.standard-v1")),
        new("layer_06_store", 6, PureRunNodeKind.Store, new ContentId("store.pure-run.standard-v1")),
        new("layer_06_event", 6, PureRunNodeKind.Mystery, new ContentId("event.pure-run.cursed-chest"))
    ]);
}
