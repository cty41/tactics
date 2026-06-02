using System.Collections.Generic;
using Tactics.Common.Battle;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.RoguelikeMap
{
    /// <summary>
    /// FTL 风格地图生成器（网格布局）。
    /// 使用网格布局 + 距离约束 + BFS 连通性检查的方式生成地图。
    /// 节点按网格单元排列，从左下到右上依次为 Start → 中间节点 → Boss。
    /// </summary>
    public static class RoguelikeMapGenerator
    {
        private const int MaxRetries = 50;
        /// <summary>
        /// 根据配置生成一张 FTL 风格的 Roguelike 地图（网格布局）。
        /// </summary>
        /// <param name="config">地图配置</param>
        /// <returns>生成的地图，失败返回 null</returns>
        public static RoguelikeMap GetMap(RoguelikeMapConfig config)
        {
            if (config == null)
            {
                TLog.Error("[RoguelikeMapGenerator] Config is null.");
                return null;
            }

            if (config.nodeCount < 2)
            {
                TLog.Error("[RoguelikeMapGenerator] nodeCount must be >= 2.");
                return null;
            }

            TLog.Info($"[RoguelikeMapGenerator] Generating FTL-style map: gridColumns={config.gridColumns}, gridRows={config.gridRows}, " +
                      $"nodeCount={config.nodeCount}, maxReachableDistance={config.maxReachableDistance}, " +
                      $"minDistance={config.minDistanceBetweenNodes}, storeMinDistance={config.storeMinDistance}");

            for (int retry = 0; retry < MaxRetries; retry++)
            {
                var nodes = new List<RoguelikeMapNode>();
                var connections = new List<(RoguelikeMapNode from, RoguelikeMapNode to)>();
                int nextId = 0;

                // 1. 计算网格单元尺寸
                int gridCols = config.gridColumns;
                int gridRows = config.gridRows;
                float mapWidth = config.maxReachableDistance * gridCols * 0.8f;
                float mapHeight = config.maxReachableDistance * gridRows * 0.6f;
                float cellWidth = mapWidth / gridCols;
                float cellHeight = mapHeight / gridRows;

                TLog.Info($"[RoguelikeMapGenerator] Attempt {retry + 1}/{MaxRetries}: grid={gridCols}x{gridRows}, cellSize=({cellWidth:F1},{cellHeight:F1})");

                // 2. 在网格单元内放置节点
                for (int col = 0; col < gridCols; col++)
                {
                    for (int row = 0; row < gridRows; row++)
                    {
                        // 单元内随机位置（避开边界 10%-90%）
                        float x = col * cellWidth + UnityEngine.Random.Range(0.1f, 0.9f) * cellWidth;
                        float y = row * cellHeight + UnityEngine.Random.Range(0.1f, 0.9f) * cellHeight;
                        Vector2 pos = new Vector2(x, y);

                        // 确定节点类型
                        RoguelikeNodeType nodeType;
                        string blueprintName;
                        if (col == 0 && row == 0)
                        {
                            nodeType = RoguelikeNodeType.Start;
                            blueprintName = "Start";
                        }
                        else if (col == gridCols - 1 && row == gridRows - 1)
                        {
                            nodeType = RoguelikeNodeType.Boss;
                            blueprintName = "Boss";
                        }
                        else
                        {
                            nodeType = config.randomNodes[UnityEngine.Random.Range(0, config.randomNodes.Count)];
                            blueprintName = GetBlueprintName(config, nodeType);
                        }

                        // 商店间距约束
                        if (nodeType == RoguelikeNodeType.Store && !IsValidStorePosition(pos, nodes, config.storeMinDistance))
                        {
                            TLog.Info($"[RoguelikeMapGenerator] Store too close, skipping at ({pos.x:F1},{pos.y:F1})");
                            continue;
                        }

                        var node = new RoguelikeMapNode($"n{nextId++}", nodeType, blueprintName, pos);
                        node.encounterConfigPath = EncounterConfigLoader.GetDefaultEncounterPath(nodeType);
                        nodes.Add(node);
                        TLog.Info($"[RoguelikeMapGenerator] Placed node {node.nodeId} ({nodeType}) at ({pos.x:F1},{pos.y:F1}) in cell [{col},{row}]");
                    }
                }

                // 3. 建立连接（使用 BuildConnections）
                connections = BuildConnections(nodes, config.maxReachableDistance);

                // 4. 填充 incoming/outgoing
                foreach (var (from, to) in connections)
                {
                    from.AddOutgoing(to.nodeId);
                    to.AddIncoming(from.nodeId);
                }

                // 5. 验证
                if (!IsFullyConnected(nodes[0], nodes))
                {
                    TLog.Info($"[RoguelikeMapGenerator] Not all nodes reachable (attempt {retry + 1}/{MaxRetries}), retrying...");
                    continue;
                }

                if (!HasMinimumConnections(nodes))
                {
                    TLog.Info($"[RoguelikeMapGenerator] Some nodes have < 2 connections (attempt {retry + 1}/{MaxRetries}), retrying...");
                    continue;
                }

                // 6. 成功
                LogMapResult(nodes, connections);
                return new RoguelikeMap(config.name, nodes[nodes.Count - 1].nodeId, nodes, new HashSet<string>(),
                    config.maxReachableDistance, config.visionRange);
            }

            TLog.Error($"[RoguelikeMapGenerator] Failed to generate connected map after {MaxRetries} attempts.");
            return null;
        }

        /// <summary>
        /// 建立节点间的纯距离连接。
        /// 1) 对每个节点，连接最近的 1-3 个可达节点
        /// 2) 侧向补充：连接数不足 2 的节点，补连最近的可达节点
        /// 3) 额外 20% 随机连接
        /// </summary>
        private static List<(RoguelikeMapNode from, RoguelikeMapNode to)> BuildConnections(
            List<RoguelikeMapNode> nodes,
            float maxReachableDistance)
        {
            var connections = new List<(RoguelikeMapNode from, RoguelikeMapNode to)>();
            var connectionSet = new HashSet<(string, string)>();
            const int MaxConnectionsPerNode = 4;

            // 1) 对每个节点，连接最近的 1-3 个可达节点
            for (int i = 0; i < nodes.Count; i++)
            {
                var nodeA = nodes[i];
                var candidates = new List<(int index, float distance)>();

                for (int j = 0; j < nodes.Count; j++)
                {
                    if (i == j) continue;
                    float dist = MapReachabilityUtility.CalculateDistance(nodeA.position, nodes[j].position);
                    if (dist <= maxReachableDistance)
                    {
                        candidates.Add((j, dist));
                    }
                }

                // 按距离排序，取前 1-3 个
                candidates.Sort((a, b) => a.distance.CompareTo(b.distance));
                int currentConn = CountConnections(nodeA, connectionSet);
                int maxForward = Mathf.Max(0, MaxConnectionsPerNode - currentConn);
                int connectCount = Mathf.Min(UnityEngine.Random.Range(1, 4), candidates.Count, maxForward);

                for (int c = 0; c < connectCount; c++)
                {
                    var target = nodes[candidates[c].index];
                    var key = (nodeA.nodeId, target.nodeId);
                    var reverseKey = (target.nodeId, nodeA.nodeId);
                    if (connectionSet.Add(key) && !connectionSet.Contains(reverseKey))
                    {
                        connections.Add((nodeA, target));
                    }
                }
            }

            // 2) 侧向补充：连接数不足 2 的节点，补连最近的可达节点
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                int currentConnections = CountConnections(node, connectionSet);

                if (currentConnections >= 2 || currentConnections >= MaxConnectionsPerNode)
                    continue;

                var nearby = new List<(int index, float distance)>();
                for (int j = 0; j < nodes.Count; j++)
                {
                    if (i == j) continue;
                    float dist = MapReachabilityUtility.CalculateDistance(node.position, nodes[j].position);
                    if (dist <= maxReachableDistance)
                    {
                        nearby.Add((j, dist));
                    }
                }

                nearby.Sort((a, b) => a.distance.CompareTo(b.distance));

                foreach (var (index, _) in nearby)
                {
                    if (currentConnections >= 2) break;

                    var target = nodes[index];
                    var key = (node.nodeId, target.nodeId);
                    var reverseKey = (target.nodeId, node.nodeId);

                    if (connectionSet.Contains(key) || connectionSet.Contains(reverseKey))
                        continue;

                    connectionSet.Add(key);
                    connections.Add((node, target));
                    currentConnections++;
                }
            }

            // 3) 额外 20% 随机连接
            int extraCount = Mathf.CeilToInt(nodes.Count * 0.2f);
            int extraAdded = 0;
            int maxExtraAttempts = nodes.Count * 5;
            int extraAttempt = 0;

            while (extraAdded < extraCount && extraAttempt < maxExtraAttempts)
            {
                extraAttempt++;
                int a = UnityEngine.Random.Range(0, nodes.Count);
                int b = UnityEngine.Random.Range(0, nodes.Count);

                if (a == b) continue;

                var nodeA = nodes[a];
                var nodeB = nodes[b];
                float dist = MapReachabilityUtility.CalculateDistance(nodeA.position, nodeB.position);
                if (dist > maxReachableDistance) continue;

                int connA = CountConnections(nodeA, connectionSet);
                int connB = CountConnections(nodeB, connectionSet);
                if (connA >= MaxConnectionsPerNode || connB >= MaxConnectionsPerNode)
                    continue;

                var key = (nodeA.nodeId, nodeB.nodeId);
                var reverseKey = (nodeB.nodeId, nodeA.nodeId);
                if (connectionSet.Contains(key) || connectionSet.Contains(reverseKey)) continue;

                connectionSet.Add(key);
                connections.Add((nodeA, nodeB));
                extraAdded++;
            }

            return connections;
        }

        /// <summary>
        /// 检查位置是否与已有节点保持最小距离。
        /// </summary>
        private static bool IsValidPosition(Vector2 pos, List<RoguelikeMapNode> existing, float minDistance)
        {
            foreach (var node in existing)
            {
                if (MapReachabilityUtility.CalculateDistance(pos, node.position) < minDistance)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 检查位置是否与已有商店保持最小距离。
        /// </summary>
        private static bool IsValidStorePosition(Vector2 pos, List<RoguelikeMapNode> existing, float storeMinDistance)
        {
            foreach (var node in existing)
            {
                if (node.nodeType == RoguelikeNodeType.Store &&
                    MapReachabilityUtility.CalculateDistance(pos, node.position) < storeMinDistance)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 从配置的蓝图列表中随机选取匹配类型的蓝图名称。
        /// 无匹配蓝图时返回类型名称。
        /// </summary>
        private static string GetBlueprintName(RoguelikeMapConfig config, RoguelikeNodeType nodeType)
        {
            if (config.nodeBlueprints == null || config.nodeBlueprints.Count == 0)
                return nodeType.ToString();

            var matching = config.nodeBlueprints.FindAll(b => b != null && b.nodeType == nodeType);
            if (matching.Count == 0)
                return nodeType.ToString();

            return matching[UnityEngine.Random.Range(0, matching.Count)].name;
        }

        /// <summary>
        /// BFS 检查从 start 出发是否可达所有节点（遍历 incoming 和 outgoing 双向边）。
        /// </summary>
        private static bool IsFullyConnected(RoguelikeMapNode start, List<RoguelikeMapNode> allNodes)
        {
            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(start.nodeId);
            visited.Add(start.nodeId);

            var nodeMap = new Dictionary<string, RoguelikeMapNode>();
            foreach (var node in allNodes)
                nodeMap[node.nodeId] = node;

            while (queue.Count > 0)
            {
                string currentId = queue.Dequeue();
                if (!nodeMap.TryGetValue(currentId, out var current))
                    continue;

                foreach (var neighborId in current.outgoing)
                {
                    if (visited.Add(neighborId))
                        queue.Enqueue(neighborId);
                }

                foreach (var neighborId in current.incoming)
                {
                    if (visited.Add(neighborId))
                        queue.Enqueue(neighborId);
                }
            }

            bool connected = visited.Count == allNodes.Count;
            if (!connected)
            {
                TLog.Info($"[RoguelikeMapGenerator] BFS: {visited.Count}/{allNodes.Count} reachable from start.");
            }
            return connected;
        }

        /// <summary>
        /// 检查所有节点是否至少有 2 个连接。
        /// </summary>
        private static bool HasMinimumConnections(List<RoguelikeMapNode> nodes)
        {
            foreach (var node in nodes)
            {
                int total = node.outgoing.Count + node.incoming.Count;
                if (total < 2)
                {
                    TLog.Info($"[RoguelikeMapGenerator] Node {node.nodeId} ({node.nodeType}) has only {total} connections.");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 统计节点在连接集合中的连接数。
        /// </summary>
        private static int CountConnections(RoguelikeMapNode node, HashSet<(string, string)> connectionSet)
        {
            int count = 0;
            foreach (var (from, to) in connectionSet)
            {
                if (from == node.nodeId || to == node.nodeId)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 输出生成结果摘要日志。
        /// </summary>
        private static void LogMapResult(List<RoguelikeMapNode> nodes, List<(RoguelikeMapNode from, RoguelikeMapNode to)> connections)
        {
            // 统计各类型节点数量
            var typeCounts = new Dictionary<RoguelikeNodeType, int>();
            foreach (var node in nodes)
            {
                if (!typeCounts.ContainsKey(node.nodeType))
                    typeCounts[node.nodeType] = 0;
                typeCounts[node.nodeType]++;
            }

            string typeSummary = string.Join(", ", typeCounts);
            TLog.Info($"[RoguelikeMapGenerator] FTL-style map generated: {nodes.Count} nodes, {connections.Count} connections. Types: [{typeSummary}]");
        }
    }
}
