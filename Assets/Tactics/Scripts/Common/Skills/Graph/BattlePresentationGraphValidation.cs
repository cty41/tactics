using System.Collections.Generic;

namespace Tactics.Common.Skills.Graph
{
    public sealed class PresentationGraphDiagnostic
    {
        public string Code { get; set; }
        public string NodeId { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Enforces the finite, acyclic control-flow contract used by presentation graphs.
    /// </summary>
    public static class BattlePresentationGraphValidation
    {
        public static bool Validate(
            BattlePresentationGraph graph,
            out List<PresentationGraphDiagnostic> errors)
        {
            errors = new List<PresentationGraphDiagnostic>();
            if (graph == null)
            {
                errors.Add(Error("MissingGraph", null, "Presentation graph is missing."));
                return false;
            }

            var nodeIds = new HashSet<string>();
            var entries = new HashSet<PresentationCueKind>();
            foreach (PresentationNodeRecord node in graph.Nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
                {
                    errors.Add(Error("MissingNodeId", null, "Every node requires an id."));
                    continue;
                }
                if (!nodeIds.Add(node.NodeId))
                    errors.Add(Error("DuplicateNodeId", node.NodeId, "Node ids must be unique."));
                if (node is PresentationEntryNodeRecord entry && !entries.Add(entry.Cue))
                    errors.Add(Error("DuplicateEntry", node.NodeId, $"Only one {entry.Cue} entry is allowed."));
            }

            var outgoingCounts = new Dictionary<string, int>();
            foreach (PresentationEdgeRecord edge in graph.Edges)
            {
                if (edge == null || !nodeIds.Contains(edge.SourceNodeId) || !nodeIds.Contains(edge.TargetNodeId))
                {
                    errors.Add(Error("InvalidEdge", null, "Every edge must connect two existing nodes."));
                    continue;
                }
                outgoingCounts.TryGetValue(edge.SourceNodeId, out int count);
                outgoingCounts[edge.SourceNodeId] = count + 1;
            }

            foreach (PresentationNodeRecord node in graph.Nodes)
            {
                if (node == null)
                    continue;
                outgoingCounts.TryGetValue(node.NodeId, out int outgoing);
                if (node is PresentationFinishNodeRecord && outgoing > 0)
                    errors.Add(Error("FinishHasOutgoing", node.NodeId, "Finish nodes cannot have outgoing edges."));
                if (node is not PresentationFinishNodeRecord && outgoing == 0)
                    errors.Add(Error("MissingOutgoing", node.NodeId, "Every non-Finish node requires an outgoing edge."));
                if (node is not PresentationForkNodeRecord && node is not PresentationFinishNodeRecord && outgoing > 1)
                    errors.Add(Error("MultipleOutgoing", node.NodeId, "Only Fork nodes may have multiple outputs."));
                if (node is PresentationForkNodeRecord fork)
                {
                    if (outgoing < 2)
                        errors.Add(Error("ForkNeedsBranches", node.NodeId, "Fork requires at least two branches."));
                    if (graph.FindNode(fork.JoinNodeId) is not PresentationJoinNodeRecord)
                        errors.Add(Error("ForkMissingJoin", node.NodeId, "Fork must reference a Join node."));
                    else
                        ValidateForkBranches(graph, fork, errors);
                }
                if (node is PresentationProjectileNodeRecord projectile && projectile.Profile == null)
                    errors.Add(Error("MissingProjectileProfile", node.NodeId, "Projectile nodes require a profile."));
                if (node is PresentationPrefabFxNodeRecord prefabFx && prefabFx.Profile == null)
                    errors.Add(Error("MissingPrefabFxProfile", node.NodeId, "Prefab FX nodes require a profile."));
                if (node is PresentationProceduralVfxNodeRecord procedural && procedural.Recipe == null)
                    errors.Add(Error("MissingVfxRecipe", node.NodeId, "Procedural VFX nodes require a recipe."));
            }

            DetectCycles(graph, errors);
            ValidateReachability(graph, errors);
            ValidateRequiredMarkers(graph, errors);
            return errors.Count == 0;
        }

        private static void ValidateRequiredMarkers(
            BattlePresentationGraph graph,
            List<PresentationGraphDiagnostic> errors)
        {
            foreach (PresentationNodeRecord node in graph.Nodes)
            {
                if (node is not PresentationEntryNodeRecord entry)
                    continue;
                PresentationMarkerKind? required = entry.Cue switch
                {
                    PresentationCueKind.Action => PresentationMarkerKind.Release,
                    PresentationCueKind.Projectile => PresentationMarkerKind.Impact,
                    _ => null
                };
                if (required == null || PathEmitsMarker(graph, entry, required.Value))
                    continue;
                errors.Add(Error(
                    "MissingRequiredMarker",
                    entry.NodeId,
                    $"{entry.Cue} entry must emit {required.Value}."));
            }
        }

        private static bool PathEmitsMarker(
            BattlePresentationGraph graph,
            PresentationEntryNodeRecord entry,
            PresentationMarkerKind marker)
        {
            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(entry.NodeId);
            while (queue.Count > 0)
            {
                string nodeId = queue.Dequeue();
                if (!visited.Add(nodeId))
                    continue;
                PresentationNodeRecord node = graph.FindNode(nodeId);
                if (node == null)
                    continue;
                if (node.Enabled &&
                    node is PresentationMarkerNodeRecord markerNode &&
                    markerNode.Marker == marker)
                    return true;
                if (node.Enabled &&
                    marker == PresentationMarkerKind.Release &&
                    node is PresentationUnitTweenNodeRecord tween && tween.EmitReleaseMarker)
                {
                    return true;
                }
                if (node.Enabled &&
                    marker == PresentationMarkerKind.Impact &&
                    node is PresentationProjectileNodeRecord projectile && projectile.EmitImpactMarker)
                {
                    return true;
                }
                foreach (PresentationEdgeRecord edge in graph.GetEdgesFrom(nodeId))
                    queue.Enqueue(edge.TargetNodeId);
            }
            return false;
        }

        private static void ValidateForkBranches(
            BattlePresentationGraph graph,
            PresentationForkNodeRecord fork,
            List<PresentationGraphDiagnostic> errors)
        {
            foreach (PresentationEdgeRecord branch in graph.GetEdgesFrom(fork.NodeId))
            {
                var visited = new HashSet<string>();
                var queue = new Queue<string>();
                queue.Enqueue(branch.TargetNodeId);
                bool reachesJoin = false;
                while (queue.Count > 0)
                {
                    string nodeId = queue.Dequeue();
                    if (!visited.Add(nodeId))
                        continue;
                    if (nodeId == fork.JoinNodeId)
                    {
                        reachesJoin = true;
                        break;
                    }
                    foreach (PresentationEdgeRecord edge in graph.GetEdgesFrom(nodeId))
                        queue.Enqueue(edge.TargetNodeId);
                }
                if (!reachesJoin)
                {
                    errors.Add(Error(
                        "ForkBranchMissesJoin",
                        fork.NodeId,
                        $"Every Fork branch must reach Join '{fork.JoinNodeId}'."));
                    return;
                }
            }
        }

        private static void DetectCycles(
            BattlePresentationGraph graph,
            List<PresentationGraphDiagnostic> errors)
        {
            var states = new Dictionary<string, int>();
            foreach (PresentationNodeRecord node in graph.Nodes)
            {
                if (node != null && !states.ContainsKey(node.NodeId))
                    Visit(node.NodeId, graph, states, errors);
            }
        }

        private static void Visit(
            string nodeId,
            BattlePresentationGraph graph,
            Dictionary<string, int> states,
            List<PresentationGraphDiagnostic> errors)
        {
            states[nodeId] = 1;
            foreach (PresentationEdgeRecord edge in graph.GetEdgesFrom(nodeId))
            {
                states.TryGetValue(edge.TargetNodeId, out int state);
                if (state == 1)
                {
                    errors.Add(Error("CycleDetected", edge.TargetNodeId, "Presentation graphs cannot contain cycles."));
                    continue;
                }
                if (state == 0)
                    Visit(edge.TargetNodeId, graph, states, errors);
            }
            states[nodeId] = 2;
        }

        private static void ValidateReachability(
            BattlePresentationGraph graph,
            List<PresentationGraphDiagnostic> errors)
        {
            var reachable = new HashSet<string>();
            var queue = new Queue<string>();
            foreach (PresentationNodeRecord node in graph.Nodes)
            {
                if (node is PresentationEntryNodeRecord)
                {
                    reachable.Add(node.NodeId);
                    queue.Enqueue(node.NodeId);
                }
            }
            if (queue.Count == 0)
                errors.Add(Error("MissingEntry", null, "At least one Entry node is required."));

            while (queue.Count > 0)
            {
                foreach (PresentationEdgeRecord edge in graph.GetEdgesFrom(queue.Dequeue()))
                {
                    if (reachable.Add(edge.TargetNodeId))
                        queue.Enqueue(edge.TargetNodeId);
                }
            }
            foreach (PresentationNodeRecord node in graph.Nodes)
            {
                if (node != null && !reachable.Contains(node.NodeId))
                    errors.Add(Error("UnreachableNode", node.NodeId, "Node is not reachable from an Entry."));
            }
        }

        private static PresentationGraphDiagnostic Error(string code, string nodeId, string message)
        {
            return new PresentationGraphDiagnostic { Code = code, NodeId = nodeId, Message = message };
        }
    }
}
