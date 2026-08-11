using Tactics.Application.Runs;
using Tactics.Core.AI;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Skills;
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
    IReadOnlyList<ContentId> StatusIds);

public sealed record BattleUiTarget(ContentId SkillId, GridPoint Cell, UnitInstanceId? UnitId);

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
    IReadOnlyCollection<GridPoint> Corpses,
    IReadOnlyDictionary<UnitInstanceId, GridPoint> DroppedSpears,
    IReadOnlyList<BattleEvent> RecentEvents,
    string? FailureCode);

public sealed record PlayableBattleSessionContext(
    BattleState InitialState,
    int PlayerNumber,
    IReadOnlyDictionary<UnitInstanceId, IReadOnlyList<SkillDefinition>> SkillsByUnit,
    IReadOnlyDictionary<UnitInstanceId, AiDefinition> AiByUnit,
    IReadOnlyDictionary<ContentId, SkillDefinition> SkillCatalog,
    EncounterRequest? EncounterRequest = null,
    IReadOnlyDictionary<UnitInstanceId, string>? CharacterIds = null);

public sealed record BattleUiIntentResult(
    bool Succeeded,
    string? FailureCode,
    BattleUiSnapshot Snapshot,
    IReadOnlyList<BattleEvent> Events,
    PureRunBattleResult? BattleResult = null);

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
    private BattleTargetingMode _targetingMode;
    private ContentId? _selectedSkillId;
    private string? _failureCode;
    private PureRunBattleResult? _battleResult;

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
        AdvanceAutomaticTurns();
    }

    public BattleState State { get; private set; }
    public PureRunBattleResult? BattleResult => _battleResult;

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
        if (active.Unit.PlayerNumber != _context.PlayerNumber)
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

    public BattleUiSnapshot CaptureSnapshot()
    {
        BattleUnitState active = State.Units[State.ActiveUnitId];
        IReadOnlyList<SkillDefinition> skills = _context.SkillsByUnit.TryGetValue(active.Unit.InstanceId, out IReadOnlyList<SkillDefinition>? values)
            ? values.OrderBy(skill => skill.ContentId.Value, StringComparer.Ordinal).ToArray()
            : Array.Empty<SkillDefinition>();
        GridPoint[] moves = active.IsAlive && active.Unit.PlayerNumber == _context.PlayerNumber && !active.HasMovedThisTurn
            ? State.Board.Cells.Keys.Where(cell => _transitions.Apply(State, new MoveUnitCommand(active.Unit.InstanceId, cell)).Succeeded).OrderBy(cell => cell.X).ThenBy(cell => cell.Y).ToArray()
            : Array.Empty<GridPoint>();
        BattleUiTarget[] targets = skills.SelectMany(skill => LegalTargets(active, skill)).ToArray();
        return new BattleUiSnapshot(
            DeterminePhase(), State.Round, State.ActiveUnitId, _targetingMode, _selectedSkillId,
            State.Units.Values.OrderBy(unit => unit.Unit.SpawnOrdinal).Select(ToSnapshot).ToArray(),
            skills, moves, targets, State.Corpses.ToArray(), State.DroppedSpears,
            _recentEvents.TakeLast(64).ToArray(), _failureCode);
    }

    private BattleUiIntentResult SelectUnit(SelectUnitIntent intent) =>
        intent.UnitId == State.ActiveUnitId
            ? Result(true, null, Array.Empty<BattleEvent>())
            : Result(false, "battle.unit_not_active", Array.Empty<BattleEvent>());

    private BattleUiIntentResult SetMoveMode()
    {
        _targetingMode = BattleTargetingMode.Move;
        _selectedSkillId = null;
        return Result(true, null, Array.Empty<BattleEvent>());
    }

    private BattleUiIntentResult SetSkillMode(SelectSkillIntent intent)
    {
        if (!_context.SkillsByUnit.TryGetValue(State.ActiveUnitId, out IReadOnlyList<SkillDefinition>? skills) ||
            skills.All(skill => skill.ContentId != intent.SkillId))
            return Result(false, "battle.skill_not_available", Array.Empty<BattleEvent>());
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
        BattleTransition transition = _transitions.Apply(State, command);
        if (!transition.Succeeded)
            return Result(false, (transition.Events.LastOrDefault() as CommandRejectedEvent)?.Reason ?? "battle.command_rejected", transition.Events);
        State = transition.State;
        Append(transition.Events);
        _targetingMode = BattleTargetingMode.None;
        _selectedSkillId = null;
        EvaluateTerminal();
        if (_battleResult is null)
            AdvanceAutomaticTurns();
        return Result(true, null, transition.Events);
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
            if (active.IsAlive && active.Unit.PlayerNumber == _context.PlayerNumber)
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
            if (!_context.AiByUnit.TryGetValue(active.Unit.InstanceId, out AiDefinition? definition))
            {
                _failureCode = "battle.ai_definition_missing";
                return;
            }
            int patternIndex = _patternIndices.GetValueOrDefault(active.Unit.InstanceId);
            AiTurnPlan plan = _decisions.Decide(State, definition, _context.SkillCatalog, patternIndex);
            AiPlanExecutionResult result = _aiTurns.Execute(State, plan, _context.SkillCatalog);
            State = result.State;
            _patternIndices[active.Unit.InstanceId] = result.NextPatternIndex;
            Append(result.Events);
        }
    }

    private IEnumerable<BattleUiTarget> LegalTargets(BattleUnitState active, SkillDefinition skill)
    {
        if (!active.IsAlive || active.Unit.PlayerNumber != _context.PlayerNumber)
            yield break;
        foreach (GridPoint cell in State.Board.Cells.Keys.OrderBy(cell => cell.X).ThenBy(cell => cell.Y))
        {
            UnitInstanceId? targetId = State.Units.Values.FirstOrDefault(unit => unit.IsAlive && unit.Unit.Position == cell)?.Unit.InstanceId;
            if (_transitions.Apply(State, new UseSkillCommand(active.Unit.InstanceId, targetId, cell, skill)).Succeeded)
                yield return new BattleUiTarget(skill.ContentId, cell, targetId);
        }
    }

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

    private PlayableBattlePhase DeterminePhase()
    {
        if (_failureCode is not null) return PlayableBattlePhase.Faulted;
        if (_battleResult is not null) return _battleResult.PlayerVictory ? PlayableBattlePhase.Victory : PlayableBattlePhase.Defeat;
        return State.Units[State.ActiveUnitId].Unit.PlayerNumber == _context.PlayerNumber ? PlayableBattlePhase.PlayerTurn : PlayableBattlePhase.AiTurn;
    }

    private void Append(IEnumerable<BattleEvent> events) => _recentEvents.AddRange(events);
    private BattleUiIntentResult Result(bool succeeded, string? failureCode, IReadOnlyList<BattleEvent> events) =>
        new(succeeded, failureCode, CaptureSnapshot(), events, _battleResult);

    private static BattleUiUnitSnapshot ToSnapshot(BattleUnitState unit) => new(
        unit.Unit.InstanceId, unit.Unit.DefinitionId, unit.Unit.Position, unit.Unit.PlayerNumber,
        unit.IsAlive, unit.CurrentHealth, unit.MaxHealth, unit.CurrentMana, unit.MaxMana,
        unit.Statuses.Keys.OrderBy(id => id.Value, StringComparer.Ordinal).ToArray());
}
