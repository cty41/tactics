using Tactics.Core.Content;
using Tactics.Core.Items;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Runs;

public sealed record RunStoreOffer(ContentId ContentId, int Price, bool IsConsumable);
public sealed record RunEventOutcome(string EventId, string OptionId, string CharacterId, int SuccessRate, int Roll,
    bool Succeeded, string Effect, int Amount, ContentId? EffectContentId = null);
public sealed record LayerFourNodeResolution(bool Succeeded, string? RejectionCode, PureRunState State,
    IReadOnlyList<RunStoreOfferState>? StoreOffers = null, RunMysteryResolutionState? EventOutcome = null,
    EncounterRequest? EncounterRequest = null, bool WasDuplicate = false);

/// <summary>Owns deterministic Layer 4 route effects; adapters submit intents and never assemble final run state.</summary>
public sealed class PureRunLayerFourNodeService
{
    private const int BackpackCapacity = 20;

    public LayerFourNodeResolution SelectNode(PureRunState run, PureRunMapDefinition map, string nodeId)
    {
        if (run.Phase != PureRunPhase.AwaitingLayerFourChoice || run.PendingProgression.Count != 0)
            return Fail("layer4.choice_unavailable", run);
        PureRunMapState current = run.MapState ?? new PureRunMapService(map).UnlockLayerFour(run.Seed);
        PureRunMapResult begun = new PureRunMapService(map).BeginNode(current, nodeId);
        if (!begun.Succeeded) return Fail(begun.RejectionCode!, run);
        PureRunState updated = Copy(run, phase: PureRunPhase.ResolvingLayerFourNode,
            map: begun.State, transaction: begun.Transaction, checkpoint: null);
        return new(true, null, updated);
    }

    public LayerFourNodeResolution PreviewRest(PureRunState run) =>
        Validate(run, PureRunNodeKind.Rest, out LayerFourNodeResolution? failure) ?
            new(true, null, run, WasDuplicate: false) : failure!;

    public LayerFourNodeResolution ConfirmRest(PureRunState run)
    {
        if (!Validate(run, PureRunNodeKind.Rest, out LayerFourNodeResolution? failure)) return failure!;
        RunCharacterState[] party = run.Party.Select(character => character.IsDead ? character : CopyVitals(character,
            Math.Min(character.MaxHealth, character.CurrentHealth + Percent(character.MaxHealth, 30)),
            Math.Min(character.MaxMana, character.CurrentMana + Percent(character.MaxMana, 30)))).ToArray();
        return Commit(run, party: party);
    }

    public LayerFourNodeResolution OpenStore(PureRunState run, IEnumerable<RunStoreOffer> equipment,
        IEnumerable<RunStoreOffer> consumables)
    {
        if (!Validate(run, PureRunNodeKind.Store, out LayerFourNodeResolution? failure)) return failure!;
        if (run.MapState!.StoreOffers is { Count: > 0 } persisted)
            return new(true, null, run, persisted);
        var random = new Random(PureRunMapService.DeriveSeed(run.Seed, $"store:{run.NodeTransaction!.NodeId}"));
        RunStoreOffer[] itemPool = consumables.OrderBy(value => value.ContentId.Value, StringComparer.Ordinal).ToArray();
        RunStoreOffer[] gearPool = equipment.OrderBy(value => value.ContentId.Value, StringComparer.Ordinal).ToArray();
        if (itemPool.Length == 0) return Fail("store.consumable_pool_empty", run);
        var offers = new List<RunStoreOffer> { itemPool[random.Next(itemPool.Length)] };
        RunStoreOffer[] combined = itemPool.Concat(gearPool)
            .Where(value => offers.All(existing => existing.ContentId != value.ContentId)).ToArray();
        while (offers.Count < 3 && combined.Length > 0)
        {
            int index = random.Next(combined.Length); offers.Add(combined[index]);
            combined = combined.Where((_, candidate) => candidate != index).ToArray();
        }
        if (offers.Count != 3) return Fail("store.offer_pool_too_small", run);
        RunStoreOfferState[] stable = offers.OrderBy(value => value.ContentId.Value, StringComparer.Ordinal)
            .Select((value, index) => new RunStoreOfferState(value.ContentId, value.Price, value.IsConsumable,
                new ItemInstanceId($"store-l4-{index}-{value.ContentId.Value}"))).ToArray();
        PureRunMapState map = run.MapState! with { StoreOffers = stable, NodeLifecycle = RunNodeLifecycle.Pending };
        PureRunState updated = Copy(run, map: map);
        return new(true, null, updated, stable);
    }

    public LayerFourNodeResolution Purchase(PureRunState run, ItemInstanceId instanceId,
        IReadOnlyDictionary<ContentId, ConsumableDefinition> consumables,
        IReadOnlyDictionary<ContentId, EquipmentDefinition> equipment)
    {
        if (!Validate(run, PureRunNodeKind.Store, out LayerFourNodeResolution? failure)) return failure!;
        RunStoreOfferState? offer = run.MapState!.StoreOffers?.FirstOrDefault(value => value.InstanceId == instanceId);
        if (offer is null) return Fail("store.offer_unknown", run);
        if (offer.Purchased) return Fail("store.offer_already_purchased", run);
        if (run.Gold < offer.Price) return Fail("store.insufficient_gold", run);
        if (run.BackpackConsumables.Count + run.BackpackEquipment.Count >= BackpackCapacity)
            return Fail("inventory.capacity_reached", run);
        BattleConsumableState[] items = run.BackpackConsumables.ToArray();
        RunEquipmentState[] gear = run.BackpackEquipment.ToArray();
        if (offer.IsConsumable)
        {
            if (!consumables.TryGetValue(offer.ContentId, out ConsumableDefinition? definition))
                return Fail("store.definition_unknown", run);
            items = items.Append(new BattleConsumableState(offer.InstanceId, offer.ContentId,
                definition.MaxCharges, definition.MaxCharges)).ToArray();
        }
        else
        {
            if (!equipment.TryGetValue(offer.ContentId, out EquipmentDefinition? definition))
                return Fail("store.definition_unknown", run);
            gear = gear.Append(new RunEquipmentState(offer.InstanceId, offer.ContentId, definition.Slot)).ToArray();
        }
        RunStoreOfferState[] offers = run.MapState.StoreOffers!.Select(value =>
            value.InstanceId == instanceId ? value with { Purchased = true } : value).ToArray();
        PureRunMapState map = run.MapState with { StoreOffers = offers, NodeLifecycle = RunNodeLifecycle.Pending };
        return new(true, null, Copy(run, gold: run.Gold - offer.Price, consumables: items, equipment: gear, map: map), offers);
    }

    public LayerFourNodeResolution LeaveStore(PureRunState run) =>
        Validate(run, PureRunNodeKind.Store, out LayerFourNodeResolution? failure) ? Commit(run) : failure!;

    public LayerFourNodeResolution ResolveMystery(PureRunState run, string eventId, string optionId,
        string characterId, int baseSuccessRate, int attributeValue, string successEffect, int successAmount,
        ContentId? successContentId, string failureEffect, int failureAmount, ContentId? failureContentId)
    {
        if (!Validate(run, PureRunNodeKind.Mystery, out LayerFourNodeResolution? failure)) return failure!;
        if (run.MapState!.MysteryResolution is RunMysteryResolutionState persisted)
            return new(true, null, run, EventOutcome: persisted);
        if (!run.MapState.MysteryEventAssignments.TryGetValue(run.NodeTransaction!.NodeId, out string? assigned) ||
            !string.Equals(assigned, eventId, StringComparison.Ordinal)) return Fail("event.assignment_mismatch", run);
        if (run.Party.All(value => value.CharacterId != characterId || value.IsDead)) return Fail("event.character_unknown", run);
        int rate = Math.Clamp(baseSuccessRate + (attributeValue - 5) * 5, 5, 95);
        int roll = new Random(PureRunMapService.DeriveSeed(run.Seed,
            $"event-check:{run.NodeTransaction.NodeId}:{optionId}:{characterId}")).Next(0, 100);
        bool succeeded = roll < rate;
        var outcome = new RunMysteryResolutionState(eventId, optionId, characterId, rate, roll, succeeded,
            succeeded ? successEffect : failureEffect, succeeded ? successAmount : failureAmount,
            succeeded ? successContentId : failureContentId);
        PureRunState updated = Copy(run, map: run.MapState with
        {
            MysteryResolution = outcome,
            NodeLifecycle = RunNodeLifecycle.Resolved
        });
        return new(true, null, updated, EventOutcome: outcome);
    }

    public LayerFourNodeResolution ConfirmMystery(PureRunState run,
        IReadOnlyDictionary<ContentId, ConsumableDefinition> consumables)
    {
        if (!Validate(run, PureRunNodeKind.Mystery, out LayerFourNodeResolution? failure)) return failure!;
        RunMysteryResolutionState? outcome = run.MapState!.MysteryResolution;
        if (outcome is null) return Fail("event.not_resolved", run);
        RunCharacterState[] party = run.Party.ToArray();
        BattleConsumableState[] backpack = run.BackpackConsumables.ToArray();
        int gold = run.Gold;
        List<RunPersistentStatusState> statuses = run.MapState.PendingStatuses?.ToList() ?? new();
        switch (outcome.Effect)
        {
            case "Gold": gold = Math.Min(50, checked(gold + outcome.Amount)); break;
            case "Damage":
                party = party.Select(value => value.CharacterId == outcome.CharacterId
                    ? CopyVitals(value, Math.Max(0, value.CurrentHealth - outcome.Amount), value.CurrentMana) : value).ToArray(); break;
            case "Item":
                if (outcome.EffectContentId is not ContentId itemId || !consumables.TryGetValue(itemId, out ConsumableDefinition? item))
                    return Fail("event.item_unknown", run);
                string itemInstance = $"event-l4-{outcome.EventId}-{outcome.OptionId}";
                if (backpack.All(value => value.InstanceId.Value != itemInstance))
                    backpack = backpack.Append(new BattleConsumableState(new ItemInstanceId(itemInstance), itemId,
                        item.MaxCharges, item.MaxCharges)).ToArray();
                break;
            case "Buff": case "Debuff":
                if (outcome.EffectContentId is not ContentId statusId) return Fail("event.status_unknown", run);
                if (!statuses.Any(value => value.CharacterId == outcome.CharacterId && value.StatusId == statusId))
                    statuses.Add(new RunPersistentStatusState(outcome.CharacterId, statusId, outcome.Amount));
                break;
            case "Nothing": break;
            default: return Fail("event.effect_unknown", run);
        }
        PureRunMapState map = run.MapState with
        {
            MysteryResolution = outcome with { Confirmed = true },
            PendingStatuses = statuses,
            NodeLifecycle = RunNodeLifecycle.Resolved
        };
        PureRunState effected = Copy(run, party: party, consumables: backpack, gold: gold, map: map);
        if (party.All(value => value.IsDead)) return new(true, null, Copy(effected, phase: PureRunPhase.Defeated));
        return Commit(effected);
    }

    public LayerFourNodeResolution BeginN4(PureRunState run, ContentId encounterId)
    {
        if (!Validate(run, PureRunNodeKind.Battle, out LayerFourNodeResolution? failure)) return failure!;
        long revision = run.Revision + 1;
        var checkpoint = new RunEncounterCheckpoint(encounterId, 3, revision, run.Party.ToArray(),
            run.BackpackConsumables.ToArray(), run.BackpackEquipment.ToArray());
        PureRunState pending = Copy(run, phase: PureRunPhase.PendingBattle, encounterId: encounterId,
            checkpoint: checkpoint, map: run.MapState! with { NodeLifecycle = RunNodeLifecycle.Pending });
        return new(true, null, pending, EncounterRequest: new EncounterRequest(run.RunId, checkpoint.Revision, encounterId, checkpoint.Party));
    }

    private static bool Validate(PureRunState run, PureRunNodeKind expected, out LayerFourNodeResolution? failure)
    {
        RunNodeTransaction? transaction = run.NodeTransaction;
        if (transaction is null || transaction.Kind != expected) { failure = Fail("node.transaction_invalid", run); return false; }
        if (run.AppliedTransactionKeys.Contains(transaction.TransactionKey, StringComparer.Ordinal) || transaction.Committed)
        { failure = new(true, null, run, WasDuplicate: true); return false; }
        if (run.MapState?.SelectedNodeId != transaction.NodeId) { failure = Fail("node.selection_mismatch", run); return false; }
        failure = null; return true;
    }

    private static LayerFourNodeResolution Commit(PureRunState run, IReadOnlyList<RunCharacterState>? party = null)
    {
        RunNodeTransaction transaction = run.NodeTransaction!;
        PureRunMapState map = run.MapState! with
        {
            Phase = PureRunMapPhase.ReadyForLayerFive,
            CurrentNodeId = transaction.NodeId,
            ReachableNodeIds = Array.Empty<string>(),
            VisitedNodeIds = run.MapState.VisitedNodeIds.Append(transaction.NodeId).Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            PendingNodeId = null,
            PendingTransactionKey = null,
            NodeLifecycle = RunNodeLifecycle.Committed
        };
        PureRunState updated = Copy(run, phase: PureRunPhase.ReadyForLayerFive, party: party,
            appliedKey: transaction.TransactionKey, checkpoint: null, map: map,
            transaction: transaction with { Committed = true });
        return new(true, null, updated);
    }

    private static PureRunState Copy(PureRunState run, PureRunPhase? phase = null,
        ContentId? encounterId = null, IReadOnlyList<RunCharacterState>? party = null,
        IReadOnlyList<BattleConsumableState>? consumables = null, IReadOnlyList<RunEquipmentState>? equipment = null,
        int? gold = null, string? appliedKey = null, RunEncounterCheckpoint? checkpoint = null,
        PureRunMapState? map = null, RunNodeTransaction? transaction = null) => new(run.RunId, run.Seed,
        run.Revision + 1, phase ?? run.Phase, run.EncounterIndex, encounterId ?? run.EncounterContentId,
        party ?? run.Party, consumables ?? run.BackpackConsumables, equipment ?? run.BackpackEquipment,
        run.PendingProgression, appliedKey is null ? run.AppliedTransactionKeys : run.AppliedTransactionKeys.Append(appliedKey).ToArray(),
        gold ?? run.Gold, run.BattlesCompleted, run.EnemiesDefeated, run.AcquiredItems, checkpoint,
        map ?? run.MapState, transaction ?? run.NodeTransaction);

    private static int Percent(int value, int percent) => (int)Math.Ceiling(value * percent / 100d);
    private static RunCharacterState CopyVitals(RunCharacterState value, int hp, int mp) => new(value.CharacterId,
        value.UnitContentId, value.Level, value.Attributes, hp, value.MaxHealth, mp, value.MaxMana, hp == 0,
        value.LearnedSkills, value.Equipment, value.CarriedConsumables, value.LearnedSkillStates);
    private static LayerFourNodeResolution Fail(string code, PureRunState run) => new(false, code, run);
}
