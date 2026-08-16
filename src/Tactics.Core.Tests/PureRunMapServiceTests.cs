using NUnit.Framework;
using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Core.Tests;

public sealed class PureRunMapServiceTests
{
    [Test]
    public void LayerFour_IsStableAndCommitsOnlyOneReachableNode()
    {
        PureRunMapService service = CreateService();
        PureRunMapState first = service.UnlockLayerFour(42);
        PureRunMapState replay = service.UnlockLayerFour(42);
        Assert.That(first.ReachableNodeIds, Is.EqualTo(replay.ReachableNodeIds));
        Assert.That(first.VisitedNodeIds, Is.EqualTo(replay.VisitedNodeIds));
        Assert.That(first.MysteryEventAssignments, Is.EquivalentTo(replay.MysteryEventAssignments));
        Assert.That(first.ReachableNodeIds, Has.Count.EqualTo(4));

        PureRunMapResult begun = service.BeginNode(first, "layer_04_rest");
        Assert.That(begun.Succeeded, Is.True);
        PureRunMapResult committed = service.CommitNode(begun.State, begun.Transaction!, Array.Empty<string>());
        Assert.That(committed.Succeeded, Is.True);
        Assert.That(committed.State.Phase, Is.EqualTo(PureRunMapPhase.ReadyForLayerFive));
        Assert.That(service.BeginNode(committed.State, "layer_04_store").RejectionCode, Is.EqualTo("map.not_choosing"));
    }

    [Test]
    public void InvalidAndDuplicateTransactions_DoNotMutateState()
    {
        PureRunMapService service = CreateService();
        PureRunMapState state = service.UnlockLayerFour(7);
        Assert.That(service.BeginNode(state, "layer_05_battle").Succeeded, Is.False);
        PureRunMapResult begun = service.BeginNode(state, "layer_04_event");
        PureRunMapResult duplicate = service.CommitNode(begun.State, begun.Transaction!, [begun.Transaction!.TransactionKey]);
        Assert.That(duplicate.WasDuplicate, Is.True);
        Assert.That(duplicate.State, Is.EqualTo(begun.State));
    }

    private static PureRunMapService CreateService() => new(new PureRunMapDefinition(
        new ContentId("run-map.pure-run.layer4-v1"), 2,
        new[]
        {
            new PureRunMapNodeDefinition("layer_04_battle", 4, PureRunNodeKind.Battle, new ContentId("encounter.pure-run.n4")),
            new PureRunMapNodeDefinition("layer_04_rest", 4, PureRunNodeKind.Rest, new ContentId("rest.pure-run.standard-v1")),
            new PureRunMapNodeDefinition("layer_04_store", 4, PureRunNodeKind.Store, new ContentId("store.pure-run.standard-v1")),
            new PureRunMapNodeDefinition("layer_04_event", 4, PureRunNodeKind.Mystery, new ContentId("event.pure-run.cursed-chest"))
        }));
}
