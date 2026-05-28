using System.Collections.Generic;
using System.Text;
using Tactics.Common.Units.Buffs;
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

        /// <summary>获得的装备ID列表</summary>
        public List<string> EquipmentIds { get; set; } = new List<string>();

        /// <summary>获得的物品ID列表</summary>
        public List<string> ItemIds { get; set; } = new List<string>();

        /// <summary>获得的增益效果</summary>
        public List<BuffConfig> Buffs { get; set; } = new List<BuffConfig>();

        /// <summary>获得的减益效果</summary>
        public List<BuffConfig> Debuffs { get; set; } = new List<BuffConfig>();

        /// <summary>恢复的HP总量</summary>
        public int HealAmount { get; set; }

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
            if (state == null || GoldAmount <= 0) return;

            state.Gold += GoldAmount;
            TLog.Info($"[RewardResult] Added {GoldAmount} gold to player state. Total={state.Gold}");
        }

        /// <summary>
        /// 应用装备到玩家状态
        /// </summary>
        public bool ApplyEquipmentToState(PlayerAdventureState state)
        {
            if (state == null || EquipmentIds.Count == 0) return false;

            bool anyAdded = false;
            foreach (var equipId in EquipmentIds)
            {
                if (RoguelikeRewardHelper.TryAddEquipmentToInventory(equipId, out string equipmentName, state))
                {
                    TLog.Info($"[RewardResult] Added equipment: {equipmentName}");
                    anyAdded = true;
                }
            }
            return anyAdded;
        }

        /// <summary>
        /// 应用HP变化到队伍
        /// </summary>
        public void ApplyHpChangeToParty(List<CharacterDefinition> party)
        {
            if (party == null || party.Count == 0) return;

            if (HealAmount > 0)
            {
                foreach (var character in party)
                {
                    int oldHp = character.CurrentHp;
                    character.CurrentHp = System.Math.Min(character.MaxHp, character.CurrentHp + HealAmount);
                    TLog.Info($"[RewardResult] {character.DisplayName} healed: {oldHp} -> {character.CurrentHp}/{character.MaxHp}");
                }
            }

            if (DamageAmount > 0)
            {
                foreach (var character in party)
                {
                    int oldHp = character.CurrentHp;
                    character.CurrentHp = System.Math.Max(0, character.CurrentHp - DamageAmount);
                    TLog.Info($"[RewardResult] {character.DisplayName} damaged: {oldHp} -> {character.CurrentHp}/{character.MaxHp}");
                }
            }
        }

        /// <summary>
        /// 应用Buff到队伍
        /// </summary>
        public void ApplyBuffsToParty(List<CharacterDefinition> party)
        {
            if (party == null || party.Count == 0) return;

            foreach (var character in party)
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
            EquipmentIds.AddRange(other.EquipmentIds);
            ItemIds.AddRange(other.ItemIds);
            Buffs.AddRange(other.Buffs);
            Debuffs.AddRange(other.Debuffs);
            HealAmount += other.HealAmount;
            DamageAmount += other.DamageAmount;
            ExperienceAmount += other.ExperienceAmount;
            EnemiesDefeated += other.EnemiesDefeated;
            EventsCompleted += other.EventsCompleted;
        }

        /// <summary>
        /// 获取展示文本
        /// </summary>
        public string GetDisplayText()
        {
            var sb = new StringBuilder();

            if (GoldAmount > 0)
                sb.AppendLine($"获得金币: {GoldAmount}");

            if (EquipmentIds.Count > 0)
                sb.AppendLine($"获得装备: {string.Join(", ", EquipmentIds)}");

            if (ItemIds.Count > 0)
                sb.AppendLine($"获得物品: {string.Join(", ", ItemIds)}");

            if (HealAmount > 0)
                sb.AppendLine($"恢复HP: {HealAmount}");

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
    }
}
