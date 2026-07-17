using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tactics.Common.Units.Buffs;
using Tactics.Consumables;
using Tactics.Equipment;
using Tactics.RoguelikeMap.Economy;
using Tactics.Roster;
using Tactics.Runtime.Utilities;

namespace Tactics.RoguelikeMap.Interaction
{
    /// <summary>
    /// 统一奖励结果类
    /// 用于收束各节点的奖励处理和展示
    /// </summary>
    [System.Serializable]
    public class RewardResult
    {
        /// <summary>金币奖励</summary>
        public int GoldAmount { get; set; }

        /// <summary>金币花费</summary>
        public int GoldCost { get; set; }

        /// <summary>获得的装备ID列表</summary>
        public List<string> EquipmentIds { get; set; } = new List<string>();

        /// <summary>获得的物品ID列表</summary>
        public List<string> ItemIds { get; set; } = new List<string>();

        /// <summary>
        /// 受影响角色 ID 列表。为空时表示对整队生效。
        /// </summary>
        public List<string> TargetCharacterIds { get; set; } = new List<string>();

        /// <summary>获得的增益效果</summary>
        public List<BuffConfig> Buffs { get; set; } = new List<BuffConfig>();

        /// <summary>获得的减益效果</summary>
        public List<BuffConfig> Debuffs { get; set; } = new List<BuffConfig>();

        /// <summary>恢复的HP总量</summary>
        public int HealAmount { get; set; }

        /// <summary>按最大HP比例恢复（0-1）。优先于固定 HealAmount。</summary>
        public float HealPercent { get; set; }

        /// <summary>恢复的MP总量</summary>
        public int ManaHealAmount { get; set; }

        /// <summary>按最大MP比例恢复（0-1）。优先于固定 ManaHealAmount。</summary>
        public float ManaHealPercent { get; set; }

        /// <summary>受到的伤害总量</summary>
        public int DamageAmount { get; set; }

        /// <summary>是否为战斗奖励</summary>
        public bool IsBattleReward { get; set; }

        /// <summary>获得的经验值</summary>
        public int ExperienceAmount { get; set; }

        /// <summary>击败的敌人数量</summary>
        public int EnemiesDefeated { get; set; }

        /// <summary>完成的事件数量</summary>
        public int EventsCompleted { get; set; }

        /// <summary>
        /// 应用到 RunSummary
        /// </summary>
        public void ApplyToSummary(RunSummary summary)
        {
            if (summary == null) return;

            if (GoldAmount > 0)
                summary.AddGold(GoldAmount);

            foreach (var equipId in EquipmentIds)
                summary.AddEquipment(equipId);

            foreach (var itemId in ItemIds)
                summary.AddItem(itemId);

            for (int i = 0; i < EnemiesDefeated; i++)
                summary.IncrementEnemiesDefeated();

            for (int i = 0; i < EventsCompleted; i++)
                summary.IncrementEventsCompleted();

            TLog.Info($"[RewardResult] Applied to RunSummary: Gold={GoldAmount}, Equipment={EquipmentIds.Count}, Items={ItemIds.Count}");
        }

        /// <summary>
        /// 应用金币到玩家状态
        /// </summary>
        public void ApplyGoldToState(PlayerAdventureState state)
        {
            if (state == null || (GoldAmount <= 0 && GoldCost <= 0)) return;

            RunGoldManager.Instance.SyncFromState(state);

            if (GoldAmount > 0)
                RunGoldManager.Instance.AddGold(GoldAmount);

            if (GoldCost > 0)
                RunGoldManager.Instance.SpendGold(GoldCost);

            RunGoldManager.Instance.SyncToState(state);
            TLog.Info($"[RewardResult] Applied gold delta to player state. Reward={GoldAmount}, Cost={GoldCost}, Total={state.Gold}");
        }

        /// <summary>
        /// 应用装备到玩家状态
        /// </summary>
        public bool ApplyEquipmentToState(PlayerAdventureState state)
        {
            if (state == null || EquipmentIds.Count == 0) return false;

            state.Inventory ??= new List<string>();
            bool anyAdded = false;
            foreach (var equipId in EquipmentIds)
            {
                if (string.IsNullOrWhiteSpace(equipId))
                    continue;

                if (!EquipmentDatabase.Contains(equipId))
                {
                    TLog.Warning($"[RewardResult] Equipment '{equipId}' not found.");
                    continue;
                }

                state.Inventory.Add(equipId);
                var def = EquipmentDatabase.GetById(equipId);
                TLog.Info($"[RewardResult] Added equipment: {def?.DisplayName ?? equipId}");
                anyAdded = true;
            }
            return anyAdded;
        }

        /// <summary>
        /// 应用物品到玩家状态。
        /// </summary>
        public bool ApplyItemsToState(PlayerAdventureState state)
        {
            if (state == null || ItemIds.Count == 0) return false;

            state.Inventory ??= new List<string>();
            bool anyAdded = false;
            foreach (var itemId in ItemIds)
            {
                if (string.IsNullOrWhiteSpace(itemId))
                    continue;

                var consumable = ConsumableDatabase.GetById(itemId);
                if (consumable != null)
                {
                    state.ConsumableInstances ??= new List<ConsumableInstance>();
                    state.ConsumableInstances.Add(ConsumableInstance.Create(consumable));
                    TLog.Info($"[RewardResult] Added consumable: {consumable.DisplayName}");
                }
                else
                {
                    state.Inventory.Add(itemId);
                    TLog.Info($"[RewardResult] Added legacy item: {itemId}");
                }
                anyAdded = true;
            }
            return anyAdded;
        }

        /// <summary>
        /// 应用HP变化到队伍
        /// </summary>
        public void ApplyHpChangeToParty(List<CharacterDefinition> party)
        {
            if (party == null || party.Count == 0) return;

            if (HealPercent > 0f || HealAmount > 0)
            {
                foreach (var character in ResolveTargetCharacters(party))
                {
                    if (character.IsDead)
                        continue;

                    int oldHp = character.CurrentHp;
                    int healValue = HealPercent > 0f
                        ? (int)System.Math.Ceiling(character.MaxHp * HealPercent)
                        : HealAmount;
                    character.CurrentHp = System.Math.Min(character.MaxHp, character.CurrentHp + healValue);
                    TLog.Info($"[RewardResult] {character.DisplayName} healed: {oldHp} -> {character.CurrentHp}/{character.MaxHp}");
                }
            }

            if (DamageAmount > 0)
            {
                foreach (var character in ResolveTargetCharacters(party))
                {
                    if (character.IsDead)
                        continue;

                    int oldHp = character.CurrentHp;
                    character.CurrentHp = System.Math.Max(0, character.CurrentHp - DamageAmount);
                    if (character.CurrentHp <= 0)
                        character.IsDead = true;
                    TLog.Info($"[RewardResult] {character.DisplayName} damaged: {oldHp} -> {character.CurrentHp}/{character.MaxHp}");
                }
            }
        }

        /// <summary>
        /// 应用MP变化到队伍。
        /// </summary>
        public void ApplyMpChangeToParty(List<CharacterDefinition> party)
        {
            if (party == null || party.Count == 0 || (ManaHealAmount <= 0 && ManaHealPercent <= 0f))
                return;

            foreach (var character in ResolveTargetCharacters(party))
            {
                if (character.IsDead)
                    continue;

                int oldMp = character.CurrentMp ?? 0;
                int manaValue = ManaHealPercent > 0f
                    ? (int)System.Math.Ceiling(character.MaxMp * ManaHealPercent)
                    : ManaHealAmount;
                character.CurrentMp = System.Math.Min(character.MaxMp, oldMp + manaValue);
                TLog.Info($"[RewardResult] {character.DisplayName} mana healed: {oldMp} -> {character.CurrentMp}/{character.MaxMp}");
            }
        }

        /// <summary>
        /// 应用Buff到队伍
        /// </summary>
        public void ApplyBuffsToParty(List<CharacterDefinition> party)
        {
            if (party == null || party.Count == 0) return;

            foreach (var character in ResolveTargetCharacters(party))
            {
                foreach (var buff in Buffs)
                {
                    character.AddPendingBuff(buff);
                    TLog.Info($"[RewardResult] {character.DisplayName} gained buff: {buff.BuffName}");
                }

                foreach (var debuff in Debuffs)
                {
                    character.AddPendingBuff(debuff);
                    TLog.Info($"[RewardResult] {character.DisplayName} gained debuff: {debuff.BuffName}");
                }
            }
        }

        /// <summary>
        /// 合并另一个 RewardResult
        /// </summary>
        public void Merge(RewardResult other)
        {
            if (other == null) return;

            GoldAmount += other.GoldAmount;
            GoldCost += other.GoldCost;
            EquipmentIds.AddRange(other.EquipmentIds);
            ItemIds.AddRange(other.ItemIds);
            Buffs.AddRange(other.Buffs);
            Debuffs.AddRange(other.Debuffs);
            if (TargetCharacterIds.Count == 0)
                TargetCharacterIds.AddRange(other.TargetCharacterIds);
            HealAmount += other.HealAmount;
            HealPercent = System.Math.Max(HealPercent, other.HealPercent);
            ManaHealAmount += other.ManaHealAmount;
            ManaHealPercent = System.Math.Max(ManaHealPercent, other.ManaHealPercent);
            DamageAmount += other.DamageAmount;
            ExperienceAmount += other.ExperienceAmount;
            EnemiesDefeated += other.EnemiesDefeated;
            EventsCompleted += other.EventsCompleted;
        }

        /// <summary>
        /// 将结果统一应用到玩家状态与队伍。
        /// </summary>
        public void ApplyToState(PlayerAdventureState state)
        {
            if (state == null)
                return;

            ApplyGoldToState(state);
            ApplyEquipmentToState(state);
            ApplyItemsToState(state);
            ApplyHpChangeToParty(state.Roster);
            ApplyMpChangeToParty(state.Roster);
            ApplyBuffsToParty(state.Roster);

            // Map effects can kill a character outside battle. Keep death loadout
            // behavior identical to battle settlement and let the caller save once.
            CharacterLoadoutService.AutoUnloadDeadCharacters(state);

            if (ExperienceAmount > 0 && state.Roster != null)
            {
                foreach (var character in state.Roster)
                {
                    if (character.IsDead)
                        continue;

                    character.Experience += ExperienceAmount;
                }
            }
        }

        /// <summary>
        /// 获取展示文本
        /// </summary>
        public string GetDisplayText()
        {
            var sb = new StringBuilder();

            if (GoldAmount > 0)
                sb.AppendLine($"获得金币: {GoldAmount}");

            if (GoldCost > 0)
                sb.AppendLine($"花费金币: {GoldCost}");

            if (EquipmentIds.Count > 0)
                sb.AppendLine($"获得装备: {string.Join(", ", EquipmentIds)}");

            if (ItemIds.Count > 0)
                sb.AppendLine($"获得物品: {string.Join("、", ItemIds.Select(ConsumableDatabase.GetAcquisitionDisplayText))}");

            if (TargetCharacterIds.Count > 0)
                sb.AppendLine($"目标角色: {string.Join(", ", TargetCharacterIds)}");

            if (HealPercent > 0f)
                sb.AppendLine($"恢复HP: {HealPercent:P0}");
            else if (HealAmount > 0)
                sb.AppendLine($"恢复HP: {HealAmount}");

            if (ManaHealPercent > 0f)
                sb.AppendLine($"恢复MP: {ManaHealPercent:P0}");
            else if (ManaHealAmount > 0)
                sb.AppendLine($"恢复MP: {ManaHealAmount}");

            if (DamageAmount > 0)
                sb.AppendLine($"受到伤害: {DamageAmount}");

            if (ExperienceAmount > 0)
                sb.AppendLine($"获得经验: {ExperienceAmount}");

            if (Buffs.Count > 0)
                sb.AppendLine($"获得增益: {string.Join(", ", Buffs.ConvertAll(b => b.BuffName))}");

            if (Debuffs.Count > 0)
                sb.AppendLine($"获得减益: {string.Join(", ", Debuffs.ConvertAll(b => b.BuffName))}");

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// 创建空的奖励结果
        /// </summary>
        public static RewardResult Empty()
        {
            return new RewardResult();
        }

        /// <summary>
        /// 创建金币奖励
        /// </summary>
        public static RewardResult Gold(int amount)
        {
            return new RewardResult { GoldAmount = amount };
        }

        /// <summary>
        /// 创建金币花费结果。
        /// </summary>
        public static RewardResult GoldCostResult(int amount)
        {
            return new RewardResult { GoldCost = amount };
        }

        /// <summary>
        /// 创建装备奖励
        /// </summary>
        public static RewardResult Equipment(string equipmentId)
        {
            return new RewardResult { EquipmentIds = new List<string> { equipmentId } };
        }

        /// <summary>
        /// 创建战斗奖励
        /// </summary>
        public static RewardResult Battle(int gold, int experience, int enemiesDefeated = 1)
        {
            return new RewardResult
            {
                GoldAmount = gold,
                ExperienceAmount = experience,
                EnemiesDefeated = enemiesDefeated,
                IsBattleReward = true
            };
        }

        private IEnumerable<CharacterDefinition> ResolveTargetCharacters(List<CharacterDefinition> party)
        {
            if (party == null || party.Count == 0)
                yield break;

            if (TargetCharacterIds == null || TargetCharacterIds.Count == 0)
            {
                foreach (var character in party)
                    yield return character;
                yield break;
            }

            foreach (var character in party)
            {
                if (character != null && TargetCharacterIds.Contains(character.Id))
                    yield return character;
            }
        }
    }
}
