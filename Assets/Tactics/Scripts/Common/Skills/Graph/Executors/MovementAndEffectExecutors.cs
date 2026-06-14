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
        private const float ChargeCollisionDamage = 1f;

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

            var chargeResult = await ExecuteChargeAsync(caster, target, grid, casterCell, targetCell, dirX, dirY, distance);
            if (!chargeResult)
                return SkillNodeExecutionResult.Failed(chargeResult.FailReason);

            TLog.Info($"[DashToTarget] Charge resolved: {chargeResult.Message}");
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

        private async Task<ChargeResolution> ExecuteChargeAsync(
            IUnit caster,
            IUnit target,
            IGridController grid,
            ICell casterCell,
            ICell targetCell,
            int dirX,
            int dirY,
            int distance)
        {
            for (int i = 1; i < distance; i++)
            {
                var coord = new Common.Utilities.Vector2IntImpl(
                    casterCell.GridCoordinates.x + dirX * i,
                    casterCell.GridCoordinates.y + dirY * i);
                var cell = grid.CellManager.GetCellAt(coord);

                if (cell == null || !grid.CellManager.IsCellWalkable(cell))
                    return ChargeResolution.Fail("Charge path is blocked.");

                if (cell.CurrentUnits.Count > 0)
                {
                    var blockingUnit = cell.CurrentUnits[0];
                    var blockerStopCell = GetRelativeCell(casterCell, dirX, dirY, i - 1, grid) ?? casterCell;

                    await MoveUnitAsync(caster, casterCell, blockerStopCell, grid);
                    ApplyCollisionDamage(caster, blockingUnit);

                    return ChargeResolution.Success($"Blocked by '{blockingUnit}'.");
                }
            }

            var retreatCell = GetRelativeCell(targetCell, dirX, dirY, 1, grid);
            var canRetreat = retreatCell != null
                && grid.CellManager.IsCellWalkable(retreatCell)
                && retreatCell.CurrentUnits.Count == 0;

            if (canRetreat)
            {
                await MoveUnitAsync(target, targetCell, retreatCell, grid);
                await MoveUnitAsync(caster, casterCell, targetCell, grid);
                ApplyCollisionDamage(caster, target);
                return ChargeResolution.Success($"Reached target '{targetCell.GridCoordinates}'.");
            }

            var stopCell = distance > 1
                ? GetRelativeCell(casterCell, dirX, dirY, distance - 1, grid)
                : casterCell;

            if (stopCell != null && stopCell != casterCell)
                await MoveUnitAsync(caster, casterCell, stopCell, grid);

            ApplyCollisionDamage(caster, target);
            ApplyCollisionDamage(target, caster);
            return ChargeResolution.Success("Target could not retreat.");
        }

        private void ApplyCollisionDamage(IUnit attacker, IUnit target)
        {
            if (attacker == null || target == null)
                return;

            CombatComponent.ApplyDamage(
                attacker, target, ChargeCollisionDamage, false, ElementType.None,
                canTriggerBeforeAttacked: false, canCrit: false, canTriggerDamageTaken: true);
        }

        private async Task MoveUnitAsync(IUnit unit, ICell source, ICell destination, IGridController grid)
        {
            if (unit == null || source == null || destination == null || ReferenceEquals(source, destination))
                return;

            var (dirX, dirY) = GetCardinalDirection(source.GridCoordinates, destination.GridCoordinates);
            var path = BuildStraightLinePath(source, destination, dirX, dirY, grid);
            if (path.Count == 0)
                return;

            await grid.UnitManager.MarkAsMoving(unit, source, destination, path);
            await unit.MovementAnimation(path, destination);

            source.CurrentUnits.Remove(unit);
            source.IsTaken = source.CurrentUnits.Count > 0;

            unit.CurrentCell = destination;
            if (!destination.CurrentUnits.Contains(unit))
                destination.CurrentUnits.Add(unit);
            destination.IsTaken = destination.CurrentUnits.Count > 0;
            unit.WorldPosition = destination.WorldPosition;

            await grid.UnitManager.UnMarkAsMoving(unit, source, destination, path);
            unit.InvokeUnitMoved(new UnitMovedEventArgs(unit, source, destination, path));
        }

        private Cells.ICell GetRelativeCell(Cells.ICell origin, int dirX, int dirY, int steps, IGridController grid)
        {
            if (origin == null)
                return null;

            if (steps == 0)
                return origin;

            var coord = new Common.Utilities.Vector2IntImpl(
                origin.GridCoordinates.x + dirX * steps,
                origin.GridCoordinates.y + dirY * steps);
            return grid.CellManager.GetCellAt(coord);
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

        private readonly struct ChargeResolution
        {
            public bool IsSuccess { get; }
            public string Message { get; }
            public string FailReason { get; }

            private ChargeResolution(bool success, string message, string failReason)
            {
                IsSuccess = success;
                Message = message;
                FailReason = failReason;
            }

            public static ChargeResolution Success(string message) => new(true, message, null);
            public static ChargeResolution Fail(string reason) => new(false, null, reason);

            public static implicit operator bool(ChargeResolution resolution) => resolution.IsSuccess;
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

            // 命中率惩罚检查（HeavyShot 等技能）
            if (record.AccuracyPenalty > 0f && !CombatComponent.IsHit(caster, target, record.AccuracyPenalty))
            {
                TLog.Info($"[ApplyDamage] Attack missed (accuracyPenalty={record.AccuracyPenalty}).");
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

    public class ProjectileLaunchNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.ProjectileLaunch;

        public async Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var record = (ProjectileLaunchNodeRecord)node;
            var caster = context.Caster;
            var target = context.PrimaryTarget;
            var grid = context.GridController;

            if (target == null || target.CurrentCell == null)
                return SkillNodeExecutionResult.Failed("No target for projectile launch.");

            if (caster == null || caster.CurrentCell == null)
                return SkillNodeExecutionResult.Failed("No caster for projectile launch.");

            TLog.Info($"[ProjectileLaunch] Launching projectile from ({caster.CurrentCell.GridCoordinates.x},{caster.CurrentCell.GridCoordinates.y}) to ({target.CurrentCell.GridCoordinates.x},{target.CurrentCell.GridCoordinates.y})");

            context.RecordEvent("ProjectileLaunched", node.NodeId, target);

            float travelTime = UnityEngine.Mathf.Max(0.05f, record.TravelTime);
            await Task.Delay((int)(travelTime * 1000));

            context.SetBlackboard("ProjectileHit", true);
            context.SetBlackboard("ProjectileTarget", target);
            context.RecordEvent("ProjectileHit", node.NodeId, target);

            TLog.Info("[ProjectileLaunch] Projectile reached target.");
            return SkillNodeExecutionResult.Success();
        }
    }

    public class OnHitNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.OnHit;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            bool hit = context.GetBlackboard<bool>("ProjectileHit", false);
            if (!hit)
            {
                TLog.Info("[OnHit] Projectile did not hit target.");
                return Task.FromResult(SkillNodeExecutionResult.Failed("Projectile missed."));
            }

            var savedTarget = context.GetBlackboard<IUnit>("ProjectileTarget");
            if (savedTarget != null && context.PrimaryTarget == null)
                context.PrimaryTarget = savedTarget;

            TLog.Info("[OnHit] Projectile hit confirmed.");
            return Task.FromResult(SkillNodeExecutionResult.Success());
        }
    }

    public class ApplyBuffNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.ApplyBuff;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var record = (ApplyBuffNodeRecord)node;
            var caster = context.Caster;
            var target = context.PrimaryTarget;
            var grid = context.GridController;

            if (record.BuffConfig == null)
                return Task.FromResult(SkillNodeExecutionResult.Failed("BuffConfig is null."));

            if (target == null)
                return Task.FromResult(SkillNodeExecutionResult.Failed("No target for buff application."));

            var buff = new Units.Buffs.Buff(record.BuffConfig, caster, record.Duration);
            target.AddBuff(buff);
            TLog.Info($"[ApplyBuff] Applied '{record.BuffConfig.BuffName}' to target (duration={record.Duration}).");
            return Task.FromResult(SkillNodeExecutionResult.Success());
        }
    }

    public class SelectSelfNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.SelectSelf;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            if (context.Caster == null)
                return Task.FromResult(SkillNodeExecutionResult.Failed("No caster for SelectSelf."));

            context.PrimaryTarget = context.Caster;
            TLog.Info("[SelectSelf] Target set to caster.");
            return Task.FromResult(SkillNodeExecutionResult.Success());
        }
    }

    public class SelectAllyNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.SelectAlly;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var record = (SelectAllyNodeRecord)node;
            var caster = context.Caster;
            var grid = context.GridController;

            if (context.PrimaryTarget != null)
                return Task.FromResult(SkillNodeExecutionResult.Success());

            var allies = grid.UnitManager.GetFriendlyUnits(grid.TurnContext.CurrentPlayer);
            Units.IUnit bestAlly = null;
            int bestDist = int.MaxValue;

            foreach (var ally in allies)
            {
                if (ally == null || ally.CurrentCell == null) continue;
                if (ReferenceEquals(ally, caster)) continue;
                int dist = ally.CurrentCell.GetDistance(caster.CurrentCell);
                if (dist <= record.MaxRange && dist < bestDist)
                {
                    bestDist = dist;
                    bestAlly = ally;
                }
            }

            if (bestAlly == null)
                return Task.FromResult(SkillNodeExecutionResult.Failed("No valid ally in range."));

            context.PrimaryTarget = bestAlly;
            TLog.Info($"[SelectAlly] Target set to ally at distance {bestDist}.");
            return Task.FromResult(SkillNodeExecutionResult.Success());
        }
    }

    public class ApplyHealNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.ApplyHeal;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var record = (ApplyHealNodeRecord)node;
            var caster = context.Caster;
            var target = context.PrimaryTarget;

            if (target == null)
                return Task.FromResult(SkillNodeExecutionResult.Failed("No target for heal."));

            float healAmount = record.HealAmount;
            float maxHeal = target.MaxHealth - target.Health;
            float actualHeal = UnityEngine.Mathf.Min(healAmount, maxHeal);

            target.ModifyHealth(actualHeal, caster);
            TLog.Info($"[ApplyHeal] Healed target for {actualHeal} HP.");
            return Task.FromResult(SkillNodeExecutionResult.Success());
        }
    }

    public class DashToAllyNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.DashToAlly;

        public async Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var record = (DashToAllyNodeRecord)node;
            var caster = context.Caster;
            var target = context.PrimaryTarget;
            var grid = context.GridController;

            if (target?.CurrentCell == null || caster?.CurrentCell == null)
                return SkillNodeExecutionResult.Failed("Invalid caster or ally target for dash.");

            int distance = target.CurrentCell.GetDistance(caster.CurrentCell);
            if (distance > record.MaxRange)
                return SkillNodeExecutionResult.Failed($"Ally out of dash range ({distance} > {record.MaxRange}).");

            var targetCell = target.CurrentCell;
            var casterCell = caster.CurrentCell;
            var (dirX, dirY) = GetCardinalDirection(casterCell.GridCoordinates, targetCell.GridCoordinates);
            if (dirX == 0 && dirY == 0)
                return SkillNodeExecutionResult.Failed("Dash destination must be on the same row or column.");

            var (stopCell, hitUnit) = FindDashStopCell(casterCell, dirX, dirY, distance, grid);
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
            caster.InvokeUnitMoved(new Units.UnitMovedEventArgs(caster, casterCell, stopCell, path));

            if (hitUnit != null && hitUnit.CurrentCell.GetDistance(stopCell) <= 1)
            {
                context.PrimaryTarget = hitUnit;
                TLog.Info($"[DashToAlly] Hit unit at stop cell, target set for heal.");
            }

            TLog.Info($"[DashToAlly] Dashed to ({stopCell.GridCoordinates.x}, {stopCell.GridCoordinates.y})");
            return SkillNodeExecutionResult.Success();
        }

        private (int dx, int dy) GetCardinalDirection(Common.Utilities.Vector2IntImpl from, Common.Utilities.Vector2IntImpl to)
        {
            int dx = to.x - from.x;
            int dy = to.y - from.y;
            if (dx != 0 && dy != 0) return (0, 0);
            return (dx == 0 ? 0 : (dx > 0 ? 1 : -1),
                    dy == 0 ? 0 : (dy > 0 ? 1 : -1));
        }

        private (Cells.ICell stopCell, Units.IUnit hitUnit) FindDashStopCell(Cells.ICell start, int dirX, int dirY, int maxDist, Controllers.IGridController grid)
        {
            Cells.ICell lastValid = start;
            Units.IUnit hitUnit = null;
            for (int i = 1; i <= maxDist; i++)
            {
                var coord = new Common.Utilities.Vector2IntImpl(
                    start.GridCoordinates.x + dirX * i,
                    start.GridCoordinates.y + dirY * i);
                var cell = grid.CellManager.GetCellAt(coord);
                if (cell == null) break;
                if (!grid.CellManager.IsCellWalkable(cell)) break;
                if (cell.CurrentUnits.Count > 0)
                {
                    lastValid = GetRelativeCell(start, dirX, dirY, i - 1, grid);
                    hitUnit = cell.CurrentUnits[0];
                    break;
                }
                lastValid = cell;
            }
            if (lastValid == start && hitUnit == null)
                lastValid = GetRelativeCell(start, dirX, dirY, maxDist, grid);
            return (lastValid, hitUnit);
        }

        private Cells.ICell GetRelativeCell(Cells.ICell origin, int dirX, int dirY, int steps, Controllers.IGridController grid)
        {
            if (steps == 0) return origin;
            var coord = new Common.Utilities.Vector2IntImpl(
                origin.GridCoordinates.x + dirX * steps,
                origin.GridCoordinates.y + dirY * steps);
            return grid.CellManager.GetCellAt(coord);
        }

        private System.Collections.Generic.List<Cells.ICell> BuildStraightLinePath(Cells.ICell start, Cells.ICell end, int dirX, int dirY, Controllers.IGridController grid)
        {
            var path = new System.Collections.Generic.List<Cells.ICell>();
            int steps = UnityEngine.Mathf.Max(
                UnityEngine.Mathf.Abs(end.GridCoordinates.x - start.GridCoordinates.x),
                UnityEngine.Mathf.Abs(end.GridCoordinates.y - start.GridCoordinates.y));
            for (int i = 1; i <= steps; i++)
            {
                var coord = new Common.Utilities.Vector2IntImpl(
                    start.GridCoordinates.x + dirX * i,
                    start.GridCoordinates.y + dirY * i);
                var cell = grid.CellManager.GetCellAt(coord);
                if (cell != null)
                    path.Add(cell);
            }
            return path;
        }
    }
}
