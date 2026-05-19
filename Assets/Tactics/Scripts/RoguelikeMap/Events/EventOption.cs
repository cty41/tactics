using Newtonsoft.Json;
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

            // 公式: 基础成功率 + (属性值 - 10) × 5%
            int rate = baseSuccessRate + (attributeValue - 10) * 5;
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
        /// 执行选项
        /// </summary>
        /// <param name="attributeValue">属性值</param>
        /// <returns>是否成功</returns>
        public bool Execute(int attributeValue)
        {
            if (attribute == AttributeType.None)
            {
                success?.Apply();
                return true;
            }

            int successRate = CalculateSuccessRate(attributeValue);
            int roll = UnityEngine.Random.Range(0, 100);

            if (roll < successRate)
            {
                success?.Apply();
                return true;
            }
            else
            {
                failure?.Apply();
                return false;
            }
        }
    }
}
