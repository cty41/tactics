using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.RoguelikeMap;
using Tactics.Runtime.Utilities;

namespace Tactics.RoguelikeMap
{
    /// <summary>
    /// Forward-only node state manager.
    /// Uses MapRevealSystem to reveal outgoing choices and permanently closes visited nodes.
    /// </summary>
    public class NodeStateManager
    {
        private readonly RoguelikeMap _map;
        private readonly MapRevealSystem _revealSystem;
        private readonly Dictionary<string, RoguelikeMapNode> _nodeLookup;
        private readonly string _currentNodeIdHint;

        public string CurrentNodeId { get; private set; }

        public NodeStateManager(RoguelikeMap map, string currentNodeIdHint = null)
        {
            _map = map;
            _nodeLookup = map.nodes.ToDictionary(n => n.nodeId, n => n);
            _revealSystem = new MapRevealSystem(map, map.visionRange);
            _currentNodeIdHint = currentNodeIdHint;
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
                        visitedNode.VisitState = NodeVisitState.Visited;
                }

                // 从最后访问的节点计算当前视野
                CurrentNodeId = ResolveCurrentNodeId();
                _map.currentNodeId = CurrentNodeId;
                _revealSystem.UpdateReveal(CurrentNodeId);

                TLog.Info($"[NodeStateManager] 从存档恢复，最后访问: {CurrentNodeId}, " +
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
                startNode.VisitState = NodeVisitState.Visited;
                _map.visitedNodes.Add(startNode.nodeId);
                _map.currentNodeId = startNode.nodeId;
                CurrentNodeId = startNode.nodeId;

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

            if (node.VisitState == NodeVisitState.Visited || !node.IsReachable)
            {
                TLog.Warning($"[NodeStateManager] Node is not a valid forward choice: {nodeId}");
                return new List<RoguelikeMapNode>();
            }

            var previousVisibility = _map.nodes.ToDictionary(n => n.nodeId, n => n.Visibility);

            node.VisitState = NodeVisitState.Visited;
            node.IsReachable = false;
            _map.visitedNodes.Add(nodeId);
            _map.currentNodeId = nodeId;
            CurrentNodeId = nodeId;
            TLog.Info($"[NodeStateManager] 节点已访问: {nodeId}");

            // 使用 MapRevealSystem 重新计算视野
            _revealSystem.UpdateReveal(nodeId);

            // 返回新揭示的节点（状态从 Unrevealed 变为可见的节点）
            var revealedNodes = _map.nodes
                .Where(n => n.Visibility != NodeVisibility.Hidden
                            && previousVisibility.TryGetValue(n.nodeId, out var previous)
                            && previous == NodeVisibility.Hidden)
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

            return node.IsReachable && node.VisitState == NodeVisitState.Unvisited;
        }

        /// <summary>
        /// 获取节点当前状态
        /// </summary>
        [Obsolete("Use NodeVisibility/NodeVisitState instead.")]
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
            return _map.nodes.Where(n => n.VisitState == NodeVisitState.Visited).ToList();
        }

        /// <summary>
        /// 检查是否到达Boss节点
        /// </summary>
        public bool HasReachedBoss()
        {
            return _map.nodes.Any(n => n.nodeType == RoguelikeNodeType.Boss && n.VisitState == NodeVisitState.Visited);
        }

        /// <summary>
        /// 重置所有节点状态
        /// </summary>
        public void ResetStates()
        {
            foreach (var node in _map.nodes)
            {
                node.Visibility = NodeVisibility.Hidden;
                node.IsReachable = false;
                node.VisitState = NodeVisitState.Unvisited;
            }
            TLog.Info("[NodeStateManager] 所有节点状态已重置");
        }

        private string ResolveCurrentNodeId()
        {
            if (!string.IsNullOrEmpty(_currentNodeIdHint) &&
                _nodeLookup.TryGetValue(_currentNodeIdHint, out var hintedNode) &&
                hintedNode.VisitState == NodeVisitState.Visited)
            {
                return hintedNode.nodeId;
            }

            if (!string.IsNullOrEmpty(_map.currentNodeId) &&
                _nodeLookup.TryGetValue(_map.currentNodeId, out var persistedNode) &&
                persistedNode.VisitState == NodeVisitState.Visited)
            {
                return persistedNode.nodeId;
            }

            if (_map.visitedNodes.Count == 1)
                return _map.visitedNodes.First();

            return _map.visitedNodes.Last();
        }
    }
}
