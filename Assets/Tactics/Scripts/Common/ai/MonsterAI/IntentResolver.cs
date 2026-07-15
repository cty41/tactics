using System.Collections.Generic;
using System.Linq;

namespace Tactics.Common.AI.MonsterAI
{
    /// <summary>
    /// 意图解析器。
    /// 处理排序、平局和最低阈值。
    /// </summary>
    public static class IntentResolver
    {
        /// <summary>
        /// 从候选列表中选择最佳意图。
        /// </summary>
        public static IntentCandidate Resolve(List<IntentCandidate> candidates, AiContext context)
        {
            // 过滤掉未通过规则的候选
            var validCandidates = candidates.Where(c => c.PassedRules).ToList();

            if (validCandidates.Count == 0)
            {
                context.DecisionLog.Info("No valid candidates found, using fallback.");
                return CreateFallbackCandidate(context);
            }

            // 记录候选列表
            context.DecisionLog.CandidateList(validCandidates);

            // 按总分排序（降序）
            validCandidates.Sort((a, b) => b.TotalScore.CompareTo(a.TotalScore));

            // 检查最高分是否超过最低阈值
            var bestCandidate = validCandidates[0];
            if (bestCandidate.TotalScore < 0f)
            {
                context.DecisionLog.Info("Best candidate score is negative, using fallback.");
                return CreateFallbackCandidate(context);
            }

            // 处理平局
            var tiedCandidates = validCandidates.Where(c =>
                System.Math.Abs(c.TotalScore - bestCandidate.TotalScore) < 0.01f
            ).ToList();

            if (tiedCandidates.Count > 1)
            {
                context.DecisionLog.Info($"Tie detected between {tiedCandidates.Count} candidates.");
                bestCandidate = ResolveTie(tiedCandidates, context);
            }

            context.DecisionLog.FinalSelection(bestCandidate);
            return bestCandidate;
        }

        /// <summary>
        /// 解决平局。
        /// </summary>
        private static IntentCandidate ResolveTie(List<IntentCandidate> tiedCandidates, AiContext context)
        {
            // 策略1: 优先选择更具体的意图
            var priorityOrder = new[]
            {
                IntentType.FinishOff,
                IntentType.BasicAttack,
                IntentType.AbilityUse,
                IntentType.Engage,
                IntentType.Retreat,
                IntentType.HoldPosition
            };

            foreach (var intentType in priorityOrder)
            {
                var candidate = tiedCandidates.FirstOrDefault(c => c.IntentType == intentType);
                if (candidate != null)
                {
                    context.DecisionLog.Info($"Tie resolved by intent priority: {intentType}");
                    return candidate;
                }
            }

            // Stable ordering makes the same run seed reproducible and avoids consuming
            // Unity's global random stream during otherwise identical decisions.
            var stable = tiedCandidates
                .OrderBy(candidate => candidate.Ability?.Name ?? string.Empty)
                .ThenBy(candidate => candidate.Target?.UnitID ?? int.MaxValue)
                .ThenBy(candidate => candidate.Destination?.GridCoordinates.x ?? int.MaxValue)
                .ThenBy(candidate => candidate.Destination?.GridCoordinates.y ?? int.MaxValue)
                .ThenBy(candidate => candidate.AbilityTargetCell?.GridCoordinates.x ?? int.MaxValue)
                .ThenBy(candidate => candidate.AbilityTargetCell?.GridCoordinates.y ?? int.MaxValue)
                .First();
            context.DecisionLog.Info("Tie resolved by stable candidate ordering.");
            return stable;
        }

        /// <summary>
        /// 创建兜底候选（当没有有效候选时）。
        /// </summary>
        private static IntentCandidate CreateFallbackCandidate(AiContext context)
        {
            // 默认选择待机
            var fallback = new IntentCandidate(
                IntentType.HoldPosition,
                ActionType.Wait,
                null,
                context.Self.CurrentCell,
                null,
                0f
            );
            fallback.PassedRules = true;
            fallback.TotalScore = 0f;
            context.DecisionLog.Info("Created fallback candidate: HoldPosition");
            return fallback;
        }
    }
}
