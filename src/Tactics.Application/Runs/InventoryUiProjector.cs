using Tactics.Core.Content;
using Tactics.Core.Items;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Runs;

public sealed record InventoryAttributeProjection(UnitAttributes Base, UnitAttributes Bonus, UnitAttributes Total,
    UnitDerivedStats DerivedStats);
public sealed record InventoryCharacterSnapshot(RunCharacterState Character, InventoryAttributeProjection Attributes);
public sealed record InventoryItemDetailSnapshot(ItemInstanceId InstanceId, ContentId DefinitionId, string DisplayName,
    ItemRarity Rarity, int Price, EquipmentSlot? Slot, UnitAttributes AttributeBonuses, string Description);
public sealed record InventoryUiSnapshot(
    IReadOnlyList<InventoryCharacterSnapshot> Characters,
    InventoryCharacterSnapshot SelectedCharacter,
    IReadOnlyList<RunEquipmentState> BackpackEquipment,
    IReadOnlyList<BattleConsumableState> BackpackConsumables,
    InventoryItemDetailSnapshot? SelectedItem);

/// <summary>Projects committed run inventory facts for UI without adapter-owned stat calculations.</summary>
public sealed class InventoryUiProjector
{
    public InventoryUiSnapshot Project(PureRunState run, string selectedCharacterId, ItemInstanceId? selectedItemId,
        IReadOnlyDictionary<ContentId, EquipmentDefinition> equipment,
        IReadOnlyDictionary<ContentId, ConsumableDefinition> consumables,
        IReadOnlyDictionary<ContentId, float> baseSpeeds)
    {
        ArgumentNullException.ThrowIfNull(run);
        InventoryCharacterSnapshot[] characters = run.Party.Select(character =>
        {
            EquipmentDefinition[] loadout = character.Equipment.Select(item => equipment[item.DefinitionId]).ToArray();
            EquipmentStatProjection projection = EquipmentStatProjector.Project(character.Attributes,
                baseSpeeds[character.UnitContentId], loadout);
            return new InventoryCharacterSnapshot(character, new InventoryAttributeProjection(character.Attributes,
                Subtract(projection.Attributes, character.Attributes), projection.Attributes, projection.DerivedStats));
        }).ToArray();
        InventoryCharacterSnapshot selected = characters.FirstOrDefault(value => value.Character.CharacterId == selectedCharacterId)
            ?? characters[0];
        InventoryItemDetailSnapshot? detail = null;
        if (selectedItemId is ItemInstanceId id)
        {
            RunEquipmentState? equipmentItem = run.BackpackEquipment.FirstOrDefault(value => value.InstanceId == id);
            if (equipmentItem is not null)
            {
                EquipmentDefinition definition = equipment[equipmentItem.DefinitionId];
                detail = new InventoryItemDetailSnapshot(id, definition.ContentId, definition.DisplayName,
                    definition.Rarity, definition.Price, definition.Slot, definition.AttributeBonuses, string.Empty);
            }
            else if (run.BackpackConsumables.FirstOrDefault(value => value.InstanceId == id) is BattleConsumableState consumable)
            {
                ConsumableDefinition definition = consumables[consumable.DefinitionId];
                detail = new InventoryItemDetailSnapshot(id, definition.ContentId, definition.DisplayName,
                    definition.Rarity, definition.Price, null, default, definition.Description);
            }
        }
        return new InventoryUiSnapshot(characters, selected, run.BackpackEquipment, run.BackpackConsumables, detail);
    }

    private static UnitAttributes Subtract(UnitAttributes total, UnitAttributes basis) => new(
        total.Strength - basis.Strength, total.Agility - basis.Agility,
        total.Constitution - basis.Constitution, total.Intelligence - basis.Intelligence,
        total.Charisma - basis.Charisma, total.Luck - basis.Luck);
}
