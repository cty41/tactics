using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Items;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class InventoryUiProjectorTests
{
    [Test]
    public void ProjectsBaseBonusTotalAndSelectedEquipmentDetails()
    {
        UnitAttributes baseAttributes = new(5, 5, 5, 6, 5, 5);
        var definition = new EquipmentDefinition(new ContentId("item.equipment.sword-01"), "sword", "Iron Sword",
            EquipmentSlot.Weapon, ItemRarity.Uncommon, 8, new UnitAttributes(2, 0, 1, 0, 0, 0));
        var equipped = new RunEquipmentState(new ItemInstanceId("equipped-sword"), definition.ContentId, definition.Slot);
        var backpack = new RunEquipmentState(new ItemInstanceId("bag-sword"), definition.ContentId, definition.Slot);
        RunCharacterState character = new("mage", new ContentId("unit.pure-run.mage"), 2, baseAttributes,
            20, 20, 10, 15, false, Array.Empty<ContentId>(), [equipped]);
        PureRunState run = new("run", 7, 1, PureRunPhase.Ready, 0, new ContentId("encounter.pure-run.n1"),
            [character, Character("necro"), Character("amazon")], backpackEquipment: [backpack]);

        InventoryUiSnapshot snapshot = new InventoryUiProjector().Project(run, "mage", backpack.InstanceId,
            new Dictionary<ContentId, EquipmentDefinition> { [definition.ContentId] = definition },
            new Dictionary<ContentId, ConsumableDefinition>(),
            new Dictionary<ContentId, float>
            {
                [character.UnitContentId] = 3,
                [new ContentId("unit.pure-run.necro")] = 3,
                [new ContentId("unit.pure-run.amazon")] = 3
            });

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.SelectedCharacter.Attributes.Base.Strength, Is.EqualTo(5));
            Assert.That(snapshot.SelectedCharacter.Attributes.Bonus.Strength, Is.EqualTo(2));
            Assert.That(snapshot.SelectedCharacter.Attributes.Total.Strength, Is.EqualTo(7));
            Assert.That(snapshot.SelectedCharacter.Attributes.Total.Constitution, Is.EqualTo(6));
            Assert.That(snapshot.SelectedItem!.DisplayName, Is.EqualTo("Iron Sword"));
            Assert.That(snapshot.SelectedItem.Slot, Is.EqualTo(EquipmentSlot.Weapon));
            Assert.That(snapshot.SelectedItem.Rarity, Is.EqualTo(ItemRarity.Uncommon));
            Assert.That(snapshot.SelectedItem.AttributeBonuses.Strength, Is.EqualTo(2));
        });
    }

    private static RunCharacterState Character(string id) => new(id, new ContentId($"unit.pure-run.{id}"), 1,
        new UnitAttributes(5, 5, 5, 5, 5, 5), 20, 20, 5, 10, false, Array.Empty<ContentId>());
}
