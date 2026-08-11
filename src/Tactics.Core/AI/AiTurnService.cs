using Tactics.Core.Battle;
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
        var events=new List<BattleEvent>(); BattleState next=state; AiIntentCandidate selected=plan.Selected;
        if(selected.Intent==AiIntentKind.Move){ BattleTransition move=_transitions.Apply(next,new MoveUnitCommand(plan.ActorId,selected.Destination)); next=move.State; events.AddRange(move.Events); }
        else if(selected.SkillId is { } skillId){ BattleTransition skill=_transitions.Apply(next,new UseSkillCommand(plan.ActorId,selected.TargetId,selected.TargetCell,skills[skillId])); next=skill.State; events.AddRange(skill.Events); }
        if(next.ActiveUnitId==plan.ActorId){ BattleTransition end=_transitions.Apply(next,new EndTurnCommand(plan.ActorId)); next=end.State; events.AddRange(end.Events); }
        return new AiPlanExecutionResult(next,events,new AiDecisionEvent(plan.ActorId,plan.Candidates.Count,selected.Intent,selected.SkillId,selected.TotalScore));
    }
}
