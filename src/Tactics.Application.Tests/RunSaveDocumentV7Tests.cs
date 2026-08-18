using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class RunSaveDocumentV7Tests
{
    [Test]
    public void V7_RoundTripsEmptySnapshot()
    {
        var snapshot = new PureRunSaveSnapshot(0, null, null);
        RunSaveDecodeResultV7 decoded = RunSaveDocumentV7.Decode(RunSaveDocumentV7.Encode(snapshot));
        Assert.Multiple(() =>
        {
            Assert.That(decoded.Succeeded, Is.True);
            Assert.That(decoded.RequiresNewRun, Is.False);
            Assert.That(decoded.Snapshot, Is.EqualTo(snapshot));
        });
    }

    [Test]
    public void V6ActiveRun_IsClearedButTerminalSummaryIsPreserved()
    {
        RunCharacterState[] party = new[] { "mage", "necro", "amazon" }.Select(value =>
            new RunCharacterState(value, new ContentId($"unit.{value}"), 1,
                new UnitAttributes(5, 5, 5, 5, 5, 5), 20, 20, 5, 15, false,
                [new ContentId($"skill.{value}.lv1")])).ToArray();
        var active = new PureRunState("run", 5, 3, PureRunPhase.Ready, 0,
            new ContentId("encounter.pure-run.n1"), party);
        var summary = new PureRunSummary("old-run", 4, PureRunOutcome.Defeated, 1, 2, 3,
            Array.Empty<ContentId>(), ["mage"], ["battle:n1:settlement"]);
        var source = new PureRunSaveSnapshot(3, active, summary);
        RunSaveDecodeResultV7 decoded = RunSaveDocumentV7.Decode(RunSaveDocumentV6.Encode(source));
        Assert.Multiple(() =>
        {
            Assert.That(decoded.Succeeded, Is.True);
            Assert.That(decoded.MigratedFromSchema, Is.EqualTo(6));
            Assert.That(decoded.RequiresNewRun, Is.True);
            Assert.That(decoded.Snapshot!.ActiveRun, Is.Null);
            Assert.That(decoded.Snapshot.PendingRunSetup, Is.Null);
            Assert.That(decoded.Snapshot.TerminalSummary!.RunId, Is.EqualTo(source.TerminalSummary!.RunId));
            Assert.That(decoded.Snapshot.TerminalSummary.Outcome, Is.EqualTo(source.TerminalSummary.Outcome));
            Assert.That(decoded.Snapshot.TerminalSummary.DeadCharacters,
                Is.EquivalentTo(source.TerminalSummary.DeadCharacters));
            Assert.That(decoded.Snapshot.TerminalSummary.AppliedTransactionKeys,
                Is.EquivalentTo(source.TerminalSummary.AppliedTransactionKeys));
        });
    }
}
