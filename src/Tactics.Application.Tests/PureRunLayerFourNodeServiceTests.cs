using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class PureRunLayerFourNodeServiceTests
{
    [Test]
    public void Rest_RecoversThirtyPercentAndIsIdempotent()
    {
        PureRunState run = CreateRun();
        var tx = new RunNodeTransaction("node:rest:resolve", "layer_04_rest", PureRunNodeKind.Rest);
        LayerFourNodeResolution result = new PureRunLayerFourNodeService().ResolveRest(run, tx);
        Assert.That(result.State.Party[0].CurrentHealth, Is.EqualTo(14));
        Assert.That(result.State.Party[0].CurrentMana, Is.EqualTo(8));
        Assert.That(new PureRunLayerFourNodeService().ResolveRest(result.State, tx).WasDuplicate, Is.True);
    }

    [Test]
    public void StoreAndEvent_AreStableForSameSeed()
    {
        PureRunState run = CreateRun();
        var store = new RunNodeTransaction("node:store:resolve", "layer_04_store", PureRunNodeKind.Store);
        RunStoreOffer[] items = [new(new ContentId("item.a"), 2, true), new(new ContentId("item.b"), 3, true)];
        RunStoreOffer[] gear = [new(new ContentId("item.c"), 4, false), new(new ContentId("item.d"), 5, false)];
        var service = new PureRunLayerFourNodeService();
        Assert.That(service.OpenStore(run, store, gear, items).StoreOffers,
            Is.EqualTo(service.OpenStore(run, store, gear, items).StoreOffers));
        var evt = new RunNodeTransaction("node:event:resolve", "layer_04_event", PureRunNodeKind.Mystery);
        RunEventOutcome? first = service.ResolveEvent(run, evt, "event", "option", "mage", 60, 6, "Gold", 20, "Nothing", 0).EventOutcome;
        RunEventOutcome? replay = service.ResolveEvent(run, evt, "event", "option", "mage", 60, 6, "Gold", 20, "Nothing", 0).EventOutcome;
        Assert.That(first, Is.EqualTo(replay));
    }

    private static PureRunState CreateRun()
    {
        UnitAttributes attributes = new(5, 5, 5, 5, 5, 5);
        string[] ids = ["mage", "necro", "amazon"];
        var party = ids.Select(id => new RunCharacterState(id, new ContentId($"unit.{id}"), 1, attributes,
            6, 24, 3, 15, false, [new ContentId($"skill.{id}")])).ToArray();
        return new PureRunState("run", 42, 4, PureRunPhase.Ready, 2, new ContentId("encounter.pure-run.n3"), party);
    }
}
