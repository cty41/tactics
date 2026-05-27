using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Tactics.AssetPipeline;
using Tactics.Common.Units.Buffs;
using Tactics.Equipment;
using Tactics.RoguelikeMap.Interaction;
using Tactics.RoguelikeMap.Economy;
using Tactics.Roster;
using Tactics.Runtime.Utilities;

namespace Tactics.RoguelikeMap.Events
{
    /// <summary>
    /// 事件结果类型
    /// </summary>
    public enum EventResultType
    {
        Nothing,    // 无效果
        Gold,       // 获得金币
        Item,       // 获得物品
        Equipment,  // 获得装备
        Heal,       // 恢复HP
        Damage,     // 受到伤害
        Buff,       // 获得增益
        Debuff      // 获得减益
    }

    /// <summary>
    /// 事件效果目标类型（BG3风格）
    /// </summary>
    public enum EventTargetType
    {
        Self,       // 自身
        RandomAlly, // 随机队友
        All         // 全体（UI展示属性最高者）
    }

    /// <summary>
    /// 事件结果
    /// </summary>
    [System.Serializable]
    public class EventResult
    {
        [JsonProperty("type")]
        public EventResultType type;

        [JsonProperty("target")]
        public EventTargetType target = EventTargetType.All;

        [JsonProperty("amount")]
        public int amount;

        [JsonProperty("itemId")]
        public string itemId;

        [JsonProperty("description")]
        public string description;

        /// <summary>
        /// 应用事件结果
        /// </summary>
        /// <param name="ctx">事件效果上下文（包含队伍和目标选取逻辑），null时仅输出日志</param>
        public void Apply(EventEffectContext ctx)
        {
            bool shouldSaveState = false;
            switch (type)
            {
                case EventResultType.Gold:
                    RunGoldManager.Instance.AddGold(amount);
                    TLog.Info($"[EventResult] 获得 {amount} 金币");
                    break;
                case EventResultType.Heal:
                    ApplyHeal(ctx);
                    shouldSaveState = true;
                    break;
                case EventResultType.Damage:
                    ApplyDamage(ctx);
                    shouldSaveState = true;
                    break;
                case EventResultType.Item:
                    TLog.Warning($"[EventResult] Item 奖励暂未接入独立背包系统: {itemId}");
                    break;
                case EventResultType.Equipment:
                    if (RoguelikeRewardHelper.TryAddEquipmentToInventory(itemId, out string equipmentName, ctx?.AdventureState))
                    {
                        TLog.Info($"[EventResult] 获得装备: {equipmentName}");
                        shouldSaveState = true;
                    }
                    break;
                case EventResultType.Buff:
                    ApplyBuff(ctx, isDebuff: false);
                    shouldSaveState = true;
                    break;
                case EventResultType.Debuff:
                    ApplyBuff(ctx, isDebuff: true);
                    shouldSaveState = true;
                    break;
                case EventResultType.Nothing:
                    TLog.Info($"[EventResult] 无效果");
                    break;
            }

            if (shouldSaveState)
                ctx?.SaveAdventureState();
        }

        /// <summary>
        /// 恢复HP：All目标对全队生效，其他目标选取单个角色
        /// </summary>
        private void ApplyHeal(EventEffectContext ctx)
        {
            if (ctx == null)
            {
                TLog.Warning("[EventResult] Heal 效果需要 EventEffectContext，当前为 null");
                return;
            }

            if (target == EventTargetType.All)
            {
                foreach (var character in ctx.Party)
                {
                    HealCharacter(character, amount);
                }
                TLog.Info($"[EventResult] 全队每人回复 {amount} HP");
            }
            else
            {
                var character = ctx.PickTarget(target, AttributeType.None);
                if (character != null)
                {
                    HealCharacter(character, amount);
                    TLog.Info($"[EventResult] {character.DisplayName} 回复 {amount} HP (HP: {character.CurrentHp}/{character.MaxHp})");
                }
            }
        }

        /// <summary>
        /// 造成伤害：All目标对全队生效，其他目标选取单个角色
        /// </summary>
        private void ApplyDamage(EventEffectContext ctx)
        {
            if (ctx == null)
            {
                TLog.Warning("[EventResult] Damage 效果需要 EventEffectContext，当前为 null");
                return;
            }

            if (target == EventTargetType.All)
            {
                foreach (var character in ctx.Party)
                {
                    DamageCharacter(character, amount);
                }
                TLog.Info($"[EventResult] 全队每人受到 {amount} 伤害");
            }
            else
            {
                var character = ctx.PickTarget(target, AttributeType.None);
                if (character != null)
                {
                    DamageCharacter(character, amount);
                    TLog.Info($"[EventResult] {character.DisplayName} 受到 {amount} 伤害 (HP: {character.CurrentHp}/{character.MaxHp})");
                }
            }
        }

        /// <summary>
        /// 添加 Buff/Debuff 到 PendingBuffs：通过 itemId 加载 BuffConfig 资产
        /// </summary>
        private void ApplyBuff(EventEffectContext ctx, bool isDebuff)
        {
            if (ctx == null)
            {
                TLog.Warning($"[EventResult] {(isDebuff ? "Debuff" : "Buff")} 效果需要 EventEffectContext，当前为 null");
                return;
            }

            if (string.IsNullOrEmpty(itemId))
            {
                TLog.Warning($"[EventResult] {(isDebuff ? "Debuff" : "Buff")} 效果的 itemId 为空");
                return;
            }

            BuffConfig buffConfig;
            try
            {
                buffConfig = GameAssetManager.Instance.Load<BuffConfig>(itemId);
            }
            catch (Exception e)
            {
                TLog.Error($"[EventResult] 加载 BuffConfig 失败: {itemId}, 错误: {e.Message}");
                return;
            }

            if (buffConfig == null)
            {
                TLog.Warning($"[EventResult] BuffConfig 未找到: {itemId}");
                return;
            }

            string label = isDebuff ? "减益" : "增益";

            if (target == EventTargetType.All)
            {
                foreach (var character in ctx.Party)
                {
                    character.AddPendingBuff(buffConfig);
                    TLog.Info($"[EventResult] {character.DisplayName} 获得{label}: {buffConfig.BuffName}");
                }
            }
            else
            {
                var character = ctx.PickTarget(target, AttributeType.None);
                if (character != null)
                {
                    character.AddPendingBuff(buffConfig);
                    TLog.Info($"[EventResult] {character.DisplayName} 获得{label}: {buffConfig.BuffName}");
                }
            }
        }

        /// <summary>恢复角色HP，不超过上限</summary>
        private static void HealCharacter(CharacterDefinition character, int amount)
        {
            int oldHp = character.CurrentHp;
            character.CurrentHp = Math.Min(character.MaxHp, character.CurrentHp + amount);
            TLog.Info($"[EventResult] {character.DisplayName} HP: {oldHp} → {character.CurrentHp}/{character.MaxHp}");
        }

        /// <summary>对角色造成伤害，不低于0</summary>
        private static void DamageCharacter(CharacterDefinition character, int amount)
        {
            int oldHp = character.CurrentHp;
            character.CurrentHp = Math.Max(0, character.CurrentHp - amount);
            TLog.Info($"[EventResult] {character.DisplayName} HP: {oldHp} → {character.CurrentHp}/{character.MaxHp}");
        }
    }
}
