using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Tactics.AssetPipeline;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Interactables;
using Tactics.Common.Units;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Utilities;
using Tactics.Runtime.BattleLog;
using Tactics.Runtime.Utilities;
using UnityEngine;

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
                var approachCell = distance > 1
                    ? GetRelativeCell(casterCell, dirX, dirY, distance - 1, grid)
                    : casterCell;

                if (approachCell != null && approachCell != casterCell)
                    await MoveUnitAsync(caster, casterCell, approachCell, grid);

                await MoveUnitAsync(target, targetCell, retreatCell, grid);

                var fromCell = (approachCell != null && approachCell != casterCell) ? approachCell : casterCell;
                await MoveUnitAsync(caster, fromCell, targetCell, grid);

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
                attacker, target, ChargeCollisionDamage, false, DamageCategory.Physical, ElementType.None,
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

            context.SetBlackboard("HasLastDamageResolution", false);
            context.SetBlackboard<IUnit>("LastDamageTarget", null);

            if (target == null)
            {
                TLog.Info("[ApplyDamage] No target present, skipping damage application.");
                return Task.FromResult(SkillNodeExecutionResult.Success());
            }

            // 命中率惩罚检查（HeavyShot 等技能）
            if (record.AccuracyPenalty > 0f && !CombatComponent.IsHit(caster, target, record.AccuracyPenalty))
            {
                TLog.Info($"[ApplyDamage] Attack missed (accuracyPenalty={record.AccuracyPenalty}).");
                context.SetBlackboard("HasLastDamageResolution", true);
                context.SetBlackboard("LastDamageHit", false);
                context.SetBlackboard("LastDamageTarget", target);
                return Task.FromResult(SkillNodeExecutionResult.Success());
            }

            var resolution = CombatComponent.ApplyDamage(
                caster, target, record.BaseDamage, record.IsRanged,
                record.DamageType == SkillGraphDamageType.Physical
                    ? DamageCategory.Physical
                    : DamageCategory.Magic,
                record.ElementType,
                canTriggerBeforeAttacked: true,
                canCrit: record.CanCrit,
                canTriggerDamageTaken: true);

            context.SetBlackboard("HasLastDamageResolution", true);
            context.SetBlackboard("LastDamageHit", resolution.WasHit);
            context.SetBlackboard("LastDamageTarget", target);

            TLog.Info($"[ApplyDamage] Dealt {record.BaseDamage} damage to target.");
            return Task.FromResult(SkillNodeExecutionResult.Success());
        }
    }

    /// <summary>
    /// Resolves consecutive close-range thrusts against the graph's primary target.
    /// </summary>
    public class MultiStabNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.MultiStab;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var record = (MultiStabNodeRecord)node;
            var caster = context.Caster;
            var target = context.PrimaryTarget;
            if (caster == null || target == null)
                return Task.FromResult(SkillNodeExecutionResult.Failed("No caster or target for multi stab."));

            int segments = Mathf.Max(1, record.SegmentCount);
            for (int i = 0; i < segments; i++)
            {
                CombatComponent.ApplyDamage(
                    caster, target, record.DamagePerSegment, false, DamageCategory.Physical, ElementType.None,
                    canTriggerBeforeAttacked: true,
                    canCrit: true,
                    canTriggerDamageTaken: true);
                context.RecordEvent("MultiStabHit", node.NodeId, target);
            }

            TLog.Info($"[MultiStab] Resolved {segments} segments against {target.UnitID}.");
            return Task.FromResult(SkillNodeExecutionResult.Success());
        }
    }

    public class ApplyShieldNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.ApplyShield;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var record = (ApplyShieldNodeRecord)node;
            var caster = context.Caster;
            if (caster == null) return Task.FromResult(SkillNodeExecutionResult.Failed("No caster for shield."));
            CombatComponent.ApplyDamageShield(caster, caster.Charisma * record.AttributeMultiplier);
            context.RecordEvent("ShieldApplied", node.NodeId, caster);
            TLog.Info($"[ApplyShield] Applied {caster.Charisma * record.AttributeMultiplier} shield.");
            return Task.FromResult(SkillNodeExecutionResult.Success());
        }
    }

    public class ApplyKnockbackNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.ApplyKnockback;

        public async Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var record = (ApplyKnockbackNodeRecord)node;
            var caster = context.Caster;
            var target = context.PrimaryTarget;
            var grid = context.GridController;

            if (target?.CurrentCell == null || caster?.CurrentCell == null)
            {
                TLog.Info("[ApplyKnockback] No target present, skipping knockback.");
                return SkillNodeExecutionResult.Success();
            }

            var targetMb = target as UnityEngine.MonoBehaviour;
            if (targetMb == null)
            {
                TLog.Info("[ApplyKnockback] Target destroyed, skipping knockback.");
                return SkillNodeExecutionResult.Success();
            }

            var targetCell = target.CurrentCell;
            var casterCell = caster.CurrentCell;

            int dx = targetCell.GridCoordinates.x - casterCell.GridCoordinates.x;
            int dy = targetCell.GridCoordinates.y - casterCell.GridCoordinates.y;
            float mag = UnityEngine.Mathf.Sqrt(dx * dx + dy * dy);
            if (mag < 0.01f)
                return SkillNodeExecutionResult.Failed("Cannot determine knockback direction.");

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
                var startWorldPos = targetCell.WorldPosition.ToVector3();
                var endWorldPos = knockCell.WorldPosition.ToVector3();

                targetCell.CurrentUnits.Remove(target);
                targetCell.IsTaken = targetCell.CurrentUnits.Count > 0;
                target.CurrentCell = knockCell;
                if (!knockCell.CurrentUnits.Contains(target))
                    knockCell.CurrentUnits.Add(target);
                knockCell.IsTaken = true;

                if (targetMb != null && record.Duration > 0f)
                {
                    float elapsed = 0f;
                    while (elapsed < record.Duration)
                    {
                        elapsed += UnityEngine.Time.deltaTime;
                        float t = UnityEngine.Mathf.Clamp01(elapsed / record.Duration);
                        var pos = UnityEngine.Vector3.Lerp(startWorldPos, endWorldPos, t);
                        if (targetMb == null) break;
                        targetMb.transform.position = pos;
                        await System.Threading.Tasks.Task.Yield();
                    }
                }

                if (targetMb != null)
                    targetMb.transform.position = endWorldPos;

                TLog.Info($"[ApplyKnockback] Knocked target to ({knockCell.GridCoordinates.x}, {knockCell.GridCoordinates.y})");
            }
            else
            {
                TLog.Info("[ApplyKnockback] Knockback destination blocked, target stays in place.");
            }

            return SkillNodeExecutionResult.Success();
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
            var cancellationToken = context.RuntimeScope?.Token ?? context.CancellationToken;
            await Task.Delay((int)(travelTime * 1000), cancellationToken);

            context.SetBlackboard("ProjectileHit", true);
            context.SetBlackboard("ProjectileTarget", target);
            context.RecordEvent("ProjectileHit", node.NodeId, target);

            if (record.DropOnHit)
            {
                var dropCell = FindNearestEmptyCell(target.CurrentCell, grid, record.DropSearchRadius);
                if (dropCell != null)
                {
                    context.SetBlackboard("ProjectileDropCell", dropCell);
                    context.RecordEventAtCell("ProjectileDropped", node.NodeId, dropCell);
                    TLog.Info($"[ProjectileLaunch] Projectile dropped near target at ({dropCell.GridCoordinates.x},{dropCell.GridCoordinates.y}).");
                }
            }

            TLog.Info("[ProjectileLaunch] Projectile reached target.");
            return SkillNodeExecutionResult.Success();
        }

        private static Cells.ICell FindNearestEmptyCell(Cells.ICell origin, Controllers.IGridController grid, int radius)
        {
            if (origin == null || grid?.CellManager == null) return null;
            var candidates = new List<Cells.ICell>();
            foreach (var cell in grid.CellManager.GetCells())
            {
                if (cell == null || cell == origin) continue;
                if (cell.GetDistance(origin) > Mathf.Max(1, radius)) continue;
                if (cell.CurrentUnits.Count != 0 || !grid.CellManager.IsCellWalkable(cell)) continue;
                candidates.Add(cell);
            }
            candidates.Sort((a, b) => a.GetDistance(origin).CompareTo(b.GetDistance(origin)));
            return candidates.Count > 0 ? candidates[0] : null;
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

            if (record.RequiresSuccessfulHit)
            {
                bool hasResolution = context.GetBlackboard<bool>("HasLastDamageResolution", false);
                var damageTarget = context.GetBlackboard<IUnit>("LastDamageTarget", null);
                if (!hasResolution || !ReferenceEquals(damageTarget, target))
                    return Task.FromResult(SkillNodeExecutionResult.Failed(
                        $"Buff '{record.BuffConfig.BuffName}' requires a damage result for the same target."));

                if (!context.GetBlackboard<bool>("LastDamageHit", false))
                {
                    TLog.Info($"[ApplyBuff] Skipped '{record.BuffConfig.BuffName}' because the attached hit failed.");
                    return Task.FromResult(SkillNodeExecutionResult.Success());
                }
            }

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
                if (!record.IncludeSelf && ReferenceEquals(ally, caster)) continue;
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

            float healthBefore = target.Health;
            float requestedHeal = UnityEngine.Mathf.Min(record.HealAmount, target.MaxHealth - healthBefore);
            target.ModifyHealth(requestedHeal, caster);
            float actualHeal = UnityEngine.Mathf.Max(0f, target.Health - healthBefore);
            TLog.Info($"[ApplyHeal] Healed target for {actualHeal} HP.");

            if (TBattleLog.IsBattleActive)
            {
                TBattleLog.Log(new HealLogData
                {
                    Healer = GetUnitName(caster),
                    Target = GetUnitName(target),
                    HealAmount = actualHeal,
                    RemainingHealth = target.Health
                });
            }

            return Task.FromResult(SkillNodeExecutionResult.Success());
        }

        private static string GetUnitName(IUnit unit)
        {
            if (unit is INamedUnit named && !string.IsNullOrWhiteSpace(named.UnitName))
                return named.UnitName;

            return unit == null ? "Unknown" : $"Unit_{unit.UnitID}";
        }
    }

    public class ApplyManaNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.ApplyMana;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var record = (ApplyManaNodeRecord)node;
            var target = context.PrimaryTarget;
            if (target == null)
                return Task.FromResult(SkillNodeExecutionResult.Failed("No target for mana restoration."));

            float restoredMana = UnityEngine.Mathf.Min(record.ManaAmount, target.MaxMana - target.Mana);
            target.Mana += restoredMana;
            TLog.Info($"[ApplyMana] Restored {restoredMana} MP.");
            return Task.FromResult(SkillNodeExecutionResult.Success());
        }
    }

    public class RemoveHarmfulBuffsNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.RemoveHarmfulBuffs;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var target = context.PrimaryTarget;
            if (target == null)
                return Task.FromResult(SkillNodeExecutionResult.Failed("No target for cleanse."));

            var harmfulBuffs = target.GetActiveBuffs()
                .Where(buff => buff?.Config?.Polarity == BuffPolarity.Harmful)
                .ToList();

            foreach (var buff in harmfulBuffs)
                target.RemoveBuff(buff);

            context.SetBlackboard("RemovedHarmfulBuffCount", harmfulBuffs.Count);
            TLog.Info($"[RemoveHarmfulBuffs] Removed {harmfulBuffs.Count} harmful effects.");
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

    public class SummonUnitNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.SummonUnit;

        public async Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var record = (SummonUnitNodeRecord)node;
            var caster = context.Caster;
            var grid = context.GridController;
            var corpses = context.TargetCorpses;

            if (record.RequiresCorpse && (corpses == null || corpses.Count == 0))
                return SkillNodeExecutionResult.Failed("No corpses to summon from.");

            int summoned = 0;
            var spawnCells = record.RequiresCorpse
                ? corpses.Select(c => c?.CurrentCell).Where(c => c != null).ToList()
                : new List<ICell> { FindNearestEmptyCell(caster.CurrentCell, grid, 1) };

            foreach (var spawnCell in spawnCells)
            {
                var corpse = record.RequiresCorpse
                    ? corpses.FirstOrDefault(c => c != null && !c.IsDestroyed && c.CurrentCell == spawnCell)
                    : null;
                if (record.RequiresCorpse && corpse == null) continue;

                ICell corpseCell = spawnCell;
                if (corpseCell == null) continue;
                corpse?.Consume();

                GameObject prefab = null;
                if (!string.IsNullOrEmpty(record.UnitPrefabPath))
                {
                    var mgr = GameAssetManager.Instance;
                    if (mgr != null)
                    {
                        var path = GameAssetManager.NormalizeAssetPath(record.UnitPrefabPath);
                        prefab = mgr.Load<GameObject>(path);
                    }
                }

                if (prefab == null)
                {
                    TLog.Error($"[SummonUnit] Prefab not found: {record.UnitPrefabPath}");
                    continue;
                }

                var container = grid.UnitManager?.ContainerTransform;
                var go = UnityEngine.Object.Instantiate(prefab, corpseCell.WorldPosition.ToVector3(), UnityEngine.Quaternion.identity, container);
                if (!string.IsNullOrEmpty(record.SummonName))
                    go.name = record.SummonName;
                var unit = go.GetComponent<IUnit>();
                if (unit != null)
                {
                    unit.CanReceiveHealing = record.CanReceiveHealing;
                    unit.OwnerUnitId = caster.UnitID;
                    unit.PlayerNumber = caster.PlayerNumber;
                    unit.CurrentCell = corpseCell;
                    corpseCell.CurrentUnits.Add(unit);
                    corpseCell.IsTaken = true;

                    grid.UnitManager.AddUnit(unit);
                    try
                    {
                        unit.Initialize(gridController: grid);
                    }
                    catch (UnassignedReferenceException) when (grid is Testing.SkillGraphTestGridController)
                    {
                        // Production TilemapUnit prefabs receive their tilemap from the scene.
                        // The lightweight graph test world intentionally has no tilemap scene;
                        // the base Unit state initialized above is sufficient for behavior tests.
                        TLog.Warning($"[SummonUnit] Skipped scene-only TilemapUnit initialization for test summon '{go.name}'.");
                    }

                    unit.Facing = caster.Facing;
                    var registry = SummonRegistry.For(grid);
                    var replacements = registry?.Register(
                        caster,
                        record.SummonCategory,
                        unit,
                        record.MaxActive) ?? new List<IUnit>();
                    foreach (var replacement in replacements)
                        registry.Despawn(replacement);

                    if (registry == null)
                    {
                        unit.OwnerUnit = caster;
                        unit.OwnerUnitId = caster.UnitID;
                        caster.SummonedUnit = unit;
                    }

                    summoned++;
                    TLog.Info($"[SummonUnit] Unit summoned for caster {caster.UnitID} at {corpseCell.GridCoordinates}");

                    if (TBattleLog.IsBattleActive)
                    {
                        TBattleLog.Log(new SkillLogData
                        {
                            Source = GetUnitName(caster),
                            SkillName = string.IsNullOrWhiteSpace(record.SummonName) ? "Summon" : $"Summon {record.SummonName}",
                            Target = GetUnitName(unit)
                        });
                    }
                }
                else
                {
                    TLog.Error($"[SummonUnit] Prefab missing IUnit component: {record.UnitPrefabPath}");
                    UnityEngine.Object.Destroy(go);
                }
            }

            if (summoned == 0)
                return SkillNodeExecutionResult.Failed("No units summoned.");

            return SkillNodeExecutionResult.Success();
        }

        private static string GetUnitName(IUnit unit)
        {
            if (unit is INamedUnit named && !string.IsNullOrWhiteSpace(named.UnitName))
                return named.UnitName;

            return unit == null ? "Unknown" : $"Unit_{unit.UnitID}";
        }

        private static ICell FindNearestEmptyCell(ICell origin, IGridController grid, int radius)
        {
            if (origin == null || grid?.CellManager == null) return null;
            var candidates = grid.CellManager.GetCells()
                .Where(cell => cell != null && cell != origin && cell.GetDistance(origin) <= Mathf.Max(1, radius)
                    && cell.CurrentUnits.Count == 0 && grid.CellManager.IsCellWalkable(cell))
                .OrderBy(cell => cell.GetDistance(origin))
                .ToList();
            return candidates.FirstOrDefault();
        }
    }
}
