using System.Collections.Generic;
using System.Linq;
using Tactics.RoguelikeMap;
using Tactics.Runtime.Utilities;

namespace Tactics.RoguelikeMap
{
    /// <summary>
    /// 节点状态管理器（FTL 风格自由图模式）
    /// 使用 MapRevealSystem 进行基于 BFS 的视野计算和状态转换。
    /// </summary>
    public class NodeStateManager
    {
        private readonly RoguelikeMap _map;
        private readonly MapRevealSystem _revealSystem;
        private readonly Dictionary<string, RoguelikeMapNode> _nodeLookup;

        public NodeStateManager(RoguelikeMap map)
        {
            _map = map;
            _nodeLookup = map.nodes.ToDictionary(n => n.nodeId, n => n);
            _revealSystem = new MapRevealSystem(map, map.visionRange);
        }

        /// <summary>
        /// 初始化节点状态。
        /// 如果 visitedNodes 非空（加载存档），从最后访问节点恢复视野。
        /// 否则（新地图），从起点节点开始。
        /// </summary>
        public void InitializeStates()
        {
            if (_map.visitedNodes.Count > 0)
            {
                // 恢复存档状态：所有已访问节点标记为 Visited
                foreach (var nodeId in _map.visitedNodes)
                {
                    if (_nodeLookup.TryGetValue(nodeId, out var visitedNode))
                        visitedNode.state = NodeState.Visited;
                }

                // 从最后访问的节点计算当前视野
                var lastVisited = _map.visitedNodes.Last();
                _revealSystem.UpdateReveal(lastVisited);

                TLog.Info($"[NodeStateManager] 从存档恢复，最后访问: {lastVisited}, " +
                          $"已访问节点数: {_map.visitedNodes.Count}");
            }
            else
            {
                // 新地图：找到起点节点（无入边）
                var startNode = _map.nodes.FirstOrDefault(n => n.incoming.Count == 0);
                if (startNode == null)
                {
                    TLog.Warning("[NodeStateManager] No start node found (no node with empty incoming list).");
                    return;
                }

                // 起点设为已访问
                startNode.state = NodeState.Visited;
                _map.visitedNodes.Add(startNode.nodeId);

                // 从起点计算视野
                _revealSystem.UpdateReveal(startNode.nodeId);

                TLog.Info($"[NodeStateManager] 初始化完成（free-graph BFS 视野模式），起点: {startNode.nodeId}");
            }
        }

        /// <summary>
        /// 访问节点。
        /// 将节点设为 Visited，调用 MapRevealSystem 更新视野。
        /// </summary>
        /// <param name="nodeId">被访问的节点ID</param>
        /// <returns>新揭示或变为可到达的节点列表</returns>
        public List<RoguelikeMapNode> VisitNode(string nodeId)
        {
            if (!_nodeLookup.TryGetValue(nodeId, out var node))
            {
                TLog.Warning($"[NodeStateManager] 节点不存在: {nodeId}");
                return new List<RoguelikeMapNode>();
            }

            // 记录变更前的状态，用于返回新揭示的节点
            var previousStates = _map.nodes.ToDictionary(n => n.nodeId, n => n.state);

            // 将当前节点设为已访问
            node.state = NodeState.Visited;
            TLog.Info($"[NodeStateManager] 节点已访问: {nodeId}");

            // 使用 MapRevealSystem 重新计算视野
            _revealSystem.UpdateReveal(nodeId);

            // 返回新揭示的节点（状态从 Unrevealed 变为 Revealed 或 Reachable 的节点）
            var revealedNodes = _map.nodes
                .Where(n => n.state != NodeState.Visited
                            && previousStates.TryGetValue(n.nodeId, out var prev)
                            && prev == NodeState.Unrevealed
                            && n.state != NodeState.Unrevealed)
                .ToList();

            TLog.Info($"[NodeStateManager] 新揭示节点数: {revealedNodes.Count}");
            return revealedNodes;
        }

        /// <summary>
        /// 检查节点是否可点击（只有 Reachable 状态可点击）
        /// </summary>
        public bool IsNodeClickable(string nodeId)
        {
            if (!_nodeLookup.TryGetValue(nodeId, out var node))
                return false;

            return node.state == NodeState.Reachable;
        }

        /// <summary>
        /// 获取节点当前状态
        /// </summary>
        public NodeState GetNodeState(string nodeId)
        {
            if (!_nodeLookup.TryGetValue(nodeId, out var node))
                return NodeState.Unrevealed;

            return node.state;
        }

        /// <summary>
        /// 获取所有可到达的节点
        /// </summary>
        public List<RoguelikeMapNode> GetReachableNodes()
        {
            return _revealSystem.GetReachableNodes();
        }

        /// <summary>
        /// 获取所有已访问的节点
        /// </summary>
        public List<RoguelikeMapNode> GetVisitedNodes()
        {
            return _map.nodes.Where(n => n.state == NodeState.Visited).ToList();
        }

        /// <summary>
        /// 检查是否到达Boss节点
        /// </summary>
        public bool HasReachedBoss()
        {
            return _map.nodes.Any(n => n.nodeType == RoguelikeNodeType.Boss && n.state == NodeState.Visited);
        }

        /// <summary>
        /// 重置所有节点状态
        /// </summary>
        public void ResetStates()
        {
            foreach (var node in _map.nodes)
            {
                node.state = NodeState.Unrevealed;
            }
            TLog.Info("[NodeStateManager] 所有节点状态已重置");
        }
    }
}
