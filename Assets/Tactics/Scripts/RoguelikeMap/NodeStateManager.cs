using System.Collections.Generic;
using System.Linq;
using Tactics.RoguelikeMap;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.RoguelikeMap
{
    /// <summary>
    /// 节点状态管理器
    /// 负责管理节点的状态转换逻辑
    /// </summary>
    public class NodeStateManager
    {
        private readonly RoguelikeMap _map;
        private readonly Dictionary<Vector2Int, RoguelikeMapNode> _nodeLookup;

        public NodeStateManager(RoguelikeMap map)
        {
            _map = map;
            _nodeLookup = map.nodes.ToDictionary(n => n.point, n => n);
        }

        /// <summary>
        /// 初始化节点状态
        /// 第1层节点设为Reachable，其余设为Unrevealed
        /// </summary>
        public void InitializeStates()
        {
            foreach (var node in _map.nodes)
            {
                if (node.point.y == 0)
                {
                    // 第1层节点设为可到达
                    node.state = NodeState.Reachable;
                }
                else
                {
                    // 其他节点设为未揭示
                    node.state = NodeState.Unrevealed;
                }
            }

            TLog.Info($"[NodeStateManager] 初始化完成，第1层节点设为Reachable");
        }

        /// <summary>
        /// 访问节点
        /// 将节点设为Visited，并揭示下一层相邻节点
        /// </summary>
        /// <param name="nodePoint">被访问的节点坐标</param>
        /// <returns>新揭示的节点列表</returns>
        public List<RoguelikeMapNode> VisitNode(Vector2Int nodePoint)
        {
            if (!_nodeLookup.TryGetValue(nodePoint, out var node))
            {
                TLog.Warning($"[NodeStateManager] 节点不存在: {nodePoint}");
                return new List<RoguelikeMapNode>();
            }

            // 将当前节点设为已访问
            node.state = NodeState.Visited;
            TLog.Info($"[NodeStateManager] 节点已访问: {nodePoint}");

            // 揭示下一层相邻节点
            var revealedNodes = RevealNextLayerNodes(node);

            // 将当前层可到达的节点设为已访问（如果所有相邻节点都已访问）
            UpdateReachableNodes();

            return revealedNodes;
        }

        /// <summary>
        /// 揭示下一层相邻节点
        /// </summary>
        private List<RoguelikeMapNode> RevealNextLayerNodes(RoguelikeMapNode visitedNode)
        {
            var revealedNodes = new List<RoguelikeMapNode>();

            // 遍历所有出边（指向的节点）
            foreach (var outgoingPoint in visitedNode.outgoing)
            {
                if (_nodeLookup.TryGetValue(outgoingPoint, out var nextNode))
                {
                    if (nextNode.state == NodeState.Unrevealed)
                    {
                        nextNode.state = NodeState.Revealed;
                        revealedNodes.Add(nextNode);
                        TLog.Info($"[NodeStateManager] 节点已揭示: {outgoingPoint}");
                    }
                }
            }

            return revealedNodes;
        }

        /// <summary>
        /// 更新可到达节点
        /// 当访问一个节点后，检查是否需要将其他节点设为可到达
        /// </summary>
        private void UpdateReachableNodes()
        {
            // 找到当前已访问的最高层
            int maxVisitedLayer = _map.nodes
                .Where(n => n.state == NodeState.Visited)
                .Max(n => n.point.y);

            // 将下一层的已揭示节点设为可到达
            foreach (var node in _map.nodes)
            {
                if (node.point.y == maxVisitedLayer + 1 && node.state == NodeState.Revealed)
                {
                    node.state = NodeState.Reachable;
                    TLog.Info($"[NodeStateManager] 节点变为可到达: {node.point}");
                }
            }
        }

        /// <summary>
        /// 检查节点是否可点击
        /// </summary>
        public bool IsNodeClickable(Vector2Int nodePoint)
        {
            if (!_nodeLookup.TryGetValue(nodePoint, out var node))
                return false;

            return node.state == NodeState.Reachable;
        }

        /// <summary>
        /// 获取节点状态
        /// </summary>
        public NodeState GetNodeState(Vector2Int nodePoint)
        {
            if (!_nodeLookup.TryGetValue(nodePoint, out var node))
                return NodeState.Unrevealed;

            return node.state;
        }

        /// <summary>
        /// 获取所有可到达的节点
        /// </summary>
        public List<RoguelikeMapNode> GetReachableNodes()
        {
            return _map.nodes.Where(n => n.state == NodeState.Reachable).ToList();
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
            TLog.Info($"[NodeStateManager] 所有节点状态已重置");
        }
    }
}
