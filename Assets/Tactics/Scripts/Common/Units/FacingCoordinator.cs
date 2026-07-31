using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Units.Tween;

namespace Tactics.Common.Units
{
    /// <summary>
    /// Controls whether a displacement updates the moving unit's facing.
    /// </summary>
    internal enum MovementFacingPolicy
    {
        Preserve,
        FollowPath
    }

    /// <summary>
    /// Centralizes runtime facing changes caused by targeting and movement.
    /// </summary>
    internal static class FacingCoordinator
    {
        public static bool FaceTarget(IUnit unit, ICell target)
        {
            return unit?.CurrentCell != null && FaceStep(unit, unit.CurrentCell, target);
        }

        public static bool FaceStep(IUnit unit, ICell source, ICell destination)
        {
            if (unit == null || source == null || destination == null)
                return false;

            if (!FacingResolver.TryResolve(
                    source.GridCoordinates,
                    destination.GridCoordinates,
                    unit.Facing,
                    out var facing))
            {
                return false;
            }

            unit.Facing = facing;
            return true;
        }

        public static async Task AnimateMovementAsync(
            IUnit unit,
            IEnumerable<ICell> path,
            ICell destination,
            MovementFacingPolicy policy)
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));

            var pathList = path?.Where(cell => cell != null).ToList() ?? new List<ICell>();
            if (policy == MovementFacingPolicy.Preserve || pathList.Count == 0)
            {
                await unit.MovementAnimation(pathList, destination);
                return;
            }

            void OnUnitLeftCell(UnitChangedGridPositionEventArgs eventArgs)
            {
                if (ReferenceEquals(eventArgs.AffectedUnit, unit))
                {
                    FaceStep(unit, eventArgs.LeftCell, eventArgs.EnteredCell);
                    UnitAnimationCoordinator.BeginMoveStep(
                        unit,
                        eventArgs.LeftCell,
                        eventArgs.EnteredCell);
                }
            }

            void OnUnitEnteredCell(UnitChangedGridPositionEventArgs eventArgs)
            {
                if (ReferenceEquals(eventArgs.AffectedUnit, unit))
                    UnitAnimationCoordinator.EndMoveStep(unit);
            }

            unit.UnitLeftCell += OnUnitLeftCell;
            unit.UnitEnteredCell += OnUnitEnteredCell;
            try
            {
                // Apply the first segment before animation begins. UnityMoveComponent
                // raises UnitLeftCell for every later segment, including path turns.
                FaceStep(unit, unit.CurrentCell, pathList[0]);
                await unit.MovementAnimation(pathList, destination);
            }
            finally
            {
                unit.UnitLeftCell -= OnUnitLeftCell;
                unit.UnitEnteredCell -= OnUnitEnteredCell;
                UnitAnimationCoordinator.EndMoveStep(unit);
            }
        }
    }
}
