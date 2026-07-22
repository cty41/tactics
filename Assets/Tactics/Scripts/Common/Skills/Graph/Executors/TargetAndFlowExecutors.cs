using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Interactables;
using Tactics.Common.Units;
using Tactics.Runtime.Utilities;

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

            // If user clicked an empty tile and downstream can consume TargetPoint (e.g. DashToTarget),
            // allow proceeding without PrimaryTarget so dash-to-point still works.
            if (context.TargetPoint != null && GraphHasDownstreamDash(context))
                return Task.FromResult(SkillNodeExecutionResult.Success());

            var enemies = grid.UnitManager.GetEnemyUnits(grid.TurnContext.CurrentPlayer);
            IUnit bestTarget = null;
            int bestDist = int.MaxValue;

            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.CurrentCell == null) continue;
                int dist = enemy.CurrentCell.GetDistance(caster.CurrentCell);
                if (dist >= record.MinRange && dist <= record.MaxRange && dist < bestDist)
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

        private static bool GraphHasDownstreamDash(SkillExecutionContext context)
        {
            var runtimeDef = context.RuntimeDef;
            if (runtimeDef == null) return false;

            var visited = new System.Collections.Generic.HashSet<string>();
            var queue = new System.Collections.Generic.Queue<string>();

            // Seed with outgoing edges from current node
            var edges = runtimeDef.GetEdgesFrom(context.CurrentNodeId);
            for (int i = 0; i < edges.Count; i++)
            {
                if (visited.Add(edges[i].TargetNodeId))
                    queue.Enqueue(edges[i].TargetNodeId);
            }

            while (queue.Count > 0)
            {
                var nodeId = queue.Dequeue();
                var node = runtimeDef.GetNode(nodeId);
                if (node is DashToTargetNodeRecord)
                    return true;

                var nextEdges = runtimeDef.GetEdgesFrom(nodeId);
                for (int i = 0; i < nextEdges.Count; i++)
                {
                    if (visited.Add(nextEdges[i].TargetNodeId))
                        queue.Enqueue(nextEdges[i].TargetNodeId);
                }
            }

            return false;
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

                int dx = cell.GridCoordinates.x - center.GridCoordinates.x;
                int dy = cell.GridCoordinates.y - center.GridCoordinates.y;
                if (record.Shape == SkillGraphAreaShape.Cross && dx != 0 && dy != 0)
                    continue;

                foreach (var unit in cell.CurrentUnits)
                {
                    if (unit != null && MatchesFaction(unit, context.Caster, record.TargetFaction))
                        targets.Add(unit);
                }
            }

            context.TargetSet = targets;
            return Task.FromResult(SkillNodeExecutionResult.Success());
        }

        private static bool MatchesFaction(Units.IUnit unit, Units.IUnit caster, SkillGraphTargetFaction faction)
        {
            if (faction == SkillGraphTargetFaction.All || caster == null)
                return true;
            bool samePlayer = unit.PlayerNumber == caster.PlayerNumber;
            return faction == SkillGraphTargetFaction.Allies ? samePlayer : !samePlayer;
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
                return Task.FromResult(SkillNodeExecutionResult.Branch("OnComplete"));
            }

            context.PrimaryTarget = context.TargetSet[index];
            context.SetBlackboard("ForEachIndex", index + 1);
            return Task.FromResult(SkillNodeExecutionResult.Success());
        }
    }

    public class SelectCorpseTargetNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.SelectCorpseTarget;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var record = (SelectCorpseTargetNodeRecord)node;
            var caster = context.Caster;
            var grid = context.GridController;

            if (context.TargetCorpses != null && context.TargetCorpses.Count > 0)
                return Task.FromResult(SkillNodeExecutionResult.Success());

            // Player/test input selects one physical corpse cell. Preserve that choice so a
            // single cast can never consume every corpse discovered by the AI fallback scan.
            var selectedCorpse = context.TargetPoint?.CurrentInteractables
                .OfType<Corpse>()
                .FirstOrDefault(corpse => !corpse.IsDestroyed);
            if (selectedCorpse != null)
            {
                context.TargetCorpses = new List<Corpse> { selectedCorpse };
                return Task.FromResult(SkillNodeExecutionResult.Success());
            }

            var corpses = new List<Corpse>();
            var allCells = grid.CellManager.GetCells();

            foreach (var cell in allCells)
            {
                if (caster?.CurrentCell != null)
                {
                    int distance = cell.GetDistance(caster.CurrentCell);
                    if (distance < record.MinRange || distance > record.MaxRange)
                        continue;
                }

                foreach (var interactable in cell.CurrentInteractables)
                {
                    if (interactable is Corpse corpse && !corpse.IsDestroyed)
                    {
                        corpses.Add(corpse);
                    }
                }
            }

            if (corpses.Count == 0)
            {
                TLog.Info("[SelectCorpseTarget] No corpses found on battlefield.");
                return Task.FromResult(SkillNodeExecutionResult.Failed("No corpses found."));
            }

            context.TargetCorpses = corpses;
            TLog.Info($"[SelectCorpseTarget] Found {corpses.Count} corpse(s).");
            return Task.FromResult(SkillNodeExecutionResult.Success());
        }
    }
}
