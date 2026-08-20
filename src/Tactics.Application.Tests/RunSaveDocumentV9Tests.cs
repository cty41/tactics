using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class RunSaveDocumentV9Tests
{
    [Test]
    public void RoundTripPreservesAdventureBoardAndEventContext()
    {
        PureRunState run = Run() with { };
        var service = new RunAdventureTransitionService();
        run = service.EnterBoard(run, new ContentId("adventure-board.node.layer-04-event"));
        run = service.BeginEventBattle(run, RunAdventureEventContextKind.FallenAltarGuardian, "node", "altar");

        RunSaveDecodeResultV9 decoded = RunSaveDocumentV9.Decode(RunSaveDocumentV9.Encode(new(run.Revision, run, null)));
        Assert.That(decoded.Succeeded, Is.True);
        RunAdventureState actual = decoded.Snapshot!.ActiveRun!.AdventureState!;
        Assert.Multiple(() =>
        {
            Assert.That(actual.Lifecycle, Is.EqualTo(run.AdventureState!.Lifecycle));
            Assert.That(actual.BoardContentId, Is.EqualTo(run.AdventureState.BoardContentId));
            Assert.That(actual.LeaderId, Is.EqualTo(run.AdventureState.LeaderId));
            Assert.That(actual.ActorCells, Is.EquivalentTo(run.AdventureState.ActorCells));
            Assert.That(actual.PendingEventContext, Is.EqualTo(RunAdventureEventContextKind.FallenAltarGuardian));
            Assert.That(actual.PendingEventNodeId, Is.EqualTo("node"));
            Assert.That(actual.PendingEventObjectId, Is.EqualTo("altar"));
        });
    }

    [Test]
    public void V8ActiveAndPendingSetupAreClearedButTerminalSummaryIsRetained()
    {
        PureRunState run = Run();
        var summary = new PureRunSummary("old", 1, PureRunOutcome.Defeated, 0, 0, 0, [], [], []);
        var setup = new PendingRunSetup(7, 0, []);
        RunSaveDecodeResultV9 decoded = RunSaveDocumentV9.Decode(RunSaveDocumentV8.Encode(new(run.Revision, run, summary, setup)));
        Assert.Multiple(() =>
        {
            Assert.That(decoded.RequiresNewRun, Is.True);
            Assert.That(decoded.Snapshot!.ActiveRun, Is.Null);
            Assert.That(decoded.Snapshot.PendingRunSetup, Is.Null);
            Assert.That(decoded.Snapshot.TerminalSummary, Is.EqualTo(summary));
        });
    }

    private static PureRunState Run()
    {
        UnitAttributes attributes = new(5, 5, 5, 5, 5, 5);
        RunCharacterState[] party = new[] { "mage", "necro", "amazon" }.Select(id => new RunCharacterState(
            id, new ContentId("unit." + id), 1, attributes, 20, 20, 10, 10, false, [new ContentId("skill." + id)])).ToArray();
        return new PureRunState("run", 7, 4, PureRunPhase.Ready, 0, new ContentId("encounter.n1"), party,
            adventureState: new RunAdventureTransitionService().CreateInitial(party));
    }
}
