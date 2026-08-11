using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Core.AI;

public enum AiArchetype { Charger, Ranged, Area, Support, EliteCharger, ElitePoisonCaster }
public enum AiIntentKind { Move, Attack, Debuff, AreaAttack, Engage, FinishOff, Retreat, HoldPosition, EndTurn }

public sealed record AiProfileDefinition(float DistanceWeight, float DamageWeight, float TargetCountWeight, float HarmfulStatusWeight);

public sealed record AiDefinition(
    ContentId ContentId,
    AiArchetype Archetype,
    AiProfileDefinition Profile,
    IReadOnlyList<ContentId> SkillIds,
    IReadOnlyList<ContentId> PatternSkillIds,
    AiDecisionGraphDefinition? DecisionGraph = null,
    int MaximumEngageCandidatesPerTarget = 3,
    int PreferredMinimumRange = 1,
    int PreferredMaximumRange = 1,
    float PreferredRangeRepositionBonus = 0);

public sealed record AiDecisionGraphDefinition(
    IReadOnlyList<AiIntentDefinition> Intents,
    IReadOnlyList<AiRuleDefinition> Rules,
    IReadOnlyList<AiScoreDefinition> Scores,
    IReadOnlyList<AiDecisionEdge> Edges,
    string SourceSha256);
public sealed record AiIntentDefinition(string NodeId, string IntentType, float BasePriority, bool Enabled);
public sealed record AiRuleDefinition(string NodeId, string RuleType, float Parameter, bool Enabled);
public sealed record AiCurveKey(float Time, float Value, float InSlope, float OutSlope);
public sealed record AiScoreDefinition(string NodeId, string ScoreType, float Weight, bool Enabled, IReadOnlyList<AiCurveKey> Curve);
public sealed record AiDecisionEdge(string SourceNodeId, string TargetNodeId);

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
    string RejectionReason,
    float BasePriority = 0,
    bool MoveBeforeSkill = false)
{
    public float TotalScore => BasePriority + DistanceScore + DamageScore + TargetCountScore + StatusScore;
}

public sealed record AiTurnPlan(UnitInstanceId ActorId, AiIntentCandidate Selected, IReadOnlyList<AiIntentCandidate> Candidates, int PatternIndex, bool UsesPattern);

public sealed record AiDecisionEvent(UnitInstanceId ActorId, int CandidateCount, AiIntentKind Intent, ContentId? SkillId, GridPoint Destination, UnitInstanceId? TargetId, float Score);
public sealed record AiExecutionFrame(string Stage,Tactics.Core.Battle.BattleState State,IReadOnlyList<Tactics.Core.Battle.BattleEvent> Events);
public sealed record AiPlanExecutionResult(Tactics.Core.Battle.BattleState State, IReadOnlyList<Tactics.Core.Battle.BattleEvent> Events, AiDecisionEvent Decision, int NextPatternIndex,IReadOnlyList<AiExecutionFrame>? Frames=null);
