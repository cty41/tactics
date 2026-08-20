using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Runs;
using Tactics.Core.Content;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

[TestFixture]
public sealed class RunSaveDocumentV10Tests
{
    [Test]
    public void V10RoundTripPreservesImmediateExitAdventureState()
    {
        PureRunState run = Run();
        var service = new RunAdventureTransitionService();
        run = service.EnterBoard(run, new ContentId("adventure-board.pure-run.node.layer-01-battle"));
        run = service.ResolveBoard(run);

        RunSaveDecodeResultV10 decoded = RunSaveDocumentV10.Decode(
            RunSaveDocumentV10.Encode(new PureRunSaveSnapshot(run.Revision, run, null)));

        Assert.Multiple(() =>
        {
            Assert.That(decoded.Succeeded, Is.True);
            Assert.That(decoded.RequiresNewRun, Is.False);
            Assert.That(decoded.MigratedFromSchema, Is.Zero);
            RunAdventureState actual = decoded.Snapshot!.ActiveRun!.AdventureState!;
            Assert.That(actual.BoardContentId, Is.EqualTo(run.AdventureState!.BoardContentId));
            Assert.That(actual.Lifecycle, Is.EqualTo(RunAdventureLifecycle.MapActive));
            Assert.That(actual.ActorCells, Is.EquivalentTo(run.AdventureState.ActorCells));
            Assert.That(actual.ExitRevision, Is.EqualTo(run.AdventureState.ExitRevision));
        });
    }

    [Test]
    public void V9ActiveRunRequiresNewRunButTerminalSummarySurvives()
    {
        PureRunState run = Run();
        var summary = new PureRunSummary(run.RunId, run.Seed, PureRunOutcome.Defeated, 1, 2, 3, [], [], []);

        RunSaveDecodeResultV10 decoded = RunSaveDocumentV10.Decode(
            RunSaveDocumentV9.Encode(new PureRunSaveSnapshot(run.Revision, run, summary)));

        Assert.Multiple(() =>
        {
            Assert.That(decoded.Succeeded, Is.True);
            Assert.That(decoded.RequiresNewRun, Is.True);
            Assert.That(decoded.MigratedFromSchema, Is.EqualTo(9));
            Assert.That(decoded.Snapshot!.ActiveRun, Is.Null);
            Assert.That(decoded.Snapshot.TerminalSummary, Is.EqualTo(summary));
        });
    }

    private static PureRunState Run()
    {
        UnitAttributes attributes = new(5, 5, 5, 5, 5, 5);
        RunCharacterState[] party = new[] { "mage", "necro", "amazon" }.Select(id => new RunCharacterState(
            id, new ContentId("unit." + id), 1, attributes, 20, 20, 10, 10, false,
            [new ContentId("skill." + id)])).ToArray();
        return new PureRunState("run", 7, 4, PureRunPhase.Ready, 0, new ContentId("encounter.n1"), party,
            adventureState: new RunAdventureTransitionService().CreateInitial(party));
    }
}
