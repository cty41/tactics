using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class RunSaveDocumentV8Tests
{
    [Test]
    public void RoundTripPreservesEscortLifecycleAndNodeIdentity()
    {
        PureRunState run = Run(new RunEscortState("escort.lost-villager.v1", RunEscortLifecycle.Traveling,
            true, "layer_04_event", "layer_06_event", 4));
        var snapshot = new PureRunSaveSnapshot(run.Revision, run, null);

        RunSaveDecodeResultV8 decoded = RunSaveDocumentV8.Decode(RunSaveDocumentV8.Encode(snapshot));

        Assert.That(decoded.Succeeded, Is.True);
        Assert.That(decoded.Snapshot!.ActiveRun!.EscortState, Is.EqualTo(run.EscortState));
        Assert.That(decoded.MigratedFromSchema, Is.Zero);
        Assert.That(decoded.RequiresNewRun, Is.False);
    }

    [Test]
    public void OlderActiveRunRequiresRestartAtV8Boundary()
    {
        var snapshot = new PureRunSaveSnapshot(4, Run(null), null);
        RunSaveDecodeResultV8 decoded = RunSaveDocumentV8.Decode(RunSaveDocumentV7.Encode(snapshot));
        Assert.That(decoded.Succeeded, Is.True);
        Assert.That(decoded.MigratedFromSchema, Is.EqualTo(7));
        Assert.That(decoded.RequiresNewRun, Is.True);
        Assert.That(decoded.Snapshot!.ActiveRun, Is.Null);
    }

    private static PureRunState Run(RunEscortState? escort)
    {
        UnitAttributes attributes = new(5, 5, 5, 5, 5, 5);
        RunCharacterState[] party = new[] { "mage", "necro", "amazon" }.Select(id => new RunCharacterState(
            id, new ContentId("unit." + id), 1, attributes, 20, 20, 10, 10, false,
            [new ContentId("skill." + id)])).ToArray();
        return new PureRunState("run", 7, 4, PureRunPhase.AwaitingLayerSixChoice, 5,
            new ContentId("encounter.pure-run.n5"), party, escortState: escort);
    }
}
