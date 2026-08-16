using System;
using System.Collections.Generic;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// Immutable structural plan shared by runtime and editor presentation backends.
    /// </summary>
    public sealed class PresentationExecutionPlan
    {
        public PresentationExecutionPlan(PresentationCueKind cue, PresentationPlanStep root)
        {
            Cue = cue;
            Root = root ?? new PresentationSequenceStep(Array.Empty<PresentationPlanStep>());
        }

        public PresentationCueKind Cue { get; }
        public PresentationPlanStep Root { get; }
    }

    public abstract class PresentationPlanStep
    {
    }

    public sealed class PresentationSequenceStep : PresentationPlanStep
    {
        public PresentationSequenceStep(IReadOnlyList<PresentationPlanStep> children)
        {
            Children = children ?? Array.Empty<PresentationPlanStep>();
        }

        public IReadOnlyList<PresentationPlanStep> Children { get; }
    }

    public sealed class PresentationParallelStep : PresentationPlanStep
    {
        public PresentationParallelStep(
            string forkNodeId,
            string joinNodeId,
            IReadOnlyList<PresentationPlanStep> branches)
        {
            ForkNodeId = forkNodeId;
            JoinNodeId = joinNodeId;
            Branches = branches ?? Array.Empty<PresentationPlanStep>();
        }

        public string ForkNodeId { get; }
        public string JoinNodeId { get; }
        public IReadOnlyList<PresentationPlanStep> Branches { get; }
    }

    public sealed class PresentationLeafStep : PresentationPlanStep
    {
        public PresentationLeafStep(PresentationNodeRecord node)
        {
            Node = node;
        }

        public PresentationNodeRecord Node { get; }
        public string NodeId => Node?.NodeId;
    }

    /// <summary>
    /// Compiles graph traversal into an explicit sequence/parallel tree without executing leaves.
    /// </summary>
    public static class PresentationExecutionPlanCompiler
    {
        public static PresentationExecutionPlan Compile(
            BattlePresentationGraph graph,
            PresentationCueKind cue)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));

            PresentationEntryNodeRecord entry = graph.FindEntry(cue);
            if (entry == null || !entry.Enabled)
                return new PresentationExecutionPlan(cue, Empty());

            PresentationPlanStep root = CompileNext(
                graph,
                entry.NodeId,
                null,
                new HashSet<string>());
            return new PresentationExecutionPlan(cue, root);
        }

        /// <summary>
        /// Compiles one node for isolated authoring preview while preserving Fork/Join context.
        /// </summary>
        /// <param name="graph">Source presentation graph.</param>
        /// <param name="nodeId">Node to preview.</param>
        /// <returns>A plan containing one leaf or the complete fork region.</returns>
        public static PresentationExecutionPlan CompileNodeScope(
            BattlePresentationGraph graph,
            string nodeId)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));
            PresentationNodeRecord node = graph.FindNode(nodeId)
                ?? throw new InvalidOperationException($"Presentation node '{nodeId}' was not found.");
            if (!node.Enabled || node is PresentationFinishNodeRecord or PresentationJoinNodeRecord)
                return new PresentationExecutionPlan(PresentationCueKind.Action, Empty());
            if (node is PresentationEntryNodeRecord entry)
                return Compile(graph, entry.Cue);
            if (node is not PresentationForkNodeRecord fork)
            {
                return new PresentationExecutionPlan(
                    PresentationCueKind.Action,
                    new PresentationLeafStep(node));
            }

            var path = new HashSet<string> { fork.NodeId };
            var branches = new List<PresentationPlanStep>();
            foreach (PresentationEdgeRecord branch in graph.GetEdgesFrom(fork.NodeId))
            {
                branches.Add(CompileNode(
                    graph,
                    graph.FindNode(branch.TargetNodeId),
                    fork.JoinNodeId,
                    new HashSet<string>(path)));
            }
            PresentationPlanStep region = Sequence(
                new PresentationLeafStep(fork),
                new PresentationParallelStep(fork.NodeId, fork.JoinNodeId, branches));
            return new PresentationExecutionPlan(PresentationCueKind.Action, region);
        }

        private static PresentationPlanStep CompileNext(
            BattlePresentationGraph graph,
            string sourceNodeId,
            string stopBeforeNodeId,
            HashSet<string> path)
        {
            List<PresentationEdgeRecord> edges = graph.GetEdgesFrom(sourceNodeId);
            if (edges.Count == 0)
                return Empty();

            return CompileNode(
                graph,
                graph.FindNode(edges[0].TargetNodeId),
                stopBeforeNodeId,
                path);
        }

        private static PresentationPlanStep CompileNode(
            BattlePresentationGraph graph,
            PresentationNodeRecord node,
            string stopBeforeNodeId,
            HashSet<string> path)
        {
            if (node == null || node.NodeId == stopBeforeNodeId ||
                node is PresentationFinishNodeRecord)
            {
                return Empty();
            }

            if (!path.Add(node.NodeId))
                throw new InvalidOperationException($"Presentation plan encountered cycle at '{node.NodeId}'.");

            try
            {
                if (!node.Enabled)
                    return CompileNext(graph, node.NodeId, stopBeforeNodeId, path);

                if (node is PresentationForkNodeRecord fork)
                {
                    var branches = new List<PresentationPlanStep>();
                    foreach (PresentationEdgeRecord branch in graph.GetEdgesFrom(fork.NodeId))
                    {
                        branches.Add(CompileNode(
                            graph,
                            graph.FindNode(branch.TargetNodeId),
                            fork.JoinNodeId,
                            new HashSet<string>(path)));
                    }

                    PresentationPlanStep continuation = CompileNext(
                        graph,
                        fork.JoinNodeId,
                        stopBeforeNodeId,
                        path);
                    return Sequence(
                        new PresentationLeafStep(fork),
                        new PresentationParallelStep(fork.NodeId, fork.JoinNodeId, branches),
                        continuation);
                }

                return Sequence(
                    new PresentationLeafStep(node),
                    CompileNext(graph, node.NodeId, stopBeforeNodeId, path));
            }
            finally
            {
                path.Remove(node.NodeId);
            }
        }

        private static PresentationPlanStep Sequence(params PresentationPlanStep[] steps)
        {
            var children = new List<PresentationPlanStep>();
            foreach (PresentationPlanStep step in steps)
            {
                if (step is PresentationSequenceStep sequence)
                    children.AddRange(sequence.Children);
                else if (step != null)
                    children.Add(step);
            }
            return new PresentationSequenceStep(children);
        }

        private static PresentationSequenceStep Empty()
        {
            return new PresentationSequenceStep(Array.Empty<PresentationPlanStep>());
        }
    }
}
