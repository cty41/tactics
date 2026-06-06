using System.Collections.Generic;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Utilities;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Common.Skills.Graph
{
    public class LaunchUnitNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.LaunchUnit;

        public async Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var record = (LaunchUnitNodeRecord)node;
            var caster = context.Caster;
            var target = context.PrimaryTarget;
            var grid = context.GridController;

            if (target == null)
                return SkillNodeExecutionResult.Failed("No target for launch.");
            if (target.CurrentCell == null || caster.CurrentCell == null)
                return SkillNodeExecutionResult.Failed("Invalid caster or target for launch.");

            var startCell = target.CurrentCell;
            var casterCell = caster.CurrentCell;

            // 1. 计算十字方向
            var (dirX, dirY) = GetCardinalDirection(casterCell.GridCoordinates, startCell.GridCoordinates);
            if (dirX == 0 && dirY == 0)
            {
                // 目标与施法者同格，默认向上
                dirY = -1;
            }

            // 2. 沿方向找落点
            var (landingCell, distance) = FindLandingCell(startCell, dirX, dirY, record.LaunchDistance, grid);

            if (landingCell == null || landingCell == startCell)
                return SkillNodeExecutionResult.Failed("No valid landing cell.");

            // 3. 播放主飞行抛物线动画
            await PlayFlightAnimation(target, startCell, landingCell, record.FlightHeight, record.FlightDuration);

            // 4. 检查落点是否可行走
            if (!grid.CellManager.IsCellWalkable(landingCell) || landingCell.CurrentUnits.Count > 0)
            {
                // 不可行走，需要反弹
                var nearestWalkable = FindNearestWalkableCell(landingCell, grid);
                if (nearestWalkable != null && nearestWalkable != landingCell)
                {
                    // 反弹阶段 1：低高度飞到不可行走点
                    await PlayFlightAnimation(target, startCell, landingCell, record.BounceHeight, record.BounceDuration);
                    // 反弹阶段 2：从可用格低高度落地
                    await PlayFlightAnimation(target, landingCell, nearestWalkable, record.BounceHeight * 0.5f, record.BounceDuration);
                    landingCell = nearestWalkable;
                }
                else
                {
                    // 找不到可用格，停在原地
                    landingCell = startCell;
                }
            }

            // 5. 更新目标位置
            if (landingCell != startCell)
            {
                startCell.CurrentUnits.Remove(target);
                startCell.IsTaken = startCell.CurrentUnits.Count > 0;

                target.CurrentCell = landingCell;
                if (!landingCell.CurrentUnits.Contains(target))
                    landingCell.CurrentUnits.Add(target);
                landingCell.IsTaken = true;
                target.WorldPosition = landingCell.WorldPosition;

                if (target is MonoBehaviour mb)
                    mb.transform.position = landingCell.WorldPosition.ToVector3();
            }

            // 6. 落地伤害
            if (record.LandingDamage > 0f)
            {
                var unitsAtLanding = new List<IUnit>(landingCell.CurrentUnits);
                foreach (var unit in unitsAtLanding)
                {
                    if (unit == null || ReferenceEquals(unit, target)) continue;
                    // 对落点其他单位造成伤害
                    CombatComponent.ApplyDamage(caster, unit, record.LandingDamage, false, ElementType.None,
                        canTriggerBeforeAttacked: false, canCrit: false, canTriggerDamageTaken: true);
                    TLog.Info($"[LaunchUnit] Landing damage {record.LandingDamage} to unit at landing cell.");
                }

                // 对目标也造成落地伤害
                CombatComponent.ApplyDamage(caster, target, record.LandingDamage, false, ElementType.None,
                    canTriggerBeforeAttacked: false, canCrit: false, canTriggerDamageTaken: true);
                TLog.Info($"[LaunchUnit] Landing damage {record.LandingDamage} to launched target.");
            }

            TLog.Info($"[LaunchUnit] Target launched to ({landingCell.GridCoordinates.x}, {landingCell.GridCoordinates.y}).");
            return SkillNodeExecutionResult.Success();
        }

        private (int dx, int dy) GetCardinalDirection(Vector2IntImpl from, Vector2IntImpl to)
        {
            int dx = to.x - from.x;
            int dy = to.y - from.y;
            if (dx != 0 && dy != 0) return (0, 0);
            return (dx == 0 ? 0 : (dx > 0 ? 1 : -1),
                    dy == 0 ? 0 : (dy > 0 ? 1 : -1));
        }

        private (ICell landingCell, int distance) FindLandingCell(ICell start, int dirX, int dirY, int maxDist, IGridController grid)
        {
            ICell lastValid = start;
            int actualDist = 0;
            for (int i = 1; i <= maxDist; i++)
            {
                var coord = new Vector2IntImpl(
                    start.GridCoordinates.x + dirX * i,
                    start.GridCoordinates.y + dirY * i);
                var cell = grid.CellManager.GetCellAt(coord);
                if (cell == null) break;
                if (!grid.CellManager.IsCellWalkable(cell))
                {
                    // 落到不可行走格子上，标记该格为目标（后续会反弹）
                    return (cell, i);
                }
                lastValid = cell;
                actualDist = i;
            }
            return (lastValid, actualDist);
        }

        private ICell FindNearestWalkableCell(ICell from, IGridController grid)
        {
            // BFS 找最近可行走格子
            var visited = new HashSet<ICell> { from };
            var queue = new Queue<ICell>();
            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var neighbours = current.GetNeighbours(grid.CellManager);
                foreach (var n in neighbours)
                {
                    if (visited.Contains(n)) continue;
                    visited.Add(n);
                    if (grid.CellManager.IsCellWalkable(n) && n.CurrentUnits.Count == 0)
                        return n;
                    queue.Enqueue(n);
                }
            }
            return null;
        }

        private async Task PlayFlightAnimation(IUnit target, ICell from, ICell to, float height, float duration)
        {
            if (target is not MonoBehaviour mb)
            {
                await Task.CompletedTask;
                return;
            }

            var startPos = from.WorldPosition.ToVector3();
            var endPos = to.WorldPosition.ToVector3();
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 伪 3D 抛物线：平面线性插值 + Y 轴高度偏移
                float heightOffset = 4f * height * t * (1f - t);
                var flatPos = Vector3.Lerp(startPos, endPos, t);
                mb.transform.position = flatPos + new Vector3(0, heightOffset, 0);

                await Task.Yield();
            }

            mb.transform.position = endPos;
        }
    }
}
