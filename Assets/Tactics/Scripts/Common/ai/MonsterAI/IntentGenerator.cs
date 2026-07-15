using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Cells;
using Tactics.Common.Interactables;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Runtime.Utilities;

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
                        GenerateEngageCandidates(context, intent, candidates);
                        break;
                    case IntentType.BasicAttack:
                        GenerateBasicAttackCandidates(context, intent, candidates);
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

            // Legacy brains may not contain an AbilityUse node. Encounter archetypes can still
            // provide explicit ability configs; generate those candidates through the same
            // legality provider and scorer instead of silently reducing every monster to melee.
            bool graphHasAbilityIntent = graph.Nodes
                .OfType<IntentNodeRecord>()
                .Any(node => node.Enabled && node.IntentType == IntentType.AbilityUse);
            if (!graphHasAbilityIntent && context.AvailableAbilities.Any(ability => !IsMoveAbility(ability)))
            {
                var implicitIntent = new IntentNodeRecord
                {
                    NodeId = "implicit-ability-use",
                    IntentType = IntentType.AbilityUse,
                    BasePriority = 10f
                };
                GenerateAbilityCandidates(context, implicitIntent, candidates);
            }

            context.DecisionLog.Info($"Generated {candidates.Count} intent candidates.");
            return candidates;
        }

        /// <summary>
        /// 生成接敌候选：优先保留移动后可攻击的位置；若本回合无法进入攻击位，则保留少量最近前进格。
        /// </summary>
        private static void GenerateEngageCandidates(AiContext context, IntentNodeRecord intent, List<IntentCandidate> candidates)
        {
            int maxPerTarget = System.Math.Max(1, context.BrainAsset.MaxEngageCandidatesPerTarget);

            foreach (var target in context.CandidateTargets)
            {
                if (target.CurrentCell == null) continue;

                var attackCells = context.ReachableCells
                    .Where(cell => CalcDist(cell, target.CurrentCell) <= context.Self.AttackRange + 0.5f)
                    .OrderBy(cell => CalcDist(context.Self.CurrentCell, cell))
                    .ThenBy(cell => CalcDist(cell, target.CurrentCell))
                    .Take(maxPerTarget)
                    .ToList();

                var selectedCells = attackCells.Count > 0
                    ? attackCells
                    : context.ReachableCells
                        .OrderBy(cell => CalcDist(cell, target.CurrentCell))
                        .ThenBy(cell => CalcDist(context.Self.CurrentCell, cell))
                        .Take(maxPerTarget)
                        .ToList();

                // TEMP: diagnostic log for freeze bug investigation — remove after fix confirmed
                TLog.Info($"[IntentGen] Engage target Unit_{target.UnitID}: " +
                    $"reachableCells={context.ReachableCells.Count}, " +
                    $"attackCells={attackCells.Count}, " +
                    $"selectedCells={selectedCells.Count}, " +
                    $"distToTarget={CalcDist(context.Self.CurrentCell, target.CurrentCell):F2}, " +
                    $"attackRange={context.Self.AttackRange}, " +
                    $"selfPos=({context.Self.CurrentCell?.GridCoordinates.x},{context.Self.CurrentCell?.GridCoordinates.y}), " +
                    $"targetPos=({target.CurrentCell?.GridCoordinates.x},{target.CurrentCell?.GridCoordinates.y})");

                foreach (var cell in selectedCells)
                {
                    bool canAttackFromCell = CalcDist(cell, target.CurrentCell) <= context.Self.AttackRange + 0.5f;
                    var c = new IntentCandidate(IntentType.Engage, ActionType.Move, target, cell, null, intent.BasePriority, sourceIntentNodeId: intent.NodeId);
                    if (canAttackFromCell)
                    {
                        c.EstimatedDamage = context.Self.CalculateDamageDealt(target, target.CurrentCell, cell);
                        c.EstimatedKillChance = target.Health > 0 ? System.Math.Min(1f, c.EstimatedDamage / target.Health) : 1f;
                    }
                    candidates.Add(c);
                }
            }
        }

        private static void GenerateBasicAttackCandidates(AiContext context, IntentNodeRecord intent, List<IntentCandidate> candidates)
        {
            foreach (var target in context.CandidateTargets)
            {
                if (target.CurrentCell == null) continue;
                if (CalcDist(context.Self.CurrentCell, target.CurrentCell) > context.Self.AttackRange + 0.5f) continue;

                var candidate = new IntentCandidate(
                    IntentType.BasicAttack,
                    ActionType.Attack,
                    target,
                    context.Self.CurrentCell,
                    null,
                    intent.BasePriority,
                    sourceIntentNodeId: intent.NodeId);
                candidate.EstimatedDamage = context.Self.CalculateDamageDealt(target, target.CurrentCell, context.Self.CurrentCell);
                candidate.EstimatedKillChance = target.Health > 0 ? System.Math.Min(1f, candidate.EstimatedDamage / target.Health) : 1f;
                candidates.Add(candidate);
            }
        }

        private static void GenerateAbilityCandidates(AiContext context, IntentNodeRecord intent, List<IntentCandidate> candidates)
        {
            foreach (var ability in context.AvailableAbilities)
            {
                if (!ability.IsReady || IsMoveAbility(ability)) continue;

                var origins = context.ReachableCells
                    .Append(context.Self.CurrentCell)
                    .Where(cell => cell != null)
                    .Distinct();

                foreach (var origin in origins)
                {
                    foreach (var option in EnumerateAbilityTargetOptions(context, ability, origin))
                    {
                        var candidate = new IntentCandidate(
                            IntentType.AbilityUse,
                            ActionType.UseAbility,
                            option.PrimaryTarget,
                            origin,
                            ability,
                            intent.BasePriority,
                            option.Targets,
                            option.TargetCell,
                            intent.NodeId);

                        EstimateAbilityOutcome(candidate, context);
                        candidates.Add(candidate);
                    }
                }
            }
        }

        private static IEnumerable<AbilityTargetOption> EnumerateAbilityTargetOptions(
            AiContext context,
            AbilityInfo ability,
            ICell origin)
        {
            if (ability.Ability is IAbilityTargetingProvider targetingProvider)
            {
                var potentialTargets = context.Enemies
                    .Concat(context.Allies)
                    .Append(context.Self);

                var query = new AbilityTargetQuery(
                    context.Self,
                    origin,
                    context.GridController,
                    potentialTargets);
                foreach (var option in targetingProvider.QueryTargets(query).Options)
                {
                    yield return new AbilityTargetOption(
                        option.TargetPoint,
                        option.Targets?.ToList() ?? new List<IUnit>());
                }

                yield break;
            }

            if (ability.Ability is SkillGraphAbilityImpl skillGraph)
            {
                foreach (var option in EnumerateSkillGraphTargetOptions(context, ability, skillGraph, origin))
                    yield return option;
                yield break;
            }

            foreach (var target in context.CandidateTargets)
            {
                if (target.CurrentCell == null) continue;
                if (CalcDist(origin, target.CurrentCell) > ability.Range + 0.5f) continue;
                yield return new AbilityTargetOption(target.CurrentCell, new List<IUnit> { target });
            }
        }

        private static IEnumerable<AbilityTargetOption> EnumerateSkillGraphTargetOptions(
            AiContext context,
            AbilityInfo ability,
            SkillGraphAbilityImpl skillGraph,
            ICell origin)
        {
            var firstNode = skillGraph.FindFirstSelectionNode();
            switch (firstNode)
            {
                case SelectSelfNodeRecord:
                    yield return new AbilityTargetOption(context.Self.CurrentCell, new List<IUnit> { context.Self });
                    yield break;

                case SelectAllyNodeRecord allySelect:
                    foreach (var ally in GetPotentialAllyTargets(context))
                    {
                        if (ally.CurrentCell == null) continue;
                        if (CalcDist(origin, ally.CurrentCell) > allySelect.MaxRange + 0.5f) continue;
                        yield return new AbilityTargetOption(ally.CurrentCell, new List<IUnit> { ally });
                    }
                    yield break;

                case SelectPrimaryTargetNodeRecord primarySelect:
                    foreach (var target in context.CandidateTargets)
                    {
                        if (target.CurrentCell == null) continue;
                        if (CalcDist(origin, target.CurrentCell) > ability.Range + 0.5f) continue;
                        yield return new AbilityTargetOption(target.CurrentCell, new List<IUnit> { target });
                    }
                    yield break;

                case SelectCorpseTargetNodeRecord:
                    foreach (var cell in context.GridController.CellManager.GetCells())
                    {
                        bool hasCorpse = false;
                        foreach (var interactable in cell.CurrentInteractables)
                        {
                            if (interactable is Corpse corpse && !corpse.IsDestroyed) { hasCorpse = true; break; }
                        }
                        if (hasCorpse)
                            yield return new AbilityTargetOption(cell, new List<IUnit>());
                    }
                    yield break;

                default:
                    foreach (var target in context.CandidateTargets)
                    {
                        if (target.CurrentCell == null) continue;
                        if (CalcDist(origin, target.CurrentCell) > ability.Range + 0.5f) continue;
                        yield return new AbilityTargetOption(target.CurrentCell, new List<IUnit> { target });
                    }
                    yield break;
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
                candidates.Add(new IntentCandidate(IntentType.Retreat, ActionType.Move, null, bestCell, null, intent.BasePriority, sourceIntentNodeId: intent.NodeId));
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
                    if (CalcDist(cell, target.CurrentCell) > context.Self.AttackRange + 0.5f) continue;
                    float d = CalcDist(cell, target.CurrentCell);
                    if (d < bestDist) { bestDist = d; bestCell = cell; }
                }

                bool canAttackFromCurrent = CalcDist(context.Self.CurrentCell, target.CurrentCell) <= context.Self.AttackRange + 0.5f;
                if (bestCell != null || canAttackFromCurrent)
                {
                    var attackCell = bestCell ?? context.Self.CurrentCell;
                    var c = new IntentCandidate(IntentType.FinishOff, ActionType.Attack, target, bestCell, null, intent.BasePriority + context.BrainAsset.LowHealthTargetBonus, sourceIntentNodeId: intent.NodeId);
                    c.EstimatedDamage = context.Self.CalculateDamageDealt(target, target.CurrentCell, attackCell);
                    c.EstimatedKillChance = target.Health > 0 ? System.Math.Min(1f, c.EstimatedDamage / target.Health) : 1f;
                    candidates.Add(c);
                }
            }
        }

        private static void GenerateHoldCandidate(AiContext context, IntentNodeRecord intent, List<IntentCandidate> candidates)
        {
            candidates.Add(new IntentCandidate(IntentType.HoldPosition, ActionType.Wait, null, context.Self.CurrentCell, null, intent.BasePriority, sourceIntentNodeId: intent.NodeId));
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
