using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Interactables;
using UnityEngine;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Targets a corpse interactable within range. Used by summon skeleton ability.
    /// Returns Corpse interactables on the cell within range.
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

        public IEnumerable<Corpse> GetCorpseTargets(IUnit caster, ICell selectedCell, IGridController gridController)
        {
            if (selectedCell == null || caster?.CurrentCell == null) yield break;

            int distance = selectedCell.GetDistance(caster.CurrentCell);
            if (distance < _minRange || distance > _maxRange) yield break;

            foreach (var interactable in selectedCell.CurrentInteractables)
            {
                if (interactable is Corpse corpse && !corpse.IsDestroyed)
                {
                    yield return corpse;
                }
            }
        }

        public bool IsValidCorpseTarget(IUnit caster, Corpse target, IGridController gridController)
        {
            if (target == null || target.IsDestroyed) return false;
            if (target.CurrentCell == null || caster?.CurrentCell == null) return false;

            int distance = target.CurrentCell.GetDistance(caster.CurrentCell);
            return distance >= _minRange && distance <= _maxRange;
        }

        public override IEnumerable<IUnit> GetTargets(IUnit caster, ICell selectedCell, IGridController gridController)
        {
            yield break;
        }

        public override bool IsValidTarget(IUnit caster, IUnit target, IGridController gridController)
        {
            return false;
        }
    }
}
