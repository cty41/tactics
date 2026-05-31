using System.Collections.Generic;
using Tactics.Common.Cells;
using Tactics.Common.Units;
using Tactics.Runtime.Utilities;

namespace Tactics.Common.AI.MonsterAI
{
    public static class RuleFilter
    {
        public static void Filter(List<IntentCandidate> candidates, AiContext context)
        {
            var graph = context.BrainAsset.DecisionGraph;
            if (graph == null) return;

            foreach (var candidate in candidates)
            {
                if (!candidate.PassedRules) continue;

                // 找到对应的意图节点，获取关联的规则节点
                IntentNodeRecord intentRecord = null;
                List<RuleNodeRecord> connectedRules = new();

                foreach (var node in graph.Nodes)
                {
                    if (node is IntentNodeRecord intent && intent.IntentType == candidate.IntentType)
                    {
                        intentRecord = intent;
                        break;
                    }
                }

                if (intentRecord != null)
                {
                    foreach (var edge in graph.Edges)
                    {
                        if (edge.SourceNodeId != intentRecord.NodeId) continue;
                        var child = graph.FindNode(edge.TargetNodeId);
                        if (child is RuleNodeRecord rule && rule.Enabled)
                            connectedRules.Add(rule);
                    }
                }

                foreach (var rule in connectedRules)
                {
                    // 检查冷却/一次性状态
                    if (!CanTrigger(rule))
                    {
                        candidate.PassedRules = false;
                        candidate.RuleFailureReason = $"Rule '{rule.RuleName}' on cooldown ({rule.RemainingCooldown}) or already triggered (one-shot).";
                        context.DecisionLog.RuleFiltered(candidate.IntentType.ToString(), rule.RuleName, candidate.RuleFailureReason);
                        break;
                    }

                    if (!ApplyRule(rule, candidate, context))
                    {
                        candidate.PassedRules = false;
                        candidate.RuleFailureReason = $"Rule '{rule.RuleName}' failed.";
                        context.DecisionLog.RuleFiltered(candidate.IntentType.ToString(), rule.RuleName, candidate.RuleFailureReason);
                        break;
                    }

                    // 更新规则状态
                    MarkTriggered(rule);
                }
            }
        }

        private static bool CanTrigger(RuleNodeRecord rule)
        {
            if (rule.IsOneShot && rule.HasTriggered) return false;
            if (rule.RemainingCooldown > 0) return false;
            return true;
        }

        private static void MarkTriggered(RuleNodeRecord rule)
        {
            if (rule.IsOneShot) rule.HasTriggered = true;
            if (rule.CooldownTurns > 0) rule.RemainingCooldown = rule.CooldownTurns;
        }

        /// <summary>每回合开始减少冷却</summary>
        public static void TickCooldowns(AiDecisionGraph graph)
        {
            if (graph == null) return;
            foreach (var node in graph.Nodes)
            {
                if (node is RuleNodeRecord rule && rule.RemainingCooldown > 0)
                {
                    rule.RemainingCooldown--;
                    TLog.Info($"[RuleFilter] Rule '{rule.RuleName}' cooldown: {rule.RemainingCooldown}");
                }
            }
        }

        private static bool ApplyRule(RuleNodeRecord rule, IntentCandidate candidate, AiContext context)
        {
            switch (rule.RuleType)
            {
                case RuleType.TargetInRange:
                    return candidate.Target?.CurrentCell != null &&
                           CalcDist(context.Self.CurrentCell, candidate.Target.CurrentCell) <= context.Self.AttackRange + 0.5f;
                case RuleType.TargetInMoveAttackRange:
                    return candidate.Destination != null && candidate.Target?.CurrentCell != null &&
                           CalcDist(candidate.Destination, candidate.Target.CurrentCell) <= context.Self.AttackRange + 0.5f;
                case RuleType.HealthAboveThreshold:
                    return context.GetSelfHealthPercent() >= rule.Parameter;
                case RuleType.HealthBelowThreshold:
                    return context.GetSelfHealthPercent() <= rule.Parameter;
                case RuleType.HasAvailableAbility:
                    return context.AvailableAbilities.Count > 0;
                case RuleType.TargetKillable:
                    return candidate.Target != null && context.IsTargetKillable(candidate.Target);
                case RuleType.DestinationSafe:
                    return candidate.Destination == null || IsSafe(candidate.Destination, context.Enemies);
                case RuleType.HasAllyNearby:
                    return HasAllyNearby(context);
                case RuleType.HasAbilityTag:
                    return HasAbilityTag(candidate, (AbilityAiTags)(int)rule.Parameter);
                case RuleType.HasDamageAbility:
                    return HasAbilityTag(candidate, AbilityAiTags.Damage);
                case RuleType.HasHealAbility:
                    return HasAbilityTag(candidate, AbilityAiTags.Heal);
                case RuleType.HasControlAbility:
                    return HasAbilityTag(candidate, AbilityAiTags.Control);
                case RuleType.HasAOEAbility:
                    return HasAbilityTag(candidate, AbilityAiTags.Aoe);
                case RuleType.TargetNeedsHealing:
                    return CandidateTargetsNeedHealing(candidate);
                case RuleType.MultiTargetOpportunity:
                    return candidate.EstimatedTargetsHit >= System.Math.Max(2, (int)rule.Parameter);
                default:
                    return true;
            }
        }

        private static bool IsSafe(ICell cell, List<IUnit> enemies)
        {
            foreach (var e in enemies)
            {
                if (e.CurrentCell == null) continue;
                if (CalcDist(cell, e.CurrentCell) <= e.AttackRange + 0.5f) return false;
            }
            return true;
        }

        private static bool HasAllyNearby(AiContext context)
        {
            foreach (var ally in context.Allies)
            {
                if (ally.CurrentCell == null) continue;
                if (CalcDist(context.Self.CurrentCell, ally.CurrentCell) <= 3f) return true;
            }
            return false;
        }

        private static bool HasAbilityTag(IntentCandidate candidate, AbilityAiTags tag)
        {
            return candidate.Action == ActionType.UseAbility &&
                   candidate.Ability != null &&
                   candidate.Ability.HasTag(tag);
        }

        private static bool CandidateTargetsNeedHealing(IntentCandidate candidate)
        {
            if (!HasAbilityTag(candidate, AbilityAiTags.Heal)) return false;

            foreach (var target in candidate.Targets)
            {
                if (target == null || target.IsDowned) continue;
                if (target.Health < target.MaxHealth) return true;
            }

            return false;
        }

        private static float CalcDist(ICell a, ICell b)
        {
            if (a == null || b == null) return float.MaxValue;
            float dx = a.GridCoordinates.x - b.GridCoordinates.x;
            float dy = a.GridCoordinates.y - b.GridCoordinates.y;
            return (float)System.Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
