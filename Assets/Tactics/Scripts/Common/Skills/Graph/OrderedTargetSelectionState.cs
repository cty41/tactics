using System;
using System.Collections.Generic;
using Tactics.Common.Units;

namespace Tactics.Common.Skills.Graph
{
    public enum OrderedSelectionStage
    {
        Selecting,
        Ready,
        Committed,
        Cancelled
    }

    /// <summary>
    /// Ordered, duplicate-preserving target queue used by multi-stage abilities.
    /// Undo always removes the most recently selected segment.
    /// </summary>
    public sealed class OrderedTargetSelectionState
    {
        private readonly List<IUnit> _targets = new();

        public int RequiredCount { get; }
        public IReadOnlyList<IUnit> Targets => _targets;
        public OrderedSelectionStage Stage { get; private set; } = OrderedSelectionStage.Selecting;

        public OrderedTargetSelectionState(int requiredCount)
        {
            if (requiredCount < 1)
                throw new ArgumentOutOfRangeException(nameof(requiredCount));
            RequiredCount = requiredCount;
        }

        public bool TryAdd(IUnit target)
        {
            if (target == null || Stage is OrderedSelectionStage.Committed or OrderedSelectionStage.Cancelled ||
                _targets.Count >= RequiredCount)
                return false;

            _targets.Add(target);
            Stage = _targets.Count == RequiredCount
                ? OrderedSelectionStage.Ready
                : OrderedSelectionStage.Selecting;
            return true;
        }

        public bool UndoLast()
        {
            if (_targets.Count == 0 || Stage is OrderedSelectionStage.Committed or OrderedSelectionStage.Cancelled)
                return false;
            _targets.RemoveAt(_targets.Count - 1);
            Stage = OrderedSelectionStage.Selecting;
            return true;
        }

        public IReadOnlyList<IUnit> Commit()
        {
            if (Stage != OrderedSelectionStage.Ready)
                return Array.Empty<IUnit>();
            Stage = OrderedSelectionStage.Committed;
            return _targets.ToArray();
        }

        public void Cancel()
        {
            if (Stage == OrderedSelectionStage.Committed)
                return;
            _targets.Clear();
            Stage = OrderedSelectionStage.Cancelled;
        }
    }
}
