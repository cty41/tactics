using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Utilities;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Charge attack effect: moves the caster in a straight line along one of the four cardinal
    /// directions toward the target. Stops if any unit (ally or enemy) blocks the path.
    /// On arrival, deals collision damage to blocking units and knocks the primary target back 1 tile.
    /// </summary>
    [Serializable]
    public class ChargeAttackEffect : AbilityEffect
    {
        [SerializeField] private int _maxChargeRange = 4;
        [SerializeField] private bool _stopOnAllUnits = true;
        [SerializeField] private float _collisionDamage = 1f;

        public override async Task Execute(IUnit caster, IEnumerable<IUnit> targets, IGridController gridController)
        {
            foreach (var target in targets)
            {
                if (target == null) continue;

                ICell targetCell = target.CurrentCell;
                ICell casterCell = caster.CurrentCell;
                if (targetCell == null || casterCell == null) continue;

                // Step 1: Get cardinal direction (only horizontal or vertical, not diagonal)
                var (dirX, dirY) = GetCardinalDirection(casterCell.GridCoordinates, targetCell.GridCoordinates);
                if (dirX == 0 && dirY == 0) continue; // Not on same row or column

                // Step 2: Check if target is within charge range
                int distance = Mathf.Abs(targetCell.GridCoordinates.x - casterCell.GridCoordinates.x)
                             + Mathf.Abs(targetCell.GridCoordinates.y - casterCell.GridCoordinates.y);
                if (distance > _maxChargeRange)
                {
                    TLog.Warning($"[ChargeAttackEffect] Target out of range ({distance} > {_maxChargeRange})");
                    continue;
                }

                // Step 3: Scan the straight-line path for obstacles/units
                var (stopCell, blockedByUnit, hitTarget) = ScanChargePath(
                    casterCell, dirX, dirY, distance, targetCell, gridController);

                if (stopCell == null || stopCell == casterCell) continue;

                // Step 4: Build straight-line path from caster to stop cell
                var path = BuildStraightLinePath(casterCell, stopCell, dirX, dirY, gridController);
                if (path.Count == 0) continue;

                // Step 5: Execute charge movement (fast step-by-step movement)
                var moveCommand = new MoveCommand(casterCell, stopCell, path);
                await moveCommand.Execute(caster, gridController);

                // Step 6: Apply collision damage to blocking unit
                if (blockedByUnit != null && blockedByUnit.Health > 0 && _collisionDamage > 0f)
                {
                    CombatComponent.ApplyDamage(caster, blockedByUnit, _collisionDamage, false, ElementType.None,
                        canTriggerBeforeAttacked: false, canCrit: false, canTriggerDamageTaken: true);
                }

                // Step 7: If we reached the actual target, knock it back 1 tile along charge direction
                if (hitTarget && target.Health > 0)
                {
                    var knockbackCoord = new Vector2IntImpl(
                        targetCell.GridCoordinates.x + dirX,
                        targetCell.GridCoordinates.y + dirY);
                    var knockbackCell = gridController.CellManager.GetCellAt(knockbackCoord);

                    if (knockbackCell != null
                        && gridController.CellManager.IsCellWalkable(knockbackCell)
                        && !knockbackCell.CurrentUnits.Any())
                    {
                        // Move target to knockback cell
                        targetCell.CurrentUnits.Remove(target);
                        targetCell.IsTaken = targetCell.CurrentUnits.Count > 0;

                        target.CurrentCell = knockbackCell;
                        if (!knockbackCell.CurrentUnits.Contains(target))
                            knockbackCell.CurrentUnits.Add(target);
                        knockbackCell.IsTaken = true;

                        // Note: The visual knockback effect (parabolic animation) is handled here
                        if (target is MonoBehaviour mb)
                        {
                            mb.transform.position = knockbackCell.WorldPosition.ToVector3();
                        }
                    }
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Returns unit direction vector (dx, dy) where exactly one of dx/dy is non-zero (cardinal).
        /// Returns (0,0) for diagonal or same position.
        /// </summary>
        private (int dx, int dy) GetCardinalDirection(Vector2IntImpl from, Vector2IntImpl to)
        {
            int dx = to.x - from.x;
            int dy = to.y - from.y;

            if (dx != 0 && dy != 0) return (0, 0); // Diagonal
            return (dx == 0 ? 0 : (dx > 0 ? 1 : -1),
                    dy == 0 ? 0 : (dy > 0 ? 1 : -1));
        }

        /// <summary>
        /// Scans cells along the charge path, checking for collisions.
        /// Returns: (stopCell, unitBlockingPath, reachedTarget)
        /// </summary>
        private (ICell stopCell, IUnit blockingUnit, bool hitTarget) ScanChargePath(
            ICell startCell, int dirX, int dirY, int maxDistance,
            ICell targetCell, IGridController gridController)
        {
            for (int i = 1; i <= maxDistance; i++)
            {
                var coord = new Vector2IntImpl(
                    startCell.GridCoordinates.x + dirX * i,
                    startCell.GridCoordinates.y + dirY * i);
                var cell = gridController.CellManager.GetCellAt(coord);

                if (cell == null)
                    return (GetRelativeCell(startCell, dirX, dirY, i - 1, gridController), null, false);

                if (!gridController.CellManager.IsCellWalkable(cell))
                    return (GetRelativeCell(startCell, dirX, dirY, i - 1, gridController), null, false);

                // Check for units on this cell
                var occupants = cell.CurrentUnits.ToList();
                if (occupants.Any())
                {
                    if (cell == targetCell)
                    {
                        // Reached the target!
                        return (cell, null, true);
                    }

                    // Blocked by another unit
                    if (_stopOnAllUnits)
                    {
                        return (GetRelativeCell(startCell, dirX, dirY, i - 1, gridController), occupants.First(), false);
                    }
                }
            }

            // Reached max range without hitting target or obstacle
            return (GetRelativeCell(startCell, dirX, dirY, maxDistance, gridController), null, false);
        }

        private ICell GetRelativeCell(ICell origin, int dirX, int dirY, int steps, IGridController gridController)
        {
            if (steps == 0) return origin;
            var coord = new Vector2IntImpl(
                origin.GridCoordinates.x + dirX * steps,
                origin.GridCoordinates.y + dirY * steps);
            return gridController.CellManager.GetCellAt(coord);
        }

        private List<ICell> BuildStraightLinePath(ICell start, ICell end, int dirX, int dirY, IGridController gridController)
        {
            var path = new List<ICell>();
            int steps = Mathf.Max(
                Mathf.Abs(end.GridCoordinates.x - start.GridCoordinates.x),
                Mathf.Abs(end.GridCoordinates.y - start.GridCoordinates.y));

            for (int i = 1; i <= steps; i++)
            {
                var cell = GetRelativeCell(start, dirX, dirY, i, gridController);
                if (cell != null) path.Add(cell);
            }
            return path;
        }
    }
}
