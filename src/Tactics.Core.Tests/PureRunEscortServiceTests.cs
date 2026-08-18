using NUnit.Framework;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

public sealed class PureRunEscortServiceTests
{
    [Test]
    public void EscortPersistsAcrossTravelAndRequiresNpcSurvivalForCompletion()
    {
        var service = new PureRunEscortService();
        PureRunState accepted = service.Accept(Run(), "escort.lost-villager.v1", "layer_04_event",
            "layer_06_event").State;
        Assert.That(accepted.EscortState!.Lifecycle, Is.EqualTo(RunEscortLifecycle.Accepted));

        PureRunState traveling = service.BeginTravel(accepted).State;
        Assert.That(traveling.EscortState!.DestinationNodeId, Is.EqualTo("layer_06_event"));
        PureRunState pending = service.BeginBattle(traveling).State;
        Assert.That(service.ResolveBattle(pending, enemiesDefeated: true, protectedNpcAlive: true)
            .State.EscortState!.Lifecycle, Is.EqualTo(RunEscortLifecycle.Completed));
        RunEscortState failed = service.ResolveBattle(pending, enemiesDefeated: true, protectedNpcAlive: false)
            .State.EscortState!;
        Assert.That(failed.Lifecycle, Is.EqualTo(RunEscortLifecycle.Failed));
        Assert.That(failed.ProtectedNpcAlive, Is.False);
    }

    [Test]
    public void EscortTransitionsRejectDuplicateOrOutOfOrderUse()
    {
        var service = new PureRunEscortService();
        PureRunState run = Run();
        Assert.That(service.BeginTravel(run).RejectionCode, Is.EqualTo("escort.not_accepted"));
        PureRunState accepted = service.Accept(run, "quest", "a", "b").State;
        Assert.That(service.Accept(accepted, "other", "a", "b").RejectionCode,
            Is.EqualTo("escort.already_active"));
        Assert.That(service.BeginBattle(accepted).RejectionCode, Is.EqualTo("escort.battle_unavailable"));
    }

    private static PureRunState Run()
    {
        UnitAttributes attributes = new(5, 5, 5, 5, 5, 5);
        RunCharacterState[] party = new[] { "mage", "necro", "amazon" }.Select(id => new RunCharacterState(
            id, new ContentId("unit." + id), 1, attributes, 20, 20, 10, 10, false,
            [new ContentId("skill." + id)])).ToArray();
        return new PureRunState("run", 7, 1, PureRunPhase.AwaitingLayerFourChoice, 3,
            new ContentId("encounter.pure-run.n3"), party);
    }
}
