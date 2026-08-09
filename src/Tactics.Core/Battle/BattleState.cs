using System.Collections.ObjectModel;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Randomness;
using Tactics.Core.Turns;
using Tactics.Core.Units;

namespace Tactics.Core.Battle;

/// <summary>
/// Represents an immutable battle snapshot consumed by commands and AI evaluation.
/// </summary>
/// <remarks>
/// The snapshot owns defensive copies of units and turn order. Random state is explicit so a state plus
/// command sequence is sufficient to replay gameplay without Unity, Godot, or process-global state.
/// </remarks>
public sealed class BattleState
{
    private readonly IReadOnlyDictionary<UnitInstanceId, BattleUnitState> _units;
    private readonly IReadOnlyList<UnitInstanceId> _turnOrder;
    private readonly IReadOnlyDictionary<UnitInstanceId, GridPoint> _droppedSpears;

    /// <summary>
    /// Creates a battle snapshot.
    /// </summary>
    /// <param name="board">Immutable 10x10 board facts.</param>
    /// <param name="units">Runtime unit states with unique IDs.</param>
    /// <param name="turnOrder">Complete deterministic turn order.</param>
    /// <param name="round">One-based round number.</param>
    /// <param name="activeIndex">Index of the active unit in turn order.</param>
    /// <param name="randomState">Serializable state used for the next deterministic random draw.</param>
    public BattleState(
        BoardSnapshot board,
        IEnumerable<BattleUnitState> units,
        IReadOnlyList<UnitInstanceId> turnOrder,
        int round = 1,
        int activeIndex = 0,
        ulong randomState = 0,
        IReadOnlyDictionary<UnitInstanceId, GridPoint>? droppedSpears = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(turnOrder);
        if (round < 1)
            throw new ArgumentOutOfRangeException(nameof(round));
        if (turnOrder.Count == 0)
            throw new ArgumentException("Turn order must contain at least one unit.", nameof(turnOrder));
        if (activeIndex < 0 || activeIndex >= turnOrder.Count)
            throw new ArgumentOutOfRangeException(nameof(activeIndex));

        var unitMap = new Dictionary<UnitInstanceId, BattleUnitState>();
        foreach (BattleUnitState unit in units)
        {
            if (!unitMap.TryAdd(unit.Unit.InstanceId, unit))
                throw new ArgumentException($"Duplicate battle unit ID: {unit.Unit.InstanceId}.", nameof(units));
            if (!board.Contains(unit.Unit.Position))
                throw new ArgumentException($"Unit {unit.Unit.InstanceId} is outside the board.", nameof(units));
        }

        UnitInstanceId[] order = turnOrder.ToArray();
        if (order.Distinct().Count() != order.Length)
            throw new ArgumentException("Turn order cannot contain duplicate unit IDs.", nameof(turnOrder));
        if (order.Any(unitId => !unitMap.ContainsKey(unitId)))
            throw new ArgumentException("Turn order references a unit that is not present in battle state.", nameof(turnOrder));

        Board = board;
        _units = new ReadOnlyDictionary<UnitInstanceId, BattleUnitState>(unitMap);
        _turnOrder = Array.AsReadOnly(order);
        Round = round;
        ActiveIndex = activeIndex;
        RandomState = randomState;
        var spearMap = new Dictionary<UnitInstanceId, GridPoint>();
        foreach ((UnitInstanceId ownerId, GridPoint cell) in droppedSpears ??
                 new Dictionary<UnitInstanceId, GridPoint>())
        {
            if (!unitMap.ContainsKey(ownerId))
                throw new ArgumentException($"Dropped spear owner '{ownerId}' is missing.", nameof(droppedSpears));
            if (!board.Contains(cell))
                throw new ArgumentException($"Dropped spear for '{ownerId}' is outside the board.", nameof(droppedSpears));
            if (spearMap.Values.Contains(cell))
                throw new ArgumentException($"Multiple dropped spears occupy '{cell}'.", nameof(droppedSpears));
            spearMap.Add(ownerId, cell);
        }
        _droppedSpears = new ReadOnlyDictionary<UnitInstanceId, GridPoint>(spearMap);
    }

    /// <summary>
    /// Gets immutable board facts without runtime occupancy overlay.
    /// </summary>
    public BoardSnapshot Board { get; }

    /// <summary>
    /// Gets runtime unit states by stable unit ID.
    /// </summary>
    public IReadOnlyDictionary<UnitInstanceId, BattleUnitState> Units => _units;

    /// <summary>
    /// Gets deterministic turn order.
    /// </summary>
    public IReadOnlyList<UnitInstanceId> TurnOrder => _turnOrder;

    /// <summary>
    /// Gets the one-based round number.
    /// </summary>
    public int Round { get; }

    /// <summary>
    /// Gets the current turn-order index.
    /// </summary>
    public int ActiveIndex { get; }

    /// <summary>
    /// Gets the unit allowed to issue the next command.
    /// </summary>
    public UnitInstanceId ActiveUnitId => _turnOrder[ActiveIndex];

    /// <summary>
    /// Gets serializable state for the next deterministic random draw.
    /// </summary>
    public ulong RandomState { get; }

    /// <summary>
    /// Gets Amazon spear locations keyed by their owning unit. Missing means the owner is holding the spear.
    /// </summary>
    public IReadOnlyDictionary<UnitInstanceId, GridPoint> DroppedSpears => _droppedSpears;

    /// <summary>
    /// Gets the versioned random algorithm associated with <see cref="RandomState"/>.
    /// </summary>
    public string RandomAlgorithmId => DeterministicRandom.AlgorithmId;

    /// <summary>
    /// Looks up runtime state by unit ID.
    /// </summary>
    /// <param name="unitId">Stable unit ID.</param>
    /// <param name="unit">Resolved unit when present.</param>
    /// <returns>True when the unit exists.</returns>
    public bool TryGetUnit(UnitInstanceId unitId, out BattleUnitState? unit) => _units.TryGetValue(unitId, out unit);

    /// <summary>
    /// Returns a snapshot with one unit replaced.
    /// </summary>
    /// <param name="unit">Updated unit with an existing ID.</param>
    /// <returns>The updated battle snapshot.</returns>
    public BattleState WithUnit(BattleUnitState unit)
    {
        if (!_units.ContainsKey(unit.Unit.InstanceId))
            throw new ArgumentException("Cannot add a new unit through WithUnit.", nameof(unit));

        var units = new Dictionary<UnitInstanceId, BattleUnitState>(_units)
        {
            [unit.Unit.InstanceId] = unit
        };
        return new BattleState(Board, units.Values, _turnOrder, Round, ActiveIndex, RandomState, _droppedSpears);
    }

    /// <summary>
    /// Replaces one unit after an initiative change and reorders only units that have not acted this round.
    /// </summary>
    /// <param name="unit">Updated unit state containing the new initiative.</param>
    /// <returns>A snapshot whose current and already-acted prefix remains stable.</returns>
    public BattleState WithInitiativeChanged(BattleUnitState unit)
    {
        BattleState updated = WithUnit(unit);
        int changedIndex = Array.IndexOf(_turnOrder.ToArray(), unit.Unit.InstanceId);
        if (changedIndex <= ActiveIndex)
            return updated;

        UnitInstanceId[] stablePrefix = _turnOrder.Take(ActiveIndex + 1).ToArray();
        InitiativeRoundState pending = InitiativeRoundState.StartRound(
            _turnOrder
                .Skip(ActiveIndex + 1)
                .Select(unitId => updated._units[unitId].Unit)
                .Select(value => new InitiativeEntry(
                    value.InstanceId,
                    value.Initiative,
                    value.PlayerNumber,
                    value.SpawnOrdinal)));
        UnitInstanceId[] reordered = stablePrefix
            .Concat(pending.Remaining.Select(entry => entry.UnitId))
            .ToArray();
        return new BattleState(Board, updated._units.Values, reordered, Round, ActiveIndex, RandomState, _droppedSpears);
    }

    /// <summary>
    /// Advances turn order and resets movement use for the incoming active unit.
    /// </summary>
    /// <returns>The advanced battle snapshot.</returns>
    public BattleState AdvanceTurn()
    {
        int nextIndex = ActiveIndex + 1;
        int nextRound = Round;
        if (nextIndex >= _turnOrder.Count)
        {
            nextIndex = 0;
            nextRound++;
        }

        var units = new Dictionary<UnitInstanceId, BattleUnitState>(_units);
        UnitInstanceId incomingUnitId = _turnOrder[nextIndex];
        units[incomingUnitId] = units[incomingUnitId].PrepareForTurn();
        return new BattleState(Board, units.Values, _turnOrder, nextRound, nextIndex, RandomState, _droppedSpears);
    }

    /// <summary>
    /// Returns whether the owner currently has a dropped spear and its cell.
    /// </summary>
    public bool TryGetDroppedSpear(UnitInstanceId ownerId, out GridPoint cell) =>
        _droppedSpears.TryGetValue(ownerId, out cell);

    /// <summary>
    /// Returns a snapshot with a newly dropped spear. An owner cannot drop a second spear.
    /// </summary>
    public BattleState WithDroppedSpear(UnitInstanceId ownerId, GridPoint cell)
    {
        if (!_units.ContainsKey(ownerId))
            throw new ArgumentException("Dropped spear owner is not in battle.", nameof(ownerId));
        if (_droppedSpears.ContainsKey(ownerId))
            throw new InvalidOperationException($"Unit '{ownerId}' already has a dropped spear.");
        if (!Board.Contains(cell) || Board.GetCell(cell).BlocksMovement ||
            _units.Values.Any(unit => unit.IsAlive && unit.Unit.Position == cell) ||
            _droppedSpears.Values.Contains(cell))
        {
            throw new InvalidOperationException($"Cell '{cell}' cannot receive a dropped spear.");
        }

        var spears = new Dictionary<UnitInstanceId, GridPoint>(_droppedSpears)
        {
            [ownerId] = cell
        };
        return new BattleState(Board, _units.Values, _turnOrder, Round, ActiveIndex, RandomState, spears);
    }

    /// <summary>
    /// Creates a board whose occupancy is derived from all other live units.
    /// </summary>
    /// <param name="movingUnitId">Unit whose origin must remain walkable during pathfinding.</param>
    /// <returns>A board occupancy snapshot for movement evaluation.</returns>
    public BoardSnapshot CreateMovementBoard(UnitInstanceId movingUnitId) => Board.WithOccupancy(
        _units.Values
            .Where(unit => unit.IsAlive && unit.Unit.InstanceId != movingUnitId)
            .Select(unit => unit.Unit.Position)
            .Concat(_droppedSpears.Values));
}
