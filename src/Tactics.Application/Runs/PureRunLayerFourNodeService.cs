using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Runs;

public sealed record RunStoreOffer(ContentId ContentId, int Price, bool IsConsumable);
public sealed record RunEventOutcome(string EventId, string OptionId, string CharacterId, int SuccessRate, int Roll, bool Succeeded, string Effect, int Amount);
public sealed record LayerFourNodeResolution(bool Succeeded, string? RejectionCode, PureRunState State, IReadOnlyList<RunStoreOffer>? StoreOffers = null, RunEventOutcome? EventOutcome = null, EncounterRequest? EncounterRequest = null, bool WasDuplicate = false);

public sealed class PureRunLayerFourNodeService
{
    public LayerFourNodeResolution ResolveRest(PureRunState run, RunNodeTransaction transaction)
    {
        if (!Validate(run, transaction, PureRunNodeKind.Rest, out LayerFourNodeResolution? failure)) return failure!;
        RunCharacterState[] party = run.Party.Select(character => character.IsDead ? character : CopyVitals(character,
            Math.Min(character.MaxHealth, character.CurrentHealth + Percent(character.MaxHealth, 30)),
            Math.Min(character.MaxMana, character.CurrentMana + Percent(character.MaxMana, 30)))).ToArray();
        return Success(run, transaction, party);
    }

    public LayerFourNodeResolution OpenStore(PureRunState run, RunNodeTransaction transaction,
        IEnumerable<RunStoreOffer> equipment, IEnumerable<RunStoreOffer> consumables)
    {
        if (!Validate(run, transaction, PureRunNodeKind.Store, out LayerFourNodeResolution? failure)) return failure!;
        var random = new Random(PureRunMapService.DeriveSeed(run.Seed, $"store:{transaction.NodeId}"));
        RunStoreOffer[] itemPool = consumables.OrderBy(value => value.ContentId.Value, StringComparer.Ordinal).ToArray();
        RunStoreOffer[] gearPool = equipment.OrderBy(value => value.ContentId.Value, StringComparer.Ordinal).ToArray();
        var offers = new List<RunStoreOffer>();
        if (itemPool.Length > 0) offers.Add(itemPool[random.Next(itemPool.Length)]);
        RunStoreOffer[] combined = itemPool.Concat(gearPool).Where(value => offers.All(existing => existing.ContentId != value.ContentId)).ToArray();
        while (offers.Count < 3 && combined.Length > 0)
        {
            int index = random.Next(combined.Length);
            offers.Add(combined[index]);
            combined = combined.Where((_, candidate) => candidate != index).ToArray();
        }
        return new LayerFourNodeResolution(true, null, run, offers.OrderBy(value => value.ContentId.Value, StringComparer.Ordinal).ToArray());
    }

    public LayerFourNodeResolution ResolveEvent(PureRunState run, RunNodeTransaction transaction, string eventId,
        string optionId, string characterId, int baseSuccessRate, int attributeValue, string successEffect,
        int successAmount, string failureEffect, int failureAmount)
    {
        if (!Validate(run, transaction, PureRunNodeKind.Mystery, out LayerFourNodeResolution? failure)) return failure!;
        if (run.Party.All(value => value.CharacterId != characterId)) return Fail("event.character_unknown", run);
        int rate = Math.Clamp(baseSuccessRate + (attributeValue - 5) * 5, 5, 95);
        int roll = new Random(PureRunMapService.DeriveSeed(run.Seed, $"event-check:{transaction.NodeId}:{optionId}")).Next(0, 100);
        bool succeeded = roll < rate;
        var outcome = new RunEventOutcome(eventId, optionId, characterId, rate, roll, succeeded,
            succeeded ? successEffect : failureEffect, succeeded ? successAmount : failureAmount);
        return new LayerFourNodeResolution(true, null, run, EventOutcome: outcome);
    }

    public LayerFourNodeResolution BeginN4(PureRunState run, RunNodeTransaction transaction, ContentId encounterId)
    {
        if (!Validate(run, transaction, PureRunNodeKind.Battle, out LayerFourNodeResolution? failure)) return failure!;
        var request = new EncounterRequest(run.RunId, run.Revision, encounterId, run.Party);
        return new LayerFourNodeResolution(true, null, run, EncounterRequest: request);
    }

    private static bool Validate(PureRunState run, RunNodeTransaction transaction, PureRunNodeKind expected, out LayerFourNodeResolution? failure)
    {
        string key = transaction.TransactionKey;
        if (run.AppliedTransactionKeys.Contains(key, StringComparer.Ordinal)) { failure = new(true, null, run, WasDuplicate: true); return false; }
        if (transaction.Kind != expected || transaction.Committed) { failure = Fail("node.transaction_invalid", run); return false; }
        failure = null; return true;
    }

    private static LayerFourNodeResolution Success(PureRunState run, RunNodeTransaction transaction, IReadOnlyList<RunCharacterState> party)
    {
        var updated = new PureRunState(run.RunId, run.Seed, run.Revision + 1, run.Phase, run.EncounterIndex,
            run.EncounterContentId, party, run.BackpackConsumables, run.BackpackEquipment, run.PendingProgression,
            run.AppliedTransactionKeys.Append(transaction.TransactionKey).ToArray(), run.Gold, run.BattlesCompleted,
            run.EnemiesDefeated, run.AcquiredItems, run.Checkpoint);
        return new LayerFourNodeResolution(true, null, updated);
    }

    private static int Percent(int value, int percent) => (int)Math.Ceiling(value * percent / 100d);
    private static RunCharacterState CopyVitals(RunCharacterState value, int hp, int mp) => new(value.CharacterId,
        value.UnitContentId, value.Level, value.Attributes, hp, value.MaxHealth, mp, value.MaxMana, hp == 0,
        value.LearnedSkills, value.Equipment, value.CarriedConsumables, value.LearnedSkillStates);
    private static LayerFourNodeResolution Fail(string code, PureRunState run) => new(false, code, run);
}
