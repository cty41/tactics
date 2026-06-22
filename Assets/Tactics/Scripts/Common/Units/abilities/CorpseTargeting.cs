using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Interactables;
using Tactics.Common.Units.Buffs;
using UnityEngine;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Targets a corpse interactable within range. Used by summon skeleton ability.
    /// Returns the dead unit on the cell that has an associated Corpse interactable.
    /// </summary>
    [Serializable]
    public class CorpseTargeting : TargetingStrategy
    {
        [SerializeField] private int _minRange;
        [SerializeField] private int _maxRange = 3;

        public int MinRange => _minRange;
        public int MaxRange => _maxRange;

        public CorpseTargeting()
        {
            _targetType = TargetType.SingleEnemy;
        }

        public override IEnumerable<IUnit> GetTargets(IUnit caster, ICell selectedCell, IGridController gridController)
        {
            // Check if cell has a Corpse interactable
            bool hasCorpse = selectedCell.CurrentInteractables
                .Any(i => i is Corpse && !i.IsDestroyed);

            if (!hasCorpse) yield break;

            // Return the dead unit on this cell (the original unit that became corpse)
            foreach (var unit in selectedCell.CurrentUnits)
            {
                if (unit != null && unit.IsCorpse && unit.IsDowned)
                {
                    int distance = selectedCell.GetDistance(caster.CurrentCell);
                    if (distance >= _minRange && distance <= _maxRange)
                    {
                        yield return unit;
                    }
                }
            }
        }

        public override bool IsValidTarget(IUnit caster, IUnit target, IGridController gridController)
        {
            if (target == null || !target.IsCorpse) return false;
            if (target.CurrentCell == null || caster.CurrentCell == null) return false;

            // Must have Corpse interactable on the cell
            bool hasCorpse = target.CurrentCell.CurrentInteractables
                .Any(i => i is Corpse && !i.IsDestroyed);
            if (!hasCorpse) return false;

            int distance = target.CurrentCell.GetDistance(caster.CurrentCell);
            return distance >= _minRange && distance <= _maxRange;
        }
    }
}
