using Tactics.Core.Content;
using Tactics.Core.Items;

namespace Tactics.Core.Runs;

public sealed class PureRunSettlementService
{
    public const string ContractId = "pure-run-settlement-v1";
    private const int GoldCap = 50;

    public PureRunSettlementResult Apply(
        PureRunDefinition definition,
        PureRunState state,
        PureRunBattleResult result,
        IReadOnlyList<ContentId> consumableDropPool)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(consumableDropPool);
        string transactionKey = $"battle:{result.EncounterContentId.Value}:settlement";
        if (state.AppliedTransactionKeys.Contains(transactionKey, StringComparer.Ordinal))
            return new PureRunSettlementResult(true, null, state, null, true);
        string? rejection = Validate(state, result);
        if (rejection is not null)
            return new PureRunSettlementResult(false, rejection, state, null, false);

        RunCharacterState[] party = MergeParty(state, result, recoverAfterVictory: result.PlayerVictory);
        string[] transactions = state.AppliedTransactionKeys.Append(transactionKey)
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (!result.PlayerVictory)
        {
            PureRunSummary defeated = CreateSummary(state, PureRunOutcome.Defeated, party, transactions);
            return new PureRunSettlementResult(true, null, null, defeated, false);
        }

        int reward = CalculateGold(result.TotalRounds);
        int nextGold = Math.Min(GoldCap, checked(state.Gold + reward));
        ContentId? drop = RollDrop(state.Seed, state.EncounterContentId, consumableDropPool);
        IReadOnlyList<ContentId> acquired = drop is null
            ? state.AcquiredItems
            : state.AcquiredItems.Append(drop.Value).OrderBy(value => value.Value, StringComparer.Ordinal).ToArray();
        BattleConsumableState[] backpack = state.BackpackConsumables.ToArray();
        if (drop is not null)
        {
            string instanceId = $"drop-{state.EncounterIndex + 1}-{drop.Value.Value}";
            backpack = backpack.Append(new BattleConsumableState(
                new ItemInstanceId(instanceId), drop.Value, remainingCharges: 1, maxCharges: 1)).ToArray();
        }

        string? progressionTarget = party.Where(character => !character.IsDead)
            .OrderBy(character => character.Level)
            .ThenBy(character => Array.FindIndex(party, candidate => candidate.CharacterId == character.CharacterId))
            .Select(character => character.CharacterId).FirstOrDefault();
        PendingProgression[] progression = progressionTarget is null
            ? state.PendingProgression.ToArray()
            : state.PendingProgression.Append(new PendingProgression(
                $"progression:{state.EncounterContentId.Value}", state.EncounterContentId.Value, progressionTarget)).ToArray();
        int battles = state.BattlesCompleted + 1;
        int defeatedEnemies = checked(state.EnemiesDefeated + result.EnemiesDefeated);
        if (state.EncounterIndex == definition.Encounters.Count - 1)
        {
            if (definition.LayerFourMapContentId is not null)
            {
                var awaitingMap = new PureRunState(
                    state.RunId, state.Seed, state.Revision + 1, PureRunPhase.AwaitingLayerFourChoice,
                    state.EncounterIndex, state.EncounterContentId, party, backpack, state.BackpackEquipment,
                    progression, transactions, nextGold, battles, defeatedEnemies, acquired);
                return new PureRunSettlementResult(true, null, awaitingMap, null, false);
            }
            var terminalState = new PureRunState(
                state.RunId, state.Seed, state.Revision + 1, PureRunPhase.SliceCompleted,
                state.EncounterIndex, state.EncounterContentId, party, backpack, state.BackpackEquipment,
                progression, transactions, nextGold, battles, defeatedEnemies, acquired);
            return new PureRunSettlementResult(
                true, null, null, CreateSummary(terminalState, PureRunOutcome.SliceCompleted, party, transactions), false);
        }

        int nextIndex = state.EncounterIndex + 1;
        var next = new PureRunState(
            state.RunId, state.Seed, state.Revision + 1, PureRunPhase.Ready,
            nextIndex, definition.Encounters[nextIndex], party, backpack, state.BackpackEquipment,
            progression, transactions, nextGold, battles, defeatedEnemies, acquired);
        return new PureRunSettlementResult(true, null, next, null, false);
    }

    public PureRunSummary Abandon(PureRunState state) =>
        CreateSummary(state, PureRunOutcome.Abandoned, state.Party, state.AppliedTransactionKeys);

    private static string? Validate(PureRunState state, PureRunBattleResult result)
    {
        if (state.Phase != PureRunPhase.PendingBattle || state.Checkpoint is null)
            return "run.no_pending_battle";
        if (!string.Equals(state.RunId, result.RunId, StringComparison.Ordinal))
            return "run.result_run_mismatch";
        if (state.Checkpoint.Revision != result.CheckpointRevision)
            return "run.result_revision_mismatch";
        if (state.EncounterContentId != result.EncounterContentId)
            return "run.result_encounter_mismatch";
        if (result.TotalRounds < 1 || result.EnemiesDefeated < 0)
            return "run.result_invalid_totals";
        if (result.Party.Count != state.Party.Count ||
            result.Party.Select(item => item.CharacterId).Distinct(StringComparer.Ordinal).Count() != state.Party.Count)
            return "run.result_invalid_party";
        foreach (BattlePartyResult member in result.Party)
        {
            RunCharacterState? prior = state.Party.FirstOrDefault(item => item.CharacterId == member.CharacterId);
            if (prior is null || member.CurrentHealth < 0 || member.CurrentHealth > prior.MaxHealth ||
                member.CurrentMana < 0 || member.CurrentMana > prior.MaxMana || member.IsDead != (member.CurrentHealth == 0))
                return "run.result_invalid_character";
            if (member.CarriedConsumables.Any(item => item.RemainingCharges < 0 || item.RemainingCharges > item.MaxCharges))
                return "run.result_invalid_consumable";
        }
        return null;
    }

    private static RunCharacterState[] MergeParty(PureRunState state, PureRunBattleResult result, bool recoverAfterVictory)
    {
        return state.Party.Select(prior =>
        {
            BattlePartyResult current = result.Party.Single(item => item.CharacterId == prior.CharacterId);
            bool wasDead = current.IsDead;
            int health = recoverAfterVictory ? Math.Min(prior.MaxHealth,
                checked(current.CurrentHealth + prior.Attributes.Constitution * 2)) : current.CurrentHealth;
            int mana = recoverAfterVictory ? Math.Min(prior.MaxMana,
                checked(current.CurrentMana + prior.Attributes.Charisma)) : current.CurrentMana;
            bool dead = health == 0;
            return new RunCharacterState(
                prior.CharacterId, prior.UnitContentId, prior.Level, prior.Attributes,
                health, prior.MaxHealth, mana, prior.MaxMana, dead, prior.LearnedSkills,
                wasDead ? Array.Empty<RunEquipmentState>() : prior.Equipment,
                wasDead ? Array.Empty<BattleConsumableState>() : current.CarriedConsumables);
        }).ToArray();
    }

    private static int CalculateGold(int rounds) => 3 + (rounds switch
    {
        <= 3 => 5,
        <= 5 => 3,
        <= 10 => 1,
        _ => 0
    });

    private static ContentId? RollDrop(int runSeed, ContentId encounter, IReadOnlyList<ContentId> pool)
    {
        if (pool.Count == 0)
            return null;
        int chanceSeed = DeriveSeed(runSeed, $"battle-drop:{encounter.Value}");
        var chance = new Random(chanceSeed);
        if (chance.NextDouble() >= 0.25)
            return null;
        int itemSeed = DeriveSeed(runSeed, $"battle-drop-item:{encounter.Value}");
        return pool.OrderBy(value => value.Value, StringComparer.Ordinal).ElementAt(new Random(itemSeed).Next(pool.Count));
    }

    public static int DeriveSeed(int runSeed, string streamName, int ordinal = 0)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)runSeed) * 16777619u;
            hash = (hash ^ (uint)ordinal) * 16777619u;
            foreach (char character in streamName ?? string.Empty)
                hash = (hash ^ character) * 16777619u;
            return (int)hash;
        }
    }

    private static PureRunSummary CreateSummary(
        PureRunState state,
        PureRunOutcome outcome,
        IReadOnlyList<RunCharacterState> party,
        IReadOnlyList<string> transactions) => new(
            state.RunId, state.Seed, outcome, state.BattlesCompleted, state.EnemiesDefeated,
            state.Gold, state.AcquiredItems,
            party.Where(character => character.IsDead).Select(character => character.CharacterId).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            transactions.OrderBy(value => value, StringComparer.Ordinal).ToArray());
}
