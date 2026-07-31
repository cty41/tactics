using System.Linq;
using Tactics.Common.Cells;
using Tactics.Common.Units;

namespace Tactics.Common.Controllers.GridStates
{
    /// <summary>
    /// Represents the state of the grid that awaits player input, specifically for unit selection.
    /// </summary>
    public class GridStateAwaitInput : GridState
    {
        public override void OnCellClicked(ICell cell, GridController gridController)
        {
            var activeUnit = gridController.TurnContext.PlayableUnits().FirstOrDefault();
            if (activeUnit?.CurrentCell == null || cell == null ||
                !FacingResolver.IsOrthogonallyAdjacent(
                    activeUnit.CurrentCell.GridCoordinates,
                    cell.GridCoordinates))
            {
                return;
            }

            FacingCoordinator.FaceTarget(activeUnit, cell);
        }

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
                // 点击单位后保持 AwaitInput 状态，不自动切换到技能选择
                // 玩家需要通过 UI 按钮手动选择移动或技能
                unit.InvokeUnitSelected();
            }
        }
    }
}
