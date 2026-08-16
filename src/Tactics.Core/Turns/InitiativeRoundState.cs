using System.Collections.ObjectModel;
using Tactics.Core.Units;

namespace Tactics.Core.Turns;

/// <summary>
/// Immutable current-round initiative partition.
/// </summary>
/// <remarks>
/// The active and already-acted units keep their positions for the current round. Initiative
/// changes only reorder the remaining partition, matching the frozen Unity battle contract.
/// Callers provide the currently eligible units when taking the next turn; defeated, removed,
/// or engine-specific decoy units must be filtered by the adapter before entering Core.
/// </remarks>
public sealed class InitiativeRoundState
{
    private readonly IReadOnlyList<InitiativeEntry> _remaining;
    private readonly IReadOnlySet<UnitInstanceId> _acted;

    private InitiativeRoundState(
        InitiativeEntry? current,
        IEnumerable<InitiativeEntry> remaining,
        IEnumerable<UnitInstanceId> acted)
    {
        Current = current;
        _remaining = Array.AsReadOnly(remaining.ToArray());
        _acted = new ReadOnlySet<UnitInstanceId>(acted.ToHashSet());
    }

    /// <summary>
    /// Gets the unit whose action has already started, when any.
    /// </summary>
    public InitiativeEntry? Current { get; }

    /// <summary>
    /// Gets the sorted units that have not started an action in this round.
    /// </summary>
    public IReadOnlyList<InitiativeEntry> Remaining => _remaining;

    /// <summary>
    /// Gets stable IDs of units whose action started in this round, including <see cref="Current"/>.
    /// </summary>
    public IReadOnlySet<UnitInstanceId> Acted => _acted;

    /// <summary>
    /// Starts a new partition from all currently eligible units.
    /// </summary>
    public static InitiativeRoundState StartRound(IEnumerable<InitiativeEntry> eligibleUnits)
    {
        InitiativeEntry[] entries = MaterializeUnique(eligibleUnits);
        return new InitiativeRoundState(null, InitiativeOrder.Sort(entries), Array.Empty<UnitInstanceId>());
    }

    /// <summary>
    /// Synchronizes eligibility and starts the next available action.
    /// </summary>
    public InitiativeTakeNextResult TakeNext(IEnumerable<InitiativeEntry> eligibleUnits)
    {
        InitiativeEntry[] eligible = MaterializeUnique(eligibleUnits);
        InitiativeRoundState synchronized = Synchronize(eligible);
        bool startedNewRound = false;

        if (synchronized._remaining.Count == 0)
        {
            synchronized = StartRound(eligible);
            startedNewRound = true;
        }

        if (synchronized._remaining.Count == 0)
            return new InitiativeTakeNextResult(synchronized, null, startedNewRound);

        InitiativeEntry current = synchronized._remaining[0];
        var acted = synchronized._acted.ToHashSet();
        acted.Add(current.UnitId);
        var next = new InitiativeRoundState(current, synchronized._remaining.Skip(1), acted);
        return new InitiativeTakeNextResult(next, current, startedNewRound);
    }

    /// <summary>
    /// Replaces and reorders a unit only when it remains eligible to act in the current round.
    /// </summary>
    public InitiativeRoundState NotifyInitiativeChanged(InitiativeEntry changedUnit)
    {
        int index = _remaining
            .Select((entry, candidateIndex) => (entry, candidateIndex))
            .Where(candidate => candidate.entry.UnitId == changedUnit.UnitId)
            .Select(candidate => candidate.candidateIndex)
            .DefaultIfEmpty(-1)
            .First();
        if (index < 0)
            return this;

        InitiativeEntry[] remaining = _remaining.ToArray();
        remaining[index] = changedUnit;
        return new InitiativeRoundState(Current, InitiativeOrder.Sort(remaining), _acted);
    }

    /// <summary>
    /// Returns the visible current-round order, optionally including the active unit.
    /// </summary>
    public IReadOnlyList<InitiativeEntry> GetCurrentRoundOrder(bool includeCurrent = true)
    {
        if (!includeCurrent || Current is null)
            return _remaining;

        return Array.AsReadOnly(new[] { Current.Value }.Concat(_remaining).ToArray());
    }

    /// <summary>
    /// Clears all current-round ownership.
    /// </summary>
    public InitiativeRoundState Reset() =>
        new(null, Array.Empty<InitiativeEntry>(), Array.Empty<UnitInstanceId>());

    private InitiativeRoundState Synchronize(IReadOnlyList<InitiativeEntry> eligibleUnits)
    {
        IReadOnlyDictionary<UnitInstanceId, InitiativeEntry> eligible = eligibleUnits.ToDictionary(entry => entry.UnitId);
        InitiativeEntry? current = Current is { } currentEntry && eligible.TryGetValue(currentEntry.UnitId, out InitiativeEntry refreshedCurrent)
            ? refreshedCurrent
            : null;
        var acted = _acted.Where(eligible.ContainsKey).ToHashSet();
        var remaining = _remaining
            .Where(entry => eligible.ContainsKey(entry.UnitId))
            .Select(entry => eligible[entry.UnitId])
            .ToList();
        var remainingIds = remaining.Select(entry => entry.UnitId).ToHashSet();

        foreach (InitiativeEntry entry in eligibleUnits)
        {
            if (current?.UnitId == entry.UnitId || acted.Contains(entry.UnitId) || remainingIds.Contains(entry.UnitId))
                continue;

            remaining.Add(entry);
            remainingIds.Add(entry.UnitId);
        }

        return new InitiativeRoundState(current, InitiativeOrder.Sort(remaining), acted);
    }

    private static InitiativeEntry[] MaterializeUnique(IEnumerable<InitiativeEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        InitiativeEntry[] materialized = entries.ToArray();
        if (materialized.Select(entry => entry.UnitId).Distinct().Count() != materialized.Length)
            throw new ArgumentException("Initiative participants must have unique unit IDs.", nameof(entries));
        return materialized;
    }
}

/// <summary>
/// Result of selecting the next current-round unit.
/// </summary>
public sealed record InitiativeTakeNextResult(
    InitiativeRoundState State,
    InitiativeEntry? Current,
    bool StartedNewRound);
