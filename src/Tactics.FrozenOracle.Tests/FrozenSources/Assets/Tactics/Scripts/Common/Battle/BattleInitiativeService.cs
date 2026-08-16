using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Tactics.Common.Controllers;
using Tactics.Common.Units;

namespace Tactics.Common.Battle
{
    /// <summary>
    /// Owns the current-round initiative partition. Only units that have not yet
    /// started an action are reordered after a speed change.
    /// </summary>
    public sealed class BattleInitiativeService
    {
        private static readonly ConditionalWeakTable<IGridController, BattleInitiativeService> Services = new();

        private readonly List<IUnit> _remaining = new();
        private readonly HashSet<IUnit> _acted = new();

        public IUnit Current { get; private set; }
        public IReadOnlyList<IUnit> Remaining => _remaining;
        public IReadOnlyCollection<IUnit> Acted => _acted;

        public static BattleInitiativeService For(IGridController gridController)
        {
            return gridController == null ? null : Services.GetValue(gridController, _ => new BattleInitiativeService());
        }

        public static void Attach(IGridController gridController, BattleInitiativeService service)
        {
            if (gridController == null || service == null)
                return;

            Services.Remove(gridController);
            Services.Add(gridController, service);
        }

        public void StartRound(IEnumerable<IUnit> units)
        {
            _acted.Clear();
            _remaining.Clear();
            Current = null;
            AddAliveUnits(units);
            SortRemaining();
        }

        public IUnit TakeNext(IEnumerable<IUnit> aliveUnits)
        {
            Synchronize(aliveUnits);
            if (_remaining.Count == 0)
            {
                StartRound(aliveUnits);
            }

            if (_remaining.Count == 0)
            {
                Current = null;
                return null;
            }

            Current = _remaining[0];
            _remaining.RemoveAt(0);
            _acted.Add(Current);
            return Current;
        }

        public void NotifyInitiativeChanged(IUnit unit)
        {
            if (unit != null && _remaining.Contains(unit))
                SortRemaining();
        }

        public IReadOnlyList<IUnit> GetCurrentRoundOrder(bool includeCurrent = true)
        {
            var result = new List<IUnit>();
            if (includeCurrent && Current != null)
                result.Add(Current);
            result.AddRange(_remaining);
            return result;
        }

        public void Reset()
        {
            Current = null;
            _remaining.Clear();
            _acted.Clear();
        }

        private void Synchronize(IEnumerable<IUnit> aliveUnits)
        {
            var alive = (aliveUnits ?? Enumerable.Empty<IUnit>())
                .Where(unit => unit != null && unit.Health > 0 && !AmazonBattleState.IsDecoy(unit))
                .Distinct()
                .ToList();
            var aliveSet = alive.ToHashSet();

            _remaining.RemoveAll(unit => !aliveSet.Contains(unit));
            _acted.RemoveWhere(unit => !aliveSet.Contains(unit));
            if (Current != null && !aliveSet.Contains(Current))
                Current = null;

            AddAliveUnits(alive.Where(unit => !ReferenceEquals(unit, Current) && !_acted.Contains(unit)));
            SortRemaining();
        }

        private void AddAliveUnits(IEnumerable<IUnit> units)
        {
            foreach (var unit in units ?? Enumerable.Empty<IUnit>())
            {
                if (unit == null || unit.Health <= 0 || AmazonBattleState.IsDecoy(unit) || ReferenceEquals(unit, Current) ||
                    _acted.Contains(unit) || _remaining.Contains(unit))
                    continue;
                _remaining.Add(unit);
            }
        }

        private void SortRemaining()
        {
            _remaining.Sort(CompareUnits);
        }

        private static int CompareUnits(IUnit left, IUnit right)
        {
            int initiative = right.Initiative.CompareTo(left.Initiative);
            if (initiative != 0) return initiative;
            int player = left.PlayerNumber.CompareTo(right.PlayerNumber);
            return player != 0 ? player : left.UnitID.CompareTo(right.UnitID);
        }
    }
}
