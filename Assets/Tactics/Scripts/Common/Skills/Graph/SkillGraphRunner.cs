using System.Collections.Generic;
using System.Threading.Tasks;
using Tactics.Runtime.Utilities;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// 技能图运行器。
    /// 逐节点解释执行 SkillGraph。
    /// </summary>
    public class SkillGraphRunner
    {
        /// <summary>
        /// 执行整个技能图。
        /// </summary>
        public async Task<SkillGraphExecutionState> Execute(SkillExecutionContext context)
        {
            if (context.RuntimeDef.EntryNodeId == null)
            {
                context.Abort("No entry node in skill graph.");
                TLog.Warning("[SkillGraphRunner] No entry node.");
                return context.State;
            }

            // 将执行任务注册到 RuntimeScope
            context.RuntimeScope?.Track(Task.CurrentId.HasValue ? Task.CompletedTask : Task.CompletedTask);

            int stageIndex = 0;

            while (context.IsRunning)
            {
                // 使用 RuntimeScope 的 CancellationToken，如果没有则使用 context 的
                var cancellationToken = context.RuntimeScope?.Token ?? context.CancellationToken;
                if (cancellationToken.IsCancellationRequested)
                {
                    context.Abort("Execution cancelled.");
                    TLog.Info($"[SkillGraphRunner] Cancelled at node '{context.CurrentNodeId}'.");
                    return context.State;
                }

                context.StepCount++;

                if (context.StepCount > context.MaxSteps)
                {
                    context.Abort($"Exceeded max step count ({context.MaxSteps}). Possible infinite loop.");
                    TLog.Warning($"[SkillGraphRunner] Aborted: max steps exceeded at node '{context.CurrentNodeId}'.");
                    return context.State;
                }

                var currentNode = context.RuntimeDef.GetNode(context.CurrentNodeId);
                if (currentNode == null)
                {
                    context.Abort($"Node '{context.CurrentNodeId}' not found.");
                    TLog.Warning($"[SkillGraphRunner] Aborted: node '{context.CurrentNodeId}' not found.");
                    return context.State;
                }

                if (!currentNode.Enabled)
                {
                    SkipToNext(context, currentNode);
                    continue;
                }

                var executor = SkillNodeExecutorRegistry.Get(currentNode.NodeType);
                if (executor == null)
                {
                    context.Abort($"No executor for node type '{currentNode.NodeType}'.");
                    TLog.Warning($"[SkillGraphRunner] Aborted: no executor for '{currentNode.NodeType}'.");
                    return context.State;
                }

                SkillNodeExecutionResult result;
                try
                {
                    result = await executor.Execute(currentNode, context);
                }
                catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (System.Exception ex)
                {
                    context.Abort($"Exception in node '{currentNode.NodeId}': {ex.Message}");
                    TLog.Error($"[SkillGraphRunner] Exception in node '{currentNode.NodeId}': {ex}");
                    return context.State;
                }

                context.RecordStage(stageIndex++, currentNode.NodeId,
                    result.IsFailed ? SkillGraphExecutionState.Failed :
                    SkillGraphExecutionState.Completed,
                    result.FailReason);

                if (result.IsFailed)
                {
                    context.Fail(result.FailReason);
                    TLog.Info($"[SkillGraphRunner] Failed at node '{currentNode.NodeId}': {result.FailReason}");
                    return context.State;
                }

                if (result.IsCompleted)
                {
                    context.Complete();
                    TLog.Info($"[SkillGraphRunner] Completed at node '{currentNode.NodeId}'.");
                    return context.State;
                }

                if (result.IsWaiting)
                {
                    TLog.Info($"[SkillGraphRunner] Waiting at node '{currentNode.NodeId}'.");
                    return context.State;
                }

                // Navigate to next node
                if (!AdvanceToNext(context, currentNode, result))
                {
                    context.Abort($"No outgoing edge from node '{currentNode.NodeId}'.");
                    TLog.Warning($"[SkillGraphRunner] Aborted: no outgoing edge from '{currentNode.NodeId}'.");
                    return context.State;
                }
            }

            return context.State;
        }

        private bool AdvanceToNext(SkillExecutionContext context, SkillGraphNodeRecord currentNode, SkillNodeExecutionResult result)
        {
            string portName = result.BranchPort;
            var edges = context.RuntimeDef.GetEdgesFrom(currentNode.NodeId);

            if (edges.Count == 0)
                return false;

            // If branch result, find matching port
            if (!string.IsNullOrEmpty(portName))
            {
                for (int i = 0; i < edges.Count; i++)
                {
                    if (edges[i].PortType.ToString() == portName)
                    {
                        context.CurrentNodeId = edges[i].TargetNodeId;
                        return true;
                    }
                }
                // Fallback: try default port
                for (int i = 0; i < edges.Count; i++)
                {
                    if (edges[i].PortType == SkillGraphPortType.Default)
                    {
                        context.CurrentNodeId = edges[i].TargetNodeId;
                        return true;
                    }
                }
                return false;
            }

            // Default: take first edge
            context.CurrentNodeId = edges[0].TargetNodeId;
            return true;
        }

        private void SkipToNext(SkillExecutionContext context, SkillGraphNodeRecord currentNode)
        {
            var edges = context.RuntimeDef.GetEdgesFrom(currentNode.NodeId);
            if (edges.Count > 0)
                context.CurrentNodeId = edges[0].TargetNodeId;
            else
                context.Abort($"Disabled node '{currentNode.NodeId}' has no outgoing edges.");
        }
    }
}
