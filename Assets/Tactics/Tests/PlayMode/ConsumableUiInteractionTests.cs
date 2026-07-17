using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Tactics.Tests.PlayMode
{
    public class ConsumableUiInteractionTests
    {
        [Test]
        public void InventoryTree_ContainsUnifiedFiltersCarriedSlotAndPopoverContract()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Tactics/Arts/UI/Inventory.uxml");
            Assert.That(asset, Is.Not.Null);
            var root = asset.CloneTree();

            Assert.That(root.Q<Button>("InventoryFilterAll"), Is.Not.Null);
            Assert.That(root.Q<Button>("InventoryFilterEquipment"), Is.Not.Null);
            Assert.That(root.Q<Button>("InventoryFilterConsumable"), Is.Not.Null);
            Assert.That(root.Q<VisualElement>("CarriedConsumableSlot"), Is.Not.Null);
            Assert.That(root.Q<VisualElement>("InventoryItemPopover"), Is.Not.Null);
            Assert.That(root.Q<Button>("InventoryItemActionButton"), Is.Not.Null);
            Assert.That(root.Q<VisualElement>("InventoryItemPopover").ClassListContains("inventory-item-popover"), Is.True);
        }

        [Test]
        public void BattleTree_PlacesMoveAndConsumableAboveSeparateSkillRow()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Tactics/Arts/UI/Battle.uxml");
            Assert.That(asset, Is.Not.Null);
            var root = asset.CloneTree();
            var actionPanel = root.Q<VisualElement>("ActionPanel");
            var skillPanel = root.Q<VisualElement>("SkillPanel");
            var moveButton = root.Q<Button>("MoveButton");
            var consumableButton = root.Q<Button>("BattleConsumableButton");

            Assert.That(actionPanel, Is.Not.Null);
            Assert.That(skillPanel, Is.Not.Null);
            Assert.That(moveButton.parent, Is.SameAs(actionPanel));
            Assert.That(consumableButton.parent, Is.SameAs(actionPanel));
            Assert.That(skillPanel.parent, Is.SameAs(actionPanel.parent));
            Assert.That(skillPanel.Q<Button>("BattleConsumableButton"), Is.Null);
            Assert.That(actionPanel.parent.IndexOf(actionPanel), Is.LessThan(actionPanel.parent.IndexOf(skillPanel)));
        }
    }
}
