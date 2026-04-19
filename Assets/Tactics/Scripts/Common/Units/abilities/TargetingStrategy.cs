using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using UnityEngine;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Base class for targeting strategies. Defines how targets are selected for an ability.
    /// </summary>
    [Serializable]
    public abstract class TargetingStrategy
    {
        [SerializeField] protected TargetType _targetType;

        public TargetType TargetType => _targetType;

        /// <summary>
        /// Returns the list of targets based on the selected cell.
        /// </summary>
        public abstract IEnumerable<IUnit> GetTargets(IUnit caster, ICell selectedCell, IGridController gridController);

        /// <summary>
        /// Checks if a unit is a valid target for this strategy.
        /// </summary>
        public abstract bool IsValidTarget(IUnit caster, IUnit target, IGridController gridController);

        /// <summary>
        /// Displays a preview of the targeting area on the grid.
        /// </summary>
        public virtual void DisplayPreview(IGridController gridController) { }

        /// <summary>
        /// Cleans up any visual highlights from the preview.
        /// </summary>
        public virtual void CleanUpPreview(IGridController gridController) { }
    }

    /// <summary>
    /// Targets the caster itself.
    /// </summary>
    [Serializable]
    public class SelfTargeting : TargetingStrategy
    {
        public SelfTargeting()
        {
            _targetType = TargetType.Self;
        }

        public override IEnumerable<IUnit> GetTargets(IUnit caster, ICell selectedCell, IGridController gridController)
        {
            yield return caster;
        }

        public override bool IsValidTarget(IUnit caster, IUnit target, IGridController gridController)
        {
            return ReferenceEquals(caster, target);
        }
    }

    /// <summary>
    /// Targets a single enemy within range.
    /// </summary>
    [Serializable]
    public class SingleTargetEnemy : TargetingStrategy
    {
        [SerializeField] private int _minRange;
        [SerializeField] private int _maxRange = 1;

        public int MinRange => _minRange;
        public int MaxRange => _maxRange;

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
            int distance = target.CurrentCell.GetDistance(caster.CurrentCell);
            return distance >= _minRange &&
                   distance <= _maxRange &&
                   target.PlayerNumber != caster.PlayerNumber;
        }
    }

    /// <summary>
    /// Targets a single ally within range.
    /// </summary>
    [Serializable]
    public class SingleTargetAlly : TargetingStrategy
    {
        [SerializeField] private int _maxRange = 1;

        public int MaxRange => _maxRange;

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
            int distance = target.CurrentCell.GetDistance(caster.CurrentCell);
            return distance <= _maxRange &&
                   target.PlayerNumber == caster.PlayerNumber;
        }
    }

    /// <summary>
    /// Targets all units within an area of effect.
    /// </summary>
    [Serializable]
    public class AoETargeting : TargetingStrategy
    {
        [SerializeField] private int _radius = 1;
        [SerializeField] private int _maxRange = 4;
        [SerializeField] private AoeShape _shape = AoeShape.Cross;

        public int Radius => _radius;
        public int MaxRange => _maxRange;
        public AoeShape Shape => _shape;

        public override IEnumerable<IUnit> GetTargets(IUnit caster, ICell selectedCell, IGridController gridController)
        {
            var aoeCells = GetAoeCells(selectedCell, gridController);
            foreach (var cell in aoeCells)
            {
                foreach (var unit in cell.CurrentUnits)
                {
                    yield return unit;
                }
            }
        }

        public override bool IsValidTarget(IUnit caster, IUnit target, IGridController gridController)
        {
            int distance = target.CurrentCell.GetDistance(caster.CurrentCell);
            return distance <= _maxRange;
        }

        public override void DisplayPreview(IGridController gridController)
        {
            // Preview is shown on cell hover in GenericAbilityImpl
        }

        private HashSet<ICell> GetAoeCells(ICell center, IGridController gridController)
        {
            var cells = new HashSet<ICell> { center };
            if (_shape == AoeShape.Cross)
            {
                var neighbours = center.GetNeighbours(gridController.CellManager);
                foreach (var n in neighbours)
                {
                    cells.Add(n);
                }
            }
            else if (_shape == AoeShape.Circle)
            {
                cells.UnionWith(gridController.CellManager.GetCells().Where(c => c.GetDistance(center) <= _radius));
            }
            return cells;
        }
    }

    /// <summary>
    /// Targets multiple enemies within range.
    /// </summary>
    [Serializable]
    public class MultiTargetEnemy : TargetingStrategy
    {
        [SerializeField] private int _maxTargets = 3;
        [SerializeField] private int _maxRange = 4;

        public int MaxTargets => _maxTargets;
        public int MaxRange => _maxRange;

        public override IEnumerable<IUnit> GetTargets(IUnit caster, ICell selectedCell, IGridController gridController)
        {
            var enemies = gridController.UnitManager.GetEnemyUnits(gridController.TurnContext.CurrentPlayer);
            int count = 0;
            foreach (var enemy in enemies)
            {
                if (count >= _maxTargets) break;
                int distance = enemy.CurrentCell.GetDistance(selectedCell);
                if (distance <= _maxRange)
                {
                    yield return enemy;
                    count++;
                }
            }
        }

        public override bool IsValidTarget(IUnit caster, IUnit target, IGridController gridController)
        {
            int distance = target.CurrentCell.GetDistance(caster.CurrentCell);
            return distance <= _maxRange &&
                   target.PlayerNumber != caster.PlayerNumber;
        }
    }

    /// <summary>
    /// Targets a location for movement followed by attack.
    /// </summary>
    [Serializable]
    public class MoveThenAttackTargeting : TargetingStrategy
    {
        [SerializeField] private int _moveRange;

        public int MoveRange => _moveRange;

        public override IEnumerable<IUnit> GetTargets(IUnit caster, ICell selectedCell, IGridController gridController)
        {
            foreach (var unit in selectedCell.CurrentUnits)
            {
                yield return unit;
            }
        }

        public override bool IsValidTarget(IUnit caster, IUnit target, IGridController gridController)
        {
            return target.PlayerNumber != caster.PlayerNumber;
        }
    }

    /// <summary>
    /// Targets a location for movement followed by healing.
    /// </summary>
    [Serializable]
    public class MoveThenHealTargeting : TargetingStrategy
    {
        [SerializeField] private int _moveRange;
        [SerializeField] private int _healRange = 1;

        public int MoveRange => _moveRange;
        public int HealRange => _healRange;

        public override IEnumerable<IUnit> GetTargets(IUnit caster, ICell selectedCell, IGridController gridController)
        {
            foreach (var cell in selectedCell.GetNeighbours(gridController.CellManager))
            {
                foreach (var unit in cell.CurrentUnits)
                {
                    if (IsValidTarget(caster, unit, gridController))
                    {
                        yield return unit;
                    }
                }
            }
        }

        public override bool IsValidTarget(IUnit caster, IUnit target, IGridController gridController)
        {
            return target.PlayerNumber == caster.PlayerNumber &&
                   target.Health < target.MaxHealth;
        }
    }

    /// <summary>
    /// Defines the type of targeting.
    /// </summary>
    public enum TargetType
    {
        Self,
        SingleEnemy,
        SingleAlly,
        AoE,
        MultiEnemy,
        MoveThenAttack,
        MoveThenHeal
    }

    /// <summary>
    /// Defines the shape of an area of effect.
    /// </summary>
    public enum AoeShape
    {
        Circle,
        Cross,
        Line
    }
}
