using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

[TestFixture]
public sealed class PureRunFlowProjectorTests
{
    private readonly PureRunFlowProjector _projector = new();

    [Test]
    public void ReadyRunProjectsFixedSevenLayerMapAndCurrentEncounter()
    {
        PureRunFlowSnapshot snapshot = _projector.Project(Run(PureRunPhase.Ready), Definition(), Map());

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Page, Is.EqualTo(PureRunFlowPage.Map));
            Assert.That(snapshot.Map!.Nodes, Has.Count.EqualTo(14));
            Assert.That(snapshot.Map.Connections, Has.Count.EqualTo(19));
            Assert.That(snapshot.Map.Connections, Has.All.Property(nameof(PureRunMapConnectionSnapshot.Revealed)).True);
            Assert.That(Node(snapshot, "layer_01_battle").State, Is.EqualTo(PureRunMapNodeState.Current));
            Assert.That(snapshot.Map.FocusNodeId, Is.EqualTo("layer_01_battle"));
        });
    }

    [Test]
    public void LayerFourChoiceRevealsExactlyFourAvailableRoutes()
    {
        PureRunState run = Run(PureRunPhase.AwaitingLayerFourChoice, battles: 3,
            map: new PureRunMapService(Map()).UnlockLayerFour(7));
        PureRunFlowSnapshot snapshot = _projector.Project(run, Definition(), Map());

        Assert.That(snapshot.Map!.Nodes.Where(value => value.State == PureRunMapNodeState.Available)
            .Select(value => value.NodeId), Is.EquivalentTo(new[]
        {
            "layer_04_battle", "layer_04_event", "layer_04_rest", "layer_04_store"
        }));
    }

    [Test]
    public void LegacyLayerFourChoiceWithoutMapStateStillRevealsFourRoutes()
    {
        PureRunState run = Run(PureRunPhase.AwaitingLayerFourChoice, battles: 3);

        PureRunFlowSnapshot snapshot = _projector.Project(run, Definition(), Map());

        Assert.That(snapshot.Map!.Nodes.Where(value => value.State == PureRunMapNodeState.Available)
            .Select(value => value.NodeId), Is.EquivalentTo(new[]
        {
            "layer_04_battle", "layer_04_event", "layer_04_rest", "layer_04_store"
        }));
    }

    [Test]
    public void SelectedRouteLocksSiblingsAndRestoresNodePage()
    {
        PureRunMapDefinition map = Map();
        PureRunMapState choosing = new PureRunMapService(map).UnlockLayerFour(7);
        PureRunMapResult begun = new PureRunMapService(map).BeginNode(choosing, "layer_04_store");
        PureRunState run = Run(PureRunPhase.ResolvingLayerFourNode, battles: 3, map: begun.State,
            transaction: begun.Transaction);
        PureRunFlowSnapshot snapshot = _projector.Project(run, Definition(), map);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Page, Is.EqualTo(PureRunFlowPage.Store));
            Assert.That(Node(snapshot, "layer_04_store").State, Is.EqualTo(PureRunMapNodeState.Selected));
            Assert.That(Node(snapshot, "layer_04_rest").UnavailableReason, Is.EqualTo("map.route_locked"));
        });
    }

    [Test]
    public void PendingProgressionOwnsFlowAndBlocksMapEntry()
    {
        PureRunState run = Run(PureRunPhase.ReadyForLayerSix, battles: 4,
            progression: [new PendingProgression("progression:e1", "e1", "mage")]);
        PureRunFlowSnapshot snapshot = _projector.Project(run, Definition(), Map());

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Page, Is.EqualTo(PureRunFlowPage.Progression));
            Assert.That(snapshot.BlockingReason, Is.EqualTo("progression.required"));
            Assert.That(snapshot.Actions, Does.Contain(PureRunFlowAction.CompleteProgression));
            Assert.That(snapshot.Actions, Does.Not.Contain(PureRunFlowAction.BeginAvailableNode));
        });
    }

    [Test]
    public void LayerSixRoutesBecomeAvailableOnlyAfterEliteProgressionIsConsumed()
    {
        PureRunFlowSnapshot snapshot = _projector.Project(
            Run(PureRunPhase.ReadyForLayerSix, battles: 4), Definition(), Map());

        Assert.That(snapshot.Map!.Nodes.Where(value => value.State == PureRunMapNodeState.Available)
            .Select(value => value.NodeId), Is.EquivalentTo(new[]
        {
            "layer_06_battle", "layer_06_event", "layer_06_rest", "layer_06_store"
        }));
    }

    [Test]
    public void PendingBattleProjectsBattleAndStableResumeAction()
    {
        PureRunFlowSnapshot snapshot = _projector.Project(Run(PureRunPhase.PendingBattle), Definition(), Map());
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Page, Is.EqualTo(PureRunFlowPage.Battle));
            Assert.That(snapshot.Actions, Does.Contain(PureRunFlowAction.ResumeBattle));
            Assert.That(Node(snapshot, "layer_01_battle").State, Is.EqualTo(PureRunMapNodeState.Pending));
        });
    }

    [Test]
    public void TerminalSummaryDoesNotPretendThereIsAnActiveMap()
    {
        PureRunFlowSnapshot snapshot = _projector.ProjectTerminal(new PureRunSummary("run", 7,
            PureRunOutcome.BossVictory, 7, 12, 30, [], [], []));
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Page, Is.EqualTo(PureRunFlowPage.Summary));
            Assert.That(snapshot.Map, Is.Null);
            Assert.That(snapshot.Actions, Is.EqualTo(new[] { PureRunFlowAction.ReturnHome }));
        });
    }

    [TestCase(PureRunNodeKind.Battle)]
    [TestCase(PureRunNodeKind.Rest)]
    [TestCase(PureRunNodeKind.Store)]
    [TestCase(PureRunNodeKind.Mystery)]
    public void BossIdentityWinsOverCommittedLayerSixTransaction(PureRunNodeKind priorKind)
    {
        PureRunState state = Run(PureRunPhase.ReadyForBoss, battles: 6,
            transaction: new RunNodeTransaction("node:layer_06:resolve", $"layer_06_{priorKind.ToString().ToLowerInvariant()}", priorKind, true));
        PureRunFullRunService service = new();
        FullRunTransitionResult begun = service.BeginBoss(state, Map());
        PureRunBattleResult battle = new(begun.State.RunId, begun.State.Checkpoint!.Revision,
            new ContentId("encounter.pure-run.special"), true, 3, 1,
            begun.State.Party.Select(value => new BattlePartyResult(value.CharacterId, value.CurrentHealth,
                value.CurrentMana, value.IsDead, value.CarriedConsumables)).ToArray());
        FullRunTransitionResult completed = service.CompleteBoss(begun.State, battle);

        Assert.Multiple(() =>
        {
            Assert.That(completed.Succeeded, Is.True);
            Assert.That(completed.TerminalSummary?.Outcome, Is.EqualTo(PureRunOutcome.BossVictory));
            Assert.That(completed.TerminalSummary?.BossDefeated, Is.True);
        });
    }

    private static PureRunMapNodeSnapshot Node(PureRunFlowSnapshot snapshot, string id) =>
        snapshot.Map!.Nodes.Single(value => value.NodeId == id);

    private static PureRunState Run(PureRunPhase phase, int battles = 0, PureRunMapState? map = null,
        RunNodeTransaction? transaction = null, IReadOnlyList<PendingProgression>? progression = null)
    {
        UnitAttributes attributes = new(5, 5, 5, 6, 5, 5);
        RunCharacterState Character(string id, string unit, string skill) => new(id, new ContentId(unit), 1,
            attributes, 20, 20, 10, 10, false, [new ContentId(skill)]);
        return new PureRunState("run", 7, 1, phase, 0, new ContentId("encounter.pure-run.n1"),
        [
            Character("mage", "unit.pure-run.mage", "skill.mage.fireball.lv1"),
            Character("necromancer", "unit.pure-run.necromancer", "skill.necromancer.summon-skeleton.lv1"),
            Character("amazon", "unit.pure-run.amazon", "skill.amazon.thrust.lv1")
        ], pendingProgression: progression, battlesCompleted: battles, mapState: map, nodeTransaction: transaction);
    }

    private static PureRunDefinition Definition() => new(new ContentId("run.pure-run"),
        [new ContentId("encounter.pure-run.n1"), new ContentId("encounter.pure-run.n2"), new ContentId("encounter.pure-run.n3")],
        [
            new PureRunPartyTemplate("mage", new ContentId("unit.pure-run.mage"), new ContentId("skill.mage.fireball.lv1"), new UnitAttributes(5,5,5,6,5,5)),
            new PureRunPartyTemplate("necromancer", new ContentId("unit.pure-run.necromancer"), new ContentId("skill.necromancer.summon-skeleton.lv1"), new UnitAttributes(5,5,5,5,6,5)),
            new PureRunPartyTemplate("amazon", new ContentId("unit.pure-run.amazon"), new ContentId("skill.amazon.thrust.lv1"), new UnitAttributes(5,6,5,5,5,5))
        ]);

    private static PureRunMapDefinition Map() => new(new ContentId("run-map.pure-run"), 2,
    [
        new PureRunMapNodeDefinition("layer_04_battle",4,PureRunNodeKind.Battle,new ContentId("encounter.pure-run.n4")),
        new PureRunMapNodeDefinition("layer_04_rest",4,PureRunNodeKind.Rest,new ContentId("rest.standard")),
        new PureRunMapNodeDefinition("layer_04_store",4,PureRunNodeKind.Store,new ContentId("store.standard")),
        new PureRunMapNodeDefinition("layer_04_event",4,PureRunNodeKind.Mystery,new ContentId("event.mystery")),
        new PureRunMapNodeDefinition("layer_06_battle",6,PureRunNodeKind.Battle,new ContentId("encounter.pure-run.e1")),
        new PureRunMapNodeDefinition("layer_06_rest",6,PureRunNodeKind.Rest,new ContentId("rest.standard")),
        new PureRunMapNodeDefinition("layer_06_store",6,PureRunNodeKind.Store,new ContentId("store.standard")),
        new PureRunMapNodeDefinition("layer_06_event",6,PureRunNodeKind.Mystery,new ContentId("event.mystery"))
    ]);
}
