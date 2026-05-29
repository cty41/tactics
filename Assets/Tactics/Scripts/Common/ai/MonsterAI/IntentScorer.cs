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

                // 按图节点评分
                if (graph != null)
                {
                    foreach (var node in graph.Nodes)
                    {
                        if (node is not ScoreNodeRecord score || !score.Enabled) continue;
                        float raw = CalcRawScore(score.ScoreType, candidate, context);
                        float norm = Mathf.Clamp01(raw);
                        float curve = profile != null ? profile.ApplyCurve(norm, score.ResponseCurve) : norm;
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

                case ScoreType.KillPotential:
                    return candidate.EstimatedKillChance;

                case ScoreType.AllyProximity:
                    return CalcAllyProximity(candidate.Destination ?? context.Self.CurrentCell, context.Allies);

                default:
                    return 0.5f;
            }
        }

        private static void ApplyProfileScores(IntentCandidate candidate, AiContext context, AIProfile profile)
        {
            void TryScore(ScoreType type, bool enabled)
            {
                if (!enabled) return;
                var (weight, curve) = profile.GetScoreConfig(type);
                float raw = CalcRawScore(type, candidate, context);
                float norm = Mathf.Clamp01(raw);
                float cVal = profile.ApplyCurve(norm, curve);
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
        }

        private static void ApplyDefaultScores(IntentCandidate candidate, AiContext context)
        {
            float ds = CalcRawScore(ScoreType.DistanceToTarget, candidate, context);
            candidate.AddScore("DistanceToTarget", ds, ds, ds * 5f);
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

        private static float CalcDist(ICell a, ICell b)
        {
            if (a == null || b == null) return float.MaxValue;
            float dx = a.GridCoordinates.x - b.GridCoordinates.x;
            float dy = a.GridCoordinates.y - b.GridCoordinates.y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
