using System.Linq;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Interactables;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// Defines the shared admission rule for cells targeted by corpse-consuming skills.
    /// </summary>
    public static class NecromancerCorpseTargetRules
    {
        /// <summary>
        /// Returns whether the cell is a registered fixed-board cell containing an available corpse and no live unit.
        /// </summary>
        public static bool IsLegalCorpseTarget(ICell cell, IGridController gridController)
        {
            if (cell == null || gridController?.CellManager == null)
                return false;

            int x = cell.GridCoordinates.x;
            int y = cell.GridCoordinates.y;
            if (!BattleBoardSpec.Contains(x, y) ||
                !ReferenceEquals(gridController.CellManager.GetCellAt(cell.GridCoordinates), cell))
            {
                return false;
            }

            bool hasCorpse = cell.CurrentInteractables.Any(
                interactable => interactable is Corpse corpse && !corpse.IsDestroyed);
            if (!hasCorpse)
                return false;

            return !cell.CurrentUnits.Any(unit => unit != null && !unit.IsDowned);
        }
    }
}
