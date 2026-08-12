using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Items;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class PureRunLayerFourNodeServiceTests
{
    private readonly PureRunLayerFourNodeService _service = new();

    [Test]
    public void SelectingRouteLocksOthersAndRestCommitsOnce()
    {
        PureRunState selected = Select(CreateRun(), "layer_04_rest").State;
        Assert.That(selected.MapState!.ReachableNodeIds, Is.EqualTo(new[] { "layer_04_rest" }));
        LayerFourNodeResolution result = _service.ConfirmRest(selected);
        Assert.Multiple(() =>
        {
            Assert.That(result.State.Party[0].CurrentHealth, Is.EqualTo(14));
            Assert.That(result.State.Party[0].CurrentMana, Is.EqualTo(8));
            Assert.That(result.State.Phase, Is.EqualTo(PureRunPhase.ReadyForLayerFive));
            Assert.That(result.State.MapState!.NodeLifecycle, Is.EqualTo(RunNodeLifecycle.Committed));
            Assert.That(result.State.MapState.ReachableNodeIds, Is.Empty);
        });
        Assert.That(_service.ConfirmRest(result.State).WasDuplicate, Is.True);
    }

    [Test]
    public void StoreOffersAndPurchasesPersistDeterministically()
    {
        PureRunState run = Select(CreateRun(gold: 20), "layer_04_store").State;
        RunStoreOffer[] items = [new(new ContentId("item.a"), 2, true), new(new ContentId("item.b"), 3, true)];
        RunStoreOffer[] gear = [new(new ContentId("item.c"), 4, false), new(new ContentId("item.d"), 5, false)];
        LayerFourNodeResolution opened = _service.OpenStore(run, gear, items);
        LayerFourNodeResolution replay = _service.OpenStore(opened.State, gear.Reverse(), items.Reverse());
        Assert.That(replay.StoreOffers, Is.EqualTo(opened.StoreOffers));
        Assert.That(opened.StoreOffers, Has.Count.EqualTo(3));
        Assert.That(opened.StoreOffers!.Count(value => value.IsConsumable), Is.GreaterThanOrEqualTo(1));
        RunStoreOfferState offer = opened.StoreOffers!.First(value => value.IsConsumable);
        var definitions = new Dictionary<ContentId, ConsumableDefinition>
        {
            [offer.ContentId] = new(offer.ContentId, "source", "Item", "", ItemRarity.Common, offer.Price, 1,
                ConsumableEffectKind.RestoreHealth, 1, 1, ConsumableTargetMode.Self)
        };
        LayerFourNodeResolution bought = _service.Purchase(opened.State, offer.InstanceId, definitions,
            new Dictionary<ContentId, EquipmentDefinition>());
        Assert.That(bought.State.Gold, Is.EqualTo(20 - offer.Price));
        Assert.That(bought.State.BackpackConsumables.Single().InstanceId, Is.EqualTo(offer.InstanceId));
        Assert.That(_service.Purchase(bought.State, offer.InstanceId, definitions,
            new Dictionary<ContentId, EquipmentDefinition>()).RejectionCode, Is.EqualTo("store.offer_already_purchased"));
        Assert.That(_service.LeaveStore(bought.State).State.Phase, Is.EqualTo(PureRunPhase.ReadyForLayerFive));
    }

    [Test]
    public void MysteryRollIsPersistedAndDamageIsAppliedOnce()
    {
        PureRunState run = Select(CreateRun(allAtOneHealth: true), "layer_04_event").State;
        string eventId = run.MapState!.MysteryEventAssignments["layer_04_event"];
        LayerFourNodeResolution resolved = _service.ResolveMystery(run, eventId, "risk", "mage", 0, 5,
            "Nothing", 0, null, "Damage", 99, null);
        LayerFourNodeResolution replay = _service.ResolveMystery(resolved.State, eventId, "other", "amazon", 100, 99,
            "Gold", 50, null, "Nothing", 0, null);
        Assert.That(replay.EventOutcome, Is.EqualTo(resolved.EventOutcome));
        LayerFourNodeResolution confirmed = _service.ConfirmMystery(resolved.State,
            new Dictionary<ContentId, ConsumableDefinition>());
        Assert.Multiple(() =>
        {
            Assert.That(confirmed.State.Party.Single(value => value.CharacterId == "mage").IsDead, Is.True);
            Assert.That(confirmed.State.Phase, Is.EqualTo(PureRunPhase.ReadyForLayerFive));
            Assert.That(confirmed.State.MapState!.MysteryResolution!.Confirmed, Is.True);
        });
    }

    [Test]
    public void MysteryDamageThatKillsLastLivingCharacterEntersDefeated()
    {
        PureRunState initial = CreateRun(allAtOneHealth: true);
        RunCharacterState[] party = initial.Party.Select((value, index) => index == 0 ? value :
            new RunCharacterState(value.CharacterId, value.UnitContentId, value.Level, value.Attributes, 0,
                value.MaxHealth, value.CurrentMana, value.MaxMana, true, value.LearnedSkills)).ToArray();
        var wounded = new PureRunState(initial.RunId, initial.Seed, initial.Revision, initial.Phase,
            initial.EncounterIndex, initial.EncounterContentId, party, gold: initial.Gold);
        PureRunState run = Select(wounded, "layer_04_event").State;
        string eventId = run.MapState!.MysteryEventAssignments["layer_04_event"];
        LayerFourNodeResolution resolved = _service.ResolveMystery(run, eventId, "risk", "mage", 0, 5,
            "Nothing", 0, null, "Damage", 99, null);
        Assert.That(_service.ConfirmMystery(resolved.State,
            new Dictionary<ContentId, ConsumableDefinition>()).State.Phase, Is.EqualTo(PureRunPhase.Defeated));
    }

    private LayerFourNodeResolution Select(PureRunState run, string nodeId) => _service.SelectNode(run, Map(), nodeId);

    private static PureRunMapDefinition Map() => new(new ContentId("run-map.pure-run.layer4-v1"), 2,
    [
        new("layer_04_battle", 4, PureRunNodeKind.Battle, new ContentId("encounter.pure-run.n4")),
        new("layer_04_rest", 4, PureRunNodeKind.Rest, new ContentId("rest.pure-run.standard-v1")),
        new("layer_04_store", 4, PureRunNodeKind.Store, new ContentId("store.pure-run.standard-v1")),
        new("layer_04_event", 4, PureRunNodeKind.Mystery, new ContentId("event.pure-run.cursed-chest"))
    ]);

    private static PureRunState CreateRun(int gold = 0, bool allAtOneHealth = false)
    {
        UnitAttributes attributes = new(5, 5, 5, 5, 5, 5);
        var party = new[] { "mage", "necro", "amazon" }.Select(id => new RunCharacterState(id,
            new ContentId($"unit.{id}"), 1, attributes, allAtOneHealth ? 1 : 6, 24, 3, 15, false,
            [new ContentId($"skill.{id}")])).ToArray();
        return new PureRunState("run", 42, 4, PureRunPhase.AwaitingLayerFourChoice, 2,
            new ContentId("encounter.pure-run.n3"), party, gold: gold);
    }
}
