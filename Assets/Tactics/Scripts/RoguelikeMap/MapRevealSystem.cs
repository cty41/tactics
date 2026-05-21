using System.Collections.Generic;
using System.Linq;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.RoguelikeMap
{
    /// <summary>
    /// FTL-style limited vision fog of war system.
    /// Uses BFS from a current node along graph connections (incoming + outgoing)
    /// to determine which nodes are Revealed, Reachable, or Unrevealed.
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
        /// BFS from currentNodeId along incoming+outgoing connections.
        /// Nodes within 1 hop → Reachable.
        /// Nodes within visionRange cumulative Euclidean distance but not direct → Revealed.
        /// All other non-Visited nodes → Unrevealed.
        /// Visited nodes stay Visited and act as transparent vision anchors.
        /// </summary>
        public void UpdateReveal(string currentNodeId)
        {
            if (!_nodeLookup.TryGetValue(currentNodeId, out _))
            {
                TLog.Warning($"[MapRevealSystem] Node not found: {currentNodeId}");
                return;
            }

            // Reset all non-Visited nodes to Unrevealed before recalculating
            foreach (var node in _map.nodes)
            {
                if (node.state != NodeState.Visited)
                    node.state = NodeState.Unrevealed;
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

                // Traverse both outgoing and incoming connections
                var neighbors = new HashSet<string>();
                foreach (var outId in node.outgoing) neighbors.Add(outId);
                foreach (var inId in node.incoming) neighbors.Add(inId);

                foreach (var neighborId in neighbors)
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

                    // Visited nodes stay Visited but are transparent for further traversal
                    if (neighborNode.state == NodeState.Visited)
                    {
                        queue.Enqueue((neighborId, newCumDist, newHops));
                        continue;
                    }

                    if (newHops == 1)
                    {
                        // Direct neighbor (1 hop) → Reachable (clickable)
                        neighborNode.state = NodeState.Reachable;
                    }
                    else if (newCumDist <= _visionRange)
                    {
                        // Within vision but not direct → Revealed (visible, not clickable)
                        neighborNode.state = NodeState.Revealed;
                    }

                    queue.Enqueue((neighborId, newCumDist, newHops));
                }
            }

            TLog.Info($"[MapRevealSystem] Vision from {currentNodeId}: " +
                      $"{_map.nodes.Count(n => n.state == NodeState.Reachable)} reachable, " +
                      $"{_map.nodes.Count(n => n.state == NodeState.Revealed)} revealed, " +
                      $"range={_visionRange}");
        }

        /// <summary>
        /// Returns all nodes currently in Reachable state (clickable).
        /// </summary>
        public List<RoguelikeMapNode> GetReachableNodes()
        {
            return _map.nodes.Where(n => n.state == NodeState.Reachable).ToList();
        }
    }
}
