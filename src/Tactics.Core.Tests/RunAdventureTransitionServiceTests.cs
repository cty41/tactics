using NUnit.Framework;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

public sealed class RunAdventureTransitionServiceTests
{
    [Test]
    public void RouteAndEventTransitionsArePersistedAndMonotonic()
    {
        var service = new RunAdventureTransitionService();
        PureRunState run = Run(service);
        run = service.BeginRouteSelection(run, new ContentId("adventure-board.route"));
        run = service.SelectRoute(run, 1, "route-a-rest");
        run = service.SelectRoute(run, 2, "route-b-event");
        run = service.CommitRoute(run);
        run = service.ActivateMap(run, new ContentId("map.main"));
        run = service.BeginEventBattle(run, RunAdventureEventContextKind.CursedChestMimic, "node-event", "cursed-chest");

        long pendingRevision = run.Revision;
        Assert.That(run.AdventureState!.PendingEventContext, Is.EqualTo(RunAdventureEventContextKind.CursedChestMimic));
        run = service.ResolveEventBattle(run);
        Assert.Multiple(() =>
        {
            Assert.That(run.Revision, Is.GreaterThan(pendingRevision));
            Assert.That(run.AdventureState!.PendingEventContext, Is.EqualTo(RunAdventureEventContextKind.None));
            Assert.That(run.AdventureState.PendingEventNodeId, Is.Null);
            Assert.That(run.AdventureState.PendingEventObjectId, Is.Null);
        });
        Assert.That(service.ResolveEventBattle(run), Is.SameAs(run));
    }

    [Test]
    public void LeaderMovementTreatsIdlePartyAsObstacles()
    {
        var service = new RunAdventureTransitionService();
        PureRunState run = Run(service);
        AdventureBoardDefinition board = Board(run.AdventureState!);
        Assert.Throws<InvalidOperationException>(() => service.MoveLeader(run, board, new GridPoint(1, 4)));
        PureRunState moved = service.MoveLeader(run, board, new GridPoint(3, 5));
        Assert.That(moved.AdventureState!.ActorCells.Single(value => value.ActorId == "mage").Cell,
            Is.EqualTo(new GridPoint(3, 5)));
    }

    private static PureRunState Run(RunAdventureTransitionService service)
    {
        UnitAttributes attributes = new(5, 5, 5, 5, 5, 5);
        RunCharacterState[] party = new[] { "mage", "necro", "amazon" }.Select(id => new RunCharacterState(
            id, new ContentId("unit." + id), 1, attributes, 20, 20, 10, 10, false, [new ContentId("skill." + id)])).ToArray();
        return new PureRunState("run", 7, 1, PureRunPhase.Ready, 0, new ContentId("encounter.n1"), party,
            adventureState: service.CreateInitial(party));
    }

    private static AdventureBoardDefinition Board(RunAdventureState state) => new(
        state.BoardContentId, 10, 10, [], [], state.ActorCells.Select(value => new AdventureActorPlacement(value.ActorId, value.Cell)).ToArray(),
        new GridPoint(0, 0), new GridPoint(9, 9));
}
