using Tactics.Application.Runs;
using Tactics.Application.Presentation;
using Tactics.Core.AI;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Pathfinding;
using Tactics.Core.Skills;
using Tactics.Core.Statuses;
using Tactics.Core.Units;

namespace Tactics.Application.Battle;

public enum PlayableBattlePhase { PlayerTurn, AiTurn, Victory, Defeat, Faulted }
public enum BattleTargetingMode { None, Move, Skill }

public abstract record BattleUiIntent;
public sealed record SelectUnitIntent(UnitInstanceId UnitId) : BattleUiIntent;
public sealed record BeginMoveIntent : BattleUiIntent;
public sealed record SelectSkillIntent(ContentId SkillId) : BattleUiIntent;
public sealed record ConfirmCellIntent(GridPoint Cell) : BattleUiIntent;
public sealed record CancelTargetingIntent : BattleUiIntent;
public sealed record EndTurnIntent : BattleUiIntent;

public sealed record BattleUiStatusSnapshot(ContentId StatusId, StatusEffectKind EffectKind,
    StatusPolarity Polarity, int RemainingTurns, int StackCount);
public sealed record BattleUiUnitSnapshot(
    UnitInstanceId UnitId,
    ContentId DefinitionId,
    GridPoint Cell,
    int PlayerNumber,
    bool IsAlive,
    int CurrentHealth,
    int MaxHealth,
    int CurrentMana,
    int MaxMana,
    bool HasMovedThisTurn,
    IReadOnlyList<ContentId> StatusIds,
    IReadOnlyDictionary<ContentId, int> SuccessfulSkillUses,
    IReadOnlyList<BattleUiStatusSnapshot>? Statuses = null);

public sealed record BattleUiTarget(ContentId SkillId, GridPoint Cell, UnitInstanceId? UnitId);
public sealed record BattleUiSkillPreview(
    ContentId SkillId,
    IReadOnlyList<GridPoint> RangeCells,
    IReadOnlyList<BattleUiTarget> LegalTargets);
public sealed record BattleUiSkillAvailability(ContentId SkillId, bool IsAvailable, string? FailureCode);
public sealed record BattleUiMoveAvailability(bool IsAvailable, string? FailureCode);
public sealed record BattleUiLineOfSightSnapshot(IReadOnlyList<GridPoint> RayCells, GridPoint? BlockingCell,
    LineOfSightBlockingKind? BlockingKind, UnitInstanceId? BlockingUnitId);
public sealed record BattleUiImpactPreview(
    ContentId SkillId,
    GridPoint Cell,
    bool IsInRange,
    bool IsLegal,
    string? FailureCode,
    IReadOnlyList<GridPoint> PathCells,
    GridPoint? PrimaryImpactCell,
    UnitInstanceId? PrimaryImpactUnitId,
    IReadOnlyList<GridPoint> ImpactCells,
    IReadOnlyList<UnitInstanceId> ImpactUnitIds,
    BattleUiLineOfSightSnapshot? LineOfSight = null);
public enum BattleUiLogCategory { Gameplay, Ai, Rejected }
public sealed record BattleUiLogEntry(BattleUiLogCategory Category,string Message,string EventType);
public sealed record BattleUiFrame(string Stage,BattleUiSnapshot Snapshot,AiDecisionEvent? Decision,IReadOnlyList<BattleEvent> Events,BattlePresentationFrame Presentation);

public sealed record BattleUiSnapshot(
    PlayableBattlePhase Phase,
    int Round,
    UnitInstanceId ActiveUnitId,
    BattleTargetingMode TargetingMode,
    ContentId? SelectedSkillId,
    IReadOnlyList<BattleUiUnitSnapshot> Units,
    IReadOnlyList<SkillDefinition> ActiveSkills,
    IReadOnlyList<GridPoint> LegalMoveCells,
    IReadOnlyList<BattleUiTarget> LegalTargets,
    BattleUiSkillPreview? SkillPreview,
    IReadOnlyCollection<GridPoint> Corpses,
    IReadOnlyDictionary<UnitInstanceId, GridPoint> DroppedSpears,
    IReadOnlyList<BattleEvent> RecentEvents,
    IReadOnlyList<UnitInstanceId> TurnOrder,
    int ActiveTurnIndex,
    string? FailureCode,
    IReadOnlyCollection<GridPoint>? BlockedCells = null,
    IReadOnlyList<BattleUiSkillAvailability>? SkillAvailability = null,
    BattleUiMoveAvailability MoveAvailability = null!);

public sealed record PlayableBattleSessionContext(
    BattleState InitialState,
    int PlayerNumber,
    IReadOnlyDictionary<UnitInstanceId, IReadOnlyList<SkillDefinition>> SkillsByUnit,
    IReadOnlyDictionary<UnitInstanceId, AiDefinition> AiByUnit,
    IReadOnlyDictionary<ContentId, SkillDefinition> SkillCatalog,
    EncounterRequest? EncounterRequest = null,
    IReadOnlyDictionary<UnitInstanceId, string>? CharacterIds = null,
    IReadOnlyCollection<GridPoint>? BlockedCells = null,
    IReadOnlyDictionary<ContentId, SummonControllerDefinition>? SummonControllers = null);

public sealed record SummonControllerDefinition(
    AiDefinition Ai,
    IReadOnlyDictionary<int, SkillDefinition> SkillsByLevel,
    SkillExecutionKind OwnerSummonKind);

public sealed record BattleUiIntentResult(
    bool Succeeded,
    string? FailureCode,
    BattleUiSnapshot Snapshot,
    IReadOnlyList<BattleEvent> Events,
    PureRunBattleResult? BattleResult = null,
    BattlePresentationFrame? Presentation = null);

/// <summary>
/// Coordinates player UI intents and deterministic enemy turns without duplicating battle rules.
/// </summary>
public sealed class PlayableBattleSessionService
{
    private const int MaximumAutomaticCommands = 64;
    private readonly PlayableBattleSessionContext _context;
    private readonly BattleTransitionService _transitions;
    private readonly AiDecisionService _decisions;
    private readonly AiTurnService _aiTurns;
    private readonly Dictionary<UnitInstanceId, int> _patternIndices = new();
    private readonly List<BattleEvent> _recentEvents = new();
    private readonly int _initialEnemyCount;
    private readonly Queue<(string Stage,BattleState State,AiDecisionEvent? Decision,IReadOnlyList<BattleEvent> Events)> _automaticFrames=new();
    private BattleTargetingMode _targetingMode;
    private ContentId? _selectedSkillId;
    private string? _failureCode;
    private PureRunBattleResult? _battleResult;
    private BattleUiSnapshot? _lastPresentedSnapshot;
    private static readonly ContentId SkeletonUnitId = new("unit.pure-run.skeleton-warrior");
    private static readonly ContentId FireDemonUnitId = new("unit.pure-run.fire-demon");
    private static readonly ContentId MeleeAttackId = new("skill.basic.melee");
    private static readonly ContentId MagicAttackId = new("skill.basic.magic");
    private static readonly ContentId FireDemonAttackId = new("skill.summon.fire-demon-attack");

    public PlayableBattleSessionService(
        PlayableBattleSessionContext context,
        BattleTransitionService? transitions = null,
        AiDecisionService? decisions = null,
        AiTurnService? aiTurns = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        State = context.InitialState ?? throw new ArgumentNullException(nameof(context.InitialState));
        _transitions = transitions ?? new BattleTransitionService();
        _decisions = decisions ?? new AiDecisionService(_transitions);
        _aiTurns = aiTurns ?? new AiTurnService(_transitions);
        _initialEnemyCount = State.Units.Values.Count(unit => unit.Unit.PlayerNumber != context.PlayerNumber);
        _lastPresentedSnapshot = CaptureSnapshot(State, false);
        AdvanceAutomaticTurns();
    }

    public BattleState State { get; private set; }
    public PureRunBattleResult? BattleResult => _battleResult;
    public bool HasPendingAutomaticFrames => _automaticFrames.Count>0;
    public BattleUiFrame? DequeueAutomaticFrame()
    {
        if(!_automaticFrames.TryDequeue(out var frame))return null;
        BattleUiSnapshot after=CaptureSnapshot(frame.State,false);
        BattleUiSnapshot before=_lastPresentedSnapshot??CaptureSnapshot(State,false);
        BattlePresentationFrame presentation=BattlePresentationFrameCompiler.Compile(frame.Stage,before,after,frame.Events,_context.SkillCatalog);
        _lastPresentedSnapshot=after;
        return new BattleUiFrame(frame.Stage,after,frame.Decision,frame.Events,presentation);
    }

    public BattleUiIntentResult Submit(BattleUiIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (_battleResult is not null || _failureCode is not null)
            return Result(false, _failureCode ?? "battle.already_finished", Array.Empty<BattleEvent>());
        BattleUnitState active = State.Units[State.ActiveUnitId];
        if (!active.IsAlive)
        {
            AdvanceAutomaticTurns();
            return Result(true, null, Array.Empty<BattleEvent>());
        }
        if (active.Unit.PlayerNumber != _context.PlayerNumber || ControllerFor(active) is not null || IsNonActingSummon(active))
            return Result(false, "battle.ai_turn_input_rejected", Array.Empty<BattleEvent>());

        return intent switch
        {
            SelectUnitIntent select => SelectUnit(select),
            BeginMoveIntent => SetMoveMode(),
            SelectSkillIntent select => SetSkillMode(select),
            ConfirmCellIntent confirm => ConfirmCell(confirm),
            CancelTargetingIntent => Cancel(),
            EndTurnIntent => ApplyCommand(new EndTurnCommand(active.Unit.InstanceId)),
            _ => Result(false, "battle.unsupported_intent", Array.Empty<BattleEvent>())
        };
    }

    public BattleUiSnapshot CaptureSnapshot()=>CaptureSnapshot(State,true);
    private BattleUiSnapshot CaptureSnapshot(BattleState view,bool interactive)
    {
        BattleUnitState active = view.Units[view.ActiveUnitId];
        IReadOnlyList<SkillDefinition> skills = SkillsFor(active);
        bool playerControlled = active.IsAlive && active.Unit.PlayerNumber == _context.PlayerNumber &&
            ControllerFor(active) is null && !IsNonActingSummon(active);
        GridPoint[] moves = interactive && playerControlled && !active.HasMovedThisTurn
            ? view.Board.Cells.Keys.Where(cell => _transitions.Apply(view, new MoveUnitCommand(active.Unit.InstanceId, cell)).Succeeded).OrderBy(cell => cell.X).ThenBy(cell => cell.Y).ToArray()
            : Array.Empty<GridPoint>();
        BattleUiTarget[] targets = interactive?skills.SelectMany(skill => LegalTargets(view, active, skill)).ToArray():Array.Empty<BattleUiTarget>();
        BattleUiSkillPreview? skillPreview = interactive && _targetingMode == BattleTargetingMode.Skill && _selectedSkillId is ContentId selectedSkillId
            ? CreateSkillPreview(view, active, _context.SkillCatalog[selectedSkillId], targets)
            : null;
        return new BattleUiSnapshot(
            DeterminePhase(view), view.Round, view.ActiveUnitId, interactive?_targetingMode:BattleTargetingMode.None, interactive?_selectedSkillId:null,
            view.Units.Values.OrderBy(unit => unit.Unit.SpawnOrdinal).Select(ToSnapshot).ToArray(),
            skills, moves, targets, skillPreview, view.Corpses.ToArray(), view.DroppedSpears,
            _recentEvents.TakeLast(100).ToArray(),view.TurnOrder.ToArray(),view.ActiveIndex, _failureCode,
            _context.BlockedCells ?? Array.Empty<GridPoint>(),
            skills.Select(skill => Availability(view, active, skill)).ToArray(),
            MoveAvailability(view, active));
    }

    public IReadOnlyList<GridPoint> PreviewMovePath(GridPoint destination)
    {
        BattleTransition probe=_transitions.Apply(State,new MoveUnitCommand(State.ActiveUnitId,destination));
        return probe.Succeeded?probe.Events.OfType<UnitMovedEvent>().Single().Path:Array.Empty<GridPoint>();
    }

    public BattleUiImpactPreview? PreviewSkillTarget(GridPoint cell)
    {
        if (_targetingMode != BattleTargetingMode.Skill || _selectedSkillId is not ContentId skillId)
            return null;
        BattleUnitState actor = State.Units[State.ActiveUnitId];
        SkillDefinition skill = _context.SkillCatalog[skillId];
        BattleUiSkillPreview preview = CreateSkillPreview(State, actor, skill, LegalTargets(State, actor, skill).ToArray());
        bool inRange = preview.RangeCells.Contains(cell);
        UnitInstanceId? targetId = State.Units.Values.FirstOrDefault(unit => unit.IsAlive && unit.Unit.Position == cell)?.Unit.InstanceId;
        BattleTransition probe = _transitions.Apply(State, new UseSkillCommand(actor.Unit.InstanceId, targetId, cell, skill));
        CommandRejectedEvent? rejection = probe.Events.OfType<CommandRejectedEvent>().LastOrDefault();
        bool legal = rejection is null;
        UnitInstanceId[] impactedIds = probe.Events
            .SelectMany(EventTargets)
            .Where(id => id != actor.Unit.InstanceId)
            .Distinct()
            .ToArray();
        GridPoint[] impactedCells = skill.AreaRadius > 0
            ? State.Board.Cells.Keys.Where(candidate => Math.Abs(candidate.X - cell.X) + Math.Abs(candidate.Y - cell.Y) <= skill.AreaRadius).ToArray()
            : impactedIds.Where(State.Units.ContainsKey).Select(id => State.Units[id].Unit.Position).Distinct().ToArray();
        UnitInstanceId? primaryId = probe.Events.OfType<SkillUsedEvent>().Select(evt => (UnitInstanceId?)evt.TargetId).FirstOrDefault();
        GridPoint? primaryCell = primaryId is UnitInstanceId primary && State.TryGetUnit(primary, out BattleUnitState? primaryUnit) && primaryUnit is not null
            ? primaryUnit.Unit.Position
            : null;
        LineOfSightResult? lineOfSight = skill.RequiresLineOfSight
            ? new SupercoverLineOfSight().Trace(State.Board, actor.Unit.Position, cell,
                SkillRuntimeService.LivingBlockers(State, actor.Unit.InstanceId, cell, skill.ExecutionKind))
            : null;
        GridPoint[] path = skill.UsesLineTargeting || skill.ExecutionKind == SkillExecutionKind.PoisonSpear
            ? RayCells(actor.Unit.Position, cell).ToArray()
            : Array.Empty<GridPoint>();
        BattleUiLineOfSightSnapshot? losSnapshot = lineOfSight is null ? null : new BattleUiLineOfSightSnapshot(
            lineOfSight.RayCells, lineOfSight.BlockingCell, lineOfSight.BlockingKind, lineOfSight.BlockingUnitId);
        return new BattleUiImpactPreview(skillId, cell, inRange, legal, rejection?.Reason, path, primaryCell, primaryId, impactedCells, impactedIds, losSnapshot);
    }

    private BattleUiIntentResult SelectUnit(SelectUnitIntent intent) =>
        intent.UnitId == State.ActiveUnitId
            ? Result(true, null, Array.Empty<BattleEvent>())
            : Result(false, "battle.unit_not_active", Array.Empty<BattleEvent>());

    private BattleUiIntentResult SetMoveMode()
    {
        BattleUiMoveAvailability availability = MoveAvailability(State, State.Units[State.ActiveUnitId]);
        if (!availability.IsAvailable)
            return Result(false, availability.FailureCode, Array.Empty<BattleEvent>());
        _targetingMode = BattleTargetingMode.Move;
        _selectedSkillId = null;
        return Result(true, null, Array.Empty<BattleEvent>());
    }

    private BattleUiMoveAvailability MoveAvailability(BattleState view, BattleUnitState actor)
    {
        string? failure = null;
        if (!actor.IsAlive || actor.Unit.PlayerNumber != _context.PlayerNumber || ControllerFor(actor) is not null || IsNonActingSummon(actor))
            failure = "move_not_player_controlled";
        else if (DeterminePhase(view) != PlayableBattlePhase.PlayerTurn)
            failure = "move_not_current_turn";
        else if (actor.HasMovedThisTurn)
            failure = "move_already_used";
        else if (!view.Board.Cells.Keys.Any(cell => cell != actor.Unit.Position &&
                     _transitions.Apply(view, new MoveUnitCommand(actor.Unit.InstanceId, cell)).Succeeded))
            failure = "move_no_legal_destination";
        return new BattleUiMoveAvailability(failure is null, failure);
    }

    private BattleUiIntentResult SetSkillMode(SelectSkillIntent intent)
    {
        BattleUnitState actor = State.Units[State.ActiveUnitId];
        SkillDefinition? skill = SkillsFor(actor).SingleOrDefault(skill => skill.ContentId == intent.SkillId);
        if (skill is null)
            return Result(false, "battle.skill_not_available", Array.Empty<BattleEvent>());
        BattleUiSkillAvailability availability = Availability(State, actor, skill);
        if (!availability.IsAvailable)
            return Result(false, availability.FailureCode, Array.Empty<BattleEvent>());
        _targetingMode = BattleTargetingMode.Skill;
        _selectedSkillId = intent.SkillId;
        return Result(true, null, Array.Empty<BattleEvent>());
    }

    private BattleUiIntentResult ConfirmCell(ConfirmCellIntent intent)
    {
        UnitInstanceId actorId = State.ActiveUnitId;
        if (_targetingMode == BattleTargetingMode.Move)
            return ApplyCommand(new MoveUnitCommand(actorId, intent.Cell));
        if (_targetingMode != BattleTargetingMode.Skill || _selectedSkillId is not ContentId skillId)
            return Result(false, "battle.targeting_not_active", Array.Empty<BattleEvent>());
        SkillDefinition skill = _context.SkillCatalog[skillId];
        UnitInstanceId? targetId = State.Units.Values.FirstOrDefault(unit => unit.IsAlive && unit.Unit.Position == intent.Cell)?.Unit.InstanceId;
        return ApplyCommand(new UseSkillCommand(actorId, targetId, intent.Cell, skill));
    }

    private BattleUiIntentResult Cancel()
    {
        _targetingMode = BattleTargetingMode.None;
        _selectedSkillId = null;
        return Result(true, null, Array.Empty<BattleEvent>());
    }

    private BattleUiIntentResult ApplyCommand(BattleCommand command)
    {
        BattleUiSnapshot before = CaptureSnapshot();
        BattleTransition transition = _transitions.Apply(State, command);
        if (!transition.Succeeded)
            return Result(false, (transition.Events.LastOrDefault() as CommandRejectedEvent)?.Reason ?? "battle.command_rejected", transition.Events);
        State = transition.State;
        Append(transition.Events);
        _targetingMode = BattleTargetingMode.None;
        _selectedSkillId = null;
        EvaluateTerminal();
        BattleUiSnapshot presentationAfter = CaptureSnapshot(State, false);
        _lastPresentedSnapshot = presentationAfter;
        if (_battleResult is null)
            AdvanceAutomaticTurns();
        BattleUiSnapshot after = CaptureSnapshot();
        return Result(true, null, transition.Events, BattlePresentationFrameCompiler.Compile("Player",before,presentationAfter,transition.Events,_context.SkillCatalog));
    }

    private void AdvanceAutomaticTurns()
    {
        int commandCount = 0;
        while (_battleResult is null && _failureCode is null)
        {
            EvaluateTerminal();
            if (_battleResult is not null)
                return;
            BattleUnitState active = State.Units[State.ActiveUnitId];
            SummonControllerDefinition? summonController = ControllerFor(active);
            if (active.IsAlive && active.Unit.PlayerNumber == _context.PlayerNumber && summonController is null && !IsNonActingSummon(active))
                return;
            if (++commandCount > MaximumAutomaticCommands)
            {
                _failureCode = "battle.ai_command_guard_exceeded";
                return;
            }
            if (!active.IsAlive)
            {
                BattleTransition skip = _transitions.Apply(State, new EndTurnCommand(active.Unit.InstanceId));
                State = skip.State;
                Append(skip.Events);
                continue;
            }
            if (IsNonActingSummon(active))
            {
                BattleTransition skip = _transitions.Apply(State, new EndTurnCommand(active.Unit.InstanceId));
                State = skip.State;
                Append(skip.Events);
                continue;
            }
            AiDefinition? definition = summonController is null
                ? null
                : summonController.Ai with { SkillIds = SkillsFor(active).Select(skill => skill.ContentId).ToArray() };
            if (definition is null && !_context.AiByUnit.TryGetValue(active.Unit.InstanceId, out definition))
            {
                _failureCode = "battle.ai_definition_missing";
                return;
            }
            int patternIndex = _patternIndices.GetValueOrDefault(active.Unit.InstanceId);
            AiTurnPlan plan = _decisions.Decide(State, definition, _context.SkillCatalog, patternIndex);
            AiPlanExecutionResult result = _aiTurns.Execute(State, plan, _context.SkillCatalog);
            _automaticFrames.Enqueue(("Decision",State,result.Decision,Array.Empty<BattleEvent>()));
            foreach(AiExecutionFrame frame in result.Frames??Array.Empty<AiExecutionFrame>())_automaticFrames.Enqueue((frame.Stage,frame.State,null,frame.Events));
            State = result.State;
            _patternIndices[active.Unit.InstanceId] = result.NextPatternIndex;
            Append(result.Events);
        }
    }

    private IEnumerable<BattleUiTarget> LegalTargets(BattleState view, BattleUnitState active, SkillDefinition skill)
    {
        if (!active.IsAlive || active.Unit.PlayerNumber != _context.PlayerNumber || ControllerFor(active) is not null || IsNonActingSummon(active))
            yield break;
        foreach (GridPoint cell in view.Board.Cells.Keys.OrderBy(cell => cell.X).ThenBy(cell => cell.Y))
        {
            UnitInstanceId? targetId = view.Units.Values.FirstOrDefault(unit => unit.IsAlive && unit.Unit.Position == cell)?.Unit.InstanceId;
            if (_transitions.Apply(view, new UseSkillCommand(active.Unit.InstanceId, targetId, cell, skill)).Succeeded)
                yield return new BattleUiTarget(skill.ContentId, cell, targetId);
        }
    }

    private static BattleUiSkillAvailability Availability(BattleState view, BattleUnitState actor, SkillDefinition skill)
    {
        string? failure = SkillRuntimeService.AvailabilityFailure(actor, skill);
        if (failure is null && skill.ExecutionKind == SkillExecutionKind.PoisonSpear && view.DroppedSpears.ContainsKey(actor.Unit.InstanceId))
            failure = "spear_not_held";
        if (failure is null && skill.ExecutionKind == SkillExecutionKind.PickupSpear)
        {
            if (!view.DroppedSpears.TryGetValue(actor.Unit.InstanceId, out GridPoint spear))
                failure = "spear_not_dropped";
            else if (Math.Max(Math.Abs(actor.Unit.Position.X - spear.X), Math.Abs(actor.Unit.Position.Y - spear.Y)) != 1)
                failure = "spear_not_adjacent";
        }
        if (failure is null && (skill.ExecutionKind is SkillExecutionKind.SummonSkeleton or SkillExecutionKind.SummonSkeletonMage || skill.ExecutionProfile.RequiresCorpse) && view.Corpses.Count == 0)
            failure = "corpse_not_found";
        return new BattleUiSkillAvailability(skill.ContentId, failure is null, failure);
    }

    private static BattleUiSkillPreview CreateSkillPreview(BattleState view, BattleUnitState actor, SkillDefinition skill, IEnumerable<BattleUiTarget> allTargets)
    {
        BattleUiTarget[] legal = allTargets.Where(target => target.SkillId == skill.ContentId).ToArray();
        GridPoint[] range = skill.ExecutionKind switch
        {
            SkillExecutionKind.SummonSkeleton => view.Corpses.OrderBy(cell => cell.X).ThenBy(cell => cell.Y).ToArray(),
            SkillExecutionKind.PickupSpear => view.TryGetDroppedSpear(actor.Unit.InstanceId, out GridPoint spear) ? new[] { spear } : Array.Empty<GridPoint>(),
            SkillExecutionKind.Thrust => view.Board.Cells.Keys.Where(cell => IsWithinRange(actor.Unit.Position, cell, skill) &&
                (cell.X == actor.Unit.Position.X || cell.Y == actor.Unit.Position.Y)).OrderBy(cell => cell.X).ThenBy(cell => cell.Y).ToArray(),
            _ => view.Board.Cells.Keys.Where(cell => IsWithinRange(actor.Unit.Position, cell, skill)).OrderBy(cell => cell.X).ThenBy(cell => cell.Y).ToArray()
        };
        return new BattleUiSkillPreview(skill.ContentId, range, legal);
    }

    private static bool IsWithinRange(GridPoint origin, GridPoint cell, SkillDefinition skill)
    {
        int distance = Math.Abs(origin.X - cell.X) + Math.Abs(origin.Y - cell.Y);
        return distance >= skill.MinRange && distance <= skill.MaxRange;
    }

    private static IEnumerable<GridPoint> RayCells(GridPoint origin, GridPoint target)
    {
        int dx = target.X - origin.X;
        int dy = target.Y - origin.Y;
        int steps = GreatestCommonDivisor(Math.Abs(dx), Math.Abs(dy));
        if (steps == 0) yield break;
        int stepX = dx / steps;
        int stepY = dy / steps;
        for (int index = 1; index <= steps; index++)
            yield return new GridPoint(origin.X + stepX * index, origin.Y + stepY * index);
    }

    private static int GreatestCommonDivisor(int left, int right)
    {
        while (right != 0) (left, right) = (right, left % right);
        return left;
    }

    private static IEnumerable<UnitInstanceId> EventTargets(BattleEvent evt) => evt switch
    {
        DamageAppliedEvent damage => new[] { damage.TargetId },
        StatusAppliedEvent status => new[] { status.TargetId },
        UnitSummonedEvent summon => new[] { summon.SummonId },
        _ => Array.Empty<UnitInstanceId>()
    };

    private void EvaluateTerminal()
    {
        bool playerAlive = State.Units.Values.Any(unit => unit.IsAlive && unit.Unit.PlayerNumber == _context.PlayerNumber);
        bool enemyAlive = State.Units.Values.Any(unit => unit.IsAlive && unit.Unit.PlayerNumber != _context.PlayerNumber);
        if (playerAlive && enemyAlive)
            return;
        if (_context.EncounterRequest is not EncounterRequest request)
            return;
        var characterIds = _context.CharacterIds ?? new Dictionary<UnitInstanceId, string>();
        BattlePartyResult[] party = State.Units.Values
            .Where(unit => unit.Unit.PlayerNumber == _context.PlayerNumber && characterIds.ContainsKey(unit.Unit.InstanceId))
            .OrderBy(unit => unit.Unit.SpawnOrdinal)
            .Select(unit => new BattlePartyResult(
                characterIds[unit.Unit.InstanceId], unit.CurrentHealth, unit.CurrentMana, !unit.IsAlive,
                unit.Consumables.Values.OrderBy(item => item.InstanceId.Value, StringComparer.Ordinal).ToArray()))
            .ToArray();
        int defeated = _initialEnemyCount - State.Units.Values.Count(unit => unit.IsAlive && unit.Unit.PlayerNumber != _context.PlayerNumber);
        _battleResult = new PureRunBattleResult(request.RunId, request.CheckpointRevision, request.EncounterContentId,
            playerAlive && !enemyAlive, State.Round, defeated, party);
    }

    private PlayableBattlePhase DeterminePhase()=>DeterminePhase(State);
    private PlayableBattlePhase DeterminePhase(BattleState view)
    {
        if (_failureCode is not null) return PlayableBattlePhase.Faulted;
        bool playerAlive=view.Units.Values.Any(unit=>unit.IsAlive&&unit.Unit.PlayerNumber==_context.PlayerNumber);
        bool enemyAlive=view.Units.Values.Any(unit=>unit.IsAlive&&unit.Unit.PlayerNumber!=_context.PlayerNumber);
        if(!playerAlive)return PlayableBattlePhase.Defeat;
        if(!enemyAlive)return PlayableBattlePhase.Victory;
        BattleUnitState active = view.Units[view.ActiveUnitId];
        return active.Unit.PlayerNumber == _context.PlayerNumber && ControllerFor(active) is null && !IsNonActingSummon(active)
            ? PlayableBattlePhase.PlayerTurn
            : PlayableBattlePhase.AiTurn;
    }

    private void Append(IEnumerable<BattleEvent> events) => _recentEvents.AddRange(events);
    private IReadOnlyList<SkillDefinition> SkillsFor(BattleUnitState unit)
    {
        if (_context.SkillsByUnit.TryGetValue(unit.Unit.InstanceId, out IReadOnlyList<SkillDefinition>? skills)) return skills;
        if (ControllerFor(unit) is SummonControllerDefinition controller)
        {
            int level = 1;
            if (unit.SummonOwnerId is UnitInstanceId ownerId && _context.SkillsByUnit.TryGetValue(ownerId, out IReadOnlyList<SkillDefinition>? ownerSkills))
                level = ownerSkills.Where(skill => skill.ExecutionKind == controller.OwnerSummonKind).Select(skill => skill.Level).DefaultIfEmpty(1).Max();
            if (!controller.SkillsByLevel.TryGetValue(level, out SkillDefinition? skill))
                skill = controller.SkillsByLevel.OrderBy(item => item.Key).Last().Value;
            return new[] { skill };
        }
        ContentId? summonSkill = unit.SummonOwnerId is null ? null : DynamicSummonBasicSkill(unit.Unit.DefinitionId);
        if (summonSkill is ContentId id) return new[] { _context.SkillCatalog[id] };
        return Array.Empty<SkillDefinition>();
    }

    private SummonControllerDefinition? ControllerFor(BattleUnitState unit) =>
        unit.SummonOwnerId is not null && _context.SummonControllers is not null &&
        _context.SummonControllers.TryGetValue(unit.Unit.DefinitionId, out SummonControllerDefinition? controller)
            ? controller
            : null;

    private static bool IsNonActingSummon(BattleUnitState unit) =>
        string.Equals(unit.SummonCategory, "Decoy", StringComparison.Ordinal) ||
        unit.Unit.DefinitionId == new ContentId("unit.pure-run.amazon-decoy");

    public static ContentId? DynamicSummonBasicSkill(ContentId unitDefinitionId)
    {
        if (unitDefinitionId == SkeletonUnitId) return MeleeAttackId;
        if (unitDefinitionId == FireDemonUnitId) return FireDemonAttackId;
        return null;
    }
    private BattleUiIntentResult Result(bool succeeded, string? failureCode, IReadOnlyList<BattleEvent> events, BattlePresentationFrame? presentation=null) =>
        new(succeeded, failureCode, CaptureSnapshot(), events,HasPendingAutomaticFrames?null:_battleResult,presentation);

    private static BattleUiUnitSnapshot ToSnapshot(BattleUnitState unit) => new(
        unit.Unit.InstanceId, unit.Unit.DefinitionId, unit.Unit.Position, unit.Unit.PlayerNumber,
        unit.IsAlive, unit.CurrentHealth, unit.MaxHealth, unit.CurrentMana, unit.MaxMana,
        unit.HasMovedThisTurn,
        unit.Statuses.Keys.OrderBy(id => id.Value, StringComparer.Ordinal).ToArray(),
        unit.SuccessfulSkillUses,
        unit.Statuses.Values.OrderBy(status => status.ContentId.Value, StringComparer.Ordinal)
            .Select(status => new BattleUiStatusSnapshot(status.ContentId, status.EffectKind, status.Polarity,
                status.RemainingTurns, status.StackCount)).ToArray());
}
