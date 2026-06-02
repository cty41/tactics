using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Tactics.RoguelikeMap
{
    /// <summary>
    /// 地图数据的可序列化表示，用于 Editor 和 Runtime 之间的 JSON 数据交换。
    /// </summary>
    [Serializable]
    public class SerializableMapData
    {
        [JsonProperty("version")]
        public int version = 1;

        [JsonProperty("nodes")]
        public List<SerializableNodeData> nodes = new List<SerializableNodeData>();

        [JsonProperty("maxReachableDistance")]
        public int maxReachableDistance;

        [JsonProperty("visionRange")]
        public int visionRange;
    }

    /// <summary>
    /// 单个节点的可序列化表示。
    /// </summary>
    [Serializable]
    public class SerializableNodeData
    {
        [Serializable]
        public class SerializableWeightedEquipmentData
        {
            [JsonProperty("equipmentId")]
            public string equipmentId;

            [JsonProperty("weight")]
            public float weight = 1f;
        }

        [Serializable]
        public class SerializableStoreGoodData
        {
            [JsonProperty("equipmentId")]
            public string equipmentId;

            [JsonProperty("price")]
            public int price;
        }

        [Serializable]
        public class SerializableBuffEntryData
        {
            /// <summary>
            /// BuffConfig 的资产路径（相对于 Assets/），用于 JSON 序列化 ScriptableObject 引用。
            /// </summary>
            [JsonProperty("buffConfigPath")]
            public string buffConfigPath;

            [JsonProperty("weight")]
            public float weight = 1f;
        }

        /// <summary>
        /// 节点唯一标识（对应 RoguelikeMapNode.nodeId）。
        /// </summary>
        [JsonProperty("nodeId")]
        public string nodeId;

        /// <summary>
        /// 节点类型名称，对应 RoguelikeNodeType 枚举名。
        /// </summary>
        [JsonProperty("type")]
        public string type;

        /// <summary>
        /// 节点的视觉位置（x, y）。
        /// </summary>
        [JsonProperty("position")]
        public SerializableVector2 position;

        /// <summary>
        /// 出边列表，每个元素为指向节点的 nodeId。
        /// </summary>
        [JsonProperty("outgoing")]
        public string[] outgoing = Array.Empty<string>();

        /// <summary>
        /// 入边列表，每个元素为来源节点的 nodeId。
        /// </summary>
        [JsonProperty("incoming")]
        public string[] incoming = Array.Empty<string>();

        /// <summary>
        /// 节点对应的蓝图名称。
        /// </summary>
        [JsonProperty("blueprintName")]
        public string blueprintName;

        /// <summary>
        /// 事件 ID（用于 Mystery / 自定义事件节点）。
        /// </summary>
        [JsonProperty("eventId")]
        public string eventId;

        /// <summary>
        /// 战斗遭遇配置路径（用于敌方节点动态生成怪物）。
        /// </summary>
        [JsonProperty("encounterConfigPath")]
        public string encounterConfigPath;

        /// <summary>
        /// 商店 ID（用于 Store 节点）。
        /// </summary>
        [JsonProperty("shopId")]
        public string shopId;

        /// <summary>
        /// 宝藏 ID（用于 Treasure 节点）。
        /// </summary>
        [JsonProperty("treasureId")]
        public string treasureId;

        /// <summary>
        /// 宝藏金币下限（用于 Treasure 节点）。
        /// </summary>
        [JsonProperty("goldMin")]
        public int? goldMin;

        /// <summary>
        /// 宝藏金币上限（用于 Treasure 节点）。
        /// </summary>
        [JsonProperty("goldMax")]
        public int? goldMax;

        [JsonProperty("treasureEquipment")]
        public List<SerializableWeightedEquipmentData> treasureEquipment = new List<SerializableWeightedEquipmentData>();

        [JsonProperty("storeGoods")]
        public List<SerializableStoreGoodData> storeGoods = new List<SerializableStoreGoodData>();

        [JsonProperty("treasureBuffs")]
        public List<SerializableBuffEntryData> treasureBuffs = new List<SerializableBuffEntryData>();
    }

    /// <summary>
    /// 可序列化的二维向量。
    /// </summary>
    [Serializable]
    public struct SerializableVector2
    {
        [JsonProperty("x")]
        public float x;

        [JsonProperty("y")]
        public float y;

        public SerializableVector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public Vector2 ToVector2() => new Vector2(x, y);

        public static SerializableVector2 FromVector2(Vector2 v) => new SerializableVector2(v.x, v.y);
    }

    /// <summary>
    /// 地图数据的 JSON 序列化/反序列化器。
    /// 遵循 EventGraphSerializer 风格。
    /// </summary>
    public static class MapDataSerializer
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>
        /// 将 SerializableMapData 序列化为 JSON 字符串。
        /// </summary>
        public static string Serialize(SerializableMapData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            Validate(data);
            return JsonConvert.SerializeObject(data, Settings);
        }

        /// <summary>
        /// 将 JSON 字符串反序列化为 SerializableMapData。
        /// </summary>
        public static SerializableMapData Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON string cannot be empty", nameof(json));

            var data = JsonConvert.DeserializeObject<SerializableMapData>(json);
            if (data == null)
                throw new InvalidOperationException("JSON deserialization failed");

            Validate(data);
            return data;
        }

        /// <summary>
        /// 验证地图数据的完整性。
        /// </summary>
        public static void Validate(SerializableMapData data)
        {
            if (data.version <= 0)
                throw new InvalidOperationException("version must be positive");

            if (data.nodes == null || data.nodes.Count == 0)
                throw new InvalidOperationException("At least one node required");

            var nodeIds = new HashSet<string>();

            foreach (var node in data.nodes)
            {
                if (string.IsNullOrWhiteSpace(node.nodeId))
                    throw new InvalidOperationException("Node nodeId cannot be empty");

                if (!nodeIds.Add(node.nodeId))
                    throw new InvalidOperationException($"Duplicate nodeId: {node.nodeId}");

                if (string.IsNullOrWhiteSpace(node.type))
                    throw new InvalidOperationException($"Node {node.nodeId}: type cannot be empty");

                if (!Enum.TryParse<RoguelikeNodeType>(node.type, true, out _))
                    throw new InvalidOperationException($"Node {node.nodeId}: invalid type '{node.type}'");
            }

            // 验证连接引用的 nodeId 都存在
            foreach (var node in data.nodes)
            {
                if (node.outgoing != null)
                {
                    foreach (var targetId in node.outgoing)
                    {
                        if (!nodeIds.Contains(targetId))
                            throw new InvalidOperationException(
                                $"Node {node.nodeId}: outgoing references unknown nodeId '{targetId}'");
                    }
                }

                if (node.incoming != null)
                {
                    foreach (var sourceId in node.incoming)
                    {
                        if (!nodeIds.Contains(sourceId))
                            throw new InvalidOperationException(
                                $"Node {node.nodeId}: incoming references unknown nodeId '{sourceId}'");
                    }
                }
            }
        }

        /// <summary>
        /// 将 SerializableMapData 转换为运行时 RoguelikeMap。
        /// </summary>
        public static RoguelikeMap ToRuntimeMap(SerializableMapData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            Validate(data);

            var nodes = new List<RoguelikeMapNode>();

            foreach (var nodeData in data.nodes)
            {
                var nodeType = Enum.Parse<RoguelikeNodeType>(nodeData.type, true);
                var node = new RoguelikeMapNode(nodeData.nodeId, nodeType, nodeData.blueprintName,
                    nodeData.position.ToVector2());
                node.eventId = nodeData.eventId ?? "";
                node.encounterConfigPath = nodeData.encounterConfigPath ?? "";
                if (nodeData.goldMin.HasValue || nodeData.goldMax.HasValue)
                {
                    node.treasureConfig = new TreasureNodeConfig
                    {
                        goldMin = nodeData.goldMin ?? 2,
                        goldMax = nodeData.goldMax ?? 5,
                        equipmentEntries = nodeData.treasureEquipment?.Select(e => new EquipmentEntry
                        {
                            equipmentId = e.equipmentId,
                            weight = e.weight
                        }).ToList() ?? new List<EquipmentEntry>()
                    };
                }
                if (nodeData.storeGoods != null && nodeData.storeGoods.Count > 0)
                {
                    node.storeConfig = new StoreNodeConfig
                    {
                        goods = nodeData.storeGoods.Select(g => new StoreGoodEntry
                        {
                            equipmentId = g.equipmentId,
                            price = g.price
                        }).ToList()
                    };
                }
                nodes.Add(node);
            }

            // 第二遍：重建连接关系
            for (int i = 0; i < data.nodes.Count; i++)
            {
                var nodeData = data.nodes[i];
                var node = nodes[i];

                if (nodeData.incoming != null)
                {
                    foreach (var sourceId in nodeData.incoming)
                        node.AddIncoming(sourceId);
                }

                if (nodeData.outgoing != null)
                {
                    foreach (var targetId in nodeData.outgoing)
                        node.AddOutgoing(targetId);
                }
            }

            // 找到 Boss 节点名称
            var bossNode = nodes.FirstOrDefault(n => n.nodeType == RoguelikeNodeType.Boss);
            string bossNodeName = bossNode?.blueprintName ?? string.Empty;

            return new RoguelikeMap(string.Empty, bossNodeName, nodes, new HashSet<string>(),
                data.maxReachableDistance, data.visionRange);
        }

        /// <summary>
        /// 将运行时 RoguelikeMap 转换为 SerializableMapData。
        /// </summary>
        public static SerializableMapData FromRuntimeMap(RoguelikeMap map, int maxReachableDistance = 0, int visionRange = 0)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            var data = new SerializableMapData
            {
                version = 1,
                maxReachableDistance = maxReachableDistance,
                visionRange = visionRange,
                nodes = new List<SerializableNodeData>(map.nodes.Count)
            };

            foreach (var node in map.nodes)
            {
                var nodeData = new SerializableNodeData
                {
                    nodeId = node.nodeId,
                    type = node.nodeType.ToString(),
                    position = SerializableVector2.FromVector2(node.position),
                    blueprintName = node.blueprintName,
                    incoming = node.incoming.ToArray(),
                    outgoing = node.outgoing.ToArray(),
                    eventId = string.IsNullOrEmpty(node.eventId) ? null : node.eventId,
                    encounterConfigPath = string.IsNullOrEmpty(node.encounterConfigPath) ? null : node.encounterConfigPath,
                    goldMin = node.treasureConfig?.goldMin,
                    goldMax = node.treasureConfig?.goldMax,
                    treasureEquipment = node.treasureConfig?.equipmentEntries?.Select(e => new SerializableNodeData.SerializableWeightedEquipmentData
                    {
                        equipmentId = e.equipmentId,
                        weight = e.weight
                    }).ToList() ?? new List<SerializableNodeData.SerializableWeightedEquipmentData>(),
                    storeGoods = node.storeConfig?.goods?.Select(g => new SerializableNodeData.SerializableStoreGoodData
                    {
                        equipmentId = g.equipmentId,
                        price = g.price
                    }).ToList() ?? new List<SerializableNodeData.SerializableStoreGoodData>()
                };

                data.nodes.Add(nodeData);
            }

            return data;
        }

        /// <summary>
        /// 将 Vector2Int 格式化为 "x,y" 字符串。
        /// </summary>
        [Obsolete("Legacy helper for old Vector2Int-based node IDs. Will be removed.")]
        private static string FormatNodeId(Vector2Int point)
        {
            return $"{point.x},{point.y}";
        }

        /// <summary>
        /// 将 "x,y" 字符串解析为 Vector2Int。
        /// </summary>
        [Obsolete("Legacy helper for old Vector2Int-based node IDs. Will be removed.")]
        private static Vector2Int ParseNodeId(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                throw new ArgumentException("nodeId cannot be empty", nameof(nodeId));

            var parts = nodeId.Split(',');
            if (parts.Length != 2)
                throw new FormatException($"Invalid nodeId format: '{nodeId}'. Expected 'x,y'.");

            if (!int.TryParse(parts[0].Trim(), out int x) || !int.TryParse(parts[1].Trim(), out int y))
                throw new FormatException($"Invalid nodeId values: '{nodeId}'. Expected integers.");

            return new Vector2Int(x, y);
        }
    }
}
