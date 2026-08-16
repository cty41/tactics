using Tactics.Core.Content;
using Tactics.Core.Units;

namespace Tactics.Core.Items;

public readonly record struct ItemInstanceId
{
    public ItemInstanceId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Item instance ID cannot be empty.", nameof(value));
        Value = value.Trim();
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public enum ItemRarity
{
    Common,
    Uncommon,
    Rare
}

public enum ConsumableEffectKind
{
    RestoreHealth,
    RestoreMana,
    RemoveHarmfulBuffs
}

public enum ConsumableTargetMode
{
    Self,
    AllyIncludingSelf
}

public enum EquipmentSlot
{
    Weapon,
    Armor,
    Helmet,
    Boots,
    Accessory,
    Shield
}

public sealed record ConsumableDefinition
{
    public ConsumableDefinition(
        ContentId contentId,
        string sourceId,
        string displayName,
        string description,
        ItemRarity rarity,
        int price,
        int maxCharges,
        ConsumableEffectKind effectKind,
        int magnitude,
        int maxRange,
        ConsumableTargetMode targetMode)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("SourceId cannot be empty.", nameof(sourceId));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("DisplayName cannot be empty.", nameof(displayName));
        if (!Enum.IsDefined(rarity))
            throw new ArgumentOutOfRangeException(nameof(rarity));
        if (price < 0)
            throw new ArgumentOutOfRangeException(nameof(price));
        if (maxCharges <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCharges));
        if (!Enum.IsDefined(effectKind))
            throw new ArgumentOutOfRangeException(nameof(effectKind));
        if (magnitude < 0)
            throw new ArgumentOutOfRangeException(nameof(magnitude));
        if (maxRange < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRange));
        if (!Enum.IsDefined(targetMode))
            throw new ArgumentOutOfRangeException(nameof(targetMode));
        if (effectKind == ConsumableEffectKind.RemoveHarmfulBuffs && magnitude != 0)
            throw new ArgumentException("Cleansing consumables must use zero magnitude.", nameof(magnitude));

        ContentId = contentId;
        SourceId = sourceId.Trim();
        DisplayName = displayName.Trim();
        Description = description?.Trim() ?? string.Empty;
        Rarity = rarity;
        Price = price;
        MaxCharges = maxCharges;
        EffectKind = effectKind;
        Magnitude = magnitude;
        MaxRange = maxRange;
        TargetMode = targetMode;
    }

    public ContentId ContentId { get; }
    public string SourceId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public ItemRarity Rarity { get; }
    public int Price { get; }
    public int MaxCharges { get; }
    public ConsumableEffectKind EffectKind { get; }
    public int Magnitude { get; }
    public int MaxRange { get; }
    public ConsumableTargetMode TargetMode { get; }
}

public sealed record BattleConsumableState
{
    public BattleConsumableState(
        ItemInstanceId instanceId,
        ContentId definitionId,
        int remainingCharges,
        int maxCharges)
    {
        if (maxCharges <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCharges));
        if (remainingCharges < 0 || remainingCharges > maxCharges)
            throw new ArgumentOutOfRangeException(nameof(remainingCharges));
        InstanceId = instanceId;
        DefinitionId = definitionId;
        RemainingCharges = remainingCharges;
        MaxCharges = maxCharges;
    }

    public ItemInstanceId InstanceId { get; }
    public ContentId DefinitionId { get; }
    public int RemainingCharges { get; }
    public int MaxCharges { get; }

    public BattleConsumableState WithRemainingCharges(int remainingCharges) =>
        new(InstanceId, DefinitionId, remainingCharges, MaxCharges);
}

public sealed record EquipmentDefinition
{
    public EquipmentDefinition(
        ContentId contentId,
        string sourceId,
        string displayName,
        EquipmentSlot slot,
        ItemRarity rarity,
        int price,
        UnitAttributes attributeBonuses)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("SourceId cannot be empty.", nameof(sourceId));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("DisplayName cannot be empty.", nameof(displayName));
        if (!Enum.IsDefined(slot))
            throw new ArgumentOutOfRangeException(nameof(slot));
        if (!Enum.IsDefined(rarity))
            throw new ArgumentOutOfRangeException(nameof(rarity));
        if (price < 0)
            throw new ArgumentOutOfRangeException(nameof(price));

        ContentId = contentId;
        SourceId = sourceId.Trim();
        DisplayName = displayName.Trim();
        Slot = slot;
        Rarity = rarity;
        Price = price;
        AttributeBonuses = attributeBonuses;
    }

    public ContentId ContentId { get; }
    public string SourceId { get; }
    public string DisplayName { get; }
    public EquipmentSlot Slot { get; }
    public ItemRarity Rarity { get; }
    public int Price { get; }
    public UnitAttributes AttributeBonuses { get; }
}

public sealed record EquipmentStatProjection(UnitAttributes Attributes, UnitDerivedStats DerivedStats);

/// <summary>
/// Projects a unique equipment loadout through the frozen Unity unit-derived contract.
/// </summary>
public static class EquipmentStatProjector
{
    public const string ContractId = "equipment-stat-projection-v1";

    public static EquipmentStatProjection Project(
        UnitAttributes baseAttributes,
        float baseSpeed,
        IEnumerable<EquipmentDefinition> equipment)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        EquipmentDefinition[] materialized = equipment.ToArray();
        EquipmentSlot? duplicateSlot = materialized
            .GroupBy(definition => definition.Slot)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateSlot is not null)
            throw new ArgumentException($"Equipment slot '{duplicateSlot}' is occupied more than once.", nameof(equipment));

        UnitAttributes projected = materialized.Aggregate(baseAttributes, AddBonuses);
        return new EquipmentStatProjection(projected, UnitDerivedStatRules.Calculate(projected, baseSpeed));
    }

    private static UnitAttributes AddBonuses(UnitAttributes current, EquipmentDefinition definition) => new(
        checked(current.Strength + definition.AttributeBonuses.Strength),
        checked(current.Agility + definition.AttributeBonuses.Agility),
        checked(current.Constitution + definition.AttributeBonuses.Constitution),
        checked(current.Intelligence + definition.AttributeBonuses.Intelligence),
        checked(current.Charisma + definition.AttributeBonuses.Charisma),
        checked(current.Luck + definition.AttributeBonuses.Luck));
}
