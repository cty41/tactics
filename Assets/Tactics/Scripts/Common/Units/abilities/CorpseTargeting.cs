using System;
using System.Collections.Generic;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units.Buffs;
using UnityEngine;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Targets a corpse unit within range. Used by summon skeleton ability.
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
            foreach (var unit in selectedCell.CurrentUnits)
            {
                if (IsValidTarget(caster, unit, gridController))
                {
                    yield return unit;
                }
            }
        }

        public override bool IsValidTarget(IUnit caster, IUnit target, IGridController gridController)
        {
            if (target == null || !target.IsCorpse) return false;
            if (target.CurrentCell == null || caster.CurrentCell == null) return false;

            int distance = target.CurrentCell.GetDistance(caster.CurrentCell);
            return distance >= _minRange && distance <= _maxRange;
        }
    }
}
