using Tactics.Core.Units;

namespace Tactics.Core.Turns;

public sealed class TurnState
{
    private readonly IReadOnlyList<UnitInstanceId> _order;

    public TurnState(int round, IReadOnlyList<UnitInstanceId> order, int activeIndex = 0)
    {
        if (round < 1)
            throw new ArgumentOutOfRangeException(nameof(round));
        if (order is null || order.Count == 0)
            throw new ArgumentException("A turn order must contain at least one unit.", nameof(order));
        if (activeIndex < 0 || activeIndex >= order.Count)
            throw new ArgumentOutOfRangeException(nameof(activeIndex));

        Round = round;
        _order = order.ToArray();
        ActiveIndex = activeIndex;
    }

    public int Round { get; private set; }
    public int ActiveIndex { get; private set; }
    public IReadOnlyList<UnitInstanceId> Order => _order;
    public UnitInstanceId ActiveUnitId => _order[ActiveIndex];

    public void Advance()
    {
        ActiveIndex++;
        if (ActiveIndex < _order.Count)
            return;

        ActiveIndex = 0;
        Round++;
    }
}
