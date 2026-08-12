using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class RunSaveDocumentV3Tests
{
    [Test]
    public void V3_RoundTripsMapAndPendingTransactionDeterministically()
    {
        PureRunSaveSnapshot snapshot = CreateSnapshot();
        string first = RunSaveDocumentV3.Encode(snapshot);
        string second = RunSaveDocumentV3.Encode(snapshot);
        Assert.That(first, Is.EqualTo(second));
        RunSaveDecodeResultV3 decoded = RunSaveDocumentV3.Decode(first);
        Assert.That(decoded.Succeeded, Is.True);
        Assert.That(decoded.Snapshot!.ActiveRun!.MapState!.PendingNodeId, Is.EqualTo("layer_04_event"));
        Assert.That(decoded.Snapshot.ActiveRun.NodeTransaction!.TransactionKey, Is.EqualTo("node:layer_04_event:resolve"));
        Assert.That(decoded.Snapshot.ActiveRun.MapState.NodeLifecycle, Is.EqualTo(RunNodeLifecycle.Pending));
        Assert.That(decoded.Snapshot.ActiveRun.MapState.StoreOffers, Has.Count.EqualTo(1));
    }

    [Test]
    public void V2_IsAcceptedAndReportedForMigration()
    {
        string legacy = RunSaveDocumentV2.Encode(CreateSnapshot());
        RunSaveDecodeResultV3 decoded = RunSaveDocumentV3.Decode(legacy);
        Assert.That(decoded.Succeeded, Is.True);
        Assert.That(decoded.MigratedFromSchema, Is.EqualTo(2));
        Assert.That(RunSaveDocumentV3.Encode(decoded.Snapshot!), Does.Contain("\"schemaVersion\": 3"));
    }

    private static PureRunSaveSnapshot CreateSnapshot()
    {
        UnitAttributes attributes = new(5, 5, 5, 5, 5, 5);
        var party = new[] { "mage", "necro", "amazon" }.Select(id => new RunCharacterState(id,
            new ContentId($"unit.{id}"), 1, attributes, 20, 20, 10, 10, false, [new ContentId($"skill.{id}")])).ToArray();
        var map = new PureRunMapState(PureRunMapPhase.ResolvingNode, "layer_03_battle", ["layer_04_event"],
            ["start", "layer_03_battle"], new Dictionary<string, string> { ["layer_04_event"] = "lost_villager_001" },
            "layer_04_event", "node:layer_04_event:resolve", "layer_04_event", RunNodeLifecycle.Pending,
            [new RunStoreOfferState(new ContentId("item.consumable.life-potion"), 3, true,
                new Tactics.Core.Items.ItemInstanceId("store-1"))]);
        var tx = new RunNodeTransaction("node:layer_04_event:resolve", "layer_04_event", PureRunNodeKind.Mystery);
        var run = new PureRunState("run", 9, 4, PureRunPhase.ResolvingLayerFourNode, 2,
            new ContentId("encounter.pure-run.n3"), party, mapState: map, nodeTransaction: tx);
        return new PureRunSaveSnapshot(4, run, null);
    }
}
