using System.Threading.Tasks;
using Tactics.Common.Units;

namespace Tactics.Common.Skills.Graph
{
    public class StartNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.Start;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            return Task.FromResult(SkillNodeExecutionResult.Success());
        }
    }

    public class SelectPrimaryTargetNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.SelectPrimaryTarget;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var record = (SelectPrimaryTargetNodeRecord)node;
            var caster = context.Caster;
            var grid = context.GridController;

            if (context.PrimaryTarget != null)
                return Task.FromResult(SkillNodeExecutionResult.Success());

            // If the user explicitly clicked a cell and there is no target on it,
            // preserve the dash-to-point intent instead of auto-binding a nearby enemy.
            if (context.TargetPoint != null)
                return Task.FromResult(SkillNodeExecutionResult.Success());

            var enemies = grid.UnitManager.GetEnemyUnits(grid.TurnContext.CurrentPlayer);
            IUnit bestTarget = null;
            int bestDist = int.MaxValue;

            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.CurrentCell == null) continue;
                int dist = enemy.CurrentCell.GetDistance(caster.CurrentCell);
                if (dist <= record.MaxRange && dist < bestDist)
                {
                    bestDist = dist;
                    bestTarget = enemy;
                }
            }

            if (bestTarget == null)
                return Task.FromResult(SkillNodeExecutionResult.Failed("No valid target in range."));

            context.PrimaryTarget = bestTarget;
            return Task.FromResult(SkillNodeExecutionResult.Success());
        }
    }

    public class SelectTargetPointNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.SelectTargetPoint;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var record = (SelectTargetPointNodeRecord)node;

            if (context.TargetPoint != null)
                return Task.FromResult(SkillNodeExecutionResult.Success());

            if (context.PrimaryTarget?.CurrentCell != null)
            {
                context.TargetPoint = context.PrimaryTarget.CurrentCell;
                return Task.FromResult(SkillNodeExecutionResult.Success());
            }

            return Task.FromResult(SkillNodeExecutionResult.Failed("No target point available."));
        }
    }

    public class CollectTargetsInAreaNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.CollectTargetsInArea;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var record = (CollectTargetsInAreaNodeRecord)node;
            var center = context.TargetPoint;

            if (center == null)
                return Task.FromResult(SkillNodeExecutionResult.Failed("No target point for area collection."));

            var grid = context.GridController;
            var targets = new System.Collections.Generic.List<Units.IUnit>();
            var cells = grid.CellManager.GetCells();

            foreach (var cell in cells)
            {
                if (cell.GetDistance(center) > record.Radius) continue;

                foreach (var unit in cell.CurrentUnits)
                {
                    if (unit != null)
                        targets.Add(unit);
                }
            }

            context.TargetSet = targets;
            return Task.FromResult(SkillNodeExecutionResult.Success());
        }
    }

    public class ForEachTargetNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.ForEachTarget;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            if (context.TargetSet == null || context.TargetSet.Count == 0)
                return Task.FromResult(SkillNodeExecutionResult.Failed("Target set is empty."));

            int index = context.GetBlackboard<int>("ForEachIndex", 0);
            if (index >= context.TargetSet.Count)
            {
                context.SetBlackboard("ForEachIndex", 0);
                return Task.FromResult(SkillNodeExecutionResult.Success());
            }

            context.PrimaryTarget = context.TargetSet[index];
            context.SetBlackboard("ForEachIndex", index + 1);
            return Task.FromResult(SkillNodeExecutionResult.Success());
        }
    }
}
