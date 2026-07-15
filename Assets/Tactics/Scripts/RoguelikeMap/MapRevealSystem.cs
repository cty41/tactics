using System.Collections.Generic;
using System.Linq;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.RoguelikeMap
{
    /// <summary>
    /// Forward-only limited vision fog of war system.
    /// Uses BFS from the current node along outgoing connections only.
    /// </summary>
    public class MapRevealSystem
    {
        private readonly RoguelikeMap _map;
        private readonly Dictionary<string, RoguelikeMapNode> _nodeLookup;
        private readonly float _visionRange;

        public MapRevealSystem(RoguelikeMap map, float visionRange)
        {
            _map = map;
            _visionRange = visionRange;
            _nodeLookup = map.nodes.ToDictionary(n => n.nodeId, n => n);
        }

        /// <summary>
        /// BFS from currentNodeId along outgoing connections.
        /// Nodes within 1 hop → Visibility=Revealed, IsReachable=true.
        /// Nodes within visionRange cumulative Euclidean distance but not direct → Visibility=Fogged.
        /// All other non-Visited nodes → Visibility=Hidden.
        /// Visited nodes are never reachable again.
        /// </summary>
        public void UpdateReveal(string currentNodeId)
        {
            if (!_nodeLookup.TryGetValue(currentNodeId, out _))
            {
                TLog.Warning($"[MapRevealSystem] Node not found: {currentNodeId}");
                return;
            }

            // Reset all non-Visited nodes to Hidden before recalculating
            foreach (var node in _map.nodes)
            {
                if (node.VisitState != NodeVisitState.Visited)
                {
                    node.Visibility = NodeVisibility.Hidden;
                    node.IsReachable = false;
                }
                else
                {
                    // Visited nodes: reset IsReachable (will be recalculated)
                    node.IsReachable = false;
                }
            }

            // BFS state: nodeId -> (cumulativeDistance, hopCount)
            var visited = new HashSet<string> { currentNodeId };
            var queue = new Queue<(string nodeId, float cumDist, int hops)>();
            queue.Enqueue((currentNodeId, 0f, 0));

            while (queue.Count > 0)
            {
                var (nodeId, cumDist, hops) = queue.Dequeue();

                if (!_nodeLookup.TryGetValue(nodeId, out var node))
                    continue;

                foreach (var neighborId in node.outgoing)
                {
                    if (visited.Contains(neighborId))
                        continue;

                    if (!_nodeLookup.TryGetValue(neighborId, out var neighborNode))
                        continue;

                    float edgeDist = MapReachabilityUtility.CalculateDistance(
                        node.position, neighborNode.position);
                    float newCumDist = cumDist + edgeDist;
                    int newHops = hops + 1;

                    // BFS pruning: stop if cumulative distance exceeds vision range
                    if (newCumDist > _visionRange)
                        continue;

                    visited.Add(neighborId);

                    // A visited node can remain a vision anchor, but it is never selectable again.
                    if (neighborNode.VisitState == NodeVisitState.Visited)
                    {
                        neighborNode.IsReachable = false;
                        queue.Enqueue((neighborId, newCumDist, newHops));
                        continue;
                    }

                    if (newHops == 1)
                    {
                        // Direct neighbor (1 hop) → Revealed + Reachable (clickable)
                        neighborNode.Visibility = NodeVisibility.Revealed;
                        neighborNode.IsReachable = true;
                    }
                    else if (newCumDist <= _visionRange)
                    {
                        // Within vision but not direct → Fogged (visible, not clickable)
                        neighborNode.Visibility = NodeVisibility.Fogged;
                    }

                    queue.Enqueue((neighborId, newCumDist, newHops));
                }
            }

            TLog.Info($"[MapRevealSystem] Vision from {currentNodeId}: " +
                      $"{_map.nodes.Count(n => n.IsReachable)} reachable, " +
                      $"{_map.nodes.Count(n => n.Visibility == NodeVisibility.Revealed)} revealed, " +
                      $"{_map.nodes.Count(n => n.Visibility == NodeVisibility.Fogged)} fogged, " +
                      $"range={_visionRange}");
        }

        /// <summary>
        /// Returns all nodes currently in Reachable state (clickable).
        /// </summary>
        public List<RoguelikeMapNode> GetReachableNodes()
        {
            return _map.nodes
                .Where(n => n.IsReachable && n.VisitState == NodeVisitState.Unvisited)
                .ToList();
        }
    }
}
