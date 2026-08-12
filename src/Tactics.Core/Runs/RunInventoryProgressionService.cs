using Tactics.Core.Content;
using Tactics.Core.Items;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Core.Runs;

public sealed record RunMutationResult(bool Succeeded, string? RejectionCode, PureRunState State);

/// <summary>Applies revision-checked Inventory and progression transactions without UI-owned mutation.</summary>
public sealed class RunInventoryProgressionService
{
    public RunMutationResult Equip(PureRunState state, long revision, string characterId, ItemInstanceId instanceId,
        IReadOnlyDictionary<ContentId, EquipmentDefinition> definitions, float baseSpeed)
    {
        if (state.Revision != revision) return Reject(state, "run.revision_mismatch");
        RunCharacterState? character = state.Party.FirstOrDefault(value => value.CharacterId == characterId);
        RunEquipmentState? item = state.BackpackEquipment.FirstOrDefault(value => value.InstanceId == instanceId);
        if (character is null || character.IsDead) return Reject(state, "inventory.character_unavailable");
        if (item is null || !definitions.TryGetValue(item.DefinitionId, out EquipmentDefinition? definition)) return Reject(state, "inventory.item_not_found");
        RunEquipmentState? replaced = character.Equipment.FirstOrDefault(value => value.Slot == definition.Slot);
        RunEquipmentState[] loadout = character.Equipment.Where(value => value.Slot != definition.Slot).Append(item with { Slot = definition.Slot }).ToArray();
        RunEquipmentState[] backpack = state.BackpackEquipment.Where(value => value.InstanceId != instanceId).Concat(replaced is null ? Array.Empty<RunEquipmentState>() : new[] { replaced }).ToArray();
        EquipmentStatProjection projection = EquipmentStatProjector.Project(character.Attributes, baseSpeed, loadout.Select(value => definitions[value.DefinitionId]));
        RunCharacterState updated = Copy(character, equipment: loadout, maxHealth: projection.DerivedStats.MaxHealth, maxMana: projection.DerivedStats.MaxMana);
        return Success(Copy(state, party: Replace(state.Party, updated), backpackEquipment: backpack));
    }

    public RunMutationResult Unequip(PureRunState state, long revision, string characterId, EquipmentSlot slot)
    {
        if (state.Revision != revision) return Reject(state, "run.revision_mismatch");
        RunCharacterState? character = state.Party.FirstOrDefault(value => value.CharacterId == characterId);
        RunEquipmentState? item = character?.Equipment.FirstOrDefault(value => value.Slot == slot);
        if (character is null || item is null) return Reject(state, "inventory.slot_empty");
        RunCharacterState updated = Copy(character, equipment: character.Equipment.Where(value => value.Slot != slot).ToArray());
        return Success(Copy(state, party: Replace(state.Party, updated), backpackEquipment: state.BackpackEquipment.Append(item).ToArray()));
    }

    public RunMutationResult Carry(PureRunState state, long revision, string characterId, ItemInstanceId instanceId)
    {
        if (state.Revision != revision) return Reject(state, "run.revision_mismatch");
        RunCharacterState? character = state.Party.FirstOrDefault(value => value.CharacterId == characterId);
        BattleConsumableState? item = state.BackpackConsumables.FirstOrDefault(value => value.InstanceId == instanceId);
        if (character is null || character.IsDead) return Reject(state, "inventory.character_unavailable");
        if (item is null) return Reject(state, "inventory.item_not_found");
        BattleConsumableState[] backpack = state.BackpackConsumables.Where(value => value.InstanceId != instanceId)
            .Concat(character.CarriedConsumables).ToArray();
        RunCharacterState updated = Copy(character, carried: new[] { item });
        return Success(Copy(state, party: Replace(state.Party, updated), backpackConsumables: backpack));
    }

    public RunMutationResult Unload(PureRunState state, long revision, string characterId)
    {
        if (state.Revision != revision) return Reject(state, "run.revision_mismatch");
        RunCharacterState? character = state.Party.FirstOrDefault(value => value.CharacterId == characterId);
        if (character is null || character.CarriedConsumables.Count == 0) return Reject(state, "inventory.carried_slot_empty");
        RunCharacterState updated = Copy(character, carried: Array.Empty<BattleConsumableState>());
        return Success(Copy(state, party: Replace(state.Party, updated), backpackConsumables: state.BackpackConsumables.Concat(character.CarriedConsumables).ToArray()));
    }

    public RunMutationResult CompleteProgression(PureRunState state, long revision, string transactionKey,
        UnitAttributes attributes, ContentId? skillId, IReadOnlyDictionary<ContentId, SkillDefinition> skills)
    {
        if (state.Revision != revision) return Reject(state, "run.revision_mismatch");
        PendingProgression? pending = state.PendingProgression.FirstOrDefault(value => value.TransactionKey == transactionKey);
        if (pending is null) return Reject(state, "progression.not_found");
        RunCharacterState character = state.Party.Single(value => value.CharacterId == pending.CharacterId);
        int spent = AttributeTotal(attributes) - AttributeTotal(character.Attributes);
        if (spent != pending.AttributePoints || AnyAttributeLower(attributes, character.Attributes)) return Reject(state, "progression.invalid_attributes");
        RunLearnedSkillState[] learned = character.LearnedSkillStates.ToArray();
        if (skillId is ContentId selected)
        {
            if (!skills.TryGetValue(selected, out SkillDefinition? definition) || !definition.GrowthVisible || definition.Level > 2) return Reject(state, "progression.invalid_skill");
            RunLearnedSkillState? prior = learned.FirstOrDefault(value => value.BranchId == definition.BranchId);
            if (definition.Level != (prior?.Level ?? 0) + 1) return Reject(state, "progression.invalid_skill_level");
            learned = learned.Where(value => value.BranchId != definition.BranchId).Append(new RunLearnedSkillState(definition.BranchId, definition.Level, selected)).ToArray();
        }
        RunCharacterState updated = new(character.CharacterId, character.UnitContentId, character.Level + 1, attributes,
            character.CurrentHealth, character.MaxHealth, character.CurrentMana, character.MaxMana, character.IsDead,
            learned.Select(value => value.DefinitionId).ToArray(), character.Equipment, character.CarriedConsumables, learned);
        PureRunState next = Copy(state, party: Replace(state.Party, updated), pending: state.PendingProgression.Where(value => value.TransactionKey != transactionKey).ToArray(), transaction: transactionKey);
        return Success(next);
    }

    private static int AttributeTotal(UnitAttributes value) => value.Strength + value.Agility + value.Constitution + value.Intelligence + value.Charisma + value.Luck;
    private static bool AnyAttributeLower(UnitAttributes value, UnitAttributes prior) => value.Strength < prior.Strength || value.Agility < prior.Agility || value.Constitution < prior.Constitution || value.Intelligence < prior.Intelligence || value.Charisma < prior.Charisma || value.Luck < prior.Luck;
    private static RunCharacterState[] Replace(IReadOnlyList<RunCharacterState> party, RunCharacterState updated) => party.Select(value => value.CharacterId == updated.CharacterId ? updated : value).ToArray();
    private static RunCharacterState Copy(RunCharacterState value, IReadOnlyList<RunEquipmentState>? equipment = null, IReadOnlyList<BattleConsumableState>? carried = null, int? maxHealth = null, int? maxMana = null) =>
        new(value.CharacterId, value.UnitContentId, value.Level, value.Attributes, Math.Min(value.CurrentHealth, maxHealth ?? value.MaxHealth), maxHealth ?? value.MaxHealth, Math.Min(value.CurrentMana, maxMana ?? value.MaxMana), maxMana ?? value.MaxMana, value.IsDead, value.LearnedSkills, equipment ?? value.Equipment, carried ?? value.CarriedConsumables, value.LearnedSkillStates);
    private static PureRunState Copy(PureRunState value, IReadOnlyList<RunCharacterState>? party = null, IReadOnlyList<BattleConsumableState>? backpackConsumables = null, IReadOnlyList<RunEquipmentState>? backpackEquipment = null, IReadOnlyList<PendingProgression>? pending = null, string? transaction = null) =>
        new(value.RunId, value.Seed, value.Revision + 1, value.Phase, value.EncounterIndex, value.EncounterContentId, party ?? value.Party,
            backpackConsumables ?? value.BackpackConsumables, backpackEquipment ?? value.BackpackEquipment, pending ?? value.PendingProgression,
            transaction is null ? value.AppliedTransactionKeys : value.AppliedTransactionKeys.Append(transaction).ToArray(), value.Gold, value.BattlesCompleted, value.EnemiesDefeated, value.AcquiredItems, value.Checkpoint);
    private static RunMutationResult Success(PureRunState state) => new(true, null, state);
    private static RunMutationResult Reject(PureRunState state, string code) => new(false, code, state);
}
