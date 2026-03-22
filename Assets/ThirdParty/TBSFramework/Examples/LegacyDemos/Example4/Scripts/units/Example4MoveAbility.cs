using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.Units;
using TurnBasedStrategyFramework.Unity.Units.Abilities;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Units.Abilities
{
    /// <summary>
    /// A move ability that handles interactions with both regular units and structures in Example 4.
    /// </summary>
    public class Example4MoveAbility : MoveAbility
    {
        [SerializeField] private UnitType structureUnitType;

        public override void OnUnitClicked(IUnit unit, IGridController gridController)
        {
            if ((unit as ITypedUnit).UnitType.Equals(structureUnitType) && UnitReference.GetAvailableDestinations(gridController.CellManager.GetCells()).Contains(unit.CurrentCell))
            {
                OnCellClicked(unit.CurrentCell, gridController);
            }
            else
            {
                base.OnUnitClicked(unit, gridController);
            }
        }
    }
}