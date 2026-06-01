using System.Collections.Generic;
using Tactics.Common.Cells;
using Tactics.Common.Units;

namespace Tactics.Common.AI.MonsterAI
{
    /// <summary>
    /// 动作类型枚举。
    /// </summary>
    public enum ActionType
    {
        None,
        Move,
        Attack,
        UseAbility,
        Wait
    }

    /// <summary>
    /// 候选动作方案。
    /// 完整的可执行方案，包含落点、动作、目标、预估结果。
    /// </summary>
    public class IntentCandidate
    {
        /// <summary>意图类型</summary>
        public IntentType IntentType { get; }

        /// <summary>生成该候选的图意图节点 ID。为空时表示非图候选或旧兼容候选。</summary>
        public string SourceIntentNodeId { get; }

        /// <summary>执行动作类型</summary>
        public ActionType Action { get; }

        /// <summary>移动落点（可为 null）</summary>
        public ICell Destination { get; }

        /// <summary>候选技能（可为 null）</summary>
        public AbilityInfo Ability { get; }

        /// <summary>候选目标（可为 null）</summary>
        public IUnit Target { get; }

        /// <summary>候选目标集合，用于 AOE / 群体技能。</summary>
        public List<IUnit> Targets { get; }

        /// <summary>技能点击目标格（可为 null）。</summary>
        public ICell AbilityTargetCell { get; }

        /// <summary>预估造成伤害</summary>
        public float EstimatedDamage { get; set; }

        /// <summary>预估击杀概率（0-1）</summary>
        public float EstimatedKillChance { get; set; }

        public int EstimatedTargetsHit { get; set; }
        public float EstimatedTotalDamage { get; set; }
        public float EstimatedFriendlyFireDamage { get; set; }
        public float EstimatedControlValue { get; set; }
        public float EstimatedUtilityValue { get; set; }
        public float EstimatedHealValue { get; set; }

        /// <summary>总分</summary>
        public float TotalScore { get; set; }

        /// <summary>分项评分 (scoreName -> rawValue, curveValue, weightedValue)</summary>
        public Dictionary<string, ScoreDetail> ScoreBreakdown { get; }

        /// <summary>规则失败原因（如果被过滤）</summary>
        public string RuleFailureReason { get; set; }

        /// <summary>是否通过规则过滤</summary>
        public bool PassedRules { get; set; }

        /// <summary>基础优先级</summary>
        public float BasePriority { get; }

        public IntentCandidate(
            IntentType intentType,
            ActionType action,
            IUnit target,
            ICell destination,
            AbilityInfo ability,
            float basePriority,
            List<IUnit> targets = null,
            ICell abilityTargetCell = null,
            string sourceIntentNodeId = null)
        {
            IntentType = intentType;
            SourceIntentNodeId = sourceIntentNodeId;
            Action = action;
            Target = target;
            Destination = destination;
            Ability = ability;
            Targets = targets ?? (target != null ? new List<IUnit> { target } : new List<IUnit>());
            AbilityTargetCell = abilityTargetCell ?? target?.CurrentCell;
            BasePriority = basePriority;
            TotalScore = 0f;
            ScoreBreakdown = new Dictionary<string, ScoreDetail>();
            PassedRules = true;
            RuleFailureReason = null;
        }

        /// <summary>
        /// 添加分项评分（含原始值、曲线值、加权值）。
        /// </summary>
        public void AddScore(string scoreName, float rawValue, float curveValue, float weightedValue)
        {
            ScoreBreakdown[scoreName] = new ScoreDetail
            {
                RawValue = rawValue,
                CurveValue = curveValue,
                WeightedValue = weightedValue
            };
        }

        /// <summary>
        /// 计算加权总分。
        /// </summary>
        public void CalculateTotalScore()
        {
            float sum = 0f;
            foreach (var kvp in ScoreBreakdown)
            {
                sum += kvp.Value.WeightedValue;
            }
            TotalScore = BasePriority + sum;
        }

        public string GetDebugInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Intent: {IntentType} | Action: {Action}");
            sb.AppendLine($"  Target: {(Target != null ? $"Unit_{Target.UnitID}" : "None")}");
            sb.AppendLine($"  TargetsHit: {EstimatedTargetsHit}");
            sb.AppendLine($"  Destination: {(Destination != null ? $"({Destination.GridCoordinates.x}, {Destination.GridCoordinates.y})" : "None")}");
            sb.AppendLine($"  AbilityTargetCell: {(AbilityTargetCell != null ? $"({AbilityTargetCell.GridCoordinates.x}, {AbilityTargetCell.GridCoordinates.y})" : "None")}");
            sb.AppendLine($"  Ability: {(Ability?.Name ?? "None")}");
            sb.AppendLine($"  Base: {BasePriority} | Total: {TotalScore:F2} | Pass: {PassedRules}");
            sb.AppendLine($"  EstDamage: {EstimatedDamage:F0} | TotalDamage: {EstimatedTotalDamage:F0} | FriendlyFire: {EstimatedFriendlyFireDamage:F0} | Heal: {EstimatedHealValue:F0} | KillChance: {EstimatedKillChance:P0}");
            if (!string.IsNullOrEmpty(RuleFailureReason))
                sb.AppendLine($"  FAIL: {RuleFailureReason}");
            foreach (var kvp in ScoreBreakdown)
                sb.AppendLine($"  [{kvp.Key}] raw={kvp.Value.RawValue:F2} curve={kvp.Value.CurveValue:F2} w={kvp.Value.WeightedValue:F2}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// 评分明细：原始值、曲线后值、加权后值。
    /// </summary>
    public struct ScoreDetail
    {
        public float RawValue;
        public float CurveValue;
        public float WeightedValue;
    }
}
