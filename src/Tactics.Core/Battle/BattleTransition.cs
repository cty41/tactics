namespace Tactics.Core.Battle;

/// <summary>
/// Contains the immutable state and ordered events produced by one command.
/// </summary>
/// <remarks>
/// A rejected command returns the original state and exactly one <see cref="CommandRejectedEvent"/>.
/// Successful gameplay state is complete before presentation consumes any event.
/// </remarks>
public sealed class BattleTransition
{
    /// <summary>
    /// Creates a transition result.
    /// </summary>
    /// <param name="state">Resulting battle state.</param>
    /// <param name="events">Ordered gameplay events.</param>
    public BattleTransition(BattleState state, IEnumerable<BattleEvent> events)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        Events = Array.AsReadOnly((events ?? throw new ArgumentNullException(nameof(events))).ToArray());
    }

    /// <summary>
    /// Gets the resulting immutable state.
    /// </summary>
    public BattleState State { get; }

    /// <summary>
    /// Gets deterministic events in application order.
    /// </summary>
    public IReadOnlyList<BattleEvent> Events { get; }

    /// <summary>
    /// Gets whether the command completed without rejection.
    /// </summary>
    public bool Succeeded => Events.All(item => item is not CommandRejectedEvent);
}
