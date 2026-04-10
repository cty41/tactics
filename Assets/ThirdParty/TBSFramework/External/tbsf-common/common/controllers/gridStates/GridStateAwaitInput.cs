using System.Linq;
using TurnBasedStrategyFramework.Common.Units;

namespace TurnBasedStrategyFramework.Common.Controllers.GridStates
{
    /// <summary>
    /// Represents the state of the grid that awaits player input, specifically for unit selection.
    /// </summary>
    public class GridStateAwaitInput : GridState
    {
        /// <summary>
        /// Called when a unit is clicked while awaiting input.
        /// If the clicked unit is a playable unit, the state transitions to GridStateUnitSelected.
        /// </summary>
        /// <param name="unit">The unit that was clicked.</param>
        /// <param name="gridController">The grid controller.</param>
        public override void OnUnitClicked(IUnit unit, GridController gridController)
        {
            var activeUnit = gridController.TurnContext.PlayableUnits().FirstOrDefault();
            // UnitSpeed 期望每回合只有一个可行动单位；当场景绑定异常时也至少阻止越权点击。
            if (activeUnit != null && ReferenceEquals(activeUnit, unit))
            {
                gridController.GridState = new GridStateUnitSelected(unit, unit.GetBaseAbilities());
            }
        }
    }
}