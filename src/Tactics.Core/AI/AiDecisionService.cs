using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Core.AI;

/// <summary>Builds and stably ranks legal intents by probing the canonical battle transition service.</summary>
public sealed class AiDecisionService
{
    public AiTurnPlan Decide(BattleState state, AiDefinition definition, IReadOnlyDictionary<ContentId, SkillDefinition> skills, int patternIndex = 0)
    {
        BattleUnitState actor = state.Units[state.ActiveUnitId];
        var candidates = new List<AiIntentCandidate>();
        foreach (ContentId skillId in definition.SkillIds.OrderBy(value => value.Value, StringComparer.Ordinal))
        {
            SkillDefinition skill = skills[skillId];
            foreach (BattleUnitState target in state.Units.Values.Where(unit => unit.IsAlive && unit.Unit.PlayerNumber != actor.Unit.PlayerNumber).OrderBy(unit => unit.Unit.InstanceId.Value, StringComparer.Ordinal))
            {
                int distance = Manhattan(actor.Unit.Position, target.Unit.Position);
                bool legal = distance >= skill.MinRange && distance <= skill.MaxRange && actor.CurrentMana >= skill.ManaCost;
                int targetCount = skill.ExecutionKind == SkillExecutionKind.AreaBlast ? state.Units.Values.Count(unit => unit.IsAlive && unit.Unit.PlayerNumber != actor.Unit.PlayerNumber && Manhattan(unit.Unit.Position,target.Unit.Position)<=2) : 1;
                AiIntentKind intent = skill.ExecutionKind == SkillExecutionKind.AreaBlast ? AiIntentKind.AreaAttack : skill.ExecutionKind == SkillExecutionKind.AmplifyDamage ? AiIntentKind.Debuff : AiIntentKind.Attack;
                float distanceScore = definition.Archetype == AiArchetype.Ranged ? -Math.Abs(distance-3)*definition.Profile.DistanceWeight : -distance*definition.Profile.DistanceWeight;
                candidates.Add(new AiIntentCandidate(intent,skillId,actor.Unit.Position,target.Unit.InstanceId,target.Unit.Position,distanceScore,skill.Damage*definition.Profile.DamageWeight,targetCount*definition.Profile.TargetCountWeight,intent==AiIntentKind.Debuff?definition.Profile.HarmfulStatusWeight:0,legal,legal?string.Empty:"range_or_mana"));
            }
        }
        if (!candidates.Any(item=>item.IsLegal))
        {
            BattleUnitState? nearest=state.Units.Values.Where(unit=>unit.IsAlive&&unit.Unit.PlayerNumber!=actor.Unit.PlayerNumber).OrderBy(unit=>Manhattan(actor.Unit.Position,unit.Unit.Position)).ThenBy(unit=>unit.Unit.InstanceId.Value,StringComparer.Ordinal).FirstOrDefault();
            GridPoint destination=nearest is null?actor.Unit.Position:state.Board.GetNeighbours(actor.Unit.Position).Where(cell=>!state.Board.GetCell(cell).BlocksMovement&&!state.Units.Values.Any(unit=>unit.IsAlive&&unit.Unit.Position==cell)).OrderBy(cell=>Manhattan(cell,nearest.Unit.Position)).ThenBy(cell=>cell.X).ThenBy(cell=>cell.Y).FirstOrDefault(actor.Unit.Position);
            candidates.Add(new AiIntentCandidate(destination==actor.Unit.Position?AiIntentKind.EndTurn:AiIntentKind.Move,null,destination,null,destination,0,0,0,0,true,string.Empty));
        }
        AiIntentCandidate selected=candidates.Where(item=>item.IsLegal).OrderByDescending(item=>item.TotalScore).ThenBy(item=>item.Intent).ThenBy(item=>item.SkillId?.Value??string.Empty,StringComparer.Ordinal).ThenBy(item=>item.Destination.X).ThenBy(item=>item.Destination.Y).ThenBy(item=>item.TargetId?.Value??string.Empty,StringComparer.Ordinal).First();
        bool pattern=definition.PatternSkillIds.Count>0&&selected.SkillId==definition.PatternSkillIds[patternIndex%definition.PatternSkillIds.Count];
        return new AiTurnPlan(actor.Unit.InstanceId,selected,candidates,patternIndex,pattern);
    }

    private static int Manhattan(GridPoint a, GridPoint b)=>Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y);
}
