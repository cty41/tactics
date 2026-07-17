using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Consumables;
using Tactics.Equipment;

namespace Tactics.Roster
{
    /// <summary>
    /// Owns all equipment and carried-consumable mutations for adventure characters.
    /// </summary>
    /// <remarks>
    /// Consumable instances stay in the adventure state's master list while carried.
    /// The shared backpack is a filtered view. Replaced and unloaded entries are moved
    /// to the end of their backing list so UI ordering stays deterministic.
    /// </remarks>
    public static class CharacterLoadoutService
    {
        /// <summary>
        /// Returns consumable instances that are not currently carried by any character.
        /// </summary>
        /// <param name="state">Adventure state to inspect.</param>
        /// <returns>A stable-order snapshot of backpack instances.</returns>
        public static IReadOnlyList<ConsumableInstance> GetBackpackConsumables(PlayerAdventureState state)
        {
            if (state?.ConsumableInstances == null)
                return Array.Empty<ConsumableInstance>();

            var carriedIds = new HashSet<string>(
                state.Roster?
                    .Where(character => character != null && !string.IsNullOrWhiteSpace(character.CarriedConsumableInstanceId))
                    .Select(character => character.CarriedConsumableInstanceId)
                ?? Enumerable.Empty<string>(),
                StringComparer.Ordinal);

            return state.ConsumableInstances
                .Where(instance => instance != null && !carriedIds.Contains(instance.InstanceId))
                .ToList();
        }

        /// <summary>
        /// Equips an inventory item, returning any replaced item to the backpack tail.
        /// </summary>
        /// <param name="state">Adventure state to mutate.</param>
        /// <param name="characterId">Target roster character ID.</param>
        /// <param name="equipmentId">Equipment definition ID already present in inventory.</param>
        /// <returns>True when the loadout changed.</returns>
        public static bool TryEquipEquipment(
            PlayerAdventureState state,
            string characterId,
            string equipmentId)
        {
            var character = FindLivingCharacter(state, characterId);
            var definition = EquipmentDatabase.GetById(equipmentId);
            if (character == null || definition == null || state.Inventory == null)
                return false;

            int inventoryIndex = state.Inventory.FindIndex(id =>
                string.Equals(id, equipmentId, StringComparison.Ordinal));
            if (inventoryIndex < 0)
                return false;

            character.Equipment ??= new Dictionary<EquipmentSlot, string>();
            character.Equipment.TryGetValue(definition.Slot, out string replacedId);

            state.Inventory.RemoveAt(inventoryIndex);
            character.Equipment[definition.Slot] = equipmentId;
            if (!string.IsNullOrWhiteSpace(replacedId))
                state.Inventory.Add(replacedId);

            return true;
        }

        /// <summary>
        /// Unequips a character item and appends it to the shared equipment backpack.
        /// </summary>
        /// <param name="state">Adventure state to mutate.</param>
        /// <param name="characterId">Target roster character ID.</param>
        /// <param name="slot">Equipment slot to clear.</param>
        /// <returns>True when an item was unloaded.</returns>
        public static bool TryUnequipEquipment(
            PlayerAdventureState state,
            string characterId,
            EquipmentSlot slot)
        {
            var character = FindCharacter(state, characterId);
            if (character?.Equipment == null ||
                !character.Equipment.TryGetValue(slot, out string equipmentId) ||
                string.IsNullOrWhiteSpace(equipmentId))
            {
                return false;
            }

            state.Inventory ??= new List<string>();
            character.Equipment[slot] = null;
            state.Inventory.Add(equipmentId);
            return true;
        }

        /// <summary>
        /// Carries a backpack consumable and returns the replaced instance to the backpack tail.
        /// </summary>
        /// <param name="state">Adventure state to mutate.</param>
        /// <param name="characterId">Target living roster character ID.</param>
        /// <param name="instanceId">Consumable instance currently present in the shared backpack.</param>
        /// <returns>True when the carried slot changed.</returns>
        public static bool TryCarryConsumable(
            PlayerAdventureState state,
            string characterId,
            string instanceId)
        {
            var character = FindLivingCharacter(state, characterId);
            var instance = FindConsumable(state, instanceId);
            if (character == null || instance == null)
                return false;

            bool alreadyCarried = state.Roster?.Any(candidate =>
                candidate != null &&
                string.Equals(candidate.CarriedConsumableInstanceId, instanceId, StringComparison.Ordinal)) == true;
            if (alreadyCarried)
                return false;

            string replacedId = character.CarriedConsumableInstanceId;
            character.CarriedConsumableInstanceId = instanceId;
            if (!string.IsNullOrWhiteSpace(replacedId))
                MoveConsumableToBack(state, replacedId);

            return true;
        }

        /// <summary>
        /// Unloads the character's carried consumable to the shared backpack tail.
        /// </summary>
        /// <param name="state">Adventure state to mutate.</param>
        /// <param name="characterId">Target roster character ID.</param>
        /// <returns>True when a carried reference was cleared.</returns>
        public static bool TryUnloadConsumable(PlayerAdventureState state, string characterId)
        {
            var character = FindCharacter(state, characterId);
            if (character == null || string.IsNullOrWhiteSpace(character.CarriedConsumableInstanceId))
                return false;

            string instanceId = character.CarriedConsumableInstanceId;
            character.CarriedConsumableInstanceId = null;
            MoveConsumableToBack(state, instanceId);
            return true;
        }

        /// <summary>
        /// Commits one completed consumable use and removes exhausted instances.
        /// </summary>
        /// <param name="state">Adventure state to mutate.</param>
        /// <param name="instanceId">Carried instance that completed its battle effect.</param>
        /// <returns>True when a charge was consumed.</returns>
        public static bool TryCommitConsumableUse(PlayerAdventureState state, string instanceId)
        {
            var instance = FindConsumable(state, instanceId);
            bool isCarried = state?.Roster?.Any(character =>
                character != null &&
                string.Equals(character.CarriedConsumableInstanceId, instanceId, StringComparison.Ordinal)) == true;
            if (instance == null || instance.RemainingCharges <= 0 || !isCarried)
                return false;

            instance.RemainingCharges--;
            if (instance.RemainingCharges > 0)
                return true;

            state.ConsumableInstances.Remove(instance);
            foreach (var character in state.Roster ?? Enumerable.Empty<CharacterDefinition>())
            {
                if (character != null &&
                    string.Equals(character.CarriedConsumableInstanceId, instanceId, StringComparison.Ordinal))
                {
                    character.CarriedConsumableInstanceId = null;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns all dead-character equipment and consumables to the shared backpack.
        /// </summary>
        /// <param name="state">Adventure state to mutate.</param>
        /// <returns>True when at least one loadout entry changed.</returns>
        /// <remarks>
        /// The operation is idempotent and intentionally produces no player-facing message.
        /// </remarks>
        public static bool AutoUnloadDeadCharacters(PlayerAdventureState state)
        {
            if (state?.Roster == null)
                return false;

            state.Inventory ??= new List<string>();
            bool changed = false;
            foreach (var character in state.Roster.Where(character => character?.IsDead == true))
            {
                character.Equipment ??= new Dictionary<EquipmentSlot, string>();
                foreach (var slot in character.Equipment.Keys.ToList())
                {
                    string equipmentId = character.Equipment[slot];
                    if (string.IsNullOrWhiteSpace(equipmentId))
                        continue;

                    character.Equipment[slot] = null;
                    state.Inventory.Add(equipmentId);
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(character.CarriedConsumableInstanceId))
                    continue;

                string instanceId = character.CarriedConsumableInstanceId;
                character.CarriedConsumableInstanceId = null;
                MoveConsumableToBack(state, instanceId);
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Repairs consumable IDs and carried references without dropping valid copies.
        /// </summary>
        /// <param name="state">Adventure state to repair.</param>
        /// <returns>True when the state changed.</returns>
        public static bool RepairLoadouts(PlayerAdventureState state)
        {
            if (state == null)
                return false;

            state.Inventory ??= new List<string>();
            state.ConsumableInstances ??= new List<ConsumableInstance>();
            state.Roster ??= new List<CharacterDefinition>();

            bool changed = false;
            var knownIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < state.ConsumableInstances.Count; index++)
            {
                var instance = state.ConsumableInstances[index];
                if (instance == null || instance.RemainingCharges <= 0)
                {
                    state.ConsumableInstances.RemoveAt(index);
                    index--;
                    changed = true;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(instance.InstanceId) || !knownIds.Add(instance.InstanceId))
                {
                    instance.InstanceId = CreateUniqueInstanceId(knownIds);
                    knownIds.Add(instance.InstanceId);
                    changed = true;
                }
            }

            var instanceIds = new HashSet<string>(
                state.ConsumableInstances.Select(instance => instance.InstanceId),
                StringComparer.Ordinal);
            var claimedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var character in state.Roster)
            {
                if (character == null)
                    continue;

                character.Equipment ??= new Dictionary<EquipmentSlot, string>();
                string carriedId = character.CarriedConsumableInstanceId;
                if (string.IsNullOrWhiteSpace(carriedId))
                    continue;

                if (!instanceIds.Contains(carriedId) || !claimedIds.Add(carriedId))
                {
                    character.CarriedConsumableInstanceId = null;
                    changed = true;
                }
            }

            return AutoUnloadDeadCharacters(state) || changed;
        }

        private static CharacterDefinition FindLivingCharacter(PlayerAdventureState state, string characterId)
        {
            var character = FindCharacter(state, characterId);
            return character?.IsDead == false ? character : null;
        }

        private static CharacterDefinition FindCharacter(PlayerAdventureState state, string characterId)
        {
            return state?.Roster?.FirstOrDefault(character =>
                character != null &&
                string.Equals(character.Id, characterId, StringComparison.Ordinal));
        }

        private static ConsumableInstance FindConsumable(PlayerAdventureState state, string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                return null;

            return state?.ConsumableInstances?.FirstOrDefault(instance =>
                instance != null &&
                string.Equals(instance.InstanceId, instanceId, StringComparison.Ordinal));
        }

        private static void MoveConsumableToBack(PlayerAdventureState state, string instanceId)
        {
            var instance = FindConsumable(state, instanceId);
            if (instance == null)
                return;

            state.ConsumableInstances.Remove(instance);
            state.ConsumableInstances.Add(instance);
        }

        private static string CreateUniqueInstanceId(ISet<string> knownIds)
        {
            string candidate;
            do
            {
                candidate = Guid.NewGuid().ToString("N");
            }
            while (knownIds.Contains(candidate));

            return candidate;
        }
    }
}
