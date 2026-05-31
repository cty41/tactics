using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Cells;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;

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
                        GenerateAbilityCandidates(context, intent, candidates);
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

        private static void GenerateAbilityCandidates(AiContext context, IntentNodeRecord intent, List<IntentCandidate> candidates)
        {
            foreach (var ability in context.AvailableAbilities)
            {
                if (!ability.IsReady || IsMoveAbility(ability)) continue;

                foreach (var option in EnumerateAbilityTargetOptions(context, ability))
                {
                    var candidate = new IntentCandidate(
                        IntentType.AbilityUse,
                        ActionType.UseAbility,
                        option.PrimaryTarget,
                        context.Self.CurrentCell,
                        ability,
                        intent.BasePriority,
                        option.Targets,
                        option.TargetCell);

                    EstimateAbilityOutcome(candidate, context);
                    candidates.Add(candidate);
                }
            }
        }

        private static IEnumerable<AbilityTargetOption> EnumerateAbilityTargetOptions(AiContext context, AbilityInfo ability)
        {
            if (ability.Ability is GenericAbilityImpl generic && generic.Config?.TargetingStrategy != null)
            {
                foreach (var option in EnumerateGenericAbilityTargetOptions(context, ability, generic.Config.TargetingStrategy))
                    yield return option;
                yield break;
            }

            foreach (var target in context.CandidateTargets)
            {
                if (target.CurrentCell == null) continue;
                if (CalcDist(context.Self.CurrentCell, target.CurrentCell) > ability.Range + 0.5f) continue;
                yield return new AbilityTargetOption(target.CurrentCell, new List<IUnit> { target });
            }
        }

        private static IEnumerable<AbilityTargetOption> EnumerateGenericAbilityTargetOptions(AiContext context, AbilityInfo ability, TargetingStrategy strategy)
        {
            switch (strategy)
            {
                case SelfTargeting:
                    yield return new AbilityTargetOption(context.Self.CurrentCell, new List<IUnit> { context.Self });
                    yield break;

                case AoETargeting:
                    foreach (var cell in GetCellsInRange(context, ability.Range))
                    {
                        var targets = strategy.GetTargets(context.Self, cell, context.GridController)
                            .Where(unit => unit != null && !unit.IsDowned)
                            .ToList();
                        if (targets.Count > 0)
                            yield return new AbilityTargetOption(cell, targets);
                    }
                    yield break;

                case MultiTargetEnemy:
                    foreach (var target in context.CandidateTargets)
                    {
                        if (target.CurrentCell == null) continue;
                        if (CalcDist(context.Self.CurrentCell, target.CurrentCell) > ability.Range + 0.5f) continue;

                        var targets = strategy.GetTargets(context.Self, target.CurrentCell, context.GridController)
                            .Where(unit => unit != null && !unit.IsDowned)
                            .ToList();
                        if (targets.Count > 0)
                            yield return new AbilityTargetOption(target.CurrentCell, targets);
                    }
                    yield break;

                case SingleTargetAlly:
                case MoveThenHealTargeting:
                    foreach (var ally in GetPotentialAllyTargets(context))
                    {
                        if (ally.CurrentCell == null) continue;
                        if (CalcDist(context.Self.CurrentCell, ally.CurrentCell) > ability.Range + 0.5f) continue;
                        if (!strategy.IsValidTarget(context.Self, ally, context.GridController)) continue;

                        yield return new AbilityTargetOption(ally.CurrentCell, new List<IUnit> { ally });
                    }
                    yield break;

                default:
                    foreach (var target in context.CandidateTargets)
                    {
                        if (target.CurrentCell == null) continue;
                        if (CalcDist(context.Self.CurrentCell, target.CurrentCell) > ability.Range + 0.5f) continue;
                        if (!strategy.IsValidTarget(context.Self, target, context.GridController)) continue;

                        yield return new AbilityTargetOption(target.CurrentCell, new List<IUnit> { target });
                    }
                    yield break;
            }
        }

        private static IEnumerable<ICell> GetCellsInRange(AiContext context, int range)
        {
            foreach (var cell in context.GridController.CellManager.GetCells())
            {
                if (CalcDist(context.Self.CurrentCell, cell) <= range + 0.5f)
                    yield return cell;
            }
        }

        private static IEnumerable<IUnit> GetPotentialAllyTargets(AiContext context)
        {
            yield return context.Self;
            foreach (var ally in context.Allies)
                yield return ally;
        }

        private static void EstimateAbilityOutcome(IntentCandidate candidate, AiContext context)
        {
            var ability = candidate.Ability;
            if (ability == null) return;

            candidate.EstimatedTargetsHit = candidate.Targets.Count;

            foreach (var target in candidate.Targets)
            {
                if (target == null || target.IsDowned) continue;

                bool isEnemy = target.PlayerNumber != context.Self.PlayerNumber;
                bool isAlly = target.PlayerNumber == context.Self.PlayerNumber;

                if (ability.HasTag(AbilityAiTags.Damage) && isEnemy)
                {
                    float damage = ability.BaseDamage > 0f
                        ? ability.BaseDamage
                        : context.Self.CalculateDamageDealt(target, target.CurrentCell, context.Self.CurrentCell);
                    candidate.EstimatedTotalDamage += damage;
                    if (target == candidate.Target)
                        candidate.EstimatedDamage = damage;
                }
                else if (ability.HasTag(AbilityAiTags.Damage) && isAlly)
                {
                    float damage = ability.BaseDamage > 0f
                        ? ability.BaseDamage
                        : context.Self.CalculateDamageDealt(target, target.CurrentCell, context.Self.CurrentCell);
                    candidate.EstimatedFriendlyFireDamage += damage;
                }

                if (ability.HasTag(AbilityAiTags.Heal) && isAlly)
                {
                    float missingHealth = System.Math.Max(0f, target.MaxHealth - target.Health);
                    float heal = ability.HealAmount > 0f ? System.Math.Min(ability.HealAmount, missingHealth) : missingHealth;
                    candidate.EstimatedHealValue += heal;
                }

                if (ability.HasTag(AbilityAiTags.Control) && isEnemy)
                    candidate.EstimatedControlValue += ability.ControlValue > 0f ? ability.ControlValue : 0.35f;

                if ((ability.HasTag(AbilityAiTags.Buff) && isAlly) || (ability.HasTag(AbilityAiTags.Debuff) && isEnemy))
                    candidate.EstimatedUtilityValue += ability.UtilityValue > 0f ? ability.UtilityValue : 0.25f;
            }

            candidate.EstimatedDamage = candidate.EstimatedDamage > 0f ? candidate.EstimatedDamage : candidate.EstimatedTotalDamage;
            candidate.EstimatedKillChance = candidate.Target != null && candidate.Target.Health > 0
                ? System.Math.Min(1f, candidate.EstimatedDamage / candidate.Target.Health)
                : 0f;
        }

        private static bool IsMoveAbility(AbilityInfo ability)
        {
            return ability.Name == "Move";
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

        private readonly struct AbilityTargetOption
        {
            public ICell TargetCell { get; }
            public List<IUnit> Targets { get; }
            public IUnit PrimaryTarget => Targets.Count > 0 ? Targets[0] : null;

            public AbilityTargetOption(ICell targetCell, List<IUnit> targets)
            {
                TargetCell = targetCell;
                Targets = targets;
            }
        }
    }
}
