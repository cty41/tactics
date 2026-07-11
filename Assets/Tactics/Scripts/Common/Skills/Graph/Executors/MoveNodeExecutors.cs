using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Runtime.Utilities;

namespace Tactics.Common.Skills.Graph
{
    public class SelectMoveDestinationNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.SelectMoveDestination;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var caster = context.Caster;
            var grid = context.GridController;
            var targetPoint = context.TargetPoint;

            if (caster == null || grid == null)
                return Task.FromResult(SkillNodeExecutionResult.Failed("No caster or grid for move."));

            if (targetPoint == null)
                return Task.FromResult(SkillNodeExecutionResult.Failed("No destination selected."));

            // 可达格已经在 SkillGraphAbilityImpl 中预计算并校验
            // 这里只确认 TargetPoint 已设置
            TLog.Info($"[SelectMoveDestination] Destination set to ({targetPoint.GridCoordinates.x}, {targetPoint.GridCoordinates.y}).");
            return Task.FromResult(SkillNodeExecutionResult.Success());
        }
    }

    public class ExecuteMoveNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.ExecuteMove;

        public async Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var record = (ExecuteMoveNodeRecord)node;
            var caster = context.Caster;
            var grid = context.GridController;
            var destination = context.TargetPoint;

            if (caster == null || grid == null || destination == null)
                return SkillNodeExecutionResult.Failed("Invalid caster, grid, or destination for move.");

            // 查找路径
            var path = caster.FindPath(destination, grid.CellManager);
            if (path == null || path.Count == 0)
                return SkillNodeExecutionResult.Failed("No path to destination.");

            if (!caster.IsCellMovableTo(destination))
                return SkillNodeExecutionResult.Failed("Destination is not movable to.");

            var sourceCell = caster.CurrentCell;

            // 构造并执行移动命令（内部会处理动画、移动点扣除、位置更新）
            var moveCommand = new MoveCommand(sourceCell, destination, path);
            await moveCommand.Execute(caster, grid);

            // 标记 Move 已使用
            if (record.MarkAsBasicAbilityUsed)
                caster.MarkBasicAbilityUsed("Move");

            TLog.Info($"[ExecuteMove] Moved from ({sourceCell.GridCoordinates.x},{sourceCell.GridCoordinates.y}) to ({destination.GridCoordinates.x},{destination.GridCoordinates.y}).");
            return SkillNodeExecutionResult.Success();
        }
    }

    /// <summary>
    /// Executes a direct relocation for teleport-style skills. Unlike normal
    /// movement it intentionally skips pathfinding and movement-point costs.
    /// </summary>
    public class TeleportNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.Teleport;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var caster = context.Caster;
            var destination = context.TargetPoint;
            if (caster?.CurrentCell == null || destination == null)
                return Task.FromResult(SkillNodeExecutionResult.Failed("Invalid caster or teleport destination."));

            if (destination.IsTaken)
                return Task.FromResult(SkillNodeExecutionResult.Failed("Teleport destination is occupied."));

            var source = caster.CurrentCell;
            source.CurrentUnits.Remove(caster);
            source.IsTaken = source.CurrentUnits.Count > 0;

            caster.CurrentCell = destination;
            destination.CurrentUnits.Add(caster);
            destination.IsTaken = true;
            caster.WorldPosition = destination.WorldPosition;

            context.RecordEvent("Teleported", node.NodeId, caster);
            TLog.Info($"[Teleport] Moved caster from ({source.GridCoordinates.x},{source.GridCoordinates.y}) to ({destination.GridCoordinates.x},{destination.GridCoordinates.y}).");
            return Task.FromResult(SkillNodeExecutionResult.Success());
        }
    }
}
