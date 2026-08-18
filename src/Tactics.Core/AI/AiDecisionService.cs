using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Core.AI;

/// <summary>Generates current-position, reposition and move-then-skill candidates through canonical transitions.</summary>
public sealed class AiDecisionService
{
    private readonly BattleTransitionService _transitions;
    public AiDecisionService(BattleTransitionService? transitions = null) => _transitions = transitions ?? new BattleTransitionService();

    public AiTurnPlan Decide(BattleState state, AiDefinition definition,
        IReadOnlyDictionary<ContentId, SkillDefinition> skills, int patternIndex = 0,
        bool targetOwnFaction = false)
    {
        BattleUnitState actor = state.Units[state.ActiveUnitId];
        BattleUnitState[] enemies = state.Units.Values.Where(unit => unit.IsAlive &&
                (targetOwnFaction
                    ? unit.Unit.PlayerNumber == actor.Unit.PlayerNumber && unit.Unit.InstanceId != actor.Unit.InstanceId
                    : unit.Unit.PlayerNumber != actor.Unit.PlayerNumber))
            .OrderBy(unit => unit.Unit.InstanceId.Value, StringComparer.Ordinal).ToArray();
        var origins = new List<(GridPoint Cell, BattleState State)> { (actor.Unit.Position, state) };
        foreach (GridPoint cell in state.Board.Cells.Keys.OrderBy(cell => cell.X).ThenBy(cell => cell.Y))
        {
            BattleTransition move = _transitions.Apply(state, new MoveUnitCommand(actor.Unit.InstanceId, cell));
            if (move.Succeeded) origins.Add((cell, move.State));
        }

        var candidates = new List<AiIntentCandidate>();
        foreach (ContentId skillId in definition.SkillIds.OrderBy(value => value.Value, StringComparer.Ordinal))
        {
            SkillDefinition skill = skills[skillId];
            if (skill.ExecutionKind is SkillExecutionKind.Bane or SkillExecutionKind.DemonicRegeneration)
            {
                BattleTransition selfProbe = _transitions.Apply(state, new UseSkillCommand(
                    actor.Unit.InstanceId, actor.Unit.InstanceId, actor.Unit.Position, skill));
                if (selfProbe.Succeeded)
                {
                    bool regeneration = skill.ExecutionKind == SkillExecutionKind.DemonicRegeneration;
                    float missingHealth = 1f - actor.CurrentHealth / (float)actor.MaxHealth;
                    float priority = regeneration ? 20f + missingHealth * 30f : 18f;
                    candidates.Add(new AiIntentCandidate(
                        regeneration ? AiIntentKind.Retreat : AiIntentKind.Debuff,
                        skillId, actor.Unit.Position, actor.Unit.InstanceId, actor.Unit.Position,
                        0, 0, 0, regeneration ? missingHealth * 10f : definition.Profile.HarmfulStatusWeight,
                        true, string.Empty, priority));
                }
                continue;
            }
            foreach ((GridPoint origin, BattleState probeState) in origins)
            foreach (BattleUnitState originalTarget in enemies)
            {
                BattleUnitState target = probeState.Units[originalTarget.Unit.InstanceId];
                BattleTransition probe = _transitions.Apply(probeState, new UseSkillCommand(actor.Unit.InstanceId, target.Unit.InstanceId, target.Unit.Position, skill));
                if (!probe.Succeeded) continue;
                int distance = Manhattan(origin, target.Unit.Position);
                int targets = skill.ExecutionKind == SkillExecutionKind.AreaBlast
                    ? probeState.Units.Values.Count(unit => unit.IsAlive && unit.Unit.PlayerNumber != actor.Unit.PlayerNumber && Manhattan(unit.Unit.Position, target.Unit.Position) <= 2) : 1;
                bool basic = skill.ExecutionKind is SkillExecutionKind.MeleeAttack or SkillExecutionKind.MagicAttack or SkillExecutionKind.RangedAttack;
                AiIntentKind intent = skill.ExecutionKind == SkillExecutionKind.AreaBlast ? AiIntentKind.AreaAttack : skill.ExecutionKind == SkillExecutionKind.AmplifyDamage ? AiIntentKind.Debuff : AiIntentKind.Attack;
                float basePriority = basic ? IntentPriority(definition, "BasicAttack", 25) : 10;
                float proximity = GraphScore(definition, "BasicAttack", "DistanceToTarget", Math.Clamp(distance / 10f, 0f, 1f), 5f);
                float damage = skill.Damage * definition.Profile.DamageWeight;
                float targetScore = targets * definition.Profile.TargetCountWeight +
                    GraphScore(definition, "BasicAttack", "TargetHealth", target.CurrentHealth / (float)target.MaxHealth, 0f);
                float status = intent == AiIntentKind.Debuff ? definition.Profile.HarmfulStatusWeight : 0;
                float reposition = PreferredRangeBonus(definition, actor.Unit.Position, origin, target.Unit.Position);
                candidates.Add(new AiIntentCandidate(intent, skillId, origin, target.Unit.InstanceId, target.Unit.Position,
                    proximity + reposition, damage, targetScore, status, true, string.Empty, basePriority, origin != actor.Unit.Position));
                if (target.CurrentHealth <= Math.Max(1, skill.Damage))
                    candidates.Add(new AiIntentCandidate(AiIntentKind.FinishOff, skillId, origin, target.Unit.InstanceId, target.Unit.Position,
                        GraphScore(definition, "FinishOff", "DistanceToTarget", Math.Clamp(distance / 10f, 0f, 1f), 5f) + reposition,
                        damage + GraphScore(definition, "FinishOff", "KillPotential", 1f, 8f), targetScore, status, true, string.Empty,
                        IntentPriority(definition, "FinishOff", 35), origin != actor.Unit.Position));
            }
        }

        foreach (BattleUnitState target in enemies)
        foreach ((GridPoint origin, _) in origins.Where(value => value.Cell != actor.Unit.Position)
                     .OrderBy(value => Manhattan(value.Cell, target.Unit.Position)).Take(definition.MaximumEngageCandidatesPerTarget))
        {
            float proximity = GraphScore(definition, "Engage", "DistanceToTarget", Math.Clamp(Manhattan(origin, target.Unit.Position) / 10f, 0f, 1f), 5f);
            candidates.Add(new AiIntentCandidate(AiIntentKind.Engage, null, origin, target.Unit.InstanceId, target.Unit.Position,
                proximity + PreferredRangeBonus(definition, actor.Unit.Position, origin, target.Unit.Position), 0, 0, 0, true, string.Empty,
                IntentPriority(definition, "Engage", 15)));
        }

        if (enemies.Length > 0 && actor.CurrentHealth <= actor.MaxHealth * .3f)
        {
            (GridPoint Cell, BattleState State) retreat = origins.OrderByDescending(value => enemies.Min(enemy => Manhattan(value.Cell, enemy.Unit.Position)))
                .ThenBy(value => value.Cell.X).ThenBy(value => value.Cell.Y).First();
            if (retreat.Cell != actor.Unit.Position)
                candidates.Add(new AiIntentCandidate(AiIntentKind.Retreat, null, retreat.Cell, null, retreat.Cell, 0, 0, 0, 0, true, string.Empty,
                    IntentPriority(definition, "Retreat", 30)));
        }
        candidates.Add(new AiIntentCandidate(AiIntentKind.HoldPosition, null, actor.Unit.Position, null, actor.Unit.Position, 0, 0, 0, 0, true, string.Empty,
            IntentPriority(definition, "HoldPosition", 1)));

        IOrderedEnumerable<AiIntentCandidate> ranked = candidates.OrderByDescending(item => item.TotalScore).ThenBy(item => item.Intent)
            .ThenBy(item => item.SkillId?.Value ?? string.Empty, StringComparer.Ordinal).ThenBy(item => item.Destination.X).ThenBy(item => item.Destination.Y)
            .ThenBy(item => item.TargetId?.Value ?? string.Empty, StringComparer.Ordinal);
        ContentId? patternSkill = definition.PatternSkillIds.Count == 0 ? null : definition.PatternSkillIds[patternIndex % definition.PatternSkillIds.Count];
        AiIntentCandidate? patternCandidate = patternSkill is null ? null : ranked.FirstOrDefault(item => item.SkillId == patternSkill);
        AiIntentCandidate selected = patternCandidate ?? ranked.First();
        return new AiTurnPlan(actor.Unit.InstanceId, selected, candidates, patternIndex, patternCandidate is not null);
    }

    private static float PreferredRangeBonus(AiDefinition definition, GridPoint current, GridPoint destination, GridPoint target)
    {
        if (definition.PreferredRangeRepositionBonus <= 0 || Manhattan(current, target) >= definition.PreferredMinimumRange) return 0;
        int distance = Manhattan(destination, target);
        return distance >= definition.PreferredMinimumRange && distance <= definition.PreferredMaximumRange ? definition.PreferredRangeRepositionBonus : 0;
    }

    private static float IntentPriority(AiDefinition definition, string intentType, float fallback) =>
        definition.DecisionGraph?.Intents.FirstOrDefault(intent => intent.Enabled && intent.IntentType == intentType)?.BasePriority ?? fallback;

    private static float GraphScore(AiDefinition definition, string intentType, string scoreType, float input, float fallbackWeight)
    {
        AiDecisionGraphDefinition? graph = definition.DecisionGraph;
        AiIntentDefinition? intent = graph?.Intents.FirstOrDefault(value => value.Enabled && value.IntentType == intentType);
        if (graph is null || intent is null)
            return (1f - input) * fallbackWeight;
        HashSet<string> connected = graph.Edges.Where(edge => edge.SourceNodeId == intent.NodeId).Select(edge => edge.TargetNodeId).ToHashSet(StringComparer.Ordinal);
        AiScoreDefinition? score = graph.Scores.FirstOrDefault(value => value.Enabled && value.ScoreType == scoreType && connected.Contains(value.NodeId));
        if (score is null)
            return 0;
        return EvaluateCurve(score.Curve, input) * score.Weight;
    }

    private static float EvaluateCurve(IReadOnlyList<AiCurveKey> keys, float input)
    {
        if (keys.Count == 0) return input;
        AiCurveKey[] ordered = keys.OrderBy(key => key.Time).ToArray();
        if (input <= ordered[0].Time) return ordered[0].Value;
        for (int index = 1; index < ordered.Length; index++)
        {
            if (input > ordered[index].Time) continue;
            AiCurveKey left = ordered[index - 1]; AiCurveKey right = ordered[index];
            float amount = (input - left.Time) / Math.Max(float.Epsilon, right.Time - left.Time);
            return left.Value + ((right.Value - left.Value) * amount);
        }
        return ordered[^1].Value;
    }
    private static int Manhattan(GridPoint a, GridPoint b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
}
