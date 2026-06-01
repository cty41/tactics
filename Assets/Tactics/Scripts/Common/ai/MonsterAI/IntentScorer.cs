using System.Collections.Generic;
using Tactics.Common.Cells;
using Tactics.Common.Units;
using UnityEngine;

namespace Tactics.Common.AI.MonsterAI
{
    public static class IntentScorer
    {
        public static void Score(List<IntentCandidate> candidates, AiContext context)
        {
            var graph = context.BrainAsset.DecisionGraph;
            var profile = context.BrainAsset.Profile;

            foreach (var candidate in candidates)
            {
                if (!candidate.PassedRules) continue;

                // 按候选来源意图节点的连线评分，避免 HoldPosition 吃到全图所有 ScoreNode。
                if (graph != null)
                {
                    foreach (var score in GetConnectedScores(graph, candidate))
                    {
                        if (!IsScoreApplicable(score.ScoreType, candidate)) continue;

                        float raw = CalcRawScore(score.ScoreType, candidate, context);
                        float norm = Mathf.Clamp01(raw);
                        float curve = ApplyScoreCurve(score.ScoreType, norm, score.ResponseCurve, profile);
                        float weighted = curve * score.Weight;
                        candidate.AddScore(score.ScoreName, raw, curve, weighted);
                        context.DecisionLog.ScoreAdded(candidate.IntentType.ToString(), score.ScoreName, raw, curve, weighted);
                    }
                }
                else if (profile != null)
                {
                    ApplyProfileScores(candidate, context, profile);
                }

                if (candidate.ScoreBreakdown.Count == 0)
                    ApplyDefaultScores(candidate, context);

                // 随机扰动
                if (profile != null)
                {
                    var noise = (Random.value - 0.5f) * 2f * profile.NoiseFactor;
                    candidate.AddScore("Noise", noise, noise, noise);
                }

                candidate.CalculateTotalScore();
            }
        }

        private static List<ScoreNodeRecord> GetConnectedScores(AiDecisionGraph graph, IntentCandidate candidate)
        {
            var scores = new List<ScoreNodeRecord>();
            var intentRecord = FindIntentRecord(graph, candidate);
            if (intentRecord == null)
            {
                return scores;
            }

            foreach (var edge in graph.Edges)
            {
                if (edge.SourceNodeId != intentRecord.NodeId) continue;

                var child = graph.FindNode(edge.TargetNodeId);
                if (child is ScoreNodeRecord score && score.Enabled)
                {
                    scores.Add(score);
                }
            }

            return scores;
        }

        private static IntentNodeRecord FindIntentRecord(AiDecisionGraph graph, IntentCandidate candidate)
        {
            if (!string.IsNullOrEmpty(candidate.SourceIntentNodeId))
            {
                return graph.FindNode(candidate.SourceIntentNodeId) as IntentNodeRecord;
            }

            foreach (var node in graph.Nodes)
            {
                if (node is IntentNodeRecord intent && intent.IntentType == candidate.IntentType)
                {
                    return intent;
                }
            }

            return null;
        }

        private static float CalcRawScore(ScoreType type, IntentCandidate candidate, AiContext context)
        {
            switch (type)
            {
                case ScoreType.DistanceToTarget:
                    if (candidate.Target?.CurrentCell == null) return 0f;
                    var cell = candidate.Destination ?? context.Self.CurrentCell;
                    float d = CalcDist(cell, candidate.Target.CurrentCell);
                    return 1f - Mathf.Clamp01(d / 10f);

                case ScoreType.TargetHealth:
                    if (candidate.Target == null) return 0f;
                    return 1f - context.GetTargetHealthPercent(candidate.Target);

                case ScoreType.SelfHealth:
                    return context.GetSelfHealthPercent();

                case ScoreType.TargetValue:
                    if (candidate.Target == null) return 0f;
                    return (candidate.Target.MaxHealth * 0.1f + candidate.Target.AttackFactor * 0.5f) / 100f;

                case ScoreType.PositionSafety:
                    return CalcSafety(candidate.Destination ?? context.Self.CurrentCell, context.Enemies);

                case ScoreType.AbilityEffectiveness:
                    return CalcAbilityEffectiveness(candidate, context);

                case ScoreType.KillPotential:
                    return candidate.EstimatedKillChance;

                case ScoreType.AllyProximity:
                    return CalcAllyProximity(candidate.Destination ?? context.Self.CurrentCell, context.Allies);

                case ScoreType.AOEValue:
                    return candidate.Ability != null && candidate.Ability.HasTag(AbilityAiTags.Aoe)
                        ? Mathf.Clamp01(candidate.EstimatedTargetsHit / 4f)
                        : 0f;

                case ScoreType.HealUrgency:
                    return Mathf.Clamp01(candidate.EstimatedHealValue / GetAverageMaxHealth(context.Allies, context.Self));

                case ScoreType.ControlValue:
                    return Mathf.Clamp01(candidate.EstimatedControlValue);

                case ScoreType.BuffUtility:
                    return candidate.Ability != null && candidate.Ability.HasTag(AbilityAiTags.Buff)
                        ? Mathf.Clamp01(candidate.EstimatedUtilityValue)
                        : 0f;

                case ScoreType.DebuffUtility:
                    return candidate.Ability != null && candidate.Ability.HasTag(AbilityAiTags.Debuff)
                        ? Mathf.Clamp01(candidate.EstimatedUtilityValue)
                        : 0f;

                default:
                    return 0.5f;
            }
        }

        private static void ApplyProfileScores(IntentCandidate candidate, AiContext context, AIProfile profile)
        {
            void TryScore(ScoreType type, bool enabled)
            {
                if (!enabled) return;
                if (!IsScoreApplicable(type, candidate)) return;

                var (weight, curve) = profile.GetScoreConfig(type);
                float raw = CalcRawScore(type, candidate, context);
                float norm = Mathf.Clamp01(raw);
                float cVal = ApplyScoreCurve(type, norm, curve, profile);
                float wVal = cVal * weight;
                candidate.AddScore(type.ToString(), raw, cVal, wVal);
            }

            TryScore(ScoreType.DistanceToTarget, profile.EnableDistanceScore);
            TryScore(ScoreType.TargetHealth, profile.EnableTargetHealthScore);
            TryScore(ScoreType.SelfHealth, profile.EnableSelfHealthScore);
            TryScore(ScoreType.TargetValue, profile.EnableTargetValueScore);
            TryScore(ScoreType.PositionSafety, profile.EnablePositionSafetyScore);
            TryScore(ScoreType.KillPotential, profile.EnableKillPotentialScore);
            TryScore(ScoreType.AllyProximity, profile.EnableAllyProximityScore);
            TryScore(ScoreType.AbilityEffectiveness, profile.EnableAbilityEffectivenessScore);
            TryScore(ScoreType.AOEValue, profile.EnableAOEValueScore);
            TryScore(ScoreType.HealUrgency, profile.EnableHealUrgencyScore);
            TryScore(ScoreType.ControlValue, profile.EnableControlValueScore);
            TryScore(ScoreType.BuffUtility, profile.EnableBuffUtilityScore);
            TryScore(ScoreType.DebuffUtility, profile.EnableDebuffUtilityScore);
        }

        private static void ApplyDefaultScores(IntentCandidate candidate, AiContext context)
        {
            if (IsScoreApplicable(ScoreType.DistanceToTarget, candidate))
            {
                float ds = CalcRawScore(ScoreType.DistanceToTarget, candidate, context);
                candidate.AddScore("DistanceToTarget", ds, ds, ds * 5f);
            }

            if (candidate.Target != null)
            {
                float hs = CalcRawScore(ScoreType.TargetHealth, candidate, context);
                candidate.AddScore("TargetHealth", hs, hs, hs * 3f);
            }
            if (candidate.IntentType == IntentType.FinishOff)
                candidate.AddScore("FinishOffBonus", 1f, 1f, context.BrainAsset.LowHealthTargetBonus);
            if (candidate.IntentType == IntentType.Retreat && context.IsSelfLowHealth())
                candidate.AddScore("RetreatBonus", 1f, 1f, context.BrainAsset.RetreatBaseScore);
        }

        private static float ApplyScoreCurve(ScoreType type, float normalizedValue, AnimationCurve curve, AIProfile profile)
        {
            // DistanceToTarget 的 raw 语义固定为“接近度”：越近越高。
            // 旧资产里可能保存了反向曲线；这里用正向值兜底，避免奖励远离目标。
            if (type == ScoreType.DistanceToTarget)
            {
                return normalizedValue;
            }

            return profile != null ? profile.ApplyCurve(normalizedValue, curve) : normalizedValue;
        }

        private static bool IsScoreApplicable(ScoreType type, IntentCandidate candidate)
        {
            switch (type)
            {
                case ScoreType.DistanceToTarget:
                case ScoreType.TargetHealth:
                case ScoreType.TargetValue:
                    return candidate.Target != null;
                case ScoreType.AOEValue:
                case ScoreType.AbilityEffectiveness:
                case ScoreType.HealUrgency:
                case ScoreType.ControlValue:
                case ScoreType.BuffUtility:
                case ScoreType.DebuffUtility:
                    return candidate.Ability != null;
                default:
                    return true;
            }
        }

        private static float CalcSafety(ICell cell, List<IUnit> enemies)
        {
            if (enemies.Count == 0) return 1f;
            float threat = 0f;
            foreach (var e in enemies)
            {
                if (e.CurrentCell == null) continue;
                threat += 1f / (CalcDist(cell, e.CurrentCell) + 1f);
            }
            return 1f - Mathf.Clamp01(threat / enemies.Count);
        }

        private static float CalcAllyProximity(ICell cell, List<IUnit> allies)
        {
            if (allies.Count == 0) return 0.5f;
            float sum = 0f;
            foreach (var a in allies)
            {
                if (a.CurrentCell == null) continue;
                sum += CalcDist(cell, a.CurrentCell);
            }
            float avg = sum / allies.Count;
            if (avg >= 2f && avg <= 4f) return 1f;
            if (avg < 2f) return avg / 2f;
            return 1f - Mathf.Clamp01((avg - 4f) / 6f);
        }

        private static float CalcAbilityEffectiveness(IntentCandidate candidate, AiContext context)
        {
            if (candidate.Action != ActionType.UseAbility || candidate.Ability == null) return 0f;

            float healthBasis = GetAverageMaxHealth(context.Enemies, context.Self);
            float damageScore = Mathf.Clamp01(candidate.EstimatedTotalDamage / healthBasis);
            float friendlyFirePenalty = Mathf.Clamp01(candidate.EstimatedFriendlyFireDamage / healthBasis);
            float healScore = Mathf.Clamp01(candidate.EstimatedHealValue / GetAverageMaxHealth(context.Allies, context.Self));
            float aoeScore = candidate.Ability.HasTag(AbilityAiTags.Aoe)
                ? Mathf.Clamp01(candidate.EstimatedTargetsHit / 4f)
                : 0f;
            float controlScore = Mathf.Clamp01(candidate.EstimatedControlValue);
            float utilityScore = Mathf.Clamp01(candidate.EstimatedUtilityValue);

            return Mathf.Clamp01(
                candidate.EstimatedKillChance * 0.35f +
                damageScore * 0.25f +
                healScore * 0.15f +
                aoeScore * 0.1f +
                controlScore * 0.1f +
                utilityScore * 0.05f -
                friendlyFirePenalty * 0.3f);
        }

        private static float GetAverageMaxHealth(List<IUnit> units, IUnit fallback)
        {
            float sum = 0f;
            int count = 0;
            foreach (var unit in units)
            {
                if (unit == null || unit.IsDowned || unit.MaxHealth <= 0f) continue;
                sum += unit.MaxHealth;
                count++;
            }

            if (count > 0) return Mathf.Max(1f, sum / count);
            return Mathf.Max(1f, fallback?.MaxHealth ?? 1f);
        }

        private static float CalcDist(ICell a, ICell b)
        {
            if (a == null || b == null) return float.MaxValue;
            float dx = a.GridCoordinates.x - b.GridCoordinates.x;
            float dy = a.GridCoordinates.y - b.GridCoordinates.y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
