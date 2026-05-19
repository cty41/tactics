using Tactics.RoguelikeMap.Events;
using Tactics.Runtime.Utilities;

namespace Tactics.RoguelikeMap.Events
{
    /// <summary>
    /// 属性判定系统
    /// 实现BG3式属性成功率计算
    /// </summary>
    public static class AttributeCheckSystem
    {
        /// <summary>
        /// 计算成功率
        /// 公式: 基础成功率 + (属性值 - 10) × 5%
        /// </summary>
        /// <param name="baseSuccessRate">基础成功率</param>
        /// <param name="attributeValue">属性值</param>
        /// <returns>实际成功率（0-100）</returns>
        public static int CalculateSuccessRate(int baseSuccessRate, int attributeValue)
        {
            int rate = baseSuccessRate + (attributeValue - 10) * 5;
            return ClampRate(rate);
        }

        /// <summary>
        /// 计算成功率（使用EventOption）
        /// </summary>
        public static int CalculateSuccessRate(EventOption option, int attributeValue)
        {
            if (option.attribute == AttributeType.None)
                return 100;

            return CalculateSuccessRate(option.baseSuccessRate, attributeValue);
        }

        /// <summary>
        /// 限制成功率在5-95之间
        /// </summary>
        private static int ClampRate(int rate)
        {
            if (rate < 5) return 5;
            if (rate > 95) return 95;
            return rate;
        }

        /// <summary>
        /// 执行属性判定
        /// </summary>
        /// <param name="option">事件选项</param>
        /// <param name="attributeValue">属性值</param>
        /// <returns>是否成功</returns>
        public static bool PerformCheck(EventOption option, int attributeValue)
        {
            if (option.attribute == AttributeType.None)
            {
                TLog.Info($"[AttributeCheck] 自动成功");
                return true;
            }

            int successRate = CalculateSuccessRate(option, attributeValue);
            int roll = UnityEngine.Random.Range(0, 100);

            TLog.Info($"[AttributeCheck] 属性: {option.GetAttributeName()}, 值: {attributeValue}, 成功率: {successRate}%, 掷骰: {roll}");

            bool success = roll < successRate;

            if (success)
            {
                TLog.Info($"[AttributeCheck] 判定成功!");
                option.success?.Apply();
            }
            else
            {
                TLog.Info($"[AttributeCheck] 判定失败!");
                option.failure?.Apply();
            }

            return success;
        }

        /// <summary>
        /// 获取属性值对应的描述
        /// </summary>
        public static string GetAttributeDescription(int attributeValue)
        {
            if (attributeValue <= 6) return "极低";
            if (attributeValue <= 8) return "低";
            if (attributeValue <= 10) return "普通";
            if (attributeValue <= 12) return "较高";
            if (attributeValue <= 14) return "高";
            if (attributeValue <= 16) return "很高";
            if (attributeValue <= 18) return "极高";
            return "巅峰";
        }

        /// <summary>
        /// 获取成功率对应的颜色（用于UI显示）
        /// </summary>
        public static UnityEngine.Color GetSuccessRateColor(int successRate)
        {
            if (successRate >= 70)
                return UnityEngine.Color.green;
            if (successRate >= 40)
                return UnityEngine.Color.yellow;
            return UnityEngine.Color.red;
        }
    }
}
