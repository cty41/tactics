using System;
using System.Linq;
using TurnBasedStrategyFramework.Common.Cells;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Cells;
using TurnBasedStrategyFramework.Unity.Units;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Units
{
    /// <summary>
    /// Represents a unit used in Example 4.
    /// </summary>
    public class Example4Unit : Unit, ITypedUnit, IColoredUnit
    {
        [SerializeField] private ScriptableObject _unitType;
        [SerializeField] private ScriptableObject _structureUnitType;
        [SerializeField] private Color _color;

        public ScriptableObject UnitType => _unitType;
        public Color Color { get { return _color; } set { _color = value; } }

        public override bool IsUnitAttackable(IUnit otherUnit, ICell otherUnitCell, ICell attackSourceCell)
        {
            return !_unitType.Equals(_structureUnitType) && base.IsUnitAttackable(otherUnit, otherUnitCell, attackSourceCell) && !(otherUnit as ITypedUnit).UnitType.Equals(_structureUnitType);
        }

        public override float CalculateDamageDealt(IUnit defender, ICell defenderCell, ICell aggressorCell)
        {
            return base.CalculateDamageDealt(defender, defenderCell, aggressorCell) * (Health / MaxHealth);
        }

        public override float CalculateDamageTaken(IUnit aggressor, float damageDealt, ICell aggressorCell, ICell defenderCell)
        {
            return Math.Max(base.CalculateDamageTaken(aggressor, damageDealt, aggressorCell, defenderCell) - (defenderCell as IDefenceAffectingCell).DefenceModifier, 1);
        }

        public override bool IsCellTraversable(ICell source, ICell destination)
        {
            return base.IsCellTraversable(source, destination) || (destination.CurrentUnits.Count > 0 && !destination.CurrentUnits.Any(u => !(u as ITypedUnit).UnitType.Equals(_structureUnitType) && u.PlayerNumber != PlayerNumber));
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            CurrentCell.InvokeCellHighlighted();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            CurrentCell.InvokeCellDehighlighted();
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            base.OnPointerClick(eventData);
        }
    }
}