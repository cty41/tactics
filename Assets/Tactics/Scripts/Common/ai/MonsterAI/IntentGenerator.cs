using System.Collections.Generic;
using Tactics.Common.Cells;
using Tactics.Common.Units;

namespace Tactics.Common.AI.MonsterAI
{
    public static class IntentGenerator
    {
        public static List<IntentCandidate> Generate(AiContext context)
        {
            var candidates = new List<IntentCandidate>();
            var graph = context.BrainAsset.DecisionGraph;

            if (graph == null)
            {
                GenerateDefaultCandidates(context, candidates);
                return candidates;
            }

            foreach (var node in graph.Nodes)
            {
                if (node is not IntentNodeRecord intent || !intent.Enabled) continue;

                // 通过边获取关联的 rule/score 节点
                var childNodes = new List<GraphNodeRecord>();
                foreach (var edge in graph.Edges)
                {
                    if (edge.SourceNodeId == intent.NodeId)
                    {
                        var child = graph.FindNode(edge.TargetNodeId);
                        if (child != null) childNodes.Add(child);
                    }
                }
                var rules = childNodes.FindAll(n => n is RuleNodeRecord);
                var scores = childNodes.FindAll(n => n is ScoreNodeRecord);

                switch (intent.IntentType)
                {
                    case IntentType.Engage:
                        GenerateActionCandidates(context, intent, IntentType.Engage, ActionType.Move, candidates);
                        break;
                    case IntentType.BasicAttack:
                        GenerateActionCandidates(context, intent, IntentType.BasicAttack, ActionType.Attack, candidates);
                        break;
                    case IntentType.AbilityUse:
                        GenerateActionCandidates(context, intent, IntentType.AbilityUse, ActionType.UseAbility, candidates);
                        break;
                    case IntentType.Retreat:
                        GenerateRetreatCandidates(context, intent, candidates);
                        break;
                    case IntentType.FinishOff:
                        GenerateFinishOffCandidates(context, intent, candidates);
                        break;
                    case IntentType.HoldPosition:
                        GenerateHoldCandidate(context, intent, candidates);
                        break;
                }
            }

            context.DecisionLog.Info($"Generated {candidates.Count} intent candidates.");
            return candidates;
        }

        /// <summary>对每个可达格+每个敌人生成动作候选</summary>
        private static void GenerateActionCandidates(AiContext context, IntentNodeRecord intent, IntentType intentType, ActionType actionType, List<IntentCandidate> candidates)
        {
            foreach (var cell in context.ReachableCells)
            {
                foreach (var target in context.CandidateTargets)
                {
                    if (target.CurrentCell == null) continue;

                    AbilityInfo ability = null;
                    if (actionType == ActionType.UseAbility)
                    {
                        foreach (var ab in context.AvailableAbilities)
                        {
                            if (ab.Name == "Move") continue;
                            float dist = CalcDist(cell, target.CurrentCell);
                            if (dist <= ab.Range + 0.5f)
                            {
                                ability = ab;
                                break;
                            }
                        }
                        if (ability == null) continue;
                    }
                    else if (actionType == ActionType.Attack)
                    {
                        float dist = CalcDist(cell, target.CurrentCell);
                        if (dist > context.Self.AttackRange + 0.5f) continue;
                    }

                    var c = new IntentCandidate(intentType, actionType, target, cell, ability, intent.BasePriority);
                    // 预估结果
                    if (actionType == ActionType.Attack || actionType == ActionType.UseAbility)
                    {
                        c.EstimatedDamage = context.Self.CalculateDamageDealt(target, target.CurrentCell, cell);
                        c.EstimatedKillChance = target.Health > 0 ? System.Math.Min(1f, c.EstimatedDamage / target.Health) : 1f;
                    }
                    candidates.Add(c);
                }
            }
        }

        private static void GenerateRetreatCandidates(AiContext context, IntentNodeRecord intent, List<IntentCandidate> candidates)
        {
            ICell bestCell = null;
            float bestSafety = float.MinValue;
            foreach (var cell in context.ReachableCells)
            {
                float safety = 0f;
                foreach (var enemy in context.Enemies)
                {
                    if (enemy.CurrentCell == null) continue;
                    safety += CalcDist(cell, enemy.CurrentCell);
                }
                if (safety > bestSafety) { bestSafety = safety; bestCell = cell; }
            }
            if (bestCell != null)
                candidates.Add(new IntentCandidate(IntentType.Retreat, ActionType.Move, null, bestCell, null, intent.BasePriority));
        }

        private static void GenerateFinishOffCandidates(AiContext context, IntentNodeRecord intent, List<IntentCandidate> candidates)
        {
            foreach (var target in context.CandidateTargets)
            {
                if (target.CurrentCell == null) continue;
                float hp = context.GetTargetHealthPercent(target);
                if (hp > context.BrainAsset.KillableDamageThreshold) continue;

                ICell bestCell = null;
                float bestDist = float.MaxValue;
                foreach (var cell in context.ReachableCells)
                {
                    float d = CalcDist(cell, target.CurrentCell);
                    if (d < bestDist) { bestDist = d; bestCell = cell; }
                }
                if (bestCell != null)
                {
                    var c = new IntentCandidate(IntentType.FinishOff, ActionType.Attack, target, bestCell, null, intent.BasePriority + context.BrainAsset.LowHealthTargetBonus);
                    c.EstimatedDamage = context.Self.CalculateDamageDealt(target, target.CurrentCell, bestCell);
                    c.EstimatedKillChance = target.Health > 0 ? System.Math.Min(1f, c.EstimatedDamage / target.Health) : 1f;
                    candidates.Add(c);
                }
            }
        }

        private static void GenerateHoldCandidate(AiContext context, IntentNodeRecord intent, List<IntentCandidate> candidates)
        {
            candidates.Add(new IntentCandidate(IntentType.HoldPosition, ActionType.Wait, null, context.Self.CurrentCell, null, intent.BasePriority));
        }

        private static void GenerateDefaultCandidates(AiContext context, List<IntentCandidate> candidates)
        {
            foreach (var target in context.CandidateTargets)
                candidates.Add(new IntentCandidate(IntentType.BasicAttack, ActionType.Attack, target, null, null, 10f));
            candidates.Add(new IntentCandidate(IntentType.HoldPosition, ActionType.Wait, null, context.Self.CurrentCell, null, 5f));
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
