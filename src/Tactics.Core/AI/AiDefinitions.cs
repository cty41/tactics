using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Core.AI;

public enum AiArchetype { Charger, Ranged, Area, Support, EliteCharger, ElitePoisonCaster }
public enum AiIntentKind { Move, Attack, Debuff, AreaAttack, EndTurn }

public sealed record AiProfileDefinition(float DistanceWeight, float DamageWeight, float TargetCountWeight, float HarmfulStatusWeight);

public sealed record AiDefinition(
    ContentId ContentId,
    AiArchetype Archetype,
    AiProfileDefinition Profile,
    IReadOnlyList<ContentId> SkillIds,
    IReadOnlyList<ContentId> PatternSkillIds);

public sealed record AiIntentCandidate(
    AiIntentKind Intent,
    ContentId? SkillId,
    GridPoint Destination,
    UnitInstanceId? TargetId,
    GridPoint TargetCell,
    float DistanceScore,
    float DamageScore,
    float TargetCountScore,
    float StatusScore,
    bool IsLegal,
    string RejectionReason)
{
    public float TotalScore => DistanceScore + DamageScore + TargetCountScore + StatusScore;
}

public sealed record AiTurnPlan(UnitInstanceId ActorId, AiIntentCandidate Selected, IReadOnlyList<AiIntentCandidate> Candidates, int PatternIndex, bool UsesPattern);

public sealed record AiDecisionEvent(UnitInstanceId ActorId, int CandidateCount, AiIntentKind Intent, ContentId? SkillId, float Score);
public sealed record AiPlanExecutionResult(Tactics.Core.Battle.BattleState State, IReadOnlyList<Tactics.Core.Battle.BattleEvent> Events, AiDecisionEvent Decision, int NextPatternIndex);
