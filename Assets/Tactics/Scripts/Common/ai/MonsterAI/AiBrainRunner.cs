using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Controllers;
using Tactics.Common.Units;
using Tactics.Runtime.Utilities;

namespace Tactics.Common.AI.MonsterAI
{
    /// <summary>
    /// AI 运行器。
    /// 单位单回合 AI 入口，职责固定为：
    /// 1. 构建 AiContext
    /// 2. 生成候选意图
    /// 3. 规则过滤
    /// 4. 评分聚合
    /// 5. 选择最佳候选
    /// 6. 调用执行器落地
    /// </summary>
    public static class AiBrainRunner
    {
        /// <summary>
        /// 执行 AI 决策和行动。
        /// </summary>
        public static async Task Execute(IUnit unit, IGridController gridController, AiBrainAsset brainAsset)
        {
            await ExecuteTurn(unit, gridController, brainAsset);
        }

        /// <summary>
        /// Executes one AI turn and exposes the selected plan and execution outcome.
        /// </summary>
        public static async Task<AiTurnResult> ExecuteTurn(IUnit unit, IGridController gridController, AiBrainAsset brainAsset)
        {
            if (unit == null)
            {
                TLog.Error("[AiBrainRunner] Unit is null.");
                return null;
            }

            if (gridController == null)
            {
                TLog.Error("[AiBrainRunner] GridController is null.");
                return null;
            }

            if (brainAsset == null)
            {
                TLog.Error("[AiBrainRunner] BrainAsset is null.");
                return null;
            }

            if (!brainAsset.IsValid())
            {
                TLog.Error("[AiBrainRunner] BrainAsset is not valid.");
                return null;
            }

            TLog.Info($"[AiBrainRunner] Executing AI for unit: {unit.UnitID}");

            // 1. 构建上下文
            var context = AiContextBuilder.Build(unit, gridController, brainAsset);

            // 2. 生成候选意图
            var candidates = IntentGenerator.Generate(context);

            int patternStep = -1;
            bool usedFallback = false;
            string expectedAbility = null;
            if (brainAsset.PatternSteps.Count > 0)
            {
                patternStep = AiPatternRuntime.GetStep(unit) % brainAsset.PatternSteps.Count;
                expectedAbility = brainAsset.PatternSteps[patternStep].AbilityName;
            }

            // 3. 规则过滤
            RuleFilter.Filter(candidates, context);

            // 4. 评分聚合
            IntentScorer.Score(candidates, context);

            // 5. 选择最佳候选
            var candidatesToResolve = candidates;
            if (expectedAbility != null)
            {
                var legalPatternCandidates = candidates
                    .Where(candidate => candidate.PassedRules && candidate.Ability?.Name == expectedAbility)
                    .ToList();
                if (legalPatternCandidates.Count > 0)
                    candidatesToResolve = legalPatternCandidates;
                else
                    usedFallback = true;
            }
            var selected = IntentResolver.Resolve(candidatesToResolve, context);

            // 6. 调用执行器落地
            var execution = await IntentExecutor.ExecuteWithResult(selected, context);
            if (IsDestroyed(unit))
                return new AiTurnResult(null, execution, patternStep);
            if (execution.Succeeded && !usedFallback && expectedAbility != null && selected?.Ability?.Name == expectedAbility)
                AiPatternRuntime.Advance(unit, brainAsset.PatternSteps.Count);

            if (usedFallback && execution.Succeeded)
                execution = AiActionExecutionResult.Success(execution.AbilityName, execution.Moved, true);

            var plan = selected == null
                ? null
                : new AiActionPlan(
                    unit,
                    gridController,
                    unit.CurrentCell,
                    selected.Destination,
                    selected.AbilityTargetCell,
                    selected.Targets,
                    selected.Ability);

            // 输出决策日志
            if (brainAsset.EnableVerboseLogging)
            {
                TLog.Info(context.DecisionLog.GetFormattedLog());
            }

            TLog.Info($"[AiBrainRunner] Completed AI execution for unit: {unit.UnitID}");
            return new AiTurnResult(plan, execution, patternStep);
        }

        private static bool IsDestroyed(IUnit unit)
        {
            return unit is UnityEngine.Object unityObject && unityObject == null;
        }

        /// <summary>
        /// 执行 AI 决策和行动，返回决策日志。
        /// </summary>
        public static async Task<AiDecisionLog> ExecuteWithLog(IUnit unit, IGridController gridController, AiBrainAsset brainAsset)
        {
            if (unit == null)
            {
                TLog.Error("[AiBrainRunner] Unit is null.");
                return null;
            }

            if (gridController == null)
            {
                TLog.Error("[AiBrainRunner] GridController is null.");
                return null;
            }

            if (brainAsset == null)
            {
                TLog.Error("[AiBrainRunner] BrainAsset is null.");
                return null;
            }

            if (!brainAsset.IsValid())
            {
                TLog.Error("[AiBrainRunner] BrainAsset is not valid.");
                return null;
            }

            // 1. 构建上下文
            var context = AiContextBuilder.Build(unit, gridController, brainAsset);

            // 2. 生成候选意图
            var candidates = IntentGenerator.Generate(context);

            // 3. 规则过滤
            RuleFilter.Filter(candidates, context);

            // 4. 评分聚合
            IntentScorer.Score(candidates, context);

            // 5. 选择最佳候选
            var selected = IntentResolver.Resolve(candidates, context);

            // 6. 调用执行器落地
            await IntentExecutor.Execute(selected, context);

            return context.DecisionLog;
        }
    }
}
