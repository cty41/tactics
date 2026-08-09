using System.Collections.ObjectModel;
using Tactics.Core.Board;
using Tactics.Core.Content;

namespace Tactics.Core.Battle;

/// <summary>
/// Stores immutable runtime state for one battle unit.
/// </summary>
/// <remarks>
/// Content definitions remain outside this type. Health, movement use, and status duration are per-battle
/// values and every mutation returns a new instance so AI evaluation and replay snapshots cannot alias state.
/// </remarks>
public sealed class BattleUnitState
{
    private readonly IReadOnlyDictionary<ContentId, BattleStatusState> _statuses;
    private readonly IReadOnlyDictionary<ContentId, int> _statusDurations;

    /// <summary>
    /// Creates runtime state for a unit.
    /// </summary>
    /// <param name="unit">Immutable unit facts and current board position.</param>
    /// <param name="maxHealth">Maximum health. Must be positive.</param>
    /// <param name="currentHealth">Current health in the inclusive range from zero to max health.</param>
    /// <param name="hasMovedThisTurn">Whether the one movement use has been consumed this turn.</param>
    /// <param name="statusDurations">Optional status ID to remaining-turn mapping.</param>
    public BattleUnitState(
        UnitState unit,
        int maxHealth,
        int currentHealth,
        bool hasMovedThisTurn = false,
        IReadOnlyDictionary<ContentId, int>? statusDurations = null,
        int maxMana = 0,
        int currentMana = 0,
        IReadOnlyDictionary<ContentId, BattleStatusState>? statuses = null)
    {
        if (maxHealth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxHealth));
        if (currentHealth < 0 || currentHealth > maxHealth)
            throw new ArgumentOutOfRangeException(nameof(currentHealth));
        if (maxMana < 0)
            throw new ArgumentOutOfRangeException(nameof(maxMana));
        if (currentMana < 0 || currentMana > maxMana)
            throw new ArgumentOutOfRangeException(nameof(currentMana));
        if (statusDurations is not null && statusDurations.Count > 0 && statuses is not null && statuses.Count > 0)
            throw new ArgumentException("Provide either legacy status durations or detailed statuses, not both.");

        Unit = unit with { IsAlive = currentHealth > 0 };
        MaxHealth = maxHealth;
        CurrentHealth = currentHealth;
        HasMovedThisTurn = hasMovedThisTurn;
        MaxMana = maxMana;
        CurrentMana = currentMana;

        var detailedStatuses = new Dictionary<ContentId, BattleStatusState>();
        foreach ((ContentId statusId, int remainingTurns) in statusDurations ?? new Dictionary<ContentId, int>())
        {
            if (remainingTurns <= 0)
                throw new ArgumentOutOfRangeException(nameof(statusDurations), "Status durations must be positive.");
            detailedStatuses.Add(statusId, new BattleStatusState(statusId, Unit.InstanceId, remainingTurns, 0));
        }
        foreach ((ContentId statusId, BattleStatusState status) in statuses ??
                 new Dictionary<ContentId, BattleStatusState>())
        {
            if (statusId != status.ContentId)
                throw new ArgumentException("Detailed status key must match its ContentId.", nameof(statuses));
            detailedStatuses.Add(statusId, status);
        }

        _statuses = new ReadOnlyDictionary<ContentId, BattleStatusState>(detailedStatuses);
        _statusDurations = new ReadOnlyDictionary<ContentId, int>(
            detailedStatuses.ToDictionary(item => item.Key, item => item.Value.RemainingTurns));
    }

    /// <summary>
    /// Gets immutable unit facts including the current board position.
    /// </summary>
    public UnitState Unit { get; }

    /// <summary>
    /// Gets the unit's maximum health.
    /// </summary>
    public int MaxHealth { get; }

    /// <summary>
    /// Gets current health. Zero means the unit is defeated.
    /// </summary>
    public int CurrentHealth { get; }

    /// <summary>
    /// Gets the unit's maximum mana captured in this battle.
    /// </summary>
    public int MaxMana { get; }

    /// <summary>
    /// Gets current mana available for ability costs.
    /// </summary>
    public int CurrentMana { get; }

    /// <summary>
    /// Gets whether the unit has consumed its one movement use this turn.
    /// </summary>
    public bool HasMovedThisTurn { get; }

    /// <summary>
    /// Gets immutable status durations keyed by stable content ID.
    /// </summary>
    public IReadOnlyDictionary<ContentId, int> StatusDurations => _statusDurations;

    /// <summary>
    /// Gets detailed immutable status instances keyed by stable content ID.
    /// </summary>
    public IReadOnlyDictionary<ContentId, BattleStatusState> Statuses => _statuses;

    /// <summary>
    /// Gets whether this runtime unit can still participate in battle.
    /// </summary>
    public bool IsAlive => CurrentHealth > 0 && Unit.IsAlive;

    /// <summary>
    /// Returns a copy at a new cell and optionally updates movement-use state.
    /// </summary>
    /// <param name="position">New local board position.</param>
    /// <param name="hasMovedThisTurn">New movement-use state.</param>
    /// <returns>The updated immutable unit state.</returns>
    public BattleUnitState WithPosition(GridPoint position, bool hasMovedThisTurn) =>
        new(
            Unit with { Position = position },
            MaxHealth,
            CurrentHealth,
            hasMovedThisTurn,
            maxMana: MaxMana,
            currentMana: CurrentMana,
            statuses: _statuses);

    /// <summary>
    /// Returns a copy with clamped health and matching alive state.
    /// </summary>
    /// <param name="currentHealth">New health before clamping to the valid range.</param>
    /// <returns>The updated immutable unit state.</returns>
    public BattleUnitState WithHealth(int currentHealth) =>
        new(
            Unit,
            MaxHealth,
            Math.Clamp(currentHealth, 0, MaxHealth),
            HasMovedThisTurn,
            maxMana: MaxMana,
            currentMana: CurrentMana,
            statuses: _statuses);

    /// <summary>
    /// Returns a copy with clamped mana.
    /// </summary>
    public BattleUnitState WithMana(int currentMana) =>
        new(
            Unit,
            MaxHealth,
            CurrentHealth,
            HasMovedThisTurn,
            maxMana: MaxMana,
            currentMana: Math.Clamp(currentMana, 0, MaxMana),
            statuses: _statuses);

    /// <summary>
    /// Returns a copy with the given status duration.
    /// </summary>
    /// <param name="statusId">Stable status content ID.</param>
    /// <param name="remainingTurns">Positive remaining duration.</param>
    /// <returns>The updated immutable unit state.</returns>
    public BattleUnitState WithStatus(ContentId statusId, int remainingTurns)
    {
        if (remainingTurns <= 0)
            throw new ArgumentOutOfRangeException(nameof(remainingTurns));

        BattleStatusState status = _statuses.TryGetValue(statusId, out BattleStatusState? current)
            ? new BattleStatusState(statusId, current.SourceId, remainingTurns, current.DamagePerTurn)
            : new BattleStatusState(statusId, Unit.InstanceId, remainingTurns, 0);
        return WithStatus(status);
    }

    /// <summary>
    /// Returns a copy with a complete status instance added or replaced.
    /// </summary>
    public BattleUnitState WithStatus(BattleStatusState status)
    {
        ArgumentNullException.ThrowIfNull(status);
        var statuses = new Dictionary<ContentId, BattleStatusState>(_statuses)
        {
            [status.ContentId] = status
        };
        return new BattleUnitState(
            Unit,
            MaxHealth,
            CurrentHealth,
            HasMovedThisTurn,
            maxMana: MaxMana,
            currentMana: CurrentMana,
            statuses: statuses);
    }

    /// <summary>
    /// Returns a copy without the selected status.
    /// </summary>
    public BattleUnitState WithoutStatus(ContentId statusId)
    {
        var statuses = new Dictionary<ContentId, BattleStatusState>(_statuses);
        statuses.Remove(statusId);
        return new BattleUnitState(
            Unit,
            MaxHealth,
            CurrentHealth,
            HasMovedThisTurn,
            maxMana: MaxMana,
            currentMana: CurrentMana,
            statuses: statuses);
    }

    /// <summary>
    /// Returns a copy whose one-per-turn movement use is available again.
    /// </summary>
    /// <returns>The updated immutable unit state.</returns>
    public BattleUnitState PrepareForTurn() =>
        new(
            Unit,
            MaxHealth,
            CurrentHealth,
            hasMovedThisTurn: false,
            maxMana: MaxMana,
            currentMana: CurrentMana,
            statuses: _statuses);
}
