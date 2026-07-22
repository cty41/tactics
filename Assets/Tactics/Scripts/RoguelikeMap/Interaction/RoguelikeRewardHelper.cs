using System.Collections.Generic;
using System.Linq;
using Tactics.Equipment;
using Tactics.RoguelikeMap.Events;
using Tactics.Roster;
using Tactics.Runtime.Utilities;

namespace Tactics.RoguelikeMap.Interaction
{
    public static class RoguelikeRewardHelper
    {
        public static bool TryAddInventoryItem(string itemId, PlayerAdventureState state = null, bool saveState = true)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                TLog.Warning("[RoguelikeRewardHelper] itemId 为空");
                return false;
            }

            state ??= PlayerAdventureStateStore.LoadRepairAndSave();
            if (state == null)
            {
                TLog.Warning("[RoguelikeRewardHelper] 无法加载玩家状态");
                return false;
            }

            state.Inventory ??= new List<string>();
            state.Inventory.Add(itemId);
            if (saveState)
                PlayerAdventureStateStore.Save(state);

            return true;
        }

        public static bool TryAddEquipmentToInventory(string equipmentId, out string displayName, PlayerAdventureState state = null, bool saveState = true)
        {
            displayName = equipmentId;
            if (string.IsNullOrWhiteSpace(equipmentId))
            {
                TLog.Warning("[RoguelikeRewardHelper] equipmentId 为空");
                return false;
            }

            if (!EquipmentDatabase.Contains(equipmentId))
            {
                TLog.Warning($"[RoguelikeRewardHelper] Equipment '{equipmentId}' not found.");
                return false;
            }

            state ??= PlayerAdventureStateStore.LoadRepairAndSave();
            if (state == null)
            {
                TLog.Warning("[RoguelikeRewardHelper] 无法加载玩家状态");
                return false;
            }

            state.Inventory ??= new List<string>();
            state.Inventory.Add(equipmentId);
            if (saveState)
                PlayerAdventureStateStore.Save(state);

            var def = EquipmentDatabase.GetById(equipmentId);
            displayName = def?.DisplayName ?? equipmentId;
            return true;
        }

        public static EventEffectContext CreateActivePartyContext(PlayerAdventureState state)
        {
            if (state?.Roster == null || state.ActivePartyCharacterIds == null)
                return new EventEffectContext(new List<CharacterDefinition>(), null, state);

            var party = state.Roster
                .Where(c => c != null && !c.IsDead && state.ActivePartyCharacterIds.Contains(c.Id))
                .ToList();

            string selfCharacterId = party.FirstOrDefault()?.Id;
            return new EventEffectContext(party, selfCharacterId, state);
        }
    }
}
