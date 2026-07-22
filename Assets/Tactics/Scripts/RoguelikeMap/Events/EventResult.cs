using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Tactics.AssetPipeline;
using Tactics.Common.Units.Buffs;
using Tactics.Consumables;
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

        [JsonProperty("itemPoolId")]
        public string itemPoolId;

        [JsonProperty("description")]
        public string description;

        /// <summary>
        /// 尝试将事件结果翻译为统一的 RewardResult。
        /// 对目标敏感语义仍通过 TargetCharacterIds 保留。
        /// </summary>
        public RewardResult ToRewardResult(EventEffectContext ctx = null)
        {
            var result = RewardResult.Empty();

            switch (type)
            {
                case EventResultType.Nothing:
                    return result;
                case EventResultType.Gold:
                    result.GoldAmount = amount;
                    return result;
                case EventResultType.Item:
                    string resolvedItemId = itemId;
                    if (string.IsNullOrWhiteSpace(resolvedItemId) && !string.IsNullOrWhiteSpace(itemPoolId))
                    {
                        int runSeed = ctx?.AdventureState?.RunSeed ?? 0;
                        string nodeId = Tactics.Roguelike.RoguelikeMapRuntimeState.CurrentNodeId ?? "event";
                        int seed = Tactics.Roguelike.RoguelikeMapRuntimeState.DeriveSeed(
                            runSeed,
                            $"event-item:{nodeId}:{itemPoolId}");
                        resolvedItemId = ConsumableDatabase.Roll(itemPoolId, seed)?.Id;
                    }
                    if (!string.IsNullOrWhiteSpace(resolvedItemId))
                        result.ItemIds.Add(resolvedItemId);
                    return result;
                case EventResultType.Equipment:
                    if (!string.IsNullOrWhiteSpace(itemId))
                        result.EquipmentIds.Add(itemId);
                    return result;
                case EventResultType.Heal:
                    result.HealAmount = amount;
                    ApplyTargetSelection(ctx, result);
                    return result;
                case EventResultType.Damage:
                    result.DamageAmount = amount;
                    ApplyTargetSelection(ctx, result);
                    return result;
                case EventResultType.Buff:
                case EventResultType.Debuff:
                    var buffConfig = ResolveBuffConfig();
                    if (buffConfig != null)
                    {
                        if (type == EventResultType.Buff)
                            result.Buffs.Add(buffConfig);
                        else
                            result.Debuffs.Add(buffConfig);
                        ApplyTargetSelection(ctx, result);
                    }
                    return result;
                default:
                    return result;
            }
        }

        /// <summary>
         /// 应用事件结果
         /// </summary>
         /// <param name="ctx">事件效果上下文（包含队伍和目标选取逻辑），null时仅输出日志</param>
        public RewardResult Apply(EventEffectContext ctx)
        {
            var rewardResult = ToRewardResult(ctx);

            bool hasUnifiedPayload = rewardResult.GoldAmount > 0 ||
                                     rewardResult.GoldCost > 0 ||
                                     rewardResult.EquipmentIds.Count > 0 ||
                                     rewardResult.ItemIds.Count > 0 ||
                                     rewardResult.HealAmount > 0 ||
                                     rewardResult.DamageAmount > 0 ||
                                     rewardResult.Buffs.Count > 0 ||
                                     rewardResult.Debuffs.Count > 0;

            if (hasUnifiedPayload)
            {
                ctx?.ApplyRewardResult(rewardResult);

                TLog.Info($"[EventResult] 统一结果已应用: {rewardResult.GetDisplayText()}");
                return rewardResult;
            }

            if (type == EventResultType.Nothing)
            {
                TLog.Info("[EventResult] 无效果");
            }

            return rewardResult;
        }

        public string GetDisplayText(EventEffectContext ctx = null, RewardResult appliedRewardResult = null)
        {
            string unifiedText = (appliedRewardResult ?? ToRewardResult(ctx))?.GetDisplayText();
            if (!string.IsNullOrWhiteSpace(unifiedText))
            {
                return string.IsNullOrWhiteSpace(description)
                    ? unifiedText
                    : $"{description}\n{unifiedText}";
            }

            if (!string.IsNullOrWhiteSpace(description))
                return description;

            return type == EventResultType.Nothing ? "无效果" : string.Empty;
        }

        private void ApplyTargetSelection(EventEffectContext ctx, RewardResult rewardResult)
        {
            if (ctx == null || rewardResult == null || target == EventTargetType.All)
                return;

            var character = ctx.PickTarget(target, AttributeType.None);
            if (character != null)
                rewardResult.TargetCharacterIds.Add(character.Id);
        }

        private BuffConfig ResolveBuffConfig()
        {
            if (string.IsNullOrEmpty(itemId))
            {
                TLog.Warning($"[EventResult] Buff/Debuff 效果的 itemId 为空");
                return null;
            }

            try
            {
                var buffConfig = GameAssetManager.Instance?.Load<BuffConfig>(itemId);
                if (buffConfig != null)
                {
                    buffConfig.RuntimeSourceAssetPath = itemId;
                    return buffConfig;
                }

                // In gameplay tests the asset runtime may be absent; create a minimal runtime config
                // so unified-result targeting/writeback can still be verified without broad asset bootstrap.
                return CreateRuntimeFallbackBuffConfig(itemId);
            }
            catch (Exception e)
            {
                TLog.Error($"[EventResult] 加载 BuffConfig 失败: {itemId}, 错误: {e.Message}");
                return CreateRuntimeFallbackBuffConfig(itemId);
            }
        }

        private BuffConfig CreateRuntimeFallbackBuffConfig(string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                return null;

            string buffName = Path.GetFileNameWithoutExtension(sourceId);
            if (string.IsNullOrWhiteSpace(buffName))
                buffName = sourceId;

            var buffConfig = UnityEngine.ScriptableObject.CreateInstance<BuffConfig>();
            typeof(BuffConfig).GetField("_buffName", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(buffConfig, buffName);
            buffConfig.RuntimeSourceAssetPath = sourceId;
            TLog.Warning($"[EventResult] 使用运行时 BuffConfig 回退: {buffName}");
            return buffConfig;
        }
    }
}
