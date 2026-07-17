using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tactics.Consumables;
using Tactics.Equipment;
using Tactics.Roguelike;
using Tactics.RoguelikeMap.Interaction;
using Tactics.Roster;

namespace Tactics.Tests.Editor
{
    public class CharacterLoadoutServiceTests
    {
        [TearDown]
        public void TearDown()
        {
            PureRunSessionStore.Clear();
        }

        [Test]
        public void ConsumableCarryReplaceAndUnload_PreservesIndependentInstancesAndTailOrder()
        {
            var state = CreateState();
            var character = state.Roster[0];
            var life = ConsumableInstance.Create(ConsumableDatabase.GetById("life_potion"), "life_a");
            var mana = ConsumableInstance.Create(ConsumableDatabase.GetById("mana_potion"), "mana_b");
            var cleanse = ConsumableInstance.Create(ConsumableDatabase.GetById("cleansing_potion"), "cleanse_c");
            state.ConsumableInstances.AddRange(new[] { life, mana, cleanse });

            Assert.That(CharacterLoadoutService.TryCarryConsumable(state, character.Id, life.InstanceId), Is.True);
            Assert.That(CharacterLoadoutService.TryCarryConsumable(state, character.Id, mana.InstanceId), Is.True);
            Assert.That(character.CarriedConsumableInstanceId, Is.EqualTo(mana.InstanceId));
            Assert.That(
                CharacterLoadoutService.GetBackpackConsumables(state).Select(item => item.InstanceId),
                Is.EqualTo(new[] { cleanse.InstanceId, life.InstanceId }));

            Assert.That(CharacterLoadoutService.TryUnloadConsumable(state, character.Id), Is.True);
            Assert.That(character.CarriedConsumableInstanceId, Is.Null);
            Assert.That(
                CharacterLoadoutService.GetBackpackConsumables(state).Select(item => item.InstanceId),
                Is.EqualTo(new[] { cleanse.InstanceId, life.InstanceId, mana.InstanceId }));
        }

        [Test]
        public void EquipmentEquip_ReplacesSameSlotAndReturnsOldItemToBackpackTail()
        {
            var state = CreateState();
            var character = state.Roster[0];
            character.Equipment[EquipmentSlot.Weapon] = "staff_01";
            state.Inventory.Add("leather_armor_01");
            state.Inventory.Add("sword_01");

            Assert.That(CharacterLoadoutService.TryEquipEquipment(state, character.Id, "sword_01"), Is.True);
            Assert.That(character.Equipment[EquipmentSlot.Weapon], Is.EqualTo("sword_01"));
            Assert.That(state.Inventory, Is.EqualTo(new[] { "leather_armor_01", "staff_01" }));
        }

        [Test]
        public void DeadCharacter_CannotReceiveEquipmentOrConsumable()
        {
            var state = CreateState();
            var character = state.Roster[0];
            character.IsDead = true;
            state.Inventory.Add("sword_01");
            var item = ConsumableInstance.Create(ConsumableDatabase.GetById("life_potion"), "dead_item");
            state.ConsumableInstances.Add(item);

            Assert.That(CharacterLoadoutService.TryEquipEquipment(state, character.Id, "sword_01"), Is.False);
            Assert.That(CharacterLoadoutService.TryCarryConsumable(state, character.Id, item.InstanceId), Is.False);
            Assert.That(state.Inventory, Does.Contain("sword_01"));
            Assert.That(character.CarriedConsumableInstanceId, Is.Null);
        }

        [Test]
        public void AutoUnloadDeadCharacters_IsIdempotentAndDoesNotDuplicateLoadout()
        {
            var state = CreateState();
            var character = state.Roster[0];
            var item = ConsumableInstance.Create(ConsumableDatabase.GetById("life_potion"), "death_item");
            state.ConsumableInstances.Add(item);
            character.Equipment[EquipmentSlot.Weapon] = "sword_01";
            character.CarriedConsumableInstanceId = item.InstanceId;
            character.IsDead = true;

            Assert.That(CharacterLoadoutService.AutoUnloadDeadCharacters(state), Is.True);
            Assert.That(CharacterLoadoutService.AutoUnloadDeadCharacters(state), Is.False);
            Assert.That(state.Inventory.Count(id => id == "sword_01"), Is.EqualTo(1));
            Assert.That(character.Equipment[EquipmentSlot.Weapon], Is.Null);
            Assert.That(character.CarriedConsumableInstanceId, Is.Null);
            Assert.That(CharacterLoadoutService.GetBackpackConsumables(state).Single().InstanceId, Is.EqualTo(item.InstanceId));
        }

        [Test]
        public void EventDamageDeath_AutoUnloadsWithinRewardApplication()
        {
            var state = CreateState();
            var character = state.Roster[0];
            var item = ConsumableInstance.Create(ConsumableDatabase.GetById("mana_potion"), "event_item");
            state.ConsumableInstances.Add(item);
            character.CurrentHp = 5;
            character.Equipment[EquipmentSlot.Weapon] = "sword_01";
            character.CarriedConsumableInstanceId = item.InstanceId;

            var reward = RewardResult.Empty();
            reward.TargetCharacterIds.Add(character.Id);
            reward.DamageAmount = 5;
            reward.ApplyToState(state);

            Assert.That(character.IsDead, Is.True);
            Assert.That(character.Equipment[EquipmentSlot.Weapon], Is.Null);
            Assert.That(character.CarriedConsumableInstanceId, Is.Null);
            Assert.That(state.Inventory, Does.Contain("sword_01"));
            Assert.That(CharacterLoadoutService.GetBackpackConsumables(state).Single().InstanceId, Is.EqualTo(item.InstanceId));
        }

        [Test]
        public void VersionFourMigration_ConvertsLegacyCopiesAndIsIdempotent()
        {
            var state = CreateState();
            state.Version = 4;
            state.ConsumableInstances = new List<ConsumableInstance>
            {
                new ConsumableInstance { InstanceId = "duplicate", DefinitionId = "field_ration", RemainingCharges = 1, MaxCharges = 3 },
                new ConsumableInstance { InstanceId = "duplicate", DefinitionId = "catnip_tonic", RemainingCharges = 1, MaxCharges = 3 },
                new ConsumableInstance { InstanceId = "bandage", DefinitionId = "bandage_roll", RemainingCharges = 3, MaxCharges = 3 }
            };
            var deadCharacter = state.Roster[0];
            deadCharacter.IsDead = true;
            deadCharacter.Equipment[EquipmentSlot.Weapon] = "sword_01";
            deadCharacter.CarriedConsumableInstanceId = "duplicate";

            Assert.That(RepairState(state), Is.True);
            Assert.That(state.Version, Is.EqualTo(PlayerAdventureStateStore.CurrentVersion));
            Assert.That(state.ConsumableInstances.Count(item => item.DefinitionId == "life_potion"), Is.EqualTo(1));
            Assert.That(state.ConsumableInstances.Count(item => item.DefinitionId == "mana_potion"), Is.EqualTo(1));
            Assert.That(state.ConsumableInstances.Count(item => item.DefinitionId == "cleansing_potion"), Is.EqualTo(3));
            Assert.That(state.ConsumableInstances.Select(item => item.InstanceId).Distinct().Count(), Is.EqualTo(5));
            Assert.That(state.Roster.All(character => string.IsNullOrWhiteSpace(character.CarriedConsumableInstanceId)), Is.True);
            Assert.That(deadCharacter.Equipment[EquipmentSlot.Weapon], Is.Null);
            Assert.That(state.Inventory, Does.Contain("sword_01"));
            Assert.That(RepairState(state), Is.False);
        }

        private static PlayerAdventureState CreateState()
        {
            return PlayerAdventureStateStore.CreatePureRunState(20260717);
        }

        private static bool RepairState(PlayerAdventureState state)
        {
            var method = typeof(PlayerAdventureStateStore).GetMethod(
                "RepairInPlace",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(null, new object[] { state });
        }
    }
}
