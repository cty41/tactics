using System.Collections.Generic;
using Newtonsoft.Json;
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
    /// 事件结果
    /// </summary>
    [System.Serializable]
    public class EventResult
    {
        [JsonProperty("type")]
        public EventResultType type;

        [JsonProperty("amount")]
        public int amount;

        [JsonProperty("itemId")]
        public string itemId;

        [JsonProperty("description")]
        public string description;

        /// <summary>
        /// 应用事件结果
        /// </summary>
        public void Apply()
        {
            // TODO: 实现具体的效果应用逻辑
            switch (type)
            {
                case EventResultType.Gold:
                    // TODO: 增加金币
                    TLog.Info($"[EventResult] 获得 {amount} 金币");
                    break;
                case EventResultType.Heal:
                    // TODO: 恢复HP
                    TLog.Info($"[EventResult] 恢复 {amount} HP");
                    break;
                case EventResultType.Damage:
                    // TODO: 受到伤害
                    TLog.Info($"[EventResult] 受到 {amount} 伤害");
                    break;
                case EventResultType.Item:
                    TLog.Info($"[EventResult] 获得物品: {itemId}");
                    break;
                case EventResultType.Equipment:
                    TLog.Info($"[EventResult] 获得装备: {itemId}");
                    break;
                case EventResultType.Nothing:
                    TLog.Info($"[EventResult] 无效果");
                    break;
            }
        }
    }
}
