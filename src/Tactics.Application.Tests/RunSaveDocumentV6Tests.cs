using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class RunSaveDocumentV6Tests
{
    [Test]
    public void V6RoundTripPreservesMapIdentityAndTreasureOutcome()
    {
        var treasure = new RunTreasureResolutionState("layer_04_treasure", 4,
            new ContentId("item.equipment.staff-01"), null,
            new ContentId("buff.event-damage-reduction"), "mage");
        var map = new PureRunMapState(PureRunMapPhase.ResolvingNode, "layer_03_battle",
            ["layer_04_treasure"], ["start", "layer_01_battle", "layer_02_battle", "layer_03_battle"],
            new Dictionary<string, string>(), "layer_04_treasure", "node:layer_04_treasure:resolve",
            "layer_04_treasure", RunNodeLifecycle.Resolved, MapContentId: new ContentId("run-map.pure-run.default-v1"),
            MapLayoutVersion: 3, TreasureResolution: treasure);
        var party = new[] { "mage", "necro", "amazon" }.Select(value => new RunCharacterState(value,
            new ContentId($"unit.{value}"), 1, new UnitAttributes(5, 5, 5, 5, 5, 5), 20, 20, 10, 10,
            false, [new ContentId($"skill.{value}.lv1")])).ToArray();
        var run = new PureRunState("run", 7, 9, PureRunPhase.ResolvingLayerFourNode, 2,
            new ContentId("encounter.pure-run.n3"), party, mapState: map,
            nodeTransaction: new RunNodeTransaction("node:layer_04_treasure:resolve", "layer_04_treasure",
                PureRunNodeKind.Treasure));
        var snapshot = new PureRunSaveSnapshot(9, run, null);

        string first = RunSaveDocumentV6.Encode(snapshot);
        RunSaveDecodeResultV6 decoded = RunSaveDocumentV6.Decode(first);
        string second = RunSaveDocumentV6.Encode(decoded.Snapshot!);

        Assert.Multiple(() =>
        {
            Assert.That(decoded.Succeeded, Is.True);
            Assert.That(decoded.Snapshot!.ActiveRun!.MapState!.MapContentId,
                Is.EqualTo(new ContentId("run-map.pure-run.default-v1")));
            Assert.That(decoded.Snapshot.ActiveRun.MapState.TreasureResolution, Is.EqualTo(treasure));
            Assert.That(second, Is.EqualTo(first));
        });
    }

    [Test]
    public void V5MigratesDeterministically()
    {
        var snapshot = new PureRunSaveSnapshot(0, null, null);
        RunSaveDecodeResultV6 decoded = RunSaveDocumentV6.Decode(RunSaveDocumentV5.Encode(snapshot));
        Assert.Multiple(() =>
        {
            Assert.That(decoded.Succeeded, Is.True);
            Assert.That(decoded.MigratedFromSchema, Is.EqualTo(5));
            Assert.That(RunSaveDocumentV6.Encode(decoded.Snapshot!), Does.Contain("\"schemaVersion\": 6"));
        });
    }
}
