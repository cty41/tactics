using Tactics.Core.Battle;
using Tactics.Core.Content;
using Tactics.Core.Skills;

namespace Tactics.Core.AI;

/// <summary>Executes at most one move, one canonical skill, and EndTurn with revalidation between steps.</summary>
public sealed class AiTurnService
{
    private readonly BattleTransitionService _transitions;
    public AiTurnService(BattleTransitionService? transitions=null)=>_transitions=transitions??new BattleTransitionService();

    public AiPlanExecutionResult Execute(BattleState state, AiTurnPlan plan, IReadOnlyDictionary<Tactics.Core.Content.ContentId,SkillDefinition> skills)
    {
        if(state.ActiveUnitId!=plan.ActorId) throw new InvalidOperationException("AI plan actor is not active.");
        var events=new List<BattleEvent>();var frames=new List<AiExecutionFrame>(); BattleState next=state; AiIntentCandidate selected=plan.Selected; bool selectedSucceeded=true;
        if(selected.Destination!=next.Units[plan.ActorId].Unit.Position){ BattleTransition move=_transitions.Apply(next,new MoveUnitCommand(plan.ActorId,selected.Destination)); selectedSucceeded=move.Succeeded; next=move.State; events.AddRange(move.Events);frames.Add(new AiExecutionFrame("Move",next,move.Events)); }
        if(selectedSucceeded && selected.SkillId is { } skillId){ BattleTransition skill=_transitions.Apply(next,new UseSkillCommand(plan.ActorId,selected.TargetId,selected.TargetCell,skills[skillId])); selectedSucceeded=skill.Succeeded; next=skill.State; events.AddRange(skill.Events);frames.Add(new AiExecutionFrame("Skill",next,skill.Events)); }
        if(next.ActiveUnitId==plan.ActorId){ BattleTransition end=_transitions.Apply(next,new EndTurnCommand(plan.ActorId)); next=end.State; events.AddRange(end.Events);frames.Add(new AiExecutionFrame("EndTurn",next,end.Events)); }
        int nextPatternIndex=plan.UsesPattern&&selectedSucceeded?plan.PatternIndex+1:plan.PatternIndex;
        ContentId? targetDefinitionId=selected.TargetId is { } targetId&&state.TryGetUnit(targetId,out BattleUnitState? target)?target?.Unit.DefinitionId:null;
        return new AiPlanExecutionResult(next,events,new AiDecisionEvent(plan.ActorId,plan.Candidates.Count,selected.Intent,selected.SkillId,selected.Destination,selected.TargetId,selected.TotalScore,targetDefinitionId,selected.DistanceScore,selected.DamageScore,selected.TargetCountScore,selected.StatusScore),nextPatternIndex,frames);
    }
}
