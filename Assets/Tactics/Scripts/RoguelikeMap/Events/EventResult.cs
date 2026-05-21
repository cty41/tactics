using System.Collections.Generic;
using Newtonsoft.Json;
using Tactics.RoguelikeMap.Economy;
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
            switch (type)
            {
                case EventResultType.Gold:
                    RunGoldManager.Instance.AddGold(amount);
                    TLog.Info($"[EventResult] 获得 {amount} 金币");
                    break;
                case EventResultType.Heal:
                    if (target == EventTargetType.All)
                    {
                        TLog.Info($"[EventResult] 全队每人回复 {amount} HP — TODO: 对接角色HP系统");
                    }
                    else
                    {
                        var character = ctx?.PickTarget(target, AttributeType.None);
                        TLog.Info($"[EventResult] {character?.DisplayName ?? "???"} 回复 {amount} HP — TODO: 对接角色HP系统");
                    }
                    break;
                case EventResultType.Damage:
                    if (target == EventTargetType.All)
                    {
                        TLog.Info($"[EventResult] 全队每人受到 {amount} 伤害 — TODO: 对接角色HP系统");
                    }
                    else
                    {
                        var character = ctx?.PickTarget(target, AttributeType.None);
                        TLog.Info($"[EventResult] {character?.DisplayName ?? "???"} 受到 {amount} 伤害 — TODO: 对接角色HP系统");
                    }
                    break;
                case EventResultType.Item:
                    TLog.Info($"[EventResult] 获得物品: {itemId} (TODO)");
                    break;
                case EventResultType.Equipment:
                    TLog.Info($"[EventResult] 获得装备: {itemId} (TODO)");
                    break;
                case EventResultType.Buff:
                    TLog.Info($"[EventResult] 获得增益: {itemId} (TODO)");
                    break;
                case EventResultType.Debuff:
                    TLog.Info($"[EventResult] 获得减益: {itemId} (TODO)");
                    break;
                case EventResultType.Nothing:
                    TLog.Info($"[EventResult] 无效果");
                    break;
            }
        }
    }
}
