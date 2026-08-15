using Tactics.Core.Content;
using Tactics.Core.Items;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Core.Runs;

public sealed record RunMutationResult(bool Succeeded, string? RejectionCode, PureRunState State);

/// <summary>Applies revision-checked Inventory and progression transactions without UI-owned mutation.</summary>
public sealed class RunInventoryProgressionService
{
    public const int GrowthOfferSize = 3;

    public IReadOnlyList<SkillDefinition> GrowthCandidates(RunCharacterState character,
        IReadOnlyDictionary<ContentId, SkillDefinition> skills)
    {
        SkillRole role = CharacterRole(character);
        return skills.Values.Where(skill => skill.GrowthVisible && !skill.Hidden && skill.Role == role)
            .Where(skill => skill.Level == (character.LearnedSkillStates.FirstOrDefault(value => value.BranchId == skill.BranchId)?.Level ?? 0) + 1)
            .Where(skill => string.IsNullOrEmpty(skill.PrerequisiteBranchId) ||
                character.LearnedSkillStates.Any(value => value.BranchId == skill.PrerequisiteBranchId && value.Level >= 1))
            .OrderBy(skill => skill.BranchId, StringComparer.Ordinal).ThenBy(skill => skill.Level).ToArray();
    }

    /// <summary>Builds the frozen Unity-style deterministic three-choice growth offer.</summary>
    public IReadOnlyList<SkillDefinition> GrowthOffer(PureRunState state, RunCharacterState character,
        IReadOnlyDictionary<ContentId, SkillDefinition> skills, PureRunDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(definition);
        ContentId startingSkillContentId = SelectedStartingSkill(character, definition);
        SkillDefinition[] legal = GrowthCandidates(character, skills)
            .Where(skill => CanUnlockWithAttributePoints(character, skill, 0))
            .ToArray();
        var newBranches = legal.Where(skill => skill.Level == 1)
            .OrderBy(skill => skill.ContentId.Value, StringComparer.Ordinal).ToList();
        var upgrades = legal.Where(skill => skill.Level > 1)
            .OrderBy(skill => skill.ContentId.Value, StringComparer.Ordinal).ToList();
        int seed = PureRunSettlementService.DeriveSeed(state.Seed,
            $"skill-offer-{character.CharacterId}", character.Level + 1);
        var random = new Random(seed);
        var offer = new List<SkillDefinition>(GrowthOfferSize);
        SkillDefinition? guaranteed = GuaranteedAdvancedSkill(state, character, skills, legal, startingSkillContentId);
        if (guaranteed is not null)
        {
            offer.Add(guaranteed);
            newBranches.RemoveAll(value => value.ContentId == guaranteed.ContentId);
            upgrades.RemoveAll(value => value.ContentId == guaranteed.ContentId);
        }
        // Unity removes the guaranteed advanced branch before either pool consumes RNG.
        // Keeping this order is part of the frozen three-slot offer contract: changing it
        // preserves slot zero but deterministically changes slots one and two.
        Shuffle(newBranches, random);
        Shuffle(upgrades, random);
        if (offer.Count == 0 && newBranches.Count > 0 && upgrades.Count > 0)
        { offer.Add(newBranches[0]); newBranches.RemoveAt(0); }
        if (upgrades.Count > 0 && (guaranteed is not null || offer.Count > 0 || newBranches.Count == 0))
        { offer.Add(upgrades[0]); upgrades.RemoveAt(0); }
        var remaining = newBranches.Concat(upgrades).ToList();
        Shuffle(remaining, random);
        offer.AddRange(remaining.Take(GrowthOfferSize - offer.Count));
        return offer;
    }

    public bool CanUnlockWithAttributePoints(RunCharacterState character, SkillDefinition skill, int points) =>
        points >= 0 && AttributeValue(character.Attributes, skill.RequiredAttribute) + points >= skill.MinimumAttribute;

    public IReadOnlyList<SkillDefinition> PreviewGrowthOffer(PureRunState state, string transactionKey,
        UnitAttributes attributes, IReadOnlyDictionary<ContentId, SkillDefinition> skills,
        PureRunDefinition definition)
    {
        PendingProgression pending = state.PendingProgression.Single(value => value.TransactionKey == transactionKey);
        RunCharacterState character = state.Party.Single(value => value.CharacterId == pending.CharacterId);
        int spent = AttributeTotal(attributes) - AttributeTotal(character.Attributes);
        if (spent != pending.AttributePoints || AnyAttributeLower(attributes, character.Attributes))
            return Array.Empty<SkillDefinition>();
        RunCharacterState preview = new(character.CharacterId, character.UnitContentId, character.Level, attributes,
            character.CurrentHealth, character.MaxHealth, character.CurrentMana, character.MaxMana, character.IsDead,
            character.LearnedSkills, character.Equipment, character.CarriedConsumables, character.LearnedSkillStates,
            character.StartingSkillContentId);
        return GrowthOffer(state, preview, skills, definition);
    }

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

    public RunMutationResult Unequip(PureRunState state, long revision, string characterId, EquipmentSlot slot,
        IReadOnlyDictionary<ContentId, EquipmentDefinition>? definitions = null, float baseSpeed = 3f)
    {
        if (state.Revision != revision) return Reject(state, "run.revision_mismatch");
        RunCharacterState? character = state.Party.FirstOrDefault(value => value.CharacterId == characterId);
        RunEquipmentState? item = character?.Equipment.FirstOrDefault(value => value.Slot == slot);
        if (character is null || item is null) return Reject(state, "inventory.slot_empty");
        RunEquipmentState[] remaining = character.Equipment.Where(value => value.Slot != slot).ToArray();
        RunCharacterState updated;
        if (definitions is not null)
        {
            EquipmentStatProjection projection = EquipmentStatProjector.Project(character.Attributes, baseSpeed,
                remaining.Select(value => definitions[value.DefinitionId]));
            updated = Copy(character, equipment: remaining, maxHealth: projection.DerivedStats.MaxHealth,
                maxMana: projection.DerivedStats.MaxMana);
        }
        else updated = Copy(character, equipment: remaining);
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

    public RunMutationResult AllocateProgressionAttributes(PureRunState state, long revision, string transactionKey,
        UnitAttributes attributes)
    {
        if (state.Revision != revision) return Reject(state, "run.revision_mismatch");
        PendingProgression? pending = state.PendingProgression.FirstOrDefault(value => value.TransactionKey == transactionKey);
        if (pending is null) return Reject(state, "progression.not_found");
        if (pending.ProposedAttributes is UnitAttributes existing)
            return existing == attributes
                ? new RunMutationResult(true, null, state)
                : Reject(state, "progression.attributes_already_allocated");
        RunCharacterState character = state.Party.Single(value => value.CharacterId == pending.CharacterId);
        int spent = AttributeTotal(attributes) - AttributeTotal(character.Attributes);
        if (spent != pending.AttributePoints || AnyAttributeLower(attributes, character.Attributes))
            return Reject(state, "progression.invalid_attributes");
        PendingProgression updated = pending with { ProposedAttributes = attributes, SelectedSkillContentId = null };
        return Success(Copy(state, pending: state.PendingProgression.Select(value =>
            value.TransactionKey == transactionKey ? updated : value).ToArray()));
    }

    public RunMutationResult CompleteProgression(PureRunState state, long revision, string transactionKey,
        UnitAttributes attributes, ContentId? skillId, IReadOnlyDictionary<ContentId, SkillDefinition> skills,
        PureRunDefinition definition)
    {
        if (state.Revision != revision) return Reject(state, "run.revision_mismatch");
        PendingProgression? pending = state.PendingProgression.FirstOrDefault(value => value.TransactionKey == transactionKey);
        if (pending is null) return Reject(state, "progression.not_found");
        RunCharacterState character = state.Party.Single(value => value.CharacterId == pending.CharacterId);
        if (pending.ProposedAttributes is UnitAttributes proposed && proposed != attributes)
            return Reject(state, "progression.attributes_not_allocated");
        int spent = AttributeTotal(attributes) - AttributeTotal(character.Attributes);
        if (spent != pending.AttributePoints || AnyAttributeLower(attributes, character.Attributes)) return Reject(state, "progression.invalid_attributes");
        RunCharacterState preview = new(character.CharacterId, character.UnitContentId, character.Level, attributes,
            character.CurrentHealth, character.MaxHealth, character.CurrentMana, character.MaxMana, character.IsDead,
            character.LearnedSkills, character.Equipment, character.CarriedConsumables, character.LearnedSkillStates,
            character.StartingSkillContentId);
        IReadOnlyList<SkillDefinition> candidates = GrowthOffer(state, preview, skills, definition);
        if (skillId is null && candidates.Count > 0) return Reject(state, "progression.skill_required");
        if (skillId is ContentId candidateId && candidates.All(skill => skill.ContentId != candidateId))
            return Reject(state, "progression.invalid_skill");
        RunLearnedSkillState[] learned = character.LearnedSkillStates.ToArray();
        if (skillId is ContentId selected)
        {
            if (!skills.TryGetValue(selected, out SkillDefinition? selectedDefinition) || !selectedDefinition.GrowthVisible || selectedDefinition.Level > 3) return Reject(state, "progression.invalid_skill");
            if (AttributeValue(attributes, selectedDefinition.RequiredAttribute) < selectedDefinition.MinimumAttribute) return Reject(state, "progression.attribute_requirement_not_met");
            RunLearnedSkillState? prior = learned.FirstOrDefault(value => value.BranchId == selectedDefinition.BranchId);
            if (selectedDefinition.Level != (prior?.Level ?? 0) + 1) return Reject(state, "progression.invalid_skill_level");
            learned = learned.Where(value => value.BranchId != selectedDefinition.BranchId).Append(new RunLearnedSkillState(selectedDefinition.BranchId, selectedDefinition.Level, selected)).ToArray();
        }
        RunCharacterState updated = new(character.CharacterId, character.UnitContentId, character.Level + 1, attributes,
            character.CurrentHealth, character.MaxHealth, character.CurrentMana, character.MaxMana, character.IsDead,
            learned.Select(value => value.DefinitionId).ToArray(), character.Equipment, character.CarriedConsumables, learned,
            character.StartingSkillContentId);
        ContentId startingSkillContentId = SelectedStartingSkill(character, definition);
        string? guaranteeTransaction = skillId is ContentId chosen && skills.TryGetValue(chosen, out SkillDefinition? chosenDefinition) &&
            IsStartingAdvanced(chosenDefinition, skills, startingSkillContentId)
            ? GuaranteeTransaction(character.CharacterId) : null;
        string[] transactions = state.AppliedTransactionKeys.Append(transactionKey)
            .Concat(guaranteeTransaction is null ? [] : new[] { guaranteeTransaction }).Distinct(StringComparer.Ordinal).ToArray();
        PureRunState next = Copy(state, party: Replace(state.Party, updated), pending: state.PendingProgression.Where(value => value.TransactionKey != transactionKey).ToArray(), transactions: transactions);
        return Success(next);
    }

    private static SkillDefinition? GuaranteedAdvancedSkill(PureRunState state, RunCharacterState character,
        IReadOnlyDictionary<ContentId, SkillDefinition> skills, IReadOnlyList<SkillDefinition> legal,
        ContentId startingSkillContentId)
    {
        if (state.AppliedTransactionKeys.Contains(GuaranteeTransaction(character.CharacterId), StringComparer.Ordinal) ||
            character.LearnedSkillStates.Any(value => skills.TryGetValue(value.DefinitionId, out SkillDefinition? learned) &&
                !string.IsNullOrEmpty(learned.PrerequisiteBranchId)) ||
            !skills.TryGetValue(startingSkillContentId, out SkillDefinition? startingDefinition)) return null;
        return legal.FirstOrDefault(value => value.Level == 1 &&
            value.PrerequisiteBranchId == startingDefinition.BranchId);
    }

    private static bool IsStartingAdvanced(SkillDefinition value,
        IReadOnlyDictionary<ContentId, SkillDefinition> skills, ContentId startingSkillContentId) =>
        skills.TryGetValue(startingSkillContentId, out SkillDefinition? definition) &&
        value.Level == 1 && value.PrerequisiteBranchId == definition.BranchId;

    private static string GuaranteeTransaction(string characterId) => $"growth-guarantee:{characterId}";

    private static ContentId SelectedStartingSkill(RunCharacterState character, PureRunDefinition definition) =>
        character.StartingSkillContentId ?? definition.Party.Single(value =>
            value.CharacterId == character.CharacterId).StartingSkillContentId;

    private static int AttributeTotal(UnitAttributes value) => value.Strength + value.Agility + value.Constitution + value.Intelligence + value.Charisma + value.Luck;
    private static void Shuffle<T>(IList<T> values, Random random)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }
    private static int AttributeValue(UnitAttributes value, string name) => name switch
    {
        "Strength" => value.Strength, "Agility" => value.Agility, "Constitution" => value.Constitution,
        "Intelligence" => value.Intelligence, "Charisma" => value.Charisma, "Luck" => value.Luck,
        "" => int.MaxValue, _ => int.MinValue
    };
    private static SkillRole CharacterRole(RunCharacterState character) => character.UnitContentId.Value switch
    {
        "unit.pure-run.mage" => SkillRole.Mage,
        "unit.pure-run.necromancer" => SkillRole.Necromancer,
        "unit.pure-run.amazon" => SkillRole.Amazon,
        _ => throw new InvalidOperationException($"Unknown progression character '{character.CharacterId}' ({character.UnitContentId.Value}).")
    };
    private static bool AnyAttributeLower(UnitAttributes value, UnitAttributes prior) => value.Strength < prior.Strength || value.Agility < prior.Agility || value.Constitution < prior.Constitution || value.Intelligence < prior.Intelligence || value.Charisma < prior.Charisma || value.Luck < prior.Luck;
    private static RunCharacterState[] Replace(IReadOnlyList<RunCharacterState> party, RunCharacterState updated) => party.Select(value => value.CharacterId == updated.CharacterId ? updated : value).ToArray();
    private static RunCharacterState Copy(RunCharacterState value, IReadOnlyList<RunEquipmentState>? equipment = null, IReadOnlyList<BattleConsumableState>? carried = null, int? maxHealth = null, int? maxMana = null) =>
        new(value.CharacterId, value.UnitContentId, value.Level, value.Attributes, Math.Min(value.CurrentHealth, maxHealth ?? value.MaxHealth), maxHealth ?? value.MaxHealth, Math.Min(value.CurrentMana, maxMana ?? value.MaxMana), maxMana ?? value.MaxMana, value.IsDead, value.LearnedSkills, equipment ?? value.Equipment, carried ?? value.CarriedConsumables, value.LearnedSkillStates, value.StartingSkillContentId);
    private static PureRunState Copy(PureRunState value, IReadOnlyList<RunCharacterState>? party = null, IReadOnlyList<BattleConsumableState>? backpackConsumables = null, IReadOnlyList<RunEquipmentState>? backpackEquipment = null, IReadOnlyList<PendingProgression>? pending = null, string? transaction = null, IReadOnlyList<string>? transactions = null) =>
        new(value.RunId, value.Seed, value.Revision + 1, value.Phase, value.EncounterIndex, value.EncounterContentId, party ?? value.Party,
            backpackConsumables ?? value.BackpackConsumables, backpackEquipment ?? value.BackpackEquipment, pending ?? value.PendingProgression,
            transactions ?? (transaction is null ? value.AppliedTransactionKeys : value.AppliedTransactionKeys.Append(transaction).ToArray()), value.Gold, value.BattlesCompleted, value.EnemiesDefeated, value.AcquiredItems, value.Checkpoint,
            value.MapState, value.NodeTransaction);
    private static RunMutationResult Success(PureRunState state) => new(true, null, state);
    private static RunMutationResult Reject(PureRunState state, string code) => new(false, code, state);
}
