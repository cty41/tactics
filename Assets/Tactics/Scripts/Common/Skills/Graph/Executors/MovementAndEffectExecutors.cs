using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Utilities;
using Tactics.Runtime.Utilities;

namespace Tactics.Common.Skills.Graph
{
    public class DashToTargetNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.DashToTarget;

        public async Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var record = (DashToTargetNodeRecord)node;
            var caster = context.Caster;
            var target = context.PrimaryTarget;
            var grid = context.GridController;
            var destinationCell = target?.CurrentCell ?? context.TargetPoint;

            if (destinationCell == null || caster?.CurrentCell == null)
                return SkillNodeExecutionResult.Failed("Invalid caster or dash destination.");

            int distance = destinationCell.GetDistance(caster.CurrentCell);
            if (distance > record.MaxRange)
                return SkillNodeExecutionResult.Failed($"Target out of dash range ({distance} > {record.MaxRange}).");

            var targetCell = destinationCell;
            var casterCell = caster.CurrentCell;
            var (dirX, dirY) = GetCardinalDirection(casterCell.GridCoordinates, targetCell.GridCoordinates);
            if (dirX == 0 && dirY == 0)
                return SkillNodeExecutionResult.Failed("Dash destination must be on the same row or column.");

            var stopCell = FindDashStopCell(casterCell, dirX, dirY, distance, grid);
            if (stopCell == null || stopCell == casterCell)
                return SkillNodeExecutionResult.Failed("No valid dash destination.");

            var path = BuildStraightLinePath(casterCell, stopCell, dirX, dirY, grid);
            if (path.Count == 0)
                return SkillNodeExecutionResult.Failed("No dash path available.");

            await grid.UnitManager.MarkAsMoving(caster, casterCell, stopCell, path);
            await caster.MovementAnimation(path, stopCell);

            casterCell.CurrentUnits.Remove(caster);
            casterCell.IsTaken = casterCell.CurrentUnits.Count > 0;

            caster.CurrentCell = stopCell;
            if (!stopCell.CurrentUnits.Contains(caster))
                stopCell.CurrentUnits.Add(caster);
            stopCell.IsTaken = true;
            caster.WorldPosition = stopCell.WorldPosition;

            await grid.UnitManager.UnMarkAsMoving(caster, casterCell, stopCell, path);
            caster.InvokeUnitMoved(new UnitMovedEventArgs(caster, casterCell, stopCell, path));

            if (target != null && record.CollisionDamage > 0f && target.CurrentCell.GetDistance(stopCell) <= 1)
            {
                CombatComponent.ApplyDamage(
                    caster, target, record.CollisionDamage, false, ElementType.None,
                    canTriggerBeforeAttacked: false, canCrit: false, canTriggerDamageTaken: true);
            }

            TLog.Info($"[DashToTarget] Dashed to ({stopCell.GridCoordinates.x}, {stopCell.GridCoordinates.y})");
            return SkillNodeExecutionResult.Success();
        }

        private (int dx, int dy) GetCardinalDirection(Common.Utilities.Vector2IntImpl from, Common.Utilities.Vector2IntImpl to)
        {
            int dx = to.x - from.x;
            int dy = to.y - from.y;

            if (dx != 0 && dy != 0)
                return (0, 0);

            return (dx == 0 ? 0 : (dx > 0 ? 1 : -1),
                    dy == 0 ? 0 : (dy > 0 ? 1 : -1));
        }

        private Cells.ICell FindDashStopCell(Cells.ICell start, int dirX, int dirY, int maxDist, Controllers.IGridController grid)
        {
            Cells.ICell lastValid = start;
            for (int i = 1; i <= maxDist; i++)
            {
                var coord = new Common.Utilities.Vector2IntImpl(
                    start.GridCoordinates.x + dirX * i,
                    start.GridCoordinates.y + dirY * i);
                var cell = grid.CellManager.GetCellAt(coord);
                if (cell == null) break;
                if (!grid.CellManager.IsCellWalkable(cell)) break;
                if (cell.CurrentUnits.Count > 0) break;
                lastValid = cell;
            }
            return lastValid;
        }

        private System.Collections.Generic.List<ICell> BuildStraightLinePath(ICell start, ICell end, int dirX, int dirY, IGridController grid)
        {
            var path = new System.Collections.Generic.List<ICell>();
            int steps = UnityEngine.Mathf.Max(
                UnityEngine.Mathf.Abs(end.GridCoordinates.x - start.GridCoordinates.x),
                UnityEngine.Mathf.Abs(end.GridCoordinates.y - start.GridCoordinates.y));

            for (int i = 1; i <= steps; i++)
            {
                var coord = new Vector2IntImpl(
                    start.GridCoordinates.x + dirX * i,
                    start.GridCoordinates.y + dirY * i);
                var cell = grid.CellManager.GetCellAt(coord);
                if (cell != null)
                    path.Add(cell);
            }

            return path;
        }
    }

    public class ApplyDamageNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.ApplyDamage;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var record = (ApplyDamageNodeRecord)node;
            var caster = context.Caster;
            var target = context.PrimaryTarget;

            if (target == null)
            {
                TLog.Info("[ApplyDamage] No target present, skipping damage application.");
                return Task.FromResult(SkillNodeExecutionResult.Success());
            }

            CombatComponent.ApplyDamage(
                caster, target, record.BaseDamage, record.IsRanged,
                record.DamageType == SkillGraphDamageType.Physical
                    ? ElementType.None
                    : ElementType.Fire,
                canTriggerBeforeAttacked: true,
                canCrit: record.CanCrit,
                canTriggerDamageTaken: true);

            TLog.Info($"[ApplyDamage] Dealt {record.BaseDamage} damage to target.");
            return Task.FromResult(SkillNodeExecutionResult.Success());
        }
    }

    public class ApplyKnockbackNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.ApplyKnockback;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var record = (ApplyKnockbackNodeRecord)node;
            var caster = context.Caster;
            var target = context.PrimaryTarget;
            var grid = context.GridController;

            if (target?.CurrentCell == null || caster?.CurrentCell == null)
            {
                TLog.Info("[ApplyKnockback] No target present, skipping knockback.");
                return Task.FromResult(SkillNodeExecutionResult.Success());
            }

            var targetCell = target.CurrentCell;
            var casterCell = caster.CurrentCell;

            int dx = targetCell.GridCoordinates.x - casterCell.GridCoordinates.x;
            int dy = targetCell.GridCoordinates.y - casterCell.GridCoordinates.y;
            float mag = UnityEngine.Mathf.Sqrt(dx * dx + dy * dy);
            if (mag < 0.01f)
                return Task.FromResult(SkillNodeExecutionResult.Failed("Cannot determine knockback direction."));

            int dirX = UnityEngine.Mathf.RoundToInt(dx / mag);
            int dirY = UnityEngine.Mathf.RoundToInt(dy / mag);

            var knockCoord = new Common.Utilities.Vector2IntImpl(
                targetCell.GridCoordinates.x + dirX * record.Distance,
                targetCell.GridCoordinates.y + dirY * record.Distance);
            var knockCell = grid.CellManager.GetCellAt(knockCoord);

            if (knockCell != null
                && grid.CellManager.IsCellWalkable(knockCell)
                && knockCell.CurrentUnits.Count == 0)
            {
                targetCell.CurrentUnits.Remove(target);
                targetCell.IsTaken = targetCell.CurrentUnits.Count > 0;
                target.CurrentCell = knockCell;
                if (!knockCell.CurrentUnits.Contains(target))
                    knockCell.CurrentUnits.Add(target);
                knockCell.IsTaken = true;

                if (target is UnityEngine.MonoBehaviour mb)
                    mb.transform.position = knockCell.WorldPosition.ToVector3();

                TLog.Info($"[ApplyKnockback] Knocked target to ({knockCell.GridCoordinates.x}, {knockCell.GridCoordinates.y})");
            }
            else
            {
                TLog.Info("[ApplyKnockback] Knockback destination blocked, target stays in place.");
            }

            return Task.FromResult(SkillNodeExecutionResult.Success());
        }
    }

    public class FinishNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.Finish;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            return Task.FromResult(SkillNodeExecutionResult.Completed());
        }
    }

    public class FailNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.Fail;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            return Task.FromResult(SkillNodeExecutionResult.Failed("Skill graph reached Fail node."));
        }
    }
}
