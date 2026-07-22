using Newtonsoft.Json;
using Tactics.RoguelikeMap.Interaction;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.RoguelikeMap.Events
{
    /// <summary>
    /// 属性类型
    /// </summary>
    public enum AttributeType
    {
        None,       // 无属性要求（自动成功）
        Strength,   // 力量
        Dexterity,  // 敏捷
        Constitution, // 体质
        Intelligence, // 智力
        Charisma    // 魅力
    }

    /// <summary>
    /// 事件选项
    /// </summary>
    [System.Serializable]
    public class EventOption
    {
        [JsonProperty("id")]
        public string stableOptionId;

        [JsonProperty("text")]
        public string text;

        [JsonProperty("attribute")]
        public AttributeType attribute;

        [JsonProperty("baseSuccessRate")]
        public int baseSuccessRate;

        [JsonProperty("success")]
        public EventResult success;

        [JsonProperty("failure")]
        public EventResult failure;

        /// <summary>
        /// 计算实际成功率
        /// </summary>
        /// <param name="attributeValue">属性值</param>
        /// <returns>实际成功率（0-100）</returns>
        public int CalculateSuccessRate(int attributeValue)
        {
            if (attribute == AttributeType.None)
                return 100;

            // Pure Run baseline attributes start at 5.
            int rate = baseSuccessRate + (attributeValue - 5) * 5;
            return Mathf.Clamp(rate, 5, 95);
        }

        /// <summary>
        /// 获取属性名称
        /// </summary>
        public string GetAttributeName()
        {
            switch (attribute)
            {
                case AttributeType.Strength: return "力量";
                case AttributeType.Dexterity: return "敏捷";
                case AttributeType.Constitution: return "体质";
                case AttributeType.Intelligence: return "智力";
                case AttributeType.Charisma: return "魅力";
                default: return "无";
            }
        }

        /// <summary>
        /// 执行选项（旧版，无上下文时仅输出日志）
        /// </summary>
        /// <param name="attributeValue">属性值</param>
        /// <returns>是否成功</returns>
        public bool Execute(int attributeValue)
        {
            return Execute(attributeValue, out _, out _);
        }

        public bool Execute(int attributeValue, out EventResult resolvedResult, out RewardResult appliedRewardResult)
        {
            resolvedResult = null;
            appliedRewardResult = null;

            if (attribute == AttributeType.None)
            {
                resolvedResult = success;
                appliedRewardResult = success?.Apply(null);
                return true;
            }

            return AttributeCheckSystem.PerformCheck(this, attributeValue, out resolvedResult, out appliedRewardResult);
        }

        /// <summary>
        /// 执行选项（使用事件效果上下文，自动选取目标并读取属性值）
        /// </summary>
        /// <param name="context">事件效果上下文（包含队伍和目标选取逻辑）</param>
        /// <returns>是否成功</returns>
        public bool Execute(EventEffectContext context)
        {
            return Execute(context, out _, out _);
        }

        public bool Execute(EventEffectContext context, out EventResult resolvedResult, out RewardResult appliedRewardResult)
        {
            resolvedResult = null;
            appliedRewardResult = null;

            if (context == null)
                return Execute(10, out resolvedResult, out appliedRewardResult);

            return AttributeCheckSystem.PerformCheck(this, context, out resolvedResult, out appliedRewardResult);
        }
    }
}
