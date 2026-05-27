using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Tactics.RoguelikeMap;
using Tactics.Runtime.Utilities;
using RoguelikeMapData = Tactics.RoguelikeMap.RoguelikeMap;

namespace Tactics.Editor.RoguelikeMapEditor
{
    /// <summary>
    /// Editor-only 文档模型，作为编辑器的唯一数据源（Single Source of Truth）。
    /// MapGraphView 和 MapInspectorPanel 都只读写此模型。
    /// 
    /// 数据流：
    ///   Load: JSON → SerializableMapData → MapEditorDocument
    ///   Save/Export: MapEditorDocument → SerializableMapData → JSON
    /// </summary>
    [Serializable]
    public class MapEditorDocument
    {
        /// <summary>所有节点数据。</summary>
        public List<EditableMapNodeData> nodes = new();

        /// <summary>最大可达距离（用于画布边界和连接计算）。</summary>
        public int maxReachableDistance = 200;

        /// <summary>视野范围。</summary>
        public int visionRange;

        /// <summary>文档是否已被修改。</summary>
        public bool IsDirty { get; set; }

        /// <summary>文档内容变更事件。</summary>
        public event Action OnDocumentChanged;

        // ═══════════════════════════════════════════
        //  工厂方法：从 SerializableMapData 加载
        // ═══════════════════════════════════════════

        /// <summary>
        /// 从 SerializableMapData 创建文档模型。
        /// </summary>
        public static MapEditorDocument FromSerializable(SerializableMapData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var doc = new MapEditorDocument
            {
                maxReachableDistance = data.maxReachableDistance,
                visionRange = data.visionRange
            };

            foreach (var nodeData in data.nodes)
            {
                doc.nodes.Add(EditableMapNodeData.FromSerializableNode(nodeData));
            }

            // incoming 由 outgoing 隐式定义，在 ToSerializable() 时自动重建
            TLog.Info($"[MapEditorDocument] Loaded from SerializableMapData: {doc.nodes.Count} nodes");
            return doc;
        }

        /// <summary>
        /// 从运行时 RoguelikeMap 创建文档模型（用于 Generate 后的转换）。
        /// </summary>
        public static MapEditorDocument FromRuntimeMap(RoguelikeMapData map, int maxReachableDistance, int visionRange)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            var doc = new MapEditorDocument
            {
                maxReachableDistance = maxReachableDistance,
                visionRange = visionRange
            };

            foreach (var node in map.nodes)
            {
                doc.nodes.Add(EditableMapNodeData.FromRuntimeNode(node));
            }

            TLog.Info($"[MapEditorDocument] Loaded from runtime map: {doc.nodes.Count} nodes");
            return doc;
        }

        // ═══════════════════════════════════════════
        //  转换方法：导出为 SerializableMapData
        // ═══════════════════════════════════════════

        /// <summary>
        /// 转换为 SerializableMapData 用于 JSON 序列化。
        /// 自动从 outgoing 重建 incoming。
        /// </summary>
        public SerializableMapData ToSerializable()
        {
            var data = new SerializableMapData
            {
                version = 1,
                maxReachableDistance = maxReachableDistance,
                visionRange = visionRange
            };

            // 构建 incoming 映射
            var incomingMap = new Dictionary<string, List<string>>();
            foreach (var node in nodes)
            {
                if (!incomingMap.ContainsKey(node.nodeId))
                    incomingMap[node.nodeId] = new List<string>();

                foreach (var targetId in node.outgoing)
                {
                    if (!incomingMap.ContainsKey(targetId))
                        incomingMap[targetId] = new List<string>();
                    incomingMap[targetId].Add(node.nodeId);
                }
            }

            foreach (var node in nodes)
            {
                var nodeData = node.ToSerializableNode();
                // 重建 incoming
                if (incomingMap.TryGetValue(node.nodeId, out var incoming))
                    nodeData.incoming = incoming.ToArray();
                else
                    nodeData.incoming = Array.Empty<string>();

                data.nodes.Add(nodeData);
            }

            return data;
        }

        // ═══════════════════════════════════════════
        //  节点操作
        // ═══════════════════════════════════════════

        /// <summary>
        /// 添加新节点。
        /// </summary>
        public EditableMapNodeData AddNode(RoguelikeNodeType type, Vector2 position)
        {
            var node = new EditableMapNodeData
            {
                nodeId = $"{type.ToString().ToLower()}_{Guid.NewGuid().ToString()[..5]}",
                nodeType = type,
                position = position
            };

            if (type == RoguelikeNodeType.Treasure)
            {
                node.goldMin = 2;
                node.goldMax = 5;
            }

            nodes.Add(node);
            MarkDirty();
            TLog.Info($"[MapEditorDocument] Added node: {node.nodeId} ({type}) at {position}");
            return node;
        }

        /// <summary>
        /// 添加已有的可编辑节点数据（用于从运行时地图导入）。
        /// </summary>
        public void AddNode(EditableMapNodeData node)
        {
            nodes.Add(node);
            MarkDirty();
        }

        /// <summary>
        /// 移除节点及其所有相关连接。
        /// </summary>
        public void RemoveNode(string nodeId)
        {
            var node = GetNode(nodeId);
            if (node == null) return;

            // 移除此节点的 outgoing
            nodes.Remove(node);

            // 移除其他节点对此节点的 outgoing 引用
            foreach (var other in nodes)
            {
                other.outgoing.Remove(nodeId);
            }

            MarkDirty();
            TLog.Info($"[MapEditorDocument] Removed node: {nodeId}");
        }

        /// <summary>
        /// 获取指定节点。
        /// </summary>
        public EditableMapNodeData GetNode(string nodeId)
        {
            return nodes.FirstOrDefault(n => n.nodeId == nodeId);
        }

        /// <summary>
        /// 检查节点是否存在。
        /// </summary>
        public bool HasNode(string nodeId)
        {
            return nodes.Any(n => n.nodeId == nodeId);
        }

        // ═══════════════════════════════════════════
        //  连接操作
        // ═══════════════════════════════════════════

        /// <summary>
        /// 添加连接（fromId → toId）。
        /// </summary>
        public void AddConnection(string fromId, string toId)
        {
            var fromNode = GetNode(fromId);
            if (fromNode == null)
            {
                TLog.Warning($"[MapEditorDocument] AddConnection: source node '{fromId}' not found");
                return;
            }
            if (!HasNode(toId))
            {
                TLog.Warning($"[MapEditorDocument] AddConnection: target node '{toId}' not found");
                return;
            }
            if (fromId == toId)
            {
                TLog.Warning($"[MapEditorDocument] AddConnection: cannot connect node to itself");
                return;
            }

            fromNode.AddOutgoing(toId);
            MarkDirty();
            TLog.Info($"[MapEditorDocument] Added connection: {fromId} → {toId}");
        }

        /// <summary>
        /// 移除连接（fromId → toId）。
        /// </summary>
        public void RemoveConnection(string fromId, string toId)
        {
            var fromNode = GetNode(fromId);
            if (fromNode == null) return;

            fromNode.RemoveOutgoing(toId);
            MarkDirty();
            TLog.Info($"[MapEditorDocument] Removed connection: {fromId} → {toId}");
        }

        /// <summary>
        /// 获取所有连接关系（fromId, toId）。
        /// </summary>
        public List<(string from, string to)> GetAllConnections()
        {
            var connections = new List<(string, string)>();
            foreach (var node in nodes)
            {
                foreach (var targetId in node.outgoing)
                {
                    connections.Add((node.nodeId, targetId));
                }
            }
            return connections;
        }

        /// <summary>
        /// 获取指定节点的 outgoing 连接列表。
        /// </summary>
        public List<string> GetOutgoing(string nodeId)
        {
            var node = GetNode(nodeId);
            return node?.outgoing ?? new List<string>();
        }

        // ═══════════════════════════════════════════
        //  工具方法
        // ═══════════════════════════════════════════

        /// <summary>
        /// 获取 Boss 节点 ID。
        /// </summary>
        public string GetBossNodeId()
        {
            var boss = nodes.FirstOrDefault(n => n.nodeType == RoguelikeNodeType.Boss);
            return boss?.nodeId ?? string.Empty;
        }

        /// <summary>
        /// 标记文档为已修改并触发变更事件。
        /// </summary>
        public void MarkDirty()
        {
            IsDirty = true;
            OnDocumentChanged?.Invoke();
        }

        /// <summary>
        /// 清除脏标记（保存后调用）。
        /// </summary>
        public void ClearDirty()
        {
            IsDirty = false;
        }

        /// <summary>
        /// 清空所有节点数据。
        /// </summary>
        public void Clear()
        {
            nodes.Clear();
            MarkDirty();
        }
    }
}
