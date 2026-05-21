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
        /// 执行属性判定（旧版，无上下文时仅输出日志）
        /// </summary>
        /// <param name="option">事件选项</param>
        /// <param name="attributeValue">属性值</param>
        /// <returns>是否成功</returns>
        public static bool PerformCheck(EventOption option, int attributeValue)
        {
            if (option.attribute == AttributeType.None)
            {
                option.success?.Apply(null);
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
                option.success?.Apply(null);
            }
            else
            {
                TLog.Info($"[AttributeCheck] 判定失败!");
                option.failure?.Apply(null);
            }

            return success;
        }

        /// <summary>
        /// 执行属性判定（使用事件效果上下文，自动选取目标并读取属性值）
        /// </summary>
        /// <param name="option">事件选项</param>
        /// <param name="ctx">事件效果上下文</param>
        /// <returns>是否成功</returns>
        public static bool PerformCheck(EventOption option, EventEffectContext ctx)
        {
            if (option == null)
            {
                TLog.Warning("[AttributeCheck] 选项为空");
                return false;
            }

            if (ctx == null || ctx.Party.Count == 0)
            {
                TLog.Warning("[AttributeCheck] 上下文为空或队伍为空，降级为旧版判定");
                return PerformCheck(option, 0);
            }

            // 无属性要求，自动成功
            if (option.attribute == AttributeType.None)
            {
                TLog.Info("[AttributeCheck] 无属性要求，自动成功");
                option.success?.Apply(ctx);
                return true;
            }

            // 根据success/failure的target字段选取角色
            EventTargetType targetType = (option.success != null && option.success.target != EventTargetType.All)
                ? option.success.target
                : (option.failure != null ? option.failure.target : EventTargetType.All);

            // 选取目标角色
            var character = ctx.PickTarget(targetType, option.attribute);
            if (character == null)
            {
                TLog.Warning("[AttributeCheck] 无法选取目标角色，降级为旧版判定");
                return PerformCheck(option, 0);
            }

            // 读取属性值
            int attributeValue = EventEffectContext.GetCharacterAttribute(character, option.attribute);

            // 生成BG3风格判定描述
            string adjudicatorText = EventEffectContext.GetAdjudicatorDescription(character, option.attribute);
            TLog.Info($"[AttributeCheck] {adjudicatorText}");

            // 执行判定
            int successRate = CalculateSuccessRate(option, attributeValue);
            int roll = UnityEngine.Random.Range(0, 100);

            TLog.Info($"[AttributeCheck] 目标: {character.DisplayName}, " +
                      $"属性: {option.GetAttributeName()}, 值: {attributeValue}, " +
                      $"成功率: {successRate}%, 掷骰: {roll}");

            bool success = roll < successRate;

            if (success)
            {
                TLog.Info("[AttributeCheck] 判定成功!");
                option.success?.Apply(ctx);
            }
            else
            {
                TLog.Info("[AttributeCheck] 判定失败!");
                option.failure?.Apply(ctx);
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
        /// 阈值: ≥60% 绿色, 40-59% 黄色, &lt;40% 红色
        /// </summary>
        public static UnityEngine.Color GetSuccessRateColor(int successRate)
        {
            if (successRate >= 60)
                return new UnityEngine.Color(0.30f, 0.69f, 0.31f); // #4CAF50
            if (successRate >= 40)
                return new UnityEngine.Color(1.0f, 0.76f, 0.03f);  // #FFC107
            return new UnityEngine.Color(0.96f, 0.26f, 0.21f);     // #F44336
        }
    }
}
