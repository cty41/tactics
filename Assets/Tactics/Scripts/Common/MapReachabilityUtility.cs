using System;
using System.Collections.Generic;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.RoguelikeMap
{
    /// <summary>
    /// 可达性计算工具类，供编辑器和 Runtime 共享使用。
    /// 提供节点间距离计算、可达节点查询和全连接关系生成。
    /// </summary>
    public static class MapReachabilityUtility
    {
        /// <summary>
        /// 计算两点之间的欧几里得距离。
        /// </summary>
        public static float CalculateDistance(Vector2 a, Vector2 b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 返回 from 节点在 maxDistance 范围内可达的节点列表（排除自身）。
        /// </summary>
        public static List<RoguelikeMapNode> GetReachableNodes(
            RoguelikeMapNode from,
            List<RoguelikeMapNode> allNodes,
            float maxDistance)
        {
            var result = new List<RoguelikeMapNode>();

            foreach (var node in allNodes)
            {
                if (node.nodeId == from.nodeId)
                    continue;

                if (CalculateDistance(from.position, node.position) <= maxDistance)
                {
                    result.Add(node);
                }
            }

            TLog.Info($"[MapReachabilityUtility] GetReachableNodes: {from.nodeId} -> {result.Count} nodes within {maxDistance}");
            return result;
        }

        /// <summary>
        /// 返回所有节点间在 maxDistance 范围内的连接关系。
        /// 每个连接只记录一次（双向不重复），且不会包含自连接。
        /// </summary>
        public static List<(RoguelikeMapNode from, RoguelikeMapNode to)> GetAllConnections(
            List<RoguelikeMapNode> nodes,
            float maxDistance)
        {
            var result = new List<(RoguelikeMapNode from, RoguelikeMapNode to)>();
            var seen = new HashSet<string>();

            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    var a = nodes[i];
                    var b = nodes[j];
                    float dist = CalculateDistance(a.position, b.position);

                    if (dist <= maxDistance)
                    {
                        string key = string.Compare(a.nodeId, b.nodeId, StringComparison.Ordinal) < 0
                            ? $"{a.nodeId}|{b.nodeId}"
                            : $"{b.nodeId}|{a.nodeId}";

                        if (seen.Add(key))
                        {
                            result.Add((a, b));
                        }
                    }
                }
            }

            TLog.Info($"[MapReachabilityUtility] GetAllConnections: {result.Count} connections found within {maxDistance}");
            return result;
        }
    }
}
